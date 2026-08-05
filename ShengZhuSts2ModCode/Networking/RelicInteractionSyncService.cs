using System.Threading;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

/// <summary>
/// 通用遗物联机同步服务。
/// 联机战斗内的高风险右键和半自动入口优先提交官方动作队列；单机和低风险交互保留旧同步流程。
/// </summary>
internal static class RelicInteractionSyncService
{
    /// <summary>
    /// 当前已注册的跑图位置消息缓冲器。
    /// </summary>
    private static RunLocationTargetedMessageBuffer? _registeredBuffer;

    /// <summary>
    /// 远端遗物交互串行锁，避免多个同步消息同时改手牌、Power 或弹选择界面。
    /// </summary>
    private static readonly SemaphoreSlim _remoteInteractionLock = new(1, 1);

    /// <summary>
    /// 休息处交互取消时使用的稳定下标标记；正常牌组下标一定不会小于 0。
    /// </summary>
    private const int RestSiteInteractionCanceledIndex = -1;

    /// <summary>
    /// 等待远端休息处二段选择的最长时间，避免远端断线或异常时后台任务永久悬挂。
    /// </summary>
    private const int RemoteRestSiteInteractionTimeoutMs = 20 * 60 * 1000;

    /// <summary>
    /// 远端休息处交互完成后短暂保留等待键，用来吞掉同一次点击产生的即时重复消息。
    /// </summary>
    private const int CompletedRestSiteInteractionKeepAliveMs = 2000;

    /// <summary>
    /// 正在等待远端完成的休息处遗物交互。
    /// </summary>
    private static readonly Dictionary<RestSiteInteractionKey, TaskCompletionSource<bool>> _pendingRestSiteInteractions = [];

    /// <summary>
    /// 休息处遗物交互等待锁。
    /// </summary>
    private static readonly object _pendingRestSiteInteractionLock = new();

    /// <summary>
    /// 已提交到官方动作队列、但本端尚未执行结束的遗物交互。
    /// 防止联机右键连续点击时把同一个状态修改动作重复入队。
    /// </summary>
    private static readonly HashSet<QueuedInteractionKey> _queuedInteractionKeys = [];

    /// <summary>
    /// 遗物交互入队锁。
    /// </summary>
    private static readonly object _queuedInteractionLock = new();

    /// <summary>
    /// 请求设置符咒激发状态。
    /// </summary>
    /// <param name="relic">目标符咒。</param>
    /// <param name="activate">是否激发。</param>
    /// <returns>异步任务。</returns>
    public static async Task RequestSetActivated(RelicModel relic, bool activate)
    {
        TalismanRelicType relicType = TalismanRelicTypeHelper.FromRelic(relic);
        if (relicType == TalismanRelicType.None || relic.Owner == null)
        {
            MainFile.Logger.Info($"【遗物联机】设置激发失败：遗物类型或持有者无效，遗物={relic.Id.Entry}。");
            return;
        }

        if (!CanLocalEndpointInitiate(relic.Owner, "设置符咒激发"))
        {
            return;
        }

        if (relic is ITalismanInteractionState state && state.CooldownTurnsRemaining > 0)
        {
            MainFile.Logger.Info($"【遗物联机】设置激发已跳过：目标符咒仍在冷却，本端不会广播无效激发，遗物={relic.Id.Entry}，剩余冷却={state.CooldownTurnsRemaining}，请求激发={activate}。");
            return;
        }

        if (TrySubmitQueuedInteraction(relic.Owner, RelicInteractionType.SetActivation, relicType, activate, 0))
        {
            return;
        }

        await BroadcastAndExecute(
            relic.Owner,
            RelicInteractionType.SetActivation,
            relicType,
            activate,
            player =>
            {
                RelicModel? targetRelic = FindRelic(player, relicType);
                ExecuteSetActivated(targetRelic, activate);
                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// 请求触发鼠符咒变化手牌。
    /// </summary>
    /// <param name="relic">鼠符咒。</param>
    /// <param name="isAutoTrigger">是否为半自动触发。</param>
    /// <returns>异步任务。</returns>
    public static async Task RequestRatUse(RatTalisman relic, bool isAutoTrigger)
    {
        if (relic.Owner == null)
        {
            MainFile.Logger.Info("【遗物联机】鼠符咒触发失败：遗物持有者为空。");
            return;
        }

        if (!CanLocalEndpointInitiate(relic.Owner, "触发鼠符咒"))
        {
            return;
        }

        if (TrySubmitQueuedInteraction(relic.Owner, RelicInteractionType.RatTransform, TalismanRelicType.Rat, isAutoTrigger, 0))
        {
            return;
        }

        await BroadcastAndExecute(
            relic.Owner,
            RelicInteractionType.RatTransform,
            TalismanRelicType.Rat,
            isAutoTrigger,
            player =>
            {
                RatTalisman? rat = player.GetRelic<RatTalisman>();
                return rat?.RunSyncedReplaceCurseOrStatus(new BlockingPlayerChoiceContext(), isAutoTrigger) ?? Task.CompletedTask;
            });
    }

    /// <summary>
    /// 请求切换鼠符咒圣主形态。
    /// </summary>
    /// <param name="relic">鼠符咒。</param>
    /// <param name="targetHumanForm">目标是否为人形态。</param>
    /// <returns>异步任务。</returns>
    public static async Task RequestRatFormSwitch(RatTalisman relic, bool targetHumanForm)
    {
        if (relic.Owner == null)
        {
            MainFile.Logger.Info("【遗物联机】鼠符咒形态切换失败：遗物持有者为空。");
            return;
        }

        if (!CanLocalEndpointInitiate(relic.Owner, "切换圣主形态"))
        {
            return;
        }

        if (TrySubmitQueuedInteraction(relic.Owner, RelicInteractionType.RatFormSwitch, TalismanRelicType.Rat, targetHumanForm, 0))
        {
            return;
        }

        await BroadcastAndExecute(
            relic.Owner,
            RelicInteractionType.RatFormSwitch,
            TalismanRelicType.Rat,
            targetHumanForm,
            player => ShengZhuFormService.RunSyncedSwitch(player, targetHumanForm, new BlockingPlayerChoiceContext()));
    }

    /// <summary>
    /// 请求触发马符咒移除负面效果。
    /// </summary>
    /// <param name="relic">马符咒。</param>
    /// <param name="isAutoTrigger">是否为半自动触发。</param>
    /// <returns>异步任务。</returns>
    public static async Task RequestHorseUse(HorseTalisman relic, bool isAutoTrigger)
    {
        if (relic.Owner == null)
        {
            MainFile.Logger.Info("【遗物联机】马符咒触发失败：遗物持有者为空。");
            return;
        }

        if (!CanLocalEndpointInitiate(relic.Owner, "触发马符咒"))
        {
            return;
        }

        if (TrySubmitQueuedInteraction(relic.Owner, RelicInteractionType.HorseRemoveDebuff, TalismanRelicType.Horse, isAutoTrigger, 0))
        {
            return;
        }

        await BroadcastAndExecute(
            relic.Owner,
            RelicInteractionType.HorseRemoveDebuff,
            TalismanRelicType.Horse,
            isAutoTrigger,
            player =>
            {
                HorseTalisman? horse = player.GetRelic<HorseTalisman>();
                return horse?.RunSyncedRemoveDebuff(new BlockingPlayerChoiceContext(), isAutoTrigger) ?? Task.CompletedTask;
            });
    }

    /// <summary>
    /// 请求触发猴符咒战斗内七十二变。
    /// </summary>
    /// <param name="relic">猴符咒。</param>
    /// <returns>异步任务。</returns>
    public static async Task RequestMonkeyCombatTransform(MonkeyTalisman relic)
    {
        if (relic.Owner == null)
        {
            MainFile.Logger.Info("【遗物联机】猴符咒触发失败：遗物持有者为空。");
            return;
        }

        if (!CanLocalEndpointInitiate(relic.Owner, "触发猴符咒"))
        {
            return;
        }

        if (TrySubmitQueuedInteraction(relic.Owner, RelicInteractionType.MonkeyCombatTransform, TalismanRelicType.Monkey, false, 0))
        {
            return;
        }

        await BroadcastAndExecute(
            relic.Owner,
            RelicInteractionType.MonkeyCombatTransform,
            TalismanRelicType.Monkey,
            false,
            player =>
            {
                MonkeyTalisman? monkey = player.GetRelic<MonkeyTalisman>();
                return monkey?.RunSyncedCombatTransform(new BlockingPlayerChoiceContext()) ?? Task.CompletedTask;
            });
    }

    /// <summary>
    /// 请求同步猴符咒休息处七十二变。
    /// </summary>
    /// <param name="relic">猴符咒。</param>
    /// <param name="payload">本次七十二变的稳定选择结果。</param>
    /// <returns>本地执行成功时返回 true。</returns>
    public static async Task<bool> RequestMonkeyRestTransform(MonkeyTalisman relic, MonkeyRestTransformPayload payload)
    {
        if (relic.Owner == null)
        {
            MainFile.Logger.Info("【遗物联机】猴符咒休息处七十二变失败：遗物持有者为空。");
            return false;
        }

        if (!CanLocalEndpointInitiate(relic.Owner, "猴符咒休息处七十二变"))
        {
            return false;
        }

        bool result = false;
        await BroadcastAndExecute(
            relic.Owner,
            RelicInteractionType.MonkeyRestTransform,
            TalismanRelicType.Monkey,
            payload.TargetWasUpgraded,
            async player =>
            {
                MonkeyTalisman? monkey = player.GetRelic<MonkeyTalisman>();
                result = monkey != null && await monkey.RunSyncedRestSiteTransform(payload);
            },
            payload.TargetDeckIndex,
            0,
            payload.TargetWasUpgraded,
            payload.TargetCardId,
            payload.ReplacementCardId);

        return result;
    }

    /// <summary>
    /// 请求同步潘库宝盒休息处附魔。
    /// </summary>
    /// <param name="box">潘库宝盒。</param>
    /// <param name="payload">本次附魔的稳定选择结果。</param>
    /// <returns>本地执行成功时返回 true。</returns>
    public static async Task<bool> RequestPanKuRestEnchant(PanKuBox box, PanKuRestEnchantPayload payload)
    {
        if (box.Owner == null)
        {
            MainFile.Logger.Info("【遗物联机】潘库宝盒休息处附魔失败：遗物持有者为空。");
            return false;
        }

        if (!CanLocalEndpointInitiate(box.Owner, "潘库宝盒休息处附魔"))
        {
            return false;
        }

        bool result = false;
        await BroadcastAndExecute(
            box.Owner,
            RelicInteractionType.PanKuRestEnchant,
            TalismanRelicType.None,
            payload.TargetWasUpgraded,
            player =>
            {
                PanKuBox? localBox = player.GetRelic<PanKuBox>();
                result = localBox != null && localBox.RunSyncedRestSiteEnchant(payload);
                return Task.CompletedTask;
            },
            payload.TargetDeckIndex,
            (int)payload.Kind,
            payload.TargetWasUpgraded,
            payload.TargetCardId,
            string.Empty);

        return result;
    }

    /// <summary>
    /// 通知其它客户端：本地玩家取消了需要二段选择的休息处遗物交互。
    /// </summary>
    /// <param name="owner">触发玩家。</param>
    /// <param name="interactionType">被取消的交互类型。</param>
    /// <param name="targetRelicType">目标符咒类型。</param>
    /// <param name="reason">取消原因。</param>
    /// <returns>异步任务。</returns>
    internal static async Task NotifyRestSiteInteractionCanceled(Player owner, RelicInteractionType interactionType, TalismanRelicType targetRelicType, string reason)
    {
        if (!IsRestSiteRelicInteraction(interactionType))
        {
            return;
        }

        if (!CanLocalEndpointInitiate(owner, reason))
        {
            return;
        }

        MainFile.Logger.Info($"【遗物联机】广播休息处遗物交互取消：玩家={owner.NetId}，类型={interactionType}，原因={reason}。");
        await BroadcastAndExecute(
            owner,
            interactionType,
            targetRelicType,
            false,
            _ => Task.CompletedTask,
            RestSiteInteractionCanceledIndex);
    }

    /// <summary>
    /// 远端休息处选项等待本地玩家真正完成二段选择后再返回，避免提前清空休息处选项列表。
    /// </summary>
    /// <param name="owner">正在操作休息处的远端玩家。</param>
    /// <param name="interactionType">等待的休息处遗物交互类型。</param>
    /// <param name="sourceName">日志来源名。</param>
    /// <returns>远端交互成功时返回 true；取消、重复或超时时返回 false。</returns>
    internal static async Task<bool> WaitForRemoteRestSiteInteraction(Player owner, RelicInteractionType interactionType, string sourceName)
    {
        if (!IsRestSiteRelicInteraction(interactionType))
        {
            return false;
        }

        RestSiteInteractionKey key = new(owner.NetId, interactionType);
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pendingRestSiteInteractionLock)
        {
            if (_pendingRestSiteInteractions.TryGetValue(key, out TaskCompletionSource<bool>? existingCompletion))
            {
                string state = existingCompletion.Task.IsCompleted ? "已完成" : "等待中";
                MainFile.Logger.Info($"【遗物联机】跳过重复的远端休息处等待：来源={sourceName}，玩家={owner.NetId}，类型={interactionType}，状态={state}。");
                return false;
            }

            _pendingRestSiteInteractions[key] = completion;
        }

        MainFile.Logger.Info($"【遗物联机】等待远端休息处遗物交互完成：来源={sourceName}，玩家={owner.NetId}，类型={interactionType}。");
        Task timeoutTask = Task.Delay(RemoteRestSiteInteractionTimeoutMs);
        Task finishedTask = await Task.WhenAny(completion.Task, timeoutTask);
        if (finishedTask != completion.Task)
        {
            RemovePendingRestSiteInteraction(key);
            MainFile.Logger.Info($"【遗物联机】等待远端休息处遗物交互超时：来源={sourceName}，玩家={owner.NetId}，类型={interactionType}，处理=不消耗休息处选项。");
            return false;
        }

        bool result = await completion.Task;
        MainFile.Logger.Info($"【遗物联机】远端休息处遗物交互完成：来源={sourceName}，玩家={owner.NetId}，类型={interactionType}，结果={result}。");
        return result;
    }

    /// <summary>
    /// 请求同步潘库宝盒奖励领取。
    /// </summary>
    /// <param name="box">潘库宝盒。</param>
    /// <param name="payload">本次奖励领取的最终选择。</param>
    /// <returns>本地执行成功时返回 true。</returns>
    public static async Task<bool> RequestPanKuRewardClaim(PanKuBox box, PanKuRewardClaimPayload payload)
    {
        if (box.Owner == null)
        {
            MainFile.Logger.Info("【遗物联机】潘库宝盒奖励领取失败：遗物持有者为空。");
            return false;
        }

        if (!CanLocalEndpointInitiate(box.Owner, "潘库宝盒奖励领取"))
        {
            return false;
        }

        bool result = false;
        await BroadcastAndExecute(
            box.Owner,
            RelicInteractionType.PanKuRewardClaim,
            TalismanRelicType.None,
            payload.TargetWasUpgraded,
            async player =>
            {
                PanKuBox? localBox = player.GetRelic<PanKuBox>();
                result = localBox != null && await localBox.RunSyncedRewardClaim(payload);
            },
            payload.TargetDeckIndex,
            (int)payload.Kind,
            payload.TargetWasUpgraded,
            ((int)payload.ClaimType).ToString(),
            payload.TargetCardId);

        return result;
    }

    /// <summary>
    /// 请求使用兔符咒刷新目标符咒冷却。
    /// </summary>
    /// <param name="rabbit">兔符咒。</param>
    /// <param name="target">被刷新的目标符咒。</param>
    /// <returns>异步任务。</returns>
    public static async Task<bool> RequestRabbitRefreshTarget(RabbitTalisman rabbit, RelicModel target)
    {
        if (rabbit.Owner == null)
        {
            MainFile.Logger.Info("【遗物联机】兔符咒刷新失败：遗物持有者为空。");
            return false;
        }

        TalismanRelicType targetType = TalismanRelicTypeHelper.FromRelic(target);
        if (targetType == TalismanRelicType.None)
        {
            MainFile.Logger.Info($"【遗物联机】兔符咒刷新失败：目标不是可同步符咒，目标={target.Id.Entry}。");
            return false;
        }

        if (!CanLocalEndpointInitiate(rabbit.Owner, "兔符咒刷新"))
        {
            return false;
        }

        if (TrySubmitQueuedInteraction(rabbit.Owner, RelicInteractionType.RabbitRefreshTarget, targetType, false, 0))
        {
            return true;
        }

        bool result = false;
        await BroadcastAndExecute(
            rabbit.Owner,
            RelicInteractionType.RabbitRefreshTarget,
            targetType,
            false,
            async player =>
            {
                RabbitTalisman? localRabbit = player.GetRelic<RabbitTalisman>();
                RelicModel? localTarget = FindRelic(player, targetType);
                result = localRabbit != null && localTarget != null && await localRabbit.RunSyncedRefreshTargetRelic(localTarget, true, new BlockingPlayerChoiceContext());
            });

        return result;
    }

    /// <summary>
    /// 请求设置符咒探测仪半自动模式。
    /// </summary>
    /// <param name="locator">符咒探测仪。</param>
    /// <param name="semiAutoMode">是否为半自动。</param>
    /// <returns>异步任务。</returns>
    public static async Task RequestTalismanLocatorMode(TalismanLocator locator, bool semiAutoMode)
    {
        if (locator.Owner == null)
        {
            MainFile.Logger.Info("【遗物联机】探测仪切挡失败：遗物持有者为空。");
            return;
        }

        if (!CanLocalEndpointInitiate(locator.Owner, "切换符咒探测仪"))
        {
            return;
        }

        if (TrySubmitQueuedInteraction(locator.Owner, RelicInteractionType.TalismanLocatorMode, TalismanRelicType.TalismanLocator, semiAutoMode, 0))
        {
            return;
        }

        await BroadcastAndExecute(
            locator.Owner,
            RelicInteractionType.TalismanLocatorMode,
            TalismanRelicType.TalismanLocator,
            semiAutoMode,
            player =>
            {
                TalismanLocator? localLocator = player.GetRelic<TalismanLocator>();
                return localLocator?.RunSyncedSetSemiAutoMode(semiAutoMode, true) ?? Task.CompletedTask;
            });
    }

    /// <summary>
    /// 请求设置黑影令牌烧牌挡位。
    /// </summary>
    /// <param name="token">黑影令牌。</param>
    /// <param name="exhaustMode">烧牌挡位。</param>
    /// <returns>异步任务。</returns>
    public static async Task RequestShadowKhanTokenMode(ShadowKhanToken token, int exhaustMode)
    {
        if (token.Owner == null)
        {
            MainFile.Logger.Info("【遗物联机】黑影令牌切挡失败：遗物持有者为空。");
            return;
        }

        if (!CanLocalEndpointInitiate(token.Owner, "切换黑影令牌"))
        {
            return;
        }

        int safeMode = Math.Clamp(exhaustMode, 0, 2);
        if (TrySubmitQueuedInteraction(token.Owner, RelicInteractionType.ShadowKhanTokenMode, TalismanRelicType.None, false, safeMode))
        {
            return;
        }

        await BroadcastAndExecute(
            token.Owner,
            RelicInteractionType.ShadowKhanTokenMode,
            TalismanRelicType.None,
            false,
            player =>
            {
                ShadowKhanToken? localToken = player.GetRelic<ShadowKhanToken>();
                localToken?.RunSyncedSetExhaustMode(safeMode);
                return Task.CompletedTask;
            },
            safeMode);
    }

    /// <summary>
    /// 请求同步岁月史书残卷改写现实。
    /// 战斗内选项优先走官方动作队列；非战斗房间状态走普通房间消息。
    /// </summary>
    /// <param name="fragment">岁月史书残卷。</param>
    /// <param name="option">本次改写选项。</param>
    /// <returns>本地执行成功时返回 true。</returns>
    public static async Task<bool> RequestHistoryBookRewrite(HistoryBookFragment fragment, HistoryBookRewriteOption option)
    {
        if (fragment.Owner == null)
        {
            MainFile.Logger.Info("【遗物联机】岁月史书残卷改写失败：遗物持有者为空。");
            return false;
        }

        if (!CanLocalEndpointInitiate(fragment.Owner, "岁月史书残卷改写"))
        {
            return false;
        }

        int optionValue = (int)option;
        if (TrySubmitQueuedInteraction(fragment.Owner, RelicInteractionType.HistoryBookRewrite, TalismanRelicType.None, false, optionValue))
        {
            return true;
        }

        bool result = false;
        await BroadcastAndExecute(
            fragment.Owner,
            RelicInteractionType.HistoryBookRewrite,
            TalismanRelicType.None,
            false,
            async player =>
            {
                HistoryBookFragment? localFragment = player.GetRelic<HistoryBookFragment>();
                result = localFragment != null && await localFragment.RunSyncedRewrite(option, new BlockingPlayerChoiceContext());
            },
            optionValue);

        return result;
    }

    /// <summary>
    /// 确保联机消息处理器已注册。
    /// </summary>
    /// <param name="runManager">当前跑团管理器。</param>
    /// <returns>注册成功或已注册时返回 true。</returns>
    internal static bool EnsureRegisteredForRun(RunManager runManager)
    {
        if (!runManager.IsInProgress || runManager.DebugOnlyGetState() == null)
        {
            return false;
        }

        RunLocationTargetedMessageBuffer? buffer = runManager.RunLocationTargetedBuffer;
        if (buffer == null)
        {
            return false;
        }

        if (ReferenceEquals(_registeredBuffer, buffer))
        {
            return true;
        }

        if (_registeredBuffer != null)
        {
            try
            {
                _registeredBuffer.UnregisterMessageHandler<RelicInteractionUseMessage>(HandleUseMessage);
            }
            catch (Exception ex)
            {
                MainFile.Logger.Info($"【遗物联机】反注册旧消息处理器时发生异常，继续注册新处理器：{ex}");
            }
        }

        buffer.RegisterMessageHandler<RelicInteractionUseMessage>(HandleUseMessage);
        _registeredBuffer = buffer;
        MainFile.Logger.Info("【遗物联机】已注册通用遗物交互消息处理器。");
        return true;
    }

    /// <summary>
    /// 广播消息并在本机执行同一逻辑。
    /// </summary>
    /// <param name="owner">触发玩家。</param>
    /// <param name="interactionType">交互类型。</param>
    /// <param name="targetRelicType">目标遗物类型。</param>
    /// <param name="boolValue">通用布尔参数。</param>
    /// <param name="execute">本机执行逻辑。</param>
    /// <returns>异步任务。</returns>
    private static async Task BroadcastAndExecute(Player owner, RelicInteractionType interactionType, TalismanRelicType targetRelicType, bool boolValue, Func<Player, Task> execute, int intValue = 0, int secondIntValue = 0, bool secondBoolValue = false, string stringValue = "", string secondStringValue = "")
    {
        RunManager runManager = RunManager.Instance;
        bool canUseNetworkBuffer = EnsureRegisteredForRun(runManager);
        if (canUseNetworkBuffer && runManager.NetService.Type != NetGameType.Singleplayer)
        {
            runManager.NetService.SendMessage(new RelicInteractionUseMessage
            {
                Location = runManager.RunLocationTargetedBuffer.CurrentLocation,
                PlayerNetId = owner.NetId,
                InteractionType = interactionType,
                TargetRelicType = targetRelicType,
                BoolValue = boolValue,
                IntValue = intValue,
                SecondIntValue = secondIntValue,
                SecondBoolValue = secondBoolValue,
                StringValue = stringValue,
                SecondStringValue = secondStringValue
            });
            MainFile.Logger.Info($"【遗物联机】已广播遗物交互：玩家={owner.NetId}，类型={interactionType}，目标={targetRelicType}，布尔参数={boolValue}，整数参数={intValue}，第二整数={secondIntValue}，第二布尔={secondBoolValue}，字符串={stringValue}，第二字符串={secondStringValue}。");
        }
        else if (!canUseNetworkBuffer)
        {
            MainFile.Logger.Info($"【遗物联机】当前未进入有效跑团，按本地流程执行：类型={interactionType}。");
        }

        await execute(owner);
    }

    /// <summary>
    /// 判断指定交互是否属于休息处二段选择遗物交互。
    /// </summary>
    /// <param name="interactionType">交互类型。</param>
    /// <returns>属于休息处二段选择遗物交互时返回 true。</returns>
    private static bool IsRestSiteRelicInteraction(RelicInteractionType interactionType)
    {
        return interactionType is RelicInteractionType.MonkeyRestTransform
            or RelicInteractionType.PanKuRestEnchant;
    }

    /// <summary>
    /// 判断远端遗物消息是否表示休息处交互被取消。
    /// </summary>
    /// <param name="message">遗物交互消息。</param>
    /// <returns>是取消消息时返回 true。</returns>
    private static bool IsRestSiteInteractionCanceledMessage(RelicInteractionUseMessage message)
    {
        return IsRestSiteRelicInteraction(message.InteractionType)
               && message.IntValue == RestSiteInteractionCanceledIndex;
    }

    /// <summary>
    /// 完成正在等待的远端休息处遗物交互。
    /// </summary>
    /// <param name="playerNetId">玩家网络 ID。</param>
    /// <param name="interactionType">交互类型。</param>
    /// <param name="result">交互结果。</param>
    private static void CompletePendingRestSiteInteraction(ulong playerNetId, RelicInteractionType interactionType, bool result)
    {
        RestSiteInteractionKey key = new(playerNetId, interactionType);
        TaskCompletionSource<bool>? completion = null;
        lock (_pendingRestSiteInteractionLock)
        {
            if (_pendingRestSiteInteractions.TryGetValue(key, out TaskCompletionSource<bool>? pendingCompletion))
            {
                completion = pendingCompletion;
            }
        }

        if (completion == null)
        {
            return;
        }

        if (completion.TrySetResult(result))
        {
            MainFile.Logger.Info($"【遗物联机】已唤醒远端休息处等待：玩家={playerNetId}，类型={interactionType}，结果={result}。");
            _ = RemovePendingRestSiteInteractionAfterDelay(key);
        }
    }

    /// <summary>
    /// 移除正在等待的远端休息处遗物交互。
    /// </summary>
    /// <param name="key">等待键。</param>
    private static void RemovePendingRestSiteInteraction(RestSiteInteractionKey key)
    {
        lock (_pendingRestSiteInteractionLock)
        {
            _pendingRestSiteInteractions.Remove(key);
        }
    }

    /// <summary>
    /// 延迟移除已完成的远端休息处等待键，避免同一次点击的重复消息再次进入等待。
    /// </summary>
    /// <param name="key">等待键。</param>
    /// <returns>异步任务。</returns>
    private static async Task RemovePendingRestSiteInteractionAfterDelay(RestSiteInteractionKey key)
    {
        await Task.Delay(CompletedRestSiteInteractionKeepAliveMs);
        RemovePendingRestSiteInteraction(key);
    }

    /// <summary>
    /// 尝试把联机战斗内遗物交互提交到官方动作队列。
    /// 单机和非战斗场景不在这里处理，继续走旧的本地执行流程，避免破坏单人体验。
    /// </summary>
    /// <param name="owner">触发玩家。</param>
    /// <param name="interactionType">交互类型。</param>
    /// <param name="targetRelicType">目标遗物类型。</param>
    /// <param name="boolValue">通用布尔参数。</param>
    /// <param name="intValue">通用整数参数。</param>
    /// <returns>本次请求已由动作队列路径处理时返回 true。</returns>
    private static bool TrySubmitQueuedInteraction(Player owner, RelicInteractionType interactionType, TalismanRelicType targetRelicType, bool boolValue, int intValue)
    {
        if (!ShouldUseQueuedInteraction(owner, interactionType))
        {
            return false;
        }

        QueuedInteractionKey key = new(owner.NetId, interactionType, targetRelicType, boolValue, intValue);
        lock (_queuedInteractionLock)
        {
            if (!_queuedInteractionKeys.Add(key))
            {
                MainFile.Logger.Info($"【遗物联机请求】已有相同遗物交互等待执行，已跳过重复入队：玩家={owner.NetId}，类型={interactionType}，目标={targetRelicType}，布尔参数={boolValue}，整数参数={intValue}。");
                return true;
            }
        }

        try
        {
            RelicInteractionGameAction action = new(owner, interactionType, targetRelicType, boolValue, intValue);
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);
            MainFile.Logger.Info($"【遗物联机请求】已提交官方动作队列：玩家={owner.NetId}，类型={interactionType}，目标={targetRelicType}，布尔参数={boolValue}，整数参数={intValue}。");
            return true;
        }
        catch (Exception ex)
        {
            MarkQueuedInteractionFinished(owner.NetId, interactionType, targetRelicType, boolValue, intValue);
            MainFile.Logger.Info($"【遗物联机请求】提交官方动作队列失败，将回退到旧同步流程：玩家={owner.NetId}，类型={interactionType}，目标={targetRelicType}，异常={ex}。");
            return false;
        }
    }

    /// <summary>
    /// 判断当前遗物交互是否应该走官方动作队列。
    /// </summary>
    /// <param name="owner">触发玩家。</param>
    /// <param name="interactionType">交互类型。</param>
    /// <returns>应该走动作队列时返回 true。</returns>
    private static bool ShouldUseQueuedInteraction(Player owner, RelicInteractionType interactionType)
    {
        if (!RequiresQueuedInteraction(interactionType))
        {
            return false;
        }

        RunManager runManager = RunManager.Instance;
        if (runManager.NetService.Type == NetGameType.Singleplayer)
        {
            return false;
        }

        if (!runManager.IsInProgress || runManager.DebugOnlyGetState() == null)
        {
            MainFile.Logger.Info($"【遗物联机请求】当前不在有效跑团内，沿用旧流程：玩家={owner.NetId}，类型={interactionType}。");
            return false;
        }

        if (owner.PlayerCombatState == null && owner.Creature?.CombatState == null)
        {
            MainFile.Logger.Info($"【遗物联机请求】当前不是战斗内遗物交互，沿用旧流程：玩家={owner.NetId}，类型={interactionType}。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 判断交互类型是否属于必须排队的高风险战斗交互。
    /// </summary>
    /// <param name="interactionType">交互类型。</param>
    /// <returns>需要排队时返回 true。</returns>
    private static bool RequiresQueuedInteraction(RelicInteractionType interactionType)
    {
        return interactionType is RelicInteractionType.SetActivation
            or RelicInteractionType.RatTransform
            or RelicInteractionType.RatFormSwitch
            or RelicInteractionType.HorseRemoveDebuff
            or RelicInteractionType.MonkeyCombatTransform
            or RelicInteractionType.TalismanLocatorMode
            or RelicInteractionType.ShadowKhanTokenMode
            or RelicInteractionType.RabbitRefreshTarget
            or RelicInteractionType.HistoryBookRewrite;
    }

    /// <summary>
    /// 执行动作队列中的遗物交互。
    /// </summary>
    /// <param name="player">触发玩家。</param>
    /// <param name="interactionType">交互类型。</param>
    /// <param name="targetRelicType">目标遗物类型。</param>
    /// <param name="boolValue">通用布尔参数。</param>
    /// <param name="intValue">通用整数参数。</param>
    /// <param name="choiceContext">官方动作队列选择上下文。</param>
    /// <returns>异步任务。</returns>
    internal static async Task ExecuteQueuedInteraction(Player player, RelicInteractionType interactionType, TalismanRelicType targetRelicType, bool boolValue, int intValue, PlayerChoiceContext choiceContext)
    {
        try
        {
            switch (interactionType)
            {
                case RelicInteractionType.SetActivation:
                    ExecuteSetActivated(FindRelic(player, targetRelicType), boolValue);
                    break;
                case RelicInteractionType.RatTransform:
                    await (player.GetRelic<RatTalisman>()?.RunSyncedReplaceCurseOrStatus(choiceContext, boolValue) ?? Task.CompletedTask);
                    break;
                case RelicInteractionType.RatFormSwitch:
                    await ShengZhuFormService.RunSyncedSwitch(player, boolValue, choiceContext);
                    break;
                case RelicInteractionType.HorseRemoveDebuff:
                    await (player.GetRelic<HorseTalisman>()?.RunSyncedRemoveDebuff(choiceContext, boolValue) ?? Task.CompletedTask);
                    break;
                case RelicInteractionType.MonkeyCombatTransform:
                    await (player.GetRelic<MonkeyTalisman>()?.RunSyncedCombatTransform(choiceContext) ?? Task.CompletedTask);
                    break;
                case RelicInteractionType.RabbitRefreshTarget:
                    RabbitTalisman? rabbit = player.GetRelic<RabbitTalisman>();
                    RelicModel? target = FindRelic(player, targetRelicType);
                    if (rabbit != null && target != null)
                    {
                        await rabbit.RunSyncedRefreshTargetRelic(target, true, choiceContext);
                    }
                    break;
                case RelicInteractionType.TalismanLocatorMode:
                    await (player.GetRelic<TalismanLocator>()?.RunSyncedSetSemiAutoMode(boolValue, true) ?? Task.CompletedTask);
                    break;
                case RelicInteractionType.ShadowKhanTokenMode:
                    player.GetRelic<ShadowKhanToken>()?.RunSyncedSetExhaustMode(intValue);
                    break;
                case RelicInteractionType.HistoryBookRewrite:
                    await (player.GetRelic<HistoryBookFragment>()?.RunSyncedRewrite((HistoryBookRewriteOption)intValue, choiceContext) ?? Task.FromResult(false));
                    break;
                default:
                    MainFile.Logger.Info($"【遗物联机Action】不支持的遗物交互类型：玩家={player.NetId}，类型={interactionType}，目标={targetRelicType}。");
                    break;
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【遗物联机Action】执行遗物交互时发生异常，已记录并跳过，避免中断联机动作队列：玩家={player.NetId}，类型={interactionType}，目标={targetRelicType}，异常={ex}。");
        }
    }

    /// <summary>
    /// 标记动作队列中的遗物交互已结束。
    /// </summary>
    /// <param name="ownerNetId">玩家网络 ID。</param>
    /// <param name="interactionType">交互类型。</param>
    /// <param name="targetRelicType">目标遗物类型。</param>
    /// <param name="boolValue">通用布尔参数。</param>
    /// <param name="intValue">通用整数参数。</param>
    internal static void MarkQueuedInteractionFinished(ulong ownerNetId, RelicInteractionType interactionType, TalismanRelicType targetRelicType, bool boolValue, int intValue)
    {
        QueuedInteractionKey key = new(ownerNetId, interactionType, targetRelicType, boolValue, intValue);
        lock (_queuedInteractionLock)
        {
            _queuedInteractionKeys.Remove(key);
        }
    }

    /// <summary>
    /// 判断当前端是否应该发起该玩家的遗物交互。
    /// </summary>
    /// <param name="owner">触发玩家。</param>
    /// <param name="actionName">交互名称。</param>
    /// <returns>当前端应发起时返回 true。</returns>
    private static bool CanLocalEndpointInitiate(Player owner, string actionName)
    {
        RunManager runManager = RunManager.Instance;
        if (runManager.NetService.Type == NetGameType.Singleplayer)
        {
            return true;
        }

        if (runManager.NetService.NetId == owner.NetId)
        {
            return true;
        }

        MainFile.Logger.Info($"【遗物联机】跳过非本端玩家的本地发起：操作={actionName}，玩家={owner.NetId}，本端={runManager.NetService.NetId}。");
        return false;
    }

    /// <summary>
    /// 处理远端玩家发来的遗物交互消息。
    /// </summary>
    /// <param name="message">遗物交互消息。</param>
    /// <param name="senderId">触发玩家的网络 ID。</param>
    private static void HandleUseMessage(RelicInteractionUseMessage message, ulong senderId)
    {
        _ = HandleUseMessageSafely(message, senderId);
    }

    /// <summary>
    /// 带异常保护地执行远端遗物交互。
    /// </summary>
    /// <param name="message">遗物交互消息。</param>
    /// <param name="senderId">触发玩家的网络 ID。</param>
    /// <returns>异步任务。</returns>
    private static async Task HandleUseMessageSafely(RelicInteractionUseMessage message, ulong senderId)
    {
        try
        {
            RunManager runManager = RunManager.Instance;
            if (runManager.NetService.NetId == senderId)
            {
                MainFile.Logger.Info("【遗物联机】收到本机遗物交互消息，已跳过避免重复执行。");
                return;
            }

            await _remoteInteractionLock.WaitAsync();
            try
            {
                ulong playerNetId = message.PlayerNetId == 0 ? senderId : message.PlayerNetId;
                Player? player = runManager.DebugOnlyGetState()?.GetPlayer(playerNetId);
                if (player == null)
                {
                    MainFile.Logger.Info($"【遗物联机】远端交互失败：找不到玩家，发送者={senderId}，目标玩家={playerNetId}。");
                    return;
                }

                MainFile.Logger.Info($"【遗物联机】收到远端遗物交互：发送者={senderId}，目标玩家={playerNetId}，类型={message.InteractionType}，目标={message.TargetRelicType}，布尔参数={message.BoolValue}，整数参数={message.IntValue}。");
                await ExecuteRemoteInteraction(player, message);
            }
            finally
            {
                _remoteInteractionLock.Release();
            }
        }
        catch (Exception ex)
        {
            ulong playerNetId = message.PlayerNetId == 0 ? senderId : message.PlayerNetId;
            if (IsRestSiteRelicInteraction(message.InteractionType))
            {
                CompletePendingRestSiteInteraction(playerNetId, message.InteractionType, false);
            }

            MainFile.Logger.Info($"【遗物联机】处理远端遗物交互时发生异常：{ex}");
        }
    }

    /// <summary>
    /// 按消息内容执行远端遗物交互。
    /// </summary>
    /// <param name="player">触发玩家。</param>
    /// <param name="message">遗物交互消息。</param>
    /// <returns>异步任务。</returns>
    private static async Task ExecuteRemoteInteraction(Player player, RelicInteractionUseMessage message)
    {
        if (IsRestSiteInteractionCanceledMessage(message))
        {
            CompletePendingRestSiteInteraction(player.NetId, message.InteractionType, false);
            MainFile.Logger.Info($"【遗物联机】收到远端休息处遗物交互取消：玩家={player.NetId}，类型={message.InteractionType}。");
            return;
        }

        switch (message.InteractionType)
        {
            case RelicInteractionType.SetActivation:
                ExecuteSetActivated(FindRelic(player, message.TargetRelicType), message.BoolValue);
                break;
            case RelicInteractionType.RatTransform:
                await (player.GetRelic<RatTalisman>()?.RunSyncedReplaceCurseOrStatus(new BlockingPlayerChoiceContext(), message.BoolValue) ?? Task.CompletedTask);
                break;
            case RelicInteractionType.RatFormSwitch:
                await ShengZhuFormService.RunSyncedSwitch(player, message.BoolValue, new BlockingPlayerChoiceContext());
                break;
            case RelicInteractionType.HorseRemoveDebuff:
                await (player.GetRelic<HorseTalisman>()?.RunSyncedRemoveDebuff(new BlockingPlayerChoiceContext(), message.BoolValue) ?? Task.CompletedTask);
                break;
            case RelicInteractionType.MonkeyCombatTransform:
                await (player.GetRelic<MonkeyTalisman>()?.RunSyncedCombatTransform(new BlockingPlayerChoiceContext()) ?? Task.CompletedTask);
                break;
            case RelicInteractionType.RabbitRefreshTarget:
                RabbitTalisman? rabbit = player.GetRelic<RabbitTalisman>();
                RelicModel? target = FindRelic(player, message.TargetRelicType);
                if (rabbit != null && target != null)
                {
                    await rabbit.RunSyncedRefreshTargetRelic(target, true, new BlockingPlayerChoiceContext());
                }
                break;
            case RelicInteractionType.MonkeyRestTransform:
                MonkeyRestTransformPayload monkeyPayload = new(message.IntValue, message.StringValue, message.SecondBoolValue, message.SecondStringValue);
                bool monkeyResult = await (player.GetRelic<MonkeyTalisman>()?.RunSyncedRestSiteTransform(monkeyPayload) ?? Task.FromResult(false));
                CompletePendingRestSiteInteraction(player.NetId, message.InteractionType, monkeyResult);
                break;
            case RelicInteractionType.PanKuRestEnchant:
                PanKuRestEnchantPayload panKuPayload = new(message.IntValue, message.StringValue, message.SecondBoolValue, (PanKuDemonQiKind)message.SecondIntValue);
                bool panKuResult = player.GetRelic<PanKuBox>()?.RunSyncedRestSiteEnchant(panKuPayload) == true;
                CompletePendingRestSiteInteraction(player.NetId, message.InteractionType, panKuResult);
                break;
            case RelicInteractionType.PanKuRewardClaim:
                PanKuRewardClaimType claimType = Enum.TryParse(message.StringValue, out PanKuRewardClaimType parsedClaimType)
                    ? parsedClaimType
                    : PanKuRewardClaimType.None;
                PanKuRewardClaimPayload rewardPayload = new(claimType, (PanKuDemonQiKind)message.SecondIntValue, message.IntValue, message.SecondStringValue, message.SecondBoolValue);
                await (player.GetRelic<PanKuBox>()?.RunSyncedRewardClaim(rewardPayload) ?? Task.FromResult(false));
                break;
            case RelicInteractionType.HistoryBookRewrite:
                await (player.GetRelic<HistoryBookFragment>()?.RunSyncedRewrite((HistoryBookRewriteOption)message.IntValue, new BlockingPlayerChoiceContext()) ?? Task.FromResult(false));
                break;
            case RelicInteractionType.TalismanLocatorMode:
                TalismanLocator? locator = player.GetRelic<TalismanLocator>();
                if (locator == null)
                {
                    MainFile.Logger.Info($"【遗物联机】探测仪切挡失败：目标玩家没有符咒探测仪，玩家={player.NetId}。");
                    break;
                }

                await locator.RunSyncedSetSemiAutoMode(message.BoolValue, false);
                break;
            case RelicInteractionType.ShadowKhanTokenMode:
                ShadowKhanToken? shadowToken = player.GetRelic<ShadowKhanToken>();
                if (shadowToken == null)
                {
                    MainFile.Logger.Info($"【遗物联机】黑影令牌切挡失败：目标玩家没有黑影令牌，玩家={player.NetId}。");
                    break;
                }

                shadowToken.RunSyncedSetExhaustMode(message.IntValue);
                break;
            default:
                MainFile.Logger.Info($"【遗物联机】收到未知遗物交互类型：{message.InteractionType}。");
                break;
        }
    }

    /// <summary>
    /// 找到玩家持有的指定符咒。
    /// </summary>
    /// <param name="player">玩家。</param>
    /// <param name="relicType">符咒类型编号。</param>
    /// <returns>匹配的遗物；不存在时返回 null。</returns>
    private static RelicModel? FindRelic(Player player, TalismanRelicType relicType)
    {
        return player.Relics.FirstOrDefault(relic => TalismanRelicTypeHelper.FromRelic(relic) == relicType);
    }

    /// <summary>
    /// 执行符咒激发状态设置。
    /// </summary>
    /// <param name="relic">目标遗物。</param>
    /// <param name="activate">是否激发。</param>
    private static void ExecuteSetActivated(RelicModel? relic, bool activate)
    {
        switch (relic)
        {
            case OxTalisman ox:
                ox.SetActivated(activate);
                break;
            case PigTalisman pig:
                pig.SetActivated(activate);
                break;
            case RabbitTalisman rabbit:
                rabbit.SetActivated(activate);
                break;
            case SnakeTalisman snake:
                snake.SetActivated(activate);
                break;
            case SheepTalisman sheep:
                sheep.SetActivated(activate);
                break;
            default:
                MainFile.Logger.Info($"【遗物联机】设置激发失败：目标遗物不支持同步激发，类型={relic?.GetType().Name ?? "空"}。");
                break;
        }
    }

    /// <summary>
    /// 已提交到动作队列的遗物交互键。
    /// </summary>
    /// <param name="OwnerNetId">玩家网络 ID。</param>
    /// <param name="InteractionType">交互类型。</param>
    /// <param name="TargetRelicType">目标遗物类型。</param>
    /// <param name="BoolValue">通用布尔参数。</param>
    /// <param name="IntValue">通用整数参数。</param>
    private readonly record struct QueuedInteractionKey(ulong OwnerNetId, RelicInteractionType InteractionType, TalismanRelicType TargetRelicType, bool BoolValue, int IntValue);

    /// <summary>
    /// 正在等待远端完成的休息处遗物交互键。
    /// </summary>
    /// <param name="OwnerNetId">玩家网络 ID。</param>
    /// <param name="InteractionType">休息处遗物交互类型。</param>
    private readonly record struct RestSiteInteractionKey(ulong OwnerNetId, RelicInteractionType InteractionType);
}
