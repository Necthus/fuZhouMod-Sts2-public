using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 牛符咒：右键激发后，下一张攻击牌在伤害预览和实际结算中造成2倍伤害，然后进入2回合冷却。
/// </summary>
public class OxTalisman : ShengZhuSts2ModRelic, ITalismanRightClickable, ITalismanCooldownResettable, ITalismanInteractionState
{
    /// <summary>
    /// 是否已激发。
    /// </summary>
    private bool _activated;

    /// <summary>
    /// 是否已激发，写入存档和联机状态，避免伤害翻倍分支不一致。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public bool Activated
    {
        get => _activated;
        set => _activated = value;
    }

    /// <summary>
    /// 冷却剩余回合数。
    /// </summary>
    private int _cooldownTurnsRemaining;

    /// <summary>
    /// 冷却剩余回合数，写入存档和联机状态，避免一端可用一端冷却。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int SavedCooldownTurnsRemaining
    {
        get => _cooldownTurnsRemaining;
        set => _cooldownTurnsRemaining = Math.Max(0, value);
    }

    /// <summary>
    /// 遗物稀有度：稀有。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Rare;

    /// <summary>
    /// 显示冷却计数器。
    /// </summary>
    public override bool ShowCounter => _cooldownTurnsRemaining > 0;

    /// <summary>
    /// 计数器显示值：冷却中显示剩余回合，激发中显示特殊标记。
    /// </summary>
    public override int DisplayAmount => _cooldownTurnsRemaining;

    /// <summary>
    /// 当前是否处于激发状态。
    /// </summary>
    public bool IsActivated => _activated;

    /// <summary>
    /// 当前剩余冷却回合数。
    /// </summary>
    public int CooldownTurnsRemaining => _cooldownTurnsRemaining;

    /// <summary>
    /// 是否处于冷却中（供兔符咒刷新用）。
    /// </summary>
    public bool IsOnCooldown => _cooldownTurnsRemaining > 0;

    /// <summary>
    /// 战斗开始时重置状态。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        _activated = false;
        _cooldownTurnsRemaining = 0;
        UpdateTalismanVisualState(_activated, _cooldownTurnsRemaining);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 右键点击：切换激发状态。
    /// </summary>
    public Task OnRightClick(PlayerChoiceContext context)
    {
        if (_cooldownTurnsRemaining > 0)
        {
            return Task.CompletedTask;
        }

        return RelicInteractionSyncService.RequestSetActivated(this, !_activated);
    }

    /// <summary>
    /// 出牌后：若已激发且打出攻击牌，则消耗激发状态并进入冷却。
    /// 鼠符咒共鸣：额外获得1层永久力量。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_activated || cardPlay?.Card == null || Owner?.Creature == null || !IsCardPlayedByOwner(cardPlay))
        {
            return;
        }

        // 只对攻击牌生效
        if (cardPlay.Card.Type != CardType.Attack)
        {
            return;
        }

        _activated = false;
        _cooldownTurnsRemaining = 2;
        UpdateTalismanVisualState(_activated, _cooldownTurnsRemaining);
        RefreshHandAttackPreviews();
        Flash();

        // 鼠符咒共鸣：额外获得1层永久力量
        if (HasRatResonance())
        {
            await PowerCmd.Apply<StrengthPower>(Owner.Creature, 1, Owner.Creature, null);
        }

        // 伤害翻倍已经在 ModifyDamageAdditive 中参与预览和结算，这里只负责消耗状态。
    }

    /// <summary>
    /// 修改攻击牌伤害：牛符咒激发时，在加法修正阶段额外增加一次当前伤害，使卡牌预览与实际结算都显示为翻倍后数值。
    /// </summary>
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!_activated || _cooldownTurnsRemaining > 0)
        {
            return 0m;
        }

        if (dealer != Owner?.Creature || cardSource == null || cardSource.Type != CardType.Attack)
        {
            return 0m;
        }

        if (amount <= 0m)
        {
            return 0m;
        }

        return amount;
    }

    /// <summary>
    /// 回合结束时递减冷却。
    /// </summary>
    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side == CombatSide.Player && _cooldownTurnsRemaining > 0)
        {
            _cooldownTurnsRemaining--;
            UpdateTalismanVisualState(_activated, _cooldownTurnsRemaining);
            RefreshHandAttackPreviews();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 重置冷却（供兔符咒调用）。
    /// </summary>
    public void ResetCooldown()
    {
        _cooldownTurnsRemaining = 0;
        _activated = false;
        UpdateTalismanVisualState(_activated, _cooldownTurnsRemaining);
        RefreshHandAttackPreviews();
        Flash();
    }

    /// <summary>
    /// 设置激发状态（供半自动模式调用）。
    /// </summary>
    public void SetActivated(bool activate)
    {
        if (_cooldownTurnsRemaining > 0) return;
        _activated = activate;
        UpdateTalismanVisualState(_activated, _cooldownTurnsRemaining);
        RefreshHandAttackPreviews();
        if (activate) Flash();
    }

    /// <summary>
    /// 刷新手牌攻击牌预览：牛符咒开关后，主动让卡面伤害数字重新计算，避免显示停留在上一次状态。
    /// </summary>
    private void RefreshHandAttackPreviews()
    {
        var combatState = Owner?.PlayerCombatState;
        if (combatState == null)
        {
            return;
        }

        int refreshedCount = 0;
        int missingNodeCount = 0;
        foreach (CardModel card in combatState.Hand.Cards.Where(card => card.Type == CardType.Attack).ToList())
        {
            NCard? cardNode = NCard.FindOnTable(card, PileType.Hand);
            if (cardNode == null)
            {
                missingNodeCount++;
                continue;
            }

            cardNode.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
            refreshedCount++;
        }
    }
}
