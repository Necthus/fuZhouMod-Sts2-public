using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;

/// <summary>
/// 面具脸谱：从抽牌堆选1张面具加入手牌并设为0费；若抽牌堆中没有面具，则随机加入黑影兵团牌到手牌。
/// </summary>
public class MaskFace : BaseMaskSupportCard
{
    /// <summary>
    /// 构造卡牌数值：1费技能。
    /// </summary>
    public MaskFace() : base(1, CardType.Skill, CardRarity.Common, TargetType.None)
    {
    }

    /// <summary>
    /// 出牌时从抽牌堆选面具牌或随机生成兵团牌。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var playerCombatState = Owner?.PlayerCombatState;
        var combatState = Owner?.Creature?.CombatState;
        if (playerCombatState == null || combatState == null)
        {
            return;
        }

        var maskCards = playerCombatState.DrawPile.Cards
            .Where(c => c is BaseMaskCard || c is TaLaMask)
            .ToList();

        if (maskCards.Count > 0)
        {
            var prefs = new CardSelectorPrefs(new LocString("cards", Id.Entry + ".selectionScreenPrompt"), 1, 1);
            var selected = (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                maskCards,
                Owner!,
                prefs)).FirstOrDefault();

            if (selected != null)
            {
                selected.EnergyCost.SetThisTurn(0, reduceOnly: true);

                if (playerCombatState.Hand.Cards.Count >= 10)
                {
                    await CardPileCmd.Add(selected, PileType.Discard);
                    MainFile.Logger.Info($"【面具脸谱】手牌已满，所选面具进入弃牌堆：{selected.Id.Entry}");
                }
                else
                {
                    await CardPileCmd.Add(selected, PileType.Hand);
                    MainFile.Logger.Info($"【面具脸谱】选择面具加入手牌并设为0费：{selected.Id.Entry}");
                }
            }
        }
        else
        {
            int shadowKhanCount = IsUpgraded ? 4 : 2;
            for (int i = 0; i < shadowKhanCount; i++)
            {
                var selectedType = ShadowKhanCardHelper.ShadowKhanTypes[StableRandomHelper.NextInt(Owner, ShadowKhanCardHelper.ShadowKhanTypes.Length)];
                var card = ShadowKhanCardHelper.CreateCardByType(combatState, selectedType, Owner!);
                if (card != null)
                {
                    await CardPileCmdHelper.AddGeneratedCardToCombat(card, PileType.Hand, addedByPlayer: true);
                }
            }

            MainFile.Logger.Info($"【面具脸谱】抽牌堆没有面具，随机生成黑影兵团牌数量={shadowKhanCount}。");
        }
    }
}
