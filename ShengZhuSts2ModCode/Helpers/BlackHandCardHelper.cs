using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 黑手帮卡牌公共助手：统一管理黑手帮/阿福牌识别、牌堆统计和赏金收尾逻辑。
/// </summary>
public static class BlackHandCardHelper
{
    /// <summary>
    /// 当前统计所属战斗，切换战斗时会重置全战斗记录。
    /// </summary>
    private static ICombatState? _currentCombatState;

    /// <summary>
    /// 当前统计所属回合号，切换回合时会重置本回合记录。
    /// </summary>
    private static int _playedTurnNumber = -1;

    /// <summary>
    /// 本回合每名玩家已经打出的黑手帮牌数量。
    /// </summary>
    private static readonly Dictionary<ulong, int> BlackHandPlayedThisTurnByPlayer = [];

    /// <summary>
    /// 本回合每名玩家已经打出的阿福牌数量。
    /// </summary>
    private static readonly Dictionary<ulong, int> AfuPlayedThisTurnByPlayer = [];

    /// <summary>
    /// 本回合已经记录过的卡牌实例，用于避免重复记录同一张牌。
    /// </summary>
    private static readonly HashSet<CardModel> RecordedCardsThisTurn = [];

    /// <summary>
    /// 本场战斗每名玩家已经打出过的不同阿福牌ID。
    /// </summary>
    private static readonly Dictionary<ulong, HashSet<string>> UniqueAfuCardIdsThisCombatByPlayer = [];

    /// <summary>
    /// 本场战斗中由【捞人】授予黑手帮身份的卡牌。
    /// </summary>
    private static readonly HashSet<CardModel> PullStringsBlackHandCardsThisCombat = [];

    /// <summary>
    /// 判断给定卡牌是否属于黑手帮体系牌。
    /// </summary>
    /// <param name="card">待判断的卡牌。</param>
    /// <returns>属于黑手帮体系时返回 true。</returns>
    public static bool IsBlackHandCard(CardModel? card)
    {
        return card is BlackHandGangCard || (card != null && PullStringsBlackHandCardsThisCombat.Contains(card));
    }

    /// <summary>
    /// 将【捞人】加入手牌的卡牌标记为本场战斗内的黑手帮牌。
    /// </summary>
    /// <param name="card">被捞回手牌的卡牌。</param>
    public static void GrantBlackHandCardTagForCombat(CardModel? card)
    {
        if (card == null)
        {
            return;
        }

        EnsureCombatState(card);
        if (PullStringsBlackHandCardsThisCombat.Add(card))
        {
            ShengZhuLogHelper.VerboseCombatInfo(() => $"【捞人】授予黑手帮词条：卡牌={card.Id.Entry}，玩家={card.Owner?.NetId}。");
        }
    }

    /// <summary>
    /// 判断给定卡牌是否属于阿福体系牌（通过继承 AhFuCard 判断）。
    /// </summary>
    /// <param name="card">待判断的卡牌。</param>
    /// <returns>属于阿福体系时返回 true。</returns>
    public static bool IsAhFuCard(CardModel? card)
    {
        return card is AhFuCard;
    }

    /// <summary>
    /// 判断给定卡牌是否属于“阿福或黑手帮”联合筛选范围。
    /// </summary>
    /// <param name="card">待判断的卡牌。</param>
    /// <returns>满足任一体系时返回 true。</returns>
    public static bool IsAhFuOrBlackHandCard(CardModel? card)
    {
        return IsBlackHandCard(card) || IsAhFuCard(card);
    }

    /// <summary>
    /// 判断给定卡牌是否属于金鸡王宝藏的减费范围。
    /// </summary>
    /// <param name="card">待判断的卡牌。</param>
    /// <returns>满足阿福、黑手帮或黑暗杀手体系时返回 true。</returns>
    public static bool IsValmontTreasureCostReducedCard(CardModel? card)
    {
        return IsAhFuOrBlackHandCard(card) || IsDarkWarriorCard(card);
    }

    /// <summary>
    /// 判断给定卡牌是否属于刀龙黑暗杀手体系牌。
    /// </summary>
    /// <param name="card">待判断的卡牌。</param>
    /// <returns>属于黑暗杀手体系时返回 true。</returns>
    public static bool IsDarkWarriorCard(CardModel? card)
    {
        return card is Gan or Wen or Cui or Shan;
    }

    /// <summary>
    /// 统计主牌组中黑手帮卡的数量，供瓦龙等动态牌使用。
    /// </summary>
    /// <param name="player">要统计的玩家。</param>
    /// <returns>主牌组中的黑手帮卡数量。</returns>
    public static int CountBlackHandInMasterDeck(Player? player)
    {
        return player?.Deck.Cards.Count(IsBlackHandCard) ?? 0;
    }

    /// <summary>
    /// 按战斗对象重置黑手帮/阿福出牌统计；同一场战斗只会真正清理一次。
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
        ResetCombatCounters();
    }

    /// <summary>
    /// 强制重置黑手帮/阿福出牌统计。
    /// </summary>
    public static void ResetCombatState()
    {
        _currentCombatState = null;
        ResetCombatCounters();
    }

    /// <summary>
    /// 记录一张牌已经完成打出，用于替代不稳定的出牌堆时机判断。
    /// </summary>
    /// <param name="card">已经打出的牌。</param>
    public static void RecordPlayedCard(CardModel? card)
    {
        if (card == null)
        {
            return;
        }

        EnsureCombatState(card);
        EnsureTurnState(card);

        ulong? ownerId = GetOwnerNetId(card);
        if (ownerId == null)
        {
            return;
        }

        if (!RecordedCardsThisTurn.Add(card))
        {
            return;
        }

        if (IsBlackHandCard(card))
        {
            BlackHandPlayedThisTurnByPlayer[ownerId.Value] = CountBlackHandPlayedThisTurn(ownerId.Value) + 1;
        }

        if (IsAhFuCard(card))
        {
            AfuPlayedThisTurnByPlayer[ownerId.Value] = CountAfuPlayedThisTurn(ownerId.Value) + 1;
            GetUniqueAfuCardIds(ownerId.Value).Add(card.Id.Entry);
        }

        ShengZhuLogHelper.VerboseCombatInfo(() => $"【黑手帮/阿福计数】记录打出：卡牌={card.Id.Entry}，玩家={ownerId.Value}，回合={_playedTurnNumber}，黑手帮本回合={CountBlackHandPlayedThisTurn(ownerId.Value)}，阿福本回合={CountAfuPlayedThisTurn(ownerId.Value)}，阿福本战斗不同={CountUniqueAfuPlayedThisCombat(ownerId.Value)}");
    }

    /// <summary>
    /// 统计本回合已打出的黑手帮卡数量，可排除当前正在结算的牌。
    /// </summary>
    /// <param name="currentCard">当前牌；若已在出牌堆里，则会从统计中排除。</param>
    /// <returns>已打出的黑手帮卡数量。</returns>
    public static int CountBlackHandPlayedThisTurn(CardModel? currentCard = null)
    {
        if (currentCard != null)
        {
            EnsureCombatState(currentCard);
            EnsureTurnState(currentCard);
        }

        ulong? ownerId = GetOwnerNetId(currentCard);
        if (ownerId == null)
        {
            return 0;
        }

        int count = CountBlackHandPlayedThisTurn(ownerId.Value);
        if (currentCard != null && RecordedCardsThisTurn.Contains(currentCard) && IsBlackHandCard(currentCard))
        {
            count--;
        }

        return Math.Max(0, count);
    }

    /// <summary>
    /// 统计本回合已打出的阿福牌数量，可排除当前正在结算的牌。
    /// </summary>
    /// <param name="currentCard">当前牌；若已经记录，则会从统计中排除。</param>
    /// <returns>本回合已打出的阿福牌数量。</returns>
    public static int CountAfuPlayedThisTurn(CardModel? currentCard = null)
    {
        if (currentCard != null)
        {
            EnsureCombatState(currentCard);
            EnsureTurnState(currentCard);
        }

        ulong? ownerId = GetOwnerNetId(currentCard);
        if (ownerId == null)
        {
            return 0;
        }

        int count = CountAfuPlayedThisTurn(ownerId.Value);
        if (currentCard != null && RecordedCardsThisTurn.Contains(currentCard) && IsAhFuCard(currentCard))
        {
            count--;
        }

        return Math.Max(0, count);
    }

    /// <summary>
    /// 统计本场战斗已打出过的不同阿福牌数量，可排除当前正在结算的牌。
    /// </summary>
    /// <param name="currentCard">当前牌；若已经记录，则会从统计中排除。</param>
    /// <returns>不同阿福牌数量。</returns>
    public static int CountUniqueAfuPlayedThisCombat(CardModel? currentCard = null)
    {
        if (currentCard != null)
        {
            EnsureCombatState(currentCard);
            EnsureTurnState(currentCard);
        }

        ulong? ownerId = GetOwnerNetId(currentCard);
        if (ownerId == null)
        {
            return 0;
        }

        HashSet<string> uniqueAfuCardIds = GetUniqueAfuCardIds(ownerId.Value);
        int count = uniqueAfuCardIds.Count;
        if (currentCard != null && RecordedCardsThisTurn.Contains(currentCard) && IsAhFuCard(currentCard) && uniqueAfuCardIds.Contains(currentCard.Id.Entry))
        {
            count--;
        }

        return Math.Max(0, count);
    }

    /// <summary>
    /// 判断当前牌结算前，同一玩家本回合是否已经打出过阿福牌。
    /// 优先读取正式记录；记录尚未落地时，再用出牌堆兜底支持自动重放牌。
    /// </summary>
    /// <param name="currentCard">当前正在结算或即将结算的阿福牌。</param>
    /// <returns>同一玩家本回合之前打出过阿福牌时返回 true。</returns>
    public static bool HasAfuPlayedThisTurnBefore(CardModel? currentCard)
    {
        if (currentCard == null)
        {
            return false;
        }

        EnsureCombatState(currentCard);
        EnsureTurnState(currentCard);

        if (CountAfuPlayedThisTurn(currentCard) > 0)
        {
            return true;
        }

        IReadOnlyList<CardModel>? playPile = currentCard.Owner?.PlayerCombatState?.PlayPile.Cards;
        if (playPile == null)
        {
            return false;
        }

        foreach (CardModel playedCard in playPile)
        {
            if (ReferenceEquals(playedCard, currentCard))
            {
                break;
            }

            if (IsAhFuCard(playedCard) && IsSameOwner(playedCard, currentCard))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断两张牌是否属于同一名玩家，联机时使用 NetId 避免对象引用差异。
    /// </summary>
    /// <param name="left">第一张牌。</param>
    /// <param name="right">第二张牌。</param>
    /// <returns>两张牌持有者相同时返回 true。</returns>
    public static bool IsSameOwner(CardModel? left, CardModel? right)
    {
        ulong? leftOwnerId = GetOwnerNetId(left);
        ulong? rightOwnerId = GetOwnerNetId(right);
        return leftOwnerId != null && rightOwnerId != null && leftOwnerId.Value == rightOwnerId.Value;
    }

    /// <summary>
    /// 获取当前回合上一张打出的牌，供“暗号”之类的前后手联动使用。
    /// </summary>
    /// <param name="currentCard">当前正在结算的牌；若已进出牌堆，则会先排除自己。</param>
    /// <returns>上一张打出的牌；若不存在则返回 null。</returns>
    public static CardModel? GetPreviousCardPlayedThisTurn(CardModel? currentCard = null)
    {
        IReadOnlyList<CardModel>? playPile = currentCard?.Owner.PlayerCombatState?.PlayPile.Cards;
        if (playPile == null || playPile.Count == 0)
        {
            return null;
        }

        // 找到当前牌在出牌堆中的位置
        int currentIndex = -1;
        for (int i = 0; i < playPile.Count; i++)
        {
            if (ReferenceEquals(playPile[i], currentCard))
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex > 0)
        {
            // 当前牌在出牌堆中，取它前面那张
            return playPile[currentIndex - 1];
        }

        if (currentIndex == 0)
        {
            // 当前牌是出牌堆第一张，没有上一张
            return null;
        }

        // 当前牌不在出牌堆中（消耗牌等），取出牌堆最后一张
        return playPile[^1];
    }

    /// <summary>
    /// 获取来源持有者当前可交互的所有敌人。
    /// </summary>
    /// <param name="sourceCard">来源卡牌。</param>
    /// <returns>仍在战斗中的敌人列表。</returns>
    public static IReadOnlyList<Creature> GetLivingOpponents(CardModel? sourceCard)
    {
        if (sourceCard?.CombatState == null || sourceCard.Owner?.Creature == null)
        {
            return Array.Empty<Creature>();
        }

        return sourceCard.CombatState
            .GetOpponentsOf(sourceCard.Owner.Creature)
            .Where(IsLivingCreature)
            .ToList();
    }

    /// <summary>
    /// 判断目标是否仍可视为战斗中存活单位。
    /// </summary>
    /// <param name="creature">待判断单位。</param>
    /// <returns>单位仍可参与战斗时返回 true。</returns>
    public static bool IsLivingCreature(Creature? creature)
    {
        return creature != null && creature.IsAlive && !creature.IsDead;
    }

    /// <summary>
    /// 在黑手帮牌或相关效果结算后，顺手清一次悬赏奖励，避免漏发金币。
    /// </summary>
    /// <param name="choiceContext">当前出牌上下文。</param>
    /// <param name="sourceCard">来源卡牌。</param>
    /// <returns>异步任务。</returns>
    public static async Task ResolveBountyRewards(PlayerChoiceContext choiceContext, CardModel? sourceCard)
    {
        if (sourceCard?.Owner == null)
        {
            return;
        }

        await BountyPower.ResolvePendingRewards(choiceContext, sourceCard.Owner);
    }

    /// <summary>
    /// 清理整场战斗统计。
    /// </summary>
    private static void ResetCombatCounters()
    {
        _playedTurnNumber = -1;
        BlackHandPlayedThisTurnByPlayer.Clear();
        AfuPlayedThisTurnByPlayer.Clear();
        RecordedCardsThisTurn.Clear();
        UniqueAfuCardIdsThisCombatByPlayer.Clear();
        PullStringsBlackHandCardsThisCombat.Clear();
        ShengZhuLogHelper.VerboseCombatInfo(() => "【黑手帮/阿福计数】战斗统计已重置。");
    }

    /// <summary>
    /// 确保统计所属战斗和当前卡牌所在战斗一致。
    /// </summary>
    private static void EnsureCombatState(CardModel card)
    {
        ICombatState? combatState = card.Owner?.Creature?.CombatState;
        if (combatState == null || ReferenceEquals(_currentCombatState, combatState))
        {
            return;
        }

        _currentCombatState = combatState;
        ResetCombatCounters();
    }

    /// <summary>
    /// 确保本回合统计和当前回合一致。
    /// </summary>
    private static void EnsureTurnState(CardModel card)
    {
        int currentTurnNumber = GetCurrentTurnNumber(card);
        if (currentTurnNumber == _playedTurnNumber)
        {
            return;
        }

        _playedTurnNumber = currentTurnNumber;
        BlackHandPlayedThisTurnByPlayer.Clear();
        AfuPlayedThisTurnByPlayer.Clear();
        RecordedCardsThisTurn.Clear();
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【黑手帮/阿福计数】检测到新回合，重置本回合统计：回合={currentTurnNumber}");
    }

    /// <summary>
    /// 读取指定玩家本回合已经打出的黑手帮牌数量。
    /// </summary>
    /// <param name="ownerId">玩家联机ID。</param>
    /// <returns>该玩家本回合黑手帮牌数量。</returns>
    private static int CountBlackHandPlayedThisTurn(ulong ownerId)
    {
        return BlackHandPlayedThisTurnByPlayer.TryGetValue(ownerId, out int count) ? count : 0;
    }

    /// <summary>
    /// 读取指定玩家本回合已经打出的阿福牌数量。
    /// </summary>
    /// <param name="ownerId">玩家联机ID。</param>
    /// <returns>该玩家本回合阿福牌数量。</returns>
    private static int CountAfuPlayedThisTurn(ulong ownerId)
    {
        return AfuPlayedThisTurnByPlayer.TryGetValue(ownerId, out int count) ? count : 0;
    }

    /// <summary>
    /// 读取指定玩家本场战斗已经打出的不同阿福牌数量。
    /// </summary>
    /// <param name="ownerId">玩家联机ID。</param>
    /// <returns>该玩家本场战斗不同阿福牌数量。</returns>
    private static int CountUniqueAfuPlayedThisCombat(ulong ownerId)
    {
        return UniqueAfuCardIdsThisCombatByPlayer.TryGetValue(ownerId, out HashSet<string>? ids) ? ids.Count : 0;
    }

    /// <summary>
    /// 获取指定玩家本场战斗已经打出的不同阿福牌ID集合。
    /// </summary>
    /// <param name="ownerId">玩家联机ID。</param>
    /// <returns>该玩家的不同阿福牌ID集合。</returns>
    private static HashSet<string> GetUniqueAfuCardIds(ulong ownerId)
    {
        if (!UniqueAfuCardIdsThisCombatByPlayer.TryGetValue(ownerId, out HashSet<string>? ids))
        {
            ids = [];
            UniqueAfuCardIdsThisCombatByPlayer[ownerId] = ids;
        }

        return ids;
    }

    /// <summary>
    /// 读取卡牌持有者的联机ID。
    /// </summary>
    /// <param name="card">要读取持有者的卡牌。</param>
    /// <returns>持有者联机ID；没有持有者时返回 null。</returns>
    private static ulong? GetOwnerNetId(CardModel? card)
    {
        return card?.Owner?.NetId;
    }

    /// <summary>
    /// 读取当前战斗回合数。
    /// </summary>
    private static int GetCurrentTurnNumber(CardModel card)
    {
        var combatState = card.Owner?.Creature?.CombatState;
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
            var type = combatState.GetType();
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
