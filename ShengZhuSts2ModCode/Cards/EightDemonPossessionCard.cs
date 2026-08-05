using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 八魔附体：潘库宝盒集齐八种魔气后获得的先古彩蛋能力牌。
/// </summary>
public class EightDemonPossessionCard : ShengZhuSts2ModCard
{
    /// <summary>
    /// 每回合可触发的攻击牌数量。
    /// </summary>
    private const int TriggersPerTurn = 3;

    /// <summary>
    /// 普通异常基础层数下限。
    /// </summary>
    private const int BaseNormalAmountMin = 1;

    /// <summary>
    /// 普通异常基础层数上限。
    /// </summary>
    private const int BaseNormalAmountMax = 5;

    /// <summary>
    /// 力量下降基础点数下限。
    /// </summary>
    private const int BaseStrengthLossMin = 1;

    /// <summary>
    /// 力量下降基础点数上限。
    /// </summary>
    private const int BaseStrengthLossMax = 3;

    /// <summary>
    /// 禁止战斗内随机生成，确保只能通过潘库宝盒集齐八魔获得。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 禁止修饰器或其它随机获得渠道生成，潘库宝盒觉醒逻辑会手动加入牌组。
    /// </summary>
    public override bool CanBeGeneratedByModifiers => false;

    /// <summary>
    /// 构造八魔附体卡牌，基础版每次随机2种异常，升级版每次随机3种异常。
    /// </summary>
    public EightDemonPossessionCard() : base(0, CardType.Power, CardRarity.Ancient, TargetType.Self)
    {
        WithVar("TriggerCount", TriggersPerTurn, 0);
        WithVar("EffectCount", 2, 1);
        WithVar("NormalAmountMin", BaseNormalAmountMin, 1);
        WithVar("NormalAmountMax", BaseNormalAmountMax, 1);
        WithVar("StrengthLossMin", BaseStrengthLossMin, 1);
        WithVar("StrengthLossMax", BaseStrengthLossMax, 1);
        WithKeywords(CardKeyword.Innate);
    }

    /// <summary>
    /// 升级时仅提升每次随机异常数量，固有效果保持不变。
    /// </summary>
    protected override void OnUpgrade()
    {
    }

    /// <summary>
    /// 打出时获得八魔附体能力，后续每回合前三张攻击牌给目标随机施加异常。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            MainFile.Logger.Info("【八魔附体】打出失败：玩家或生物为空。");
            return;
        }

        EightDemonPossessionPower? power = await CommonActions.ApplySelf<EightDemonPossessionPower>(choiceContext, this, 1);
        if (power == null)
        {
            MainFile.Logger.Info("【八魔附体】能力施加失败。");
            return;
        }

        int effectCount = (int)DynamicVars["EffectCount"].BaseValue;
        int normalAmountMin = (int)DynamicVars["NormalAmountMin"].BaseValue;
        int normalAmountMax = (int)DynamicVars["NormalAmountMax"].BaseValue;
        int strengthLossMin = (int)DynamicVars["StrengthLossMin"].BaseValue;
        int strengthLossMax = (int)DynamicVars["StrengthLossMax"].BaseValue;
        power.Configure(TriggersPerTurn, effectCount, normalAmountMin, normalAmountMax, strengthLossMin, strengthLossMax);
        MainFile.Logger.Info($"【八魔附体】打出完成：每回合触发={TriggersPerTurn}，每次异常种类={effectCount}，普通异常={normalAmountMin}~{normalAmountMax}，力量下降={strengthLossMin}~{strengthLossMax}，升级={IsUpgraded}。");
    }
}
