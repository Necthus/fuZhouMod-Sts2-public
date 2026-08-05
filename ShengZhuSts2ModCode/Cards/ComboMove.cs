using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 组合技：1费非普通技能牌，从抽牌堆中选择一张攻击牌放入手牌，其费用本回合变为0。升级后费用变0。
/// </summary>
public class ComboMove : AhFuCard
{
    // 构造卡牌数值：1 费，技能牌，升级后费用变 0。
    public ComboMove() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
    {
    }

    // 升级时把基础费用从 1 降到 0。
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    // 出牌时从抽牌堆中选择一张攻击牌放入手牌，并将其费用本回合设为0。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var playerCombatState = Owner.PlayerCombatState;
        if (playerCombatState == null)
        {
            return;
        }

        // 从抽牌堆中筛选攻击牌
        List<CardModel> attackCards = playerCombatState.DrawPile.Cards
            .Where(c => c.Type == CardType.Attack)
            .ToList();

        if (attackCards.Count == 0)
        {
            await ResolveBountyRewards(choiceContext);
            return;
        }

        // 让玩家选择一张攻击牌
        CardSelectorPrefs prefs = new CardSelectorPrefs(
            new LocString("cards", Id.Entry + ".selectionScreenPrompt"),
            1,
            1);
        IReadOnlyList<CardModel> selectedCards = (await CardSelectCmd.FromSimpleGrid(choiceContext, attackCards, Owner, prefs)).ToList();

        // 将选中的牌加入手牌并设费用为0（本回合）
        foreach (CardModel selectedCard in selectedCards)
        {
            await CardPileCmd.Add(selectedCard, PileType.Hand);
            selectedCard.EnergyCost.SetThisTurn(0, reduceOnly: true);
            MainFile.Logger.Info($"【组合技】检索攻击牌加入手牌并设为本回合0费：{selectedCard.Id.Entry}");
        }

        await ResolveBountyRewards(choiceContext);
    }
}
