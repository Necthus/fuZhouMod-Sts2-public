using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 黑影兵团辅助工具类：提供兵团牌相关的全局计数和状态管理。
/// </summary>
public static class ShadowKhanHelper
{
    /// <summary>
    /// 最近一次已经重置过的战斗状态，避免同一场战斗首回合重复清空。
    /// </summary>
    private static ICombatState? _lastResetCombatState;

    /// <summary>
    /// 明塔-噬影团历史消耗攻击牌伤害合计（本场战斗）。
    /// </summary>
    private static int _mingTaTotalDamage;

    /// <summary>
    /// 本回合已打出的黑影兵团牌数量。
    /// 这里记录的是“当前卡之前已经打出的数量”。
    /// </summary>
    private static int _shadowKhanPlayedThisTurn;

    /// <summary>
    /// 当前计数对应的回合号。
    /// </summary>
    private static int _shadowKhanPlayedTurnNumber = -1;

    /// <summary>
    /// 获取本回合当前卡之前已打出的黑影兵团牌数量。
    /// </summary>
    /// <param name="currentCard">当前正在打出的牌。</param>
    /// <returns>本回合已打出的其他兵团牌数量。</returns>
    public static int GetShadowKhanPlayedThisTurn(CardModel currentCard)
    {
        EnsureShadowKhanTurnState(currentCard);
        return _shadowKhanPlayedThisTurn;
    }

    /// <summary>
    /// 记录1张黑影兵团牌已经完成打出。
    /// </summary>
    public static void RecordShadowKhanPlayed(CardModel currentCard)
    {
        EnsureShadowKhanTurnState(currentCard);
        _shadowKhanPlayedThisTurn++;
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【黑影兵团计数】记录打出：卡牌={currentCard.Id.Entry}，回合={_shadowKhanPlayedTurnNumber}，本回合累计={_shadowKhanPlayedThisTurn}");
    }

    /// <summary>
    /// 累加明塔噬影团消耗的攻击牌伤害。
    /// </summary>
    /// <param name="damage">消耗的攻击牌伤害值。</param>
    public static void AddMingTaDamage(int damage)
    {
        _mingTaTotalDamage += Math.Max(0, damage);
    }

    /// <summary>
    /// 获取明塔噬影团历史消耗攻击牌伤害合计。
    /// </summary>
    /// <returns>累计伤害值。</returns>
    public static int GetMingTaTotalDamage()
    {
        return _mingTaTotalDamage;
    }

    /// <summary>
    /// 战斗结束时重置所有兵团相关状态（包括面具管理器）。
    /// </summary>
    public static void ResetCombatState()
    {
        _lastResetCombatState = null;
        ResetCombatStateCore();
    }

    /// <summary>
    /// 按战斗对象重置兵团状态；同一场战斗只会真正清理一次。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    public static void ResetCombatStateForCombat(ICombatState? combatState)
    {
        if (combatState == null)
        {
            ResetCombatState();
            return;
        }

        if (ReferenceEquals(_lastResetCombatState, combatState))
        {
            return;
        }

        _lastResetCombatState = combatState;
        ResetCombatStateCore();
    }

    /// <summary>
    /// 执行真正的战斗状态清理。
    /// </summary>
    private static void ResetCombatStateCore()
    {
        _mingTaTotalDamage = 0;
        _shadowKhanPlayedThisTurn = 0;
        _shadowKhanPlayedTurnNumber = -1;
        MaskManager.Clear();
    }

    /// <summary>
    /// 确保黑影兵团本回合计数和当前回合同步。
    /// </summary>
    private static void EnsureShadowKhanTurnState(CardModel currentCard)
    {
        int currentTurnNumber = GetCurrentTurnNumber(currentCard);
        if (currentTurnNumber == _shadowKhanPlayedTurnNumber)
        {
            return;
        }

        _shadowKhanPlayedTurnNumber = currentTurnNumber;
        _shadowKhanPlayedThisTurn = 0;
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【黑影兵团计数】检测到新回合，重置本回合计数：回合={currentTurnNumber}");
    }

    /// <summary>
    /// 读取当前战斗回合数。
    /// </summary>
    private static int GetCurrentTurnNumber(CardModel currentCard)
    {
        var combatState = currentCard.Owner?.Creature?.CombatState;
        if (combatState == null)
        {
            return 0;
        }

        if (combatState.RoundNumber > 0)
        {
            return combatState.RoundNumber;
        }

        try
        {
            var type = combatState.GetType();
            var property = type.GetProperty("TurnNumber")
                           ?? type.GetProperty("TurnCount")
                           ?? type.GetProperty("Turn");
            if (property?.GetValue(combatState) is int intValue)
            {
                return intValue;
            }
        }
        catch
        {
            // 忽略反射失败，兜底返回0。
        }

        return 0;
    }
}
