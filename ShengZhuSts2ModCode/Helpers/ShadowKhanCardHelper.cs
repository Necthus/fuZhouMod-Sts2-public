using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 黑影兵团卡牌创建辅助类：提供安全的反射创建卡牌方法和兵团类型列表。
/// </summary>
public static class ShadowKhanCardHelper
{
    /// <summary>
    /// 所有可随机生成的兵团牌类型。
    /// </summary>
    public static readonly Type[] ShadowKhanTypes =
    [
        typeof(KaBoPincer),
        typeof(SaMoTroll),
        typeof(LaZuoBlade),
        typeof(LeiSuAlien),
        typeof(ManNiMantis),
        typeof(MingTaShadow),
        typeof(BaTeBat),
        typeof(YiKaSamurai)
    ];

    /// <summary>
    /// 通过反射调用泛型 CreateCard 方法，根据运行时类型创建卡牌。
    /// 使用 GetMethods 筛选避免 AmbiguousMatchException。
    /// </summary>
    /// <param name="combatState">战斗状态对象。</param>
    /// <param name="cardType">要创建的卡牌类型。</param>
    /// <param name="owner">玩家对象。</param>
    /// <returns>创建的卡牌实例，失败返回 null。</returns>
    public static CardModel? CreateCardByType(object combatState, Type cardType, Player owner)
    {
        // 使用 GetMethods 筛选，避免多个重载导致 AmbiguousMatchException
        var method = combatState.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "CreateCard"
                                 && m.IsGenericMethodDefinition
                                 && m.GetGenericArguments().Length == 1
                                 && m.GetParameters().Length == 1
                                 && m.GetParameters()[0].ParameterType == typeof(Player));

        if (method == null)
        {
            MainFile.Logger.Info("【ShadowKhanCardHelper】未找到匹配的 CreateCard<T>(Player) 方法。");
            return null;
        }

        try
        {
            var genericMethod = method.MakeGenericMethod(cardType);
            return genericMethod.Invoke(combatState, [owner]) as CardModel;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【ShadowKhanCardHelper】创建卡牌失败：类型={cardType.Name}，异常={ex.Message}");
            return null;
        }
    }
}
