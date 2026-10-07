using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 先攻：目标有黑手时追加伤害，否则先施加黑手层数。
public class BlackHandFirstStrike : BlackHandGangCard
{
    // 构造卡牌数值：1 费，基础伤害 8，额外伤害 6，升级后基础伤害 +3。
    public BlackHandFirstStrike() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithCalculatedDamage(6, 6, static (_, target) => target != null && target.GetPowerAmount<BlackHandPower>() > 0 ? 1m : 0m, upgrade: 3);
        WithVar("Magic", 6);
        WithVar("ApplyAmount", 2);
    }

    // 出牌时先按当前目标状态计算伤害，再根据是否已有黑手决定是否补黑手层数。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        bool hasBlackHand = cardPlay.Target.HasPower<BlackHandPower>();
        decimal damage = ((CalculatedVar)DynamicVars["CalculatedDamage"]).Calculate(cardPlay.Target);

        if (!hasBlackHand)
        {
            await ApplyBlackHand(choiceContext, cardPlay.Target, (int)DynamicVars["ApplyAmount"].BaseValue);
        }

        await CommonActions.CardAttack(this, cardPlay, cardPlay.Target, damage, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move).Execute(choiceContext);
        await ResolveBountyRewards(choiceContext);
    }
}
