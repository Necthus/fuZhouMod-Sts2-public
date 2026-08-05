using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 裸绞：重击目标，若黑手足够则吃掉全部黑手并临时削减力量。
public class BlackHandSiegeRam : BlackHandGangCard
{
    // 构造卡牌数值：3 费，造成 32 点伤害，升级后伤害 +5。
    public BlackHandSiegeRam() : base(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(32, 5);
    }

    // 出牌时先攻击，再按消耗掉的黑手层数临时削减目标力量。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);

        int currentBlackHand = BlackHandPower.GetAmount(cardPlay.Target);
        if (currentBlackHand < 3)
        {
            await ResolveBountyRewards(choiceContext);
            return;
        }

        int consumedBlackHand = await ConsumeAllBlackHand(choiceContext, cardPlay.Target);
        int strengthLoss = consumedBlackHand / 3;
        if (strengthLoss > 0)
        {
            bool hadArtifact = cardPlay.Target.HasPower<ArtifactPower>();
            await PowerCmd.Apply<StrengthPower>(cardPlay.Target, -strengthLoss, Owner.Creature, this);
            if (!hadArtifact)
            {
                await BlackHandStrengthReturnPower.Apply(choiceContext, cardPlay.Target, Owner, this, 2, strengthLoss);
            }
        }

        await ResolveBountyRewards(choiceContext);
    }
}
