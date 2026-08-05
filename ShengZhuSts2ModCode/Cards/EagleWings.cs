using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 飞鹰展翅：1费技能牌，获得飞行（敌人伤害-50%，受到未格挡HP伤害后飞行取消）。
/// 基础版消耗，升级后费用变为0并去掉消耗。
/// </summary>
public class EagleWings : AhFuCard
{
    /// <summary>
    /// 构造卡牌数值：1费技能牌，基础版消耗，升级后费用变为0并去掉消耗。
    /// </summary>
    public EagleWings() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithVar("Magic", 1);
    }

    /// <summary>
    /// 升级时把基础费用从 1 降到 0，并移除消耗关键词。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        RemoveKeyword(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 出牌时获得飞行（使用游戏内置的FlutterPower）。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int flightAmount = (int)DynamicVars["Magic"].BaseValue;
        await PowerCmd.Apply<FlightAfuPower>(Owner.Creature, flightAmount, Owner.Creature, this);
        await ResolveBountyRewards(choiceContext);
    }
}
