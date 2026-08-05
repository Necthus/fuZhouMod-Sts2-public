using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 夺舍禁攻能力：本回合禁止拥有者继续使用攻击牌。
/// </summary>
public class PossessionNoAttackPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 禁攻属于减益能力，方便玩家看到限制。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 使用计数器显示剩余回合。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 阻止拥有者使用攻击牌。
    /// </summary>
    /// <param name="card">将要使用的牌。</param>
    /// <param name="autoPlayType">自动打出类型。</param>
    /// <returns>允许使用时返回 true。</returns>
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        bool shouldBlock = Owner != null && card.Owner?.Creature == Owner && card.Type == CardType.Attack;
        if (shouldBlock)
        {
            MainFile.Logger.Info($"【夺舍】本回合禁止使用攻击牌：卡牌={card.Id.Entry}。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 拥有者回合结束时移除此能力。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">当前结束回合的一方。</param>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (Owner != null && side == Owner.Side)
        {
            await PowerCmd.Remove(this);
            MainFile.Logger.Info("【夺舍】回合结束，解除禁止攻击。");
        }
    }
}
