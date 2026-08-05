using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;

/// <summary>
/// 拉佐面具：施加拉佐面具能力，回合开始时生成刺刃团。
/// </summary>
public class LaZuoMask : BaseMaskCard
{
    /// <summary>
    /// 构造卡牌数值：2费，施加1层拉佐面具能力。
    /// </summary>
    public LaZuoMask() : base(2, CardRarity.Uncommon)
    {
        WithVar("Magic", 1);
        WithUpgradingCardTip<LaZuoBlade>();
    }

    /// <summary>
    /// 施加拉佐面具能力。
    /// </summary>
    protected override async Task<BaseMaskPower?> ApplyMaskPower(PlayerChoiceContext choiceContext)
    {
        return await CommonActions.ApplySelf<LaZuoPower>(choiceContext, this, (int)DynamicVars["Magic"].BaseValue);
    }

    /// <summary>
    /// 创建一张刺刃团。
    /// </summary>
    protected override CardModel CreateShadowKhanCard()
    {
        return Owner!.Creature!.CombatState!.CreateCard<LaZuoBlade>(Owner);
    }
}
