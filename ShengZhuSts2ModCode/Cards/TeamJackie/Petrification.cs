using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.TeamJackie;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 石化：对目标施加石化，基础每层增伤10%，升级15%，并消耗。
/// </summary>
public class Petrification : TeamJackieCard
{
    /// <summary>
    /// 构造卡牌数值：1费技能，消耗。
    /// </summary>
    public Petrification() : base(1, CardType.Skill, TargetType.AnyEnemy)
    {
        WithVar("Magic", 10, 5);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 出牌时给目标石化1层。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        PetrifiedPower? existing = cardPlay.Target.Powers.OfType<PetrifiedPower>().FirstOrDefault();
        int percent = (int)DynamicVars["Magic"].BaseValue;
        if (existing == null)
        {
            PetrifiedPower? applied = await PowerCmd.Apply<PetrifiedPower>(cardPlay.Target, 1, Owner?.Creature, this);
            if (applied != null)
            {
                await applied.Configure(choiceContext, cardPlay.Target, Owner?.Creature, this, percent);
            }
            return;
        }

        await existing.AddPetrifiedStack(choiceContext, cardPlay.Target, Owner?.Creature, this, 1, percent);
    }
}
