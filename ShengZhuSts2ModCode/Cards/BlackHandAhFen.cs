using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 黑手帮·阿奋：圣主初始牌组里的基础攻击牌。
public class BlackHandAhFen : BlackHandGangCard
{
    // 构造卡牌数值：1 费，造成 6 点伤害，升级后额外增加 3 点伤害。
    public BlackHandAhFen() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
        WithDamage(6, 3);
        WithTags(CardTag.Strike);
    }

    // 出牌时对目标造成一次普通攻击。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);
    }
}
