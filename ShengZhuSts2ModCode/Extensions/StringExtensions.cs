using Godot;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Extensions;

//Mostly utilities to get asset paths.
public static class StringExtensions
{
    public static string ImagePath(this string path)
    {
        return Path.Join(MainFile.ResPath, "images", path);
    }

    public static string CardImagePath(this string path)
    {
        path = Path.Join(MainFile.ResPath, "images", "card_portraits", path);
        if (ResourceLoader.Exists(path)) return path;
        
        MainFile.Logger.Info("Could not find card image path: " + path);
        return Path.Join(MainFile.ResPath, "images", "card_portraits", "card.png");
    }

    /// <summary>
    /// 解析卡牌小图路径：优先使用主文件名，找不到再尝试备用文件名。
    /// </summary>
    /// <param name="path">主文件名。</param>
    /// <param name="fallbackPath">备用文件名。</param>
    /// <returns>可用的卡牌小图路径。</returns>
    public static string CardImagePath(this string path, string fallbackPath)
    {
        string primaryPath = Path.Join(MainFile.ResPath, "images", "card_portraits", path);
        if (ResourceLoader.Exists(primaryPath)) return primaryPath;

        string secondaryPath = Path.Join(MainFile.ResPath, "images", "card_portraits", fallbackPath);
        if (ResourceLoader.Exists(secondaryPath)) return secondaryPath;

        MainFile.Logger.Info("Could not find card image path: " + primaryPath);
        return Path.Join(MainFile.ResPath, "images", "card_portraits", "card.png");
    }

    public static string BigCardImagePath(this string path)
    {
        path = Path.Join(MainFile.ResPath, "images", "card_portraits", "big", path);
        if (ResourceLoader.Exists(path)) return path;
        
        MainFile.Logger.Info("Could not find big card image path: " + path);
        return Path.Join(MainFile.ResPath, "images", "card_portraits", "big", "card.png");
    }

    /// <summary>
    /// 解析卡牌大图路径：优先使用主文件名，找不到再尝试备用文件名。
    /// </summary>
    /// <param name="path">主文件名。</param>
    /// <param name="fallbackPath">备用文件名。</param>
    /// <returns>可用的卡牌大图路径。</returns>
    public static string BigCardImagePath(this string path, string fallbackPath)
    {
        string primaryPath = Path.Join(MainFile.ResPath, "images", "card_portraits", "big", path);
        if (ResourceLoader.Exists(primaryPath)) return primaryPath;

        string secondaryPath = Path.Join(MainFile.ResPath, "images", "card_portraits", "big", fallbackPath);
        if (ResourceLoader.Exists(secondaryPath)) return secondaryPath;

        MainFile.Logger.Info("Could not find big card image path: " + primaryPath);
        return Path.Join(MainFile.ResPath, "images", "card_portraits", "big", "card.png");
    }

    public static string PowerImagePath(this string path)
    {
        path = Path.Join(MainFile.ResPath, "images", "powers", path);
        if (ResourceLoader.Exists(path)) return path;
        
        MainFile.Logger.Info("Could not find power image path: " + path);
        return Path.Join(MainFile.ResPath, "images", "powers", "power.png");
    }

    /// <summary>
    /// 解析能力小图路径：优先使用主文件名，找不到再尝试备用文件名。
    /// </summary>
    /// <param name="path">主文件名。</param>
    /// <param name="fallbackPath">备用文件名。</param>
    /// <returns>可用的能力小图路径。</returns>
    public static string PowerImagePath(this string path, string fallbackPath)
    {
        string primaryPath = Path.Join(MainFile.ResPath, "images", "powers", path);
        if (ResourceLoader.Exists(primaryPath)) return primaryPath;

        string secondaryPath = Path.Join(MainFile.ResPath, "images", "powers", fallbackPath);
        if (ResourceLoader.Exists(secondaryPath)) return secondaryPath;

        MainFile.Logger.Info("Could not find power image path: " + primaryPath);
        return Path.Join(MainFile.ResPath, "images", "powers", "power.png");
    }

    public static string BigPowerImagePath(this string path)
    {
        path = Path.Join(MainFile.ResPath, "images", "powers", "big", path);
        if (ResourceLoader.Exists(path)) return path;
        
        MainFile.Logger.Info("Could not find big power image path: " + path);
        return Path.Join(MainFile.ResPath, "images", "powers", "big", "power.png");
    }

    /// <summary>
    /// 解析能力大图路径：优先使用主文件名，找不到再尝试备用文件名。
    /// </summary>
    /// <param name="path">主文件名。</param>
    /// <param name="fallbackPath">备用文件名。</param>
    /// <returns>可用的能力大图路径。</returns>
    public static string BigPowerImagePath(this string path, string fallbackPath)
    {
        string primaryPath = Path.Join(MainFile.ResPath, "images", "powers", "big", path);
        if (ResourceLoader.Exists(primaryPath)) return primaryPath;

        string secondaryPath = Path.Join(MainFile.ResPath, "images", "powers", "big", fallbackPath);
        if (ResourceLoader.Exists(secondaryPath)) return secondaryPath;

        MainFile.Logger.Info("Could not find big power image path: " + primaryPath);
        return Path.Join(MainFile.ResPath, "images", "powers", "big", "power.png");
    }

    public static string RelicImagePath(this string path)
    {
        path = Path.Join(MainFile.ResPath, "images", "relics", path);
        if (ResourceLoader.Exists(path)) return path;
        
        MainFile.Logger.Info("Could not find relic image path: " + path);
        return Path.Join(MainFile.ResPath, "images", "relics", "relic.png");
    }

    public static string BigRelicImagePath(this string path)
    {
        path = Path.Join(MainFile.ResPath, "images", "relics", "big", path);
        if (ResourceLoader.Exists(path)) return path;
        
        MainFile.Logger.Info("Could not find big relic image path: " + path);
        return Path.Join(MainFile.ResPath, "images", "relics", "big", "relic.png");
    }

    public static string CharacterUiPath(this string path)
    {
        return Path.Join(MainFile.ResPath, "images", "charui", path);
    }
}
