namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 圣主日志辅助类：集中控制高频战斗调试日志，默认关闭以避免战斗中大量刷日志造成卡顿。
/// </summary>
public static class ShengZhuLogHelper
{
    /// <summary>
    /// 是否输出高频战斗调试日志；性能排查需要现场信息时可手动打开。
    /// </summary>
    public static bool EnableVerboseCombatLog { get; set; } = false;

    /// <summary>
    /// 输出高频战斗调试日志；关闭时不会构造日志字符串。
    /// </summary>
    /// <param name="messageFactory">日志文本创建函数。</param>
    public static void VerboseCombatInfo(Func<string> messageFactory)
    {
        if (!EnableVerboseCombatLog)
        {
            return;
        }

        MainFile.Logger.Info(messageFactory());
    }
}
