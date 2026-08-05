using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 巨大小玉：使当前格挡翻倍，基础版消耗，升级后不消耗。
/// </summary>
public class GiantJade : TeamJackieCard
{
    /// <summary>
    /// 构造卡牌数值：1费技能，基础版消耗。
    /// </summary>
    public GiantJade() : base(1, CardType.Skill, TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 升级时移除消耗。
    /// </summary>
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 出牌时按当前格挡量再获得同等格挡。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null || Owner.Creature.Block <= 0)
        {
            return;
        }

        await CreatureCmd.GainBlock(Owner.Creature, Owner.Creature.Block, ValueProp.Move, cardPlay);
    }
}
