using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 护送：先给自己一段格挡，对所有敌人施加黑手，并在下回合开始时按敌方黑手总层数再拿一段格挡。
public class BlackHandEscort : BlackHandGangCard
{
    // 构造卡牌数值：2 费，获得 14 点格挡，升级后格挡 +4，并对所有敌人施加 4 层黑手。
    public BlackHandEscort() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(14, 4);
        WithVar("BlackHandApply", 4);
    }

    // 出牌时先获得格挡，再给所有存活敌人施加黑手，最后挂上“下回合按黑手总层数得格挡”的能力。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await CommonActions.CardBlock(this, cardPlay);

        int blackHandAmount = (int)DynamicVars["BlackHandApply"].BaseValue;
        foreach (var creature in GetLivingOpponents())
        {
            await ApplyBlackHand(choiceContext, creature, blackHandAmount);
        }

        await CommonActions.Apply<BlackHandEscortPower>(choiceContext, Owner.Creature, this, 1);
        await ResolveBountyRewards(choiceContext);
    }
}
