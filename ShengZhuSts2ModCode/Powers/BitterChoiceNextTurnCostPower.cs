using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 苦涩的抉择费用惩罚：下个玩家回合开始时，让当前战斗牌堆中的卡牌本回合费用增加。
/// </summary>
public class BitterChoiceNextTurnCostPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 本回合已经开始结算的费用惩罚数值；用于给回合中后续生成的黑影兵团卡补同样加费。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int ActiveCostIncreaseThisTurn { get; set; }

    /// <summary>
    /// 费用惩罚属于减益能力。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>
    /// 使用计数器显示下回合费用增加量。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 玩家回合开始时标记本回合费用惩罚已经生效。
    /// 费用通过 TryModifyEnergyCostInCombat 动态计算，避免马符咒移除本能力后卡牌残留已写入的加费。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">当前回合玩家。</param>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner?.Player == null || player != Owner.Player || Amount <= 0)
        {
            return;
        }

        ActiveCostIncreaseThisTurn = Amount;
        List<CardModel> affectedCards = GetAffectedCards(player);
        MainFile.Logger.Info($"【联机同步】【苦涩的抉择】下回合费用惩罚生效：玩家={player.NetId}，动态费用+{ActiveCostIncreaseThisTurn}，当前可影响卡牌数={affectedCards.Count}。");
        await Task.CompletedTask;
    }

    /// <summary>
    /// 动态修正当前战斗费用；能力被马符咒移除后，这里的费用惩罚会立即失效。
    /// </summary>
    /// <param name="card">正在计算费用的卡牌。</param>
    /// <param name="originalCost">原始费用。</param>
    /// <param name="modifiedCost">修正后费用。</param>
    /// <returns>费用被修改时返回 true。</returns>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (ActiveCostIncreaseThisTurn <= 0 || !CanAffectCard(card))
        {
            return false;
        }

        modifiedCost = originalCost + ActiveCostIncreaseThisTurn;
        return true;
    }

    /// <summary>
    /// 玩家回合结束时清理本回合已经生效的费用惩罚；若本回合又打出苦涩的抉择，则保留下回合的新惩罚层数。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">当前结束回合的阵营。</param>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (Owner == null || side != Owner.Side || ActiveCostIncreaseThisTurn <= 0)
        {
            return;
        }

        int activeCostIncrease = ActiveCostIncreaseThisTurn;
        ActiveCostIncreaseThisTurn = 0;
        if (Amount <= activeCostIncrease)
        {
            MainFile.Logger.Info($"【联机同步】【苦涩的抉择】费用惩罚回合结束，移除能力：玩家={Owner.Player?.NetId.ToString() ?? "无"}，已结算费用={activeCostIncrease}。");
            await PowerCmd.Remove(this);
            return;
        }

        await PowerCmd.ModifyAmount(this, -activeCostIncrease, Owner, null);
        MainFile.Logger.Info($"【联机同步】【苦涩的抉择】费用惩罚回合结束，保留下回合新惩罚：玩家={Owner.Player?.NetId.ToString() ?? "无"}，已结算费用={activeCostIncrease}，剩余层数={Amount}。");
    }

    /// <summary>
    /// 给本回合苦涩惩罚已经生效后新生成的黑影兵团卡补同样的费用增加。
    /// </summary>
    /// <param name="player">卡牌所属玩家。</param>
    /// <param name="card">新生成的卡牌。</param>
    /// <param name="source">生成来源，用于日志排查。</param>
    public static void TryApplyToGeneratedShadowKhanCard(Player? player, CardModel? card, string source)
    {
        if (player?.Creature == null || card is not BaseShadowKhanCard)
        {
            return;
        }

        var power = player.Creature.GetPower<BitterChoiceNextTurnCostPower>();
        if (power == null || power.ActiveCostIncreaseThisTurn <= 0)
        {
            return;
        }

        MainFile.Logger.Info($"【联机同步】【苦涩的抉择】生成黑影兵团沿用动态加费：玩家={player.NetId}，来源={source}，卡牌={card.Id.Entry}，费用+{power.ActiveCostIncreaseThisTurn}。");
    }

    /// <summary>
    /// 获取要加费的当前战斗卡牌，按牌堆顺序稳定处理并去重。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>需要加费的卡牌列表。</returns>
    private static List<CardModel> GetAffectedCards(Player player)
    {
        var state = player.PlayerCombatState;
        if (state == null)
        {
            return [];
        }

        return state.Hand.Cards
            .Concat(state.DrawPile.Cards)
            .Concat(state.DiscardPile.Cards)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// 判断当前卡牌是否属于本能力持有者。
    /// </summary>
    /// <param name="card">待判断的卡牌。</param>
    /// <returns>属于持有者时返回 true。</returns>
    private bool CanAffectCard(CardModel? card)
    {
        return Owner?.Player != null
               && card?.Owner != null
               && card.Owner.NetId == Owner.Player.NetId;
    }
}
