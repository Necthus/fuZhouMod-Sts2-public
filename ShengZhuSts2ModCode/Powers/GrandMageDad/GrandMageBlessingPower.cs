using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

/// <summary>
/// 大法师老爹的大法师的庇佑：本阶段先做最小保护标记和异常免疫展示。
/// </summary>
public class GrandMageBlessingPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 塔2里和 1 代目标一致的魔气单次伤害上限。
    /// </summary>
    public const int DemonQiSingleDamageCap = 30;

    private int _negateCount = 2;
    private bool _isNegating;

    /// <summary>
    /// 大法师的庇佑属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器显示剩余异常抵消次数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 动态变量：剩余抵消次数、魔气上限。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Negate", 2),
        new DynamicVar("Cap", DemonQiSingleDamageCap)
    ];

    /// <summary>
    /// 回合开始时重置抵消次数。
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

        _negateCount = 2;
        RefreshDisplay();
        ShengZhuLogHelper.VerboseCombatInfo(() => "【大法师老爹】【大法师的庇佑】回合开始，重置减益抵消次数为2。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 监听能力层数变化，抵消本回合前几次刚施加到老爹身上的减益。
    /// </summary>
    /// <param name="power">发生变化的能力。</param>
    /// <param name="amount">变化量。</param>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            RefreshDisplay();
            return;
        }

        if (!CanNegate(power, amount, applier))
        {
            return;
        }

        int negateAmount = Math.Min(power.Amount, (int)amount);
        if (negateAmount <= 0 || !TryConsumeNegate())
        {
            return;
        }

        _isNegating = true;
        try
        {
            await PowerCmd.ModifyAmount(power, -negateAmount, Owner, cardSource, false);
            ShengZhuLogHelper.VerboseCombatInfo(() => $"【大法师老爹】【大法师的庇佑】抵消减益：能力={power.Id.Entry}，层数={negateAmount}，来源={GetCreatureLogName(applier)}。");
        }
        finally
        {
            _isNegating = false;
        }
    }

    /// <summary>
    /// 尝试消耗一次异常抵消。
    /// </summary>
    /// <returns>是否成功消耗。</returns>
    public bool TryConsumeNegate()
    {
        if (_negateCount <= 0)
        {
            return false;
        }

        _negateCount--;
        RefreshDisplay();
        Flash();
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【大法师老爹】【大法师的庇佑】消耗1次减益抵消，剩余={_negateCount}。");
        return true;
    }

    /// <summary>
    /// 判断高温蒸汽是否应该转为伤害。
    /// </summary>
    /// <param name="target">目标单位。</param>
    /// <returns>有庇佑时返回 true。</returns>
    public static bool ShouldConvertSteamToDamage(Creature? target)
    {
        return target != null && target.HasPower<GrandMageBlessingPower>();
    }

    /// <summary>
    /// 判断本次能力变化是否应由庇佑抵消。
    /// </summary>
    /// <param name="power">发生变化的能力。</param>
    /// <param name="amount">变化量。</param>
    /// <param name="applier">施加者。</param>
    /// <returns>需要抵消时返回 true。</returns>
    private bool CanNegate(PowerModel power, decimal amount, Creature? applier)
    {
        return !_isNegating
               && Owner != null
               && power != this
               && power.Owner == Owner
               && amount > 0
               && power.Type == PowerType.Debuff
               && applier != null
               && applier != Owner;
    }

    /// <summary>
    /// 刷新显示层数和动态变量。
    /// </summary>
    private void RefreshDisplay()
    {
        DynamicVars["Negate"].BaseValue = _negateCount;
        DynamicVars["Cap"].BaseValue = DemonQiSingleDamageCap;
        if (Amount != _negateCount)
        {
            SetAmount(_negateCount, true);
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
