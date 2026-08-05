using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 黑手帮·瓦龙：费用随主牌组黑手帮数量下降，伤害随本回合已打黑手帮牌数上升。
public class BlackHandValmont : BlackHandGangCard
{
    private const int BaseCost = 3;

    // 构造卡牌数值：3 费，基础伤害 12，升级后基础伤害 +3；每张已打黑手帮牌额外 +2 伤害，升级后额外值 +1。
    public BlackHandValmont() : base(BaseCost, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithCalculatedDamage(12, 2, static (card, _) => BlackHandCardHelper.CountBlackHandPlayedThisTurn(card), upgrade: 3, bonusUpgrade: 1);
        WithCalculatedVar("BlackHandApply", 2, static (card, _) => BlackHandCardHelper.CountBlackHandPlayedThisTurn(card), upgrade: 0, bonusUpgrade: 0);
        WithVar("Magic", 2, 1);
    }

    // 通过费用计算钩子临时修正瓦龙费用，避免 UI 可打出检查把动态费用写进联机状态。
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this))
        {
            return false;
        }

        int discount = CountBlackHandInMasterDeck() / 5;
        if (discount <= 0)
        {
            return false;
        }

        modifiedCost = Math.Max(0, originalCost - discount);
        return modifiedCost != originalCost;
    }

    // 出牌时先按动态伤害攻击，再按“2 + 本回合已打黑手帮牌数”施加黑手。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);
        await ApplyBlackHand(choiceContext, cardPlay.Target, 2 + CountBlackHandPlayedThisTurn());
        await ResolveBountyRewards(choiceContext);
    }
}
