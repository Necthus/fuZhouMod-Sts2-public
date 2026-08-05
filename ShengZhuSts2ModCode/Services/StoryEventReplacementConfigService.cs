using System.Text.Json;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>
/// x层剧情事件替换池配置服务。
/// 负责读取 JSON 默认值、维护选人阶段共享配置，并在开局后把配置固化到探测仪存档字段。
/// </summary>
public static class StoryEventReplacementConfigService
{
    /// <summary>
    /// 根配置文件中的剧情事件替换楼层字段名。
    /// </summary>
    private const string ConfiguredStoryEventActNumberConfigKey = "x层事件替换池楼层";

    /// <summary>
    /// 根配置文件中的 x 层剧情事件池字段名。
    /// </summary>
    private const string ConfiguredStoryEventPoolConfigKey = "x层事件替换池";

    /// <summary>
    /// 旧版二层剧情事件池字段名，仅用于兼容旧配置。
    /// </summary>
    private const string LegacySecondActStoryEventPoolConfigKey = "二层事件替换池";

    /// <summary>
    /// 根配置文件中的剧情事件池英文备用字段名。
    /// </summary>
    private const string ConfiguredStoryEventPoolFallbackConfigKey = "storyEventReplacementPool";

    /// <summary>
    /// 默认剧情事件替换楼层。
    /// </summary>
    private const int DefaultStoryEventActNumber = 2;

    /// <summary>
    /// 剧情事件替换楼层下限。
    /// </summary>
    public const int MinStoryEventActNumber = 1;

    /// <summary>
    /// 剧情事件替换楼层上限。
    /// </summary>
    public const int MaxStoryEventActNumber = 3;

    /// <summary>
    /// UI 可选的全部剧情事件名，顺序固定用于联机和随机稳定性。
    /// </summary>
    public static readonly IReadOnlyList<string> AllSelectableEventNames =
    [
        "远古封印",
        "恶魔小龙",
        "刀龙黑气",
        "西瓦的手镯",
        "岁月史书"
    ];

    /// <summary>
    /// 代码兜底默认池；与当前 ShengZhuSts2Mod.json 默认值保持一致。
    /// </summary>
    private static readonly IReadOnlyList<string> CodeDefaultEventNames =
    [
        "远古封印",
        "恶魔小龙",
        "刀龙黑气",
        "西瓦的手镯"
    ];

    /// <summary>
    /// 当前选人阶段的共享临时配置。
    /// </summary>
    private static StoryEventReplacementConfig _currentSelectionConfig = CreateCodeDefaultConfig();

    /// <summary>
    /// 已冻结、等待写入本局圣主探测仪的配置。
    /// </summary>
    private static StoryEventReplacementConfig? _pendingRunConfig;

    /// <summary>
    /// 选人阶段配置是否已经冻结到本局，冻结后不再接受迟到的 lobby 配置覆盖。
    /// </summary>
    private static bool _isSelectionConfigFrozen;

    /// <summary>
    /// 当前选人阶段共享临时配置。
    /// </summary>
    public static StoryEventReplacementConfig CurrentSelectionConfig => _currentSelectionConfig.Clone();

    /// <summary>
    /// 每次进入选人流程时，用 JSON 默认值初始化共享临时配置。
    /// </summary>
    public static void InitializeSelectionFromJson()
    {
        _currentSelectionConfig = LoadDefaultConfigFromJson();
        _pendingRunConfig = null;
        _isSelectionConfigFrozen = false;
        MainFile.Logger.Info($"【选人事件池】已按 JSON 初始化本次选人共享配置：{_currentSelectionConfig}。");
    }

    /// <summary>
    /// 应用选人阶段共享配置。
    /// </summary>
    /// <param name="config">待应用配置。</param>
    /// <param name="source">配置来源，用于日志。</param>
    public static void ApplySelectionConfig(StoryEventReplacementConfig config, string source)
    {
        if (_isSelectionConfigFrozen)
        {
            MainFile.Logger.Info($"【选人事件池】已冻结本局配置，忽略迟到配置：来源={source}，待应用={config}，当前={_currentSelectionConfig}。");
            return;
        }

        _currentSelectionConfig = ValidateConfig(config.ActNumber, config.EventNames, fallbackWhenAllInvalid: false);
        MainFile.Logger.Info($"【选人事件池】已更新共享配置：来源={source}，{_currentSelectionConfig}。");
    }

    /// <summary>
    /// 读取 JSON 默认配置，配置缺失或错误时使用代码默认值。
    /// </summary>
    /// <returns>默认配置。</returns>
    public static StoryEventReplacementConfig LoadDefaultConfigFromJson()
    {
        string? configPath = FindModConfigPath();
        if (string.IsNullOrWhiteSpace(configPath))
        {
            MainFile.Logger.Info("【选人事件池】未找到 Mod 配置文件，使用代码默认配置。");
            return CreateCodeDefaultConfig();
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            int actNumber = TryGetConfiguredStoryEventActNumber(document.RootElement, out int configuredActNumber)
                ? configuredActNumber
                : DefaultStoryEventActNumber;

            IReadOnlyList<string>? configuredNames = TryReadConfiguredStoryEventNames(document.RootElement, configPath);
            StoryEventReplacementConfig config = configuredNames == null
                ? new StoryEventReplacementConfig(actNumber, CodeDefaultEventNames)
                : new StoryEventReplacementConfig(actNumber, configuredNames);

            StoryEventReplacementConfig validated = ValidateConfig(config.ActNumber, config.EventNames, fallbackWhenAllInvalid: true);
            MainFile.Logger.Info($"【选人事件池】已读取 JSON 默认配置：路径={configPath}，{validated}。");
            return validated;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【选人事件池】读取 JSON 默认配置失败，使用代码默认配置：路径={configPath}，异常={ex.Message}。");
            return CreateCodeDefaultConfig();
        }
    }

    /// <summary>
    /// 从探测仪存档字段创建本局配置。
    /// </summary>
    /// <param name="actNumber">存档楼层。</param>
    /// <param name="eventNamesCsv">存档事件名。</param>
    /// <returns>校验后的本局配置。</returns>
    public static StoryEventReplacementConfig CreateFromSavedFields(int actNumber, string eventNamesCsv)
    {
        string[] names = string.IsNullOrWhiteSpace(eventNamesCsv)
            ? []
            : eventNamesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ValidateConfig(actNumber, names, fallbackWhenAllInvalid: false);
    }

    /// <summary>
    /// 从当前选人配置恢复 JSON 默认值。
    /// </summary>
    /// <returns>JSON 默认配置。</returns>
    public static StoryEventReplacementConfig ResetSelectionToJsonDefault()
    {
        StoryEventReplacementConfig config = LoadDefaultConfigFromJson();
        ApplySelectionConfig(config, "重置默认");
        return config;
    }

    /// <summary>
    /// 根据最终角色列表冻结本局配置；没有圣主时丢弃本次选人配置。
    /// </summary>
    /// <param name="characters">最终开局角色列表。</param>
    public static void PreparePendingRunConfig(IEnumerable<CharacterModel?> characters)
    {
        bool hasShengZhu = characters.Any(character => character != null && (character is ShengZhu || character.Id.Entry == ShengZhu.CharacterId));
        if (!hasShengZhu)
        {
            _pendingRunConfig = null;
            _isSelectionConfigFrozen = true;
            MainFile.Logger.Info("【选人事件池】本局最终没有圣主，丢弃选人阶段共享配置。");
            return;
        }

        _pendingRunConfig = _currentSelectionConfig.Clone();
        _isSelectionConfigFrozen = true;
        MainFile.Logger.Info($"【选人事件池】已冻结本局圣主事件池配置：{_pendingRunConfig}。");
    }

    /// <summary>
    /// 把等待固化的本局配置写入指定探测仪。
    /// </summary>
    /// <param name="locator">符咒探测仪。</param>
    /// <returns>写入成功时返回 true。</returns>
    public static bool TryApplyPendingRunConfig(TalismanLocator locator)
    {
        if (_pendingRunConfig == null)
        {
            return false;
        }

        locator.SetStoryEventReplacementConfig(_pendingRunConfig);
        MainFile.Logger.Info($"【选人事件池】已把本局配置写入符咒探测仪：玩家={locator.Owner?.NetId.ToString() ?? "空"}，{_pendingRunConfig}。");
        return true;
    }

    /// <summary>
    /// 清理已经写入本局遗物的待固化配置。
    /// </summary>
    public static void ClearPendingRunConfig()
    {
        _pendingRunConfig = null;
    }

    /// <summary>
    /// 按固定候选顺序校验配置。
    /// </summary>
    /// <param name="actNumber">目标楼层。</param>
    /// <param name="eventNames">事件中文名。</param>
    /// <param name="fallbackWhenAllInvalid">全部无效时是否回退代码默认池。</param>
    /// <returns>校验后的配置。</returns>
    public static StoryEventReplacementConfig ValidateConfig(int actNumber, IEnumerable<string> eventNames, bool fallbackWhenAllInvalid)
    {
        int clampedActNumber = Math.Clamp(actNumber, MinStoryEventActNumber, MaxStoryEventActNumber);
        if (clampedActNumber != actNumber)
        {
            MainFile.Logger.Info($"【选人事件池】楼层超出范围，已修正为 {clampedActNumber}：原值={actNumber}。");
        }

        HashSet<string> configuredNameSet = eventNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToHashSet();
        List<string> validNames = AllSelectableEventNames
            .Where(configuredNameSet.Contains)
            .ToList();

        foreach (string invalidName in configuredNameSet.Where(name => !AllSelectableEventNames.Contains(name)))
        {
            MainFile.Logger.Info($"【选人事件池】事件名无效，已跳过：{invalidName}。可选项={string.Join("、", AllSelectableEventNames)}。");
        }

        if (validNames.Count == 0 && configuredNameSet.Count > 0 && fallbackWhenAllInvalid)
        {
            MainFile.Logger.Info($"【选人事件池】配置事件池没有任何有效事件，回退代码默认池：{string.Join("、", CodeDefaultEventNames)}。");
            validNames = CodeDefaultEventNames.ToList();
        }

        return new StoryEventReplacementConfig(clampedActNumber, validNames);
    }

    /// <summary>
    /// 创建代码兜底默认配置。
    /// </summary>
    /// <returns>默认配置。</returns>
    private static StoryEventReplacementConfig CreateCodeDefaultConfig()
    {
        return new StoryEventReplacementConfig(DefaultStoryEventActNumber, CodeDefaultEventNames);
    }

    /// <summary>
    /// 从配置根节点读取剧情事件替换楼层。
    /// </summary>
    /// <param name="rootElement">配置根节点。</param>
    /// <param name="actNumber">读取到的楼层。</param>
    /// <returns>字段存在且类型正确时返回 true。</returns>
    private static bool TryGetConfiguredStoryEventActNumber(JsonElement rootElement, out int actNumber)
    {
        actNumber = DefaultStoryEventActNumber;
        if (!rootElement.TryGetProperty(ConfiguredStoryEventActNumberConfigKey, out JsonElement actNumberElement))
        {
            MainFile.Logger.Info($"【选人事件池】配置文件未填写 {ConfiguredStoryEventActNumberConfigKey}，使用默认楼层 {DefaultStoryEventActNumber}。");
            return false;
        }

        if (actNumberElement.ValueKind == JsonValueKind.Number && actNumberElement.TryGetInt32(out actNumber))
        {
            return true;
        }

        MainFile.Logger.Info($"【选人事件池】{ConfiguredStoryEventActNumberConfigKey} 不是整数，使用默认楼层 {DefaultStoryEventActNumber}。");
        return false;
    }

    /// <summary>
    /// 从配置根节点读取剧情事件池。
    /// </summary>
    /// <param name="rootElement">配置根节点。</param>
    /// <param name="configPath">配置文件路径，用于日志。</param>
    /// <returns>读取到的事件名；字段缺失时返回 null。</returns>
    private static IReadOnlyList<string>? TryReadConfiguredStoryEventNames(JsonElement rootElement, string configPath)
    {
        if (!TryGetConfiguredStoryEventPoolProperty(rootElement, out JsonElement poolElement))
        {
            MainFile.Logger.Info($"【选人事件池】配置文件未填写 {ConfiguredStoryEventPoolConfigKey}，使用代码默认事件池：路径={configPath}。");
            return null;
        }

        if (poolElement.ValueKind != JsonValueKind.Array)
        {
            MainFile.Logger.Info($"【选人事件池】{ConfiguredStoryEventPoolConfigKey} 不是数组，使用代码默认事件池：路径={configPath}。");
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
                MainFile.Logger.Info($"【选人事件池】事件替换池里存在非文本项，已跳过：类型={item.ValueKind}。");
            }
        }

        return eventNames;
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
    /// 查找可编辑的 Mod 根配置文件，优先读取当前 DLL 所在目录。
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
}
