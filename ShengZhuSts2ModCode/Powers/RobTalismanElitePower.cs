using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 抢夺符咒精英强化：用于标记抢夺符咒事件【开战吧】中被强化的精英怪，并展示生命强化公式。
/// </summary>
public class RobTalismanElitePower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 抢夺符咒强化属于敌人的增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 使用计数器显示本次最终增加的生命值。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 悬浮说明中展示进阶数、人数、分配前总生命和当前怪获得力量，方便核对公式。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Ascension", 0),
        new DynamicVar("PlayerCount", 1),
        new DynamicVar("TotalHp", 60),
        new DynamicVar("Strength", 2)
    ];

    /// <summary>
    /// 设置本次强化公式里使用的实际参数。
    /// </summary>
    /// <param name="ascensionLevel">当前进阶数。</param>
    /// <param name="playerCount">当前玩家人数。</param>
    /// <param name="strengthBonus">当前怪物实际获得的力量。</param>
    /// <param name="totalHpBonus">分配前的生命强化总量。</param>
    public void SetFormulaDetails(int ascensionLevel, int playerCount, int strengthBonus, int totalHpBonus)
    {
        DynamicVars["Ascension"].BaseValue = ascensionLevel;
        DynamicVars["PlayerCount"].BaseValue = playerCount;
        DynamicVars["TotalHp"].BaseValue = totalHpBonus;
        DynamicVars["Strength"].BaseValue = strengthBonus;
    }
}
