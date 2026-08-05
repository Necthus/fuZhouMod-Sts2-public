using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.NiJiaSupport;

/// <summary>
/// 忍者协作：消耗牌堆每有一张尼嘉-忍者团，就提高尼嘉-忍者团的每段伤害。
/// </summary>
public class NinjaCooperation : BaseMaskSupportCard
{
    /// <summary>
    /// 构造卡牌数值：1费金色能力牌。
    /// </summary>
    public NinjaCooperation() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithVar("Magic", 1);
    }

    /// <summary>
    /// 升级时为自身补上固有关键词。
    /// </summary>
    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }

    /// <summary>
    /// 出牌时获得忍者协作能力。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await CommonActions.Apply<NinjaCooperationPower>(choiceContext, Owner.Creature, this, (int)DynamicVars["Magic"].BaseValue);
        MainFile.Logger.Info($"【忍者协作】获得能力：层数={(int)DynamicVars["Magic"].BaseValue}。");
    }
}
