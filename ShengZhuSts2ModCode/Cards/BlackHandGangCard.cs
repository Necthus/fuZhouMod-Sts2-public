using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 黑手帮卡牌基类：统一封装黑手施加、消耗、敌方列表和赏金收尾等公共能力。
/// </summary>
public abstract class BlackHandGangCard(int cost, CardType type, CardRarity rarity, TargetType target) :
    ShengZhuSts2ModCard(cost, type, rarity, target)
{
    /// <summary>
    /// 记录本回合上一张打出的牌（在当前牌的 OnPlay 之前更新）。
    /// 利用 BeforeCardPlayed 钩子，每当有牌被打出时记录下来。
    /// </summary>
    private CardModel? _lastPlayedBeforeThis;

    /// <summary>
    /// 是否需要在上一张牌是黑手帮牌时显示金色高亮。
    /// </summary>
    protected virtual bool ShouldGlowWhenPreviousBlackHandCard => false;

    /// <summary>
    /// 条件满足时让手牌发出金色高亮，提示黑手帮前后手联动效果可以触发。
    /// </summary>
    protected override bool ShouldGlowGoldInternal => ShouldGlowWhenPreviousBlackHandCard && BlackHandCardHelper.IsBlackHandCard(_lastPlayedBeforeThis);

    /// <summary>
    /// 在任何牌被打出前触发，记录"上一张打出的牌"。
    /// 当 cardPlay.Card 是自己时，说明自己即将被打出，此时 _lastPlayedBeforeThis 已经是上一张牌了。
    /// 当 cardPlay.Card 不是自己且属于同一玩家时，更新记录。
    /// </summary>
    /// <param name="cardPlay">即将打出的牌信息。</param>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card, this) && BlackHandCardHelper.IsSameOwner(cardPlay.Card, this))
        {
            // 同一玩家的其他牌被打出，记录它作为"上一张"
            _lastPlayedBeforeThis = cardPlay.Card;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 回合开始时重置记录。
    /// </summary>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        _lastPlayedBeforeThis = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取上一张打出的牌（基于 BeforeCardPlayed 钩子记录）。
    /// </summary>
    /// <returns>上一张打出的牌；若不存在则返回 null。</returns>
    protected CardModel? GetPreviousCardPlayedThisTurn()
    {
        return _lastPlayedBeforeThis;
    }
    /// <summary>
    /// 读取当前主牌组中的黑手帮卡数量。
    /// </summary>
    /// <returns>主牌组黑手帮卡数量。</returns>
    protected int CountBlackHandInMasterDeck()
    {
        return BlackHandCardHelper.CountBlackHandInMasterDeck(Owner);
    }

    /// <summary>
    /// 统计本回合已打出的黑手帮卡数量，并默认排除当前牌。
    /// </summary>
    /// <returns>已打出的黑手帮卡数量。</returns>
    protected int CountBlackHandPlayedThisTurn()
    {
        return BlackHandCardHelper.CountBlackHandPlayedThisTurn(this);
    }

    /// <summary>
    /// 获取当前仍可交互的敌人列表。
    /// </summary>
    /// <returns>存活敌人列表。</returns>
    protected IReadOnlyList<Creature> GetLivingOpponents()
    {
        return BlackHandCardHelper.GetLivingOpponents(this);
    }

    /// <summary>
    /// 为目标施加黑手。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">目标单位。</param>
    /// <param name="amount">施加层数。</param>
    /// <returns>最终的黑手能力实例。</returns>
    protected Task<BlackHandPower?> ApplyBlackHand(PlayerChoiceContext choiceContext, Creature target, int amount)
    {
        return BlackHandPower.Apply(choiceContext, target, this, amount);
    }

    /// <summary>
    /// 消耗目标指定层数的黑手。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">目标单位。</param>
    /// <param name="amount">要消耗的层数。</param>
    /// <returns>实际消耗层数。</returns>
    protected Task<int> ConsumeBlackHand(PlayerChoiceContext choiceContext, Creature target, int amount)
    {
        return BlackHandPower.Consume(choiceContext, target, Owner, this, amount);
    }

    /// <summary>
    /// 消耗目标全部黑手。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">目标单位。</param>
    /// <returns>实际消耗层数。</returns>
    protected Task<int> ConsumeAllBlackHand(PlayerChoiceContext choiceContext, Creature target)
    {
        return BlackHandPower.ConsumeAll(choiceContext, target, Owner, this);
    }

    /// <summary>
    /// 在卡牌结算后顺手清一次悬赏，尽量避免金币漏发。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <returns>异步任务。</returns>
    protected Task ResolveBountyRewards(PlayerChoiceContext choiceContext)
    {
        return BlackHandCardHelper.ResolveBountyRewards(choiceContext, this);
    }
}
