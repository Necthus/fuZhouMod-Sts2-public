using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Talismans;

/// <summary>
/// 戌狗免死能力：在持续回合内，致命伤害不会让你死亡，触发时回复HP并获得无实体。
/// Amount 存储剩余持续回合数，_reviveCount 存储剩余免死次数。
/// </summary>
public class DogTalismanCardPower : ShengZhuSts2ModPower, IHasSecondAmount
{
    /// <summary>
    /// 默认触发免死时回复的HP量。
    /// </summary>
    private const int DefaultReviveHealAmount = 10;

    /// <summary>
    /// 触发免死时希望稳定到的HP量。
    /// </summary>
    private int _reviveHealAmount = DefaultReviveHealAmount;

    /// <summary>
    /// 剩余免死次数。
    /// </summary>
    private int _reviveCount = 1;

    /// <summary>
    /// 本能力是否已经触发过免死。
    /// </summary>
    private bool _reviveTriggered;

    /// <summary>
    /// 未触发免死的最大生命奖励是否已经发放，避免回合结束和战斗结束重复奖励。
    /// </summary>
    private bool _maxHpRewardGranted;

    /// <summary>
    /// 戌狗属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器堆叠方式，显示剩余回合数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// Revives显示剩余免死次数。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Revives", 1), new DynamicVar("Heal", DefaultReviveHealAmount)];

    /// <summary>
    /// 初次获得能力时，从卡牌读取免死后的回复量。
    /// </summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (cardSource?.DynamicVars.TryGetValue("Magic", out DynamicVar? magicVar) == true)
        {
            _reviveHealAmount = (int)magicVar.BaseValue;
        }

        RefreshReviveDisplay();
        Owner?.Player?.GetRelic<DogTalisman>()?.RegisterDogCardPowerRewardCandidate("获得戌狗Buff");
        return Task.CompletedTask;
    }

    /// <summary>
    /// IHasSecondAmount实现：右侧数字显示剩余免死次数。
    /// </summary>
    public string GetSecondAmount()
    {
        return _reviveCount.ToString();
    }

    /// <summary>
    /// 叠加戌狗能力：增加持续回合，并额外增加1次免死。
    /// </summary>
    public async Task AddStack(int turns, int reviveHealAmount)
    {
        _reviveCount++;
        _reviveHealAmount = Math.Max(_reviveHealAmount, reviveHealAmount);
        RefreshReviveDisplay();
        Owner?.Player?.GetRelic<DogTalisman>()?.RegisterDogCardPowerRewardCandidate("叠加戌狗Buff");
        await PowerCmd.ModifyAmount(this, Math.Max(0, turns), Owner, null, false);
        MainFile.Logger.Info($"【戌狗】叠加免死能力：增加{turns}回合，剩余免死次数={_reviveCount}，触发后生命稳定到约{_reviveHealAmount}。");
    }

    /// <summary>
    /// 修改实际将要损失的生命值：如果会致死且还有免死次数，则只掉到1HP。
    /// 使用 BeforeOsty 阶段，让戌狗先于狗符咒、瓶装精灵、蜥蜴尾巴等免死资源触发。
    /// </summary>
    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Owner == null || target != Owner || _reviveCount <= 0 || dealer == null || dealer == Owner)
        {
            return amount;
        }

        decimal currentHp = Owner.CurrentHp;
        if (currentHp > 0 && amount >= currentHp)
        {
            _reviveCount--;
            _reviveTriggered = true;
            RefreshReviveDisplay();
            Flash();
            _needsHeal = true;
            Owner.Player?.GetRelic<DogTalisman>()?.MarkDogCardPowerTriggered();

            MainFile.Logger.Info($"【戌狗】免死触发！实际致命伤害{amount}被降为{currentHp - 1}，剩余免死次数={_reviveCount}。");
            return currentHp - 1;
        }

        return amount;
    }

    /// <summary>
    /// 标记是否需要在伤害后回血。
    /// </summary>
    private bool _needsHeal;

    /// <summary>
    /// 受到伤害后，如果触发了免死则回血。
    /// </summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Owner == null || target != Owner || !_needsHeal)
        {
            return;
        }

        _needsHeal = false;
        int healAfterSaveAmount = Math.Max(0, _reviveHealAmount - 1);
        await CreatureCmd.Heal(Owner, healAfterSaveAmount);
        await PowerCmd.Apply<IntangiblePower>(Owner, 1, Owner, null);
        MainFile.Logger.Info($"【戌狗】免死后回复{healAfterSaveAmount}HP，使生命稳定到约{_reviveHealAmount}HP，并获得1层无实体。");

        // 如果免死次数耗尽，移除自身
        if (_reviveCount <= 0)
        {
            await PowerCmd.Remove(this);
            MainFile.Logger.Info("【戌狗】免死次数耗尽，能力移除。");
        }
    }

    /// <summary>
    /// 回合结束时递减持续回合数，归零时移除。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side != CombatSide.Player || Owner == null)
        {
            return;
        }

        if (Amount <= 1 || _reviveCount <= 0)
        {
            await TryGrantUntriggeredMaxHpReward("持续回合耗尽");
            await PowerCmd.Remove(this);
            MainFile.Logger.Info("【戌狗】持续回合耗尽或免死次数为0，能力移除。");
        }
        else
        {
            await PowerCmd.ModifyAmount(this, -1, null, null, false);
            MainFile.Logger.Info($"【戌狗】回合结束，剩余回合数={Amount - 1}，免死次数={_reviveCount}。");
        }
    }

    /// <summary>
    /// 战斗结束时，如果免死没有触发且仍持有狗符咒遗物，奖励最大生命。
    /// </summary>
    public override async Task AfterCombatEnd(MegaCrit.Sts2.Core.Rooms.CombatRoom room)
    {
        await TryGrantUntriggeredMaxHpReward("战斗胜利");
    }

    /// <summary>
    /// 刷新剩余免死次数的悬浮说明和第二数字。
    /// </summary>
    private void RefreshReviveDisplay()
    {
        DynamicVars["Revives"].BaseValue = _reviveCount;
        DynamicVars["Heal"].BaseValue = _reviveHealAmount;
        this.InvokeSecondAmountChanged();
    }

    /// <summary>
    /// 持有狗符咒遗物时，若本能力未触发免死，则发放一次最大生命奖励。
    /// </summary>
    private async Task TryGrantUntriggeredMaxHpReward(string reason)
    {
        if (_reviveTriggered || _maxHpRewardGranted || Owner == null || Owner.IsDead)
        {
            return;
        }

        DogTalisman? dogTalisman = Owner.Player?.GetRelic<DogTalisman>();
        if (dogTalisman == null)
        {
            return;
        }

        bool granted = await dogTalisman.TryGrantDogCardPowerUntriggeredMaxHpReward(reason);
        if (granted)
        {
            _maxHpRewardGranted = true;
        }
    }
}
