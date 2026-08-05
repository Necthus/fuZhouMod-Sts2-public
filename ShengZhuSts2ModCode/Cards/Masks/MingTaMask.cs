using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;

/// <summary>
/// 明塔面具：施加明塔面具能力，回合开始时生成噬影团。
/// </summary>
public class MingTaMask : BaseMaskCard
{
    /// <summary>
    /// 构造卡牌数值：1费，施加1层明塔面具能力。
    /// </summary>
    public MingTaMask() : base(1, CardRarity.Uncommon)
    {
        WithVar("Magic", 1);
        WithUpgradingCardTip<MingTaShadow>();
    }

    /// <summary>
    /// 施加明塔面具能力。
    /// </summary>
    protected override async Task<BaseMaskPower?> ApplyMaskPower(PlayerChoiceContext choiceContext)
    {
        return await CommonActions.ApplySelf<MingTaPower>(choiceContext, this, (int)DynamicVars["Magic"].BaseValue);
    }

    /// <summary>
    /// 创建一张噬影团。
    /// </summary>
    protected override CardModel CreateShadowKhanCard()
    {
        return Owner!.Creature!.CombatState!.CreateCard<MingTaShadow>(Owner);
    }
}
