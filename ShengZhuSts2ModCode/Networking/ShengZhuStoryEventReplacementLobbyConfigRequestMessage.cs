using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

/// <summary>
/// 圣主选人阶段配置请求消息。
/// 新加入客户端注册好消息处理器后，主动请求房主回发当前共享配置。
/// </summary>
public class ShengZhuStoryEventReplacementLobbyConfigRequestMessage : INetMessage, IPacketSerializable
{
    /// <summary>
    /// 请求消息只发给房主，不需要广播。
    /// </summary>
    public bool ShouldBroadcast => false;

    /// <summary>
    /// 请求消息需要可靠传输。
    /// </summary>
    public NetTransferMode Mode => NetTransferMode.Reliable;

    /// <summary>
    /// 消息日志级别。
    /// </summary>
    public LogLevel LogLevel => LogLevel.VeryDebug;

    /// <summary>
    /// 界面切换或联机缓冲期间保留消息。
    /// </summary>
    public bool ShouldBuffer => true;

    /// <summary>
    /// 写入消息内容。当前没有额外字段。
    /// </summary>
    /// <param name="writer">网络包写入器。</param>
    public void Serialize(PacketWriter writer)
    {
    }

    /// <summary>
    /// 读取消息内容。当前没有额外字段。
    /// </summary>
    /// <param name="reader">网络包读取器。</param>
    public void Deserialize(PacketReader reader)
    {
    }
}
