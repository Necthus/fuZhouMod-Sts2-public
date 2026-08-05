using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 潜龙在渊：1费普通技能牌，获得格挡，保留手牌，下回合额外获得1点能量，然后结束当前回合。
/// </summary>
public class HiddenDragon : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造卡牌数值：1费，获得6点格挡，升级后格挡+3。
    /// </summary>
    public HiddenDragon() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(6, 3);
        WithVar("Magic", 1);
    }

    /// <summary>
    /// 出牌时获得格挡，保留当前手牌，下回合获得额外能量，并立刻结束回合。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var playerCombatState = Owner?.PlayerCombatState;
        if (playerCombatState == null || Owner?.Creature == null)
        {
            return;
        }

        await CommonActions.CardBlock(this, cardPlay);

        var handCards = playerCombatState.Hand.Cards.ToList();
        foreach (CardModel card in handCards)
        {
            card.GiveSingleTurnRetain();
        }

        int energyAmount = (int)DynamicVars["Magic"].BaseValue;
        await CommonActions.Apply<EnergyNextTurnPower>(choiceContext, Owner.Creature, this, energyAmount);

        MainFile.Logger.Info($"【潜龙在渊】获得{DynamicVars.Block.BaseValue}点格挡，保留{handCards.Count}张手牌，下回合额外获得{energyAmount}点能量，并结束当前回合。");
        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }
}
