using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 压扁：伤害等于8加战斗内四个牌堆的牌数，升级降为2费。
/// </summary>
public class Flatten : TeamJackieCard
{
    /// <summary>
    /// 构造卡牌数值：3费攻击，升级降费。
    /// </summary>
    public Flatten() : base(3, CardType.Attack, TargetType.AnyEnemy)
    {
        WithCalculatedVar("CalculatedDamage", 0, static (card, _) => CalculateDamage(card), upgrade: 0, bonusUpgrade: 0);
    }

    /// <summary>
    /// 升级时费用从3降到2。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时造成动态伤害。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null || Owner?.Creature == null)
        {
            return;
        }

        await CreatureCmd.Damage(choiceContext, cardPlay.Target, CalculateDamage(this), ValueProp.Move, Owner.Creature, this, cardPlay);
    }

    /// <summary>
    /// 计算一代动态伤害：8 + 抽牌堆、手牌、弃牌堆、消耗堆总牌数。
    /// </summary>
    /// <param name="card">当前卡牌。</param>
    /// <returns>本次基础伤害。</returns>
    private static decimal CalculateDamage(CardModel card)
    {
        var state = card.Owner?.PlayerCombatState;
        if (state == null)
        {
            return 8;
        }

        return 8 + state.DrawPile.Cards.Count + state.Hand.Cards.Count + state.DiscardPile.Cards.Count + state.ExhaustPile.Cards.Count;
    }
}
