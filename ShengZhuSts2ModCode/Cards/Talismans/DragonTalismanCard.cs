using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 辰龙：2费稀有攻击牌。对所有敌人造成15(升级20)点伤害。如果击杀了任何敌人，重新触发此卡效果。
/// </summary>
public class DragonTalismanCard : TalismanCard
{
    /// <summary>
    /// 持有龙符咒遗物时的基础伤害提升。
    /// </summary>
    private const int DragonRelicDamageBonus = 8;

    /// <summary>
    /// 构造卡牌数值：2费，攻击牌，稀有。15点伤害(升级+5)。
    /// </summary>
    public DragonTalismanCard() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
        WithCalculatedDamage("Damage", 15, static (card, _) => CalculateDragonDamageBonus(card), upgrade: 5, bonusUpgrade: 0);
    }

    /// <summary>
    /// 出牌时对所有敌人造成伤害，击杀后连锁触发。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await ExecuteDragonAttack(choiceContext, cardPlay, 0);
    }

    /// <summary>
    /// 执行龙符咒攻击逻辑：对所有敌人造成伤害，若击杀则递归触发。
    /// </summary>
    private async Task ExecuteDragonAttack(PlayerChoiceContext choiceContext, CardPlay cardPlay, int chainDepth)
    {
        var combatState = Owner!.Creature!.CombatState;
        if (combatState == null)
        {
            return;
        }

        if (chainDepth >= 50)
        {
            MainFile.Logger.Info("【辰龙】连锁次数达到50次，停止继续触发，避免异常无限连锁。");
            return;
        }

        // 获取当前存活敌人
        List<Creature> opponents = combatState
            .GetOpponentsOf(Owner.Creature)
            .Where(c => c.IsAlive && !c.IsDead)
            .ToList();

        if (opponents.Count == 0)
        {
            return;
        }

        LogDragonDamageBonus();

        // 用攻击命令执行卡牌伤害，保证伤害预览、力量修正和联机同步走同一套流程。
        var attack = await DamageCmd.Attack((CalculatedDamageVar)DynamicVars["Damage"])
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .Execute(choiceContext);
        bool killedAny = attack.Results.SelectMany(results => results).Any(result => result.Receiver.IsDead || !result.Receiver.IsAlive);

        // 根据真实伤害结果判断击杀，避免死后召唤抵消存活数量导致连锁丢失。
        int aliveCountAfter = combatState
            .GetOpponentsOf(Owner.Creature)
            .Count(c => c.IsAlive && !c.IsDead);

        if (killedAny && aliveCountAfter > 0)
        {
            MainFile.Logger.Info($"【辰龙】击杀触发连锁！当前仍有{aliveCountAfter}个敌人存活，再次触发。");
            await ExecuteDragonAttack(choiceContext, cardPlay, chainDepth + 1);
        }
    }

    /// <summary>
    /// 计算辰龙的龙符咒共鸣附加伤害；普通伤害链后续还会计算力量，所以这里先抵消一次普通力量。
    /// </summary>
    private static decimal CalculateDragonDamageBonus(CardModel card)
    {
        if (!HasDragonTalismanRelic(card) || card.Owner?.Creature == null)
        {
            return 0m;
        }

        decimal strength = card.Owner.Creature.GetPower<StrengthPower>()?.Amount ?? 0m;
        decimal strengthBonus = Math.Abs(strength) * 2m;

        return DragonRelicDamageBonus + strengthBonus - strength;
    }

    /// <summary>
    /// 判断当前卡牌持有者是否拥有龙符咒遗物。
    /// </summary>
    private static bool HasDragonTalismanRelic(CardModel card)
    {
        return card.Owner?.GetRelic<DragonTalisman>() != null;
    }

    /// <summary>
    /// 记录辰龙共鸣伤害修正，方便对照卡面预览和实战结算。
    /// </summary>
    private void LogDragonDamageBonus()
    {
        if (!HasDragonTalismanRelic(this) || Owner?.Creature == null)
        {
            return;
        }

        decimal strength = Owner.Creature.GetPower<StrengthPower>()?.Amount ?? 0m;
        decimal strengthBonus = Math.Abs(strength) * 2m;
        decimal damageBonus = CalculateDragonDamageBonus(this);
        MainFile.Logger.Info($"【辰龙】龙符咒共鸣：固定增伤={DragonRelicDamageBonus}，当前力量={strength}，绝对值双倍力量加成={strengthBonus}，抵消普通力量={strength}，动态伤害修正={damageBonus}。");
    }
}
