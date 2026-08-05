using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

/// <summary>
/// 曼尼-螳形团：造成伤害，敌人每有1层负面状态（易伤/虚弱/诅咒）额外打出1次，最多2次。
/// </summary>
public class ManNiMantis : BaseShadowKhanCard
{
    /// <summary>
    /// 构造卡牌数值：0费攻击，3(5)伤害。消耗。
    /// </summary>
    public ManNiMantis() : base(0, CardType.Attack, TargetType.AnyEnemy)
    {
        WithCalculatedDamage("Damage", 3, static (card, target) => ((ManNiMantis)card).GetDamageBonus(target), default, upgrade: 2, bonusUpgrade: 0);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 螳形团印在卡面上的基础伤害。
    /// </summary>
    public override int PrintedBaseDamage => GetPrintedBaseValue(3, 2);

    /// <summary>
    /// 出牌时造成伤害，根据目标负面状态层数额外打击（最多2次）。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            if (cardPlay.Target == null)
            {
                return;
            }

            await ShadowKhanAttack(choiceContext, cardPlay.Target);

            int extraHits = 0;
            var target = cardPlay.Target;

            var vulnerable = target.GetPower<VulnerablePower>();
            if (vulnerable != null)
            {
                extraHits += vulnerable.Amount;
            }

            var weak = target.GetPower<WeakPower>();
            if (weak != null)
            {
                extraHits += weak.Amount;
            }

            var curse = target.GetPower<LeiSuCursePower>();
            if (curse != null)
            {
                extraHits += curse.Amount;
            }

            extraHits = Math.Min(extraHits, 2);

            for (int i = 0; i < extraHits; i++)
            {
                await ShadowKhanAttack(choiceContext, cardPlay.Target);
            }
        }
        finally
        {
            ShadowKhanHelper.RecordShadowKhanPlayed(this);
        }
    }

    /// <summary>
    /// 计算手牌展示用的螳形团伤害。
    /// </summary>
    private decimal GetDamageBonus(Creature? target)
    {
        return GetDisplayedShadowKhanDamageBonus(PrintedBaseDamage, 0, target);
    }
}
