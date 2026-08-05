using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>
/// 虎符咒失衡服务：负责战斗开始时按玩家分别判定、稳定随机并施加失衡惩罚。
/// </summary>
public static class TalismanImbalanceService
{
    /// <summary>
    /// 轻度失衡的最低符咒数量。
    /// </summary>
    private const int LightTierMinTalismanCount = 6;

    /// <summary>
    /// 轻度失衡的最高符咒数量。
    /// </summary>
    private const int LightTierMaxTalismanCount = 8;

    /// <summary>
    /// 重度失衡的最低符咒数量。
    /// </summary>
    private const int HeavyTierMinTalismanCount = 9;

    /// <summary>
    /// 重度失衡的最高符咒数量。
    /// </summary>
    private const int HeavyTierMaxTalismanCount = 11;

    /// <summary>
    /// 固定减益池，顺序不得随意变动，否则会影响联机稳定随机结果。
    /// </summary>
    private static readonly TalismanImbalanceDebuffKind[] DebuffPool =
    [
        TalismanImbalanceDebuffKind.Vulnerable,
        TalismanImbalanceDebuffKind.Frail,
        TalismanImbalanceDebuffKind.Debilitate,
        TalismanImbalanceDebuffKind.Tender,
        //TalismanImbalanceDebuffKind.Sloth,
        TalismanImbalanceDebuffKind.Constrict,
        TalismanImbalanceDebuffKind.Tangled,
        TalismanImbalanceDebuffKind.Shrink,
        TalismanImbalanceDebuffKind.Disintegration
    ];

    /// <summary>
    /// 玩家方第一回合开始前，为每名满足条件的玩家施加失衡机制。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <returns>异步任务。</returns>
    public static async Task ApplyForCombatStart(ICombatState? combatState)
    {
        if (combatState == null)
        {
            return;
        }

        foreach (Player player in combatState.Players)
        {
            await ApplyForPlayer(player);
        }
    }

    /// <summary>
    /// 对单个玩家执行失衡判定和施加。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>异步任务。</returns>
    private static async Task ApplyForPlayer(Player? player)
    {
        if (player?.Creature == null || player.Creature.IsDead || player.Creature.GetPower<TalismanImbalancePlayerPower>() != null)
        {
            return;
        }

        int talismanCount = TalismanHelper.CountOwnedAnimalTalismans(player);
        if (!ShouldApply(player, talismanCount))
        {
            return;
        }

        int tier = GetTier(talismanCount);
        string talismanStableKey = TalismanHelper.BuildOwnedTalismanStableKey(player);
        TalismanImbalanceDebuffKind debuffKind = RollDebuffKind(player, talismanCount, tier, talismanStableKey);
        int debuffAmount = RollDebuffAmount(player, talismanCount, tier, talismanStableKey, debuffKind);
        TalismanImbalanceMode mode = RollMode(player, talismanCount, tier, talismanStableKey);
        int percent = CalculatePercent(talismanCount);

        TalismanImbalancePlayerPower? imbalancePower;
        using (TalismanImbalancePlayerPower.UseModeForNextApply(mode))
        {
            imbalancePower = await PowerCmd.Apply<TalismanImbalancePlayerPower>(player.Creature, percent, player.Creature, null);
        }

        if (imbalancePower == null)
        {
            MainFile.Logger.Info($"【虎符咒失衡】施加失衡状态失败：玩家={FormatPlayer(player)}，符咒数={talismanCount}。");
            return;
        }

        imbalancePower.Configure(talismanCount, tier, debuffKind, debuffAmount, mode, percent);
        await ApplyDebuff(player.Creature, debuffKind, debuffAmount);

        MainFile.Logger.Info($"【虎符咒失衡】已施加：玩家={FormatPlayer(player)}，符咒数={talismanCount}，档位={tier}，模式={TalismanImbalancePlayerPower.GetModeDisplayName(mode)}，百分比={percent}%，减益={TalismanImbalancePlayerPower.GetDebuffDisplayName(debuffKind)}{debuffAmount}层，符咒Key={talismanStableKey}。");
    }

    /// <summary>
    /// 判断玩家是否应触发虎符咒失衡。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="talismanCount">动物符咒数量。</param>
    /// <returns>满足触发条件时返回 true。</returns>
    private static bool ShouldApply(Player player, int talismanCount)
    {
        return talismanCount > 5
            && talismanCount <= HeavyTierMaxTalismanCount
            && !TalismanHelper.HasTigerTalisman(player);
    }

    /// <summary>
    /// 根据符咒数量换算失衡档位。
    /// </summary>
    /// <param name="talismanCount">动物符咒数量。</param>
    /// <returns>1=轻度，2=重度。</returns>
    private static int GetTier(int talismanCount)
    {
        if (talismanCount >= HeavyTierMinTalismanCount && talismanCount <= HeavyTierMaxTalismanCount)
        {
            return 2;
        }

        return talismanCount >= LightTierMinTalismanCount && talismanCount <= LightTierMaxTalismanCount ? 1 : 0;
    }

    /// <summary>
    /// 计算阴阳失衡的增强/削弱百分比。
    /// </summary>
    /// <param name="talismanCount">动物符咒数量。</param>
    /// <returns>百分比数值。</returns>
    private static int CalculatePercent(int talismanCount)
    {
        return 5 + Math.Max(0, talismanCount - 5) * 3;
    }

    /// <summary>
    /// 稳定随机减益种类，不推进游戏随机源。
    /// </summary>
    private static TalismanImbalanceDebuffKind RollDebuffKind(Player player, int talismanCount, int tier, string talismanStableKey)
    {
        int index = StableRandomHelper.StableIndex(
            player,
            DebuffPool.Length,
            "TalismanImbalance.DebuffKind",
            talismanCount.ToString(),
            tier.ToString(),
            talismanStableKey);
        return DebuffPool[index];
    }

    /// <summary>
    /// 稳定随机减益层数，不推进游戏随机源。
    /// </summary>
    private static int RollDebuffAmount(Player player, int talismanCount, int tier, string talismanStableKey, TalismanImbalanceDebuffKind debuffKind)
    {
        int min = tier >= 2 ? 3 : 1;
        int max = tier >= 2 ? 4 : 2;
        int range = max - min + 1;
        int offset = StableRandomHelper.StableIndex(
            player,
            range,
            "TalismanImbalance.DebuffAmount",
            talismanCount.ToString(),
            tier.ToString(),
            talismanStableKey,
            debuffKind.ToString());
        return min + offset;
    }

    /// <summary>
    /// 稳定随机阴阳模式，不推进游戏随机源。
    /// </summary>
    private static TalismanImbalanceMode RollMode(Player player, int talismanCount, int tier, string talismanStableKey)
    {
        int index = StableRandomHelper.StableIndex(
            player,
            2,
            "TalismanImbalance.Mode",
            talismanCount.ToString(),
            tier.ToString(),
            talismanStableKey);
        return index == 0 ? TalismanImbalanceMode.Yang : TalismanImbalanceMode.Yin;
    }

    /// <summary>
    /// 按随机结果给玩家施加对应减益。
    /// </summary>
    private static Task ApplyDebuff(Creature owner, TalismanImbalanceDebuffKind debuffKind, int amount)
    {
        return debuffKind switch
        {
            TalismanImbalanceDebuffKind.Vulnerable => ApplyPower<VulnerablePower>(owner, amount),
            TalismanImbalanceDebuffKind.Frail => ApplyPower<FrailPower>(owner, amount),
            TalismanImbalanceDebuffKind.Debilitate => ApplyPower<DebilitatePower>(owner, amount),
            TalismanImbalanceDebuffKind.WasteAway => ApplyPower<WasteAwayPower>(owner, amount),
            TalismanImbalanceDebuffKind.Tender => ApplyPower<TenderPower>(owner, amount),
            TalismanImbalanceDebuffKind.Constrict => ApplyPower<ConstrictPower>(owner, amount),
            TalismanImbalanceDebuffKind.Tangled => ApplyPower<TangledPower>(owner, amount),
            TalismanImbalanceDebuffKind.Shrink => ApplyPower<ShrinkPower>(owner, amount),
            TalismanImbalanceDebuffKind.Disintegration => ApplyPower<DisintegrationPower>(owner, amount),
            _ => Task.CompletedTask
        };
    }

    /// <summary>
    /// 施加具体 Power，来源记为玩家自身，方便日志和历史记录归因。
    /// </summary>
    private static Task ApplyPower<TPower>(Creature owner, int amount)
        where TPower : PowerModel
    {
        return PowerCmd.Apply<TPower>(owner, amount, owner, null);
    }

    /// <summary>
    /// 格式化玩家信息，方便联机日志排查。
    /// </summary>
    private static string FormatPlayer(Player player)
    {
        return $"{player.NetId}/{player.Character.Id.Entry}";
    }
}
