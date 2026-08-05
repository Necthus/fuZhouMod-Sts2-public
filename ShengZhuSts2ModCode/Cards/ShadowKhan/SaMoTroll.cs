using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

/// <summary>
/// 萨摩-巨魔团：获得格挡，每打出2张黑影兵团牌额外获得(2)格挡。
/// </summary>
public class SaMoTroll : BaseShadowKhanCard
{
    /// <summary>
    /// 构造卡牌数值：0费技能，5(6)格挡，每2张兵团额外(2)格挡。消耗。
    /// </summary>
    public SaMoTroll() : base(0, CardType.Skill, TargetType.Self)
    {
        WithCalculatedBlock("Block", 5, static (card, _) => ((SaMoTroll)card).GetBlockBonus(), default, upgrade: 1, bonusUpgrade: 0);
        WithVar("Magic", 1, 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 出牌时获得格挡，并根据本回合已打出的黑影兵团数量获得额外格挡。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            int shadowKhanPlayed = ShadowKhanHelper.GetShadowKhanPlayedThisTurn(this);
            int bonusCount = shadowKhanPlayed / 2;
            int bonusBlock = (int)DynamicVars["Magic"].BaseValue * bonusCount;
            int printedBaseBlock = GetPrintedBaseValue(5, 1);
            int currentBaseBlock = printedBaseBlock + bonusBlock;
            int totalBlock = Math.Max(0, currentBaseBlock + GetDominionAmount());

            MainFile.Logger.Info($"【巨魔团】结算日志：本回合当前卡之前已打出黑影兵团={shadowKhanPlayed}，额外触发次数={bonusCount}，每次额外格挡={(int)DynamicVars["Magic"].BaseValue}，基础格挡={printedBaseBlock}，额外格挡={bonusBlock}，影噬={GetDominionAmount()}，最终总格挡={totalBlock}");
            await ShadowKhanBlock(choiceContext, currentBaseBlock, cardPlay);
        }
        finally
        {
            ShadowKhanHelper.RecordShadowKhanPlayed(this);
        }
    }

    /// <summary>
    /// 计算影噬加成 + 本回合兵团牌打出数量的额外格挡。
    /// </summary>
    private decimal GetBlockBonus()
    {
        int printedBaseBlock = GetPrintedBaseValue(5, 1);
        int bonusBlock = GetCurrentBaseBlock() - printedBaseBlock;
        return GetDisplayedShadowKhanBlockBonus(printedBaseBlock, bonusBlock);
    }

    /// <summary>
    /// 计算本次巨魔团应结算的基础格挡值。
    /// </summary>
    private int GetCurrentBaseBlock()
    {
        int shadowKhanPlayed = ShadowKhanHelper.GetShadowKhanPlayedThisTurn(this);
        int bonusCount = shadowKhanPlayed / 2;
        int bonusBlock = (int)DynamicVars["Magic"].BaseValue * bonusCount;
        return GetPrintedBaseValue(5, 1) + bonusBlock;
    }
}
