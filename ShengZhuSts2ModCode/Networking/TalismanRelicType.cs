using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

/// <summary>
/// 联机消息中使用的符咒遗物类型编号。
/// 使用固定编号而不是类型全名，避免重命名命名空间时旧消息字段失效。
/// </summary>
internal enum TalismanRelicType
{
    /// <summary>
    /// 未知符咒。
    /// </summary>
    None = 0,

    /// <summary>
    /// 鼠符咒。
    /// </summary>
    Rat = 1,

    /// <summary>
    /// 牛符咒。
    /// </summary>
    Ox = 2,

    /// <summary>
    /// 兔符咒。
    /// </summary>
    Rabbit = 3,

    /// <summary>
    /// 蛇符咒。
    /// </summary>
    Snake = 4,

    /// <summary>
    /// 马符咒。
    /// </summary>
    Horse = 5,

    /// <summary>
    /// 羊符咒。
    /// </summary>
    Sheep = 6,

    /// <summary>
    /// 猴符咒。
    /// </summary>
    Monkey = 7,

    /// <summary>
    /// 猪符咒。
    /// </summary>
    Pig = 8,

    /// <summary>
    /// 符咒探测仪。
    /// </summary>
    TalismanLocator = 9
}

/// <summary>
/// 符咒遗物类型编号转换工具。
/// </summary>
internal static class TalismanRelicTypeHelper
{
    /// <summary>
    /// 根据遗物实例获取固定类型编号。
    /// </summary>
    /// <param name="relic">遗物实例。</param>
    /// <returns>固定类型编号。</returns>
    public static TalismanRelicType FromRelic(object? relic)
    {
        return relic switch
        {
            RatTalisman => TalismanRelicType.Rat,
            OxTalisman => TalismanRelicType.Ox,
            RabbitTalisman => TalismanRelicType.Rabbit,
            SnakeTalisman => TalismanRelicType.Snake,
            HorseTalisman => TalismanRelicType.Horse,
            SheepTalisman => TalismanRelicType.Sheep,
            MonkeyTalisman => TalismanRelicType.Monkey,
            PigTalisman => TalismanRelicType.Pig,
            TalismanLocator => TalismanRelicType.TalismanLocator,
            _ => TalismanRelicType.None
        };
    }
}
