namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

/// <summary>
/// 遗物联机交互类型。
/// 这里只描述“玩家做了哪种遗物操作”，具体效果仍由原遗物逻辑执行，避免改变游戏表现。
/// </summary>
internal enum RelicInteractionType
{
    /// <summary>
    /// 未知交互。
    /// </summary>
    None = 0,

    /// <summary>
    /// 设置某个符咒的激发状态。
    /// </summary>
    SetActivation = 1,

    /// <summary>
    /// 鼠符咒变化手牌。
    /// </summary>
    RatTransform = 2,

    /// <summary>
    /// 猴符咒战斗内七十二变。
    /// </summary>
    MonkeyCombatTransform = 3,

    /// <summary>
    /// 兔符咒刷新目标符咒冷却。
    /// </summary>
    RabbitRefreshTarget = 4,

    /// <summary>
    /// 符咒探测仪切换半自动模式。
    /// </summary>
    TalismanLocatorMode = 5,

    /// <summary>
    /// 黑影令牌切换烧牌挡位。
    /// </summary>
    ShadowKhanTokenMode = 6,

    /// <summary>
    /// 马符咒移除负面效果。
    /// </summary>
    HorseRemoveDebuff = 7,

    /// <summary>
    /// 猴符咒休息处七十二变。
    /// </summary>
    MonkeyRestTransform = 8,

    /// <summary>
    /// 潘库宝盒休息处附魔。
    /// </summary>
    PanKuRestEnchant = 9,

    /// <summary>
    /// 潘库宝盒奖励领取。
    /// </summary>
    PanKuRewardClaim = 10,

    /// <summary>
    /// 岁月史书残卷改写现实。
    /// </summary>
    HistoryBookRewrite = 11,

    /// <summary>
    /// 鼠符咒切换圣主人形态/石像形态。
    /// </summary>
    RatFormSwitch = 12
}
