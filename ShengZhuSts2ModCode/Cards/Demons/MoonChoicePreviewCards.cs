using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Demons;

/// <summary>
/// 月之恶魔选项预览卡基类：只用于选择界面展示，不能进入任何可收藏卡池。
/// </summary>
public abstract class MoonChoicePreviewCard : ShengZhuSts2ModCard
{
    /// <summary>
    /// 选项预览卡不允许被战斗随机生成。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 选项预览卡不允许被修饰器或随机生成器选中。
    /// </summary>
    public override bool CanBeGeneratedByModifiers => false;

    /// <summary>
    /// 构造隐藏的月之恶魔选项预览卡。
    /// </summary>
    protected MoonChoicePreviewCard()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.None, false)
    {
    }

    /// <summary>
    /// 预览卡不会被真正打出，保留空实现避免误触发效果。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <param name="cardPlay">出牌信息。</param>
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// 月之恶魔选项：反转自己的正数力量和敏捷。
/// </summary>
public class MoonChoiceSelfPreviewCard : MoonChoicePreviewCard
{
}

/// <summary>
/// 月之恶魔选项：获得一张用于反转敌人的临时代币。
/// </summary>
public class MoonChoiceEnemyPreviewCard : MoonChoicePreviewCard
{
}

/// <summary>
/// 月之恶魔选项：获得队伍共享的恶魔异常解除限制。
/// </summary>
public class MoonChoiceUncapPreviewCard : MoonChoicePreviewCard
{
}

/// <summary>
/// 月之恶魔选项：联机时获得一张用于反转队友的临时代币。
/// </summary>
public class MoonChoiceAllyPreviewCard : MoonChoicePreviewCard
{
}
