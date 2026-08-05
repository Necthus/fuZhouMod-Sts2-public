using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.NiJiaSupport;

/// <summary>
/// 烟雾弹：尼嘉-忍者团攻击时让敌人临时下降力量。
/// </summary>
public class SmokeBomb : BaseMaskSupportCard
{
    /// <summary>
    /// 构造卡牌数值：1费蓝色能力牌。
    /// </summary>
    public SmokeBomb() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("Magic", 1);
    }

    /// <summary>
    /// 出牌时获得烟雾弹能力，升级版改为每段攻击都触发。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        SmokeBombPower? power = await CommonActions.Apply<SmokeBombPower>(choiceContext, Owner.Creature, this, (int)DynamicVars["Magic"].BaseValue);
        if (power != null && IsUpgraded && !Owner.Creature.HasPower<SmokeBombAttackModePower>())
        {
            await PowerCmd.Apply<SmokeBombAttackModePower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
        }

        MainFile.Logger.Info($"【烟雾弹】获得能力：升级={IsUpgraded}，层数={(int)DynamicVars["Magic"].BaseValue}。");
    }
}
