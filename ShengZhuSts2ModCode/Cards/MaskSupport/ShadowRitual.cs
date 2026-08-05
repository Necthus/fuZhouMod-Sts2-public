using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;

/// <summary>
/// 潜影仪式：获得2(3)层影噬，失去5HP（不可被护盾格挡）。消耗。
/// </summary>
public class ShadowRitual : BaseMaskSupportCard
{
    /// <summary>
    /// 失去的HP数值。
    /// </summary>
    private const int HpLoss = 5;

    /// <summary>
    /// 构造卡牌数值：1费技能，获得2(3)层影噬，失去5HP。消耗。
    /// </summary>
    public ShadowRitual() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("Magic", 2, 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 出牌时获得影噬并直接失去HP（不可被护盾格挡）。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得影噬
        await CommonActions.ApplySelf<DominionPower>(choiceContext, this, (int)DynamicVars["Magic"].BaseValue);

        // 固定失去5HP，不走伤害结算链，避免被易伤或增伤放大。
        await CreatureCmd.SetCurrentHp(Owner!.Creature!, Math.Max(0, Owner!.Creature!.CurrentHp - HpLoss));
    }
}
