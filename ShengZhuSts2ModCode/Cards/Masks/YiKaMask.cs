using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;

/// <summary>
/// 伊卡面具：施加伊卡面具能力，回合开始时生成武士团。
/// </summary>
public class YiKaMask : BaseMaskCard
{
    /// <summary>
    /// 构造卡牌数值：2费，施加1层伊卡面具能力。
    /// </summary>
    public YiKaMask() : base(2, CardRarity.Rare)
    {
        WithVar("Magic", 1);
        WithUpgradingCardTip<YiKaSamurai>();
    }

    /// <summary>
    /// 施加伊卡面具能力。
    /// </summary>
    protected override async Task<BaseMaskPower?> ApplyMaskPower(PlayerChoiceContext choiceContext)
    {
        return await CommonActions.ApplySelf<YiKaPower>(choiceContext, this, (int)DynamicVars["Magic"].BaseValue);
    }

    /// <summary>
    /// 创建一张武士团。
    /// </summary>
    protected override CardModel CreateShadowKhanCard()
    {
        return Owner!.Creature!.CombatState!.CreateCard<YiKaSamurai>(Owner);
    }
}
