using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 鸡符咒全队跨路线补丁：
/// 原版自由移动判断只看当前 hook listener，联机时队友遗物可能不会让本机地图节点变成可点击。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldAllowFreeTravel))]
public static class RoosterTeamFreeTravelPatch
{
    /// <summary>
    /// 当原版判断不允许自由移动时，额外扫描全队玩家的鸡符咒，让跨路线能力真正全队共享。
    /// </summary>
    /// <param name="runState">当前跑团状态。</param>
    /// <param name="__result">原版自由移动判断结果。</param>
    public static void Postfix(IRunState runState, ref bool __result)
    {
        if (__result)
        {
            return;
        }

        Player? roosterOwner = FindRoosterOwnerWithFreeTravel(runState);
        if (roosterOwner == null)
        {
            return;
        }

        __result = true;
        MainFile.Logger.Info($"【鸡符咒】检测到玩家{roosterOwner.NetId}持有可用鸡符咒，允许全队跨路线选择房间。");
    }

    /// <summary>
    /// 查找仍可提供凌空虚渡的鸡符咒持有者。
    /// </summary>
    /// <param name="runState">当前跑团状态。</param>
    /// <returns>找到时返回持有者；否则返回 null。</returns>
    private static Player? FindRoosterOwnerWithFreeTravel(IRunState runState)
    {
        foreach (Player player in runState.Players)
        {
            if (player.Creature.IsDead)
            {
                continue;
            }

            RoosterTalisman? rooster = player.GetRelic<RoosterTalisman>();
            if (rooster?.CanAllowTeamFreeTravel == true)
            {
                return player;
            }
        }

        return null;
    }
}
