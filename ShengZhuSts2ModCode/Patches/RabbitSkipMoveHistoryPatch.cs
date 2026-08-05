using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 空行动历史记录补丁：
/// 本 Mod 会用空 MoveState 吃掉敌人的实际行动，该空行动只服务于回合流程，不应进入战斗历史。
/// </summary>
[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.MonsterPerformedMove))]
public static class RabbitSkipMoveHistoryPatch
{
    /// <summary>
    /// 前置补丁：如果是本 Mod 的空行动，则阻止写入战斗历史。
    /// </summary>
    public static bool Prefix(CombatState? combatState, MonsterModel? monster, MoveState? move, IEnumerable<Creature>? targets)
    {
        if (move == null || !EnemyIntentControlHelper.IsSkipMoveId(move.Id))
        {
            return true;
        }

        MainFile.Logger.Info($"【敌人行动控制】跳过空行动历史记录：敌人={monster?.Id.Entry ?? "空"}，回合={combatState?.RoundNumber.ToString() ?? "空"}，空行动={move.Id}，目标数量={GetKnownTargetCount(targets)}。");
        return false;
    }

    /// <summary>
    /// 获取日志用目标数量；如果目标是延迟枚举，不为了日志强制枚举。
    /// </summary>
    /// <param name="targets">目标集合。</param>
    /// <returns>已知数量；未知时返回 -1。</returns>
    private static int GetKnownTargetCount(IEnumerable<Creature>? targets)
    {
        return targets switch
        {
            null => 0,
            IReadOnlyCollection<Creature> readOnlyCollection => readOnlyCollection.Count,
            ICollection<Creature> collection => collection.Count,
            _ => -1
        };
    }
}
