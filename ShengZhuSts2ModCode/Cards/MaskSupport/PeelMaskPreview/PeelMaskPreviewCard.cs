using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Extensions;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport.PeelMaskPreview;

/// <summary>
/// 剥离面具预览卡基类：仅用于剥离面具选择界面展示，显示剥离后返还的面具牌数和兵团牌数。
/// 每个面具对应一个子类，拥有独立的国际化 key（标题=面具名称，描述=返还信息）。
/// 图片复用对应面具牌的卡图。
/// 不加入任何卡池，不可被打出。
/// </summary>
public abstract class PeelMaskPreviewCard : ShengZhuSts2ModCard
{
    /// <summary>
    /// 剥离面具预览卡只允许选择界面临时创建，不能被能力药水等随机生成。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 对应面具牌的图片文件名（不含路径，如 "yikamask.png"）。
    /// </summary>
    protected abstract string MaskImageFileName { get; }

    /// <summary>
    /// 构造剥离面具预览卡（无参，默认返还数为0，创建后通过 DynamicVars 设置实际值）。
    /// </summary>
    protected PeelMaskPreviewCard()
        : base(0, CardType.Power, CardRarity.Token, TargetType.None)
    {
        WithVar("MaskReturn", 0);
        WithVar("SoldierReturn", 0);
    }

    /// <summary>
    /// 使用对应面具牌的小图。
    /// </summary>
    public override string PortraitPath => MaskImageFileName.CardImagePath();

    /// <summary>
    /// 使用对应面具牌的大图。
    /// </summary>
    public override string CustomPortraitPath => MaskImageFileName.BigCardImagePath();

    /// <summary>
    /// 预览卡不可被打出。
    /// </summary>
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        return Task.CompletedTask;
    }
}
