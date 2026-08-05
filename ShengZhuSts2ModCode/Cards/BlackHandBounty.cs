using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 悬赏：让玩家施加黑手时附带伤害，并在黑手目标死亡时发金币。
public class BlackHandBounty : BlackHandGangCard
{
    // 构造卡牌数值：1 费能力牌，基础击杀赏金 8，升级后 +2。
    public BlackHandBounty() : base(1, CardType.Power, CardRarity.Common, TargetType.Self)
    {
        WithVar("Magic", 8, 2);
    }

    // 出牌时给自己挂上悬赏能力。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await BountyPower.Apply(choiceContext, Owner, this, (int)DynamicVars["Magic"].BaseValue);
    }
}
