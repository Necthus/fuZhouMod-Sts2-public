namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Keywords;

/// <summary>
/// 自定义关键字信息：存储一个关键字的ID、显示名称、别名列表、描述和关联关键字。
/// 对应 card_keywords.json 中的一条记录。
/// </summary>
public class KeywordInfo
{
    /// <summary>
    /// 关键字唯一标识，用于 EXTRA 关联引用。
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// 关键字显示名称（tooltip 标题）。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 别名数组，用于在卡牌描述中匹配该关键字。
    /// </summary>
    public string[] Names { get; set; } = [];

    /// <summary>
    /// tooltip 描述文本。
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 关联的其他关键字ID数组。匹配到本关键字时，关联关键字也会一并显示。
    /// </summary>
    public string[] Extra { get; set; } = [];
}
