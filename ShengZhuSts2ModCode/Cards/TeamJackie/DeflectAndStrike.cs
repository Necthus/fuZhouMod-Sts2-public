using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.TeamJackie;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 借力打力：先获得格挡，本回合完全格挡敌方攻击时反弹伤害。
/// </summary>
public class DeflectAndStrike : TeamJackieCard
{
    /// <summary>
    /// 构造卡牌数值：1费技能，获得4点格挡，升级后格挡+4。
    /// </summary>
    public DeflectAndStrike() : base(1, CardType.Skill, TargetType.Self)
    {
        WithBlock(4, 4);
    }

    /// <summary>
    /// 出牌时先获得格挡，再施加借力打力能力。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        if (DynamicVars.Block.BaseValue > 0)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue, ValueProp.Move, cardPlay);
        }

        await CommonActions.ApplySelf<DeflectAndStrikePower>(choiceContext, this, 1);
    }
}
