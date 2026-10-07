using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 目标失衡：造成伤害，若打出生命伤害则随机改变目标意图，并尽量不让新攻击意图更高。
/// </summary>
public class TargetImbalance : TeamJackieCard
{
    /// <summary>
    /// 目标失衡暂时屏蔽所有获取渠道，不允许战斗中随机生成。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 目标失衡暂时屏蔽所有获取渠道，不允许被修饰器随机生成。
    /// </summary>
    public override bool CanBeGeneratedByModifiers => false;

    /// <summary>
    /// 构造卡牌数值：1费攻击，消耗，6/9伤害。
    /// </summary>
    public TargetImbalance() : base(1, CardType.Attack, TargetType.AnyEnemy)
    {
        WithDamage(6, 3);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 出牌时造成伤害，成功破血后重摇目标意图。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null || Owner?.Creature == null)
        {
            return;
        }

        IReadOnlyList<DamageResult> results = (await CreatureCmd.Damage(choiceContext, cardPlay.Target, DynamicVars.Damage.BaseValue, ValueProp.Move, Owner.Creature, this, cardPlay)).ToList();
        DamageResult? result = results.FirstOrDefault();
        if (result == null || result.UnblockedDamage <= 0 || cardPlay.Target.IsDead || cardPlay.Target.Monster == null)
        {
            return;
        }

        RerollMoveWithoutIncreasingDamage(cardPlay.Target);
    }

    /// <summary>
    /// 最多重摇5次，尽量避免新意图攻击总伤害高于旧意图。
    /// </summary>
    /// <param name="target">目标敌人。</param>
    private void RerollMoveWithoutIncreasingDamage(Creature target)
    {
        Creature? playerCreature = Owner?.Creature;
        if (playerCreature == null || target.Monster == null)
        {
            return;
        }

        int oldDamage = GetMoveDamage(target, playerCreature);
        var oldMove = target.Monster.NextMove;
        for (int i = 0; i < 5; i++)
        {
            target.Monster.RollMove([playerCreature]);
            int newDamage = GetMoveDamage(target, playerCreature);
            if (newDamage <= oldDamage)
            {
                MainFile.Logger.Info($"【目标失衡】重摇目标意图成功：旧伤害={oldDamage}，新伤害={newDamage}，尝试次数={i + 1}。");
                return;
            }
        }

        target.Monster.SetMoveImmediate(oldMove, true);
        MainFile.Logger.Info($"【目标失衡】5次重摇未找到更低攻击意图，恢复旧意图：旧伤害={oldDamage}。");
    }

    /// <summary>
    /// 计算目标当前意图对玩家的攻击总伤害。
    /// </summary>
    /// <param name="target">目标敌人。</param>
    /// <param name="playerCreature">玩家单位。</param>
    /// <returns>当前攻击意图总伤害。</returns>
    private static int GetMoveDamage(Creature target, Creature playerCreature)
    {
        if (target.Monster == null)
        {
            return 0;
        }

        int total = 0;
        foreach (AbstractIntent intent in target.Monster.NextMove.Intents)
        {
            if (intent is AttackIntent attackIntent)
            {
                total += attackIntent.GetTotalDamage([playerCreature], target);
            }
        }

        return total;
    }
}
