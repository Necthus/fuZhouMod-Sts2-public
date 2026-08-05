using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 兔符咒：
/// 效果1：右键激发后，本轮打出下一张有费卡牌后返还能量，并进入1回合冷却。
/// 效果2：激发状态下右键其他冷却中的符咒，刷新其冷却，兔符咒进入2回合冷却。
/// 再次右键点击可取消激发。
/// </summary>
public class RabbitTalisman : ShengZhuSts2ModRelic, ITalismanRightClickable, ITalismanCooldownResettable, ITalismanInteractionState
{
    /// <summary>
    /// 返还能量后进入的冷却回合数。
    /// </summary>
    private const int EnergyRefundCooldownTurns = 1;

    /// <summary>
    /// 刷新其他符咒冷却后进入的冷却回合数。
    /// </summary>
    private const int RefreshTargetCooldownTurns = 2;

    /// <summary>
    /// 是否已激发（等待返还能量）。
    /// </summary>
    private bool _activated;

    /// <summary>
    /// 是否已激发，写入存档和联机状态，避免返还能量分支不一致。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public bool Activated
    {
        get => _activated;
        set => _activated = value;
    }

    /// <summary>
    /// 是否处于"刷新模式"（等待玩家右键其他符咒来刷新CD）。
    /// 当兔符咒被激发时，同时进入刷新模式。
    /// </summary>
    private bool _refreshMode;

    /// <summary>
    /// 是否处于刷新模式，写入存档和联机状态，避免两端对右键目标判断不一致。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public bool RefreshMode
    {
        get => _refreshMode;
        set => _refreshMode = value;
    }

    /// <summary>
    /// 冷却剩余回合数。为 0 表示可触发。
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
    /// 计数器值：显示剩余冷却回合。
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
    /// 是否处于冷却中（供其他兔符咒刷新用——虽然不太可能有两个兔符咒）。
    /// </summary>
    public bool IsOnCooldown => _cooldownTurnsRemaining > 0;

    /// <summary>
    /// 兔符咒是否处于激发+刷新模式（供RelicRightClickPatch联动判断）。
    /// </summary>
    public bool IsInRefreshMode => _activated && _refreshMode && _cooldownTurnsRemaining <= 0;

    /// <summary>
    /// 战斗开始前重置冷却，确保每场战斗可正常触发。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        _activated = false;
        _refreshMode = false;
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
    /// 执行同步后的兔符咒刷新目标冷却。
    /// </summary>
    /// <param name="target">被右键点击的目标符咒。</param>
    /// <param name="triggerSemiAutoTarget">是否在本次同步流程内触发自动挡目标。</param>
    /// <param name="choiceContext">玩家选择上下文。</param>
    /// <returns>是否成功刷新。</returns>
    internal async Task<bool> RunSyncedRefreshTargetRelic(RelicModel target, bool triggerSemiAutoTarget, PlayerChoiceContext choiceContext)
    {
        if (!IsInRefreshMode)
        {
            return false;
        }

        // 不能刷新自己
        if (target == this)
        {
            return false;
        }

        // 目标必须是可刷新的符咒且处于冷却中
        if (target is not ITalismanCooldownResettable resettable || !resettable.IsOnCooldown)
        {
            return false;
        }

        // 执行刷新
        resettable.ResetCooldown();
        if (triggerSemiAutoTarget)
        {
            await TriggerSemiAutoTargetIfNeeded(target, choiceContext);
        }

        // 兔符咒进入冷却
        _activated = false;
        _refreshMode = false;
        _cooldownTurnsRemaining = RefreshTargetCooldownTurns;
        UpdateTalismanVisualState(_activated, _cooldownTurnsRemaining);
        Flash();

        return true;
    }

    /// <summary>
    /// 自动挡下，刷新右键触发型符咒冷却后立刻尝试触发目标符咒。
    /// 鼠符咒和马符咒自身会检查是否有可处理对象，没有则不会弹窗也不会进入冷却。
    /// 这里必须在兔符咒刷新同步流程内直接执行，避免额外广播导致远端先触发目标、后刷新冷却。
    /// </summary>
    /// <param name="target">被兔符咒刷新的目标符咒。</param>
    /// <param name="choiceContext">玩家选择上下文。</param>
    private async Task TriggerSemiAutoTargetIfNeeded(RelicModel target, PlayerChoiceContext choiceContext)
    {
        if (!TalismanLocator.IsSemiAuto(Owner))
        {
            return;
        }

        switch (target)
        {
            case RatTalisman rat:
                await rat.RunSyncedReplaceCurseOrStatus(choiceContext, true);
                MainFile.Logger.Info("【兔符咒】自动挡刷新鼠符咒冷却后，已在同步流程内尝试自动触发鼠符咒。");
                break;
            case OxTalisman ox:
                ox.SetActivated(true);
                MainFile.Logger.Info("【兔符咒】自动挡刷新牛符咒冷却后，已在同步流程内自动激发牛符咒。");
                break;
            case PigTalisman pig:
                pig.SetActivated(true);
                MainFile.Logger.Info("【兔符咒】自动挡刷新猪符咒冷却后，已在同步流程内自动激发猪符咒。");
                break;
            case SnakeTalisman snake:
                snake.SetActivated(true);
                MainFile.Logger.Info("【兔符咒】自动挡刷新蛇符咒冷却后，已在同步流程内自动激发蛇符咒。");
                break;
            case HorseTalisman horse:
                await horse.RunSyncedRemoveDebuff(choiceContext, true);
                MainFile.Logger.Info("【兔符咒】自动挡刷新马符咒冷却后，已在同步流程内尝试自动触发马符咒。");
                break;
            case SheepTalisman sheep:
                sheep.SetActivated(true);
                MainFile.Logger.Info("【兔符咒】自动挡刷新羊符咒冷却后，已在同步流程内自动激发羊符咒。");
                break;
        }
    }

    /// <summary>
    /// 出牌后检查是否满足触发条件：
    /// 若已激发且该牌实际消耗了能量（非0费牌），则返还本次消耗并进入 1 回合冷却。
    /// 打出0费牌不会消耗激发状态，也不会进入冷却。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_activated || _cooldownTurnsRemaining > 0 || cardPlay?.Card == null || cardPlay.IsAutoPlay || !IsCardPlayedByOwner(cardPlay))
        {
            return;
        }

        int spentEnergy = cardPlay.Resources.EnergySpent;
        if (spentEnergy <= 0)
        {
            // 0费牌不消耗激发状态，继续等待下一张有费牌
            return;
        }

        if (Owner == null)
        {
            return;
        }

        // 效果1触发：返还能量
        _activated = false;
        _refreshMode = false;
        _cooldownTurnsRemaining = EnergyRefundCooldownTurns;
        UpdateTalismanVisualState(_activated, _cooldownTurnsRemaining);
        Flash();
        await PlayerCmd.GainEnergy(spentEnergy, Owner);
    }

    /// <summary>
    /// 鼠符咒共鸣：回合开始时额外获得1点能量。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null || player != Owner)
        {
            return;
        }

        if (HasRatResonance())
        {
            Flash();
            await PlayerCmd.GainEnergy(1, Owner);
        }
    }

    /// <summary>
    /// 玩家回合结束时递减冷却回合。
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
    /// 重置冷却（供其他兔符咒调用，虽然不太可能）。
    /// </summary>
    public void ResetCooldown()
    {
        _cooldownTurnsRemaining = 0;
        _activated = false;
        _refreshMode = false;
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
        _refreshMode = activate;
        UpdateTalismanVisualState(_activated, _cooldownTurnsRemaining);
        if (activate) Flash();
    }
}
