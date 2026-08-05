using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Keywords;

/// <summary>
/// 关键字悬浮提示服务：扫描卡牌描述文本，匹配已注册的自定义关键字，生成对应的 HoverTip 列表。
/// </summary>
public static class KeywordTooltipService
{
    /// <summary>
    /// 按卡牌 ID 缓存静态关键字提示，避免鼠标悬浮和选卡目标预览时反复扫描述、跑正则。
    /// </summary>
    private static readonly Dictionary<string, IReadOnlyList<IHoverTip>> StaticTooltipsByCardId = [];

    /// <summary>
    /// 个别卡牌需要保留描述文字，但不应展示某些同名机制词条。
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> ExcludedKeywordIdsByCardId = new()
    {
        ["SHENGZHUSTS2MOD-ANCIENT_RATSO"] = ["Curse"]
    };

    /// <summary>
    /// 为指定卡牌构建自定义关键字的悬浮提示列表。
    /// </summary>
    /// <param name="card">目标卡牌。</param>
    /// <returns>匹配到的关键字 tooltip 列表；无匹配则返回空集合。</returns>
    public static IEnumerable<IHoverTip> BuildTooltips(CardModel card)
    {
        if (ShouldUseRuntimeTooltip(card))
        {
            return BuildTooltipsCore(card, true);
        }

        string cardId = card.Id.Entry;
        bool includeStaticBlackHand = card is BlackHandGangCard;
        if (!StaticTooltipsByCardId.TryGetValue(cardId, out IReadOnlyList<IHoverTip>? cachedTooltips))
        {
            cachedTooltips = BuildTooltipsCore(card, includeStaticBlackHand).ToList();
            StaticTooltipsByCardId[cardId] = cachedTooltips;
        }

        return cachedTooltips;
    }

    /// <summary>
    /// 判断当前卡牌是否需要运行时补充黑手帮提示。
    /// </summary>
    /// <param name="card">目标卡牌。</param>
    /// <returns>需要运行时处理时返回 true。</returns>
    private static bool ShouldUseRuntimeTooltip(CardModel card)
    {
        return card is not BlackHandGangCard
               && BlackHandCardHelper.IsBlackHandCard(card);
    }

    /// <summary>
    /// 实际构建卡牌关键字提示。
    /// </summary>
    /// <param name="card">目标卡牌。</param>
    /// <param name="includeRuntimeBlackHand">是否补充运行时黑手帮提示。</param>
    /// <returns>匹配到的关键字 tooltip 列表；无匹配则返回空集合。</returns>
    private static IEnumerable<IHoverTip> BuildTooltipsCore(CardModel card, bool includeRuntimeBlackHand)
    {
        var keywords = KeywordRegistry.GetAll();
        if (keywords.Count == 0)
        {
            return [];
        }

        // 获取卡牌描述文本并清洗格式标记
        string? rawDescription = GetCardDescription(card);
        if (string.IsNullOrEmpty(rawDescription))
        {
            return [];
        }

        string searchText = CleanDescription(rawDescription);
        var tooltips = new List<IHoverTip>();
        var addedIds = new HashSet<string>();

        // 扫描所有关键字
        foreach (var keyword in keywords)
        {
            if (IsKeywordExcluded(card, keyword))
            {
                continue;
            }

            if (ContainsKeywordName(searchText, keyword))
            {
                AddKeywordTooltip(tooltips, addedIds, keyword);
            }
        }

        if (includeRuntimeBlackHand)
        {
            AddRuntimeBlackHandCardTooltip(card, tooltips, addedIds);
        }

        return tooltips;
    }

    /// <summary>
    /// 为运行时被【捞人】纳入黑手帮体系的卡牌补充黑手帮悬浮提示。
    /// </summary>
    /// <param name="card">目标卡牌。</param>
    /// <param name="tooltips">当前悬浮提示列表。</param>
    /// <param name="addedIds">已添加过的关键字 ID。</param>
    private static void AddRuntimeBlackHandCardTooltip(CardModel card, List<IHoverTip> tooltips, HashSet<string> addedIds)
    {
        if (!BlackHandCardHelper.IsBlackHandCard(card))
        {
            return;
        }

        KeywordInfo? keyword = KeywordRegistry.GetById("BlackHandCard");
        if (keyword != null)
        {
            AddKeywordTooltip(tooltips, addedIds, keyword);
        }
    }

    /// <summary>
    /// 判断当前卡牌是否需要隐藏指定关键字提示。
    /// </summary>
    private static bool IsKeywordExcluded(CardModel card, KeywordInfo keyword)
    {
        if (string.IsNullOrEmpty(keyword.Id))
        {
            return false;
        }

        return ExcludedKeywordIdsByCardId.TryGetValue(card.Id.Entry, out var excludedKeywordIds)
            && excludedKeywordIds.Contains(keyword.Id);
    }

    /// <summary>
    /// 获取卡牌的本地化描述原始文本（不经过 SmartFormat 变量解析）。
    /// </summary>
    private static string? GetCardDescription(CardModel card)
    {
        try
        {
            string key = card.Id.Entry + ".description";
            return GetRawLocText("cards", key);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从缓存的本地化字典中获取原始文本。
    /// </summary>
    private static string? GetRawLocText(string table, string key)
    {
        if (!_initialized || _cardsDict == null || _cardsDict.Count == 0)
        {
            // 重新尝试初始化（可能之前初始化时 pck 还没加载）
            _initialized = false;
            InitializeCardsDict();
        }

        if (table != "cards" || _cardsDict == null)
        {
            return null;
        }

        return _cardsDict.GetValueOrDefault(key);
    }

    private static bool _initialized;
    private static Dictionary<string, string>? _cardsDict;

    /// <summary>
    /// 从 cards.json 文件加载原始本地化文本到内存字典。
    /// </summary>
    private static void InitializeCardsDict()
    {
        _initialized = true;

        try
        {
            // 使用与 KeywordRegistry 相同的语言，确保关键字别名和卡牌描述是同一语言
            string lang = KeywordRegistry.LoadedLanguage;
            string path = $"res://{MainFile.ModId}/localization/{lang}/cards.json";

            if (!Godot.FileAccess.FileExists(path))
            {
                // 回退到中文
                path = $"res://{MainFile.ModId}/localization/zhs/cards.json";
                lang = "zhs";
            }

            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                MainFile.Logger.Info("【关键字提示】无法打开 cards.json");
                return;
            }

            string json = file.GetAsText();
            _cardsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            MainFile.Logger.Info($"【关键字提示】成功从 {lang}/cards.json 加载 {_cardsDict?.Count ?? 0} 条本地化文本");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【关键字提示】加载 cards.json 异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 清洗描述文本中的格式标记，保留纯文本用于关键字匹配。
    /// </summary>
    private static string CleanDescription(string raw)
    {
        string cleaned = raw;

        // 去除 SimpleLoc 启用标记（行首的 #）
        if (cleaned.StartsWith('#'))
        {
            cleaned = cleaned[1..];
        }

        // 去除 SimpleLoc 高亮标记 *
        cleaned = cleaned.Replace("*", "");

        // 去除 BBCode 风格的颜色标记 [gold]...[/gold] [blue]...[/blue] 等
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\[/?[a-zA-Z_]+\]", "");

        // 去除变量占位符 {Damage:diff()} 等
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\{[^}]+\}", "");

        // 去除 Mod 前缀
        cleaned = cleaned.Replace($"{MainFile.ModId}:", "");
        cleaned = cleaned.Replace($"{MainFile.ModId.ToLowerInvariant()}:", "");

        // 去除杀塔1遗留的颜色标记
        cleaned = cleaned.Replace("#y", "").Replace("#b", "").Replace("#r", "").Replace("#g", "").Replace("#p", "");

        return cleaned;
    }

    /// <summary>
    /// 检查清洗后的描述文本是否包含指定关键字的任一名称。
    /// </summary>
    private static bool ContainsKeywordName(string searchText, KeywordInfo keyword)
    {
        if (ContainsSingleName(searchText, keyword.Name))
        {
            return true;
        }

        foreach (string name in keyword.Names)
        {
            if (ContainsSingleName(searchText, name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 检查文本是否包含指定名称。
    /// 对 CJK 字符使用子串匹配，对拉丁字符使用单词边界匹配。
    /// </summary>
    private static bool ContainsSingleName(string searchText, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (ContainsCjk(name))
        {
            return searchText.Contains(name, StringComparison.OrdinalIgnoreCase);
        }

        int index = 0;
        while (index <= searchText.Length - name.Length)
        {
            int foundIndex = searchText.IndexOf(name, index, StringComparison.OrdinalIgnoreCase);
            if (foundIndex < 0)
            {
                return false;
            }

            int endIndex = foundIndex + name.Length;
            bool validStart = foundIndex == 0 || IsWordBoundary(searchText[foundIndex - 1]);
            bool validEnd = endIndex >= searchText.Length || IsWordBoundary(searchText[endIndex]);

            if (validStart && validEnd)
            {
                return true;
            }

            index = foundIndex + 1;
        }

        return false;
    }

    /// <summary>
    /// 将匹配到的关键字添加到 tooltip 列表，支持 EXTRA 关联递归展开。
    /// </summary>
    private static void AddKeywordTooltip(List<IHoverTip> tooltips, HashSet<string> addedIds, KeywordInfo keyword)
    {
        if (string.IsNullOrEmpty(keyword.Id) || !addedIds.Add(keyword.Id))
        {
            return;
        }

        if (!string.IsNullOrEmpty(keyword.Name) && !string.IsNullOrEmpty(keyword.Description))
        {
            string cleanedDesc = CleanTooltipDescription(keyword.Description);
            var titleLocString = new LocString("static_hover_tips", $"KEYWORD_{keyword.Id}.title");
            tooltips.Add(new HoverTip(titleLocString, cleanedDesc, null));
        }

        foreach (string extraId in keyword.Extra)
        {
            var extraKeyword = KeywordRegistry.GetById(extraId);
            if (extraKeyword != null)
            {
                AddKeywordTooltip(tooltips, addedIds, extraKeyword);
            }
        }
    }

    /// <summary>
    /// 清洗 tooltip 描述中的杀塔1颜色标记。
    /// </summary>
    private static string CleanTooltipDescription(string description)
    {
        return description
            .Replace("#y", "")
            .Replace("#b", "")
            .Replace("#r", "")
            .Replace("#g", "")
            .Replace("#p", "")
            .Replace(" NL ", "\n")
            .Replace("NL", "\n");
    }

    private static bool ContainsCjk(string text)
    {
        foreach (char c in text)
        {
            if (c is >= '\u4E00' and <= '\u9FFF' or >= '\u3400' and <= '\u4DBF' or >= '\uF900' and <= '\uFAFF')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWordBoundary(char c)
    {
        return !char.IsLetterOrDigit(c);
    }
}
