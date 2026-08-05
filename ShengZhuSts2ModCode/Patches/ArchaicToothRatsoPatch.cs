using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 古老牙齿预览补丁：圣主牌组存在黑手帮·拉苏时，显示拉苏专属先古变化预览。
/// </summary>
[HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.SetupForPlayer))]
public static class ArchaicToothRatsoSetupPatch
{
    /// <summary>
    /// 圣主专属前置补丁；命中拉苏变化时跳过本体基础牌变化逻辑。
    /// </summary>
    /// <param name="__instance">古老牙齿实例。</param>
    /// <param name="player">当前玩家。</param>
    /// <param name="__result">是否可以设置古老牙齿。</param>
    /// <returns>需要继续走本体逻辑时返回 true。</returns>
    public static bool Prefix(ArchaicTooth __instance, Player player, ref bool __result)
    {
        bool handled = AncientRatsoTransformHelper.TrySetupPreview(
            player,
            (starterCard, ancientCard) => __instance.SetupForTests(starterCard.ToSerializable(), ancientCard.ToSerializable()));

        if (!handled)
        {
            return true;
        }

        __result = true;
        return false;
    }
}

/// <summary>
/// 古老牙齿获得后补丁：圣主牌组存在黑手帮·拉苏时，实际变化为先古拉苏。
/// </summary>
[HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.AfterObtained))]
public static class ArchaicToothRatsoAfterObtainedPatch
{
    /// <summary>
    /// 圣主专属前置补丁；命中拉苏变化时跳过本体基础牌变化逻辑。
    /// </summary>
    /// <param name="__instance">古老牙齿实例。</param>
    /// <param name="__result">替换任务。</param>
    /// <returns>需要继续走本体逻辑时返回 true。</returns>
    public static bool Prefix(ArchaicTooth __instance, ref Task __result)
    {
        if (!AncientRatsoTransformHelper.CanTransformRatso(__instance.Owner))
        {
            return true;
        }

        __result = AncientRatsoTransformHelper.TransformRatso(__instance.Owner);
        return false;
    }
}
