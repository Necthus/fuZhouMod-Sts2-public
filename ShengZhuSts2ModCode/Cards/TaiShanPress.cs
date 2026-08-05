using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 泰山压顶：2费非普通能力牌，获得泰山压顶能力（回合结束时对随机敌人造成本回合总伤害30%的伤害）。升级后费用变1。
/// </summary>
public class TaiShanPress : AhFuCard
{
    // 构造卡牌数值：2 费，能力牌，升级后费用变 1。
    public TaiShanPress() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    // 升级时把基础费用从 2 降到 1。
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    // 出牌时获得泰山压顶能力。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.Apply<TaiShanPressPower>(choiceContext, Owner.Creature, this, 1);
        await ResolveBountyRewards(choiceContext);
    }
}
