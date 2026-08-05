using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 黑手帮·拉苏：获得少量格挡后抽牌，再手动丢弃同等数量的牌。
public class BlackHandRatso : BlackHandGangCard
{
    // 构造卡牌数值：1 费，4 点格挡，抽 1 弃 1，升级后格挡 +2、抽弃数量 +1。
    public BlackHandRatso() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        WithBlock(4, 2);
        WithCards(1, 1);
    }

    // 出牌时先获得格挡并抽牌，再从手牌中选择相同数量的牌丢弃。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);
        await CommonActions.Draw(this, choiceContext);

        int discardCount = (int)DynamicVars["Cards"].BaseValue;
        if (discardCount <= 0)
        {
            return;
        }

        var playerCombatState = Owner.PlayerCombatState;
        if (playerCombatState == null)
        {
            return;
        }

        int handCount = playerCombatState.Hand.Cards.Count;
        if (handCount <= 0)
        {
            return;
        }

        int actualDiscardCount = Math.Min(discardCount, handCount);
        IReadOnlyList<CardModel> selectedCards = (await CommonActions.SelectCards(
            this,
            new LocString("cards", Id.Entry + ".selectionScreenPrompt"),
            choiceContext,
            PileType.Hand,
            actualDiscardCount)).ToList();

        foreach (CardModel selectedCard in selectedCards)
        {
            await CardCmd.Discard(choiceContext, selectedCard);
        }
    }
}
