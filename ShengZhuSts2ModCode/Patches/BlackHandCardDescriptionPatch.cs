using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Keywords;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 捞人黑手帮词条显示补丁：给被【捞人】临时纳入黑手帮体系的卡牌追加卡面词条。
/// </summary>
[HarmonyPatch]
public static class BlackHandCardDescriptionPatch
{
    /// <summary>
    /// 实际找到的卡牌描述方法；找不到时跳过补丁，避免 Mod 初始化失败。
    /// </summary>
    private static MethodBase? _targetMethod;

    /// <summary>
    /// Harmony 准备阶段：只在找到精确目标方法时启用补丁。
    /// </summary>
    /// <returns>找到目标方法时返回 true，否则跳过本补丁。</returns>
    public static bool Prepare()
    {
        _targetMethod = FindDescriptionMethod();
        if (_targetMethod != null)
        {
            MainFile.Logger.Info($"【捞人词条显示Patch】已找到卡牌描述方法：{FormatMethod(_targetMethod)}。");
            return true;
        }

        MainFile.Logger.Info("【捞人词条显示Patch】未找到 CardModel.GetDescriptionForPile 字符串方法，已跳过卡面词条追加，避免初始化失败。");
        return false;
    }

    /// <summary>
    /// 动态指定要 Patch 的卡牌描述方法。
    /// </summary>
    /// <returns>已找到的方法。</returns>
    public static MethodBase? TargetMethod()
    {
        return _targetMethod;
    }

    /// <summary>
    /// 在卡牌描述生成后，给被捞人标记的非原生黑手帮牌追加黑手帮词条。
    /// </summary>
    /// <param name="__instance">正在生成描述的卡牌。</param>
    /// <param name="__result">原始描述文本。</param>
    public static void Postfix(CardModel __instance, ref string __result)
    {
        if (!ShouldAppendBlackHandKeyword(__instance, __result))
        {
            return;
        }

        string keywordLine = ResolveBlackHandKeywordLine();
        __result = string.IsNullOrEmpty(__result)
            ? keywordLine
            : __result + "\n" + keywordLine;
    }

    /// <summary>
    /// 查找卡牌描述方法，避免使用特性直接绑定导致版本差异时 PatchAll 崩溃。
    /// </summary>
    /// <returns>找到的描述方法；找不到时返回 null。</returns>
    private static MethodBase? FindDescriptionMethod()
    {
        MethodInfo[] methods = typeof(CardModel)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        MethodInfo? currentVersionMethod = methods.FirstOrDefault(method =>
        {
            if (method.Name != nameof(CardModel.GetDescriptionForPile) || method.ReturnType != typeof(string) || !method.IsPublic)
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 2
                   && parameters[0].ParameterType == typeof(PileType)
                   && parameters[1].ParameterType == typeof(Creature);
        });
        if (currentVersionMethod != null)
        {
            return currentVersionMethod;
        }

        // 兼容后续版本：只要公开描述方法仍以 PileType 开头，就可以安全追加纯显示文本。
        return methods.FirstOrDefault(method =>
        {
            if (method.Name != nameof(CardModel.GetDescriptionForPile) || method.ReturnType != typeof(string) || !method.IsPublic)
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length >= 1 && parameters[0].ParameterType == typeof(PileType);
        });
    }

    /// <summary>
    /// 格式化补丁目标方法，方便日志确认是否命中正确重载。
    /// </summary>
    /// <param name="method">补丁目标方法。</param>
    /// <returns>便于阅读的方法签名。</returns>
    private static string FormatMethod(MethodBase method)
    {
        string parameters = string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name));
        return $"{method.DeclaringType?.Name}.{method.Name}({parameters})";
    }

    /// <summary>
    /// 判断当前卡牌是否需要追加黑手帮词条。
    /// </summary>
    /// <param name="card">待检查卡牌。</param>
    /// <param name="description">当前描述文本。</param>
    /// <returns>需要追加时返回 true。</returns>
    private static bool ShouldAppendBlackHandKeyword(CardModel? card, string? description)
    {
        if (card == null || card is BlackHandGangCard || !BlackHandCardHelper.IsBlackHandCard(card))
        {
            return false;
        }

        return string.IsNullOrEmpty(description)
            || (!description.Contains("*黑手帮*", StringComparison.OrdinalIgnoreCase)
                && !description.Contains("*Blackhand*", StringComparison.OrdinalIgnoreCase)
                && !description.Contains("[color=#E6C14A]黑手帮[/color]", StringComparison.OrdinalIgnoreCase)
                && !description.Contains("[color=#E6C14A]Blackhand[/color]", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 按当前语言返回要追加到卡面描述中的黑手帮词条文本。
    /// </summary>
    /// <returns>本地化后的黑手帮词条行。</returns>
    private static string ResolveBlackHandKeywordLine()
    {
        return KeywordRegistry.LoadedLanguage == "zhs"
            ? "[color=#E6C14A]黑手帮[/color]"
            : "[color=#E6C14A]Blackhand[/color]";
    }
}
