using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 金鸡王的宝藏：3费罕见技能牌，消耗。本回合内打出的阿福和黑手帮牌费用变为0。升级后费用变2。
/// </summary>
public class ValmontTreasure : AhFuCard
{
    // 构造卡牌数值：3 费，技能牌，消耗。
    public ValmontTreasure() : base(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
    }

    // 升级时把基础费用从 3 降到 2。
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    // 出牌时获得金鸡王宝藏能力，并立即刷新手牌中阿福/黑手帮牌的费用。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ValmontTreasurePower? power = await CommonActions.Apply<ValmontTreasurePower>(choiceContext, Owner.Creature, this, 1);

        // 能力施加成功后立即刷新手牌费用
        power?.InitializeHandCosts();

        await ResolveBountyRewards(choiceContext);
    }
}
