using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 弗朗明哥舞步：1费普通技能牌，获得8点格挡。若阿福连招激活，下回合额外获得1点能量。
/// </summary>
public class FlamingoStep : AhFuCard
{
    // 构造卡牌数值：1 费，获得 8 点格挡（升级+3）；连招激活时下回合获得 1 点能量。
    public FlamingoStep() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(8, 3);
        WithVar("Magic", 1);
    }

    // 出牌时获得格挡，若阿福连招激活则施加"下回合额外能量"效果。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);

        // 阿福连招激活：下回合额外获得能量（EnergyNextTurnPower）
        if (IsAfuComboActive())
        {
            int energyAmount = (int)DynamicVars["Magic"].BaseValue;
            await CommonActions.Apply<EnergyNextTurnPower>(choiceContext, Owner.Creature, this, energyAmount);
        }

        await ResolveBountyRewards(choiceContext);
    }
}
