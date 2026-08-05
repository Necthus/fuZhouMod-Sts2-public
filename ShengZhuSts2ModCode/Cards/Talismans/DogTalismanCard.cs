using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Talismans;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 戌狗：2费稀有技能牌。接下来5回合(升级8回合)内免死，触发时回复10(升级25)HP。消耗。
/// </summary>
public class DogTalismanCard : TalismanCard
{
    /// <summary>
    /// 基础持续回合数。
    /// </summary>
    private const int BaseDuration = 5;

    /// <summary>
    /// 升级后持续回合数。
    /// </summary>
    private const int UpgradeDuration = 8;

    /// <summary>
    /// 触发时回复的HP量。
    /// </summary>
    private const int BaseReviveHeal = 10;

    /// <summary>
    /// 升级后触发时回复的HP量。
    /// </summary>
    private const int UpgradeReviveHeal = 25;

    /// <summary>
    /// 构造卡牌数值：2费，技能牌，稀有，消耗。Magic=10(升级+15)表示回血量。
    /// </summary>
    public DogTalismanCard() : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithVar("Magic", BaseReviveHeal, UpgradeReviveHeal - BaseReviveHeal);
    }

    /// <summary>
    /// 出牌时获得戌狗免死能力。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int duration = IsUpgraded ? UpgradeDuration : BaseDuration;
        int reviveHeal = (int)DynamicVars["Magic"].BaseValue;
        DogTalismanCardPower? existingPower = Owner.Creature.GetPower<DogTalismanCardPower>();
        if (existingPower != null)
        {
            await existingPower.AddStack(duration, reviveHeal);
            MainFile.Logger.Info($"【戌狗】叠加免死能力：增加{duration}回合，触发时回复{reviveHeal}HP。");
            return;
        }

        // Amount 传入持续回合数，Power内部管理免死次数。
        await CommonActions.Apply<DogTalismanCardPower>(choiceContext, Owner.Creature, this, duration);
        MainFile.Logger.Info($"【戌狗】获得免死能力：持续{duration}回合，触发时回复{reviveHeal}HP。");
    }
}
