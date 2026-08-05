using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 黑手帮·周：圣主初始牌组里的基础防御牌。
public class BlackHandChow : BlackHandGangCard
{
    // 构造卡牌数值：1 费，获得 5 点格挡，升级后额外增加 3 点格挡。
    public BlackHandChow() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
        // 使用原生 BlockVar，让黏糊、敏捷和虎符咒都走游戏标准格挡修正链路。
        WithBlock(5, 3);
        WithTags(CardTag.Defend);
    }

    // 出牌时获得格挡。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardBlock(this, cardPlay);
    }
}
