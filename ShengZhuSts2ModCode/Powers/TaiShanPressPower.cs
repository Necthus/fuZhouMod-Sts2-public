using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 泰山压顶能力：回合结束时，对一名随机敌人造成本回合你造成总伤害30%的伤害。
/// </summary>
public class TaiShanPressPower : TurnDamageEchoPower
{
    /// <summary>
    /// 回合结束时，对一名随机存活敌人造成预计伤害。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="opponents">当前存活敌人列表。</param>
    /// <param name="bonusDamage">本次预计结算伤害。</param>
    /// <returns>异步任务。</returns>
    protected override async Task DealEndTurnDamage(PlayerChoiceContext choiceContext, IReadOnlyList<Creature> opponents, int bonusDamage)
    {
        Creature randomTarget = opponents[StableRandomHelper.NextInt(Owner?.Player, opponents.Count)];
        await CreatureCmd.Damage(choiceContext, randomTarget, bonusDamage, ValueProp.Unpowered, Owner, null);
    }
}
