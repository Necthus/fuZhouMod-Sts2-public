using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 飞鹤捕虾：1费非普通技能牌，丢弃手牌中所有非攻击牌，每丢弃1张抽1张牌。升级后费用变0。
/// </summary>
public class CraneCatchesShrimp : AhFuCard
{
    // 构造卡牌数值：1 费，技能牌，升级后费用变 0。
    public CraneCatchesShrimp() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
    {
    }

    // 升级时把基础费用从 1 降到 0。
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    // 出牌时丢弃手牌中所有非攻击牌，每丢弃1张抽1张牌。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var playerCombatState = Owner.PlayerCombatState;
        if (playerCombatState == null)
        {
            return;
        }

        // 收集手牌中所有非攻击牌（排除自身，因为自身已在结算中）
        List<CardModel> nonAttackCards = playerCombatState.Hand.Cards
            .Where(c => c.Type != CardType.Attack && !ReferenceEquals(c, this))
            .ToList();

        int discardCount = nonAttackCards.Count;

        // 逐一丢弃非攻击牌
        foreach (CardModel card in nonAttackCards)
        {
            await CardCmd.Discard(choiceContext, card);
        }

        // 抽等量的牌
        if (discardCount > 0)
        {
            await CardPileCmd.Draw(choiceContext, discardCount, Owner);
        }

        await ResolveBountyRewards(choiceContext);
    }
}
