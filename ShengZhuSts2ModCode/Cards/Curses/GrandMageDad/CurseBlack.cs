using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Curses.GrandMageDad;

/// <summary>
/// 布莱克诅咒：抽到后让技能牌本回合费用增加，回合结束时给予玩家 1 层易伤。
/// </summary>
public class CurseBlack : GrandMageDadCurseCard
{
    /// <summary>
    /// 这张诅咒在手牌回合结束时会触发负面效果。
    /// </summary>
    public override bool HasTurnEndInHandEffect => true;

    /// <summary>
    /// 抽到时施加技能税。
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

        MarkTaxPending("抽到布莱克");
        await GrandMageCurseSkillTaxPower.ApplyOrRefresh(Owner.Creature, this);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【大法师老爹诅咒】【布莱克】抽到触发：玩家={Owner.NetId}，下一张技能牌需要支付布莱克代价。");
    }

    /// <summary>
    /// 玩家回合结束时施加易伤。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        if (Owner?.Creature == null || Owner.Creature.Side != CombatSide.Player)
        {
            return;
        }

        await PowerCmd.Apply<VulnerablePower>(Owner.Creature, 1, Owner.Creature, this);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【大法师老爹诅咒】【布莱克】回合结束触发：玩家={Owner.NetId}，施加1层易伤。");
    }
}
