using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 左右互搏：抽2/3张，选择1张手牌丢弃，再选择1张手牌放回抽牌堆顶。
/// </summary>
public class Ambidexterity : TeamJackieCard
{
    /// <summary>
    /// 构造卡牌数值：1费技能，抽2/3。
    /// </summary>
    public Ambidexterity() : base(1, CardType.Skill, TargetType.None)
    {
        WithCards(2, 1);
    }

    /// <summary>
    /// 出牌时执行抽牌、弃牌和放回牌堆顶流程。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.PlayerCombatState == null)
        {
            return;
        }

        await CommonActions.Draw(this, choiceContext);
        if (Owner.PlayerCombatState.Hand.Cards.Count == 0)
        {
            return;
        }

        IReadOnlyList<CardModel> discardSelection = (await CommonActions.SelectCards(
            this,
            new LocString("cards", Id.Entry + ".discardPrompt"),
            choiceContext,
            PileType.Hand,
            1)).ToList();
        CardModel? discardCard = discardSelection.FirstOrDefault();
        if (discardCard != null)
        {
            await CardCmd.Discard(choiceContext, discardCard);
        }

        if (Owner.PlayerCombatState.Hand.Cards.Count == 0)
        {
            return;
        }

        IReadOnlyList<CardModel> topSelection = (await CommonActions.SelectCards(
            this,
            new LocString("cards", Id.Entry + ".topDeckPrompt"),
            choiceContext,
            PileType.Hand,
            1)).ToList();
        CardModel? topCard = topSelection.FirstOrDefault();
        if (topCard != null)
        {
            await CardPileCmd.Add(topCard, PileType.Draw, CardPilePosition.Top);
        }
    }
}
