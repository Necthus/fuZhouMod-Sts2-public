using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>
/// 虎符咒失衡全局战斗监听器：只在玩家方第一回合开始前触发服务判定。
/// </summary>
public class TalismanImbalanceCombatHook : ShengZhuSts2ModPower
{
    /// <summary>
    /// 全局监听器不展示在战斗界面。
    /// </summary>
    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 隐藏监听器使用增益类型占位，不参与真实状态显示。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 隐藏监听器不叠层。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>
    /// 玩家方第一回合开始前，对全体玩家分别执行虎符咒失衡判定。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">即将开始回合的一方。</param>
    /// <param name="combatState">当前战斗状态。</param>
    /// <returns>异步任务。</returns>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || combatState.RoundNumber != 1)
        {
            return;
        }

        await TalismanImbalanceService.ApplyForCombatStart(combatState);
    }
}
