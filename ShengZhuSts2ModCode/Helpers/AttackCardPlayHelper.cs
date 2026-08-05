using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 攻击牌出牌统计工具：记录当前战斗当前回合已经打出的攻击牌，供夺舍等卡牌限制使用。
/// </summary>
public static class AttackCardPlayHelper
{
    /// <summary>
    /// 当前统计所属战斗。
    /// </summary>
    private static ICombatState? _currentCombatState;

    /// <summary>
    /// 当前统计所属回合号。
    /// </summary>
    private static int _playedTurnNumber = -1;

    /// <summary>
    /// 本回合每名玩家已经打出的攻击牌数量；夺舍只检查自己，不影响阿福团队共享统计。
    /// </summary>
    private static readonly Dictionary<ulong, int> AttackPlayedThisTurnByPlayer = [];

    /// <summary>
    /// 本回合每名玩家已经记录过的卡牌实例，避免同一张牌被重复统计。
    /// </summary>
    private static readonly Dictionary<ulong, HashSet<CardModel>> RecordedCardsThisTurnByPlayer = [];

    /// <summary>
    /// 按战斗对象重置攻击牌统计。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    public static void ResetCombatStateForCombat(ICombatState? combatState)
    {
        if (combatState == null)
        {
            ResetCombatState();
            return;
        }

        if (ReferenceEquals(_currentCombatState, combatState))
        {
            return;
        }

        _currentCombatState = combatState;
        ResetTurnCounters();
    }

    /// <summary>
    /// 强制重置攻击牌统计。
    /// </summary>
    public static void ResetCombatState()
    {
        _currentCombatState = null;
        ResetTurnCounters();
    }

    /// <summary>
    /// 记录一张牌已经完成打出。
    /// </summary>
    /// <param name="card">刚打出的牌。</param>
    public static void RecordPlayedCard(CardModel? card)
    {
        if (card == null)
        {
            return;
        }

        EnsureCombatState(card);
        EnsureTurnState(card);

        ulong? ownerId = GetOwnerId(card);
        if (ownerId == null)
        {
            ShengZhuLogHelper.VerboseCombatInfo(() => $"【攻击牌统计】跳过无归属卡牌：卡牌={card.Id.Entry}。");
            return;
        }

        if (!GetRecordedCards(ownerId.Value).Add(card))
        {
            return;
        }

        if (card.Type == CardType.Attack)
        {
            int count = CountAttackPlayedThisTurn(ownerId.Value) + 1;
            AttackPlayedThisTurnByPlayer[ownerId.Value] = count;
            ShengZhuLogHelper.VerboseCombatInfo(() => $"【攻击牌统计】记录攻击牌：玩家={ownerId.Value}，卡牌={card.Id.Entry}，回合={_playedTurnNumber}，本玩家本回合攻击牌={count}。");
        }
    }

    /// <summary>
    /// 判断本回合当前牌之前是否已经打出攻击牌。
    /// </summary>
    /// <param name="currentCard">当前正在判断的牌。</param>
    /// <returns>本回合已打出攻击牌时返回 true。</returns>
    public static bool HasAttackPlayedThisTurn(CardModel? currentCard)
    {
        if (currentCard != null)
        {
            EnsureCombatState(currentCard);
            EnsureTurnState(currentCard);
        }

        ulong? ownerId = GetOwnerId(currentCard);
        return ownerId != null && CountAttackPlayedThisTurn(ownerId.Value) > 0;
    }

    /// <summary>
    /// 清理本回合计数。
    /// </summary>
    private static void ResetTurnCounters()
    {
        _playedTurnNumber = -1;
        AttackPlayedThisTurnByPlayer.Clear();
        RecordedCardsThisTurnByPlayer.Clear();
        ShengZhuLogHelper.VerboseCombatInfo(() => "【攻击牌统计】战斗统计已重置。");
    }

    /// <summary>
    /// 确保统计所属战斗和当前卡牌所在战斗一致。
    /// </summary>
    /// <param name="card">当前卡牌。</param>
    private static void EnsureCombatState(CardModel card)
    {
        ICombatState? combatState = card.Owner?.Creature?.CombatState;
        if (combatState == null || ReferenceEquals(_currentCombatState, combatState))
        {
            return;
        }

        _currentCombatState = combatState;
        ResetTurnCounters();
    }

    /// <summary>
    /// 确保统计所属回合和当前卡牌所在回合一致。
    /// </summary>
    /// <param name="card">当前卡牌。</param>
    private static void EnsureTurnState(CardModel card)
    {
        int currentTurnNumber = GetCurrentTurnNumber(card);
        if (currentTurnNumber == _playedTurnNumber)
        {
            return;
        }

        _playedTurnNumber = currentTurnNumber;
        AttackPlayedThisTurnByPlayer.Clear();
        RecordedCardsThisTurnByPlayer.Clear();
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【攻击牌统计】检测到新回合，重置本回合攻击牌统计：回合={currentTurnNumber}。");
    }

    /// <summary>
    /// 获取卡牌归属玩家的联机ID，用于把攻击牌统计拆到每名玩家自己名下。
    /// </summary>
    /// <param name="card">要读取归属的卡牌。</param>
    /// <returns>玩家联机ID；没有归属时返回 null。</returns>
    private static ulong? GetOwnerId(CardModel? card)
    {
        return card?.Owner?.NetId;
    }

    /// <summary>
    /// 获取指定玩家本回合已经记录过的卡牌集合。
    /// </summary>
    /// <param name="ownerId">玩家联机ID。</param>
    /// <returns>该玩家本回合记录集合。</returns>
    private static HashSet<CardModel> GetRecordedCards(ulong ownerId)
    {
        if (!RecordedCardsThisTurnByPlayer.TryGetValue(ownerId, out HashSet<CardModel>? cards))
        {
            cards = [];
            RecordedCardsThisTurnByPlayer[ownerId] = cards;
        }

        return cards;
    }

    /// <summary>
    /// 统计指定玩家本回合已经打出的攻击牌数量。
    /// </summary>
    /// <param name="ownerId">玩家联机ID。</param>
    /// <returns>该玩家本回合攻击牌数量。</returns>
    private static int CountAttackPlayedThisTurn(ulong ownerId)
    {
        return AttackPlayedThisTurnByPlayer.TryGetValue(ownerId, out int count) ? count : 0;
    }

    /// <summary>
    /// 读取当前战斗回合数。
    /// </summary>
    /// <param name="card">当前卡牌。</param>
    /// <returns>当前回合号。</returns>
    private static int GetCurrentTurnNumber(CardModel card)
    {
        ICombatState? combatState = card.Owner?.Creature?.CombatState;
        if (combatState == null)
        {
            return 0;
        }

        if (combatState.RoundNumber > 0)
        {
            return combatState.RoundNumber;
        }

        try
        {
            Type type = combatState.GetType();
            var property = type.GetProperty("TurnNumber")
                           ?? type.GetProperty("TurnCount")
                           ?? type.GetProperty("Turn");
            if (property?.GetValue(combatState) is int intValue)
            {
                return intValue;
            }
        }
        catch
        {
            // 忽略反射失败，兜底返回0。
        }

        return 0;
    }
}
