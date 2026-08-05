using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

/// <summary>
/// 遗物交互的官方动作队列动作。
/// 联机战斗内的右键和自动挡效果通过它统一排队，避免自定义消息回调直接修改战斗状态。
/// </summary>
internal sealed class RelicInteractionGameAction : GameAction
{
    /// <summary>
    /// 本次动作所属玩家。
    /// </summary>
    private readonly Player _player;

    /// <summary>
    /// 遗物交互类型。
    /// </summary>
    private readonly RelicInteractionType _interactionType;

    /// <summary>
    /// 本次交互涉及的目标符咒类型。
    /// </summary>
    private readonly TalismanRelicType _targetRelicType;

    /// <summary>
    /// 通用布尔参数。
    /// </summary>
    private readonly bool _boolValue;

    /// <summary>
    /// 通用整数参数。
    /// </summary>
    private readonly int _intValue;

    /// <summary>
    /// 本动作的玩家网络 ID。
    /// </summary>
    public override ulong OwnerId => _player.NetId;

    /// <summary>
    /// 遗物右键和自动挡属于玩家战斗操作，敌方行动期间由游戏动作队列延后。
    /// </summary>
    public override GameActionType ActionType => GameActionType.CombatPlayPhaseOnly;

    /// <summary>
    /// 创建遗物交互动作。
    /// </summary>
    /// <param name="player">动作所属玩家。</param>
    /// <param name="interactionType">遗物交互类型。</param>
    /// <param name="targetRelicType">目标符咒类型。</param>
    /// <param name="boolValue">通用布尔参数。</param>
    /// <param name="intValue">通用整数参数。</param>
    internal RelicInteractionGameAction(Player player, RelicInteractionType interactionType, TalismanRelicType targetRelicType, bool boolValue, int intValue)
    {
        _player = player;
        _interactionType = interactionType;
        _targetRelicType = targetRelicType;
        _boolValue = boolValue;
        _intValue = intValue;
    }

    /// <summary>
    /// 在官方动作队列中执行遗物交互。
    /// </summary>
    /// <returns>异步任务。</returns>
    protected override async Task ExecuteAction()
    {
        MainFile.Logger.Info($"【遗物联机Action】开始执行：玩家={OwnerId}，类型={_interactionType}，目标={_targetRelicType}，布尔参数={_boolValue}，整数参数={_intValue}。");
        try
        {
            await RelicInteractionSyncService.ExecuteQueuedInteraction(
                _player,
                _interactionType,
                _targetRelicType,
                _boolValue,
                _intValue,
                new GameActionPlayerChoiceContext(this));
        }
        finally
        {
            RelicInteractionSyncService.MarkQueuedInteractionFinished(OwnerId, _interactionType, _targetRelicType, _boolValue, _intValue);
            MainFile.Logger.Info($"【遗物联机Action】执行结束：玩家={OwnerId}，类型={_interactionType}，目标={_targetRelicType}。");
        }
    }

    /// <summary>
    /// 动作取消时释放本地待执行标记，避免一次取消后右键永久被锁。
    /// </summary>
    protected override void CancelAction()
    {
        RelicInteractionSyncService.MarkQueuedInteractionFinished(OwnerId, _interactionType, _targetRelicType, _boolValue, _intValue);
        MainFile.Logger.Info($"【遗物联机Action】动作被取消：玩家={OwnerId}，类型={_interactionType}，目标={_targetRelicType}。");
    }

    /// <summary>
    /// 转成联机可序列化的动作数据。
    /// </summary>
    /// <returns>网络动作数据。</returns>
    public override INetAction ToNetAction()
    {
        return new RelicInteractionNetAction(_interactionType, _targetRelicType, _boolValue, _intValue);
    }

    /// <summary>
    /// 返回调试文本，方便动作队列日志定位。
    /// </summary>
    /// <returns>调试文本。</returns>
    public override string ToString()
    {
        return $"RelicInteractionGameAction player={OwnerId}, type={_interactionType}, target={_targetRelicType}, bool={_boolValue}, int={_intValue}";
    }
}
