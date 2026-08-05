using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 大象踢腿：1费普通技能牌，获得8点格挡。若目标意图为攻击，额外造成5点伤害。
/// </summary>
public class ElephantKick : AhFuCard
{
    // 构造卡牌数值：1 费，获得 8 点格挡（升级+3），若目标意图攻击则造成 5 点伤害（升级+2）。
    public ElephantKick() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithBlock(8, 3);
        WithDamage(5, 2);
    }

    // 出牌时先获得格挡，再判断目标意图是否为攻击，若是则造成伤害。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        // 先获得格挡
        await CommonActions.CardBlock(this, cardPlay);

        // 判断目标意图是否为攻击
        bool isAttacking = cardPlay.Target.Monster?.IntendsToAttack ?? false;
        if (isAttacking)
        {
            await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);
        }

        await ResolveBountyRewards(choiceContext);
    }
}
