using MegaCrit.Sts2.Core.Entities.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 影噬能力：黑影兵团卡不再受力量与敏捷影响，改为受影噬影响。
/// 每1层影噬，使黑影兵团卡造成的伤害或获得的格挡+1。
/// </summary>
public class DominionPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 影噬属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 影噬使用计数器堆叠方式。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;
}
