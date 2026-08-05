using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 我成了瘸腿鹅临时力量：施加时获得力量，拥有者回合结束时自动扣回。
/// </summary>
public class LameGooseTemporaryStrengthPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 瘸腿鹅临时力量属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 用计数器显示本回合需要扣回的力量层数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 首次施加临时力量时，先给玩家同等力量。
    /// </summary>
    /// <param name="target">获得临时力量的目标。</param>
    /// <param name="amount">本次临时力量层数。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>异步任务。</returns>
    public override async Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(target, amount, applier, cardSource, true);
        MainFile.Logger.Info($"【我成了瘸腿鹅】获得临时力量：目标={target.Name}，力量+{amount}。");
    }

    /// <summary>
    /// 临时力量叠层时，补给新增层数对应的力量。
    /// </summary>
    /// <param name="power">发生变化的能力。</param>
    /// <param name="amount">本次变化量。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power != this || amount <= 0 || amount == Amount || Owner == null)
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(Owner, amount, applier, cardSource, true);
        MainFile.Logger.Info($"【我成了瘸腿鹅】叠加临时力量：目标={Owner.Name}，力量+{amount}，当前需回收={Amount}。");
    }

    /// <summary>
    /// 拥有者回合结束时，扣回本能力记录的临时力量并移除自身。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">当前结束回合的阵营。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (Owner == null || side != Owner.Side)
        {
            return;
        }

        Creature owner = Owner;
        int strengthToRemove = Amount;
        Flash();
        await PowerCmd.Remove(this);

        if (strengthToRemove > 0)
        {
            await PowerCmd.Apply<StrengthPower>(owner, -strengthToRemove, owner, null, true);
            MainFile.Logger.Info($"【我成了瘸腿鹅】回合结束回收临时力量：目标={owner.Name}，力量-{strengthToRemove}。");
        }
    }
}
