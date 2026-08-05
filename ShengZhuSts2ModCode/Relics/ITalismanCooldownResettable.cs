namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 可被兔符咒刷新冷却的符咒接口。
/// 实现此接口的符咒遗物可以被兔符咒右键点击来重置冷却。
/// </summary>
public interface ITalismanCooldownResettable
{
    /// <summary>
    /// 当前是否处于冷却中（可被刷新）。
    /// </summary>
    bool IsOnCooldown { get; }

    /// <summary>
    /// 重置冷却状态，使符咒立即可用。
    /// </summary>
    void ResetCooldown();
}
