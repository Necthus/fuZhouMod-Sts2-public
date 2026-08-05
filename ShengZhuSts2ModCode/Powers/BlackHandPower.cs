using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

// 黑手：记录目标身上的黑手层数，供黑手帮相关卡牌判断额外效果。
public class BlackHandPower : ShengZhuSts2ModPower
{
    // 黑手属于可叠层的减益能力。
    public override PowerType Type => PowerType.Debuff;

    // 黑手使用计数器堆叠方式来显示层数。
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 获取目标当前持有的黑手层数。
    /// </summary>
    /// <param name="target">目标单位。</param>
    /// <returns>目标身上的黑手层数；若没有则返回 0。</returns>
    public static int GetAmount(Creature? target)
    {
        if (target == null)
        {
            return 0;
        }

        BlackHandPower? blackHandPower = target.GetPower<BlackHandPower>();
        if (blackHandPower != null)
        {
            return blackHandPower.Amount;
        }

        foreach (PowerModel power in target.Powers)
        {
            if (power.Id.Entry == "SHENGZHUSTS2MOD-BLACK_HAND_POWER" || power.GetType().Name == nameof(BlackHandPower))
            {
                return power.Amount;
            }
        }

        return 0;
    }

    /// <summary>
    /// 判断目标当前是否至少有 1 层黑手。
    /// </summary>
    /// <param name="target">目标单位。</param>
    /// <returns>存在黑手时返回 true。</returns>
    public static bool HasAny(Creature? target)
    {
        return GetAmount(target) > 0;
    }

    /// <summary>
    /// 以卡牌为来源向目标施加黑手，并通知相关监听能力。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">被施加黑手的目标。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="amount">施加层数。</param>
    /// <returns>最终的黑手能力实例。</returns>
    public static async Task<BlackHandPower?> Apply(PlayerChoiceContext choiceContext, Creature target, CardModel cardSource, int amount)
    {
        return await Apply(choiceContext, target, cardSource.Owner, cardSource, amount);
    }

    /// <summary>
    /// 以玩家或能力为来源向目标施加黑手，并通知相关监听能力。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">被施加黑手的目标。</param>
    /// <param name="sourcePlayer">来源玩家。</param>
    /// <param name="cardSource">来源卡牌；若来自能力可为 null。</param>
    /// <param name="amount">施加层数。</param>
    /// <returns>最终的黑手能力实例。</returns>
    public static async Task<BlackHandPower?> Apply(PlayerChoiceContext choiceContext, Creature target, Player? sourcePlayer, CardModel? cardSource, int amount)
    {
        if (target == null || amount <= 0)
        {
            return null;
        }

        BlackHandPower? result = cardSource != null
            ? await CommonActions.Apply<BlackHandPower>(choiceContext, target, cardSource, amount)
            : await PowerCmd.Apply<BlackHandPower>(target, amount, sourcePlayer?.Creature, null);

        if (result != null && sourcePlayer?.Creature != null)
        {
            await NotifyApplied(choiceContext, target, sourcePlayer, amount);
        }

        return result;
    }

    /// <summary>
    /// 消耗目标身上的指定层数黑手。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">被消耗黑手的目标。</param>
    /// <param name="sourcePlayer">来源玩家。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="amount">希望消耗的层数。</param>
    /// <returns>本次实际消耗的黑手层数。</returns>
    public static async Task<int> Consume(PlayerChoiceContext choiceContext, Creature target, Player? sourcePlayer, CardModel? cardSource, int amount)
    {
        if (target == null || amount <= 0)
        {
            return 0;
        }

        BlackHandPower? power = target.GetPower<BlackHandPower>();
        if (power == null)
        {
            return 0;
        }

        int consumed = Math.Min(amount, power.Amount);
        if (consumed <= 0)
        {
            return 0;
        }

        if (consumed >= power.Amount)
        {
            await PowerCmd.Remove(power);
        }
        else
        {
            await PowerCmd.ModifyAmount(power, -consumed, sourcePlayer?.Creature, cardSource);
        }

        return consumed;
    }

    /// <summary>
    /// 消耗目标全部黑手层数。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">被消耗黑手的目标。</param>
    /// <param name="sourcePlayer">来源玩家。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>本次实际消耗的黑手总层数。</returns>
    public static async Task<int> ConsumeAll(PlayerChoiceContext choiceContext, Creature target, Player? sourcePlayer, CardModel? cardSource)
    {
        int current = GetAmount(target);
        if (current <= 0)
        {
            return 0;
        }

        return await Consume(choiceContext, target, sourcePlayer, cardSource, current);
    }

    /// <summary>
    /// 在玩家成功施加黑手后，通知其身上所有需要联动的能力。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">本次被施加黑手的目标。</param>
    /// <param name="sourcePlayer">来源玩家。</param>
    /// <param name="amount">本次实际施加层数。</param>
    /// <returns>异步任务。</returns>
    private static async Task NotifyApplied(PlayerChoiceContext choiceContext, Creature target, Player sourcePlayer, int amount)
    {
        foreach (IBlackHandAppliedListener listener in sourcePlayer.Creature.Powers.OfType<IBlackHandAppliedListener>().ToList())
        {
            await listener.OnBlackHandApplied(choiceContext, target, amount);
        }
    }
}
