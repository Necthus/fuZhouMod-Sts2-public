using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 岁月史书残卷房间临时状态管理器：记录休息处多选、商店零元购和商店补货是否只在当前房间生效。
/// </summary>
internal static class HistoryBookRewriteManager
{
    /// <summary>
    /// 当前房间临时状态列表；按玩家和房间对象隔离，避免联机队友或下一房间串状态。
    /// </summary>
    private static readonly List<HistoryBookRoomRewriteState> ActiveStates = [];

    /// <summary>
    /// 开启本次休息处多选。
    /// </summary>
    /// <param name="player">残卷持有者。</param>
    public static void EnableCampfireMultiSelect(Player player)
    {
        HistoryBookRoomRewriteState state = GetOrCreateState(player);
        state.CampfireMultiSelect = true;
        MainFile.Logger.Info($"【岁月史书残卷】开启休息处多选：玩家={player.NetId}，房间={FormatRoom(player.RunState.CurrentRoom)}，作用范围=仅该玩家当前房间。");
    }

    /// <summary>
    /// 开启本次商店零元购。
    /// </summary>
    /// <param name="player">残卷持有者。</param>
    public static void EnableShopFree(Player player)
    {
        HistoryBookRoomRewriteState state = GetOrCreateState(player);
        state.ShopFree = true;
        MainFile.Logger.Info($"【岁月史书残卷】开启商店零元购：玩家={player.NetId}，房间={FormatRoom(player.RunState.CurrentRoom)}，作用范围=仅该玩家当前房间。");
        RefreshMerchantEntriesForPlayer(player, "开启商店零元购");
    }

    /// <summary>
    /// 开启本次商店补货。
    /// </summary>
    /// <param name="player">残卷持有者。</param>
    public static void EnableShopRestock(Player player)
    {
        HistoryBookRoomRewriteState state = GetOrCreateState(player);
        state.ShopRestock = true;
        MainFile.Logger.Info($"【岁月史书残卷】开启商店补货：玩家={player.NetId}，房间={FormatRoom(player.RunState.CurrentRoom)}，作用范围=仅该玩家当前房间。");
    }

    /// <summary>
    /// 判断休息处是否应保留剩余选项。
    /// </summary>
    /// <param name="player">正在操作休息处的玩家。</param>
    /// <returns>当前房间已开启残卷休息处多选时返回 true。</returns>
    public static bool ShouldKeepRestSiteOptions(Player player)
    {
        return FindActiveState(player)?.CampfireMultiSelect == true;
    }

    /// <summary>
    /// 判断商店商品是否应改价为 0。
    /// </summary>
    /// <param name="player">购买商品的玩家。</param>
    /// <returns>当前房间已开启残卷零元购时返回 true。</returns>
    public static bool ShouldMakeMerchantFree(Player player)
    {
        return FindActiveState(player)?.ShopFree == true;
    }

    /// <summary>
    /// 判断商店商品售出后是否应补货。
    /// </summary>
    /// <param name="entry">商品条目。</param>
    /// <param name="player">购买商品的玩家。</param>
    /// <returns>当前房间已开启残卷补货时返回 true。</returns>
    public static bool ShouldRefillMerchantEntry(MerchantEntry entry, Player player)
    {
        return entry != null && FindActiveState(player)?.ShopRestock == true;
    }

    /// <summary>
    /// 判断商店删牌服务是否应保持可用。
    /// </summary>
    /// <param name="player">正在使用删牌服务的玩家。</param>
    /// <returns>当前房间已开启残卷补货时返回 true。</returns>
    public static bool ShouldKeepMerchantCardRemovalAvailable(Player player)
    {
        return FindActiveState(player)?.ShopRestock == true;
    }

    /// <summary>
    /// 刷新指定玩家当前商店的条目显示，避免零元购状态已生效但价格标签仍显示旧价格。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="source">刷新来源。</param>
    private static void RefreshMerchantEntriesForPlayer(Player player, string source)
    {
        if (player.RunState.CurrentRoom is not MerchantRoom merchantRoom)
        {
            MainFile.Logger.Info($"【岁月史书残卷】商店价格刷新跳过：来源={source}，玩家={player.NetId}，原因=当前房间不是商店。");
            return;
        }

        MerchantInventory? inventory = merchantRoom.Inventories.FirstOrDefault(item => item.Player.NetId == player.NetId);
        if (inventory == null || inventory.Player.NetId != player.NetId)
        {
            MainFile.Logger.Info($"【岁月史书残卷】商店价格刷新跳过：来源={source}，目标玩家={player.NetId}，库存玩家={inventory?.Player.NetId.ToString() ?? "空"}，原因=仅刷新目标玩家自己的商店库存。");
            return;
        }

        int count = 0;
        foreach (MerchantEntry entry in inventory.AllEntries)
        {
            entry.OnMerchantInventoryUpdated();
            count++;
        }

        MainFile.Logger.Info($"【岁月史书残卷】商店价格显示已刷新：来源={source}，玩家={player.NetId}，条目数={count}，作用范围=仅该玩家当前商店。");
    }

    /// <summary>
    /// 获取或创建当前玩家、当前房间的临时状态。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>当前房间状态。</returns>
    private static HistoryBookRoomRewriteState GetOrCreateState(Player player)
    {
        AbstractRoom? room = player.RunState.CurrentRoom;
        HistoryBookRoomRewriteState? existing = ActiveStates.FirstOrDefault(state => state.OwnerNetId == player.NetId && ReferenceEquals(state.Room, room));
        if (existing != null)
        {
            return existing;
        }

        ActiveStates.RemoveAll(state => state.OwnerNetId == player.NetId && !ReferenceEquals(state.Room, room));
        HistoryBookRoomRewriteState created = new(player.NetId, room);
        ActiveStates.Add(created);
        return created;
    }

    /// <summary>
    /// 查找当前玩家、当前房间仍有效的临时状态。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>状态；不存在时返回 null。</returns>
    private static HistoryBookRoomRewriteState? FindActiveState(Player player)
    {
        AbstractRoom? room = player.RunState.CurrentRoom;
        ActiveStates.RemoveAll(state => state.OwnerNetId == player.NetId && !ReferenceEquals(state.Room, room));
        return ActiveStates.FirstOrDefault(state => state.OwnerNetId == player.NetId && ReferenceEquals(state.Room, room));
    }

    /// <summary>
    /// 格式化房间信息，方便日志排查。
    /// </summary>
    /// <param name="room">房间。</param>
    /// <returns>房间文本。</returns>
    private static string FormatRoom(AbstractRoom? room)
    {
        return room?.GetType().Name ?? "空";
    }

    /// <summary>
    /// 单个玩家在单个房间内开启的残卷临时状态。
    /// </summary>
    private sealed class HistoryBookRoomRewriteState
    {
        /// <summary>
        /// 残卷持有者网络 ID。
        /// </summary>
        public ulong OwnerNetId { get; }

        /// <summary>
        /// 生效房间对象。
        /// </summary>
        public AbstractRoom? Room { get; }

        /// <summary>
        /// 是否开启休息处多选。
        /// </summary>
        public bool CampfireMultiSelect { get; set; }

        /// <summary>
        /// 是否开启商店零元购。
        /// </summary>
        public bool ShopFree { get; set; }

        /// <summary>
        /// 是否开启商店补货。
        /// </summary>
        public bool ShopRestock { get; set; }

        /// <summary>
        /// 构造房间状态。
        /// </summary>
        /// <param name="ownerNetId">残卷持有者网络 ID。</param>
        /// <param name="room">生效房间对象。</param>
        public HistoryBookRoomRewriteState(ulong ownerNetId, AbstractRoom? room)
        {
            OwnerNetId = ownerNetId;
            Room = room;
        }
    }
}
