using MegaCrit.Sts2.Core.Entities.Players;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 动物符咒辅助类：统一维护十二生肖符咒的稳定顺序和基础统计逻辑。
/// </summary>
public static class TalismanHelper
{
    /// <summary>
    /// 十二生肖符咒遗物类型，顺序固定用于联机稳定随机上下文。
    /// </summary>
    public static readonly Type[] AnimalTalismanTypes =
    [
        typeof(RatTalisman),
        typeof(OxTalisman),
        typeof(TigerTalisman),
        typeof(RabbitTalisman),
        typeof(DragonTalisman),
        typeof(SnakeTalisman),
        typeof(HorseTalisman),
        typeof(SheepTalisman),
        typeof(MonkeyTalisman),
        typeof(RoosterTalisman),
        typeof(DogTalisman),
        typeof(PigTalisman)
    ];

    /// <summary>
    /// 统计玩家当前持有的动物符咒数量。
    /// </summary>
    /// <param name="player">待统计的玩家。</param>
    /// <returns>已持有的动物符咒数量。</returns>
    public static int CountOwnedAnimalTalismans(Player? player)
    {
        if (player == null)
        {
            return 0;
        }

        return AnimalTalismanTypes.Count(talismanType => HasTalisman(player, talismanType));
    }

    /// <summary>
    /// 判断玩家是否持有虎符咒。
    /// </summary>
    /// <param name="player">待检查的玩家。</param>
    /// <returns>持有虎符咒时返回 true。</returns>
    public static bool HasTigerTalisman(Player? player)
    {
        return player != null && HasTalisman(player, typeof(TigerTalisman));
    }

    /// <summary>
    /// 构建玩家已持有符咒的稳定字符串，用于联机稳定随机。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>按固定顺序拼接的符咒类型名。</returns>
    public static string BuildOwnedTalismanStableKey(Player? player)
    {
        if (player == null)
        {
            return "NO_PLAYER";
        }

        List<string> ownedKeys = [];
        foreach (Type talismanType in AnimalTalismanTypes)
        {
            if (HasTalisman(player, talismanType))
            {
                ownedKeys.Add(talismanType.FullName ?? talismanType.Name);
            }
        }

        return ownedKeys.Count == 0 ? "NO_TALISMAN" : string.Join(";", ownedKeys);
    }

    /// <summary>
    /// 判断玩家是否持有指定类型的符咒。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="talismanType">符咒类型。</param>
    /// <returns>持有该符咒时返回 true。</returns>
    private static bool HasTalisman(Player player, Type talismanType)
    {
        return player.Relics.Any(relic => relic.GetType() == talismanType);
    }
}
