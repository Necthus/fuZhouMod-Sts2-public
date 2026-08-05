using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Talismans;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 飞行能力：来自敌人的攻击伤害减50%。受到未被格挡的生命值伤害后，失去1层飞行。
/// 当层数归零时移除自身。使用游戏内置飞行图标。
/// </summary>
public class FlightAfuPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 使用游戏内置的飞行图标（FlutterPower）。
    /// </summary>
    public override string CustomPackedIconPath => "res://images/atlases/power_atlas.sprites/flutter_power.tres";

    /// <summary>
    /// 大图也使用游戏内置的飞行图标。
    /// </summary>
    public override string CustomBigIconPath => "res://images/atlases/power_atlas.sprites/flutter_power.tres";
    /// <summary>
    /// 飞行属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 飞行使用计数器堆叠方式显示剩余层数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 修改受到的伤害乘数：返回0.5使伤害减半。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // 只对自己受到的攻击伤害生效，返回0.5使伤害减半
        if (Owner != null && target == Owner && IsPrimaryFlightReductionSource())
        {
            return 0.5m;
        }

        // 不修改：返回乘数1
        return 1m;
    }

    /// <summary>
    /// 生命值减少后，失去1层飞行。层数归零时移除自身。
    /// </summary>
    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (Owner == null || creature != Owner || delta >= 0)
        {
            return;
        }

        await ReduceFlightAfterHpLoss();
    }

    /// <summary>
    /// 执行飞行扣层，统一处理伤害和直接失去生命两类入口。
    /// </summary>
    private async Task ReduceFlightAfterHpLoss()
    {
        Flash();

        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            await PowerCmd.ModifyAmount(this, -1, null, null, false);
        }
    }

    /// <summary>
    /// 只让第一个飞行减伤能力生效，避免多层飞行把减伤叠加。
    /// </summary>
    private bool IsPrimaryFlightReductionSource()
    {
        if (Owner == null)
        {
            return false;
        }

        foreach (var power in Owner.Powers)
        {
            if (power is RoosterTalismanCardPower or FlightAfuPower)
            {
                return ReferenceEquals(power, this);
            }
        }

        return false;
    }
}
