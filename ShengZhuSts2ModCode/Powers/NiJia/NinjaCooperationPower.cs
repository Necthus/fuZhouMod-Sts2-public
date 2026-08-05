using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

/// <summary>
/// 忍者协作能力：消耗堆中每张尼嘉-忍者团都会提高尼嘉-忍者团每段伤害。
/// </summary>
public class NinjaCooperationPower : ShengZhuSts2ModPower, IHasSecondAmount
{
    /// <summary>
    /// CurrentBonus 显示当前消耗堆提供的每段总伤害加成。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("CurrentBonus", 0)
    ];

    /// <summary>
    /// 忍者协作属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 可以叠层，每层都按消耗堆尼嘉-忍者团数量提供加成。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// IHasSecondAmount 实现：右侧数字显示当前每段总伤害加成。
    /// </summary>
    /// <returns>当前每段总伤害加成文本。</returns>
    public string GetSecondAmount()
    {
        RefreshDamageBonusDisplay(Owner?.Player, false);
        return GetCurrentDamageBonus(Owner?.Player).ToString();
    }

    /// <summary>
    /// 能力施加后刷新一次当前伤害加成显示。
    /// </summary>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshDamageBonusDisplay();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 能力层数变化后刷新当前伤害加成显示。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="power">发生变化的能力。</param>
    /// <param name="amount">本次变化量。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this)
        {
            RefreshDamageBonusDisplay();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 尼嘉-忍者团进入或离开消耗堆后刷新当前伤害加成显示。
    /// </summary>
    /// <param name="card">发生牌堆变化的卡牌。</param>
    /// <param name="oldPileType">旧牌堆。</param>
    /// <param name="source">变化来源。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? source)
    {
        if (IsRelevantNinjaPileChange(card, oldPileType))
        {
            RefreshDamageBonusDisplay();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取当前每段尼嘉-忍者团伤害加成。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>每段额外伤害。</returns>
    public int GetDamageBonus(Player? player)
    {
        RefreshDamageBonusDisplay(player);
        return GetCurrentDamageBonus(player);
    }

    /// <summary>
    /// 计算当前每段尼嘉-忍者团伤害加成。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>每段额外伤害。</returns>
    private int GetCurrentDamageBonus(Player? player)
    {
        if (player?.PlayerCombatState?.ExhaustPile == null || Amount <= 0)
        {
            return 0;
        }

        int exhaustedNinjaCount = player.PlayerCombatState.ExhaustPile.Cards.Count(card => card is NiJiaNinja);
        return exhaustedNinjaCount * Amount;
    }

    /// <summary>
    /// 刷新悬浮说明和第二数字中的当前总加成。
    /// </summary>
    /// <param name="player">当前玩家；为空时使用能力持有者。</param>
    /// <param name="notifySecondAmount">是否通知第二数字刷新。</param>
    private void RefreshDamageBonusDisplay(Player? player = null, bool notifySecondAmount = true)
    {
        if (!DynamicVars.ContainsKey("CurrentBonus"))
        {
            return;
        }

        int currentBonus = GetCurrentDamageBonus(player ?? Owner?.Player);
        if (DynamicVars["CurrentBonus"].BaseValue == currentBonus)
        {
            return;
        }

        DynamicVars["CurrentBonus"].BaseValue = currentBonus;
        if (notifySecondAmount)
        {
            this.InvokeSecondAmountChanged();
        }
    }

    /// <summary>
    /// 判断本次牌堆变化是否可能影响消耗堆中的尼嘉-忍者团数量。
    /// </summary>
    /// <param name="card">发生牌堆变化的卡牌。</param>
    /// <param name="oldPileType">旧牌堆。</param>
    /// <returns>可能影响时返回 true。</returns>
    private bool IsRelevantNinjaPileChange(CardModel card, PileType oldPileType)
    {
        if (Owner?.Player?.PlayerCombatState?.ExhaustPile == null || card is not NiJiaNinja || !IsOwnerCard(card))
        {
            return false;
        }

        return oldPileType == PileType.Exhaust
               || Owner.Player.PlayerCombatState.ExhaustPile.Cards.Contains(card);
    }

    /// <summary>
    /// 判断卡牌是否属于当前能力持有者，避免联机时误统计队友消耗堆。
    /// </summary>
    /// <param name="card">待检查卡牌。</param>
    /// <returns>属于当前玩家时返回 true。</returns>
    private bool IsOwnerCard(CardModel card)
    {
        if (Owner?.Player == null || card.Owner == null)
        {
            return false;
        }

        return ReferenceEquals(card.Owner, Owner.Player) || card.Owner.NetId == Owner.Player.NetId;
    }
}
