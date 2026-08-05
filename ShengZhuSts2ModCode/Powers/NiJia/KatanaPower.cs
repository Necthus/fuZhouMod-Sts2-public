using MegaCrit.Sts2.Core.Entities.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

/// <summary>
/// 武士刀能力：让尼嘉-忍者团攻击所有敌人。
/// </summary>
public class KatanaPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 武士刀属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 武士刀只表示开关，不通过层数继续提高效果。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.None;
}
