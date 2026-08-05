using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 刀龙黑气事件战斗辅助类：只从第一层精英池中选择遭遇，并负责进入事件内精英战。
/// </summary>
public static class DaoLongEventCombatHelper
{
    /// <summary>
    /// 第一层章节下标。
    /// </summary>
    private const int FirstActIndex = 0;

    /// <summary>
    /// 判断当前玩家是否存在可用于刀龙事件的一层精英遭遇。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>存在一层精英遭遇时返回 true。</returns>
    public static bool HasFirstActEliteEncounters(Player? player)
    {
        return GetFirstActEliteEncounters(player).Count > 0;
    }

    /// <summary>
    /// 进入一场第一层精英战斗；战斗胜利后不恢复事件页，并正常结算精英奖励。
    /// </summary>
    /// <param name="player">触发事件的玩家。</param>
    /// <returns>成功进入战斗时返回 true。</returns>
    public static async Task<bool> StartFirstActEliteCombat(Player player)
    {
        if (!SharedEventVoteHelper.ShouldExecuteLocalGlobalAction(player, "刀龙黑气事件"))
        {
            return true;
        }

        Player? canonicalPlayer = SharedEventVoteHelper.GetCanonicalPlayer(player);
        List<EncounterModel> eliteEncounters = GetFirstActEliteEncounters(canonicalPlayer);
        if (eliteEncounters.Count == 0)
        {
            MainFile.Logger.Info($"【刀龙黑气事件】没有可用的一层精英战：玩家={player.NetId}/{player.Character.Id.Entry}。");
            return false;
        }

        EncounterModel? selectedEncounter = StableRandomHelper.PickByStableHash(
            canonicalPlayer,
            eliteEncounters,
            "DaoLongEventCombatHelper.FirstActEliteCombat",
            encounter => encounter.Id.Entry);
        if (selectedEncounter == null)
        {
            MainFile.Logger.Info($"【刀龙黑气事件】稳定随机未选出一层精英战：候选数量={eliteEncounters.Count}。");
            return false;
        }

        EncounterModel encounter = selectedEncounter.ToMutable();
        CombatRoom combatRoom = new(encounter, player.RunState)
        {
            ShouldResumeParentEventAfterCombat = false
        };

        RecordCombatReplayInitialStateIfNeeded();
        MainFile.Logger.Info($"【联机同步】来源=刀龙黑气事件，玩家={player.NetId}/{player.Character.Id.Entry}，动作=进入一层精英战，候选数={eliteEncounters.Count}，候选ID=[{string.Join(",", eliteEncounters.Select(elite => elite.Id.Entry))}]，选中={encounter.Id.Entry}，随机源=StableRandomHelper，战后恢复父事件=false。");
        await RunManager.Instance.EnterRoomWithoutExitingCurrentRoom(combatRoom, fadeToBlack: true);
        return true;
    }

    /// <summary>
    /// 获取第一层精英遭遇，并按遭遇 ID 稳定排序。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>稳定排序后的一层精英遭遇列表。</returns>
    private static List<EncounterModel> GetFirstActEliteEncounters(Player? player)
    {
        if (player?.RunState?.Acts == null || player.RunState.Acts.Count <= FirstActIndex)
        {
            return [];
        }

        return player.RunState.Acts[FirstActIndex].AllEliteEncounters
            .OrderBy(encounter => encounter.Id.Entry, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 事件内手动进入战斗时补录战斗回放初始状态，避免出牌时回放记录器缺少初始快照。
    /// </summary>
    private static void RecordCombatReplayInitialStateIfNeeded()
    {
        if (!RunManager.Instance.CombatReplayWriter.IsEnabled)
        {
            MainFile.Logger.Info("【刀龙黑气事件】战斗回放记录器未启用，跳过补录初始状态。");
            return;
        }

        RunManager.Instance.CombatReplayWriter.RecordInitialState(RunManager.Instance.ToSave(null));
        MainFile.Logger.Info("【刀龙黑气事件】已为事件精英战斗补录回放初始状态。");
    }
}
