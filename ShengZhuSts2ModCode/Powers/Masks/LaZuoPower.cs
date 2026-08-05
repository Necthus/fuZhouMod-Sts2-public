using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

/// <summary>
/// 拉佐面具能力：回合开始时生成刺刃团。
/// </summary>
public class LaZuoPower : BaseMaskPower
{
    /// <summary>
    /// 创建一张刺刃团卡牌。
    /// </summary>
    protected override CardModel CreateShadowKhanCard(Player player)
    {
        return Owner!.CombatState!.CreateCard<LaZuoBlade>(player);
    }
}
