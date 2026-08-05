using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 卯兔玩家回合结束钩子补丁：
/// 卯兔制造的伪结束回合不应消耗玩家侧一回合能力或自动挡符咒。
/// </summary>
public static class RabbitTurnEndHookPatch
{
    /// <summary>
    /// 保存本次 Hook 被临时停用的卯兔玩家，原版 Hook 执行结束后需要恢复。
    /// </summary>
    private sealed class RabbitHookSuppressionState
    {
        /// <summary>
        /// 本次临时停用 Hook 的玩家列表。
        /// </summary>
        public List<Player> DeactivatedPlayers { get; } = [];
    }

    /// <summary>
    /// 拦截玩家侧回合结束前模型钩子。
    /// </summary>
    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeTurnEnd))]
    public static class BeforeTurnEndPatch
    {
        /// <summary>
        /// 若当前是卯兔伪结束回合，则只临时停用卯兔玩家自己的 BeforeTurnEnd 模型钩子。
        /// </summary>
        private static void Prefix(ICombatState combatState, CombatSide side, IEnumerable<Creature> participants, out RabbitHookSuppressionState __state)
        {
            __state = BeginSuppressingPlayerHooks(combatState, side, "BeforeTurnEnd");
        }

        /// <summary>
        /// 原版 BeforeTurnEnd 模型钩子执行结束后恢复卯兔玩家自己的 Hook。
        /// </summary>
        private static void Postfix(ref Task __result, RabbitHookSuppressionState __state)
        {
            __result = RestorePlayerHooksAfter(__result, __state, "BeforeTurnEnd");
        }
    }

    /// <summary>
    /// 拦截玩家侧回合结束后模型钩子。
    /// </summary>
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterTurnEnd))]
    public static class AfterTurnEndPatch
    {
        /// <summary>
        /// 若当前是卯兔伪结束回合，则只临时停用卯兔玩家自己的 AfterTurnEnd 模型钩子。
        /// </summary>
        private static void Prefix(ICombatState combatState, CombatSide side, IEnumerable<Creature> participants, out RabbitHookSuppressionState __state)
        {
            __state = BeginSuppressingPlayerHooks(combatState, side, "AfterTurnEnd");
        }

        /// <summary>
        /// 原版 AfterTurnEnd 模型钩子执行结束后恢复卯兔玩家自己的 Hook。
        /// </summary>
        private static void Postfix(ref Task __result, RabbitHookSuppressionState __state, ICombatState combatState, CombatSide side, IEnumerable<Creature> participants)
        {
            __result = RestorePlayerHooksAndClearContextAfter(__result, __state, "AfterTurnEnd", combatState, side);
        }
    }

    /// <summary>
    /// 在原版回合结束 Hook 执行前，临时停用卯兔玩家自己的 Hook。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <param name="side">当前结算阵营。</param>
    /// <param name="hookName">日志用 Hook 名称。</param>
    /// <returns>本次停用记录。</returns>
    private static RabbitHookSuppressionState BeginSuppressingPlayerHooks(ICombatState combatState, CombatSide side, string hookName)
    {
        RabbitHookSuppressionState state = new();
        foreach (Player player in RabbitTurnSkipContext.GetPlayersToSuppressTurnEndHooks(combatState, side))
        {
            if (!player.IsActiveForHooks)
            {
                continue;
            }

            player.DeactivateHooks();
            state.DeactivatedPlayers.Add(player);
        }

        if (state.DeactivatedPlayers.Count > 0)
        {
            MainFile.Logger.Info($"【卯兔】临时停用卯兔玩家自己的 {hookName} 模型钩子：玩家数={state.DeactivatedPlayers.Count}。");
        }

        return state;
    }

    /// <summary>
    /// 等待原版 Hook 执行完成后恢复卯兔玩家自己的 Hook，异常时也会恢复。
    /// </summary>
    /// <param name="originalTask">原版 Hook 返回的任务。</param>
    /// <param name="state">本次停用记录。</param>
    /// <param name="hookName">日志用 Hook 名称。</param>
    /// <returns>包装后的任务。</returns>
    private static async Task RestorePlayerHooksAfter(Task originalTask, RabbitHookSuppressionState state, string hookName)
    {
        if (state.DeactivatedPlayers.Count == 0)
        {
            await originalTask;
            return;
        }

        try
        {
            await originalTask;
        }
        finally
        {
            foreach (Player player in state.DeactivatedPlayers)
            {
                if (player.Creature?.IsAlive == true)
                {
                    player.ActivateHooks();
                }
            }

            MainFile.Logger.Info($"【卯兔】恢复卯兔玩家自己的 {hookName} 模型钩子：玩家数={state.DeactivatedPlayers.Count}。");
        }
    }

    /// <summary>
    /// 等待玩家侧 AfterTurnEnd 完成后恢复 Hook，并清理卯兔伪结束上下文。
    /// </summary>
    /// <param name="originalTask">原版 Hook 返回的任务。</param>
    /// <param name="state">本次停用记录。</param>
    /// <param name="hookName">日志用 Hook 名称。</param>
    /// <param name="combatState">当前战斗状态。</param>
    /// <param name="side">当前结算阵营。</param>
    /// <returns>包装后的任务。</returns>
    private static async Task RestorePlayerHooksAndClearContextAfter(Task originalTask, RabbitHookSuppressionState state, string hookName, ICombatState combatState, CombatSide side)
    {
        try
        {
            await RestorePlayerHooksAfter(originalTask, state, hookName);
        }
        finally
        {
            if (side == CombatSide.Player)
            {
                RabbitTurnSkipContext.ForceClear(combatState, "玩家侧 AfterTurnEnd 完成");
            }
        }
    }
}
