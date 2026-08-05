using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 牌堆命令兼容工具：适配游戏新版生成牌命令从 bool 改为 Player? 的接口。
/// </summary>
public static class CardPileCmdHelper
{
    /// <summary>
    /// 将生成的单张牌加入战斗牌堆。
    /// </summary>
    /// <param name="card">要加入的生成牌。</param>
    /// <param name="pileType">目标牌堆。</param>
    /// <param name="addedByPlayer">旧版是否由玩家生成的标记。</param>
    /// <param name="position">插入位置。</param>
    /// <returns>加入结果。</returns>
    public static Task<CardPileAddResult> AddGeneratedCardToCombat(CardModel card, PileType pileType, bool addedByPlayer, CardPilePosition position = CardPilePosition.Bottom)
    {
        return CardPileCmd.AddGeneratedCardToCombat(card, pileType, GetCreator(card, addedByPlayer), position);
    }

    /// <summary>
    /// 将生成的多张牌加入战斗牌堆。
    /// </summary>
    /// <param name="cards">要加入的生成牌。</param>
    /// <param name="pileType">目标牌堆。</param>
    /// <param name="addedByPlayer">旧版是否由玩家生成的标记。</param>
    /// <param name="position">插入位置。</param>
    /// <returns>加入结果。</returns>
    public static Task<IReadOnlyList<CardPileAddResult>> AddGeneratedCardsToCombat(IEnumerable<CardModel> cards, PileType pileType, bool addedByPlayer, CardPilePosition position = CardPilePosition.Bottom)
    {
        List<CardModel> cardList = cards.ToList();
        return CardPileCmd.AddGeneratedCardsToCombat(cardList, pileType, GetCreator(cardList.FirstOrDefault(), addedByPlayer), position);
    }

    /// <summary>
    /// 将状态牌加入指定目标的战斗牌堆并显示预览。
    /// </summary>
    /// <typeparam name="T">要生成的卡牌类型。</typeparam>
    /// <param name="target">目标单位。</param>
    /// <param name="pileType">目标牌堆。</param>
    /// <param name="count">生成数量。</param>
    /// <param name="addedByPlayer">旧版是否由玩家生成的标记。</param>
    /// <param name="position">插入位置。</param>
    /// <returns>异步任务。</returns>
    public static Task AddToCombatAndPreview<T>(Creature target, PileType pileType, int count, bool addedByPlayer, CardPilePosition position = CardPilePosition.Bottom)
        where T : CardModel
    {
        return CardPileCmd.AddToCombatAndPreview<T>(target, pileType, count, GetCreator(target, addedByPlayer), position);
    }

    /// <summary>
    /// 将状态牌加入多个目标的战斗牌堆并显示预览。
    /// </summary>
    /// <typeparam name="T">要生成的卡牌类型。</typeparam>
    /// <param name="targets">目标单位列表。</param>
    /// <param name="pileType">目标牌堆。</param>
    /// <param name="count">生成数量。</param>
    /// <param name="addedByPlayer">旧版是否由玩家生成的标记。</param>
    /// <param name="position">插入位置。</param>
    /// <returns>异步任务。</returns>
    public static Task AddToCombatAndPreview<T>(IEnumerable<Creature> targets, PileType pileType, int count, bool addedByPlayer, CardPilePosition position = CardPilePosition.Bottom)
        where T : CardModel
    {
        List<Creature> targetList = targets.ToList();
        return CardPileCmd.AddToCombatAndPreview<T>(targetList, pileType, count, GetCreator(targetList.FirstOrDefault(), addedByPlayer), position);
    }

    /// <summary>
    /// 按旧版 bool 标记推断生成者。
    /// </summary>
    /// <param name="card">生成牌。</param>
    /// <param name="addedByPlayer">是否由玩家生成。</param>
    /// <returns>生成者玩家；非玩家生成时返回 null。</returns>
    private static Player? GetCreator(CardModel? card, bool addedByPlayer)
    {
        return addedByPlayer ? card?.Owner : null;
    }

    /// <summary>
    /// 按旧版 bool 标记从目标单位推断生成者。
    /// </summary>
    /// <param name="target">目标单位。</param>
    /// <param name="addedByPlayer">是否由玩家生成。</param>
    /// <returns>生成者玩家；非玩家生成时返回 null。</returns>
    private static Player? GetCreator(Creature? target, bool addedByPlayer)
    {
        return addedByPlayer ? target?.Player ?? target?.PetOwner : null;
    }
}
