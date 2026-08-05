using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 西瓦手镯：每回合开始后，持有者本回合前两张攻击牌各重放一次。
/// </summary>
public class ShivaBracelet : ShengZhuSts2ModRelic
{
    /// <summary>
    /// 每回合可重放的攻击牌数量。
    /// </summary>
    private const int ReplaysPerTurn = 2;

    /// <summary>
    /// 本回合剩余重放次数。
    /// </summary>
    private int _remainingReplaysThisTurn;

    /// <summary>
    /// 遗物稀有度：事件遗物。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Event;

    /// <summary>
    /// 显示本回合剩余重放次数。
    /// </summary>
    public override bool ShowCounter => true;

    /// <summary>
    /// 计数器值：本回合剩余重放次数。
    /// </summary>
    public override int DisplayAmount => _remainingReplaysThisTurn;

    /// <summary>
    /// 战斗开始时重置首回合次数，避免第一回合开始钩子缺失时不能触发。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        ResetTurnReplays("战斗开始");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 持有者回合开始时重置本回合可重放次数。
    /// </summary>
    /// <param name="choiceContext">玩家选择上下文。</param>
    /// <param name="player">当前开始回合的玩家。</param>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null || player != Owner)
        {
            return Task.CompletedTask;
        }

        ResetTurnReplays("玩家回合开始");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 持有者打出攻击牌后，如果本回合还有次数，则复制并自动重放一次。
    /// </summary>
    /// <param name="choiceContext">玩家选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay == null)
        {
            // MainFile.Logger.Info($"【西瓦手镯】收到空出牌事件：玩家={Owner?.NetId}，剩余次数={_remainingReplaysThisTurn}。");
            return;
        }

        // MainFile.Logger.Info($"【西瓦手镯】收到出牌事件：玩家={Owner?.NetId}，卡牌={FormatCard(cardPlay?.Card)}，类型={cardPlay?.Card?.Type.ToString() ?? "空"}，自动播放={cardPlay?.IsAutoPlay.ToString() ?? "空"}，出牌序号={FormatPlayIndex(cardPlay)}，目标={FormatTarget(cardPlay?.Target)}，剩余次数={_remainingReplaysThisTurn}。");

        if (!CanReplay(cardPlay))
        {
            return;
        }

        int remainingBeforeReplay = _remainingReplaysThisTurn;
        CardModel? sourceCard = cardPlay!.Card;
        if (sourceCard == null)
        {
            // MainFile.Logger.Info($"【西瓦手镯】跳过重放：通过检查后卡牌仍为空，玩家={Owner?.NetId}。");
            return;
        }

        _remainingReplaysThisTurn--;
        InvokeDisplayAmountChanged();
        Flash();
        // MainFile.Logger.Info($"【西瓦手镯】准备重放：玩家={Owner?.NetId}，原卡={FormatCard(sourceCard)}，出牌序号={FormatPlayIndex(cardPlay)}，目标={FormatTarget(cardPlay.Target)}，触发前剩余={remainingBeforeReplay}，扣除后剩余={_remainingReplaysThisTurn}。");

        CardModel replayCard = sourceCard.CreateDupe();
        replayCard.ExhaustOnNextPlay = true;
        // MainFile.Logger.Info($"【西瓦手镯】创建重放复制牌：原卡={FormatCard(sourceCard)}，复制牌={FormatCard(replayCard)}，复制牌下次打出后消耗={replayCard.ExhaustOnNextPlay}。");

        await CardPileCmdHelper.AddGeneratedCardToCombat(replayCard, PileType.Hand, true);
        // MainFile.Logger.Info($"【西瓦手镯】复制牌已加入手牌，准备自动打出：复制牌={FormatCard(replayCard)}，目标={FormatTarget(cardPlay.Target)}。");

        await CardCmd.AutoPlay(choiceContext, replayCard, cardPlay.Target);
        // MainFile.Logger.Info($"【西瓦手镯】自动打出完成：玩家={Owner?.NetId}，原卡={FormatCard(sourceCard)}，复制牌={FormatCard(replayCard)}，出牌序号={FormatPlayIndex(cardPlay)}，目标={FormatTarget(cardPlay.Target)}，剩余次数={_remainingReplaysThisTurn}。");
    }

    /// <summary>
    /// 玩家回合结束时清空剩余次数，避免非持有者回合误显示可用。
    /// </summary>
    /// <param name="choiceContext">玩家选择上下文。</param>
    /// <param name="side">结束回合的一方。</param>
    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side == CombatSide.Player && _remainingReplaysThisTurn != 0)
        {
            _remainingReplaysThisTurn = 0;
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 判断当前出牌是否可以被西瓦手镯重放。
    /// </summary>
    /// <param name="cardPlay">本次出牌信息。</param>
    /// <returns>可以重放时返回 true。</returns>
    private bool CanReplay(CardPlay? cardPlay)
    {
        if (_remainingReplaysThisTurn <= 0)
        {
            // MainFile.Logger.Info($"【西瓦手镯】跳过重放：本回合次数已用尽，玩家={Owner?.NetId}，卡牌={FormatCard(cardPlay?.Card)}。");
            return false;
        }

        if (cardPlay?.Card == null)
        {
            // MainFile.Logger.Info($"【西瓦手镯】跳过重放：出牌事件没有卡牌，玩家={Owner?.NetId}。");
            return false;
        }

        if (cardPlay.PlayIndex > 0)
        {
            // MainFile.Logger.Info($"【西瓦手镯】跳过重放：同一次出牌的后续结算不重复消耗手镯，玩家={Owner?.NetId}，卡牌={FormatCard(cardPlay.Card)}，出牌序号={FormatPlayIndex(cardPlay)}。");
            return false;
        }

        if (Owner?.Creature == null)
        {
            // MainFile.Logger.Info($"【西瓦手镯】跳过重放：遗物持有者或战斗对象为空，玩家={Owner?.NetId}，卡牌={FormatCard(cardPlay.Card)}，出牌序号={FormatPlayIndex(cardPlay)}。");
            return false;
        }

        if (!IsCardPlayedByOwner(cardPlay))
        {
            // MainFile.Logger.Info($"【西瓦手镯】跳过重放：不是持有者打出的牌，玩家={Owner.NetId}，卡牌={FormatCard(cardPlay.Card)}，出牌序号={FormatPlayIndex(cardPlay)}，目标={FormatTarget(cardPlay.Target)}。");
            return false;
        }

        if (cardPlay.IsAutoPlay)
        {
            // MainFile.Logger.Info($"【西瓦手镯】跳过重放：本次是自动播放，避免重放递归，玩家={Owner.NetId}，卡牌={FormatCard(cardPlay.Card)}，出牌序号={FormatPlayIndex(cardPlay)}，目标={FormatTarget(cardPlay.Target)}。");
            return false;
        }

        if (cardPlay.Card.Type != CardType.Attack)
        {
            // MainFile.Logger.Info($"【西瓦手镯】跳过重放：不是攻击牌，玩家={Owner.NetId}，卡牌={FormatCard(cardPlay.Card)}，类型={cardPlay.Card.Type}，出牌序号={FormatPlayIndex(cardPlay)}。");
            return false;
        }

        if (cardPlay.Target != null && (cardPlay.Target.IsDead || !cardPlay.Target.IsAlive))
        {
            // MainFile.Logger.Info($"【西瓦手镯】跳过重放：目标已不可用，玩家={Owner.NetId}，卡牌={FormatCard(cardPlay.Card)}，出牌序号={FormatPlayIndex(cardPlay)}，目标={FormatTarget(cardPlay.Target)}。");
            return false;
        }

        // MainFile.Logger.Info($"【西瓦手镯】允许重放：玩家={Owner.NetId}，卡牌={FormatCard(cardPlay.Card)}，出牌序号={FormatPlayIndex(cardPlay)}，目标={FormatTarget(cardPlay.Target)}，剩余次数={_remainingReplaysThisTurn}。");
        return true;
    }

    /// <summary>
    /// 重置本回合攻击牌重放次数。
    /// </summary>
    /// <param name="source">触发来源。</param>
    private void ResetTurnReplays(string source)
    {
        _remainingReplaysThisTurn = ReplaysPerTurn;
        InvokeDisplayAmountChanged();
        // MainFile.Logger.Info($"【西瓦手镯】重置回合次数：来源={source}，玩家={Owner?.NetId}，次数={ReplaysPerTurn}。");
    }

    /// <summary>
    /// 格式化卡牌信息，方便观察自带重放和西瓦手镯复制牌的差异。
    /// </summary>
    /// <param name="card">要输出的卡牌。</param>
    /// <returns>可读的卡牌信息。</returns>
    private static string FormatCard(CardModel? card)
    {
        if (card == null)
        {
            return "空";
        }

        return $"{card.Id.Entry}(类型={card.Type}，升级={card.IsUpgraded}，自动消耗={card.ExhaustOnNextPlay})";
    }

    /// <summary>
    /// 格式化目标信息，方便确认重放时目标是否死亡或变化。
    /// </summary>
    /// <param name="target">要输出的目标。</param>
    /// <returns>可读的目标信息。</returns>
    private static string FormatTarget(Creature? target)
    {
        if (target == null)
        {
            return "空";
        }

        return $"{target.Name}(HP={target.CurrentHp}，格挡={target.Block}，存活={target.IsAlive}，死亡={target.IsDead})";
    }

    /// <summary>
    /// 格式化同一次出牌的结算序号，方便排查自带重放牌和西瓦手镯的交互。
    /// </summary>
    /// <param name="cardPlay">本次出牌信息。</param>
    /// <returns>可读的出牌序号。</returns>
    private static string FormatPlayIndex(CardPlay? cardPlay)
    {
        if (cardPlay == null)
        {
            return "空";
        }

        return $"{cardPlay.PlayIndex + 1}/{cardPlay.PlayCount}";
    }
}
