using MegaCrit.Sts2.Core.Commands;
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
        int minDamage = (int)DynamicVars["MinDamage"].BaseValue;
        int maxDamage = (int)DynamicVars["MaxDamage"].BaseValue;
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
            await CreatureCmd.Damage(choiceContext, enemies, damage, ValueProp.Move, Owner.Creature, this, cardPlay);
        }
    }
}
