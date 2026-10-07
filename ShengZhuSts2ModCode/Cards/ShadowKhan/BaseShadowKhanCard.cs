using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

/// <summary>
/// 黑影兵团卡牌基类：所有由面具能力生成的衍生攻击/技能牌继承此类。
/// 特征：0费、Token稀有度、自带消耗、不进入正常奖励池。
/// 默认情况下，其伤害和格挡不受力量/敏捷/虚弱/脆弱影响，改为受“影噬”(DominionPower)影响。
/// 拥有恶魔法典时，影噬与力量/敏捷先混合，再进入正常攻防修正链。
/// 每1层影噬使兵团卡的伤害和格挡+1。
/// </summary>
public abstract class BaseShadowKhanCard(int cost, CardType type, TargetType target) :
    ShengZhuSts2ModCard(cost, type, CardRarity.Token, target)
{
    /// <summary>
    /// 当前正在结算的出牌信息，用于让石像等护甲修正能识别黑影兵团来源。
    /// </summary>
    private CardPlay? _currentCardPlay;

    /// <summary>
    /// 黑影兵团牌不应进入正常奖励池，只能由面具能力衍生生成。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 获取黑影兵团牌印在卡面上的基础伤害值。
    /// 仅攻击牌需要重写，默认返回0。
    /// </summary>
    public virtual int PrintedBaseDamage => 0;

    /// <summary>
    /// 获取黑影兵团牌印在卡面上的基础格挡值。
    /// 仅格挡牌需要重写，默认返回0。
    /// </summary>
    public virtual int PrintedBaseBlock => 0;

    /// <summary>
    /// 获取拥有者身上的影噬层数。
    /// </summary>
    /// <returns>影噬层数，无则返回0。</returns>
    protected int GetDominionAmount()
    {
        var creature = Owner?.Creature;
        if (creature == null) return 0;
        var power = creature.GetPower<DominionPower>();
        return power?.Amount ?? 0;
    }

    /// <summary>
    /// 判断恶魔法典是否生效。
    /// </summary>
    /// <returns>拥有恶魔法典能力时返回 true。</returns>
    protected bool IsDemonCodexActive()
    {
        return Owner?.Creature?.HasPower<DemonCodexPower>() == true;
    }

    /// <summary>
    /// 计算当前牌的卡面基础值，升级后自动叠加升级数值。
    /// </summary>
    protected int GetPrintedBaseValue(int baseValue, int upgradeValue)
    {
        return baseValue + (IsUpgraded ? upgradeValue : 0);
    }

    /// <summary>
    /// 记录当前黑影兵团牌的出牌信息，方便后续格挡结算识别卡牌来源。
    /// </summary>
    /// <param name="cardPlay">即将打出的牌信息。</param>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, this))
        {
            _currentCardPlay = cardPlay;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 出牌结束后清理临时出牌信息，避免影响后续结算。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <param name="cardPlay">刚完成的出牌信息。</param>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, this))
        {
            _currentCardPlay = null;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 计算手牌展示用的黑影兵团伤害，只吃影噬，不吃力量和虚弱。
    /// 当卡牌没有 Owner 时（如预览卡），返回印刷基础伤害值。
    /// </summary>
    protected decimal CalculateDisplayedShadowKhanDamage(int baseDamage)
    {
        return Math.Max(0, baseDamage + GetDominionAmount());
    }

    /// <summary>
    /// 计算卡面展示用的黑影兵团伤害；只有恶魔法典生效时才进入普通伤害预览链。
    /// </summary>
    /// <param name="damage">已经包含影噬和牌自身额外值的原始伤害。</param>
    /// <param name="target">当前预览目标。</param>
    /// <returns>卡面应显示的最终伤害。</returns>
    protected decimal CalculateDisplayedShadowKhanDamage(int damage, Creature? target)
    {
        int rawDamage = Math.Max(0, damage);
        if (!IsDemonCodexActive())
        {
            return rawDamage;
        }

        var creature = Owner?.Creature;
        var runState = Owner?.RunState;
        var combatState = CombatState ?? creature?.CombatState;
        if (creature == null || runState == null || combatState == null)
        {
            return rawDamage;
        }

        decimal modifiedDamage = Hook.ModifyDamage(
            runState,
            combatState,
            target,
            creature,
            rawDamage,
            ValueProp.Move,
            this,
            null,
            ModifyDamageHookType.All,
            CardPreviewMode.Normal,
            out _);

        return Math.Max(0, Math.Floor(modifiedDamage));
    }

    /// <summary>
    /// 按当前 WithCalculatedDamage 写法，返回卡面应追加到印刷基础伤害上的展示加成。
    /// </summary>
    /// <param name="printedBaseDamage">印刷基础伤害。</param>
    /// <param name="extraDamage">除影噬外的牌自身额外伤害。</param>
    /// <param name="target">当前预览目标。</param>
    /// <returns>展示加成。</returns>
    protected decimal GetDisplayedShadowKhanDamageBonus(int printedBaseDamage, int extraDamage, Creature? target)
    {
        int rawDamage = Math.Max(0, printedBaseDamage + GetDominionAmount() + extraDamage);
        return CalculateDisplayedShadowKhanDamage(rawDamage, target) - printedBaseDamage;
    }

    /// <summary>
    /// 计算手牌展示用的黑影兵团格挡，只吃影噬，不吃敏捷和脆弱。
    /// 当卡牌没有 Owner 时（如预览卡），返回印刷基础格挡值。
    /// </summary>
    protected decimal CalculateDisplayedShadowKhanBlock(int baseBlock)
    {
        return Math.Max(0, baseBlock + GetDominionAmount());
    }

    /// <summary>
    /// 计算卡面展示用的黑影兵团格挡；只有恶魔法典生效时才进入普通格挡预览链。
    /// </summary>
    /// <param name="block">已经包含影噬和牌自身额外值的原始格挡。</param>
    /// <returns>卡面应显示的最终格挡。</returns>
    protected decimal CalculateModifiedDisplayedShadowKhanBlock(int block)
    {
        int rawBlock = Math.Max(0, block);
        if (!IsDemonCodexActive())
        {
            return rawBlock;
        }

        var creature = Owner?.Creature;
        var combatState = CombatState ?? creature?.CombatState;
        if (creature == null || combatState == null)
        {
            return rawBlock;
        }

        decimal modifiedBlock = Hook.ModifyBlock(
            combatState,
            creature,
            rawBlock,
            ValueProp.Move,
            this,
            _currentCardPlay,
            out _);

        return Math.Max(0, Math.Floor(modifiedBlock));
    }

    /// <summary>
    /// 按当前 WithCalculatedBlock 写法，返回卡面应追加到印刷基础格挡上的展示加成。
    /// </summary>
    /// <param name="printedBaseBlock">印刷基础格挡。</param>
    /// <param name="extraBlock">除影噬外的牌自身额外格挡。</param>
    /// <returns>展示加成。</returns>
    protected decimal GetDisplayedShadowKhanBlockBonus(int printedBaseBlock, int extraBlock)
    {
        int rawBlock = Math.Max(0, printedBaseBlock + GetDominionAmount() + extraBlock);
        return CalculateModifiedDisplayedShadowKhanBlock(rawBlock) - printedBaseBlock;
    }

    /// <summary>
    /// 计算兵团卡实际伤害 = 卡面基础伤害 + 影噬层数。
    /// 不受力量/虚弱影响。
    /// </summary>
    /// <returns>最终伤害值。</returns>
    protected int CalculateShadowKhanDamage()
    {
        int dominion = GetDominionAmount();
        return Math.Max(0, PrintedBaseDamage + dominion);
    }

    /// <summary>
    /// 计算兵团卡实际格挡 = 卡面基础格挡 + 影噬层数。
    /// 不受敏捷/脆弱影响。
    /// </summary>
    /// <returns>最终格挡值。</returns>
    protected int CalculateShadowKhanBlock()
    {
        int dominion = GetDominionAmount();
        return Math.Max(0, PrintedBaseBlock + dominion);
    }

    /// <summary>
    /// 兵团卡专用攻击：只按“卡面基础伤害 + 影噬”结算，不吃力量和虚弱。
    /// 这里不再移除玩家身上的虚弱，避免玩家看到Buff闪烁。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <param name="target">攻击目标。</param>
    /// <returns>异步任务。</returns>
    protected async Task ShadowKhanAttack(PlayerChoiceContext choiceContext, Creature target)
    {
        await ShadowKhanAttack(choiceContext, target, CalculateShadowKhanDamage());
    }

    /// <summary>
    /// 黑影兵团专用攻击：使用指定伤害值结算，保持虚弱图标显示不变。
    /// </summary>
    protected async Task ShadowKhanAttack(PlayerChoiceContext choiceContext, Creature target, int damage)
    {
        if (IsDemonCodexActive())
        {
            await PoweredShadowKhanAttack(choiceContext, target, damage);
            return;
        }

        var creature = Owner?.Creature;
        bool targetHadBlackHand = target.HasPower<BlackHandPower>();
        int finalDamage = ApplyOxTalismanDamage(damage);
        int imbalanceAdjustedDamage = TalismanImbalancePlayerPower.ModifyCardDamageForPlayer(Owner, this, finalDamage);
        int flutterAdjustedDamage = ApplyFlutterDamageReduction(target, imbalanceAdjustedDamage, creature);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【黑影兵团攻击】卡牌={Id.Entry}，卡面基础伤害={PrintedBaseDamage}，影噬={GetDominionAmount()}，基础结算伤害={damage}，牛符咒后={finalDamage}，虎符咒失衡后={imbalanceAdjustedDamage}，最终伤害={flutterAdjustedDamage}");
        var damageResults = (await CreatureCmd.Damage(choiceContext, target, flutterAdjustedDamage, ValueProp.Move | ValueProp.Unpowered, creature, this, null)).ToList();
        await ResolveFlutterAfterShadowKhanAttack(choiceContext, target, damageResults, creature);
        if (targetHadBlackHand && !BlackHandCardHelper.IsLivingCreature(target))
        {
            await BountyPower.ResolveRewardForTarget(choiceContext, Owner, target, true);
        }

        await BlackHandCardHelper.ResolveBountyRewards(choiceContext, this);
    }

    /// <summary>
    /// 黑影兵团专用批量攻击：同一段伤害一次性结算多个敌人，减少群体多段攻击的等待时间。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <param name="targets">本段攻击候选目标。</param>
    /// <param name="damage">基础伤害+影噬后的伤害。</param>
    /// <returns>本段实际发起攻击的目标列表。</returns>
    protected async Task<IReadOnlyList<Creature>> ShadowKhanAttackMany(PlayerChoiceContext choiceContext, IEnumerable<Creature> targets, int damage)
    {
        List<Creature> livingTargets = targets.Where(BlackHandCardHelper.IsLivingCreature).ToList();
        if (livingTargets.Count == 0)
        {
            return [];
        }

        if (livingTargets.Count == 1)
        {
            await ShadowKhanAttack(choiceContext, livingTargets[0], damage);
            return livingTargets;
        }

        if (IsDemonCodexActive())
        {
            await PoweredShadowKhanAttackMany(choiceContext, livingTargets, damage);
            return livingTargets;
        }

        await UnpoweredShadowKhanAttackMany(choiceContext, livingTargets, damage);
        return livingTargets;
    }

    /// <summary>
    /// 批量结算未启用恶魔法典时的黑影兵团伤害，按最终伤害分组以兼容振翅等目标单独减伤。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <param name="targets">本段实际攻击目标。</param>
    /// <param name="damage">基础伤害+影噬后的伤害。</param>
    private async Task UnpoweredShadowKhanAttackMany(PlayerChoiceContext choiceContext, IReadOnlyList<Creature> targets, int damage)
    {
        var creature = Owner?.Creature;
        int finalDamage = ApplyOxTalismanDamage(damage);
        int imbalanceAdjustedDamage = TalismanImbalancePlayerPower.ModifyCardDamageForPlayer(Owner, this, finalDamage);
        var attackStates = targets
            .Select(target => new
            {
                Target = target,
                TargetHadBlackHand = target.HasPower<BlackHandPower>(),
                Damage = ApplyFlutterDamageReduction(target, imbalanceAdjustedDamage, creature)
            })
            .ToList();

        foreach (var damageGroup in attackStates.GroupBy(state => state.Damage))
        {
            List<Creature> groupTargets = damageGroup.Select(state => state.Target).ToList();
            ShengZhuLogHelper.VerboseCombatInfo(() => $"【黑影兵团群体攻击】卡牌={Id.Entry}，目标数={groupTargets.Count}，目标=[{string.Join(",", groupTargets.Select(target => target.Name))}]，卡面基础伤害={PrintedBaseDamage}，影噬={GetDominionAmount()}，基础结算伤害={damage}，牛符咒后={finalDamage}，虎符咒失衡后={imbalanceAdjustedDamage}，最终伤害={damageGroup.Key}");
            IReadOnlyList<DamageResult> damageResults = (await CreatureCmd.Damage(choiceContext, groupTargets, damageGroup.Key, ValueProp.Move | ValueProp.Unpowered, creature, this, null)).ToList();
            foreach (var state in damageGroup)
            {
                await ResolveFlutterAfterShadowKhanAttack(choiceContext, state.Target, damageResults, creature);
            }
        }

        foreach (var state in attackStates)
        {
            if (state.TargetHadBlackHand && !BlackHandCardHelper.IsLivingCreature(state.Target))
            {
                await BountyPower.ResolveRewardForTarget(choiceContext, Owner, state.Target, true);
            }
        }

        await BlackHandCardHelper.ResolveBountyRewards(choiceContext, this);
    }

    /// <summary>
    /// 恶魔法典生效时的黑影兵团攻击：基础伤害+影噬后，交给本体正常伤害链处理力量、虚弱、易伤等修正。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <param name="target">攻击目标。</param>
    /// <param name="damage">基础伤害+影噬后的伤害。</param>
    private async Task PoweredShadowKhanAttack(PlayerChoiceContext choiceContext, Creature target, int damage)
    {
        var creature = Owner?.Creature;
        bool targetHadBlackHand = target.HasPower<BlackHandPower>();
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【黑影兵团攻击】恶魔法典生效：卡牌={Id.Entry}，卡面基础伤害={PrintedBaseDamage}，影噬={GetDominionAmount()}，进入常规修正前伤害={damage}。");
        await CreatureCmd.Damage(choiceContext, target, damage, ValueProp.Move, creature, this, null);
        if (targetHadBlackHand && !BlackHandCardHelper.IsLivingCreature(target))
        {
            await BountyPower.ResolveRewardForTarget(choiceContext, Owner, target, true);
        }

        await BlackHandCardHelper.ResolveBountyRewards(choiceContext, this);
    }

    /// <summary>
    /// 批量结算恶魔法典生效时的黑影兵团伤害，交给常规伤害链处理力量、虚弱、易伤等修正。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <param name="targets">本段实际攻击目标。</param>
    /// <param name="damage">进入常规修正前的伤害。</param>
    private async Task PoweredShadowKhanAttackMany(PlayerChoiceContext choiceContext, IReadOnlyList<Creature> targets, int damage)
    {
        var creature = Owner?.Creature;
        var targetStates = targets
            .Select(target => new
            {
                Target = target,
                TargetHadBlackHand = target.HasPower<BlackHandPower>()
            })
            .ToList();

        ShengZhuLogHelper.VerboseCombatInfo(() => $"【黑影兵团群体攻击】恶魔法典生效：卡牌={Id.Entry}，目标数={targets.Count}，目标=[{string.Join(",", targets.Select(target => target.Name))}]，卡面基础伤害={PrintedBaseDamage}，影噬={GetDominionAmount()}，进入常规修正前伤害={damage}。");
        await CreatureCmd.Damage(choiceContext, targets, damage, ValueProp.Move, creature, this, null);

        foreach (var state in targetStates)
        {
            if (state.TargetHadBlackHand && !BlackHandCardHelper.IsLivingCreature(state.Target))
            {
                await BountyPower.ResolveRewardForTarget(choiceContext, Owner, state.Target, true);
            }
        }

        await BlackHandCardHelper.ResolveBountyRewards(choiceContext, this);
    }

    /// <summary>
    /// 黑影兵团牌不走普通力量/虚弱链路，所以这里单独补上牛符咒对“基础伤害+影噬”的翻倍。
    /// </summary>
    private int ApplyOxTalismanDamage(int damage)
    {
        OxTalisman? oxTalisman = Owner?.Relics.OfType<OxTalisman>().FirstOrDefault();
        if (oxTalisman == null || !oxTalisman.IsActivated || oxTalisman.CooldownTurnsRemaining > 0)
        {
            return damage;
        }

        int doubledDamage = damage * 2;
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【牛符咒】黑影兵团伤害翻倍参与计算：卡牌={Id.Entry}，翻倍前伤害={damage}，翻倍后伤害={doubledDamage}，影噬={GetDominionAmount()}");
        return doubledDamage;
    }

    /// <summary>
    /// 黑影兵团攻击不走普通力量/虚弱链路，但仍应被原版振翅按攻击伤害减半。
    /// </summary>
    private int ApplyFlutterDamageReduction(Creature target, int damage, Creature? dealer)
    {
        if (damage <= 0)
        {
            return damage;
        }

        FlutterPower? flutterPower = target.GetPower<FlutterPower>();
        if (flutterPower == null)
        {
            return damage;
        }

        decimal multiplier = flutterPower.ModifyDamageMultiplicative(target, damage, ValueProp.Move, dealer, this, null);
        int reducedDamage = Math.Max(0, (int)Math.Floor(damage * multiplier));
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【黑影兵团攻击】触发振翅减伤：卡牌={Id.Entry}，目标={target.Name}，减伤前={damage}，倍率={multiplier}，减伤后={reducedDamage}。");
        return reducedDamage;
    }

    /// <summary>
    /// 黑影兵团攻击造成未格挡伤害后，复用原版振翅自己的扣层/击落逻辑。
    /// </summary>
    private async Task ResolveFlutterAfterShadowKhanAttack(PlayerChoiceContext choiceContext, Creature target, IReadOnlyList<DamageResult> damageResults, Creature? dealer)
    {
        DamageResult? targetResult = damageResults.FirstOrDefault(result => ReferenceEquals(result.Receiver, target) && result.UnblockedDamage > 0);
        if (targetResult == null)
        {
            return;
        }

        FlutterPower? flutterPower = target.GetPower<FlutterPower>();
        if (flutterPower == null)
        {
            return;
        }

        ShengZhuLogHelper.VerboseCombatInfo(() => $"【黑影兵团攻击】触发振翅扣层：卡牌={Id.Entry}，目标={target.Name}，未格挡伤害={targetResult.UnblockedDamage}，当前振翅={flutterPower.Amount}。");
        await flutterPower.AfterDamageReceived(choiceContext, target, targetResult, ValueProp.Move, dealer, this);
    }

    /// <summary>
    /// 兵团卡专用格挡：使用固定数值获得格挡，绕过敏捷/脆弱。
    /// 格挡 = 卡面基础格挡 + 影噬层数。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <returns>异步任务。</returns>
    protected async Task ShadowKhanBlock(PlayerChoiceContext choiceContext, CardPlay? cardPlay = null)
    {
        await ShadowKhanBlock(choiceContext, PrintedBaseBlock, cardPlay);
    }

    protected async Task ShadowKhanBlock(PlayerChoiceContext choiceContext, int baseBlock, CardPlay? cardPlay = null)
    {
        int block = Math.Max(0, baseBlock + GetDominionAmount());
        if (IsDemonCodexActive())
        {
            if (block > 0)
            {
                ShengZhuLogHelper.VerboseCombatInfo(() => $"【黑影兵团格挡】恶魔法典生效：卡牌={Id.Entry}，基础格挡={baseBlock}，影噬={GetDominionAmount()}，进入常规修正前格挡={block}。");
                await CreatureCmd.GainBlock(Owner!.Creature!, block, ValueProp.Move, cardPlay ?? _currentCardPlay);
            }
            return;
        }

        block = TalismanImbalancePlayerPower.ModifyCardBlockForPlayer(Owner, this, block);
        if (block > 0)
        {
            await CreatureCmd.GainBlock(Owner!.Creature!, block, default, cardPlay ?? _currentCardPlay);
        }
    }
}
