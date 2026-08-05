using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Orbs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

/// <summary>
/// 面具栏显示服务：独立绘制圣主面具球，避免面具状态进入原生充能球轨道。
/// </summary>
public static class MaskOrbDisplayService
{
    /// <summary>
    /// 面具栏根节点名称，用于重复刷新时找到并复用旧节点。
    /// </summary>
    private const string DisplayRootName = "ShengZhuMaskOrbDisplayRoot";

    /// <summary>
    /// 面具圆弧中心相对角色碰撞框顶部的下移距离。
    /// </summary>
    private static readonly Vector2 ArcCenterOffset = Vector2.Down * 210f;

    /// <summary>
    /// 面具圆弧横向半径；决定2个面具和10个面具占用的固定总宽度。
    /// </summary>
    private const float ArcRadiusX = 205f;

    /// <summary>
    /// 面具圆弧纵向半径；决定中间面具向上拱起的高度。
    /// </summary>
    private const float ArcRadiusY = 205f;

    /// <summary>
    /// 面具圆弧起始角度，左侧面具从角色左肩外侧开始。
    /// </summary>
    private const float ArcStartDegrees = 180f;

    /// <summary>
    /// 面具圆弧结束角度，右侧面具落到角色右肩外侧。
    /// </summary>
    private const float ArcEndDegrees = 360f;

    /// <summary>
    /// 记录每名玩家上次刷新出的面具栏状态；状态完全一致时跳过整条节点重建。
    /// </summary>
    private static readonly Dictionary<Player, MaskDisplaySnapshot> LastSnapshots = new();

    /// <summary>
    /// 刷新指定玩家的独立面具栏。
    /// </summary>
    /// <param name="player">需要刷新面具栏的玩家。</param>
    /// <param name="capacity">当前面具容量。</param>
    /// <param name="maskStates">当前应该显示的面具球状态。</param>
    public static void Refresh(Player? player, int capacity, IReadOnlyList<MaskOrbDisplayState> maskStates)
    {
        NCreature? creatureNode = GetCreatureNode(player);
        if (player == null || creatureNode == null)
        {
            MainFile.Logger.Info("【独立面具栏】未找到玩家或战斗节点，跳过刷新。");
            return;
        }

        capacity = Math.Max(0, capacity);
        IReadOnlyList<MaskOrbDisplayState> visibleStates = maskStates.Count <= capacity
            ? maskStates
            : maskStates.Take(capacity).ToList();
        if (capacity == 0)
        {
            Clear(player);
            MainFile.Logger.Info("【独立面具栏】容量为0，已清理面具栏。");
            return;
        }

        Control? existingRoot = FindDisplayRoot(creatureNode);
        if (existingRoot != null && IsSameSnapshot(player, capacity, visibleStates))
        {
            RefreshRootTransform(creatureNode, existingRoot);
            return;
        }

        MaskDisplaySnapshot? oldSnapshot = LastSnapshots.TryGetValue(player, out MaskDisplaySnapshot? snapshot) ? snapshot : null;
        Control root = GetOrCreateDisplayRoot(creatureNode);
        RefreshRootTransform(creatureNode, root);

        RefreshSlots(root, player, capacity, visibleStates, oldSnapshot);
        LastSnapshots[player] = MaskDisplaySnapshot.Create(capacity, visibleStates);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【独立面具栏】刷新完成：玩家={player.Character.Id.Entry}，容量={capacity}，面具数={visibleStates.Count}，原生充能球容量={player.PlayerCombatState?.OrbQueue.Capacity ?? 0}，面具={string.Join(",", visibleStates.Select(state => state.MaskPowerKey))}");
    }

    /// <summary>
    /// 清理指定玩家的独立面具栏。
    /// </summary>
    /// <param name="player">需要清理面具栏的玩家。</param>
    public static void Clear(Player? player)
    {
        NCreature? creatureNode = GetCreatureNode(player);
        if (creatureNode == null)
        {
            return;
        }

        int removed = RemoveDisplayRoots(creatureNode);
        if (player != null)
        {
            LastSnapshots.Remove(player);
        }

        if (removed > 0)
        {
            MainFile.Logger.Info($"【独立面具栏】清理显示节点：玩家={player?.Character.Id.Entry ?? "未知"}，数量={removed}");
        }
    }

    /// <summary>
    /// 清理当前战斗房间里所有玩家的独立面具栏，防止战斗结束后穿透到奖励或地图层。
    /// </summary>
    public static void ClearAll()
    {
        if (NCombatRoom.Instance == null)
        {
            return;
        }

        int removed = 0;
        foreach (NCreature creatureNode in NCombatRoom.Instance.CreatureNodes)
        {
            removed += RemoveDisplayRoots(creatureNode);
        }

        LastSnapshots.Clear();
        if (removed > 0)
        {
            MainFile.Logger.Info($"【独立面具栏】清理当前战斗房间所有面具栏：数量={removed}");
        }
    }

    /// <summary>
    /// 清理指定战斗节点上的独立面具栏。
    /// </summary>
    /// <param name="creatureNode">需要清理的战斗角色节点。</param>
    public static void Clear(NCreature? creatureNode)
    {
        if (creatureNode == null)
        {
            return;
        }

        int removed = RemoveDisplayRoots(creatureNode);
        if (creatureNode.Entity?.Player != null)
        {
            LastSnapshots.Remove(creatureNode.Entity.Player);
        }

        if (removed > 0)
        {
            MainFile.Logger.Info($"【独立面具栏】清理战斗节点面具栏：角色={creatureNode.Entity?.LogName ?? "未知"}，数量={removed}");
        }
    }

    /// <summary>
    /// 创建一个已装载面具的槽位节点。
    /// </summary>
    /// <param name="player">面具所属玩家。</param>
    /// <param name="state">面具显示状态。</param>
    /// <param name="isLocal">是否为本地玩家。</param>
    /// <returns>充能球风格的面具显示节点。</returns>
    private static NOrb CreateFilledSlot(Player player, MaskOrbDisplayState state, bool isLocal)
    {
        var maskOrb = (MaskOrb)ModelDb.Orb<MaskOrb>().ToMutable(state.MaskAmount);
        maskOrb.MaskPowerKey = state.MaskPowerKey;
        maskOrb.MaskName = state.MaskName;
        maskOrb.MaskAmount = state.MaskAmount;
        maskOrb.Owner = player;
        NOrb slot = NOrb.Create(isLocal, maskOrb);
        NormalizeFilledSlotNativeUi(slot);
        return slot;
    }

    /// <summary>
    /// 整理已装载面具槽位的原生UI，只保留面具层数数字。
    /// </summary>
    /// <param name="slot">已装载面具的槽位节点。</param>
    private static void NormalizeFilledSlotNativeUi(NOrb slot)
    {
        TextureRect? outline = slot.GetNodeOrNull<TextureRect>("%Outline");
        if (outline != null)
        {
            outline.Visible = false;
        }

        Control? passiveAmount = slot.GetNodeOrNull<Control>("%PassiveAmount");
        if (passiveAmount != null)
        {
            passiveAmount.Visible = true;
        }

        Control? evokeAmount = slot.GetNodeOrNull<Control>("%EvokeAmount");
        if (evokeAmount != null)
        {
            evokeAmount.Visible = false;
        }
    }

    /// <summary>
    /// 获取或创建面具栏根节点；如果检测到多个历史根节点，只保留第一个。
    /// </summary>
    /// <param name="creatureNode">玩家战斗节点。</param>
    /// <returns>可复用的面具栏根节点。</returns>
    private static Control GetOrCreateDisplayRoot(NCreature creatureNode)
    {
        Control? existingRoot = null;
        List<Node> duplicateRoots = [];
        foreach (Node child in creatureNode.GetChildren())
        {
            if (child.Name != DisplayRootName)
            {
                continue;
            }

            if (existingRoot == null && child is Control controlRoot)
            {
                existingRoot = controlRoot;
                continue;
            }

            duplicateRoots.Add(child);
        }

        foreach (Node duplicateRoot in duplicateRoots)
        {
            creatureNode.RemoveChild(duplicateRoot);
            duplicateRoot.QueueFree();
        }

        if (existingRoot != null)
        {
            CopyNativeOrbLayer(creatureNode, existingRoot);
            return existingRoot;
        }

        Control root = new()
        {
            Name = DisplayRootName,
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        CopyNativeOrbLayer(creatureNode, root);
        creatureNode.AddChild(root);
        return root;
    }

    /// <summary>
    /// 刷新面具槽位节点，只替换状态变化的槽位。
    /// </summary>
    /// <param name="root">面具栏根节点。</param>
    /// <param name="player">当前玩家。</param>
    /// <param name="capacity">显示容量。</param>
    /// <param name="visibleStates">当前显示状态。</param>
    /// <param name="oldSnapshot">上一次显示快照。</param>
    private static void RefreshSlots(Control root, Player player, int capacity, IReadOnlyList<MaskOrbDisplayState> visibleStates, MaskDisplaySnapshot? oldSnapshot)
    {
        bool isLocal = LocalContext.IsMe(player);
        List<NOrb> slots = GetSlotNodes(root);
        TrimExtraSlots(root, slots, capacity);

        for (int index = 0; index < capacity; index++)
        {
            NOrb slot;
            if (index < slots.Count && IsSameSlot(oldSnapshot, index, visibleStates))
            {
                slot = slots[index];
            }
            else
            {
                slot = ReplaceSlot(root, slots, player, index, visibleStates, isLocal);
            }

            if (index < visibleStates.Count)
            {
                NormalizeFilledSlotNativeUi(slot);
            }

            slot.Position = ResolveSlotPosition(index, capacity, isLocal);
        }
    }

    /// <summary>
    /// 获取当前根节点上的面具槽位。
    /// </summary>
    /// <param name="root">面具栏根节点。</param>
    /// <returns>槽位列表。</returns>
    private static List<NOrb> GetSlotNodes(Control root)
    {
        List<NOrb> slots = [];
        foreach (Node child in root.GetChildren())
        {
            if (child is NOrb slot)
            {
                slots.Add(slot);
            }
        }

        return slots;
    }

    /// <summary>
    /// 删除超出当前容量的旧槽位。
    /// </summary>
    /// <param name="root">面具栏根节点。</param>
    /// <param name="slots">槽位列表。</param>
    /// <param name="capacity">当前容量。</param>
    private static void TrimExtraSlots(Control root, List<NOrb> slots, int capacity)
    {
        for (int index = slots.Count - 1; index >= capacity; index--)
        {
            NOrb slot = slots[index];
            root.RemoveChild(slot);
            slot.QueueFree();
            slots.RemoveAt(index);
        }
    }

    /// <summary>
    /// 替换指定位置的面具槽位。
    /// </summary>
    /// <param name="root">面具栏根节点。</param>
    /// <param name="slots">槽位列表。</param>
    /// <param name="player">当前玩家。</param>
    /// <param name="index">槽位位置。</param>
    /// <param name="visibleStates">当前显示状态。</param>
    /// <param name="isLocal">是否为本地玩家。</param>
    /// <returns>新槽位节点。</returns>
    private static NOrb ReplaceSlot(Control root, List<NOrb> slots, Player player, int index, IReadOnlyList<MaskOrbDisplayState> visibleStates, bool isLocal)
    {
        if (index < slots.Count)
        {
            NOrb oldSlot = slots[index];
            root.RemoveChild(oldSlot);
            oldSlot.QueueFree();
            slots.RemoveAt(index);
        }

        NOrb newSlot = CreateSlot(player, index, visibleStates, isLocal);
        root.AddChild(newSlot);
        root.MoveChild(newSlot, index);
        slots.Insert(index, newSlot);
        return newSlot;
    }

    /// <summary>
    /// 创建指定位置的槽位节点。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="index">槽位位置。</param>
    /// <param name="visibleStates">当前显示状态。</param>
    /// <param name="isLocal">是否为本地玩家。</param>
    /// <returns>槽位节点。</returns>
    private static NOrb CreateSlot(Player player, int index, IReadOnlyList<MaskOrbDisplayState> visibleStates, bool isLocal)
    {
        return index < visibleStates.Count
            ? CreateFilledSlot(player, visibleStates[index], isLocal)
            : NOrb.Create(isLocal);
    }

    /// <summary>
    /// 判断指定位置的槽位状态是否与上次一致。
    /// </summary>
    /// <param name="oldSnapshot">上一次显示快照。</param>
    /// <param name="index">槽位位置。</param>
    /// <param name="visibleStates">当前显示状态。</param>
    /// <returns>一致时返回 true。</returns>
    private static bool IsSameSlot(MaskDisplaySnapshot? oldSnapshot, int index, IReadOnlyList<MaskOrbDisplayState> visibleStates)
    {
        if (oldSnapshot == null || index >= oldSnapshot.Capacity)
        {
            return false;
        }

        bool oldHasMask = index < oldSnapshot.States.Count;
        bool newHasMask = index < visibleStates.Count;
        if (!oldHasMask || !newHasMask)
        {
            return oldHasMask == newHasMask;
        }

        MaskOrbDisplayState oldState = oldSnapshot.States[index];
        MaskOrbDisplayState newState = visibleStates[index];
        return oldState.MaskPowerKey == newState.MaskPowerKey
            && oldState.MaskAmount == newState.MaskAmount
            && oldState.MaskName == newState.MaskName;
    }

    /// <summary>
    /// 刷新面具栏根节点的位置、旋转和缩放，跳过重建时也保持跟随玩家节点。
    /// </summary>
    /// <param name="creatureNode">玩家战斗节点。</param>
    /// <param name="root">面具栏根节点。</param>
    private static void RefreshRootTransform(NCreature creatureNode, Control root)
    {
        root.GlobalPosition = ResolveDisplayPosition(creatureNode);
        root.Rotation = -creatureNode.Rotation;
        root.Scale = ResolveDisplayScale(creatureNode);
    }

    /// <summary>
    /// 复制原生充能球轨道的渲染层级，避免独立面具栏压到暂停菜单或提示层上面。
    /// </summary>
    /// <param name="creatureNode">玩家战斗节点。</param>
    /// <param name="root">独立面具栏根节点。</param>
    private static void CopyNativeOrbLayer(NCreature creatureNode, Control root)
    {
        if (creatureNode.OrbManager == null)
        {
            return;
        }

        root.ZIndex = creatureNode.OrbManager.ZIndex;
        root.ZAsRelative = creatureNode.OrbManager.ZAsRelative;
    }

    /// <summary>
    /// 移除指定战斗节点下的全部面具栏根节点。
    /// </summary>
    /// <param name="creatureNode">玩家战斗节点。</param>
    /// <returns>移除的节点数量。</returns>
    private static int RemoveDisplayRoots(NCreature creatureNode)
    {
        int removed = 0;
        List<Node> displayRoots = [];
        foreach (Node child in creatureNode.GetChildren())
        {
            if (child.Name != DisplayRootName)
            {
                continue;
            }

            displayRoots.Add(child);
        }

        foreach (Node displayRoot in displayRoots)
        {
            creatureNode.RemoveChild(displayRoot);
            displayRoot.QueueFree();
            removed++;
        }

        return removed;
    }

    /// <summary>
    /// 查找当前战斗节点上的面具栏根节点。
    /// </summary>
    /// <param name="creatureNode">玩家战斗节点。</param>
    /// <returns>已有面具栏根节点；不存在时返回 null。</returns>
    private static Control? FindDisplayRoot(NCreature creatureNode)
    {
        foreach (Node child in creatureNode.GetChildren())
        {
            if (child.Name == DisplayRootName && child is Control root)
            {
                return root;
            }
        }

        return null;
    }

    /// <summary>
    /// 判断本次目标状态是否和上次完全一致。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="capacity">当前容量。</param>
    /// <param name="visibleStates">当前显示状态。</param>
    /// <returns>一致时返回 true。</returns>
    private static bool IsSameSnapshot(Player player, int capacity, IReadOnlyList<MaskOrbDisplayState> visibleStates)
    {
        return LastSnapshots.TryGetValue(player, out MaskDisplaySnapshot? snapshot)
            && snapshot.IsSame(capacity, visibleStates);
    }

    /// <summary>
    /// 计算面具圆弧中心位置；按角色碰撞框顶部向下定位，让面具围绕角色上半身。
    /// </summary>
    /// <param name="creatureNode">玩家战斗节点。</param>
    /// <returns>面具栏根节点局部坐标。</returns>
    private static Vector2 ResolveDisplayPosition(NCreature creatureNode)
    {
        return creatureNode.GetTopOfHitbox() + ArcCenterOffset;
    }

    /// <summary>
    /// 计算面具栏缩放，尽量跟随原生充能球栏的视觉尺度。
    /// </summary>
    /// <param name="creatureNode">玩家战斗节点。</param>
    /// <returns>面具栏缩放。</returns>
    private static Vector2 ResolveDisplayScale(NCreature creatureNode)
    {
        return creatureNode.Visuals.Scale.X > 1f
            ? Vector2.One
            : creatureNode.Visuals.Scale.Lerp(Vector2.One, 0.5f);
    }

    /// <summary>
    /// 计算单个面具槽位在椭圆上半弧里的位置；3个面具开始形成明显弧形。
    /// </summary>
    /// <param name="index">槽位序号。</param>
    /// <param name="capacity">总容量。</param>
    /// <param name="isLocal">是否为本地玩家。</param>
    /// <returns>槽位局部坐标。</returns>
    private static Vector2 ResolveSlotPosition(int index, int capacity, bool isLocal)
    {
        if (capacity <= 1)
        {
            return Vector2.Zero;
        }

        float progress = index / (float)(capacity - 1);
        float degrees = Mathf.Lerp(ArcStartDegrees, ArcEndDegrees, progress);
        float radians = float.DegreesToRadians(degrees);
        float x = Mathf.Cos(radians) * ArcRadiusX;
        float y = Mathf.Sin(radians) * ArcRadiusY;
        return new Vector2(x, y);
    }

    /// <summary>
    /// 获取玩家对应的战斗节点。
    /// </summary>
    /// <param name="player">待查找的玩家。</param>
    /// <returns>玩家战斗节点；找不到时返回 null。</returns>
    private static NCreature? GetCreatureNode(Player? player)
    {
        if (player?.Creature == null)
        {
            return null;
        }

        return NCombatRoom.Instance?.GetCreatureNode(player.Creature);
    }
}

/// <summary>
/// 独立面具栏的单个面具显示状态。
/// </summary>
/// <param name="MaskPowerKey">面具能力唯一键。</param>
/// <param name="MaskAmount">面具层数或塔拉施放次数。</param>
/// <param name="MaskName">面具本地化名称键。</param>
public record MaskOrbDisplayState(string MaskPowerKey, int MaskAmount, string MaskName);

/// <summary>
/// 独立面具栏上一次显示状态快照。
/// </summary>
/// <param name="Capacity">显示容量。</param>
/// <param name="States">显示出来的面具状态。</param>
internal sealed record MaskDisplaySnapshot(int Capacity, IReadOnlyList<MaskOrbDisplayState> States)
{
    /// <summary>
    /// 从当前显示状态创建快照。
    /// </summary>
    /// <param name="capacity">显示容量。</param>
    /// <param name="states">显示状态。</param>
    /// <returns>快照。</returns>
    public static MaskDisplaySnapshot Create(int capacity, IReadOnlyList<MaskOrbDisplayState> states)
    {
        return new MaskDisplaySnapshot(capacity, states.ToList());
    }

    /// <summary>
    /// 判断当前快照是否与传入状态一致。
    /// </summary>
    /// <param name="capacity">显示容量。</param>
    /// <param name="states">显示状态。</param>
    /// <returns>完全一致时返回 true。</returns>
    public bool IsSame(int capacity, IReadOnlyList<MaskOrbDisplayState> states)
    {
        if (Capacity != capacity || States.Count != states.Count)
        {
            return false;
        }

        for (int i = 0; i < States.Count; i++)
        {
            MaskOrbDisplayState oldState = States[i];
            MaskOrbDisplayState newState = states[i];
            if (oldState.MaskPowerKey != newState.MaskPowerKey
                || oldState.MaskAmount != newState.MaskAmount
                || oldState.MaskName != newState.MaskName)
            {
                return false;
            }
        }

        return true;
    }
}
