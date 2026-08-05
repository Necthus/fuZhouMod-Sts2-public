using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Enchantments;

/// <summary>
/// 恶魔魔气附魔基类：统一限制附魔对象，并提供卡面额外说明。
/// </summary>
public abstract class AbstractDemonQiEnchantment : CustomEnchantmentModel
{
    /// <summary>
    /// 魔气短名。
    /// </summary>
    protected abstract string Prefix { get; }

    /// <summary>
    /// 魔气说明。
    /// </summary>
    protected abstract string ExtraDescription { get; }

    /// <summary>
    /// 附魔图标文件名。
    /// </summary>
    protected abstract string IconFileName { get; }

    /// <summary>
    /// 恶魔魔气会在卡面显示额外文本。
    /// </summary>
    public override bool HasExtraCardText => true;

    /// <summary>
    /// 魔气图标复用默认缺失图，避免没有资源时阻塞功能。
    /// </summary>
    protected override string CustomIconPath => $"res://ShengZhuSts2Mod/images/orbs/masks/{IconFileName}.png";

    /// <summary>
    /// 默认诅咒、状态、任务牌不能附魔，且一张牌只能有一种魔气。
    /// </summary>
    /// <param name="card">候选卡牌。</param>
    /// <returns>可附魔时返回 true。</returns>
    public override bool CanEnchant(CardModel card)
    {
        return base.CanEnchant(card)
            && card.Type is not (CardType.Curse or CardType.Status or CardType.Quest)
            && card.Enchantment is not AbstractDemonQiEnchantment;
    }

    /// <summary>
    /// 魔气附魔标题。
    /// </summary>
    /// <returns>用于本地化的调试文本。</returns>
    public override string ToString()
    {
        return $"【{Prefix}】{ExtraDescription}";
    }
}

/// <summary>
/// 天之魔气：打出后对受影响敌人有 1/8 概率施加恐惧。
/// </summary>
public class HeavenDemonQiEnchantment : AbstractDemonQiEnchantment
{
    /// <summary>
    /// 魔气短名。
    /// </summary>
    protected override string Prefix => "天";

    /// <summary>
    /// 图标文件名。
    /// </summary>
    protected override string IconFileName => "HeavenDemonQi";

    /// <summary>
    /// 魔气说明。
    /// </summary>
    protected override string ExtraDescription => "给予目标八分之一概率施加恐惧。";

    /// <summary>
    /// 打出时判定恐惧。
    /// </summary>
    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (Card?.Owner?.Creature == null)
        {
            MainFile.Logger.Info("【天之魔气】恐惧判定跳过：卡牌、拥有者或来源生物为空。");
            return;
        }

        IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> targets = DemonQiTargetHelper.GetAffectedEnemies(Card, cardPlay);
        if (targets.Count == 0)
        {
            MainFile.Logger.Info($"【天之魔气】恐惧判定跳过：卡牌={Card.Title}，原因=没有可影响敌人。");
            return;
        }

        foreach (var target in targets)
        {
            int roll = StableRandomHelper.NextInt(Card.Owner, 8);
            bool applyFear = roll == 0;
            MainFile.Logger.Info($"【天之魔气】恐惧判定：卡牌={Card.Title}，目标={target.Name}，概率=1/8，摇点={roll}/8，玩家={Card.Owner.NetId}，施加恐惧={applyFear}。");
            if (applyFear)
            {
                await PowerCmd.Apply<FearPower>(target, 1, Card.Owner.Creature, Card);
            }
        }
    }
}

/// <summary>
/// 地之魔气：打出后本回合格挡不清空。
/// </summary>
public class EarthDemonQiEnchantment : AbstractDemonQiEnchantment
{
    /// <summary>
    /// 魔气短名。
    /// </summary>
    protected override string Prefix => "地";

    /// <summary>
    /// 图标文件名。
    /// </summary>
    protected override string IconFileName => "EarthDemonQi";

    /// <summary>
    /// 魔气说明。
    /// </summary>
    protected override string ExtraDescription => "本回合未消耗完的护甲不消失。";

    /// <summary>
    /// 打出时赋予模糊。
    /// </summary>
    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (Card?.Owner?.Creature != null)
        {
            await PowerCmd.Apply<BlurPower>(Card.Owner.Creature, 1, Card.Owner.Creature, Card);
        }
    }
}

/// <summary>
/// 风之魔气：打出后抽牌，并给所有敌人风势。
/// </summary>
public class WindDemonQiEnchantment : AbstractDemonQiEnchantment
{
    /// <summary>
    /// 魔气短名。
    /// </summary>
    protected override string Prefix => "风";

    /// <summary>
    /// 图标文件名。
    /// </summary>
    protected override string IconFileName => "WindDemonQi";

    /// <summary>
    /// 魔气说明。
    /// </summary>
    protected override string ExtraDescription => "抽2张卡，并给予所有敌人2层风势。";

    /// <summary>
    /// 打出时抽牌并施加风势。
    /// </summary>
    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        var card = Card;
        var creature = card?.Owner?.Creature;
        if (card?.Owner == null || creature?.CombatState == null)
        {
            return;
        }

        await CardPileCmd.Draw(choiceContext, 2, card.Owner);
        foreach (var target in creature.CombatState.HittableEnemies)
        {
            await PowerCmd.Apply<WindCatalystPower>(target, 2, creature, card);
        }
    }
}

/// <summary>
/// 雷之魔气：在战斗中每打出一次，当前牌费用降低 1。
/// </summary>
public class ThunderDemonQiEnchantment : AbstractDemonQiEnchantment
{
    /// <summary>
    /// 魔气短名。
    /// </summary>
    protected override string Prefix => "雷";

    /// <summary>
    /// 图标文件名。
    /// </summary>
    protected override string IconFileName => "ThunderDemonQi";

    /// <summary>
    /// 魔气说明。
    /// </summary>
    protected override string ExtraDescription => "在战斗中，每被打出一次耗能降低1点。";

    /// <summary>
    /// 打出后本场战斗降费。
    /// </summary>
    public override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (Card != null)
        {
            Card.EnergyCost.AddThisCombat(-1, true);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// 水之魔气：攻击牌打出后给受影响敌人潮湿。
/// </summary>
public class WaterDemonQiEnchantment : AbstractDemonQiEnchantment
{
    /// <summary>
    /// 魔气短名。
    /// </summary>
    protected override string Prefix => "水";

    /// <summary>
    /// 图标文件名。
    /// </summary>
    protected override string IconFileName => "WaterDemonQi";

    /// <summary>
    /// 魔气说明。
    /// </summary>
    protected override string ExtraDescription => "给予目标2层潮湿。";

    /// <summary>
    /// 水魔气只允许攻击牌。
    /// </summary>
    public override bool CanEnchant(CardModel card)
    {
        return base.CanEnchant(card) && card.Type == CardType.Attack;
    }

    /// <summary>
    /// 打出时施加潮湿。
    /// </summary>
    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (Card?.Owner?.Creature == null)
        {
            return;
        }

        foreach (var target in DemonQiTargetHelper.GetAffectedEnemies(Card, cardPlay))
        {
            await PanKuDemonQiHelper.ApplySoaked(target, 2, Card.Owner.Creature, Card);
        }
    }
}

/// <summary>
/// 火之魔气：攻击牌打出后给受影响敌人灼烧。
/// </summary>
public class FireDemonQiEnchantment : AbstractDemonQiEnchantment
{
    /// <summary>
    /// 魔气短名。
    /// </summary>
    protected override string Prefix => "火";

    /// <summary>
    /// 图标文件名。
    /// </summary>
    protected override string IconFileName => "FireDemonQi";

    /// <summary>
    /// 魔气说明。
    /// </summary>
    protected override string ExtraDescription => "给予目标2层灼烧。";

    /// <summary>
    /// 火魔气只允许攻击牌。
    /// </summary>
    public override bool CanEnchant(CardModel card)
    {
        return base.CanEnchant(card) && card.Type == CardType.Attack;
    }

    /// <summary>
    /// 打出时施加灼烧。
    /// </summary>
    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (Card?.Owner?.Creature == null)
        {
            return;
        }

        foreach (var target in DemonQiTargetHelper.GetAffectedEnemies(Card, cardPlay))
        {
            await PanKuDemonQiHelper.ApplyBurning(target, 2, Card.Owner.Creature, Card);
        }
    }
}

/// <summary>
/// 山之魔气：移除虚无与消耗。
/// </summary>
public class MountainDemonQiEnchantment : AbstractDemonQiEnchantment
{
    /// <summary>
    /// 魔气短名。
    /// </summary>
    protected override string Prefix => "山";

    /// <summary>
    /// 图标文件名。
    /// </summary>
    protected override string IconFileName => "MountainDemonQi";

    /// <summary>
    /// 魔气说明。
    /// </summary>
    protected override string ExtraDescription => "移除此牌的虚无与消耗属性。";

    /// <summary>
    /// 山魔气只允许有虚无或消耗的牌。
    /// </summary>
    public override bool CanEnchant(CardModel card)
    {
        return base.CanEnchant(card) && (card.Keywords.Contains(CardKeyword.Ethereal) || card.Keywords.Contains(CardKeyword.Exhaust));
    }

    /// <summary>
    /// 附魔时移除虚无与消耗。
    /// </summary>
    protected override void OnEnchant()
    {
        Card.RemoveKeyword(CardKeyword.Ethereal);
        Card.RemoveKeyword(CardKeyword.Exhaust);
    }
}

/// <summary>
/// 月之魔气：每场战斗第一次命中正力量敌人时，将其力量反转为负值。
/// </summary>
public class MoonDemonQiEnchantment : AbstractDemonQiEnchantment
{
    /// <summary>
    /// 本场战斗是否已经触发过。
    /// </summary>
    private bool _triggeredThisCombat;

    /// <summary>
    /// 魔气短名。
    /// </summary>
    protected override string Prefix => "月";

    /// <summary>
    /// 图标文件名。
    /// </summary>
    protected override string IconFileName => "MoonDemonQi";

    /// <summary>
    /// 魔气说明。
    /// </summary>
    protected override string ExtraDescription => "将目标身上的正数力量反转为负数。";

    /// <summary>
    /// 月魔气只允许攻击牌。
    /// </summary>
    public override bool CanEnchant(CardModel card)
    {
        return base.CanEnchant(card) && card.Type == CardType.Attack;
    }

    /// <summary>
    /// 战斗开始重置触发标记。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        _triggeredThisCombat = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 打出时反转正力量。
    /// </summary>
    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (_triggeredThisCombat || Card?.Owner?.Creature == null)
        {
            return;
        }

        List<(Creature Target, StrengthPower Strength, int OldAmount)> strengthTargets = DemonQiTargetHelper.GetAffectedEnemies(Card, cardPlay)
            .Select(target => (Target: target, Strength: target.GetPower<StrengthPower>()))
            .Where(item => item.Strength != null && item.Strength.Amount > 0)
            .Select(item => (item.Target, Strength: item.Strength!, OldAmount: item.Strength!.Amount))
            .ToList();
        if (strengthTargets.Count == 0)
        {
            return;
        }

        foreach ((Creature target, StrengthPower strength, int oldAmount) in strengthTargets)
        {
            await PowerCmd.ModifyAmount(strength, -oldAmount * 2, Card.Owner.Creature, Card);
            MainFile.Logger.Info($"【月之魔气】反转目标力量：目标={target.Name}，原力量={oldAmount}。");
        }

        _triggeredThisCombat = true;
    }
}

/// <summary>
/// 恶魔魔气目标辅助：按卡牌目标类型推导本次出牌影响的敌人。
/// </summary>
internal static class DemonQiTargetHelper
{
    /// <summary>
    /// 获取本次出牌会影响到的敌人。
    /// </summary>
    /// <param name="card">附魔所在卡牌。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    /// <returns>受影响敌人列表。</returns>
    public static IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> GetAffectedEnemies(CardModel card, CardPlay? cardPlay)
    {
        if (card.Owner?.Creature?.CombatState == null)
        {
            return [];
        }

        if (card is IActualEnemyTargetProvider actualTargetProvider)
        {
            return actualTargetProvider.GetActualEnemyTargets(cardPlay);
        }

        if (card.TargetType == TargetType.AllEnemies)
        {
            return BlackHandCardHelper.GetLivingOpponents(card);
        }

        if (cardPlay?.Target != null && cardPlay.Target.IsMonster && cardPlay.Target.IsAlive)
        {
            return [cardPlay.Target];
        }

        return [];
    }
}
