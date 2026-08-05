using BaseLib.Abstracts;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Extensions;
using Godot;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;

// 圣主药水池：先占位，后续迁移药水时再扩展。
public class ShengZhuSts2ModPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => ShengZhu.Color;
    

    public override string BigEnergyIconPath => "charui/big_energy.png".ImagePath();
    public override string TextEnergyIconPath => "charui/text_energy.png".ImagePath();
}
