using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

/// <summary>
/// 烟雾弹临时力量下降：施加时降低力量，拥有者回合结束时自动返还。
/// </summary>
public class SmokeBombTemporaryStrengthPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 批量烟雾弹路径会统一处理力量下降的目标，避免本能力内部再次单体扣力量。
    /// </summary>
    private static readonly Dictionary<Creature, decimal> BatchHandledStrengthLossByTarget = [];

    /// <summary>
    /// 烟雾弹临时力量下降属于减益能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 用计数器显示本回合需要返还的力量层数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 首次施加临时降力时，先让目标失去同等力量。
    /// </summary>
    /// <param name="target">被降低力量的目标。</param>
    /// <param name="amount">本次临时降力层数。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>异步任务。</returns>
    public override async Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0)
        {
            return;
        }

        if (ConsumeStrengthLossHandledByBatch(target, amount))
        {
            MainFile.Logger.Info($"【烟雾弹】批量路径接管临时降力返还：目标={target.Name}，返还={amount}。");
            return;
        }

        await PowerCmd.Apply<StrengthPower>(target, -amount, applier, cardSource, true);
        MainFile.Logger.Info($"【烟雾弹】施加临时降力：目标={target.Name}，力量-{amount}。");
    }

    /// <summary>
    /// 临时降力叠层时，只补扣新增层数对应的力量。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="power">发生变化的能力。</param>
    /// <param name="amount">本次变化量。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power != this || amount <= 0 || amount == Amount || Owner == null)
        {
            return;
        }

        if (ConsumeStrengthLossHandledByBatch(Owner, amount))
        {
            MainFile.Logger.Info($"【烟雾弹】批量路径接管叠加临时降力返还：目标={Owner.Name}，返还={amount}，当前需返还={Amount}。");
            return;
        }

        await PowerCmd.Apply<StrengthPower>(Owner, -amount, applier, cardSource, true);
        MainFile.Logger.Info($"【烟雾弹】叠加临时降力：目标={Owner.Name}，力量-{amount}，当前需返还={Amount}。");
    }

    /// <summary>
    /// 拥有者回合结束时，返还本能力记录的临时力量并移除自身。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">当前结束回合的阵营。</param>
    /// <param name="participants">当前阵营参与者。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner == null || side != Owner.Side)
        {
            return;
        }

        Creature owner = Owner;
        int strengthToReturn = Amount;
        Flash();
        await PowerCmd.Remove(this);

        if (strengthToReturn > 0)
        {
            await PowerCmd.Apply<StrengthPower>(owner, strengthToReturn, owner, null, true);
            MainFile.Logger.Info($"【烟雾弹】回合结束返还临时降力：目标={owner.Name}，力量+{strengthToReturn}。");
        }
    }

    /// <summary>
    /// 标记指定目标的力量下降由烟雾弹批量路径统一处理。
    /// </summary>
    /// <param name="targets">由批量路径处理力量下降的目标。</param>
    /// <param name="amount">每个目标本次下降的力量。</param>
    internal static void MarkStrengthLossHandledByBatch(IEnumerable<Creature> targets, decimal amount)
    {
        if (amount <= 0)
        {
            return;
        }

        foreach (Creature target in targets)
        {
            BatchHandledStrengthLossByTarget[target] = BatchHandledStrengthLossByTarget.GetValueOrDefault(target) + amount;
        }
    }

    /// <summary>
    /// 清理批量路径预登记，避免异常时污染后续单体结算。
    /// </summary>
    /// <param name="targets">需要清理的目标。</param>
    internal static void ClearStrengthLossHandledByBatch(IEnumerable<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            BatchHandledStrengthLossByTarget.Remove(target);
        }
    }

    /// <summary>
    /// 消耗批量路径预登记；成功时表示本次不应再单体扣力量。
    /// </summary>
    /// <param name="target">当前目标。</param>
    /// <param name="amount">本次能力记录的临时降力。</param>
    /// <returns>已经由批量路径处理时返回 true。</returns>
    private static bool ConsumeStrengthLossHandledByBatch(Creature target, decimal amount)
    {
        if (!BatchHandledStrengthLossByTarget.TryGetValue(target, out decimal pendingAmount))
        {
            return false;
        }

        decimal remainingAmount = pendingAmount - amount;
        if (remainingAmount <= 0)
        {
            BatchHandledStrengthLossByTarget.Remove(target);
        }
        else
        {
            BatchHandledStrengthLossByTarget[target] = remainingAmount;
        }

        return true;
    }
}
