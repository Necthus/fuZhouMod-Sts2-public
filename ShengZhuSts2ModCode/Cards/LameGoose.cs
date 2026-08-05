using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 我成了瘸腿鹅：0费普通技能牌，获得4点格挡。若阿福连招激活，本回合获得1点临时力量。
/// </summary>
public class LameGoose : AhFuCard
{
    // 构造卡牌数值：0 费，获得 4 点格挡（升级+2）；连招激活时获得 1 点临时力量。
    public LameGoose() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(4, 2);
        WithVar("Magic", 1);
    }

    // 出牌时获得格挡，若阿福连招激活则获得临时力量（回合结束失去）。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);

        // 阿福连招激活：通过二代临时力量能力获得力量，回合结束由能力自动扣回。
        if (IsAfuComboActive())
        {
            int strengthAmount = (int)DynamicVars["Magic"].BaseValue;
            await PowerCmd.Apply<LameGooseTemporaryStrengthPower>(Owner.Creature, strengthAmount, Owner.Creature, this);
        }

        await ResolveBountyRewards(choiceContext);
    }
}
