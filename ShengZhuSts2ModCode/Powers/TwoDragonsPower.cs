using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 二龙戏珠能力：你打出的下N张牌额外触发一次（Amount表示剩余触发次数）。
/// </summary>
public class TwoDragonsPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 二龙戏珠属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 二龙戏珠使用计数器堆叠方式显示剩余触发次数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 修改卡牌打出次数：让牌额外触发一次，同时消耗一层计数。
    /// 当计数归零时移除自身。
    /// </summary>
    /// <param name="card">即将打出的卡牌。</param>
    /// <param name="target">卡牌目标。</param>
    /// <param name="playCount">当前打出次数。</param>
    /// <returns>修改后的打出次数。</returns>
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (Owner == null || Amount <= 0 || !IsOwnerCard(card))
        {
            return playCount;
        }

        Flash();

        // 消耗一层
        if (Amount <= 1)
        {
            // 标记待移除，延迟到下一个异步钩子中处理
            _pendingRemoval = true;
            SetAmount(0);
        }
        else
        {
            SetAmount(Amount - 1);
        }

        return playCount + 1;
    }

    /// <summary>
    /// 标记是否需要在下次异步钩子中移除自身。
    /// </summary>
    private bool _pendingRemoval;

    /// <summary>
    /// 在牌打出后检查是否需要移除自身。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">刚打出的牌信息。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_pendingRemoval && IsOwnerCard(cardPlay.Card))
        {
            _pendingRemoval = false;
            await PowerCmd.Remove(this);
        }
    }

    /// <summary>
    /// 判断当前打出的牌是否属于二龙戏珠持有者本人，避免联机时队友消耗本效果。
    /// </summary>
    /// <param name="card">当前打出的卡牌。</param>
    /// <returns>属于能力持有者本人时返回 true。</returns>
    private bool IsOwnerCard(CardModel? card)
    {
        return Owner != null && card?.Owner?.Creature == Owner;
    }
}
