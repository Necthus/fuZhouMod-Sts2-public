using System.Reflection;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 十二符咒共鸣描述动态切换补丁：
/// 拦截 RelicModel 的描述 LocString 获取，当符咒持有鼠符咒时返回共鸣版描述。
/// 
/// 策略：通过反射找到 RelicModel 中返回描述 LocString 的属性 getter 并 Patch。
/// 候选属性名：Description, DescriptionLocString, _descriptionLocString 等。
/// </summary>
[HarmonyPatch]
public static class TalismanDescriptionPatch
{
    /// <summary>
    /// 记录实际 Patch 到的目标，用于日志。
    /// </summary>
    private static string _patchedTarget = "";

    /// <summary>
    /// 动态指定要 Patch 的方法。
    /// </summary>
    public static MethodBase? TargetMethod()
    {
        // 策略1：找 RelicModel.Description 属性 getter
        MethodBase? result = TryFindDescriptionGetter("Description");
        if (result != null) return result;

        // 策略2：找 RelicModel.DescriptionLocString 属性 getter
        result = TryFindDescriptionGetter("DescriptionLocString");
        if (result != null) return result;

        // 策略3：找 CustomRelicModel.Description 属性 getter
        result = TryFindDescriptionGetterOnType(typeof(CustomRelicModel), "Description");
        if (result != null) return result;

        // 策略4：遍历 RelicModel 所有返回 LocString 的属性
        result = TryFindAnyLocStringProperty();
        if (result != null) return result;

        MainFile.Logger.Info("【符咒描述Patch】所有策略均未找到可 Patch 的描述属性，共鸣描述切换将不生效。");
        DumpRelicModelProperties();
        return null;
    }

    /// <summary>
    /// 在 RelicModel 上查找指定名称的属性 getter。
    /// </summary>
    private static MethodBase? TryFindDescriptionGetter(string propertyName)
    {
        return TryFindDescriptionGetterOnType(typeof(RelicModel), propertyName);
    }

    /// <summary>
    /// 在指定类型上查找指定名称的属性 getter。
    /// </summary>
    private static MethodBase? TryFindDescriptionGetterOnType(Type type, string propertyName)
    {
        PropertyInfo? prop = type.GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (prop == null) return null;

        MethodInfo? getter = prop.GetGetMethod(true);
        if (getter == null) return null;

        // 确认返回类型是 LocString 或包含 LocString
        if (getter.ReturnType == typeof(LocString) || getter.ReturnType == typeof(string))
        {
            _patchedTarget = $"{type.Name}.{propertyName}.get";
            MainFile.Logger.Info($"【符咒描述Patch】成功找到目标：{_patchedTarget}，返回类型={getter.ReturnType.Name}");
            return getter;
        }

        return null;
    }

    /// <summary>
    /// 遍历 RelicModel 所有属性，找到名称中包含 "desc" 且返回 LocString 的属性。
    /// </summary>
    private static MethodBase? TryFindAnyLocStringProperty()
    {
        foreach (PropertyInfo prop in typeof(RelicModel).GetProperties(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (prop.PropertyType != typeof(LocString)) continue;
            if (!prop.Name.Contains("escription", StringComparison.OrdinalIgnoreCase)) continue;

            MethodInfo? getter = prop.GetGetMethod(true);
            if (getter != null)
            {
                _patchedTarget = $"RelicModel.{prop.Name}.get (fallback)";
                MainFile.Logger.Info($"【符咒描述Patch】回退策略成功：{_patchedTarget}");
                return getter;
            }
        }

        return null;
    }

    /// <summary>
    /// 输出 RelicModel 的所有属性信息到日志，帮助调试。
    /// </summary>
    private static void DumpRelicModelProperties()
    {
        MainFile.Logger.Info("【符咒描述Patch调试】RelicModel 属性列表：");
        foreach (PropertyInfo prop in typeof(RelicModel).GetProperties(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            MainFile.Logger.Info($"  {prop.PropertyType.Name} {prop.Name} (get={prop.CanRead}, set={prop.CanWrite})");
        }

        MainFile.Logger.Info("【符咒描述Patch调试】CustomRelicModel 属性列表：");
        foreach (PropertyInfo prop in typeof(CustomRelicModel).GetProperties(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            MainFile.Logger.Info($"  {prop.PropertyType.Name} {prop.Name} (get={prop.CanRead}, set={prop.CanWrite})");
        }
    }

    /// <summary>
    /// 后置补丁：如果当前遗物是我们的符咒且持有鼠符咒，替换返回的描述。
    /// </summary>
    public static void Postfix(RelicModel __instance, ref object __result)
    {
        try
        {
            if (__instance is not ShengZhuSts2ModRelic talisman)
            {
                return;
            }

            // 鼠符咒自身不切换
            if (talisman is RatTalisman)
            {
                return;
            }

            // 安全检查：规范模型（Canonical）访问 Owner 会抛异常，
            // 通过 IsMutable 或 try-catch 判断是否为可变副本。
            if (!IsOwnedMutableRelic(talisman))
            {
                return;
            }

            string dynamicKey = talisman.GetDynamicDescriptionKey();
            string baseKey = talisman.Id.Entry + ".description";

            // 如果动态key和基础key相同，不需要替换
            if (dynamicKey == baseKey)
            {
                if (__result is LocString sameLocString)
                {
                    talisman.CustomizeDescription(sameLocString);
                }
                return;
            }

            // 根据返回类型替换结果
            if (__result is LocString)
            {
                LocString locString = new("relics", dynamicKey);
                talisman.CustomizeDescription(locString);
                __result = locString;
            }
            else if (__result is string)
            {
                var locString = new LocString("relics", dynamicKey);
                talisman.CustomizeDescription(locString);
                __result = locString.ToString()!;
            }
        }
        catch (Exception)
        {
            // 静默忽略：规范模型或其他异常情况不影响游戏运行
        }
    }

    /// <summary>
    /// 安全检查遗物是否为已分配 Owner 的可变副本。
    /// 规范模型访问 Owner 会抛异常，这里用 try-catch 兜底。
    /// </summary>
    private static bool IsOwnedMutableRelic(ShengZhuSts2ModRelic talisman)
    {
        try
        {
            return talisman.Owner != null;
        }
        catch
        {
            return false;
        }
    }
}
