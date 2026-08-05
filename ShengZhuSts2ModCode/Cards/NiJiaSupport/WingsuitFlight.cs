using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.NiJiaSupport;

/// <summary>
/// 翼装飞行：让尼嘉-忍者团获得保留，并按打出数量获得临时影噬。
/// </summary>
public class WingsuitFlight : BaseMaskSupportCard
{
    /// <summary>
    /// 构造卡牌数值：1费灰色能力牌。
    /// </summary>
    public WingsuitFlight() : base(1, CardType.Power, CardRarity.Common, TargetType.Self)
    {
    }

    /// <summary>
    /// 升级时把基础费用从1降到0。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时获得翼装飞行能力，后续尼嘉-忍者团会获得保留并触发临时影噬。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await CommonActions.Apply<WingsuitFlightPower>(choiceContext, Owner.Creature, this, 1);
        MainFile.Logger.Info($"【联机同步】来源=翼装飞行，玩家={Owner.NetId}/{Owner.Character.Id.Entry}，动作=获得尼嘉-忍者团保留与临时影噬能力，升级={IsUpgraded}。");
    }
}
