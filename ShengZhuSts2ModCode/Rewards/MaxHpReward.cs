using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rewards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Rewards;

/// <summary>
/// 最大生命奖励：用于十二符咒罗盘在缺少符咒候选时补足奖励选择。
/// </summary>
public class MaxHpReward : Reward
{
    /// <summary>
    /// 本次奖励增加的最大生命值。
    /// </summary>
    public int Amount { get; }

    /// <summary>
    /// 自定义奖励不参与原版奖励类型序列化。
    /// </summary>
    protected override RewardType RewardType => RewardType.None;

    /// <summary>
    /// 放在遗物奖励同组，保证互斥奖励按钮按遗物奖励样式排列。
    /// </summary>
    public override int RewardsSetIndex => 3;

    /// <summary>
    /// 奖励按钮显示文本。
    /// </summary>
    public override LocString Description
    {
        get
        {
            LocString description = new("relics", "SHENGZHUSTS2MOD-MAX_HP_REWARD.description");
            description.Add("Amount", Amount);
            return description;
        }
    }

    /// <summary>
    /// 最大生命奖励创建时就已经有固定数值。
    /// </summary>
    public override bool IsPopulated => true;

    /// <summary>
    /// 创建最大生命奖励。
    /// </summary>
    /// <param name="amount">增加的最大生命值。</param>
    /// <param name="player">领取奖励的玩家。</param>
    public MaxHpReward(int amount, Player player) : base(player)
    {
        Amount = Math.Max(0, amount);
    }

    /// <summary>
    /// 固定奖励不需要额外填充。
    /// </summary>
    public override void Populate()
    {
    }

    /// <summary>
    /// 创建奖励图标，使用文字图标避免依赖额外本体资源路径。
    /// </summary>
    /// <returns>奖励图标控件。</returns>
    public override Control CreateIcon()
    {
        Label label = new()
        {
            Text = $"+{Amount}",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ThemeTypeVariation = "HeaderSmall"
        };
        label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        return label;
    }

    /// <summary>
    /// 领取奖励时增加玩家最大生命值。
    /// </summary>
    /// <returns>成功领取时返回 true。</returns>
    protected override async Task<bool> OnSelect()
    {
        if (Amount <= 0)
        {
            MainFile.Logger.Info("【十二符咒罗盘】最大生命奖励数值无效，跳过领取。");
            return false;
        }

        await CreatureCmd.GainMaxHp(Player.Creature, Amount);
        MainFile.Logger.Info($"【十二符咒罗盘】领取最大生命奖励：+{Amount}");
        return true;
    }

    /// <summary>
    /// 最大生命奖励没有图鉴解锁状态需要标记。
    /// </summary>
    public override void MarkContentAsSeen()
    {
    }
}
