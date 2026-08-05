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
/// 乌鸦坐飞机：施加时获得敏捷，拥有者回合结束时自动扣回。
/// </summary>
public class TemporaryDexterityPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 乌鸦坐飞机属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用乌鸦坐飞机卡图作为能力小图。
    /// </summary>
    public override string CustomPackedIconPath => Path.Join(MainFile.ResPath, "images", "card_portraits", "crowflying.png");

    /// <summary>
    /// 使用乌鸦坐飞机卡图作为能力大图。
    /// </summary>
    public override string CustomBigIconPath => Path.Join(MainFile.ResPath, "images", "card_portraits", "big", "crowflying.png");

    /// <summary>
    /// 用计数器显示本回合需要扣回的敏捷层数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 首次施加乌鸦坐飞机时，先给玩家同等敏捷。
    /// </summary>
    /// <param name="target">获得乌鸦坐飞机的目标。</param>
    /// <param name="amount">本次乌鸦坐飞机层数。</param>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>异步任务。</returns>
    public override async Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0)
        {
            return;
        }

        await PowerCmd.Apply<DexterityPower>(target, amount, applier, cardSource, true);
        MainFile.Logger.Info($"【乌鸦坐飞机】获得临时敏捷：目标={target.Name}，敏捷+{amount}。");
    }

    /// <summary>
    /// 乌鸦坐飞机叠层时，补给新增层数对应的敏捷。
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

        await PowerCmd.Apply<DexterityPower>(Owner, amount, applier, cardSource, true);
        MainFile.Logger.Info($"【乌鸦坐飞机】叠加临时敏捷：目标={Owner.Name}，敏捷+{amount}，当前需回收={Amount}。");
    }

    /// <summary>
    /// 拥有者回合结束时，扣回本能力记录的敏捷并移除自身。
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
        int dexterityToRemove = Amount;
        Flash();
        await PowerCmd.Remove(this);

        if (dexterityToRemove > 0)
        {
            await PowerCmd.Apply<DexterityPower>(owner, -dexterityToRemove, owner, null, true);
            MainFile.Logger.Info($"【乌鸦坐飞机】回合结束回收临时敏捷：目标={owner.Name}，敏捷-{dexterityToRemove}。");
        }
    }
}
