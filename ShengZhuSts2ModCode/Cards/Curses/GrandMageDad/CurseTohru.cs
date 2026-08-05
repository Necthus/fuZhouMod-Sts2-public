using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Curses.GrandMageDad;

/// <summary>
/// 特鲁诅咒：抽到后让攻击牌本回合费用增加，若本回合没有打出攻击牌则回合结束失去生命。
/// </summary>
public class CurseTohru : GrandMageDadCurseCard
{
    /// <summary>
    /// 未打出攻击牌时受到的生命损失。
    /// </summary>
    private const int HpLoss = 6;

    /// <summary>
    /// 这张诅咒在手牌回合结束时会触发负面效果。
    /// </summary>
    public override bool HasTurnEndInHandEffect => true;

    /// <summary>
    /// 抽到时施加攻击税。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="card">被抽到的卡牌。</param>
    /// <param name="fromHandDraw">是否来自手牌抽牌。</param>
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (!ReferenceEquals(card, this) || Owner?.Creature == null)
        {
            return;
        }

        MarkTaxPending("抽到特鲁");
        await GrandMageCurseAttackTaxPower.ApplyOrRefresh(Owner.Creature, this);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【大法师老爹诅咒】【特鲁】抽到触发：玩家={Owner.NetId}，下一张攻击牌需要支付特鲁代价。");
    }

    /// <summary>
    /// 玩家回合结束时，若本回合没有打出攻击牌则失去生命。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        if (AttackCardPlayHelper.HasAttackPlayedThisTurn(this))
        {
            return;
        }

        await CreatureCmd.Damage(choiceContext, Owner.Creature, HpLoss, ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【大法师老爹诅咒】【特鲁】回合结束触发：玩家={Owner.NetId}，本回合未打出攻击牌，失去生命={HpLoss}。");
    }
}
