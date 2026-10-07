using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

// 收网：先打一段，若目标黑手够多则每 5 层再追击一次。
public class BlackHandCloseNet : BlackHandGangCard
{
    private const int BaseDamage = 10;
    private const int DamageUpgrade = 4;
    private const int BlackHandPerExtraHit = 5;

    // 构造卡牌数值：2 费，造成 10 点伤害，升级后伤害 +4。
    public BlackHandCloseNet() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(BaseDamage, DamageUpgrade);
        WithCalculatedVar("TotalDamage", 0, static (card, target) => CalculateTotalPreviewDamage(card, target), upgrade: 0, bonusUpgrade: 0);
    }

    // 出牌时先攻击一次，再按目标黑手每 5 层追加一次同伤害追击。
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
        {
            return;
        }

        decimal hitDamage = DynamicVars.Damage.BaseValue;
        int blackHandAmount = BlackHandPower.GetAmount(cardPlay.Target);
        int extraHits = blackHandAmount / BlackHandPerExtraHit;
        decimal previewMainDamage = CalculateModifiedHitDamage(this, cardPlay.Target, hitDamage, out decimal rawPreviewMainDamage);
        decimal previewTotalDamage = previewMainDamage * (extraHits + 1);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【收网】实际结算前计算：目标={cardPlay.Target.Name}，目标能力={BuildPowerDebugText(cardPlay.Target)}，黑手层数={blackHandAmount}，每{BlackHandPerExtraHit}层追加1次，追加次数={extraHits}，单段原始伤害={hitDamage}，单段修正后={rawPreviewMainDamage}，单段取整后={previewMainDamage}，预计总伤害={previewMainDamage}*{extraHits + 1}={previewTotalDamage}。");

        await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);

        if (extraHits > 0 && !cardPlay.Target.IsDead)
        {
            await ConsumeBlackHand(choiceContext, cardPlay.Target, extraHits * BlackHandPerExtraHit);
            for (int i = 0; i < extraHits && !cardPlay.Target.IsDead; i++)
            {
                await CreatureCmd.Damage(choiceContext, cardPlay.Target, previewMainDamage, ValueProp.Move | ValueProp.Unpowered, Owner.Creature, this, cardPlay);
            }
        }

        await ResolveBountyRewards(choiceContext);
    }

    // 逐段计算总伤害预览，避免先合并伤害再被虚弱等效果修正导致取整错误。
    private static decimal CalculateTotalPreviewDamage(CardModel card, Creature? target)
    {
        if (target == null)
        {
            return 0;
        }

        decimal mainHitDamage = GetBaseHitDamage(card);
        int blackHandAmount = BlackHandPower.GetAmount(target);
        int extraHits = blackHandAmount / BlackHandPerExtraHit;
        decimal modifiedMainHitDamage = CalculateModifiedHitDamage(card, target, mainHitDamage, out decimal rawModifiedMainHitDamage);
        decimal totalDamage = modifiedMainHitDamage * (extraHits + 1);

        ShengZhuLogHelper.VerboseCombatInfo(() => $"【收网】卡面预览计算：目标={target.Name}，目标能力={BuildPowerDebugText(target)}，黑手层数={blackHandAmount}，每{BlackHandPerExtraHit}层追加1次，追加次数={extraHits}，单段原始伤害={mainHitDamage}，单段修正后={rawModifiedMainHitDamage}，单段取整后={modifiedMainHitDamage}，预计总伤害={modifiedMainHitDamage}*{extraHits + 1}={totalDamage}。");
        return totalDamage;
    }

    // 获取收网当前等级的基础单段伤害，用于总伤害预览的额外段计算。
    private static int GetBaseHitDamage(CardModel card)
    {
        return BaseDamage + (card.IsUpgraded ? DamageUpgrade : 0);
    }

    // 按游戏伤害修正链计算单段伤害，虚弱等每段单独取整后再汇总。
    private static decimal CalculateModifiedHitDamage(CardModel card, Creature target, decimal rawDamage, out decimal rawModifiedDamage)
    {
        if (card.Owner?.RunState == null || card.Owner?.Creature == null)
        {
            rawModifiedDamage = rawDamage;
            return rawDamage;
        }

        rawModifiedDamage = Hook.ModifyDamage(
            card.Owner.RunState,
            card.CombatState,
            target,
            card.Owner.Creature,
            rawDamage,
            ValueProp.Move,
            card,
            null,
            ModifyDamageHookType.All,
            CardPreviewMode.Normal,
            out _);

        return Math.Floor(rawModifiedDamage);
    }

    // 构建目标能力调试文本，用于确认预览目标身上的黑手层数来源。
    private static string BuildPowerDebugText(Creature target)
    {
        if (target.Powers.Count == 0)
        {
            return "无";
        }

        return string.Join("；", target.Powers.Select(power => $"{power.Id.Entry}/{power.GetType().Name}/{power.Amount}"));
    }
}
