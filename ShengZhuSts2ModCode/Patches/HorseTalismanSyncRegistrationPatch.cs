using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// Harmony Patch：跑团联机组件初始化后注册遗物同步消息处理器。
/// 防止远端玩家先触发遗物交互时，本机还没懒注册导致漏收消息。
/// </summary>
[HarmonyPatch(typeof(RunManager), "InitializeShared")]
public static class HorseTalismanSyncRegistrationPatch
{
    /// <summary>
    /// RunManager 创建本局消息缓冲器后，注册遗物消息处理器。
    /// </summary>
    /// <param name="__instance">当前跑团管理器。</param>
    public static void Postfix(RunManager __instance)
    {
        RelicInteractionSyncService.EnsureRegisteredForRun(__instance);
    }
}
