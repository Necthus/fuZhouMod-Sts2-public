using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 岁月史书残卷商店删牌补丁：补货状态下跳过删牌服务置灰，让本次商店可以多次删牌。
/// </summary>
[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.OnCardRemovalUsed))]
public static class HistoryBookMerchantCardRemovalPatch
{
    /// <summary>
    /// 删牌成功后，如果残卷商店补货已开启，则不调用原版 SetUsed。
    /// </summary>
    /// <param name="__instance">商店库存节点。</param>
    /// <returns>需要继续执行原版逻辑时返回 true。</returns>
    public static bool Prefix(NMerchantInventory __instance)
    {
        MerchantInventory? inventory = __instance.Inventory;
        Player? player = inventory?.Player;
        MerchantCardRemovalEntry? removalEntry = inventory?.CardRemovalEntry;
        if (player == null || removalEntry == null || !HistoryBookRewriteManager.ShouldKeepMerchantCardRemovalAvailable(player))
        {
            return true;
        }

        removalEntry.CalcCost();
        removalEntry.OnMerchantInventoryUpdated();
        MainFile.Logger.Info($"【岁月史书残卷】商店补货保留删牌服务：玩家={player.NetId}，作用范围=仅该玩家当前商店，下次删牌价格={removalEntry.Cost}。");
        return false;
    }
}
