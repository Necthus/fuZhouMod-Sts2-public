using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

/// <summary>
/// 圣主选人阶段 x 层剧情事件替换池配置同步消息。
/// 消息在 lobby 阶段使用，所有同局玩家都需要接收同一份共享配置。
/// </summary>
public class ShengZhuStoryEventReplacementLobbyConfigMessage : INetMessage, IPacketSerializable
{
    /// <summary>
    /// 消息版本，后续字段变化时用于兼容。
    /// </summary>
    private const int MessageVersion = 1;

    /// <summary>
    /// 允许房主把客户端消息广播给其它客户端。
    /// </summary>
    public bool ShouldBroadcast => true;

    /// <summary>
    /// 选人配置必须可靠传输。
    /// </summary>
    public NetTransferMode Mode => NetTransferMode.Reliable;

    /// <summary>
    /// 消息日志级别。
    /// </summary>
    public LogLevel LogLevel => LogLevel.VeryDebug;

    /// <summary>
    /// 界面切换或联机缓冲期间保留消息，避免配置丢失。
    /// </summary>
    public bool ShouldBuffer => true;

    /// <summary>
    /// 目标楼层。
    /// </summary>
    public int ActNumber { get; set; }

    /// <summary>
    /// 事件中文名列表。
    /// </summary>
    public List<string> EventNames { get; set; } = [];

    /// <summary>
    /// 从配置创建同步消息。
    /// </summary>
    /// <param name="config">剧情事件替换池配置。</param>
    /// <returns>同步消息。</returns>
    public static ShengZhuStoryEventReplacementLobbyConfigMessage FromConfig(StoryEventReplacementConfig config)
    {
        return new ShengZhuStoryEventReplacementLobbyConfigMessage
        {
            ActNumber = config.ActNumber,
            EventNames = config.EventNames.ToList()
        };
    }

    /// <summary>
    /// 转回剧情事件替换池配置。
    /// </summary>
    /// <returns>校验后的配置。</returns>
    public StoryEventReplacementConfig ToConfig()
    {
        return StoryEventReplacementConfigService.ValidateConfig(ActNumber, EventNames, fallbackWhenAllInvalid: false);
    }

    /// <summary>
    /// 写入消息内容。
    /// </summary>
    /// <param name="writer">网络包写入器。</param>
    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(MessageVersion, 4);
        writer.WriteInt(ActNumber, 3);
        writer.WriteInt(EventNames.Count, 3);
        foreach (string eventName in EventNames)
        {
            writer.WriteString(eventName);
        }
    }

    /// <summary>
    /// 读取消息内容。
    /// </summary>
    /// <param name="reader">网络包读取器。</param>
    public void Deserialize(PacketReader reader)
    {
        _ = reader.ReadInt(4);
        ActNumber = reader.ReadInt(3);
        int count = reader.ReadInt(3);
        EventNames.Clear();
        for (int i = 0; i < count; i++)
        {
            EventNames.Add(reader.ReadString());
        }
    }
}
