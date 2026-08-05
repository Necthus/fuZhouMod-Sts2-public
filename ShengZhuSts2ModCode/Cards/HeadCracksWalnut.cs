using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 脑袋砸核桃：1费普通攻击牌，造成9点伤害。若阿福连招激活，抽1(2)张牌。
/// </summary>
public class HeadCracksWalnut : AhFuCard
{
    // 构造卡牌数值：1 费，造成 9 点伤害（升级+3）；连招激活时抽 1 张牌，升级后抽 2 张。
    public HeadCracksWalnut() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(9, 3);
        WithVar("Magic", 1, 1);
    }

    // 出牌时造成伤害，若阿福连招激活则额外抽牌。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);

        // 阿福连招激活：抽牌
        if (IsAfuComboActive())
        {
            int drawCount = (int)DynamicVars["Magic"].BaseValue;
            await CardPileCmd.Draw(choiceContext, drawCount, Owner);
        }

        await ResolveBountyRewards(choiceContext);
    }
}
