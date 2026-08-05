using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Demons;

/// <summary>
/// 月之恶魔反转代币基类：只允许由月之恶魔在战斗中临时加入手牌。
/// </summary>
public abstract class MoonInvertTokenCard : ShengZhuSts2ModCard
{
    /// <summary>
    /// 反转代币不允许被战斗随机生成。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 反转代币不允许被修饰器或随机生成器选中。
    /// </summary>
    public override bool CanBeGeneratedByModifiers => false;

    /// <summary>
    /// 构造隐藏的月之恶魔反转代币。
    /// </summary>
    /// <param name="target">代币可选择的目标类型。</param>
    protected MoonInvertTokenCard(TargetType target)
        : base(0, CardType.Skill, CardRarity.Token, target, false)
    {
        WithKeywords(CardKeyword.Exhaust, CardKeyword.Ethereal);
    }

    /// <summary>
    /// 对合法目标执行月之恶魔卡片专用的力量和敏捷反转。
    /// </summary>
    /// <param name="target">目标生物。</param>
    protected async Task InvertTarget(Creature? target)
    {
        if (Owner?.Creature == null || target == null || !target.IsAlive)
        {
            return;
        }

        await CardMoonDemonQi.InvertNonZeroStrengthAndDexterity(target, Owner.Creature, this);
        MainFile.Logger.Info($"【月之恶魔代币】反转数值：使用者={Owner.Creature.Name}，目标={target.Name}。");
    }
}

/// <summary>
/// 月之恶魔敌人反转代币：选择一个敌人并反转其力量和敏捷。
/// </summary>
public class MoonInvertEnemyTokenCard : MoonInvertTokenCard
{
    /// <summary>
    /// 构造敌人反转代币。
    /// </summary>
    public MoonInvertEnemyTokenCard()
        : base(TargetType.AnyEnemy)
    {
    }

    /// <summary>
    /// 出牌时反转选中敌人的力量和敏捷。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <param name="cardPlay">出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await InvertTarget(cardPlay.Target);
    }
}

/// <summary>
/// 月之恶魔队友反转代币：联机时选择一个队友并反转其力量和敏捷。
/// </summary>
public class MoonInvertAllyTokenCard : MoonInvertTokenCard
{
    /// <summary>
    /// 构造队友反转代币。
    /// </summary>
    public MoonInvertAllyTokenCard()
        : base(TargetType.AnyAlly)
    {
    }

    /// <summary>
    /// 出牌时反转选中队友的力量和敏捷。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <param name="cardPlay">出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == Owner?.Creature)
        {
            MainFile.Logger.Info("【月之恶魔代币】队友反转代币不能对自己使用，已跳过。");
            return;
        }

        await InvertTarget(cardPlay.Target);
    }
}
