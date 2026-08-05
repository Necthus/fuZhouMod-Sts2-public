using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 寅虎·技：1费非普通技能牌。选择一个敌人，若其HP高于你则其力量-3(升级-5)，若低于你则你力量+3(升级+5)。消耗。
/// </summary>
public class TigerTalismanSkillCard : TalismanCard
{
    /// <summary>
    /// 构造卡牌数值：1费，技能牌，非普通，消耗。Magic=3(升级+2)表示力量变化量。
    /// </summary>
    public TigerTalismanSkillCard() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithVar("Magic", 3, 2);
    }

    /// <summary>
    /// 出牌时比较敌我HP，决定力量增减方向。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null || Owner?.Creature == null)
        {
            return;
        }

        int magicValue = (int)DynamicVars["Magic"].BaseValue;
        if (HasTalismanRelic<TigerTalisman>())
        {
            magicValue++;
        }
        decimal playerHp = Owner.Creature.CurrentHp;
        decimal targetHp = cardPlay.Target.CurrentHp;

        if (targetHp > playerHp)
        {
            // 敌人HP高于玩家：敌人力量永久-N
            await PowerCmd.Apply<StrengthPower>(cardPlay.Target, -magicValue, Owner.Creature, this);
            MainFile.Logger.Info($"【寅虎·技】敌人HP({targetHp})>玩家HP({playerHp})，敌人力量-{magicValue}。");
        }
        else if (targetHp < playerHp)
        {
            // 玩家HP高于敌人：玩家力量永久+N
            await PowerCmd.Apply<StrengthPower>(Owner.Creature, magicValue, Owner.Creature, this);
            MainFile.Logger.Info($"【寅虎·技】玩家HP({playerHp})>敌人HP({targetHp})，玩家力量+{magicValue}。");
        }
        else
        {
            MainFile.Logger.Info($"【寅虎·技】敌我HP相等({playerHp})，无效果。");
        }
    }
}
