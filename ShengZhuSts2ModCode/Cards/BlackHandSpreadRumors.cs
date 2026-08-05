using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 阴谋：给目标叠黑手；若目标已有黑手，则返还 1 点能量。
public class BlackHandSpreadRumors : BlackHandGangCard
{
    // 构造卡牌数值：0 费，施加 2 层黑手，升级后额外 +2 层。
    public BlackHandSpreadRumors() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithVar("Magic", 2, 2);
    }

    // 出牌时先判断目标原有状态，再施加黑手并按条件返还能量。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        bool hadBlackHand = BlackHandPower.HasAny(cardPlay.Target);
        await ApplyBlackHand(choiceContext, cardPlay.Target, (int)DynamicVars["Magic"].BaseValue);

        if (hadBlackHand)
        {
            await PlayerCmd.GainEnergy(1, Owner);
        }

        await ResolveBountyRewards(choiceContext);
    }
}
