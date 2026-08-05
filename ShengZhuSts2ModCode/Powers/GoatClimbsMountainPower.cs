using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 山羊爬山能力：每打出1张牌，本回合获得1点临时力量。
/// </summary>
public class GoatClimbsMountainPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 本回合已打出的牌计数器（用于日志观察每张牌触发情况）。
    /// </summary>
    private int _cardsPlayedThisTurn;

    /// <summary>
    /// 山羊爬山属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 山羊爬山使用计数器堆叠方式显示层数（每层代表每张牌获得的临时力量点数）。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 回合开始时重置本回合打牌计数。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">当前回合玩家。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner != null && player.Creature == Owner)
        {
            _cardsPlayedThisTurn = 0;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 每当一张牌被打出后，获得 Amount 点临时力量。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">刚打出的牌信息。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner == null || cardPlay.Card?.Owner?.Creature != Owner)
        {
            return;
        }

        _cardsPlayedThisTurn++;

        Flash();
        await PowerCmd.Apply<StrengthPower>(Owner, Amount, Owner, null);
        _strengthGainedThisTurn += Amount;
        MainFile.Logger.Info($"【山羊爬山】打出牌触发临时力量：本回合计数={_cardsPlayedThisTurn}，本次获得={Amount}，本回合累计待扣={_strengthGainedThisTurn}。");
    }

    /// <summary>
    /// 本回合通过此能力获得的临时力量总量（回合结束时扣除）。
    /// </summary>
    private int _strengthGainedThisTurn;

    /// <summary>
    /// 回合结束时扣除本回合获得的临时力量。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">当前结束回合的阵营。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side != MegaCrit.Sts2.Core.Combat.CombatSide.Player || Owner == null)
        {
            return;
        }

        if (_strengthGainedThisTurn > 0)
        {
            await PowerCmd.Apply<StrengthPower>(Owner, -_strengthGainedThisTurn, Owner, null);
            _strengthGainedThisTurn = 0;
        }
    }
}
