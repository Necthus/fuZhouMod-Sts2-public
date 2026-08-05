using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

/// <summary>
/// 雷苏-异形团：施加诅咒（LeiSuCursePower），本回合每多打1张兵团+1层。
/// </summary>
public class LeiSuAlien : BaseShadowKhanCard
{
    /// <summary>
    /// 构造卡牌数值：0费技能，施加5(8)层诅咒 + 本回合已打出兵团牌数。消耗。
    /// 使用 WithCalculatedVar 让手牌中实时显示最终诅咒层数。
    /// </summary>
    public LeiSuAlien() : base(0, CardType.Skill, TargetType.AnyEnemy)
    {
        WithCalculatedVar("Magic", 5, static (card, _) => ShadowKhanHelper.GetShadowKhanPlayedThisTurn(card), upgrade: 3, bonusUpgrade: 0);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 出牌时对目标施加诅咒。
    /// 最终诅咒层数 = 基础Magic值 + 本回合已打出兵团牌数。
    /// 注意：DynamicVars["Magic"].BaseValue 在 OnPlay 时不含动态计算加成，需手动计算。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            if (cardPlay.Target == null)
            {
                return;
            }

            int baseCurse = IsUpgraded ? 8 : 5;
            int shadowKhanPlayed = ShadowKhanHelper.GetShadowKhanPlayedThisTurn(this);
            int totalCurse = baseCurse + shadowKhanPlayed;

            MainFile.Logger.Info($"【雷苏异形团】结算日志：基础诅咒={baseCurse}，本回合已打出兵团={shadowKhanPlayed}，最终总诅咒={totalCurse}");
            await CommonActions.Apply<LeiSuCursePower>(choiceContext, cardPlay.Target, this, totalCurse);
        }
        finally
        {
            ShadowKhanHelper.RecordShadowKhanPlayed(this);
        }
    }
}
