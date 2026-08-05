using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 乌鸦坐飞机：2费非普通攻击牌，造成12点伤害，本回合获得临时敏捷。
/// 鸡符咒联动：拥有鸡符咒时，费用减1。
/// </summary>
public class CrowFlying : AhFuCard
{
    /// <summary>
    /// 基础费用。
    /// </summary>
    private const int BaseCost = 2;

    // 构造卡牌数值：2 费，造成 12 点伤害（升级+6），本回合获得 2 点临时敏捷（升级+1）。
    public CrowFlying() : base(BaseCost, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(12, 6);
        WithVar("Magic", 2, 1);
    }

    /// <summary>
    /// 通过费用计算钩子临时修正乌鸦坐飞机费用，避免本地 UI 可打出检查把费用写进联机状态。
    /// </summary>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) || Owner?.GetRelic<RoosterTalisman>() == null)
        {
            return false;
        }

        modifiedCost = Math.Max(0, originalCost - 1);
        return modifiedCost != originalCost;
    }

    // 出牌时对目标造成伤害，并获得临时敏捷。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);
        int dexterityAmount = (int)DynamicVars["Magic"].BaseValue;
        await CommonActions.Apply<TemporaryDexterityPower>(choiceContext, Owner.Creature, this, dexterityAmount);
        await ResolveBountyRewards(choiceContext);
    }

}
