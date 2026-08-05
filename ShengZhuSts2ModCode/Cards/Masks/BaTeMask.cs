using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;

/// <summary>
/// 巴特面具：施加巴特面具能力，回合开始时生成夜蝠团。
/// </summary>
public class BaTeMask : BaseMaskCard
{
    /// <summary>
    /// 构造卡牌数值：2费，施加1层巴特面具能力。
    /// </summary>
    public BaTeMask() : base(2, CardRarity.Rare)
    {
        WithVar("Magic", 1);
        WithUpgradingCardTip<BaTeBat>();
    }

    /// <summary>
    /// 施加巴特面具能力。
    /// </summary>
    protected override async Task<BaseMaskPower?> ApplyMaskPower(PlayerChoiceContext choiceContext)
    {
        return await CommonActions.ApplySelf<BaTePower>(choiceContext, this, (int)DynamicVars["Magic"].BaseValue);
    }

    /// <summary>
    /// 创建一张夜蝠团。
    /// </summary>
    protected override CardModel CreateShadowKhanCard()
    {
        return Owner!.Creature!.CombatState!.CreateCard<BaTeBat>(Owner);
    }
}
