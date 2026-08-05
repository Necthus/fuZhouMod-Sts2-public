using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 暗号：若上一张牌是黑手帮牌则回能抽牌，否则改为获得格挡。
public class BlackHandCodePhrase : BlackHandGangCard
{
    /// <summary>
    /// 上一张牌是黑手帮牌时，“暗号”的回能抽牌分支会触发，需要显示高亮提示。
    /// </summary>
    protected override bool ShouldGlowWhenPreviousBlackHandCard => true;

    // 构造卡牌数值：0 费，获得 4 点格挡，升级后格挡 +1，打出后消耗。
    public BlackHandCodePhrase() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(4, 1);
        WithKeywords(CardKeyword.Exhaust);
    }

    // 出牌时读取上一张已打出的牌，决定走“回能抽牌”还是“获得格挡”分支。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? previousCard = GetPreviousCardPlayedThisTurn();
        if (BlackHandCardHelper.IsBlackHandCard(previousCard))
        {
            await PlayerCmd.GainEnergy(1, Owner);
            await CardPileCmd.Draw(choiceContext, 1, Owner);
            return;
        }

        await CommonActions.CardBlock(this, cardPlay);
    }
}
