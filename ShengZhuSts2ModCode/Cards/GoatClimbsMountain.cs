using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 山羊爬山：1费非普通能力牌，获得山羊爬山能力（每打出1张牌获得1点临时力量）。升级后费用变0。
/// </summary>
public class GoatClimbsMountain : AhFuCard
{
    // 构造卡牌数值：1 费，能力牌，升级后费用变 0。
    public GoatClimbsMountain() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    // 升级时把基础费用从 1 降到 0。
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    // 出牌时获得山羊爬山能力。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.Apply<GoatClimbsMountainPower>(choiceContext, Owner.Creature, this, 1);
        await ResolveBountyRewards(choiceContext);
    }
}
