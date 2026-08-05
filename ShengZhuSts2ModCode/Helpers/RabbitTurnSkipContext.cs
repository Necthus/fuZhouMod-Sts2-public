using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 卯兔伪结束回合上下文：
/// 用于标记当前玩家回合结束是卯兔制造的伪结束，玩家侧回合结束模型钩子需要跳过。
/// </summary>
public static class RabbitTurnSkipContext
{
    /// <summary>
    /// 按战斗状态保存正在执行卯兔伪结束回合的玩家，避免联机时玩家之间互相覆盖上下文。
    /// </summary>
    private static readonly Dictionary<ICombatState, HashSet<Player>> ActivePlayersByCombat = [];

    /// <summary>
    /// 开启卯兔伪结束回合上下文。
    /// </summary>
    public static void Begin(Player player, ICombatState combatState)
    {
        if (!ActivePlayersByCombat.TryGetValue(combatState, out HashSet<Player>? players))
        {
            players = [];
            ActivePlayersByCombat[combatState] = players;
        }

        if (!players.Add(player))
        {
            MainFile.Logger.Info($"【卯兔】重复开启伪结束回合上下文，保持当前记录：玩家={player.NetId}。");
            return;
        }

        MainFile.Logger.Info($"【卯兔】开启伪结束回合上下文：玩家={player.NetId}，当前战斗卯兔玩家数={players.Count}。");
    }

    /// <summary>
    /// 关闭卯兔伪结束回合上下文。
    /// </summary>
    public static void End(Player player, ICombatState? combatState)
    {
        if (combatState == null)
        {
            return;
        }

        RemovePlayer(player, combatState, "正常关闭");
    }

    /// <summary>
    /// 强制清理卯兔上下文，用于异常兜底，避免伪结束标记残留到后续真实回合。
    /// </summary>
    public static void ForceClear(string reason)
    {
        int count = ActivePlayersByCombat.Sum(pair => pair.Value.Count);
        if (count <= 0)
        {
            return;
        }

        ActivePlayersByCombat.Clear();
        MainFile.Logger.Info($"【卯兔】强制清理全部伪结束回合上下文：原因={reason}，清理数量={count}。");
    }

    /// <summary>
    /// 强制清理指定玩家的卯兔上下文，用于敌方回合回调兜底，避免影响其他玩家。
    /// </summary>
    public static void ForceClear(Player player, ICombatState? combatState, string reason)
    {
        if (combatState == null)
        {
            return;
        }

        RemovePlayer(player, combatState, reason);
    }

    /// <summary>
    /// 强制清理指定战斗的全部卯兔上下文，用于玩家侧伪结束回合 Hook 完成后的收尾。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <param name="reason">清理原因。</param>
    public static void ForceClear(ICombatState? combatState, string reason)
    {
        if (combatState == null || !ActivePlayersByCombat.Remove(combatState, out HashSet<Player>? players))
        {
            return;
        }

        MainFile.Logger.Info($"【卯兔】清理本场伪结束回合上下文：原因={reason}，清理玩家数={players.Count}。");
    }

    /// <summary>
    /// 获取当前需要临时屏蔽回合结束 Hook 的卯兔玩家。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <param name="side">当前结算阵营。</param>
    /// <returns>需要屏蔽自身 Hook 的玩家列表。</returns>
    public static List<Player> GetPlayersToSuppressTurnEndHooks(ICombatState combatState, CombatSide side)
    {
        if (side != CombatSide.Player
            || !ActivePlayersByCombat.TryGetValue(combatState, out HashSet<Player>? players)
            || players.Count == 0)
        {
            return [];
        }

        return players
            .Where(player => player.Creature?.IsAlive == true)
            .ToList();
    }

    /// <summary>
    /// 从指定战斗状态中移除指定玩家的卯兔上下文。
    /// </summary>
    private static void RemovePlayer(Player player, ICombatState combatState, string reason)
    {
        if (!ActivePlayersByCombat.TryGetValue(combatState, out HashSet<Player>? players))
        {
            return;
        }

        if (!players.Remove(player))
        {
            MainFile.Logger.Info($"【卯兔】清理伪结束回合上下文时未找到玩家：原因={reason}，玩家={player.NetId}。");
            return;
        }

        if (players.Count == 0)
        {
            ActivePlayersByCombat.Remove(combatState);
        }

        MainFile.Logger.Info($"【卯兔】清理伪结束回合上下文：原因={reason}，玩家={player.NetId}，剩余玩家数={players.Count}。");
    }
}
