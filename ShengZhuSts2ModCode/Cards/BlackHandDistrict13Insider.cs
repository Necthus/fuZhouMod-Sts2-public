using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 十三区内奸：查看牌库顶若干张牌，可丢弃任意张；弃牌得格挡，保留则给全体叠黑手。
public class BlackHandDistrict13Insider : BlackHandGangCard
{
    // 构造卡牌数值：1 费，查看牌库顶 4 张，升级后额外 +2 张。
    public BlackHandDistrict13Insider() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("Magic", 4, 2);
        WithVar("BlockPerDiscard", 2);
    }

    // 出牌时从牌库顶选出要丢弃的牌，再按保留张数对所有敌人施加黑手。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var playerCombatState = Owner.PlayerCombatState;
        if (playerCombatState == null)
        {
            return;
        }

        int previewCount = Math.Min((int)DynamicVars["Magic"].BaseValue, playerCombatState.DrawPile.Cards.Count);
        if (previewCount <= 0)
        {
            return;
        }

        List<CardModel> previewCards = playerCombatState.DrawPile.Cards.Take(previewCount).ToList();
        CardSelectorPrefs prefs = new CardSelectorPrefs(
            new LocString("cards", Id.Entry + ".selectionScreenPrompt"),
            0,
            previewCards.Count);
        IReadOnlyList<CardModel> discardedCards = (await CardSelectCmd.FromSimpleGrid(choiceContext, previewCards, Owner, prefs)).ToList();

        foreach (CardModel discardedCard in discardedCards)
        {
            await CardPileCmd.Add(discardedCard, PileType.Discard);
        }

        int discardedCount = discardedCards.Count;
        if (discardedCount > 0)
        {
            await CreatureCmd.GainBlock(Owner.Creature, discardedCount * (int)DynamicVars["BlockPerDiscard"].BaseValue, ValueProp.Move, cardPlay);
        }

        int keptCount = previewCount - discardedCount;
        if (keptCount > 0)
        {
            foreach (Creature creature in GetLivingOpponents())
            {
                await ApplyBlackHand(choiceContext, creature, keptCount);
            }
        }

        await ResolveBountyRewards(choiceContext);
    }
}
