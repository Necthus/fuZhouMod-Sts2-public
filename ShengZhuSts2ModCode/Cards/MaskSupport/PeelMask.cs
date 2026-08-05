using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport.PeelMaskPreview;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;

/// <summary>
/// 剥离面具：选择一个当前已加载的面具剥离。
/// 普通面具会返对应兵团卡与面具牌；塔拉会返塔拉面具牌、随机兵团卡并回收扩展栏位。
/// </summary>
public class PeelMask : BaseMaskSupportCard
{
    /// <summary>
    /// 预览选择项。
    /// </summary>
    private sealed record PeelOption(CardModel PreviewCard, string MaskPowerKey, BaseMaskPower? MaskPower, TaLaPower? TaLaPower);

    /// <summary>
    /// 构造卡牌数值：1费技能，基础版返还面具降费，升级后额外增加面具容量。
    /// </summary>
    public PeelMask() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
    {
    }

    /// <summary>
    /// 升级时不再降低自身费用，改为剥离成功后增加面具容量。
    /// </summary>
    protected override void OnUpgrade()
    {
    }

    /// <summary>
    /// 出牌时选择一个已加载的面具进行剥离。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = Owner?.Creature;
        var player = Owner;
        if (creature == null || player == null)
        {
            return;
        }

        var options = BuildPeelOptions(creature, player);
        if (options.Count == 0)
        {
            MainFile.Logger.Info("【剥离面具】当前没有可剥离的面具。");
            return;
        }

        PeelOption selectedOption;
        if (options.Count == 1)
        {
            selectedOption = options[0];
        }
        else
        {
            var prefs = new CardSelectorPrefs(new LocString("cards", Id.Entry + ".selectionScreenPrompt"), 1, 1);
            var selectedCard = (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                options.Select(option => option.PreviewCard).ToList(),
                Owner!,
                prefs)).FirstOrDefault();
            if (selectedCard == null)
            {
                return;
            }

            selectedOption = options.First(option =>
                ReferenceEquals(option.PreviewCard, selectedCard)
                || option.PreviewCard.Id.Entry == selectedCard.Id.Entry);
        }

        if (selectedOption.MaskPower != null)
        {
            await PerformPeel(choiceContext, player, selectedOption.MaskPower);
            return;
        }

        if (selectedOption.TaLaPower != null)
        {
            await PerformTaLaPeel(choiceContext, player, selectedOption.TaLaPower);
        }
    }

    /// <summary>
    /// 构建当前可剥离的面具选项。
    /// </summary>
    private List<PeelOption> BuildPeelOptions(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature, Player player)
    {
        var combatState = creature.CombatState;
        if (combatState == null)
        {
            return [];
        }

        var normalPowers = creature.Powers
            .OfType<BaseMaskPower>()
            .ToDictionary(power => power.MaskPowerKey, power => power);

        var options = new List<PeelOption>();
        foreach (var maskKey in MaskManager.GetCurrentMaskKeys(player))
        {
            if (!normalPowers.TryGetValue(maskKey, out var maskPower))
            {
                continue;
            }

            // 计算剥离后返还数量
            int maskAmount = maskPower.Amount;
            int maskReturnCount = maskAmount;
            int soldierReturnCount = maskAmount * MaskManager.EvictSoldierRefundPerStack;

            // 创建对应面具的预览卡（标题=面具名称，描述=返还信息）
            var previewCard = CreatePeelPreviewCard(combatState, player, maskKey, maskReturnCount, soldierReturnCount);
            if (previewCard == null)
            {
                continue;
            }

            options.Add(new PeelOption(previewCard, maskKey, maskPower, null));
        }

        var taLaPower = creature.GetPower<TaLaPower>();
        if (taLaPower != null)
        {
            // 塔拉面具：返还塔拉面具牌数 = 施放记录数，返还兵团数 = 施放记录数 × 2
            var peelRecords = taLaPower.GetPeelMaskRecordsSnapshot();
            int taLaReturnCount = peelRecords.Count;
            int taLaSoldierCount = taLaReturnCount * MaskManager.EvictSoldierRefundPerStack;
            MainFile.Logger.Info($"【剥离面具】构建塔拉剥离选项：塔拉记录数={taLaReturnCount}，基础={peelRecords.Count(record => !record.Upgraded)}，升级={peelRecords.Count(record => record.Upgraded)}");

            var previewCard = CreatePeelPreviewCard(combatState, player, MaskManager.TaLaMaskPowerKey, taLaReturnCount, taLaSoldierCount);
            if (previewCard != null)
            {
                options.Add(new PeelOption(previewCard, MaskManager.TaLaMaskPowerKey, null, taLaPower));
            }
        }

        return options;
    }

    /// <summary>
    /// 根据面具能力键创建对应的剥离预览卡实例。
    /// </summary>
    private static CardModel? CreatePeelPreviewCard(object combatState, Player player, string maskPowerKey, int maskReturn, int soldierReturn)
    {
        Type? previewType = GetPeelPreviewType(maskPowerKey);
        if (previewType == null)
        {
            return null;
        }

        var card = ShadowKhanCardHelper.CreateCardByType(combatState, previewType, player);
        if (card == null)
        {
            return null;
        }

        // 设置返还数量动态变量
        if (card.DynamicVars.ContainsKey("MaskReturn"))
        {
            card.DynamicVars["MaskReturn"].BaseValue = maskReturn;
        }

        if (card.DynamicVars.ContainsKey("SoldierReturn"))
        {
            card.DynamicVars["SoldierReturn"].BaseValue = soldierReturn;
        }

        return card;
    }

    /// <summary>
    /// 根据面具能力键映射对应的剥离预览卡类型。
    /// </summary>
    private static Type? GetPeelPreviewType(string maskPowerKey)
    {
        if (maskPowerKey == (typeof(NiJiaPower).FullName ?? typeof(NiJiaPower).Name))
            return typeof(PeelNiJiaPreview);
        if (maskPowerKey == (typeof(KaBoPower).FullName ?? typeof(KaBoPower).Name))
            return typeof(PeelKaBoPreview);
        if (maskPowerKey == (typeof(SaMoPower).FullName ?? typeof(SaMoPower).Name))
            return typeof(PeelSaMoPreview);
        if (maskPowerKey == (typeof(LaZuoPower).FullName ?? typeof(LaZuoPower).Name))
            return typeof(PeelLaZuoPreview);
        if (maskPowerKey == (typeof(LeiSuPower).FullName ?? typeof(LeiSuPower).Name))
            return typeof(PeelLeiSuPreview);
        if (maskPowerKey == (typeof(ManNiPower).FullName ?? typeof(ManNiPower).Name))
            return typeof(PeelManNiPreview);
        if (maskPowerKey == (typeof(MingTaPower).FullName ?? typeof(MingTaPower).Name))
            return typeof(PeelMingTaPreview);
        if (maskPowerKey == (typeof(BaTePower).FullName ?? typeof(BaTePower).Name))
            return typeof(PeelBaTePreview);
        if (maskPowerKey == (typeof(YiKaPower).FullName ?? typeof(YiKaPower).Name))
            return typeof(PeelYiKaPreview);
        if (maskPowerKey == MaskManager.TaLaMaskPowerKey)
            return typeof(PeelTaLaPreview);

        return null;
    }

    /// <summary>
    /// 执行普通面具剥离：返兵团卡、返面具牌、移除面具能力并同步显示。
    /// </summary>
    private async Task PerformPeel(PlayerChoiceContext choiceContext, Player player, BaseMaskPower maskPower)
    {
        MainFile.Logger.Info($"【剥离面具】剥离普通面具：{maskPower.MaskPowerKey}，层数={maskPower.Amount}");

        int returnedMaskCostReduction = 1;
        await maskPower.OnEvict(choiceContext, player, returnedMaskCostReduction);
        await PowerCmd.Remove(maskPower);
        await MaskManager.OnMaskRemovedAsync(maskPower.MaskPowerKey, player);
        await GrantUpgradedCapacity(player);

        if (Owner?.Creature != null)
        {
            await MaskManager.EnforceCapacityAsync(Owner.Creature, choiceContext);
        }
    }

    /// <summary>
    /// 升级版剥离成功后增加 1 点本场面具容量，并同步独立面具栏。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>异步任务。</returns>
    private async Task GrantUpgradedCapacity(Player player)
    {
        if (!IsUpgraded)
        {
            return;
        }

        MaskManager.AddExtraMaskCapacity(player, 1);
        await MaskManager.EnsureMaskCapacitySlotsAsync(player);
        MainFile.Logger.Info("【剥离面具】升级效果：剥离成功后面具容量+1。");
    }

    /// <summary>
    /// 执行塔拉面具剥离：返塔拉面具牌、随机返兵团卡、移除塔拉能力并回收扩展栏位。
    /// </summary>
    private async Task PerformTaLaPeel(PlayerChoiceContext choiceContext, Player player, TaLaPower taLaPower)
    {
        var peelRecords = taLaPower.GetPeelMaskRecordsSnapshot();
        int baseCount = taLaPower.GetPeelBaseMaskCount();
        int upgradedCount = taLaPower.GetPeelUpgradedMaskCount();
        int randomShadowCount = peelRecords.Count * MaskManager.EvictSoldierRefundPerStack;

        MainFile.Logger.Info($"【剥离面具】剥离塔拉面具：扩充栏位={taLaPower.Amount}，返基础塔拉={baseCount}，返升级塔拉={upgradedCount}，返塔拉总数={peelRecords.Count}，返随机兵团数={randomShadowCount}，当前每回合随机兵团数={taLaPower.GetRandomShadowKhanCardsPerTurn()}");

        var combatState = Owner?.Creature?.CombatState;
        if (combatState != null)
        {
            var returnedTaLaMaskCards = new List<CardModel>();
            foreach (var record in peelRecords)
            {
                var taLaMaskCard = MaskManager.CreateMaskCardByPowerKey(combatState, MaskManager.TaLaMaskPowerKey, player);
                if (taLaMaskCard == null)
                {
                    continue;
                }

                if (record.Upgraded)
                {
                    CardCmd.Upgrade(taLaMaskCard, CardPreviewStyle.None);
                }

                int totalReduction = Math.Max(0, record.PermanentCostReductionCount + 1);
                int taLaBaseCost = record.Upgraded ? 1 : 2;
                MaskManager.ApplyPermanentPeelCostReduction(taLaMaskCard, MaskManager.TaLaMaskPowerKey, totalReduction, taLaBaseCost);
                returnedTaLaMaskCards.Add(taLaMaskCard);
            }

            if (returnedTaLaMaskCards.Count > 0)
            {
                await MaskManager.AddGeneratedMaskCardsToDiscard(player, returnedTaLaMaskCards);
            }

            foreach (var record in peelRecords)
            {
                for (int i = 0; i < MaskManager.EvictSoldierRefundPerStack; i++)
                {
                    await taLaPower.AddRandomShadowKhanToHand(choiceContext, player, record.Upgraded);
                }
            }
        }

        await PowerCmd.Remove(taLaPower);
        await MaskManager.OnTaLaRemovedAsync(player);
        await GrantUpgradedCapacity(player);

        bool capacityChanged = false;
        if (Owner?.Creature != null)
        {
            capacityChanged = await MaskManager.EnforceCapacityAsync(Owner.Creature, choiceContext);
        }

        if (!capacityChanged)
        {
            await MaskManager.SyncMaskOrbsIncrementalAsync(player);
        }
    }
}
