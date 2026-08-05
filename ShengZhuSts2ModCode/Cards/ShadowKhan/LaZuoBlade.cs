using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

/// <summary>
/// 拉佐-刺刀团：造成伤害，若造成未格挡伤害则施加易伤。有诅咒时伤害+25%。
/// </summary>
public class LaZuoBlade : BaseShadowKhanCard
{
    /// <summary>
    /// 构造卡牌数值：0费攻击，5(8)伤害，施加1层易伤。消耗。
    /// </summary>
    public LaZuoBlade() : base(0, CardType.Attack, TargetType.AnyEnemy)
    {
        WithCalculatedDamage("Damage", 5, static (card, target) => ((LaZuoBlade)card).GetDamageBonus(target), default, upgrade: 3, bonusUpgrade: 0);
        WithVar("Magic", 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 刺刀团印在卡面上的基础伤害。
    /// </summary>
    public override int PrintedBaseDamage => GetPrintedBaseValue(5, 3);

    /// <summary>
    /// 出牌时造成伤害，若造成了未格挡伤害则施加易伤。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            if (cardPlay.Target == null)
            {
                return;
            }

            int hpBefore = cardPlay.Target.CurrentHp;
            await ShadowKhanAttack(choiceContext, cardPlay.Target, CalculateLaZuoDamage(cardPlay.Target));
            int hpAfter = cardPlay.Target.CurrentHp;

            if (hpAfter < hpBefore)
            {
                await CommonActions.Apply<VulnerablePower>(choiceContext, cardPlay.Target, this, (int)DynamicVars["Magic"].BaseValue);
            }
        }
        finally
        {
            ShadowKhanHelper.RecordShadowKhanPlayed(this);
        }
    }

    /// <summary>
    /// 计算手牌展示用的刺刀团伤害。
    /// </summary>
    private decimal GetDamageBonus(Creature? target)
    {
        return CalculateDisplayedShadowKhanDamage(CalculateLaZuoDamage(target), target) - PrintedBaseDamage;
    }

    /// <summary>
    /// 计算刺刀团实际结算伤害：基础伤害只吃影噬；目标有雷苏诅咒时按一代规则增加25%。
    /// </summary>
    private int CalculateLaZuoDamage(Creature? target)
    {
        int damage = CalculateShadowKhanDamage();
        if (target?.GetPower<LeiSuCursePower>() == null)
        {
            return damage;
        }

        return (int)(damage * 1.25m);
    }
}
