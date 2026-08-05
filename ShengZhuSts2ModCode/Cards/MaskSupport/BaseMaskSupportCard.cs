using MegaCrit.Sts2.Core.Entities.Cards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.MaskSupport;

/// <summary>
/// 面具辅助牌基类：用于标识面具体系的辅助卡牌（如剥离面具、黑影女王等）。
/// 通过继承关系替代1代的 MASK_SUPPORT tag。
/// </summary>
public abstract class BaseMaskSupportCard(int cost, CardType type, CardRarity rarity, TargetType target) :
    ShengZhuSts2ModCard(cost, type, rarity, target)
{
}
