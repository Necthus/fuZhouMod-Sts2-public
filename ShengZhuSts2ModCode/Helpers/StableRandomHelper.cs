using MegaCrit.Sts2.Core.Entities.Players;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 联机稳定随机辅助类：统一使用本局运行状态里的随机源，避免各客户端使用本地 Random 导致结果不同步。
/// </summary>
public static class StableRandomHelper
{
    /// <summary>
    /// 从 0 到指定上限之间取随机整数；上限无效时返回 0。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="exclusiveMax">不包含的上限。</param>
    /// <returns>随机下标。</returns>
    public static int NextInt(Player? player, int exclusiveMax)
    {
        if (exclusiveMax <= 0)
        {
            return 0;
        }

        return player?.RunState.Rng.Niche.NextInt(exclusiveMax) ?? 0;
    }

    /// <summary>
    /// 在闭区间内取随机整数。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="minInclusive">包含的下限。</param>
    /// <param name="maxInclusive">包含的上限。</param>
    /// <returns>随机整数。</returns>
    public static int NextIntInclusive(Player? player, int minInclusive, int maxInclusive)
    {
        if (maxInclusive <= minInclusive)
        {
            return minInclusive;
        }

        return minInclusive + NextInt(player, maxInclusive - minInclusive + 1);
    }

    /// <summary>
    /// 从候选列表中取一个随机元素；无玩家随机源时固定取第一个，避免本地随机分叉。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="candidates">候选列表。</param>
    /// <typeparam name="T">候选类型。</typeparam>
    /// <returns>随机候选；候选为空时返回默认值。</returns>
    public static T? PickOrDefault<T>(Player? player, IReadOnlyList<T> candidates)
    {
        if (candidates.Count == 0)
        {
            return default;
        }

        return candidates[NextInt(player, candidates.Count)];
    }

    /// <summary>
    /// 根据局种子和业务上下文生成稳定下标，不推进游戏随机源。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="exclusiveMax">不包含的上限。</param>
    /// <param name="scope">随机点名称。</param>
    /// <param name="contextParts">额外上下文。</param>
    /// <returns>稳定下标。</returns>
    public static int StableIndex(Player? player, int exclusiveMax, string scope, params string[] contextParts)
    {
        if (exclusiveMax <= 0)
        {
            return 0;
        }

        uint hash = 2166136261u;
        AddStableHash(ref hash, player?.RunState.Rng.StringSeed ?? "NO_SEED");
        AddStableHash(ref hash, player?.NetId.ToString() ?? "NO_PLAYER");
        AddStableHash(ref hash, player?.RunState.CurrentActIndex.ToString() ?? "NO_ACT");
        AddStableHash(ref hash, player?.RunState.ActFloor.ToString() ?? "NO_ACT_FLOOR");
        AddStableHash(ref hash, player?.RunState.TotalFloor.ToString() ?? "NO_TOTAL_FLOOR");
        AddStableHash(ref hash, player?.RunState.CurrentMapCoord?.col.ToString() ?? "NO_COL");
        AddStableHash(ref hash, player?.RunState.CurrentMapCoord?.row.ToString() ?? "NO_ROW");
        AddStableHash(ref hash, scope);
        foreach (string part in contextParts)
        {
            AddStableHash(ref hash, part);
        }

        return (int)(hash % (uint)exclusiveMax);
    }

    /// <summary>
    /// 从候选列表中稳定选择一个元素；会先按稳定键排序，避免底层枚举顺序变化影响结果。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="candidates">候选列表。</param>
    /// <param name="scope">随机点名称。</param>
    /// <param name="keySelector">候选稳定键。</param>
    /// <param name="contextParts">额外上下文。</param>
    /// <typeparam name="T">候选类型。</typeparam>
    /// <returns>稳定候选；候选为空时返回默认值。</returns>
    public static T? PickByStableHash<T>(Player? player, IReadOnlyList<T> candidates, string scope, Func<T, string> keySelector, params string[] contextParts)
    {
        if (candidates.Count == 0)
        {
            return default;
        }

        List<T> orderedCandidates = candidates
            .OrderBy(candidate => keySelector(candidate) ?? string.Empty, StringComparer.Ordinal)
            .ToList();
        string candidateKeys = string.Join(";", orderedCandidates.Select(candidate => keySelector(candidate) ?? string.Empty));
        string[] stableContextParts = contextParts.Concat([candidateKeys]).ToArray();
        return orderedCandidates[StableIndex(player, orderedCandidates.Count, scope, stableContextParts)];
    }

    /// <summary>
    /// 使用固定 FNV-1a 算法追加字符串，避免运行时随机盐影响结果。
    /// </summary>
    /// <param name="hash">当前哈希值。</param>
    /// <param name="value">追加内容。</param>
    private static void AddStableHash(ref uint hash, string value)
    {
        foreach (char character in value)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        hash ^= '|';
        hash *= 16777619u;
    }
}
