using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 爆破：先造成全体基础伤害，再吃掉全体敌人的黑手并按消耗量转成伤害，层数够高时再群体挂易伤和虚弱。
public class BlackHandBlastDistrict13 : BlackHandGangCard
{
    // 构造卡牌数值：1 费，对全体造成 8 点基础伤害，升级后 +4；每层黑手转 1 点伤害，升级后每层额外 +1 伤害，且群体减益层数也 +1。
    public BlackHandBlastDistrict13() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
        WithDamage(8, 4);
        WithVar("Magic", 1, 1);
        WithVar("DebuffAmount", 1, 1);
    }

    // 出牌时先结算全体基础伤害，再结算黑手消耗与追加伤害，最后按总消耗量决定是否补群体减益。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        int totalConsumed = 0;
        IReadOnlyList<Creature> opponents = GetLivingOpponents();
        if (opponents.Count == 0)
        {
            return;
        }

        await CreatureCmd.Damage(choiceContext, opponents, DynamicVars.Damage.BaseValue, ValueProp.Move, Owner.Creature, this);

        foreach (Creature creature in opponents.Where(creature => creature.IsAlive && !creature.IsDead))
        {
            int consumed = await ConsumeAllBlackHand(choiceContext, creature);
            totalConsumed += consumed;
            if (consumed > 0)
            {
                await CreatureCmd.Damage(choiceContext, creature, consumed * (int)DynamicVars["Magic"].BaseValue, ValueProp.Move, Owner.Creature, this);
            }
        }

        if (totalConsumed >= 8)
        {
            int debuffAmount = (int)DynamicVars["DebuffAmount"].BaseValue;
            foreach (Creature creature in GetLivingOpponents())
            {
                await CommonActions.Apply<VulnerablePower>(choiceContext, creature, this, debuffAmount);
                await CommonActions.Apply<WeakPower>(choiceContext, creature, this, debuffAmount);
            }
        }

        await ResolveBountyRewards(choiceContext);
    }
}
