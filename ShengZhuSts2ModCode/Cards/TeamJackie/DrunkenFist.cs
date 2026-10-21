using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 醉拳：X费，对所有敌人随机造成 6~10/8~12 点伤害 X 次。
/// </summary>
public class DrunkenFist : TeamJackieCard
{
    /// <summary>
    /// 构造卡牌数值：X费全体攻击。
    /// </summary>
    public DrunkenFist() : base(0, CardType.Attack, TargetType.AllEnemies)
    {
        WithVar("MinDamage", 6, 2);
        WithVar("MaxDamage", 10, 2);
    }

    /// <summary>
    /// 让2代按X费牌处理本卡。
    /// </summary>
    protected override bool HasEnergyCostX => true;

    /// <summary>
    /// 出牌时按实际支付的X值逐次随机伤害。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature?.CombatState == null)
        {
            return;
        }

        int hitCount = Math.Max(0, ResolveEnergyXValue());
        if (hitCount == 0 || !Owner.Creature.CombatState.GetOpponentsOf(Owner.Creature).Any(creature => creature.IsAlive && !creature.IsDead))
        {
            return;
        }

        int minDamage = (int)DynamicVars["MinDamage"].BaseValue;
        int maxDamage = (int)DynamicVars["MaxDamage"].BaseValue;
        // 每段保留独立随机数，但整张牌作为同一次攻击结算活力等攻击钩子。
        // 提前击杀所有敌人时，await using 仍会完成攻击结束结算。
        await using var attack = await AttackCommand.CreateContextAsync(Owner.Creature.CombatState, choiceContext, cardPlay);
        for (int i = 0; i < hitCount; i++)
        {
            List<Creature> enemies = Owner.Creature.CombatState.GetOpponentsOf(Owner.Creature)
                .Where(creature => creature.IsAlive && !creature.IsDead)
                .ToList();
            if (enemies.Count == 0)
            {
                return;
            }

            int damage = StableRandomHelper.NextIntInclusive(Owner, minDamage, maxDamage);
            var results = await CreatureCmd.Damage(choiceContext, enemies, damage, ValueProp.Move, Owner.Creature, this, cardPlay);
            attack.AddHit(results);
        }
    }
}
