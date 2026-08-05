using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 虎符·阳成龙：除自身外每张手牌使伤害降低2，最低2。
/// </summary>
public class TigerTalismanYang : TeamJackieCard
{
    /// <summary>
    /// 未升级基础伤害。
    /// </summary>
    private const int BaseDamage = 12;

    /// <summary>
    /// 升级增加的基础伤害。
    /// </summary>
    private const int UpgradeDamage = 4;

    /// <summary>
    /// 手牌扣减后的最低基础伤害。
    /// </summary>
    private const int MinimumBaseDamage = 2;

    /// <summary>
    /// 除自身外每张手牌降低的基础伤害。
    /// </summary>
    private const int DamageReductionPerOtherCard = 2;

    /// <summary>
    /// 构造卡牌数值：0费攻击，基础12/16。
    /// </summary>
    public TigerTalismanYang() : base(0, CardType.Attack, TargetType.AnyEnemy)
    {
        WithCalculatedDamage(BaseDamage, -DamageReductionPerOtherCard, static (card, _) => CalculateReductionCount(card), upgrade: UpgradeDamage, bonusUpgrade: 0);
    }

    /// <summary>
    /// 出牌时造成当前动态伤害。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null || Owner?.Creature == null)
        {
            return;
        }

        decimal damage = ((CalculatedVar)DynamicVars["CalculatedDamage"]).Calculate(cardPlay.Target);
        await CommonActions.CardAttack(this, cardPlay.Target, damage).Execute(choiceContext);
    }

    /// <summary>
    /// 计算一代动态扣减次数：除自身外每张手牌使基础伤害降低2，但最低保留2点基础伤害。
    /// </summary>
    /// <param name="card">当前卡牌。</param>
    /// <returns>本次参与扣减的手牌张数。</returns>
    private static decimal CalculateReductionCount(CardModel card)
    {
        int handCount = card.Owner?.PlayerCombatState?.Hand.Cards.Count ?? 0;
        int otherHandCount = Math.Max(0, handCount - 1);
        int currentBaseDamage = BaseDamage + (card.IsUpgraded ? UpgradeDamage : 0);
        int maxReductionCount = Math.Max(0, (currentBaseDamage - MinimumBaseDamage) / DamageReductionPerOtherCard);
        return Math.Min(otherHandCount, maxReductionCount);
    }
}
