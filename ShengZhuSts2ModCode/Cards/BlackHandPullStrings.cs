using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 捞人：每回合开始时从弃牌堆捞牌回手，升级后改为固有。
public class BlackHandPullStrings : BlackHandGangCard
{
    // 构造卡牌数值：1 费能力牌，默认每回合捞 1 张。
    public BlackHandPullStrings() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    // 升级时为自身补上固有关键词。
    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }

    // 出牌时给自己挂上捞人能力。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.Apply<BlackHandPullStringsPower>(choiceContext, Owner.Creature, this, 1);
    }
}
