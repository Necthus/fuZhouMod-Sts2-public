using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.TeamJackie;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 紧急逃生：龙小组能力牌，获得覆甲，并在未来数回合免疫且反弹来自怪物的减益。
/// </summary>
public class EmergencyEscape : TeamJackieCard
{
    /// <summary>
    /// 构造卡牌数值：2费能力，获得8(10)覆甲，保护2(3)回合。
    /// </summary>
    public EmergencyEscape() : base(2, CardType.Power, TargetType.Self)
    {
        WithVar("Plating", 8, 2);
        WithVar("Magic", 2, 1);
    }

    /// <summary>
    /// 出牌时获得覆甲和紧急逃生保护。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        int platingAmount = (int)DynamicVars["Plating"].BaseValue;
        int protectTurns = (int)DynamicVars["Magic"].BaseValue;
        await PowerCmd.Apply<PlatingPower>(Owner.Creature, platingAmount, Owner.Creature, this);
        await PowerCmd.Apply<EmergencyEscapePower>(Owner.Creature, protectTurns, Owner.Creature, this);
        MainFile.Logger.Info($"【紧急逃生】获得覆甲={platingAmount}，来自怪物减益免疫反弹回合={protectTurns}。");
    }
}
