using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 负面效果预览卡：仅用于马符咒选择界面展示。
/// 创建后通过反射修改标题为对应debuff的国际化名称，使玩家能区分不同的负面效果。
/// 不加入任何卡池，不可被打出。
/// </summary>
public class DebuffPreviewCard : ShengZhuSts2ModCard
{
    /// <summary>
    /// 负面效果预览卡只允许选择界面临时创建，不能被能力药水等随机生成。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 构造负面效果预览卡。
    /// </summary>
    public DebuffPreviewCard()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.None)
    {
        WithVar("DebuffAmount", 0);
    }

    /// <summary>
    /// 预览卡不可被打出。
    /// </summary>
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        return Task.CompletedTask;
    }
}
