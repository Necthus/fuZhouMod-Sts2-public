using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 囚禁：按目标黑手层数临时降低力量，并附带 1 层虚弱。
public class BlackHandCoerciveInterrogation : BlackHandGangCard
{
    // 构造卡牌数值：1 费，施加 1 层虚弱；升级后额外多降 1 点力量。
    public BlackHandCoerciveInterrogation() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithVar("Magic", 1);
        WithCalculatedVar("StrengthLoss", 0, static (card, target) => CalculateStrengthLoss(card, target), upgrade: 0, bonusUpgrade: 0);
    }

    // 出牌时按目标黑手层数计算临时降力，并施加虚弱。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        int strengthLoss = CalculateStrengthLoss(this, cardPlay.Target);

        if (strengthLoss > 0)
        {
            bool hadArtifact = cardPlay.Target.HasPower<ArtifactPower>();
            await PowerCmd.Apply<StrengthPower>(cardPlay.Target, -strengthLoss, Owner.Creature, this);
            if (!hadArtifact)
            {
                await BlackHandStrengthReturnPower.Apply(choiceContext, cardPlay.Target, Owner, this, 1, strengthLoss);
            }
        }

        await CommonActions.Apply<WeakPower>(choiceContext, cardPlay.Target, this, (int)DynamicVars["Magic"].BaseValue);
    }

    // 按目标黑手层数计算本回合临时降低的力量。
    private static int CalculateStrengthLoss(CardModel card, Creature? target)
    {
        if (target == null)
        {
            return 0;
        }

        int strengthLoss = BlackHandPower.GetAmount(target) / 2;
        if (card.IsUpgraded)
        {
            strengthLoss += 1;
        }

        return strengthLoss;
    }
}
