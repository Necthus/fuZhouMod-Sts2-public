using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 巳蛇：1费稀有技能牌。获得1层无实体。消耗+虚无。升级后去掉虚无。
/// </summary>
public class SnakeTalismanCard : TalismanCard
{
    /// <summary>
    /// 构造卡牌数值：1费，技能牌，稀有，消耗+虚无。Magic=1表示无实体层数。
    /// </summary>
    public SnakeTalismanCard() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust, CardKeyword.Ethereal);
        WithVar("Magic", 1);
    }

    /// <summary>
    /// 升级时去掉虚无。
    /// </summary>
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Ethereal);
    }

    /// <summary>
    /// 出牌时获得1层无实体。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int amount = (int)DynamicVars["Magic"].BaseValue;
        await PowerCmd.Apply<IntangiblePower>(Owner.Creature, amount, Owner.Creature, this);
        MainFile.Logger.Info($"【巳蛇】获得{amount}层无实体。");

        if (HasTalismanRelic<SnakeTalisman>())
        {
            await PowerCmd.Apply<ArtifactPower>(Owner.Creature, 1, Owner.Creature, this);
            MainFile.Logger.Info("【巳蛇】蛇符咒共鸣：额外获得1层人工制品。");
        }
    }
}
