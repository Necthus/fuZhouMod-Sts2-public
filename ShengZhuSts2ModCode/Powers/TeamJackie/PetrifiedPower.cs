using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.TeamJackie;

/// <summary>
/// 石化能力：目标每回合逐步获得力量，受到攻击伤害时按层数提高伤害。
/// </summary>
public class PetrifiedPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 默认每层提高的攻击伤害百分比。
    /// </summary>
    private const int DefaultPercentPerStack = 10;

    /// <summary>
    /// 石化层数上限。
    /// </summary>
    private const int MaxAmount = 10;

    /// <summary>
    /// 每层提高的攻击伤害百分比。
    /// </summary>
    private int _percentPerStack = DefaultPercentPerStack;

    /// <summary>
    /// 每层提高的攻击伤害百分比，保存到存档以避免升级版石化读档后丢失数值。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int PercentPerStack
    {
        get => _percentPerStack;
        set
        {
            _percentPerStack = value > 0 ? value : DefaultPercentPerStack;
            RefreshDescriptionValues();
        }
    }

    /// <summary>
    /// 小图直接使用石化卡牌图。
    /// </summary>
    public override string CustomPackedIconPath => Path.Join(MainFile.ResPath, "images", "card_portraits", "petrification.png");

    /// <summary>
    /// 大图直接使用石化卡牌图。
    /// </summary>
    public override string CustomBigIconPath => Path.Join(MainFile.ResPath, "images", "card_portraits", "big", "petrification.png");

    /// <summary>
    /// PercentPerStack 显示每层增伤，TotalPercent 显示当前总增伤。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("PercentPerStack", PercentPerStack),
        new DynamicVar("TotalPercent", 0)
    ];

    /// <summary>
    /// 能力类型：负面。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 层数显示为计数器。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 初次施加时给目标1点力量，并记录每层百分比。
    /// </summary>
    public async Task Configure(PlayerChoiceContext choiceContext, Creature target, Creature? source, CardModel? cardSource, int percentPerStack)
    {
        PercentPerStack = percentPerStack;
        RefreshDescriptionValues();
        await PowerCmd.Apply<StrengthPower>(target, 1, source, cardSource);
    }

    /// <summary>
    /// 叠加石化时补力量，层数最高10。
    /// </summary>
    public async Task AddPetrifiedStack(PlayerChoiceContext choiceContext, Creature target, Creature? source, CardModel? cardSource, int addAmount, int percentPerStack)
    {
        PercentPerStack = percentPerStack;
        RefreshDescriptionValues();
        int before = Amount;
        int targetAmount = Math.Min(MaxAmount, Amount + addAmount);
        if (targetAmount > before)
        {
            await PowerCmd.Apply<StrengthPower>(target, targetAmount - before, source, cardSource);
            await PowerCmd.ModifyAmount(this, targetAmount - before, source, cardSource);
        }
    }

    /// <summary>
    /// 目标回合开始时，若未到10层，石化加深并再给1力量。
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (Owner == null || side != CombatSide.Enemy || Amount >= MaxAmount)
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(Owner, 1, null, null);
        await PowerCmd.ModifyAmount(this, 1, null, null);
    }

    /// <summary>
    /// 能力层数变化时刷新描述中的当前总增伤。
    /// </summary>
    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            RefreshDescriptionValues();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 按石化层数提高目标受到的攻击伤害。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Owner == null || target != Owner || !props.IsPoweredAttack())
        {
            return 1m;
        }

        return 1m + Amount * PercentPerStack / 100m;
    }

    /// <summary>
    /// 刷新能力描述里展示的每层增伤和当前总增伤。
    /// </summary>
    private void RefreshDescriptionValues()
    {
        if (!DynamicVars.ContainsKey("PercentPerStack") || !DynamicVars.ContainsKey("TotalPercent"))
        {
            return;
        }

        DynamicVars["PercentPerStack"].BaseValue = PercentPerStack;
        DynamicVars["TotalPercent"].BaseValue = Amount * PercentPerStack;
    }
}
