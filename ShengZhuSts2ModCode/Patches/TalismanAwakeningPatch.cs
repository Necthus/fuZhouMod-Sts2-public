using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 十二符咒觉醒检查补丁：拦截遗物被添加到玩家的方法。
/// 策略：通过反射找到 Player.Relics 集合类型的 Add/AddInternal 方法并 Patch。
/// 如果所有方法都找不到，则依赖 BeforeCombatStart 兜底。
/// </summary>
[HarmonyPatch]
public static class TalismanAwakeningPatch
{
    /// <summary>
    /// 所有十二符咒遗物类型，用于快速前置过滤。
    /// </summary>
    private static readonly HashSet<Type> TalismanTypeSet =
    [
        typeof(RatTalisman),
        typeof(OxTalisman),
        typeof(TigerTalisman),
        typeof(RabbitTalisman),
        typeof(DragonTalisman),
        typeof(SnakeTalisman),
        typeof(HorseTalisman),
        typeof(SheepTalisman),
        typeof(MonkeyTalisman),
        typeof(RoosterTalisman),
        typeof(DogTalisman),
        typeof(PigTalisman)
    ];

    /// <summary>
    /// 记录实际 Patch 到的目标类型，用于 Postfix 中判断参数含义。
    /// </summary>
    private static string _patchedTarget = "";

    /// <summary>
    /// 动态指定要 Patch 的方法。
    /// 按优先级依次尝试多种策略。
    /// </summary>
    public static MethodBase? TargetMethod()
    {
        // 策略1：找 Player.Relics 属性的实际集合类型的 Add 方法
        MethodBase? result = TryFindRelicCollectionAddMethod();
        if (result != null) return result;

        // 策略2：找 RelicReward 的领取方法
        result = TryFindRelicRewardClaimMethod();
        if (result != null) return result;

        // 策略3：找 RelicModel.Owner setter
        result = TryFindOwnerSetter();
        if (result != null) return result;

        MainFile.Logger.Info("【十二符咒觉醒Patch】所有策略均未找到可 Patch 的方法，将依赖 BeforeCombatStart 兜底。");
        // 输出 Player.Relics 的类型信息帮助调试
        DumpRelicsTypeInfo();
        return null;
    }

    /// <summary>
    /// 策略1：通过 Player.Relics 属性找到底层集合的 Add 方法。
    /// </summary>
    private static MethodBase? TryFindRelicCollectionAddMethod()
    {
        PropertyInfo? relicsProp = typeof(Player).GetProperty("Relics", BindingFlags.Instance | BindingFlags.Public);
        if (relicsProp == null)
        {
            MainFile.Logger.Info("【十二符咒觉醒Patch】Player.Relics 属性不存在。");
            return null;
        }

        Type relicsType = relicsProp.PropertyType;
        MainFile.Logger.Info($"【十二符咒觉醒Patch】Player.Relics 类型: {relicsType.FullName}");

        // 尝试找 AddInternal、Add、AddRelic 等方法
        string[] methodNames = ["AddInternal", "Add", "AddRelic", "Insert"];
        foreach (string methodName in methodNames)
        {
            // 在声明类型上找
            MethodInfo? method = relicsType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method != null)
            {
                // 确认方法参数中包含 RelicModel 或其基类
                var parameters = method.GetParameters();
                if (parameters.Any(p => p.ParameterType.IsAssignableFrom(typeof(RelicModel)) ||
                                        typeof(RelicModel).IsAssignableFrom(p.ParameterType)))
                {
                    _patchedTarget = $"RelicCollection.{methodName}";
                    MainFile.Logger.Info($"【十二符咒觉醒Patch】策略1成功：{relicsType.Name}.{methodName}");
                    return method;
                }
            }
        }

        // 如果集合类型实现了 ICollection<RelicModel>，找接口的 Add 方法
        Type? collectionInterface = relicsType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(ICollection<>) &&
                                 i.GetGenericArguments()[0].IsAssignableFrom(typeof(RelicModel)));

        if (collectionInterface != null)
        {
            MethodInfo? addMethod = relicsType.GetMethod("Add",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [collectionInterface.GetGenericArguments()[0]], null);

            if (addMethod != null)
            {
                _patchedTarget = "RelicCollection.Add(ICollection)";
                MainFile.Logger.Info($"【十二符咒觉醒Patch】策略1成功（ICollection接口）：{relicsType.Name}.Add");
                return addMethod;
            }
        }

        MainFile.Logger.Info($"【十二符咒觉醒Patch】策略1失败：{relicsType.Name} 中未找到合适的 Add 方法。");
        return null;
    }

    /// <summary>
    /// 策略2：找 RelicReward 的领取方法。
    /// </summary>
    private static MethodBase? TryFindRelicRewardClaimMethod()
    {
        string[] candidateNames = ["OnClaim", "Claim", "OnSelect", "Apply", "OnCollect", "Collect"];
        foreach (string name in candidateNames)
        {
            MethodInfo? method = typeof(RelicReward).GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                _patchedTarget = $"RelicReward.{name}";
                MainFile.Logger.Info($"【十二符咒觉醒Patch】策略2成功：RelicReward.{name}");
                return method;
            }
        }

        MainFile.Logger.Info("【十二符咒觉醒Patch】策略2失败：RelicReward 中未找到领取方法。");
        return null;
    }

    /// <summary>
    /// 策略3：找 RelicModel.Owner setter。
    /// </summary>
    private static MethodBase? TryFindOwnerSetter()
    {
        PropertyInfo? ownerProp = typeof(RelicModel).GetProperty("Owner",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo? setter = ownerProp?.GetSetMethod(true);
        if (setter != null)
        {
            _patchedTarget = "RelicModel.Owner.set";
            MainFile.Logger.Info("【十二符咒觉醒Patch】策略3成功：RelicModel.Owner setter");
            return setter;
        }

        MainFile.Logger.Info("【十二符咒觉醒Patch】策略3失败：RelicModel.Owner 无 setter。");
        return null;
    }

    /// <summary>
    /// 输出 Player.Relics 的详细类型信息到日志，帮助后续调试。
    /// </summary>
    private static void DumpRelicsTypeInfo()
    {
        PropertyInfo? relicsProp = typeof(Player).GetProperty("Relics", BindingFlags.Instance | BindingFlags.Public);
        if (relicsProp == null) return;

        Type relicsType = relicsProp.PropertyType;
        MainFile.Logger.Info($"【十二符咒觉醒Patch调试】Relics类型: {relicsType.FullName}");
        MainFile.Logger.Info($"【十二符咒觉醒Patch调试】Relics基类: {relicsType.BaseType?.FullName}");
        MainFile.Logger.Info($"【十二符咒觉醒Patch调试】Relics接口: {string.Join(", ", relicsType.GetInterfaces().Select(i => i.Name))}");

        foreach (var method in relicsType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            string paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            MainFile.Logger.Info($"【十二符咒觉醒Patch调试】  方法: {method.Name}({paramStr}) -> {method.ReturnType.Name}");
        }
    }

    /// <summary>
    /// 后置补丁：根据 Patch 目标的不同，提取遗物和玩家信息并触发觉醒检查。
    /// </summary>
    public static void Postfix(object __instance, object[] __args)
    {
        try
        {
            Player? player = null;

            // 从参数或实例中提取遗物信息
            RelicModel? relic = ExtractRelic(__instance, __args);

            if (relic != null)
            {
                // 性能优化：只关心符咒类型
                if (!TalismanTypeSet.Contains(relic.GetType()))
                {
                    return;
                }

                player = relic.Owner;
            }

            // 如果从遗物拿不到 player，尝试从实例获取
            if (player == null && __instance is Player p)
            {
                player = p;
            }

            if (player == null)
            {
                return;
            }

            TalismanAwakeningHelper.CheckAndTriggerAwakening(player);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【十二符咒觉醒Patch】Postfix 异常：{ex.Message}");
        }
    }

    /// <summary>
    /// 从 Patch 的实例和参数中提取遗物对象。
    /// </summary>
    private static RelicModel? ExtractRelic(object __instance, object[] __args)
    {
        // 如果实例本身就是 RelicModel
        if (__instance is RelicModel relicModel)
        {
            return relicModel;
        }

        // 如果实例是 RelicReward，从私有字段获取
        if (__instance is RelicReward relicReward)
        {
            FieldInfo? relicField = typeof(RelicReward).GetField("_relic", BindingFlags.Instance | BindingFlags.NonPublic);
            return relicField?.GetValue(relicReward) as RelicModel;
        }

        // 从参数中找 RelicModel
        foreach (object? arg in __args)
        {
            if (arg is RelicModel argRelic)
            {
                return argRelic;
            }
        }

        return null;
    }
}
