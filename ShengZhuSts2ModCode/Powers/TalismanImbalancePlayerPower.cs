using Godot;
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
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 虎符咒失衡能力：玩家持有超过 5 个符咒遗物且没有虎符咒时，本场战斗获得的常驻惩罚。
/// </summary>
public class TalismanImbalancePlayerPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 重度档位的打牌后受伤数值。
    /// </summary>
    private const int HeavyTierDamageAfterCardPlayed = 1;

    /// <summary>
    /// 阳失衡图标文件名。
    /// </summary>
    private const string YangIconFileName = "talismanimbalance_yang.png";

    /// <summary>
    /// 阴失衡图标文件名。
    /// </summary>
    private const string YinIconFileName = "talismanimbalance_yin.png";

    /// <summary>
    /// 创建能力前临时记录本次阴阳模式，避免战斗栏小图标在 Configure 前缓存成默认图标。
    /// </summary>
    private static readonly AsyncLocal<TalismanImbalanceMode> PendingModeForApply = new();

    /// <summary>
    /// 触发时的符咒遗物数量，保存后用于 SL 保持说明和倍率不变。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int TalismanCount { get; set; }

    /// <summary>
    /// 失衡档位：1=6~8，2=9~11。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int Tier { get; set; }

    /// <summary>
    /// 稳定随机出的减益枚举数值。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int DebuffKindValue { get; set; }

    /// <summary>
    /// 稳定随机出的减益层数。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int DebuffAmount { get; set; }

    /// <summary>
    /// 本次随机减益还剩多少个玩家回合后被失衡机制兜底移除。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int RemainingDebuffTurns { get; set; }

    /// <summary>
    /// 稳定随机出的阴阳模式枚举数值。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int ModeValue { get; set; }

    /// <summary>
    /// 攻防增强或削弱百分比。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int Percent { get; set; }

    /// <summary>
    /// 标记本能力已经由服务完成配置，避免未初始化状态参与修正。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public bool HasConfigured { get; set; }

    /// <summary>
    /// 失衡属于带收益的负面机制，标记为 Debuff 方便玩家识别代价。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 本体常驻本场战斗，不作为持续回合数递减。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>
    /// 根据阴阳模式和档位切换描述。
    /// </summary>
    protected override string SmartDescriptionLocKey => GetSmartDescriptionLocKey();

    /// <summary>
    /// 根据当前阴阳模式切换小图标。
    /// </summary>
    public override string CustomPackedIconPath => GetModeIconPath(false);

    /// <summary>
    /// 根据当前阴阳模式切换大图标。
    /// </summary>
    public override string CustomBigIconPath => GetModeIconPath(true);

    /// <summary>
    /// 创建虎符咒失衡能力时，先接收服务层已经随机好的阴阳模式。
    /// </summary>
    public TalismanImbalancePlayerPower()
    {
        if (PendingModeForApply.Value != TalismanImbalanceMode.None)
        {
            ModeValue = (int)PendingModeForApply.Value;
        }
    }

    /// <summary>
    /// 描述变量：符咒数、百分比、减益层数、重度打牌受伤。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("TalismanCount", 0),
        new DynamicVar("Percent", 0),
        new DynamicVar("DebuffAmount", 0),
        new DynamicVar("RemainingDebuffTurns", 0),
        new DynamicVar("PlayDamage", HeavyTierDamageAfterCardPlayed)
    ];

    /// <summary>
    /// 被施加后刷新描述变量。
    /// </summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshDescriptionVars();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 在 PowerCmd.Apply 创建能力前，临时指定本次阴阳模式。
    /// </summary>
    /// <param name="mode">本次稳定随机出的阴阳模式。</param>
    /// <returns>释放后恢复之前的临时模式。</returns>
    public static IDisposable UseModeForNextApply(TalismanImbalanceMode mode)
    {
        TalismanImbalanceMode previousMode = PendingModeForApply.Value;
        PendingModeForApply.Value = mode;
        return new PendingModeScope(previousMode);
    }

    /// <summary>
    /// 配置本场战斗稳定随机出的失衡结果。
    /// </summary>
    /// <param name="talismanCount">符咒遗物数量。</param>
    /// <param name="tier">失衡档位。</param>
    /// <param name="debuffKind">随机减益种类。</param>
    /// <param name="debuffAmount">随机减益层数。</param>
    /// <param name="mode">阴阳模式。</param>
    /// <param name="percent">增强或削弱百分比。</param>
    public void Configure(int talismanCount, int tier, TalismanImbalanceDebuffKind debuffKind, int debuffAmount, TalismanImbalanceMode mode, int percent)
    {
        TalismanCount = Math.Max(0, talismanCount);
        Tier = Math.Clamp(tier, 1, 2);
        DebuffKindValue = (int)debuffKind;
        DebuffAmount = Math.Max(0, debuffAmount);
        RemainingDebuffTurns = Math.Max(0, debuffAmount);
        ModeValue = (int)mode;
        Percent = Math.Max(0, percent);
        HasConfigured = true;
        RefreshDescriptionVars();
        Flash();
    }

    /// <summary>
    /// 回合抽牌数减少 1，按玩家自己分别生效，不影响队友。
    /// </summary>
    /// <param name="player">正在抽手牌的玩家。</param>
    /// <param name="count">原抽牌数。</param>
    /// <returns>修正后的抽牌数。</returns>
    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (!CanAffectPlayer(player))
        {
            return count;
        }

        RefreshDescriptionVars();
        return Math.Max(0m, count - 1m);
    }

    /// <summary>
    /// 玩家回合结束时推进随机减益兜底倒计时，确保非持续型减益也会按需求消失。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">刚结束回合的一方。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (Owner == null || side != Owner.Side || RemainingDebuffTurns <= 0)
        {
            return;
        }

        PowerModel? trackedDebuff = GetTrackedDebuffPower();
        if (trackedDebuff == null)
        {
            RemainingDebuffTurns = 0;
            RefreshDescriptionVars();
            MainFile.Logger.Info($"【虎符咒失衡】随机减益已提前不存在，停止兜底倒计时：玩家={FormatOwner()}，减益={GetDebuffDisplayName(CurrentDebuffKind)}。");
            return;
        }

        RemainingDebuffTurns = Math.Max(0, RemainingDebuffTurns - 1);
        RefreshDescriptionVars();
        if (RemainingDebuffTurns > 0)
        {
            return;
        }

        await PowerCmd.Remove(trackedDebuff);
        MainFile.Logger.Info($"【虎符咒失衡】随机减益到期移除：玩家={FormatOwner()}，减益={GetDebuffDisplayName(CurrentDebuffKind)}，初始回合数={DebuffAmount}。");
    }

    /// <summary>
    /// 重度失衡时，打出的每张牌结算完成后受到 1 点可被格挡的伤害。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">刚完成结算的出牌信息。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!CanTakeDamageAfterCardPlayed(cardPlay))
        {
            return;
        }

        RefreshDescriptionVars();
        await CreatureCmd.Damage(choiceContext, Owner, HeavyTierDamageAfterCardPlayed, ValueProp.Unpowered, Owner, null, cardPlay);
        MainFile.Logger.Info($"【虎符咒失衡】重度惩罚：玩家={FormatOwner()}，卡牌={cardPlay.Card.Id.Entry}，卡牌结算后受到{HeavyTierDamageAfterCardPlayed}点可格挡伤害。");
    }

    /// <summary>
    /// 修正来自玩家自己卡牌的伤害；黑影兵团牌走专用入口，避免倍率重复。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (!CanModifyCardDamage(cardSource, dealer))
        {
            return 1m;
        }

        RefreshDescriptionVars();
        return CurrentMode == TalismanImbalanceMode.Yang ? GetEnhancedMultiplier() : GetWeakenedMultiplier();
    }

    /// <summary>
    /// 修正来自玩家自己卡牌的格挡；黑影兵团牌走专用入口，避免倍率重复。
    /// </summary>
    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        CardModel? sourceCard = cardSource ?? cardPlay?.Card;
        if (!CanModifyCardBlock(target, sourceCard))
        {
            return 1m;
        }

        RefreshDescriptionVars();
        return CurrentMode == TalismanImbalanceMode.Yin ? GetEnhancedMultiplier() : GetWeakenedMultiplier();
    }

    /// <summary>
    /// 黑影兵团伤害专用修正入口：保留“卡面基础值 + 影噬”公式，只在最终值上应用失衡倍率。
    /// </summary>
    /// <param name="player">卡牌拥有者。</param>
    /// <param name="card">黑影兵团牌。</param>
    /// <param name="damage">当前伤害。</param>
    /// <returns>应用失衡后的伤害。</returns>
    public static int ModifyCardDamageForPlayer(Player? player, CardModel? card, int damage)
    {
        TalismanImbalancePlayerPower? power = player?.Creature?.GetPower<TalismanImbalancePlayerPower>();
        return power == null ? damage : power.ModifyShadowKhanDamage(card, damage);
    }

    /// <summary>
    /// 黑影兵团格挡专用修正入口：保留“卡面基础值 + 影噬”公式，只在最终值上应用失衡倍率。
    /// </summary>
    /// <param name="player">卡牌拥有者。</param>
    /// <param name="card">黑影兵团牌。</param>
    /// <param name="block">当前格挡。</param>
    /// <returns>应用失衡后的格挡。</returns>
    public static int ModifyCardBlockForPlayer(Player? player, CardModel? card, int block)
    {
        TalismanImbalancePlayerPower? power = player?.Creature?.GetPower<TalismanImbalancePlayerPower>();
        return power == null ? block : power.ModifyShadowKhanBlock(card, block);
    }

    /// <summary>
    /// 获取减益中文名，供日志使用。
    /// </summary>
    public static string GetDebuffDisplayName(TalismanImbalanceDebuffKind debuffKind)
    {
        return debuffKind switch
        {
            TalismanImbalanceDebuffKind.Vulnerable => "易伤",
            TalismanImbalanceDebuffKind.Frail => "脆弱",
            TalismanImbalanceDebuffKind.Debilitate => "摧残",
            TalismanImbalanceDebuffKind.WasteAway => "衰朽",
            TalismanImbalanceDebuffKind.Tender => "柔嫩",
            //TalismanImbalanceDebuffKind.Sloth => "懒惰",
            TalismanImbalanceDebuffKind.Constrict => "紧缠",
            TalismanImbalanceDebuffKind.Tangled => "缠结",
            TalismanImbalanceDebuffKind.Shrink => "缩小",
            TalismanImbalanceDebuffKind.Disintegration => "瓦解",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取阴阳模式中文名，供日志使用。
    /// </summary>
    public static string GetModeDisplayName(TalismanImbalanceMode mode)
    {
        return mode switch
        {
            TalismanImbalanceMode.Yang => "阳",
            TalismanImbalanceMode.Yin => "阴",
            _ => "未定"
        };
    }

    /// <summary>
    /// 当前保存的阴阳模式。
    /// </summary>
    private TalismanImbalanceMode CurrentMode => Enum.IsDefined(typeof(TalismanImbalanceMode), ModeValue)
        ? (TalismanImbalanceMode)ModeValue
        : TalismanImbalanceMode.None;

    /// <summary>
    /// 当前保存的随机减益种类。
    /// </summary>
    private TalismanImbalanceDebuffKind CurrentDebuffKind => Enum.IsDefined(typeof(TalismanImbalanceDebuffKind), DebuffKindValue)
        ? (TalismanImbalanceDebuffKind)DebuffKindValue
        : TalismanImbalanceDebuffKind.Vulnerable;

    /// <summary>
    /// 判断失衡能力是否已具备完整配置。
    /// </summary>
    private bool IsConfigured => HasConfigured && CurrentMode != TalismanImbalanceMode.None && Percent > 0;

    /// <summary>
    /// 选择当前应使用的描述 key。
    /// </summary>
    private string GetSmartDescriptionLocKey()
    {
        string suffix = CurrentMode switch
        {
            TalismanImbalanceMode.Yang => Tier >= 2 ? ".smartDescription_yang_heavy" : ".smartDescription_yang",
            TalismanImbalanceMode.Yin => Tier >= 2 ? ".smartDescription_yin_heavy" : ".smartDescription_yin",
            _ => ".smartDescription"
        };

        return Id.Entry + suffix;
    }

    /// <summary>
    /// 根据当前阴阳模式获取项目内图标路径。
    /// </summary>
    /// <param name="useBigImage">是否使用大图目录。</param>
    /// <returns>当前模式对应的图标路径。</returns>
    private string GetModeIconPath(bool useBigImage)
    {
        TalismanImbalanceMode iconMode = CurrentMode != TalismanImbalanceMode.None
            ? CurrentMode
            : PendingModeForApply.Value;
        if (iconMode == TalismanImbalanceMode.None)
        {
            return useBigImage ? base.CustomBigIconPath : base.CustomPackedIconPath;
        }

        string fileName = iconMode == TalismanImbalanceMode.Yin ? YinIconFileName : YangIconFileName;
        string iconPath = useBigImage
            ? Path.Join(MainFile.ResPath, "images", "powers", "big", fileName)
            : Path.Join(MainFile.ResPath, "images", "powers", fileName);

        return ResourceLoader.Exists(iconPath)
            ? iconPath
            : useBigImage ? base.CustomBigIconPath : base.CustomPackedIconPath;
    }

    /// <summary>
    /// 判断当前抽牌修正是否应影响该玩家。
    /// </summary>
    private bool CanAffectPlayer(Player player)
    {
        return IsConfigured && Owner?.Player != null && (ReferenceEquals(player, Owner.Player) || player.NetId == Owner.Player.NetId);
    }

    /// <summary>
    /// 判断重度档位的打牌后受伤是否可以触发。
    /// </summary>
    private bool CanTakeDamageAfterCardPlayed(CardPlay cardPlay)
    {
        return IsConfigured
            && Tier >= 2
            && Owner != null
            && Owner.IsAlive
            && IsOwnedCard(cardPlay.Card);
    }

    /// <summary>
    /// 判断普通伤害修正是否可以影响本次卡牌伤害。
    /// </summary>
    private bool CanModifyCardDamage(CardModel? cardSource, Creature? dealer)
    {
        if (!IsConfigured || Owner == null || cardSource == null || cardSource is BaseShadowKhanCard)
        {
            return false;
        }

        return IsOwnedCard(cardSource) && (dealer == null || ReferenceEquals(dealer, Owner));
    }

    /// <summary>
    /// 判断普通格挡修正是否可以影响本次卡牌格挡。
    /// </summary>
    private bool CanModifyCardBlock(Creature target, CardModel? sourceCard)
    {
        if (!IsConfigured || Owner == null || target != Owner || sourceCard == null || sourceCard is BaseShadowKhanCard)
        {
            return false;
        }

        return IsOwnedCard(sourceCard);
    }

    /// <summary>
    /// 判断卡牌是否属于当前失衡玩家。
    /// </summary>
    private bool IsOwnedCard(CardModel? card)
    {
        if (Owner?.Player == null || card?.Owner == null)
        {
            return false;
        }

        return ReferenceEquals(card.Owner, Owner.Player) || card.Owner.NetId == Owner.Player.NetId;
    }

    /// <summary>
    /// 对黑影兵团伤害应用失衡倍率。
    /// </summary>
    private int ModifyShadowKhanDamage(CardModel? card, int damage)
    {
        if (!IsConfigured || card is not BaseShadowKhanCard || damage <= 0)
        {
            return damage;
        }

        decimal multiplier = CurrentMode == TalismanImbalanceMode.Yang ? GetEnhancedMultiplier() : GetWeakenedMultiplier();
        return ApplyFinalMultiplier(damage, multiplier);
    }

    /// <summary>
    /// 对黑影兵团格挡应用失衡倍率。
    /// </summary>
    private int ModifyShadowKhanBlock(CardModel? card, int block)
    {
        if (!IsConfigured || card is not BaseShadowKhanCard || block <= 0)
        {
            return block;
        }

        decimal multiplier = CurrentMode == TalismanImbalanceMode.Yin ? GetEnhancedMultiplier() : GetWeakenedMultiplier();
        return ApplyFinalMultiplier(block, multiplier);
    }

    /// <summary>
    /// 获取本次失衡随机施加的减益 Power。
    /// </summary>
    private PowerModel? GetTrackedDebuffPower()
    {
        if (Owner == null)
        {
            return null;
        }

        return CurrentDebuffKind switch
        {
            TalismanImbalanceDebuffKind.Vulnerable => Owner.GetPower<VulnerablePower>(),
            TalismanImbalanceDebuffKind.Frail => Owner.GetPower<FrailPower>(),
            TalismanImbalanceDebuffKind.Debilitate => Owner.GetPower<DebilitatePower>(),
            TalismanImbalanceDebuffKind.WasteAway => Owner.GetPower<WasteAwayPower>(),
            TalismanImbalanceDebuffKind.Tender => Owner.GetPower<TenderPower>(),
            TalismanImbalanceDebuffKind.Constrict => Owner.GetPower<ConstrictPower>(),
            TalismanImbalanceDebuffKind.Tangled => Owner.GetPower<TangledPower>(),
            TalismanImbalanceDebuffKind.Shrink => Owner.GetPower<ShrinkPower>(),
            TalismanImbalanceDebuffKind.Disintegration => Owner.GetPower<DisintegrationPower>(),
            _ => null
        };
    }

    /// <summary>
    /// 获取增强倍率。
    /// </summary>
    private decimal GetEnhancedMultiplier()
    {
        return 1m + Math.Max(0, Percent) / 100m;
    }

    /// <summary>
    /// 获取削弱倍率，最低不低于 0。
    /// </summary>
    private decimal GetWeakenedMultiplier()
    {
        return Math.Max(0m, 1m - Math.Max(0, Percent) / 100m);
    }

    /// <summary>
    /// 对最终整数值应用倍率。
    /// </summary>
    private static int ApplyFinalMultiplier(int value, decimal multiplier)
    {
        return Math.Max(0, (int)Math.Floor(value * multiplier));
    }

    /// <summary>
    /// 刷新能力描述里的动态变量和图标数字。
    /// </summary>
    private void RefreshDescriptionVars()
    {
        if (Owner != null && Percent > 0)
        {
            SetAmount(Percent);
        }

        if (DynamicVars.ContainsKey("TalismanCount"))
        {
            DynamicVars["TalismanCount"].BaseValue = Math.Max(0, TalismanCount);
        }

        if (DynamicVars.ContainsKey("Percent"))
        {
            DynamicVars["Percent"].BaseValue = Math.Max(0, Percent);
        }

        if (DynamicVars.ContainsKey("DebuffAmount"))
        {
            DynamicVars["DebuffAmount"].BaseValue = Math.Max(0, DebuffAmount);
        }

        if (DynamicVars.ContainsKey("RemainingDebuffTurns"))
        {
            DynamicVars["RemainingDebuffTurns"].BaseValue = Math.Max(0, RemainingDebuffTurns);
        }

        if (DynamicVars.ContainsKey("PlayDamage"))
        {
            DynamicVars["PlayDamage"].BaseValue = HeavyTierDamageAfterCardPlayed;
        }
    }

    /// <summary>
    /// 格式化当前能力拥有者，方便联机日志排查。
    /// </summary>
    private string FormatOwner()
    {
        return Owner?.Player == null ? "未知玩家" : $"{Owner.Player.NetId}/{Owner.Player.Character.Id.Entry}";
    }

    /// <summary>
    /// 还原创建能力前的临时阴阳模式。
    /// </summary>
    /// <param name="previousMode">进入作用域前的阴阳模式。</param>
    private sealed class PendingModeScope(TalismanImbalanceMode previousMode) : IDisposable
    {
        /// <summary>
        /// 防止重复释放导致临时模式被错误覆盖。
        /// </summary>
        private bool _isDisposed;

        /// <summary>
        /// 释放临时阴阳模式上下文。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            PendingModeForApply.Value = previousMode;
            _isDisposed = true;
        }
    }
}
