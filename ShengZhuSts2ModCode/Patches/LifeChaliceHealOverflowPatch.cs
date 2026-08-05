using System.Threading;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 生命之杯治疗溢出补丁：让所有官方治疗命令产生的溢出治疗都转换为最大生命值。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal))]
public static class LifeChaliceHealOverflowPatch
{
    /// <summary>
    /// 当前异步调用链是否正在执行生命之杯溢出转换，避免补丁处理自己的后续校正命令。
    /// </summary>
    private static readonly AsyncLocal<int> OverflowDepth = new();

    /// <summary>
    /// 原治疗命令执行前采集生命之杯溢出转换所需状态。
    /// </summary>
    /// <param name="creature">接受治疗的生物。</param>
    /// <param name="amount">本次治疗量。</param>
    /// <param name="playAnim">是否播放治疗动画。</param>
    /// <param name="__state">传递给后置补丁的处理状态。</param>
    private static void Prefix(Creature creature, decimal amount, bool playAnim, out LifeChaliceHealOverflowState __state)
    {
        try
        {
            __state = CreateState(creature, amount);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【生命之杯】治疗溢出状态采集异常，已跳过：治疗量={amount}，异常={ex.GetType().Name}，消息={ex.Message}。");
            __state = LifeChaliceHealOverflowState.Skip();
        }
    }

    /// <summary>
    /// 包装原治疗任务，在治疗成功完成后把溢出部分转换为最大生命值。
    /// </summary>
    /// <param name="__result">原治疗任务。</param>
    /// <param name="__state">前置补丁采集的处理状态。</param>
    private static void Postfix(ref Task? __result, LifeChaliceHealOverflowState? __state)
    {
        if (__state == null || !__state.ShouldHandle)
        {
            return;
        }

        if (__result == null)
        {
            MainFile.Logger.Info($"【生命之杯】治疗溢出转换跳过：原治疗任务为空，治疗量={__state.HealAmount}，治疗前={__state.CurrentHpBefore}/{__state.MaxHpBefore}。");
            return;
        }

        __result = ConvertOverflowAfterHeal(__result, __state);
    }

    /// <summary>
    /// 创建单次治疗溢出转换状态。
    /// </summary>
    /// <param name="creature">接受治疗的生物。</param>
    /// <param name="amount">本次治疗量。</param>
    /// <returns>单次治疗溢出转换状态。</returns>
    private static LifeChaliceHealOverflowState CreateState(Creature? creature, decimal amount)
    {
        if (OverflowDepth.Value > 0)
        {
            return LifeChaliceHealOverflowState.Skip();
        }

        Player? player = creature?.Player;
        if (player == null || creature == null)
        {
            return LifeChaliceHealOverflowState.Skip();
        }

        if (player.GetRelic<LifeChalice>() == null)
        {
            return LifeChaliceHealOverflowState.Skip();
        }

        decimal overflowHeal = LifeChaliceHealingService.CalculateOverflowHeal(creature, amount);
        if (overflowHeal <= 0)
        {
            return LifeChaliceHealOverflowState.Skip();
        }

        return LifeChaliceHealOverflowState.Handle(player, overflowHeal, creature.CurrentHp, creature.MaxHp, amount);
    }

    /// <summary>
    /// 等待原治疗完成后执行溢出转换。
    /// </summary>
    /// <param name="originalTask">原治疗任务。</param>
    /// <param name="state">本次治疗溢出转换状态。</param>
    private static async Task ConvertOverflowAfterHeal(Task originalTask, LifeChaliceHealOverflowState state)
    {
        await originalTask;

        OverflowDepth.Value++;
        try
        {
            try
            {
                await LifeChaliceHealingService.ConvertOverflowHealToMaxHp(
                    state.Player,
                    state.OverflowHeal,
                    $"通用治疗溢出，治疗量={state.HealAmount}，治疗前={state.CurrentHpBefore}/{state.MaxHpBefore}");
            }
            catch (Exception ex)
            {
                MainFile.Logger.Info($"【生命之杯】治疗溢出转换异常兜底：治疗量={state.HealAmount}，溢出={state.OverflowHeal}，治疗前={state.CurrentHpBefore}/{state.MaxHpBefore}，异常={ex.GetType().Name}，消息={ex.Message}。");
            }
        }
        finally
        {
            OverflowDepth.Value = Math.Max(0, OverflowDepth.Value - 1);
        }
    }

    /// <summary>
    /// 单次治疗的生命之杯溢出转换状态。
    /// </summary>
    private sealed class LifeChaliceHealOverflowState
    {
        /// <summary>
        /// 是否需要在原治疗完成后处理溢出。
        /// </summary>
        public bool ShouldHandle { get; private init; }

        /// <summary>
        /// 生命之杯持有者。
        /// </summary>
        public Player Player { get; private init; } = null!;

        /// <summary>
        /// 本次需要转换为最大生命的溢出治疗量。
        /// </summary>
        public decimal OverflowHeal { get; private init; }

        /// <summary>
        /// 治疗前当前生命值。
        /// </summary>
        public int CurrentHpBefore { get; private init; }

        /// <summary>
        /// 治疗前最大生命值。
        /// </summary>
        public int MaxHpBefore { get; private init; }

        /// <summary>
        /// 原治疗量。
        /// </summary>
        public decimal HealAmount { get; private init; }

        /// <summary>
        /// 创建需要处理的状态。
        /// </summary>
        /// <param name="player">生命之杯持有者。</param>
        /// <param name="overflowHeal">溢出治疗量。</param>
        /// <param name="currentHpBefore">治疗前当前生命。</param>
        /// <param name="maxHpBefore">治疗前最大生命。</param>
        /// <param name="healAmount">原治疗量。</param>
        /// <returns>需要处理的状态。</returns>
        public static LifeChaliceHealOverflowState Handle(Player player, decimal overflowHeal, int currentHpBefore, int maxHpBefore, decimal healAmount)
        {
            return new LifeChaliceHealOverflowState
            {
                ShouldHandle = true,
                Player = player,
                OverflowHeal = overflowHeal,
                CurrentHpBefore = currentHpBefore,
                MaxHpBefore = maxHpBefore,
                HealAmount = healAmount
            };
        }

        /// <summary>
        /// 创建无需处理的状态。
        /// </summary>
        /// <returns>无需处理的状态。</returns>
        public static LifeChaliceHealOverflowState Skip()
        {
            return new LifeChaliceHealOverflowState
            {
                ShouldHandle = false
            };
        }
    }
}
