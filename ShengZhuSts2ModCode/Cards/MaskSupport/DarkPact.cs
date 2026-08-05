using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;

/// <summary>
/// 黑暗契约：1费技能。获得 1(2) 层影噬，失去 2 点力量。消耗。
/// 与 1 代 CardDarkPact 实现一致：归类于面具辅助体系（MASK_SUPPORT），
/// 通过继承 <see cref="BaseMaskSupportCard"/> 来标识。
/// </summary>
public class DarkPact : BaseMaskSupportCard
{
    /// <summary>
    /// 失去的力量数值（基础与升级一致，仅影噬层数升级）。
    /// </summary>
    private const int StrengthLoss = 2;

    /// <summary>
    /// 构造卡牌数值：1费技能，普通稀有度，自瞄。Magic=影噬层数（基础1，升级+1）。消耗。
    /// </summary>
    public DarkPact() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithVar("Magic", 1, 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 出牌时获得影噬，并失去 2 点力量。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        int dominionAmount = (int)DynamicVars["Magic"].BaseValue;

        // 获得影噬
        await CommonActions.ApplySelf<DominionPower>(choiceContext, this, dominionAmount);

        // 失去 2 点力量（永久负向施加）
        await PowerCmd.Apply<StrengthPower>(Owner.Creature, -StrengthLoss, Owner.Creature, this);

        MainFile.Logger.Info($"【黑暗契约】打出：获得影噬={dominionAmount}，失去力量={StrengthLoss}，升级={IsUpgraded}");
    }
}
