using MegaCrit.Sts2.Core.Entities.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 甘能力：记录本场战斗中甘的暴击率成长层数，每层代表后续甘暴击率提升 10%。
/// </summary>
public class GanPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 甘能力属于增益。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 甘能力按计数器方式堆叠。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;
}
