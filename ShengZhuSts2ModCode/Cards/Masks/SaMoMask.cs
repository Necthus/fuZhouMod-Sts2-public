using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;

/// <summary>
/// 萨莫面具：施加萨莫面具能力，回合开始时生成巨魔团。
/// </summary>
public class SaMoMask : BaseMaskCard
{
    /// <summary>
    /// 构造卡牌数值：1费，施加1层萨莫面具能力。
    /// </summary>
    public SaMoMask() : base(1, CardRarity.Common)
    {
        WithVar("Magic", 1);
        WithUpgradingCardTip<SaMoTroll>();
    }

    /// <summary>
    /// 施加萨莫面具能力。
    /// </summary>
    protected override async Task<BaseMaskPower?> ApplyMaskPower(PlayerChoiceContext choiceContext)
    {
        return await CommonActions.ApplySelf<SaMoPower>(choiceContext, this, (int)DynamicVars["Magic"].BaseValue);
    }

    /// <summary>
    /// 创建一张巨魔团。
    /// </summary>
    protected override CardModel CreateShadowKhanCard()
    {
        return Owner!.Creature!.CombatState!.CreateCard<SaMoTroll>(Owner);
    }
}
