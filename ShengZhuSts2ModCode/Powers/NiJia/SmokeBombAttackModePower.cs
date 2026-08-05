using MegaCrit.Sts2.Core.Entities.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

/// <summary>
/// 烟雾弹攻击触发模式：隐藏标记能力，用来稳定记录升级版烟雾弹已生效。
/// </summary>
public class SmokeBombAttackModePower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 该标记不展示在战斗界面，只为联机同步和读档恢复保留状态。
    /// </summary>
    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 隐藏标记使用增益类型占位。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 该标记只表示开关，不需要叠层显示。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.None;
}
