using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 尼嘉面具：施加尼嘉面具能力，回合开始时生成忍者团。
/// </summary>
public class NiJiaMask : BaseMaskCard
{
    /// <summary>
    /// 构造卡牌数值：0费，施加1层尼嘉面具能力，并预览升级同步的忍者团衍生牌。
    /// </summary>
    public NiJiaMask() : base(0, CardRarity.Common)
    {
        WithVar("Magic", 1);
        WithUpgradingCardTip<NiJiaNinja>();
    }

    /// <summary>
    /// 施加尼嘉面具能力。
    /// </summary>
    protected override async Task<BaseMaskPower?> ApplyMaskPower(PlayerChoiceContext choiceContext)
    {
        return await CommonActions.ApplySelf<NiJiaPower>(choiceContext, this, (int)DynamicVars["Magic"].BaseValue);
    }

    /// <summary>
    /// 创建一张忍者团。
    /// </summary>
    protected override CardModel CreateShadowKhanCard()
    {
        CardModel card = Owner!.Creature!.CombatState!.CreateCard<NiJiaNinja>(Owner);
        WingsuitFlightPower.TryApplyRetainToNiJiaNinja(Owner, card, "打出尼嘉面具生成");
        return card;
    }
}
