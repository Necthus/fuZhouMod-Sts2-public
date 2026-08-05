using MegaCrit.Sts2.Core.Entities.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

/// <summary>
/// 双棍能力：让尼嘉-忍者团获得三倍影噬伤害加成。
/// </summary>
public class DoubleSticksPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 双棍属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 双棍只表示开关，不通过层数继续提高倍率。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>
    /// 影噬伤害加成倍率。
    /// </summary>
    public int DominionDamageMultiplier => 3;
}
