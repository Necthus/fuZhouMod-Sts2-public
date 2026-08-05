using System.Text.Json;
using Godot;
using GodotFileAccess = Godot.FileAccess;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Keywords;

/// <summary>
/// 关键字注册表：负责从 card_keywords.json 加载所有自定义关键字，并提供按 ID 查询的能力。
/// </summary>
public static class KeywordRegistry
{
    /// <summary>
    /// 按 ID 索引的关键字字典。
    /// </summary>
    private static readonly Dictionary<string, KeywordInfo> KeywordsById = new();

    /// <summary>
    /// 所有已注册关键字的列表（保持加载顺序）。
    /// </summary>
    private static readonly List<KeywordInfo> AllKeywords = [];

    /// <summary>
    /// 是否已完成初始化。
    /// </summary>
    private static bool _initialized;

    /// <summary>
    /// 初始化关键字注册表：从当前语言的 card_keywords.json 加载数据。
    /// 如果当前语言文件不存在，则回退到中文。
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // 获取当前游戏语言
        string lang = GetCurrentLanguage();
        LoadedLanguage = lang;
        string path = $"res://{MainFile.ModId}/localization/{lang}/keyword_definitions.json";

        // 如果当前语言文件不存在，回退到中文
        if (!GodotFileAccess.FileExists(path))
        {
            MainFile.Logger.Info($"关键字定义文件不存在: {path}，回退到 zhs");
            path = $"res://{MainFile.ModId}/localization/zhs/keyword_definitions.json";
        }

        LoadKeywords(path);
        MainFile.Logger.Info($"关键字注册表初始化完成，共加载 {AllKeywords.Count} 个关键字（语言: {lang}）");
    }

    /// <summary>
    /// 获取所有已注册的关键字列表。
    /// </summary>
    public static IReadOnlyList<KeywordInfo> GetAll()
    {
        return AllKeywords;
    }

    /// <summary>
    /// 按 ID 查询关键字。
    /// </summary>
    /// <param name="id">关键字 ID。</param>
    /// <returns>找到则返回 KeywordInfo，否则返回 null。</returns>
    public static KeywordInfo? GetById(string id)
    {
        return KeywordsById.GetValueOrDefault(id);
    }

    /// <summary>
    /// 从指定路径加载关键字 JSON 文件。
    /// </summary>
    /// <param name="path">Godot 资源路径。</param>
    private static void LoadKeywords(string path)
    {
        KeywordsById.Clear();
        AllKeywords.Clear();

        try
        {
            using var file = GodotFileAccess.Open(path, GodotFileAccess.ModeFlags.Read);
            if (file == null)
            {
                MainFile.Logger.Info($"无法打开关键字文件: {path}，错误: {GodotFileAccess.GetOpenError()}");
                return;
            }

            string json = file.GetAsText();
            var keywords = JsonSerializer.Deserialize<KeywordJsonEntry[]>(json, JsonOptions);

            if (keywords == null)
            {
                MainFile.Logger.Info($"关键字文件解析结果为空: {path}");
                return;
            }

            foreach (var entry in keywords)
            {
                var info = new KeywordInfo
                {
                    Id = entry.ID ?? "",
                    Name = entry.PROPER_NAME ?? "",
                    Names = entry.NAMES ?? [],
                    Description = entry.DESCRIPTION ?? "",
                    Extra = entry.EXTRA ?? []
                };

                if (!string.IsNullOrEmpty(info.Id))
                {
                    KeywordsById[info.Id] = info;
                }

                AllKeywords.Add(info);
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"加载关键字文件异常: {path}，错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取当前游戏语言代码。
    /// </summary>
    private static string GetCurrentLanguage()
    {
        string locale = TranslationServer.GetLocale();

        // 映射常见 locale 到本项目的语言目录名
        return locale switch
        {
            "zh_CN" or "zh" or "zhs" => "zhs",
            _ => "eng"
        };
    }

    /// <summary>
    /// 初始化时确定的语言代码，供其他模块使用（确保 cards.json 和 keyword_definitions.json 加载同一语言）。
    /// </summary>
    public static string LoadedLanguage { get; private set; } = "zhs";

    /// <summary>
    /// JSON 反序列化选项：不区分大小写。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// JSON 文件中的条目结构（与杀塔1的 Keywords.json 格式一致）。
    /// </summary>
    private class KeywordJsonEntry
    {
        public string? ID { get; set; }
        public string? PROPER_NAME { get; set; }
        public string[]? NAMES { get; set; }
        public string? DESCRIPTION { get; set; }
        public string[]? EXTRA { get; set; }
    }
}
