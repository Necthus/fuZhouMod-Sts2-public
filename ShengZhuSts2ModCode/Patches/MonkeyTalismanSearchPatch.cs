using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 猴符咒检索框补丁：在选牌网格初始化完成后注入专用检索栏。
/// </summary>
[HarmonyPatch(typeof(NCardGridSelectionScreen), "ConnectSignalsAndInitGrid")]
public static class MonkeyTalismanSearchAttachPatch
{
    /// <summary>
    /// 选牌界面初始化完成后，注入猴符咒专用检索框。
    /// </summary>
    /// <param name="__instance">当前选牌界面。</param>
    public static void Postfix(NCardGridSelectionScreen __instance)
    {
        MonkeyTalismanSearchHelper.TryAttachSearchBar(__instance);
    }
}

/// <summary>
/// 猴符咒检索框补丁：在选牌界面退出时清理专用检索状态。
/// </summary>
[HarmonyPatch(typeof(NCardGridSelectionScreen), nameof(NCardGridSelectionScreen._ExitTree))]
public static class MonkeyTalismanSearchCleanupPatch
{
    /// <summary>
    /// 选牌界面退出时，清理猴符咒专用检索状态。
    /// </summary>
    /// <param name="__instance">当前选牌界面。</param>
    public static void Prefix(NCardGridSelectionScreen __instance)
    {
        MonkeyTalismanSearchHelper.RemoveSearchBar(__instance);
    }
}
