using System.Collections;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync;
using MegaCrit.Sts2.Core.Rewards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 二选一奖励联机同步补丁：把 LinkedRewardSet 的子奖励选择编码成合法同步下标，避免子奖励直接同步成 -1/255。
/// </summary>
[HarmonyPatch]
public static class LinkedRewardSetSyncPatch
{
    /// <summary>
    /// 子奖励同步编码起点。原版 rewardIndex 只写 8 位，因此保留 0-127 给原版外层奖励下标。
    /// </summary>
    private const int LinkedChildRewardIndexBase = 128;

    /// <summary>
    /// 子奖励下标占用 4 位，最多支持每组 16 个子奖励。
    /// </summary>
    private const int ChildIndexMask = 0x0F;

    /// <summary>
    /// 外层奖励下标最多支持 0-7，编码范围 128-255。
    /// </summary>
    private const int MaxParentIndex = 7;

    /// <summary>
    /// 每个 LinkedRewardSet 最多支持 16 个子奖励。
    /// </summary>
    private const int MaxChildIndex = 15;

    /// <summary>
    /// 原版同步器中的消息缓冲器字段，用于读取当前房间位置。
    /// </summary>
    private static readonly FieldInfo? MessageBufferField = AccessTools.Field(typeof(RewardsSetSynchronizer), "_messageBuffer");

    /// <summary>
    /// 原版同步器中的网络服务字段，用于发送奖励选择消息。
    /// </summary>
    private static readonly FieldInfo? NetServiceField = AccessTools.Field(typeof(RewardsSetSynchronizer), "_netService");

    /// <summary>
    /// 原版同步器获取玩家奖励状态的私有方法。
    /// </summary>
    private static readonly MethodInfo? GetRewardStateForPlayerMethod = AccessTools.Method(typeof(RewardsSetSynchronizer), "GetRewardStateForPlayer");

    /// <summary>
    /// 原版同步器完成奖励组检查的私有方法。
    /// </summary>
    private static readonly MethodInfo? CompleteRewardsSetIfNecessaryMethod = AccessTools.Method(typeof(RewardsSetSynchronizer), "CompleteRewardsSetIfNecessary");

    /// <summary>
    /// Reward.SuccessfullySelected 的编译器生成字段，用于把外层 LinkedRewardSet 标记成已领取。
    /// </summary>
    private static readonly FieldInfo? RewardSuccessfullySelectedField = AccessTools.Field(typeof(Reward), "<SuccessfullySelected>k__BackingField");

    /// <summary>
    /// 拦截本地子奖励领取，避免原版把子奖励在外层奖励列表中查成 -1。
    /// </summary>
    [HarmonyPatch(typeof(RewardsSetSynchronizer), nameof(RewardsSetSynchronizer.SelectLocalReward))]
    [HarmonyPrefix]
    public static bool SelectLocalRewardPrefix(RewardsSetSynchronizer __instance, Reward reward, ref Task<bool> __result)
    {
        if (!TryGetLinkedChildReward(reward, out LinkedRewardSet parentRewardSet, out int childIndex))
        {
            return true;
        }

        __result = SelectLocalLinkedChildReward(__instance, reward, parentRewardSet, childIndex);
        return false;
    }

    /// <summary>
    /// 拦截远端奖励下标选择，识别我们编码过的 LinkedRewardSet 子奖励下标。
    /// </summary>
    [HarmonyPatch(typeof(RewardsSetSynchronizer), "SelectRewardForPlayer", typeof(Player), typeof(int))]
    [HarmonyPrefix]
    public static bool SelectRewardForPlayerPrefix(RewardsSetSynchronizer __instance, Player player, int rewardIndex, ref Task __result)
    {
        if (!TryDecodeLinkedChildRewardIndex(rewardIndex, out int parentIndex, out int childIndex))
        {
            return true;
        }

        __result = SelectRemoteLinkedChildReward(__instance, player, parentIndex, childIndex);
        return false;
    }

    /// <summary>
    /// 本地领取 LinkedRewardSet 子奖励，并向远端发送可解码的合法奖励下标。
    /// </summary>
    private static async Task<bool> SelectLocalLinkedChildReward(RewardsSetSynchronizer synchronizer, Reward childReward, LinkedRewardSet parentRewardSet, int childIndex)
    {
        if (!TryGetTopRewardsSetState(synchronizer, childReward.Player, out object setState, out RewardsSet rewardsSet))
        {
            MainFile.Logger.Info($"【二选一奖励联机】本地同步失败：找不到当前奖励组，玩家={childReward.Player?.NetId}，处理=本次点击无效。");
            return false;
        }

        int parentIndex = rewardsSet.Rewards.IndexOf(parentRewardSet);
        if (!TryEncodeLinkedChildRewardIndex(parentIndex, childIndex, out int encodedRewardIndex))
        {
            MainFile.Logger.Info($"【二选一奖励联机】本地同步失败：外层下标或子下标超出编码范围，玩家={childReward.Player?.NetId}，外层下标={parentIndex}，子下标={childIndex}，处理=本次点击无效。");
            return false;
        }

        if (!TrySendRewardSelectedMessage(synchronizer, rewardsSet, encodedRewardIndex))
        {
            MainFile.Logger.Info($"【二选一奖励联机】本地同步失败：无法发送奖励选择消息，玩家={childReward.Player?.NetId}，奖励组={rewardsSet.Id}，编码={encodedRewardIndex}，处理=本次点击无效。");
            return false;
        }

        MainFile.Logger.Info($"【二选一奖励联机】发送子奖励选择：玩家={childReward.Player.NetId}，奖励组={rewardsSet.Id}，外层下标={parentIndex}，子下标={childIndex}，编码={encodedRewardIndex}。");
        return await SelectLinkedChildReward(synchronizer, setState, parentRewardSet, childReward, "本地");
    }

    /// <summary>
    /// 远端解码 LinkedRewardSet 子奖励，并领取同一个子奖励。
    /// </summary>
    private static async Task SelectRemoteLinkedChildReward(RewardsSetSynchronizer synchronizer, Player player, int parentIndex, int childIndex)
    {
        if (!TryGetTopRewardsSetState(synchronizer, player, out object setState, out RewardsSet rewardsSet))
        {
            MainFile.Logger.Info($"【二选一奖励联机】远端同步失败：找不到当前奖励组，玩家={player.NetId}，外层下标={parentIndex}，子下标={childIndex}。");
            return;
        }

        if (parentIndex < 0 || parentIndex >= rewardsSet.Rewards.Count)
        {
            MainFile.Logger.Info($"【二选一奖励联机】远端同步失败：外层奖励下标越界，玩家={player.NetId}，奖励组={rewardsSet.Id}，外层下标={parentIndex}，奖励数={rewardsSet.Rewards.Count}。");
            return;
        }

        if (rewardsSet.Rewards[parentIndex] is not LinkedRewardSet parentRewardSet)
        {
            MainFile.Logger.Info($"【二选一奖励联机】远端同步失败：外层奖励不是二选一奖励，玩家={player.NetId}，奖励组={rewardsSet.Id}，外层下标={parentIndex}，实际类型={rewardsSet.Rewards[parentIndex].GetType().Name}。");
            return;
        }

        if (parentRewardSet.SuccessfullySelected)
        {
            MainFile.Logger.Info($"【二选一奖励联机】远端收到重复子奖励选择，二选一奖励已完成，玩家={player.NetId}，奖励组={rewardsSet.Id}，外层下标={parentIndex}。");
            return;
        }

        IReadOnlyList<Reward> childRewards = parentRewardSet.Rewards;
        if (childIndex < 0 || childIndex >= childRewards.Count)
        {
            MainFile.Logger.Info($"【二选一奖励联机】远端同步失败：子奖励下标越界，玩家={player.NetId}，奖励组={rewardsSet.Id}，外层下标={parentIndex}，子下标={childIndex}，子奖励数={childRewards.Count}。");
            return;
        }

        MainFile.Logger.Info($"【二选一奖励联机】远端领取子奖励：玩家={player.NetId}，奖励组={rewardsSet.Id}，外层下标={parentIndex}，子下标={childIndex}。");
        await SelectLinkedChildReward(synchronizer, setState, parentRewardSet, childRewards[childIndex], "远端");
    }

    /// <summary>
    /// 实际领取子奖励、跳过同组剩余奖励，并把外层二选一奖励标记为已完成。
    /// </summary>
    private static async Task<bool> SelectLinkedChildReward(RewardsSetSynchronizer synchronizer, object setState, LinkedRewardSet parentRewardSet, Reward childReward, string source)
    {
        if (parentRewardSet.SuccessfullySelected)
        {
            MainFile.Logger.Info($"【二选一奖励联机】{source}领取跳过：二选一奖励已经完成，玩家={childReward.Player.NetId}。");
            return true;
        }

        bool selected = await childReward.SelectUnsynchronized();
        if (!selected)
        {
            MainFile.Logger.Info($"【二选一奖励联机】{source}领取失败：子奖励返回未领取，玩家={childReward.Player.NetId}，子奖励={FormatReward(childReward)}。");
            return false;
        }

        parentRewardSet.OnSkipped();
        MarkRewardSuccessfullySelected(parentRewardSet);
        CompleteRewardsSetIfNecessaryMethod?.Invoke(synchronizer, [setState]);
        MainFile.Logger.Info($"【二选一奖励联机】{source}领取完成：玩家={childReward.Player.NetId}，子奖励={FormatReward(childReward)}，外层二选一已标记完成。");
        return true;
    }

    /// <summary>
    /// 判断奖励是否是 LinkedRewardSet 的子奖励，并取出它在子奖励列表中的位置。
    /// </summary>
    private static bool TryGetLinkedChildReward(Reward reward, out LinkedRewardSet parentRewardSet, out int childIndex)
    {
        parentRewardSet = reward.ParentRewardSet!;
        childIndex = -1;
        if (parentRewardSet == null)
        {
            return false;
        }

        IReadOnlyList<Reward> childRewards = parentRewardSet.Rewards;
        for (int i = 0; i < childRewards.Count; i++)
        {
            if (ReferenceEquals(childRewards[i], reward))
            {
                childIndex = i;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 读取指定玩家当前栈顶奖励组状态。
    /// </summary>
    private static bool TryGetTopRewardsSetState(RewardsSetSynchronizer synchronizer, Player player, out object setState, out RewardsSet rewardsSet)
    {
        setState = null!;
        rewardsSet = null!;

        object? rewardState = GetRewardStateForPlayerMethod?.Invoke(synchronizer, [player]);
        if (rewardState == null)
        {
            return false;
        }

        FieldInfo? rewardsStackField = AccessTools.Field(rewardState.GetType(), "rewardsStack");
        if (rewardsStackField?.GetValue(rewardState) is not IList rewardsStack || rewardsStack.Count == 0)
        {
            return false;
        }

        object? currentSetState = rewardsStack[rewardsStack.Count - 1];
        if (currentSetState == null)
        {
            return false;
        }

        FieldInfo? rewardsSetField = AccessTools.Field(currentSetState.GetType(), "set");
        RewardsSet? currentRewardsSet = rewardsSetField?.GetValue(currentSetState) as RewardsSet;
        if (currentRewardsSet == null)
        {
            return false;
        }

        setState = currentSetState;
        rewardsSet = currentRewardsSet;
        return true;
    }

    /// <summary>
    /// 发送经过编码的奖励选择消息。
    /// </summary>
    private static bool TrySendRewardSelectedMessage(RewardsSetSynchronizer synchronizer, RewardsSet rewardsSet, int encodedRewardIndex)
    {
        RunLocationTargetedMessageBuffer? messageBuffer = MessageBufferField?.GetValue(synchronizer) as RunLocationTargetedMessageBuffer;
        INetGameService? netService = NetServiceField?.GetValue(synchronizer) as INetGameService;
        if (messageBuffer == null || netService == null)
        {
            return false;
        }

        RewardSelectedMessage message = new()
        {
            location = messageBuffer.CurrentLocation,
            setId = rewardsSet.Id,
            rewardIndex = encodedRewardIndex
        };
        netService.SendMessage(message);
        return true;
    }

    /// <summary>
    /// 把外层奖励下标和子奖励下标编码进 8 位 rewardIndex。
    /// </summary>
    private static bool TryEncodeLinkedChildRewardIndex(int parentIndex, int childIndex, out int encodedRewardIndex)
    {
        encodedRewardIndex = -1;
        if (parentIndex < 0 || parentIndex > MaxParentIndex || childIndex < 0 || childIndex > MaxChildIndex)
        {
            return false;
        }

        encodedRewardIndex = LinkedChildRewardIndexBase + (parentIndex << 4) + childIndex;
        return true;
    }

    /// <summary>
    /// 从 8 位 rewardIndex 中解出外层奖励下标和子奖励下标。
    /// </summary>
    private static bool TryDecodeLinkedChildRewardIndex(int rewardIndex, out int parentIndex, out int childIndex)
    {
        parentIndex = -1;
        childIndex = -1;
        if (rewardIndex < LinkedChildRewardIndexBase)
        {
            return false;
        }

        int payload = rewardIndex - LinkedChildRewardIndexBase;
        parentIndex = payload >> 4;
        childIndex = payload & ChildIndexMask;
        return true;
    }

    /// <summary>
    /// 将外层 LinkedRewardSet 标记为已成功领取，让原版奖励组完成判断可以正常通过。
    /// </summary>
    private static void MarkRewardSuccessfullySelected(Reward reward)
    {
        RewardSuccessfullySelectedField?.SetValue(reward, true);
    }

    /// <summary>
    /// 格式化奖励类型，方便日志定位。
    /// </summary>
    private static string FormatReward(Reward reward)
    {
        return reward.GetType().Name;
    }
}
