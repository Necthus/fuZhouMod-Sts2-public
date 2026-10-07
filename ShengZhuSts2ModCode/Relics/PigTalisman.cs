using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using System.Runtime.CompilerServices;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 猪符咒：右键激发后，你的下一次攻击在计算伤害前移除敌方护甲和人造制品，然后进入3回合冷却。
/// 对全体攻击时对所有敌人生效。仅当至少移除了护甲或人造制品其一时才消耗激发。
/// </summary>
public class PigTalisman : ShengZhuSts2ModRelic, ITalismanRightClickable, ITalismanCooldownResettable, ITalismanInteractionState
{
    // CardPlay distinguishes replays and nested auto-plays of the same card.
    private ConditionalWeakTable<CardPlay, List<Creature>> _splashTargets = new();

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _splashTargets = new();
    }

    /// <summary>
    /// 是否已激发。
    /// </summary>
    private bool _activated;

    /// <summary>
    /// 是否已激发，写入存档和联机状态，避免移除格挡/人造制品分支不一致。
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
        _splashTargets.Clear();
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
    /// 出牌前：若已激发且打出攻击牌，移除目标格挡和人造制品。
    /// </summary>
    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        CaptureSplashTargets(cardPlay);
        if (!_activated || cardPlay?.Card == null || Owner?.Creature == null || !IsCardPlayedByOwner(cardPlay))
        {
            return;
        }

        if (cardPlay.Card.Type != CardType.Attack)
        {
            return;
        }

        bool consumed = false;

        // 获取所有可攻击的敌人（对全体攻击时处理所有敌人，单体时只处理目标）
        IEnumerable<Creature> targets = GetTargets(cardPlay);

        foreach (Creature target in targets)
        {
            if (await RemoveBlockAndArtifact(target))
            {
                consumed = true;
            }
        }

        // 仅当至少移除了护甲或人造制品其一时，才消耗激发并进入冷却
        if (consumed)
        {
            _activated = false;
            _cooldownTurnsRemaining = 3;
            UpdateTalismanVisualState(_activated, _cooldownTurnsRemaining);
            Flash();
        }
    }

    /// <summary>
    /// 根据卡牌目标类型获取攻击目标列表。
    /// </summary>
    private IEnumerable<Creature> GetTargets(CardPlay cardPlay)
    {
        if (cardPlay.Card is IActualEnemyTargetProvider provider)
        {
            return provider.GetActualEnemyTargets(cardPlay);
        }
        // 如果是全体攻击，返回所有可攻击敌人
        if (cardPlay.Card.TargetType == TargetType.AllEnemies)
        {
            return Owner?.Creature?.CombatState?.HittableEnemies?.ToList()
                   ?? (IReadOnlyList<Creature>)Array.Empty<Creature>();
        }

        // 单体攻击，返回目标
        if (cardPlay.Target != null)
        {
            return [cardPlay.Target];
        }

        return Array.Empty<Creature>();
    }

    /// <summary>
    /// 移除目标的格挡和人造制品（Artifact）。
    /// </summary>
    /// <returns>是否至少成功移除了护甲或人造制品其一。</returns>
    private async Task<bool> RemoveBlockAndArtifact(Creature target)
    {
        bool removed = false;

        // Direct block removal avoids triggering damage hooks or hurting the target.
        if (target.Block > 0)
        {
            await CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), target, target.Block, Owner!.Creature);
            removed = true;
        }

        // 移除人造制品（Artifact）
        PowerModel? artifact = target.Powers
            .FirstOrDefault(p => p is ArtifactPower);

        if (artifact != null)
        {
            await PowerCmd.Remove(artifact);
            removed = true;
        }

        return removed;
    }

    /// <summary>
    /// 鼠符咒共鸣：打出单体攻击牌时，对相邻敌人造成15%伤害的溅射。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_splashTargets.TryGetValue(cardPlay, out List<Creature>? adjacentTargets))
        {
            return;
        }
        _splashTargets.Remove(cardPlay);

        if (!HasRatResonance() || cardPlay?.Card == null || Owner?.Creature == null || !IsCardPlayedByOwner(cardPlay))
        {
            return;
        }

        // 只对单体攻击牌生效
        if (cardPlay.Card.Type != CardType.Attack || cardPlay.Target == null)
        {
            return;
        }

        // 计算溅射伤害（卡牌伤害的15%，最少1点）
        decimal cardDamage = cardPlay.Card.DynamicVars.ContainsKey("Damage")
            ? cardPlay.Card.DynamicVars["Damage"].BaseValue
            : 0;
        int splashDamage = Math.Max(1, (int)(cardDamage * 0.15m));

        if (adjacentTargets.Count > 0)
        {
            Flash();
            foreach (Creature adjacent in adjacentTargets)
            {
                if (!adjacent.IsAlive || adjacent.IsDead || adjacent.CombatState != Owner.Creature.CombatState)
                {
                    continue;
                }
                await CreatureCmd.Damage(choiceContext, adjacent, splashDamage, ValueProp.Unpowered, Owner.Creature, cardPlay.Card, cardPlay);
            }
        }
    }

    private void CaptureSplashTargets(CardPlay cardPlay)
    {
        if (!HasRatResonance() || !IsCardPlayedByOwner(cardPlay)
            || cardPlay.Card.Type != CardType.Attack || cardPlay.Target == null
            || cardPlay.Card.TargetType != TargetType.AnyEnemy
            || Owner?.Creature?.CombatState == null)
        {
            return;
        }

        // Cards that become area attacks through resonance must not also splash.
        if (cardPlay.Card is IActualEnemyTargetProvider provider
            && provider.GetActualEnemyTargets(cardPlay).Count != 1)
        {
            return;
        }

        List<Creature> enemies = Owner.Creature.CombatState.HittableEnemies.ToList();
        int index = enemies.IndexOf(cardPlay.Target);
        if (index < 0)
        {
            return;
        }

        List<Creature> adjacent = [];
        if (index > 0) adjacent.Add(enemies[index - 1]);
        if (index + 1 < enemies.Count) adjacent.Add(enemies[index + 1]);
        _splashTargets.AddOrUpdate(cardPlay, adjacent);
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
