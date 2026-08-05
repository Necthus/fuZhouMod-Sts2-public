using System.Text.Json;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>
/// 大法师老爹战斗状态：保存后续符咒封存、循环位置和随机结果等需要联机一致的数据。
/// </summary>
public sealed class GrandMageDadState
{
    /// <summary>
    /// 当前状态结构版本，后续字段变化时用于兼容旧存档。
    /// </summary>
    public int StateVersion { get; set; } = 1;

    /// <summary>
    /// 老爹已经执行过的行动次数。
    /// </summary>
    public int TurnIndex { get; set; }

    /// <summary>
    /// 当前副意图代码；第一版只记录，后续接双意图 UI 时读取。
    /// </summary>
    public string SecondaryIntentCode { get; set; } = string.Empty;

    /// <summary>
    /// 本回合累计承伤，后续给正气不灭和符咒返还使用。
    /// </summary>
    public int DamageTakenThisTurn { get; set; }

    /// <summary>
    /// 是否正在终极封印吟唱中；处决回合会读取它判断是否继续处决。
    /// </summary>
    public bool IsChanting { get; set; }

    /// <summary>
    /// 吟唱开始时记录的护盾值，用于读档和 UI 展示。
    /// </summary>
    public int ChantShieldAmount { get; set; }

    /// <summary>
    /// 序列化为存档字符串。
    /// </summary>
    /// <returns>JSON 字符串。</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    /// <summary>
    /// 从存档字符串恢复老爹状态；损坏时返回空状态，避免读档直接中断。
    /// </summary>
    /// <param name="json">存档里的 JSON 字符串。</param>
    /// <returns>恢复后的老爹状态。</returns>
    public static GrandMageDadState FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new GrandMageDadState();
        }

        try
        {
            return JsonSerializer.Deserialize<GrandMageDadState>(json) ?? new GrandMageDadState();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【联机异常兜底】【大法师老爹】阶段=读取自定义状态，原因=JSON解析失败，处理=使用空状态，异常={ex.Message}。");
            return new GrandMageDadState();
        }
    }
}
