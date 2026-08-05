using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

/// <summary>
/// 巴特-夜蝠团：造成伤害，抽牌。有诅咒时恢复1HP。
/// </summary>
public class BaTeBat : BaseShadowKhanCard
{
    /// <summary>
    /// 构造卡牌数值：0费攻击，4(5)伤害，抽1(2)张牌。消耗。
    /// </summary>
    public BaTeBat() : base(0, CardType.Attack, TargetType.AnyEnemy)
    {
        WithCalculatedDamage("Damage", 4, static (card, target) => ((BaTeBat)card).GetDamageBonus(target), default, upgrade: 1, bonusUpgrade: 0);
        WithVar("Magic", 1, 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 夜蝠团印在卡面上的基础伤害。
    /// </summary>
    public override int PrintedBaseDamage => GetPrintedBaseValue(4, 1);

    /// <summary>
    /// 出牌时造成伤害，抽牌，若目标有诅咒则恢复1HP。
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
            await CardPileCmd.Draw(choiceContext, (int)DynamicVars["Magic"].BaseValue, Owner);

            var curse = cardPlay.Target.GetPower<LeiSuCursePower>();
            if (curse != null)
            {
                await CreatureCmd.Heal(Owner!.Creature!, 1, true);
            }
        }
        finally
        {
            ShadowKhanHelper.RecordShadowKhanPlayed(this);
        }
    }

    /// <summary>
    /// 计算手牌展示用的夜蝠团伤害。
    /// </summary>
    private decimal GetDamageBonus(Creature? target)
    {
        return GetDisplayedShadowKhanDamageBonus(PrintedBaseDamage, 0, target);
    }
}
