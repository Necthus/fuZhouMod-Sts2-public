using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 牌组联机同步辅助：用稳定下标和卡牌ID在远端定位同一张牌。
/// </summary>
internal static class DeckSyncHelper
{
    /// <summary>
    /// 获取卡牌在玩家牌组中的当前下标。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="card">目标卡牌。</param>
    /// <returns>找到时返回下标，否则返回 -1。</returns>
    public static int GetDeckIndex(Player player, CardModel card)
    {
        return player.Deck.Cards.ToList().IndexOf(card);
    }

    /// <summary>
    /// 按同步信息定位牌组里的目标牌，并校验ID与升级状态。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="deckIndex">目标牌组下标。</param>
    /// <param name="expectedCardId">预期卡牌ID。</param>
    /// <param name="expectedUpgraded">预期升级状态。</param>
    /// <param name="source">日志来源。</param>
    /// <param name="card">定位到的卡牌。</param>
    /// <returns>定位且校验成功时返回 true。</returns>
    public static bool TryGetDeckCard(Player player, int deckIndex, string expectedCardId, bool expectedUpgraded, string source, out CardModel? card)
    {
        card = null;
        List<CardModel> deckCards = player.Deck.Cards.ToList();
        if (deckIndex < 0 || deckIndex >= deckCards.Count)
        {
            MainFile.Logger.Info($"【联机异常兜底】【{source}】牌组定位失败：玩家={player.NetId}，下标={deckIndex}，牌组数量={deckCards.Count}，处理=跳过。");
            return false;
        }

        CardModel candidate = deckCards[deckIndex];
        if (candidate.Id.Entry != expectedCardId || candidate.IsUpgraded != expectedUpgraded)
        {
            MainFile.Logger.Info($"【联机异常兜底】【{source}】牌组校验失败：玩家={player.NetId}，下标={deckIndex}，预期={expectedCardId}{(expectedUpgraded ? "+" : string.Empty)}，实际={candidate.Id.Entry}{(candidate.IsUpgraded ? "+" : string.Empty)}，处理=跳过。");
            return false;
        }

        card = candidate;
        return true;
    }

    /// <summary>
    /// 按卡牌ID查找模型库中的卡牌原型。
    /// </summary>
    /// <param name="cardId">卡牌ID。</param>
    /// <returns>找到的卡牌原型；找不到时返回 null。</returns>
    public static CardModel? FindCardPrototype(string cardId)
    {
        return ModelDb.AllCards.FirstOrDefault(card => card.Id.Entry == cardId);
    }
}
