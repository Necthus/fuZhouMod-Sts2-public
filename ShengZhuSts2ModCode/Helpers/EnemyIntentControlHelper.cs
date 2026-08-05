using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 敌人意图控制辅助类：统一处理跳过怪物当前行动的安全检查和同步命令。
/// </summary>
public static class EnemyIntentControlHelper
{
    /// <summary>
    /// 卯兔用于临时吃掉怪物行动的空行动ID。
    /// </summary>
    public const string RabbitSkipMoveId = "SHENGZHUSTS2MOD_RABBIT_SKIP";

    /// <summary>
    /// 高温蒸汽用于临时吃掉怪物行动的空行动ID。
    /// </summary>
    public const string SteamSkipMoveId = "SHENGZHUSTS2MOD_STEAM_SKIP";

    /// <summary>
    /// 恐惧用于临时吃掉怪物行动的空行动ID。
    /// </summary>
    public const string FearSkipMoveId = "SHENGZHUSTS2MOD_FEAR_SKIP";

    /// <summary>
    /// 判断指定行动ID是否是本 Mod 用来跳过怪物行动的空行动。
    /// </summary>
    /// <param name="moveId">待判断行动ID。</param>
    /// <returns>是空行动时返回 true。</returns>
    public static bool IsSkipMoveId(string? moveId)
    {
        return moveId == RabbitSkipMoveId
            || moveId == SteamSkipMoveId
            || moveId == FearSkipMoveId;
    }

    /// <summary>
    /// 通过官方命令跳过单个敌人的当前行动，避免直接改怪物状态机导致联机不同步。
    /// </summary>
    /// <param name="enemy">目标敌人。</param>
    /// <param name="skipMoveId">本次来源的空行动ID，仅用于日志和兼容历史过滤。</param>
    /// <param name="sourceLabel">日志来源标签。</param>
    /// <returns>处理结果。</returns>
    public static async Task<EnemyIntentControlResult> TrySkipEnemyMoveOnce(Creature enemy, string skipMoveId, string sourceLabel)
    {
        if (!CanProcessEnemy(enemy, out string reason))
        {
            MainFile.Logger.Info($"【{sourceLabel}】跳过敌人行动失败：敌人={GetCreatureLogName(enemy)}，原因={reason}。");
            return EnemyIntentControlResult.Failed;
        }

        try
        {
            var originalMove = enemy.Monster!.NextMove;
            if (IsSkipMoveId(originalMove.Id))
            {
                MainFile.Logger.Info($"【{sourceLabel}】敌人当前已经是空行动，跳过重复处理：敌人={GetCreatureLogName(enemy)}，当前行动={originalMove.Id}。");
                return EnemyIntentControlResult.KeptTemporaryMove;
            }

            if (!enemy.Monster.MoveStateMachine!.States.ContainsKey(originalMove.Id))
            {
                MainFile.Logger.Info($"【{sourceLabel}】敌人当前行动是未注册临时行动，保持原行动避免状态机崩溃：敌人={GetCreatureLogName(enemy)}，当前行动={originalMove.Id}。");
                return EnemyIntentControlResult.KeptTemporaryMove;
            }

            await CreatureCmd.Stun(enemy, null);
            MainFile.Logger.Info($"【{sourceLabel}】已用同步眩晕命令跳过敌人行动：敌人={GetCreatureLogName(enemy)}，原行动={originalMove.Id}，来源空行动={skipMoveId}。");
            return EnemyIntentControlResult.StunFallback;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"【{sourceLabel}】同步眩晕命令失败，已放弃该敌人以避免联机分叉：敌人={GetCreatureLogName(enemy)}，异常={ex}");
            return EnemyIntentControlResult.Failed;
        }
    }

    /// <summary>
    /// 检查敌人当前是否适合被行动跳过逻辑处理。
    /// </summary>
    /// <param name="enemy">目标敌人。</param>
    /// <param name="reason">不可处理原因。</param>
    /// <returns>可处理时返回 true。</returns>
    private static bool CanProcessEnemy(Creature? enemy, out string reason)
    {
        if (enemy == null)
        {
            reason = "敌人为空";
            return false;
        }

        if (!enemy.IsAlive || enemy.IsDead)
        {
            reason = "敌人已死亡或非存活状态";
            return false;
        }

        if (enemy.CombatState == null)
        {
            reason = "敌人不在战斗中";
            return false;
        }

        if (enemy.Monster == null)
        {
            reason = "敌人没有怪物模型";
            return false;
        }

        if (enemy.Monster.MoveStateMachine == null)
        {
            reason = "敌人行动状态机为空";
            return false;
        }

        if (enemy.Monster.NextMove == null)
        {
            reason = "敌人当前行动为空";
            return false;
        }

        reason = "";
        return true;
    }

    /// <summary>
    /// 获取日志用的敌人名称，避免日志本身因为空值报错。
    /// </summary>
    /// <param name="creature">目标生物。</param>
    /// <returns>日志名称。</returns>
    private static string GetCreatureLogName(Creature? creature)
    {
        if (creature == null)
        {
            return "空敌人";
        }

        return $"{creature.Name}/{creature.Monster?.Id.Entry ?? "无怪物ID"}";
    }
}

/// <summary>
/// 敌人行动控制结果。
/// </summary>
public enum EnemyIntentControlResult
{
    /// <summary>
    /// 空行动替换成功，保留给旧日志和历史兼容。
    /// </summary>
    SkipMove,

    /// <summary>
    /// 当前已经是未注册临时行动，保留原行动以避免怪物状态机找不到后续状态。
    /// </summary>
    KeptTemporaryMove,

    /// <summary>
    /// 同步眩晕命令成功。
    /// </summary>
    StunFallback,

    /// <summary>
    /// 空行动和降级均失败。
    /// </summary>
    Failed
}
