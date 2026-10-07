using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 痞子能力：回合开始给所有敌人加黑手，并在单回合累计阈值后触发伤害与抽牌。
/// </summary>
public class BlackHandNoNameYetPower : ShengZhuSts2ModPower, IBlackHandAppliedListener
{
    private const int TriggerThreshold = 4;
    private readonly Dictionary<Creature, int> _gainedThisTurn = [];

    /// <summary>
    /// 痞子能力描述中额外展示的黑手层数和抽牌数。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars => base.CanonicalVars.Concat([new DynamicVar("BlackHandPerTurn", 1), new DynamicVar("Cards", 1)]);

    /// <summary>
    /// 每回合自动施加给所有敌人的黑手层数。
    /// </summary>
    public int BlackHandPerTurn { get; private set; } = 1;

    /// <summary>
    /// 每次痞子聚合触发时抽牌的数量。
    /// </summary>
    public int CardsToDraw { get; private set; } = 1;

    /// <summary>
    /// 痞子属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 痞子使用计数器显示触发伤害。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 施加或叠加痞子能力；重复施加时既提高触发伤害，也提高每回合施加层数。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">能力拥有者。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="triggerDamage">触发时造成的伤害。</param>
    /// <param name="blackHandPerTurn">每回合自动施加给所有敌人的黑手层数。</param>
    /// <returns>最终的痞子能力实例。</returns>
    public static async Task<BlackHandNoNameYetPower?> Apply(PlayerChoiceContext choiceContext, Player player, CardModel cardSource, int triggerDamage, int blackHandPerTurn)
    {
        if (player?.Creature == null)
        {
            return null;
        }

        int resolvedBlackHandPerTurn = Math.Max(0, blackHandPerTurn);
        BlackHandNoNameYetPower? existing = player.Creature.GetPower<BlackHandNoNameYetPower>();
        if (existing == null)
        {
            BlackHandNoNameYetPower? created = await CommonActions.Apply<BlackHandNoNameYetPower>(choiceContext, player.Creature, cardSource, triggerDamage);
            if (created != null)
            {
                created.BlackHandPerTurn = resolvedBlackHandPerTurn;
                created.CardsToDraw = 1;
                created.RefreshDescriptionVars();
            }
            return created;
        }

        await PowerCmd.ModifyAmount(existing, triggerDamage, player.Creature, cardSource);
        existing.BlackHandPerTurn += resolvedBlackHandPerTurn;
        existing.CardsToDraw += 1;
        existing.RefreshDescriptionVars();
        return existing;
    }

    /// <summary>
    /// 玩家回合开始时重置本回合统计，并向所有存活敌人施加黑手。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">当前回合玩家。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner?.Player == null || player != Owner.Player || Owner.CombatState == null)
        {
            return;
        }

        _gainedThisTurn.Clear();
        List<Creature> opponents = Owner.CombatState.GetOpponentsOf(Owner)
            .Where(BlackHandCardHelper.IsLivingCreature)
            .ToList();
        foreach (Creature creature in opponents)
        {
            if (!BlackHandCardHelper.IsLivingCreature(creature))
            {
                continue;
            }

            await BlackHandPower.Apply(choiceContext, creature, player, null, BlackHandPerTurn);
        }
    }

    /// <summary>
    /// 当目标在同一回合累计获得足够多黑手时，触发伤害并抽牌。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="target">本次获得黑手的目标。</param>
    /// <param name="appliedAmount">本次施加层数。</param>
    /// <returns>异步任务。</returns>
    public async Task OnBlackHandApplied(PlayerChoiceContext choiceContext, Creature target, int appliedAmount)
    {
        if (Owner?.Player == null || appliedAmount <= 0 || Amount <= 0 || !BlackHandCardHelper.IsLivingCreature(target))
        {
            return;
        }

        int total = _gainedThisTurn.GetValueOrDefault(target) + appliedAmount;
        int triggerCount = total / TriggerThreshold;
        int remaining = total % TriggerThreshold;
        if (triggerCount > 0)
        {
            int totalDamage = CalculateTotalTriggerDamage(triggerCount);
            int cardsToDraw = CalculateTotalDrawCount(triggerCount);
            await CreatureCmd.Damage(choiceContext, target, totalDamage, ValueProp.Unpowered, Owner, null, null);
            if (cardsToDraw > 0)
            {
                await CardPileCmd.Draw(choiceContext, cardsToDraw, Owner.Player);
            }
            MainFile.Logger.Info($"【痞子】聚合触发：目标={target.Name}，本次黑手={appliedAmount}，累计黑手={total}，触发次数={triggerCount}，总伤害={totalDamage}，抽牌={cardsToDraw}，余数={remaining}。");
        }

        if (remaining > 0)
        {
            _gainedThisTurn[target] = remaining;
        }
        else
        {
            _gainedThisTurn.Remove(target);
        }
    }

    /// <summary>
    /// 按触发次数计算总伤害，避免极端重放导致循环结算过多；超过整数上限时按最大值兜底。
    /// </summary>
    /// <param name="triggerCount">本次聚合出的触发次数。</param>
    /// <returns>本次应造成的总伤害。</returns>
    private int CalculateTotalTriggerDamage(int triggerCount)
    {
        long totalDamage = (long)Amount * triggerCount;
        if (totalDamage > int.MaxValue)
        {
            MainFile.Logger.Info($"【痞子】聚合伤害超过整数上限，已按最大值兜底：单次伤害={Amount}，触发次数={triggerCount}。");
            return int.MaxValue;
        }

        return Math.Max(0, (int)totalDamage);
    }

    /// <summary>
    /// 按触发次数和痞子张数计算总抽牌数；超过整数上限时按最大值兜底。
    /// </summary>
    /// <param name="triggerCount">本次聚合出的触发次数。</param>
    /// <returns>本次应抽的牌数。</returns>
    private int CalculateTotalDrawCount(int triggerCount)
    {
        long totalDraw = (long)Math.Max(0, CardsToDraw) * triggerCount;
        if (totalDraw > int.MaxValue)
        {
            MainFile.Logger.Info($"【痞子】聚合抽牌超过整数上限，已按最大值兜底：单次抽牌={CardsToDraw}，触发次数={triggerCount}。");
            return int.MaxValue;
        }

        return Math.Max(0, (int)totalDraw);
    }

    /// <summary>
    /// 刷新能力描述里展示的每回合黑手层数和抽牌数。
    /// </summary>
    private void RefreshDescriptionVars()
    {
        if (DynamicVars.ContainsKey("BlackHandPerTurn"))
        {
            DynamicVars["BlackHandPerTurn"].BaseValue = Math.Max(0, BlackHandPerTurn);
        }

        if (DynamicVars.ContainsKey("Cards"))
        {
            DynamicVars["Cards"].BaseValue = Math.Max(0, CardsToDraw);
        }
    }
}
