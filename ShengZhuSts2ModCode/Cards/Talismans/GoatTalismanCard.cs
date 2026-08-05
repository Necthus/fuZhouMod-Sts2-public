using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 未羊：1费稀有技能牌。选择一个敌人，将其身上的所有负面状态翻倍，升级后翻两倍。消耗。
/// </summary>
public class GoatTalismanCard : TalismanCard
{
    /// <summary>
    /// 基础版最终倍率：当前层数变为2倍。
    /// </summary>
    private const int BaseDebuffMultiplier = 2;

    /// <summary>
    /// 升级版最终倍率：当前层数变为4倍，也就是翻两倍。
    /// </summary>
    private const int UpgradedDebuffMultiplier = 4;

    /// <summary>
    /// 构造卡牌数值：1费，技能牌，稀有，消耗。
    /// </summary>
    public GoatTalismanCard() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 升级时不再降费，改为让负面状态翻两倍。
    /// </summary>
    protected override void OnUpgrade()
    {
    }

    /// <summary>
    /// 出牌时将目标身上所有负面状态翻倍，升级后翻两倍。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        foreach (Creature target in GetTargets(cardPlay))
        {
            await DoubleDebuffs(target);
        }
    }

    /// <summary>
    /// 获取未羊要处理的目标；持有羊符咒遗物时改为所有存活敌人。
    /// </summary>
    private List<Creature> GetTargets(CardPlay cardPlay)
    {
        if (HasTalismanRelic<SheepTalisman>() && Owner?.Creature?.CombatState != null)
        {
            return Owner.Creature.CombatState
                .GetOpponentsOf(Owner.Creature)
                .Where(c => c.IsAlive && !c.IsDead)
                .ToList();
        }

        return cardPlay.Target == null ? [] : [cardPlay.Target];
    }

    /// <summary>
    /// 将指定目标身上所有负面状态按当前卡牌升级状态放大。
    /// </summary>
    private async Task DoubleDebuffs(Creature target)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        // 获取目标身上所有负面效果
        var debuffs = target.Powers
            .Where(p => p.Type == PowerType.Debuff && p.Amount > 0)
            .ToList();

        if (debuffs.Count == 0)
        {
            MainFile.Logger.Info("【未羊】目标没有负面状态，无效果。");
            return;
        }

        // 将每个负面效果按倍率补足层数：未升级最终2倍，升级最终4倍。
        int multiplier = IsUpgraded ? UpgradedDebuffMultiplier : BaseDebuffMultiplier;
        foreach (var debuff in debuffs)
        {
            int currentAmount = debuff.Amount;
            int amountToAdd = currentAmount * (multiplier - 1);
            await PowerCmd.ModifyAmount(debuff, amountToAdd, Owner.Creature, this, false);
            MainFile.Logger.Info($"【未羊】放大负面状态：{debuff.Id.Entry}，升级={IsUpgraded}，倍率={multiplier}，{currentAmount} → {currentAmount * multiplier}。");
        }
    }
}
