using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 生命之杯药水使用补丁：在官方药水使用动作完成后，安全重放药水效果并触发生命之杯回血。
/// </summary>
[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.OnUseWrapper))]
public static class LifeChalicePotionUsePatch
{
    /// <summary>
    /// 记录已经由生命之杯处理过的官方用药动作，避免同一个动作被其他重放链路反复处理。
    /// </summary>
    private static readonly ConditionalWeakTable<UsePotionAction, ResolvedMarker> ResolvedActions = new();

    /// <summary>
    /// 当前异步调用链是否正在执行生命之杯重放。
    /// </summary>
    private static readonly AsyncLocal<int> ReplayDepth = new();

    /// <summary>
    /// protected OnUse 方法签名。
    /// </summary>
    private static readonly Type[] OnUseParameterTypes = [typeof(PlayerChoiceContext), typeof(Creature)];

    /// <summary>
    /// 原版药水使用前采集能否由生命之杯处理的上下文。
    /// </summary>
    /// <param name="__instance">正在使用的药水。</param>
    /// <param name="choiceContext">玩家选择上下文。</param>
    /// <param name="target">药水目标。</param>
    /// <param name="__state">传递给后置补丁的处理状态。</param>
    private static void Prefix(PotionModel __instance, PlayerChoiceContext choiceContext, Creature? target, out LifeChalicePotionUseState __state)
    {
        __state = CreateState(__instance, choiceContext, target);
    }

    /// <summary>
    /// 包装原版药水使用任务，等原效果成功结束后追加生命之杯效果。
    /// </summary>
    /// <param name="__result">原版药水使用任务。</param>
    /// <param name="__state">前置补丁采集的处理状态。</param>
    private static void Postfix(ref Task __result, LifeChalicePotionUseState __state)
    {
        if (!__state.ShouldHandle)
        {
            return;
        }

        __result = ReplayAndHealAfterOriginal(__result, __state);
    }

    /// <summary>
    /// 创建本次药水使用的生命之杯处理状态。
    /// </summary>
    /// <param name="potion">正在使用的药水。</param>
    /// <param name="choiceContext">玩家选择上下文。</param>
    /// <param name="target">药水目标。</param>
    /// <returns>本次处理状态。</returns>
    private static LifeChalicePotionUseState CreateState(PotionModel potion, PlayerChoiceContext choiceContext, Creature? target)
    {
        Player? owner = potion.Owner;
        if (owner == null)
        {
            return LifeChalicePotionUseState.Skip("药水持有者为空");
        }

        if (ReplayDepth.Value > 0)
        {
            return LifeChalicePotionUseState.Skip("当前处于生命之杯重放上下文");
        }

        if (potion.Usage == PotionUsage.Automatic)
        {
            return LifeChalicePotionUseState.Skip("自动药水不参与重放");
        }

        if (choiceContext is not GameActionPlayerChoiceContext gameActionContext || gameActionContext.Action is not UsePotionAction action)
        {
            return LifeChalicePotionUseState.Skip("不是官方用药动作上下文");
        }

        if (!ReferenceEquals(action.Player, owner))
        {
            return LifeChalicePotionUseState.Skip("用药动作玩家与药水持有者不一致");
        }

        if (ResolvedActions.TryGetValue(action, out _))
        {
            return LifeChalicePotionUseState.Skip("同一个用药动作已经处理过");
        }

        if (potion.HasBeenRemovedFromState)
        {
            return LifeChalicePotionUseState.Skip("药水已经离开药水栏");
        }

        if (!IsPotionStillInActionSlot(owner, potion, action))
        {
            return LifeChalicePotionUseState.Skip("药水不在官方用药动作记录的栏位中");
        }

        if (owner.GetRelic<LifeChalice>() == null)
        {
            return LifeChalicePotionUseState.Skip("玩家未持有生命之杯");
        }

        ResolvedActions.Add(action, new ResolvedMarker());
        return LifeChalicePotionUseState.Handle(potion, choiceContext, target, action, owner);
    }

    /// <summary>
    /// 等待原版药水效果完成后，执行生命之杯的重放和回血。
    /// </summary>
    /// <param name="originalTask">原版药水使用任务。</param>
    /// <param name="state">本次处理状态。</param>
    private static async Task ReplayAndHealAfterOriginal(Task originalTask, LifeChalicePotionUseState state)
    {
        await originalTask;

        LifeChalice? lifeChalice = state.Owner.GetRelic<LifeChalice>();
        if (lifeChalice == null)
        {
            MainFile.Logger.Info($"【生命之杯】原药水完成后跳过：玩家={state.Owner.NetId}，药水={FormatPotion(state.Potion)}，原因=玩家已不再持有生命之杯。");
            return;
        }

        bool replayed = await TryReplayPotionEffect(state, lifeChalice);
        await LifeChaliceHealingService.HealWithOverflowToMaxHp(state.Owner, LifeChalice.HealPercent, $"药水={state.Potion.Id.Entry}，已重放={replayed}");
    }

    /// <summary>
    /// 尝试重放一次药水 protected OnUse 效果。
    /// </summary>
    /// <param name="state">本次处理状态。</param>
    /// <param name="lifeChalice">触发效果的生命之杯。</param>
    /// <returns>成功进入重放流程时返回 true。</returns>
    private static async Task<bool> TryReplayPotionEffect(LifeChalicePotionUseState state, LifeChalice lifeChalice)
    {
        if (!CanReplayAfterOriginal(state, out string reason))
        {
            MainFile.Logger.Info($"【生命之杯】跳过药水重放：玩家={state.Owner.NetId}，药水={FormatPotion(state.Potion)}，目标={FormatTarget(state.Target)}，原因={reason}。");
            return false;
        }

        MethodInfo? onUseMethod = AccessTools.Method(state.Potion.GetType(), "OnUse", OnUseParameterTypes);
        if (onUseMethod == null)
        {
            MainFile.Logger.Info($"【生命之杯】跳过药水重放：玩家={state.Owner.NetId}，药水={FormatPotion(state.Potion)}，原因=找不到 OnUse 方法。");
            return false;
        }

        MainFile.Logger.Info($"【生命之杯】开始药水重放：玩家={state.Owner.NetId}，药水={FormatPotion(state.Potion)}，目标={FormatTarget(state.Target)}。");
        lifeChalice.Flash();
        ReplayDepth.Value++;
        bool pushedModel = false;
        bool beganEffect = false;

        try
        {
            state.ChoiceContext.PushModel(state.Potion);
            pushedModel = true;
            CombatManager.Instance.BeginCardOrPotionEffect(state.Owner);
            beganEffect = true;
            Task? replayTask = onUseMethod.Invoke(state.Potion, [state.ChoiceContext, state.Target]) as Task;
            if (replayTask == null)
            {
                MainFile.Logger.Info($"【生命之杯】药水重放失败：玩家={state.Owner.NetId}，药水={FormatPotion(state.Potion)}，原因=OnUse 未返回 Task。");
                return false;
            }

            await replayTask;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【生命之杯】药水重放异常兜底：玩家={state.Owner.NetId}，药水={FormatPotion(state.Potion)}，异常={ex.GetType().Name}，消息={ex.Message}。");
            return false;
        }
        finally
        {
            if (beganEffect)
            {
                CombatManager.Instance.EndCardOrPotionEffect(state.Owner);
            }

            if (pushedModel)
            {
                state.ChoiceContext.PopModel(state.Potion);
            }

            ReplayDepth.Value = Math.Max(0, ReplayDepth.Value - 1);
        }

        await CombatManager.Instance.CheckForEmptyHand(state.ChoiceContext, state.Owner);
        MainFile.Logger.Info($"【生命之杯】药水重放完成：玩家={state.Owner.NetId}，药水={FormatPotion(state.Potion)}，目标={FormatTarget(state.Target)}。");
        return true;
    }

    /// <summary>
    /// 判断原药水完成后是否仍允许重放。
    /// </summary>
    /// <param name="state">本次处理状态。</param>
    /// <param name="reason">不能重放时的原因。</param>
    /// <returns>允许重放时返回 true。</returns>
    private static bool CanReplayAfterOriginal(LifeChalicePotionUseState state, out string reason)
    {
        if (state.Action.WasEnqueuedInCombat && CombatManager.Instance.IsOverOrEnding)
        {
            reason = "战斗已经结束或正在结束";
            return false;
        }

        if (state.Potion.Usage == PotionUsage.Automatic)
        {
            reason = "自动药水不参与重放";
            return false;
        }

        if (!state.Potion.IsValidTarget(state.Target))
        {
            reason = "原目标已经死亡或不再合法";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// 判断药水是否仍位于官方用药动作记录的栏位中。
    /// </summary>
    /// <param name="owner">药水持有者。</param>
    /// <param name="potion">正在使用的药水。</param>
    /// <param name="action">官方用药动作。</param>
    /// <returns>药水仍在对应栏位时返回 true。</returns>
    private static bool IsPotionStillInActionSlot(Player owner, PotionModel potion, UsePotionAction action)
    {
        try
        {
            return ReferenceEquals(owner.GetPotionAtSlotIndex((int)action.PotionIndex), potion);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【生命之杯】检查药水栏位异常：玩家={owner.NetId}，药水={FormatPotion(potion)}，栏位={action.PotionIndex}，异常={ex.GetType().Name}。");
            return false;
        }
    }

    /// <summary>
    /// 格式化药水信息。
    /// </summary>
    /// <param name="potion">药水对象。</param>
    /// <returns>可读的药水信息。</returns>
    private static string FormatPotion(PotionModel? potion)
    {
        return potion == null ? "空" : $"{potion.Id.Entry}(用法={potion.Usage}，目标类型={potion.TargetType})";
    }

    /// <summary>
    /// 格式化目标信息。
    /// </summary>
    /// <param name="target">药水目标。</param>
    /// <returns>可读的目标信息。</returns>
    private static string FormatTarget(Creature? target)
    {
        return target == null ? "空" : $"{target.Name}(HP={target.CurrentHp}，存活={target.IsAlive}，死亡={target.IsDead})";
    }

    /// <summary>
    /// ConditionalWeakTable 使用的空标记对象。
    /// </summary>
    private sealed class ResolvedMarker
    {
    }

    /// <summary>
    /// 单次药水使用的生命之杯处理状态。
    /// </summary>
    private sealed class LifeChalicePotionUseState
    {
        /// <summary>
        /// 是否需要在原药水完成后追加生命之杯效果。
        /// </summary>
        public bool ShouldHandle { get; private init; }

        /// <summary>
        /// 正在使用的药水。
        /// </summary>
        public PotionModel Potion { get; private init; } = null!;

        /// <summary>
        /// 原用药动作的选择上下文。
        /// </summary>
        public PlayerChoiceContext ChoiceContext { get; private init; } = null!;

        /// <summary>
        /// 原药水目标。
        /// </summary>
        public Creature? Target { get; private init; }

        /// <summary>
        /// 官方用药动作。
        /// </summary>
        public UsePotionAction Action { get; private init; } = null!;

        /// <summary>
        /// 药水持有者。
        /// </summary>
        public Player Owner { get; private init; } = null!;

        /// <summary>
        /// 创建需要处理的状态。
        /// </summary>
        /// <param name="potion">正在使用的药水。</param>
        /// <param name="choiceContext">选择上下文。</param>
        /// <param name="target">药水目标。</param>
        /// <param name="action">官方用药动作。</param>
        /// <param name="owner">药水持有者。</param>
        /// <returns>需要处理的状态。</returns>
        public static LifeChalicePotionUseState Handle(PotionModel potion, PlayerChoiceContext choiceContext, Creature? target, UsePotionAction action, Player owner)
        {
            return new LifeChalicePotionUseState
            {
                ShouldHandle = true,
                Potion = potion,
                ChoiceContext = choiceContext,
                Target = target,
                Action = action,
                Owner = owner
            };
        }

        /// <summary>
        /// 创建跳过处理的状态。
        /// </summary>
        /// <param name="reason">跳过原因。</param>
        /// <returns>无需处理的状态。</returns>
        public static LifeChalicePotionUseState Skip(string reason)
        {
            return new LifeChalicePotionUseState
            {
                ShouldHandle = false
            };
        }
    }
}
