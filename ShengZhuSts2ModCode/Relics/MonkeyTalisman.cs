using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 猴符咒：在休息处提供独立的"七十二变"选项，且只允许自己使用。
/// 选择卡组中一张牌，将其变换为当前职业卡池中自选的任意一张牌。
/// 每次获取后限 3 次使用，用完后不再出现休息处选项。
/// 鼠符咒共鸣：战斗中可右键激发一次"七十二变"（仅自己可用，变换手牌中1张牌为职业池中自选牌）。
/// </summary>
public class MonkeyTalisman : ShengZhuSts2ModRelic, ITalismanRightClickable, ITalismanInteractionState
{
    /// <summary>
    /// 七十二变允许出现的常规目标牌稀有度。
    /// </summary>
    private static readonly HashSet<CardRarity> AllowedReplacementRarities =
    [
        CardRarity.Basic,
        CardRarity.Common,
        CardRarity.Uncommon,
        CardRarity.Rare
    ];

    /// <summary>
    /// 七十二变最大可用次数。
    /// </summary>
    public const int MaxTransformUses = 3;

    /// <summary>
    /// 休息处七十二变成功后回复的最大生命百分比。
    /// </summary>
    private const int RestSiteTransformHealPercent = 20;

    /// <summary>
    /// 剩余可用次数。
    /// </summary>
    private int _remainingUses = MaxTransformUses;

    /// <summary>
    /// 剩余七十二变次数，写入存档，避免SL后恢复默认次数。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int RemainingUses
    {
        get => _remainingUses;
        set => _remainingUses = Math.Clamp(value, 0, MaxTransformUses);
    }

    /// <summary>
    /// 本场战斗是否已使用过战斗内七十二变（鼠符咒共鸣）。
    /// </summary>
    private bool _usedInCombat;

    /// <summary>
    /// 本场战斗是否已使用过战斗内七十二变，写入存档和联机状态，避免重复使用判断分歧。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public bool UsedInCombat
    {
        get => _usedInCombat;
        set => _usedInCombat = value;
    }

    /// <summary>
    /// 是否正在执行战斗内七十二变流程（防止重复右键触发重入）。
    /// </summary>
    private bool _isProcessing;

    /// <summary>
    /// 遗物稀有度：稀有。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Rare;

    /// <summary>
    /// 显示剩余次数计数器。
    /// </summary>
    public override bool ShowCounter => true;

    /// <summary>
    /// 计数器显示值：剩余可用次数。
    /// </summary>
    public override int DisplayAmount => RemainingUses;

    /// <summary>
    /// 当前是否处于可激发状态（鼠符咒共鸣且本场未使用过）。
    /// </summary>
    public bool IsActivated => false;

    /// <summary>
    /// 猴符咒无冷却。
    /// </summary>
    public int CooldownTurnsRemaining => 0;

    /// <summary>
    /// 战斗开始时重置战斗内使用标记。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        _usedInCombat = false;
        _isProcessing = false;
        // 有鼠符咒共鸣时显示为可用状态
        if (HasRatResonance())
        {
            UpdateTalismanVisualState(false, 0);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 右键点击：鼠符咒共鸣时在战斗中触发七十二变（变换手牌中1张牌）。
    /// </summary>
    public async Task OnRightClick(PlayerChoiceContext context)
    {
        if (!IsLocalPlayer(Owner))
        {
            MainFile.Logger.Info($"【猴符咒】忽略非本地玩家的右键请求：持有者={Owner?.NetId.ToString() ?? "空"}。");
            return;
        }

        await RelicInteractionSyncService.RequestMonkeyCombatTransform(this);
    }

    /// <summary>
    /// 执行同步后的战斗内七十二变。
    /// </summary>
    /// <param name="context">玩家选择上下文。</param>
    /// <returns>异步任务。</returns>
    internal async Task RunSyncedCombatTransform(PlayerChoiceContext context)
    {
        // 只有鼠符咒共鸣时才能在战斗中使用
        if (!HasRatResonance() || _usedInCombat || _isProcessing || Owner?.PlayerCombatState == null)
        {
            return;
        }

        var combatState = Owner.PlayerCombatState;
        if (combatState.Hand.Cards.Count == 0)
        {
            return;
        }

        MainFile.Logger.Info($"【联机同步】【猴符咒】玩家={Owner.NetId}，动作=准备战斗内七十二变，手牌候选数={combatState.Hand.Cards.Count}，候选ID=[{FormatCardIds(combatState.Hand.Cards)}]。");
        _isProcessing = true;
        try
        {
            // 第一步：从手牌中选择要变换的牌（可取消）
            CardSelectorPrefs targetPrefs = new(
                new LocString("relics", "SHENGZHUSTS2MOD-MONKEY_TALISMAN.combatSelectTarget"), 0, 1)
            {
                Cancelable = true,
                RequireManualConfirmation = true
            };

            IReadOnlyList<CardModel> targetSelection = (await CardSelectCmd.FromHand(
                context, Owner, targetPrefs, null!, null!)).ToList();
            var targetCard = targetSelection.FirstOrDefault();

            if (targetCard == null)
            {
                MainFile.Logger.Info($"【联机同步】【猴符咒】玩家={Owner.NetId}，动作=选择原牌，选中=取消。");
                MainFile.Logger.Info("【猴符咒】玩家取消了战斗内七十二变（第一步：选择手牌）。");
                return;
            }

            MainFile.Logger.Info($"【联机同步】【猴符咒】玩家={Owner.NetId}，动作=选择原牌，选中={FormatCardKey(targetCard)}。");
            // 第二步：从职业卡池中选择新牌（点击即选中，Cancelable支持取消）
            List<CardModel> replacementPool = BuildReplacementPool();
            MainFile.Logger.Info($"【联机同步】【猴符咒】玩家={Owner.NetId}，动作=准备选择新牌，候选数={replacementPool.Count}，候选ID=[{FormatCardIds(replacementPool)}]。");
            if (replacementPool.Count == 0)
            {
                MainFile.Logger.Info("【猴符咒】当前职业卡池为空，战斗内七十二变不可用。");
                return;
            }

            CardSelectorPrefs replacementPrefs = new(
                new LocString("relics", "SHENGZHUSTS2MOD-MONKEY_TALISMAN.combatSelectReplacement"),
                1,
                1)
            {
                Cancelable = true
            };

            IReadOnlyList<CardModel> replacementSelection = (await CardSelectCmd.FromSimpleGrid(
                context,
                replacementPool,
                Owner,
                replacementPrefs)).ToList();
            CardModel? newCard = replacementSelection.FirstOrDefault();

            if (newCard == null)
            {
                MainFile.Logger.Info($"【联机同步】【猴符咒】玩家={Owner.NetId}，动作=选择新牌，选中=取消。");
                MainFile.Logger.Info("【猴符咒】玩家取消了战斗内七十二变（第二步：选择新牌）。");
                return;
            }

            MainFile.Logger.Info($"【联机同步】【猴符咒】玩家={Owner.NetId}，动作=选择新牌，选中={FormatCardKey(newCard)}。");
            // 执行变换：消耗原牌，将新牌加入手牌
            _usedInCombat = true;
            UpdateTalismanVisualState(false, 1);
            Flash();

            // 继承升级状态：若原牌已升级且新牌可升级，则自动升级新牌
            if (targetCard.IsUpgraded && newCard.IsUpgradable)
            {
                CardCmd.Upgrade(newCard);
            }

            await CardCmd.Exhaust(context, targetCard);
            await CardPileCmdHelper.AddGeneratedCardToCombat(newCard, PileType.Hand, true);

            MainFile.Logger.Info($"【猴符咒】战斗内七十二变完成：{targetCard.Title} → {newCard.Title}");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 在休息处选项列表中注入"七十二变"选项。
    /// 当剩余次数为0时不再注入。
    /// </summary>
    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (Owner == null || player.NetId != Owner.NetId)
        {
            MainFile.Logger.Info($"【联机同步】【猴符咒】跳过非持有者休息处选项注入：选项玩家={player.NetId}，持有者={Owner?.NetId.ToString() ?? "空"}。");
            return false;
        }

        if (RemainingUses > 0 && MonkeyTalismanRestSiteOption.HasTransformTargetCard(player))
        {
            options.Add(new MonkeyTalismanRestSiteOption(player));
            return true;
        }

        return false;
    }

    /// <summary>
    /// 消耗一次七十二变使用次数，并刷新计数器显示。
    /// </summary>
    public void ConsumeTransformUse()
    {
        if (RemainingUses > 0)
        {
            RemainingUses--;
            InvokeDisplayAmountChanged();
            MainFile.Logger.Info($"【猴符咒】消耗一次七十二变，剩余次数：{RemainingUses}");
        }
    }

    /// <summary>
    /// 执行同步后的休息处七十二变：按牌组下标和卡牌ID重放同一次永久变牌。
    /// </summary>
    /// <param name="payload">休息处七十二变同步数据。</param>
    /// <returns>执行成功时返回 true。</returns>
    internal async Task<bool> RunSyncedRestSiteTransform(MonkeyRestTransformPayload payload)
    {
        if (Owner?.Creature == null)
        {
            MainFile.Logger.Info("【联机异常兜底】【猴符咒】休息处七十二变重放失败：持有者或生物为空，处理=跳过。");
            return false;
        }

        if (RemainingUses <= 0)
        {
            MainFile.Logger.Info($"【联机异常兜底】【猴符咒】休息处七十二变重放失败：玩家={Owner.NetId}，剩余次数={RemainingUses}，处理=跳过。");
            return false;
        }

        if (!DeckSyncHelper.TryGetDeckCard(Owner, payload.TargetDeckIndex, payload.TargetCardId, payload.TargetWasUpgraded, "猴符咒", out CardModel? targetCard) || targetCard == null)
        {
            return false;
        }

        CardModel? replacementPrototype = DeckSyncHelper.FindCardPrototype(payload.ReplacementCardId);
        if (replacementPrototype == null || !IsReplacementCard(replacementPrototype, Owner))
        {
            MainFile.Logger.Info($"【联机异常兜底】【猴符咒】休息处七十二变新牌无效：玩家={Owner.NetId}，新牌={payload.ReplacementCardId}，处理=跳过。");
            return false;
        }

        string targetTitle = targetCard.Title;
        string replacementTitle = replacementPrototype.Title;
        CardModel replacementCard = Owner.RunState.CreateCard(replacementPrototype, Owner);
        if (payload.TargetWasUpgraded && replacementCard.IsUpgradable)
        {
            CardCmd.Upgrade(replacementCard);
        }

        EndlessDarknessHelper.BeginDelayedAwakeningCheck(Owner);
        try
        {
            await CardCmd.Transform(targetCard, replacementCard);
        }
        finally
        {
            EndlessDarknessHelper.EndDelayedAwakeningCheck(Owner);
        }

        await HealAfterRestSiteTransform();
        ConsumeTransformUse();
        Flash();
        EndlessDarknessHelper.CheckAndTriggerAwakening(Owner);
        MainFile.Logger.Info($"【联机同步】【猴符咒】休息处七十二变完成：玩家={Owner.NetId}，下标={payload.TargetDeckIndex}，{targetTitle}({payload.TargetCardId}) → {replacementTitle}({payload.ReplacementCardId})，剩余次数={RemainingUses}。");
        return true;
    }

    /// <summary>
    /// 休息处七十二变成功后按最大生命百分比回复生命值。
    /// </summary>
    /// <returns>异步任务。</returns>
    private async Task HealAfterRestSiteTransform()
    {
        if (Owner?.Creature == null)
        {
            MainFile.Logger.Info("【猴符咒】休息处七十二变回血跳过：玩家或生物为空。");
            return;
        }

        int healAmount = (int)(Owner.Creature.MaxHp * RestSiteTransformHealPercent / 100m);
        if (healAmount <= 0)
        {
            return;
        }

        await CreatureCmd.Heal(Owner.Creature, healAmount);
        MainFile.Logger.Info($"【猴符咒】休息处七十二变回复生命：玩家={Owner.NetId}，最大生命={Owner.Creature.MaxHp}，比例={RestSiteTransformHealPercent}%，回复={healAmount}。");
    }

    /// <summary>
    /// 构建当前角色卡池中可自选的变化目标牌列表（战斗内七十二变用）。
    /// 使用 CombatState.CreateCard&lt;T&gt;(Player) 泛型方法创建可变副本，
    /// 确保卡牌绑定了 Owner，避免战斗中 FromSimpleGrid 崩溃。
    /// </summary>
    private List<CardModel> BuildReplacementPool()
    {
        var cardPool = Owner?.Character?.CardPool;
        var cs = Owner?.Creature?.CombatState;
        if (cs == null || Owner == null)
        {
            return [];
        }

        // 缓存 CreateCard<T>(Player) 泛型方法定义
        var createCardMethod = cs.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "CreateCard"
                                 && m.IsGenericMethodDefinition
                                 && m.GetGenericArguments().Length == 1
                                 && m.GetParameters().Length == 1
                                 && m.GetParameters()[0].ParameterType == typeof(MegaCrit.Sts2.Core.Entities.Players.Player));

        if (createCardMethod == null)
        {
            MainFile.Logger.Info("【猴符咒】未找到 CombatState.CreateCard<T>(Player) 方法，战斗内七十二变不可用。");
            return [];
        }

        var result = new List<CardModel>();
        foreach (CardModel c in ModelDb.AllCards)
        {
            if (c.Pool != cardPool) continue;
            if (!IsReplacementCard(c, Owner)) continue;

            try
            {
                var genericMethod = createCardMethod.MakeGenericMethod(c.GetType());
                var mutable = genericMethod.Invoke(cs, [Owner]) as CardModel;
                if (mutable != null)
                {
                    result.Add(mutable);
                }
            }
            catch (Exception ex)
            {
                MainFile.Logger.Info($"【联机同步】【猴符咒】创建战斗内七十二变候选失败：玩家={Owner.NetId}，候选={c.Id.Entry}，异常={ex.GetType().Name}:{ex.Message}。");
                // 某些卡牌类型可能无法创建，跳过
            }
        }

        result.Sort((a, b) =>
        {
            int cmp = a.Rarity.CompareTo(b.Rarity);
            return cmp != 0 ? cmp : string.Compare(a.Id.Entry, b.Id.Entry, StringComparison.Ordinal);
        });

        return result;
    }

    /// <summary>
    /// 格式化卡牌列表，方便联机两端对比候选顺序。
    /// </summary>
    /// <param name="cards">候选卡牌。</param>
    /// <returns>卡牌 ID 文本。</returns>
    private static string FormatCardIds(IEnumerable<CardModel> cards)
    {
        return string.Join(",", cards.Select(FormatCardKey));
    }

    /// <summary>
    /// 格式化单张卡牌的稳定键。
    /// </summary>
    /// <param name="card">目标卡牌。</param>
    /// <returns>卡牌稳定键。</returns>
    private static string FormatCardKey(CardModel card)
    {
        return $"{card.Id.Entry}{(card.IsUpgraded ? "+" : string.Empty)}";
    }

    /// <summary>
    /// 判断某张卡能否作为七十二变目标牌；持有合作时额外放行龙小组卡牌。
    /// </summary>
    /// <param name="card">待判断卡牌。</param>
    /// <param name="player">当前玩家。</param>
    /// <returns>可作为七十二变目标牌时返回 true。</returns>
    internal static bool IsReplacementCard(CardModel card, Player? player)
    {
        if (card.Type is CardType.Quest or CardType.Curse or CardType.Status)
        {
            return false;
        }

        if (AllowedReplacementRarities.Contains(card.Rarity))
        {
            return true;
        }

        return DragoEventHelper.HasCollaboration(player) && TeamJackieCardHelper.IsTeamJackieCard(card);
    }

    /// <summary>
    /// 判断指定玩家是不是本地玩家本人。
    /// </summary>
    /// <param name="player">待判断玩家。</param>
    /// <returns>是本地玩家时返回 true。</returns>
    internal static bool IsLocalPlayer(Player? player)
    {
        return player != null && LocalContext.IsMe(player);
    }
}

/// <summary>
/// 猴符咒的营火选项：七十二变。
/// 两步流程：先从卡组中选择要替换的牌，再从当前职业全卡池中自选新牌。
/// </summary>
public class MonkeyTalismanRestSiteOption : RestSiteOption
{
    /// <summary>
    /// 选项ID：使用自定义ID，框架会根据此ID查找图片和标题。
    /// </summary>
    public override string OptionId => "MONKEY_TALISMAN_TRANSFORM";

    /// <summary>
    /// 覆盖描述文本为"七十二变"的说明。
    /// </summary>
    public override LocString Description => new("rest_site_ui", "OPTION_MONKEY_TALISMAN_TRANSFORM.description");

    /// <summary>
    /// 提供自定义图片路径，让框架能预加载猴符咒的休息处图标。
    /// </summary>
    public override IEnumerable<string> AssetPaths
    {
        get
        {
            yield return "res://images/ui/rest_site/option_monkey_talisman_transform.png";
        }
    }

    /// <summary>
    /// 是否允许选择七十二变选项。
    /// </summary>
    public override bool IsEnabled => HasTransformTargetCard(Owner);

    /// <summary>
    /// 构造函数。
    /// </summary>
    public MonkeyTalismanRestSiteOption(Player owner) : base(owner)
    {
    }

    /// <summary>
    /// 选择此选项时：两步选牌流程。
    /// 第一步：从卡组中选择要替换的牌。
    /// 第二步：从当前职业全卡池中自选新牌。
    /// </summary>
    public override async Task<bool> OnSelect()
    {
        if (Owner == null)
        {
            MainFile.Logger.Info("【联机异常兜底】【猴符咒】休息处七十二变取消：玩家为空。");
            return false;
        }

        bool isLocalOwner = MonkeyTalisman.IsLocalPlayer(Owner);
        if (!isLocalOwner)
        {
            MainFile.Logger.Info($"【联机同步】【猴符咒】跟随远端休息处七十二变选项：玩家={Owner.NetId}，本端跳过选牌UI，等待遗物同步消息重放后再关闭休息处。");
            return await RelicInteractionSyncService.WaitForRemoteRestSiteInteraction(Owner, RelicInteractionType.MonkeyRestTransform, "猴符咒休息处七十二变");
        }

        if (Owner.GetRelic<MonkeyTalisman>() == null)
        {
            MainFile.Logger.Info($"【联机同步】【猴符咒】休息处七十二变取消：玩家={Owner.NetId}，原因=玩家没有猴符咒。");
            return await CancelLocalSelection("玩家没有猴符咒");
        }

        // 第一步：让玩家从卡组中选择要变化的原型牌。
        // 休息处不使用 CardSelectCmd，避免联机两端 Choice IDs 因本地 UI 消耗不同步。
        List<CardModel> targetPool = BuildTargetPool();
        CardSelectorPrefs targetPrefs = new(
            new LocString("rest_site_ui", "OPTION_MONKEY_TALISMAN_TRANSFORM.selectTarget"), 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };

        CardModel? targetCard = await PanKuRewardChoiceSelector.SelectOneCard(targetPool, targetPrefs);

        if (targetCard == null)
        {
            // 玩家取消了第一步，不消耗休息机会
            return await CancelLocalSelection("玩家取消原型牌选择");
        }

        // 第二步：从当前角色全卡池中自选变化后的新牌。
        List<CardModel> replacementPool = BuildReplacementPool();
        if (replacementPool.Count == 0)
        {
            // 原型牌尚未被移除，直接返回即可。
            MainFile.Logger.Info("【猴符咒】当前角色卡池中没有可选的变化目标，本次七十二变取消");
            return await CancelLocalSelection("当前角色卡池中没有可选变化目标");
        }

        CardSelectorPrefs replacementPrefs = new(
            new LocString("rest_site_ui", "OPTION_MONKEY_TALISMAN_TRANSFORM.selectReplacement"),
            0,
            1)
        {
            RequireManualConfirmation = true
        };

        CardModel? replacementPrototype = await PanKuRewardChoiceSelector.SelectOneCard(replacementPool, replacementPrefs);

        if (replacementPrototype == null)
        {
            // 玩家第二步不选牌直接确认，不消耗休息机会。
            MainFile.Logger.Info("【猴符咒】玩家取消了变化目标选择，本次七十二变取消");
            return await CancelLocalSelection("玩家取消变化目标选择");
        }

        var monkeyTalisman = Owner.Relics.OfType<MonkeyTalisman>().FirstOrDefault();
        if (monkeyTalisman == null)
        {
            MainFile.Logger.Info($"【联机异常兜底】【猴符咒】休息处七十二变取消：玩家={Owner.NetId}，原因=玩家没有猴符咒。");
            return await CancelLocalSelection("玩家没有猴符咒");
        }

        int targetDeckIndex = DeckSyncHelper.GetDeckIndex(Owner, targetCard);
        if (targetDeckIndex < 0)
        {
            MainFile.Logger.Info($"【联机异常兜底】【猴符咒】休息处七十二变取消：玩家={Owner.NetId}，原因=目标牌不在牌组，目标={targetCard.Id.Entry}。");
            return await CancelLocalSelection("目标牌不在牌组");
        }

        MonkeyRestTransformPayload payload = new(targetDeckIndex, targetCard.Id.Entry, targetCard.IsUpgraded, replacementPrototype.Id.Entry);
        return await RelicInteractionSyncService.RequestMonkeyRestTransform(monkeyTalisman, payload);
    }

    /// <summary>
    /// 本地玩家取消休息处七十二变时，同步通知远端放弃等待。
    /// </summary>
    /// <param name="reason">取消原因。</param>
    /// <returns>固定返回 false，表示不消耗休息处选项。</returns>
    private async Task<bool> CancelLocalSelection(string reason)
    {
        if (Owner != null)
        {
            await RelicInteractionSyncService.NotifyRestSiteInteractionCanceled(Owner, RelicInteractionType.MonkeyRestTransform, TalismanRelicType.Monkey, $"猴符咒休息处七十二变取消：{reason}");
        }

        return false;
    }

    /// <summary>
    /// 判断玩家卡组里是否存在可以作为七十二变原型的牌。
    /// </summary>
    public static bool HasTransformTargetCard(Player player)
    {
        CardPile deck = PileType.Deck.GetPile(player);
        return deck.Cards.Any(IsTransformTargetCard);
    }

    /// <summary>
    /// 构建当前卡组中可作为七十二变原型的牌列表。
    /// </summary>
    private List<CardModel> BuildTargetPool()
    {
        CardPile deck = PileType.Deck.GetPile(Owner);
        return deck.Cards
            .Where(IsTransformTargetCard)
            .ToList();
    }

    /// <summary>
    /// 判断某张卡能否作为七十二变的原型牌。
    /// </summary>
    private static bool IsTransformTargetCard(CardModel card)
    {
        return card.Type != CardType.Quest && card.IsTransformable;
    }

    /// <summary>
    /// 构建当前角色卡池中可自选的变化目标牌列表。
    /// </summary>
    private List<CardModel> BuildReplacementPool()
    {
        var cardPool = Owner.Character?.CardPool;
        return ModelDb.AllCards
            .Where(c => c.Pool == cardPool && IsReplacementCard(c))
            .OrderBy(c => c.Rarity)
            .ThenBy(c => c.Id.Entry)
            .ToList();
    }

    /// <summary>
    /// 判断某张卡能否作为七十二变的目标牌。
    /// </summary>
    private bool IsReplacementCard(CardModel card)
    {
        return MonkeyTalisman.IsReplacementCard(card, Owner);
    }
}
