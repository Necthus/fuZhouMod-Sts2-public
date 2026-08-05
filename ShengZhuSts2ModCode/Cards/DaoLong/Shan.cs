using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 山：刀龙黑暗杀手能力牌，回合结束时按本回合造成伤害对全体敌人追加伤害。
/// </summary>
public class Shan : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造山卡牌数值。
    /// </summary>
    public Shan() : base(2, CardType.Power, CardRarity.Event, TargetType.Self)
    {
    }

    /// <summary>
    /// 升级时费用从 2 降到 1。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时获得山能力。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.Apply<ShanPower>(choiceContext, Owner.Creature, this, 1);
    }
}
