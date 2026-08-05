using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 刀龙黑气辅助类：负责判断可加持牌，并把黑手帮和泰山压顶牌替换为甘文崔山。
/// </summary>
public static class DaoLongBlackQiHelper
{
    /// <summary>
    /// 判断玩家主牌组里是否存在刀龙黑气可替换的目标牌。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>存在目标牌时返回 true。</returns>
    public static bool HasEligibleCards(Player? player)
    {
        return player?.Deck?.Cards.Any(IsEligibleCard) == true;
    }

    /// <summary>
    /// 将玩家主牌组中的目标牌全部替换为甘文崔山，并继承原牌升级状态。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>成功替换的牌数量。</returns>
    public static async Task<int> TransformDeck(Player player)
    {
        if (player.Deck == null)
        {
            MainFile.Logger.Info("【刀龙黑气】玩家牌组为空，无法执行替换。");
            return 0;
        }

        List<CardModel> targets = player.Deck.Cards
            .Where(IsEligibleCard)
            .ToList();

        if (targets.Count == 0)
        {
            MainFile.Logger.Info("【刀龙黑气】未找到可加持的目标牌。");
            return 0;
        }

        int transformedCount = 0;
        foreach (CardModel oldCard in targets)
        {
            CardModel? newCard = CreateReplacementCard(player, oldCard);
            if (newCard == null)
            {
                MainFile.Logger.Info($"【刀龙黑气】找不到替换牌：原牌={oldCard.GetType().Name}");
                continue;
            }

            string oldTitle = oldCard.Title;
            string newTitle = newCard.Title;
            if (oldCard.IsUpgraded && newCard.IsUpgradable)
            {
                CardCmd.Upgrade(newCard);
            }

            await CardCmd.Transform(oldCard, newCard);
            transformedCount++;
            MainFile.Logger.Info($"【刀龙黑气】替换完成：{oldTitle} -> {newTitle}，继承升级={oldCard.IsUpgraded}");
        }

        MainFile.Logger.Info($"【刀龙黑气】本次共替换 {transformedCount} 张牌。");
        return transformedCount;
    }

    /// <summary>
    /// 判断一张牌是否是刀龙黑气的目标牌。
    /// </summary>
    /// <param name="card">待检查卡牌。</param>
    /// <returns>是目标牌时返回 true。</returns>
    private static bool IsEligibleCard(CardModel card)
    {
        return card.GetType() == typeof(BlackHandChow)
               || card.GetType() == typeof(BlackHandRatso)
               || card.GetType() == typeof(BlackHandAhFen)
               || card.GetType() == typeof(TaiShanPress);
    }

    /// <summary>
    /// 根据原牌创建对应的甘文崔山新牌。
    /// </summary>
    /// <param name="player">卡牌所属玩家。</param>
    /// <param name="oldCard">原牌。</param>
    /// <returns>可放入牌组的新牌；找不到映射时返回 null。</returns>
    private static CardModel? CreateReplacementCard(Player player, CardModel oldCard)
    {
        Type? replacementType = GetReplacementType(oldCard);
        if (replacementType == null)
        {
            return null;
        }

        CardModel? prototype = ModelDb.AllCards.FirstOrDefault(card => card.GetType() == replacementType);
        return prototype == null ? null : player.RunState.CreateCard(prototype, player);
    }

    /// <summary>
    /// 获取原牌对应的替换牌类型。
    /// </summary>
    /// <param name="oldCard">原牌。</param>
    /// <returns>替换牌类型；无映射时返回 null。</returns>
    private static Type? GetReplacementType(CardModel oldCard)
    {
        if (oldCard.GetType() == typeof(BlackHandChow))
        {
            return typeof(Gan);
        }

        if (oldCard.GetType() == typeof(BlackHandRatso))
        {
            return typeof(Wen);
        }

        if (oldCard.GetType() == typeof(BlackHandAhFen))
        {
            return typeof(Cui);
        }

        if (oldCard.GetType() == typeof(TaiShanPress))
        {
            return typeof(Shan);
        }

        return null;
    }
}
