using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 尼嘉面具能力：回合开始时生成忍者团。
/// </summary>
public class NiJiaPower : BaseMaskPower
{
    /// <summary>
    /// 创建一张忍者团卡牌。
    /// </summary>
    protected override CardModel CreateShadowKhanCard(Player player)
    {
        CardModel card = Owner!.CombatState!.CreateCard<NiJiaNinja>(player);
        WingsuitFlightPower.TryApplyRetainToNiJiaNinja(player, card, "尼嘉面具回合开始生成");
        return card;
    }
}
