using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Extensions;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Keywords;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

[Pool(typeof(ShengZhuSts2ModCardPool))]
// 圣主卡牌基类：统一处理卡牌所属牌池、图片路径和自定义关键字悬浮提示。
public abstract class ShengZhuSts2ModCard : ConstructedCardModel
{
    /// <summary>
    /// 面具牌在本场战斗内被【剥离面具】降低费用的次数。
    /// 这是局内运行时状态，不写入存档，避免下一场战斗继续保留减费。
    /// </summary>
    public int MaskPermanentCostReductionCount { get; set; }

    /// <summary>
    /// 构造函数：初始化卡牌基础属性，并注册自定义关键字悬浮提示。
    /// </summary>
    /// <param name="cost">费用。</param>
    /// <param name="type">卡牌类型。</param>
    /// <param name="rarity">稀有度。</param>
    /// <param name="target">目标类型。</param>
    protected ShengZhuSts2ModCard(int cost, CardType type, CardRarity rarity, TargetType target)
        : base(cost, type, rarity, target)
    {
        // 注册自定义关键字悬浮提示生成器。
        // 使用 WithTip 注册单个 TooltipSource，内部返回多个 tooltip 中的第一个匹配。
        // 使用 WithTips 注册多 tooltip 生成器。
        WithTips(static card => KeywordTooltipService.BuildTooltips(card));
    }

    /// <summary>
    /// 构造函数：初始化卡牌基础属性，并允许特殊伪卡不显示在卡牌图鉴里。
    /// </summary>
    /// <param name="cost">费用。</param>
    /// <param name="type">卡牌类型。</param>
    /// <param name="rarity">稀有度。</param>
    /// <param name="target">目标类型。</param>
    /// <param name="showInCardLibrary">是否显示在卡牌图鉴里。</param>
    protected ShengZhuSts2ModCard(int cost, CardType type, CardRarity rarity, TargetType target, bool showInCardLibrary)
        : base(cost, type, rarity, target, showInCardLibrary)
    {
        // 注册自定义关键字悬浮提示生成器。
        // 使用 WithTip 注册单个 TooltipSource，内部返回多个 tooltip 中的第一个匹配。
        // 使用 WithTips 注册多 tooltip 生成器。
        WithTips(static card => KeywordTooltipService.BuildTooltips(card));
    }

    //Image size:
    //Normal art: 1000x760 (Using 500x380 should also work, it will simply be scaled.)
    //Full art: 606x852
    public override string CustomPortraitPath => ResolveBigPortraitPath();
    
    //Smaller variants of card images for efficiency:
    //Smaller variant of fullart: 250x350
    //Smaller variant of normalart: 250x190
    
    //Uses card_portraits/card_name.png as image path. These should be smaller images.
    public override string PortraitPath => ResolvePortraitPath();
    public override string BetaPortraitPath => ResolvePortraitPath();

    /// <summary>
    /// 为当前卡牌添加"升级时同步升级预览牌"的提示。
    /// 兼容当前项目里已有的无参调用写法。
    /// </summary>
    /// <typeparam name="T">预览卡牌类型。</typeparam>
    protected void WithUpgradingCardTip<T>()
        where T : CardModel, new()
    {
        WithUpgradingCardTip<T>(static (_, _) => { });
    }

    /// <summary>
    /// 解析卡牌小图路径：优先使用标准蛇形命名，再兼容去下划线旧命名。
    /// </summary>
    /// <returns>可用的小图路径。</returns>
    private string ResolvePortraitPath()
    {
        return ResolvePortrait(false);
    }

    /// <summary>
    /// 解析卡牌大图路径：优先使用标准蛇形命名，再兼容去下划线旧命名。
    /// </summary>
    /// <returns>可用的大图路径。</returns>
    private string ResolveBigPortraitPath()
    {
        return ResolvePortrait(true);
    }

    /// <summary>
    /// 按"标准名 -> 去下划线名 -> 默认图"顺序解析卡牌图片。
    /// </summary>
    /// <param name="useBigImage">是否解析大图。</param>
    /// <returns>存在的资源路径；若都不存在，则返回默认图路径。</returns>
    private string ResolvePortrait(bool useBigImage)
    {
        string entry = Id.Entry.RemovePrefix().ToLowerInvariant();
        string compactEntry = entry.Replace("_", string.Empty);
        string fileName = $"{compactEntry}.png";

        return useBigImage
            ? fileName.BigCardImagePath($"{entry}.png")
            : fileName.CardImagePath($"{entry}.png");
    }
}
