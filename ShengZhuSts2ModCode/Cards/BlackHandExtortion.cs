using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 敲诈：打伤目标后按黑手层数拿钱，再消耗少量黑手。
public class BlackHandExtortion : BlackHandGangCard
{
    // 构造卡牌数值：1 费，造成 7 点伤害，升级后伤害 +3。
    public BlackHandExtortion() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(7, 3);
    }

    // 出牌时先攻击，再按黑手层数结算金币；只有真的拿到钱时才消耗 2 层黑手。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);

        int blackHandAmount = BlackHandPower.GetAmount(cardPlay.Target);
        int goldGain = (blackHandAmount / 3) * 3;
        if (goldGain > 0)
        {
            await PlayerCmd.GainGold(goldGain, Owner);
            await ConsumeBlackHand(choiceContext, cardPlay.Target, 2);
        }

        await ResolveBountyRewards(choiceContext);
    }
}
