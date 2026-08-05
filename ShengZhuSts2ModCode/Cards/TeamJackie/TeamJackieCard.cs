using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 龙小组卡牌基类：统一标记合作遗物专属卡牌。
/// </summary>
public abstract class TeamJackieCard(int cost, CardType type, TargetType target)
    : ShengZhuSts2ModCard(cost, type, CardRarity.Event, target)
{
}

/// <summary>
/// 龙小组卡牌识别工具：用于奖励注入和猴符咒放行。
/// </summary>
public static class TeamJackieCardHelper
{
    /// <summary>
    /// 判断指定卡牌是否属于龙小组。
    /// </summary>
    /// <param name="card">待判断卡牌。</param>
    /// <returns>属于龙小组时返回 true。</returns>
    public static bool IsTeamJackieCard(CardModel? card)
    {
        return card is TeamJackieCard and not TargetImbalance;
    }
}
