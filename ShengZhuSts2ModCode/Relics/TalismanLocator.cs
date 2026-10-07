using System.Reflection;
using System.Text.Json;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Events;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Rewards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 符咒探测仪：圣主初始遗物。
/// 功能1：每次战胜精英获得遗物时，可以放弃该遗物，改为获得一个你未拥有的随机符咒遗物。
/// 功能2：右键点击切换半自动/手动模式。半自动模式下符咒在回合开始时自动激发。
/// 计数器显示：半自动模式=1，手动模式=0。
/// </summary>
public class TalismanLocator : ShengZhuSts2ModRelic, ITalismanRightClickable
{
    /// <summary>
    /// 所有十二符咒遗物类型列表。
    /// </summary>
    private static readonly Type[] AllTalismanTypes =
    [
        typeof(RatTalisman),
        typeof(OxTalisman),
        typeof(TigerTalisman),
        typeof(RabbitTalisman),
        typeof(DragonTalisman),
        typeof(SnakeTalisman),
        typeof(HorseTalisman),
        typeof(SheepTalisman),
        typeof(MonkeyTalisman),
        typeof(RoosterTalisman),
        typeof(DogTalisman),
        typeof(PigTalisman)
    ];

    /// <summary>
    /// 根配置文件中的剧情事件替换楼层字段名，填写 1 到 3 的层数数字。
    /// </summary>
    private const string ConfiguredStoryEventActNumberConfigKey = "x层事件替换池楼层";

    /// <summary>
    /// 默认剧情事件替换楼层；保持旧版本默认第二层首个问号事件兜底。
    /// </summary>
    private const int DefaultStoryEventActNumber = 2;

    /// <summary>
    /// 剧情事件替换楼层下限。
    /// </summary>
    private const int MinStoryEventActNumber = 1;

    /// <summary>
    /// 剧情事件替换楼层上限。
    /// </summary>
    private const int MaxStoryEventActNumber = 3;

    /// <summary>
    /// 根配置文件中的 x 层剧情事件池字段名，数组里直接写中文事件名。
    /// </summary>
    private const string ConfiguredStoryEventPoolConfigKey = "x层事件替换池";

    /// <summary>
    /// 旧版二层剧情事件池字段名，仅用于兼容旧配置。
    /// </summary>
    private const string LegacySecondActStoryEventPoolConfigKey = "二层事件替换池";

    /// <summary>
    /// 根配置文件中的剧情事件池英文备用字段名，方便以后外部工具读取。
    /// </summary>
    private const string ConfiguredStoryEventPoolFallbackConfigKey = "storyEventReplacementPool";

    /// <summary>
    /// 默认剧情事件池；配置文件缺失或写错时使用这组兜底值。
    /// </summary>
    private static readonly string[] DefaultStoryEventNames =
    [
        "远古封印",
        "恶魔小龙",
        "刀龙黑气",
        "西瓦的手镯",
        "岁月史书"
    ];

    /// <summary>
    /// 剧情事件中文名到事件模型的映射，配置数组只能填写这些中文名。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Func<EventModel>> StoryEventFactories = new Dictionary<string, Func<EventModel>>
    {
        ["远古封印"] = static () => ModelDb.Event<PanKuBoxEvent>(),
        ["恶魔小龙"] = static () => ModelDb.Event<DragoEvent>(),
        ["刀龙黑气"] = static () => ModelDb.Event<DaoLongEvent>(),
        ["西瓦的手镯"] = static () => ModelDb.Event<ShivaBraceletEvent>(),
        ["岁月史书"] = static () => ModelDb.Event<HistoryBookEvent>()
    };

    /// <summary>
    /// 遗物奖励在展示阶段已经生成了内部遗物，但公开的 ClaimedRelic 只有领取后才有值。
    /// </summary>
    private static readonly FieldInfo? RelicRewardRelicField = typeof(RelicReward).GetField("_relic", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 旧版存档字段：本局选人界面固化的剧情事件替换楼层。
    /// 名称保留 IncreasedBlock 是为了兼容旧存档和联机 checksum 字段名。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int IncreasedBlock { get; set; }

    /// <summary>
    /// 本局是否已经从选人界面固化了 x 层剧情事件替换池配置。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public bool HasCustomStoryEventReplacementConfig { get; set; }

    /// <summary>
    /// 本局是否已经发放过圣主开局随机符咒（通过SavedProperty持久化到存档，SL后不会重复发放）。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public bool HasGrantedStartingTalisman
    {
        get => _hasGrantedStartingTalisman;
        set => _hasGrantedStartingTalisman = value;
    }

    private bool _hasGrantedStartingTalisman;

    /// <summary>
    /// 旧版存档字段：本局是否已经遇到过配置兜底目标剧情事件。
    /// 名称保留 CurrentBlock 是为了兼容旧存档和联机 checksum 字段名。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public bool CurrentBlock
    {
        get => _hasSeenSecondActStoryEvent;
        set => _hasSeenSecondActStoryEvent = value;
    }

    private bool _hasSeenSecondActStoryEvent;

    /// <summary>
    /// 旧版存档字段：半自动模式开关。true=半自动，false=手动。
    /// 名称保留 TinkerTimeType 是为了兼容旧存档和联机 checksum 字段名。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public bool TinkerTimeType
    {
        get => _semiAutoMode;
        set => _semiAutoMode = value;
    }

    private bool _semiAutoMode = true;

    /// <summary>
    /// 本局开局补发的符咒类型名，仅用于存档追踪和联机日志排查。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public string GrantedStartingTalismanTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 旧版存档字段：本局选人界面固化的剧情事件池中文名，使用逗号分隔。
    /// 名称保留 CombatsSeen 是为了兼容旧存档和联机 checksum 字段名。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public string CombatsSeen { get; set; } = string.Empty;

    /// <summary>
    /// 半自动模式开关。true=半自动，false=手动。
    /// </summary>
    public bool SemiAutoMode
    {
        get => TinkerTimeType;
        set => TinkerTimeType = value;
    }

    /// <summary>
    /// 本局是否已经遇到过配置兜底目标剧情事件。
    /// 变量名保留 SecondAct 是为了兼容旧业务语义。
    /// </summary>
    public bool HasSeenSecondActStoryEvent
    {
        get => CurrentBlock;
        set => CurrentBlock = value;
    }

    /// <summary>
    /// 本局选人界面固化的剧情事件替换楼层。
    /// </summary>
    public int SavedStoryEventActNumber
    {
        get => IncreasedBlock;
        set => IncreasedBlock = value;
    }

    /// <summary>
    /// 本局选人界面固化的剧情事件池中文名，使用逗号分隔。
    /// 空字符串表示关闭该兜底替换。
    /// </summary>
    public string SavedStoryEventPoolNamesCsv
    {
        get => CombatsSeen;
        set => CombatsSeen = value;
    }

    /// <summary>
    /// 遗物稀有度：初始遗物。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Starter;

    /// <summary>
    /// 显示计数器：半自动模式状态。
    /// </summary>
    public override bool ShowCounter => true;

    /// <summary>
    /// 计数器显示值：1=半自动，0=手动。
    /// </summary>
    public override int DisplayAmount => _semiAutoMode ? 1 : 0;

    /// <summary>
    /// 获取当前是否为半自动模式（供其他符咒查询）。
    /// </summary>
    public static bool IsSemiAuto(Player? player)
    {
        if (player == null) return false;
        TalismanLocator? locator = player.Relics.OfType<TalismanLocator>().FirstOrDefault();
        return locator?._semiAutoMode ?? false;
    }

    /// <summary>
    /// 右键点击：切换半自动/手动模式。
    /// </summary>
    public async Task OnRightClick(PlayerChoiceContext context)
    {
        await RelicInteractionSyncService.RequestTalismanLocatorMode(this, !_semiAutoMode);
    }

    /// <summary>
    /// 执行同步后的探测仪半自动模式设置。
    /// </summary>
    /// <param name="semiAutoMode">是否为半自动模式。</param>
    /// <param name="syncCurrentCombatActivations">是否由本端继续请求同步当前战斗符咒激发状态。</param>
    /// <returns>异步任务。</returns>
    internal async Task RunSyncedSetSemiAutoMode(bool semiAutoMode, bool syncCurrentCombatActivations)
    {
        _semiAutoMode = semiAutoMode;
        MainFile.Logger.Info($"【符咒探测仪】同步切换模式：当前挡位={(_semiAutoMode ? "自动挡" : "手动挡")}，计数器={DisplayAmount}");
        if (syncCurrentCombatActivations)
        {
            await SyncCurrentCombatActivationStates();
        }

        InvokeDisplayAmountChanged();
        Flash();
    }

    /// <summary>
    /// 写入本局选人界面固化的 x 层剧情事件替换池配置。
    /// </summary>
    /// <param name="config">选人界面共享配置。</param>
    internal void SetStoryEventReplacementConfig(StoryEventReplacementConfig config)
    {
        StoryEventReplacementConfig validatedConfig = StoryEventReplacementConfigService.ValidateConfig(config.ActNumber, config.EventNames, fallbackWhenAllInvalid: false);
        HasCustomStoryEventReplacementConfig = true;
        SavedStoryEventActNumber = validatedConfig.ActNumber;
        SavedStoryEventPoolNamesCsv = validatedConfig.ToSavedCsv();
        MainFile.Logger.Info($"【x层剧情事件】已写入本局选人配置：玩家={Owner?.NetId.ToString() ?? "空"}，楼层={SavedStoryEventActNumber}，事件=[{SavedStoryEventPoolNamesCsv}]。");
    }

    /// <summary>
    /// 玩家回合开始时：若为半自动模式，自动激发所有可激发的符咒。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (!_semiAutoMode || Owner == null || player != Owner)
        {
            return;
        }

        await SyncActivationStatesInTurnStart(choiceContext, player, true);
    }

    /// <summary>
    /// 战斗开始前：兜底补发开局随机符咒，并检查十二符咒觉醒（以防 Patch 未能触发）。
    /// </summary>
    public override async Task BeforeCombatStart()
    {
        await StartingTalismanGrantService.TryGrantStartingTalisman(this);
        TalismanAwakeningHelper.CheckAndTriggerAwakening(Owner);
    }

    /// <summary>
    /// 进入房间后兜底补发开局随机符咒，并记录是否已经实际遇到配置兜底目标剧情事件。
    /// </summary>
    /// <param name="room">当前房间。</param>
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        await StartingTalismanGrantService.TryGrantStartingTalisman(this);
        MarkSecondActStoryEventSeenIfNeeded(room);
    }

    /// <summary>
    /// 修改下一次事件：圣主每拥有 1 个符咒，有 3% 概率把事件替换为抢夺符咒事件。
    /// </summary>
    /// <param name="currentEvent">原本即将出现的事件。</param>
    /// <returns>原事件或抢夺符咒事件。</returns>
    public override EventModel ModifyNextEvent(EventModel currentEvent)
    {
        if (Owner == null || Owner.Character is not ShengZhu)
        {
            MainFile.Logger.Info($"【抢夺符咒】事件替换判定：原事件={currentEvent.Id.Entry}，持有者无效或不是圣主，不替换事件。");
            return currentEvent;
        }

        // 2026-06-01 按需求屏蔽恶魔小龙事件的二层及以后强制遇到逻辑，保留普通事件池随机出现。
        // if (ShouldForceDragoEvent(currentEvent))
        // {
        //     MainFile.Logger.Info($"【恶魔小龙事件】强制替换事件：原事件={currentEvent.Id.Entry}，当前章节={Owner.RunState.CurrentActIndex}。");
        //     return ModelDb.Event<DragoEvent>();
        // }

        EventModel storyEvent = TryForceConfiguredActFirstUnknownEvent(currentEvent, out bool hasHandledStoryEvent);
        if (hasHandledStoryEvent)
        {
            return storyEvent;
        }

        if (currentEvent is RobTalismanEvent)
        {
            MainFile.Logger.Info($"【抢夺符咒】事件替换判定：原事件={currentEvent.Id.Entry}，已经是抢夺符咒事件，不重复替换。");
            return currentEvent;
        }

        int talismanCount = RobTalismanEventHelper.CountOwnedTalismans(Owner);
        if (talismanCount <= 0)
        {
            MainFile.Logger.Info($"【抢夺符咒】事件替换判定：原事件={currentEvent.Id.Entry}，符咒数=0，概率=0%，不替换事件。");
            return currentEvent;
        }

        float chance = talismanCount * RobTalismanEventHelper.ChancePerTalisman;
        float roll = Owner.RunState.Rng.Niche.NextFloat();
        bool shouldReplace = roll < chance;
        MainFile.Logger.Info($"【抢夺符咒】事件替换判定：原事件={currentEvent.Id.Entry}，符咒数={talismanCount}，概率={chance:P0}，随机值={roll:F4}，结果={(shouldReplace ? "替换" : "保留原事件")}");

        return shouldReplace ? ModelDb.Event<RobTalismanEvent>() : currentEvent;
    }

    /// <summary>
    /// 配置楼层首次进入问号事件时，强制兜底为配置池里的剧情事件之一。
    /// </summary>
    /// <param name="currentEvent">原本即将出现的事件。</param>
    /// <param name="hasHandledStoryEvent">已经完成 x 层剧情事件处理时返回 true，阻止低优先级事件继续替换。</param>
    /// <returns>原事件或兜底剧情事件。</returns>
    private EventModel TryForceConfiguredActFirstUnknownEvent(EventModel currentEvent, out bool hasHandledStoryEvent)
    {
        hasHandledStoryEvent = false;
        if (!ShouldForceConfiguredActStoryEvent(currentEvent))
        {
            return currentEvent;
        }

        if (IsConfiguredStoryEvent(currentEvent))
        {
            MarkSecondActStoryEventSeen("自然生成目标事件", currentEvent);
            MainFile.Logger.Info($"【x层剧情事件】首个问号房已经自然生成目标事件：事件={currentEvent.Id.Entry}，不再替换。");
            hasHandledStoryEvent = true;
            return currentEvent;
        }

        int configuredActNumber = GetConfiguredStoryEventActNumber();
        IReadOnlyList<EventModel> candidates = GetAllowedConfiguredStoryEvents();
        EventModel? chosenEvent = StableRandomHelper.PickByStableHash(
            Owner,
            candidates,
            "TalismanLocator.ConfiguredActFirstUnknownEvent",
            storyEvent => storyEvent.Id.Entry,
            configuredActNumber.ToString(),
            currentEvent.Id.Entry);
        if (chosenEvent == null)
        {
            MainFile.Logger.Info($"【x层剧情事件】没有可用目标事件，保留原事件：配置楼层={configuredActNumber}，原事件={currentEvent.Id.Entry}。");
            return currentEvent;
        }

        MainFile.Logger.Info($"【联机同步】来源=符咒探测仪，玩家={Owner?.NetId}/{Owner?.Character.Id.Entry}，动作=x层首个问号房替换事件，配置楼层={configuredActNumber}，候选数={candidates.Count}，候选ID=[{string.Join(",", candidates.Select(storyEvent => storyEvent.Id.Entry))}]，选中={chosenEvent.Id.Entry}，随机源=StableRandomHelper，原事件={currentEvent.Id.Entry}。");
        MarkSecondActStoryEventSeen("强制替换目标事件", chosenEvent);
        hasHandledStoryEvent = true;
        return chosenEvent;
    }

    /// <summary>
    /// 判断当前事件房是否需要执行配置楼层首个问号事件兜底。
    /// </summary>
    /// <param name="currentEvent">原本即将出现的事件。</param>
    /// <returns>需要兜底替换时返回 true。</returns>
    private bool ShouldForceConfiguredActStoryEvent(EventModel currentEvent)
    {
        if (Owner?.RunState is not RunState runState)
        {
            MainFile.Logger.Info($"【x层剧情事件】事件替换判定：原事件={currentEvent.Id.Entry}，当前运行状态无效，不替换。");
            return false;
        }

        StoryEventReplacementConfig config = GetEffectiveStoryEventReplacementConfig();
        if (!config.IsEnabled)
        {
            return false;
        }
        int configuredActNumber = config.ActNumber;
        int configuredActIndex = configuredActNumber - 1;
        if (runState.CurrentActIndex != configuredActIndex)
        {
            return false;
        }

        if (!IsFirstUnknownEventRoomInCurrentAct(runState))
        {
            MainFile.Logger.Info($"【x层剧情事件】事件替换判定：原事件={currentEvent.Id.Entry}，配置楼层={configuredActNumber}，不是该层首个问号事件，不替换。");
            return false;
        }

        if (HasVisitedSecondActStoryEvent(runState))
        {
            MainFile.Logger.Info($"【x层剧情事件】事件替换判定：原事件={currentEvent.Id.Entry}，本局已遇到过目标事件之一，不替换。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 判断当前地图点是否是当前章节第一个实际进入的问号事件房。
    /// </summary>
    /// <param name="runState">当前爬塔状态。</param>
    /// <returns>当前是首个问号事件房时返回 true。</returns>
    private static bool IsFirstUnknownEventRoomInCurrentAct(IRunState runState)
    {
        if (runState.CurrentMapPoint?.PointType != MapPointType.Unknown)
        {
            return false;
        }

        int unknownEventRoomCount = CountUnknownEventRoomsInCurrentAct(runState);
        MapPointHistoryEntry? currentEntry = runState.GetHistoryEntryFor(runState.MapLocation);
        if (currentEntry == null)
        {
            return unknownEventRoomCount == 0;
        }

        if (currentEntry.MapPointType != MapPointType.Unknown)
        {
            return false;
        }

        if (!currentEntry.HasRoomOfType(RoomType.Event))
        {
            MainFile.Logger.Info($"【x层剧情事件】当前问号房历史已创建但房间类型尚未写入，按首个问号事件继续判定：已记录问号事件数={unknownEventRoomCount}。");
            return unknownEventRoomCount == 0;
        }

        return unknownEventRoomCount == 1;
    }

    /// <summary>
    /// 统计当前章节已经进入过的问号事件房数量。
    /// </summary>
    /// <param name="runState">当前爬塔状态。</param>
    /// <returns>问号地图点且实际房间为事件的数量。</returns>
    private static int CountUnknownEventRoomsInCurrentAct(IRunState runState)
    {
        if (runState.CurrentActIndex < 0 || runState.CurrentActIndex >= runState.MapPointHistory.Count)
        {
            return 0;
        }

        return runState.MapPointHistory[runState.CurrentActIndex]
            .Count(entry => entry.MapPointType == MapPointType.Unknown && entry.HasRoomOfType(RoomType.Event));
    }

    /// <summary>
    /// 判断本局是否已经遇到过配置兜底目标事件之一。
    /// </summary>
    /// <param name="runState">当前爬塔状态。</param>
    /// <returns>已经遇到过目标事件时返回 true。</returns>
    private bool HasVisitedSecondActStoryEvent(RunState runState)
    {
        if (HasAnyTalismanLocatorSeenSecondActStoryEvent(runState))
        {
            return true;
        }

        EventModel? visitedStoryEvent = GetConfiguredStoryEvents()
            .FirstOrDefault(storyEvent => runState.VisitedEventIds.Contains(storyEvent.Id));
        if (visitedStoryEvent == null)
        {
            return false;
        }

        MarkSecondActStoryEventSeen("历史事件记录", visitedStoryEvent);
        return true;
    }

    /// <summary>
    /// 判断本局所有符咒探测仪中是否已经记录过配置兜底目标剧情事件。
    /// </summary>
    /// <param name="runState">当前爬塔状态。</param>
    /// <returns>任意探测仪已记录时返回 true。</returns>
    private static bool HasAnyTalismanLocatorSeenSecondActStoryEvent(RunState runState)
    {
        return runState.Players
            .SelectMany(player => player.Relics.OfType<TalismanLocator>())
            .Any(locator => locator.HasSeenSecondActStoryEvent);
    }

    /// <summary>
    /// 如果当前房间是目标剧情事件房，则记录本局已经遇到过目标事件。
    /// </summary>
    /// <param name="room">当前房间。</param>
    private void MarkSecondActStoryEventSeenIfNeeded(AbstractRoom room)
    {
        if (room is EventRoom eventRoom && IsConfiguredStoryEvent(eventRoom.CanonicalEvent))
        {
            MarkSecondActStoryEventSeen("进入事件房", eventRoom.CanonicalEvent);
        }
    }

    /// <summary>
    /// 记录本局已经遇到过配置兜底目标剧情事件，并同步写入同局所有符咒探测仪。
    /// </summary>
    /// <param name="source">记录来源。</param>
    /// <param name="eventModel">目标事件。</param>
    private void MarkSecondActStoryEventSeen(string source, EventModel eventModel)
    {
        if (Owner?.RunState is not RunState runState || !IsConfiguredStoryEvent(eventModel))
        {
            return;
        }

        int markedCount = 0;
        foreach (TalismanLocator locator in runState.Players.SelectMany(player => player.Relics.OfType<TalismanLocator>()))
        {
            if (!locator.HasSeenSecondActStoryEvent)
            {
                markedCount++;
            }

            locator.HasSeenSecondActStoryEvent = true;
        }

        MainFile.Logger.Info($"【x层剧情事件】已记录本局遇到目标事件：来源={source}，事件={eventModel.Id.Entry}，同步探测仪数={markedCount}。");
    }

    /// <summary>
    /// 判断事件是否属于配置兜底目标事件。
    /// </summary>
    /// <param name="eventModel">待判断事件。</param>
    /// <returns>属于目标事件时返回 true。</returns>
    private bool IsConfiguredStoryEvent(EventModel eventModel)
    {
        return GetConfiguredStoryEvents().Any(storyEvent => storyEvent.Id == eventModel.Id);
    }

    /// <summary>
    /// 获取当前运行状态下允许兜底出现的配置剧情事件。
    /// </summary>
    /// <returns>可用目标事件列表。</returns>
    private IReadOnlyList<EventModel> GetAllowedConfiguredStoryEvents()
    {
        if (Owner == null)
        {
            return [];
        }

        return GetConfiguredStoryEvents()
            .Where(storyEvent => storyEvent.IsAllowed(Owner.RunState))
            .ToList();
    }

    /// <summary>
    /// 获取配置楼层首个问号房的兜底剧情事件列表。
    /// </summary>
    /// <returns>配置文件允许的剧情事件列表。</returns>
    private IReadOnlyList<EventModel> GetConfiguredStoryEvents()
    {
        List<EventModel> events = [];
        foreach (string eventName in GetConfiguredStoryEventNames())
        {
            if (!StoryEventFactories.TryGetValue(eventName, out Func<EventModel>? factory))
            {
                continue;
            }

            events.Add(factory());
        }

        return events;
    }

    /// <summary>
    /// 读取配置文件里的剧情事件池；配置缺失或全写错时回退默认池子，空数组则表示关闭该兜底池。
    /// </summary>
    /// <returns>去重后的中文事件名列表。</returns>
    private IReadOnlyList<string> GetConfiguredStoryEventNames()
    {
        IReadOnlyList<string> configuredNames = GetEffectiveStoryEventReplacementConfig().EventNames;
        if (configuredNames.Count == 0)
        {
            MainFile.Logger.Info("【x层剧情事件】配置的事件替换池为空，本局不会强制替换配置楼层首个问号事件。");
            return configuredNames;
        }
        return configuredNames;
    }

    /// <summary>
    /// 读取并校验剧情事件替换楼层；配置缺失时保持旧版本默认第二层。
    /// </summary>
    /// <returns>实际生效的层数，范围 1 到 3。</returns>
    private int GetConfiguredStoryEventActNumber()
    {
        return GetEffectiveStoryEventReplacementConfig().ActNumber;
    }

    /// <summary>
    /// 获取本局实际生效的剧情事件替换池配置。
    /// 优先读取选人界面固化到存档的配置，没有时回退 JSON 默认配置。
    /// </summary>
    /// <returns>剧情事件替换池配置。</returns>
    private StoryEventReplacementConfig GetEffectiveStoryEventReplacementConfig()
    {
        if (HasCustomStoryEventReplacementConfig)
        {
            StoryEventReplacementConfig savedConfig = StoryEventReplacementConfigService.CreateFromSavedFields(SavedStoryEventActNumber, SavedStoryEventPoolNamesCsv);
            MainFile.Logger.Info($"【x层剧情事件】使用本局选人配置：{savedConfig}。");
            return savedConfig;
        }

        StoryEventReplacementConfig jsonConfig = StoryEventReplacementConfigService.LoadDefaultConfigFromJson();
        MainFile.Logger.Info($"【x层剧情事件】本局没有选人固化配置，使用 JSON 默认配置：{jsonConfig}。");
        return jsonConfig;
    }

    /// <summary>
    /// 从 DLL 同目录或当前工作目录读取 Mod 根配置文件里的剧情事件替换楼层。
    /// </summary>
    /// <returns>读取成功时返回配置楼层；配置文件或字段不存在时返回 null。</returns>
    private static int? LoadConfiguredStoryEventActNumberFromConfig()
    {
        string? configPath = FindModConfigPath();
        if (string.IsNullOrWhiteSpace(configPath))
        {
            MainFile.Logger.Info("【x层剧情事件】未找到 Mod 配置文件，使用默认事件替换楼层。");
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            if (!TryGetConfiguredStoryEventActNumber(document.RootElement, out int actNumber))
            {
                MainFile.Logger.Info($"【x层剧情事件】配置文件未填写 {ConfiguredStoryEventActNumberConfigKey}，使用默认事件替换楼层 {DefaultStoryEventActNumber}：路径={configPath}。");
                return null;
            }

            MainFile.Logger.Info($"【x层剧情事件】已读取配置事件替换楼层：路径={configPath}，楼层={actNumber}。");
            return actNumber;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【x层剧情事件】读取配置事件替换楼层失败，使用默认楼层 {DefaultStoryEventActNumber}：路径={configPath}，异常={ex.Message}。");
            return null;
        }
    }

    /// <summary>
    /// 从 DLL 同目录或当前工作目录读取 Mod 根配置文件里的剧情事件中文数组。
    /// </summary>
    /// <returns>读取成功时返回配置数组；配置文件或字段不存在时返回 null。</returns>
    private static IReadOnlyList<string>? LoadConfiguredStoryEventNamesFromConfig()
    {
        string? configPath = FindModConfigPath();
        if (string.IsNullOrWhiteSpace(configPath))
        {
            MainFile.Logger.Info("【x层剧情事件】未找到 Mod 配置文件，使用默认事件替换池。");
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            if (!TryGetConfiguredStoryEventPoolProperty(document.RootElement, out JsonElement poolElement))
            {
                MainFile.Logger.Info($"【x层剧情事件】配置文件未填写 {ConfiguredStoryEventPoolConfigKey}，使用默认事件替换池：路径={configPath}。");
                return null;
            }

            if (poolElement.ValueKind != JsonValueKind.Array)
            {
                MainFile.Logger.Info($"【x层剧情事件】{ConfiguredStoryEventPoolConfigKey} 不是数组，使用默认事件替换池：路径={configPath}。");
                return null;
            }

            List<string> eventNames = [];
            foreach (JsonElement item in poolElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    string? eventName = item.GetString();
                    if (!string.IsNullOrWhiteSpace(eventName))
                    {
                        eventNames.Add(eventName.Trim());
                    }
                }
                else
                {
                    MainFile.Logger.Info($"【x层剧情事件】事件替换池里存在非文本项，已跳过：类型={item.ValueKind}。");
                }
            }

            MainFile.Logger.Info($"【x层剧情事件】已读取配置事件替换池：路径={configPath}，配置=[{string.Join("、", eventNames)}]。");
            return eventNames;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【x层剧情事件】读取配置文件失败，使用默认事件替换池：路径={configPath}，异常={ex.Message}。");
            return null;
        }
    }

    /// <summary>
    /// 从配置根节点里读取剧情事件替换楼层字段。
    /// </summary>
    /// <param name="rootElement">配置根节点。</param>
    /// <param name="actNumber">读取到的层数。</param>
    /// <returns>字段存在且类型正确时返回 true。</returns>
    private static bool TryGetConfiguredStoryEventActNumber(JsonElement rootElement, out int actNumber)
    {
        actNumber = DefaultStoryEventActNumber;
        if (!rootElement.TryGetProperty(ConfiguredStoryEventActNumberConfigKey, out JsonElement actNumberElement))
        {
            return false;
        }

        if (actNumberElement.ValueKind == JsonValueKind.Number && actNumberElement.TryGetInt32(out actNumber))
        {
            return true;
        }

        MainFile.Logger.Info($"【x层剧情事件】{ConfiguredStoryEventActNumberConfigKey} 不是整数，使用默认楼层 {DefaultStoryEventActNumber}。");
        actNumber = DefaultStoryEventActNumber;
        return false;
    }

    /// <summary>
    /// 从配置根节点里读取剧情事件池字段，优先中文新字段，兼容旧二层字段和英文备用字段。
    /// </summary>
    /// <param name="rootElement">配置根节点。</param>
    /// <param name="poolElement">读取到的事件池数组节点。</param>
    /// <returns>找到字段时返回 true。</returns>
    private static bool TryGetConfiguredStoryEventPoolProperty(JsonElement rootElement, out JsonElement poolElement)
    {
        return rootElement.TryGetProperty(ConfiguredStoryEventPoolConfigKey, out poolElement)
            || rootElement.TryGetProperty(LegacySecondActStoryEventPoolConfigKey, out poolElement)
            || rootElement.TryGetProperty(ConfiguredStoryEventPoolFallbackConfigKey, out poolElement);
    }

    /// <summary>
    /// 查找可编辑的 Mod 根配置文件，优先读取当前 DLL 所在目录，方便发布后直接修改。
    /// </summary>
    /// <returns>配置文件完整路径；找不到时返回 null。</returns>
    private static string? FindModConfigPath()
    {
        string fileName = $"{MainFile.ModId}.json";
        string? assemblyPath = typeof(MainFile).Assembly.Location;
        string? assemblyDirectory = string.IsNullOrWhiteSpace(assemblyPath) ? null : Path.GetDirectoryName(assemblyPath);
        string[] candidateDirectories =
        [
            assemblyDirectory ?? string.Empty,
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        ];

        foreach (string directory in candidateDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            string path = Path.Combine(directory, fileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// 判断是否应按一代逻辑强制进入恶魔小龙事件：圣主二层及以后、未持有合作且当前不是恶魔小龙事件。
    /// </summary>
    /// <param name="currentEvent">原本即将出现的事件。</param>
    /// <returns>需要强制替换时返回 true。</returns>
    private bool ShouldForceDragoEvent(EventModel currentEvent)
    {
        if (Owner == null || currentEvent is DragoEvent || DragoEventHelper.HasCollaboration(Owner))
        {
            return false;
        }

        return Owner.RunState.CurrentActIndex > 0;
    }

    /// <summary>
    /// 在探测仪切换挡位时，立刻同步当前战斗中的符咒激发状态。
    /// 自动挡会激发所有未冷却的可激发符咒，手动挡会取消所有已激发的可激发符咒。
    /// </summary>
    private async Task SyncCurrentCombatActivationStates()
    {
        if (Owner == null)
        {
            MainFile.Logger.Info("【符咒探测仪】当前没有持有者，跳过切挡后的符咒同步。");
            return;
        }

        if (Owner.PlayerCombatState == null)
        {
            MainFile.Logger.Info($"【符咒探测仪】当前不在战斗中，仅切换挡位，不同步符咒激发状态：当前挡位={(_semiAutoMode ? "自动挡" : "手动挡")}");
            return;
        }

        await SyncActivationStates(Owner, _semiAutoMode);
        MainFile.Logger.Info($"【符咒探测仪】切挡后已同步符咒状态：当前挡位={(_semiAutoMode ? "自动挡" : "手动挡")}");
    }

    /// <summary>
    /// 同步所有符咒的激发状态。
    /// </summary>
    private static async Task SyncActivationStatesInTurnStart(PlayerChoiceContext choiceContext, Player player, bool activate)
    {
        MainFile.Logger.Info($"【联机同步】【符咒探测仪】来源=回合开始Hook，玩家={player.NetId}，动作=半自动符咒内联结算，激发={activate}。");

        RatTalisman? rat = player.Relics.OfType<RatTalisman>().FirstOrDefault();
        if (activate && rat != null)
        {
            await rat.RunSyncedReplaceCurseOrStatus(choiceContext, true);
        }

        OxTalisman? ox = player.Relics.OfType<OxTalisman>().FirstOrDefault();
        if (ox != null)
        {
            ox.SetActivated(activate);
        }

        SnakeTalisman? snake = player.Relics.OfType<SnakeTalisman>().FirstOrDefault();
        if (snake != null)
        {
            snake.SetActivated(activate);
        }

        PigTalisman? pig = player.Relics.OfType<PigTalisman>().FirstOrDefault();
        if (pig != null)
        {
            pig.SetActivated(activate);
        }

        SheepTalisman? sheep = player.Relics.OfType<SheepTalisman>().FirstOrDefault();
        if (sheep != null)
        {
            sheep.SetActivated(activate);
        }

        RabbitTalisman? rabbit = player.Relics.OfType<RabbitTalisman>().FirstOrDefault();
        if (rabbit != null)
        {
            rabbit.SetActivated(activate);
        }

        HorseTalisman? horse = player.Relics.OfType<HorseTalisman>().FirstOrDefault();
        if (activate && horse != null)
        {
            await horse.RunSyncedRemoveDebuff(choiceContext, true);
        }
    }

    /// <summary>
    /// 通过遗物联机服务同步所有符咒的激发状态。
    /// </summary>
    private static async Task SyncActivationStates(Player player, bool activate)
    {
        RatTalisman? rat = player.Relics.OfType<RatTalisman>().FirstOrDefault();
        if (activate && rat != null)
        {
            await RelicInteractionSyncService.RequestRatUse(rat, true);
        }

        OxTalisman? ox = player.Relics.OfType<OxTalisman>().FirstOrDefault();
        if (ox != null)
        {
            await RelicInteractionSyncService.RequestSetActivated(ox, activate);
        }

        SnakeTalisman? snake = player.Relics.OfType<SnakeTalisman>().FirstOrDefault();
        if (snake != null)
        {
            await RelicInteractionSyncService.RequestSetActivated(snake, activate);
        }

        PigTalisman? pig = player.Relics.OfType<PigTalisman>().FirstOrDefault();
        if (pig != null)
        {
            await RelicInteractionSyncService.RequestSetActivated(pig, activate);
        }

        SheepTalisman? sheep = player.Relics.OfType<SheepTalisman>().FirstOrDefault();
        if (sheep != null)
        {
            await RelicInteractionSyncService.RequestSetActivated(sheep, activate);
        }

        RabbitTalisman? rabbit = player.Relics.OfType<RabbitTalisman>().FirstOrDefault();
        if (rabbit != null)
        {
            await RelicInteractionSyncService.RequestSetActivated(rabbit, activate);
        }

        HorseTalisman? horse = player.Relics.OfType<HorseTalisman>().FirstOrDefault();
        if (activate && horse != null)
        {
            await TriggerHorseTalismanAuto(horse);
        }
    }

    /// <summary>
    /// 非回合开始路径触发马符咒自动挡。切挡即时同步没有 Hook 选择上下文，仍走通用遗物同步。
    /// </summary>
    /// <param name="horse">马符咒。</param>
    /// <returns>异步任务。</returns>
    private static async Task TriggerHorseTalismanAuto(HorseTalisman horse)
    {
        await RelicInteractionSyncService.RequestHorseUse(horse, true);
    }

    /// <summary>
    /// 奖励阶段前置改写：尝试把普通遗物奖励改造成与随机符咒互斥二选一。
    /// </summary>
    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        return TryInjectTalismanChoice(player, rewards, room);
    }

    /// <summary>
    /// 奖励阶段后置改写：再次兜底尝试注入互斥二选一。
    /// </summary>
    public override bool TryModifyRewardsLate(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        return TryInjectTalismanChoice(player, rewards, room);
    }

    /// <summary>
    /// 向奖励中注入"普通遗物/随机符咒"互斥二选一。
    /// </summary>
    private bool TryInjectTalismanChoice(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player == null || rewards == null)
        {
            return false;
        }

        if (Owner == null || player.NetId != Owner.NetId)
        {
            MainFile.Logger.Info($"【符咒探测仪】跳过非持有者奖励改写：奖励玩家={player.NetId}，持有者={Owner?.NetId.ToString() ?? "空"}。");
            return false;
        }

        if (room is not CombatRoom combatRoom)
        {
            MainFile.Logger.Info($"【符咒探测仪】当前奖励不是战斗结算，跳过符咒二选一：房间={room?.GetType().Name ?? "空"}");
            return false;
        }

        if (combatRoom.ParentEventId != null)
        {
            MainFile.Logger.Info($"【符咒探测仪】当前战斗来自事件，跳过符咒二选一：事件={combatRoom.ParentEventId}");
            return false;
        }

        // 检查奖励中是否已包含符咒选择
        if (ContainsTalismanChoice(rewards))
        {
            return false;
        }

        if (ShouldOfferBossTalismanReward(player, combatRoom))
        {
            return TryInjectBossTalismanReward(player, rewards, room);
        }

        if (rewards.Count == 0)
        {
            return false;
        }

        // 查找第一个普通遗物奖励
        int relicRewardIndex = FindFirstNormalRelicRewardIndex(rewards);
        if (relicRewardIndex < 0)
        {
            MainFile.Logger.Info("【符咒探测仪】本次战斗结算没有可替换的普通遗物奖励。");
            return false;
        }

        if (rewards[relicRewardIndex] is not RelicReward normalRelicReward)
        {
            return false;
        }

        RelicModel? normalRewardRelic = GetRewardRelic(normalRelicReward);
        if (normalRewardRelic == null)
        {
            MainFile.Logger.Info("【符咒探测仪】无法读取原本遗物奖励，跳过符咒二选一。");
            return false;
        }

        // 获取一个玩家未拥有的稳定随机符咒；同一场奖励页 SL 后仍会选到同一个符咒。
        RelicModel? randomTalisman = GetStableMissingTalisman(player, normalRewardRelic, room);
        if (randomTalisman == null)
        {
            MainFile.Logger.Info("【符咒探测仪】玩家已拥有全部符咒，保留原本遗物奖励。");
            return false; // 所有符咒都已拥有
        }

        // 创建互斥二选一
        RelicReward talismanReward = new(randomTalisman, player);
        LinkedRewardSet linkedRewardSet = new([normalRelicReward, talismanReward], player);
        rewards[relicRewardIndex] = linkedRewardSet;
        MainFile.Logger.Info($"【符咒探测仪】已把战斗结算遗物奖励改为二选一：原遗物={normalRewardRelic.Id.Entry}，稳定符咒={randomTalisman.Id.Entry}，已拥有符咒数={CountOwnedTalismans()}");
        Flash();
        return true;
    }

    /// <summary>
    /// 向 Boss 战结算中追加一个稳定缺失符咒奖励；符咒集齐后改为最大生命补偿。
    /// </summary>
    /// <param name="player">当前奖励玩家。</param>
    /// <param name="rewards">当前奖励列表。</param>
    /// <param name="room">当前房间。</param>
    /// <returns>成功追加时返回 true。</returns>
    private bool TryInjectBossTalismanReward(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        string rewardContext = GetBossTalismanRewardContext(player, room);
        RelicModel? randomTalisman = GetStableMissingTalisman(player, rewardContext, room);
        Reward reward = randomTalisman == null
            ? new MaxHpReward(5, player)
            : new RelicReward(randomTalisman, player);

        rewards.Add(reward);
        MainFile.Logger.Info(randomTalisman == null
            ? $"【符咒探测仪】Boss战后已追加最大生命补偿：玩家={player.NetId}，最大生命+5，已拥有符咒数={CountOwnedTalismans()}，上下文={rewardContext}。"
            : $"【符咒探测仪】Boss战后已追加稳定缺失符咒：玩家={player.NetId}，符咒={randomTalisman.Id.Entry}，已拥有符咒数={CountOwnedTalismans()}，上下文={rewardContext}。");
        Flash();
        return true;
    }

    /// <summary>
    /// 获取一个玩家未拥有的稳定随机符咒遗物。
    /// </summary>
    private static RelicModel? GetStableMissingTalisman(Player player, RelicModel normalRewardRelic, AbstractRoom? room)
    {
        return GetStableMissingTalisman(player, normalRewardRelic.Id.ToString(), room);
    }

    /// <summary>
    /// 获取一个玩家未拥有的稳定随机符咒遗物。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="rewardContext">奖励上下文；Boss奖励没有原遗物时使用固定上下文。</param>
    /// <param name="room">当前房间。</param>
    /// <returns>稳定缺失符咒；符咒集齐时返回 null。</returns>
    private static RelicModel? GetStableMissingTalisman(Player player, string rewardContext, AbstractRoom? room)
    {
        List<Type> missingTypes = [];

        foreach (Type talismanType in AllTalismanTypes)
        {
            bool owned = player.Relics.Any(r => r.GetType() == talismanType);
            if (!owned)
            {
                missingTypes.Add(talismanType);
            }
        }

        if (missingTypes.Count == 0)
        {
            return null;
        }

        int chosenIndex = GetStableTalismanIndex(player, rewardContext, room, missingTypes);
        Type chosenType = missingTypes[chosenIndex];
        return ModelDb.AllRelics.FirstOrDefault(r => r.GetType() == chosenType)?.ToMutable();
    }

    /// <summary>
    /// 根据本局种子、当前楼层、原本遗物和缺失符咒列表计算稳定下标，避免 SL 后重新摇出不同符咒。
    /// </summary>
    private static int GetStableTalismanIndex(Player player, string rewardContext, AbstractRoom? room, IReadOnlyList<Type> missingTypes)
    {
        uint hash = 2166136261u;
        AddStableHash(ref hash, player.RunState.Rng.StringSeed);
        AddStableHash(ref hash, player.NetId.ToString());
        AddStableHash(ref hash, player.RunState.CurrentActIndex.ToString());
        AddStableHash(ref hash, player.RunState.ActFloor.ToString());
        AddStableHash(ref hash, player.RunState.TotalFloor.ToString());
        AddStableHash(ref hash, player.RunState.CurrentMapCoord?.col.ToString() ?? "NO_COL");
        AddStableHash(ref hash, player.RunState.CurrentMapCoord?.row.ToString() ?? "NO_ROW");
        AddStableHash(ref hash, room?.RoomType.ToString() ?? "NO_ROOM");
        AddStableHash(ref hash, rewardContext);

        foreach (Type missingType in missingTypes)
        {
            AddStableHash(ref hash, missingType.FullName ?? missingType.Name);
        }

        return (int)(hash % (uint)missingTypes.Count);
    }

    /// <summary>
    /// 使用固定 FNV-1a 算法追加字符串，避免 .NET 字符串哈希在不同进程中变化。
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
    /// 统计玩家已拥有的符咒数量。
    /// </summary>
    private int CountOwnedTalismans()
    {
        if (Owner == null)
        {
            return 0;
        }

        int count = 0;
        foreach (Type talismanType in AllTalismanTypes)
        {
            if (Owner.Relics.Any(r => r.GetType() == talismanType))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 判断当前 Boss 战是否应该发放符咒 Boss 奖励。
    /// </summary>
    /// <param name="player">当前奖励玩家。</param>
    /// <param name="combatRoom">当前战斗房间。</param>
    /// <returns>需要发放时返回 true。</returns>
    protected static bool ShouldOfferBossTalismanReward(Player player, CombatRoom combatRoom)
    {
        if (combatRoom.RoomType != RoomType.Boss)
        {
            return false;
        }

        if (player.RunState.CurrentMapPoint?.PointType != MapPointType.Boss || player.RunState.CurrentMapCoord == null)
        {
            MainFile.Logger.Info($"【符咒Boss奖励】当前战斗不是地图上的Boss房，跳过：玩家={player.NetId}，房间类型={combatRoom.RoomType}，地图点={player.RunState.CurrentMapPoint?.PointType.ToString() ?? "空"}。");
            return false;
        }

        if (!IsCurrentPointFinalBoss(player))
        {
            return true;
        }

        MainFile.Logger.Info($"【符咒Boss奖励】当前Boss为最终通关Boss，跳过奖励：玩家={player.NetId}，章节={player.RunState.CurrentActIndex + 1}，坐标={FormatCurrentMapCoord(player)}。");
        return false;
    }

    /// <summary>
    /// 判断当前地图点是否是整局最后一个 Boss 点。
    /// </summary>
    /// <param name="player">当前奖励玩家。</param>
    /// <returns>当前点是最后通关 Boss 时返回 true。</returns>
    private static bool IsCurrentPointFinalBoss(Player player)
    {
        if (player.RunState.CurrentActIndex < player.RunState.Acts.Count - 1)
        {
            return false;
        }

        MapCoord? currentCoord = player.RunState.CurrentMapCoord;
        if (currentCoord == null)
        {
            return false;
        }

        MapPoint finalBossPoint = player.RunState.Map.SecondBossMapPoint ?? player.RunState.Map.BossMapPoint;
        return IsSameCoord(currentCoord.Value, finalBossPoint.coord);
    }

    /// <summary>
    /// 生成 Boss 符咒奖励的稳定上下文，保证同一奖励页 SL 后候选不变化。
    /// </summary>
    /// <param name="player">当前奖励玩家。</param>
    /// <param name="room">当前房间。</param>
    /// <returns>稳定上下文文本。</returns>
    protected static string GetBossTalismanRewardContext(Player player, AbstractRoom? room)
    {
        string roomModelId = (room?.ModelId as object)?.ToString() ?? "NO_ROOM_MODEL";
        return $"BossTalismanReward:{player.RunState.CurrentActIndex}:{FormatCurrentMapCoord(player)}:{roomModelId}";
    }

    /// <summary>
    /// 格式化当前地图坐标，用于稳定随机上下文和联机日志。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>坐标文本。</returns>
    private static string FormatCurrentMapCoord(Player player)
    {
        MapCoord? coord = player.RunState.CurrentMapCoord;
        return coord == null ? "NO_COORD" : $"{coord.Value.col}:{coord.Value.row}";
    }

    /// <summary>
    /// 判断两个地图坐标是否相同。
    /// </summary>
    /// <param name="left">左侧坐标。</param>
    /// <param name="right">右侧坐标。</param>
    /// <returns>坐标相同时返回 true。</returns>
    private static bool IsSameCoord(MapCoord left, MapCoord right)
    {
        return left.col == right.col && left.row == right.row;
    }

    /// <summary>
    /// 查找第一个可替换的普通遗物奖励位置。
    /// </summary>
    private static int FindFirstNormalRelicRewardIndex(IReadOnlyList<Reward> rewards)
    {
        for (int i = 0; i < rewards.Count; i++)
        {
            if (rewards[i] is not RelicReward relicReward)
            {
                continue;
            }

            RelicModel? rewardRelic = GetRewardRelic(relicReward);
            if (rewardRelic != null && !IsTalismanRelic(rewardRelic))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 判断奖励列表中是否已经包含符咒选择。
    /// </summary>
    private static bool ContainsTalismanChoice(IEnumerable<Reward> rewards)
    {
        foreach (Reward reward in rewards)
        {
            if (reward is MaxHpReward)
            {
                return true;
            }

            if (reward is RelicReward relicReward && IsTalismanRelic(GetRewardRelic(relicReward)))
            {
                return true;
            }

            if (reward is LinkedRewardSet linkedRewardSet)
            {
                foreach (Reward childReward in linkedRewardSet.Rewards)
                {
                    if (childReward is MaxHpReward)
                    {
                        return true;
                    }

                    if (childReward is RelicReward linkedRelicReward && IsTalismanRelic(GetRewardRelic(linkedRelicReward)))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 获取遗物奖励当前展示的遗物。
    /// </summary>
    private static RelicModel? GetRewardRelic(RelicReward relicReward)
    {
        return RelicRewardRelicField?.GetValue(relicReward) as RelicModel;
    }

    /// <summary>
    /// 判断指定遗物是否为十二符咒之一。
    /// </summary>
    private static bool IsTalismanRelic(RelicModel? relicModel)
    {
        if (relicModel == null)
        {
            return false;
        }

        return AllTalismanTypes.Contains(relicModel.GetType());
    }
}
