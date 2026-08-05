using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 黑手施加监听接口：用于让赏金、痞子等能力在黑手层数增加时联动触发。
/// </summary>
public interface IBlackHandAppliedListener
{
    /// <summary>
    /// 当玩家向目标施加黑手后触发。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">被施加黑手的目标。</param>
    /// <param name="appliedAmount">本次实际施加的黑手层数。</param>
    /// <returns>异步任务。</returns>
    Task OnBlackHandApplied(PlayerChoiceContext choiceContext, Creature target, int appliedAmount);
}
