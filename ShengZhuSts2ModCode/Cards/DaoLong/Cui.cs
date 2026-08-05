using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 崔：刀龙黑暗杀手技能牌，获得格挡和火焰屏障。
/// </summary>
public class Cui : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造崔卡牌数值。
    /// </summary>
    public Cui() : base(1, CardType.Skill, CardRarity.Event, TargetType.Self)
    {
        WithBlock(8, 3);
        WithVar("Magic", 3, 2);
    }

    /// <summary>
    /// 出牌时获得格挡，并获得等同魔法数值的火焰屏障。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);
        await PowerCmd.Apply<FlameBarrierPower>(Owner.Creature, (int)DynamicVars["Magic"].BaseValue, Owner.Creature, this);
    }
}
