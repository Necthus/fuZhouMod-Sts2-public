using System.Reflection;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.PanKuPreview;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Rewards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 潘库宝盒：标记前路精英/怪物房为恶魔之门，战胜后提供恶魔卡、直接附魔、寄宿魔气三选一。
/// </summary>
public class PanKuBox : ShengZhuSts2ModRelic
{
    /// <summary>
    /// 潘库恶魔门绑定数据：记录地图坐标和该门固定奖励的魔气。
    /// </summary>
    private sealed class MarkedGate
    {
        /// <summary>
        /// 恶魔门所在地图坐标。
        /// </summary>
        public MapCoord Coord { get; init; }

        /// <summary>
        /// 该恶魔门固定对应的魔气；旧存档可能暂时为空。
        /// </summary>
        public PanKuDemonQiKind? Kind { get; set; }
    }

    /// <summary>
    /// 坐标分隔符，用于保存恶魔门节点。
    /// </summary>
    private const char CoordSeparator = ';';

    /// <summary>
    /// 字段分隔符，用于保存节点坐标和寄宿魔气。
    /// </summary>
    private const char FieldSeparator = ':';

    /// <summary>
    /// 保存寄宿魔气。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public string StoredKindsCsv { get; set; } = string.Empty;

    /// <summary>
    /// 保存恶魔门地图坐标和绑定魔气；塔2地图任务标记不会进地图存档，所以这里自行保存并读档恢复。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public string MarkedCoordsCsv { get; set; } = string.Empty;

    /// <summary>
    /// 保存已经生成过地狱之门的地图标识，避免同一层反复补门。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public string GeneratedMapKey { get; set; } = string.Empty;

    /// <summary>
    /// 保存奖励页 SL 兜底用的待领取魔气。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public string PendingRewardKind { get; set; } = string.Empty;

    /// <summary>
    /// 保存奖励页 SL 兜底用的待领取节点。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public string PendingRewardCoord { get; set; } = string.Empty;

    /// <summary>
    /// 保存上一次扫描到的牌组/附魔魔气，用于卡牌被删除或变化后的魔气回收。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public string KnownActiveKindsCsv { get; set; } = string.Empty;

    /// <summary>
    /// 潘库宝盒来自事件，不进入普通掉落池。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Event;

    /// <summary>
    /// 显示尚未拥有的魔气数量。
    /// </summary>
    public override bool ShowCounter => true;

    /// <summary>
    /// 计数器显示缺失魔气数。
    /// </summary>
    public override int DisplayAmount => CountMissingKinds(Owner);

    /// <summary>
    /// 有寄宿魔气时切换到带寄宿列表的描述。
    /// </summary>
    /// <returns>描述文本的本地化 key。</returns>
    public override string GetDynamicDescriptionKey()
    {
        return GetStoredKinds().Count == 0
            ? base.GetDynamicDescriptionKey()
            : Id.Entry + ".description_stored";
    }

    /// <summary>
    /// 给潘库宝盒描述补充当前寄宿魔气列表。
    /// </summary>
    /// <param name="description">准备展示的遗物描述。</param>
    public override void CustomizeDescription(LocString description)
    {
        description.Add("StoredQi", FormatStoredKindsForDisplay());
    }

    /// <summary>
    /// 获得后立刻刷新计数并标记前路恶魔门。
    /// </summary>
    public override Task AfterObtained()
    {
        RecycleLostQiFromSnapshot(Owner);
        InvokeDisplayAmountChanged();
        EightDemonPossessionHelper.CheckAndTriggerAwakening(Owner);
        GenerateMapMarksOnce(Owner?.RunState?.Map);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 进入房间后刷新计数，并在恶魔门精英战中施加和抢夺符咒一致的强化。
    /// </summary>
    /// <param name="room">当前房间。</param>
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        RecycleLostQiFromSnapshot(Owner);
        InvokeDisplayAmountChanged();
        EightDemonPossessionHelper.CheckAndTriggerAwakening(Owner);

        if (Owner == null || room is not CombatRoom combatRoom || Owner.RunState.CurrentMapCoord == null)
        {
            return;
        }

        if (!HasMarkedCoord(Owner.RunState.CurrentMapCoord.Value) || combatRoom.RoomType != RoomType.Elite)
        {
            return;
        }

        await PanKuDemonQiHelper.ApplyPanKuEliteBuff(combatRoom, Owner);
    }

    /// <summary>
    /// 地图界面刷新时重新扫描当前地图，确保跨层后继续补齐恶魔门标记。
    /// </summary>
    /// <param name="map">当前地图；为空时使用玩家运行状态里的地图。</param>
    public void RefreshMapMarks(ActMap? map = null)
    {
        RecycleLostQiFromSnapshot(Owner);
        InvokeDisplayAmountChanged();
        RestoreOrGenerateMapMarks(map ?? Owner?.RunState?.Map);
    }

    /// <summary>
    /// 奖励生成前尝试注入潘库奖励。
    /// </summary>
    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        RecycleLostQiFromSnapshot(player);
        return TryInjectPanKuReward(player, rewards, room);
    }

    /// <summary>
    /// 奖励生成后兜底尝试注入潘库奖励。
    /// </summary>
    public override bool TryModifyRewardsLate(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        RecycleLostQiFromSnapshot(player);
        return TryInjectPanKuReward(player, rewards, room);
    }

    /// <summary>
    /// 在休息处提供寄宿魔气附魔选项。
    /// </summary>
    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (Owner == null || player.NetId != Owner.NetId)
        {
            MainFile.Logger.Info($"【联机同步】【潘库宝盒】跳过非持有者休息处选项注入：选项玩家={player.NetId}，持有者={Owner?.NetId.ToString() ?? "空"}。");
            return false;
        }

        RecycleLostQiFromSnapshot(player);
        if (GetStoredKinds().Count == 0)
        {
            return false;
        }

        options.Add(new PanKuEnchantRestSiteOption(player));
        return true;
    }

    /// <summary>
    /// 执行同步后的休息处附魔：按牌组下标和卡牌ID重放同一次潘库魔气附魔。
    /// </summary>
    /// <param name="payload">休息处附魔同步数据。</param>
    /// <returns>执行成功时返回 true。</returns>
    internal bool RunSyncedRestSiteEnchant(PanKuRestEnchantPayload payload)
    {
        if (Owner == null)
        {
            MainFile.Logger.Info("【联机异常兜底】【潘库宝盒】休息处附魔重放失败：持有者为空，处理=跳过。");
            return false;
        }

        if (!GetStoredKinds().Contains(payload.Kind))
        {
            MainFile.Logger.Info($"【联机异常兜底】【潘库宝盒】休息处附魔重放失败：玩家={Owner.NetId}，魔气={PanKuDemonQiHelper.GetShortName(payload.Kind)}，原因=宝盒中没有该寄宿魔气，处理=跳过。");
            return false;
        }

        if (!DeckSyncHelper.TryGetDeckCard(Owner, payload.TargetDeckIndex, payload.TargetCardId, payload.TargetWasUpgraded, "潘库宝盒", out CardModel? target) || target == null)
        {
            return false;
        }

        if (!PanKuDemonQiHelper.CanEnchant(target, payload.Kind))
        {
            MainFile.Logger.Info($"【联机异常兜底】【潘库宝盒】休息处附魔重放失败：玩家={Owner.NetId}，目标={target.Id.Entry}，魔气={PanKuDemonQiHelper.GetShortName(payload.Kind)}，原因=目标不可附魔，处理=跳过。");
            return false;
        }

        PanKuDemonQiHelper.Enchant(target, payload.Kind);
        RemoveStoredKind(payload.Kind);
        ClearPendingReward();
        Flash();
        MainFile.Logger.Info($"【联机同步】【潘库宝盒】休息处附魔完成：玩家={Owner.NetId}，下标={payload.TargetDeckIndex}，目标={target.Title}({target.Id.Entry})，魔气={PanKuDemonQiHelper.GetShortName(payload.Kind)}。");
        return true;
    }

    /// <summary>
    /// 执行同步后的潘库奖励领取：只重放最终选择结果，不在远端打开奖励页二级选择。
    /// </summary>
    /// <param name="payload">奖励领取同步数据。</param>
    /// <returns>执行成功时返回 true。</returns>
    internal async Task<bool> RunSyncedRewardClaim(PanKuRewardClaimPayload payload)
    {
        if (Owner == null)
        {
            MainFile.Logger.Info("【联机异常兜底】【潘库宝盒】奖励领取重放失败：持有者为空，处理=跳过。");
            return false;
        }

        switch (payload.ClaimType)
        {
            case PanKuRewardClaimType.DemonCard:
                CardModel card = PanKuDemonQiHelper.CreateDemonCard(Owner, payload.Kind);
                await CardPileCmd.Add(card, PileType.Deck);
                FinishSyncedRewardClaim();
                MainFile.Logger.Info($"【联机同步】【潘库宝盒】奖励领取完成：玩家={Owner.NetId}，类型=恶魔卡，魔气={PanKuDemonQiHelper.GetShortName(payload.Kind)}，卡牌={card.Id.Entry}。");
                return true;
            case PanKuRewardClaimType.StoreQi:
                AddStoredKind(payload.Kind);
                FinishSyncedRewardClaim();
                MainFile.Logger.Info($"【联机同步】【潘库宝盒】奖励领取完成：玩家={Owner.NetId}，类型=寄宿魔气，魔气={PanKuDemonQiHelper.GetShortName(payload.Kind)}。");
                return true;
            case PanKuRewardClaimType.EnchantDeckCard:
                return RunSyncedRewardEnchant(payload);
            default:
                MainFile.Logger.Info($"【联机异常兜底】【潘库宝盒】奖励领取重放失败：玩家={Owner.NetId}，未知领取类型={payload.ClaimType}，处理=跳过。");
                return false;
        }
    }

    /// <summary>
    /// 执行潘库奖励中的直接附魔领取。
    /// </summary>
    /// <param name="payload">奖励领取同步数据。</param>
    /// <returns>执行成功时返回 true。</returns>
    private bool RunSyncedRewardEnchant(PanKuRewardClaimPayload payload)
    {
        if (Owner == null)
        {
            return false;
        }

        if (!DeckSyncHelper.TryGetDeckCard(Owner, payload.TargetDeckIndex, payload.TargetCardId, payload.TargetWasUpgraded, "潘库宝盒奖励", out CardModel? target) || target == null)
        {
            return false;
        }

        if (!PanKuDemonQiHelper.CanEnchant(target, payload.Kind))
        {
            MainFile.Logger.Info($"【联机异常兜底】【潘库宝盒】奖励附魔重放失败：玩家={Owner.NetId}，目标={target.Id.Entry}，魔气={PanKuDemonQiHelper.GetShortName(payload.Kind)}，原因=目标不可附魔，处理=跳过。");
            return false;
        }

        PanKuDemonQiHelper.Enchant(target, payload.Kind);
        FinishSyncedRewardClaim();
        MainFile.Logger.Info($"【联机同步】【潘库宝盒】奖励领取完成：玩家={Owner.NetId}，类型=直接附魔，下标={payload.TargetDeckIndex}，目标={target.Title}({target.Id.Entry})，魔气={PanKuDemonQiHelper.GetShortName(payload.Kind)}。");
        return true;
    }

    /// <summary>
    /// 完成同步奖励领取后的统一收尾：清理待领奖励、刷新计数和检查八魔附体。
    /// </summary>
    private void FinishSyncedRewardClaim()
    {
        ClearPendingReward();
        Flash();
        EightDemonPossessionHelper.CheckAndTriggerAwakening(Owner);
    }

    /// <summary>
    /// 获取所有寄宿魔气。
    /// </summary>
    /// <returns>寄宿魔气列表。</returns>
    public List<PanKuDemonQiKind> GetStoredKinds()
    {
        return ParseKinds(StoredKindsCsv);
    }

    /// <summary>
    /// 添加寄宿魔气，已有同类时不重复添加。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    public void AddStoredKind(PanKuDemonQiKind kind)
    {
        List<PanKuDemonQiKind> kinds = GetStoredKinds();
        if (!kinds.Contains(kind))
        {
            kinds.Add(kind);
            StoredKindsCsv = FormatKinds(kinds);
            InvokeDisplayAmountChanged();
            MainFile.Logger.Info($"【潘库宝盒】寄宿魔气：玩家={Owner?.NetId.ToString() ?? "空"}，魔气={PanKuDemonQiHelper.GetShortName(kind)}。");
            EightDemonPossessionHelper.CheckAndTriggerAwakening(Owner);
        }
    }

    /// <summary>
    /// 移除寄宿魔气。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    /// <returns>移除成功时返回 true。</returns>
    public bool RemoveStoredKind(PanKuDemonQiKind kind)
    {
        List<PanKuDemonQiKind> kinds = GetStoredKinds();
        bool removed = kinds.Remove(kind);
        if (removed)
        {
            StoredKindsCsv = FormatKinds(kinds);
            InvokeDisplayAmountChanged();
            MainFile.Logger.Info($"【潘库宝盒】消耗寄宿魔气：{PanKuDemonQiHelper.GetShortName(kind)}。");
            EightDemonPossessionHelper.CheckAndTriggerAwakening(Owner);
        }

        return removed;
    }

    /// <summary>
    /// 判断坐标是否被标记为恶魔门。
    /// </summary>
    /// <param name="coord">地图坐标。</param>
    /// <returns>已标记时返回 true。</returns>
    public bool HasMarkedCoord(MapCoord coord)
    {
        return ParseMarkedGates(MarkedCoordsCsv).Any(marked => IsSameCoord(marked.Coord, coord));
    }

    /// <summary>
    /// 尝试获取指定恶魔门绑定的固定魔气。
    /// </summary>
    /// <param name="coord">地图坐标。</param>
    /// <param name="kind">绑定魔气。</param>
    /// <returns>坐标存在且已绑定魔气时返回 true。</returns>
    public bool TryGetMarkedKind(MapCoord coord, out PanKuDemonQiKind kind)
    {
        MarkedGate? gate = ParseMarkedGates(MarkedCoordsCsv).FirstOrDefault(marked => IsSameCoord(marked.Coord, coord));
        if (gate?.Kind != null)
        {
            kind = gate.Kind.Value;
            return true;
        }

        kind = default;
        return false;
    }

    /// <summary>
    /// 清理待领取奖励记录并刷新计数。
    /// </summary>
    public void ClearPendingReward()
    {
        PendingRewardKind = string.Empty;
        PendingRewardCoord = string.Empty;
        InvokeDisplayAmountChanged();
    }

    /// <summary>
    /// 注入潘库三选一奖励；若奖励页 SL 后重进同节点，则恢复原本待领取魔气。
    /// </summary>
    private bool TryInjectPanKuReward(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player != Owner || rewards.Any(ContainsPanKuReward))
        {
            return false;
        }

        MapCoord? coord = player.RunState.CurrentMapCoord;
        if (coord == null || room is not CombatRoom)
        {
            return false;
        }

        PanKuDemonQiKind? pendingKind = TryGetPendingKindFor(coord.Value);
        bool hasMarkedCoord = HasMarkedCoord(coord.Value);
        PanKuDemonQiKind? kind = pendingKind ?? TryGetMarkedKindFor(coord.Value) ?? ChooseMissingKind(player);
        if (kind == null || pendingKind == null && !hasMarkedCoord)
        {
            return false;
        }

        AddRewardSet(player, rewards, kind.Value);
        if (pendingKind == null)
        {
            RemoveMarkedCoord(coord.Value);
            PendingRewardKind = kind.Value.ToString();
            PendingRewardCoord = FormatCoord(coord.Value);
        }

        Flash();
        MainFile.Logger.Info($"【潘库宝盒】已注入恶魔门奖励：魔气={PanKuDemonQiHelper.GetShortName(kind.Value)}，坐标={PendingRewardCoord}。");
        return true;
    }

    /// <summary>
    /// 向奖励列表追加互斥三选一。
    /// </summary>
    private static void AddRewardSet(Player player, List<Reward> rewards, PanKuDemonQiKind kind)
    {
        rewards.Add(new PanKuChoiceReward(kind, player));
    }

    /// <summary>
    /// 判断奖励或互斥奖励内是否已经有潘库奖励。
    /// </summary>
    private static bool ContainsPanKuReward(Reward reward)
    {
        return reward is PanKuRewardBase
            || reward is LinkedRewardSet linkedRewardSet && linkedRewardSet.Rewards.Any(child => child is PanKuRewardBase);
    }

    /// <summary>
    /// 如果当前节点有待领取奖励记录，则恢复对应魔气。
    /// </summary>
    private PanKuDemonQiKind? TryGetPendingKindFor(MapCoord coord)
    {
        if (PendingRewardCoord != FormatCoord(coord))
        {
            return null;
        }

        return Enum.TryParse(PendingRewardKind, out PanKuDemonQiKind kind) ? kind : null;
    }

    /// <summary>
    /// 如果当前节点已有恶魔门绑定，则返回该门固定魔气。
    /// </summary>
    /// <param name="coord">地图坐标。</param>
    /// <returns>绑定魔气；没有绑定时返回 null。</returns>
    private PanKuDemonQiKind? TryGetMarkedKindFor(MapCoord coord)
    {
        return TryGetMarkedKind(coord, out PanKuDemonQiKind kind) ? kind : null;
    }

    /// <summary>
    /// 从缺失魔气里稳定选择一个奖励。
    /// </summary>
    private PanKuDemonQiKind? ChooseMissingKind(Player player)
    {
        HashSet<PanKuDemonQiKind> owned = PanKuDemonQiHelper.CollectOwnedKinds(player, this);
        return PanKuDemonQiHelper.AllKinds
            .Where(kind => !owned.Contains(kind))
            .OrderBy(kind => GetStableKindScore(player, kind))
            .Cast<PanKuDemonQiKind?>()
            .FirstOrDefault();
    }

    /// <summary>
    /// 恢复当前地图已有标记；如果检测到进入新地图，则只生成一次新地狱之门。
    /// </summary>
    /// <param name="map">当前地图。</param>
    private void RestoreOrGenerateMapMarks(ActMap? map)
    {
        if (Owner == null || map == null)
        {
            return;
        }

        string mapKey = GetCurrentMapKey(Owner);
        if (GeneratedMapKey == mapKey)
        {
            RestoreExistingMapMarks(map);
            return;
        }

        MarkedCoordsCsv = string.Empty;
        PendingRewardKind = string.Empty;
        PendingRewardCoord = string.Empty;
        GeneratedMapKey = mapKey;
        MarkMapNodes(map);
        MainFile.Logger.Info($"【潘库宝盒】检测到新地图，已尝试生成本层地狱之门：地图={mapKey}。");
    }

    /// <summary>
    /// 获得潘库宝盒时，在当前地图立刻生成一次地狱之门。
    /// </summary>
    /// <param name="map">当前地图。</param>
    private void GenerateMapMarksOnce(ActMap? map)
    {
        if (Owner == null || map == null)
        {
            return;
        }

        string mapKey = GetCurrentMapKey(Owner);
        if (GeneratedMapKey == mapKey)
        {
            RestoreExistingMapMarks(map);
            return;
        }

        MarkedCoordsCsv = string.Empty;
        PendingRewardKind = string.Empty;
        PendingRewardCoord = string.Empty;
        GeneratedMapKey = mapKey;
        MarkMapNodes(map);
        MainFile.Logger.Info($"【潘库宝盒】获得遗物后生成本层地狱之门：地图={mapKey}。");
    }

    /// <summary>
    /// 只恢复已经存在的地狱之门视觉，不新增节点。
    /// </summary>
    /// <param name="map">当前地图。</param>
    private void RestoreExistingMapMarks(ActMap map)
    {
        if (Owner == null)
        {
            return;
        }

        if (CountMissingKinds(Owner) == 0)
        {
            if (!string.IsNullOrEmpty(MarkedCoordsCsv))
            {
                MarkedCoordsCsv = string.Empty;
                MainFile.Logger.Info("【潘库宝盒】八魔气已集齐，清理未来地狱之门标记。");
            }

            return;
        }

        EnsureMarkedGateKinds(Owner, map);
        PanKuDemonQiHelper.RestoreMapQuestMarks(this, map);
    }

    /// <summary>
    /// 生成当前地图上后续节点为恶魔门。
    /// </summary>
    private void MarkMapNodes(ActMap? map)
    {
        if (Owner == null || map == null)
        {
            return;
        }

        int needed = CountMissingKinds(Owner);
        if (needed <= 0)
        {
            MarkedCoordsCsv = string.Empty;
            MainFile.Logger.Info("【潘库宝盒】八魔气已集齐，本层不生成地狱之门。");
            return;
        }

        Queue<PanKuDemonQiKind> newKinds = new(ChooseKindsForNewMarkedPoints(Owner, map, needed));
        if (newKinds.Count == 0)
        {
            MainFile.Logger.Info("【潘库宝盒】没有可绑定的缺失魔气，跳过新增恶魔门。");
            return;
        }

        int currentRow = Owner.RunState.CurrentMapCoord?.row ?? -1;
        List<MapPoint> elites = [];
        List<MapPoint> monsters = [];
        foreach (MapPoint point in map.GetAllMapPoints())
        {
            if (point.coord.row <= currentRow || HasMarkedCoord(point.coord) || !point.CanBeModified)
            {
                continue;
            }

            if (point.PointType == MapPointType.Elite)
            {
                elites.Add(point);
            }
            else if (point.PointType == MapPointType.Monster)
            {
                monsters.Add(point);
            }
        }

        int marked = 0;
        foreach (MapPoint point in elites.OrderBy(point => GetStablePointScore(Owner, point)))
        {
            if (marked >= needed) break;
            if (!TryAddMarkedPoint(point, newKinds)) break;
            marked++;
        }

        foreach (MapPoint point in monsters.OrderBy(point => GetStablePointScore(Owner, point)))
        {
            if (marked >= needed) break;
            if (newKinds.Count == 0) break;
            point.PointType = MapPointType.Elite;
            if (!TryAddMarkedPoint(point, newKinds)) break;
            marked++;
        }
    }

    /// <summary>
    /// 添加恶魔门节点，并使用塔2任务标记高亮。
    /// </summary>
    private bool TryAddMarkedPoint(MapPoint point, Queue<PanKuDemonQiKind> newKinds)
    {
        List<MarkedGate> gates = ParseMarkedGates(MarkedCoordsCsv);
        if (gates.Any(gate => IsSameCoord(gate.Coord, point.coord)))
        {
            if (!point.Quests.Contains(this))
            {
                point.AddQuest(this);
            }

            MainFile.Logger.Info($"【潘库宝盒】恶魔之门已存在，跳过重复标记：列={point.coord.col}，行={point.coord.row}。");
            return true;
        }

        if (!newKinds.TryDequeue(out PanKuDemonQiKind kind))
        {
            return false;
        }

        gates.Add(new MarkedGate { Coord = point.coord, Kind = kind });
        MarkedCoordsCsv = FormatMarkedGates(gates);
        if (!point.Quests.Contains(this))
        {
            point.AddQuest(this);
        }

            MainFile.Logger.Info($"【潘库宝盒】标记恶魔之门：列={point.coord.col}，行={point.coord.row}，卦象={PanKuDemonQiHelper.GetBaguaSymbol(kind)}，魔气={PanKuDemonQiHelper.GetShortName(kind)}。");
        return true;
    }

    /// <summary>
    /// 获取当前地图标识；同一幕内保持不变，进入下一幕后变化。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>地图标识。</returns>
    private static string GetCurrentMapKey(Player player)
    {
        return $"Act:{player.RunState.CurrentActIndex}";
    }

    /// <summary>
    /// 移除已结算恶魔门坐标。
    /// </summary>
    private void RemoveMarkedCoord(MapCoord coord)
    {
        List<MarkedGate> gates = ParseMarkedGates(MarkedCoordsCsv)
            .Where(marked => !IsSameCoord(marked.Coord, coord))
            .ToList();
        MarkedCoordsCsv = FormatMarkedGates(gates);
    }

    /// <summary>
    /// 统计未来仍有效的恶魔门坐标。
    /// </summary>
    private int CountFutureMarkedCoords(Player player, ActMap map)
    {
        int currentRow = player.RunState.CurrentMapCoord?.row ?? -1;
        return map.GetAllMapPoints()
            .Count(point => point.coord.row > currentRow && HasMarkedCoord(point.coord));
    }

    /// <summary>
    /// 给旧存档里只有坐标、没有魔气的恶魔门补上固定魔气。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="map">当前地图。</param>
    private void EnsureMarkedGateKinds(Player player, ActMap map)
    {
        List<MarkedGate> gates = ParseMarkedGates(MarkedCoordsCsv);
        if (gates.Count == 0 || gates.All(gate => gate.Kind != null))
        {
            return;
        }

        HashSet<PanKuDemonQiKind> owned = PanKuDemonQiHelper.CollectOwnedKinds(player, this);
        HashSet<PanKuDemonQiKind> used = gates
            .Where(gate => gate.Kind != null)
            .Select(gate => gate.Kind!.Value)
            .ToHashSet();
        bool changed = false;
        foreach (MarkedGate gate in gates.Where(gate => gate.Kind == null && IsFuturePoint(player, map, gate.Coord)))
        {
            PanKuDemonQiKind? kind = PanKuDemonQiHelper.AllKinds
                .Where(candidate => !owned.Contains(candidate) && !used.Contains(candidate))
                .OrderBy(candidate => GetStableKindScore(player, candidate))
                .Cast<PanKuDemonQiKind?>()
                .FirstOrDefault();
            if (kind == null)
            {
                MainFile.Logger.Info($"【潘库宝盒】旧恶魔门缺少可绑定魔气：坐标={FormatCoord(gate.Coord)}。");
                continue;
            }

            gate.Kind = kind.Value;
            used.Add(kind.Value);
            changed = true;
            MainFile.Logger.Info($"【潘库宝盒】为旧恶魔门补绑定魔气：坐标={FormatCoord(gate.Coord)}，卦象={PanKuDemonQiHelper.GetBaguaSymbol(kind.Value)}，魔气={PanKuDemonQiHelper.GetShortName(kind.Value)}。");
        }

        if (changed)
        {
            MarkedCoordsCsv = FormatMarkedGates(gates);
        }
    }

    /// <summary>
    /// 为即将新增的恶魔门挑选尚未拥有、也未绑定到未来恶魔门的魔气。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="map">当前地图。</param>
    /// <param name="count">需要数量。</param>
    /// <returns>待绑定魔气列表。</returns>
    private List<PanKuDemonQiKind> ChooseKindsForNewMarkedPoints(Player player, ActMap map, int count)
    {
        HashSet<PanKuDemonQiKind> owned = PanKuDemonQiHelper.CollectOwnedKinds(player, this);
        HashSet<PanKuDemonQiKind> marked = GetFutureMarkedKinds(player, map);
        return PanKuDemonQiHelper.AllKinds
            .Where(kind => !owned.Contains(kind) && !marked.Contains(kind))
            .OrderBy(kind => GetStableKindScore(player, kind))
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// 获取未来恶魔门已经绑定的魔气，用于避免重复绑定。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="map">当前地图。</param>
    /// <returns>未来恶魔门绑定魔气集合。</returns>
    private HashSet<PanKuDemonQiKind> GetFutureMarkedKinds(Player player, ActMap map)
    {
        return ParseMarkedGates(MarkedCoordsCsv)
            .Where(gate => gate.Kind != null && IsFuturePoint(player, map, gate.Coord))
            .Select(gate => gate.Kind!.Value)
            .ToHashSet();
    }

    /// <summary>
    /// 判断坐标是否仍是当前地图上的未来节点。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="map">当前地图。</param>
    /// <param name="coord">待检查坐标。</param>
    /// <returns>仍在未来地图中时返回 true。</returns>
    private static bool IsFuturePoint(Player player, ActMap map, MapCoord coord)
    {
        int currentRow = player.RunState.CurrentMapCoord?.row ?? -1;
        return coord.row > currentRow && map.GetAllMapPoints().Any(point => IsSameCoord(point.coord, coord));
    }

    /// <summary>
    /// 统计缺失魔气数量。
    /// </summary>
    private int CountMissingKinds(Player? player)
    {
        return Math.Max(0, PanKuDemonQiHelper.AllKinds.Length - PanKuDemonQiHelper.CollectOwnedKinds(player, this).Count);
    }

    /// <summary>
    /// 判断寄宿魔气中是否存在当前可附魔目标。
    /// </summary>
    private bool HasAnyStoredQiTarget(Player player)
    {
        return GetStoredKinds().Any(kind => PanKuDemonQiHelper.GetEnchantTargets(player, kind).Count > 0);
    }

    /// <summary>
    /// 格式化寄宿魔气列表，用于遗物描述。
    /// </summary>
    /// <returns>寄宿魔气中文短名列表。</returns>
    private string FormatStoredKindsForDisplay()
    {
        List<PanKuDemonQiKind> kinds = GetStoredKinds();
        if (kinds.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("、", kinds.Select(PanKuDemonQiHelper.GetDisplayName));
    }

    /// <summary>
    /// 对比上一轮牌组/附魔魔气快照，将丢失的魔气回收到宝盒里。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    private void RecycleLostQiFromSnapshot(Player? player)
    {
        if (Owner == null || player?.Deck?.Cards == null)
        {
            return;
        }

        if (player.NetId != Owner.NetId)
        {
            return;
        }

        List<PanKuDemonQiKind> previousKinds = ParseKinds(KnownActiveKindsCsv);
        List<PanKuDemonQiKind> currentKinds = CollectActiveKinds(player);
        foreach (PanKuDemonQiKind previousKind in previousKinds)
        {
            if (currentKinds.Contains(previousKind) || GetStoredKinds().Contains(previousKind))
            {
                continue;
            }

            AddStoredKind(previousKind);
            MainFile.Logger.Info($"【潘库宝盒】检测到魔气来源消失，已回收到宝盒：玩家={Owner.NetId}，魔气={PanKuDemonQiHelper.GetShortName(previousKind)}。");
        }

        KnownActiveKindsCsv = FormatKinds(currentKinds);
    }

    /// <summary>
    /// 只统计牌组中的恶魔卡和附魔魔气，不包含宝盒寄宿魔气。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>牌组/附魔魔气集合。</returns>
    private static List<PanKuDemonQiKind> CollectActiveKinds(Player player)
    {
        List<PanKuDemonQiKind> kinds = [];
        foreach (CardModel card in player.Deck.Cards)
        {
            if (PanKuDemonQiHelper.TryGetKindFromCard(card, out PanKuDemonQiKind cardKind) && !kinds.Contains(cardKind))
            {
                kinds.Add(cardKind);
            }

            if (PanKuDemonQiHelper.TryGetKindFromEnchantment(card.Enchantment, out PanKuDemonQiKind enchantKind) && !kinds.Contains(enchantKind))
            {
                kinds.Add(enchantKind);
            }
        }

        return kinds;
    }

    /// <summary>
    /// 为魔气奖励计算稳定随机分数。
    /// </summary>
    private static uint GetStableKindScore(Player player, PanKuDemonQiKind kind)
    {
        uint hash = 2166136261u;
        AddStableHash(ref hash, player.RunState.Rng.StringSeed);
        AddStableHash(ref hash, player.RunState.TotalFloor.ToString());
        AddStableHash(ref hash, player.RunState.CurrentMapCoord?.col.ToString() ?? "NO_COL");
        AddStableHash(ref hash, player.RunState.CurrentMapCoord?.row.ToString() ?? "NO_ROW");
        AddStableHash(ref hash, kind.ToString());
        return hash;
    }

    /// <summary>
    /// 为地图节点计算稳定随机分数。
    /// </summary>
    private static uint GetStablePointScore(Player player, MapPoint point)
    {
        uint hash = 2166136261u;
        AddStableHash(ref hash, player.RunState.Rng.StringSeed);
        AddStableHash(ref hash, player.RunState.CurrentActIndex.ToString());
        AddStableHash(ref hash, point.coord.col.ToString());
        AddStableHash(ref hash, point.coord.row.ToString());
        return hash;
    }

    /// <summary>
    /// 使用固定 FNV-1a 算法追加字符串。
    /// </summary>
    private static void AddStableHash(ref uint hash, string value)
    {
        foreach (char character in value)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        hash ^= '|';
        hash *= 16777619u;
    }

    /// <summary>
    /// 解析魔气 CSV。
    /// </summary>
    private static List<PanKuDemonQiKind> ParseKinds(string csv)
    {
        return csv.Split(CoordSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(text => Enum.TryParse(text, out PanKuDemonQiKind _))
            .Select(Enum.Parse<PanKuDemonQiKind>)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// 格式化魔气 CSV。
    /// </summary>
    private static string FormatKinds(IEnumerable<PanKuDemonQiKind> kinds)
    {
        return string.Join(CoordSeparator, kinds.Distinct());
    }

    /// <summary>
    /// 解析恶魔门 CSV；兼容旧存档的“列:行”格式和新版“列:行:魔气”格式。
    /// </summary>
    private static List<MarkedGate> ParseMarkedGates(string csv)
    {
        List<MarkedGate> result = [];
        foreach (string part in csv.Split(CoordSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pieces = part.Split(FieldSeparator);
            if (pieces.Length == 2 && int.TryParse(pieces[0], out int col) && int.TryParse(pieces[1], out int row))
            {
                result.Add(new MarkedGate { Coord = new MapCoord { col = col, row = row } });
                continue;
            }

            if (pieces.Length == 3
                && int.TryParse(pieces[0], out col)
                && int.TryParse(pieces[1], out row)
                && Enum.TryParse(pieces[2], out PanKuDemonQiKind kind))
            {
                result.Add(new MarkedGate { Coord = new MapCoord { col = col, row = row }, Kind = kind });
            }
        }

        return result;
    }

    /// <summary>
    /// 格式化恶魔门 CSV。
    /// </summary>
    private static string FormatMarkedGates(IEnumerable<MarkedGate> gates)
    {
        return string.Join(CoordSeparator, gates
            .GroupBy(gate => FormatCoord(gate.Coord))
            .Select(group => FormatMarkedGate(group.First())));
    }

    /// <summary>
    /// 格式化单个恶魔门。
    /// </summary>
    private static string FormatMarkedGate(MarkedGate gate)
    {
        return gate.Kind == null
            ? FormatCoord(gate.Coord)
            : $"{FormatCoord(gate.Coord)}{FieldSeparator}{gate.Kind.Value}";
    }

    /// <summary>
    /// 格式化单个地图坐标。
    /// </summary>
    private static string FormatCoord(MapCoord coord)
    {
        return $"{coord.col}{FieldSeparator}{coord.row}";
    }

    /// <summary>
    /// 判断两个地图坐标是否相同。
    /// </summary>
    private static bool IsSameCoord(MapCoord left, MapCoord right)
    {
        return left.col == right.col && left.row == right.row;
    }
}

/// <summary>
/// 潘库宝盒的营火选项：消耗寄宿魔气，给卡组中的可用牌附魔。
/// </summary>
public class PanKuEnchantRestSiteOption : RestSiteOption
{
    /// <summary>
    /// CardModel._titleLocString 字段的反射缓存，用于把通用预览卡标题改成具体魔气名称。
    /// </summary>
    private static readonly FieldInfo? TitleLocStringField =
        typeof(CardModel).GetField("_titleLocString", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 自定义休息处选项 ID。
    /// </summary>
    public override string OptionId => "PAN_KU_ENCHANT";

    /// <summary>
    /// 休息处显示说明。
    /// </summary>
    public override LocString Description => new("rest_site_ui", "OPTION_PAN_KU_ENCHANT.description");

    /// <summary>
    /// 复用默认休息处选项图，避免缺图阻塞功能。
    /// </summary>
    public override IEnumerable<string> AssetPaths
    {
        get
        {
            yield return "res://images/ui/rest_site/option_pan_ku_enchant.png";
        }
    }

    /// <summary>
    /// 是否允许选择潘库附魔选项。
    /// </summary>
    public override bool IsEnabled => Owner.GetRelic<PanKuBox>()?.GetStoredKinds().Count > 0;

    /// <summary>
    /// 构造潘库附魔休息处选项。
    /// </summary>
    /// <param name="owner">当前玩家。</param>
    public PanKuEnchantRestSiteOption(Player owner) : base(owner)
    {
    }

    /// <summary>
    /// 选择寄宿魔气，再选择卡组目标，成功后消耗一次休息机会。
    /// </summary>
    public override async Task<bool> OnSelect()
    {
        if (Owner == null)
        {
            MainFile.Logger.Info("【联机异常兜底】【潘库宝盒】休息处附魔取消：玩家为空。");
            return false;
        }

        if (!MonkeyTalisman.IsLocalPlayer(Owner))
        {
            MainFile.Logger.Info($"【联机同步】【潘库宝盒】跟随远端休息处附魔选项：玩家={Owner.NetId}，本端跳过选牌UI，等待遗物同步消息重放后再关闭休息处。");
            return await RelicInteractionSyncService.WaitForRemoteRestSiteInteraction(Owner, RelicInteractionType.PanKuRestEnchant, "潘库宝盒休息处附魔");
        }

        PanKuBox? box = Owner.GetRelic<PanKuBox>();
        if (box == null)
        {
            return await CancelLocalSelection("玩家没有潘库宝盒");
        }

        while (true)
        {
            Dictionary<CardModel, PanKuDemonQiKind> qiMap = [];
            List<CardModel> qiChoices = [];
            foreach (PanKuDemonQiKind storedKind in box.GetStoredKinds())
            {
                CardModel storedQiCard = Owner.RunState.CreateCard<PanKuEnchantPreviewCard>(Owner);
                if (storedQiCard is PanKuEnchantPreviewCard previewCard)
                {
                    previewCard.Kind = storedKind;
                }

                OverrideStoredQiPreviewTitle(storedQiCard, storedKind);
                qiMap[storedQiCard] = storedKind;
                qiChoices.Add(storedQiCard);
            }

            if (qiChoices.Count == 0)
            {
                return await CancelLocalSelection("没有可用寄宿魔气");
            }

            CardModel cancelCard = Owner.RunState.CreateCard<PanKuCancelPreviewCard>(Owner);
            qiChoices.Add(cancelCard);
            CardSelectorPrefs qiPrefs = new(new LocString("rest_site_ui", "OPTION_PAN_KU_ENCHANT.selectQi"), 1, 1)
            {
                Cancelable = true,
                RequireManualConfirmation = true
            };
            CardModel? qiCard = await PanKuRewardChoiceSelector.SelectOneCard(qiChoices, qiPrefs);
            if (qiCard == null || qiCard is PanKuCancelPreviewCard)
            {
                MainFile.Logger.Info("【潘库宝盒】玩家取消休息处附魔，返回休息处。");
                return await CancelLocalSelection("玩家取消魔气选择");
            }

            if (!qiMap.TryGetValue(qiCard, out PanKuDemonQiKind kind)
                && !PanKuDemonQiHelper.TryGetKindFromCard(qiCard, out kind))
            {
                MainFile.Logger.Info("【潘库宝盒】休息处附魔选择到未知魔气，返回休息处。");
                return await CancelLocalSelection("选择到未知魔气");
            }

            if (PanKuDemonQiHelper.GetEnchantTargets(Owner, kind).Count == 0)
            {
                MainFile.Logger.Info($"【潘库宝盒】休息处附魔无可用目标，重新选择魔气：魔气={PanKuDemonQiHelper.GetShortName(kind)}。");
                continue;
            }

            LocString targetPrompt = new("rest_site_ui", "OPTION_PAN_KU_ENCHANT.selectTarget");
            targetPrompt.Add("EnchantTargetType", PanKuDemonQiHelper.GetEnchantTargetTypeText(kind));
            CardSelectorPrefs targetPrefs = new(targetPrompt, 1)
            {
                Cancelable = true,
                RequireManualConfirmation = true
            };
            CardModel? target = await PanKuRewardChoiceSelector.SelectOneCard(PanKuDemonQiHelper.GetEnchantTargets(Owner, kind), targetPrefs);
            if (target == null)
            {
                MainFile.Logger.Info($"【潘库宝盒】玩家取消休息处附魔目标选择，返回魔气选择：魔气={PanKuDemonQiHelper.GetShortName(kind)}。");
                continue;
            }

            int targetDeckIndex = DeckSyncHelper.GetDeckIndex(Owner, target);
            if (targetDeckIndex < 0)
            {
                MainFile.Logger.Info($"【联机异常兜底】【潘库宝盒】休息处附魔取消：玩家={Owner.NetId}，原因=目标牌不在牌组，目标={target.Id.Entry}。");
                return await CancelLocalSelection("目标牌不在牌组");
            }

            PanKuRestEnchantPayload payload = new(targetDeckIndex, target.Id.Entry, target.IsUpgraded, kind);
            return await RelicInteractionSyncService.RequestPanKuRestEnchant(box, payload);
        }
    }

    /// <summary>
    /// 本地玩家取消休息处附魔时，同步通知远端放弃等待。
    /// </summary>
    /// <param name="reason">取消原因。</param>
    /// <returns>固定返回 false，表示不消耗休息处选项。</returns>
    private async Task<bool> CancelLocalSelection(string reason)
    {
        if (Owner != null)
        {
            await RelicInteractionSyncService.NotifyRestSiteInteractionCanceled(Owner, RelicInteractionType.PanKuRestEnchant, TalismanRelicType.None, $"潘库宝盒休息处附魔取消：{reason}");
        }

        return false;
    }

    /// <summary>
    /// 将休息处寄宿魔气预览卡标题改成具体魔气名称，避免显示成通用的“立刻附魔”。
    /// </summary>
    /// <param name="card">预览卡。</param>
    /// <param name="kind">魔气类型。</param>
    private static void OverrideStoredQiPreviewTitle(CardModel card, PanKuDemonQiKind kind)
    {
        if (TitleLocStringField == null)
        {
            MainFile.Logger.Info("【潘库宝盒】反射获取 _titleLocString 字段失败，休息处魔气预览卡标题无法覆盖。");
            return;
        }

        TitleLocStringField.SetValue(card, PanKuDemonQiHelper.GetPreviewTitleLocString(kind));
    }
}
