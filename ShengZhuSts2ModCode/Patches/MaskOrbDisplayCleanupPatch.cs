using HarmonyLib;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 独立面具栏清理补丁：战斗结束或离开战斗房间时清理显示节点，防止穿透到奖励和地图界面。
/// </summary>
public static class MaskOrbDisplayCleanupPatch
{
    /// <summary>
    /// 单个战斗角色收到战斗结束事件后，清掉挂在该角色节点下的面具栏。
    /// </summary>
    [HarmonyPatch(typeof(NCreature), "OnCombatEnded")]
    public static class CreatureCombatEndedPatch
    {
        /// <summary>
        /// 战斗结束后清理当前角色节点的独立面具栏。
        /// </summary>
        /// <param name="__instance">刚收到战斗结束事件的角色节点。</param>
        /// <param name="_">结束的战斗房间。</param>
        public static void Postfix(NCreature __instance, CombatRoom _)
        {
            MaskOrbDisplayService.Clear(__instance);
            ShengZhuFormSwitchButtonService.ClearAll();
        }
    }

    /// <summary>
    /// 战斗房间退出场景树时兜底清理所有面具栏。
    /// </summary>
    [HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom._ExitTree))]
    public static class CombatRoomExitTreePatch
    {
        /// <summary>
        /// 离开战斗房间时统一清理所有独立面具栏。
        /// </summary>
        public static void Prefix()
        {
            MaskOrbDisplayService.ClearAll();
            ShengZhuFormSwitchButtonService.ClearAll();
        }
    }
}
