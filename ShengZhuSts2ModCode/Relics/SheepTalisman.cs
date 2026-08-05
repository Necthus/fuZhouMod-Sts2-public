using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 羊符咒：右键激发后，你下一次给予敌方的负面效果层数翻倍（通过额外施加等量实现），
/// 然后进入2回合冷却。
/// </summary>
public class SheepTalisman : ShengZhuSts2ModRelic, ITalismanRightClickable, ITalismanCooldownResettable, ITalismanInteractionState
{
    /// <summary>
    /// 是否已激发。
    /// </summary>
    private bool _activated;

    /// <summary>
    /// 是否已激发，写入存档和联机状态，避免负面效果翻倍分支不一致。
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
    /// 防止递归触发标记。
    /// </summary>
    private bool _isProcessingDouble;

    /// <summary>
    /// 遗物稀有度：稀有。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Rare;

    /// <summary>
    /// 显示冷却计数器。
    /// </summary>
    public override bool ShowCounter => _cooldownTurnsRemaining > 0;

    /// <summary>
    /// 计数器显示值。
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
    /// 本场战斗是否已触发过首回合升级（鼠符咒共鸣）。
    /// </summary>
    private bool _openingHandUpgraded;

    /// <summary>
    /// 战斗开始时重置状态。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        _activated = false;
        _cooldownTurnsRemaining = 0;
        _isProcessingDouble = false;
        _openingHandUpgraded = false;
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
    /// 鼠符咒共鸣：战斗首回合开始时，升级当前手牌中所有可升级的牌。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (_openingHandUpgraded || !HasRatResonance() || Owner == null || player != Owner)
        {
            return;
        }

        _openingHandUpgraded = true;
        var combatState = player.PlayerCombatState;
        if (combatState == null)
        {
            return;
        }

        Flash();
        foreach (CardModel card in combatState.Hand.Cards.ToList())
        {
            if (ShouldSkipOpeningHandUpgrade(card))
            {
                continue;
            }

            if (card.IsUpgradable)
            {
                CardCmd.Upgrade(card);
            }
        }

        MainFile.Logger.Info("【羊符咒】鼠符咒共鸣：战斗首回合起手牌全部升级完成。");
    }

    /// <summary>
    /// 判断首回合共鸣升级是否需要跳过该卡。
    /// </summary>
    private static bool ShouldSkipOpeningHandUpgrade(CardModel card)
    {
        return card is Cards.NothingLackingCard or Cards.EndlessDarkness;
    }

    /// <summary>
    /// 当Power数值变化时：若已激发且是我方对敌方施加的负面效果，额外再施加等量。
    /// </summary>
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (!_activated || _cooldownTurnsRemaining > 0 || _isProcessingDouble)
        {
            return;
        }

        // 只对负面效果生效
        if (power.Type != PowerType.Debuff || amount <= 0)
        {
            return;
        }

        // 未羊只是放大目标已有负面状态，不算这次新给予敌方负面效果，避免羊符咒遗物重复介入。
        if (cardSource is Cards.Talismans.GoatTalismanCard)
        {
            return;
        }

        // 必须是我方施加给敌方
        if (applier != Owner?.Creature || power.Owner == Owner?.Creature)
        {
            return;
        }

        // 触发翻倍：额外施加等量
        _activated = false;
        _cooldownTurnsRemaining = 2;
        _isProcessingDouble = true;
        UpdateTalismanVisualState(_activated, _cooldownTurnsRemaining);
        Flash();

        await PowerCmd.ModifyAmount(power, amount, applier, cardSource);

        _isProcessingDouble = false;
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
        _isProcessingDouble = false;
        UpdateTalismanVisualState(_activated, _cooldownTurnsRemaining);
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
        if (activate) Flash();
    }
}
