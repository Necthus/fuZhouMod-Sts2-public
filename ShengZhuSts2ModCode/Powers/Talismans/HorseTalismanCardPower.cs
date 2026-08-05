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
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Talismans;

/// <summary>
/// 午马能力：追踪本回合造成的伤害，回合结束时按比例回复HP，然后移除自身。
/// </summary>
public class HorseTalismanCardPower : ShengZhuSts2ModPower, IHasSecondAmount
{
    /// <summary>
    /// 持有马符咒遗物时，1点溢出治疗转化的格挡值。
    /// </summary>
    private const int OverflowHealToBlockRate = 5;

    /// <summary>
    /// 默认治疗比例，避免异常情况下没有从卡牌传入比例。
    /// </summary>
    private const int DefaultHealPercent = 30;

    /// <summary>
    /// 本次午马治疗比例。
    /// </summary>
    private int _healPercent = DefaultHealPercent;

    /// <summary>
    /// 午马属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器堆叠方式，Amount显示本回合已经造成的实际伤害。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// Heal显示回合结束时将恢复的生命值，Block显示马符咒会把溢出治疗转化出的格挡。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Heal", 0), new DynamicVar("Block", 0)];

    /// <summary>
    /// 初次获得能力时，将传入的Amount解释为治疗比例，然后把Amount重置为伤害计数。
    /// </summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        _healPercent = Amount > 0 ? Amount : DefaultHealPercent;
        SetAmount(0);
        RefreshHealDisplay();
        return Task.CompletedTask;
    }

    /// <summary>
    /// IHasSecondAmount实现：右侧数字显示回合结束会恢复的生命值。
    /// </summary>
    public string GetSecondAmount()
    {
        return GetHealAmount().ToString();
    }

    /// <summary>
    /// 每当玩家实际对敌人造成未格挡伤害后，累计真实伤害。
    /// </summary>
    public override Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (Owner == null || dealer != Owner || target == Owner || result.UnblockedDamage <= 0)
        {
            return Task.CompletedTask;
        }

        SetAmount(Amount + result.UnblockedDamage);
        RefreshHealDisplay();
        Flash();

        return Task.CompletedTask;
    }

    /// <summary>
    /// 每当玩家受到伤害后刷新溢出治疗转化格挡预览。
    /// </summary>
    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Owner != null && target == Owner)
        {
            RefreshHealDisplay();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 回合结束时按比例回复本回合造成伤害，然后移除自身。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side != CombatSide.Player || Owner == null)
        {
            return;
        }

        await HealFromStoredDamage();
        await PowerCmd.Remove(this);
    }

    /// <summary>
    /// 战斗结束时也结算回血，避免本回合击杀最后敌人时漏掉午马治疗。
    /// </summary>
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        await HealFromStoredDamage();
    }

    /// <summary>
    /// 根据当前累计伤害计算回合结束治疗量。
    /// </summary>
    private int GetHealAmount()
    {
        return Amount * _healPercent / 100;
    }

    /// <summary>
    /// 计算当前治疗量会溢出的生命值；只有持有马符咒时才会转换为格挡。
    /// </summary>
    /// <param name="healAmount">本次预计治疗量。</param>
    /// <returns>溢出治疗量。</returns>
    private int GetOverflowHealAmount(int healAmount)
    {
        if (!HasHorseRelic() || Owner == null)
        {
            return 0;
        }

        decimal missingHp = Math.Max(0m, Owner.MaxHp - Owner.CurrentHp);
        return (int)Math.Max(0m, healAmount - missingHp);
    }

    /// <summary>
    /// 计算当前溢出治疗能转换出的格挡值。
    /// </summary>
    /// <returns>预计格挡值。</returns>
    private int GetOverflowBlockAmount()
    {
        return GetOverflowHealAmount(GetHealAmount()) * OverflowHealToBlockRate;
    }

    /// <summary>
    /// 刷新悬浮说明和第二数字中的治疗预览。
    /// </summary>
    private void RefreshHealDisplay()
    {
        DynamicVars["Heal"].BaseValue = GetHealAmount();
        DynamicVars["Block"].BaseValue = GetOverflowBlockAmount();
        this.InvokeSecondAmountChanged();
    }

    /// <summary>
    /// 按累计实际伤害和当前治疗比例治疗玩家，并清空本次累计。
    /// </summary>
    private async Task HealFromStoredDamage()
    {
        if (Owner == null)
        {
            return;
        }

        int damageDealt = Amount;
        int healAmount = GetHealAmount();
        if (healAmount > 0)
        {
            int overflowHeal = GetOverflowHealAmount(healAmount);

            Flash();
            await CreatureCmd.Heal(Owner, healAmount);
            MainFile.Logger.Info($"【午马】结算治疗：本回合实际造成伤害{damageDealt}，治疗比例{_healPercent}%，回复{healAmount}HP。");

            if (overflowHeal > 0)
            {
                int blockAmount = overflowHeal * OverflowHealToBlockRate;
                await CreatureCmd.GainBlock(Owner, blockAmount, ValueProp.Unpowered, null);
                MainFile.Logger.Info($"【午马】马符咒共鸣：溢出治疗{overflowHeal}，按1:{OverflowHealToBlockRate}转化为{blockAmount}点格挡。");
            }
        }

        SetAmount(0);
        RefreshHealDisplay();
    }

    /// <summary>
    /// 判断当前玩家是否持有马符咒遗物。
    /// </summary>
    private bool HasHorseRelic()
    {
        return Owner?.Player?.GetRelic<HorseTalisman>() != null;
    }
}
