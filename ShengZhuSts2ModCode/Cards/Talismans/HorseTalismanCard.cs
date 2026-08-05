using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Talismans;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Talismans;

/// <summary>
/// 午马：1费稀有技能牌。回合结束时回复本回合造成未格挡伤害总和30%(升级40%)的HP。消耗。
/// </summary>
public class HorseTalismanCard : TalismanCard
{
    /// <summary>
    /// 基础治疗比例。
    /// </summary>
    private const int BaseHealPercent = 30;

    /// <summary>
    /// 升级后治疗比例。
    /// </summary>
    private const int UpgradeHealPercent = 40;

    /// <summary>
    /// 构造卡牌数值：1费，技能牌，稀有，消耗。
    /// </summary>
    public HorseTalismanCard() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 升级时不降费，改为提高治疗比例。
    /// </summary>
    protected override void OnUpgrade()
    {
    }

    /// <summary>
    /// 出牌时获得午马能力：追踪本回合伤害，回合结束时回血。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int healPercent = IsUpgraded ? UpgradeHealPercent : BaseHealPercent;
        await CommonActions.Apply<HorseTalismanCardPower>(choiceContext, Owner.Creature, this, healPercent);
        MainFile.Logger.Info($"【午马】获得午马能力，回合结束时将回复本回合造成伤害的{healPercent}%。");
    }
}
