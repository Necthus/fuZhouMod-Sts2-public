using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.PanKuPreview;

/// <summary>
/// 潘库宝盒预览伪卡基类：仅允许在潘库宝盒选择界面临时展示，不能通过药水、商店、战斗奖励、事件或变化等渠道获取。
/// </summary>
public abstract class PanKuPreviewCard : ShengZhuSts2ModCard
{
    /// <summary>
    /// 预览伪卡不允许战斗中随机生成，避免被药水等效果拿到。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>
    /// 预览伪卡不允许被修饰器或生成器随机选中。
    /// </summary>
    public override bool CanBeGeneratedByModifiers => false;

    /// <summary>
    /// 构造潘库预览伪卡，并从卡牌图鉴中隐藏。
    /// </summary>
    /// <param name="type">卡牌类型。</param>
    protected PanKuPreviewCard(CardType type)
        : base(0, type, CardRarity.Token, TargetType.None, false)
    {
    }
}
