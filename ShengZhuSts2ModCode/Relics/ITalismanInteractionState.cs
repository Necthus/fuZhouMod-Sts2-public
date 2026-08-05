namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 符咒交互状态接口，用于右键日志和UI刷新时统一读取当前状态。
/// </summary>
public interface ITalismanInteractionState
{
    /// <summary>
    /// 当前符咒是否处于激发状态。
    /// </summary>
    bool IsActivated { get; }

    /// <summary>
    /// 当前符咒剩余冷却回合数。
    /// </summary>
    int CooldownTurnsRemaining { get; }
}
