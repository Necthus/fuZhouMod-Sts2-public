using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.NiJiaSupport;

/// <summary>
/// 此路不通：获得格挡，并将两张尼嘉-忍者团加入手牌。
/// </summary>
public class NoWayThrough : BaseMaskSupportCard
{
    /// <summary>
    /// 构造卡牌数值：1费灰色技能牌，获得6点格挡，升级后+3。
    /// </summary>
    public NoWayThrough() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(6, 3);
        WithVar("Magic", 2);
    }

    /// <summary>
    /// 出牌时获得格挡，并生成两张尼嘉-忍者团到手牌。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature?.CombatState == null)
        {
            return;
        }

        await CommonActions.CardBlock(this, cardPlay);

        int cardCount = (int)DynamicVars["Magic"].BaseValue;
        var generatedCards = new List<CardModel>();
        for (int i = 0; i < cardCount; i++)
        {
            CardModel ninja = Owner.Creature.CombatState.CreateCard<NiJiaNinja>(Owner);
            if (IsUpgraded)
            {
                CardCmd.Upgrade(ninja, CardPreviewStyle.None);
            }

            WingsuitFlightPower.TryApplyRetainToNiJiaNinja(Owner, ninja, "此路不通生成");
            generatedCards.Add(ninja);
        }

        await CardPileCmdHelper.AddGeneratedCardsToCombat(generatedCards, PileType.Hand, addedByPlayer: true);
        MainFile.Logger.Info($"【此路不通】获得格挡={DynamicVars.Block.BaseValue}，加入尼嘉-忍者团数量={generatedCards.Count}，升级={IsUpgraded}。");
    }
}
