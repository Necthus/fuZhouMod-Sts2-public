using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 痞子：给自己挂上“每回合自动叠黑手并按阈值抽牌打伤”的成长能力。
public class BlackHandNoNameYet : BlackHandGangCard
{
    // 构造卡牌数值：2 费能力牌，触发伤害 4，升级后 +2；每回合黑手 1，升级后 +1。
    public BlackHandNoNameYet() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithVar("Magic", 4, 2);
        WithVar("BlackHandAmt", 1, 1);
    }

    // 出牌时给自己挂上痞子能力。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await BlackHandNoNameYetPower.Apply(choiceContext, Owner, this, (int)DynamicVars["Magic"].BaseValue, (int)DynamicVars["BlackHandAmt"].BaseValue);
    }
}
