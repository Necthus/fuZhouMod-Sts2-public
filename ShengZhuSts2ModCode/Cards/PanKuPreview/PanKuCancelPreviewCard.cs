using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.PanKuPreview;

/// <summary>
/// 潘库宝盒返回预览卡：仅用于休息处附魔选择界面，表示返回休息处且不消耗本次机会。
/// </summary>
public class PanKuCancelPreviewCard : PanKuPreviewCard
{
    /// <summary>
    /// 构造潘库返回预览卡。
    /// </summary>
    public PanKuCancelPreviewCard()
        : base(CardType.Skill)
    {
    }

    /// <summary>
    /// 预览卡不可被打出。
    /// </summary>
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        return Task.CompletedTask;
    }
}
