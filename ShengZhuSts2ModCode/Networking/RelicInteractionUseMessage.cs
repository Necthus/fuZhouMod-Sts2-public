using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Runs;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

/// <summary>
/// 通用遗物战斗交互联机消息。
/// 消息只同步玩家意图，真正的战斗表现仍复用各遗物原有逻辑。
/// </summary>
public class RelicInteractionUseMessage : INetMessage, IPacketSerializable, IRunLocationTargetedMessage
{
    /// <summary>
    /// 是否允许消息广播给同局其它玩家。
    /// </summary>
    public bool ShouldBroadcast => true;

    /// <summary>
    /// 遗物交互会影响后续战斗分支，必须可靠传输。
    /// </summary>
    public NetTransferMode Mode => NetTransferMode.Reliable;

    /// <summary>
    /// 消息日志级别。
    /// </summary>
    public LogLevel LogLevel => LogLevel.VeryDebug;

    /// <summary>
    /// 房间切换或联机同步暂停期间保留消息，避免遗物交互丢失。
    /// </summary>
    public bool ShouldBuffer => true;

    /// <summary>
    /// 消息对应的跑图位置，用于避免房间切换期间错时处理。
    /// </summary>
    public RunLocation Location { get; set; }

    /// <summary>
    /// 发起本次遗物交互的玩家网络 ID。
    /// </summary>
    public ulong PlayerNetId { get; set; }

    /// <summary>
    /// 遗物交互类型。
    /// </summary>
    internal RelicInteractionType InteractionType { get; set; }

    /// <summary>
    /// 本次交互涉及的目标符咒类型。
    /// </summary>
    internal TalismanRelicType TargetRelicType { get; set; }

    /// <summary>
    /// 通用布尔参数。
    /// 例如：是否半自动触发、是否激发、探测仪是否半自动。
    /// </summary>
    public bool BoolValue { get; set; }

    /// <summary>
    /// 通用整数参数。
    /// 例如：黑影令牌当前挡位。
    /// </summary>
    public int IntValue { get; set; }

    /// <summary>
    /// 第二个通用整数参数。
    /// 例如：潘库宝盒休息处附魔的魔气类型。
    /// </summary>
    public int SecondIntValue { get; set; }

    /// <summary>
    /// 第二个通用布尔参数。
    /// 例如：被选中牌是否升级。
    /// </summary>
    public bool SecondBoolValue { get; set; }

    /// <summary>
    /// 通用字符串参数。
    /// 例如：被选择的目标牌ID。
    /// </summary>
    public string StringValue { get; set; } = string.Empty;

    /// <summary>
    /// 第二个通用字符串参数。
    /// 例如：变化后的新牌ID。
    /// </summary>
    public string SecondStringValue { get; set; } = string.Empty;

    /// <summary>
    /// 写入消息内容。
    /// </summary>
    /// <param name="writer">网络包写入器。</param>
    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(PlayerNetId);
        writer.WriteInt((int)InteractionType);
        writer.WriteInt((int)TargetRelicType);
        writer.WriteBool(BoolValue);
        writer.WriteInt(IntValue);
        writer.WriteInt(SecondIntValue);
        writer.WriteBool(SecondBoolValue);
        writer.WriteString(StringValue);
        writer.WriteString(SecondStringValue);
        writer.Write(Location);
    }

    /// <summary>
    /// 读取消息内容。
    /// </summary>
    /// <param name="reader">网络包读取器。</param>
    public void Deserialize(PacketReader reader)
    {
        PlayerNetId = reader.ReadULong();
        InteractionType = (RelicInteractionType)reader.ReadInt();
        TargetRelicType = (TalismanRelicType)reader.ReadInt();
        BoolValue = reader.ReadBool();
        IntValue = reader.ReadInt();
        SecondIntValue = reader.ReadInt();
        SecondBoolValue = reader.ReadBool();
        StringValue = reader.ReadString();
        SecondStringValue = reader.ReadString();
        Location = reader.Read<RunLocation>();
    }
}
