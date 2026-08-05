using MegaCrit.Sts2.Core.Entities.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 生命之杯：获得额外药水栏，并让玩家成功使用药水后重放药水效果和回复生命。
/// </summary>
public class LifeChalice : ShengZhuSts2ModRelic
{
    /// <summary>
    /// 获得生命之杯时增加的药水栏数量。
    /// </summary>
    public const int PotionSlotGain = 3;

    /// <summary>
    /// 每次成功使用药水后回复的最大生命百分比。
    /// </summary>
    public const int HealPercent = 10;

    /// <summary>
    /// 生命之杯是事件遗物。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Event;

    /// <summary>
    /// 生命之杯只能通过事件获得，不允许在商店自然售卖。
    /// </summary>
    public override bool IsAllowedInShops => false;

    /// <summary>
    /// 获得生命之杯后增加三个药水栏位。
    /// </summary>
    public override Task AfterObtained()
    {
        if (Owner == null)
        {
            MainFile.Logger.Info("【生命之杯】获得后增加药水栏失败：持有者为空。");
            return Task.CompletedTask;
        }

        int oldPotionCount = Owner.MaxPotionCount;
        Owner.AddToMaxPotionCount(PotionSlotGain);
        MainFile.Logger.Info($"【生命之杯】获得后增加药水栏：玩家={Owner.NetId}，原栏位={oldPotionCount}，增加={PotionSlotGain}，现栏位={Owner.MaxPotionCount}。");
        return Task.CompletedTask;
    }
}
