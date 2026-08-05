using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

/// <summary>
/// 烟雾弹能力：尼嘉-忍者团触发时让敌人本回合临时下降力量。
/// </summary>
public class SmokeBombPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 基础版烟雾弹在尼嘉-忍者团群体多段伤害中先累计触发次数，避免每个目标立刻排一次能力等待。
    /// </summary>
    private readonly Dictionary<CardModel, Dictionary<Creature, int>> _pendingNiJiaNinjaDamageTriggersByCard = [];

    /// <summary>
    /// 烟雾弹属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 烟雾弹可以叠层，每层多下降1点力量。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 根据是否为升级版切换能力说明。
    /// </summary>
    protected override string SmartDescriptionLocKey => IsAttackModeActive()
        ? Id.Entry + ".smartDescription_upgraded"
        : base.SmartDescriptionLocKey;

    /// <summary>
    /// 基础版监听尼嘉-忍者团造成的生命伤害，随后临时降低目标力量。
    /// </summary>
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (IsAttackModeActive() || Owner == null || dealer != Owner || cardSource is not NiJiaNinja || target == Owner || result.UnblockedDamage <= 0)
        {
            return;
        }

        AddPendingNiJiaNinjaDamageTrigger(cardSource, target);
    }

    /// <summary>
    /// 升级版由尼嘉-忍者团每段攻击后显式调用，即使没有造成生命伤害也触发。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">被攻击目标。</param>
    /// <param name="cardSource">来源尼嘉-忍者团。</param>
    public async Task OnNiJiaNinjaAttack(PlayerChoiceContext choiceContext, Creature target, CardModel cardSource)
    {
        if (!IsAttackModeActive() || Owner == null || target == Owner)
        {
            return;
        }

        await ApplyTemporaryStrengthLossForAttackCounts(choiceContext, new Dictionary<Creature, int> { [target] = 1 }, cardSource);
    }

    /// <summary>
    /// 升级版由尼嘉-忍者团群体多段攻击后批量调用，按目标合并等待时间。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="attackCounts">每个目标本次被尼嘉-忍者团攻击的次数。</param>
    /// <param name="cardSource">来源尼嘉-忍者团。</param>
    public async Task OnNiJiaNinjaAttacks(PlayerChoiceContext choiceContext, IReadOnlyDictionary<Creature, int> attackCounts, CardModel cardSource)
    {
        if (!IsAttackModeActive() || Owner == null || attackCounts.Count == 0)
        {
            return;
        }

        await ApplyTemporaryStrengthLossForAttackCounts(choiceContext, attackCounts, cardSource);
    }

    /// <summary>
    /// 结算基础版烟雾弹从尼嘉-忍者团生命伤害里累计的触发次数。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardSource">来源尼嘉-忍者团。</param>
    public async Task ResolveQueuedNiJiaNinjaDamageTriggers(PlayerChoiceContext choiceContext, CardModel cardSource)
    {
        if (Owner == null || IsAttackModeActive() || !_pendingNiJiaNinjaDamageTriggersByCard.Remove(cardSource, out Dictionary<Creature, int>? attackCounts))
        {
            return;
        }

        await ApplyTemporaryStrengthLossForAttackCounts(choiceContext, attackCounts, cardSource);
    }

    /// <summary>
    /// 记录基础版烟雾弹从尼嘉-忍者团生命伤害里触发了一次。
    /// </summary>
    /// <param name="cardSource">来源尼嘉-忍者团。</param>
    /// <param name="target">被造成生命伤害的目标。</param>
    private void AddPendingNiJiaNinjaDamageTrigger(CardModel cardSource, Creature target)
    {
        if (!_pendingNiJiaNinjaDamageTriggersByCard.TryGetValue(cardSource, out Dictionary<Creature, int>? attackCounts))
        {
            attackCounts = [];
            _pendingNiJiaNinjaDamageTriggersByCard[cardSource] = attackCounts;
        }

        attackCounts[target] = attackCounts.GetValueOrDefault(target) + 1;
    }

    /// <summary>
    /// 按最终降力数值批量结算烟雾弹触发次数，避免每个目标单独排播放命令。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="attackCounts">每个目标需要结算的烟雾弹触发次数。</param>
    /// <param name="cardSource">来源尼嘉-忍者团。</param>
    private async Task ApplyTemporaryStrengthLossForAttackCounts(PlayerChoiceContext choiceContext, IReadOnlyDictionary<Creature, int> attackCounts, CardModel cardSource)
    {
        if (Owner == null || attackCounts.Count == 0)
        {
            return;
        }

        Dictionary<int, List<Creature>> targetsByStrengthLoss = [];
        foreach (KeyValuePair<Creature, int> pair in attackCounts.Where(pair => pair.Value > 0 && pair.Key != Owner))
        {
            int remainingAttackCount = await ConsumeArtifactForAttackCount(choiceContext, pair.Key, cardSource, pair.Value);
            if (remainingAttackCount <= 0)
            {
                continue;
            }

            int totalStrengthLoss = Amount * remainingAttackCount;
            if (!targetsByStrengthLoss.TryGetValue(totalStrengthLoss, out List<Creature>? targets))
            {
                targets = [];
                targetsByStrengthLoss[totalStrengthLoss] = targets;
            }

            targets.Add(pair.Key);
            MainFile.Logger.Info($"【烟雾弹】临时下降力量进入批量队列：目标={pair.Key.Name}，攻击次数={pair.Value}，实际生效次数={remainingAttackCount}，下降={totalStrengthLoss}。");
        }

        foreach (KeyValuePair<int, List<Creature>> group in targetsByStrengthLoss)
        {
            await ApplyBatchedTemporaryStrengthLoss(choiceContext, group.Value, group.Key, cardSource);
        }
    }

    /// <summary>
    /// 按攻击次数消耗人工制品，返回剩余需要生效的攻击次数。
    /// </summary>
    private async Task<int> ConsumeArtifactForAttackCount(PlayerChoiceContext choiceContext, Creature target, CardModel? cardSource, int attackCount)
    {
        if (Owner == null || target == null || Amount <= 0 || attackCount <= 0)
        {
            return 0;
        }

        int remainingAttackCount = attackCount;
        ArtifactPower? artifactPower = target.GetPower<ArtifactPower>();
        if (artifactPower != null && artifactPower.Amount > 0)
        {
            int blockedAttackCount = Math.Min(remainingAttackCount, artifactPower.Amount);
            await PowerCmd.ModifyAmount(choiceContext, artifactPower, -blockedAttackCount, Owner, cardSource, true);
            remainingAttackCount -= blockedAttackCount;
            MainFile.Logger.Info($"【烟雾弹】人工制品抵消临时降力：目标={target.Name}，抵消次数={blockedAttackCount}，剩余触发={remainingAttackCount}。");
        }

        if (remainingAttackCount <= 0)
        {
            return 0;
        }

        return remainingAttackCount;
    }

    /// <summary>
    /// 对同一降力数值的一组目标批量挂临时降力记录，并批量扣除力量。
    /// </summary>
    private async Task ApplyBatchedTemporaryStrengthLoss(PlayerChoiceContext choiceContext, IReadOnlyList<Creature> targets, int totalStrengthLoss, CardModel? cardSource)
    {
        if (Owner == null || targets.Count == 0 || totalStrengthLoss <= 0)
        {
            return;
        }

        SmokeBombTemporaryStrengthPower.MarkStrengthLossHandledByBatch(targets, totalStrengthLoss);
        IReadOnlyList<SmokeBombTemporaryStrengthPower> appliedPowers;
        try
        {
            appliedPowers = await PowerCmd.Apply<SmokeBombTemporaryStrengthPower>(choiceContext, targets, totalStrengthLoss, Owner, cardSource, true);
        }
        finally
        {
            SmokeBombTemporaryStrengthPower.ClearStrengthLossHandledByBatch(targets);
        }

        List<Creature> appliedTargets = appliedPowers
            .Select(power => power.Owner)
            .OfType<Creature>()
            .Distinct()
            .ToList();
        if (appliedTargets.Count == 0)
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(choiceContext, appliedTargets, -totalStrengthLoss, Owner, cardSource, true);
        Flash();
        MainFile.Logger.Info($"【烟雾弹】批量临时下降力量：目标数={appliedTargets.Count}，目标=[{string.Join(",", appliedTargets.Select(target => target.Name))}]，下降={totalStrengthLoss}。");
    }

    /// <summary>
    /// 判断升级版烟雾弹的攻击段触发模式是否已经生效。
    /// </summary>
    /// <returns>存在隐藏标记能力时返回 true。</returns>
    private bool IsAttackModeActive()
    {
        return Owner?.HasPower<SmokeBombAttackModePower>() == true;
    }
}
