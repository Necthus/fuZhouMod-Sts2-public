using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

/// <summary>
/// 曼尼面具能力：回合开始时生成螳形团。
/// </summary>
public class ManNiPower : BaseMaskPower
{
    /// <summary>
    /// 创建一张螳形团卡牌。
    /// </summary>
    protected override CardModel CreateShadowKhanCard(Player player)
    {
        return Owner!.CombatState!.CreateCard<ManNiMantis>(player);
    }
}
