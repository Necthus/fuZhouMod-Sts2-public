namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Models;

/// <summary>
/// x层剧情事件替换池配置。
/// 保存本局要处理的楼层，以及允许兜底替换的剧情事件中文名列表。
/// </summary>
public sealed class StoryEventReplacementConfig
{
    /// <summary>
    /// 创建剧情事件替换池配置。
    /// </summary>
    /// <param name="actNumber">目标楼层，范围 1 到 3。</param>
    /// <param name="eventNames">允许兜底替换的事件中文名。</param>
    public StoryEventReplacementConfig(int actNumber, IEnumerable<string> eventNames)
    {
        ActNumber = actNumber;
        EventNames = eventNames.ToList();
    }

    /// <summary>
    /// 目标楼层，范围 1 到 3。
    /// </summary>
    public int ActNumber { get; }

    /// <summary>
    /// 允许兜底替换的事件中文名。空列表表示关闭该兜底功能。
    /// </summary>
    public IReadOnlyList<string> EventNames { get; }

    /// <summary>
    /// 是否启用兜底替换。
    /// </summary>
    public bool IsEnabled => EventNames.Count > 0;

    /// <summary>
    /// 复制当前配置，避免调用方误改共享列表。
    /// </summary>
    /// <returns>复制后的配置。</returns>
    public StoryEventReplacementConfig Clone()
    {
        return new StoryEventReplacementConfig(ActNumber, EventNames);
    }

    /// <summary>
    /// 把事件名列表保存成存档字段。
    /// </summary>
    /// <returns>逗号分隔的事件中文名。</returns>
    public string ToSavedCsv()
    {
        return string.Join(",", EventNames);
    }

    /// <summary>
    /// 格式化成中文日志文本。
    /// </summary>
    /// <returns>日志文本。</returns>
    public override string ToString()
    {
        return $"楼层={ActNumber}，事件=[{string.Join("、", EventNames)}]，启用={IsEnabled}";
    }
}
