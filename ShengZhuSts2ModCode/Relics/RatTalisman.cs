using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 鼠符咒：可在自动挡或右键触发时，从手牌中选择1张状态或诅咒牌，
/// 将其消耗并生成一张本职业随机攻击牌加入手牌，然后进入1回合冷却。
/// </summary>
public class RatTalisman : ShengZhuSts2ModRelic, ITalismanRightClickable, ITalismanCooldownResettable, ITalismanInteractionState
{
    /// <summary>
    /// 鼠符咒触发后的冷却回合数。
    /// </summary>
    private const int CooldownTurns = 1;

    /// <summary>
    /// 冷却剩余回合数。
    /// </summary>
    private int _cooldownTurnsRemaining;

    /// <summary>
    /// 冷却剩余回合数，写入存档和联机状态，避免一端可用一端冷却。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int SavedCooldownTurnsRemaining
    {
        get => _cooldownTurnsRemaining;
        set => _cooldownTurnsRemaining = Math.Max(0, value);
    }

    /// <summary>
    /// 圣主持有鼠符咒时当前是否为人形态。每场战斗开始会重置为人形态，避免联机两端继承形态分歧。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public bool IsHumanForm { get; private set; } = true;

    /// <summary>
    /// 本回合是否已经切换过圣主形态。保存后可避免同一回合读档重复切换。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public bool HasSwitchedFormThisTurn { get; private set; }

    /// <summary>
    /// 当前战斗中记录的石像来源荆棘层数，只用于同一场战斗内切回石像时恢复。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int SavedStatueThornsAmount { get; private set; } = ShenZhuStatuePower.InitialThorns;

    /// <summary>
    /// 当前战斗中记录的石像成长回合计数，避免同一场战斗内切回石像后成长节奏重置。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int SavedStatueTurnCount { get; private set; }

    /// <summary>
    /// 是否正在处理变化流程，防止右键和自动挡重复进入。
    /// </summary>
    private bool _isProcessing;

    /// <summary>
    /// 当前排队中的半自动变化任务，供回合开始同步流程等待完成。
    /// </summary>
    private Task _queuedAutoReplaceTask = Task.CompletedTask;

    /// <summary>
    /// 遗物稀有度：稀有。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Rare;

    /// <summary>
    /// 显示冷却计数器。
    /// </summary>
    public override bool ShowCounter => _cooldownTurnsRemaining > 0;

    /// <summary>
    /// 计数器显示值。
    /// </summary>
    public override int DisplayAmount => _cooldownTurnsRemaining;

    /// <summary>
    /// 鼠符咒立即处理手牌，没有预备激发状态。
    /// </summary>
    public bool IsActivated => false;

    /// <summary>
    /// 当前剩余冷却回合数。
    /// </summary>
    public int CooldownTurnsRemaining => _cooldownTurnsRemaining;

    /// <summary>
    /// 是否处于冷却中（供兔符咒刷新用）。
    /// </summary>
    public bool IsOnCooldown => _cooldownTurnsRemaining > 0;

    /// <summary>
    /// 战斗开始时重置冷却。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        _cooldownTurnsRemaining = 0;
        _isProcessing = false;
        ResetFormStateForNewCombat();
        UpdateTalismanVisualState(false, _cooldownTurnsRemaining);

        // 兜底检查十二符咒觉醒（覆盖非圣主角色、非二选一获取等场景）
        TalismanAwakeningHelper.CheckAndTriggerAwakening(Owner);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 战斗结束时把鼠符咒的战斗内形态字段归一，避免离开战斗后的联机状态校验读到一端人形一端石像。
    /// </summary>
    /// <param name="room">结束的战斗房间。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetFormStateOutsideCombat();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 进入非战斗房间时兜底归一形态，避免旧存档或上一场战斗残留的石像字段在营火、商店、事件离开时触发联机校验分歧。
    /// </summary>
    /// <param name="room">当前进入的房间。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom)
        {
            ResetFormStateOutsideCombat();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 玩家回合开始时：鼠符咒的自动挡触发由符咒探测仪/十二符咒罗盘统一同步。
    /// </summary>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner != null && player == Owner)
        {
            ShengZhuFormService.ResetTurnSwitch(player);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 右键点击：手动选择1张状态或诅咒牌变化为随机攻击牌。
    /// </summary>
    /// <param name="context">玩家选择上下文。</param>
    /// <returns>异步任务。</returns>
    public Task OnRightClick(PlayerChoiceContext context)
    {
        return RelicInteractionSyncService.RequestRatUse(this, false);
    }

    /// <summary>
    /// 设置当前圣主形态。
    /// </summary>
    /// <param name="isHumanForm">是否为人形态。</param>
    public void SetHumanForm(bool isHumanForm)
    {
        IsHumanForm = isHumanForm;
        MainFile.Logger.Info($"【鼠符咒形态】保存当前形态：玩家={Owner?.NetId.ToString() ?? "无持有者"}，形态={(IsHumanForm ? "人形" : "石像")}。");
    }

    /// <summary>
    /// 标记本回合已经切换过形态。
    /// </summary>
    public void MarkFormSwitchedThisTurn()
    {
        HasSwitchedFormThisTurn = true;
    }

    /// <summary>
    /// 重置本回合形态切换次数。
    /// </summary>
    public void ResetFormSwitchForTurn()
    {
        HasSwitchedFormThisTurn = false;
    }

    /// <summary>
    /// 新战斗开始时重置单场战斗内的形态切换状态，并强制以人形态进入战斗。
    /// </summary>
    public void ResetFormStateForNewCombat()
    {
        IsHumanForm = true;
        HasSwitchedFormThisTurn = false;
        SavedStatueThornsAmount = ShenZhuStatuePower.InitialThorns;
        SavedStatueTurnCount = 0;
        MainFile.Logger.Info($"【鼠符咒形态】新战斗重置形态：玩家={Owner?.NetId.ToString() ?? "无持有者"}，固定以人形态进入战斗。");
    }

    /// <summary>
    /// 离开战斗后清理只在战斗内有效的形态字段，保证非战斗联机校验两端一致。
    /// </summary>
    public void ResetFormStateOutsideCombat()
    {
        IsHumanForm = true;
        HasSwitchedFormThisTurn = false;
        SavedStatueThornsAmount = ShenZhuStatuePower.InitialThorns;
        SavedStatueTurnCount = 0;
        _isProcessing = false;
        _queuedAutoReplaceTask = Task.CompletedTask;
        MainFile.Logger.Info($"【鼠符咒形态】非战斗归一形态：玩家={Owner?.NetId.ToString() ?? "无持有者"}，固定回到人形字段，避免非战斗联机校验分歧。");
    }

    /// <summary>
    /// 保存当前石像来源状态，供同一场战斗内切回石像时恢复。
    /// </summary>
    /// <param name="statueThornsAmount">石像来源荆棘层数。</param>
    /// <param name="statueTurnCount">石像成长回合计数。</param>
    public void SaveStatueState(int statueThornsAmount, int statueTurnCount)
    {
        SavedStatueThornsAmount = Math.Max(0, statueThornsAmount);
        SavedStatueTurnCount = Math.Max(0, statueTurnCount);
        MainFile.Logger.Info($"【鼠符咒形态】保存石像状态：玩家={Owner?.NetId.ToString() ?? "无持有者"}，石像来源荆棘={SavedStatueThornsAmount}，石像计数={SavedStatueTurnCount}。");
    }

    /// <summary>
    /// 设置自动挡激发状态（供符咒探测仪/十二符咒罗盘调用）。
    /// 鼠符咒没有持续激发状态，自动挡同步时会直接尝试处理当前手牌。
    /// </summary>
    /// <param name="activate">是否激发。</param>
    public void SetActivated(bool activate)
    {
        if (!activate)
        {
            return;
        }

        QueueAutoReplaceCurseOrStatus();
    }

    /// <summary>
    /// 等待当前排队中的半自动变化任务完成，避免联机校验读到半截状态。
    /// </summary>
    /// <returns>当前半自动变化任务。</returns>
    public Task WaitForQueuedAutoReplace()
    {
        return _queuedAutoReplaceTask;
    }

    /// <summary>
    /// 排队触发半自动变化流程，避免裸 fire-and-forget 隐藏异常。
    /// </summary>
    private void QueueAutoReplaceCurseOrStatus()
    {
        if (_cooldownTurnsRemaining > 0 || _isProcessing || Owner?.PlayerCombatState == null)
        {
            _queuedAutoReplaceTask = Task.CompletedTask;
            MainFile.Logger.Info("【鼠符咒】半自动触发被跳过：冷却中、处理中或玩家战斗状态不可用。");
            return;
        }

        _queuedAutoReplaceTask = RelicInteractionSyncService.RequestRatUse(this, true);
    }

    /// <summary>
    /// 执行同步后的变化流程。
    /// </summary>
    /// <param name="choiceContext">玩家选择上下文。</param>
    /// <param name="isAutoTrigger">是否为半自动触发。</param>
    /// <returns>异步任务。</returns>
    internal Task RunSyncedReplaceCurseOrStatus(PlayerChoiceContext choiceContext, bool isAutoTrigger)
    {
        return TryReplaceCurseOrStatus(choiceContext, isAutoTrigger ? "半自动挡触发" : "右键触发");
    }

    /// <summary>
    /// 尝试把手牌中的1张状态或诅咒牌变化为随机攻击牌。
    /// </summary>
    /// <param name="choiceContext">玩家选择上下文。</param>
    /// <param name="triggerSource">触发来源，写日志用。</param>
    /// <returns>异步任务。</returns>
    private async Task TryReplaceCurseOrStatus(PlayerChoiceContext choiceContext, string triggerSource)
    {
        if (_cooldownTurnsRemaining > 0 || _isProcessing || Owner?.PlayerCombatState == null)
        {
            return;
        }

        List<CardModel> candidates = GetReplaceCandidates();
        if (candidates.Count == 0)
        {
            MainFile.Logger.Info($"【鼠符咒】{triggerSource}时手牌中没有诅咒或状态牌，不触发变化。");
            return;
        }

        MainFile.Logger.Info($"【联机同步】【鼠符咒】来源={triggerSource}，玩家={Owner.NetId}，动作=准备变化手牌，候选数={candidates.Count}，候选ID=[{FormatCardIds(candidates)}]。");
        _isProcessing = true;
        try
        {
            CardModel? targetCard = await SelectReplaceTarget(choiceContext, candidates);
            if (targetCard == null)
            {
                MainFile.Logger.Info($"【联机同步】【鼠符咒】来源={triggerSource}，玩家={Owner.NetId}，动作=选择变化目标，选中=取消。");
                MainFile.Logger.Info($"【鼠符咒】玩家取消了变化目标选择，触发来源={triggerSource}。");
                return;
            }

            MainFile.Logger.Info($"【联机同步】【鼠符咒】来源={triggerSource}，玩家={Owner.NetId}，动作=选择变化目标，选中={FormatCardKey(targetCard)}。");
            if (!IsReplaceableCard(targetCard) || !Owner.PlayerCombatState.Hand.Cards.Contains(targetCard))
            {
                MainFile.Logger.Info($"【鼠符咒】选中的卡牌已不在手牌或不可变化：{targetCard.Id.Entry}。");
                return;
            }

            await ReplaceCardWithRandomAttack(choiceContext, targetCard, triggerSource);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 获取当前手牌中可以被鼠符咒变化的状态牌或诅咒牌。
    /// </summary>
    /// <returns>可变化卡牌列表。</returns>
    private List<CardModel> GetReplaceCandidates()
    {
        return Owner?.PlayerCombatState?.Hand.Cards
            .Where(IsReplaceableCard)
            .ToList() ?? [];
    }

    /// <summary>
    /// 选择要变化的手牌。只有1张候选时直接返回，多张候选时弹出手牌选择。
    /// </summary>
    /// <param name="choiceContext">玩家选择上下文。</param>
    /// <param name="candidates">可变化候选。</param>
    /// <returns>选中的卡牌；取消时返回 null。</returns>
    private async Task<CardModel?> SelectReplaceTarget(PlayerChoiceContext choiceContext, IReadOnlyList<CardModel> candidates)
    {
        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        CardSelectorPrefs prefs = new(
            new LocString("relics", "SHENGZHUSTS2MOD-RAT_TALISMAN.selectionScreenPrompt"),
            0,
            1)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };

        IReadOnlyList<CardModel> selectedCards = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner!,
            prefs,
            IsReplaceableCard,
            null!)).ToList();

        return selectedCards.FirstOrDefault();
    }

    /// <summary>
    /// 判断卡牌是否为鼠符咒可变化目标。
    /// </summary>
    /// <param name="card">待判断卡牌。</param>
    /// <returns>是诅咒或状态牌时返回 true。</returns>
    private static bool IsReplaceableCard(CardModel card)
    {
        if (card is FranticEscape)
        {
            return false;
        }

        return card.Type == CardType.Curse || card.Type == CardType.Status;
    }

    /// <summary>
    /// 消耗指定手牌，并生成随机攻击牌加入手牌。
    /// </summary>
    /// <param name="choiceContext">玩家选择上下文。</param>
    /// <param name="targetCard">被变化的状态或诅咒牌。</param>
    /// <param name="triggerSource">触发来源，写日志用。</param>
    /// <returns>异步任务。</returns>
    private async Task ReplaceCardWithRandomAttack(PlayerChoiceContext choiceContext, CardModel targetCard, string triggerSource)
    {
        var combatId = CombatManager.Instance.CurrentCombatId;
        var result = await CardCmd.Exhaust(choiceContext, targetCard);
        if (result == null || !CombatManager.Instance.IsCurrentLiveCombat(combatId))
        {
            return;
        }

        _cooldownTurnsRemaining = CooldownTurns;
        UpdateTalismanVisualState(false, _cooldownTurnsRemaining);
        Flash();

        CardModel? randomAttack = GetRandomAttackCard(Owner!, targetCard, triggerSource);
        if (randomAttack != null && Owner!.Creature?.CombatState != null)
        {
            CardModel mutableCard = Owner.Creature.CombatState.CreateCard(randomAttack, Owner);
            ConfigureReplacementAttack(mutableCard);
            await CardPileCmdHelper.AddGeneratedCardToCombat(mutableCard, PileType.Hand, true);
            MainFile.Logger.Info($"【鼠符咒】{triggerSource}完成：{targetCard.Id.Entry} → {mutableCard.Id.Entry}，进入{CooldownTurns}回合冷却。");
            return;
        }

        await CardPileCmd.Draw(choiceContext, 1, Owner!);
        MainFile.Logger.Info($"【鼠符咒】{triggerSource}已消耗目标牌，但未找到随机攻击牌，改为抽1张：{targetCard.Id.Entry}。");
    }

    /// <summary>
    /// 从本职业卡池中随机获取一张攻击牌。
    /// 排除Token、Status、Curse、Quest等非常规稀有度的牌。
    /// 使用稳定哈希选择，不推进局内随机源，避免联机两端因异步顺序不同生成不同攻击牌。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="targetCard">被变化的目标牌。</param>
    /// <param name="triggerSource">触发来源。</param>
    /// <returns>稳定选出的攻击牌原型。</returns>
    private static CardModel? GetRandomAttackCard(Player player, CardModel targetCard, string triggerSource)
    {
        // 允许的稀有度：Basic、Common、Uncommon、Rare
        HashSet<CardRarity> allowedRarities = [CardRarity.Basic, CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare];

        // 从所有卡牌中筛选本职业的攻击牌
        List<CardModel> attackCards = ModelDb.AllCards
            .Where(c => c.Type == CardType.Attack
                        && c.Pool == player.Character?.CardPool
                        && allowedRarities.Contains(c.Rarity))
            .ToList();

        if (attackCards.Count == 0)
        {
            // 如果本职业没有攻击牌，从所有攻击牌中选
            attackCards = ModelDb.AllCards
                .Where(c => c.Type == CardType.Attack && allowedRarities.Contains(c.Rarity))
                .ToList();
        }

        if (attackCards.Count == 0)
        {
            MainFile.Logger.Info($"【联机同步】【鼠符咒】来源={triggerSource}，玩家={player.NetId}，动作=选择随机攻击牌，候选数=0，目标={FormatCardKey(targetCard)}，结果=无候选。");
            return null;
        }

        IEnumerable<string> handCardIds = player.PlayerCombatState?.Hand.Cards.Select(card => card.Id.Entry) ?? Enumerable.Empty<string>();
        string handSnapshot = string.Join(",", handCardIds);
        string roundNumber = player.Creature?.CombatState?.RoundNumber.ToString() ?? "无战斗回合";

        CardModel? selectedAttack = StableRandomHelper.PickByStableHash(
            player,
            attackCards,
            "RatTalisman.RandomAttack",
            card => card.Id.Entry,
            triggerSource,
            targetCard.Id.Entry,
            targetCard.IsUpgraded.ToString(),
            handSnapshot,
            roundNumber);
        MainFile.Logger.Info($"【联机同步】【鼠符咒】来源={triggerSource}，玩家={player.NetId}，动作=选择随机攻击牌，候选数={attackCards.Count}，候选ID=[{FormatCardIds(attackCards.OrderBy(card => card.Id.Entry, StringComparer.Ordinal))}]，选中={selectedAttack?.Id.Entry ?? "空"}，随机上下文=目标:{FormatCardKey(targetCard)};手牌:{handSnapshot};回合:{roundNumber}。");
        return selectedAttack;
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
    /// 配置鼠符咒生成的攻击牌：对齐1代，变为0费且使用后消耗。
    /// </summary>
    /// <param name="card">需要配置的生成牌。</param>
    private static void ConfigureReplacementAttack(CardModel card)
    {
        card.EnergyCost.SetCustomBaseCost(0);
        card.AddKeyword(CardKeyword.Exhaust);
        MainFile.Logger.Info($"【鼠符咒】生成攻击牌已配置为0费消耗：{card.Id.Entry}");
    }

    /// <summary>
    /// 回合结束时递减冷却。
    /// </summary>
    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side == CombatSide.Player && _cooldownTurnsRemaining > 0)
        {
            _cooldownTurnsRemaining--;
            UpdateTalismanVisualState(false, _cooldownTurnsRemaining);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 重置冷却（供兔符咒调用）。
    /// </summary>
    public void ResetCooldown()
    {
        _cooldownTurnsRemaining = 0;
        UpdateTalismanVisualState(false, _cooldownTurnsRemaining);
        Flash();
    }
}
