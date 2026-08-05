using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 拉苏：由古老牙齿将黑手帮·拉苏变化而来的先古技能牌。
/// 获得格挡后逐张抽有效牌，抽到状态或诅咒会直接消耗；随后由玩家选择手牌丢弃，并让随机可发动弃牌以奇巧方式发动。
/// </summary>
public class AncientRatso : BlackHandGangCard
{
    /// <summary>
    /// 随机发动弃牌效果的变量名。
    /// </summary>
    private const string TriggerCountVar = "TriggerCount";

    /// <summary>
    /// 构造卡牌数值：1费先古技能，基础10格挡、抽3弃3并随机发动至多1张；升级后15格挡、抽4弃4并随机发动至多2张。
    /// </summary>
    public AncientRatso() : base(1, CardType.Skill, CardRarity.Ancient, TargetType.Self)
    {
        WithBlock(10, 5);
        WithCards(3, 1);
        WithVar(TriggerCountVar, 1, 1);
    }

    /// <summary>
    /// 出牌时获得格挡，逐张抽有效牌，再选择等量手牌丢弃，并随机让可发动弃牌以奇巧方式发动。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);

        int drawCount = Math.Max(0, (int)DynamicVars["Cards"].BaseValue);
        int validDrawnCount = await DrawValidCardsAndExhaustBadCards(choiceContext, drawCount);

        IReadOnlyList<CardModel> discardedCards = await SelectCardsToDiscard(choiceContext, drawCount);
        if (discardedCards.Count == 0)
        {
            MainFile.Logger.Info($"【先古拉苏】玩家没有可丢弃的手牌，跳过随机发动：抽到有效牌={validDrawnCount}。");
            return;
        }

        ApplySlyToRandomDiscardedCards(discardedCards);
        await CardCmd.Discard(choiceContext, discardedCards);
        MainFile.Logger.Info($"【先古拉苏】结算完成：抽到有效牌={validDrawnCount}，计划弃牌={drawCount}，实际弃牌={discardedCards.Count}，升级={IsUpgraded}。");
    }

    /// <summary>
    /// 逐张抽牌，状态牌和诅咒牌直接消耗且不计入有效抽牌数量。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="targetCount">需要抽到的有效牌数量。</param>
    /// <returns>实际抽到的有效牌数量。</returns>
    private async Task<int> DrawValidCardsAndExhaustBadCards(PlayerChoiceContext choiceContext, int targetCount)
    {
        if (Owner?.PlayerCombatState == null || targetCount <= 0)
        {
            return 0;
        }

        int validDrawnCount = 0;
        while (validDrawnCount < targetCount)
        {
            CardModel? drawnCard = await DrawOneCard(choiceContext);
            if (drawnCard == null)
            {
                MainFile.Logger.Info($"【先古拉苏】无牌可抽，停止抽牌：有效牌={validDrawnCount}/{targetCount}。");
                break;
            }

            if (drawnCard.Type == CardType.Status || drawnCard.Type == CardType.Curse)
            {
                await CardCmd.Exhaust(choiceContext, drawnCard);
                MainFile.Logger.Info($"【先古拉苏】抽到状态/诅咒并消耗：卡牌={drawnCard.Id.Entry}。");
                continue;
            }

            validDrawnCount++;
        }

        return validDrawnCount;
    }

    /// <summary>
    /// 抽一张牌，并通过手牌引用差异找出本次新进入手牌的卡。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <returns>新抽到的卡；未抽到时返回 null。</returns>
    private async Task<CardModel?> DrawOneCard(PlayerChoiceContext choiceContext)
    {
        var hand = Owner?.PlayerCombatState?.Hand;
        if (Owner == null || hand == null)
        {
            return null;
        }

        HashSet<CardModel> beforeHand = hand.Cards.ToHashSet();
        await CardPileCmd.Draw(choiceContext, 1, Owner);

        return hand.Cards.FirstOrDefault(card => !beforeHand.Contains(card));
    }

    /// <summary>
    /// 让玩家从手牌中选择指定数量的牌丢弃。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="discardCount">需要丢弃的牌数量。</param>
    /// <returns>玩家选择的弃牌。</returns>
    private async Task<IReadOnlyList<CardModel>> SelectCardsToDiscard(PlayerChoiceContext choiceContext, int discardCount)
    {
        if (Owner?.PlayerCombatState?.Hand == null || discardCount <= 0)
        {
            return [];
        }

        int actualDiscardCount = Math.Min(discardCount, Owner.PlayerCombatState.Hand.Cards.Count);
        if (actualDiscardCount <= 0)
        {
            return [];
        }

        CardSelectorPrefs prefs = new(
            new LocString("cards", Id.Entry + ".selectionScreenPrompt"),
            actualDiscardCount,
            actualDiscardCount);

        return (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            prefs,
            null,
            this)).ToList();
    }

    /// <summary>
    /// 从本次弃牌里随机挑选可发动的牌，并临时施加奇巧标记；状态牌和诅咒牌不参与发动。
    /// </summary>
    /// <param name="discardedCards">本次玩家选择丢弃的牌。</param>
    private void ApplySlyToRandomDiscardedCards(IReadOnlyList<CardModel> discardedCards)
    {
        int triggerCount = Math.Max(0, (int)DynamicVars[TriggerCountVar].BaseValue);
        if (triggerCount <= 0)
        {
            return;
        }

        List<CardModel> candidates = discardedCards
            .Where(card => card.Type != CardType.Status && card.Type != CardType.Curse)
            .ToList();

        int actualTriggerCount = Math.Min(triggerCount, candidates.Count);
        for (int i = 0; i < actualTriggerCount; i++)
        {
            int index = StableRandomHelper.NextInt(Owner, candidates.Count);
            CardModel selectedCard = candidates[index];
            candidates.RemoveAt(index);

            CardCmd.ApplySingleTurnSly(selectedCard);
            MainFile.Logger.Info($"【先古拉苏】随机弃牌获得奇巧发动：卡牌={selectedCard.Id.Entry}，序号={i + 1}/{actualTriggerCount}。");
        }
    }
}
