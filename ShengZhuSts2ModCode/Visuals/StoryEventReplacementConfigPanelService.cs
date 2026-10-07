using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

/// <summary>
/// 圣主选人界面 x 层剧情事件替换池配置面板。
/// 面板只在本地玩家选中圣主时显示，后台共享配置仍会同步给所有同局玩家。
/// </summary>
public static class StoryEventReplacementConfigPanelService
{
    /// <summary>
    /// 面板根节点名称。
    /// </summary>
    private const string RootNodeName = "ShengZhuStoryEventReplacementConfigPanel";

    /// <summary>
    /// 面板背景节点名称。
    /// </summary>
    private const string BackgroundNodeName = "Background";

    /// <summary>
    /// 面板宽度。
    /// </summary>
    private const float PanelWidth = 250f;

    /// <summary>
    /// 面板基础高度。
    /// </summary>
    private const float PanelBaseHeight = 250f;

    /// <summary>
    /// 面板右侧保留的视口空隙。
    /// </summary>
    private const float RightViewportPadding = 72f;

    /// <summary>
    /// 面板底部保留的视口空隙。
    /// </summary>
    private const float BottomViewportPadding = 24f;

    /// <summary>
    /// 面板左侧最小位置。
    /// </summary>
    private const float MinPanelX = 24f;

    /// <summary>
    /// 面板顶部最小位置。
    /// </summary>
    private const float MinPanelY = 120f;

    /// <summary>
    /// 面板顶部位置占视口高度的比例。
    /// </summary>
    private const float PanelTopRatio = 0.17f;

    /// <summary>
    /// 面板内边距左侧。
    /// </summary>
    private const float PaddingLeft = 8f;

    /// <summary>
    /// 控件固定高度。
    /// </summary>
    private const float RowHeight = 24f;

    /// <summary>
    /// 事件复选框宽度。
    /// </summary>
    private const float EventCheckBoxWidth = 112f;

    /// <summary>
    /// 事件复选框高度。
    /// </summary>
    private const float EventCheckBoxHeight = 23f;

    /// <summary>
    /// 事件复选框起始纵坐标。
    /// </summary>
    private const float EventGridTop = 94f;

    /// <summary>
    /// 事件复选框横向间隔。
    /// </summary>
    private const float EventColumnSpacing = 124f;

    /// <summary>
    /// 事件复选框纵向行高。
    /// </summary>
    private const float EventRowHeight = 26f;

    /// <summary>
    /// 面板创建时的基础尺寸。
    /// </summary>
    private static readonly Vector2 PanelBaseSize = new(PanelWidth, PanelBaseHeight);

    /// <summary>
    /// 当前选人界面。
    /// </summary>
    private static NCharacterSelectScreen? _currentScreen;

    /// <summary>
    /// 当前面板根节点。
    /// </summary>
    private static Control? _root;

    /// <summary>
    /// 面板背景节点。
    /// </summary>
    private static Panel? _backgroundPanel;

    /// <summary>
    /// 楼层下拉框。
    /// </summary>
    private static OptionButton? _actOptionButton;

    /// <summary>
    /// 事件复选框。
    /// </summary>
    private static readonly Dictionary<string, CheckBox> EventCheckBoxes = [];

    /// <summary>
    /// 是否正在刷新 UI，避免程序设置控件状态时反向触发配置修改。
    /// </summary>
    private static bool _isRefreshing;

    /// <summary>
    /// 是否正在主动锁定面板布局，避免 Resized 事件递归刷新。
    /// </summary>
    private static bool _isApplyingLayout;

    /// <summary>
    /// 创建或复用配置面板。
    /// </summary>
    /// <param name="screen">选人界面。</param>
    public static void EnsurePanel(NCharacterSelectScreen screen)
    {
        if (_root != null && GodotObject.IsInstanceValid(_root) && ReferenceEquals(_currentScreen, screen))
        {
            return;
        }

        Clear();
        _currentScreen = screen;

        _root = new Control
        {
            Name = RootNodeName,
            CustomMinimumSize = PanelBaseSize,
            Size = PanelBaseSize,
            SizeFlagsHorizontal = 0,
            SizeFlagsVertical = 0,
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TopLevel = true
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _root.Resized += KeepPanelAbsoluteSize;

        _backgroundPanel = CreateBackgroundPanel();
        _root.AddChild(_backgroundPanel);

        _root.AddChild(CreateFixedLabel(GmUiText.Get("story_event_config.title"), 18, new Rect2(new Vector2(8f, 5f), new Vector2(170f, 28f))));
        _root.AddChild(CreateResetButton());
        _root.AddChild(CreateFixedLabel(GmUiText.Get("story_event_config.act"), 13, new Rect2(new Vector2(8f, 36f), new Vector2(44f, RowHeight))));

        _actOptionButton = CreateActOptionButton();
        _root.AddChild(_actOptionButton);

        _root.AddChild(CreateFixedLabel(GmUiText.Get("story_event_config.events"), 13, new Rect2(new Vector2(8f, 68f), new Vector2(120f, RowHeight))));

        int index = 0;
        foreach (string eventName in StoryEventReplacementConfigService.AllSelectableEventNames)
        {
            CheckBox checkBox = CreateEventCheckBox(eventName);
            SetFixedRect(checkBox, ResolveEventCheckBoxRect(index));
            EventCheckBoxes[eventName] = checkBox;
            _root.AddChild(checkBox);
            index++;
        }

        float closedLabelY = ResolveClosedLabelY(index);
        _root.AddChild(CreateFixedLabel(GmUiText.Get("story_event_config.closed"), 10, new Rect2(new Vector2(8f, closedLabelY), new Vector2(PanelBaseSize.X - 16f, RowHeight))));

        // 这块 UI 使用绝对坐标，不再交给 Container 布局，避免首次进入选人界面时被拉成长条。
        screen.GetTree().Root.AddChild(_root);
        Refresh(screen);
        RefreshAfterProcessFrame(screen);
        MainFile.Logger.Info("【选人事件池UI】已创建圣主事件池配置面板。");
    }

    /// <summary>
    /// 刷新当前面板。
    /// </summary>
    public static void RefreshCurrentPanel()
    {
        if (_currentScreen == null || !GodotObject.IsInstanceValid(_currentScreen))
        {
            return;
        }

        Refresh(_currentScreen);
    }

    /// <summary>
    /// 按当前共享配置刷新指定选人界面的面板。
    /// </summary>
    /// <param name="screen">选人界面。</param>
    public static void Refresh(NCharacterSelectScreen screen)
    {
        if (_root == null || !GodotObject.IsInstanceValid(_root))
        {
            return;
        }

        RefreshLayout(screen);
        bool showPanel = IsLocalPlayerShengZhu(screen);
        _root.Visible = showPanel;
        if (!showPanel)
        {
            return;
        }

        StoryEventReplacementConfig config = StoryEventReplacementConfigService.CurrentSelectionConfig;
        _isRefreshing = true;
        try
        {
            SelectActNumber(config.ActNumber);

            foreach ((string eventName, CheckBox checkBox) in EventCheckBoxes)
            {
                checkBox.ButtonPressed = config.EventNames.Contains(eventName);
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>
    /// 清理当前面板。
    /// </summary>
    public static void Clear()
    {
        _backgroundPanel = null;
        _actOptionButton = null;
        EventCheckBoxes.Clear();
        _isRefreshing = false;
        _isApplyingLayout = false;

        if (_root != null && GodotObject.IsInstanceValid(_root))
        {
            _root.QueueFree();
        }

        _root = null;
        _currentScreen = null;
    }

    /// <summary>
    /// 创建面板背景。
    /// </summary>
    /// <returns>背景节点。</returns>
    private static Panel CreateBackgroundPanel()
    {
        Panel panel = new()
        {
            Name = BackgroundNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", GmUiManager.CreatePanelStyle());
        SetFixedRect(panel, new Rect2(Vector2.Zero, PanelBaseSize));
        return panel;
    }

    /// <summary>
    /// 创建固定位置文本标签。
    /// </summary>
    /// <param name="text">显示文本。</param>
    /// <param name="fontSize">字号。</param>
    /// <param name="rect">控件矩形。</param>
    /// <returns>标签控件。</returns>
    private static Label CreateFixedLabel(string text, int fontSize, Rect2 rect)
    {
        Label label = GmUiManager.CreateLabel(text, fontSize);
        SetFixedRect(label, rect);
        return label;
    }

    /// <summary>
    /// 创建重置按钮。
    /// </summary>
    /// <returns>重置按钮。</returns>
    private static Button CreateResetButton()
    {
        Button resetButton = new()
        {
            Text = GmUiText.Get("story_event_config.reset"),
            FocusMode = Control.FocusModeEnum.None
        };
        resetButton.AddThemeFontSizeOverride("font_size", 12);
        resetButton.Pressed += ResetToJsonDefault;
        SetFixedRect(resetButton, new Rect2(new Vector2(184f, 8f), new Vector2(58f, RowHeight)));
        return resetButton;
    }

    /// <summary>
    /// 创建楼层下拉框。
    /// </summary>
    /// <returns>楼层下拉框。</returns>
    private static OptionButton CreateActOptionButton()
    {
        OptionButton optionButton = new()
        {
            FocusMode = Control.FocusModeEnum.None
        };
        optionButton.AddThemeFontSizeOverride("font_size", 12);
        for (int actNumber = StoryEventReplacementConfigService.MinStoryEventActNumber;
             actNumber <= StoryEventReplacementConfigService.MaxStoryEventActNumber;
             actNumber++)
        {
            optionButton.AddItem(GmUiText.Get($"story_event_config.act{actNumber}"));
        }

        optionButton.ItemSelected += OnActSelected;
        SetFixedRect(optionButton, new Rect2(new Vector2(64f, 36f), new Vector2(178f, RowHeight)));
        return optionButton;
    }

    /// <summary>
    /// 创建事件复选框。
    /// </summary>
    /// <param name="eventName">事件中文名。</param>
    /// <returns>复选框。</returns>
    private static CheckBox CreateEventCheckBox(string eventName)
    {
        CheckBox checkBox = new()
        {
            Text = GmUiText.GetStoryEventName(eventName),
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = 0,
            SizeFlagsVertical = 0
        };
        checkBox.AddThemeFontSizeOverride("font_size", 12);
        checkBox.Toggled += _ => SubmitCurrentUiConfig("事件勾选");
        return checkBox;
    }

    /// <summary>
    /// 下拉框选择楼层后提交配置。
    /// </summary>
    /// <param name="selectedIndex">下拉框选项下标。</param>
    private static void OnActSelected(long selectedIndex)
    {
        if (_isRefreshing)
        {
            return;
        }

        int actNumber = StoryEventReplacementConfigService.MinStoryEventActNumber + (int)selectedIndex;
        ChangeActNumber(actNumber);
    }

    /// <summary>
    /// 修改目标楼层。
    /// </summary>
    /// <param name="actNumber">目标楼层。</param>
    private static void ChangeActNumber(int actNumber)
    {
        if (_isRefreshing)
        {
            return;
        }

        StoryEventReplacementConfig current = StoryEventReplacementConfigService.CurrentSelectionConfig;
        StoryEventReplacementConfig config = new(actNumber, current.EventNames);
        StoryEventReplacementLobbySyncService.ApplyLocalConfigChange(config, "楼层按钮");
        RefreshCurrentPanel();
    }

    /// <summary>
    /// 根据当前 UI 状态提交配置。
    /// </summary>
    /// <param name="source">来源说明。</param>
    private static void SubmitCurrentUiConfig(string source)
    {
        if (_isRefreshing)
        {
            return;
        }

        int actNumber = GetSelectedActNumber();

        List<string> eventNames = EventCheckBoxes
            .Where(pair => pair.Value.ButtonPressed)
            .Select(pair => pair.Key)
            .ToList();
        StoryEventReplacementConfig config = new(actNumber, eventNames);
        StoryEventReplacementLobbySyncService.ApplyLocalConfigChange(config, source);
        RefreshCurrentPanel();
    }

    /// <summary>
    /// 重置为 JSON 默认配置。
    /// </summary>
    private static void ResetToJsonDefault()
    {
        if (_isRefreshing)
        {
            return;
        }

        StoryEventReplacementConfig config = StoryEventReplacementConfigService.LoadDefaultConfigFromJson();
        StoryEventReplacementLobbySyncService.ApplyLocalConfigChange(config, "重置默认");
        RefreshCurrentPanel();
    }

    /// <summary>
    /// 按配置楼层选中下拉框。
    /// </summary>
    /// <param name="actNumber">目标楼层。</param>
    private static void SelectActNumber(int actNumber)
    {
        if (_actOptionButton == null)
        {
            return;
        }

        int index = Math.Clamp(
            actNumber - StoryEventReplacementConfigService.MinStoryEventActNumber,
            0,
            StoryEventReplacementConfigService.MaxStoryEventActNumber - StoryEventReplacementConfigService.MinStoryEventActNumber);
        _actOptionButton.Select(index);
    }

    /// <summary>
    /// 获取当前下拉框选中的楼层。
    /// </summary>
    /// <returns>当前楼层。</returns>
    private static int GetSelectedActNumber()
    {
        if (_actOptionButton == null)
        {
            return StoryEventReplacementConfigService.CurrentSelectionConfig.ActNumber;
        }

        int selectedIndex = Math.Max(0, _actOptionButton.Selected);
        return StoryEventReplacementConfigService.MinStoryEventActNumber + selectedIndex;
    }

    /// <summary>
    /// 刷新面板位置和背景尺寸。
    /// </summary>
    /// <param name="screen">选人界面。</param>
    private static void RefreshLayout(Control screen)
    {
        if (_root == null || !GodotObject.IsInstanceValid(_root))
        {
            return;
        }

        Vector2 panelSize = ResolvePanelSize(screen);
        Vector2 viewportSize = screen.GetViewportRect().Size;
        float x = Math.Max(MinPanelX, viewportSize.X - panelSize.X - RightViewportPadding);
        float y = Math.Max(MinPanelY, viewportSize.Y * PanelTopRatio);

        _isApplyingLayout = true;
        try
        {
            SetFixedRect(_root, new Rect2(new Vector2(x, y), panelSize));
            if (_backgroundPanel != null && GodotObject.IsInstanceValid(_backgroundPanel))
            {
                SetFixedRect(_backgroundPanel, new Rect2(Vector2.Zero, panelSize));
            }
        }
        finally
        {
            _isApplyingLayout = false;
        }
    }

    /// <summary>
    /// 根据事件数量计算面板尺寸。
    /// </summary>
    /// <param name="screen">选人界面。</param>
    /// <returns>面板尺寸。</returns>
    private static Vector2 ResolvePanelSize(Control screen)
    {
        Vector2 viewportSize = screen.GetViewportRect().Size;
        int eventCount = StoryEventReplacementConfigService.AllSelectableEventNames.Count;
        float contentHeight = ResolveClosedLabelY(eventCount) + RowHeight + 8f;
        float panelHeight = Math.Max(PanelBaseHeight, contentHeight);
        float maxHeight = Math.Max(PanelBaseHeight, viewportSize.Y - BottomViewportPadding * 2f);
        return new Vector2(PanelWidth, Math.Min(panelHeight, maxHeight));
    }

    /// <summary>
    /// 计算事件复选框位置。
    /// </summary>
    /// <param name="index">事件下标。</param>
    /// <returns>控件矩形。</returns>
    private static Rect2 ResolveEventCheckBoxRect(int index)
    {
        int column = index % 2;
        int row = index / 2;
        float x = PaddingLeft + column * EventColumnSpacing;
        float y = EventGridTop + row * EventRowHeight;
        return new Rect2(new Vector2(x, y), new Vector2(EventCheckBoxWidth, EventCheckBoxHeight));
    }

    /// <summary>
    /// 计算关闭提示文本的纵坐标。
    /// </summary>
    /// <param name="eventCount">事件数量。</param>
    /// <returns>纵坐标。</returns>
    private static float ResolveClosedLabelY(int eventCount)
    {
        int rows = Math.Max(1, (eventCount + 1) / 2);
        return EventGridTop + rows * EventRowHeight + 6f;
    }

    /// <summary>
    /// 把控件固定到指定矩形，不参与父级容器布局。
    /// </summary>
    /// <param name="control">控件。</param>
    /// <param name="rect">目标矩形。</param>
    private static void SetFixedRect(Control control, Rect2 rect)
    {
        control.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        control.AnchorLeft = 0f;
        control.AnchorTop = 0f;
        control.AnchorRight = 0f;
        control.AnchorBottom = 0f;
        control.CustomMinimumSize = rect.Size;
        control.Position = rect.Position;
        control.Size = rect.Size;
        control.OffsetLeft = rect.Position.X;
        control.OffsetTop = rect.Position.Y;
        control.OffsetRight = rect.Position.X + rect.Size.X;
        control.OffsetBottom = rect.Position.Y + rect.Size.Y;
        control.SizeFlagsHorizontal = 0;
        control.SizeFlagsVertical = 0;
    }

    /// <summary>
    /// 下一帧后再次刷新面板布局，兜底处理选人界面首次布局尚未稳定导致的位置变化。
    /// </summary>
    /// <param name="screen">选人界面。</param>
    private static void RefreshAfterProcessFrame(NCharacterSelectScreen screen)
    {
        _ = RefreshAfterProcessFrameSafely(screen);
    }

    /// <summary>
    /// 等待两帧后刷新面板，并吞掉异步异常，避免 Godot 信号链报错。
    /// </summary>
    /// <param name="screen">选人界面。</param>
    /// <returns>异步任务。</returns>
    private static async Task RefreshAfterProcessFrameSafely(NCharacterSelectScreen screen)
    {
        try
        {
            SceneTree tree = screen.GetTree();
            await screen.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await screen.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            if (_root == null
                || !GodotObject.IsInstanceValid(_root)
                || !GodotObject.IsInstanceValid(screen)
                || !ReferenceEquals(_currentScreen, screen))
            {
                return;
            }

            Refresh(screen);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【选人事件池UI】延迟刷新布局时发生异常：{ex}。");
        }
    }

    /// <summary>
    /// 面板尺寸被外部改动时，重新锁回手动布局尺寸。
    /// </summary>
    private static void KeepPanelAbsoluteSize()
    {
        if (_isApplyingLayout)
        {
            return;
        }

        RefreshCurrentPanel();
    }

    /// <summary>
    /// 判断本地玩家是否已选中圣主。
    /// </summary>
    /// <param name="screen">选人界面。</param>
    /// <returns>本地玩家选中圣主时返回 true。</returns>
    private static bool IsLocalPlayerShengZhu(NCharacterSelectScreen screen)
    {
        try
        {
            var character = screen.Lobby.LocalPlayer.character;
            return character != null && (character is ShengZhu || character.Id.Entry == ShengZhu.CharacterId);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【选人事件池UI】读取本地角色失败，隐藏配置面板：{ex.Message}。");
            return false;
        }
    }
}
