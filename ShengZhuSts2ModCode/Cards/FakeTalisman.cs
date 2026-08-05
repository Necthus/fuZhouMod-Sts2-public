using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 冒牌符咒：0费技能。抽 1(2) 张牌，再选择 1 张手牌丢弃。
/// 与 1 代 CardFakeTalisman 实现一致：名字带"符咒"是梗，与符咒系统不联动。
/// </summary>
public class FakeTalisman : ShengZhuSts2ModCard
{
    /// <summary>
    /// 一次必须丢弃的手牌数（与 1 代一致：固定 1 张）。
    /// </summary>
    private const int DiscardCount = 1;

    /// <summary>
    /// 构造卡牌数值：0费技能，普通稀有度，无目标。Magic=抽牌张数（基础1，升级+1）。
    /// </summary>
    public FakeTalisman() : base(0, CardType.Skill, CardRarity.Common, TargetType.None)
    {
        WithVar("Magic", 1, 1);
    }

    /// <summary>
    /// 出牌时先按 Magic 数量抽牌，然后从手牌中选 1 张丢弃；
    /// 若极端情况下手牌为空，则跳过丢弃，与 1 代行为基本一致。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner == null)
        {
            return;
        }

        int drawCount = (int)DynamicVars["Magic"].BaseValue;

        // 抽 Magic 张牌
        if (drawCount > 0)
        {
            await CardPileCmd.Draw(choiceContext, drawCount, Owner);
        }

        // 选 1 张手牌丢弃；手牌真的为 0 时跳过（极端兜底）
        var playerCombatState = Owner.PlayerCombatState;
        if (playerCombatState == null)
        {
            return;
        }

        int handCount = playerCombatState.Hand.Cards.Count(c => !ReferenceEquals(c, this));
        if (handCount <= 0)
        {
            MainFile.Logger.Info("【冒牌符咒】抽牌后手牌仍为空（除自身外），跳过丢弃。");
            return;
        }

        IReadOnlyList<CardModel> selectedCards = (await CommonActions.SelectCards(
            this,
            new LocString("cards", Id.Entry + ".selectionScreenPrompt"),
            choiceContext,
            PileType.Hand,
            DiscardCount)).ToList();

        foreach (CardModel selectedCard in selectedCards)
        {
            await CardCmd.Discard(choiceContext, selectedCard);
        }

        MainFile.Logger.Info($"【冒牌符咒】打出：抽 {drawCount} 张，丢弃 {selectedCards.Count} 张，升级={IsUpgraded}");
    }
}
