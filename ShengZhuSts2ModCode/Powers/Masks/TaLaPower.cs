using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

/// <summary>
/// 塔拉面具能力：增加面具容量，并在回合开始时随机生成黑影兵团卡。
/// Amount 表示当前扩充了多少面具栏位。
/// SecondAmount 显示塔拉施放次数（即每回合生成兵团牌的数量）。
/// 塔拉的施放记录以能力实例为准，MaskManager 只做同场缓存，避免随 Power 模型实例复用而串场。
/// </summary>
public class TaLaPower : ShengZhuSts2ModPower, IHasSecondAmount
{
    /// <summary>
    /// 塔拉施放记录分隔符。
    /// </summary>
    private const char RecordSeparator = ';';

    /// <summary>
    /// 塔拉施放记录字段分隔符。
    /// </summary>
    private const char RecordFieldSeparator = ',';

    /// <summary>
    /// 当前塔拉能力实例持有的施放记录。
    /// 以能力实例为准，避免 MaskManager 静态状态被重置后丢失本场已打出的塔拉。
    /// </summary>
    private List<MaskManager.MaskStackRecord> _maskCastRecords = [];

    /// <summary>
    /// 保存塔拉施放记录，避免战斗中 SL 后丢失基础/升级和减费信息。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public string SavedMaskCastRecordsCsv { get; set; } = string.Empty;

    /// <summary>
    /// 保存塔拉随机兵团生成序号，保证 SL 后同一段流程拿到同样的兵团牌。
    /// </summary>
    [SavedProperty(SerializationCondition.AlwaysSave)]
    public int StableShadowKhanRandomCursor { get; set; }

    /// <summary>
    /// 记录塔拉后续生成的黑影兵团卡是否为升级版。
    /// </summary>
    public bool GenerateUpgraded { get; set; }

    /// <summary>
    /// 塔拉属于增益。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 塔拉使用计数器显示。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 模型克隆时复制塔拉施放记录列表，避免多个塔拉能力实例共用同一个List导致层数多算。
    /// </summary>
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _maskCastRecords = new List<MaskManager.MaskStackRecord>(_maskCastRecords);
    }

    /// <summary>
    /// 塔拉首次施加后只同步面具容量。
    /// 塔拉充能球和施放次数由 RecordMaskCast 统一维护，避免同名塔拉重复生成多个球。
    /// </summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner == null)
        {
            return Task.CompletedTask;
        }

        var player = Owner.Player;
        if (player == null)
        {
            return Task.CompletedTask;
        }

        var orbQueue = player.PlayerCombatState?.OrbQueue;
        if (orbQueue == null)
        {
            return Task.CompletedTask;
        }

        MainFile.Logger.Info($"【塔拉充能球】AfterApplied跳过球生成，等待RecordMaskCast统一同步：扩充栏位Amount={Amount}，当前轨道容量={orbQueue.Capacity}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 回合开始时按当前塔拉真实施放记录数随机生成黑影兵团卡；手牌已满则自动打出。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null || player.Creature != Owner || Owner.CombatState == null || Owner.IsDead)
        {
            return;
        }

        var combatState = Owner.CombatState;
        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState == null)
        {
            return;
        }

        Flash();

        List<MaskManager.MaskStackRecord> castRecords = GetMaskCastRecordsSnapshot();
        MainFile.Logger.Info($"【塔拉面具】回合开始发兵团：扩充栏位={Amount}，塔拉真实施放记录数={castRecords.Count}，升级记录数={castRecords.Count(record => record.Upgraded)}");

        foreach (MaskManager.MaskStackRecord record in castRecords)
        {
            await AddRandomShadowKhanToHand(choiceContext, player, record.Upgraded);
        }
    }

    /// <summary>
    /// 随机生成一张黑影兵团牌加入手牌；手牌已满时自动打出。
    /// </summary>
    public async Task AddRandomShadowKhanToHand(PlayerChoiceContext choiceContext, Player player, bool upgraded)
    {
        var combatState = player.Creature?.CombatState;
        var playerCombatState = player.PlayerCombatState;
        if (combatState == null || playerCombatState == null)
        {
            return;
        }

        int randomCursor = Math.Max(0, StableShadowKhanRandomCursor);
        int selectedIndex = GetStableShadowKhanTypeIndex(player, upgraded, randomCursor);
        StableShadowKhanRandomCursor = randomCursor + 1;

        var selectedType = ShadowKhanCardHelper.ShadowKhanTypes[selectedIndex];
        var shadowKhanCard = ShadowKhanCardHelper.CreateCardByType(combatState, selectedType, player);
        if (shadowKhanCard == null)
        {
            MainFile.Logger.Info($"【塔拉面具】稳定随机兵团创建失败：类型={selectedType.Name}，序号={randomCursor}");
            return;
        }

        if (upgraded)
        {
            CardCmd.Upgrade(shadowKhanCard, CardPreviewStyle.None);
        }

        if (playerCombatState.Hand.Cards.Count >= 10)
        {
            BitterChoiceNextTurnCostPower.TryApplyToGeneratedShadowKhanCard(player, shadowKhanCard, "塔拉面具满手自动打出");
            await CardPileCmdHelper.AddGeneratedCardToCombat(shadowKhanCard, PileType.Play, addedByPlayer: true);
            await CardCmd.AutoPlay(choiceContext, shadowKhanCard, null);
        }
        else
        {
            BitterChoiceNextTurnCostPower.TryApplyToGeneratedShadowKhanCard(player, shadowKhanCard, "塔拉面具加入手牌");
            await CardPileCmdHelper.AddGeneratedCardToCombat(shadowKhanCard, PileType.Hand, addedByPlayer: true);
        }

        MainFile.Logger.Info($"【塔拉面具】稳定随机获得黑影兵团：{shadowKhanCard.Title}，升级版={upgraded}，序号={randomCursor}，下次序号={StableShadowKhanRandomCursor}");
    }

    /// <summary>
    /// 记录一次塔拉施放信息，供剥离返牌使用。
    /// 记录后同步充能球显示为最新施放次数。
    /// </summary>
    public async Task RecordMaskCast(bool upgraded, int permanentCostReductionCount)
    {
        RestoreExactMaskCastRecordsIfNeeded();
        _maskCastRecords.Add(new MaskManager.MaskStackRecord(upgraded, Math.Max(0, permanentCostReductionCount)));
        SyncSavedMaskCastRecords();
        MaskManager.RecordTaLaMaskCast(Owner?.Player, upgraded, permanentCostReductionCount);
        int castCount = GetRecordedMaskCastCount();

        // 同步充能球显示为施放次数
        var player = Owner?.Player;
        if (player != null)
        {
            await MaskManager.EnsureMaskCapacitySlotsAsync(player);
            if (!MaskManager.TryUpdateMaskOrbAmount(player, MaskManager.TaLaMaskPowerKey, castCount, Id.Entry))
            {
                await MaskManager.EnsureMaskOrbDisplayed(player, MaskManager.TaLaMaskPowerKey, castCount, Id.Entry);
            }
            MaskManager.UpdateMaskOrbs(player);
        }

        // 通知UI刷新第二数值显示
        this.InvokeSecondAmountChanged();

        MainFile.Logger.Info($"【塔拉面具】RecordMaskCast完成：升级版={upgraded}，扩充栏位Amount={Amount}，施放次数={castCount}，每回合随机兵团数={GetRandomShadowKhanCardsPerTurn()}");
    }

    /// <summary>
    /// IHasSecondAmount 实现：显示塔拉施放次数（即每回合生成兵团牌的数量）。
    /// </summary>
    public string GetSecondAmount()
    {
        return GetRecordedMaskCastCount().ToString();
    }

    /// <summary>
    /// 获取当前塔拉真实施放记录数。
    /// </summary>
    public int GetRecordedMaskCastCount()
    {
        return GetExactMaskCastRecordsSnapshot().Count;
    }

    /// <summary>
    /// 获取剥离时应返还的塔拉面具总数。
    /// </summary>
    public int GetTalaCastCountForPeel()
    {
        return GetPeelMaskRecordsSnapshot().Count;
    }

    /// <summary>
    /// 获取剥离时应返还的基础版塔拉面具数量。
    /// </summary>
    public int GetPeelBaseMaskCount()
    {
        return GetPeelMaskRecordsSnapshot().Count(record => !record.Upgraded);
    }

    /// <summary>
    /// 获取剥离时应返还的升级版塔拉面具数量。
    /// </summary>
    public int GetPeelUpgradedMaskCount()
    {
        return GetPeelMaskRecordsSnapshot().Count(record => record.Upgraded);
    }

    /// <summary>
    /// 获取当前塔拉每回合随机生成黑影兵团卡的数量。
    /// </summary>
    public int GetRandomShadowKhanCardsPerTurn()
    {
        return GetRecordedMaskCastCount();
    }

    /// <summary>
    /// 获取塔拉剥离时使用的记录快照。
    /// 只返回真实施放记录，不再根据扩容层数反推张数、升级状态或费用。
    /// </summary>
    public List<MaskManager.MaskStackRecord> GetPeelMaskRecordsSnapshot()
    {
        return GetExactMaskCastRecordsSnapshot();
    }

    /// <summary>
    /// 获取回合开始发牌时使用的塔拉施放记录快照。
    /// </summary>
    public List<MaskManager.MaskStackRecord> GetMaskCastRecordsSnapshot()
    {
        return GetExactMaskCastRecordsSnapshot();
    }

    /// <summary>
    /// 获取精确的塔拉施放记录快照。
    /// 优先使用能力实例记录；实例为空时只接收 MaskManager 中已有的真实缓存，不做任何反推。
    /// </summary>
    private List<MaskManager.MaskStackRecord> GetExactMaskCastRecordsSnapshot()
    {
        RestoreExactMaskCastRecordsIfNeeded();

        if (_maskCastRecords.Count > 0)
        {
            return new List<MaskManager.MaskStackRecord>(_maskCastRecords);
        }

        return MaskManager.GetTaLaMaskCastRecordsSnapshot(Owner?.Player);
    }

    /// <summary>
    /// 当前能力实例没有记录时，从 MaskManager 接回同场真实缓存。
    /// 只在缓存里确实存在记录时恢复，避免按容量猜测导致基础/升级和减费次数错乱。
    /// </summary>
    private void RestoreExactMaskCastRecordsIfNeeded()
    {
        if (_maskCastRecords.Count > 0)
        {
            return;
        }

        var savedRecords = ParseSavedMaskCastRecords();
        if (savedRecords.Count > 0)
        {
            _maskCastRecords.AddRange(savedRecords);
            MainFile.Logger.Info($"【塔拉面具】从存档恢复真实施放记录：记录数={_maskCastRecords.Count}，升级记录数={_maskCastRecords.Count(record => record.Upgraded)}");
            return;
        }

        var cachedRecords = MaskManager.GetTaLaMaskCastRecordsSnapshot(Owner?.Player);
        if (cachedRecords.Count <= 0)
        {
            return;
        }

        _maskCastRecords.AddRange(cachedRecords);
        SyncSavedMaskCastRecords();
        MainFile.Logger.Info($"【塔拉面具】从管理器恢复真实施放记录：记录数={_maskCastRecords.Count}，升级记录数={_maskCastRecords.Count(record => record.Upgraded)}");
    }

    /// <summary>
    /// 根据局种子、当前能力和生成序号稳定选择一张黑影兵团牌。
    /// </summary>
    private int GetStableShadowKhanTypeIndex(Player player, bool upgraded, int randomCursor)
    {
        uint hash = 2166136261u;
        AddStableHash(ref hash, player.RunState.Rng.StringSeed);
        AddStableHash(ref hash, player.NetId.ToString());
        AddStableHash(ref hash, player.RunState.CurrentActIndex.ToString());
        AddStableHash(ref hash, player.RunState.ActFloor.ToString());
        AddStableHash(ref hash, player.RunState.TotalFloor.ToString());
        AddStableHash(ref hash, player.RunState.CurrentMapCoord?.col.ToString() ?? "NO_COL");
        AddStableHash(ref hash, player.RunState.CurrentMapCoord?.row.ToString() ?? "NO_ROW");
        AddStableHash(ref hash, Id.Entry);
        AddStableHash(ref hash, GetExactMaskCastRecordsSnapshot().Count.ToString());
        AddStableHash(ref hash, upgraded ? "UPGRADED" : "BASE");
        AddStableHash(ref hash, randomCursor.ToString());
        return (int)(hash % (uint)ShadowKhanCardHelper.ShadowKhanTypes.Length);
    }

    /// <summary>
    /// 使用固定 FNV-1a 算法追加字符串，避免使用运行时随机数。
    /// </summary>
    private static void AddStableHash(ref uint hash, string value)
    {
        foreach (char character in value)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        hash ^= '|';
        hash *= 16777619u;
    }

    /// <summary>
    /// 将当前塔拉施放记录同步到可保存字符串。
    /// </summary>
    private void SyncSavedMaskCastRecords()
    {
        SavedMaskCastRecordsCsv = string.Join(RecordSeparator, _maskCastRecords.Select(FormatMaskCastRecord));
    }

    /// <summary>
    /// 格式化单条塔拉施放记录。
    /// </summary>
    private static string FormatMaskCastRecord(MaskManager.MaskStackRecord record)
    {
        return $"{(record.Upgraded ? 1 : 0)}{RecordFieldSeparator}{Math.Max(0, record.PermanentCostReductionCount)}";
    }

    /// <summary>
    /// 解析存档中的塔拉施放记录。
    /// </summary>
    private List<MaskManager.MaskStackRecord> ParseSavedMaskCastRecords()
    {
        List<MaskManager.MaskStackRecord> records = [];
        foreach (string part in SavedMaskCastRecordsCsv.Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = part.Split(RecordFieldSeparator);
            if (fields.Length != 2 || !int.TryParse(fields[0], out int upgradedFlag))
            {
                continue;
            }

            int costReduction = int.TryParse(fields[1], out int parsedCostReduction)
                ? Math.Max(0, parsedCostReduction)
                : 0;
            records.Add(new MaskManager.MaskStackRecord(upgradedFlag == 1, costReduction));
        }

        return records;
    }
}
