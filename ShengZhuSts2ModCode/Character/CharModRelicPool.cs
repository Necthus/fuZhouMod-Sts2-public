using BaseLib.Abstracts;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Extensions;
using Godot;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;

/// <summary>
/// 圣主遗物池：标记为共享池，使符咒遗物可以在所有角色的游戏中出现。
/// IsShared=true 会让 BaseLib 将此池注入到所有角色的遗物奖励中。
/// </summary>
public class ShengZhuSts2ModRelicPool : CustomRelicPoolModel
{
    /// <summary>
    /// 标记为共享池，所有角色都能遇到此池中的遗物。
    /// </summary>
    public override bool IsShared => true;

    public override Color LabOutlineColor => ShengZhu.Color;

    public override string BigEnergyIconPath => "charui/big_energy.png".ImagePath();
    public override string TextEnergyIconPath => "charui/text_energy.png".ImagePath();
}
