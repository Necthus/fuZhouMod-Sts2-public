using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.TeamJackie;

/// <summary>
/// 紧急逃生能力：免疫并反弹来自怪物的减益，持续指定玩家回合数。
/// </summary>
public class EmergencyEscapePower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 每次反弹固定抽取的减益数量。
    /// </summary>
    private const int ReflectedDebuffCount = 2;

    /// <summary>
    /// 每个反弹减益固定施加的层数。
    /// </summary>
    private const int ReflectedDebuffAmount = 2;

    /// <summary>
    /// 紧急逃生专用稳定随机序号；每次成功反弹后递增，保证读档前后和联机两端结果一致。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int StableRollCursor { get; set; }

    /// <summary>
    /// 标记当前正在回滚减益，避免自身处理造成递归。
    /// </summary>
    private bool _isReflecting;

    /// <summary>
    /// 可被紧急逃生反弹的安全减益池，顺序固定以保证稳定随机一致。
    /// </summary>
    private static readonly ReflectedDebuffEntry[] ReflectedDebuffPool =
    [
        new("易伤", ApplyPower<VulnerablePower>),
        new("摧残", ApplyPower<DebilitatePower>),
        new("虚弱", ApplyPower<WeakPower>),
        new("消亡", ApplyPower<DemisePower>),
        new("缩小", ApplyPower<ShrinkPower>),
        new("力量下降", ApplyStrengthLoss)
    ];

    /// <summary>
    /// 紧急逃生属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器显示剩余保护回合。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 监听能力层数变化，拦截来自怪物并实际加到拥有者身上的减益。
    /// </summary>
    /// <param name="power">发生变化的能力。</param>
    /// <param name="amount">本次变化量。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (!CanReflect(power, amount, applier))
        {
            return;
        }

        int reflectedAmount = Math.Min(power.Amount, (int)amount);
        if (reflectedAmount <= 0)
        {
            return;
        }

        _isReflecting = true;
        try
        {
            Flash();
            await PowerCmd.ModifyAmount(power, -reflectedAmount, Owner, cardSource, false);
            List<ReflectedDebuffEntry> reflectedDebuffs = RollReflectedDebuffs(power, applier);
            await ApplyReflectedDebuffs(applier!, reflectedDebuffs, cardSource);

            MainFile.Logger.Info($"【紧急逃生】免疫并反弹来自怪物的减益：来源={applier?.Monster?.Id.Entry ?? "未知"}，原能力={power.Id.Entry}，免疫层数={reflectedAmount}，反弹={FormatDebuffs(reflectedDebuffs)}，随机序号={StableRollCursor}。");
        }
        finally
        {
            _isReflecting = false;
        }
    }

    /// <summary>
    /// 玩家回合结束时减少持续回合，归零时移除。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">当前结束回合的一方。</param>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (Owner == null || side != Owner.Side)
        {
            return;
        }

        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
            MainFile.Logger.Info("【紧急逃生】保护回合耗尽，能力移除。");
            return;
        }

        await PowerCmd.ModifyAmount(this, -1, Owner, null, false);
        MainFile.Logger.Info($"【紧急逃生】回合结束，剩余保护回合={Amount - 1}。");
    }

    /// <summary>
    /// 判断本次能力变化是否需要被紧急逃生反弹。
    /// </summary>
    /// <param name="power">发生变化的能力。</param>
    /// <param name="amount">本次变化量。</param>
    /// <param name="applier">施加来源。</param>
    /// <returns>需要反弹时返回 true。</returns>
    private bool CanReflect(PowerModel power, decimal amount, Creature? applier)
    {
        return !_isReflecting
               && Owner != null
               && power != this
               && power.Owner == Owner
               && amount > 0
               && power.Type == PowerType.Debuff
               && applier != null
               && applier != Owner
               && applier.Monster != null;
    }

    /// <summary>
    /// 从固定安全减益池中稳定抽取本次反弹的两个减益。
    /// </summary>
    /// <param name="sourcePower">被免疫的原始减益。</param>
    /// <param name="applier">原始减益来源，也是本次反弹目标。</param>
    /// <returns>本次反弹的减益列表。</returns>
    private List<ReflectedDebuffEntry> RollReflectedDebuffs(PowerModel sourcePower, Creature? applier)
    {
        List<ReflectedDebuffEntry> candidates = ReflectedDebuffPool.ToList();
        List<ReflectedDebuffEntry> rolls = [];
        int rollCursor = Math.Max(0, StableRollCursor);
        int effectCount = Math.Clamp(ReflectedDebuffCount, 1, candidates.Count);
        for (int i = 0; i < effectCount; i++)
        {
            int index = StableRandomHelper.StableIndex(Owner?.Player, candidates.Count, "EmergencyEscapePower.ReflectedDebuffIndex", BuildStableRollContext(sourcePower, applier, rollCursor, i));
            ReflectedDebuffEntry entry = candidates[index];
            candidates.RemoveAt(index);
            rolls.Add(entry);
        }

        StableRollCursor = rollCursor + 1;
        return rolls;
    }

    /// <summary>
    /// 生成紧急逃生专用稳定随机上下文，避免读档或联机时随机结果分叉。
    /// </summary>
    /// <param name="sourcePower">被免疫的原始减益。</param>
    /// <param name="applier">原始减益来源，也是本次反弹目标。</param>
    /// <param name="rollCursor">本次反弹的稳定随机序号。</param>
    /// <param name="effectIndex">本次抽取中的第几个减益。</param>
    /// <returns>稳定随机上下文。</returns>
    private string[] BuildStableRollContext(PowerModel sourcePower, Creature? applier, int rollCursor, int effectIndex)
    {
        return
        [
            sourcePower.Id.Entry,
            applier?.Monster?.Id.Entry ?? "NO_MONSTER",
            rollCursor.ToString(),
            effectIndex.ToString()
        ];
    }

    /// <summary>
    /// 将稳定随机抽出的安全减益施加给原始怪物来源。
    /// </summary>
    /// <param name="target">反弹目标。</param>
    /// <param name="debuffs">要反弹的减益列表。</param>
    /// <param name="cardSource">来源卡牌。</param>
    private async Task ApplyReflectedDebuffs(Creature target, IEnumerable<ReflectedDebuffEntry> debuffs, CardModel? cardSource)
    {
        if (!target.IsAlive || target.IsDead || !target.CanReceivePowers)
        {
            MainFile.Logger.Info($"【紧急逃生】反弹减益跳过：目标={target.Name}，原因=目标已死亡或不能接收能力。");
            return;
        }

        foreach (ReflectedDebuffEntry debuff in debuffs)
        {
            await debuff.Apply(target, ReflectedDebuffAmount, Owner!, cardSource);
        }
    }

    /// <summary>
    /// 对目标施加指定减益。
    /// </summary>
    /// <typeparam name="TPower">减益类型。</typeparam>
    /// <param name="target">施加目标。</param>
    /// <param name="amount">施加层数。</param>
    /// <param name="source">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    private static Task ApplyPower<TPower>(Creature target, int amount, Creature source, CardModel? cardSource)
        where TPower : PowerModel
    {
        return PowerCmd.Apply<TPower>(target, amount, source, cardSource);
    }

    /// <summary>
    /// 对目标施加力量下降。
    /// </summary>
    /// <param name="target">施加目标。</param>
    /// <param name="amount">下降层数。</param>
    /// <param name="source">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    private static Task ApplyStrengthLoss(Creature target, int amount, Creature source, CardModel? cardSource)
    {
        return PowerCmd.Apply<StrengthPower>(target, -amount, source, cardSource);
    }

    /// <summary>
    /// 格式化本次反弹结果，方便日志排查联机同步问题。
    /// </summary>
    /// <param name="debuffs">本次反弹的减益列表。</param>
    /// <returns>日志文本。</returns>
    private static string FormatDebuffs(IEnumerable<ReflectedDebuffEntry> debuffs)
    {
        return string.Join("、", debuffs.Select(debuff => $"{debuff.Name}{ReflectedDebuffAmount}"));
    }

    /// <summary>
    /// 紧急逃生可反弹的安全减益条目。
    /// </summary>
    /// <param name="Name">减益显示名称。</param>
    /// <param name="Apply">施加减益的方法。</param>
    private sealed record ReflectedDebuffEntry(string Name, Func<Creature, int, Creature, CardModel?, Task> Apply);
}
