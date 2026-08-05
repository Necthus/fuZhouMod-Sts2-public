using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 子鼠：1费稀有技能牌。从消耗堆中取回1张(升级2张)牌到手牌，0费牌保持0费，非0费牌本回合变1费，获得虚无与消耗。消耗。
/// </summary>
public class RatTalismanCard : TalismanCard
{
    /// <summary>
    /// 构造卡牌数值：1费，技能牌，稀有，消耗。Magic=1(升级+1)表示取回牌数。
    /// </summary>
    public RatTalismanCard() : base(1, CardType.Skill, CardRarity.Rare, TargetType.None)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithVar("Magic", 1, 1);
    }

    /// <summary>
    /// 出牌时从消耗堆中选择牌加入手牌，费用变0并获得虚无+消耗。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var playerCombatState = Owner?.PlayerCombatState;
        if (playerCombatState == null)
        {
            return;
        }

        // 获取消耗堆中的牌
        var exhaustCards = playerCombatState.ExhaustPile.Cards.ToList();
        if (exhaustCards.Count == 0)
        {
            MainFile.Logger.Info("【子鼠】消耗堆为空，无法取回卡牌。");
            return;
        }

        int numberOfCards = (int)DynamicVars["Magic"].BaseValue;

        // 如果消耗堆牌数不超过要取回的数量，全部取回
        if (exhaustCards.Count <= numberOfCards)
        {
            foreach (CardModel card in exhaustCards)
            {
                await MoveCardToHand(choiceContext, card);
            }
        }
        else
        {
            // 让玩家从消耗堆中选择
            var prefs = new CardSelectorPrefs(
                new LocString("cards", Id.Entry + ".selectionScreenPrompt"),
                numberOfCards,
                numberOfCards)
            {
                Cancelable = false
            };

            IReadOnlyList<CardModel> selectedCards = (await CardSelectCmd.FromSimpleGrid(
                choiceContext, exhaustCards, Owner!, prefs)).ToList();

            foreach (CardModel card in selectedCards)
            {
                await MoveCardToHand(choiceContext, card);
            }
        }
    }

    /// <summary>
    /// 将消耗堆中的牌移到手牌，0费牌保持0费，非0费牌本回合变1费，并添加消耗+虚无。
    /// </summary>
    private async Task MoveCardToHand(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (Owner?.PlayerCombatState == null || Owner.PlayerCombatState.Hand.Cards.Count >= 10)
        {
            MainFile.Logger.Info($"【子鼠】手牌已满，无法从消耗堆取回卡牌：{card.Id.Entry}。");
            return;
        }

        await CardPileCmd.Add(card, PileType.Hand);

        // 只降低费用，不抬高费用：0费牌保持0费，非0费牌本回合最多变为1费。
        card.EnergyCost.SetThisTurn(1, reduceOnly: true);

        // 添加消耗；持有鼠符咒遗物时不再添加虚无。
        card.AddKeyword(CardKeyword.Exhaust);
        if (!HasTalismanRelic<RatTalisman>())
        {
            card.AddKeyword(CardKeyword.Ethereal);
        }

        MainFile.Logger.Info($"【子鼠】从消耗堆取回卡牌：{card.Id.Entry}，已处理费用为最多1费+消耗，虚无={(HasTalismanRelic<RatTalisman>() ? "否" : "是")}。");
    }
}
