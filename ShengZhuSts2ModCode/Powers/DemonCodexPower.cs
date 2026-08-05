using MegaCrit.Sts2.Core.Entities.Powers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 恶魔法典能力：作为开关让黑影兵团走正常力量、敏捷和攻防减益结算。
/// </summary>
public class DemonCodexPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 恶魔法典属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器显示叠加次数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;
}
