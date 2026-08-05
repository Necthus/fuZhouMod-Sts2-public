using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Orbs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;
using System.Reflection;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 面具管理器：负责维护普通面具FIFO、容量限制、返牌记录，以及独立面具栏显示。
/// 面具栏不再写入原生充能球轨道，避免机器人卡牌把面具当成真实充能球处理。
/// </summary>
public static class MaskManager
{
    /// <summary>
    /// 基础面具容量。
    /// </summary>
    private const int BaseMaskCapacity = 3;

    /// <summary>
    /// 最大面具容量上限。
    /// </summary>
    private const int MaxMaskCapacity = 10;

    /// <summary>
    /// 面具挤出时每层返还的兵团牌数量。
    /// </summary>
    public const int EvictSoldierRefundPerStack = 2;

    /// <summary>
    /// 原生战斗房间节点类型；缓存反射结果，避免面具刷新时重复解析类型。
    /// </summary>
    private static readonly Type? CombatRoomNodeType = typeof(CombatManager).Assembly.GetType("MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom");

    /// <summary>
    /// 原生战斗房间单例属性。
    /// </summary>
    private static readonly PropertyInfo? CombatRoomInstanceProperty = CombatRoomNodeType?.GetProperty("Instance");

    /// <summary>
    /// 原生战斗房间查找角色节点的方法。
    /// </summary>
    private static readonly MethodInfo? GetCreatureNodeMethod = CombatRoomNodeType?.GetMethod("GetCreatureNode");

    /// <summary>
    /// 不同 OrbManager 类型上的 OrbManager 属性缓存。
    /// </summary>
    private static readonly Dictionary<Type, PropertyInfo?> OrbManagerPropertyCache = new();

    /// <summary>
    /// 不同 OrbManager 类型上的反射方法缓存。
    /// </summary>
    private static readonly Dictionary<Type, Dictionary<string, MethodInfo?>> OrbManagerMethodCache = new();

    /// <summary>
    /// 每名玩家独立持有一份面具运行时状态，避免联机时不同玩家的面具队列互相污染。
    /// </summary>
    private static readonly Dictionary<Player, MaskRuntimeState> PlayerStates = new();

    /// <summary>
    /// 没有玩家上下文时使用的兜底状态；正常战斗流程不应依赖它。
    /// </summary>
    private static readonly MaskRuntimeState FallbackState = new();

    /// <summary>
    /// 当前异步流程里的玩家引用，供面具球刷新使用。
    /// </summary>
    private static readonly AsyncLocal<Player?> CachedPlayer = new();

    /// <summary>
    /// 当前异步流程里的玩家运行时状态，避免联机多玩家异步交错时互相覆盖。
    /// </summary>
    private static readonly AsyncLocal<MaskRuntimeState?> CachedState = new();

    /// <summary>
    /// 当前异步流程里的玩家引用。
    /// </summary>
    private static Player? _cachedPlayer
    {
        get => CachedPlayer.Value;
        set => CachedPlayer.Value = value;
    }

    /// <summary>
    /// 当前异步流程里的玩家运行时状态。
    /// </summary>
    private static MaskRuntimeState? _cachedState
    {
        get => CachedState.Value;
        set => CachedState.Value = value;
    }

    /// <summary>
    /// 当前正在打出的普通面具卡上下文。
    /// 用于让 AfterApplied 阶段也能拿到真实的出牌上下文，避免自动挤出时退回到 fire-and-forget 分支。
    /// </summary>
    private static readonly Dictionary<Creature, PlayerChoiceContext> PendingMaskApplyContexts = new();

    /// <summary>
    /// 某张返还出来的面具牌当前携带的永久剥离减费次数。
    /// </summary>
    private static readonly Dictionary<CardModel, int> MaskCardCostReductions = new();

    /// <summary>
    /// 当前玩家的普通面具FIFO历史队列，塔拉不参与此队列。
    /// </summary>
    private static LinkedList<string> MaskHistory => CurrentState.MaskHistory;

    /// <summary>
    /// 当前玩家的普通面具堆叠记录。
    /// </summary>
    private static Dictionary<string, List<MaskStackRecord>> MaskStackRecords => CurrentState.MaskStackRecords;

    /// <summary>
    /// 当前玩家的塔拉面具施放记录。
    /// </summary>
    private static List<MaskStackRecord> TaLaMaskCastRecords => CurrentState.TaLaMaskCastRecords;

    /// <summary>
    /// 当前玩家的本场战斗额外面具容量。
    /// </summary>
    private static int ExtraMaskCapacityThisCombat
    {
        get => CurrentState.ExtraMaskCapacityThisCombat;
        set => CurrentState.ExtraMaskCapacityThisCombat = value;
    }

    /// <summary>
    /// 当前玩家的面具栏延迟刷新层数。
    /// </summary>
    private static int DeferredMaskOrbRefreshDepth
    {
        get => CurrentState.DeferredMaskOrbRefreshDepth;
        set => CurrentState.DeferredMaskOrbRefreshDepth = value;
    }

    /// <summary>
    /// 当前玩家延迟期间是否发生过需要刷新面具栏的变化。
    /// </summary>
    private static bool DeferredMaskOrbRefreshRequested
    {
        get => CurrentState.DeferredMaskOrbRefreshRequested;
        set => CurrentState.DeferredMaskOrbRefreshRequested = value;
    }

    /// <summary>
    /// 当前玩家塔拉基础版施放次数。
    /// </summary>
    private static int TaLaBaseCastCount
    {
        get => CurrentState.TaLaBaseCastCount;
        set => CurrentState.TaLaBaseCastCount = value;
    }

    /// <summary>
    /// 当前玩家塔拉升级版施放次数。
    /// </summary>
    private static int TaLaUpgradedCastCount
    {
        get => CurrentState.TaLaUpgradedCastCount;
        set => CurrentState.TaLaUpgradedCastCount = value;
    }

    /// <summary>
    /// 当前正在操作的玩家状态。
    /// </summary>
    private static MaskRuntimeState CurrentState => _cachedState ?? FallbackState;

    /// <summary>
    /// 塔拉面具能力键。
    /// </summary>
    public static string TaLaMaskPowerKey => typeof(TaLaPower).FullName ?? typeof(TaLaPower).Name;

    /// <summary>
    /// 初始化当前玩家缓存。
    /// </summary>
    private static void EnsureInitialized(Creature creature)
    {
        if (!SetCurrentPlayer(creature.Player))
        {
            return;
        }

        if (_cachedPlayer != null)
        {
            MainFile.Logger.Info($"【面具管理器】初始化玩家状态完成：玩家={_cachedPlayer.NetId}，基础栏位={BaseMaskCapacity}");
        }
    }

    /// <summary>
    /// 将当前操作上下文切换到指定玩家。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>玩家有效时返回 true。</returns>
    private static bool SetCurrentPlayer(Player? player)
    {
        if (player == null)
        {
            return _cachedState != null;
        }

        _cachedPlayer = player;
        _cachedState = GetOrCreateState(player);
        return true;
    }

    /// <summary>
    /// 获取或创建指定玩家的面具运行时状态。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>玩家独立状态。</returns>
    private static MaskRuntimeState GetOrCreateState(Player player)
    {
        if (!PlayerStates.TryGetValue(player, out MaskRuntimeState? state))
        {
            state = new MaskRuntimeState();
            PlayerStates[player] = state;
        }

        if (!ReferenceEquals(state.RunState, player.RunState))
        {
            state.Clear();
            state.RunState = player.RunState;
            MainFile.Logger.Info($"【面具管理器】检测到玩家局状态变化，已重置该玩家面具状态：玩家={player.NetId}。");
        }

        return state;
    }

    /// <summary>
    /// 获取指定玩家的已有状态；没有时返回 null。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>玩家状态。</returns>
    private static MaskRuntimeState? GetExistingState(Player? player)
    {
        return player != null && PlayerStates.TryGetValue(player, out MaskRuntimeState? state) ? state : null;
    }

    /// <summary>
    /// 战斗结束时重置所有面具状态。
    /// </summary>
    public static void Clear()
    {
        MaskCardCostReductions.Clear();
        PendingMaskApplyContexts.Clear();
        foreach (Player player in PlayerStates.Keys.ToList())
        {
            PlayerStates[player].Clear();
            RemoveLegacyMaskOrbsFromNativeQueue(player);
            MaskOrbDisplayService.Clear(player);
        }

        FallbackState.Clear();
        PlayerStates.Clear();
        _cachedPlayer = null;
        _cachedState = null;
        MainFile.Logger.Info("【面具管理器】战斗结束，已清理所有玩家的面具状态。");
    }

    /// <summary>
    /// 获取当前面具容量。
    /// 容量 = 基础3 + 塔拉扩充层数 + 本场额外容量，上限10。
    /// </summary>
    public static int GetMaskCapacity(Creature? creature)
    {
        int taLaAmount = 0;
        if (creature != null)
        {
            SetCurrentPlayer(creature.Player);
            var taLaPower = creature.GetPower<TaLaPower>();
            taLaAmount = taLaPower?.Amount ?? 0;
        }

        int extraCapacity = CurrentState.ExtraMaskCapacityThisCombat;
        int capacity = BaseMaskCapacity + taLaAmount + Math.Max(0, extraCapacity);
        return Math.Min(capacity, MaxMaskCapacity);
    }

    /// <summary>
    /// 增加本场战斗额外面具容量。
    /// </summary>
    public static void AddExtraMaskCapacity(Player? player, int amount)
    {
        SetCurrentPlayer(player);
        if (amount <= 0)
        {
            return;
        }

        ExtraMaskCapacityThisCombat += amount;
        MainFile.Logger.Info($"【面具管理器】额外容量+{amount}，当前额外容量={ExtraMaskCapacityThisCombat}");
        UpdateMaskOrbs(player);
    }

    /// <summary>
    /// 增加当前缓存玩家的本场战斗额外面具容量。
    /// </summary>
    /// <param name="amount">增加数量。</param>
    public static void AddExtraMaskCapacity(int amount)
    {
        AddExtraMaskCapacity(_cachedPlayer, amount);
    }

    /// <summary>
    /// 将面具容量最大化。
    /// </summary>
    public static void MaximizeMaskCapacity(Player? player)
    {
        SetCurrentPlayer(player);
        ExtraMaskCapacityThisCombat = Math.Max(ExtraMaskCapacityThisCombat, MaxMaskCapacity - BaseMaskCapacity);
        MainFile.Logger.Info($"【面具管理器】容量最大化，当前额外容量={ExtraMaskCapacityThisCombat}");
        UpdateMaskOrbs(player);
    }

    /// <summary>
    /// 将当前缓存玩家的面具容量最大化。
    /// </summary>
    public static void MaximizeMaskCapacity()
    {
        MaximizeMaskCapacity(_cachedPlayer);
    }

    /// <summary>
    /// 将面具容量最大化，并立即同步当前玩家的真实充能球栏位。
    /// </summary>
    public static async Task MaximizeMaskCapacityAsync(Player? player)
    {
        SetCurrentPlayer(player);

        ExtraMaskCapacityThisCombat = Math.Max(ExtraMaskCapacityThisCombat, MaxMaskCapacity - BaseMaskCapacity);
        MainFile.Logger.Info($"【面具管理器】容量最大化，当前额外容量={ExtraMaskCapacityThisCombat}");

        if (player != null)
        {
            await EnsureMaskCapacitySlotsAsync(player);
        }
        else
        {
            UpdateMaskOrbs();
        }
    }

    /// <summary>
    /// 普通面具施加时调用。
    /// 已存在同类面具时只叠层，不改变FIFO顺序；新面具满槽时按FIFO挤出最早面具。
    /// 最后只做增量同步，避免多个面具球时整排重建造成卡顿。
    /// </summary>
    public static async Task OnMaskAppliedAsync(Creature creature, string maskPowerKey, BaseMaskPower power, PlayerChoiceContext? choiceContext = null)
    {
        EnsureInitialized(creature);

        if (MaskHistory.Contains(maskPowerKey))
        {
            if (creature.Player != null)
            {
                TryUpdateMaskOrbAmount(creature.Player, maskPowerKey, power.Amount, power.Id.Entry);
            }

            MainFile.Logger.Info($"【面具管理器】面具已存在，仅叠层：{maskPowerKey}");
            await SyncMaskOrbsIncrementalAsync(creature.Player);
            return;
        }

        int capacity = GetMaskCapacity(creature);
        while (MaskHistory.Count >= capacity)
        {
            await EvictOldestMaskAsync(creature, choiceContext);
        }

        MaskHistory.AddLast(maskPowerKey);
        MainFile.Logger.Info($"【面具管理器】新面具入队：{maskPowerKey}，当前普通面具数={MaskHistory.Count}，容量={capacity}");
        await SyncMaskOrbsIncrementalAsync(creature.Player);
    }

    /// <summary>
    /// 普通面具移除时调用。
    /// </summary>
    public static void OnMaskRemoved(string maskPowerKey, Player? player = null)
    {
        SetCurrentPlayer(player);

        if (MaskHistory.Contains(maskPowerKey))
        {
            MaskHistory.Remove(maskPowerKey);
            MainFile.Logger.Info($"【面具管理器】面具移除：{maskPowerKey}，剩余普通面具数={MaskHistory.Count}");
        }

        MaskStackRecords.Remove(maskPowerKey);
        RemoveAllMaskOrbs(_cachedPlayer, maskPowerKey);
        UpdateMaskOrbs();
    }

    /// <summary>
    /// 普通面具移除时通过增量同步刷新显示。
    /// </summary>
    public static async Task OnMaskRemovedAsync(string maskPowerKey, Player? player = null)
    {
        SetCurrentPlayer(player);

        if (MaskHistory.Contains(maskPowerKey))
        {
            MaskHistory.Remove(maskPowerKey);
            MainFile.Logger.Info($"【面具管理器】面具移除：{maskPowerKey}，剩余普通面具数={MaskHistory.Count}");
        }

        MaskStackRecords.Remove(maskPowerKey);
        await SyncMaskOrbsIncrementalAsync(_cachedPlayer);
    }

    /// <summary>
    /// 塔拉移除时调用。
    /// </summary>
    public static void OnTaLaRemoved(Player? player)
    {
        SetCurrentPlayer(player);
        TaLaMaskCastRecords.Clear();
        TaLaBaseCastCount = 0;
        TaLaUpgradedCastCount = 0;
        RemoveAllMaskOrbs(_cachedPlayer, TaLaMaskPowerKey);
        UpdateMaskOrbs();
    }

    /// <summary>
    /// 塔拉移除时清空塔拉施放记录，视觉由调用方最后统一增量同步。
    /// </summary>
    public static Task OnTaLaRemovedAsync(Player? player)
    {
        SetCurrentPlayer(player);
        TaLaMaskCastRecords.Clear();
        TaLaBaseCastCount = 0;
        TaLaUpgradedCastCount = 0;
        return RefreshAfterTaLaRemovedAsync(_cachedPlayer);
    }

    /// <summary>
    /// 强制执行容量限制（异步版本，确保挤出操作完整执行）。
    /// 注意：挤出完成后只增量同步一次，避免每挤出一个面具都整排重建。
    /// </summary>
    public static async Task<bool> EnforceCapacityAsync(Creature creature, PlayerChoiceContext? choiceContext = null)
    {
        SetCurrentPlayer(creature.Player);
        int capacity = GetMaskCapacity(creature);
        bool changed = false;
        while (MaskHistory.Count > capacity)
        {
            await EvictOldestMaskAsync(creature, choiceContext);
            changed = true;
            MainFile.Logger.Info($"【面具管理器】容量收缩挤出后刷新轨道：剩余普通面具数={MaskHistory.Count}，容量={capacity}");
        }

        if (changed)
        {
            await SyncMaskOrbsIncrementalAsync(creature.Player);
        }

        MainFile.Logger.Info($"【面具管理器】EnforceCapacityAsync完成：容量={capacity}，剩余普通面具数={MaskHistory.Count}");
        return changed;
    }

    /// <summary>
    /// 强制执行容量限制（同步版本，仅用于非异步上下文的兜底）。
    /// </summary>
    public static void EnforceCapacity(Creature creature)
    {
        if (creature == null)
        {
            MainFile.Logger.Info("【面具管理器】同步容量兜底被调用，但角色为空，已跳过。");
            return;
        }

        var player = creature.Player;
        if (player == null)
        {
            MainFile.Logger.Info($"【面具管理器】同步容量兜底被调用，但角色没有玩家归属：角色={creature.GetType().Name}，已跳过。");
            return;
        }

        SetCurrentPlayer(player);
        if (creature.CombatState == null || player.PlayerCombatState == null)
        {
            MainFile.Logger.Info($"【面具管理器】同步容量兜底被调用，但当前不在有效战斗状态：玩家={player.NetId}，已跳过。");
            return;
        }

        int capacity = GetMaskCapacity(creature);
        MainFile.Logger.Info($"【面具管理器】同步容量兜底被调用：玩家={player.NetId}，普通面具数={MaskHistory.Count}，容量={capacity}。正常流程应优先使用异步容量检查。");
        while (MaskHistory.Count > capacity)
        {
            EvictOldestMask(creature);
        }

        UpdateMaskOrbs(player);
    }

    /// <summary>
    /// 记录普通面具的堆叠信息。
    /// </summary>
    public static void RecordMaskStacks(Player? player, string maskPowerKey, int stackCount, bool upgraded, int permanentCostReductionCount = 0)
    {
        SetCurrentPlayer(player);
        if (stackCount <= 0)
        {
            return;
        }

        if (!MaskStackRecords.ContainsKey(maskPowerKey))
        {
            MaskStackRecords[maskPowerKey] = [];
        }

        for (int i = 0; i < stackCount; i++)
        {
            MaskStackRecords[maskPowerKey].Add(new MaskStackRecord(upgraded, Math.Max(0, permanentCostReductionCount)));
        }
    }

    /// <summary>
    /// 记录当前缓存玩家的普通面具堆叠信息。
    /// </summary>
    public static void RecordMaskStacks(string maskPowerKey, int stackCount, bool upgraded, int permanentCostReductionCount = 0)
    {
        RecordMaskStacks(_cachedPlayer, maskPowerKey, stackCount, upgraded, permanentCostReductionCount);
    }

    /// <summary>
    /// 记录一次塔拉施放信息。
    /// </summary>
    public static void RecordTaLaMaskCast(Player? player, bool upgraded, int permanentCostReductionCount)
    {
        SetCurrentPlayer(player);
        int costReductionCount = Math.Max(0, permanentCostReductionCount);
        TaLaMaskCastRecords.Add(new MaskStackRecord(upgraded, costReductionCount));
        if (upgraded)
        {
            TaLaUpgradedCastCount++;
        }
        else
        {
            TaLaBaseCastCount++;
        }

        MainFile.Logger.Info($"【塔拉面具】记录真实施放：升级版={upgraded}，剥离减费次数={costReductionCount}，基础版次数={TaLaBaseCastCount}，升级版次数={TaLaUpgradedCastCount}，总记录数={TaLaMaskCastRecords.Count}");
    }

    /// <summary>
    /// 记录当前缓存玩家的一次塔拉施放信息。
    /// </summary>
    public static void RecordTaLaMaskCast(bool upgraded, int permanentCostReductionCount)
    {
        RecordTaLaMaskCast(_cachedPlayer, upgraded, permanentCostReductionCount);
    }

    /// <summary>
    /// 获取当前塔拉真实施放记录数。
    /// </summary>
    public static int GetTaLaMaskCastRecordCount()
    {
        return TaLaMaskCastRecords.Count;
    }

    /// <summary>
    /// 获取塔拉每回合发牌时使用的施放记录快照。
    /// </summary>
    public static List<MaskStackRecord> GetTaLaMaskCastRecordsSnapshot(Player? player)
    {
        SetCurrentPlayer(player);
        return BuildTaLaMaskRecordsSnapshot(false, false);
    }

    /// <summary>
    /// 获取当前缓存玩家的塔拉施放记录快照。
    /// </summary>
    public static List<MaskStackRecord> GetTaLaMaskCastRecordsSnapshot()
    {
        return GetTaLaMaskCastRecordsSnapshot(_cachedPlayer);
    }

    /// <summary>
    /// 获取塔拉剥离时使用的记录快照。
    /// 只返回真实记录，不再用当前升级状态兜底生成额外塔拉。
    /// </summary>
    public static List<MaskStackRecord> GetTaLaPeelMaskRecordsSnapshot(bool fallbackUpgraded)
    {
        return BuildTaLaMaskRecordsSnapshot(fallbackUpgraded, false);
    }

    /// <summary>
    /// 构建塔拉施放记录快照。
    /// 塔拉返牌需要保留逐张升级状态和减费次数，因此不能按容量或计数反推。
    /// </summary>
    private static List<MaskStackRecord> BuildTaLaMaskRecordsSnapshot(bool fallbackUpgraded, bool fallbackWhenEmpty)
    {
        return new List<MaskStackRecord>(TaLaMaskCastRecords);
    }

    /// <summary>
    /// 按当前面具容量同步独立面具栏栏位数量。
    /// </summary>
    public static async Task EnsureMaskCapacitySlotsAsync(Player? player)
    {
        SetCurrentPlayer(player);
        if (_cachedPlayer == null)
        {
            return;
        }

        if (IsMaskOrbRefreshDeferred())
        {
            RequestDeferredMaskOrbRefresh();
            return;
        }

        var creature = _cachedPlayer.Creature;
        var orbQueue = _cachedPlayer.PlayerCombatState?.OrbQueue;
        if (creature == null || orbQueue == null)
        {
            return;
        }

        int targetCapacity = GetMaskCapacity(creature);
        RefreshMaskDisplay(_cachedPlayer, targetCapacity);

        await Task.CompletedTask;
        MainFile.Logger.Info($"【面具管理器】同步独立面具栏容量：目标容量={targetCapacity}，原生充能球容量={orbQueue.Capacity}");
    }

    /// <summary>
    /// 获取普通面具的堆叠记录快照。
    /// </summary>
    public static List<MaskStackRecord> GetMaskStackSnapshot(Player? player, string maskPowerKey, int totalAmount)
    {
        SetCurrentPlayer(player);
        var records = MaskStackRecords.GetValueOrDefault(maskPowerKey) ?? [];
        var snapshot = new List<MaskStackRecord>(records);

        while (snapshot.Count < totalAmount)
        {
            snapshot.Add(new MaskStackRecord(false, 0));
        }

        if (snapshot.Count > totalAmount)
        {
            snapshot = snapshot.Take(totalAmount).ToList();
        }

        return snapshot;
    }

    /// <summary>
    /// 获取当前缓存玩家的普通面具堆叠记录快照。
    /// </summary>
    public static List<MaskStackRecord> GetMaskStackSnapshot(string maskPowerKey, int totalAmount)
    {
        return GetMaskStackSnapshot(_cachedPlayer, maskPowerKey, totalAmount);
    }

    /// <summary>
    /// 获取当前普通面具队列。
    /// </summary>
    public static List<string> GetCurrentMaskKeys(Player? player)
    {
        SetCurrentPlayer(player);
        return [.. MaskHistory];
    }

    /// <summary>
    /// 获取当前缓存玩家的普通面具队列。
    /// </summary>
    public static List<string> GetCurrentMaskKeys()
    {
        return GetCurrentMaskKeys(_cachedPlayer);
    }

    /// <summary>
    /// 根据面具能力键创建对应面具牌。
    /// </summary>
    public static CardModel? CreateMaskCardByPowerKey(object combatState, string maskPowerKey, Player owner)
    {
        var cardType = GetMaskCardType(maskPowerKey);
        if (cardType == null)
        {
            return null;
        }

        return ShadowKhanCardHelper.CreateCardByType(combatState, cardType, owner);
    }

    /// <summary>
    /// 给返还的面具牌写入永久剥离减费。
    /// </summary>
    public static void ApplyPermanentPeelCostReduction(CardModel card, string maskPowerKey, int permanentCostReductionCount, int? baseCostOverride = null)
    {
        int reduction = Math.Max(0, permanentCostReductionCount);
        int baseCost = baseCostOverride ?? GetMaskCardBaseCost(maskPowerKey);
        card.EnergyCost.SetCustomBaseCost(Math.Max(0, baseCost - reduction));

        if (card is ShengZhuSts2ModCard shengZhuCard)
        {
            shengZhuCard.MaskPermanentCostReductionCount = reduction;
        }

        if (reduction > 0)
        {
            MaskCardCostReductions[card] = reduction;
        }
        else
        {
            MaskCardCostReductions.Remove(card);
        }
    }

    /// <summary>
    /// 消耗并读取面具牌身上的永久剥离减费次数。
    /// </summary>
    public static int ConsumeMaskCardPermanentCostReduction(CardModel card)
    {
        if (card is ShengZhuSts2ModCard shengZhuCard && shengZhuCard.MaskPermanentCostReductionCount > 0)
        {
            int cardReduction = Math.Max(0, shengZhuCard.MaskPermanentCostReductionCount);
            shengZhuCard.MaskPermanentCostReductionCount = 0;
            MaskCardCostReductions.Remove(card);
            MainFile.Logger.Info($"【面具管理器】读取卡牌自身剥离减费次数：{card.Title}，次数={cardReduction}");
            return cardReduction;
        }

        if (!MaskCardCostReductions.TryGetValue(card, out int reduction))
        {
            return 0;
        }

        MaskCardCostReductions.Remove(card);
        return Math.Max(0, reduction);
    }

    /// <summary>
    /// 将返还的面具牌加入弃牌堆。
    /// </summary>
    public static async Task AddGeneratedMaskCardToDiscard(Player player, CardModel card)
    {
        await AddGeneratedMaskCardsToDiscard(player, [card]);
    }

    /// <summary>
    /// 将返还的多张面具牌批量加入弃牌堆，并同步刷新弃牌堆预览与数量显示。
    /// </summary>
    public static async Task AddGeneratedMaskCardsToDiscard(Player player, IEnumerable<CardModel> cards)
    {
        var generatedCards = cards.ToList();
        if (generatedCards.Count == 0)
        {
            return;
        }

        var addResults = await CardPileCmdHelper.AddGeneratedCardsToCombat(generatedCards, PileType.Discard, addedByPlayer: true);
        CardCmd.PreviewCardPileAdd(addResults);
    }

    /// <summary>
    /// 记录当前正在打出的普通面具卡上下文。
    /// </summary>
    public static void SetPendingMaskApplyContext(Creature? creature, PlayerChoiceContext choiceContext)
    {
        if (creature == null)
        {
            return;
        }

        PendingMaskApplyContexts[creature] = choiceContext;
    }

    /// <summary>
    /// 清理当前普通面具卡的上下文缓存。
    /// </summary>
    public static void ClearPendingMaskApplyContext(Creature? creature)
    {
        if (creature == null)
        {
            return;
        }

        PendingMaskApplyContexts.Remove(creature);
    }

    /// <summary>
    /// 获取当前普通面具卡的真实出牌上下文。
    /// 若没有则返回空，由调用方决定兜底逻辑。
    /// </summary>
    public static PlayerChoiceContext? GetPendingMaskApplyContext(Creature? creature)
    {
        if (creature == null)
        {
            return null;
        }

        return PendingMaskApplyContexts.GetValueOrDefault(creature);
    }

    /// <summary>
    /// 开始延迟刷新面具栏：用于无尽黑暗一次性添加多个面具时，避免每个面具都重建UI。
    /// </summary>
    public static void BeginDeferredMaskOrbRefresh(Creature? creature)
    {
        if (creature != null)
        {
            EnsureInitialized(creature);
        }

        DeferredMaskOrbRefreshDepth++;
        DeferredMaskOrbRefreshRequested = true;
        MainFile.Logger.Info($"【独立面具栏】进入延迟刷新：层数={DeferredMaskOrbRefreshDepth}");
    }

    /// <summary>
    /// 结束延迟刷新面具栏：若期间有面具变化，则统一刷新一次独立面具栏。
    /// </summary>
    public static async Task EndDeferredMaskOrbRefreshAsync(Player? player)
    {
        SetCurrentPlayer(player);
        if (DeferredMaskOrbRefreshDepth <= 0)
        {
            return;
        }

        DeferredMaskOrbRefreshDepth--;
        if (DeferredMaskOrbRefreshDepth > 0)
        {
            return;
        }

        bool shouldRefresh = DeferredMaskOrbRefreshRequested;
        DeferredMaskOrbRefreshRequested = false;
        if (!shouldRefresh)
        {
            return;
        }

        await RebuildMaskOrbsAsync(player ?? _cachedPlayer);
        MainFile.Logger.Info("【独立面具栏】延迟刷新结束，已统一刷新面具栏。");
    }

    /// <summary>
    /// 判断当前是否处于面具栏延迟刷新阶段。
    /// </summary>
    private static bool IsMaskOrbRefreshDeferred()
    {
        return DeferredMaskOrbRefreshDepth > 0;
    }

    /// <summary>
    /// 标记延迟刷新阶段内发生了充能球变化，结束时需要统一刷新。
    /// </summary>
    private static void RequestDeferredMaskOrbRefresh()
    {
        DeferredMaskOrbRefreshRequested = true;
    }

    /// <summary>
    /// 将返还的黑影兵团卡加入手牌；满手时自动打出。
    /// </summary>
    public static async Task AddGeneratedShadowKhanCardWithFallback(PlayerChoiceContext choiceContext, Player player, CardModel card)
    {
        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState == null)
        {
            return;
        }

        if (playerCombatState.Hand.Cards.Count >= 10)
        {
            BitterChoiceNextTurnCostPower.TryApplyToGeneratedShadowKhanCard(player, card, "面具挤出或剥离满手自动打出");
            await CardPileCmdHelper.AddGeneratedCardToCombat(card, PileType.Play, addedByPlayer: true);
            await CardCmd.AutoPlay(choiceContext, card, null);
            return;
        }

        BitterChoiceNextTurnCostPower.TryApplyToGeneratedShadowKhanCard(player, card, "面具挤出或剥离加入手牌");
        await CardPileCmdHelper.AddGeneratedCardToCombat(card, PileType.Hand, addedByPlayer: true);
    }

    /// <summary>
    /// 同步已有面具球的层数。
    /// 若存在重复球，会全部同步，但是否去重由 UpdateMaskOrbs 统一处理。
    /// </summary>
    public static bool TryUpdateMaskOrbAmount(Player? player, string maskPowerKey, int maskAmount, string? maskName = null)
    {
        SetCurrentPlayer(player);
        if (IsMaskOrbRefreshDeferred())
        {
            RequestDeferredMaskOrbRefresh();
            return false;
        }

        if (player?.Creature == null)
        {
            return false;
        }

        List<MaskOrbDisplayState> displayStates = RefreshMaskDisplay(player);
        bool visible = displayStates.Any(state => state.MaskPowerKey == maskPowerKey);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【独立面具栏】请求同步层数：{maskPowerKey}，层数={maskAmount}，显示中={visible}");
        return visible;
    }

    /// <summary>
    /// 获取指定面具球当前数量。
    /// </summary>
    public static int CountMaskOrbs(Player? player, string maskPowerKey)
    {
        SetCurrentPlayer(player);
        if (player?.Creature == null)
        {
            return 0;
        }

        return BuildDesiredMaskOrbStates(player.Creature)
            .Count(orb => orb.MaskPowerKey == maskPowerKey);
    }

    /// <summary>
    /// 确保指定面具球已通过正式轨道命令显示出来。
    /// 若球不存在则通过 Channel 放入；若球已存在则同步层数并清理重复球。
    /// </summary>
    public static async Task EnsureMaskOrbDisplayed(Player player, string maskPowerKey, int maskAmount, string maskName)
    {
        SetCurrentPlayer(player);

        if (IsMaskOrbRefreshDeferred())
        {
            RequestDeferredMaskOrbRefresh();
            return;
        }

        RemoveDuplicateMaskOrbs(player, maskPowerKey, keepOne: false);
        RefreshMaskDisplay(player);
        await Task.CompletedTask;
        MainFile.Logger.Info($"【独立面具栏】确保显示：{maskPowerKey}，层数={maskAmount}，名称={maskName}");
    }

    /// <summary>
    /// 清理指定面具球的所有重复实例。
    /// 若 keepOne 为 true，则保留第1个，其余全部清理。
    /// </summary>
    public static int RemoveDuplicateMaskOrbs(Player? player, string maskPowerKey, bool keepOne)
    {
        var orbQueue = player?.PlayerCombatState?.OrbQueue;
        if (orbQueue == null)
        {
            return 0;
        }

        var maskOrbs = orbQueue.Orbs
            .OfType<MaskOrb>()
            .Where(orb => orb.MaskPowerKey == maskPowerKey)
            .ToList();
        int skip = keepOne ? 1 : 0;
        int removed = 0;

        foreach (var orb in maskOrbs.Skip(skip))
        {
            RemoveMaskOrbVisual(player, orb);
            orbQueue.Remove(orb);
            removed++;
        }

        return removed;
    }

    /// <summary>
    /// 移除指定面具球的全部实例。
    /// </summary>
    private static int RemoveAllMaskOrbs(Player? player, string maskPowerKey)
    {
        return RemoveDuplicateMaskOrbs(player, maskPowerKey, keepOne: false);
    }

    /// <summary>
    /// 按真实面具状态轻量同步充能球：只新增、移除或更新有变化的球，异常时才走强制重建兜底。
    /// </summary>
    public static async Task SyncMaskOrbsIncrementalAsync(Player? player, int? displayCapacityOverride = null)
    {
        SetCurrentPlayer(player);
        if (_cachedPlayer == null)
        {
            return;
        }

        if (IsMaskOrbRefreshDeferred())
        {
            RequestDeferredMaskOrbRefresh();
            return;
        }

        var creature = _cachedPlayer.Creature;
        if (creature == null)
        {
            return;
        }

        int targetCapacity = Math.Clamp(displayCapacityOverride ?? GetMaskCapacity(creature), 0, MaxMaskCapacity);
        List<MaskOrbDisplayState> displayStates = RefreshMaskDisplay(_cachedPlayer, targetCapacity);
        await Task.CompletedTask;
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【独立面具栏】增量同步完成：容量={targetCapacity}，目标球数={displayStates.Count}");
    }

    /// <summary>
    /// 判断当前轨道是否不适合增量同步。
    /// 重复球和需要在中间插入球的情况，交给强制重建兜底处理。
    /// </summary>
    private static bool RequiresFullRebuildForIncrementalSync(List<MaskOrb> currentMaskOrbs, List<MaskOrbState> desiredStates)
    {
        if (currentMaskOrbs
            .GroupBy(orb => orb.MaskPowerKey)
            .Any(group => group.Count() > 1))
        {
            return true;
        }

        var desiredIndexByKey = desiredStates
            .Select((state, index) => new { state.MaskPowerKey, Index = index })
            .ToDictionary(item => item.MaskPowerKey, item => item.Index);
        var currentDesiredKeys = currentMaskOrbs
            .Where(orb => desiredIndexByKey.ContainsKey(orb.MaskPowerKey))
            .Select(orb => orb.MaskPowerKey)
            .ToList();
        var currentDesiredKeySet = currentDesiredKeys.ToHashSet();
        var desiredKeysAlreadyInQueue = desiredStates
            .Where(state => currentDesiredKeySet.Contains(state.MaskPowerKey))
            .Select(state => state.MaskPowerKey)
            .ToList();

        if (!currentDesiredKeys.SequenceEqual(desiredKeysAlreadyInQueue))
        {
            return true;
        }

        var missingIndexes = desiredStates
            .Where(state => !currentDesiredKeySet.Contains(state.MaskPowerKey))
            .Select(state => desiredIndexByKey[state.MaskPowerKey])
            .ToList();
        if (missingIndexes.Count == 0)
        {
            return false;
        }

        int firstMissingIndex = missingIndexes.Min();
        return currentDesiredKeys.Any(key => desiredIndexByKey[key] > firstMissingIndex);
    }

    /// <summary>
    /// 清空当前轨道并按真实状态重新放入面具球，强制刷新 UI。
    /// 只通过正式 Channel 放入面具球，不再手工修改轨道列表。
    /// </summary>
    public static async Task RebuildMaskOrbsAsync(Player? player, int? displayCapacityOverride = null)
    {
        SetCurrentPlayer(player);
        if (_cachedPlayer == null)
        {
            return;
        }

        if (IsMaskOrbRefreshDeferred())
        {
            RequestDeferredMaskOrbRefresh();
            return;
        }

        var creature = _cachedPlayer.Creature;
        if (creature == null)
        {
            return;
        }

        int targetCapacity = displayCapacityOverride ?? GetMaskCapacity(creature);
        List<MaskOrbDisplayState> displayStates = RefreshMaskDisplay(_cachedPlayer, targetCapacity);
        await Task.CompletedTask;
        int taLaRecordCount = creature.GetPower<TaLaPower>()?.GetRecordedMaskCastCount() ?? 0;
        MainFile.Logger.Info($"【独立面具栏】强制重建完成：容量={targetCapacity}，显示面具数={displayStates.Count}，普通面具数={MaskHistory.Count}，塔拉真实记录数={taLaRecordCount}");
    }

    /// <summary>
    /// 创建当前真实状态下应该显示的面具球列表。
    /// </summary>
    private static List<MaskOrb> CreateDesiredMaskOrbs(Creature creature, Player player)
    {
        SetCurrentPlayer(player);
        return BuildDesiredMaskOrbStates(creature)
            .Select(state => CreateMaskOrb(state.MaskPowerKey, state.MaskAmount, state.MaskName, player))
            .ToList();
    }

    /// <summary>
    /// 创建当前真实状态下应该显示的面具球状态列表。
    /// </summary>
    private static List<MaskOrbState> BuildDesiredMaskOrbStates(Creature creature)
    {
        SetCurrentPlayer(creature.Player);
        var desiredStates = new List<MaskOrbState>();
        var taLaPower = creature.GetPower<TaLaPower>();
        if (taLaPower != null && taLaPower.Amount > 0)
        {
            desiredStates.Add(new MaskOrbState(TaLaMaskPowerKey, taLaPower.GetRecordedMaskCastCount(), taLaPower.Id.Entry));
        }

        var normalPowers = creature.Powers
            .OfType<BaseMaskPower>()
            .ToDictionary(power => power.MaskPowerKey, power => power);

        foreach (string maskKey in MaskHistory)
        {
            if (normalPowers.TryGetValue(maskKey, out BaseMaskPower? power))
            {
                desiredStates.Add(new MaskOrbState(maskKey, power.Amount, power.Id.Entry));
            }
        }

        return desiredStates;
    }

    /// <summary>
    /// 创建一个面具球模型。
    /// </summary>
    private static MaskOrb CreateMaskOrb(string maskPowerKey, int maskAmount, string maskName, Player player)
    {
        var maskOrb = (MaskOrb)ModelDb.Orb<MaskOrb>().ToMutable(maskAmount);
        maskOrb.MaskPowerKey = maskPowerKey;
        maskOrb.MaskName = maskName;
        maskOrb.MaskAmount = maskAmount;
        maskOrb.Owner = player;
        return maskOrb;
    }

    /// <summary>
    /// 按当前真实面具状态刷新独立面具栏，并清理原生充能球轨道里的历史残留面具球。
    /// </summary>
    /// <param name="player">需要刷新显示的玩家。</param>
    /// <param name="displayCapacityOverride">指定显示容量；为空时按当前面具容量计算。</param>
    private static List<MaskOrbDisplayState> RefreshMaskDisplay(Player? player, int? displayCapacityOverride = null)
    {
        SetCurrentPlayer(player);
        if (_cachedPlayer?.Creature == null)
        {
            return [];
        }

        int removedLegacyCount = RemoveLegacyMaskOrbsFromNativeQueue(_cachedPlayer);
        int targetCapacity = Math.Clamp(displayCapacityOverride ?? GetMaskCapacity(_cachedPlayer.Creature), 0, MaxMaskCapacity);
        List<MaskOrbState> desiredStates = BuildDesiredMaskOrbStates(_cachedPlayer.Creature);
        List<MaskOrbDisplayState> displayStates = BuildDisplayStates(desiredStates, targetCapacity);

        MaskOrbDisplayService.Refresh(_cachedPlayer, targetCapacity, displayStates);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【独立面具栏】刷新请求：容量={targetCapacity}，显示面具数={displayStates.Count}，清理原生残留面具球={removedLegacyCount}");
        return displayStates;
    }

    /// <summary>
    /// 将内部面具状态转换成独立面具栏显示状态。
    /// </summary>
    /// <param name="desiredStates">目标面具状态。</param>
    /// <param name="targetCapacity">显示容量。</param>
    /// <returns>显示状态列表。</returns>
    private static List<MaskOrbDisplayState> BuildDisplayStates(List<MaskOrbState> desiredStates, int targetCapacity)
    {
        int displayCount = Math.Min(Math.Max(0, targetCapacity), desiredStates.Count);
        List<MaskOrbDisplayState> displayStates = new(displayCount);
        for (int i = 0; i < displayCount; i++)
        {
            MaskOrbState state = desiredStates[i];
            displayStates.Add(new MaskOrbDisplayState(state.MaskPowerKey, state.MaskAmount, state.MaskName));
        }

        return displayStates;
    }

    /// <summary>
    /// 清理旧实现遗留在原生充能球轨道里的面具球，避免机器人卡继续把面具当真实充能球处理。
    /// </summary>
    /// <param name="player">需要清理的玩家。</param>
    /// <returns>清理掉的残留面具球数量。</returns>
    private static int RemoveLegacyMaskOrbsFromNativeQueue(Player? player)
    {
        var orbQueue = player?.PlayerCombatState?.OrbQueue;
        if (orbQueue == null)
        {
            return 0;
        }

        List<OrbModel> legacyOrbs = [];
        foreach (OrbModel orb in orbQueue.Orbs)
        {
            if (orb is MaskOrb or EmptyMaskOrb)
            {
                legacyOrbs.Add(orb);
            }
        }

        foreach (OrbModel orb in legacyOrbs)
        {
            RemoveMaskOrbVisual(player, orb);
            orbQueue.Remove(orb);
        }

        int removed = legacyOrbs.Count;
        if (removed > 0)
        {
            MainFile.Logger.Info($"【独立面具栏】清理原生轨道残留面具球：数量={removed}，保留原生充能球容量={orbQueue.Capacity}");
        }

        return removed;
    }

    /// <summary>
    /// 强制把面具球数据和视觉整条重建，专门处理剥离/挤出后的残留视觉。
    /// </summary>
    private static async Task ForceRebuildMaskOrbsAsync(Player player, int targetCapacity, List<MaskOrb> desiredOrbs)
    {
        SetCurrentPlayer(player);
        if (player.Creature == null)
        {
            return;
        }

        targetCapacity = Math.Clamp(targetCapacity, 0, MaxMaskCapacity);
        var displayStates = desiredOrbs
            .Take(targetCapacity)
            .Select(orb => new MaskOrbDisplayState(orb.MaskPowerKey, orb.MaskAmount, orb.MaskName))
            .ToList();
        RemoveLegacyMaskOrbsFromNativeQueue(player);
        MaskOrbDisplayService.Refresh(player, targetCapacity, displayStates);
        await Task.CompletedTask;
        MainFile.Logger.Info($"【独立面具栏】强制同步面具球：容量={targetCapacity}，显示面具球数={displayStates.Count}");
    }

    /// <summary>
    /// 塔拉被剥离后，清空塔拉施放记录。
    /// 充能球的视觉同步由调用方最终的增量同步统一处理。
    /// </summary>
    private static Task RefreshAfterTaLaRemovedAsync(Player? player)
    {
        SetCurrentPlayer(player);
        MainFile.Logger.Info($"【面具管理器】塔拉剥离后数据清理完成，等待增量同步统一刷新视觉");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 按真实战斗状态同步面具球：
    /// 1. 清理不该存在的残留球；
    /// 2. 去重，只保留1个合法球；
    /// 3. 更新已有球层数。
    /// 不再手工创建整条轨道，也不手工补空球。
    /// </summary>
    public static void UpdateMaskOrbs()
    {
        UpdateMaskOrbs(_cachedPlayer);
    }

    /// <summary>
    /// 按指定玩家的真实战斗状态同步面具球。
    /// </summary>
    /// <param name="player">需要同步的玩家。</param>
    public static void UpdateMaskOrbs(Player? player)
    {
        SetCurrentPlayer(player);
        if (_cachedPlayer == null)
        {
            return;
        }

        if (IsMaskOrbRefreshDeferred())
        {
            RequestDeferredMaskOrbRefresh();
            return;
        }

        var creature = _cachedPlayer.Creature;
        if (creature == null)
        {
            return;
        }

        int capacity = GetMaskCapacity(creature);
        List<MaskOrbDisplayState> displayStates = RefreshMaskDisplay(_cachedPlayer, capacity);
        int taLaOrbCount = displayStates.Count(state => state.MaskPowerKey == TaLaMaskPowerKey);
        int totalMaskOrbCount = displayStates.Count;
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【独立面具栏】状态更新：容量={capacity}，普通面具数={MaskHistory.Count}，塔拉球数={taLaOrbCount}，显示面具球总数={totalMaskOrbCount}，原生充能球容量={_cachedPlayer.PlayerCombatState?.OrbQueue.Capacity ?? 0}");
    }

    /// <summary>
    /// 兜底移除面具球对应的视觉节点，避免只删数据时残留红色面具球。
    /// </summary>
    private static void RemoveMaskOrbVisual(Player? player, OrbModel orb)
    {
        if (player?.Creature == null)
        {
            return;
        }

        try
        {
            object? orbManager = GetOrbManager(player);
            InvokeOrbManagerMethod(orbManager, "EvokeOrbAnim", [orb]);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【面具管理器】移除面具球视觉失败，改为仅清理数据：{orb.GetType().Name}，原因={ex.Message}");
        }
    }

    /// <summary>
    /// 兜底清空当前玩家的充能球视觉节点；用反射避免模组加载期强依赖战斗房间节点类型。
    /// </summary>
    private static void ClearOrbVisuals(Player? player)
    {
        try
        {
            object? orbManager = GetOrbManager(player);
            InvokeOrbManagerMethod(orbManager, "ClearOrbs", null);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【面具管理器】清空充能球视觉失败，改为仅清理数据：原因={ex.Message}");
        }
    }

    /// <summary>
    /// 立即清空当前玩家的充能球视觉节点，避免渐隐动画节点残留。
    /// </summary>
    private static void ClearOrbVisualsImmediately(Player? player)
    {
        try
        {
            object? orbManager = GetOrbManager(player);
            if (orbManager == null)
            {
                MainFile.Logger.Info("【面具管理器】未找到OrbManager，无法立即清空充能球视觉。");
                return;
            }

            var managerType = orbManager.GetType();
            if (managerType.GetField("_curTween", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(orbManager) is Godot.Tween tween)
            {
                tween.Kill();
            }

            if (managerType.GetField("_orbs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(orbManager) is System.Collections.IList orbNodes)
            {
                foreach (object? orbNode in orbNodes.Cast<object>().ToList())
                {
                    if (orbNode is Godot.Node node)
                    {
                        node.QueueFree();
                    }
                }

                orbNodes.Clear();
            }

            if (managerType.GetField("_orbContainer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(orbManager) is Godot.Node orbContainer)
            {
                foreach (Godot.Node child in orbContainer.GetChildren().OfType<Godot.Node>().ToList())
                {
                    if (child.GetType().FullName == "MegaCrit.Sts2.Core.Nodes.Orbs.NOrb")
                    {
                        child.QueueFree();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【面具管理器】立即清空充能球视觉失败：原因={ex.Message}");
        }
    }

    /// <summary>
    /// 补充指定数量的充能球空槽视觉。
    /// </summary>
    private static void AddOrbSlotVisuals(Player? player, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        try
        {
            object? orbManager = GetOrbManager(player);
            InvokeOrbManagerMethod(orbManager, "AddSlotAnim", [amount]);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【面具管理器】补充充能球空槽视觉失败：数量={amount}，原因={ex.Message}");
        }
    }

    /// <summary>
    /// 按当前 OrbQueue 最后一个球补充一个充能球视觉。
    /// </summary>
    private static void AddOrbVisual(Player? player)
    {
        try
        {
            object? orbManager = GetOrbManager(player);
            InvokeOrbManagerMethod(orbManager, "AddOrbAnim", null);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【面具管理器】补充面具球视觉失败：原因={ex.Message}");
        }
    }

    /// <summary>
    /// 获取当前战斗房间里玩家对应的 OrbManager；用反射隔离 Godot 节点类型依赖。
    /// </summary>
    private static object? GetOrbManager(Player? player)
    {
        if (player?.Creature == null)
        {
            return null;
        }

        object? combatRoom = CombatRoomInstanceProperty?.GetValue(null);
        object? creatureNode = GetCreatureNodeMethod?.Invoke(combatRoom, [player.Creature]);
        if (creatureNode == null)
        {
            return null;
        }

        Type creatureNodeType = creatureNode.GetType();
        if (!OrbManagerPropertyCache.TryGetValue(creatureNodeType, out PropertyInfo? orbManagerProperty))
        {
            orbManagerProperty = creatureNodeType.GetProperty("OrbManager");
            OrbManagerPropertyCache[creatureNodeType] = orbManagerProperty;
        }

        return orbManagerProperty?.GetValue(creatureNode);
    }

    /// <summary>
    /// 调用 OrbManager 方法，缓存反射方法信息以减少面具刷新时的反射开销。
    /// </summary>
    /// <param name="orbManager">OrbManager 实例。</param>
    /// <param name="methodName">方法名。</param>
    /// <param name="args">调用参数。</param>
    private static void InvokeOrbManagerMethod(object? orbManager, string methodName, object?[]? args)
    {
        if (orbManager == null)
        {
            return;
        }

        MethodInfo? method = GetCachedOrbManagerMethod(orbManager.GetType(), methodName);
        method?.Invoke(orbManager, args);
    }

    /// <summary>
    /// 获取缓存后的 OrbManager 方法。
    /// </summary>
    /// <param name="managerType">OrbManager 类型。</param>
    /// <param name="methodName">方法名。</param>
    /// <returns>方法信息；找不到时返回 null。</returns>
    private static MethodInfo? GetCachedOrbManagerMethod(Type managerType, string methodName)
    {
        if (!OrbManagerMethodCache.TryGetValue(managerType, out Dictionary<string, MethodInfo?>? methodCache))
        {
            methodCache = new Dictionary<string, MethodInfo?>();
            OrbManagerMethodCache[managerType] = methodCache;
        }

        if (!methodCache.TryGetValue(methodName, out MethodInfo? method))
        {
            method = managerType.GetMethod(methodName);
            methodCache[methodName] = method;
        }

        return method;
    }

    /// <summary>
    /// 挤出最早的普通面具（异步版本，确保返牌操作完整执行）。
    /// 每挤出一个面具后立即移除对应充能球，让玩家看到渐进的视觉反馈。
    /// </summary>
    private static async Task EvictOldestMaskAsync(Creature creature, PlayerChoiceContext? choiceContext = null)
    {
        SetCurrentPlayer(creature.Player);
        if (MaskHistory.Count == 0)
        {
            return;
        }

        string oldestKey = MaskHistory.First!.Value;
        MaskHistory.RemoveFirst();

        BaseMaskPower? oldestPower = null;
        foreach (var power in creature.Powers)
        {
            if (power is BaseMaskPower typedPower && typedPower.MaskPowerKey == oldestKey)
            {
                oldestPower = typedPower;
                break;
            }
        }

        if (oldestPower != null)
        {
            MainFile.Logger.Info($"【面具管理器】异步挤出面具：{oldestKey}，层数={oldestPower.Amount}");
            var player = creature.Player;
            if (player != null)
            {
                var ctx = choiceContext ?? new ThrowingPlayerChoiceContext();
                await oldestPower.OnEvict(ctx, player);
                MaskStackRecords.Remove(oldestKey);
            }

            await PowerCmd.Remove(oldestPower);
        }
        else
        {
            MainFile.Logger.Info($"【面具管理器】异步挤出面具：{oldestKey}，但对应Power已不存在。");
        }

        MainFile.Logger.Info($"【面具管理器】异步挤出完成：{oldestKey}，剩余普通面具数={MaskHistory.Count}");
    }

    /// <summary>
    /// 同步兜底路径中安全执行异步任务，确保异常会进入日志。
    /// </summary>
    /// <param name="task">要执行的异步任务。</param>
    /// <param name="actionName">任务名称。</param>
    private static void RunFallbackAsyncTask(Task task, string actionName)
    {
        _ = RunFallbackAsyncTaskInternal(task, actionName);
    }

    /// <summary>
    /// 同步兜底路径中异步任务的实际执行入口。
    /// </summary>
    /// <param name="task">要执行的异步任务。</param>
    /// <param name="actionName">任务名称。</param>
    /// <returns>异步任务。</returns>
    private static async Task RunFallbackAsyncTaskInternal(Task task, string actionName)
    {
        try
        {
            await task;
            MainFile.Logger.Info($"【面具管理器】同步兜底异步任务完成：{actionName}");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【面具管理器】同步兜底异步任务异常：{actionName}，异常={ex}");
        }
    }

    /// <summary>
    /// 挤出最早的普通面具（同步fire-and-forget版本，仅用于非异步上下文）。
    /// </summary>
    private static void EvictOldestMask(Creature creature)
    {
        if (creature == null)
        {
            MainFile.Logger.Info("【面具管理器】同步兜底挤出被调用，但角色为空，已跳过。");
            return;
        }

        var player = creature.Player;
        if (player == null)
        {
            MainFile.Logger.Info($"【面具管理器】同步兜底挤出被调用，但角色没有玩家归属：角色={creature.GetType().Name}，已跳过。");
            return;
        }

        SetCurrentPlayer(player);
        if (creature.CombatState == null || player.PlayerCombatState == null)
        {
            MainFile.Logger.Info($"【面具管理器】同步兜底挤出被调用，但当前不在有效战斗状态：玩家={player.NetId}，已跳过。");
            return;
        }

        if (MaskHistory.Count == 0)
        {
            MainFile.Logger.Info($"【面具管理器】同步兜底挤出被调用，但没有可挤出的普通面具：玩家={player.NetId}。");
            return;
        }

        string oldestKey = MaskHistory.First!.Value;
        MaskHistory.RemoveFirst();

        BaseMaskPower? oldestPower = null;
        foreach (var power in creature.Powers)
        {
            if (power is BaseMaskPower typedPower && typedPower.MaskPowerKey == oldestKey)
            {
                oldestPower = typedPower;
                break;
            }
        }

        if (oldestPower != null)
        {
            MainFile.Logger.Info($"【面具管理器】同步兜底挤出面具：{oldestKey}，层数={oldestPower.Amount}");
            Task evictTask = StartFallbackEvictTask(oldestPower, oldestKey, player);
            OnMaskRemoved(oldestKey, player);
            RunFallbackAsyncTask(RemovePowerAfterFallbackEvictAsync(evictTask, oldestPower, oldestKey), $"同步兜底完成挤出面具：{oldestKey}");
        }
        else
        {
            MainFile.Logger.Info($"【面具管理器】同步兜底挤出面具：{oldestKey}，但对应Power已不存在。");
        }
    }

    /// <summary>
    /// 启动同步兜底返牌任务，并捕获同步阶段的异常，避免异常打断后续能力移除。
    /// </summary>
    private static Task StartFallbackEvictTask(BaseMaskPower oldestPower, string oldestKey, Player player)
    {
        try
        {
            return oldestPower.OnEvict(new ThrowingPlayerChoiceContext(), player);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【面具管理器】同步兜底返还被挤出面具时发生同步异常：{oldestKey}，异常={ex}");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 同步兜底路径中等待返牌流程结束后再移除能力，减少返牌和移除能力乱序的风险。
    /// </summary>
    private static async Task RemovePowerAfterFallbackEvictAsync(Task evictTask, BaseMaskPower oldestPower, string oldestKey)
    {
        try
        {
            await evictTask;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【面具管理器】同步兜底返还被挤出面具时发生异步异常，将继续移除能力：{oldestKey}，异常={ex}");
        }

        await PowerCmd.Remove(oldestPower);
    }

    /// <summary>
    /// 根据面具能力键映射面具牌类型。
    /// </summary>
    private static Type? GetMaskCardType(string maskPowerKey)
    {
        if (maskPowerKey == (typeof(NiJiaPower).FullName ?? typeof(NiJiaPower).Name))
        {
            return typeof(NiJiaMask);
        }

        if (maskPowerKey == (typeof(LaZuoPower).FullName ?? typeof(LaZuoPower).Name))
        {
            return typeof(LaZuoMask);
        }

        if (maskPowerKey == (typeof(SaMoPower).FullName ?? typeof(SaMoPower).Name))
        {
            return typeof(SaMoMask);
        }

        if (maskPowerKey == (typeof(BaTePower).FullName ?? typeof(BaTePower).Name))
        {
            return typeof(BaTeMask);
        }

        if (maskPowerKey == (typeof(KaBoPower).FullName ?? typeof(KaBoPower).Name))
        {
            return typeof(KaBoMask);
        }

        if (maskPowerKey == (typeof(LeiSuPower).FullName ?? typeof(LeiSuPower).Name))
        {
            return typeof(LeiSuMask);
        }

        if (maskPowerKey == (typeof(ManNiPower).FullName ?? typeof(ManNiPower).Name))
        {
            return typeof(ManNiMask);
        }

        if (maskPowerKey == (typeof(MingTaPower).FullName ?? typeof(MingTaPower).Name))
        {
            return typeof(MingTaMask);
        }

        if (maskPowerKey == (typeof(YiKaPower).FullName ?? typeof(YiKaPower).Name))
        {
            return typeof(YiKaMask);
        }

        if (maskPowerKey == TaLaMaskPowerKey)
        {
            return typeof(TaLaMask);
        }

        return null;
    }

    /// <summary>
    /// 根据面具能力键映射面具牌基础费用。
    /// </summary>
    private static int GetMaskCardBaseCost(string maskPowerKey)
    {
        if (maskPowerKey == (typeof(NiJiaPower).FullName ?? typeof(NiJiaPower).Name))
        {
            return 0;
        }

        if (maskPowerKey == (typeof(KaBoPower).FullName ?? typeof(KaBoPower).Name))
        {
            return 1;
        }

        if (maskPowerKey == (typeof(SaMoPower).FullName ?? typeof(SaMoPower).Name))
        {
            return 1;
        }

        if (maskPowerKey == TaLaMaskPowerKey)
        {
            return 2;
        }

        if (maskPowerKey == (typeof(BaTePower).FullName ?? typeof(BaTePower).Name))
        {
            return 2;
        }

        if (maskPowerKey == (typeof(LaZuoPower).FullName ?? typeof(LaZuoPower).Name))
        {
            return 2;
        }

        if (maskPowerKey == (typeof(LeiSuPower).FullName ?? typeof(LeiSuPower).Name))
        {
            return 1;
        }

        if (maskPowerKey == (typeof(ManNiPower).FullName ?? typeof(ManNiPower).Name))
        {
            return 2;
        }

        if (maskPowerKey == (typeof(MingTaPower).FullName ?? typeof(MingTaPower).Name))
        {
            return 1;
        }

        if (maskPowerKey == (typeof(YiKaPower).FullName ?? typeof(YiKaPower).Name))
        {
            return 2;
        }

        return 0;
    }

    /// <summary>
    /// 普通面具堆叠记录。
    /// </summary>
    public record MaskStackRecord(bool Upgraded, int PermanentCostReductionCount);

    /// <summary>
    /// 目标面具球状态。
    /// </summary>
    private record MaskOrbState(string MaskPowerKey, int MaskAmount, string MaskName);

    /// <summary>
    /// 单个玩家的一场战斗面具运行时状态。
    /// </summary>
    private sealed class MaskRuntimeState
    {
        /// <summary>
        /// 当前状态所属局对象，用于识别新局并清理旧数据。
        /// </summary>
        public object? RunState { get; set; }

        /// <summary>
        /// 普通面具FIFO历史队列。
        /// </summary>
        public LinkedList<string> MaskHistory { get; } = new();

        /// <summary>
        /// 普通面具堆叠记录。
        /// </summary>
        public Dictionary<string, List<MaskStackRecord>> MaskStackRecords { get; } = new();

        /// <summary>
        /// 塔拉面具施放记录。
        /// </summary>
        public List<MaskStackRecord> TaLaMaskCastRecords { get; } = [];

        /// <summary>
        /// 本场战斗额外面具容量。
        /// </summary>
        public int ExtraMaskCapacityThisCombat { get; set; }

        /// <summary>
        /// 面具栏延迟刷新层数。
        /// </summary>
        public int DeferredMaskOrbRefreshDepth { get; set; }

        /// <summary>
        /// 延迟期间是否请求过刷新。
        /// </summary>
        public bool DeferredMaskOrbRefreshRequested { get; set; }

        /// <summary>
        /// 塔拉基础版施放次数。
        /// </summary>
        public int TaLaBaseCastCount { get; set; }

        /// <summary>
        /// 塔拉升级版施放次数。
        /// </summary>
        public int TaLaUpgradedCastCount { get; set; }

        /// <summary>
        /// 清理该玩家的面具运行时状态。
        /// </summary>
        public void Clear()
        {
            MaskHistory.Clear();
            MaskStackRecords.Clear();
            TaLaMaskCastRecords.Clear();
            ExtraMaskCapacityThisCombat = 0;
            DeferredMaskOrbRefreshDepth = 0;
            DeferredMaskOrbRefreshRequested = false;
            TaLaBaseCastCount = 0;
            TaLaUpgradedCastCount = 0;
        }
    }
}
