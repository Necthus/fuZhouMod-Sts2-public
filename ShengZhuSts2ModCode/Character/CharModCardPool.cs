using BaseLib.Abstracts;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Extensions;
using Godot;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;

// 圣主卡牌池：控制圣主卡牌背板、能量图标和卡牌池归属。
public class ShengZhuSts2ModCardPool : CustomCardPoolModel
{
    public override string Title => ShengZhu.CharacterId; //This is not a display name.
    
    public override string BigEnergyIconPath => "charui/big_energy.png".ImagePath();
    public override string TextEnergyIconPath => "charui/text_energy.png".ImagePath();


    // BaseLib 会从 ShaderColor 读取 HSV，给默认卡框重新着色。
    public override Color ShaderColor => ShengZhu.Color;
    
    //Alternatively, leave these values at 1 and provide a custom frame image.
    /*public override Texture2D CustomFrame(CustomCardModel card)
    {
        //This will attempt to load ShengZhuSts2Mod/images/cards/frame.png
        return PreloadManager.Cache.GetTexture2D("cards/frame.png".ImagePath());
    }*/

    // 小卡图标颜色：联机队友血条等紧凑 UI 会读取这个颜色。
    public override Color DeckEntryCardColor => ShengZhu.Color;
    
    public override bool IsColorless => false;
}
