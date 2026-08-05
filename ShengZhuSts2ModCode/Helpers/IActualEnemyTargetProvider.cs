using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 实际敌方目标提供者：用于处理卡牌目标类型和真实结算范围不一致的情况。
/// </summary>
public interface IActualEnemyTargetProvider
{
    /// <summary>
    /// 获取本次出牌实际会影响到的敌方目标。
    /// </summary>
    /// <param name="cardPlay">本次出牌信息。</param>
    /// <returns>实际受影响的敌方目标列表。</returns>
    IReadOnlyList<Creature> GetActualEnemyTargets(CardPlay? cardPlay);
}
