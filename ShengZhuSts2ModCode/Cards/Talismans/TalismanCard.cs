using MegaCrit.Sts2.Core.Entities.Cards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 动物符咒卡牌基类：十二生肖符咒卡牌的统一父类。
/// 不属于阿福体系，独立于 AhFuCard。
/// </summary>
public abstract class TalismanCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : ShengZhuSts2ModCard(cost, type, rarity, target)
{
    /// <summary>
    /// 判断当前卡牌持有者是否拥有指定动物符咒遗物。
    /// </summary>
    /// <typeparam name="T">要检查的动物符咒遗物类型。</typeparam>
    /// <returns>持有对应遗物时返回 true。</returns>
    protected bool HasTalismanRelic<T>() where T : class
    {
        return Owner?.Relics.Any(relic => relic is T) == true;
    }
}
