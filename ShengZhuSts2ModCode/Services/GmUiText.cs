using ShengZhuSts2Mod.ShengZhuSts2ModCode.Keywords;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>
/// 圣主 Mod UI 文案表。
/// 选人界面的代码生成控件无法直接使用原版 JSON LocString，这里集中管理中英文文本。
/// </summary>
public static class GmUiText
{
    /// <summary>
    /// 获取当前语言下的 UI 文案。
    /// </summary>
    /// <param name="key">文案键。</param>
    /// <returns>文案内容。</returns>
    public static string Get(string key)
    {
        return KeywordRegistry.LoadedLanguage == "zhs"
            ? GetChinese(key)
            : GetEnglish(key);
    }

    /// <summary>
    /// 获取剧情事件在 UI 上显示的名称。配置、存档和联机仍使用中文名作为稳定键。
    /// </summary>
    /// <param name="eventName">剧情事件中文配置名。</param>
    /// <returns>当前语言下的显示名称。</returns>
    public static string GetStoryEventName(string eventName)
    {
        if (KeywordRegistry.LoadedLanguage == "zhs")
        {
            return eventName;
        }

        return eventName switch
        {
            "远古封印" => "Ancient",
            "恶魔小龙" => "Drago",
            "刀龙黑气" => "Dark Chi",
            "西瓦的手镯" => "Shiva",
            "岁月史书" => "Book",
            _ => eventName
        };
    }

    /// <summary>
    /// 中文文案。
    /// </summary>
    /// <param name="key">文案键。</param>
    /// <returns>中文文本。</returns>
    private static string GetChinese(string key)
    {
        return key switch
        {
            "story_event_config.title" => "圣主事件池",
            "story_event_config.act" => "楼层",
            "story_event_config.events" => "事件",
            "story_event_config.reset" => "重置",
            "story_event_config.closed" => "不选事件=关闭",
            "story_event_config.act1" => "1层",
            "story_event_config.act2" => "2层",
            "story_event_config.act3" => "3层",
            _ => key
        };
    }

    /// <summary>
    /// 英文文案。
    /// </summary>
    /// <param name="key">文案键。</param>
    /// <returns>英文文本。</returns>
    private static string GetEnglish(string key)
    {
        return key switch
        {
            "story_event_config.title" => "Events",
            "story_event_config.act" => "Act",
            "story_event_config.events" => "Events",
            "story_event_config.reset" => "Reset",
            "story_event_config.closed" => "No events = off",
            "story_event_config.act1" => "Act 1",
            "story_event_config.act2" => "Act 2",
            "story_event_config.act3" => "Act 3",
            _ => key
        };
    }
}
