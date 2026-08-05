using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 阿福揍扁了成龙：4费罕见攻击牌。
/// 造成16点伤害(升级+8)。本场战斗中每打出一张名称不同的阿福牌，伤害增加2(升级+1)。
/// 本回合每打出1张阿福牌，费用-1。
/// </summary>
public class AfuBeatJackie : AhFuCard
{
    /// <summary>
    /// 基础费用。
    /// </summary>
    private const int BaseCost = 4;

    // 构造卡牌数值：4 费，造成 16 点伤害（升级+8）；每张不同阿福牌加成 2 点（升级+1）。
    public AfuBeatJackie() : base(BaseCost, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithVar("Magic", 2, 1); // 每张不同阿福牌的伤害加成
        WithCalculatedDamage("Damage", 16, static (card, _) => ((AfuBeatJackie)card).GetDamageBonus(), default, upgrade: 8, bonusUpgrade: 0);
    }

    /// <summary>
    /// 通过费用计算钩子临时修正费用，避免本地 UI 可打出检查把费用写进联机状态。
    /// </summary>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this))
        {
            return false;
        }

        int reduction = Math.Min(BlackHandCardHelper.CountAfuPlayedThisTurn(this), BaseCost);
        if (reduction <= 0)
        {
            return false;
        }

        modifiedCost = Math.Max(0, originalCost - reduction);
        return modifiedCost != originalCost;
    }

    // 出牌时造成伤害（基础伤害 + 不同阿福牌加成）。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        decimal damage = ((CalculatedVar)DynamicVars["Damage"]).Calculate(cardPlay.Target);
        await CommonActions.CardAttack(this, cardPlay.Target, damage).Execute(choiceContext);

        await ResolveBountyRewards(choiceContext);
    }

    /// <summary>
    /// 计算本战斗不同阿福牌带来的伤害加成。
    /// </summary>
    /// <returns>额外伤害。</returns>
    private decimal GetDamageBonus()
    {
        return BlackHandCardHelper.CountUniqueAfuPlayedThisCombat(this) * (int)DynamicVars["Magic"].BaseValue;
    }

}
