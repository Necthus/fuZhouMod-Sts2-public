using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 尼嘉-忍者团：由尼嘉面具在回合开始时生成的衍生攻击牌。
/// </summary>
public class NiJiaNinja : BaseShadowKhanCard, IActualEnemyTargetProvider
{
    /// <summary>
    /// 默认基础伤害。
    /// </summary>
    private const int BaseDamage = 5;

    /// <summary>
    /// 默认升级伤害提升。
    /// </summary>
    private const int DamageUpgrade = 3;

    /// <summary>
    /// 手里剑生效后的基础伤害。
    /// </summary>
    private const int ShurikenBaseDamage = 3;

    /// <summary>
    /// 手里剑生效后的升级伤害提升。
    /// </summary>
    private const int ShurikenDamageUpgrade = 2;

    /// <summary>
    /// 构造卡牌数值：0费攻击，造成5(8)点伤害。消耗。
    /// </summary>
    public NiJiaNinja() : base(0, CardType.Attack, TargetType.AnyEnemy)
    {
        // base=5, upgrade=3 提供基础伤害值（预览卡用）；lambda 返回影噬加成（战斗中用）
        WithCalculatedDamage("Damage", BaseDamage, static (card, target) => ((NiJiaNinja)card).GetDamageBonus(target), default, upgrade: DamageUpgrade, bonusUpgrade: 0);
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 忍者团印在卡面上的基础伤害。
    /// </summary>
    public override int PrintedBaseDamage => GetCurrentPrintedBaseDamage();

    /// <summary>
    /// 获取尼嘉-忍者团本次实际影响的敌方目标；武士刀生效时视为群体攻击。
    /// </summary>
    /// <param name="cardPlay">本次出牌信息。</param>
    /// <returns>实际受影响的敌方目标列表。</returns>
    public IReadOnlyList<Creature> GetActualEnemyTargets(CardPlay? cardPlay)
    {
        return GetAttackTargets(cardPlay);
    }

    /// <summary>
    /// 出牌时对目标造成一次或多次普通攻击。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        try
        {
            IReadOnlyList<Creature> targets = GetAttackTargets(cardPlay);
            if (targets.Count == 0)
            {
                return;
            }

            int hitCount = GetHitCount();
            int hitDamage = CalculateNiJiaNinjaDamage();
            Dictionary<Creature, int> smokeBombAttackCounts = [];
            MainFile.Logger.Info($"【联机同步】来源=尼嘉-忍者团，玩家={FormatOwner()}，动作=开始结算，目标数={targets.Count}，目标=[{string.Join(",", targets.Select(target => target.Name))}]，武士刀={HasKatanaPower()}，攻击次数={hitCount}，单段基础结算伤害={hitDamage}，手里剑={HasShurikenPower()}，影噬={GetDominionAmount()}，双棍倍率={GetDominionDamageMultiplier()}，忍者协作加成={GetNinjaCooperationDamageBonus()}。");

            for (int i = 0; i < hitCount; i++)
            {
                IReadOnlyList<Creature> attackedTargets = await ShadowKhanAttackMany(choiceContext, targets, hitDamage);
                if (attackedTargets.Count == 0)
                {
                    break;
                }

                foreach (Creature target in attackedTargets)
                {
                    smokeBombAttackCounts[target] = smokeBombAttackCounts.GetValueOrDefault(target) + 1;
                }
            }

            await ResolveSmokeBombAttacks(choiceContext, smokeBombAttackCounts);
        }
        finally
        {
            ShadowKhanHelper.RecordShadowKhanPlayed(this);
        }
    }

    /// <summary>
    /// 获取当前实际印在卡面上的基础伤害，手里剑生效时改为3(5)。
    /// </summary>
    private int GetCurrentPrintedBaseDamage()
    {
        return HasShurikenPower()
            ? ShurikenBaseDamage + (IsUpgraded ? ShurikenDamageUpgrade : 0)
            : BaseDamage + (IsUpgraded ? DamageUpgrade : 0);
    }

    /// <summary>
    /// 计算影噬和尼嘉体系能力带来的动态加成。
    /// </summary>
    private decimal GetDamageBonus(Creature? target)
    {
        int baseDamageOffset = GetCurrentPrintedBaseDamage() - (BaseDamage + (IsUpgraded ? DamageUpgrade : 0));
        int extraDamage = GetDominionDamageBonus() + GetNinjaCooperationDamageBonus() + baseDamageOffset;
        return CalculateDisplayedShadowKhanDamage(Math.Max(0, BaseDamage + (IsUpgraded ? DamageUpgrade : 0) + extraDamage), target) - (BaseDamage + (IsUpgraded ? DamageUpgrade : 0));
    }

    /// <summary>
    /// 计算尼嘉-忍者团实际每段结算伤害。
    /// </summary>
    private int CalculateNiJiaNinjaDamage()
    {
        int damage = GetCurrentPrintedBaseDamage() + GetDominionDamageBonus() + GetNinjaCooperationDamageBonus();
        return Math.Max(0, damage);
    }

    /// <summary>
    /// 获取当前影噬对尼嘉-忍者团的伤害加成。
    /// </summary>
    private int GetDominionDamageBonus()
    {
        return GetDominionAmount() * GetDominionDamageMultiplier();
    }

    /// <summary>
    /// 获取双棍提供的影噬伤害倍率。
    /// </summary>
    private int GetDominionDamageMultiplier()
    {
        return Owner?.Creature?.GetPower<DoubleSticksPower>()?.DominionDamageMultiplier ?? 1;
    }

    /// <summary>
    /// 获取手里剑提供的攻击次数。
    /// </summary>
    private int GetHitCount()
    {
        return Math.Max(1, Owner?.Creature?.GetPower<ShurikenPower>()?.HitCount ?? 1);
    }

    /// <summary>
    /// 判断手里剑能力是否正在生效。
    /// </summary>
    private bool HasShurikenPower()
    {
        return Owner?.Creature?.HasPower<ShurikenPower>() == true;
    }

    /// <summary>
    /// 判断武士刀能力是否正在生效。
    /// </summary>
    private bool HasKatanaPower()
    {
        return Owner?.Creature?.HasPower<KatanaPower>() == true;
    }

    /// <summary>
    /// 获取本次攻击目标列表；武士刀生效时改为所有存活敌人。
    /// </summary>
    /// <param name="cardPlay">本次出牌信息。</param>
    /// <returns>实际攻击目标列表。</returns>
    private IReadOnlyList<Creature> GetAttackTargets(CardPlay? cardPlay)
    {
        if (HasKatanaPower() && Owner?.Creature?.CombatState != null)
        {
            return Owner.Creature.CombatState
                .GetOpponentsOf(Owner.Creature)
                .Where(target => target.IsAlive && !target.IsDead)
                .ToList();
        }

        if (cardPlay?.Target != null && cardPlay.Target.IsMonster && cardPlay.Target.IsAlive)
        {
            return [cardPlay.Target];
        }

        return [];
    }

    /// <summary>
    /// 获取忍者协作按消耗堆尼嘉-忍者团数量提供的每段伤害加成。
    /// </summary>
    private int GetNinjaCooperationDamageBonus()
    {
        return Owner?.Creature?.GetPower<NinjaCooperationPower>()?.GetDamageBonus(Owner) ?? 0;
    }

    /// <summary>
    /// 通知升级版烟雾弹：尼嘉-忍者团完成了一批攻击，按目标合并结算以减少群体多段等待。
    /// </summary>
    private async Task ResolveSmokeBombAttacks(PlayerChoiceContext choiceContext, IReadOnlyDictionary<Creature, int> attackCounts)
    {
        SmokeBombPower? smokeBombPower = Owner?.Creature?.GetPower<SmokeBombPower>();
        if (smokeBombPower == null)
        {
            return;
        }

        if (attackCounts.Count > 0)
        {
            await smokeBombPower.OnNiJiaNinjaAttacks(choiceContext, attackCounts, this);
        }

        await smokeBombPower.ResolveQueuedNiJiaNinjaDamageTriggers(choiceContext, this);
    }

    /// <summary>
    /// 格式化当前卡牌拥有者，方便联机日志对比。
    /// </summary>
    /// <returns>玩家网络ID和角色ID。</returns>
    private string FormatOwner()
    {
        return Owner == null ? "无玩家" : $"{Owner.NetId}/{Owner.Character.Id.Entry}";
    }
}
