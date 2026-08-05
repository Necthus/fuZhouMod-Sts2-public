using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 近在咫尺：失去3点生命，获得敌方攻击意图总伤害+1/+4的格挡。
/// </summary>
public class CloseAtHand : TeamJackieCard
{
    /// <summary>
    /// 构造卡牌数值：2费技能，升级降为1费。
    /// </summary>
    public CloseAtHand() : base(2, CardType.Skill, TargetType.Self)
    {
        WithVar("HpLoss", 3);
        WithVar("IntentBonus", 1, 3);
        WithCalculatedVar("CalculatedBlock", 0, static (card, _) => CalculateIncomingDamage(card), upgrade: 0, bonusUpgrade: 0);
    }

    /// <summary>
    /// 升级时费用从2降为1。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时先失去3HP，再获得敌方攻击意图总伤害+1/+4的格挡。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await CreatureCmd.SetCurrentHp(Owner.Creature, Math.Max(0, Owner.Creature.CurrentHp - DynamicVars["HpLoss"].BaseValue));
        await CreatureCmd.GainBlock(Owner.Creature, CalculateIncomingDamage(this), ValueProp.Move, cardPlay);
    }

    /// <summary>
    /// 计算所有存活敌人当前攻击意图的总伤害，并追加当前强化加值。
    /// </summary>
    /// <param name="card">当前卡牌。</param>
    /// <returns>应获得的格挡。</returns>
    private static decimal CalculateIncomingDamage(CardModel card)
    {
        decimal intentBonus = card.DynamicVars["IntentBonus"].BaseValue;
        if (card.Owner?.Creature?.CombatState == null)
        {
            return intentBonus;
        }

        int totalDamage = 0;
        Creature playerCreature = card.Owner.Creature;
        foreach (Creature enemy in playerCreature.CombatState.GetOpponentsOf(playerCreature).Where(creature => creature.IsAlive && !creature.IsDead))
        {
            if (enemy.Monster == null)
            {
                continue;
            }

            foreach (AbstractIntent intent in enemy.Monster.NextMove.Intents)
            {
                if (intent is AttackIntent attackIntent)
                {
                    totalDamage += attackIntent.GetTotalDamage([playerCreature], enemy);
                }
            }
        }

        return totalDamage + intentBonus;
    }
}
