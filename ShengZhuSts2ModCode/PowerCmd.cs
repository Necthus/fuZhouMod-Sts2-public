using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode;

/// <summary>
/// 能力命令兼容层：适配游戏新版 PowerCmd 需要 PlayerChoiceContext 的接口。
/// </summary>
public static class PowerCmd
{
    /// <summary>
    /// 给单个目标施加能力，使用不会弹出玩家选择的上下文兼容旧调用点。
    /// </summary>
    /// <typeparam name="T">能力类型。</typeparam>
    /// <param name="target">施加目标。</param>
    /// <param name="amount">施加层数。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="silent">是否静默施加。</param>
    /// <returns>施加后的能力实例。</returns>
    public static Task<T?> Apply<T>(Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
        where T : PowerModel
    {
        return MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<T>(NewChoiceContext(), target, amount, applier, cardSource, silent);
    }

    /// <summary>
    /// 给单个目标施加能力，使用调用方提供的玩家选择上下文。
    /// </summary>
    /// <typeparam name="T">能力类型。</typeparam>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">施加目标。</param>
    /// <param name="amount">施加层数。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="silent">是否静默施加。</param>
    /// <returns>施加后的能力实例。</returns>
    public static Task<T?> Apply<T>(PlayerChoiceContext choiceContext, Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
        where T : PowerModel
    {
        return MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<T>(choiceContext, target, amount, applier, cardSource, silent);
    }

    /// <summary>
    /// 给多个目标施加能力，使用不会弹出玩家选择的上下文兼容旧调用点。
    /// </summary>
    /// <typeparam name="T">能力类型。</typeparam>
    /// <param name="targets">施加目标列表。</param>
    /// <param name="amount">施加层数。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="silent">是否静默施加。</param>
    /// <returns>成功施加的能力列表。</returns>
    public static Task<IReadOnlyList<T>> Apply<T>(IEnumerable<Creature> targets, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
        where T : PowerModel
    {
        return MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<T>(NewChoiceContext(), targets, amount, applier, cardSource, silent);
    }

    /// <summary>
    /// 给多个目标施加能力，使用调用方提供的玩家选择上下文。
    /// </summary>
    /// <typeparam name="T">能力类型。</typeparam>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="targets">施加目标列表。</param>
    /// <param name="amount">施加层数。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="silent">是否静默施加。</param>
    /// <returns>成功施加的能力列表。</returns>
    public static Task<IReadOnlyList<T>> Apply<T>(PlayerChoiceContext choiceContext, IEnumerable<Creature> targets, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
        where T : PowerModel
    {
        return MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<T>(choiceContext, targets, amount, applier, cardSource, silent);
    }

    /// <summary>
    /// 施加已经创建好的能力实例，使用不会弹出玩家选择的上下文兼容旧调用点。
    /// </summary>
    /// <param name="power">能力实例。</param>
    /// <param name="target">施加目标。</param>
    /// <param name="amount">施加层数。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="silent">是否静默施加。</param>
    /// <returns>异步任务。</returns>
    public static Task Apply(PowerModel power, Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
    {
        return MegaCrit.Sts2.Core.Commands.PowerCmd.Apply(NewChoiceContext(), power, target, amount, applier, cardSource, silent);
    }

    /// <summary>
    /// 施加已经创建好的能力实例，使用调用方提供的玩家选择上下文。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="power">能力实例。</param>
    /// <param name="target">施加目标。</param>
    /// <param name="amount">施加层数。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="silent">是否静默施加。</param>
    /// <returns>异步任务。</returns>
    public static Task Apply(PlayerChoiceContext choiceContext, PowerModel power, Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
    {
        return MegaCrit.Sts2.Core.Commands.PowerCmd.Apply(choiceContext, power, target, amount, applier, cardSource, silent);
    }

    /// <summary>
    /// 修改能力层数，使用不会弹出玩家选择的上下文兼容旧调用点。
    /// </summary>
    /// <param name="power">要修改的能力。</param>
    /// <param name="offset">变化层数。</param>
    /// <param name="applier">变化来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="silent">是否静默修改。</param>
    /// <returns>修改后的层数。</returns>
    public static Task<int> ModifyAmount(PowerModel power, decimal offset, Creature? applier, CardModel? cardSource, bool silent = false)
    {
        return MegaCrit.Sts2.Core.Commands.PowerCmd.ModifyAmount(NewChoiceContext(), power, offset, applier, cardSource, silent);
    }

    /// <summary>
    /// 修改能力层数，使用调用方提供的玩家选择上下文。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="power">要修改的能力。</param>
    /// <param name="offset">变化层数。</param>
    /// <param name="applier">变化来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="silent">是否静默修改。</param>
    /// <returns>修改后的层数。</returns>
    public static Task<int> ModifyAmount(PlayerChoiceContext choiceContext, PowerModel power, decimal offset, Creature? applier, CardModel? cardSource, bool silent = false)
    {
        return MegaCrit.Sts2.Core.Commands.PowerCmd.ModifyAmount(choiceContext, power, offset, applier, cardSource, silent);
    }

    /// <summary>
    /// 将指定能力调整到目标层数；新版没有直接 SetAmount 命令，使用差值修改实现。
    /// </summary>
    /// <typeparam name="T">能力类型。</typeparam>
    /// <param name="target">能力拥有者。</param>
    /// <param name="amount">目标层数。</param>
    /// <param name="applier">变化来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="silent">是否静默修改。</param>
    /// <returns>调整后的能力实例。</returns>
    public static async Task<T?> SetAmount<T>(Creature target, int amount, Creature? applier, CardModel? cardSource, bool silent = false)
        where T : PowerModel
    {
        T? power = target.GetPower<T>();
        if (power == null)
        {
            return await Apply<T>(target, amount, applier, cardSource, silent);
        }

        await ModifyAmount(power, amount - power.Amount, applier, cardSource, silent);
        return power;
    }

    /// <summary>
    /// 转调新版原生命令的移除能力方法。
    /// </summary>
    /// <param name="power">要移除的能力。</param>
    /// <returns>异步任务。</returns>
    public static Task Remove(PowerModel? power)
    {
        return MegaCrit.Sts2.Core.Commands.PowerCmd.Remove(power);
    }

    /// <summary>
    /// 转调新版原生命令的按类型移除能力方法。
    /// </summary>
    /// <typeparam name="T">能力类型。</typeparam>
    /// <param name="creature">能力拥有者。</param>
    /// <returns>异步任务。</returns>
    public static Task Remove<T>(Creature creature)
        where T : PowerModel
    {
        return MegaCrit.Sts2.Core.Commands.PowerCmd.Remove<T>(creature);
    }

    /// <summary>
    /// 创建不会处理玩家选择的上下文，用于兼容旧版无上下文命令调用。
    /// </summary>
    /// <returns>玩家选择上下文。</returns>
    private static PlayerChoiceContext NewChoiceContext()
    {
        return new ThrowingPlayerChoiceContext();
    }
}
