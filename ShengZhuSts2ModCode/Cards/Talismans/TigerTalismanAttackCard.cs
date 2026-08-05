using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 寅虎·攻：2费非普通攻击牌。造成6点伤害，比较敌我HP百分比：
/// 敌方HP%高于你→回复3点HP；敌方HP%低于你→额外造成其已损失HP的20%伤害。
/// 升级后费用变1。
/// </summary>
public class TigerTalismanAttackCard : TalismanCard
{
    /// <summary>
    /// 持有虎符咒遗物时的回血量。
    /// </summary>
    private const int TigerRelicHealAmount = 5;

    /// <summary>
    /// 普通追加伤害比例。
    /// </summary>
    private const decimal BaseMissingHpDamageRatio = 0.2m;

    /// <summary>
    /// 持有虎符咒遗物时的追加伤害比例。
    /// </summary>
    private const decimal TigerRelicMissingHpDamageRatio = 0.3m;

    /// <summary>
    /// 构造卡牌数值：2费，攻击牌，非普通。6点伤害，Magic=3表示回血量。
    /// </summary>
    public TigerTalismanAttackCard() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(6);
        WithVar("Magic", 3);
        WithCalculatedVar("BonusDamage", 0, static (card, target) => CalculateBonusDamage(card, target), upgrade: 0, bonusUpgrade: 0);
    }

    /// <summary>
    /// 升级时费用从2降到1。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时造成伤害，并根据HP百分比比较决定额外效果。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null || Owner?.Creature == null)
        {
            return;
        }

        Creature player = Owner.Creature;
        Creature target = cardPlay.Target;

        // 计算HP百分比
        decimal playerHpPercent = CalculateHpPercent(player);
        decimal targetHpPercent = CalculateHpPercent(target);

        if (targetHpPercent > playerHpPercent)
        {
            // 敌方HP%更高：造成基础伤害 + 回复HP
            await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);
            int healAmount = HasTalismanRelic<TigerTalisman>()
                ? TigerRelicHealAmount
                : (int)DynamicVars["Magic"].BaseValue;
            await CreatureCmd.Heal(player, healAmount);
            MainFile.Logger.Info($"【寅虎·攻】敌方HP%({targetHpPercent:P0})>玩家HP%({playerHpPercent:P0})，回复{healAmount}HP。");
        }
        else
        {
            // 敌方HP%更低或相等：造成基础伤害 + 额外20%已损失HP伤害
            decimal baseDamage = DynamicVars.Damage.BaseValue;
            decimal missingHp = target.MaxHp - target.CurrentHp;
            decimal damageRatio = GetMissingHpDamageRatio(this);
            int bonusDamage = (int)CalculateBonusDamage(this, target);
            decimal totalDamage = baseDamage + bonusDamage;

            await CreatureCmd.Damage(choiceContext, target, totalDamage, ValueProp.Move, player, this);
            MainFile.Logger.Info($"【寅虎·攻】敌方HP%({targetHpPercent:P0})<=玩家HP%({playerHpPercent:P0})，额外伤害{bonusDamage}(已损失HP{missingHp}×{damageRatio:P0})。");
        }
    }

    /// <summary>
    /// 计算卡面实时预览和实际出牌共用的已损失生命附加伤害。
    /// </summary>
    /// <param name="card">当前卡牌。</param>
    /// <param name="target">预览或出牌目标。</param>
    /// <returns>附加伤害。</returns>
    private static decimal CalculateBonusDamage(CardModel card, Creature? target)
    {
        Creature? player = card.Owner?.Creature;
        if (target == null || player == null)
        {
            return 0;
        }

        decimal playerHpPercent = CalculateHpPercent(player);
        decimal targetHpPercent = CalculateHpPercent(target);
        if (targetHpPercent > playerHpPercent)
        {
            return 0;
        }

        decimal missingHp = Math.Max(0m, target.MaxHp - target.CurrentHp);
        return (int)(missingHp * GetMissingHpDamageRatio(card));
    }

    /// <summary>
    /// 计算生命值百分比，最大生命异常时按0处理。
    /// </summary>
    /// <param name="creature">需要计算的生物。</param>
    /// <returns>当前生命值百分比。</returns>
    private static decimal CalculateHpPercent(Creature creature)
    {
        return creature.MaxHp > 0 ? (decimal)creature.CurrentHp / creature.MaxHp : 0;
    }

    /// <summary>
    /// 根据是否持有虎符咒读取已损失生命附加伤害比例。
    /// </summary>
    /// <param name="card">当前卡牌。</param>
    /// <returns>伤害比例。</returns>
    private static decimal GetMissingHpDamageRatio(CardModel card)
    {
        return card.Owner?.Relics.Any(relic => relic is TigerTalisman) == true
            ? TigerRelicMissingHpDamageRatio
            : BaseMissingHpDamageRatio;
    }
}
