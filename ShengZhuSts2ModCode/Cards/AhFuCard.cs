using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 阿福卡牌基类：统一封装阿福连招判定和悬赏收尾逻辑。
/// 继承此类的卡牌自动被识别为阿福体系牌。
/// </summary>
public abstract class AhFuCard(int cost, CardType type, CardRarity rarity, TargetType target) :
    ShengZhuSts2ModCard(cost, type, rarity, target)
{
    /// <summary>
    /// 本回合是否已有其他阿福牌被打出（用于连招判定）。
    /// </summary>
    private bool _afuComboActive;

    /// <summary>
    /// 条件满足时让手牌发出金色高亮，提示这张阿福牌的连招效果可以触发。
    /// </summary>
    protected override bool ShouldGlowGoldInternal => _afuComboActive;

    /// <summary>
    /// 在任何牌被打出前触发，追踪本回合是否有阿福牌打出。
    /// </summary>
    /// <param name="cardPlay">即将打出的牌信息。</param>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, this))
        {
            _afuComboActive = BlackHandCardHelper.HasAfuPlayedThisTurnBefore(this);
        }
        else if (cardPlay.Card is AhFuCard && BlackHandCardHelper.IsSameOwner(cardPlay.Card, this))
        {
            _afuComboActive = true;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 出牌阶段开始时重置连招状态。
    /// </summary>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        _afuComboActive = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 判断本回合是否已有其他阿福牌被打出（阿福连招激活）。
    /// </summary>
    /// <returns>连招激活时返回 true。</returns>
    protected bool IsAfuComboActive()
    {
        return _afuComboActive;
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
