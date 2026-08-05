using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;

/// <summary>
/// 卡博面具：施加卡博面具能力，回合开始时生成猎钳团。
/// </summary>
public class KaBoMask : BaseMaskCard
{
    /// <summary>
    /// 构造卡牌数值：1费，施加1层卡博面具能力。
    /// </summary>
    public KaBoMask() : base(1, CardRarity.Common)
    {
        WithVar("Magic", 1);
        WithUpgradingCardTip<KaBoPincer>();
    }

    /// <summary>
    /// 施加卡博面具能力。
    /// </summary>
    protected override async Task<BaseMaskPower?> ApplyMaskPower(PlayerChoiceContext choiceContext)
    {
        return await CommonActions.ApplySelf<KaBoPower>(choiceContext, this, (int)DynamicVars["Magic"].BaseValue);
    }

    /// <summary>
    /// 创建一张猎钳团。
    /// </summary>
    protected override CardModel CreateShadowKhanCard()
    {
        return Owner!.Creature!.CombatState!.CreateCard<KaBoPincer>(Owner);
    }
}
