using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;

/// <summary>
/// 黑影女王：将消耗堆中所有黑影兵团牌按顺序释放（自动打出），基础版随后随机移除其中2张。
/// 升级后费用2→1，并且不再移除释放后的黑影兵团牌。
/// </summary>
public class ShadowQueen : BaseMaskSupportCard
{
    /// <summary>
    /// 基础版释放后随机移除的黑影兵团数量。
    /// </summary>
    private const int BaseRemoveCount = 2;

    /// <summary>
    /// 升级版释放后不再随机移除黑影兵团牌。
    /// </summary>
    private const int UpgradedRemoveCount = 0;

    /// <summary>
    /// 构造卡牌数值：2费技能。升级后费用降为1。
    /// </summary>
    public ShadowQueen() : base(2, CardType.Skill, CardRarity.Rare, TargetType.None)
    {
    }

    /// <summary>
    /// 升级时降低费用。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时将消耗堆中所有黑影兵团牌按顺序自动打出，然后从本次释放过的牌中随机移除一部分。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var playerCombatState = Owner?.PlayerCombatState;
        if (playerCombatState == null)
        {
            return;
        }

        // 收集消耗堆中所有黑影兵团牌
        var shadowKhanCards = playerCombatState.ExhaustPile.Cards
            .Where(c => c is BaseShadowKhanCard)
            .ToList();

        // 按本体刀刃陷阱的节奏自动打出：第一张保留牌堆视觉，后续跳过牌堆飞行动画。
        bool showCardPileVisuals = true;
        foreach (var card in shadowKhanCards)
        {
            await CardCmd.AutoPlay(choiceContext, card, null, skipCardPileVisuals: !showCardPileVisuals);
            showCardPileVisuals = false;
        }

        MainFile.Logger.Info($"【黑影女王】自动释放黑影兵团完成：数量={shadowKhanCards.Count}，仅首张保留牌堆视觉。");

        await RemoveRandomReleasedShadowKhanCards(shadowKhanCards);
    }

    /// <summary>
    /// 从本次释放后仍在消耗堆中的黑影兵团牌里随机移除，并先预览被移除的牌作为演出提示。
    /// 升级版移除数量为0，直接跳过，避免额外消耗联机稳定随机数。
    /// </summary>
    private async Task RemoveRandomReleasedShadowKhanCards(IReadOnlyList<CardModel> releasedCards)
    {
        int targetRemoveCount = IsUpgraded ? UpgradedRemoveCount : BaseRemoveCount;
        if (targetRemoveCount <= 0)
        {
            MainFile.Logger.Info("【黑影女王】升级版释放后不移除黑影兵团牌。");
            return;
        }

        var removableCards = releasedCards
            .Where(card => card.Pile?.Type == PileType.Exhaust)
            .ToList();
        if (removableCards.Count == 0)
        {
            MainFile.Logger.Info("【黑影女王】释放后没有可移除的黑影兵团牌。");
            return;
        }

        int actualRemoveCount = Math.Min(targetRemoveCount, removableCards.Count);
        var cardsToRemove = PickRandomCards(removableCards, actualRemoveCount);

        var previewTasks = cardsToRemove
            .Select(card => CardCmd.Preview(card, 1.2f, CardPreviewStyle.HorizontalLayout)?.Task)
            .Where(task => task != null)
            .Cast<Task>()
            .ToList();
        if (previewTasks.Count > 0)
        {
            await Task.WhenAll(previewTasks);
        }

        await CardPileCmd.RemoveFromCombat(cardsToRemove);

        string removedNames = string.Join("、", cardsToRemove.Select(card => card.Id.Entry));
        MainFile.Logger.Info($"【黑影女王】释放后随机移除黑影兵团：目标移除={targetRemoveCount}，实际移除={actualRemoveCount}，升级={IsUpgraded}，移除={removedNames}");
    }

    /// <summary>
    /// 从候选牌中无放回随机取指定数量，使用本局随机源避免联机两端移除列表不同。
    /// </summary>
    /// <param name="cards">候选牌。</param>
    /// <param name="count">需要取出的数量。</param>
    /// <returns>随机取出的牌。</returns>
    private List<CardModel> PickRandomCards(List<CardModel> cards, int count)
    {
        List<CardModel> pool = cards.ToList();
        List<CardModel> selected = [];
        while (selected.Count < count && pool.Count > 0)
        {
            int index = StableRandomHelper.NextInt(Owner, pool.Count);
            selected.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return selected;
    }
}
