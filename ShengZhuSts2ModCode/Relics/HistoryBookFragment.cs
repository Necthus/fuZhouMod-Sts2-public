using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.HistoryBookPreview;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 岁月史书残卷：右键按当前场景选择一次改写现实，最多 12 次。
/// </summary>
public class HistoryBookFragment : ShengZhuSts2ModRelic, IRelicRightClickable
{
    /// <summary>
    /// 残卷最大使用次数。
    /// </summary>
    public const int MaxUses = 12;

    /// <summary>
    /// 抽满手牌选项给予的能量。
    /// </summary>
    private const int EnergyGain = 12;

    /// <summary>
    /// 重置战斗牌堆后抽牌数量。
    /// </summary>
    private const int ResetDrawCount = 5;

    /// <summary>
    /// 剩余使用次数。
    /// </summary>
    private int _remainingUses = MaxUses;

    /// <summary>
    /// 战斗开局抽牌堆快照；用卡牌 ID 和升级状态记录，方便联机两端重放。
    /// </summary>
    private readonly List<HistoryBookCardSnapshot> _startingDrawPile = [];

    /// <summary>
    /// 是否正在处理右键菜单，避免重复打开。
    /// </summary>
    private bool _isSelectingRewrite;

    /// <summary>
    /// 剩余改写次数，写入存档。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int RemainingUses
    {
        get => _remainingUses;
        set => _remainingUses = Math.Clamp(value, 0, MaxUses);
    }

    /// <summary>
    /// 事件遗物，不进入普通遗物池。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Event;

    /// <summary>
    /// 不允许商店自然售卖。
    /// </summary>
    public override bool IsAllowedInShops => false;

    /// <summary>
    /// 显示剩余次数计数器。
    /// </summary>
    public override bool ShowCounter => true;

    /// <summary>
    /// 计数器显示值。
    /// </summary>
    public override int DisplayAmount => RemainingUses;

    /// <summary>
    /// 刷新遗物描述中的剩余次数变量。
    /// </summary>
    /// <param name="description">待渲染的遗物描述。</param>
    public override void CustomizeDescription(LocString description)
    {
        description.Add("Amount", RemainingUses);
    }

    /// <summary>
    /// 战斗开始时记录开局抽牌堆快照。
    /// </summary>
    /// <returns>异步任务。</returns>
    public override Task BeforeCombatStart()
    {
        _isSelectingRewrite = false;
        _startingDrawPile.Clear();
        if (Owner?.PlayerCombatState == null)
        {
            return Task.CompletedTask;
        }

        foreach (CardModel card in PileType.Draw.GetPile(Owner).Cards)
        {
            _startingDrawPile.Add(new HistoryBookCardSnapshot(card.Id.Entry, card.IsUpgraded));
        }

        MainFile.Logger.Info($"【岁月史书残卷】记录战斗开局抽牌堆：玩家={Owner.NetId}，数量={_startingDrawPile.Count}。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 战斗结束时清理运行时状态。
    /// </summary>
    /// <returns>异步任务。</returns>
    public override Task AfterCombatEnd(CombatRoom room)
    {
        _isSelectingRewrite = false;
        _startingDrawPile.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 右键打开残卷改写菜单。
    /// </summary>
    /// <param name="context">玩家选择上下文。</param>
    /// <returns>异步任务。</returns>
    public async Task OnRightClick(PlayerChoiceContext context)
    {
        if (!IsLocalPlayer(Owner))
        {
            MainFile.Logger.Info($"【岁月史书残卷】忽略非本地玩家右键：持有者={Owner?.NetId.ToString() ?? "空"}。");
            return;
        }

        if (Owner == null || RemainingUses <= 0 || _isSelectingRewrite)
        {
            Flash();
            return;
        }

        List<HistoryBookRewritePreviewCard> choices = BuildRewriteChoices();
        if (choices.Count == 0)
        {
            MainFile.Logger.Info($"【岁月史书残卷】当前场景没有可用改写选项：玩家={Owner.NetId}，房间={Owner.RunState.CurrentRoom?.GetType().Name ?? "空"}。");
            Flash();
            return;
        }

        _isSelectingRewrite = true;
        try
        {
            CardSelectorPrefs prefs = new(new LocString("relics", "SHENGZHUSTS2MOD-HISTORY_BOOK_FRAGMENT.choicePrompt"), 0, 1)
            {
                Cancelable = true,
                RequireManualConfirmation = true
            };

            CardModel? selected = await PanKuRewardChoiceSelector.SelectOneCard(choices, prefs);
            if (selected is not HistoryBookRewritePreviewCard previewCard)
            {
                MainFile.Logger.Info($"【岁月史书残卷】玩家取消改写，不消耗次数：玩家={Owner.NetId}。");
                return;
            }

            if (IsPlaceholderRewrite(previewCard.RewriteOption))
            {
                LogPlaceholderRewrite(previewCard.RewriteOption);
                Flash();
                return;
            }

            await RelicInteractionSyncService.RequestHistoryBookRewrite(this, previewCard.RewriteOption);
        }
        finally
        {
            _isSelectingRewrite = false;
        }
    }

    /// <summary>
    /// 执行联机同步后的残卷改写。
    /// </summary>
    /// <param name="option">改写选项。</param>
    /// <param name="context">玩家选择上下文。</param>
    /// <returns>执行成功时返回 true。</returns>
    internal async Task<bool> RunSyncedRewrite(HistoryBookRewriteOption option, PlayerChoiceContext context)
    {
        if (Owner == null)
        {
            MainFile.Logger.Info("【联机异常兜底】【岁月史书残卷】改写失败：持有者为空。");
            return false;
        }

        int cost = GetRewriteCost(option);
        if (cost <= 0 || RemainingUses < cost)
        {
            MainFile.Logger.Info($"【联机异常兜底】【岁月史书残卷】改写失败：玩家={Owner.NetId}，选项={option}，消耗={cost}，剩余={RemainingUses}。");
            Flash();
            return false;
        }

        if (!IsRewriteSceneValid(option))
        {
            MainFile.Logger.Info($"【联机异常兜底】【岁月史书残卷】改写失败：玩家={Owner.NetId}，选项={option}，作用范围={GetRewriteScopeText(option)}，原因=当前场景不允许执行该选项，房间={Owner.RunState.CurrentRoom?.GetType().Name ?? "空"}，战斗中={Owner.PlayerCombatState != null || Owner.Creature?.CombatState != null}。");
            Flash();
            return false;
        }

        MainFile.Logger.Info($"【联机同步】【岁月史书残卷】准备改写：玩家={Owner.NetId}，选项={option}，作用范围={GetRewriteScopeText(option)}。");
        bool executed = option switch
        {
            HistoryBookRewriteOption.MonsterHpToOne => await LowerAllMonsterHpToOne(),
            HistoryBookRewriteOption.StunAllMonsters => await StunAllMonsters(),
            HistoryBookRewriteOption.DrawFullAndGainEnergy => await DrawFullAndGainEnergy(context),
            HistoryBookRewriteOption.ResetCombatPiles => await ResetCombatPiles(context),
            HistoryBookRewriteOption.ExhaustStatusAndCurse => await ExhaustStatusAndCurseCards(),
            HistoryBookRewriteOption.CampfireMultiSelect => EnableCampfireMultiSelect(),
            HistoryBookRewriteOption.ShopFree => EnableShopFree(),
            HistoryBookRewriteOption.ShopRestock => EnableShopRestock(),
            _ => false
        };

        if (!executed)
        {
            return false;
        }

        RemainingUses -= cost;
        InvokeDisplayAmountChanged();
        Flash();
        MainFile.Logger.Info($"【联机同步】【岁月史书残卷】改写完成：玩家={Owner.NetId}，选项={option}，消耗={cost}，剩余={RemainingUses}。");
        return true;
    }

    /// <summary>
    /// 参考帐篷：残卷开启休息处多选后，不禁用剩余休息处选项。
    /// </summary>
    /// <param name="player">正在操作休息处的玩家。</param>
    /// <returns>应该禁用剩余选项时返回 true。</returns>
    public override bool ShouldDisableRemainingRestSiteOptions(Player player)
    {
        if (Owner == null || player.NetId != Owner.NetId)
        {
            return true;
        }

        bool keepOptions = HistoryBookRewriteManager.ShouldKeepRestSiteOptions(player);
        if (keepOptions)
        {
            Flash();
            MainFile.Logger.Info($"【岁月史书残卷】休息处多选生效，保留剩余选项：玩家={player.NetId}。");
        }

        return !keepOptions;
    }

    /// <summary>
    /// 参考送货员：残卷开启零元购后，把当前商店商品价格改为 0。
    /// </summary>
    /// <param name="player">购买商品的玩家。</param>
    /// <param name="entry">商品条目。</param>
    /// <param name="originalPrice">原始价格。</param>
    /// <returns>修改后的价格。</returns>
    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal originalPrice)
    {
        if (Owner == null || player.NetId != Owner.NetId)
        {
            return originalPrice;
        }

        return HistoryBookRewriteManager.ShouldMakeMerchantFree(player) ? 0 : originalPrice;
    }

    /// <summary>
    /// 参考送货员：残卷开启补货后，普通商品售出会补货。
    /// </summary>
    /// <param name="entry">商品条目。</param>
    /// <param name="player">购买商品的玩家。</param>
    /// <returns>需要补货时返回 true。</returns>
    public override bool ShouldRefillMerchantEntry(MerchantEntry entry, Player player)
    {
        return Owner != null
               && player.NetId == Owner.NetId
               && HistoryBookRewriteManager.ShouldRefillMerchantEntry(entry, player);
    }

    /// <summary>
    /// 根据当前场景生成残卷改写候选。
    /// </summary>
    /// <returns>候选预览卡。</returns>
    private List<HistoryBookRewritePreviewCard> BuildRewriteChoices()
    {
        if (Owner == null)
        {
            return [];
        }

        List<HistoryBookRewritePreviewCard> choices = [];
        AbstractRoom? room = Owner.RunState.CurrentRoom;
        if (room is RestSiteRoom)
        {
            AddChoiceIfAffordable<HistoryBookCampfireMultiSelectPreviewCard>(choices);
            AddChoiceIfAffordable<HistoryBookRestToFreeShopPreviewCard>(choices);
        }
        else if (room is MerchantRoom)
        {
            AddChoiceIfAffordable<HistoryBookShopFreePreviewCard>(choices);
            AddChoiceIfAffordable<HistoryBookShopToCampfirePreviewCard>(choices);
            AddChoiceIfAffordable<HistoryBookShopRestockPreviewCard>(choices);
        }
        else if (Owner.PlayerCombatState != null || Owner.Creature?.CombatState != null)
        {
            AddChoiceIfAffordable<HistoryBookMonsterHpToOnePreviewCard>(choices);
            AddChoiceIfAffordable<HistoryBookStunAllMonstersPreviewCard>(choices);
            AddChoiceIfAffordable<HistoryBookDrawFullAndGainEnergyPreviewCard>(choices);
            AddChoiceIfAffordable<HistoryBookResetCombatPilesPreviewCard>(choices);
            AddChoiceIfAffordable<HistoryBookExhaustStatusAndCursePreviewCard>(choices);
        }

        return choices;
    }

    /// <summary>
    /// 次数足够时加入一个候选预览卡。
    /// </summary>
    /// <typeparam name="T">预览卡类型。</typeparam>
    /// <param name="choices">候选列表。</param>
    private void AddChoiceIfAffordable<T>(List<HistoryBookRewritePreviewCard> choices)
        where T : HistoryBookRewritePreviewCard, new()
    {
        T preview = Owner!.RunState.CreateCard<T>(Owner) as T ?? new T();
        if (RemainingUses >= preview.Cost)
        {
            choices.Add(preview);
        }
    }

    /// <summary>
    /// 将所有存活怪物的当前生命降到 1。
    /// </summary>
    /// <returns>执行成功时返回 true。</returns>
    private async Task<bool> LowerAllMonsterHpToOne()
    {
        List<Creature> enemies = GetAliveEnemies();
        foreach (Creature enemy in enemies.Where(enemy => enemy.CurrentHp > 1))
        {
            await CreatureCmd.SetCurrentHp(enemy, 1);
        }

        MainFile.Logger.Info($"【岁月史书残卷】怪物生命改写为 1：玩家={Owner!.NetId}，目标数={enemies.Count}。");
        return enemies.Count > 0;
    }

    /// <summary>
    /// 眩晕所有存活怪物。
    /// </summary>
    /// <returns>执行成功时返回 true。</returns>
    private async Task<bool> StunAllMonsters()
    {
        List<Creature> enemies = GetAliveEnemies();
        foreach (Creature enemy in enemies)
        {
            await CreatureCmd.Stun(enemy);
        }

        MainFile.Logger.Info($"【岁月史书残卷】眩晕所有怪物：玩家={Owner!.NetId}，目标数={enemies.Count}。");
        return enemies.Count > 0;
    }

    /// <summary>
    /// 抽到当前手牌上限，并获得 12 点能量。
    /// </summary>
    /// <param name="context">玩家选择上下文。</param>
    /// <returns>执行成功时返回 true。</returns>
    private async Task<bool> DrawFullAndGainEnergy(PlayerChoiceContext context)
    {
        if (Owner?.PlayerCombatState == null)
        {
            return false;
        }

        int drawAmount = Math.Max(0, 10 - PileType.Hand.GetPile(Owner).Cards.Count);
        if (drawAmount > 0)
        {
            await CardPileCmd.Draw(context, drawAmount, Owner);
        }

        await PlayerCmd.GainEnergy(EnergyGain, Owner);
        MainFile.Logger.Info($"【岁月史书残卷】抽满手牌并获得能量：玩家={Owner.NetId}，抽牌={drawAmount}，能量={EnergyGain}。");
        return true;
    }

    /// <summary>
    /// 清空手牌、抽牌堆、弃牌堆并按开局快照重建抽牌堆，然后抽 5 张牌。
    /// </summary>
    /// <param name="context">玩家选择上下文。</param>
    /// <returns>执行成功时返回 true。</returns>
    private async Task<bool> ResetCombatPiles(PlayerChoiceContext context)
    {
        if (Owner?.PlayerCombatState == null || Owner.Creature?.CombatState == null || _startingDrawPile.Count == 0)
        {
            MainFile.Logger.Info($"【岁月史书残卷】重置战斗牌堆取消：玩家={Owner?.NetId.ToString() ?? "空"}，原因=没有开局抽牌堆快照。");
            return false;
        }

        var combatState = Owner.Creature.CombatState;
        List<CardModel> cardsToRemove = PileType.Hand.GetPile(Owner).Cards
            .Concat(PileType.Draw.GetPile(Owner).Cards)
            .Concat(PileType.Discard.GetPile(Owner).Cards)
            .ToList();

        if (cardsToRemove.Count > 0)
        {
            await CardPileCmd.RemoveFromCombat(cardsToRemove, true);
        }

        foreach (HistoryBookCardSnapshot snapshot in _startingDrawPile)
        {
            CardModel? prototype = DeckSyncHelper.FindCardPrototype(snapshot.CardId);
            if (prototype == null)
            {
                MainFile.Logger.Info($"【岁月史书残卷】重建开局抽牌堆跳过未知卡牌：玩家={Owner.NetId}，卡牌={snapshot.CardId}。");
                continue;
            }

            CardModel card = combatState.CreateCard(prototype, Owner);
            if (snapshot.IsUpgraded && card.IsUpgradable)
            {
                CardCmd.Upgrade(card);
            }

            await CardPileCmdHelper.AddGeneratedCardToCombat(card, PileType.Draw, false);
        }

        await CardPileCmd.Draw(context, ResetDrawCount, Owner);
        MainFile.Logger.Info($"【岁月史书残卷】重置战斗牌堆完成：玩家={Owner.NetId}，移除={cardsToRemove.Count}，重建={_startingDrawPile.Count}，抽牌={ResetDrawCount}。");
        return true;
    }

    /// <summary>
    /// 消耗手牌、抽牌堆、弃牌堆中的所有状态牌和诅咒牌。
    /// </summary>
    /// <returns>执行成功时返回 true。</returns>
    private async Task<bool> ExhaustStatusAndCurseCards()
    {
        if (Owner?.PlayerCombatState == null)
        {
            return false;
        }

        List<CardModel> targets = PileType.Hand.GetPile(Owner).Cards
            .Concat(PileType.Draw.GetPile(Owner).Cards)
            .Concat(PileType.Discard.GetPile(Owner).Cards)
            .Where(card => card.Type is CardType.Status or CardType.Curse)
            .ToList();

        foreach (CardModel card in targets)
        {
            await CardPileCmd.Add(card, PileType.Exhaust);
        }

        MainFile.Logger.Info($"【岁月史书残卷】消耗状态和诅咒：玩家={Owner.NetId}，数量={targets.Count}。");
        return true;
    }

    /// <summary>
    /// 开启本次休息处多选。
    /// </summary>
    /// <returns>执行成功时返回 true。</returns>
    private bool EnableCampfireMultiSelect()
    {
        if (Owner?.RunState.CurrentRoom is not RestSiteRoom)
        {
            return false;
        }

        HistoryBookRewriteManager.EnableCampfireMultiSelect(Owner);
        return true;
    }

    /// <summary>
    /// 开启本次商店零元购。
    /// </summary>
    /// <returns>执行成功时返回 true。</returns>
    private bool EnableShopFree()
    {
        if (Owner?.RunState.CurrentRoom is not MerchantRoom)
        {
            return false;
        }

        HistoryBookRewriteManager.EnableShopFree(Owner);
        return true;
    }

    /// <summary>
    /// 开启本次商店补货。
    /// </summary>
    /// <returns>执行成功时返回 true。</returns>
    private bool EnableShopRestock()
    {
        if (Owner?.RunState.CurrentRoom is not MerchantRoom)
        {
            return false;
        }

        HistoryBookRewriteManager.EnableShopRestock(Owner);
        return true;
    }

    /// <summary>
    /// 获取当前战斗中的存活敌人。
    /// </summary>
    /// <returns>存活敌人列表。</returns>
    private List<Creature> GetAliveEnemies()
    {
        return Owner?.Creature?.CombatState?.Enemies
            .Where(enemy => enemy.IsAlive && !enemy.IsDead)
            .ToList() ?? [];
    }

    /// <summary>
    /// 判断指定改写选项是否允许在当前场景执行。
    /// </summary>
    /// <param name="option">改写选项。</param>
    /// <returns>当前场景允许执行时返回 true。</returns>
    private bool IsRewriteSceneValid(HistoryBookRewriteOption option)
    {
        if (Owner == null)
        {
            return false;
        }

        return option switch
        {
            HistoryBookRewriteOption.MonsterHpToOne
                or HistoryBookRewriteOption.StunAllMonsters
                or HistoryBookRewriteOption.DrawFullAndGainEnergy
                or HistoryBookRewriteOption.ResetCombatPiles
                or HistoryBookRewriteOption.ExhaustStatusAndCurse => Owner.PlayerCombatState != null || Owner.Creature?.CombatState != null,
            HistoryBookRewriteOption.CampfireMultiSelect => Owner.RunState.CurrentRoom is RestSiteRoom,
            HistoryBookRewriteOption.ShopFree
                or HistoryBookRewriteOption.ShopRestock => Owner.RunState.CurrentRoom is MerchantRoom,
            _ => false
        };
    }

    /// <summary>
    /// 获取改写选项的联机作用范围文本。
    /// </summary>
    /// <param name="option">改写选项。</param>
    /// <returns>作用范围说明。</returns>
    private static string GetRewriteScopeText(HistoryBookRewriteOption option)
    {
        return option switch
        {
            HistoryBookRewriteOption.MonsterHpToOne
                or HistoryBookRewriteOption.StunAllMonsters => "公共战斗对象",
            HistoryBookRewriteOption.DrawFullAndGainEnergy
                or HistoryBookRewriteOption.ResetCombatPiles
                or HistoryBookRewriteOption.ExhaustStatusAndCurse => "仅残卷持有者战斗状态",
            HistoryBookRewriteOption.CampfireMultiSelect
                or HistoryBookRewriteOption.ShopFree
                or HistoryBookRewriteOption.ShopRestock => "仅残卷持有者当前房间",
            HistoryBookRewriteOption.RestToFreeShop
                or HistoryBookRewriteOption.ShopToCampfire => "暂搁置房间互换",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取某个改写选项的消耗。
    /// </summary>
    /// <param name="option">改写选项。</param>
    /// <returns>消耗次数。</returns>
    private static int GetRewriteCost(HistoryBookRewriteOption option)
    {
        return option switch
        {
            HistoryBookRewriteOption.MonsterHpToOne => 2,
            HistoryBookRewriteOption.StunAllMonsters => 1,
            HistoryBookRewriteOption.DrawFullAndGainEnergy => 1,
            HistoryBookRewriteOption.ResetCombatPiles => 1,
            HistoryBookRewriteOption.ExhaustStatusAndCurse => 1,
            HistoryBookRewriteOption.CampfireMultiSelect => 2,
            HistoryBookRewriteOption.RestToFreeShop => 4,
            HistoryBookRewriteOption.ShopFree => 2,
            HistoryBookRewriteOption.ShopToCampfire => 4,
            HistoryBookRewriteOption.ShopRestock => 6,
            _ => 0
        };
    }

    /// <summary>
    /// 判断是否为暂搁置的房间互换选项。
    /// </summary>
    /// <param name="option">改写选项。</param>
    /// <returns>是暂搁置选项时返回 true。</returns>
    private static bool IsPlaceholderRewrite(HistoryBookRewriteOption option)
    {
        return option is HistoryBookRewriteOption.RestToFreeShop or HistoryBookRewriteOption.ShopToCampfire;
    }

    /// <summary>
    /// 记录暂搁置选项原因。
    /// </summary>
    /// <param name="option">改写选项。</param>
    private static void LogPlaceholderRewrite(HistoryBookRewriteOption option)
    {
        // TODO：塔2房间互换涉及 RunLocation、房间节点和联机房间恢复流程；直接替换房间风险高，先保留选项展示但不执行、不扣次数。
        MainFile.Logger.Info($"【岁月史书残卷】选项暂未执行：选项={option}，原因=塔2房间互换需要额外确认房间节点与联机同步恢复流程，当前按需求暂时搁置且不消耗次数。");
    }

    /// <summary>
    /// 判断指定玩家是不是本地玩家。
    /// </summary>
    /// <param name="player">待判断玩家。</param>
    /// <returns>是本地玩家时返回 true。</returns>
    private static bool IsLocalPlayer(Player? player)
    {
        return player != null && LocalContext.IsMe(player);
    }

    /// <summary>
    /// 战斗开局抽牌堆卡牌快照。
    /// </summary>
    /// <param name="CardId">卡牌 ID。</param>
    /// <param name="IsUpgraded">是否升级。</param>
    private readonly record struct HistoryBookCardSnapshot(string CardId, bool IsUpgraded);
}
