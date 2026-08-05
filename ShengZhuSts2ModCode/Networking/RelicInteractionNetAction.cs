using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

/// <summary>
/// 遗物交互的官方动作队列网络数据。
/// 这里只保存稳定枚举和参数，具体遗物实例在执行时按玩家重新查找。
/// </summary>
public struct RelicInteractionNetAction : INetAction
{
    /// <summary>
    /// 遗物交互类型。
    /// </summary>
    private RelicInteractionType _interactionType;

    /// <summary>
    /// 本次交互涉及的目标符咒类型。
    /// </summary>
    private TalismanRelicType _targetRelicType;

    /// <summary>
    /// 通用布尔参数。
    /// </summary>
    private bool _boolValue;

    /// <summary>
    /// 通用整数参数。
    /// </summary>
    private int _intValue;

    /// <summary>
    /// 创建遗物交互网络动作。
    /// </summary>
    /// <param name="interactionType">遗物交互类型。</param>
    /// <param name="targetRelicType">目标符咒类型。</param>
    /// <param name="boolValue">通用布尔参数。</param>
    /// <param name="intValue">通用整数参数。</param>
    internal RelicInteractionNetAction(RelicInteractionType interactionType, TalismanRelicType targetRelicType, bool boolValue, int intValue)
    {
        _interactionType = interactionType;
        _targetRelicType = targetRelicType;
        _boolValue = boolValue;
        _intValue = intValue;
    }

    /// <summary>
    /// 将网络数据还原成官方动作队列里的遗物交互动作。
    /// </summary>
    /// <param name="player">本次动作所属玩家，由游戏联机层根据发送者确定。</param>
    /// <returns>可进入动作队列执行的遗物交互动作。</returns>
    public GameAction ToGameAction(Player player)
    {
        return new RelicInteractionGameAction(player, _interactionType, _targetRelicType, _boolValue, _intValue);
    }

    /// <summary>
    /// 写入网络包。
    /// </summary>
    /// <param name="writer">网络包写入器。</param>
    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt((int)_interactionType);
        writer.WriteInt((int)_targetRelicType);
        writer.WriteBool(_boolValue);
        writer.WriteInt(_intValue);
    }

    /// <summary>
    /// 读取网络包。
    /// </summary>
    /// <param name="reader">网络包读取器。</param>
    public void Deserialize(PacketReader reader)
    {
        _interactionType = (RelicInteractionType)reader.ReadInt();
        _targetRelicType = (TalismanRelicType)reader.ReadInt();
        _boolValue = reader.ReadBool();
        _intValue = reader.ReadInt();
    }

    /// <summary>
    /// 返回调试文本，方便日志定位具体遗物交互。
    /// </summary>
    /// <returns>调试文本。</returns>
    public override string ToString()
    {
        return $"RelicInteractionNetAction type={_interactionType}, target={_targetRelicType}, bool={_boolValue}, int={_intValue}";
    }
}
