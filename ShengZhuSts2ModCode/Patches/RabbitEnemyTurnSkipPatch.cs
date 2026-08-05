using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 卯兔敌方回合跳过补丁：
/// 在官方敌方回合开始 Hook 完成后执行卯兔登记的同步跳过，避免联机时本地 EndTurn 回调不可靠。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart))]
public static class RabbitEnemyTurnSkipPatch
{
    /// <summary>
    /// 包装原 Hook 任务：等原本的回合开始效果全部完成后，再处理卯兔跳过敌人行动。
    /// </summary>
    private static void Postfix(ref Task __result, ICombatState combatState, CombatSide side, IReadOnlyList<Creature> participants)
    {
        __result = ExecuteAfterOriginalHook(__result, combatState, side);
    }

    /// <summary>
    /// 等待原始 Hook 完成，并在敌方回合开始时处理卯兔待跳过记录。
    /// </summary>
    /// <param name="originalTask">原始 Hook 任务。</param>
    /// <param name="combatState">当前战斗状态。</param>
    /// <param name="side">当前回合阵营。</param>
    private static async Task ExecuteAfterOriginalHook(Task originalTask, ICombatState combatState, CombatSide side)
    {
        await originalTask;
        await RabbitEnemyTurnSkipService.ExecutePendingSkipAfterSideTurnStart(combatState, side);
    }
}
