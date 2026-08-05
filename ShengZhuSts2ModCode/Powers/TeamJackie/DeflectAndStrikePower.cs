using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.TeamJackie;

/// <summary>
/// 借力打力能力：本回合完全格挡敌方普通攻击时，向攻击者反弹等额伤害。
/// </summary>
public class DeflectAndStrikePower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 记录每个怪物本次攻击打过来的伤害，用于完整格挡后按一代 info.output 逻辑反弹。
    /// </summary>
    private readonly Dictionary<Creature, Queue<int>> _pendingAttackDamageByDealer = new();

    /// <summary>
    /// 能力类型：增益。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 层数显示为计数器。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 受击前记录敌人本次攻击伤害，避免完整格挡时结算结果里拿不到稳定的被格挡伤害。
    /// </summary>
    public override Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        MainFile.Logger.Info($"【借力打力】受击前钩子：目标={GetCreatureLogName(target)}，来源={GetCreatureLogName(dealer)}，伤害={amount}，属性={props}。");

        if (Owner == null || target != Owner || dealer == null || dealer == Owner || dealer.Monster == null || !props.IsPoweredAttack() || amount <= 0)
        {
            return Task.CompletedTask;
        }

        if (!_pendingAttackDamageByDealer.TryGetValue(dealer, out Queue<int>? pendingDamages))
        {
            pendingDamages = new Queue<int>();
            _pendingAttackDamageByDealer[dealer] = pendingDamages;
        }

        pendingDamages.Enqueue((int)Math.Ceiling(amount));
        MainFile.Logger.Info($"【借力打力】记录敌人攻击伤害：来源={dealer.Monster.Id.Entry}，伤害={amount}。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 受击后若完整挡住敌人的攻击动作，则按受击前记录的攻击伤害反弹给攻击者。
    /// </summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        MainFile.Logger.Info($"【借力打力】受击后钩子：目标={GetCreatureLogName(target)}，来源={GetCreatureLogName(dealer)}，生命伤害={result.UnblockedDamage}，格挡伤害={result.BlockedDamage}，完整格挡={result.WasFullyBlocked}，属性={props}。");

        if (Owner == null || target != Owner || dealer == null || dealer == Owner || dealer.Monster == null)
        {
            return;
        }

        int reflectedDamage = TakePendingAttackDamage(dealer);
        // 只要本次攻击没有造成生命伤害，就按 1 代“完整格挡”逻辑反弹；多名怪物会按每次受击独立判定。
        if (!props.IsPoweredAttack() || result.UnblockedDamage > 0 || reflectedDamage <= 0)
        {
            return;
        }

        Flash();
        await CreatureCmd.Damage(choiceContext, dealer, reflectedDamage, ValueProp.Unpowered, Owner, null);
        MainFile.Logger.Info($"【借力打力】完整格挡后反弹敌人意图伤害：来源={dealer.Monster.Id.Entry}，伤害={reflectedDamage}。");
    }

    /// <summary>
    /// 取出指定攻击者最近一次记录的攻击伤害，确保多段攻击和多名怪物按受击顺序独立结算。
    /// </summary>
    /// <param name="dealer">攻击来源。</param>
    /// <returns>记录到的攻击伤害；没有记录时返回 0。</returns>
    private int TakePendingAttackDamage(Creature dealer)
    {
        if (!_pendingAttackDamageByDealer.TryGetValue(dealer, out Queue<int>? pendingDamages) || pendingDamages.Count == 0)
        {
            MainFile.Logger.Info("【借力打力】未找到敌人攻击伤害记录，跳过本次反弹。");
            return 0;
        }

        int damage = pendingDamages.Dequeue();
        if (pendingDamages.Count == 0)
        {
            _pendingAttackDamageByDealer.Remove(dealer);
        }

        return damage;
    }

    /// <summary>
    /// 生成用于调试日志的单位名称，避免直接访问 Creature 不存在的 Id。
    /// </summary>
    /// <param name="creature">单位。</param>
    /// <returns>日志名称。</returns>
    private string GetCreatureLogName(Creature? creature)
    {
        if (creature == null)
        {
            return "无";
        }

        if (creature == Owner)
        {
            return "自己";
        }

        return creature.Monster?.Id.Entry ?? (creature.IsPlayer ? "玩家" : "未知单位");
    }

    /// <summary>
    /// 敌方回合结束时移除此能力，保持和原生火焰屏障同类临时反伤能力一致。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        MainFile.Logger.Info($"【借力打力】回合结束检查：结束方={side}，持有者阵营={Owner?.Side}。");

        if (Owner != null && Owner.Side != side)
        {
            MainFile.Logger.Info("【借力打力】敌方回合结束，移除能力。");
            await PowerCmd.Remove(this);
        }
    }
}
