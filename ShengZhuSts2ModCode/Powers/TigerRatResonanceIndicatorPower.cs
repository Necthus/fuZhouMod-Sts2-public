using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 虎鼠共鸣指示器能力：纯展示用，无任何实际效果。
/// 显示本回合打出的攻击牌数量（Amount）和技能牌数量（SecondAmount）。
/// 每回合开始时重置为0。
/// 图标复用虎符咒的遗物图标。
/// </summary>
public class TigerRatResonanceIndicatorPower : ShengZhuSts2ModPower, IHasSecondAmount
{
    /// <summary>
    /// 本回合打出的攻击牌数量。
    /// </summary>
    private int _attackCountThisTurn;

    /// <summary>
    /// 本回合打出的技能牌数量。
    /// </summary>
    private int _skillCountThisTurn;

    /// <summary>
    /// 属于增益类型（纯展示）。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器堆叠方式显示攻击牌数量。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 覆盖图标路径：使用虎符咒的遗物小图。
    /// </summary>
    public override string CustomPackedIconPath =>
        Path.Join(MainFile.ResPath, "images", "relics", "tigertalisman.png");

    /// <summary>
    /// 覆盖大图路径：使用虎符咒的遗物大图。
    /// </summary>
    public override string CustomBigIconPath =>
        Path.Join(MainFile.ResPath, "images", "relics", "big", "tigertalisman.png");

    /// <summary>
    /// IHasSecondAmount 实现：显示本回合技能牌数量。
    /// </summary>
    public string GetSecondAmount()
    {
        return _skillCountThisTurn.ToString();
    }

    /// <summary>
    /// 回合开始时重置攻击/技能计数为0。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">当前回合玩家。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner != null && player.Creature == Owner)
        {
            _attackCountThisTurn = 0;
            _skillCountThisTurn = 0;
            SetAmount(0);
            this.InvokeSecondAmountChanged();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 每当打出一张牌后，更新攻击/技能计数。
    /// Amount 显示攻击牌数量，SecondAmount 显示技能牌数量。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">刚打出的牌信息。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner == null || cardPlay.Card?.Owner?.Creature != Owner)
        {
            return Task.CompletedTask;
        }

        CardType currentType = cardPlay.Card.Type;

        if (currentType == CardType.Attack)
        {
            _attackCountThisTurn++;
            SetAmount(_attackCountThisTurn);
        }
        else if (currentType == CardType.Skill)
        {
            _skillCountThisTurn++;
            this.InvokeSecondAmountChanged();
        }

        return Task.CompletedTask;
    }
}
