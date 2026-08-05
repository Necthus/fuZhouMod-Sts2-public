using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 腐化附魔出牌代价去重补丁：同一次 CardPlay 内只允许腐化扣血一次。
/// </summary>
[HarmonyPatch(typeof(Corrupted), nameof(Corrupted.OnPlay))]
public static class CorruptedEnchantmentPlayOncePatch
{
    /// <summary>
    /// 记录已经结算过腐化代价的出牌对象，CardPlay 生命周期结束后会自动释放。
    /// </summary>
    private static readonly ConditionalWeakTable<CardPlay, ResolvedMarker> ResolvedCardPlays = new();

    /// <summary>
    /// Harmony 前置补丁：如果同一个 CardPlay 已经结算过腐化，就跳过重复扣血。
    /// </summary>
    public static bool Prefix(CardPlay? cardPlay, ref Task __result)
    {
        if (cardPlay == null)
        {
            return true;
        }

        if (ResolvedCardPlays.TryGetValue(cardPlay, out _))
        {
            MainFile.Logger.Info($"【腐化附魔】跳过重复扣血：卡牌={cardPlay.Card.Id.Entry}，序号={cardPlay.PlayIndex + 1}/{cardPlay.PlayCount}。");
            __result = Task.CompletedTask;
            return false;
        }

        ResolvedCardPlays.Add(cardPlay, new ResolvedMarker());
        return true;
    }

    /// <summary>
    /// ConditionalWeakTable 使用的空标记对象。
    /// </summary>
    private sealed class ResolvedMarker
    {
    }
}
