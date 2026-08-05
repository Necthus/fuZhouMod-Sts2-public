using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 共享事件投票辅助类：统一处理联机共享事件里的玩家筛选、条件判断、全局动作和个人动作本地兜底。
/// </summary>
public static class SharedEventVoteHelper
{
    /// <summary>
    /// 获取当前事件所在局内的所有玩家；拿不到局状态时退回当前事件持有者。
    /// </summary>
    /// <param name="owner">当前事件副本的持有者。</param>
    /// <returns>可用于共享事件判断的玩家列表。</returns>
    public static List<Player> GetRunPlayers(Player? owner)
    {
        if (owner?.RunState?.Players != null)
        {
            return owner.RunState.Players.ToList();
        }

        return owner == null ? [] : [owner];
    }

    /// <summary>
    /// 获取当前共享事件里需要参与指定规则判断的玩家。
    /// </summary>
    /// <param name="owner">当前事件副本的持有者。</param>
    /// <param name="predicate">玩家是否参与该规则。</param>
    /// <returns>参与该规则的玩家列表。</returns>
    public static List<Player> GetPlayersWhere(Player? owner, Func<Player, bool> predicate)
    {
        return GetRunPlayers(owner)
            .Where(predicate)
            .ToList();
    }

    /// <summary>
    /// 判断所有相关玩家是否都满足指定条件；没有相关玩家时返回 false，避免开放空选项。
    /// </summary>
    /// <param name="owner">当前事件副本的持有者。</param>
    /// <param name="participantPredicate">玩家是否属于该事件相关玩家。</param>
    /// <param name="conditionPredicate">玩家是否满足选项条件。</param>
    /// <returns>所有相关玩家都满足时返回 true。</returns>
    public static bool AllParticipantsMatch(Player? owner, Func<Player, bool> participantPredicate, Func<Player, bool> conditionPredicate)
    {
        List<Player> players = GetPlayersWhere(owner, participantPredicate);
        return players.Count > 0 && players.All(conditionPredicate);
    }

    /// <summary>
    /// 判断当前玩家是否应该执行共享事件中的全局动作；每台机器只让本地玩家副本执行一次。
    /// </summary>
    /// <param name="player">当前事件副本的持有者。</param>
    /// <param name="source">日志来源。</param>
    /// <returns>应该执行全局动作时返回 true。</returns>
    public static bool ShouldExecuteLocalGlobalAction(Player? player, string source)
    {
        if (player == null)
        {
            MainFile.Logger.Info($"【联机同步】来源={source}，动作=跳过共享事件全局动作，原因=玩家为空。");
            return false;
        }

        if (!LocalContext.IsMe(player))
        {
            MainFile.Logger.Info($"【联机同步】来源={source}，玩家={player.NetId}/{player.Character.Id.Entry}，动作=跳过共享事件全局动作，原因=非本地玩家副本。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 判断当前玩家是否应该执行共享事件中的个人动作；每台机器只让本地玩家副本结算自己的代价和奖励。
    /// </summary>
    /// <param name="player">当前事件副本的持有者。</param>
    /// <param name="source">日志来源。</param>
    /// <returns>应该执行个人动作时返回 true。</returns>
    public static bool ShouldExecuteLocalPlayerAction(Player? player, string source)
    {
        if (player == null)
        {
            MainFile.Logger.Info($"【联机同步】来源={source}，动作=跳过共享事件个人动作，原因=玩家为空。");
            return false;
        }

        if (!LocalContext.IsMe(player))
        {
            MainFile.Logger.Info($"【联机同步】来源={source}，玩家={player.NetId}/{player.Character.Id.Entry}，动作=跳过共享事件个人动作，原因=非本地玩家副本。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 获取共享事件用的稳定玩家；所有客户端都按玩家 NetId 取第一个，避免按本地玩家随机导致结果分叉。
    /// </summary>
    /// <param name="owner">当前事件副本的持有者。</param>
    /// <returns>稳定玩家；拿不到时返回当前持有者。</returns>
    public static Player? GetCanonicalPlayer(Player? owner)
    {
        return GetRunPlayers(owner)
            .OrderBy(player => player.NetId)
            .FirstOrDefault() ?? owner;
    }
}
