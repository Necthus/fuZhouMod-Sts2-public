using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 黑手：把目标当前已有的黑手层数再复制一份，实现层数翻倍。
public class BlackHandTangible : BlackHandGangCard
{
    // 构造卡牌数值：1 费技能牌，升级后费用变为 0。
    public BlackHandTangible() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    // 升级时把基础费用从 1 降到 0。
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    // 出牌时复制目标当前已有的黑手层数，实现翻倍。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        int current = BlackHandPower.GetAmount(cardPlay.Target);
        if (current > 0)
        {
            await ApplyBlackHand(choiceContext, cardPlay.Target, current);
        }

        await ResolveBountyRewards(choiceContext);
    }
}
