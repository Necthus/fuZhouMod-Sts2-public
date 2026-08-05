using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

/// <summary>
/// 大法师老爹的正气不灭：限制每回合实际生命承伤，后续符咒封存阶段会在打满上限时返还符咒。
/// </summary>
public class GrandMageDamageCapPower : ShengZhuSts2ModPower
{
    private int _capPerTurn;
    private int _takenThisTurn;
    private int _perHitCap;

    /// <summary>
    /// 正气不灭属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器显示本回合剩余可承受生命伤害。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 文案动态变量：总上限、剩余上限、单次上限。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Cap", 250),
        new DynamicVar("Remaining", 250),
        new DynamicVar("PerHitCap", 0)
    ];

    /// <summary>
    /// 初次生效时记录本场基础承伤上限。
    /// </summary>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        _capPerTurn = Math.Max(1, Amount);
        RefreshEncounterState();
        RefreshDisplay();
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【大法师老爹】【正气不灭】生效：每回合生命承伤上限={_capPerTurn}。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 设置狗符咒等效果带来的单次生命承伤上限。
    /// </summary>
    /// <param name="cap">单次上限；小于等于0表示关闭。</param>
    public void SetPerHitCap(int cap)
    {
        _perHitCap = Math.Max(0, cap);
        RefreshEncounterState();
        RefreshDisplay();
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【大法师老爹】【正气不灭】更新单次生命承伤上限：{_perHitCap}。");
    }

    /// <summary>
    /// 怪物方回合开始时重置本回合累计承伤。
    /// </summary>
    /// <param name="side">开始回合的阵营。</param>
    /// <param name="combatState">战斗状态。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (Owner == null || side != Owner.Side)
        {
            return Task.CompletedTask;
        }

        _takenThisTurn = 0;
        RefreshEncounterState();
        RefreshDisplay();
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【大法师老爹】【正气不灭】怪物回合开始，重置累计承伤。总上限={_capPerTurn}，单次上限={_perHitCap}。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 在生命损失前收窄数值，避免老爹一回合内实际生命伤害超过上限。
    /// </summary>
    /// <param name="target">受击者。</param>
    /// <param name="amount">本次即将损失的生命值。</param>
    /// <param name="props">伤害属性。</param>
    /// <param name="dealer">伤害来源。</param>
    /// <param name="cardSource">来源卡。</param>
    /// <returns>调整后的生命损失值。</returns>
    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Owner == null || target != Owner || amount <= 0 || _capPerTurn <= 0)
        {
            return amount;
        }

        int remainBefore = Math.Max(0, _capPerTurn - _takenThisTurn);
        decimal adjusted = amount;
        if (_perHitCap > 0)
        {
            adjusted = Math.Min(adjusted, _perHitCap);
        }

        adjusted = Math.Min(adjusted, remainBefore);
        _takenThisTurn += Math.Max(0, (int)Math.Ceiling(adjusted));
        RefreshEncounterState();
        RefreshDisplay();

        if (remainBefore > 0 && adjusted >= remainBefore)
        {
            ShengZhuLogHelper.VerboseCombatInfo(() => "【大法师老爹】【正气不灭】本回合生命承伤上限已打满，符咒返还将在封存阶段接入。");
        }

        ShengZhuLogHelper.VerboseCombatInfo(() => $"【大法师老爹】【正气不灭】拦截生命伤害：原始={amount}，调整后={adjusted}，累计={_takenThisTurn}/{_capPerTurn}，来源={GetCreatureLogName(dealer)}。");
        return adjusted;
    }

    /// <summary>
    /// 刷新显示层数和动态变量。
    /// </summary>
    private void RefreshDisplay()
    {
        int cap = Math.Max(_capPerTurn, Amount);
        int remaining = Math.Max(0, cap - _takenThisTurn);
        DynamicVars["Cap"].BaseValue = cap;
        DynamicVars["Remaining"].BaseValue = remaining;
        DynamicVars["PerHitCap"].BaseValue = _perHitCap;
        if (Amount != remaining)
        {
            SetAmount(remaining, true);
        }
    }

    /// <summary>
    /// 把当前承伤累计写进老爹遭遇状态，便于后续存档和符咒返还读取。
    /// </summary>
    private void RefreshEncounterState()
    {
        if (Owner?.CombatState?.Encounter is GrandMageDadEncounter encounter)
        {
            encounter.State.DamageTakenThisTurn = _takenThisTurn;
        }
    }

    /// <summary>
    /// 生成日志用单位名称。
    /// </summary>
    /// <param name="creature">单位。</param>
    /// <returns>日志名称。</returns>
    private static string GetCreatureLogName(Creature? creature)
    {
        if (creature == null)
        {
            return "无";
        }

        return creature.Monster?.Id.Entry ?? (creature.IsPlayer ? "玩家" : "未知单位");
    }
}
