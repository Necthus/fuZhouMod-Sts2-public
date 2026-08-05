using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

/// <summary>
/// 圣主选人阶段 x 层剧情事件替换池联机同步服务。
/// 直接绑定 StartRunLobby.NetService，不依赖跑团内 RunLocationTargetedMessageBuffer。
/// </summary>
public static class StoryEventReplacementLobbySyncService
{
    /// <summary>
    /// 当前已注册的选人阶段网络服务。
    /// </summary>
    private static INetGameService? _registeredNetService;

    /// <summary>
    /// 确保当前 lobby 已注册配置同步消息。
    /// </summary>
    /// <param name="lobby">选人 lobby。</param>
    public static void EnsureRegistered(StartRunLobby? lobby)
    {
        INetGameService? netService = lobby?.NetService;
        if (netService == null)
        {
            return;
        }

        if (ReferenceEquals(_registeredNetService, netService))
        {
            return;
        }

        Unregister();

        netService.RegisterMessageHandler<ShengZhuStoryEventReplacementLobbyConfigMessage>(HandleConfigMessage);
        netService.RegisterMessageHandler<ShengZhuStoryEventReplacementLobbyConfigRequestMessage>(HandleConfigRequestMessage);
        _registeredNetService = netService;
        MainFile.Logger.Info($"【选人事件池联机】已注册 lobby 配置同步消息：类型={netService.Type}，本端={netService.NetId}。");
    }

    /// <summary>
    /// 反注册当前 lobby 配置同步消息。
    /// </summary>
    public static void Unregister()
    {
        if (_registeredNetService == null)
        {
            return;
        }

        try
        {
            _registeredNetService.UnregisterMessageHandler<ShengZhuStoryEventReplacementLobbyConfigMessage>(HandleConfigMessage);
            _registeredNetService.UnregisterMessageHandler<ShengZhuStoryEventReplacementLobbyConfigRequestMessage>(HandleConfigRequestMessage);
            MainFile.Logger.Info("【选人事件池联机】已反注册 lobby 配置同步消息。");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【选人事件池联机】反注册消息处理器时发生异常，已忽略：{ex}。");
        }
        finally
        {
            _registeredNetService = null;
        }
    }

    /// <summary>
    /// 本地用户修改配置后，先本地应用，再按当前联机身份发送消息。
    /// </summary>
    /// <param name="config">新配置。</param>
    /// <param name="source">来源说明。</param>
    public static void ApplyLocalConfigChange(StoryEventReplacementConfig config, string source)
    {
        StoryEventReplacementConfigService.ApplySelectionConfig(config, source);
        StoryEventReplacementConfigPanelService.RefreshCurrentPanel();

        if (_registeredNetService == null || _registeredNetService.Type == NetGameType.Singleplayer)
        {
            return;
        }

        ShengZhuStoryEventReplacementLobbyConfigMessage message =
            ShengZhuStoryEventReplacementLobbyConfigMessage.FromConfig(StoryEventReplacementConfigService.CurrentSelectionConfig);
        _registeredNetService.SendMessage(message);
        MainFile.Logger.Info($"【选人事件池联机】已发送配置同步消息：来源={source}，本端={_registeredNetService.NetId}，类型={_registeredNetService.Type}。");
    }

    /// <summary>
    /// 客户端注册好消息处理器后，向房主请求当前共享配置。
    /// </summary>
    public static void RequestCurrentConfigFromHost()
    {
        if (_registeredNetService?.Type != NetGameType.Client)
        {
            return;
        }

        _registeredNetService.SendMessage(new ShengZhuStoryEventReplacementLobbyConfigRequestMessage());
        MainFile.Logger.Info($"【选人事件池联机】已请求房主回发当前共享配置：本端={_registeredNetService.NetId}。");
    }

    /// <summary>
    /// 处理远端配置同步消息。
    /// </summary>
    /// <param name="message">配置消息。</param>
    /// <param name="senderId">发送者网络 ID。</param>
    private static void HandleConfigMessage(ShengZhuStoryEventReplacementLobbyConfigMessage message, ulong senderId)
    {
        StoryEventReplacementConfig config = message.ToConfig();
        StoryEventReplacementConfigService.ApplySelectionConfig(config, $"远端玩家 {senderId}");
        StoryEventReplacementConfigPanelService.RefreshCurrentPanel();
        MainFile.Logger.Info($"【选人事件池联机】已接收远端配置：发送者={senderId}，{config}。");
    }

    /// <summary>
    /// 处理客户端的配置请求；只有房主需要响应。
    /// </summary>
    /// <param name="message">请求消息。</param>
    /// <param name="senderId">请求者网络 ID。</param>
    private static void HandleConfigRequestMessage(ShengZhuStoryEventReplacementLobbyConfigRequestMessage message, ulong senderId)
    {
        if (_registeredNetService?.Type != NetGameType.Host)
        {
            return;
        }

        ShengZhuStoryEventReplacementLobbyConfigMessage response =
            ShengZhuStoryEventReplacementLobbyConfigMessage.FromConfig(StoryEventReplacementConfigService.CurrentSelectionConfig);
        _registeredNetService.SendMessage(response, senderId);
        MainFile.Logger.Info($"【选人事件池联机】已向新玩家回发当前共享配置：目标={senderId}，{StoryEventReplacementConfigService.CurrentSelectionConfig}。");
    }
}
