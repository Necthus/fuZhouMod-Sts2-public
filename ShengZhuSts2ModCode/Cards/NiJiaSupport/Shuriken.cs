using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.NiJiaSupport;

/// <summary>
/// 手里剑：降低尼嘉-忍者团基础伤害，但让其攻击多段。
/// </summary>
public class Shuriken : BaseMaskSupportCard
{
    /// <summary>
    /// 基础版攻击次数。
    /// </summary>
    private const int BaseHitCount = 2;

    /// <summary>
    /// 升级版攻击次数。
    /// </summary>
    private const int UpgradedHitCount = 3;

    /// <summary>
    /// 构造卡牌数值：1费蓝色能力牌。
    /// </summary>
    public Shuriken() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("Magic", BaseHitCount, UpgradedHitCount - BaseHitCount);
    }

    /// <summary>
    /// 出牌时获得手里剑能力，攻击次数取已获得能力中的最大值。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        int hitCount = (int)DynamicVars["Magic"].BaseValue;
        await ShurikenPower.Apply(choiceContext, Owner.Creature, Owner.Creature, this, hitCount);
        MainFile.Logger.Info($"【手里剑】获得能力：升级={IsUpgraded}，尼嘉-忍者团攻击次数={hitCount}。");
    }
}
