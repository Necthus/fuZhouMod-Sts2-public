using Godot;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>
/// 圣主 Mod UI 渲染辅助。
/// 当前只提供选人配置面板所需的基础控件样式，后续其它 UI 可以继续复用。
/// </summary>
public static class GmUiManager
{
    /// <summary>
    /// 创建普通文本标签。
    /// </summary>
    /// <param name="text">显示文本。</param>
    /// <param name="fontSize">字号。</param>
    /// <returns>标签控件。</returns>
    public static Label CreateLabel(string text, int fontSize = 18)
    {
        Label label = new()
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", new Color("F5E9C8"));
        return label;
    }

    /// <summary>
    /// 创建面板背景样式。
    /// </summary>
    /// <returns>面板样式。</returns>
    public static StyleBoxFlat CreatePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.07f, 0.06f, 0.88f),
            BorderColor = new Color("B8860B"),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            ContentMarginLeft = 8,
            ContentMarginTop = 7,
            ContentMarginRight = 8,
            ContentMarginBottom = 7
        };
    }
}
