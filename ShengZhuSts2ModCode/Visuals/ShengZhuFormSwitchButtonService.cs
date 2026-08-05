using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

/// <summary>
/// 圣主形态切换按钮服务：在能量指示器右侧显示本地玩家自己的形态切换入口。
/// </summary>
public static class ShengZhuFormSwitchButtonService
{
    /// <summary>
    /// 按钮根节点名称，用于重复刷新时复用旧节点。
    /// </summary>
    private const string RootNodeName = "ShengZhuFormSwitchButtonRoot";

    /// <summary>
    /// 按钮节点名称。
    /// </summary>
    private const string ButtonNodeName = "ShengZhuFormSwitchButton";

    /// <summary>
    /// 按钮尺寸，固定大小避免文字变化导致战斗 UI 跳动。
    /// </summary>
    private static readonly Vector2 ButtonSize = new(128f, 42f);

    /// <summary>
    /// 能量指示器右侧空位的显示位置比例，保持形态按钮固定在玩家左下角操作区。
    /// </summary>
    private static readonly Vector2 EnergySideButtonPosRatio = new(250f / 2048f, 908f / 1080f);

    /// <summary>
    /// 按钮正常状态的文字颜色。
    /// </summary>
    private static readonly Color TextColor = new("F7F0D2");

    /// <summary>
    /// 按钮禁用状态的文字颜色。
    /// </summary>
    private static readonly Color DisabledTextColor = new("AAA9A2");

    /// <summary>
    /// 防止连续点击在本地端提交重复切换请求。
    /// </summary>
    private static bool _isRequestingSwitch;

    /// <summary>
    /// 是否已经订阅战斗和屏幕上下文事件。
    /// </summary>
    private static bool _isRuntimeHooksRegistered;

    /// <summary>
    /// 当前已订阅的覆盖层栈，避免同一节点重复订阅。
    /// </summary>
    private static NOverlayStack? _hookedOverlayStack;

    /// <summary>
    /// 当前已订阅的手牌节点，避免选牌模式变化时按钮状态滞后。
    /// </summary>
    private static NPlayerHand? _hookedPlayerHand;

    /// <summary>
    /// 当前已订阅的手牌容器，避免新抽牌后缺少聚焦刷新。
    /// </summary>
    private static Control? _hookedHandHolderContainer;

    /// <summary>
    /// 已订阅聚焦信号的手牌节点编号，避免重复连接同一张手牌。
    /// </summary>
    private static readonly HashSet<ulong> HookedHandHolderIds = [];

    /// <summary>
    /// 当前已订阅尺寸变化的战斗 UI，避免重复连接 Godot 信号。
    /// </summary>
    private static NCombatUi? _hookedResizeUi;

    /// <summary>
    /// 刷新当前战斗房间的形态切换按钮。
    /// </summary>
    public static void RefreshAll()
    {
        NCombatRoom? combatRoom = NCombatRoom.Instance;
        if (combatRoom?.Ui == null)
        {
            return;
        }

        EnsureRuntimeHooks(combatRoom);

        if (!IsActiveCombatRoom(combatRoom))
        {
            RemoveRoot(combatRoom);
            return;
        }

        Player? player = FindLocalSwitchPlayer(combatRoom);
        if (player == null)
        {
            RemoveRoot(combatRoom);
            return;
        }

        if (!CanShowForCurrentInteraction(combatRoom, player))
        {
            HideRoot(combatRoom);
            return;
        }

        Control root = GetOrCreateRoot(combatRoom);
        Button button = GetOrCreateButton(root);
        RefreshButton(button, player);
        RefreshLayout(root, combatRoom);
        root.Visible = true;
    }

    /// <summary>
    /// 下一帧后再次刷新按钮，兜底处理回合开始时 UI 状态尚未稳定导致按钮被临时隐藏的问题。
    /// </summary>
    public static void RefreshAllAfterProcessFrame()
    {
        _ = RefreshAllAfterProcessFrameSafely();
    }

    /// <summary>
    /// 等待两帧后刷新按钮，并吞掉异步异常，避免 Godot 信号链报错。
    /// </summary>
    /// <returns>异步任务。</returns>
    private static async Task RefreshAllAfterProcessFrameSafely()
    {
        try
        {
            if (NGame.Instance != null)
            {
                await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
                await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            RefreshAll();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【圣主形态按钮】延迟刷新时发生异常：{ex}");
        }
    }

    /// <summary>
    /// 清理当前战斗房间里的形态切换按钮。
    /// </summary>
    public static void ClearAll()
    {
        _isRequestingSwitch = false;

        NCombatRoom? combatRoom = NCombatRoom.Instance;
        if (combatRoom?.Ui == null)
        {
            return;
        }

        RemoveRoot(combatRoom);
    }

    /// <summary>
    /// 订阅原生战斗和屏幕事件，让按钮能跟随弹窗、选牌和回合状态及时隐藏或恢复。
    /// </summary>
    /// <param name="combatRoom">当前战斗房间。</param>
    private static void EnsureRuntimeHooks(NCombatRoom combatRoom)
    {
        if (!_isRuntimeHooksRegistered)
        {
            ActiveScreenContext.Instance.Updated += RefreshAll;
            CombatManager.Instance.CombatEnded += OnCombatFinished;
            CombatManager.Instance.CombatWon += OnCombatFinished;
            CombatManager.Instance.TurnStarted += OnCombatStateChanged;
            CombatManager.Instance.AboutToSwitchToEnemyTurn += OnCombatStateChanged;
            CombatManager.Instance.PlayerActionsDisabledChanged += OnCombatStateChanged;
            CombatManager.Instance.PlayerEndedTurn += OnPlayerEndedTurnChanged;
            CombatManager.Instance.PlayerUnendedTurn += OnPlayerUnendedTurnChanged;
            _isRuntimeHooksRegistered = true;
        }

        EnsureOverlayHook();
        EnsurePlayerHandHook(combatRoom.Ui.Hand);
        EnsureHandHolderHooks(combatRoom.Ui.Hand);
    }

    /// <summary>
    /// 订阅覆盖层栈变化，选牌、奖励和药水等覆盖层打开或关闭时刷新按钮可见性。
    /// </summary>
    private static void EnsureOverlayHook()
    {
        NOverlayStack? overlayStack = NOverlayStack.Instance;
        if (ReferenceEquals(_hookedOverlayStack, overlayStack))
        {
            return;
        }

        if (_hookedOverlayStack != null)
        {
            _hookedOverlayStack.Changed -= RefreshAll;
        }

        _hookedOverlayStack = overlayStack;
        if (_hookedOverlayStack != null)
        {
            _hookedOverlayStack.Changed += RefreshAll;
        }
    }

    /// <summary>
    /// 订阅手牌模式变化，烧牌、升级、丢弃等手牌选择流程开始或结束时刷新按钮可见性。
    /// </summary>
    /// <param name="hand">当前战斗手牌节点。</param>
    private static void EnsurePlayerHandHook(NPlayerHand? hand)
    {
        if (ReferenceEquals(_hookedPlayerHand, hand))
        {
            return;
        }

        if (_hookedPlayerHand != null)
        {
            _hookedPlayerHand.ModeChanged -= RefreshAll;
        }

        _hookedPlayerHand = hand;
        if (_hookedPlayerHand != null)
        {
            _hookedPlayerHand.ModeChanged += RefreshAll;
        }

        HookedHandHolderIds.Clear();
        _hookedHandHolderContainer = null;
    }

    /// <summary>
    /// 订阅手牌聚焦变化，鼠标悬停手牌导致卡牌放大时及时隐藏按钮，避免遮挡卡牌文本。
    /// </summary>
    /// <param name="hand">当前战斗手牌节点。</param>
    private static void EnsureHandHolderHooks(NPlayerHand? hand)
    {
        Control? cardHolderContainer = hand?.CardHolderContainer;
        if (cardHolderContainer == null)
        {
            HookedHandHolderIds.Clear();
            _hookedHandHolderContainer = null;
            return;
        }

        if (!ReferenceEquals(_hookedHandHolderContainer, cardHolderContainer))
        {
            HookedHandHolderIds.Clear();
            _hookedHandHolderContainer = cardHolderContainer;
            cardHolderContainer.Connect(Node.SignalName.ChildEnteredTree, Callable.From<Node>(OnHandHolderTreeChanged));
            cardHolderContainer.Connect(Node.SignalName.ChildExitingTree, Callable.From<Node>(OnHandHolderTreeChanged));
        }

        foreach (Node child in cardHolderContainer.GetChildren())
        {
            if (child is not NHandCardHolder holder)
            {
                continue;
            }

            ulong instanceId = holder.GetInstanceId();
            if (!HookedHandHolderIds.Add(instanceId))
            {
                continue;
            }

            holder.Connect(NHandCardHolder.SignalName.HolderFocused, Callable.From<NHandCardHolder>(OnHandHolderFocusChanged));
            holder.Connect(NHandCardHolder.SignalName.HolderUnfocused, Callable.From<NHandCardHolder>(OnHandHolderFocusChanged));
        }
    }

    /// <summary>
    /// 手牌列表变化时延迟刷新并重新订阅手牌聚焦信号。
    /// </summary>
    /// <param name="_">进入或离开的手牌节点。</param>
    private static void OnHandHolderTreeChanged(Node _)
    {
        RefreshAllAfterProcessFrame();
    }

    /// <summary>
    /// 手牌聚焦或失焦时刷新按钮显隐。
    /// </summary>
    /// <param name="_">状态变化的手牌节点。</param>
    private static void OnHandHolderFocusChanged(NHandCardHolder _)
    {
        RefreshAll();
    }

    /// <summary>
    /// 战斗结束或胜利时清理按钮，防止它穿透到奖励、地图等后续界面。
    /// </summary>
    /// <param name="_">结束的战斗房间。</param>
    private static void OnCombatFinished(CombatRoom _)
    {
        ClearAll();
    }

    /// <summary>
    /// 战斗状态变化时刷新按钮显示状态。
    /// </summary>
    /// <param name="_">当前战斗状态。</param>
    private static void OnCombatStateChanged(CombatState _)
    {
        RefreshAll();
        RefreshAllAfterProcessFrame();
    }

    /// <summary>
    /// 玩家结束回合状态变化时刷新按钮显示状态。
    /// </summary>
    /// <param name="_">状态变化的玩家。</param>
    /// <param name="__">是否允许撤销结束回合。</param>
    private static void OnPlayerEndedTurnChanged(Player _, bool __)
    {
        RefreshAll();
    }

    /// <summary>
    /// 玩家撤销结束回合状态变化时刷新按钮显示状态。
    /// </summary>
    /// <param name="_">状态变化的玩家。</param>
    private static void OnPlayerUnendedTurnChanged(Player _)
    {
        RefreshAll();
    }

    /// <summary>
    /// 判断当前战斗房间是否仍处于可以显示战斗按钮的战斗中状态。
    /// </summary>
    /// <param name="combatRoom">当前战斗房间。</param>
    /// <returns>处于进行中的正式战斗时返回 true。</returns>
    private static bool IsActiveCombatRoom(NCombatRoom combatRoom)
    {
        return combatRoom.Mode == CombatRoomMode.ActiveCombat
            && combatRoom.Visible
            && combatRoom.Ui.Visible
            && CombatManager.Instance.IsInProgress
            && !CombatManager.Instance.IsOverOrEnding;
    }

    /// <summary>
    /// 判断当前交互层是否适合显示形态按钮。
    /// </summary>
    /// <param name="combatRoom">当前战斗房间。</param>
    /// <param name="player">本端圣主玩家。</param>
    /// <returns>没有弹窗、覆盖层、手牌选择流程或聚焦手牌，并且本端玩家可操作时返回 true。</returns>
    private static bool CanShowForCurrentInteraction(NCombatRoom combatRoom, Player player)
    {
        if (CombatManager.Instance.IsPaused
            || CombatManager.Instance.EndingPlayerTurnPhaseOne
            || CombatManager.Instance.EndingPlayerTurnPhaseTwo
            || CombatManager.Instance.IsPlayerReadyToEndTurn(player)
            || !CombatManager.Instance.IsPartOfPlayerTurn(player))
        {
            return false;
        }

        if (NOverlayStack.Instance?.ScreenCount > 0 || NModalContainer.Instance?.OpenModal != null)
        {
            return false;
        }

        if (ActiveScreenContext.Instance.GetCurrentScreen() is { } currentScreen
            && !ReferenceEquals(currentScreen, combatRoom))
        {
            return false;
        }

        NPlayerHand? hand = combatRoom.Ui.Hand;
        return hand != null
            && !hand.InCardPlay
            && !hand.IsInCardSelection
            && !HasFocusedHandCard(hand)
            && hand.CurrentMode == NPlayerHand.Mode.Play;
    }

    /// <summary>
    /// 判断当前是否有手牌被鼠标或手柄聚焦，聚焦时卡牌会抬起放大，按钮需要退让。
    /// </summary>
    /// <param name="hand">当前战斗手牌节点。</param>
    /// <returns>存在有效的聚焦手牌时返回 true。</returns>
    private static bool HasFocusedHandCard(NPlayerHand hand)
    {
        return hand.FocusedHolder != null
            && GodotObject.IsInstanceValid(hand.FocusedHolder);
    }

    /// <summary>
    /// 查找本端可切换形态的圣主玩家。
    /// </summary>
    /// <param name="combatRoom">当前战斗房间。</param>
    /// <returns>本端持有鼠符咒的圣主玩家；不存在时返回 null。</returns>
    private static Player? FindLocalSwitchPlayer(NCombatRoom combatRoom)
    {
        foreach (var creatureNode in combatRoom.CreatureNodes)
        {
            Player? player = creatureNode.Entity?.Player;
            if (player != null
                && ShengZhuFormService.CanUseFormSwitch(player)
                && LocalContext.IsMe(player))
            {
                return player;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取或创建按钮根节点。
    /// </summary>
    /// <param name="combatRoom">当前战斗房间。</param>
    /// <returns>按钮根节点。</returns>
    private static Control GetOrCreateRoot(NCombatRoom combatRoom)
    {
        Control? root = FindRoot(combatRoom);
        if (root != null)
        {
            return root;
        }

        root = new Control
        {
            Name = RootNodeName,
            MouseFilter = Control.MouseFilterEnum.Pass,
            ZIndex = 20,
            CustomMinimumSize = ButtonSize,
            Size = ButtonSize
        };
        root.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        combatRoom.Ui.AddChild(root);
        EnsureResizeHook(combatRoom.Ui);
        return root;
    }

    /// <summary>
    /// 订阅战斗 UI 尺寸变化，分辨率变化时重新摆放按钮。
    /// </summary>
    /// <param name="combatUi">当前战斗 UI。</param>
    private static void EnsureResizeHook(NCombatUi combatUi)
    {
        if (ReferenceEquals(_hookedResizeUi, combatUi))
        {
            return;
        }

        _hookedResizeUi = combatUi;
        combatUi.Connect(Control.SignalName.Resized, Callable.From(RefreshAll));
    }

    /// <summary>
    /// 获取或创建形态切换按钮。
    /// </summary>
    /// <param name="root">按钮根节点。</param>
    /// <returns>形态切换按钮。</returns>
    private static Button GetOrCreateButton(Control root)
    {
        Button? button = root.GetNodeOrNull<Button>(ButtonNodeName);
        if (button != null)
        {
            return button;
        }

        button = new Button
        {
            Name = ButtonNodeName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = ButtonSize,
            Size = ButtonSize,
            ClipText = true
        };
        button.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        ApplyButtonTheme(button);
        button.Connect(Button.SignalName.ButtonUp, Callable.From(delegate
        {
            _ = HandleButtonPressedSafely();
        }));
        root.AddChild(button);
        return button;
    }

    /// <summary>
    /// 刷新按钮显示文本、可点击状态和悬浮提示。
    /// </summary>
    /// <param name="button">需要刷新的按钮。</param>
    /// <param name="player">本端玩家。</param>
    private static void RefreshButton(Button button, Player player)
    {
        RatTalisman? rat = player.GetRelic<RatTalisman>();
        if (rat == null)
        {
            button.Visible = false;
            return;
        }

        button.Visible = true;
        button.Disabled = _isRequestingSwitch || rat.HasSwitchedFormThisTurn;
        button.Text = ResolveButtonText(rat);
        button.TooltipText = ResolveTooltipText(rat);
        button.Modulate = button.Disabled
            ? new Color(1f, 1f, 1f, 0.72f)
            : Colors.White;
    }

    /// <summary>
    /// 按固定视口比例摆放形态按钮，避免跟随结束回合按钮动画上下漂移。
    /// </summary>
    /// <param name="root">按钮根节点。</param>
    /// <param name="combatRoom">当前战斗房间。</param>
    private static void RefreshLayout(Control root, NCombatRoom combatRoom)
    {
        root.CustomMinimumSize = ButtonSize;
        root.Size = ButtonSize;

        Vector2 viewportSize = combatRoom.GetViewportRect().Size;
        Vector2 targetPosition = ResolveTargetPosition(viewportSize);
        root.GlobalPosition = ClampToViewport(targetPosition, viewportSize);
    }

    /// <summary>
    /// 计算目标位置：以能量指示器右侧空位为基准，固定显示在玩家左下角操作区。
    /// </summary>
    /// <param name="viewportSize">当前视口尺寸。</param>
    /// <returns>目标全局坐标。</returns>
    private static Vector2 ResolveTargetPosition(Vector2 viewportSize)
    {
        return viewportSize * EnergySideButtonPosRatio;
    }

    /// <summary>
    /// 把按钮限制在视口范围内，避免极端分辨率下跑出屏幕。
    /// </summary>
    /// <param name="position">目标位置。</param>
    /// <param name="viewportSize">当前视口尺寸。</param>
    /// <returns>修正后的全局坐标。</returns>
    private static Vector2 ClampToViewport(Vector2 position, Vector2 viewportSize)
    {
        float x = Math.Clamp(position.X, 16f, Math.Max(16f, viewportSize.X - ButtonSize.X - 16f));
        float y = Math.Clamp(position.Y, 72f, Math.Max(72f, viewportSize.Y - ButtonSize.Y - 16f));
        return new Vector2(x, y);
    }

    /// <summary>
    /// 给形态按钮套用接近结束回合按钮的青色发光风格，避免使用 Godot 默认灰按钮。
    /// </summary>
    /// <param name="button">形态切换按钮。</param>
    private static void ApplyButtonTheme(Button button)
    {
        button.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color("10222DCC"), new Color("31F6FFFF"), 2));
        button.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color("153444E6"), new Color("7CFFFFFF"), 3));
        button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color("0A171FCC"), new Color("22C8D6FF"), 2));
        button.AddThemeStyleboxOverride("disabled", CreateButtonStyle(new Color("10182099"), new Color("6B787D99"), 2));
        button.AddThemeColorOverride("font_color", TextColor);
        button.AddThemeColorOverride("font_hover_color", TextColor);
        button.AddThemeColorOverride("font_pressed_color", TextColor);
        button.AddThemeColorOverride("font_disabled_color", DisabledTextColor);
        button.AddThemeColorOverride("font_focus_color", TextColor);
        button.AddThemeFontSizeOverride("font_size", 22);
        button.AddThemeConstantOverride("outline_size", 3);
        button.AddThemeColorOverride("font_outline_color", Colors.Black);
    }

    /// <summary>
    /// 创建形态按钮样式盒。
    /// </summary>
    /// <param name="backgroundColor">背景颜色。</param>
    /// <param name="borderColor">边框颜色。</param>
    /// <param name="borderWidth">边框宽度。</param>
    /// <returns>按钮样式。</returns>
    private static StyleBoxFlat CreateButtonStyle(Color backgroundColor, Color borderColor, int borderWidth)
    {
        StyleBoxFlat style = new()
        {
            BgColor = backgroundColor,
            BorderColor = borderColor,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3,
            ShadowColor = new Color("13F7FF66"),
            ShadowSize = 8,
            ShadowOffset = Vector2.Zero,
            ContentMarginLeft = 8f,
            ContentMarginRight = 8f,
            ContentMarginTop = 2f,
            ContentMarginBottom = 2f
        };
        return style;
    }

    /// <summary>
    /// 处理按钮点击，并保护异步异常不穿透到 Godot 信号。
    /// </summary>
    /// <returns>异步任务。</returns>
    private static async Task HandleButtonPressedSafely()
    {
        if (_isRequestingSwitch)
        {
            return;
        }

        try
        {
            _isRequestingSwitch = true;
            RefreshAll();

            NCombatRoom? combatRoom = NCombatRoom.Instance;
            Player? player = combatRoom == null ? null : FindLocalSwitchPlayer(combatRoom);
            if (player == null)
            {
                MainFile.Logger.Info("【圣主形态按钮】点击切换失败：未找到本端持有鼠符咒的圣主玩家。");
                return;
            }

            await ShengZhuFormService.RequestSwitchForPlayer(player);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【圣主形态按钮】点击切换时发生异常：{ex}");
        }
        finally
        {
            _isRequestingSwitch = false;
            RefreshAll();
        }
    }

    /// <summary>
    /// 根据当前形态和切换次数解析按钮文字。
    /// </summary>
    /// <param name="rat">鼠符咒实例。</param>
    /// <returns>按钮文字。</returns>
    private static string ResolveButtonText(RatTalisman rat)
    {
        if (rat.HasSwitchedFormThisTurn)
        {
            return L10N("SHENGZHUSTS2MOD-RAT_TALISMAN.formButtonLocked");
        }

        return rat.IsHumanForm
            ? L10N("SHENGZHUSTS2MOD-RAT_TALISMAN.formButtonToStatue")
            : L10N("SHENGZHUSTS2MOD-RAT_TALISMAN.formButtonToHuman");
    }

    /// <summary>
    /// 根据当前形态和切换次数解析按钮悬浮提示。
    /// </summary>
    /// <param name="rat">鼠符咒实例。</param>
    /// <returns>悬浮提示。</returns>
    private static string ResolveTooltipText(RatTalisman rat)
    {
        string key = rat.HasSwitchedFormThisTurn
            ? (rat.IsHumanForm
                ? "SHENGZHUSTS2MOD-RAT_TALISMAN.formTooltipLockedHuman"
                : "SHENGZHUSTS2MOD-RAT_TALISMAN.formTooltipLockedStatue")
            : (rat.IsHumanForm
                ? "SHENGZHUSTS2MOD-RAT_TALISMAN.formTooltipHuman"
                : "SHENGZHUSTS2MOD-RAT_TALISMAN.formTooltipStatue");

        return L10N(key);
    }

    /// <summary>
    /// 读取遗物表本地化文本，缺失时返回键名方便排查。
    /// </summary>
    /// <param name="key">本地化键。</param>
    /// <returns>本地化文本。</returns>
    private static string L10N(string key)
    {
        return LocString.Exists("relics", key)
            ? new LocString("relics", key).GetRawText()
            : key;
    }

    /// <summary>
    /// 查找当前战斗 UI 上已有的按钮根节点。
    /// </summary>
    /// <param name="combatRoom">当前战斗房间。</param>
    /// <returns>按钮根节点；不存在时返回 null。</returns>
    private static Control? FindRoot(NCombatRoom combatRoom)
    {
        foreach (Node child in combatRoom.Ui.GetChildren())
        {
            if (child.Name == RootNodeName && child is Control root)
            {
                return root;
            }
        }

        return null;
    }

    /// <summary>
    /// 隐藏当前战斗 UI 上已有的按钮根节点，但保留节点用于覆盖层关闭后快速恢复。
    /// </summary>
    /// <param name="combatRoom">当前战斗房间。</param>
    private static void HideRoot(NCombatRoom combatRoom)
    {
        Control? root = FindRoot(combatRoom);
        if (root == null)
        {
            return;
        }

        root.Visible = false;
    }

    /// <summary>
    /// 移除当前战斗 UI 上的按钮根节点。
    /// </summary>
    /// <param name="combatRoom">当前战斗房间。</param>
    private static void RemoveRoot(NCombatRoom combatRoom)
    {
        Control? root = FindRoot(combatRoom);
        if (root == null)
        {
            return;
        }

        combatRoom.Ui.RemoveChild(root);
        root.QueueFree();
    }
}
