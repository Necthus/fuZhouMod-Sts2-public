using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 金鸡王宝藏能力：本回合内打出的阿福、黑手帮和黑暗杀手牌费用变为0。回合结束时移除自身。
/// </summary>
public class ValmontTreasurePower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 金鸡王宝藏属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 金鸡王宝藏不叠层（单层效果）。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>
    /// 在任何牌被打出前，刷新手牌中宝藏支持牌的费用为0。
    /// 确保因抽牌效果新进入手牌的卡在被打出前已经变为0费。
    /// </summary>
    /// <param name="cardPlay">即将打出的牌信息。</param>
    /// <returns>异步任务。</returns>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (!IsOwnerCard(cardPlay.Card))
        {
            return Task.CompletedTask;
        }

        RefreshHandCosts();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 每当一张牌被打出后，刷新手牌中宝藏支持牌的费用为0。
    /// 这也覆盖了打出牌触发抽牌后的情况。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">刚打出的牌信息。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!IsOwnerCard(cardPlay.Card))
        {
            return Task.CompletedTask;
        }

        RefreshHandCosts();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 当牌被抽到手牌时，若是宝藏支持牌则本回合费用变为0。
    /// </summary>
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (IsOwnedTreasureCard(card))
        {
            ApplyTemporaryZeroCost(card);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 当牌通过非抽牌方式进入手牌时（如生成牌加入手牌），若是宝藏支持牌则本回合费用变为0。
    /// </summary>
    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, MegaCrit.Sts2.Core.Models.AbstractModel? source)
    {
        // 只处理进入手牌的情况
        if (Owner?.Player?.PlayerCombatState == null)
        {
            return Task.CompletedTask;
        }

        if (Owner.Player.PlayerCombatState.Hand.Cards.Contains(card) && IsOwnedTreasureCard(card))
        {
            ApplyTemporaryZeroCost(card);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 回合结束时移除自身（只持续一回合）。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">当前结束回合的阵营。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side == CombatSide.Player && Owner != null)
        {
            await PowerCmd.Remove(this);
        }
    }

    /// <summary>
    /// 初始化时刷新手牌费用（在能力被施加后由卡牌调用）。
    /// </summary>
    public void InitializeHandCosts()
    {
        RefreshHandCosts();
    }

    /// <summary>
    /// 遍历手牌中所有宝藏支持牌，将其费用本回合设为0。
    /// </summary>
    private void RefreshHandCosts()
    {
        if (Owner?.Player?.PlayerCombatState == null)
        {
            return;
        }

        foreach (CardModel card in Owner.Player.PlayerCombatState.Hand.Cards)
        {
            if (IsOwnedTreasureCard(card))
            {
                ApplyTemporaryZeroCost(card);
            }
        }
    }

    /// <summary>
    /// 判断卡牌是否属于当前能力持有者，避免联机时误刷新队友手牌费用。
    /// </summary>
    private bool IsOwnerCard(CardModel? card)
    {
        if (Owner?.Player == null || card?.Owner == null)
        {
            return false;
        }

        return ReferenceEquals(card.Owner, Owner.Player) || card.Owner.NetId == Owner.Player.NetId;
    }

    /// <summary>
    /// 判断卡牌是否同时属于当前能力持有者和金鸡王宝藏减费范围。
    /// </summary>
    private bool IsOwnedTreasureCard(CardModel? card)
    {
        return IsOwnerCard(card) && BlackHandCardHelper.IsValmontTreasureCostReducedCard(card);
    }

    /// <summary>
    /// 按一代金鸡王规则只修改本回合费用，回合结束后由框架自动恢复。
    /// </summary>
    private static void ApplyTemporaryZeroCost(CardModel card)
    {
        card.EnergyCost.SetThisTurn(0, reduceOnly: true);
    }
}
