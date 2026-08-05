using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

/// <summary>
/// 虎符·阴成龙：按除自身外的手牌数量获得格挡，最多 16/20。
/// </summary>
public class TigerTalismanYin : TeamJackieCard
{
    /// <summary>
    /// 构造卡牌数值：1费技能，每张手牌2/3格挡，上限16/20。
    /// </summary>
    public TigerTalismanYin() : base(1, CardType.Skill, TargetType.Self)
    {
        WithVar("Magic", 2, 1);
        WithVar("MaxBlock", 16, 4);
        WithCalculatedVar("CalculatedBlock", 0, static (card, _) => CalculateBlock(card), upgrade: 0, bonusUpgrade: 0);
    }

    /// <summary>
    /// 出牌时获得当前动态格挡。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await CreatureCmd.GainBlock(Owner.Creature, CalculateBlock(this), ValueProp.Move, cardPlay);
    }

    /// <summary>
    /// 计算一代动态格挡：除自身外每张手牌给 Magic 格挡，受上限限制。
    /// </summary>
    /// <param name="card">当前卡牌。</param>
    /// <returns>本次格挡数。</returns>
    private static decimal CalculateBlock(CardModel card)
    {
        int handCount = card.Owner?.PlayerCombatState?.Hand.Cards.Count ?? 0;
        int otherHandCount = Math.Max(0, handCount - 1);
        int perCard = (int)card.DynamicVars["Magic"].BaseValue;
        int maxBlock = (int)card.DynamicVars["MaxBlock"].BaseValue;
        return Math.Min(maxBlock, otherHandCount * perCard);
    }
}
