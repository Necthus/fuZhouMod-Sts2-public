using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using System.Runtime.CompilerServices;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Demons;

/// <summary>
/// 火之恶魔·小龙：给所有敌人施加灼烧，遇潮湿时合成为高温蒸汽。
/// </summary>
public class CardFireDemonQi : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造火魔气卡牌数值。
    /// </summary>
    public CardFireDemonQi() : base(1, CardType.Skill, CardRarity.Event, TargetType.AllEnemies)
    {
        WithVar("Magic", 2, 1);
    }

    /// <summary>
    /// 出牌时对所有可命中敌人施加灼烧。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature?.CombatState == null)
        {
            return;
        }

        int amount = (int)DynamicVars["Magic"].BaseValue;
        foreach (Creature enemy in Owner.Creature.CombatState.HittableEnemies)
        {
            await PanKuDemonQiHelper.ApplyBurning(enemy, amount, Owner.Creature, this);
        }
    }
}

/// <summary>
/// 山之恶魔·波刚：造成等同当前格挡的伤害，击杀后增加最大生命。
/// </summary>
public class CardMountainDemonQi : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造山魔气卡牌数值。
    /// </summary>
    public CardMountainDemonQi() : base(1, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy)
    {
        WithVar("Magic", 1, 1);
        WithKeywords(CardKeyword.Retain);
    }

    /// <summary>
    /// 出牌时按玩家当前格挡造成伤害，击杀目标后增加最大生命。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null || cardPlay.Target == null)
        {
            return;
        }

        int damage = Math.Max(0, Owner.Creature.Block);
        if (damage <= 0)
        {
            return;
        }

        await CreatureCmd.Damage(choiceContext, cardPlay.Target, damage, ValueProp.Move, Owner.Creature, this, cardPlay);
        if (!cardPlay.Target.IsAlive)
        {
            int maxHpGain = Math.Max(1, (int)DynamicVars["Magic"].BaseValue);
            await CreatureCmd.GainMaxHp(Owner.Creature, maxHpGain);
            MainFile.Logger.Info($"【山之恶魔】击杀目标，最大生命增加：{maxHpGain}。");
        }
    }
}

/// <summary>
/// 风之恶魔·啸风：抽牌弃牌，并给所有敌人风势。
/// </summary>
public class CardWindDemonQi : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造风魔气卡牌数值。
    /// </summary>
    public CardWindDemonQi() : base(1, CardType.Skill, CardRarity.Event, TargetType.Self)
    {
        WithVar("Magic", 3, 1);
        WithVar("DiscardCnt", 1);
        WithVar("WindAmt", 2, 1);
    }

    /// <summary>
    /// 出牌时抽牌、手动弃牌，并给敌人施加风势。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature?.CombatState == null)
        {
            return;
        }

        await CardPileCmd.Draw(choiceContext, (int)DynamicVars["Magic"].BaseValue, Owner);
        int discardCount = Math.Min((int)DynamicVars["DiscardCnt"].BaseValue, Owner.PlayerCombatState?.Hand.Cards.Count ?? 0);
        if (discardCount > 0)
        {
            IReadOnlyList<CardModel> selectedCards = (await CommonActions.SelectCards(
                this,
                new("cards", Id.Entry + ".selectionScreenPrompt"),
                choiceContext,
                PileType.Hand,
                discardCount)).ToList();

            foreach (CardModel selectedCard in selectedCards)
            {
                await CardCmd.Discard(choiceContext, selectedCard);
            }
        }

        int windAmount = (int)DynamicVars["WindAmt"].BaseValue;
        foreach (Creature enemy in Owner.Creature.CombatState.HittableEnemies)
        {
            await PowerCmd.Apply<WindCatalystPower>(enemy, windAmount, Owner.Creature, this);
        }
    }
}

/// <summary>
/// 水之恶魔·芭莎：群体攻击并施加潮湿，遇火异常时合成为高温蒸汽。
/// </summary>
public class CardWaterDemonQi : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造水魔气卡牌数值。
    /// </summary>
    public CardWaterDemonQi() : base(1, CardType.Attack, CardRarity.Event, TargetType.AllEnemies)
    {
        WithDamage(4, 2);
        WithVar("Magic", 2, 1);
    }

    /// <summary>
    /// 出牌时先造成群体伤害，再对每个敌人施加潮湿或冻伤。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature?.CombatState == null)
        {
            return;
        }

        IReadOnlyList<Creature> enemies = Owner.Creature.CombatState.HittableEnemies.ToList();
        await CreatureCmd.Damage(choiceContext, enemies, DynamicVars.Damage.BaseValue, ValueProp.Move, Owner.Creature, this, cardPlay);

        int amount = (int)DynamicVars["Magic"].BaseValue;
        foreach (Creature enemy in enemies.Where(enemy => enemy.IsAlive))
        {
            await PanKuDemonQiHelper.ApplySoaked(enemy, amount, Owner.Creature, this);
        }

        if (StableRandomHelper.NextInt(Owner, 100) < 20)
        {
            int goldGain = StableRandomHelper.NextIntInclusive(Owner, 5, 20);
            await PlayerCmd.GainGold(goldGain, Owner);
            MainFile.Logger.Info($"【水之恶魔】小概率获得金币：{goldGain}。");
        }
    }
}

/// <summary>
/// 雷之恶魔·中苏：单体攻击，每场战斗越用越低费，并按每场上限永久成长伤害；多张中苏各自独立成长。
/// </summary>
public class CardThunderDemonQi : ShengZhuSts2ModCard
{
    /// <summary>
    /// 未升级时的基础伤害。
    /// </summary>
    private const int BaseDamage = 5;

    /// <summary>
    /// 每升一级提升的伤害。
    /// </summary>
    private const int UpgradeDamageBonus = 3;

    /// <summary>
    /// 每场战斗最多获得的永久伤害成长次数。
    /// </summary>
    private const int MaxGrowthPerCombat = 3;

    /// <summary>
    /// 每场战斗内的战斗牌与主牌组原卡绑定关系，避免多张中苏互相串成长。
    /// </summary>
    private static readonly ConditionalWeakTable<ICombatState, ThunderDemonGrowthBindings> GrowthBindings = new();

    /// <summary>
    /// 当前战斗内已经成长的次数，切换战斗时清零。
    /// </summary>
    private int _growthThisCombat;

    /// <summary>
    /// 当前计数所属战斗，避免上一场战斗的成长次数影响下一场。
    /// </summary>
    private ICombatState? _trackedCombatState;

    /// <summary>
    /// 当前战斗临时牌绑定到的主牌组原卡。
    /// </summary>
    private CardThunderDemonQi? _growthDeckSource;

    /// <summary>
    /// 已获得的永久伤害加成（真值来源），由属性 setter 统一维护。
    /// </summary>
    private int _permanentDamageBonus;

    /// <summary>
    /// 已获得的永久伤害加成，写入存档实现跨战斗持久化。
    /// setter 内同步刷新牌面伤害显示，做到“所见即所得”。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int PermanentDamageBonus
    {
        get => _permanentDamageBonus;
        set
        {
            _permanentDamageBonus = Math.Max(0, value);
            RefreshDamageDisplay();
        }
    }

    /// <summary>
    /// 构造雷魔气卡牌数值。
    /// </summary>
    public CardThunderDemonQi() : base(2, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy)
    {
        WithDamage(BaseDamage, UpgradeDamageBonus);
        WithVar("Magic", MaxGrowthPerCombat);
    }

    /// <summary>
    /// 按“引擎基础伤害（含升级）+ 永久成长”重算牌面伤害基础值，使成长在牌面可见。
    /// 这里用绝对赋值：不依赖当前值、不触发升级高亮；与引擎升级的增量加成天然兼容。
    /// </summary>
    private void RefreshDamageDisplay()
    {
        DynamicVars.Damage.BaseValue = CalculateCurrentBaseDamage();
    }

    /// <summary>
    /// 计算当前这张雷之恶魔的基础伤害（含升级和永久成长）。
    /// </summary>
    /// <returns>当前基础伤害。</returns>
    private int CalculateCurrentBaseDamage()
    {
        return BaseDamage + UpgradeDamageBonus * CurrentUpgradeLevel + _permanentDamageBonus;
    }

    /// <summary>
    /// 单张主牌组原卡的一次永久成长：本场未达上限则 +1（经 setter 刷新显示和存档值）。
    /// </summary>
    /// <param name="combatState">当前战斗状态，用于判断本场成长次数。</param>
    /// <returns>实际成长时返回 true。</returns>
    private bool GrowPermanentDamage(ICombatState? combatState)
    {
        EnsureCombatGrowthState(combatState);
        if (_growthThisCombat >= MaxGrowthPerCombat)
        {
            MainFile.Logger.Info($"【雷之恶魔】本场成长已到上限：主牌组原卡={BuildDebugTag(this)}，永久成长+{PermanentDamageBonus}，本场成长={_growthThisCombat}/{MaxGrowthPerCombat}。");
            return false;
        }

        _growthThisCombat++;
        PermanentDamageBonus++;
        MainFile.Logger.Info($"【雷之恶魔】永久伤害成长：主牌组原卡={BuildDebugTag(this)}，永久成长+{PermanentDamageBonus}，本场成长={_growthThisCombat}/{MaxGrowthPerCombat}。");
        return true;
    }

    /// <summary>
    /// 切换战斗时清空本场成长计数。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    private void EnsureCombatGrowthState(ICombatState? combatState)
    {
        if (combatState == null || ReferenceEquals(_trackedCombatState, combatState))
        {
            return;
        }

        _trackedCombatState = combatState;
        _growthThisCombat = 0;
    }

    /// <summary>
    /// 从主牌组原卡同步永久成长到当前战斗临时牌，保证本次伤害和牌面显示一致。
    /// </summary>
    /// <param name="source">主牌组原卡。</param>
    private void SyncPermanentGrowthFrom(CardThunderDemonQi source)
    {
        if (ReferenceEquals(this, source))
        {
            return;
        }

        _permanentDamageBonus = source.PermanentDamageBonus;
        RefreshDamageDisplay();
    }

    /// <summary>
    /// 找到当前战斗临时牌对应的主牌组原卡，用于保存永久成长。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <returns>能找到时返回主牌组原卡，否则返回当前牌作为兜底。</returns>
    private CardThunderDemonQi ResolveGrowthSource(ICombatState? combatState)
    {
        CardThunderDemonQi? directSource = ResolveDirectGrowthSource();
        if (directSource != null)
        {
            RememberGrowthSource(combatState, directSource);
            return directSource;
        }

        if (_growthDeckSource != null && IsInOwnerDeck(_growthDeckSource))
        {
            return _growthDeckSource;
        }

        CardThunderDemonQi? fallbackSource = ResolveFallbackGrowthSource(combatState);
        if (fallbackSource != null)
        {
            RememberGrowthSource(combatState, fallbackSource);
            return fallbackSource;
        }

        MainFile.Logger.Info($"【雷之恶魔】未找到主牌组原卡，永久成长只能暂存在当前牌：当前卡={BuildDebugTag(this)}。");
        return this;
    }

    /// <summary>
    /// 优先通过引擎直接提供的来源关系寻找主牌组原卡。
    /// </summary>
    /// <returns>能确认在主牌组中的来源卡时返回该卡。</returns>
    private CardThunderDemonQi? ResolveDirectGrowthSource()
    {
        if (DeckVersion is CardThunderDemonQi deckVersion && IsInOwnerDeck(deckVersion))
        {
            return deckVersion;
        }

        CardModel? cloneSource = CloneOf;
        while (cloneSource != null)
        {
            if (cloneSource is CardThunderDemonQi thunderSource && IsInOwnerDeck(thunderSource))
            {
                return thunderSource;
            }

            cloneSource = cloneSource.CloneOf;
        }

        return IsInOwnerDeck(this) ? this : null;
    }

    /// <summary>
    /// 在引擎没有给出来源关系时，按当前战斗绑定表从主牌组中选一张未绑定的中苏作为兜底。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <returns>兜底匹配到的主牌组原卡。</returns>
    private CardThunderDemonQi? ResolveFallbackGrowthSource(ICombatState? combatState)
    {
        IReadOnlyList<CardThunderDemonQi> deckSources = GetOwnerDeckThunderCards();
        if (deckSources.Count == 0)
        {
            return null;
        }

        if (combatState == null)
        {
            return deckSources.FirstOrDefault();
        }

        ThunderDemonGrowthBindings bindings = GrowthBindings.GetValue(combatState, static _ => new ThunderDemonGrowthBindings());
        if (bindings.SourcesByCombatCard.TryGetValue(this, out CardThunderDemonQi? boundSource) && IsInOwnerDeck(boundSource))
        {
            return boundSource;
        }

        CardThunderDemonQi? source = deckSources.FirstOrDefault(card => !bindings.ClaimedSources.Contains(card))
                                      ?? deckSources.FirstOrDefault();
        if (source != null)
        {
            bindings.SourcesByCombatCard[this] = source;
            bindings.ClaimedSources.Add(source);
        }

        return source;
    }

    /// <summary>
    /// 记录当前战斗牌和主牌组原卡的绑定，后续反复打出同一张战斗牌时继续回写同一张原卡。
    /// </summary>
    /// <param name="combatState">当前战斗状态。</param>
    /// <param name="source">主牌组原卡。</param>
    private void RememberGrowthSource(ICombatState? combatState, CardThunderDemonQi source)
    {
        _growthDeckSource = source;
        if (combatState == null)
        {
            return;
        }

        ThunderDemonGrowthBindings bindings = GrowthBindings.GetValue(combatState, static _ => new ThunderDemonGrowthBindings());
        bindings.SourcesByCombatCard[this] = source;
        bindings.ClaimedSources.Add(source);
    }

    /// <summary>
    /// 获取当前玩家主牌组中的所有雷之恶魔·中苏。
    /// </summary>
    /// <returns>主牌组中的中苏卡列表。</returns>
    private IReadOnlyList<CardThunderDemonQi> GetOwnerDeckThunderCards()
    {
        try
        {
            return Owner?.Deck.Cards.OfType<CardThunderDemonQi>().ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 判断卡牌是否仍在其拥有者的主牌组里。
    /// </summary>
    /// <param name="card">待检查卡牌。</param>
    /// <returns>在主牌组中时返回 true。</returns>
    private static bool IsInOwnerDeck(CardThunderDemonQi card)
    {
        try
        {
            return card.Owner?.Deck.Cards.Contains(card) == true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 生成雷之恶魔的调试标识，便于区分多张同名卡。
    /// </summary>
    /// <param name="card">要输出的卡牌对象。</param>
    /// <returns>可区分对象的调试标识。</returns>
    private static string BuildDebugTag(CardThunderDemonQi? card)
    {
        if (card == null)
        {
            return "null";
        }

        return $"{card.Id.Entry}#{RuntimeHelpers.GetHashCode(card):X8}";
    }

    /// <summary>
    /// 降级后重算牌面伤害，避免引擎重置基础值后丢失已有的永久成长显示。
    /// </summary>
    protected override void AfterDowngraded()
    {
        RefreshDamageDisplay();
    }

    /// <summary>
    /// 升级后重算牌面伤害，避免升级流程覆盖已有永久成长显示。
    /// </summary>
    protected override void OnUpgrade()
    {
        RefreshDamageDisplay();
    }

    /// <summary>
    /// 读档后重算牌面伤害，确保主牌组预览能显示存档里的永久成长。
    /// </summary>
    protected override void AfterDeserialized()
    {
        RefreshDamageDisplay();
    }

    /// <summary>
    /// 给卡牌描述补充当前永久成长数值，方便玩家直接看懂这张牌现在长到了多少。
    /// </summary>
    /// <param name="description">卡牌描述对象。</param>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        description.Add("Growth", PermanentDamageBonus);
    }

    /// <summary>
    /// 出牌时按当前牌面伤害结算（潮湿/蒸汽翻倍），随后本场降费并尝试永久成长。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null || cardPlay.Target == null)
        {
            return;
        }

        ICombatState? combatState = Owner.Creature.CombatState;
        CardThunderDemonQi growthSource = ResolveGrowthSource(combatState);
        SyncPermanentGrowthFrom(growthSource);

        // 用当前（成长前）的牌面伤害结算，确保这一发不吃本次成长的即时加成。
        int damage = (int)DynamicVars.Damage.BaseValue;
        bool hasSoaked = cardPlay.Target.GetPower<SoakedPower>() != null;
        bool hasSteam = cardPlay.Target.GetPower<SteamPower>() != null;
        decimal damageMultiplier = (hasSoaked || hasSteam) ? 2m : 1m;
        int finalDamage = (int)Math.Floor(damage * damageMultiplier);
        MainFile.Logger.Info($"【雷之恶魔】出牌结算：当前卡={BuildDebugTag(this)}，主牌组原卡={BuildDebugTag(growthSource)}，目标={cardPlay.Target.Name}，基础伤害={damage}，永久成长={PermanentDamageBonus}，潮湿={hasSoaked}，高温蒸汽={hasSteam}，倍率={damageMultiplier:0.0}x，最终伤害={finalDamage}。");
        if (finalDamage != damage)
        {
            damage = finalDamage;
        }

        await CreatureCmd.Damage(choiceContext, cardPlay.Target, damage, ValueProp.Move, Owner.Creature, this, cardPlay);
        EnergyCost.AddThisCombat(-1, true);

        // 永久成长只写主牌组原卡；当前战斗临时牌同步显示，避免只长在临时对象上导致预览和SL丢值。
        bool grew = growthSource.GrowPermanentDamage(combatState);
        SyncPermanentGrowthFrom(growthSource);
        Owner.PlayerCombatState?.RecalculateCardValues();
        MainFile.Logger.Info($"【雷之恶魔】出牌结束：当前卡={BuildDebugTag(this)}，主牌组原卡={BuildDebugTag(growthSource)}，是否成长={grew}，当前永久成长+{PermanentDamageBonus}。");
    }

    /// <summary>
    /// 记录每场战斗内战斗牌和主牌组原卡的绑定关系。
    /// </summary>
    private sealed class ThunderDemonGrowthBindings
    {
        /// <summary>
        /// 战斗临时牌到主牌组原卡的映射。
        /// </summary>
        public Dictionary<CardThunderDemonQi, CardThunderDemonQi> SourcesByCombatCard { get; } = new(ReferenceEqualityComparer.Instance);

        /// <summary>
        /// 本场已经被绑定过的主牌组原卡，用于多张中苏时尽量保持各自独立。
        /// </summary>
        public HashSet<CardThunderDemonQi> ClaimedSources { get; } = new(ReferenceEqualityComparer.Instance);
    }
}

/// <summary>
/// 地之恶魔·地魁：获得格挡保留、无格挡补偿和大地守望。
/// </summary>
public class CardEarthDemonQi : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造地魔气卡牌数值。
    /// </summary>
    public CardEarthDemonQi() : base(2, CardType.Power, CardRarity.Event, TargetType.Self)
    {
        WithVar("Magic", 3, -1);
    }

    /// <summary>
    /// 出牌时赋予玩家地之恶魔相关能力。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        await PowerCmd.Apply<BarricadePower>(Owner.Creature, 1, Owner.Creature, this);
        await PowerCmd.Apply<EarthDemonPower>(Owner.Creature, Math.Max(1, (int)DynamicVars["Magic"].BaseValue), Owner.Creature, this);
        await PowerCmd.Apply<EarthPrisonPrepPower>(Owner.Creature, 3, Owner.Creature, this);
    }
}

/// <summary>
/// 天空恶魔·西木：攻击时额外触发目标身上的火冰异常。
/// </summary>
public class CardHeavenDemonQi : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造天魔气卡牌数值。
    /// </summary>
    public CardHeavenDemonQi() : base(2, CardType.Power, CardRarity.Event, TargetType.Self)
    {
    }

    /// <summary>
    /// 升级时把基础费用从 2 降到 1。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时赋予天空恶魔能力。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature != null)
        {
            await PowerCmd.Apply<HeavenDemonPower>(Owner.Creature, 1, Owner.Creature, this);
        }
    }
}

/// <summary>
/// 月之恶魔·咒蓝：从反转、解除限制等月之效果中选择一项。
/// </summary>
public class CardMoonDemonQi : ShengZhuSts2ModCard
{
    /// <summary>
    /// 构造月魔气卡牌数值。
    /// </summary>
    public CardMoonDemonQi() : base(2, CardType.Skill, CardRarity.Event, TargetType.None)
    {
        WithKeywords(CardKeyword.Exhaust);
    }

    /// <summary>
    /// 升级时把基础费用从 2 降到 1。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    /// <summary>
    /// 出牌时选择月之恶魔效果。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Creature == null || Owner.Creature.CombatState == null)
        {
            return;
        }

        CardModel? choice = await SelectMoonChoice(choiceContext);
        switch (choice)
        {
            case MoonChoiceSelfPreviewCard:
                await InvertNonZeroStrengthAndDexterity(Owner.Creature, Owner.Creature, this);
                MainFile.Logger.Info($"【月之恶魔】选择反转自己：目标={Owner.Creature.Name}。");
                break;
            case MoonChoiceEnemyPreviewCard:
                await AddMoonTokenToHand<MoonInvertEnemyTokenCard>();
                MainFile.Logger.Info("【月之恶魔】选择反转敌人：已将敌人反转代币加入当前玩家手牌。");
                break;
            case MoonChoiceUncapPreviewCard:
                await PowerCmd.Apply<MoonUncapPower>(Owner.Creature, 1, Owner.Creature, this);
                MainFile.Logger.Info($"【月之恶魔】选择解除限制：持有者={Owner.Creature.Name}，本轮按队伍共享跳过恶魔异常伤害上限。");
                break;
            case MoonChoiceAllyPreviewCard:
                await AddMoonTokenToHand<MoonInvertAllyTokenCard>();
                MainFile.Logger.Info("【月之恶魔】选择反转队友：已将队友反转代币加入当前玩家手牌。");
                break;
            default:
                MainFile.Logger.Info("【月之恶魔】玩家取消选择，未结算其他效果。");
                break;
        }
    }

    /// <summary>
    /// 月之恶魔卡片专用反转：力量和敏捷只要不是 0，就改成相反数；月魔气附魔不使用此方法。
    /// </summary>
    /// <param name="target">需要反转数值的目标。</param>
    /// <param name="source">效果来源生物。</param>
    /// <param name="cardSource">来源卡牌。</param>
    public static async Task InvertNonZeroStrengthAndDexterity(Creature? target, Creature? source, CardModel? cardSource)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }

        await InvertNonZeroPower<StrengthPower>(target, source, cardSource, "力量");
        await InvertNonZeroPower<DexterityPower>(target, source, cardSource, "敏捷");
    }

    /// <summary>
    /// 反转指定能力的非零层数，供月之恶魔卡片聚合复用。
    /// </summary>
    /// <typeparam name="TPower">力量或敏捷能力类型。</typeparam>
    /// <param name="target">需要反转数值的目标。</param>
    /// <param name="source">效果来源生物。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <param name="powerName">日志用中文能力名。</param>
    private static async Task InvertNonZeroPower<TPower>(Creature target, Creature? source, CardModel? cardSource, string powerName)
        where TPower : PowerModel
    {
        TPower? power = target.GetPower<TPower>();
        if (power == null || power.Amount == 0)
        {
            return;
        }

        int oldAmount = power.Amount;
        await PowerCmd.ModifyAmount(power, -oldAmount * 2, source, cardSource);
        MainFile.Logger.Info($"【月之恶魔】反转{powerName}：目标={target.Name}，{powerName}={oldAmount}->{-oldAmount}。");
    }

    /// <summary>
    /// 展示月之恶魔效果选项并返回玩家选择。
    /// </summary>
    /// <param name="choiceContext">选择上下文。</param>
    /// <returns>被选中的预览卡；取消时返回 null。</returns>
    private async Task<CardModel?> SelectMoonChoice(PlayerChoiceContext choiceContext)
    {
        ICombatState combatState = Owner!.Creature!.CombatState!;
        List<CardModel> choices =
        [
            combatState.CreateCard<MoonChoiceSelfPreviewCard>(Owner),
            combatState.CreateCard<MoonChoiceEnemyPreviewCard>(Owner),
            combatState.CreateCard<MoonChoiceUncapPreviewCard>(Owner)
        ];

        if (Owner.RunState.Players.Count > 1)
        {
            choices.Add(combatState.CreateCard<MoonChoiceAllyPreviewCard>(Owner));
        }

        CardSelectorPrefs prefs = new(new LocString("cards", Id.Entry + ".selectionScreenPrompt"), 1, 1);

        return (await CardSelectCmd.FromSimpleGrid(choiceContext, choices, Owner, prefs)).FirstOrDefault();
    }

    /// <summary>
    /// 生成月之恶魔专属临时代币并加入当前玩家手牌。
    /// </summary>
    /// <typeparam name="TCard">代币卡类型。</typeparam>
    private async Task AddMoonTokenToHand<TCard>()
        where TCard : CardModel, new()
    {
        CardModel token = Owner!.Creature!.CombatState!.CreateCard<TCard>(Owner);
        await CardPileCmdHelper.AddGeneratedCardToCombat(token, PileType.Hand, addedByPlayer: true);
    }
}
