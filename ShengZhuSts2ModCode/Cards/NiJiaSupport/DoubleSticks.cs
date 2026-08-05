using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.NiJiaSupport;

/// <summary>
/// 双棍：让尼嘉-忍者团获得三倍影噬伤害加成，升级后额外获得1点影噬。
/// </summary>
public class DoubleSticks : BaseMaskSupportCard
{
    /// <summary>
    /// 构造卡牌数值：1费灰色能力牌。
    /// </summary>
    public DoubleSticks() : base(1, CardType.Power, CardRarity.Common, TargetType.Self)
    {
        WithVar("Magic", 1);
    }

    /// <summary>
    /// 出牌时获得双棍能力，升级版额外获得1层影噬。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await CommonActions.Apply<DoubleSticksPower>(choiceContext, Owner.Creature, this, 1);
        if (IsUpgraded)
        {
            await CommonActions.Apply<DominionPower>(choiceContext, Owner.Creature, this, (int)DynamicVars["Magic"].BaseValue);
        }

        MainFile.Logger.Info($"【双棍】获得能力：升级={IsUpgraded}，影噬补充={(IsUpgraded ? (int)DynamicVars["Magic"].BaseValue : 0)}。");
    }
}
