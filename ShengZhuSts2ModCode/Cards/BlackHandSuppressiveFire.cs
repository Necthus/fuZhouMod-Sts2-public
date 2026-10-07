using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 射击：先对所有敌人打一轮基础伤害，再按各自黑手层数补伤。
public class BlackHandSuppressiveFire : BlackHandGangCard
{
    // 构造卡牌数值：1 费，对所有敌人造成 4 点伤害，升级后伤害 +3。
    public BlackHandSuppressiveFire() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
        WithDamage(4, 3);
    }

    // 出牌时先群伤，再对每个有黑手的敌人按“层数 / 2”补伤。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        IReadOnlyList<Creature> opponents = GetLivingOpponents();
        decimal damage = DynamicVars.Damage.BaseValue;

        await CreatureCmd.Damage(choiceContext, opponents, damage, ValueProp.Move, Owner.Creature, this, cardPlay);

        foreach (Creature creature in opponents)
        {
            int extraDamage = BlackHandPower.GetAmount(creature) / 2;
            if (extraDamage > 0)
            {
                await CreatureCmd.Damage(choiceContext, creature, extraDamage, ValueProp.Move, Owner.Creature, this, cardPlay);
            }
        }

        await ResolveBountyRewards(choiceContext);
    }
}
