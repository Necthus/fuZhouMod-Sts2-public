using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Curses.GrandMageDad;

/// <summary>
/// 成龙诅咒：留在手牌到回合结束时，给予玩家 1 层虚弱。
/// </summary>
public class CurseJackie : GrandMageDadCurseCard
{
    /// <summary>
    /// 这张诅咒在手牌回合结束时会触发负面效果。
    /// </summary>
    public override bool HasTurnEndInHandEffect => true;

    /// <summary>
    /// 玩家回合结束时施加虚弱。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        if (Owner?.Creature == null || Owner.Creature.Side != CombatSide.Player)
        {
            return;
        }

        await PowerCmd.Apply<WeakPower>(Owner.Creature, 1, Owner.Creature, this);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【大法师老爹诅咒】【成龙】回合结束触发：玩家={Owner.NetId}，施加1层虚弱。");
    }
}
