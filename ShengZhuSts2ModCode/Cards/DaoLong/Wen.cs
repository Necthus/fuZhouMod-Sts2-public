using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 文：刀龙黑暗杀手技能牌，获得格挡、抽弃手牌并给予敌人虚弱。
/// </summary>
public class Wen : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造文卡牌数值。
    /// </summary>
    public Wen() : base(1, CardType.Skill, CardRarity.Event, TargetType.AnyEnemy)
    {
        WithBlock(6, 2);
        WithCards(1, 1);
    }

    /// <summary>
    /// 出牌时获得格挡，抽牌后选择等量手牌丢弃，并对目标施加虚弱。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);
        await CommonActions.Draw(this, choiceContext);

        int discardCount = (int)DynamicVars["Cards"].BaseValue;
        if (discardCount > 0 && Owner?.PlayerCombatState?.Hand.Cards.Count > 0)
        {
            int actualDiscardCount = Math.Min(discardCount, Owner.PlayerCombatState.Hand.Cards.Count);
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

        if (cardPlay.Target != null)
        {
            await CommonActions.Apply<WeakPower>(choiceContext, cardPlay.Target, this, discardCount);
        }
    }
}
