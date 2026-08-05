using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 马符咒退出预览卡：仅用于马符咒选择界面，表示本次不移除负面效果。
/// </summary>
public class HorseTalismanExitPreviewCard : ShengZhuSts2ModCard
{
    /// <summary>
    /// 退出预览卡只允许选择界面临时创建，不能被能力药水等随机生成。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 构造马符咒退出预览卡。
    /// </summary>
    public HorseTalismanExitPreviewCard()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.None)
    {
    }

    /// <summary>
    /// 退出预览卡不可被打出。
    /// </summary>
    /// <param name="choiceContext">玩家选择上下文。</param>
    /// <param name="cardPlay">出牌信息。</param>
    /// <returns>异步任务。</returns>
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        return Task.CompletedTask;
    }
}
