using System.Runtime.CompilerServices;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

/// <summary>
/// 狡兔出击：阿福攻击牌，造成伤害并将复制品加入弃牌堆。
/// </summary>
public class RabbitStrike : AhFuCard
{
    /// <summary>
    /// 恢复费用效果每名玩家每回合最多触发次数。
    /// </summary>
    private const int EnergyRecoveryLimitPerTurn = 2;

    /// <summary>
    /// 按战斗隔离的恢复费用计数，避免不同战斗或不同玩家之间互相占用次数。
    /// </summary>
    private static readonly ConditionalWeakTable<ICombatState, EnergyRecoveryCombatState> EnergyRecoveryStates = new();

    /// <summary>
    /// 构造卡牌数值：0费金色攻击，造成6点伤害，升级后造成9点伤害。
    /// </summary>
    public RabbitStrike() : base(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(6, 3);
    }

    /// <summary>
    /// 出牌时造成伤害，复制自身进弃牌堆；拥有兔符咒时抽牌，阿福连招时恢复费用。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">本次出牌信息。</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null || Owner?.Creature?.CombatState == null)
        {
            return;
        }

        await CommonActions.CardAttack(this, cardPlay).Execute(choiceContext);
        await AddCopyToDiscard();

        bool hasRabbitTalisman = Owner.GetRelic<RabbitTalisman>() != null;
        bool afuComboActive = IsAfuComboActive();
        bool recoveredEnergy = false;

        if (hasRabbitTalisman)
        {
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }

        if (afuComboActive && TryConsumeEnergyRecoveryThisTurn())
        {
            await PlayerCmd.GainEnergy(1, Owner);
            recoveredEnergy = true;
        }

        await ResolveBountyRewards(choiceContext);
        MainFile.Logger.Info($"【狡兔出击】结算完成：玩家={Owner.NetId}，阿福连招={afuComboActive}，拥有兔符咒={hasRabbitTalisman}，恢复费用={recoveredEnergy}。");
    }

    /// <summary>
    /// 创建此牌复制品并加入弃牌堆。
    /// </summary>
    private async Task AddCopyToDiscard()
    {
        if (Owner?.Creature?.CombatState == null)
        {
            return;
        }

        CardModel copy = Owner.Creature.CombatState.CreateCard(ModelDb.Card<RabbitStrike>(), Owner);
        if (IsUpgraded)
        {
            CardCmd.Upgrade(copy, CardPreviewStyle.None);
        }

        await CardPileCmdHelper.AddGeneratedCardToCombat(copy, PileType.Discard, true);
    }

    /// <summary>
    /// 尝试消耗本玩家本回合一次恢复费用次数。
    /// </summary>
    /// <returns>仍有次数并成功登记时返回 true。</returns>
    private bool TryConsumeEnergyRecoveryThisTurn()
    {
        if (Owner?.Creature?.CombatState == null)
        {
            return false;
        }

        EnergyRecoveryCombatState state = GetEnergyRecoveryState(Owner.Creature.CombatState);
        return state.TryConsume(Owner.NetId);
    }

    /// <summary>
    /// 获取当前战斗的恢复费用计数状态。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <returns>当前战斗的恢复费用计数状态。</returns>
    private static EnergyRecoveryCombatState GetEnergyRecoveryState(ICombatState combatState)
    {
        EnergyRecoveryCombatState state = EnergyRecoveryStates.GetValue(combatState, _ => new EnergyRecoveryCombatState());
        int currentTurnNumber = GetCurrentTurnNumber(combatState);
        state.EnsureTurn(currentTurnNumber);
        return state;
    }

    /// <summary>
    /// 读取当前战斗回合号。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <returns>当前回合号。</returns>
    private static int GetCurrentTurnNumber(ICombatState combatState)
    {
        try
        {
            Type type = combatState.GetType();
            var property = type.GetProperty("TurnNumber")
                           ?? type.GetProperty("TurnCount")
                           ?? type.GetProperty("Turn")
                           ?? type.GetProperty("RoundNumber");
            if (property?.GetValue(combatState) is int intValue)
            {
                return intValue;
            }
        }
        catch
        {
            // 忽略反射失败，兜底返回0。
        }

        return 0;
    }

    /// <summary>
    /// 单场战斗内的恢复费用计数状态，内部再按玩家联机ID拆分次数。
    /// </summary>
    private sealed class EnergyRecoveryCombatState
    {
        /// <summary>
        /// 当前计数所属回合号。
        /// </summary>
        private int _turnNumber = -1;

        /// <summary>
        /// 每名玩家本回合已经触发的恢复费用次数。
        /// </summary>
        private readonly Dictionary<ulong, int> _countThisTurnByPlayer = [];

        /// <summary>
        /// 确保计数所属回合和当前回合一致。
        /// </summary>
        /// <param name="turnNumber">当前回合号。</param>
        public void EnsureTurn(int turnNumber)
        {
            if (turnNumber == _turnNumber)
            {
                return;
            }

            _turnNumber = turnNumber;
            _countThisTurnByPlayer.Clear();
            MainFile.Logger.Info($"【狡兔出击】恢复费用次数已重置：回合={turnNumber}。");
        }

        /// <summary>
        /// 尝试消耗指定玩家本回合一次恢复费用次数。
        /// </summary>
        /// <param name="ownerId">玩家联机ID。</param>
        /// <returns>仍有次数并成功登记时返回 true。</returns>
        public bool TryConsume(ulong ownerId)
        {
            int currentCount = _countThisTurnByPlayer.GetValueOrDefault(ownerId);
            if (currentCount >= EnergyRecoveryLimitPerTurn)
            {
                MainFile.Logger.Info($"【狡兔出击】恢复费用跳过：玩家={ownerId}，原因=本回合次数已用尽，已用={currentCount}/{EnergyRecoveryLimitPerTurn}。");
                return false;
            }

            _countThisTurnByPlayer[ownerId] = currentCount + 1;
            return true;
        }
    }
}
