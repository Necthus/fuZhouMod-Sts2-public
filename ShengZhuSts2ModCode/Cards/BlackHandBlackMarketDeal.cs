using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 黑市交易：花金币换即时爆发资源，并给所有敌人补 1 层黑手。
public class BlackHandBlackMarketDeal : BlackHandGangCard
{
    private const int BaseGoldCost = 0;
    private const int GoldCostIncreasePerUse = 3;
    private int _usesThisCombat;
    private ICombatState? _trackedCombatState;

    // 构造卡牌数值：0 费，获得 1 点能量并抽 1 张牌，升级后额外多抽 1 张。
    public BlackHandBlackMarketDeal() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithVar("Magic", 1, 1);
        WithCalculatedVar("GoldCost", BaseGoldCost, static (card, _) => GetGoldCostIncrease(card), upgrade: 0, bonusUpgrade: 0);
    }

    // 只有金币足够时才能打出这张牌。
    protected override bool IsPlayable => Owner != null && Owner.Gold >= GetCurrentGoldCost(this);

    // 出牌时先扣除金币，再返还能量、抽牌，并给所有敌人补黑手。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int goldCost = GetCurrentGoldCost(this);
        await PlayerCmd.LoseGold(goldCost, Owner);
        RecordUse(this);
        await PlayerCmd.GainEnergy(1, Owner);
        await CardPileCmd.Draw(choiceContext, (int)DynamicVars["Magic"].BaseValue, Owner);

        foreach (Creature creature in GetLivingOpponents())
        {
            await ApplyBlackHand(choiceContext, creature, 1);
        }

        await ResolveBountyRewards(choiceContext);
    }

    // 获取当前实际金币花费；战斗外预览固定显示基础费用。
    private static int GetCurrentGoldCost(BlackHandBlackMarketDeal card)
    {
        return BaseGoldCost + GetUseCount(card) * GoldCostIncreasePerUse;
    }

    // 获取金币涨价部分，供卡面动态变量显示。
    private static decimal GetGoldCostIncrease(CardModel card)
    {
        return card is BlackHandBlackMarketDeal blackMarketDeal
            ? GetUseCount(blackMarketDeal) * GoldCostIncreasePerUse
            : 0;
    }

    // 记录本场战斗内这张黑市交易自己的使用次数。
    private static void RecordUse(BlackHandBlackMarketDeal card)
    {
        EnsureCombatState(card);
        card._usesThisCombat++;
    }

    // 获取本场战斗这张黑市交易自己已经使用的次数。
    private static int GetUseCount(BlackHandBlackMarketDeal card)
    {
        if (card.Owner == null || card.IsCanonical)
        {
            return 0;
        }

        EnsureCombatState(card);
        return card._usesThisCombat;
    }

    // 战斗切换时清空这张卡自己的涨价计数，避免上一场战斗影响下一场。
    private static void EnsureCombatState(BlackHandBlackMarketDeal card)
    {
        ICombatState? combatState = card.CombatState;
        if (combatState == null || ReferenceEquals(card._trackedCombatState, combatState))
        {
            return;
        }

        card._trackedCombatState = combatState;
        card._usesThisCombat = 0;
    }
}
