using System.Reflection;
using System.Linq;
using System.Text.Json;
using BaseLib.Extensions;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using GodotFileAccess = Godot.FileAccess;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 猴符咒检索栏辅助器。
/// 仅在猴符咒的“七十二变”卡牌选择界面注入检索框，并按输入实时过滤卡牌。
/// </summary>
public static class MonkeyTalismanSearchHelper
{
    /// <summary>
    /// 允许注入检索框的提示词 key。
    /// </summary>
    private static readonly HashSet<string> AllowedPromptKeys =
    [
        "SHENGZHUSTS2MOD-MONKEY_TALISMAN.combatSelectReplacement",
        "OPTION_MONKEY_TALISMAN_TRANSFORM.selectReplacement"
    ];

    /// <summary>
    /// 已注入的检索状态。
    /// </summary>
    private static readonly Dictionary<ulong, SearchState> SearchStates = new();

    /// <summary>
    /// 已加载的卡牌本地化表，检索时同时覆盖中文和英文。
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, string>> CardLocalizationCache = new();

    /// <summary>
    /// 检索时额外读取的语言目录。
    /// </summary>
    private static readonly string[] SearchLanguages = ["zhs", "eng"];

    /// <summary>
    /// JSON 读取配置。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 检索框节点名，避免重复注入。
    /// </summary>
    private const string SearchBarNodeName = "MonkeyTalismanSearchBar";

    /// <summary>
    /// 输入框节点名。
    /// </summary>
    private const string SearchInputNodeName = "MonkeyTalismanSearchInput";

    /// <summary>
    /// 清空按钮节点名。
    /// </summary>
    private const string SearchClearButtonNodeName = "MonkeyTalismanSearchClearButton";

    /// <summary>
    /// 检索栏最大宽度，放在顶栏右侧空白区，避免遮挡遗物和卡牌。
    /// </summary>
    private const float SearchBarMaxWidth = 440f;

    /// <summary>
    /// 检索栏最小宽度，避免低分辨率下输入区域过窄。
    /// </summary>
    private const float SearchBarMinWidth = 260f;

    /// <summary>
    /// 检索栏右侧为地图、卡组和暂停按钮预留的空间。
    /// </summary>
    private const float SearchBarRightPadding = 380f;

    /// <summary>
    /// 检索栏左侧最小安全位置，避开生命、金币、药水和房间图标。
    /// </summary>
    private const float SearchBarLeftSafeX = 720f;

    /// <summary>
    /// 检索栏顶部位置，放进原生顶栏空白区。
    /// </summary>
    private const float SearchBarTopPadding = 16f;

    /// <summary>
    /// 检索栏高度。
    /// </summary>
    private const float SearchBarHeight = 36f;

    /// <summary>
    /// 在选牌网格初始化后，尝试为猴符咒的检索界面注入搜索栏。
    /// </summary>
    /// <param name="screen">选牌界面。</param>
    public static void TryAttachSearchBar(NCardGridSelectionScreen screen)
    {
        if (screen is not NSimpleCardSelectScreen)
        {
            return;
        }

        if (SearchStates.ContainsKey(screen.GetInstanceId()))
        {
            return;
        }

        CardSelectorPrefs? prefs = TryGetPrivateField<CardSelectorPrefs>(screen, "_prefs");
        if (prefs == null || !AllowedPromptKeys.Contains(prefs.Value.Prompt.LocEntryKey))
        {
            return;
        }

        NCardGrid? grid = TryGetPrivateField<NCardGrid>(screen, "_grid");
        IReadOnlyList<CardModel>? cards = TryGetPrivateField<IReadOnlyList<CardModel>>(screen, "_cards");
        if (grid == null || cards == null)
        {
            return;
        }

        Control searchParent = GetSearchBarParent(screen);
        if (searchParent.GetNodeOrNull<Node>(SearchBarNodeName) != null)
        {
            return;
        }

        var searchBar = BuildSearchBar(screen);
        searchParent.AddChild(searchBar);
        searchBar.MoveToFront();

        SearchStates[screen.GetInstanceId()] = new SearchState(grid, cards.ToList(), searchBar);
        ApplyFilter(screen, string.Empty);
    }

    /// <summary>
    /// 选牌界面退出时清理检索状态。
    /// </summary>
    /// <param name="screen">选牌界面。</param>
    public static void RemoveSearchBar(NCardGridSelectionScreen screen)
    {
        ulong id = screen.GetInstanceId();
        if (SearchStates.Remove(id, out SearchState? state))
        {
            state.SearchBar.QueueFree();
        }
    }

    /// <summary>
    /// 根据输入内容过滤卡牌。
    /// </summary>
    /// <param name="screen">选牌界面。</param>
    /// <param name="query">检索文本。</param>
    public static void ApplyFilter(NCardGridSelectionScreen screen, string query)
    {
        ulong id = screen.GetInstanceId();
        if (!SearchStates.TryGetValue(id, out SearchState? state))
        {
            return;
        }

        string normalizedQuery = NSearchBar.Normalize(query ?? string.Empty);
        List<CardModel> filteredCards = string.IsNullOrWhiteSpace(normalizedQuery)
            ? state.OriginalCards.ToList()
            : state.OriginalCards.Where(card => IsCardMatched(card, normalizedQuery)).ToList();

        ClearSelection(screen);
        state.Grid.SetCards(filteredCards, PileType.None, new List<MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.SortingOrders> { MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.SortingOrders.Ascending });
    }

    /// <summary>
    /// 构建搜索栏控件。
    /// </summary>
    /// <param name="screen">选牌界面。</param>
    /// <returns>搜索栏容器。</returns>
    private static Control BuildSearchBar(NCardGridSelectionScreen screen)
    {
        var container = new PanelContainer
        {
            Name = SearchBarNodeName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 200
        };
        container.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        UpdateSearchBarLayout(screen, container);
        screen.Connect(Control.SignalName.Resized, Callable.From(() => UpdateSearchBarLayout(screen, container)));

        var row = new HBoxContainer
        {
            Name = "MonkeyTalismanSearchRow",
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        row.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        row.AddThemeConstantOverride("separation", 8);

        var input = new NMegaLineEdit
        {
            Name = SearchInputNodeName,
            PlaceholderText = new LocString("relics", "SHENGZHUSTS2MOD-MONKEY_TALISMAN.searchPlaceholder").GetRawText(),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(140f, 30f)
        };
        input.Connect(LineEdit.SignalName.TextChanged, Callable.From<string>(text => ApplyFilter(screen, text)));
        input.Connect(LineEdit.SignalName.TextSubmitted, Callable.From<string>(text => ApplyFilter(screen, text)));
        container.Connect(Control.SignalName.GuiInput, Callable.From<InputEvent>(inputEvent =>
        {
            if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                input.TryGrabFocus();
            }
        }));

        var clearButton = new Button
        {
            Name = SearchClearButtonNodeName,
            Text = new LocString("relics", "SHENGZHUSTS2MOD-MONKEY_TALISMAN.searchClear").GetRawText(),
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(72f, 28f)
        };
        clearButton.Connect(Button.SignalName.ButtonUp, Callable.From(delegate
        {
            input.TryGrabFocus();
            input.Text = string.Empty;
            ApplyFilter(screen, string.Empty);
            input.TryGrabFocus();
        }));

        row.AddChild(input);
        row.AddChild(clearButton);
        container.AddChild(row);

        return container;
    }

    /// <summary>
    /// 按当前视口尺寸重新摆放检索栏，保证不同分辨率下居中且不遮挡底部操作区。
    /// </summary>
    /// <param name="screen">选牌界面。</param>
    /// <param name="container">检索栏容器。</param>
    private static void UpdateSearchBarLayout(NCardGridSelectionScreen screen, Control container)
    {
        float viewportWidth = screen.GetViewportRect().Size.X;
        float availableWidth = Math.Max(180f, viewportWidth - SearchBarRightPadding - SearchBarLeftSafeX);
        float width = Math.Clamp(availableWidth, Math.Min(SearchBarMinWidth, availableWidth), SearchBarMaxWidth);
        float left = Math.Max(SearchBarLeftSafeX, viewportWidth - SearchBarRightPadding - width);

        container.Position = new Vector2(left, SearchBarTopPadding);
        container.CustomMinimumSize = new Vector2(width, SearchBarHeight);
        container.Size = new Vector2(width, SearchBarHeight);
    }

    /// <summary>
    /// 获取检索栏挂载父节点，优先挂到原生顶栏，避免和遗物栏、卡牌网格互相抢鼠标事件。
    /// </summary>
    /// <param name="screen">选牌界面。</param>
    /// <returns>检索栏父节点。</returns>
    private static Control GetSearchBarParent(NCardGridSelectionScreen screen)
    {
        Control? topBar = NRun.Instance?.GlobalUi?.TopBar;
        return topBar ?? screen;
    }

    /// <summary>
    /// 判断卡牌是否匹配检索关键词。
    /// </summary>
    /// <param name="card">卡牌。</param>
    /// <param name="normalizedQuery">标准化后的检索文本。</param>
    /// <returns>匹配返回 true。</returns>
    private static bool IsCardMatched(CardModel card, string normalizedQuery)
    {
        string searchText = BuildCardSearchText(card);

        return searchText.Contains(normalizedQuery);
    }

    /// <summary>
    /// 组装一张牌的检索文本：当前显示文本、中文本地化、英文本地化和卡牌ID都参与匹配。
    /// </summary>
    /// <param name="card">卡牌。</param>
    /// <returns>标准化后的检索文本。</returns>
    private static string BuildCardSearchText(CardModel card)
    {
        List<string> parts =
        [
            card.Title,
            card.Id.Entry,
            card.GetDescriptionForPile(PileType.None)
        ];

        foreach (string language in SearchLanguages)
        {
            Dictionary<string, string> localization = LoadCardLocalization(language);
            AddLocalizedCardText(parts, localization, card.Id.Entry);
        }

        string rawText = string.Join(" ", parts);
        rawText = NSearchBar.RemoveHtmlTags(rawText)
            .Replace("[", string.Empty)
            .Replace("]", string.Empty)
            .Replace("*", string.Empty);
        return NSearchBar.Normalize(rawText);
    }

    /// <summary>
    /// 从指定语言的 cards.json 里补充卡名和描述，保证中英文都能检索到。
    /// </summary>
    /// <param name="parts">检索文本片段。</param>
    /// <param name="localization">本地化字典。</param>
    /// <param name="cardEntry">卡牌本地化前缀。</param>
    private static void AddLocalizedCardText(List<string> parts, IReadOnlyDictionary<string, string> localization, string cardEntry)
    {
        AddLocalizedValue(parts, localization, $"{cardEntry}.title");
        AddLocalizedValue(parts, localization, $"{cardEntry}.description");
        AddLocalizedValue(parts, localization, $"{cardEntry}.description_upgrade");
    }

    /// <summary>
    /// 如果本地化键存在，则加入检索文本。
    /// </summary>
    /// <param name="parts">检索文本片段。</param>
    /// <param name="localization">本地化字典。</param>
    /// <param name="key">本地化键。</param>
    private static void AddLocalizedValue(List<string> parts, IReadOnlyDictionary<string, string> localization, string key)
    {
        if (localization.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
        {
            parts.Add(value);
        }
    }

    /// <summary>
    /// 加载指定语言的卡牌本地化表。
    /// </summary>
    /// <param name="language">语言目录名。</param>
    /// <returns>本地化字典。</returns>
    private static Dictionary<string, string> LoadCardLocalization(string language)
    {
        if (CardLocalizationCache.TryGetValue(language, out Dictionary<string, string>? cached))
        {
            return cached;
        }

        string path = $"res://{MainFile.ModId}/localization/{language}/cards.json";
        try
        {
            using var file = GodotFileAccess.Open(path, GodotFileAccess.ModeFlags.Read);
            if (file == null)
            {
                MainFile.Logger.Info($"【猴符咒检索】无法打开卡牌本地化文件：{path}，错误：{GodotFileAccess.GetOpenError()}");
                CardLocalizationCache[language] = [];
                return CardLocalizationCache[language];
            }

            string json = file.GetAsText();
            Dictionary<string, string>? localization = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            CardLocalizationCache[language] = localization ?? [];
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【猴符咒检索】加载卡牌本地化文件失败：{path}，原因：{ex.Message}");
            CardLocalizationCache[language] = [];
        }

        return CardLocalizationCache[language];
    }

    /// <summary>
    /// 清空当前选牌状态，避免检索后残留旧选择。
    /// </summary>
    /// <param name="screen">选牌界面。</param>
    private static void ClearSelection(NCardGridSelectionScreen screen)
    {
        HashSet<CardModel>? selectedCards = TryGetPrivateField<HashSet<CardModel>>(screen, "_selectedCards");
        if (selectedCards == null || selectedCards.Count == 0)
        {
            return;
        }

        foreach (CardModel selectedCard in selectedCards.ToList())
        {
            UnhighlightCard(screen, selectedCard);
        }

        selectedCards.Clear();
        TryInvokePrivateMethod(screen, "CheckIfSelectionComplete");
    }

    /// <summary>
    /// 尝试取消网格中的卡牌高亮。
    /// </summary>
    /// <param name="screen">选牌界面。</param>
    /// <param name="card">要取消高亮的卡牌。</param>
    private static void UnhighlightCard(NCardGridSelectionScreen screen, CardModel card)
    {
        NCardGrid? grid = TryGetPrivateField<NCardGrid>(screen, "_grid");
        grid?.UnhighlightCard(card);
    }

    /// <summary>
    /// 读取私有字段。
    /// </summary>
    /// <typeparam name="T">字段类型。</typeparam>
    /// <param name="instance">对象实例。</param>
    /// <param name="fieldName">字段名。</param>
    /// <returns>字段值；若不存在则返回默认值。</returns>
    private static T? TryGetPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null)
        {
            return default;
        }

        object? value = field.GetValue(instance);
        if (value is T typedValue)
        {
            return typedValue;
        }

        return default;
    }

    /// <summary>
    /// 触发私有方法。
    /// </summary>
    /// <param name="instance">对象实例。</param>
    /// <param name="methodName">方法名。</param>
    private static void TryInvokePrivateMethod(object instance, string methodName)
    {
        MethodInfo? method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        method?.Invoke(instance, null);
    }

    /// <summary>
    /// 检索状态。
    /// </summary>
    /// <param name="Grid">当前网格。</param>
    /// <param name="OriginalCards">原始卡牌。</param>
    /// <param name="SearchBar">搜索栏节点。</param>
    private sealed class SearchState
    {
        public SearchState(NCardGrid grid, IReadOnlyList<CardModel> originalCards, Control searchBar)
        {
            Grid = grid;
            OriginalCards = originalCards;
            SearchBar = searchBar;
        }

        public NCardGrid Grid { get; }

        public IReadOnlyList<CardModel> OriginalCards { get; }

        public Control SearchBar { get; }
    }
}
