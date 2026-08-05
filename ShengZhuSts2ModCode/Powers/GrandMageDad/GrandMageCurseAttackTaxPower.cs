using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Curses.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

/// <summary>
/// 特鲁：当前手牌里未支付的特鲁会让下一张攻击牌费用增加。
/// </summary>
public class GrandMageCurseAttackTaxPower : GrandMageCurseTaxPower
{
    /// <summary>
    /// 只影响攻击牌。
    /// </summary>
    protected override CardType AffectedType => CardType.Attack;

    /// <summary>
    /// 本 Buff 对应的诅咒类型。
    /// </summary>
    protected override Type SourceCurseType => typeof(CurseTohru);

    /// <summary>
    /// 来源名称，用于日志。
    /// </summary>
    protected override string SourceName => "特鲁";

    /// <summary>
    /// 施加或刷新特鲁费用提示。
    /// </summary>
    /// <param name="target">玩家生物。</param>
    /// <param name="sourceCard">来源诅咒牌。</param>
    /// <returns>异步任务。</returns>
    public static Task ApplyOrRefresh(Creature target, CardModel sourceCard)
    {
        return ApplyOrRefresh<GrandMageCurseAttackTaxPower>(target, sourceCard);
    }
}

/// <summary>
/// 老爹诅咒税基类：按当前手牌未支付诅咒数量动态修正费用，不直接写入卡牌费用。
/// </summary>
public abstract class GrandMageCurseTaxPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 受影响的卡牌类型。
    /// </summary>
    protected abstract CardType AffectedType { get; }

    /// <summary>
    /// 当前 Buff 对应的诅咒类型。
    /// </summary>
    protected abstract Type SourceCurseType { get; }

    /// <summary>
    /// 来源名称，用于日志。
    /// </summary>
    protected abstract string SourceName { get; }

    /// <summary>
    /// 诅咒税属于减益能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 诅咒税使用计数器显示费用增加量。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 施加或刷新指定类型的诅咒税提示。
    /// </summary>
    /// <typeparam name="TPower">具体诅咒税能力。</typeparam>
    /// <param name="target">玩家生物。</param>
    /// <param name="sourceCard">来源诅咒牌。</param>
    /// <returns>异步任务。</returns>
    protected static async Task ApplyOrRefresh<TPower>(Creature target, CardModel sourceCard)
        where TPower : GrandMageCurseTaxPower
    {
        TPower? power = target.GetPower<TPower>();
        if (power == null)
        {
            power = await PowerCmd.Apply<TPower>(target, 1, target, sourceCard);
        }

        power?.RefreshAmountFromHand();
    }

    /// <summary>
    /// 动态修正当前费用，避免直接写卡牌费用导致无法按诅咒实例撤回。
    /// </summary>
    /// <param name="card">正在计算费用的卡牌。</param>
    /// <param name="originalCost">原始费用。</param>
    /// <param name="modifiedCost">修正后费用。</param>
    /// <returns>费用有变化时返回 true。</returns>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        int pendingCount = Math.Max(0, Amount);
        if (pendingCount <= 0 || !CanAffectCard(card))
        {
            return false;
        }

        modifiedCost = originalCost + pendingCount;
        return true;
    }

    /// <summary>
    /// 打出对应类型牌时，一次性支付当前所有未支付同类诅咒。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">刚打出的牌。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!CanAffectCard(cardPlay.Card))
        {
            return;
        }

        int paidCount = MarkPendingAsPaid($"打出{cardPlay.Card.Id.Entry}");
        RefreshAmountFromHand();
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【联机同步】【大法师老爹诅咒】支付{SourceName}代价：玩家={Owner?.Player?.NetId.ToString() ?? "无"}，打出卡牌={cardPlay.Card.Id.Entry}，支付数量={paidCount}。");
        if (Amount <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }

    /// <summary>
    /// 任何牌堆变化后刷新显示层数，覆盖诅咒被丢弃、消耗或生成进入手牌。
    /// </summary>
    /// <param name="card">移动的卡牌。</param>
    /// <param name="oldPileType">旧牌堆。</param>
    /// <param name="source">来源模型。</param>
    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? source)
    {
        if (!CanRefreshForCard(card))
        {
            return;
        }

        RefreshAmountFromHand();
        await RemoveIfEmpty();
    }

    /// <summary>
    /// 抽牌后刷新显示层数。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="card">被抽到的卡牌。</param>
    /// <param name="fromHandDraw">是否来自手牌抽牌。</param>
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (!CanRefreshForCard(card))
        {
            return Task.CompletedTask;
        }

        RefreshAmountFromHand();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 回合结束时移除显示用 Power。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">当前结束回合的一方。</param>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (Owner != null && side == Owner.Side)
        {
            await PowerCmd.Remove(this);
        }
    }

    /// <summary>
    /// 按当前手牌未支付诅咒数量刷新 Power 显示层数。
    /// </summary>
    public void RefreshAmountFromHand()
    {
        int pendingCount = GetPendingCount();
        if (Amount == pendingCount)
        {
            return;
        }

        SetAmount(pendingCount, true);
    }

    /// <summary>
    /// 没有未支付诅咒时移除显示用 Power，避免留下 0 层提示。
    /// </summary>
    /// <returns>异步任务。</returns>
    private async Task RemoveIfEmpty()
    {
        if (Owner != null && Amount <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }

    /// <summary>
    /// 判断当前卡牌是否应受到此诅咒税影响。
    /// </summary>
    /// <param name="card">卡牌。</param>
    /// <returns>应受影响时返回 true。</returns>
    private bool CanAffectCard(CardModel? card)
    {
        return GrandMageCurseTaxService.IsOwnedBy(Owner?.Player, card)
               && card?.Type == AffectedType;
    }

    /// <summary>
    /// 判断本次牌堆变化是否可能影响当前诅咒税数量。
    /// </summary>
    /// <param name="card">变化的卡牌。</param>
    /// <returns>可能影响时返回 true。</returns>
    private bool CanRefreshForCard(CardModel? card)
    {
        return GrandMageCurseTaxService.IsOwnedBy(Owner?.Player, card)
               && card != null
               && SourceCurseType.IsInstanceOfType(card);
    }

    /// <summary>
    /// 获取当前手牌里未支付的同类诅咒数量。
    /// </summary>
    /// <returns>未支付数量。</returns>
    private int GetPendingCount()
    {
        if (SourceCurseType == typeof(CurseTohru))
        {
            return GrandMageCurseTaxService.CountPending<CurseTohru>(Owner?.Player);
        }

        if (SourceCurseType == typeof(CurseBlack))
        {
            return GrandMageCurseTaxService.CountPending<CurseBlack>(Owner?.Player);
        }

        return 0;
    }

    /// <summary>
    /// 将当前手牌里未支付的同类诅咒一次性标记为已支付。
    /// </summary>
    /// <param name="reason">支付原因。</param>
    /// <returns>实际支付数量。</returns>
    private int MarkPendingAsPaid(string reason)
    {
        if (SourceCurseType == typeof(CurseTohru))
        {
            return GrandMageCurseTaxService.MarkPendingAsPaid<CurseTohru>(Owner?.Player, reason);
        }

        if (SourceCurseType == typeof(CurseBlack))
        {
            return GrandMageCurseTaxService.MarkPendingAsPaid<CurseBlack>(Owner?.Player, reason);
        }

        return 0;
    }
}
