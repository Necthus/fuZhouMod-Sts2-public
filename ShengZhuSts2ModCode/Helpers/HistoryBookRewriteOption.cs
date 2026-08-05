namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 岁月史书残卷支持的改写现实类型。
/// </summary>
internal enum HistoryBookRewriteOption
{
    /// <summary>
    /// 未知选项。
    /// </summary>
    None = 0,

    /// <summary>
    /// 将所有存活怪物当前生命降到 1。
    /// </summary>
    MonsterHpToOne = 1,

    /// <summary>
    /// 眩晕所有存活怪物。
    /// </summary>
    StunAllMonsters = 2,

    /// <summary>
    /// 抽满手牌并获得 12 点能量。
    /// </summary>
    DrawFullAndGainEnergy = 3,

    /// <summary>
    /// 重置战斗牌堆到战斗开局快照。
    /// </summary>
    ResetCombatPiles = 4,

    /// <summary>
    /// 消耗手牌、抽牌堆、弃牌堆中的状态和诅咒。
    /// </summary>
    ExhaustStatusAndCurse = 5,

    /// <summary>
    /// 本次休息处所有选项都可以选择。
    /// </summary>
    CampfireMultiSelect = 6,

    /// <summary>
    /// 将休息处改写成商店并开启零元购。
    /// </summary>
    RestToFreeShop = 7,

    /// <summary>
    /// 本次商店零元购。
    /// </summary>
    ShopFree = 8,

    /// <summary>
    /// 将商店改写成休息处并开启多选。
    /// </summary>
    ShopToCampfire = 9,

    /// <summary>
    /// 本次商店卖后补货。
    /// </summary>
    ShopRestock = 10
}
