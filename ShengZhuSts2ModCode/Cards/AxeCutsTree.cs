using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 巨斧砍大树：2费罕见攻击牌，造成14点伤害。若目标有格挡，伤害提升50%。
/// 使用 WithCalculatedDamage 让框架正确计算力量/易伤等加成后再应用格挡加成。
/// </summary>
public class AxeCutsTree : AhFuCard
{
    // 构造卡牌数值：2 费，基础伤害 14（升级+6）；目标有格挡时额外加成 50%（即 +7 伤害，升级后 +10）。
    public AxeCutsTree() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        // base=14, bonus=7（即基础伤害的50%），lambda 有格挡返回1（启用bonus）否则0
        // upgrade=6（base升级后变20），bonusUpgrade=3（bonus升级后变10，即20的50%）
        WithCalculatedDamage(14, 7, static (_, target) => target != null && target.Block > 0 ? 1m : 0m, upgrade: 6, bonusUpgrade: 3);
    }

    // 出牌时使用框架标准攻击流程，伤害由 CalculatedDamage 动态计算。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        // 通过 CalculatedVar 获取针对目标计算后的最终伤害（含力量、易伤、格挡加成等）
        decimal damage = ((CalculatedVar)DynamicVars["CalculatedDamage"]).Calculate(cardPlay.Target);
        await CommonActions.CardAttack(this, cardPlay, cardPlay.Target, damage, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move).Execute(choiceContext);

        await ResolveBountyRewards(choiceContext);
    }
}
