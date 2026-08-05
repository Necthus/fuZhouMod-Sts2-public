using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.PanKuPreview;

/// <summary>
/// 潘库宝盒附魔预览卡：仅用于潘库奖励选择界面，表示立刻将本次魔气附魔到一张牌上。
/// </summary>
public class PanKuEnchantPreviewCard : PanKuPreviewCard
{
    /// <summary>
    /// 当前奖励或休息处选择对应的魔气类型。
    /// </summary>
    public PanKuDemonQiKind? Kind { get; set; }

    /// <summary>
    /// 使用当前魔气对应的图标作为预览卡小图，避免显示默认占位图。
    /// </summary>
    public override string PortraitPath => PanKuDemonQiHelper.GetIconPath(Kind ?? PanKuDemonQiKind.Heaven);

    /// <summary>
    /// 使用当前魔气对应的图标作为预览卡大图，保持选择界面展示一致。
    /// </summary>
    public override string CustomPortraitPath => PanKuDemonQiHelper.GetIconPath(Kind ?? PanKuDemonQiKind.Heaven);

    /// <summary>
    /// 使用当前魔气对应的图标作为 Beta 图，避免特殊图片模式下显示默认占位图。
    /// </summary>
    public override string BetaPortraitPath => PanKuDemonQiHelper.GetIconPath(Kind ?? PanKuDemonQiKind.Heaven);

    /// <summary>
    /// 构造潘库附魔预览卡。
    /// </summary>
    public PanKuEnchantPreviewCard()
        : base(CardType.Skill)
    {
    }

    /// <summary>
    /// 给预览卡描述补充当前魔气的效果和可附魔类型。
    /// </summary>
    /// <param name="description">卡牌描述本地化对象。</param>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        PanKuDemonQiKind kind = Kind ?? PanKuDemonQiKind.Heaven;
        description.Add("Qi", PanKuDemonQiHelper.GetDisplayName(kind));
        description.Add("Effect", PanKuDemonQiHelper.GetEnchantmentEffectText(kind));
        description.Add("EnchantTargetType", PanKuDemonQiHelper.GetEnchantTargetTypeText(kind));
    }

    /// <summary>
    /// 预览卡不可被打出。
    /// </summary>
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        return Task.CompletedTask;
    }
}
