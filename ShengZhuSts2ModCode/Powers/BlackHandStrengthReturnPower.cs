using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 黑手力量返还：若目标因裸绞临时掉力量，则在若干回合后返还。
/// </summary>
public class BlackHandStrengthReturnPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 到时需要返还的力量值。
    /// </summary>
    public int ReturnStrength { get; private set; }

    /// <summary>
    /// 黑手力量返还属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 黑手力量返还使用计数器显示剩余回合数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 施加或叠加力量返还能力。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">能力拥有者。</param>
    /// <param name="sourcePlayer">来源玩家。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="turns">返还前等待的回合数。</param>
    /// <param name="returnStrength">到时返还的力量值。</param>
    /// <returns>最终的力量返还能力实例。</returns>
    public static async Task<BlackHandStrengthReturnPower?> Apply(PlayerChoiceContext choiceContext, Creature target, Player? sourcePlayer, CardModel? cardSource, int turns, int returnStrength)
    {
        if (target == null || returnStrength <= 0)
        {
            return null;
        }

        BlackHandStrengthReturnPower? existing = target.GetPower<BlackHandStrengthReturnPower>();
        if (existing == null)
        {
            BlackHandStrengthReturnPower? created = cardSource != null
                ? await CommonActions.Apply<BlackHandStrengthReturnPower>(choiceContext, target, cardSource, Math.Max(1, turns))
                : await PowerCmd.Apply<BlackHandStrengthReturnPower>(target, Math.Max(1, turns), sourcePlayer?.Creature, null);
            if (created != null)
            {
                created.ReturnStrength = Math.Max(1, returnStrength);
            }
            return created;
        }

        if (turns > existing.Amount)
        {
            await PowerCmd.ModifyAmount(existing, turns - existing.Amount, sourcePlayer?.Creature, cardSource);
        }
        existing.ReturnStrength += Math.Max(0, returnStrength);
        return existing;
    }

    /// <summary>
    /// 每到拥有者回合开始时减少剩余回合，归零后返还力量并移除自身。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">当前回合玩家。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null)
        {
            return;
        }

        if (Amount <= 1)
        {
            if (ReturnStrength > 0)
            {
                await PowerCmd.Apply<StrengthPower>(Owner, ReturnStrength, Owner, null);
            }

            await PowerCmd.Remove(this);
            return;
        }

        await PowerCmd.ModifyAmount(this, -1, null, null);
    }
}
