using System.Reflection;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 马符咒：右键激发后选择移除自身1个负面效果，进入2回合冷却。
/// 战斗结束时回复最大生命值的一定比例，每成功移除1次负面效果会降低回复比例。
/// </summary>
public class HorseTalisman : ShengZhuSts2ModRelic, ITalismanRightClickable, ITalismanCooldownResettable, ITalismanInteractionState
{
    /// <summary>
    /// 马符咒基础战后治疗比例。
    /// </summary>
    private const int BaseHealPercent = 20;

    /// <summary>
    /// 鼠符咒共鸣时的基础战后治疗比例。
    /// </summary>
    private const int ResonanceBaseHealPercent = 30;

    /// <summary>
    /// 每成功移除1次负面效果降低的治疗比例。
    /// </summary>
    private const int HealPercentLossPerUse = 5;

    /// <summary>
    /// 战后治疗比例下限。
    /// </summary>
    private const int MinHealPercent = 5;

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
    /// 本场战斗成功移除负面效果的次数。
    /// </summary>
    private int _removedDebuffCountThisCombat;

    /// <summary>
    /// 是否正在处理负面效果选择，防止自动挡和右键重复弹窗。
    /// </summary>
    private bool _isProcessing;

    /// <summary>
    /// 本场战斗是否停止马符咒自动挡询问。玩家在自动挡选择退出后置为 true。
    /// </summary>
    private bool _suppressAutoPromptThisCombat;

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
    /// 马符咒右键立即生效，没有预备激发状态。
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
    /// 战斗开始时重置状态。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        _cooldownTurnsRemaining = 0;
        _removedDebuffCountThisCombat = 0;
        _isProcessing = false;
        _suppressAutoPromptThisCombat = false;
        UpdateTalismanVisualState(false, _cooldownTurnsRemaining);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 右键点击：若未冷却，让玩家选择移除自身一个负面效果并进入冷却。
    /// 圣主石像buff不可被移除，石像内置虚弱也不以普通虚弱Power形式出现。
    /// </summary>
    public async Task OnRightClick(PlayerChoiceContext context)
    {
        await RelicInteractionSyncService.RequestHorseUse(this, false);
    }

    /// <summary>
    /// 尝试触发马符咒负面效果移除流程。
    /// </summary>
    /// <param name="context">玩家选择上下文。</param>
    /// <param name="isAutoTrigger">是否为自动挡触发。</param>
    /// <returns>异步任务。</returns>
    internal async Task RunSyncedRemoveDebuff(PlayerChoiceContext context, bool isAutoTrigger)
    {
        if (_cooldownTurnsRemaining > 0 || Owner?.Creature == null || _isProcessing)
        {
            return;
        }

        if (isAutoTrigger && _suppressAutoPromptThisCombat)
        {
            MainFile.Logger.Info("【马符咒】本场战斗已关闭自动挡询问，跳过本次自动触发。");
            return;
        }

        _isProcessing = true;
        try
        {
            // 收集所有可移除的负面效果：
            // 1. 类型为Debuff的Power（排除圣主石像和阴阳失衡）
            // 2. 值可正可负的Power（力量、敏捷、影噬、集中等），当Amount为负时视为负面效果
            List<PowerModel> removableDebuffs = Owner.Creature.Powers
                .Where(p => IsRemovableNegativeEffect(p))
                .OrderBy(p => p.Id.Entry, StringComparer.Ordinal)
                .ThenBy(p => p.GetType().FullName, StringComparer.Ordinal)
                .ThenBy(p => p.Amount)
                .ToList();

            string triggerSource = isAutoTrigger ? "半自动挡触发" : "右键触发";
            MainFile.Logger.Info($"【联机同步】【马符咒】来源={triggerSource}，玩家={Owner.NetId}，动作=准备移除负面效果，候选数={removableDebuffs.Count}，候选ID=[{FormatPowerKeys(removableDebuffs)}]。");
            if (removableDebuffs.Count == 0)
            {
                MainFile.Logger.Info("【马符咒】当前没有可移除的负面效果。");
                return;
            }

            HorseDebuffSelection selection = await SelectDebuffFromGrid(context, removableDebuffs);

            if (selection.Kind == HorseDebuffSelectionKind.Exit)
            {
                if (isAutoTrigger)
                {
                    _suppressAutoPromptThisCombat = true;
                    MainFile.Logger.Info($"【联机同步】【马符咒】来源={triggerSource}，玩家={Owner.NetId}，动作=选择负面效果，选中=退出自动挡。");
                    MainFile.Logger.Info("【马符咒】玩家通过退出卡关闭了本场战斗的自动挡询问。");
                }
                else
                {
                    MainFile.Logger.Info($"【联机同步】【马符咒】来源={triggerSource}，玩家={Owner.NetId}，动作=选择负面效果，选中=退出。");
                    MainFile.Logger.Info("【马符咒】玩家通过退出卡退出了本次手动选择。");
                }

                return;
            }

            PowerModel? selectedDebuff = selection.Power;
            if (selection.Kind == HorseDebuffSelectionKind.Canceled || selectedDebuff == null)
            {
                MainFile.Logger.Info($"【联机同步】【马符咒】来源={triggerSource}，玩家={Owner.NetId}，动作=选择负面效果，选中=取消。");
                MainFile.Logger.Info("【马符咒】玩家取消了负面效果选择。");
                return;
            }

            MainFile.Logger.Info($"【联机同步】【马符咒】来源={triggerSource}，玩家={Owner.NetId}，动作=选择负面效果，选中={FormatPowerKey(selectedDebuff)}。");
            // 执行移除
            _removedDebuffCountThisCombat++;
            _cooldownTurnsRemaining = 2;
            UpdateTalismanVisualState(false, _cooldownTurnsRemaining);
            Flash();
            MainFile.Logger.Info($"【马符咒】移除负面效果：{selectedDebuff.Id.Entry}");
            await PowerCmd.Remove(selectedDebuff);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 备用自动挡选择逻辑：只有一个候选时自动选中。
    /// 当前禁用，不参与调用。原因是马符咒移除负面效果必须保留玩家选择权，不能在自动挡下替玩家直接决定。
    /// 如果以后确认需求要恢复自动选择，只能在明确同步路径和玩家体验都确认后再接回调用点。
    /// </summary>
    /// <param name="debuffs">可移除的负面效果列表。</param>
    /// <param name="triggerSource">触发来源，写日志用。</param>
    /// <returns>自动选择结果；候选数不是 1 时返回跳过。</returns>
    private HorseDebuffSelection SelectOnlyDebuffForAutoTriggerIfEnabled(IReadOnlyList<PowerModel> debuffs, string triggerSource)
    {
        if (debuffs.Count == 1)
        {
            PowerModel selectedDebuff = debuffs[0];
            MainFile.Logger.Info($"【联机同步】【马符咒】来源={triggerSource}，玩家={Owner?.NetId}，动作=备用自动选择负面效果，候选数=1，选中={FormatPowerKey(selectedDebuff)}，规则=唯一候选直接移除。");
            return HorseDebuffSelection.Selected(selectedDebuff);
        }

        MainFile.Logger.Info($"【联机同步】【马符咒】来源={triggerSource}，玩家={Owner?.NetId}，动作=备用自动选择负面效果，候选数={debuffs.Count}，处理=跳过，原因=候选数不是1。");
        return HorseDebuffSelection.Skipped();
    }

    /// <summary>
    /// 通过卡牌选择界面让玩家选择要移除的负面效果。
    /// 利用反射修改预览卡标题为对应debuff的国际化名称，使玩家能区分。
    /// </summary>
    /// <param name="context">玩家选择上下文。</param>
    /// <param name="debuffs">可移除的负面效果列表。</param>
    /// <returns>玩家选择结果。</returns>
    private async Task<HorseDebuffSelection> SelectDebuffFromGrid(PlayerChoiceContext context, List<PowerModel> debuffs)
    {
        var combatState = Owner?.Creature?.CombatState;
        if (combatState == null || Owner == null)
        {
            return HorseDebuffSelection.Canceled();
        }

        // 为每个debuff创建预览卡，并建立映射关系
        var cardToPowerMap = new List<(CardModel card, PowerModel power)>();
        CardModel? exitCard = ShadowKhanCardHelper.CreateCardByType(combatState, typeof(HorseTalismanExitPreviewCard), Owner);

        foreach (PowerModel debuff in debuffs)
        {
            CardModel? previewCard = ShadowKhanCardHelper.CreateCardByType(combatState, typeof(DebuffPreviewCard), Owner);
            if (previewCard == null)
            {
                continue;
            }

            // 设置层数动态变量
            if (previewCard.DynamicVars.ContainsKey("DebuffAmount"))
            {
                previewCard.DynamicVars["DebuffAmount"].BaseValue = (int)debuff.Amount;
            }

            // 通过反射将预览卡标题替换为对应debuff的国际化名称
            OverrideCardTitle(previewCard, debuff);

            cardToPowerMap.Add((previewCard, debuff));
        }

        if (cardToPowerMap.Count == 0)
        {
            return HorseDebuffSelection.Canceled();
        }

        // 弹出选择界面
        List<CardModel> previewCards = cardToPowerMap.Select(pair => pair.card).ToList();
        if (exitCard != null)
        {
            previewCards.Add(exitCard);
        }
        else
        {
            MainFile.Logger.Info("【马符咒】退出伪卡片创建失败，本次选择界面仅展示负面效果。");
        }

        CardSelectorPrefs prefs = new(
            new LocString("relics", "SHENGZHUSTS2MOD-HORSE_TALISMAN.selectionScreenPrompt"),
            1,
            1)
        {
            Cancelable = true
        };

        IReadOnlyList<CardModel> selectedCards = (await CardSelectCmd.FromSimpleGrid(
            context,
            previewCards,
            Owner,
            prefs)).ToList();

        CardModel? selectedCard = selectedCards.FirstOrDefault();
        if (selectedCard == null)
        {
            return HorseDebuffSelection.Canceled();
        }

        if (exitCard != null && ReferenceEquals(exitCard, selectedCard))
        {
            return HorseDebuffSelection.Exit();
        }

        // 通过引用匹配找到对应的Power
        var match = cardToPowerMap.FirstOrDefault(pair => ReferenceEquals(pair.card, selectedCard));
        return match.power == null ? HorseDebuffSelection.Canceled() : HorseDebuffSelection.Selected(match.power);
    }

    /// <summary>
    /// 格式化负面效果列表，方便联机两端对比候选顺序。
    /// </summary>
    /// <param name="powers">候选负面效果。</param>
    /// <returns>负面效果稳定键文本。</returns>
    private static string FormatPowerKeys(IEnumerable<PowerModel> powers)
    {
        return string.Join(",", powers.Select(FormatPowerKey));
    }

    /// <summary>
    /// 格式化单个负面效果的稳定键。
    /// </summary>
    /// <param name="power">目标负面效果。</param>
    /// <returns>负面效果稳定键。</returns>
    private static string FormatPowerKey(PowerModel power)
    {
        return $"{power.Id.Entry}/{power.GetType().Name}/{power.Amount}";
    }

    /// <summary>
    /// CardModel._titleLocString 字段的反射缓存。
    /// </summary>
    private static readonly FieldInfo? TitleLocStringField =
        typeof(CardModel).GetField("_titleLocString", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 通过反射将预览卡的标题替换为对应Power的国际化名称。
    /// </summary>
    /// <param name="card">要修改标题的预览卡。</param>
    /// <param name="power">对应的负面效果。</param>
    private static void OverrideCardTitle(CardModel card, PowerModel power)
    {
        if (TitleLocStringField == null)
        {
            MainFile.Logger.Info("【马符咒】反射获取 _titleLocString 字段失败，无法覆盖预览卡标题。");
            return;
        }

        // 使用Power自身的国际化标题（powers表中的 entry.title）
        var powerTitleLocString = new LocString("powers", power.Id.Entry + ".title");
        TitleLocStringField.SetValue(card, powerTitleLocString);
    }

    /// <summary>
    /// 判断一个Power是否属于可被马符咒移除的负面效果。
    /// 包含两类：
    /// 1. 类型为Debuff的Power（排除圣主石像和阴阳失衡，它们是核心机制不可移除）
    /// 2. 值可正可负的Power（力量、敏捷、影噬、集中等），当Amount为负时视为负面效果
    /// </summary>
    /// <param name="power">待判断的Power。</param>
    /// <returns>是否可被移除。</returns>
    private static bool IsRemovableNegativeEffect(PowerModel power)
    {
        // 圣主石像和阴阳失衡是核心机制，永远不可被马符咒移除
        if (power is ShenZhuStatuePower or TalismanImbalancePlayerPower)
        {
            return false;
        }

        // 类型为Debuff的Power直接视为可移除的负面效果
        if (power.Type == PowerType.Debuff)
        {
            return true;
        }

        // 值可正可负的Power：Amount为负时视为负面效果
        if (power.Amount < 0 && IsNumericalPower(power))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 判断一个Power是否属于"值可正可负"的数值型Power。
    /// 这类Power的Amount为负时代表负面效果（如-3力量、-2敏捷）。
    /// </summary>
    /// <param name="power">待判断的Power。</param>
    /// <returns>是否为数值型Power。</returns>
    private static bool IsNumericalPower(PowerModel power)
    {
        return power is StrengthPower
            or DexterityPower
            or DominionPower
            or FocusPower;
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
    /// 战斗结束时：根据本场成功移除负面效果次数，回复最大生命值的一定比例。
    /// 鼠符咒共鸣：基础回复比例从20%提升为30%。
    /// </summary>
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        if (Owner?.Creature != null)
        {
            int healPercent = CalculateCombatEndHealPercent();
            int healAmount = (int)(Owner.Creature.MaxHp * healPercent / 100m);
            if (healAmount > 0)
            {
                Flash();
                MainFile.Logger.Info($"【马符咒】战斗结束治疗：基础比例={(HasRatResonance() ? ResonanceBaseHealPercent : BaseHealPercent)}%，本战移除次数={_removedDebuffCountThisCombat}，最终比例={healPercent}%，治疗={healAmount}。");
                await CreatureCmd.Heal(Owner.Creature, healAmount);
            }
        }
    }

    /// <summary>
    /// 计算本场战斗结束时的马符咒治疗比例。
    /// </summary>
    /// <returns>治疗最大生命值的百分比。</returns>
    private int CalculateCombatEndHealPercent()
    {
        int basePercent = HasRatResonance() ? ResonanceBaseHealPercent : BaseHealPercent;
        return Math.Max(MinHealPercent, basePercent - _removedDebuffCountThisCombat * HealPercentLossPerUse);
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

    /// <summary>
    /// 设置激发状态（供半自动模式调用）。
    /// 马符咒的"激发"就是直接尝试触发移除效果。
    /// </summary>
    public void SetActivated(bool activate)
    {
        if (!activate || _cooldownTurnsRemaining > 0 || _isProcessing || Owner?.Creature == null)
        {
            return;
        }

        QueueAutoRemoveDebuff();
    }

    /// <summary>
    /// 排队触发半自动移除流程，避免裸 fire-and-forget 隐藏异常。
    /// </summary>
    private void QueueAutoRemoveDebuff()
    {
        if (_cooldownTurnsRemaining > 0 || _isProcessing || Owner?.Creature == null)
        {
            MainFile.Logger.Info("【马符咒】半自动触发被跳过：冷却中、处理中或玩家战斗状态不可用。");
            return;
        }

        _ = TriggerAutoRemoveDebuff();
    }

    /// <summary>
    /// 半自动挡触发马符咒移除流程。使用异步保护，避免后台任务异常影响战斗流程。
    /// </summary>
    /// <returns>异步任务。</returns>
    private async Task TriggerAutoRemoveDebuff()
    {
        try
        {
            await RelicInteractionSyncService.RequestHorseUse(this, true);
        }
        catch (System.Exception ex)
        {
            MainFile.Logger.Info($"【马符咒】半自动触发移除负面效果时发生异常：{ex}");
        }
    }

    /// <summary>
    /// 马符咒选择界面的结果类型。
    /// </summary>
    private enum HorseDebuffSelectionKind
    {
        /// <summary>玩家选中了一个负面效果。</summary>
        Selected,

        /// <summary>玩家选中了退出伪卡片。</summary>
        Exit,

        /// <summary>玩家使用选择界面自带取消或选择无效。</summary>
        Canceled,

        /// <summary>自动挡为了避免联机选择项分叉而跳过。</summary>
        Skipped
    }

    /// <summary>
    /// 马符咒选择界面的结果。
    /// </summary>
    /// <param name="Kind">选择类型。</param>
    /// <param name="Power">选中的负面效果。</param>
    private sealed record HorseDebuffSelection(HorseDebuffSelectionKind Kind, PowerModel? Power)
    {
        /// <summary>
        /// 创建选中负面效果的结果。
        /// </summary>
        /// <param name="power">被选中的负面效果。</param>
        /// <returns>选择结果。</returns>
        public static HorseDebuffSelection Selected(PowerModel power)
        {
            return new HorseDebuffSelection(HorseDebuffSelectionKind.Selected, power);
        }

        /// <summary>
        /// 创建退出结果。
        /// </summary>
        /// <returns>选择结果。</returns>
        public static HorseDebuffSelection Exit()
        {
            return new HorseDebuffSelection(HorseDebuffSelectionKind.Exit, null);
        }

        /// <summary>
        /// 创建取消结果。
        /// </summary>
        /// <returns>选择结果。</returns>
        public static HorseDebuffSelection Canceled()
        {
            return new HorseDebuffSelection(HorseDebuffSelectionKind.Canceled, null);
        }

        /// <summary>
        /// 创建自动挡跳过结果。
        /// </summary>
        /// <returns>选择结果。</returns>
        public static HorseDebuffSelection Skipped()
        {
            return new HorseDebuffSelection(HorseDebuffSelectionKind.Skipped, null);
        }
    }
}
