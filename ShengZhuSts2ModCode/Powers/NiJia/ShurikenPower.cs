using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

/// <summary>
/// 手里剑能力：降低尼嘉-忍者团基础伤害，并让其攻击多段。
/// </summary>
public class ShurikenPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 默认多段次数。
    /// </summary>
    private const int DefaultHitCount = 2;

    /// <summary>
    /// 当前尼嘉-忍者团攻击次数；直接使用能力层数，方便联机与读档同步。
    /// </summary>
    public int HitCount => Math.Max(DefaultHitCount, Amount);

    /// <summary>
    /// 手里剑属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器显示当前攻击次数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 施加或更新手里剑能力，多张手里剑取攻击次数最大值。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">获得能力的目标。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="hitCount">新的攻击次数。</param>
    /// <returns>最终的手里剑能力实例。</returns>
    public static async Task<ShurikenPower?> Apply(PlayerChoiceContext choiceContext, Creature target, Creature? applier, CardModel cardSource, int hitCount)
    {
        int targetHitCount = Math.Max(DefaultHitCount, hitCount);
        ShurikenPower? existing = target.GetPower<ShurikenPower>();
        if (existing == null)
        {
            return await PowerCmd.Apply<ShurikenPower>(choiceContext, target, targetHitCount, applier, cardSource);
        }

        if (targetHitCount > existing.Amount)
        {
            await PowerCmd.ModifyAmount(choiceContext, existing, targetHitCount - existing.Amount, applier, cardSource);
        }

        return existing;
    }
}
