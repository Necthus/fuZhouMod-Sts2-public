using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Talismans;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 酉鸡：2费稀有技能牌。获得飞行能力：敏捷+2(升级+4)，伤害减50%，受指定次数伤害后击落并眩晕1回合。消耗。
/// </summary>
public class RoosterTalismanCard : TalismanCard
{
    /// <summary>
    /// 构造卡牌数值：2费，技能牌，稀有，消耗。Magic=2(升级+2)表示敏捷增加量。
    /// </summary>
    public RoosterTalismanCard() : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithVar("Magic", 2, 2);
    }

    /// <summary>
    /// 出牌时获得酉鸡飞行能力。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int dexAmount = (int)DynamicVars["Magic"].BaseValue;
        int hitCount = RoosterTalismanCardPower.GetMaxHitsForPlayer(Owner);
        RoosterTalismanCardPower? existingPower = Owner.Creature.GetPower<RoosterTalismanCardPower>();
        if (existingPower != null)
        {
            await existingPower.AddStack(choiceContext, Owner.Creature, this, dexAmount);
            MainFile.Logger.Info($"【酉鸡】叠加飞行能力：敏捷+{dexAmount}，伤害减半，新增受击次数{hitCount}。");
            return;
        }

        await CommonActions.Apply<RoosterTalismanCardPower>(choiceContext, Owner.Creature, this, dexAmount);
        MainFile.Logger.Info($"【酉鸡】获得飞行能力：敏捷+{dexAmount}，伤害减半，受{hitCount}次伤害后击落。");
    }
}
