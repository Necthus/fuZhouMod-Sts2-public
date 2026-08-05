using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.HistoryBookPreview;

/// <summary>
/// 岁月史书残卷改写菜单预览卡基类：只用于选择界面，不进入正常牌池。
/// </summary>
public abstract class HistoryBookRewritePreviewCard : ShengZhuSts2ModCard
{
    /// <summary>
    /// 预览伪卡不允许战斗中随机生成。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 预览伪卡不允许被修饰器或生成器随机选中。
    /// </summary>
    public override bool CanBeGeneratedByModifiers => false;

    /// <summary>
    /// 对应的残卷改写选项。
    /// </summary>
    internal abstract HistoryBookRewriteOption RewriteOption { get; }

    /// <summary>
    /// 消耗的残卷次数。
    /// </summary>
    internal abstract int Cost { get; }

    /// <summary>
    /// 构造残卷预览伪卡。
    /// </summary>
    protected HistoryBookRewritePreviewCard()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.None, false)
    {
    }
}

/// <summary>
/// 预览卡：所有存活怪物生命降到 1。
/// </summary>
public class HistoryBookMonsterHpToOnePreviewCard : HistoryBookRewritePreviewCard
{
    internal override HistoryBookRewriteOption RewriteOption => HistoryBookRewriteOption.MonsterHpToOne;
    internal override int Cost => 2;
}

/// <summary>
/// 预览卡：眩晕所有存活怪物。
/// </summary>
public class HistoryBookStunAllMonstersPreviewCard : HistoryBookRewritePreviewCard
{
    internal override HistoryBookRewriteOption RewriteOption => HistoryBookRewriteOption.StunAllMonsters;
    internal override int Cost => 1;
}

/// <summary>
/// 预览卡：抽满手牌并获得 12 点能量。
/// </summary>
public class HistoryBookDrawFullAndGainEnergyPreviewCard : HistoryBookRewritePreviewCard
{
    internal override HistoryBookRewriteOption RewriteOption => HistoryBookRewriteOption.DrawFullAndGainEnergy;
    internal override int Cost => 1;
}

/// <summary>
/// 预览卡：重置战斗牌堆到开局快照。
/// </summary>
public class HistoryBookResetCombatPilesPreviewCard : HistoryBookRewritePreviewCard
{
    internal override HistoryBookRewriteOption RewriteOption => HistoryBookRewriteOption.ResetCombatPiles;
    internal override int Cost => 1;
}

/// <summary>
/// 预览卡：消耗状态和诅咒。
/// </summary>
public class HistoryBookExhaustStatusAndCursePreviewCard : HistoryBookRewritePreviewCard
{
    internal override HistoryBookRewriteOption RewriteOption => HistoryBookRewriteOption.ExhaustStatusAndCurse;
    internal override int Cost => 1;
}

/// <summary>
/// 预览卡：休息处多选。
/// </summary>
public class HistoryBookCampfireMultiSelectPreviewCard : HistoryBookRewritePreviewCard
{
    internal override HistoryBookRewriteOption RewriteOption => HistoryBookRewriteOption.CampfireMultiSelect;
    internal override int Cost => 2;
}

/// <summary>
/// 预览卡：休息处变商店。
/// </summary>
public class HistoryBookRestToFreeShopPreviewCard : HistoryBookRewritePreviewCard
{
    internal override HistoryBookRewriteOption RewriteOption => HistoryBookRewriteOption.RestToFreeShop;
    internal override int Cost => 4;
}

/// <summary>
/// 预览卡：商店零元购。
/// </summary>
public class HistoryBookShopFreePreviewCard : HistoryBookRewritePreviewCard
{
    internal override HistoryBookRewriteOption RewriteOption => HistoryBookRewriteOption.ShopFree;
    internal override int Cost => 2;
}

/// <summary>
/// 预览卡：商店变休息处。
/// </summary>
public class HistoryBookShopToCampfirePreviewCard : HistoryBookRewritePreviewCard
{
    internal override HistoryBookRewriteOption RewriteOption => HistoryBookRewriteOption.ShopToCampfire;
    internal override int Cost => 4;
}

/// <summary>
/// 预览卡：商店卖后补货。
/// </summary>
public class HistoryBookShopRestockPreviewCard : HistoryBookRewritePreviewCard
{
    internal override HistoryBookRewriteOption RewriteOption => HistoryBookRewriteOption.ShopRestock;
    internal override int Cost => 6;
}
