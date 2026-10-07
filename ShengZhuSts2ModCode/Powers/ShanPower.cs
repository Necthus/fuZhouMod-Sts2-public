using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 山能力：统计本回合真实未格挡伤害，回合结束时对所有敌人造成 30% 追加伤害。
/// </summary>
public class ShanPower : TurnDamageEchoPower
{
    /// <summary>
    /// 回合结束时，对所有存活敌人造成预计伤害。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="opponents">当前存活敌人列表。</param>
    /// <param name="bonusDamage">本次预计结算伤害。</param>
    /// <returns>异步任务。</returns>
    protected override async Task DealEndTurnDamage(PlayerChoiceContext choiceContext, IReadOnlyList<Creature> opponents, int bonusDamage)
    {
        foreach (Creature opponent in opponents)
        {
            if (opponent.IsAlive && !opponent.IsDead)
            {
                await CreatureCmd.Damage(choiceContext, opponent, bonusDamage, ValueProp.Unpowered, Owner, null, null);
            }
        }
    }
}
