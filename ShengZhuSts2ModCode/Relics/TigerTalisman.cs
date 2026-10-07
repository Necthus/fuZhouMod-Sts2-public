using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 虎符咒：
/// 1. 交替打出攻击牌和技能牌时抽1张牌，每回合最多3次。
/// 2. 力量和敏捷取两者中的最大值：
///    - 阴平衡：若敏捷大于力量，攻击时将差值补足到伤害上（通过 ModifyDamageAdditive 实现）。
///    - 阳平衡：若力量大于敏捷，获得格挡时将差值补足到格挡上（通过 ModifyBlockAdditive 实现）。
/// </summary>
public class TigerTalisman : ShengZhuSts2ModRelic
{
    /// <summary>
    /// 每回合最大触发次数。
    /// </summary>
    private const int MaxTriggersPerTurn = 3;

    /// <summary>
    /// 上一张打出的牌类型。
    /// </summary>
    private CardType? _lastPlayedType;

    /// <summary>
    /// 本回合已触发抽牌次数。
    /// </summary>
    private int _triggersThisTurn;

    /// <summary>
    /// 本回合打出的攻击牌数量（鼠符咒共鸣用）。
    /// </summary>
    private int _attackCountThisTurn;

    /// <summary>
    /// 本回合打出的技能牌数量（鼠符咒共鸣用）。
    /// </summary>
    private int _skillCountThisTurn;

    /// <summary>
    /// 下回合需要额外抽的牌数（鼠符咒共鸣积累）。
    /// </summary>
    private int _ratResonanceDrawNextTurn;

    /// <summary>
    /// 本回合攻击牌数量，写入存档和联机状态，避免虎鼠共鸣判断两端不一致。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int SavedAttackCountThisTurn
    {
        get => _attackCountThisTurn;
        set => _attackCountThisTurn = Math.Max(0, value);
    }

    /// <summary>
    /// 本回合技能牌数量，写入存档和联机状态，避免虎鼠共鸣判断两端不一致。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int SavedSkillCountThisTurn
    {
        get => _skillCountThisTurn;
        set => _skillCountThisTurn = Math.Max(0, value);
    }

    /// <summary>
    /// 下回合虎鼠共鸣待抽牌数量，写入存档和联机状态，避免一端额外抽牌、一端不抽牌。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int SavedRatResonanceDrawNextTurn
    {
        get => _ratResonanceDrawNextTurn;
        set => _ratResonanceDrawNextTurn = Math.Max(0, value);
    }

    /// <summary>
    /// 遗物稀有度：稀有。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Rare;

    /// <summary>
    /// 显示剩余摸牌机会计数器。
    /// </summary>
    public override bool ShowCounter => true;

    /// <summary>
    /// 计数器值：显示本回合剩余摸牌机会。
    /// </summary>
    public override int DisplayAmount => MaxTriggersPerTurn - _triggersThisTurn;

    /// <summary>
    /// 战斗开始时重置状态，若有鼠符咒共鸣则施加指示器能力。
    /// </summary>
    public override async Task BeforeCombatStart()
    {
        _lastPlayedType = null;
        _triggersThisTurn = 0;
        _attackCountThisTurn = 0;
        _skillCountThisTurn = 0;
        _ratResonanceDrawNextTurn = 0;

        // 有鼠符咒共鸣时显示攻击/技能计数指示器；石像形态会同步移除，避免能力栏误导。
        await ShengZhuFormService.SyncRatResonanceIndicatorPower(Owner);
    }

    /// <summary>
    /// 回合结束时重置本回合计数和上一张牌类型。
    /// 鼠符咒共鸣：若本回合攻击数=技能数且都>0，下回合多抽2张。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            // 鼠符咒共鸣：攻击数=技能数时下回合多抽2张（通过EnergyNextTurnPower的同类机制）
            if (HasRatResonance() && _attackCountThisTurn > 0 && _attackCountThisTurn == _skillCountThisTurn)
            {
                Flash();
                _ratResonanceDrawNextTurn += 2;
                MainFile.Logger.Info($"【联机同步】来源=虎符咒鼠符咒共鸣，玩家={Owner?.NetId}/{Owner?.Character.Id.Entry}，动作=累计下回合抽牌，攻击数={_attackCountThisTurn}，技能数={_skillCountThisTurn}，待抽牌数={_ratResonanceDrawNextTurn}。");
            }

            _triggersThisTurn = 0;
            _attackCountThisTurn = 0;
            _skillCountThisTurn = 0;
            _lastPlayedType = null;
            InvokeDisplayAmountChanged();
        }
    }

    /// <summary>
    /// 回合开始时：若有共鸣抽牌积累，执行抽牌。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null || player != Owner)
        {
            return;
        }

        if (_ratResonanceDrawNextTurn > 0)
        {
            int drawCount = _ratResonanceDrawNextTurn;
            int handCountBefore = Owner.PlayerCombatState?.Hand.Cards.Count ?? 0;
            int drawPileCountBefore = Owner.PlayerCombatState?.DrawPile.Cards.Count ?? 0;
            int discardPileCountBefore = Owner.PlayerCombatState?.DiscardPile.Cards.Count ?? 0;
            _ratResonanceDrawNextTurn = 0;
            Flash();
            MainFile.Logger.Info($"【联机同步】来源=虎符咒鼠符咒共鸣，玩家={Owner.NetId}/{Owner.Character.Id.Entry}，动作=回合开始额外抽牌，抽牌数={drawCount}，抽牌前手牌={handCountBefore}，抽牌堆={drawPileCountBefore}，弃牌堆={discardPileCountBefore}。");
            await CardPileCmd.Draw(choiceContext, drawCount, Owner);
        }
    }

    /// <summary>
    /// 出牌后检查是否形成交替，满足条件则抽1张牌。同时统计攻击/技能数量。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay?.Card == null || Owner == null || !IsCardPlayedByOwner(cardPlay))
        {
            return;
        }

        CardType currentType = cardPlay.Card.Type;

        // 只关注攻击和技能
        if (currentType != CardType.Attack && currentType != CardType.Skill)
        {
            return;
        }

        if (HasRatResonance())
        {
            // 只有当前真实享受鼠符咒共鸣时才统计，石像形态期间打出的牌不算入虎鼠共鸣。
            if (currentType == CardType.Attack) _attackCountThisTurn++;
            else _skillCountThisTurn++;

            MainFile.Logger.Info($"【联机同步】来源=虎符咒鼠符咒共鸣，玩家={Owner.NetId}/{Owner.Character.Id.Entry}，动作=记录出牌计数，卡牌={cardPlay.Card.Id.Entry}，类型={currentType}，攻击数={_attackCountThisTurn}，技能数={_skillCountThisTurn}。");
        }

        // 如果上一张牌有记录，且类型不同（形成交替），且本回合触发次数<3
        if (_lastPlayedType != null && _lastPlayedType != currentType && _triggersThisTurn < MaxTriggersPerTurn)
        {
            _triggersThisTurn++;
            Flash();
            InvokeDisplayAmountChanged();
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }

        _lastPlayedType = currentType;
    }

    /// <summary>
    /// 阴平衡（力量和敏捷取最大值的攻击侧）：若敏捷大于力量，攻击时将差值补足到伤害上。
    /// </summary>
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 只对自己造成的伤害生效
        if (dealer != Owner?.Creature || Owner?.Creature == null)
        {
            return 0m;
        }

        // 无属性伤害不走普通攻击修正，避免黑影兵团等特殊结算吃到虎符咒。
        if (props.HasFlag(ValueProp.Unpowered))
        {
            return 0m;
        }

        // 黑影兵团牌只吃影噬，不吃力量、敏捷、虚弱、易伤，也不吃虎符咒阴平衡。
        if (cardSource is BaseShadowKhanCard)
        {
            return 0m;
        }

        // 明确有卡牌来源时，只允许攻击牌吃阴平衡；无卡牌来源但仍是普通Move伤害时保留兼容。
        if (cardSource != null && cardSource.Type != CardType.Attack)
        {
            return 0m;
        }

        // 获取力量和敏捷值
        decimal strength = GetPowerAmount<StrengthPower>(Owner.Creature);
        decimal dexterity = GetPowerAmount<DexterityPower>(Owner.Creature);

        // 阴平衡：敏捷 > 力量时，补差值
        if (dexterity > strength)
        {
            return dexterity - strength;
        }

        return 0m;
    }

    /// <summary>
    /// 阳平衡（力量和敏捷取最大值的格挡侧）。
    /// 普通格挡链路会先吃敏捷；虎符咒只在力量更高时补上力量和敏捷的差值。
    /// </summary>
    public override decimal ModifyBlockAdditive(Creature? target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return 0m;
        }

        // 只处理玩家自己的普通格挡，避免特殊结算和敌方来源吃到虎符咒。
        if (target != null && target != Owner.Creature)
        {
            return 0m;
        }

        if (cardSource?.Owner != null && cardSource.Owner != Owner)
        {
            return 0m;
        }

        // 无属性或非 Move 格挡不走普通力量/敏捷修正链，虎符咒也不介入。
        if (props.HasFlag(ValueProp.Unpowered) || !props.HasFlag(ValueProp.Move))
        {
            return 0m;
        }

        // 黑影兵团牌使用自己的黑影体系公式，不吃敏捷、虎符咒和普通格挡修正。
        if (cardSource is BaseShadowKhanCard)
        {
            return 0m;
        }

        // 获取力量和敏捷值
        decimal strength = GetPowerAmount<StrengthPower>(Owner.Creature);
        decimal dexterity = GetPowerAmount<DexterityPower>(Owner.Creature);

        // 阳平衡：普通格挡已获得敏捷，这里只补力量超出敏捷的差值。
        if (strength > dexterity)
        {
            decimal bonus = strength - dexterity;
            //2026-06-21 暂时屏蔽日志，出现问题需要调试的时候再打开
//            MainFile.Logger.Info($"【虎符咒阳平衡】触发：力量={strength}，敏捷={dexterity}，补差值={bonus}，target={target?.GetType().Name ?? "null"}，cardSource={cardSource?.Id.Entry ?? "null"}");
            return bonus;
        }

        return 0m;
    }

    /// <summary>
    /// 当力量或敏捷数值变化时，刷新手牌中卡牌的预览显示。
    /// 阳平衡影响格挡预览（技能牌），阴平衡影响伤害预览（攻击牌），因此两者都需要刷新。
    /// </summary>
    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        // 只关心自己身上的力量或敏捷变化
        if (Owner?.Creature == null || power.Owner != Owner.Creature)
        {
            return Task.CompletedTask;
        }

        if (power is StrengthPower or DexterityPower)
        {
            RefreshHandCardPreviews();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 刷新手牌中所有卡牌的预览显示：力量/敏捷变化后，虎符咒的平衡效果会改变卡面数值。
    /// </summary>
    private void RefreshHandCardPreviews()
    {
        var combatState = Owner?.PlayerCombatState;
        if (combatState == null)
        {
            return;
        }

        int refreshedCount = 0;
        foreach (CardModel card in combatState.Hand.Cards.ToList())
        {
            // 攻击牌受阴平衡影响（伤害），技能牌受阳平衡影响（格挡）
            if (card.Type != CardType.Attack && card.Type != CardType.Skill)
            {
                continue;
            }

            NCard? cardNode = NCard.FindOnTable(card, PileType.Hand);
            if (cardNode == null)
            {
                continue;
            }

            cardNode.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
            refreshedCount++;
        }

        if (refreshedCount > 0)
        {
            MainFile.Logger.Info($"【虎符咒】力量/敏捷变化，已刷新手牌预览：刷新数量={refreshedCount}");
        }
    }

    /// <summary>
    /// 获取指定Power的Amount值。
    /// </summary>
    private static decimal GetPowerAmount<T>(Creature creature) where T : PowerModel
    {
        T? power = creature.Powers.OfType<T>().FirstOrDefault();
        return power?.Amount ?? 0m;
    }
}
