using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 遗物右键交互接口。
/// 实现此接口的遗物可以通过右键点击触发自身效果。
/// </summary>
public interface IRelicRightClickable
{
    /// <summary>
    /// 右键点击时触发的回调。
    /// </summary>
    /// <param name="context">玩家选择上下文。</param>
    /// <returns>异步任务。</returns>
    Task OnRightClick(PlayerChoiceContext context);
}
