using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Talismans;

/// <summary>
/// 申猴能力：自身受到的伤害增加(Amount=1时25%，Amount=2时50%)。
/// 当敌人攻击意图时，对其反弹攻击输出2倍伤害。
/// 持续到敌方回合结束后移除。
/// </summary>
public class MonkeyTalismanCardPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 按攻击者记录等待反弹的伤害量。按1代使用敌人攻击输出的2倍，不按玩家实际掉血。
    /// </summary>
    private readonly Dictionary<Creature, Queue<int>> _pendingReflectDamageByDealer = new();

    /// <summary>
    /// 申猴属于增益能力（虽然有自伤副作用，但整体是增益）。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器堆叠方式。Amount=1表示25%，Amount=2表示50%。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 修改受到的伤害乘数：增加25%或50%。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Owner != null && target == Owner && props.IsPoweredAttack())
        {
            // Amount=1 → 1.25倍，Amount=2 → 1.50倍
            return Amount == 2 ? 1.50m : 1.25m;
        }

        return 1m;
    }

    /// <summary>
    /// 受到伤害前，记录敌方攻击造成的攻击输出。
    /// </summary>
    public override Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Owner == null || target != Owner || dealer?.Monster == null || dealer == Owner)
        {
            return Task.CompletedTask;
        }

        if (!props.IsPoweredAttack() || amount <= 0)
        {
            return Task.CompletedTask;
        }

        int reflectDamage = (int)amount * 2;
        RecordPendingReflectDamage(dealer, reflectDamage);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 受到敌方攻击意图伤害后，对攻击者反弹其攻击输出2倍的伤害。
    /// </summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Owner == null || target != Owner || dealer == null || dealer == Owner)
        {
            return;
        }

        int reflectDamage = TakePendingReflectDamage(dealer);

        if (reflectDamage <= 0)
        {
            return;
        }

        Flash();
        await CreatureCmd.Damage(choiceContext, dealer, reflectDamage, ValueProp.Unpowered, Owner, null);
        MainFile.Logger.Info($"【申猴】按敌方攻击输出反弹伤害{reflectDamage}给攻击者。");
    }

    /// <summary>
    /// 记录指定攻击者本次需要反弹的伤害，支持多名怪物和多段攻击按顺序结算。
    /// </summary>
    /// <param name="dealer">攻击来源。</param>
    /// <param name="reflectDamage">需要反弹的伤害。</param>
    private void RecordPendingReflectDamage(Creature dealer, int reflectDamage)
    {
        if (!_pendingReflectDamageByDealer.TryGetValue(dealer, out Queue<int>? pendingDamages))
        {
            pendingDamages = new Queue<int>();
            _pendingReflectDamageByDealer[dealer] = pendingDamages;
        }

        pendingDamages.Enqueue(reflectDamage);
        MainFile.Logger.Info($"【申猴】记录敌方攻击反弹伤害：来源={dealer.Monster?.Id.Entry ?? "未知怪物"}，反弹={reflectDamage}。");
    }

    /// <summary>
    /// 取出指定攻击者最近一次等待反弹的伤害，避免不同攻击来源互相覆盖。
    /// </summary>
    /// <param name="dealer">攻击来源。</param>
    /// <returns>需要反弹的伤害；没有记录时返回0。</returns>
    private int TakePendingReflectDamage(Creature dealer)
    {
        if (!_pendingReflectDamageByDealer.TryGetValue(dealer, out Queue<int>? pendingDamages) || pendingDamages.Count == 0)
        {
            return 0;
        }

        int reflectDamage = pendingDamages.Dequeue();
        if (pendingDamages.Count == 0)
        {
            _pendingReflectDamageByDealer.Remove(dealer);
        }

        return reflectDamage;
    }

    /// <summary>
    /// 敌方回合结束时移除自身。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (Owner == null || Owner.Side == side)
        {
            return;
        }

        MainFile.Logger.Info("【申猴】敌方回合结束，移除申猴能力。");
        _pendingReflectDamageByDealer.Clear();
        await PowerCmd.Remove(this);
    }
}
