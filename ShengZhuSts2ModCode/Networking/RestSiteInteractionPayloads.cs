using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

/// <summary>
/// 猴符咒休息处七十二变的同步数据：用牌组下标和卡牌ID定位同一张牌。
/// </summary>
/// <param name="TargetDeckIndex">被变化牌在牌组中的下标。</param>
/// <param name="TargetCardId">被变化牌ID，用于远端校验。</param>
/// <param name="TargetWasUpgraded">被变化牌是否升级，用于远端校验和继承升级。</param>
/// <param name="ReplacementCardId">变化后的新牌ID。</param>
internal readonly record struct MonkeyRestTransformPayload(int TargetDeckIndex, string TargetCardId, bool TargetWasUpgraded, string ReplacementCardId);

/// <summary>
/// 潘库宝盒休息处附魔的同步数据：用牌组下标和卡牌ID定位同一张牌。
/// </summary>
/// <param name="TargetDeckIndex">被附魔牌在牌组中的下标。</param>
/// <param name="TargetCardId">被附魔牌ID，用于远端校验。</param>
/// <param name="TargetWasUpgraded">被附魔牌是否升级，用于远端校验。</param>
/// <param name="Kind">本次消耗并附加的魔气类型。</param>
internal readonly record struct PanKuRestEnchantPayload(int TargetDeckIndex, string TargetCardId, bool TargetWasUpgraded, PanKuDemonQiKind Kind);

/// <summary>
/// 潘库宝盒奖励领取类型。
/// </summary>
internal enum PanKuRewardClaimType
{
    /// <summary>
    /// 未知领取类型。
    /// </summary>
    None = 0,

    /// <summary>
    /// 领取恶魔卡。
    /// </summary>
    DemonCard = 1,

    /// <summary>
    /// 领取寄宿魔气。
    /// </summary>
    StoreQi = 2,

    /// <summary>
    /// 直接给牌组卡牌附魔。
    /// </summary>
    EnchantDeckCard = 3
}

/// <summary>
/// 潘库宝盒奖励领取同步数据：同步最终选择结果，避免奖励页二级选择在联机两端消耗不同 Choice ID。
/// </summary>
/// <param name="ClaimType">领取类型。</param>
/// <param name="Kind">本次奖励对应的魔气类型。</param>
/// <param name="TargetDeckIndex">直接附魔目标牌在牌组中的下标；非附魔时为 -1。</param>
/// <param name="TargetCardId">直接附魔目标牌ID；非附魔时为空。</param>
/// <param name="TargetWasUpgraded">直接附魔目标牌是否升级。</param>
internal readonly record struct PanKuRewardClaimPayload(PanKuRewardClaimType ClaimType, PanKuDemonQiKind Kind, int TargetDeckIndex, string TargetCardId, bool TargetWasUpgraded);
