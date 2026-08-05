using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 黑手帮护送：下回合开始时，按敌方总黑手层数给予格挡后移除自身。
/// </summary>
public class BlackHandEscortPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 黑手帮护送属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 黑手帮护送使用计数器显示倍率。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 在拥有者回合开始时，按敌方总黑手层数给予格挡并移除该能力。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">当前回合玩家。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null || player.Creature != Owner)
        {
            return;
        }

        int totalBlackHand = 0;
        if (Owner.CombatState != null)
        {
            foreach (Creature creature in Owner.CombatState.GetOpponentsOf(Owner))
            {
                if (BlackHandCardHelper.IsLivingCreature(creature))
                {
                    totalBlackHand += BlackHandPower.GetAmount(creature);
                }
            }
        }

        if (totalBlackHand > 0)
        {
            await CreatureCmd.GainBlock(Owner, totalBlackHand * Amount, default, null);
        }

        await PowerCmd.Remove(this);
    }
}
