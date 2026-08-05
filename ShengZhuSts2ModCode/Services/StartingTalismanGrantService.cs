using System.Text.Json;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>
/// 圣主开局符咒发放服务：按配置从符咒候选池中稳定随机补发，保证 SL 和联机各端结果一致。
/// </summary>
public static class StartingTalismanGrantService
{
    /// <summary>
    /// 根配置文件中的开局随机符咒数量字段名，允许 0 到 12。
    /// </summary>
    private const string StartingTalismanCountConfigKey = "开局随机符咒数量";

    /// <summary>
    /// 根配置文件中的开局随机符咒黑名单字段名，数组里直接写中文符咒名。
    /// </summary>
    private const string StartingTalismanBlacklistConfigKey = "开局随机符咒黑名单";

    /// <summary>
    /// 默认开局随机符咒数量；保持旧版本默认开局补发 1 个符咒。
    /// </summary>
    private const int DefaultStartingTalismanCount = 1;

    /// <summary>
    /// 开局随机符咒数量下限。
    /// </summary>
    private const int MinStartingTalismanCount = 0;

    /// <summary>
    /// 开局随机符咒数量上限。
    /// </summary>
    private const int MaxStartingTalismanCount = 12;

    /// <summary>
    /// 所有可配置参与开局随机的符咒类型。
    /// </summary>
    private static readonly Type[] AllStartingTalismanTypes =
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
    /// 默认开局随机符咒黑名单；保持旧版本默认不随机鼠符咒。
    /// </summary>
    private static readonly string[] DefaultStartingTalismanBlacklistNames =
    [
        "鼠符咒"
    ];

    /// <summary>
    /// 中文符咒名到符咒类型的映射，配置黑名单只能填写这些中文名。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Type> TalismanTypesByChineseName = new Dictionary<string, Type>
    {
        ["鼠符咒"] = typeof(RatTalisman),
        ["牛符咒"] = typeof(OxTalisman),
        ["虎符咒"] = typeof(TigerTalisman),
        ["兔符咒"] = typeof(RabbitTalisman),
        ["龙符咒"] = typeof(DragonTalisman),
        ["蛇符咒"] = typeof(SnakeTalisman),
        ["马符咒"] = typeof(HorseTalisman),
        ["羊符咒"] = typeof(SheepTalisman),
        ["猴符咒"] = typeof(MonkeyTalisman),
        ["鸡符咒"] = typeof(RoosterTalisman),
        ["狗符咒"] = typeof(DogTalisman),
        ["猪符咒"] = typeof(PigTalisman)
    };

    /// <summary>
    /// 符咒类型到中文符咒名的映射，用于日志和存档追踪。
    /// </summary>
    private static readonly IReadOnlyDictionary<Type, string> TalismanChineseNamesByType = TalismanTypesByChineseName.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>
    /// 尝试为符咒探测仪持有者补发开局随机符咒；成功、关闭或无候选后都会写入探测仪存档状态。
    /// </summary>
    /// <param name="locator">触发补发的符咒探测仪。</param>
    /// <returns>异步任务。</returns>
    public static async Task TryGrantStartingTalisman(TalismanLocator locator)
    {
        if (locator.HasGrantedStartingTalisman)
        {
            return;
        }

        Player? player = locator.Owner;
        if (player == null)
        {
            MainFile.Logger.Info("【开局符咒】符咒探测仪没有持有者，暂不补发。");
            return;
        }

        if (player.Character is not ShengZhu)
        {
            MainFile.Logger.Info($"【开局符咒】持有者不是圣主，跳过补发：玩家={player.NetId}/{player.Character.Id.Entry}。");
            return;
        }

        StartingTalismanConfig config = GetConfiguredStartingTalismanConfig();
        if (config.Count <= 0)
        {
            locator.HasGrantedStartingTalisman = true;
            locator.GrantedStartingTalismanTypeName = string.Empty;
            MainFile.Logger.Info($"【联机同步】来源=开局符咒，玩家={player.NetId}/{player.Character.Id.Entry}，动作=补发随机符咒，配置数量=0，黑名单=[{string.Join("、", config.BlacklistNames)}]，黑名单后上限={config.MaxGrantCountAfterBlacklist}，结果=配置关闭。");
            return;
        }

        List<Type> candidates = GetMissingStartingTalismanTypes(player, config.BlacklistedTalismanTypes);
        if (candidates.Count == 0)
        {
            locator.HasGrantedStartingTalisman = true;
            locator.GrantedStartingTalismanTypeName = string.Empty;
            MainFile.Logger.Info($"【联机同步】来源=开局符咒，玩家={player.NetId}/{player.Character.Id.Entry}，动作=补发随机符咒，配置数量={config.Count}，黑名单=[{string.Join("、", config.BlacklistNames)}]，黑名单后上限={config.MaxGrantCountAfterBlacklist}，候选数=0，候选ID=[]，选中=无，随机源=稳定FNV，说明=玩家已拥有全部非黑名单符咒或候选全被排除。");
            return;
        }

        int grantCount = Math.Min(config.Count, candidates.Count);
        List<Type> chosenTypes = PickStableStartingTalismanTypes(player, candidates, grantCount);
        if (chosenTypes.Count == 0)
        {
            locator.HasGrantedStartingTalisman = true;
            locator.GrantedStartingTalismanTypeName = string.Empty;
            MainFile.Logger.Info($"【开局符咒】稳定随机后没有选中符咒，跳过补发：玩家={player.NetId}/{player.Character.Id.Entry}。");
            return;
        }

        locator.HasGrantedStartingTalisman = true;
        locator.GrantedStartingTalismanTypeName = string.Join(",", chosenTypes.Select(GetTalismanChineseName));
        string candidateIds = string.Join(",", candidates.Select(GetTalismanChineseName));
        string chosenIds = string.Join(",", chosenTypes.Select(GetTalismanChineseName));
        MainFile.Logger.Info($"【联机同步】来源=开局符咒，玩家={player.NetId}/{player.Character.Id.Entry}，动作=补发随机符咒，配置数量={config.Count}，黑名单=[{string.Join("、", config.BlacklistNames)}]，黑名单后上限={config.MaxGrantCountAfterBlacklist}，候选数={candidates.Count}，实际数量={chosenTypes.Count}，候选ID=[{candidateIds}]，选中=[{chosenIds}]，随机源=稳定FNV。");

        foreach (Type chosenType in chosenTypes)
        {
            RelicModel? talisman = ModelDb.AllRelics.FirstOrDefault(relic => relic.GetType() == chosenType)?.ToMutable();
            if (talisman == null)
            {
                MainFile.Logger.Info($"【开局符咒】未找到符咒原型，无法补发：符咒={GetTalismanChineseName(chosenType)}，类型={chosenType.FullName ?? chosenType.Name}。");
                continue;
            }

            await RelicCmd.Obtain(talisman, player);
        }

        TalismanAwakeningHelper.CheckAndTriggerAwakening(player);
    }

    /// <summary>
    /// 获取玩家尚未拥有且未被黑名单排除的开局符咒候选，并按中文符咒名稳定排序。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="blacklistedTypes">配置排除的符咒类型。</param>
    /// <returns>稳定排序后的候选符咒类型列表。</returns>
    private static List<Type> GetMissingStartingTalismanTypes(Player player, IReadOnlySet<Type> blacklistedTypes)
    {
        return AllStartingTalismanTypes
            .Where(talismanType => !blacklistedTypes.Contains(talismanType))
            .Where(talismanType => !player.Relics.Any(relic => relic.GetType() == talismanType))
            .OrderBy(GetTalismanChineseName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 从候选池中稳定随机选出指定数量的符咒；每次选中后从剩余池移除，避免重复发放。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="candidates">稳定排序后的候选列表。</param>
    /// <param name="count">需要选出的数量。</param>
    /// <returns>稳定选中的符咒类型列表。</returns>
    private static List<Type> PickStableStartingTalismanTypes(Player player, IReadOnlyList<Type> candidates, int count)
    {
        List<Type> remainingCandidates = candidates.ToList();
        List<Type> chosenTypes = [];
        for (int i = 0; i < count && remainingCandidates.Count > 0; i++)
        {
            int chosenIndex = GetStableStartingTalismanIndex(player, remainingCandidates, i);
            chosenTypes.Add(remainingCandidates[chosenIndex]);
            remainingCandidates.RemoveAt(chosenIndex);
        }

        return chosenTypes;
    }

    /// <summary>
    /// 计算开局符咒稳定下标；只依赖局种子、玩家、角色、轮次和候选池，不依赖楼层，避免兜底触发时机影响结果。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="candidates">当前轮次稳定排序后的候选列表。</param>
    /// <param name="pickIndex">当前是第几次选择。</param>
    /// <returns>候选下标。</returns>
    private static int GetStableStartingTalismanIndex(Player player, IReadOnlyList<Type> candidates, int pickIndex)
    {
        uint hash = 2166136261u;
        AddStableHash(ref hash, player.RunState.Rng.StringSeed);
        AddStableHash(ref hash, player.NetId.ToString());
        AddStableHash(ref hash, player.Character.Id.Entry);
        AddStableHash(ref hash, "StartingTalismanGrantService.StartingConfiguredTalisman");
        AddStableHash(ref hash, pickIndex.ToString());
        foreach (Type candidate in candidates)
        {
            AddStableHash(ref hash, GetTalismanChineseName(candidate));
        }

        return (int)(hash % (uint)candidates.Count);
    }

    /// <summary>
    /// 读取并整理开局随机符咒配置；配置缺失时保持旧版本默认值。
    /// </summary>
    /// <returns>开局随机符咒配置。</returns>
    private static StartingTalismanConfig GetConfiguredStartingTalismanConfig()
    {
        RawStartingTalismanConfig rawConfig = LoadStartingTalismanConfigFromConfig() ?? new RawStartingTalismanConfig(
            DefaultStartingTalismanCount,
            DefaultStartingTalismanBlacklistNames);
        int count = Math.Clamp(rawConfig.Count, MinStartingTalismanCount, MaxStartingTalismanCount);
        if (count != rawConfig.Count)
        {
            MainFile.Logger.Info($"【开局符咒配置】{StartingTalismanCountConfigKey} 超出范围，已修正为 {count}：原值={rawConfig.Count}。");
        }

        List<string> blacklistNames = [];
        HashSet<Type> blacklistedTypes = [];
        foreach (string blacklistName in rawConfig.BlacklistNames)
        {
            string trimmedName = blacklistName.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                continue;
            }

            if (!TalismanTypesByChineseName.TryGetValue(trimmedName, out Type? talismanType))
            {
                MainFile.Logger.Info($"【开局符咒配置】黑名单配置项无效，已跳过：{trimmedName}。可选项={string.Join("、", TalismanTypesByChineseName.Keys)}。");
                continue;
            }

            if (blacklistedTypes.Add(talismanType))
            {
                blacklistNames.Add(trimmedName);
            }
        }

        int maxGrantCountAfterBlacklist = Math.Max(MinStartingTalismanCount, AllStartingTalismanTypes.Length - blacklistedTypes.Count);
        return new StartingTalismanConfig(count, blacklistedTypes, blacklistNames, maxGrantCountAfterBlacklist);
    }

    /// <summary>
    /// 从 DLL 同目录或当前工作目录读取 Mod 根配置文件里的开局随机符咒配置。
    /// </summary>
    /// <returns>读取成功时返回原始配置；配置文件或字段不存在时返回 null。</returns>
    private static RawStartingTalismanConfig? LoadStartingTalismanConfigFromConfig()
    {
        string? configPath = FindModConfigPath();
        if (string.IsNullOrWhiteSpace(configPath))
        {
            MainFile.Logger.Info("【开局符咒配置】未找到 Mod 配置文件，使用默认配置。");
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            bool hasCount = TryGetStartingTalismanCount(document.RootElement, out int count);
            bool hasBlacklist = TryGetStartingTalismanBlacklist(document.RootElement, out IReadOnlyList<string> blacklistNames);
            if (!hasCount && !hasBlacklist)
            {
                MainFile.Logger.Info($"【开局符咒配置】配置文件未填写开局随机符咒配置，使用默认配置：路径={configPath}。");
                return null;
            }

            MainFile.Logger.Info($"【开局符咒配置】已读取配置：路径={configPath}，数量={(hasCount ? count : DefaultStartingTalismanCount)}，黑名单=[{string.Join("、", blacklistNames)}]。");
            return new RawStartingTalismanConfig(
                hasCount ? count : DefaultStartingTalismanCount,
                hasBlacklist ? blacklistNames : DefaultStartingTalismanBlacklistNames);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【开局符咒配置】读取配置文件失败，使用默认配置：路径={configPath}，异常={ex.Message}。");
            return null;
        }
    }

    /// <summary>
    /// 从配置根节点读取开局随机符咒数量。
    /// </summary>
    /// <param name="rootElement">配置根节点。</param>
    /// <param name="count">读取到的数量。</param>
    /// <returns>字段存在且类型正确时返回 true。</returns>
    private static bool TryGetStartingTalismanCount(JsonElement rootElement, out int count)
    {
        count = DefaultStartingTalismanCount;
        if (!rootElement.TryGetProperty(StartingTalismanCountConfigKey, out JsonElement countElement))
        {
            return false;
        }

        if (countElement.ValueKind == JsonValueKind.Number && countElement.TryGetInt32(out count))
        {
            return true;
        }

        MainFile.Logger.Info($"【开局符咒配置】{StartingTalismanCountConfigKey} 不是整数，使用默认数量 {DefaultStartingTalismanCount}。");
        count = DefaultStartingTalismanCount;
        return false;
    }

    /// <summary>
    /// 从配置根节点读取开局随机符咒黑名单。
    /// </summary>
    /// <param name="rootElement">配置根节点。</param>
    /// <param name="blacklistNames">读取到的黑名单中文名。</param>
    /// <returns>字段存在且类型正确时返回 true。</returns>
    private static bool TryGetStartingTalismanBlacklist(JsonElement rootElement, out IReadOnlyList<string> blacklistNames)
    {
        blacklistNames = DefaultStartingTalismanBlacklistNames;
        if (!rootElement.TryGetProperty(StartingTalismanBlacklistConfigKey, out JsonElement blacklistElement))
        {
            return false;
        }

        if (blacklistElement.ValueKind != JsonValueKind.Array)
        {
            MainFile.Logger.Info($"【开局符咒配置】{StartingTalismanBlacklistConfigKey} 不是数组，使用默认黑名单。");
            return false;
        }

        List<string> names = [];
        foreach (JsonElement item in blacklistElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? name = item.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name.Trim());
                }
            }
            else
            {
                MainFile.Logger.Info($"【开局符咒配置】黑名单里存在非文本项，已跳过：类型={item.ValueKind}。");
            }
        }

        blacklistNames = names;
        return true;
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
    /// 获取符咒类型的中文名；未知类型时回退类型名，方便日志排查。
    /// </summary>
    /// <param name="talismanType">符咒类型。</param>
    /// <returns>中文符咒名或类型名。</returns>
    private static string GetTalismanChineseName(Type talismanType)
    {
        return TalismanChineseNamesByType.TryGetValue(talismanType, out string? chineseName)
            ? chineseName
            : talismanType.FullName ?? talismanType.Name;
    }

    /// <summary>
    /// 使用固定 FNV-1a 算法追加字符串，避免 .NET 字符串哈希随机盐影响联机同步。
    /// </summary>
    /// <param name="hash">当前哈希值。</param>
    /// <param name="value">追加内容。</param>
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
    /// 已校验整理后的开局随机符咒配置。
    /// </summary>
    /// <param name="Count">实际生效的随机数量。</param>
    /// <param name="BlacklistedTalismanTypes">实际生效的黑名单类型。</param>
    /// <param name="BlacklistNames">实际生效的黑名单中文名。</param>
    /// <param name="MaxGrantCountAfterBlacklist">扣掉有效黑名单后的发放上限。</param>
    private sealed record StartingTalismanConfig(int Count, IReadOnlySet<Type> BlacklistedTalismanTypes, IReadOnlyList<string> BlacklistNames, int MaxGrantCountAfterBlacklist);

    /// <summary>
    /// 从 JSON 直接读出的原始开局随机符咒配置。
    /// </summary>
    /// <param name="Count">配置文件填写的随机数量。</param>
    /// <param name="BlacklistNames">配置文件填写的黑名单中文名。</param>
    private sealed record RawStartingTalismanConfig(int Count, IReadOnlyList<string> BlacklistNames);
}
