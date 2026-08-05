using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 抱摔：造成 6/9 加目标最大生命八分之一的伤害。
/// </summary>
public class Suplex : TeamJackieCard
{
    /// <summary>
    /// 构造卡牌数值：2费攻击。
    /// </summary>
    public Suplex() : base(2, CardType.Attack, TargetType.AnyEnemy)
    {
        WithDamage(6, 3);
        WithCalculatedVar("CalculatedDamage", 0, static (card, target) => card.DynamicVars.Damage.BaseValue + (target?.MaxHp ?? 0) / 8, upgrade: 0, bonusUpgrade: 0);
    }

    /// <summary>
    /// 出牌时按目标最大生命补充伤害。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null || Owner?.Creature == null)
        {
            return;
        }

        decimal damage = DynamicVars.Damage.BaseValue + cardPlay.Target.MaxHp / 8;
        await CreatureCmd.Damage(choiceContext, cardPlay.Target, damage, ValueProp.Move, Owner.Creature, this);
    }
}
