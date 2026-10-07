using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 独木桥：消耗任意张手牌，自动打出抽牌堆顶牌对应次数，升级后次数+1。
/// </summary>
public class SinglePlankBridge : TeamJackieCard
{
    /// <summary>
    /// 构造卡牌数值：2费技能。
    /// </summary>
    public SinglePlankBridge() : base(2, CardType.Skill, TargetType.None)
    {
    }

    /// <summary>
    /// 出牌时选择任意手牌消耗，并按数量自动打出抽牌堆顶牌。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var state = Owner?.PlayerCombatState;
        if (Owner == null || state == null)
        {
            return;
        }

        List<CardModel> selectableCards = state.Hand.Cards.Where(card => !ReferenceEquals(card, this)).ToList();
        if (selectableCards.Count == 0)
        {
            return;
        }

        CardSelectorPrefs prefs = new(
            new LocString("cards", Id.Entry + ".selectionScreenPrompt"),
            0,
            selectableCards.Count)
        {
            RequireManualConfirmation = true
        };
        IReadOnlyList<CardModel> selectedCards = (await CardSelectCmd.FromSimpleGrid(choiceContext, selectableCards, Owner, prefs)).ToList();

        foreach (CardModel selectedCard in selectedCards)
        {
            await CardCmd.Exhaust(choiceContext, selectedCard);
        }

        int playCount = selectedCards.Count + (IsUpgraded ? 1 : 0);
        if (playCount <= 0 || state.DrawPile.Cards.Count == 0)
        {
            return;
        }

        CardModel topCard = state.DrawPile.Cards.First();
        for (int i = 0; i < playCount; i++)
        {
            CardModel cardToPlay = i == playCount - 1 ? topCard : topCard.CreateDupe(Owner);
            if (i < playCount - 1)
            {
                cardToPlay.ExhaustOnNextPlay = true;
                await CardPileCmdHelper.AddGeneratedCardToCombat(cardToPlay, PileType.Hand, true);
            }
            else
            {
                await CardPileCmd.Add(cardToPlay, PileType.Hand, CardPilePosition.Top);
            }

            await CardCmd.AutoPlay(choiceContext, cardToPlay, null);
        }
    }
}
