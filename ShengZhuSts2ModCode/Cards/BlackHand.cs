using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 黑手帮：升级手牌中全部黑手帮和阿福卡，升级后范围扩大到抽牌堆。
public class BlackHand : BlackHandGangCard
{
    // 构造卡牌数值：1 费，不指定目标，打出后消耗。
    public BlackHand() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
    {
        WithKeywords(CardKeyword.Exhaust);
    }

    // 出牌时遍历目标牌堆，升级全部黑手帮和阿福牌。
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var playerCombatState = Owner.PlayerCombatState;
        if (playerCombatState == null)
        {
            return Task.CompletedTask;
        }

        IEnumerable<CardModel> upgradeTargets = IsUpgraded
            ? playerCombatState.Hand.Cards.Concat(playerCombatState.DrawPile.Cards)
            : playerCombatState.Hand.Cards;

        UpgradeEligibleCards(upgradeTargets);

        return Task.CompletedTask;
    }

    // 升级符合条件的黑手帮或阿福牌。
    private static void UpgradeEligibleCards(IEnumerable<CardModel> cards)
    {
        foreach (CardModel card in cards.Distinct().ToList())
        {
            if (BlackHandCardHelper.IsAhFuOrBlackHandCard(card) && card.IsUpgradable)
            {
                CardCmd.Upgrade(card, CardPreviewStyle.None);
            }
        }
    }
}
