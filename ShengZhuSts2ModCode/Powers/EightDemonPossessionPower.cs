using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 八魔附体能力：每回合前三张攻击牌，对攻击目标随机施加多种异常。
/// </summary>
public class EightDemonPossessionPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 默认每回合触发次数。
    /// </summary>
    private const int DefaultTriggersPerTurn = 3;

    /// <summary>
    /// 默认每次随机异常种类数。
    /// </summary>
    private const int DefaultEffectsPerTrigger = 2;

    /// <summary>
    /// 普通异常随机层数下限。
    /// </summary>
    private const int DefaultNormalAmountMin = 1;

    /// <summary>
    /// 普通异常随机层数上限。
    /// </summary>
    private const int DefaultNormalAmountMax = 5;

    /// <summary>
    /// 力量下降随机点数下限。
    /// </summary>
    private const int DefaultStrengthLossMin = 1;

    /// <summary>
    /// 力量下降随机点数上限。
    /// </summary>
    private const int DefaultStrengthLossMax = 3;

    /// <summary>
    /// 当前能力每回合最多触发次数，保存进战斗存档避免读档后丢失配置。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int TriggersPerTurn { get; set; } = DefaultTriggersPerTurn;

    /// <summary>
    /// 当前能力每次触发施加的异常种类数，升级版为3。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int EffectsPerTrigger { get; set; } = DefaultEffectsPerTrigger;

    /// <summary>
    /// 本回合还剩多少张攻击牌可以触发，直接保存避免战斗中读档后次数重置。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int RemainingTriggersThisTurn { get; set; } = DefaultTriggersPerTurn;

    /// <summary>
    /// 八魔附体专用稳定随机序号；每次触发后递增，避免本场战斗 SL 改变异常结果。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int StableRollCursor { get; set; }

    /// <summary>
    /// 当前能力普通异常随机层数下限，升级后提高。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int NormalAmountMin { get; set; } = DefaultNormalAmountMin;

    /// <summary>
    /// 当前能力普通异常随机层数上限，升级后提高。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int NormalAmountMax { get; set; } = DefaultNormalAmountMax;

    /// <summary>
    /// 当前能力力量下降随机点数下限，升级后提高。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int StrengthLossMin { get; set; } = DefaultStrengthLossMin;

    /// <summary>
    /// 当前能力力量下降随机点数上限，升级后提高。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int StrengthLossMax { get; set; } = DefaultStrengthLossMax;

    /// <summary>
    /// 八魔附体属于玩家增益。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器显示本回合剩余触发次数；真实状态仍由 RemainingTriggersThisTurn 保存。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 描述变量：剩余触发、每回合上限、每次异常数量。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Remaining", DefaultTriggersPerTurn),
        new DynamicVar("TriggerCount", DefaultTriggersPerTurn),
        new DynamicVar("EffectCount", DefaultEffectsPerTrigger),
        new DynamicVar("NormalAmountMin", DefaultNormalAmountMin),
        new DynamicVar("NormalAmountMax", DefaultNormalAmountMax),
        new DynamicVar("StrengthLossMin", DefaultStrengthLossMin),
        new DynamicVar("StrengthLossMax", DefaultStrengthLossMax)
    ];

    /// <summary>
    /// 可被随机抽取的异常池，顺序固定以保证联机随机一致。
    /// 当前支持：灼烧(BurningPower)、灼心(BurningHeartPower)、潮湿(SoakedPower)、高温蒸汽(SteamPower)、冻伤(FrostbitePower)、冰狱(FrostHellPower)、
    /// 地缚(EarthBindPower)、沉重泥沼(HeavyMirePower)、风势(WindCatalystPower)、恐惧(FearPower)、易伤(VulnerablePower)、中毒(PoisonPower)、
    /// 凌虐(ManglePower)、摧残(DebilitatePower)、虚弱(WeakPower)、消亡(DemisePower)、缩小(ShrinkPower)、紧缠(ConstrictPower)、缓慢(SlowPower)、力量下降(StrengthPower负数)。
    /// </summary>
    private static readonly DebuffEntry[] DebuffPool =
    [
        new("灼烧", false, ApplyBurning),
        new("灼心", false, ApplyPower<BurningHeartPower>),
        new("潮湿", false, ApplySoaked),
        new("高温蒸汽", false, ApplyPower<SteamPower>),
        new("冻伤", false, ApplyPower<FrostbitePower>),
        new("冰狱", false, ApplyPower<FrostHellPower>),
        new("地缚", false, ApplyEarthBind),
        new("沉重泥沼", false, ApplyPower<HeavyMirePower>),
        new("风势", false, ApplyPower<WindCatalystPower>),
        new("恐惧", false, ApplyPower<FearPower>),
        new("易伤", false, ApplyPower<VulnerablePower>),
        new("中毒", false, ApplyPower<PoisonPower>),
        new("凌虐", false, ApplyPower<ManglePower>),
        new("摧残", false, ApplyPower<DebilitatePower>),
        new("虚弱", false, ApplyPower<WeakPower>),
        new("消亡", false, ApplyPower<DemisePower>),
        new("缩小", false, ApplyPower<ShrinkPower>),
        new("紧缠", false, ApplyPower<ConstrictPower>),
        new("缓慢", false, ApplyPower<SlowPower>),
        new("力量下降", true, ApplyStrengthLoss)
    ];

    /// <summary>
    /// 施加后刷新描述变量。
    /// </summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshDescriptionVars();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 每个玩家回合开始时重置剩余触发次数。
    /// </summary>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner?.Player == null || player != Owner.Player)
        {
            return Task.CompletedTask;
        }

        RemainingTriggersThisTurn = Math.Max(0, TriggersPerTurn);
        RefreshDescriptionVars();
        MainFile.Logger.Info($"【八魔附体】回合开始重置触发次数：剩余={RemainingTriggersThisTurn}。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 攻击牌打出后，对攻击目标随机施加异常。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!CanTrigger(cardPlay))
        {
            return;
        }

        List<Creature> targets = GetAttackTargets(cardPlay);
        if (targets.Count == 0)
        {
            MainFile.Logger.Info($"【八魔附体】攻击牌没有有效目标，跳过触发：卡牌={cardPlay.Card.Id.Entry}。");
            return;
        }

        List<DebuffRoll> rolls = RollDebuffs();
        if (rolls.Count == 0)
        {
            return;
        }

        foreach (Creature target in targets)
        {
            await ApplyRollsToTarget(choiceContext, target, cardPlay.Card, rolls);
        }

        RemainingTriggersThisTurn = Math.Max(0, RemainingTriggersThisTurn - 1);
        RefreshDescriptionVars();
        Flash();
        MainFile.Logger.Info($"【八魔附体】触发完成：卡牌={cardPlay.Card.Id.Entry}，目标数={targets.Count}，效果={FormatRolls(rolls)}，剩余次数={RemainingTriggersThisTurn}，随机序号={StableRollCursor}。");
    }

    /// <summary>
    /// 配置能力触发次数和每次异常数量。
    /// </summary>
    /// <param name="triggersPerTurn">每回合触发次数。</param>
    /// <param name="effectsPerTrigger">每次异常种类数。</param>
    /// <param name="normalAmountMin">普通异常层数下限。</param>
    /// <param name="normalAmountMax">普通异常层数上限。</param>
    /// <param name="strengthLossMin">力量下降点数下限。</param>
    /// <param name="strengthLossMax">力量下降点数上限。</param>
    public void Configure(int triggersPerTurn, int effectsPerTrigger, int normalAmountMin, int normalAmountMax, int strengthLossMin, int strengthLossMax)
    {
        TriggersPerTurn = Math.Max(1, triggersPerTurn);
        EffectsPerTrigger = Math.Clamp(effectsPerTrigger, 1, DebuffPool.Length);
        NormalAmountMin = Math.Max(0, normalAmountMin);
        NormalAmountMax = Math.Max(NormalAmountMin, normalAmountMax);
        StrengthLossMin = Math.Max(0, strengthLossMin);
        StrengthLossMax = Math.Max(StrengthLossMin, strengthLossMax);
        RemainingTriggersThisTurn = Math.Max(RemainingTriggersThisTurn, TriggersPerTurn);
        RefreshDescriptionVars();
    }

    /// <summary>
    /// 判断当前打出的卡是否能触发八魔附体。
    /// </summary>
    private bool CanTrigger(CardPlay cardPlay)
    {
        if (Owner?.Player == null || Owner.IsDead || RemainingTriggersThisTurn <= 0)
        {
            return false;
        }

        return cardPlay.Card?.Owner?.Creature == Owner && cardPlay.Card.Type == CardType.Attack;
    }

    /// <summary>
    /// 获取本次攻击应当影响的目标；全体攻击按所有存活可命中敌人处理。
    /// </summary>
    private List<Creature> GetAttackTargets(CardPlay cardPlay)
    {
        if (cardPlay.Card.TargetType == TargetType.AllEnemies)
        {
            return Owner.CombatState?.HittableEnemies
                .Where(IsValidTarget)
                .ToList() ?? [];
        }

        return IsValidTarget(cardPlay.Target) ? [cardPlay.Target!] : [];
    }

    /// <summary>
    /// 判断目标是否可以接受本次异常。
    /// </summary>
    private static bool IsValidTarget(Creature? target)
    {
        return target != null && target.IsMonster && target.IsAlive && !target.IsDead && target.CanReceivePowers;
    }

    /// <summary>
    /// 从固定异常池中随机抽取本次要施加的效果和层数。
    /// </summary>
    private List<DebuffRoll> RollDebuffs()
    {
        List<DebuffEntry> candidates = DebuffPool.ToList();
        List<DebuffRoll> rolls = [];
        int effectCount = Math.Clamp(EffectsPerTrigger, 1, candidates.Count);
        int rollCursor = Math.Max(0, StableRollCursor);
        for (int i = 0; i < effectCount; i++)
        {
            int index = StableDebuffIndex(candidates.Count, rollCursor, i);
            DebuffEntry entry = candidates[index];
            candidates.RemoveAt(index);

            int amount = entry.IsStrengthLoss
                ? StableAmount(StrengthLossMin, StrengthLossMax, rollCursor, i, entry.Name)
                : StableAmount(NormalAmountMin, NormalAmountMax, rollCursor, i, entry.Name);
            rolls.Add(new DebuffRoll(entry, amount));
        }

        StableRollCursor = rollCursor + 1;
        return rolls;
    }

    /// <summary>
    /// 按固定上下文计算异常类型下标，不推进游戏随机源，保证战斗 SL 后结果不变。
    /// </summary>
    private int StableDebuffIndex(int exclusiveMax, int rollCursor, int effectIndex)
    {
        return StableRandomHelper.StableIndex(Owner?.Player, exclusiveMax, "EightDemonPossessionPower.DebuffIndex", BuildStableRollContext(rollCursor, effectIndex));
    }

    /// <summary>
    /// 按固定上下文计算异常层数，不推进游戏随机源，保证战斗 SL 后结果不变。
    /// </summary>
    private int StableAmount(int minInclusive, int maxInclusive, int rollCursor, int effectIndex, string debuffName)
    {
        int range = maxInclusive - minInclusive + 1;
        if (range <= 1)
        {
            return minInclusive;
        }

        return minInclusive + StableRandomHelper.StableIndex(Owner?.Player, range, "EightDemonPossessionPower.DebuffAmount", BuildStableRollContext(rollCursor, effectIndex, debuffName));
    }

    /// <summary>
    /// 生成八魔附体专用稳定随机上下文。
    /// </summary>
    private string[] BuildStableRollContext(int rollCursor, int effectIndex, string debuffName = "")
    {
        return
        [
            TriggersPerTurn.ToString(),
            EffectsPerTrigger.ToString(),
            NormalAmountMin.ToString(),
            NormalAmountMax.ToString(),
            StrengthLossMin.ToString(),
            StrengthLossMax.ToString(),
            rollCursor.ToString(),
            effectIndex.ToString(),
            debuffName
        ];
    }

    /// <summary>
    /// 将已随机好的效果施加给单个目标。
    /// </summary>
    private async Task ApplyRollsToTarget(PlayerChoiceContext choiceContext, Creature target, CardModel cardSource, List<DebuffRoll> rolls)
    {
        foreach (DebuffRoll roll in rolls)
        {
            if (!IsValidTarget(target))
            {
                return;
            }

            try
            {
                await roll.Entry.Apply(target, roll.Amount, Owner, cardSource);
            }
            catch (Exception ex)
            {
                MainFile.Logger.Error($"【八魔附体】施加异常失败：目标={target.Name}，异常={roll.Entry.Name}，层数={roll.Amount}，错误={ex}");
            }
        }
    }

    /// <summary>
    /// 刷新能力描述里的动态变量。
    /// </summary>
    private void RefreshDescriptionVars()
    {
        SetAmount(Math.Max(0, RemainingTriggersThisTurn));

        if (DynamicVars.ContainsKey("Remaining"))
        {
            DynamicVars["Remaining"].BaseValue = Math.Max(0, RemainingTriggersThisTurn);
        }

        if (DynamicVars.ContainsKey("TriggerCount"))
        {
            DynamicVars["TriggerCount"].BaseValue = Math.Max(0, TriggersPerTurn);
        }

        if (DynamicVars.ContainsKey("EffectCount"))
        {
            DynamicVars["EffectCount"].BaseValue = Math.Max(0, EffectsPerTrigger);
        }

        if (DynamicVars.ContainsKey("NormalAmountMin"))
        {
            DynamicVars["NormalAmountMin"].BaseValue = Math.Max(0, NormalAmountMin);
        }

        if (DynamicVars.ContainsKey("NormalAmountMax"))
        {
            DynamicVars["NormalAmountMax"].BaseValue = Math.Max(0, NormalAmountMax);
        }

        if (DynamicVars.ContainsKey("StrengthLossMin"))
        {
            DynamicVars["StrengthLossMin"].BaseValue = Math.Max(0, StrengthLossMin);
        }

        if (DynamicVars.ContainsKey("StrengthLossMax"))
        {
            DynamicVars["StrengthLossMax"].BaseValue = Math.Max(0, StrengthLossMax);
        }
    }

    /// <summary>
    /// 格式化本次随机结果，方便中文日志排查联机问题。
    /// </summary>
    private static string FormatRolls(IEnumerable<DebuffRoll> rolls)
    {
        return string.Join("、", rolls.Select(roll => $"{roll.Entry.Name}{roll.Amount}"));
    }

    /// <summary>
    /// 通过潘库宝盒已有规则施加灼烧，保留火水合成逻辑。
    /// </summary>
    private static Task ApplyBurning(Creature target, int amount, Creature source, CardModel? cardSource)
    {
        return PanKuDemonQiHelper.ApplyBurning(target, amount, source, cardSource);
    }

    /// <summary>
    /// 通过潘库宝盒已有规则施加潮湿，保留火水合成和风势转化逻辑。
    /// </summary>
    private static Task ApplySoaked(Creature target, int amount, Creature source, CardModel? cardSource)
    {
        return PanKuDemonQiHelper.ApplySoaked(target, amount, source, cardSource);
    }

    /// <summary>
    /// 通过潘库宝盒已有规则施加地缚，保留地缚与潮湿融合逻辑。
    /// </summary>
    private static Task ApplyEarthBind(Creature target, int amount, Creature source, CardModel? cardSource)
    {
        return PanKuDemonQiHelper.ApplyEarthBind(target, amount, source, cardSource);
    }

    /// <summary>
    /// 对目标施加指定内置或自定义 Power。
    /// </summary>
    private static Task ApplyPower<TPower>(Creature target, int amount, Creature source, CardModel? cardSource)
        where TPower : PowerModel
    {
        return PowerCmd.Apply<TPower>(target, amount, source, cardSource);
    }

    /// <summary>
    /// 直接降低目标力量。
    /// </summary>
    private static Task ApplyStrengthLoss(Creature target, int amount, Creature source, CardModel? cardSource)
    {
        return PowerCmd.Apply<StrengthPower>(target, -amount, source, cardSource);
    }

    /// <summary>
    /// 异常池条目。
    /// </summary>
    private sealed record DebuffEntry(string Name, bool IsStrengthLoss, Func<Creature, int, Creature, CardModel?, Task> Apply);

    /// <summary>
    /// 本次触发随机出来的异常和层数。
    /// </summary>
    private sealed record DebuffRoll(DebuffEntry Entry, int Amount);
}
