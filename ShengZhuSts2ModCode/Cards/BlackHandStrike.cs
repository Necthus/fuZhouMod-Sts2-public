using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 黑手打击：若目标没有黑手则补层，否则抽牌并继续叠层。
public class BlackHandStrike : BlackHandGangCard
{
    // 构造卡牌数值：1 费，造成 7 点伤害，升级后伤害 +4；无黑手时施加 4 层，已有黑手时施加 2 层。
    public BlackHandStrike() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(7, 4);
        WithCards(1);
        WithVar("Magic", 4);
        WithVar("ExistingBlackHandApply", 2);
        WithTags(CardTag.Strike);
    }

    // 出牌时先攻击，再根据目标是否已有黑手决定追加效果。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        bool hadBlackHand = BlackHandPower.HasAny(cardPlay.Target);
        await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);

        if (!hadBlackHand)
        {
            await ApplyBlackHand(choiceContext, cardPlay.Target, (int)DynamicVars["Magic"].BaseValue);
        }
        else
        {
            await DrawExistingBlackHandBonus(choiceContext);
            await ApplyBlackHand(choiceContext, cardPlay.Target, (int)DynamicVars["ExistingBlackHandApply"].BaseValue);
        }

        await ResolveBountyRewards(choiceContext);
    }

    // 目标已有黑手时抽 1 张牌，并记录抽牌前后状态，方便排查满手、无牌或抽牌被阻止的情况。
    private async Task DrawExistingBlackHandBonus(PlayerChoiceContext choiceContext)
    {
        var playerCombatState = Owner?.PlayerCombatState;
        if (Owner == null || playerCombatState == null)
        {
            MainFile.Logger.Info("【黑手打击】目标已有黑手，准备抽牌时玩家战斗状态为空，跳过抽牌。");
            return;
        }

        int handBefore = playerCombatState.Hand.Cards.Count;
        int drawBefore = playerCombatState.DrawPile.Cards.Count;
        int discardBefore = playerCombatState.DiscardPile.Cards.Count;
        List<CardModel> drawnCards = (await CommonActions.Draw(this, choiceContext)).ToList();

        MainFile.Logger.Info($"【黑手打击】目标已有黑手抽牌结算：抽到={drawnCards.Count}，手牌={handBefore}->{playerCombatState.Hand.Cards.Count}，抽牌堆={drawBefore}->{playerCombatState.DrawPile.Cards.Count}，弃牌堆={discardBefore}->{playerCombatState.DiscardPile.Cards.Count}。");
    }
}
