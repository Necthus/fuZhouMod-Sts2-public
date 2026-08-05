using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 安全动画触发补丁：联机时 BaseLib 自定义动画可能访问已释放的 Godot 动画节点，
/// 这里只吞掉这类纯视觉异常，避免战斗结算被动画节点生命周期打断。
/// </summary>
[HarmonyPatch(typeof(NCreature), "SetAnimationTrigger")]
public static class SafeAnimationTriggerPatch
{
    /// <summary>
    /// Harmony 终结补丁：捕获动画触发链路上的异常，只忽略已释放 Godot 动画对象。
    /// </summary>
    /// <param name="__exception">动画触发过程中抛出的异常。</param>
    /// <param name="trigger">本次尝试触发的动画名。</param>
    /// <returns>返回 null 表示吞掉异常；返回原异常表示继续抛出。</returns>
    [HarmonyFinalizer]
    public static Exception? Finalizer(Exception? __exception, string trigger)
    {
        if (__exception == null)
        {
            return null;
        }

        if (!SafeGodotAnimationExceptionHelper.IsDisposedGodotAnimationException(__exception))
        {
            return __exception;
        }

        MainFile.Logger.Info($"【联机动画兜底】忽略已释放的 Godot 动画节点异常，触发器={trigger}，异常={__exception.Message}");
        return null;
    }
}

/// <summary>
/// 安全自定义动画补丁：直接兜住 BaseLib 自定义动画播放入口，
/// 覆盖死亡、复活等不经过 NCreature.SetAnimationTrigger 的动画路径。
/// </summary>
[HarmonyPatch(typeof(CustomAnimation), nameof(CustomAnimation.PlayCustomAnimation))]
public static class SafeCustomAnimationPlayPatch
{
    /// <summary>
    /// Harmony 终结补丁：BaseLib 自定义动画节点已释放时，改为返回未播放自定义动画。
    /// </summary>
    /// <param name="__exception">播放自定义动画过程中抛出的异常。</param>
    /// <param name="__result">原方法返回值；异常被吞掉时改为 false。</param>
    /// <param name="tryAnimNames">本次尝试播放的动画名列表。</param>
    /// <returns>返回 null 表示吞掉异常；返回原异常表示继续抛出。</returns>
    [HarmonyFinalizer]
    public static Exception? Finalizer(Exception? __exception, ref bool __result, string[] tryAnimNames)
    {
        if (__exception == null)
        {
            return null;
        }

        if (!SafeGodotAnimationExceptionHelper.IsDisposedGodotAnimationException(__exception))
        {
            return __exception;
        }

        __result = false;
        MainFile.Logger.Info($"【联机动画兜底】忽略 BaseLib 自定义动画节点释放异常，动画={string.Join(",", tryAnimNames)}，异常={__exception.Message}");
        return null;
    }
}

/// <summary>
/// Godot 动画异常判断工具：统一限定只处理已释放的 Godot 视觉对象。
/// </summary>
internal static class SafeGodotAnimationExceptionHelper
{
    /// <summary>
    /// 判断异常是否属于 Godot 动画节点已释放。
    /// </summary>
    /// <param name="exception">待判断异常。</param>
    /// <returns>属于已释放 Godot 动画节点异常时返回 true。</returns>
    public static bool IsDisposedGodotAnimationException(Exception exception)
    {
        return exception is ObjectDisposedException disposedException
               && disposedException.ObjectName?.StartsWith("Godot.", StringComparison.Ordinal) == true;
    }
}
