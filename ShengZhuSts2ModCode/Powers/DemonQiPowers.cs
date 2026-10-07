using BaseLib.Abstracts;
using BaseLib.Extensions;
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
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 会按当前生命值造成恶魔异常伤害的基础能力，负责展示本场战斗实际伤害上限。
/// </summary>
public abstract class DemonQiDamagePower : ShengZhuSts2ModPower, IHasSecondAmount
{
    /// <summary>
    /// 按当前生命值计算伤害时使用的除数。
    /// </summary>
    protected abstract int DamageDivisor { get; }

    /// <summary>
    /// 单次触发的最低伤害。
    /// </summary>
    protected abstract int MinimumDamage { get; }

    /// <summary>
    /// 单人模式下的基础最高伤害。
    /// </summary>
    protected abstract int SoloDamageCap { get; }

    /// <summary>
    /// 文案动态变量：Cap 显示本场战斗按人数缩放后的实际上限，PlayerCount 显示当前人数，PreviewDamage 显示下次触发预计伤害。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Cap", SoloDamageCap),
        new DynamicVar("PlayerCount", 1),
        new DynamicVar("PreviewDamage", 0)
    ];

    /// <summary>
    /// IHasSecondAmount 实现：右侧数字显示下一次触发预计伤害。
    /// </summary>
    /// <returns>下一次触发预计伤害文本。</returns>
    public string GetSecondAmount()
    {
        return GetPreviewDamage().ToString();
    }

    /// <summary>
    /// 能力施加后刷新本场上限展示。
    /// </summary>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshDemonQiDamagePreview(cardSource);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 层数变化时刷新本场上限展示，避免叠加后描述仍停留在旧值。
    /// </summary>
    /// <param name="power">发生变化的能力。</param>
    /// <param name="amount">变化量。</param>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            RefreshDemonQiDamagePreview(cardSource);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 目标生命变化后刷新预览，避免悬浮提示停在旧血量。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <param name="target">受伤目标。</param>
    /// <param name="result">伤害结果。</param>
    /// <param name="props">伤害属性。</param>
    /// <param name="dealer">伤害来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && result.UnblockedDamage > 0)
        {
            RefreshDemonQiDamagePreview(cardSource);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 刷新描述中的本场伤害上限、玩家人数和下次触发预计伤害。
    /// </summary>
    /// <param name="cardSource">来源卡牌。</param>
    public void RefreshDemonQiDamagePreview(CardModel? cardSource = null)
    {
        if (!DynamicVars.ContainsKey("Cap") || !DynamicVars.ContainsKey("PlayerCount") || !DynamicVars.ContainsKey("PreviewDamage"))
        {
            return;
        }

        int playerCount = PanKuDemonQiHelper.GetCombatPlayerCount(Owner, Applier, cardSource);
        DynamicVars["PlayerCount"].BaseValue = playerCount;
        DynamicVars["Cap"].BaseValue = PanKuDemonQiHelper.CalculateMultiplayerDamageCap(SoloDamageCap, playerCount);
        DynamicVars["PreviewDamage"].BaseValue = GetPreviewDamage();
        this.InvokeSecondAmountChanged();
    }

    /// <summary>
    /// 按真实恶魔异常伤害公式计算下次预计伤害。
    /// </summary>
    /// <returns>下一次触发预计伤害。</returns>
    private int GetPreviewDamage()
    {
        if (Owner == null || !Owner.IsAlive)
        {
            return 0;
        }

        return PanKuDemonQiHelper.CalculateDemonQiPercentDamage(Owner, Applier, null, DamageDivisor, MinimumDamage, SoloDamageCap);
    }
}

/// <summary>
/// 灼烧：敌方回合开始时按当前生命值比例受到伤害，并逐回合减少。
/// </summary>
public class BurningPower : DemonQiDamagePower
{
    /// <summary>
    /// 灼烧生命除数。
    /// </summary>
    protected override int DamageDivisor => 8;

    /// <summary>
    /// 灼烧最低伤害。
    /// </summary>
    protected override int MinimumDamage => 2;

    /// <summary>
    /// 灼烧单人最高伤害。
    /// </summary>
    protected override int SoloDamageCap => 20;

    /// <summary>
    /// 灼烧属于负面能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 使用持续时间堆叠。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 敌方回合开始时结算灼烧伤害。
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (Owner.Side != side || !Owner.IsAlive)
        {
            return;
        }

        await PanKuDemonQiHelper.DealDemonQiPercentDamage(new BlockingPlayerChoiceContext(), Owner, Applier, null, 8, 2, 20);
        await PowerCmd.ModifyAmount(this, -1, Applier, null);
    }
}

/// <summary>
/// 灼心：灼烧进化后的高阶异常，造成更高比例伤害。
/// </summary>
public class BurningHeartPower : DemonQiDamagePower
{
    /// <summary>
    /// 灼心生命除数。
    /// </summary>
    protected override int DamageDivisor => 5;

    /// <summary>
    /// 灼心最低伤害。
    /// </summary>
    protected override int MinimumDamage => 5;

    /// <summary>
    /// 灼心单人最高伤害。
    /// </summary>
    protected override int SoloDamageCap => 40;

    /// <summary>
    /// 灼心属于负面能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 使用持续时间堆叠。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 敌方回合开始时结算灼心伤害。
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (Owner.Side != side || !Owner.IsAlive)
        {
            return;
        }

        await PanKuDemonQiHelper.DealDemonQiPercentDamage(new BlockingPlayerChoiceContext(), Owner, Applier, null, 5, 5, 40);
        await PowerCmd.ModifyAmount(this, -1, Applier, null);
    }
}

/// <summary>
/// 潮湿：基础水异常，每个敌方回合开始时减少一层。
/// </summary>
public class SoakedPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 潮湿属于负面能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 使用持续时间堆叠。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 施加潮湿后检查是否与地缚合成为沉重泥沼。
    /// </summary>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await PanKuDemonQiHelper.TryFuseEarthBindWithSoaked(Owner, applier ?? Applier, cardSource);
    }

    /// <summary>
    /// 潮湿层数变化后检查是否与地缚合成为沉重泥沼，兼容重复施加和联机同步。
    /// </summary>
    /// <param name="power">发生变化的能力。</param>
    /// <param name="amount">变化量。</param>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            await PanKuDemonQiHelper.TryFuseEarthBindWithSoaked(Owner, applier ?? Applier, cardSource);
        }
    }

    /// <summary>
    /// 敌方回合开始时自然衰减。
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (Owner.Side == side)
        {
            await PowerCmd.ModifyAmount(this, -1, Applier, null);
        }
    }
}

/// <summary>
/// 高温蒸汽：火水合成异常，回合开始时按层数概率干扰敌人行动，未干扰时造成更高比例伤害。
/// </summary>
public class SteamPower : DemonQiDamagePower
{
    /// <summary>
    /// 高温蒸汽生命除数。
    /// </summary>
    protected override int DamageDivisor => 5;

    /// <summary>
    /// 高温蒸汽最低伤害。
    /// </summary>
    protected override int MinimumDamage => 5;

    /// <summary>
    /// 高温蒸汽单人最高伤害。
    /// </summary>
    protected override int SoloDamageCap => 40;

    /// <summary>
    /// 高温蒸汽属于负面能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 使用持续时间堆叠。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 敌方回合开始时结算蒸汽干扰；干扰失败时才结算蒸汽伤害。
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (Owner.Side != side || !Owner.IsAlive)
        {
            return;
        }

        Player? randomPlayer = GetSteamRandomPlayer();
        int roll = randomPlayer == null ? 8 : StableRandomHelper.NextIntInclusive(randomPlayer, 1, 8);
        bool interruptIntent = roll <= Amount;
        MainFile.Logger.Info($"【高温蒸汽判定】目标={Owner.Name}，层数={Amount}，摇点={roll}/8，随机玩家={randomPlayer?.NetId.ToString() ?? "空"}，干扰成功={interruptIntent}。");

        if (interruptIntent)
        {
            EnemyIntentControlResult result = await EnemyIntentControlHelper.TrySkipEnemyMoveOnce(Owner, EnemyIntentControlHelper.SteamSkipMoveId, "高温蒸汽");
            MainFile.Logger.Info($"【高温蒸汽判定】干扰分支结束：目标={Owner.Name}，处理结果={result}，本次不造成伤害。");
        }
        else
        {
            MainFile.Logger.Info($"【高温蒸汽判定】判定失败，目标将正常行动并受到高温蒸汽伤害：目标={Owner.Name}。");
            await PanKuDemonQiHelper.DealDemonQiPercentDamage(new BlockingPlayerChoiceContext(), Owner, Applier, null, 5, 5, 40);
        }

        await PowerCmd.ModifyAmount(this, -1, Applier, null);
    }

    /// <summary>
    /// 获取用于高温蒸汽判定的联机稳定随机玩家。
    /// </summary>
    /// <returns>可用玩家；拿不到时返回空，随机辅助会固定返回保守值。</returns>
    private Player? GetSteamRandomPlayer()
    {
        return Applier?.Player
            ?? Owner.Player
            ?? Owner.CombatState?.Players.FirstOrDefault();
    }
}

/// <summary>
/// 冻伤：敌方回合开始时按当前生命值比例受到伤害，并逐回合减少。
/// </summary>
public class FrostbitePower : DemonQiDamagePower
{
    /// <summary>
    /// 冻伤生命除数。
    /// </summary>
    protected override int DamageDivisor => 8;

    /// <summary>
    /// 冻伤最低伤害。
    /// </summary>
    protected override int MinimumDamage => 2;

    /// <summary>
    /// 冻伤单人最高伤害。
    /// </summary>
    protected override int SoloDamageCap => 20;

    /// <summary>
    /// 冻伤属于负面能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 使用持续时间堆叠。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 敌方回合开始时结算冻伤伤害。
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (Owner.Side != side || !Owner.IsAlive)
        {
            return;
        }

        await PanKuDemonQiHelper.DealDemonQiPercentDamage(new BlockingPlayerChoiceContext(), Owner, Applier, null, 8, 2, 20);
        await PowerCmd.ModifyAmount(this, -1, Applier, null);
    }
}

/// <summary>
/// 冰狱：冻伤的高阶异常，造成更高比例伤害。
/// </summary>
public class FrostHellPower : DemonQiDamagePower
{
    /// <summary>
    /// 冰狱生命除数。
    /// </summary>
    protected override int DamageDivisor => 5;

    /// <summary>
    /// 冰狱最低伤害。
    /// </summary>
    protected override int MinimumDamage => 5;

    /// <summary>
    /// 冰狱单人最高伤害。
    /// </summary>
    protected override int SoloDamageCap => 40;

    /// <summary>
    /// 冰狱属于负面能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 使用持续时间堆叠。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 敌方回合开始时结算冰狱伤害。
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (Owner.Side != side || !Owner.IsAlive)
        {
            return;
        }

        await PanKuDemonQiHelper.DealDemonQiPercentDamage(new BlockingPlayerChoiceContext(), Owner, Applier, null, 5, 5, 40);
        await PowerCmd.ModifyAmount(this, -1, Applier, null);
    }
}

/// <summary>
/// 风势：加速敌人身上的火、冰类异常结算。
/// </summary>
public class WindCatalystPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 风势属于负面能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 使用持续时间堆叠。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 获得风势后立刻把已有潮湿转化为同层数冻伤。
    /// </summary>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (applier == null)
        {
            return;
        }

        await PanKuDemonQiHelper.ConvertSoakedToFrostbite(Owner, applier, cardSource);
    }

    /// <summary>
    /// 敌方回合开始时额外触发一次主要魔气异常。
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (Owner.Side != side || !Owner.IsAlive)
        {
            return;
        }

        if (Owner.GetPower<BurningPower>() != null || Owner.GetPower<FrostbitePower>() != null)
        {
            await PanKuDemonQiHelper.DealDemonQiPercentDamage(new BlockingPlayerChoiceContext(), Owner, Applier, null, 8, 2, 20);
        }

        if (Owner.GetPower<BurningHeartPower>() != null || Owner.GetPower<FrostHellPower>() != null)
        {
            await PanKuDemonQiHelper.DealDemonQiPercentDamage(new BlockingPlayerChoiceContext(), Owner, Applier, null, 5, 5, 40);
        }
    }

    /// <summary>
    /// 敌方回合结束时额外削减火、冰类异常，并减少风势自身。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (Owner.Side != side)
        {
            return;
        }

        await ReducePowerIfPresent<BurningPower>();
        await ReducePowerIfPresent<BurningHeartPower>();
        await ReducePowerIfPresent<FrostbitePower>();
        await ReducePowerIfPresent<FrostHellPower>();
        await PowerCmd.ModifyAmount(this, -1, Applier, null);
    }

    /// <summary>
    /// 如果目标身上存在指定异常，则通过联机命令额外减少 1 层。
    /// </summary>
    private async Task ReducePowerIfPresent<TPower>() where TPower : ShengZhuSts2ModPower
    {
        if (Owner.GetPower<TPower>() is TPower power)
        {
            await PowerCmd.ModifyAmount(power, -1, Applier, null);
        }
    }
}

/// <summary>
/// 恐惧：用于标记敌人受到天空魔气影响，目标回合开始时获得格挡并跳过行动。
/// </summary>
public class FearPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 文案动态变量：Block 显示本次恐惧生效时会给予的格挡。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Block", 15)
    ];

    /// <summary>
    /// 恐惧属于负面能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 使用持续时间堆叠。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 施加后记录恐惧来源，方便排查概率判定和挂载问题。
    /// </summary>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshFearBlockVar();
        MainFile.Logger.Info($"【恐惧】已施加：目标={Owner.Name}，来源={applier?.Name ?? "空"}，来源卡={cardSource?.Title ?? "空"}，层数={Amount}。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 层数变化时刷新格挡展示。
    /// </summary>
    /// <param name="power">发生变化的能力。</param>
    /// <param name="amount">变化量。</param>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            RefreshFearBlockVar();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 敌方回合开始时获得格挡并跳过行动，随后移除恐惧。
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (Owner.Side != side)
        {
            return;
        }

        try
        {
            if (Owner.IsMonster && Owner.IsAlive)
            {
                int blockAmount = GetFearBlockAmount();
                await CreatureCmd.GainBlock(Owner, blockAmount, ValueProp.Unpowered, null);
                EnemyIntentControlResult result = await EnemyIntentControlHelper.TrySkipEnemyMoveOnce(Owner, EnemyIntentControlHelper.FearSkipMoveId, "恐惧");
                MainFile.Logger.Info($"【恐惧】回合开始生效：目标={Owner.Name}，格挡={blockAmount}，跳过行动结果={result}。");
            }
            else
            {
                MainFile.Logger.Info($"【恐惧】回合开始跳过处理：目标={Owner.Name}，原因=目标不是存活怪物。");
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"【恐惧】回合开始处理异常，准备移除恐惧避免卡死：目标={Owner.Name}，异常={ex}");
        }

        await PowerCmd.Remove(this);
    }

    /// <summary>
    /// 按当前幕数计算恐惧给怪物的格挡，复刻1代第一幕15、第二幕20、第三幕及以后25的规则。
    /// </summary>
    /// <returns>格挡值。</returns>
    private int GetFearBlockAmount()
    {
        Player? player = Applier?.Player
            ?? Owner.CombatState?.Players.FirstOrDefault();
        int actNumber = (player?.RunState.CurrentActIndex ?? 0) + 1;
        int baseBlock = 15 + (Math.Min(3, Math.Max(1, actNumber)) - 1) * 5;
        return baseBlock * Math.Max(1, Amount);
    }

    /// <summary>
    /// 刷新恐惧描述里的格挡数值。
    /// </summary>
    private void RefreshFearBlockVar()
    {
        if (DynamicVars.ContainsKey("Block"))
        {
            DynamicVars["Block"].BaseValue = GetFearBlockAmount();
        }
    }
}

/// <summary>
/// 天空恶魔：攻击后额外触发目标身上的火、冰异常，并概率施加恐惧。
/// </summary>
public class HeavenDemonPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 天空恶魔属于玩家增益。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用层数堆叠。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 攻击造成伤害后触发天空恶魔效果。
    /// </summary>
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, MegaCrit.Sts2.Core.Models.CardModel? cardSource)
    {
        if (!CanTriggerFromDamage(dealer, props, target, cardSource))
        {
            return;
        }

        int abnormalKinds = 0;
        if (target.GetPower<BurningPower>() is BurningPower burning)
        {
            abnormalKinds++;
            await PanKuDemonQiHelper.DealDemonQiPercentDamage(choiceContext, target, Owner, cardSource, 8, 3, 20);
            await PowerCmd.ModifyAmount(burning, -1, Owner, cardSource);
        }

        if (target.GetPower<BurningHeartPower>() is BurningHeartPower burningHeart)
        {
            abnormalKinds++;
            await PanKuDemonQiHelper.DealDemonQiPercentDamage(choiceContext, target, Owner, cardSource, 5, 5, 40);
            await PowerCmd.ModifyAmount(burningHeart, -1, Owner, cardSource);
        }

        if (target.GetPower<FrostbitePower>() is FrostbitePower frostbite)
        {
            abnormalKinds++;
            await PanKuDemonQiHelper.DealDemonQiPercentDamage(choiceContext, target, Owner, cardSource, 8, 3, 20);
            await PowerCmd.ModifyAmount(frostbite, -1, Owner, cardSource);
        }

        if (target.GetPower<FrostHellPower>() is FrostHellPower frostHell)
        {
            abnormalKinds++;
            await PanKuDemonQiHelper.DealDemonQiPercentDamage(choiceContext, target, Owner, cardSource, 5, 5, 40);
            await PowerCmd.ModifyAmount(frostHell, -1, Owner, cardSource);
        }

        int chance = Math.Clamp(1 + abnormalKinds, 1, 8);
        Player? randomPlayer = Owner.Player ?? Owner.CombatState?.Players.FirstOrDefault();
        if (randomPlayer == null)
        {
            MainFile.Logger.Info($"【天空恶魔】恐惧判定跳过：目标={target.Name}，原因=找不到联机稳定随机源。");
            return;
        }

        int roll = StableRandomHelper.NextInt(randomPlayer, 8);
        bool applyFear = roll < chance;
        MainFile.Logger.Info($"【天空恶魔】恐惧判定：目标={target.Name}，异常种类={abnormalKinds}，概率={chance}/8，摇点={roll}/8，随机玩家={randomPlayer.NetId}，施加恐惧={applyFear}。");
        if (applyFear)
        {
            await PowerCmd.Apply<FearPower>(target, 1, Owner, cardSource);
        }
    }

    /// <summary>
    /// 判断本次伤害是否能触发天空恶魔，避免恶魔异常追加伤害再次触发自身。
    /// </summary>
    /// <param name="dealer">伤害来源。</param>
    /// <param name="props">伤害属性。</param>
    /// <param name="target">受击目标。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>只有自己打出的真实攻击牌伤害才返回 true。</returns>
    private bool CanTriggerFromDamage(Creature? dealer, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner || cardSource?.Type != CardType.Attack || !target.IsMonster || !target.IsAlive)
        {
            return false;
        }

        return props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered);
    }
}

/// <summary>
/// 地之恶魔：回合结束时若没有格挡，则失去生命并获得格挡。
/// </summary>
public class EarthDemonPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 地之恶魔属于玩家增益。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器展示失去生命值。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 回合结束时执行地之恶魔代价和补偿。
    /// </summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (Owner.Side != side || Owner.Block > 0)
        {
            return;
        }

        int blockGain = Math.Max(1, Owner.MaxHp / 8);
        await CreatureCmd.Damage(choiceContext, Owner, Math.Max(1, Amount), ValueProp.Unblockable | ValueProp.Unpowered, Owner, null, null);
        await CreatureCmd.GainBlock(Owner, blockGain, ValueProp.Unpowered, null);
    }
}

/// <summary>
/// 地缚：敌方回合开始时受到当前生命值 1/8 的伤害。
/// </summary>
public class EarthBindPower : DemonQiDamagePower
{
    /// <summary>
    /// 地缚生命除数。
    /// </summary>
    protected override int DamageDivisor => 8;

    /// <summary>
    /// 地缚最低伤害。
    /// </summary>
    protected override int MinimumDamage => 2;

    /// <summary>
    /// 地缚当前按基本无上限处理。
    /// </summary>
    protected override int SoloDamageCap => 999999;

    /// <summary>
    /// 地缚属于负面能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 使用计数器展示层数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 施加地缚后检查是否与潮湿合成为沉重泥沼。
    /// </summary>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await base.AfterApplied(applier, cardSource);
        await PanKuDemonQiHelper.TryFuseEarthBindWithSoaked(Owner, applier ?? Applier, cardSource);
    }

    /// <summary>
    /// 地缚层数变化后检查是否与潮湿合成为沉重泥沼，兼容重复施加和联机同步。
    /// </summary>
    /// <param name="power">发生变化的能力。</param>
    /// <param name="amount">变化量。</param>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        await base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);
        if (ReferenceEquals(power, this))
        {
            await PanKuDemonQiHelper.TryFuseEarthBindWithSoaked(Owner, applier ?? Applier, cardSource);
        }
    }

    /// <summary>
    /// 地缚不随回合自然减少。
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (Owner.Side == side && Owner.IsAlive)
        {
            await PanKuDemonQiHelper.DealDemonQiPercentDamage(new BlockingPlayerChoiceContext(), Owner, Applier, null, 8, 2, 999999);
        }
    }
}

/// <summary>
/// 沉重泥沼：地缚和潮湿合成的强化异常，回合开始造成伤害并临时降低力量。
/// </summary>
public class HeavyMirePower : DemonQiDamagePower
{
    /// <summary>
    /// 沉重泥沼生命除数。
    /// </summary>
    protected override int DamageDivisor => 5;

    /// <summary>
    /// 沉重泥沼最低伤害。
    /// </summary>
    protected override int MinimumDamage => 5;

    /// <summary>
    /// 沉重泥沼单人最高伤害。
    /// </summary>
    protected override int SoloDamageCap => 40;

    /// <summary>
    /// 沉重泥沼每次生效临时降低的力量。
    /// </summary>
    private const int StrengthLossPerTrigger = 2;

    /// <summary>
    /// 记录沉重泥沼累计扣过的力量，移除时需要返还；保存到存档，避免读档或联机重连后丢失返还值。
    /// </summary>
    private int _returnStrength;

    /// <summary>
    /// 保存沉重泥沼累计扣过的力量，读档时同步刷新描述显示。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int ReturnStrength
    {
        get => _returnStrength;
        set
        {
            _returnStrength = Math.Max(0, value);
            RefreshReturnStrengthVar();
        }
    }

    /// <summary>
    /// 文案动态变量：ReturnStrength 显示移除时会返还的力量。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars => base.CanonicalVars.Concat([new DynamicVar("ReturnStrength", 0)]);

    /// <summary>
    /// 沉重泥沼属于负面能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 使用计数器展示层数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 敌方回合开始时结算沉重泥沼伤害，并临时降低目标力量。
    /// </summary>
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (Owner.Side != side || !Owner.IsAlive)
        {
            return;
        }

        await PanKuDemonQiHelper.DealDemonQiPercentDamage(new BlockingPlayerChoiceContext(), Owner, Applier, null, 5, 5, 40);
        await PowerCmd.Apply<StrengthPower>(Owner, -StrengthLossPerTrigger, Applier ?? Owner, null);
        ReturnStrength += StrengthLossPerTrigger;
        RefreshReturnStrengthVar();
        MainFile.Logger.Info($"【沉重泥沼】回合开始生效：目标={Owner.Name}，力量下降={StrengthLossPerTrigger}，累计待返还力量={ReturnStrength}。");
    }

    /// <summary>
    /// 沉重泥沼移除时返还此前扣除的力量。
    /// </summary>
    /// <param name="oldOwner">移除前的持有者。</param>
    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (ReturnStrength > 0 && oldOwner.IsAlive)
        {
            await PowerCmd.Apply<StrengthPower>(oldOwner, ReturnStrength, oldOwner, null);
            MainFile.Logger.Info($"【沉重泥沼】移除返还力量：目标={oldOwner.Name}，返还力量={ReturnStrength}。");
        }

        ReturnStrength = 0;
        RefreshReturnStrengthVar();
    }

    /// <summary>
    /// 刷新描述里的返还力量数值。
    /// </summary>
    private void RefreshReturnStrengthVar()
    {
        if (DynamicVars.ContainsKey("ReturnStrength"))
        {
            DynamicVars["ReturnStrength"].BaseValue = ReturnStrength;
        }
    }
}

/// <summary>
/// 大地守望：连续若干回合未受伤后给全体敌人施加地缚。
/// </summary>
public class EarthPrisonPrepPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 大地守望属于玩家增益。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用持续时间展示剩余守望回合。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 受到生命伤害时重置守望，并移除地缚。
    /// </summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, MegaCrit.Sts2.Core.Models.CardModel? cardSource)
    {
        if (target != Owner || result.UnblockedDamage <= 0)
        {
            return;
        }

        await PowerCmd.SetAmount<EarthPrisonPrepPower>(Owner, 3, Owner, null);
        if (Owner.CombatState == null)
        {
            return;
        }

        foreach (Creature enemy in Owner.CombatState.Enemies)
        {
            await PowerCmd.Remove<EarthBindPower>(enemy);
            await PowerCmd.Remove<HeavyMirePower>(enemy);
        }
    }

    /// <summary>
    /// 回合结束时推进计数，归零后给全体敌人地缚并重置。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (Owner.Side != side)
        {
            return;
        }

        await PowerCmd.ModifyAmount(this, -1, Owner, null);
        if (Amount > 0)
        {
            return;
        }

        if (Owner.CombatState == null)
        {
            return;
        }

        foreach (Creature enemy in Owner.CombatState.Enemies.Where(enemy => enemy.IsAlive))
        {
            await PanKuDemonQiHelper.ApplyEarthBind(enemy, 1, Owner, null);
        }

        await PowerCmd.SetAmount<EarthPrisonPrepPower>(Owner, 3, Owner, null);
    }
}

/// <summary>
/// 月之解除限制：队伍共享的临时增益，存在期间恶魔异常伤害跳过最高上限。
/// </summary>
public class MoonUncapPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 月之解除限制属于玩家增益。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 不叠层，重复获得只刷新存在状态。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>
    /// 获得解除限制后刷新敌方恶魔异常预览，让描述立刻显示跳过上限后的伤害。
    /// </summary>
    /// <param name="applier">施加者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshEnemyDemonQiDamagePreviews(Owner, cardSource);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 玩家方下一次回合开始时移除，保证能覆盖敌方回合开始的恶魔异常自然结算。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <param name="side">开始回合的一方。</param>
    /// <param name="combatState">当前战斗状态。</param>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (Owner.Side != side)
        {
            return;
        }

        MainFile.Logger.Info($"【月之恶魔】解除限制结束：持有者={Owner.Name}。");
        await PowerCmd.Remove(this);
    }

    /// <summary>
    /// 解除限制移除后刷新敌方恶魔异常预览，让描述恢复为受上限约束的伤害。
    /// </summary>
    /// <param name="oldOwner">移除前的持有者。</param>
    public override Task AfterRemoved(Creature oldOwner)
    {
        RefreshEnemyDemonQiDamagePreviews(oldOwner, null);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 刷新所有敌人身上的伤害型恶魔异常预览。
    /// </summary>
    /// <param name="owner">解除限制持有者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    private static void RefreshEnemyDemonQiDamagePreviews(Creature owner, CardModel? cardSource)
    {
        if (owner.CombatState == null)
        {
            return;
        }

        foreach (Creature enemy in owner.CombatState.Enemies)
        {
            foreach (DemonQiDamagePower power in enemy.Powers.OfType<DemonQiDamagePower>().ToList())
            {
                power.RefreshDemonQiDamagePreview(cardSource);
            }
        }
    }
}

/// <summary>
/// 潘库恶魔门精英强化：用于展示与抢夺符咒一致的强化公式。
/// </summary>
public class PanKuElitePower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 潘库精英强化属于敌人增益。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器展示生命加成。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 公式变量：TotalHp 显示分配前总生命，Amount 显示当前怪实际获得生命。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Ascension", 0),
        new DynamicVar("PlayerCount", 1),
        new DynamicVar("TotalHp", 60),
        new DynamicVar("Strength", 2)
    ];

    /// <summary>
    /// 设置本次强化公式参数。
    /// </summary>
    /// <param name="ascensionLevel">进阶数。</param>
    /// <param name="playerCount">玩家人数。</param>
    /// <param name="strengthBonus">当前怪物实际获得的力量。</param>
    /// <param name="totalHpBonus">分配前的生命强化总量。</param>
    public void SetFormulaDetails(int ascensionLevel, int playerCount, int strengthBonus, int totalHpBonus)
    {
        DynamicVars["Ascension"].BaseValue = ascensionLevel;
        DynamicVars["PlayerCount"].BaseValue = playerCount;
        DynamicVars["TotalHp"].BaseValue = totalHpBonus;
        DynamicVars["Strength"].BaseValue = strengthBonus;
    }
}
