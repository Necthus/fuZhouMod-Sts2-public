using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 卯兔敌方回合跳过服务：
/// 先登记本场战斗下一次敌方回合需要跳过敌人，再在官方敌方回合开始 Hook 中执行同步眩晕命令。
/// </summary>
public static class RabbitEnemyTurnSkipService
{
    /// <summary>
    /// 按战斗状态保存已登记卯兔跳过效果的玩家，避免联机时依赖本地回调导致不同步。
    /// </summary>
    private static readonly Dictionary<ICombatState, HashSet<Player>> PendingPlayersByCombat = [];

    /// <summary>
    /// 登记下一次敌方回合跳过敌人行动。
    /// </summary>
    /// <param name="player">使用卯兔的玩家。</param>
    /// <param name="combatState">当前战斗状态。</param>
    public static void Register(Player player, ICombatState combatState)
    {
        if (!PendingPlayersByCombat.TryGetValue(combatState, out HashSet<Player>? players))
        {
            players = [];
            PendingPlayersByCombat[combatState] = players;
        }

        if (!players.Add(player))
        {
            MainFile.Logger.Info($"【卯兔】重复登记敌方回合跳过：玩家={player.NetId}，当前登记玩家数={players.Count}。");
            return;
        }

        MainFile.Logger.Info($"【卯兔】已登记下一次敌方回合跳过敌人行动：玩家={player.NetId}，当前登记玩家数={players.Count}，回合={combatState.RoundNumber}。");
    }

    /// <summary>
    /// 在敌方回合开始后执行所有待处理的卯兔跳过效果。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <param name="side">当前回合阵营。</param>
    public static async Task ExecutePendingSkipAfterSideTurnStart(ICombatState combatState, CombatSide side)
    {
        if (side != CombatSide.Enemy)
        {
            return;
        }

        if (!PendingPlayersByCombat.TryGetValue(combatState, out HashSet<Player>? players) || players.Count == 0)
        {
            return;
        }

        List<Player> sourcePlayers = players.ToList();
        PendingPlayersByCombat.Remove(combatState);

        string playerIds = string.Join(",", sourcePlayers.Select(player => player.NetId.ToString()));
        MainFile.Logger.Info($"【卯兔】敌方回合开始，准备执行卯兔跳过：登记玩家数={sourcePlayers.Count}，玩家={playerIds}，回合={combatState.RoundNumber}。");

        try
        {
            await SkipAllEnemyMovesOnce(combatState, playerIds);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"【卯兔】敌方回合跳过发生未预期异常，已清理登记并放弃本次跳过以避免卡死：玩家={playerIds}，异常={ex}");
        }
    }

    /// <summary>
    /// 清理指定战斗的待跳过记录，避免战斗异常结束后残留。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <param name="reason">清理原因。</param>
    public static void ForceClear(ICombatState? combatState, string reason)
    {
        if (combatState == null || !PendingPlayersByCombat.Remove(combatState, out HashSet<Player>? players))
        {
            return;
        }

        MainFile.Logger.Info($"【卯兔】清理敌方回合跳过登记：原因={reason}，清理玩家数={players.Count}。");
    }

    /// <summary>
    /// 跳过当前战斗中所有存活敌人的当前行动。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <param name="sourcePlayerIds">触发卯兔的玩家ID日志文本。</param>
    private static async Task SkipAllEnemyMovesOnce(ICombatState combatState, string sourcePlayerIds)
    {
        List<Creature> enemies = combatState.Enemies
            .Where(creature => creature.IsAlive && !creature.IsDead && creature.Monster != null)
            .ToList();

        if (enemies.Count == 0)
        {
            MainFile.Logger.Info($"【卯兔】敌方回合跳过结束：没有可处理的存活敌人，玩家={sourcePlayerIds}。");
            return;
        }

        int skippedCount = 0;
        int keptTemporaryCount = 0;
        int stunnedCount = 0;
        int failedCount = 0;
        foreach (Creature enemy in enemies)
        {
            EnemyIntentControlResult result = await EnemyIntentControlHelper.TrySkipEnemyMoveOnce(enemy, EnemyIntentControlHelper.RabbitSkipMoveId, "卯兔");
            switch (result)
            {
                case EnemyIntentControlResult.SkipMove:
                    skippedCount++;
                    break;
                case EnemyIntentControlResult.KeptTemporaryMove:
                    keptTemporaryCount++;
                    break;
                case EnemyIntentControlResult.StunFallback:
                    stunnedCount++;
                    break;
                case EnemyIntentControlResult.Failed:
                    failedCount++;
                    break;
            }
        }

        MainFile.Logger.Info($"【卯兔】敌方回合跳过完成：玩家={sourcePlayerIds}，目标={enemies.Count}，空行动成功={skippedCount}，保留临时行动={keptTemporaryCount}，同步眩晕成功={stunnedCount}，失败={failedCount}。");
    }
}
