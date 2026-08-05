using BaseLib.Abstracts;
using BaseLib.Extensions;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Extensions;
using Godot;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

public abstract class ShengZhuSts2ModPower : CustomPowerModel
{
    //Loads from ShengZhuSts2Mod/images/powers/your_power.png
    public override string CustomPackedIconPath => ResolvePackedIconPath();
    public override string CustomBigIconPath => ResolveBigIconPath();

    /// <summary>
    /// 解析能力小图路径：优先使用标准蛇形命名，再兼容去下划线旧命名。
    /// </summary>
    /// <returns>可用的小图路径。</returns>
    private string ResolvePackedIconPath()
    {
        return ResolveIcon(false);
    }

    /// <summary>
    /// 解析能力大图路径：优先使用标准蛇形命名，再兼容去下划线旧命名。
    /// </summary>
    /// <returns>可用的大图路径。</returns>
    private string ResolveBigIconPath()
    {
        return ResolveIcon(true);
    }

    /// <summary>
    /// 按“标准名 -> 去下划线名 -> 默认图”顺序解析能力图片。
    /// </summary>
    /// <param name="useBigImage">是否解析大图。</param>
    /// <returns>存在的资源路径；若都不存在，则返回默认图路径。</returns>
    private string ResolveIcon(bool useBigImage)
    {
        string entry = Id.Entry.RemovePrefix().ToLowerInvariant();
        string compactEntry = entry.Replace("_", string.Empty);
        string fileName = $"{compactEntry}.png";

        return useBigImage
            ? fileName.BigPowerImagePath($"{entry}.png")
            : fileName.PowerImagePath($"{entry}.png");
    }
}
