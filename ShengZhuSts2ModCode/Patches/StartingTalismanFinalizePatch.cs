using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 开局符咒发放补丁：等待初始遗物获得流程结束后，再为符咒探测仪按配置补发随机符咒。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.FinalizeStartingRelics))]
public static class StartingTalismanFinalizePatch
{
    /// <summary>
    /// 包装原始初始遗物任务，避免在原方法枚举玩家遗物列表时直接新增遗物。
    /// </summary>
    /// <param name="__result">原始初始遗物任务。</param>
    /// <param name="__instance">当前跑团管理器。</param>
    public static void Postfix(ref Task __result, RunManager __instance)
    {
        __result = GrantStartingTalismansAfterOriginal(__result, __instance);
    }

    /// <summary>
    /// 等原始初始遗物流程完成后，为每个符咒探测仪持有者按配置补发稳定随机符咒。
    /// </summary>
    /// <param name="originalTask">原始初始遗物任务。</param>
    /// <param name="runManager">当前跑团管理器。</param>
    /// <returns>包装后的异步任务。</returns>
    private static async Task GrantStartingTalismansAfterOriginal(Task originalTask, RunManager runManager)
    {
        await originalTask;

        RunState? runState = runManager.DebugOnlyGetState();
        if (runState == null)
        {
            MainFile.Logger.Info("【开局符咒】初始遗物流程结束后未找到运行状态，跳过补发。");
            return;
        }

        foreach (TalismanLocator locator in runState.Players.SelectMany(player => player.Relics.OfType<TalismanLocator>()).ToList())
        {
            StoryEventReplacementConfigService.TryApplyPendingRunConfig(locator);
            await StartingTalismanGrantService.TryGrantStartingTalisman(locator);
        }

        StoryEventReplacementConfigService.ClearPendingRunConfig();
    }
}
