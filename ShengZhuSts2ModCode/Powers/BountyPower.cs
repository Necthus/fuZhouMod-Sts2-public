using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 悬赏能力：施加黑手时造成伤害，带黑手的敌人死亡时发放金币。
/// </summary>
public class BountyPower : ShengZhuSts2ModPower, IBlackHandAppliedListener
{
    private readonly HashSet<Creature> _trackedTargets = [];

    /// <summary>
    /// 当前悬赏击杀奖励的金币数。
    /// </summary>
    public int GoldReward { get; private set; } = 8;

    /// <summary>
    /// 悬赏属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 悬赏使用计数器叠层显示每次触发伤害。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 施加或叠加悬赏能力，并同步更新金币奖励数值。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">能力拥有者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="goldReward">本次应设置或提升到的金币奖励。</param>
    /// <returns>最终的悬赏能力实例。</returns>
    public static async Task<BountyPower?> Apply(PlayerChoiceContext choiceContext, Player player, CardModel cardSource, int goldReward)
    {
        if (player?.Creature == null)
        {
            return null;
        }

        BountyPower? existing = player.Creature.GetPower<BountyPower>();
        if (existing == null)
        {
            BountyPower? created = await CommonActions.Apply<BountyPower>(choiceContext, player.Creature, cardSource, 2);
            if (created != null)
            {
                created.GoldReward = Math.Max(0, goldReward);
            }
            return created;
        }

        int oldDamage = existing.Amount;
        int oldGold = existing.GoldReward;
        await PowerCmd.ModifyAmount(existing, 2, player.Creature, cardSource);
        int goldPerDamage = oldDamage > 0 ? Math.Max(1, oldGold / oldDamage) : 4;
        existing.GoldReward = oldGold + goldPerDamage * 2;
        return existing;
    }

    /// <summary>
    /// 统一结算玩家身上所有悬赏能力的待发金币。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">需要结算的玩家。</param>
    /// <returns>异步任务。</returns>
    public static async Task ResolvePendingRewards(PlayerChoiceContext choiceContext, Player player)
    {
        if (player?.Creature == null)
        {
            return;
        }

        foreach (BountyPower power in player.Creature.Powers.OfType<BountyPower>().ToList())
        {
            await power.ResolvePendingRewards(choiceContext);
        }
    }

    /// <summary>
    /// 结算指定目标的悬赏奖励，用于雷苏诅咒等非普通伤害流程的斩杀兜底。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">悬赏能力所属玩家。</param>
    /// <param name="target">被击杀或即将被击杀的目标。</param>
    /// <returns>异步任务。</returns>
    public static async Task ResolveRewardForTarget(PlayerChoiceContext choiceContext, Player? player, Creature? target, bool targetHadBlackHand = false)
    {
        if (player?.Creature == null || target == null)
        {
            return;
        }

        foreach (BountyPower power in player.Creature.Powers.OfType<BountyPower>().ToList())
        {
            await power.ResolveRewardForTarget(choiceContext, target, targetHadBlackHand);
        }
    }

    /// <summary>
    /// 当玩家施加黑手时，对该目标造成悬赏伤害并登记为追踪目标。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">被施加黑手的目标。</param>
    /// <param name="appliedAmount">本次施加层数。</param>
    /// <returns>异步任务。</returns>
    public async Task OnBlackHandApplied(PlayerChoiceContext choiceContext, Creature target, int appliedAmount)
    {
        if (target == null || appliedAmount <= 0 || Amount <= 0)
        {
            return;
        }

        _trackedTargets.Add(target);
        await CreatureCmd.Damage(choiceContext, target, Amount, ValueProp.Unpowered, Owner, null, null);
        await ResolvePendingRewards(choiceContext);
    }

    /// <summary>
    /// 在玩家回合开始时顺手清一次待发的悬赏金币，避免跨回合漏结算。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">当前回合玩家。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner?.Player == null || player != Owner.Player)
        {
            return;
        }

        await ResolvePendingRewards(choiceContext);
    }

    /// <summary>
    /// 扫描已追踪目标，若其死亡则发放金币；若已脱离黑手状态则停止追踪。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <returns>异步任务。</returns>
    private async Task ResolvePendingRewards(PlayerChoiceContext choiceContext)
    {
        if (Owner?.Player == null || _trackedTargets.Count == 0)
        {
            return;
        }

        foreach (Creature trackedTarget in _trackedTargets.ToList())
        {
            if (trackedTarget == null)
            {
                _trackedTargets.Remove(trackedTarget!);
                continue;
            }

            if (!BlackHandCardHelper.IsLivingCreature(trackedTarget))
            {
                _trackedTargets.Remove(trackedTarget);
                if (GoldReward > 0)
                {
                    await PlayerCmd.GainGold(GoldReward, Owner.Player);
                }
                continue;
            }

            if (!trackedTarget.HasPower<BlackHandPower>())
            {
                _trackedTargets.Remove(trackedTarget);
            }
        }
    }

    /// <summary>
    /// 对单个目标发放悬赏奖励；只要目标仍带有黑手就会发放，并顺手移出追踪列表避免死亡回调重复发钱。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">被击杀或即将被击杀的目标。</param>
    /// <returns>异步任务。</returns>
    private async Task ResolveRewardForTarget(PlayerChoiceContext choiceContext, Creature target, bool targetHadBlackHand)
    {
        if (Owner?.Player == null || target == null)
        {
            return;
        }

        if (!targetHadBlackHand && !target.HasPower<BlackHandPower>())
        {
            _trackedTargets.Remove(target);
            return;
        }

        _trackedTargets.Remove(target);
        if (GoldReward > 0)
        {
            Flash();
            MainFile.Logger.Info($"【悬赏】指定目标结算金币：目标={target.Name}，金币={GoldReward}");
            await PlayerCmd.GainGold(GoldReward, Owner.Player);
        }
    }

    /// <summary>
    /// 当任何生物死亡时，检查它是否在追踪列表中，若是则发放金币。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="creature">死亡的生物。</param>
    /// <param name="wasRemovalPrevented">死亡是否被阻止。</param>
    /// <param name="deathAnimLength">死亡动画时长。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (wasRemovalPrevented || Owner?.Player == null)
        {
            return;
        }

        if (_trackedTargets.Remove(creature) && GoldReward > 0)
        {
            await PlayerCmd.GainGold(GoldReward, Owner.Player);
        }
    }
}
