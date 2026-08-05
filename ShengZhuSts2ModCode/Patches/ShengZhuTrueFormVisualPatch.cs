using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 圣主真身视觉补丁：在战斗、休息处、商店创建角色节点以及鼠符咒变化时刷新外观。
/// </summary>
public static class ShengZhuTrueFormVisualPatch
{
    /// <summary>
    /// 战斗角色节点准备完成后，根据当前遗物状态刷新圣主外观。
    /// </summary>
    [HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
    public static class CreatureReadyPatch
    {
        /// <summary>
        /// 节点完成初始化后刷新，避免节点还没挂进树时找不到真身子节点。
        /// </summary>
        /// <param name="__instance">刚初始化完成的战斗角色节点。</param>
        public static void Postfix(NCreature __instance)
        {
            ShengZhuTrueFormVisualService.Refresh(__instance.Entity?.Player);
            ShengZhuFormSwitchButtonService.RefreshAll();
        }
    }

    /// <summary>
    /// 休息处角色节点准备完成后，根据当前遗物状态刷新圣主外观。
    /// </summary>
    [HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter._Ready))]
    public static class RestSiteCharacterReadyPatch
    {
        /// <summary>
        /// 休息处节点创建后刷新石板/真身状态。
        /// </summary>
        /// <param name="__instance">休息处角色节点。</param>
        public static void Postfix(NRestSiteCharacter __instance)
        {
            ShengZhuTrueFormVisualService.Refresh(__instance);
        }
    }

    /// <summary>
    /// 商店角色节点准备前，跳过圣主自定义非 Spine 场景的原版动画播放。
    /// </summary>
    [HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter._Ready))]
    public static class MerchantCharacterReadyPatch
    {
        /// <summary>
        /// 圣主商店展示是普通 Node2D 拼图，不走原版 Spine 动画。
        /// </summary>
        /// <param name="__instance">商店角色节点。</param>
        /// <returns>普通节点继续执行原版逻辑，圣主自定义节点跳过原版逻辑。</returns>
        public static bool Prefix(NMerchantCharacter __instance)
        {
            return !ShengZhuTrueFormVisualService.HasShengZhuVisuals(__instance);
        }
    }

    /// <summary>
    /// 商店房间加载完玩家展示后，刷新圣主商店外观。
    /// </summary>
    [HarmonyPatch(typeof(NMerchantRoom), "AfterRoomIsLoaded")]
    public static class MerchantRoomLoadedPatch
    {
        /// <summary>
        /// 商店里玩家和节点是一一对应的，房间加载完成后统一刷新。
        /// </summary>
        /// <param name="__instance">商店房间节点。</param>
        public static void Postfix(NMerchantRoom __instance)
        {
            ShengZhuTrueFormVisualService.Refresh(__instance);
        }
    }

    /// <summary>
    /// 玩家获得遗物后，如果是鼠符咒则刷新圣主外观。
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.AddRelicInternal))]
    public static class AddRelicPatch
    {
        /// <summary>
        /// 获得鼠符咒后立即切换为真身。
        /// </summary>
        /// <param name="__instance">获得遗物的玩家。</param>
        /// <param name="relic">刚获得的遗物。</param>
        public static void Postfix(Player __instance, RelicModel relic)
        {
            if (relic is RatTalisman rat)
            {
                _ = ShengZhuFormService.ApplyRatObtainedForm(__instance, rat);
            }
        }
    }

    /// <summary>
    /// 玩家移除遗物后，如果是鼠符咒则刷新圣主外观。
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.RemoveRelicInternal))]
    public static class RemoveRelicPatch
    {
        /// <summary>
        /// 移除鼠符咒后回退为石板。
        /// </summary>
        /// <param name="__instance">失去遗物的玩家。</param>
        /// <param name="relic">刚移除的遗物。</param>
        public static void Postfix(Player __instance, RelicModel relic)
        {
            if (relic is RatTalisman)
            {
                _ = ShengZhuFormService.ApplyRatRemovedForm(__instance);
            }
        }
    }
}
