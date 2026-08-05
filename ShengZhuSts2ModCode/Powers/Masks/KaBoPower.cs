using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

/// <summary>
/// 卡博面具能力：回合开始时生成猎钳团。
/// </summary>
public class KaBoPower : BaseMaskPower
{
    /// <summary>
    /// 创建一张猎钳团卡牌。
    /// </summary>
    protected override CardModel CreateShadowKhanCard(Player player)
    {
        return Owner!.CombatState!.CreateCard<KaBoPincer>(player);
    }
}
