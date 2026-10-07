using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 电眼逼人：0费普通攻击牌，造成4点伤害并施加1层易伤。
/// 猪符咒联动：拥有猪符咒时，改为对所有敌人造成伤害并施加易伤。
/// </summary>
public class LaserEyes : AhFuCard, IActualEnemyTargetProvider
{
    // 构造卡牌数值：0 费，造成 4 点伤害并施加 1 层易伤，升级后伤害 +2。
    public LaserEyes() : base(0, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(4, 2);
        WithVar("Magic", 1);
    }

    /// <summary>
    /// 检测玩家是否拥有猪符咒。
    /// </summary>
    private bool HasPigTalisman()
    {
        return Owner?.GetRelic<PigTalisman>() != null;
    }

    /// <summary>
    /// 获取电眼逼人本次实际影响的敌方目标；持有猪符咒时视为群体攻击。
    /// </summary>
    /// <param name="cardPlay">本次出牌信息。</param>
    /// <returns>实际受影响的敌方目标列表。</returns>
    public IReadOnlyList<Creature> GetActualEnemyTargets(CardPlay? cardPlay)
    {
        if (HasPigTalisman())
        {
            return BlackHandCardHelper.GetLivingOpponents(this);
        }

        if (cardPlay?.Target != null && cardPlay.Target.IsMonster && cardPlay.Target.IsAlive)
        {
            return [cardPlay.Target];
        }

        return [];
    }

    // 出牌时：有猪符咒则对所有敌人造成伤害并施加易伤，否则对单体目标。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int vulnAmount = (int)DynamicVars["Magic"].BaseValue;

        if (HasPigTalisman())
        {
            // 猪符咒联动：对所有敌人造成伤害并施加易伤
            IReadOnlyList<Creature> opponents = BlackHandCardHelper.GetLivingOpponents(this);
            decimal damage = DynamicVars.Damage.BaseValue;

            await CreatureCmd.Damage(choiceContext, opponents, damage, ValueProp.Move, Owner.Creature, this, cardPlay);

            foreach (Creature creature in opponents)
            {
                if (!creature.IsAlive || creature.IsDead) continue;
                await CommonActions.Apply<VulnerablePower>(choiceContext, creature, this, vulnAmount);
            }
        }
        else
        {
            // 无猪符咒：单体伤害并施加易伤
            if (cardPlay.Target == null)
            {
                return;
            }

            await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);
            if (cardPlay.Target.IsAlive && !cardPlay.Target.IsDead)
            {
                await CommonActions.Apply<VulnerablePower>(choiceContext, cardPlay.Target, this, vulnAmount);
            }
        }

        await ResolveBountyRewards(choiceContext);
    }
}
