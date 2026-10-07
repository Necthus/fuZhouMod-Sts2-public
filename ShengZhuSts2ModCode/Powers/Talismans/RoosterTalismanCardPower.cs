using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Talismans;

/// <summary>
/// 酉鸡飞行能力：敏捷+N，受到的攻击伤害减50%。受到指定次数攻击伤害后击落并眩晕玩家1回合。
/// Amount 显示剩余受击次数，_dexApplied 存储敏捷增加量。
/// </summary>
public class RoosterTalismanCardPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 持有鸡符咒遗物时，击落改为失去的敏捷值。
    /// </summary>
    private const int RoosterRelicShotDownDexterityLoss = 2;

    /// <summary>
    /// 基础最大受击次数，超过后击落。
    /// </summary>
    private const int BaseMaxHits = 5;

    /// <summary>
    /// 持有鸡符咒遗物时的最大受击次数。
    /// </summary>
    private const int RoosterRelicMaxHits = 10;

    /// <summary>
    /// 剩余可承受的攻击次数。
    /// </summary>
    private int _hitsRemaining = BaseMaxHits;

    /// <summary>
    /// 记录施加的敏捷量，移除时需要扣回。
    /// </summary>
    private int _dexApplied;

    /// <summary>
    /// 标记是否已经施加了敏捷（避免重复施加）。
    /// </summary>
    private bool _dexInitialized;

    /// <summary>
    /// 酉鸡飞行属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器堆叠方式，显示剩余受击次数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// Dexterity 显示本能力提供的敏捷加成，避免占用 Amount 的剩余受击次数显示。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Dexterity", 0)];

    /// <summary>
    /// 初次获得能力时立即施加敏捷。
    /// </summary>
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner == null || _dexInitialized)
        {
            return;
        }

        _dexInitialized = true;
        _dexApplied = Amount;
        _hitsRemaining = GetMaxHitsForOwner();
        RefreshFlightDisplay();
        await PowerCmd.Apply<DexterityPower>(Owner, _dexApplied, Owner, null);
        MainFile.Logger.Info($"【酉鸡飞行】立即施加敏捷+{_dexApplied}，剩余受击次数={_hitsRemaining}。");
    }

    /// <summary>
    /// 叠加酉鸡时，补上新增敏捷和新增受击次数。
    /// </summary>
    public async Task AddStack(PlayerChoiceContext choiceContext, Creature owner, CardModel? cardSource, int dexAmount)
    {
        if (Owner == null || owner != Owner || dexAmount <= 0)
        {
            return;
        }

        _dexApplied += dexAmount;
        int addedHits = GetMaxHitsForOwner();
        _hitsRemaining += addedHits;
        RefreshFlightDisplay();

        await PowerCmd.Apply<DexterityPower>(Owner, dexAmount, Owner, cardSource, true);
        MainFile.Logger.Info($"【酉鸡飞行】叠加获得敏捷+{dexAmount}，新增受击次数={addedHits}，当前总敏捷加成={_dexApplied}，剩余受击次数={_hitsRemaining}。");
    }

    /// <summary>
    /// 移除飞行时扣回本能力施加的敏捷。
    /// </summary>
    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (_dexApplied > 0 && oldOwner.IsAlive)
        {
            await PowerCmd.Apply<DexterityPower>(oldOwner, -_dexApplied, oldOwner, null);
            MainFile.Logger.Info($"【酉鸡飞行】移除敏捷加成-{_dexApplied}。");
        }

        _dexApplied = 0;
    }

    /// <summary>
    /// 修改受到的攻击伤害乘数：返回0.5使伤害减半。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (Owner != null && target == Owner && props.IsPoweredAttack() && IsPrimaryFlightReductionSource())
        {
            return 0.5m;
        }

        return 1m;
    }

    /// <summary>
    /// 受到未格挡的攻击伤害后，减少剩余受击次数。归零时击落并眩晕玩家1回合。
    /// </summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Owner == null || target != Owner)
        {
            return;
        }

        // 对齐1代：只有普通攻击造成了实际HP伤害才计数。
        if (!props.IsPoweredAttack() || result.UnblockedDamage <= 0)
        {
            return;
        }

        // 自己卡牌造成的掉血不消耗酉鸡飞行次数；飞鹰展翅的普通飞行仍按原逻辑消耗。
        if (IsSelfCardDamage(dealer, cardSource))
        {
            MainFile.Logger.Info($"【酉鸡飞行】忽略自己卡牌造成的掉血：卡牌={cardSource?.Id.Entry ?? "空"}，剩余受击次数={_hitsRemaining}。");
            return;
        }

        _hitsRemaining--;
        RefreshFlightDisplay();
        Flash();

        MainFile.Logger.Info($"【酉鸡飞行】受到伤害，剩余受击次数={_hitsRemaining}。");

        if (_hitsRemaining <= 0)
        {
            if (HasRoosterRelic())
            {
                await PowerCmd.Apply<DexterityPower>(Owner, -RoosterRelicShotDownDexterityLoss, Owner, null);
                MainFile.Logger.Info($"【酉鸡飞行】鸡符咒共鸣：击落时不眩晕，改为失去{RoosterRelicShotDownDexterityLoss}点敏捷。");
            }
            else
            {
                await PowerCmd.Apply<RoosterShotDownPower>(Owner, 1, Owner, null);
                MainFile.Logger.Info("【酉鸡飞行】受击次数耗尽，击落！下个玩家回合将被跳过。");
            }

            await PowerCmd.Remove(this);
        }
    }

    /// <summary>
    /// 刷新酉鸡悬浮说明和图标数字：Amount 显示剩余受击次数，Dexterity 显示敏捷加成。
    /// </summary>
    private void RefreshFlightDisplay()
    {
        SetAmount(Math.Max(0, _hitsRemaining));
        DynamicVars["Dexterity"].BaseValue = _dexApplied;
    }

    /// <summary>
    /// 只让第一个飞行减伤能力生效，避免多张酉鸡或飞鹰展翅把减伤叠到 75%。
    /// </summary>
    private bool IsPrimaryFlightReductionSource()
    {
        if (Owner == null)
        {
            return false;
        }

        foreach (var power in Owner.Powers)
        {
            if (power is RoosterTalismanCardPower or FlightAfuPower)
            {
                return ReferenceEquals(power, this);
            }
        }

        return false;
    }

    /// <summary>
    /// 判断本次伤害是否来自持有者自己的卡牌掉血效果。
    /// </summary>
    /// <param name="dealer">伤害来源生物。</param>
    /// <param name="cardSource">伤害来源卡牌。</param>
    /// <returns>来自自己卡牌时返回 true。</returns>
    private bool IsSelfCardDamage(Creature? dealer, CardModel? cardSource)
    {
        if (Owner?.Player == null || dealer != Owner || cardSource?.Owner == null)
        {
            return false;
        }

        return ReferenceEquals(cardSource.Owner, Owner.Player) || cardSource.Owner.NetId == Owner.Player.NetId;
    }

    /// <summary>
    /// 判断当前玩家是否持有鸡符咒遗物。
    /// </summary>
    private bool HasRoosterRelic()
    {
        return Owner?.Player?.GetRelic<RoosterTalisman>() != null;
    }

    /// <summary>
    /// 根据当前能力持有者计算本次新增的可承受攻击次数。
    /// </summary>
    /// <returns>基础 5 次；持有鸡符咒时 10 次。</returns>
    private int GetMaxHitsForOwner()
    {
        return GetMaxHitsForPlayer(Owner?.Player);
    }

    /// <summary>
    /// 根据玩家是否持有鸡符咒计算可承受攻击次数。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>基础 5 次；持有鸡符咒时 10 次。</returns>
    public static int GetMaxHitsForPlayer(Player? player)
    {
        return player?.GetRelic<RoosterTalisman>() != null
            ? RoosterRelicMaxHits
            : BaseMaxHits;
    }
}

/// <summary>
/// 酉鸡击落：玩家下个回合开始时自动结束回合，用来模拟玩家被眩晕1回合。
/// </summary>
public class RoosterShotDownPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 击落属于负面能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 用计数器显示还会跳过多少个玩家回合。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 复用酉鸡飞行能力图标，避免新增美术资源。
    /// </summary>
    public override string CustomPackedIconPath => Path.Join(MainFile.ResPath, "images", "powers", "roostertalismancardpower.png");

    /// <summary>
    /// 复用酉鸡飞行能力大图，避免新增美术资源。
    /// </summary>
    public override string CustomBigIconPath => Path.Join(MainFile.ResPath, "images", "powers", "big", "roostertalismancardpower.png");

    /// <summary>
    /// 玩家回合开始时强制结束回合，模拟被击落后的眩晕。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null || Owner.IsDead || player.Creature != Owner)
        {
            return;
        }

        Flash();
        await PowerCmd.Remove(this);
        PlayerCmd.EndTurn(player, canBackOut: false);
        MainFile.Logger.Info("【酉鸡击落】玩家被击落眩晕，本回合已强制跳过。");
    }
}
