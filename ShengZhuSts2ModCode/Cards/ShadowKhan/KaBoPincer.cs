using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

/// <summary>
/// 卡博-猎钳团：获得格挡并施加虚弱。
/// </summary>
public class KaBoPincer : BaseShadowKhanCard
{
    /// <summary>
    /// 构造卡牌数值：0费技能，3(5)格挡，施加1层虚弱。消耗。
    /// </summary>
    public KaBoPincer() : base(0, CardType.Skill, TargetType.AnyEnemy)
    {
        WithCalculatedBlock("Block", 3, static (card, _) => ((KaBoPincer)card).GetBlockBonus(), default, upgrade: 2, bonusUpgrade: 0);
        WithVar("Magic", 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 猎钳团印在卡面上的基础格挡。
    /// </summary>
    public override int PrintedBaseBlock => GetPrintedBaseValue(3, 2);

    /// <summary>
    /// 出牌时获得格挡并对目标施加虚弱。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            await ShadowKhanBlock(choiceContext, cardPlay);

            if (cardPlay.Target != null)
            {
                await CommonActions.Apply<WeakPower>(choiceContext, cardPlay.Target, this, (int)DynamicVars["Magic"].BaseValue);
            }
        }
        finally
        {
            ShadowKhanHelper.RecordShadowKhanPlayed(this);
        }
    }

    /// <summary>
    /// 计算手牌展示用的猎钳团总格挡。
    /// </summary>
    private decimal GetBlockBonus()
    {
        return GetDisplayedShadowKhanBlockBonus(PrintedBaseBlock, 0);
    }
}
