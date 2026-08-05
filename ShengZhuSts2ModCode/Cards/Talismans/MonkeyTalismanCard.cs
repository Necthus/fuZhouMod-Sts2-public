using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Talismans;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 申猴：2费罕见技能牌。获得易伤(25%/升级50%)，敌人意图攻击时反弹等额伤害。升级后费用变1。
/// </summary>
public class MonkeyTalismanCard : TalismanCard
{
    /// <summary>
    /// 持有猴符咒遗物时额外获得的格挡。
    /// </summary>
    private const int MonkeyRelicBlock = 12;

    /// <summary>
    /// 构造卡牌数值：2费，技能牌，罕见。
    /// </summary>
    public MonkeyTalismanCard() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    /// <summary>
    /// 升级时费用从2降到1。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时获得申猴能力：自身受伤增加(25%/50%)，但敌人攻击意图时反弹等额伤害。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 传入升级状态决定倍率：未升级=25%，升级=50%
        int amount = IsUpgraded ? 2 : 1;
        await CommonActions.Apply<MonkeyTalismanCardPower>(choiceContext, Owner.Creature, this, amount);
        MainFile.Logger.Info($"【申猴】获得申猴能力，易伤倍率={(IsUpgraded ? "50%" : "25%")}。");

        if (HasTalismanRelic<MonkeyTalisman>())
        {
            await CreatureCmd.GainBlock(Owner.Creature, MonkeyRelicBlock, ValueProp.Move, cardPlay);
            MainFile.Logger.Info($"【申猴】猴符咒共鸣：获得{MonkeyRelicBlock}点格挡。");
        }
    }
}
