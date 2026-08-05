using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 选人界面圣主 x 层剧情事件替换池配置补丁。
/// </summary>
public static class CharacterSelectStoryEventConfigPatch
{
    /// <summary>
    /// 选人界面初始化结束后，初始化本次共享配置、注册 lobby 消息并创建 UI 面板。
    /// </summary>
    [HarmonyPatch(typeof(NCharacterSelectScreen), "AfterInitialized")]
    public static class AfterInitializedPatch
    {
        /// <summary>
        /// 初始化选人配置功能。
        /// </summary>
        /// <param name="__instance">选人界面。</param>
        public static void Postfix(NCharacterSelectScreen __instance)
        {
            StoryEventReplacementConfigService.InitializeSelectionFromJson();
            StoryEventReplacementLobbySyncService.EnsureRegistered(__instance.Lobby);
            StoryEventReplacementConfigPanelService.EnsurePanel(__instance);
            StoryEventReplacementLobbySyncService.RequestCurrentConfigFromHost();
        }
    }

    /// <summary>
    /// 本地选择角色后刷新配置面板显示状态。
    /// </summary>
    [HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
    public static class SelectCharacterPatch
    {
        /// <summary>
        /// 刷新配置面板。
        /// </summary>
        /// <param name="__instance">选人界面。</param>
        public static void Postfix(NCharacterSelectScreen __instance)
        {
            StoryEventReplacementConfigPanelService.Refresh(__instance);
        }
    }

    /// <summary>
    /// 任意玩家角色变化后刷新配置面板显示状态。
    /// </summary>
    [HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.PlayerChanged))]
    public static class PlayerChangedPatch
    {
        /// <summary>
        /// 刷新配置面板。
        /// </summary>
        /// <param name="__instance">选人界面。</param>
        public static void Postfix(NCharacterSelectScreen __instance)
        {
            StoryEventReplacementConfigPanelService.Refresh(__instance);
        }
    }

    /// <summary>
    /// 开始跑团前冻结当前共享配置。
    /// </summary>
    [HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.BeginRun))]
    public static class BeginRunPatch
    {
        /// <summary>
        /// 根据最终角色列表决定是否固化本局圣主配置。
        /// </summary>
        /// <param name="__instance">选人界面。</param>
        public static void Prefix(NCharacterSelectScreen __instance)
        {
            StoryEventReplacementConfigService.PreparePendingRunConfig(__instance.Lobby.Players.Select(player => player.character));
        }
    }

    /// <summary>
    /// 选人界面清理时，释放 UI 和 lobby 消息处理器。
    /// </summary>
    [HarmonyPatch(typeof(NCharacterSelectScreen), "CleanUpLobby")]
    public static class CleanUpLobbyPatch
    {
        /// <summary>
        /// 清理选人配置功能。
        /// </summary>
        public static void Prefix()
        {
            StoryEventReplacementConfigPanelService.Clear();
            StoryEventReplacementLobbySyncService.Unregister();
        }
    }
}
