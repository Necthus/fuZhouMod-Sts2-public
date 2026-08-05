using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 傲洛巴斯之触补丁：把符咒探测仪的上位遗物从头环改为十二符咒罗盘。
/// </summary>
[HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
public static class TouchOfOrobasTalismanLocatorUpgradePatch
{
    /// <summary>
    /// 当待升级初始遗物是符咒探测仪时，返回十二符咒罗盘。
    /// </summary>
    /// <param name="starterRelic">待升级初始遗物。</param>
    /// <param name="__result">上位遗物结果。</param>
    /// <returns>已经处理时返回 false，跳过原方法。</returns>
    public static bool Prefix(RelicModel starterRelic, ref RelicModel __result)
    {
        if (starterRelic is not TalismanLocator || starterRelic is TwelveTalismansCompass)
        {
            return true;
        }

        __result = ModelDb.Relic<TwelveTalismansCompass>().ToMutable();
        MainFile.Logger.Info("【傲洛巴斯之触】符咒探测仪上位遗物已替换为十二符咒罗盘。");
        return false;
    }
}

/// <summary>
/// 傲洛巴斯之触获得后补丁：替换符咒探测仪时保留当前挡位。
/// </summary>
[HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.AfterObtained))]
public static class TouchOfOrobasTalismanLocatorModePatch
{
    /// <summary>
    /// 当玩家持有符咒探测仪时，手动执行替换以保留半自动/手动挡。
    /// </summary>
    /// <param name="__instance">傲洛巴斯之触实例。</param>
    /// <param name="__result">替换任务。</param>
    /// <returns>需要走原逻辑时返回 true。</returns>
    public static bool Prefix(TouchOfOrobas __instance, ref Task __result)
    {
        TalismanLocator? locator = __instance.Owner?.Relics
            .OfType<TalismanLocator>()
            .FirstOrDefault(relic => relic.GetType() == typeof(TalismanLocator));

        if (locator == null)
        {
            return true;
        }

        __result = ReplaceLocator(locator);
        return false;
    }

    /// <summary>
    /// 用十二符咒罗盘替换符咒探测仪，并复制当前挡位。
    /// </summary>
    /// <param name="locator">当前持有的符咒探测仪。</param>
    private static async Task ReplaceLocator(TalismanLocator locator)
    {
        TwelveTalismansCompass compass = (TwelveTalismansCompass)ModelDb.Relic<TwelveTalismansCompass>().ToMutable();
        compass.SemiAutoMode = locator.SemiAutoMode;
        await RelicCmd.Replace(locator, compass);
        MainFile.Logger.Info($"【傲洛巴斯之触】已将符咒探测仪替换为十二符咒罗盘，并保留挡位={(compass.SemiAutoMode ? "半自动" : "手动")}。");
    }
}
