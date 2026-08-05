using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Orbs;

/// <summary>
/// 面具充能球：对应1代的 GenericMaskOrb。
/// 纯视觉展示用，不触发被动/唤出效果。
/// 在玩家角色上方悬浮显示当前装载的面具，显示面具图片和层数。
/// </summary>
public class MaskOrb : OrbModel
{
    /// <summary>
    /// 关联的面具Power类型标识。
    /// </summary>
    public string MaskPowerKey { get; set; } = "";

    /// <summary>
    /// 面具名称（用于Hover提示显示）。
    /// </summary>
    public string MaskName { get; set; } = "";

    /// <summary>
    /// 面具层数（用于显示）。
    /// </summary>
    public int MaskAmount { get; set; }

    /// <summary>
    /// 面具充能球的被动值 = 面具层数（用于UI显示数字）。
    /// </summary>
    public override decimal PassiveVal => MaskAmount;

    /// <summary>
    /// 面具充能球没有唤出效果，返回0。
    /// </summary>
    public override decimal EvokeVal => 0;

    /// <summary>
    /// 面具充能球的暗色调（绿色系，与1代一致）。
    /// </summary>
    public override Color DarkenedColor => new(0.2f, 1.0f, 0.2f, 1.0f);

    /// <summary>
    /// 面具充能球不触发回合结束被动效果。
    /// </summary>
    public override Task BeforeTurnEndOrbTrigger(PlayerChoiceContext choiceContext)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 面具充能球不触发回合开始效果。
    /// </summary>
    public override Task AfterTurnStartOrbTrigger(PlayerChoiceContext choiceContext)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 面具充能球不触发被动效果。
    /// </summary>
    public override Task Passive(PlayerChoiceContext choiceContext, Creature? target)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 面具充能球被唤出时不做任何事（面具挤出逻辑由MaskManager处理）。
    /// </summary>
    public override Task<IEnumerable<Creature>> Evoke(PlayerChoiceContext playerChoiceContext)
    {
        return Task.FromResult(Enumerable.Empty<Creature>());
    }
}
