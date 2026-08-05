using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 二龙戏珠：2费罕见技能牌，消耗。你本回合打出的下2张牌额外触发一次。升级后费用变1。
/// </summary>
public class TwoDragons : AhFuCard
{
    // 构造卡牌数值：2 费，技能牌，消耗。
    public TwoDragons() : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithVar("Magic", 2);
    }

    // 升级时把基础费用从 2 降到 1。
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    // 出牌时获得二龙戏珠能力（下2张牌额外触发一次）。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int triggerCount = (int)DynamicVars["Magic"].BaseValue;
        await CommonActions.Apply<TwoDragonsPower>(choiceContext, Owner.Creature, this, triggerCount);
        await ResolveBountyRewards(choiceContext);
    }
}
