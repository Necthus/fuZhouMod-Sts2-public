using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Talismans;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 丑牛：2费非普通能力牌。敌人的力量和敏捷无法高于你。升级后费用变1。
/// </summary>
public class OxTalismanCard : TalismanCard
{
    /// <summary>
    /// 构造卡牌数值：2费，能力牌，非普通。
    /// </summary>
    public OxTalismanCard() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    /// <summary>
    /// 升级时费用从2降到1。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时获得丑牛能力：敌人力量和敏捷无法高于你。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.Apply<OxTalismanCardPower>(choiceContext, Owner.Creature, this, 1);
    }
}
