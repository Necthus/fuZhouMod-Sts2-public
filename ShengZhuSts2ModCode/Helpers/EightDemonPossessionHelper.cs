using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 八魔附体觉醒辅助类：潘库宝盒集齐八种魔气后，自动给玩家加入八魔附体。
/// </summary>
public static class EightDemonPossessionHelper
{
    /// <summary>
    /// 正在处理八魔附体觉醒的玩家，避免同一流程里重复加牌。
    /// </summary>
    private static readonly HashSet<Player> AwakeningPlayers = [];

    /// <summary>
    /// 检查玩家是否已通过潘库宝盒集齐八魔，若满足则加入八魔附体。
    /// </summary>
    /// <param name="player">要检查的玩家。</param>
    public static void CheckAndTriggerAwakening(Player? player)
    {
        if (player?.Deck?.Cards == null)
        {
            return;
        }

        PanKuBox? box = player.GetRelic<PanKuBox>();
        if (box == null)
        {
            return;
        }

        if (AwakeningPlayers.Contains(player) || HasEightDemonPossession(player))
        {
            return;
        }

        HashSet<PanKuDemonQiKind> ownedKinds = PanKuDemonQiHelper.CollectOwnedKinds(player, box);
        if (ownedKinds.Count < PanKuDemonQiHelper.AllKinds.Length)
        {
            MainFile.Logger.Info($"【八魔附体】尚未集齐八魔：当前={ownedKinds.Count}/{PanKuDemonQiHelper.AllKinds.Length}。");
            return;
        }

        TriggerAwakening(player);
    }

    /// <summary>
    /// 判断玩家主牌组中是否已经有八魔附体。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>已拥有时返回 true。</returns>
    private static bool HasEightDemonPossession(Player player)
    {
        return player.Deck.Cards.Any(card => card is EightDemonPossessionCard);
    }

    /// <summary>
    /// 执行八魔附体觉醒：不消耗任何恶魔来源，只额外加入一张彩蛋卡。
    /// </summary>
    /// <param name="player">触发觉醒的玩家。</param>
    private static void TriggerAwakening(Player player)
    {
        if (!AwakeningPlayers.Add(player))
        {
            return;
        }

        try
        {
            CardModel card = (CardModel)ModelDb.Card<EightDemonPossessionCard>().ToMutable();
            DeckMutationService.AddCardToDeck(player, card, false, "八魔附体觉醒");
            MainFile.Logger.Info("【八魔附体】潘库宝盒已集齐八种魔气，觉醒完成，八魔附体已加入牌组。");
        }
        finally
        {
            AwakeningPlayers.Remove(player);
        }
    }
}
