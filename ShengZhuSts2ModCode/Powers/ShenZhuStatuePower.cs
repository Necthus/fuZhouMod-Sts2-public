using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 圣主石像能力：圣主默认的石板形态。
/// 效果：
/// 1. 获得的格挡在最终结算后额外增加 2 点，黑影兵团牌只额外增加 1 点。
/// 2. 初始获得 1 层荆棘（由圣主角色本体在战斗开始时施加）。
/// 3. 每 3 回合额外获得 1 层荆棘，最多成长到 12 层。
/// </summary>
public class ShenZhuStatuePower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 石像形态普通护甲加成值。
    /// </summary>
    public const int BlockBonusAmount = 2;

    /// <summary>
    /// 黑影兵团牌从石像形态获得的护甲加成上限。
    /// </summary>
    public const int ShadowKhanBlockBonusAmount = 1;

    /// <summary>
    /// 石像内置虚弱的普通攻击伤害倍率。
    /// </summary>
    private const decimal StatueWeakDamageMultiplier = 0.75m;

    /// <summary>
    /// 初始荆棘层数。
    /// </summary>
    public const int InitialThorns = 1;

    /// <summary>
    /// 当前已经过的回合数（用于判断每3回合触发荆棘成长）。
    /// </summary>
    private int _turnCount;

    /// <summary>
    /// 当前正在结算的出牌开始时，圣主自身的护甲值。
    /// </summary>
    private decimal _blockBeforeCurrentCardPlay;

    /// <summary>
    /// 石像这条来源自己提供的荆棘层数，只用于限制石像来源的成长上限。
    /// </summary>
    private int _statueThornsAmount;

    /// <summary>
    /// 当前由圣主石像来源提供的荆棘层数。
    /// </summary>
    public int StatueThornsAmount => _statueThornsAmount;

    /// <summary>
    /// 当前石像形态已经记录的玩家回合计数。
    /// </summary>
    public int StatueTurnCount => _turnCount;

    /// <summary>
    /// 石板形态属于负面/特殊机制状态（有代价也有收益，标记为Debuff以示代价）。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 石板形态不叠加，只有一层。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>
    /// 玩家回合开始前：
    /// 1. 记录石像形态经过的回合数。
    /// 2. 每 3 回合额外获得 1 层荆棘，最多 12 层。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">即将开始回合的一方。</param>
    /// <param name="combatState">当前战斗状态。</param>
    /// <returns>异步任务。</returns>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || Owner == null)
        {
            return;
        }

        await ApplyPlayerTurnStartEffects();
    }

    /// <summary>
    /// 记录圣主本次出牌开始前的护甲值，用于在出牌结束后输出前后变化日志。
    /// </summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Owner == null || cardPlay.Card.Owner?.Creature != Owner)
        {
            return Task.CompletedTask;
        }

        _blockBeforeCurrentCardPlay = Owner.Block;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 出牌结束后输出圣主石像补正导致的护甲前后变化。
    /// </summary>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner == null || cardPlay.Card.Owner?.Creature != Owner)
        {
            return Task.CompletedTask;
        }

        decimal blockAfter = Owner.Block;
        decimal blockDelta = blockAfter - _blockBeforeCurrentCardPlay;
        if (blockDelta > 0)
        {
            int statueBonus = GetBlockBonusForCard(cardPlay.Card);
            ShengZhuLogHelper.VerboseCombatInfo(() => $"【圣主石像】出牌护甲日志：卡牌={cardPlay.Card.Id.Entry}，出牌前护甲={_blockBeforeCurrentCardPlay}，出牌后护甲={blockAfter}，本次变化=+{blockDelta}，其中石像补正=+{statueBonus}。");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 任何荆棘变化后都强制封顶到 12 层，避免被其他来源顶穿上限。
    /// </summary>
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (Owner == null || power.Owner != Owner || power is not ThornsPower)
        {
            return;
        }

        if (!ReferenceEquals(power, this))
        {
            return;
        }

        if (_statueThornsAmount > 12)
        {
            int excess = _statueThornsAmount - 12;
            _statueThornsAmount = 12;
            SetAmount(Math.Max(0, Amount - excess));
            ShengZhuLogHelper.VerboseCombatInfo(() => $"【圣主石像】荆棘封顶：石像来源荆棘已修正为 12 层，来源={cardSource?.Id.Entry ?? "null"}，施加者={applier?.Name ?? "null"}。");
        }
    }

    /// <summary>
    /// 石像形态在护甲最终结算后追加护甲，不参与敏捷、影噬、脆弱等前置修正。
    /// 黑影兵团牌只能从石像形态获得 1 点护甲加成。
    /// </summary>
    public override decimal ModifyBlockAdditive(Creature? target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (Owner == null || target != Owner)
        {
            return 0m;
        }

        return GetBlockBonusForCard(cardSource ?? cardPlay?.Card);
    }

    /// <summary>
    /// 石像旧版内置虚弱入口：当前按平衡需求停用，保留方法方便以后恢复。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // 旧逻辑会让石像形态的普通攻击伤害降低至 75%。现在只屏蔽，不删除，方便后续需要时恢复。
        return 1m;
    }

    /// <summary>
    /// 根据卡牌来源决定石像护甲补正值，黑影兵团牌只吃 1 点。
    /// </summary>
    /// <param name="card">本次获得护甲的卡牌来源。</param>
    /// <returns>石像提供的护甲补正。</returns>
    private static int GetBlockBonusForCard(CardModel? card)
    {
        return card is BaseShadowKhanCard ? ShadowKhanBlockBonusAmount : BlockBonusAmount;
    }

    /// <summary>
    /// 执行石像形态的玩家回合开始效果。
    /// </summary>
    /// <returns>异步任务。</returns>
    public async Task ApplyPlayerTurnStartEffects()
    {
        if (Owner == null)
        {
            return;
        }

        _turnCount++;

        // 石像旧版自我虚弱已停用；这里保留闪光表现，方便确认石像回合开始效果仍在运行。
        Flash();

        // 每 3 回合额外获得 1 层荆棘，只限制石像这条来源自己的成长上限
        if (_turnCount % 3 == 0 && _statueThornsAmount < 12)
        {
            decimal totalThornsBefore = Owner.GetPower<ThornsPower>()?.Amount ?? 0;
            decimal otherThornsBefore = Math.Max(0, totalThornsBefore - _statueThornsAmount);
            int statueThornsAfter = Math.Min(12, _statueThornsAmount + 1);
            int statueThornsCanGrow = 12 - statueThornsAfter;
            decimal totalThornsAfter = totalThornsBefore + 1;

            ShengZhuLogHelper.VerboseCombatInfo(() => $"【圣主石像】荆棘成长：当前总荆棘={totalThornsBefore}，其中石像来源={_statueThornsAmount}，其他来源={otherThornsBefore}；成长后总荆棘={totalThornsAfter}，石像来源={statueThornsAfter}，其他来源={otherThornsBefore}；石像来源还可成长={statueThornsCanGrow}。");
            await PowerCmd.Apply<ThornsPower>(Owner, 1, Owner, null);
            _statueThornsAmount = statueThornsAfter;
        }
    }

    /// <summary>
    /// 初始化石像这条来源自己的荆棘层数。
    /// </summary>
    /// <param name="amount">初始层数。</param>
    public void InitializeStatueThorns(int amount)
    {
        RestoreStatueState(amount, _turnCount);
    }

    /// <summary>
    /// 恢复石像来源状态，用于鼠符咒在同一场战斗内切回石像时继承石像自己的荆棘和成长节奏。
    /// </summary>
    /// <param name="statueThornsAmount">石像来源荆棘层数。</param>
    /// <param name="turnCount">石像形态已经记录的玩家回合计数。</param>
    public void RestoreStatueState(int statueThornsAmount, int turnCount)
    {
        _statueThornsAmount = Math.Min(12, Math.Max(0, statueThornsAmount));
        _turnCount = Math.Max(0, turnCount);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【圣主石像】恢复石像状态：石像来源荆棘={_statueThornsAmount}，石像计数={_turnCount}。");
    }
}
