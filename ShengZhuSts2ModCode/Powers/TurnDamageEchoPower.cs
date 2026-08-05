using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 回合伤害回响能力基类：统计本回合玩家造成的真实未格挡伤害，并预览回合结束追加伤害。
/// </summary>
public abstract class TurnDamageEchoPower : ShengZhuSts2ModPower, IHasSecondAmount
{
    /// <summary>
    /// 追加伤害比例；每层按本回合真实伤害的 30% 结算。
    /// </summary>
    private const decimal DamageRatioPerStack = 0.3m;

    /// <summary>
    /// 防止泰山压顶和山自身在回合结束造成的伤害再次计入本回合伤害池。
    /// </summary>
    private static int s_resolvingEchoDamageDepth;

    /// <summary>
    /// 本回合累计造成的真实未格挡伤害。
    /// </summary>
    private int _damageDealtThisTurn;

    /// <summary>
    /// DamageDealt 显示本回合真实伤害，PreviewDamage 显示回合结束预计伤害。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("DamageDealt", 0),
        new DynamicVar("PreviewDamage", 0)
    ];

    /// <summary>
    /// 这类能力属于增益。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 这类能力按计数器方式堆叠，层数会放大回合结束伤害。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// IHasSecondAmount 实现：右侧数字显示回合结束预计伤害。
    /// </summary>
    /// <returns>回合结束预计伤害文本。</returns>
    public string GetSecondAmount()
    {
        return GetPreviewDamage().ToString();
    }

    /// <summary>
    /// 玩家回合开始时清空本回合累计伤害。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">当前回合玩家。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner != null && player.Creature == Owner)
        {
            _damageDealtThisTurn = 0;
            RefreshPreviewDisplay();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 每当玩家对敌方造成真实未格挡伤害后，累计到本回合伤害池。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="dealer">伤害来源。</param>
    /// <param name="result">伤害结果。</param>
    /// <param name="props">伤害属性。</param>
    /// <param name="target">受击目标。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (Owner == null || dealer != Owner || target == Owner || result.UnblockedDamage <= 0 || s_resolvingEchoDamageDepth > 0)
        {
            return Task.CompletedTask;
        }

        _damageDealtThisTurn += result.UnblockedDamage;
        RefreshPreviewDisplay();
        Flash();

        return Task.CompletedTask;
    }

    /// <summary>
    /// 自身层数变化后，刷新预计伤害。
    /// </summary>
    /// <param name="power">发生变化的能力。</param>
    /// <param name="amount">变化量。</param>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            RefreshPreviewDisplay();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 玩家回合结束时，按当前预览伤害执行子类自己的结算逻辑。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">结束回合的阵营。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side != CombatSide.Player || Owner?.CombatState == null)
        {
            return;
        }

        int bonusDamage = GetPreviewDamage();
        if (bonusDamage <= 0)
        {
            return;
        }

        List<Creature> opponents = GetAliveOpponents();
        if (opponents.Count == 0)
        {
            return;
        }

        Flash();
        s_resolvingEchoDamageDepth++;
        try
        {
            await DealEndTurnDamage(choiceContext, opponents, bonusDamage);
        }
        finally
        {
            s_resolvingEchoDamageDepth--;
        }
    }

    /// <summary>
    /// 战斗结束时清空显示，避免战斗后遗留旧预览。
    /// </summary>
    /// <param name="room">当前战斗房间。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterCombatEnd(CombatRoom room)
    {
        _damageDealtThisTurn = 0;
        RefreshPreviewDisplay();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 子类实现自己的回合结束伤害目标逻辑。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="opponents">当前存活敌人列表。</param>
    /// <param name="bonusDamage">本次预计结算伤害。</param>
    /// <returns>异步任务。</returns>
    protected abstract Task DealEndTurnDamage(PlayerChoiceContext choiceContext, IReadOnlyList<Creature> opponents, int bonusDamage);

    /// <summary>
    /// 获取当前所有存活敌人。
    /// </summary>
    /// <returns>存活敌人列表。</returns>
    private List<Creature> GetAliveOpponents()
    {
        if (Owner?.CombatState == null)
        {
            return [];
        }

        return Owner.CombatState
            .GetOpponentsOf(Owner)
            .Where(creature => creature.IsAlive && !creature.IsDead)
            .ToList();
    }

    /// <summary>
    /// 按当前累计伤害和层数计算回合结束预计伤害。
    /// </summary>
    /// <returns>回合结束预计伤害。</returns>
    private int GetPreviewDamage()
    {
        return (int)Math.Floor(_damageDealtThisTurn * DamageRatioPerStack * Amount);
    }

    /// <summary>
    /// 刷新悬浮说明和第二数字中的伤害预览。
    /// </summary>
    private void RefreshPreviewDisplay()
    {
        DynamicVars["DamageDealt"].BaseValue = _damageDealtThisTurn;
        DynamicVars["PreviewDamage"].BaseValue = GetPreviewDamage();
        this.InvokeSecondAmountChanged();
    }
}
