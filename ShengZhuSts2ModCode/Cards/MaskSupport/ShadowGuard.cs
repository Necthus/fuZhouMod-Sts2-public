using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;

/// <summary>
/// 黑影护卫：让尼嘉-忍者团造成的伤害转化为自己的护甲。
/// </summary>
public class ShadowGuard : BaseMaskSupportCard
{
    /// <summary>
    /// 构造卡牌数值：1费灰色能力。
    /// </summary>
    public ShadowGuard() : base(1, CardType.Power, CardRarity.Common, TargetType.Self)
    {
    }

    /// <summary>
    /// 出牌时获得黑影护卫能力，升级版会把被格挡吸收的伤害也计入。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        ShadowGuardPower? power = await PowerCmd.Apply<ShadowGuardPower>(Owner.Creature, 1, Owner.Creature, this);
        power?.EnableBlockedDamageIfNeeded(IsUpgraded);
        MainFile.Logger.Info($"【黑影护卫】获得能力：升级={IsUpgraded}。");
    }
}
