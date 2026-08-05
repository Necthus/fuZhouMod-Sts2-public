using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>
/// 生命之杯治疗服务：处理按最大生命百分比回复，并把溢出治疗转换为最大生命值。
/// </summary>
public static class LifeChaliceHealingService
{
    /// <summary>
    /// 计算一次治疗在当前生命上限下会产生多少溢出治疗。
    /// </summary>
    /// <param name="creature">接受治疗的生物。</param>
    /// <param name="healAmount">本次治疗量。</param>
    /// <returns>会被生命上限吃掉的溢出治疗量。</returns>
    public static decimal CalculateOverflowHeal(Creature? creature, decimal healAmount)
    {
        if (creature == null || healAmount <= 0)
        {
            return 0;
        }

        decimal missingHp = Math.Max(0, creature.MaxHp - creature.CurrentHp);
        return Math.Max(0, healAmount - missingHp);
    }

    /// <summary>
    /// 将治疗溢出量转换为最大生命值，并校正增加最大生命时自带的当前生命回复。
    /// </summary>
    /// <param name="player">生命之杯持有者。</param>
    /// <param name="overflowHeal">需要转换为最大生命值的溢出治疗量。</param>
    /// <param name="source">日志来源。</param>
    public static async Task ConvertOverflowHealToMaxHp(Player? player, decimal overflowHeal, string source)
    {
        if (player?.Creature == null)
        {
            MainFile.Logger.Info($"【生命之杯】溢出转换跳过：来源={source}，玩家或生物为空。");
            return;
        }

        if (overflowHeal <= 0)
        {
            return;
        }

        if (player.GetRelic<LifeChalice>() == null)
        {
            MainFile.Logger.Info($"【生命之杯】溢出转换跳过：来源={source}，玩家={player.NetId}，玩家已不再持有生命之杯。");
            return;
        }

        if (player.Creature.IsDead)
        {
            MainFile.Logger.Info($"【生命之杯】溢出转换跳过：来源={source}，玩家={player.NetId}，玩家已死亡，不触发复活。");
            return;
        }

        int currentHpBeforeMaxGain = player.Creature.CurrentHp;
        int maxHpBefore = player.Creature.MaxHp;
        await CreatureCmd.GainMaxHp(player.Creature, overflowHeal);
        await CreatureCmd.SetCurrentHp(player.Creature, currentHpBeforeMaxGain);
        MainFile.Logger.Info($"【生命之杯】溢出治疗转换为最大生命：来源={source}，玩家={player.NetId}，溢出={overflowHeal}，结果={player.Creature.CurrentHp}/{player.Creature.MaxHp}，原始={currentHpBeforeMaxGain}/{maxHpBefore}。");
    }

    /// <summary>
    /// 按玩家最大生命值百分比回复生命，治疗溢出时转换为最大生命值。
    /// </summary>
    /// <param name="player">需要回复的玩家。</param>
    /// <param name="healPercent">按最大生命计算的回复百分比。</param>
    /// <param name="source">日志来源。</param>
    public static async Task HealWithOverflowToMaxHp(Player? player, int healPercent, string source)
    {
        if (player?.Creature == null)
        {
            MainFile.Logger.Info($"【生命之杯】治疗跳过：来源={source}，玩家或生物为空。");
            return;
        }

        if (player.Creature.IsDead)
        {
            MainFile.Logger.Info($"【生命之杯】治疗跳过：来源={source}，玩家={player.NetId}，玩家已死亡，不触发复活。");
            return;
        }

        int maxHpBefore = player.Creature.MaxHp;
        int currentHpBefore = player.Creature.CurrentHp;
        decimal healAmount = Math.Floor(maxHpBefore * healPercent / 100m);
        decimal overflowHeal = CalculateOverflowHeal(player.Creature, healAmount);
        decimal actualHeal = Math.Min(healAmount, Math.Max(0, maxHpBefore - currentHpBefore));

        if (actualHeal > 0)
        {
            await CreatureCmd.Heal(player.Creature, actualHeal);
        }

        await ConvertOverflowHealToMaxHp(player, overflowHeal, source);

        MainFile.Logger.Info($"【生命之杯】治疗完成：来源={source}，玩家={player.NetId}，治疗比例={healPercent}%，治疗量={healAmount}，实际回复={actualHeal}，溢出={overflowHeal}，结果={player.Creature.CurrentHp}/{player.Creature.MaxHp}，原始={currentHpBefore}/{maxHpBefore}。");
    }
}
