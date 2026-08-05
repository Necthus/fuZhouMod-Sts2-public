using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 蛇符咒：右键激发后，回合结束时获得1层无实体，进入8回合冷却。
/// </summary>
public class SnakeTalisman : ShengZhuSts2ModRelic, ITalismanRightClickable, ITalismanCooldownResettable, ITalismanInteractionState
{
    /// <summary>
    /// 是否已激发。
    /// </summary>
    private bool _activated;

    /// <summary>
    /// 是否已激发，写入存档和联机状态，避免各端按不同状态结算无实体。
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
    /// 冷却剩余回合数，写入存档和联机状态，避免一端还在冷却而另一端已经可触发。
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
    /// 回合结束时：若已激发，获得1层无实体并进入冷却；否则递减冷却。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side != CombatSide.Player)
        {
            return;
        }

        // 递减冷却
        if (_cooldownTurnsRemaining > 0)
        {
            _cooldownTurnsRemaining--;
            UpdateTalismanVisualState(_activated, _cooldownTurnsRemaining);
            return;
        }

        // 激发效果：获得无实体
        if (_activated && Owner?.Creature != null)
        {
            _activated = false;
            // 鼠符咒共鸣：冷却从8缩短为6
            _cooldownTurnsRemaining = HasRatResonance() ? 6 : 8;
            UpdateTalismanVisualState(_activated, _cooldownTurnsRemaining);
            Flash();
            await PowerCmd.Apply<IntangiblePower>(Owner.Creature, 1, Owner.Creature, null);
        }
    }

    /// <summary>
    /// 重置冷却（供兔符咒调用）。
    /// </summary>
    public void ResetCooldown()
    {
        _cooldownTurnsRemaining = 0;
        _activated = false;
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
