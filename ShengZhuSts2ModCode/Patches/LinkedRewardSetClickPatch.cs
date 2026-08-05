using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 修复互斥奖励二选一的点击信号接线。
/// 原版把带按钮参数的 RewardClaimed 信号接到了无参数方法上，会导致点击后报参数数量错误。
/// </summary>
[HarmonyPatch(typeof(NLinkedRewardSet), "Reload")]
public static class LinkedRewardSetClickPatch
{
    /// <summary>
    /// 二选一父控件中保存奖励界面的私有字段。
    /// </summary>
    private static readonly FieldInfo? RewardsScreenField = typeof(NLinkedRewardSet).GetField("_rewardsScreen", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 二选一父控件中保存子奖励按钮容器的私有字段。
    /// </summary>
    private static readonly FieldInfo? RewardContainerField = typeof(NLinkedRewardSet).GetField("_rewardContainer", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 二选一父控件中保存链条图标容器的私有字段。
    /// </summary>
    private static readonly FieldInfo? ChainsContainerField = typeof(NLinkedRewardSet).GetField("_chainsContainer", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 防止同一组二选一在点击、回调或重复信号中被处理多次。
    /// </summary>
    private static readonly HashSet<NLinkedRewardSet> CompletedLinkedRewards = [];

    /// <summary>
    /// 已接入生命周期清理信号的二选一奖励节点。
    /// </summary>
    private static readonly HashSet<NLinkedRewardSet> TrackedLinkedRewards = [];

    /// <summary>
    /// 原版链条图片路径。
    /// </summary>
    private static readonly string ChainImagePath = ImageHelper.GetImagePath("/ui/reward_screen/reward_chain.png");

    /// <summary>
    /// 前置补丁：重建二选一按钮，并跳过原版错误接线。
    /// </summary>
    public static bool Prefix(NLinkedRewardSet __instance)
    {
        if (!__instance.IsNodeReady())
        {
            return false;
        }

        TrackLinkedRewardLifecycle(__instance);

        if (!TryGetUiFields(__instance, out NRewardsScreen rewardsScreen, out Control rewardContainer, out Control chainsContainer))
        {
            MainFile.Logger.Info("【二选一奖励】无法读取奖励界面字段，跳过重建二选一按钮。");
            return false;
        }

        ClearContainer(rewardContainer);
        ClearContainer(chainsContainer);

        IReadOnlyList<Reward> rewards = __instance.LinkedRewardSet.Rewards;
        for (int i = 0; i < rewards.Count; i++)
        {
            NRewardButton rewardButton = NRewardButton.Create(rewards[i], rewardsScreen);
            rewardButton.CustomMinimumSize -= Vector2.Right * 20f;
            rewardContainer.AddChild(rewardButton);
            rewardButton.Connect(NRewardButton.SignalName.RewardClaimed, Callable.From<NRewardButton>(_ => OnChildRewardClaimed(__instance)));

            if (i < rewards.Count - 1)
            {
                AddChainIcon(chainsContainer, rewardButton, i);
            }
        }

        MainFile.Logger.Info($"【二选一奖励】已重建互斥奖励按钮：选项数={rewards.Count}");
        return false;
    }

    /// <summary>
    /// 处理子奖励领取成功后的父控件收尾。
    /// </summary>
    private static void OnChildRewardClaimed(NLinkedRewardSet linkedRewardSet)
    {
        if (!TryMarkLinkedRewardCompleted(linkedRewardSet))
        {
            return;
        }

        if (!TryGetUiFields(linkedRewardSet, out NRewardsScreen rewardsScreen, out _, out _))
        {
            MainFile.Logger.Info("【二选一奖励】领取后无法读取奖励界面，无法主动移除二选一控件。");
            return;
        }

        rewardsScreen.RewardCollectedFrom(linkedRewardSet);
        // 子奖励的跳过逻辑由联机同步补丁统一处理，这里只负责收起整组 UI。
        linkedRewardSet.QueueFree();
        MainFile.Logger.Info("【二选一奖励】已完成一次互斥领取，并移除整组二选一奖励。");

        // 领取奖励后检查十二符咒觉醒：从二选一的子奖励中找到已领取的遗物的 Owner
        TryCheckTalismanAwakening(linkedRewardSet);
    }

    /// <summary>
    /// 为二选一奖励节点接入退出场景树清理，避免静态集合长期持有已释放节点。
    /// </summary>
    private static void TrackLinkedRewardLifecycle(NLinkedRewardSet linkedRewardSet)
    {
        if (!TrackedLinkedRewards.Add(linkedRewardSet))
        {
            return;
        }

        linkedRewardSet.Connect(Node.SignalName.TreeExiting, Callable.From(() => ReleaseLinkedRewardState(linkedRewardSet, "节点退出场景树")));
        MainFile.Logger.Info($"【二选一奖励】已接入生命周期清理：当前跟踪数={TrackedLinkedRewards.Count}");
    }

    /// <summary>
    /// 标记二选一奖励已完成，防止重复领取回调再次处理同一组奖励。
    /// </summary>
    private static bool TryMarkLinkedRewardCompleted(NLinkedRewardSet linkedRewardSet)
    {
        if (!CompletedLinkedRewards.Add(linkedRewardSet))
        {
            MainFile.Logger.Info("【二选一奖励】本组奖励已经完成，忽略重复领取回调。");
            return false;
        }

        MainFile.Logger.Info($"【二选一奖励】标记互斥奖励已完成：当前完成缓存数={CompletedLinkedRewards.Count}");
        return true;
    }

    /// <summary>
    /// 释放二选一奖励节点的静态状态，避免旧 UI 节点引用残留到后续奖励页。
    /// </summary>
    private static void ReleaseLinkedRewardState(NLinkedRewardSet linkedRewardSet, string reason)
    {
        bool removedCompleted = CompletedLinkedRewards.Remove(linkedRewardSet);
        bool removedTracked = TrackedLinkedRewards.Remove(linkedRewardSet);
        if (removedCompleted || removedTracked)
        {
            MainFile.Logger.Info($"【二选一奖励】已清理互斥奖励状态：原因={reason}，完成缓存数={CompletedLinkedRewards.Count}，跟踪数={TrackedLinkedRewards.Count}");
        }
    }

    /// <summary>
    /// 从二选一奖励中提取玩家信息并触发觉醒检查。
    /// </summary>
    private static void TryCheckTalismanAwakening(NLinkedRewardSet linkedRewardSet)
    {
        try
        {
            foreach (Reward reward in linkedRewardSet.LinkedRewardSet.Rewards)
            {
                if (reward is not RelicReward relicReward) continue;

                FieldInfo? relicField = typeof(RelicReward).GetField("_relic", BindingFlags.Instance | BindingFlags.NonPublic);
                var relic = relicField?.GetValue(relicReward) as MegaCrit.Sts2.Core.Models.RelicModel;
                if (relic?.Owner != null)
                {
                    TalismanAwakeningHelper.CheckAndTriggerAwakening(relic.Owner);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【二选一奖励】觉醒检查异常：{ex.Message}");
        }
    }

    /// <summary>
    /// 读取二选一控件需要的私有 UI 字段。
    /// </summary>
    private static bool TryGetUiFields(NLinkedRewardSet linkedRewardSet, out NRewardsScreen rewardsScreen, out Control rewardContainer, out Control chainsContainer)
    {
        NRewardsScreen? currentRewardsScreen = RewardsScreenField?.GetValue(linkedRewardSet) as NRewardsScreen;
        Control? currentRewardContainer = RewardContainerField?.GetValue(linkedRewardSet) as Control;
        Control? currentChainsContainer = ChainsContainerField?.GetValue(linkedRewardSet) as Control;

        if (currentRewardsScreen == null || currentRewardContainer == null || currentChainsContainer == null)
        {
            rewardsScreen = null!;
            rewardContainer = null!;
            chainsContainer = null!;
            return false;
        }

        rewardsScreen = currentRewardsScreen;
        rewardContainer = currentRewardContainer;
        chainsContainer = currentChainsContainer;
        return true;
    }

    /// <summary>
    /// 清空指定容器，避免 Reload 多次调用时重复生成按钮。
    /// </summary>
    private static void ClearContainer(Control container)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>
    /// 添加两个互斥奖励中间的链条图标。
    /// </summary>
    private static void AddChainIcon(Control chainsContainer, NRewardButton rewardButton, int index)
    {
        TextureRect chainIcon = new()
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = PreloadManager.Cache.GetCompressedTexture2D(ChainImagePath),
            Size = Vector2.One * 50f
        };

        chainsContainer.AddChild(chainIcon);
        chainIcon.GlobalPosition = chainsContainer.GlobalPosition + Vector2.Down * index * (3f + rewardButton.CustomMinimumSize.Y);
    }
}
