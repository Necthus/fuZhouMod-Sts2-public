using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Orbs;

/// <summary>
/// 空面具槽位充能球：对应1代的 EmptyMaskOrbSlot。
/// 用于填充未使用的面具栏位，纯视觉占位。
/// </summary>
public class EmptyMaskOrb : OrbModel
{
    /// <summary>
    /// 空槽位没有被动值。
    /// </summary>
    public override decimal PassiveVal => 0;

    /// <summary>
    /// 空槽位没有唤出值。
    /// </summary>
    public override decimal EvokeVal => 0;

    /// <summary>
    /// 空槽位的暗色调（灰色）。
    /// </summary>
    public override Color DarkenedColor => new(0.5f, 0.5f, 0.5f, 0.5f);

    /// <summary>
    /// 空槽位不触发回合结束效果。
    /// </summary>
    public override Task BeforeTurnEndOrbTrigger(PlayerChoiceContext choiceContext)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 空槽位不触发回合开始效果。
    /// </summary>
    public override Task AfterTurnStartOrbTrigger(PlayerChoiceContext choiceContext)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 空槽位不触发被动效果。
    /// </summary>
    public override Task Passive(PlayerChoiceContext choiceContext, Creature? target)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 空槽位不触发唤出效果。
    /// </summary>
    public override Task<IEnumerable<Creature>> Evoke(PlayerChoiceContext playerChoiceContext)
    {
        return Task.FromResult(Enumerable.Empty<Creature>());
    }
}
