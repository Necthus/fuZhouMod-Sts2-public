using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Rewards;

/// <summary>
/// 潘库宝盒奖励基类：三种奖励共用魔气类型、图标和奖励排序。
/// </summary>
public abstract class PanKuRewardBase : Reward
{
    /// <summary>
    /// 本次奖励对应的魔气类型。
    /// </summary>
    public PanKuDemonQiKind Kind { get; }

    /// <summary>
    /// 自定义奖励不参与原版奖励类型序列化。
    /// </summary>
    protected override RewardType RewardType => RewardType.None;

    /// <summary>
    /// 放在遗物奖励附近，避免插到金币和药水前面。
    /// </summary>
    public override int RewardsSetIndex => 3;

    /// <summary>
    /// 自定义奖励创建时已经完整。
    /// </summary>
    public override bool IsPopulated => true;

    /// <summary>
    /// 奖励按钮描述。
    /// </summary>
    public override LocString Description
    {
        get
        {
            LocString description = new("relics", DescriptionKey);
            description.Add("Qi", PanKuDemonQiHelper.GetDisplayName(Kind));
            return description;
        }
    }

    /// <summary>
    /// 本地化描述键。
    /// </summary>
    protected abstract string DescriptionKey { get; }

    /// <summary>
    /// 创建潘库奖励。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    /// <param name="player">领取奖励的玩家。</param>
    protected PanKuRewardBase(PanKuDemonQiKind kind, Player player) : base(player)
    {
        Kind = kind;
    }

    /// <summary>
    /// 自定义奖励不需要额外填充。
    /// </summary>
    public override void Populate()
    {
    }

    /// <summary>
    /// 创建奖励图标，使用短名文字避免缺资源。
    /// </summary>
    /// <returns>奖励图标控件。</returns>
    public override Control CreateIcon()
    {
        Label label = new()
        {
            Text = PanKuDemonQiHelper.GetDisplayName(Kind),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ThemeTypeVariation = "HeaderSmall"
        };
        label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return label;
    }

    /// <summary>
    /// 潘库奖励没有图鉴解锁状态需要标记。
    /// </summary>
    public override void MarkContentAsSeen()
    {
    }

    /// <summary>
    /// 获取玩家身上的潘库宝盒。
    /// </summary>
    protected PanKuBox? GetPanKuBox()
    {
        return Player.GetRelic<PanKuBox>();
    }

    /// <summary>
    /// 领取成功后清理待领取状态并刷新宝盒计数。
    /// </summary>
    protected void FinishReward(PanKuBox? box)
    {
        box?.ClearPendingReward();
        box?.Flash();
        EightDemonPossessionHelper.CheckAndTriggerAwakening(Player);
    }

    /// <summary>
    /// 请求同步领取潘库奖励，统一由潘库宝盒按最终选择结果重放。
    /// </summary>
    /// <param name="payload">奖励领取同步数据。</param>
    /// <returns>同步并本地执行成功时返回 true。</returns>
    private protected async Task<bool> RequestSyncedClaim(PanKuRewardClaimPayload payload)
    {
        PanKuBox? box = GetPanKuBox();
        if (box == null)
        {
            MainFile.Logger.Info("【潘库宝盒】奖励领取失败：玩家没有潘库宝盒。");
            return false;
        }

        return await RelicInteractionSyncService.RequestPanKuRewardClaim(box, payload);
    }
}

/// <summary>
/// 潘库奖励入口：点击后打开不消耗 Choice IDs 的原生选牌页。
/// </summary>
public class PanKuChoiceReward : PanKuRewardBase
{
    /// <summary>
    /// 潘库入口奖励描述键。
    /// </summary>
    protected override string DescriptionKey => "SHENGZHUSTS2MOD-PAN_KU_BOX.rewardChoice";

    /// <summary>
    /// 创建潘库入口奖励。
    /// </summary>
    /// <param name="kind">魔气类型。</param>
    /// <param name="player">领取奖励的玩家。</param>
    public PanKuChoiceReward(PanKuDemonQiKind kind, Player player) : base(kind, player)
    {
    }

    /// <summary>
    /// 领取时打开三选一原生选牌页：恶魔卡、立刻附魔、寄宿魔气。
    /// </summary>
    protected override async Task<bool> OnSelect()
    {
        if (!MonkeyTalisman.IsLocalPlayer(Player))
        {
            MainFile.Logger.Info($"【联机同步】【潘库宝盒】跳过非本端玩家的奖励二级选择：玩家={Player.NetId}。");
            return false;
        }

        while (true)
        {
            bool canEnchant = PanKuDemonQiHelper.GetEnchantTargets(Player, Kind).Count > 0;
            PanKuRewardClaimType choice = await PanKuRewardChoiceSelector.SelectRewardType(Player, Kind, canEnchant);
            if (choice == PanKuRewardClaimType.None)
            {
                MainFile.Logger.Info($"【潘库宝盒】玩家取消潘库奖励选择：魔气={PanKuDemonQiHelper.GetShortName(Kind)}。");
                return false;
            }

            if (choice == PanKuRewardClaimType.DemonCard)
            {
                PanKuRewardClaimPayload payload = new(PanKuRewardClaimType.DemonCard, Kind, -1, string.Empty, false);
                return await RequestSyncedClaim(payload);
            }

            if (choice == PanKuRewardClaimType.StoreQi)
            {
                PanKuRewardClaimPayload payload = new(PanKuRewardClaimType.StoreQi, Kind, -1, string.Empty, false);
                return await RequestSyncedClaim(payload);
            }

            if (choice == PanKuRewardClaimType.EnchantDeckCard && await TryEnchantFromReward())
            {
                return true;
            }
        }
    }

    /// <summary>
    /// 从奖励页立刻选择目标牌附魔；没有目标或取消时回到三选一。
    /// </summary>
    /// <returns>成功附魔时返回 true。</returns>
    private async Task<bool> TryEnchantFromReward()
    {
        List<CardModel> targets = PanKuDemonQiHelper.GetEnchantTargets(Player, Kind);
        if (targets.Count == 0)
        {
            MainFile.Logger.Info($"【潘库宝盒】当前没有可附魔目标，返回奖励选择：魔气={PanKuDemonQiHelper.GetShortName(Kind)}。");
            return false;
        }

        CardModel? selected = await PanKuRewardChoiceSelector.SelectEnchantTarget(Player, Kind);
        if (selected == null)
        {
            MainFile.Logger.Info($"【潘库宝盒】玩家取消直接附魔目标选择，返回奖励选择：魔气={PanKuDemonQiHelper.GetShortName(Kind)}。");
            return false;
        }

        int targetDeckIndex = DeckSyncHelper.GetDeckIndex(Player, selected);
        if (targetDeckIndex < 0)
        {
            MainFile.Logger.Info($"【联机异常兜底】【潘库宝盒】直接附魔取消：玩家={Player.NetId}，原因=目标牌不在牌组，目标={selected.Id.Entry}。");
            return false;
        }

        PanKuRewardClaimPayload payload = new(PanKuRewardClaimType.EnchantDeckCard, Kind, targetDeckIndex, selected.Id.Entry, selected.IsUpgraded);
        return await RequestSyncedClaim(payload);
    }
}

/// <summary>
/// 潘库奖励：直接获得一张对应恶魔卡。
/// </summary>
public class PanKuDemonCardReward : PanKuRewardBase
{
    /// <summary>
    /// 恶魔卡奖励描述键。
    /// </summary>
    protected override string DescriptionKey => "SHENGZHUSTS2MOD-PAN_KU_BOX.rewardCard";

    /// <summary>
    /// 创建恶魔卡奖励。
    /// </summary>
    public PanKuDemonCardReward(PanKuDemonQiKind kind, Player player) : base(kind, player)
    {
    }

    /// <summary>
    /// 领取时将恶魔卡加入牌组。
    /// </summary>
    protected override async Task<bool> OnSelect()
    {
        if (!MonkeyTalisman.IsLocalPlayer(Player))
        {
            MainFile.Logger.Info($"【联机同步】【潘库宝盒】跳过非本端玩家的恶魔卡奖励领取：玩家={Player.NetId}。");
            return false;
        }

        PanKuRewardClaimPayload payload = new(PanKuRewardClaimType.DemonCard, Kind, -1, string.Empty, false);
        return await RequestSyncedClaim(payload);
    }
}

/// <summary>
/// 潘库奖励：立刻选择一张牌进行魔气附魔。
/// </summary>
public class PanKuImmediateEnchantReward : PanKuRewardBase
{
    /// <summary>
    /// 直接附魔奖励描述键。
    /// </summary>
    protected override string DescriptionKey => "SHENGZHUSTS2MOD-PAN_KU_BOX.rewardEnchant";

    /// <summary>
    /// 创建直接附魔奖励。
    /// </summary>
    public PanKuImmediateEnchantReward(PanKuDemonQiKind kind, Player player) : base(kind, player)
    {
    }

    /// <summary>
    /// 领取时选择牌组中的可附魔目标；没有目标则自动寄宿，避免奖励卡死。
    /// </summary>
    protected override async Task<bool> OnSelect()
    {
        if (!MonkeyTalisman.IsLocalPlayer(Player))
        {
            MainFile.Logger.Info($"【联机同步】【潘库宝盒】跳过非本端玩家的直接附魔奖励领取：玩家={Player.NetId}。");
            return false;
        }

        List<CardModel> targets = PanKuDemonQiHelper.GetEnchantTargets(Player, Kind);
        if (targets.Count == 0)
        {
            PanKuRewardClaimPayload storePayload = new(PanKuRewardClaimType.StoreQi, Kind, -1, string.Empty, false);
            MainFile.Logger.Info($"【潘库宝盒】直接附魔没有可用目标，自动改为寄宿魔气：{PanKuDemonQiHelper.GetShortName(Kind)}。");
            return await RequestSyncedClaim(storePayload);
        }

        CardModel? selected = await PanKuRewardChoiceSelector.SelectEnchantTarget(Player, Kind);
        if (selected == null)
        {
            return false;
        }

        int targetDeckIndex = DeckSyncHelper.GetDeckIndex(Player, selected);
        if (targetDeckIndex < 0)
        {
            MainFile.Logger.Info($"【联机异常兜底】【潘库宝盒】直接附魔取消：玩家={Player.NetId}，原因=目标牌不在牌组，目标={selected.Id.Entry}。");
            return false;
        }

        PanKuRewardClaimPayload payload = new(PanKuRewardClaimType.EnchantDeckCard, Kind, targetDeckIndex, selected.Id.Entry, selected.IsUpgraded);
        return await RequestSyncedClaim(payload);
    }
}

/// <summary>
/// 潘库奖励：将魔气寄宿在潘库宝盒中，之后可在休息处附魔。
/// </summary>
public class PanKuStoreQiReward : PanKuRewardBase
{
    /// <summary>
    /// 寄宿魔气奖励描述键。
    /// </summary>
    protected override string DescriptionKey => "SHENGZHUSTS2MOD-PAN_KU_BOX.rewardStore";

    /// <summary>
    /// 创建寄宿魔气奖励。
    /// </summary>
    public PanKuStoreQiReward(PanKuDemonQiKind kind, Player player) : base(kind, player)
    {
    }

    /// <summary>
    /// 领取时将魔气存入宝盒。
    /// </summary>
    protected override async Task<bool> OnSelect()
    {
        if (!MonkeyTalisman.IsLocalPlayer(Player))
        {
            MainFile.Logger.Info($"【联机同步】【潘库宝盒】跳过非本端玩家的寄宿魔气奖励领取：玩家={Player.NetId}。");
            return false;
        }

        PanKuRewardClaimPayload payload = new(PanKuRewardClaimType.StoreQi, Kind, -1, string.Empty, false);
        return await RequestSyncedClaim(payload);
    }
}
