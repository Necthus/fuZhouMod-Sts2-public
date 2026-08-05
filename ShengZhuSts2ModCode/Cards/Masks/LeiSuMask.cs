using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;

/// <summary>
/// 雷苏面具：施加雷苏面具能力，回合开始时生成异形团。
/// </summary>
public class LeiSuMask : BaseMaskCard
{
    /// <summary>
    /// 构造卡牌数值：1费，施加1层雷苏面具能力。
    /// </summary>
    public LeiSuMask() : base(1, CardRarity.Uncommon)
    {
        WithVar("Magic", 1);
        WithUpgradingCardTip<LeiSuAlien>();
    }

    /// <summary>
    /// 施加雷苏面具能力。
    /// </summary>
    protected override async Task<BaseMaskPower?> ApplyMaskPower(PlayerChoiceContext choiceContext)
    {
        return await CommonActions.ApplySelf<LeiSuPower>(choiceContext, this, (int)DynamicVars["Magic"].BaseValue);
    }

    /// <summary>
    /// 创建一张异形团。
    /// </summary>
    protected override CardModel CreateShadowKhanCard()
    {
        return Owner!.Creature!.CombatState!.CreateCard<LeiSuAlien>(Owner);
    }
}
