using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Curses.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

/// <summary>
/// 老爹诅咒税服务：按玩家当前手牌统计未支付的布莱克和特鲁诅咒，避免用全局费用层数串状态。
/// </summary>
internal static class GrandMageCurseTaxService
{
    /// <summary>
    /// 统计指定玩家当前手牌中未支付的指定诅咒数量。
    /// </summary>
    /// <typeparam name="TCurse">诅咒类型。</typeparam>
    /// <param name="player">玩家。</param>
    /// <returns>未支付诅咒数量。</returns>
    public static int CountPending<TCurse>(Player? player)
        where TCurse : GrandMageDadCurseCard
    {
        if (player?.PlayerCombatState == null)
        {
            return 0;
        }

        int count = 0;
        foreach (CardModel card in player.PlayerCombatState.Hand.Cards)
        {
            if (card is TCurse { IsTaxPending: true })
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 将指定玩家当前手牌中所有未支付的指定诅咒标记为已支付。
    /// </summary>
    /// <typeparam name="TCurse">诅咒类型。</typeparam>
    /// <param name="player">玩家。</param>
    /// <param name="reason">支付原因。</param>
    /// <returns>实际支付的诅咒数量。</returns>
    public static int MarkPendingAsPaid<TCurse>(Player? player, string reason)
        where TCurse : GrandMageDadCurseCard
    {
        if (player?.PlayerCombatState == null)
        {
            return 0;
        }

        int count = 0;
        foreach (CardModel card in player.PlayerCombatState.Hand.Cards)
        {
            if (card is not TCurse { IsTaxPending: true } curse)
            {
                continue;
            }

            curse.MarkTaxPaidWithoutRefresh(reason);
            count++;
        }

        RefreshPowers(player);
        return count;
    }

    /// <summary>
    /// 刷新玩家身上诅咒税 Power 的显示层数。
    /// </summary>
    /// <param name="player">玩家。</param>
    public static void RefreshPowers(Player? player)
    {
        if (player?.Creature == null)
        {
            return;
        }

        player.Creature.GetPower<GrandMageCurseAttackTaxPower>()?.RefreshAmountFromHand();
        player.Creature.GetPower<GrandMageCurseSkillTaxPower>()?.RefreshAmountFromHand();
    }

    /// <summary>
    /// 判断卡牌是否属于指定玩家。
    /// </summary>
    /// <param name="player">玩家。</param>
    /// <param name="card">卡牌。</param>
    /// <returns>属于该玩家时返回 true。</returns>
    public static bool IsOwnedBy(Player? player, CardModel? card)
    {
        return player != null
               && card?.Owner != null
               && (ReferenceEquals(card.Owner, player) || card.Owner.NetId == player.NetId);
    }

}
