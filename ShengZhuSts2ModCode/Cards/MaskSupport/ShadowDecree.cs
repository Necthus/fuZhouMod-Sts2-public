using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;

/// <summary>
/// 黑影敕令：获得4层影噬，并施加黑影敕令状态（回合开始失去1力量1敏捷1HP）。
/// 升级后费用2→1。
/// </summary>
public class ShadowDecree : BaseMaskSupportCard
{
    /// <summary>
    /// 构造卡牌数值：2费能力牌，获得4层影噬。升级后费用降为1。
    /// </summary>
    public ShadowDecree() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithVar("Magic", 4);
    }

    /// <summary>
    /// 升级时降低费用。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时获得4层影噬并施加黑影敕令状态。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得4层影噬
        await CommonActions.ApplySelf<DominionPower>(choiceContext, this, (int)DynamicVars["Magic"].BaseValue);

        // 施加黑影敕令状态
        await CommonActions.ApplySelf<ShadowDecreePower>(choiceContext, this, 1);
    }
}
