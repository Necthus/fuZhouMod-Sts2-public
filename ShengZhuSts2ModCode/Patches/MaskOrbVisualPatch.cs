using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.addons.mega_text;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Orbs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// Harmony Patch：拦截 OrbModel 的视觉相关方法，
/// 为 MaskOrb 和 EmptyMaskOrb 提供自定义的 Sprite 和图标，
/// 避免引擎去默认路径查找不存在的资源导致 NullReferenceException。
/// </summary>
public static class MaskOrbVisualPatch
{
    /// <summary>
    /// 面具图片的基础路径。
    /// </summary>
    private const string MaskImageBasePath = "res://ShengZhuSts2Mod/images/orbs/masks/";

    /// <summary>
    /// 空槽位的默认图片路径。
    /// </summary>
    private const string EmptySlotImagePath = "res://ShengZhuSts2Mod/images/orbs/masks/empty_slot.png";

    /// <summary>
    /// 默认面具图片（找不到对应面具图时的兜底）。
    /// </summary>
    private const string DefaultMaskImagePath = "res://ShengZhuSts2Mod/images/orbs/masks/BaTe.png";

    /// <summary>
    /// 面具充能球图标目标边长，缩小以避免多球重叠。数字越大渲染的面具充能球越大
    /// </summary>
    private const float MaskOrbTargetSize = 42.5f;

    /// <summary>
    /// 面具球静态贴图节点名称。
    /// </summary>
    private const string MaskOrbSpriteNodeName = "MaskOrbSprite";

    /// <summary>
    /// 面具能力键到图片路径的缓存，避免渲染与悬浮提示反复检查资源路径。
    /// </summary>
    private static readonly Dictionary<string, string> MaskImagePathCache = new();

    /// <summary>
    /// 面具能力键到本地化 entry 的缓存。
    /// </summary>
    private static readonly Dictionary<string, string> PowerLocEntryCache = new();

    /// <summary>
    /// 空槽位图片路径缓存。
    /// </summary>
    private static string? CachedEmptySlotImagePath;

    /// <summary>
    /// 判断 OrbModel 是否为面具系充能球。
    /// </summary>
    private static bool IsMaskOrbType(OrbModel orb)
    {
        return orb is MaskOrb or EmptyMaskOrb;
    }

    /// <summary>
    /// 生成调试日志用的面具球名称，方便排查到底是哪一种球在渲染。
    /// </summary>
    private static string ResolveOrbDebugName(OrbModel orb)
    {
        if (orb is MaskOrb maskOrb)
        {
            return string.IsNullOrEmpty(maskOrb.MaskPowerKey)
                ? "普通面具球(未命名)"
                : maskOrb.MaskPowerKey;
        }

        if (orb is EmptyMaskOrb)
        {
            return "空面具槽位球";
        }

        return orb.GetType().FullName ?? orb.GetType().Name;
    }

    /// <summary>
    /// 根据 MaskOrb 的 MaskPowerKey 解析对应的面具图片路径。
    /// 例如："ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks.BaTePower" → "BaTe.png"
    /// </summary>
    private static string ResolveMaskImagePath(MaskOrb maskOrb)
    {
        string key = maskOrb.MaskPowerKey;
        if (string.IsNullOrEmpty(key))
        {
            return DefaultMaskImagePath;
        }

        if (MaskImagePathCache.TryGetValue(key, out string? cachedPath))
        {
            return cachedPath;
        }

        // 从完整类名中提取短名：取最后一段，去掉 "Power" 后缀
        string shortName = key;
        int lastDot = key.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < key.Length - 1)
        {
            shortName = key[(lastDot + 1)..];
        }

        if (shortName.EndsWith("Power"))
        {
            shortName = shortName[..^5];
        }

        string path = MaskImageBasePath + shortName + ".png";

        // 检查资源是否存在，不存在则使用默认图
        if (ResourceLoader.Exists(path))
        {
            MaskImagePathCache[key] = path;
            return path;
        }

        MainFile.Logger.Info($"【面具充能球】找不到面具图片：{path}，使用默认图。");
        MaskImagePathCache[key] = DefaultMaskImagePath;
        return DefaultMaskImagePath;
    }

    /// <summary>
    /// 解析空槽位图片路径。
    /// </summary>
    /// <returns>空槽位图片路径；找不到时返回默认面具图片。</returns>
    private static string ResolveEmptySlotImagePath()
    {
        if (CachedEmptySlotImagePath != null)
        {
            return CachedEmptySlotImagePath;
        }

        CachedEmptySlotImagePath = ResourceLoader.Exists(EmptySlotImagePath)
            ? EmptySlotImagePath
            : DefaultMaskImagePath;
        return CachedEmptySlotImagePath;
    }

    /// <summary>
    /// Patch OrbModel.CreateSprite()：
    /// 对面具系充能球返回一个简单的 Sprite2D 节点（显示面具图片），
    /// 而不是去加载不存在的 Spine 动画场景。
    /// </summary>
    [HarmonyPatch(typeof(OrbModel), nameof(OrbModel.CreateSprite))]
    public static class CreateSpritePatch
    {
        /// <summary>
        /// 前置拦截：如果是面具系充能球，直接返回自定义 Sprite2D，跳过原方法。
        /// </summary>
        public static bool Prefix(OrbModel __instance, ref Node2D __result)
        {
            if (!IsMaskOrbType(__instance))
            {
                return true; // 非面具球，走原逻辑
            }

            // 创建一个简单的 Sprite2D 节点来显示面具图片
            string imagePath;
            if (__instance is MaskOrb maskOrb)
            {
                imagePath = ResolveMaskImagePath(maskOrb);
            }
            else
            {
                // EmptyMaskOrb：使用空槽位图片，如果不存在则用默认面具图
                imagePath = ResolveEmptySlotImagePath();
            }

            var sprite = new Sprite2D
            {
                Name = MaskOrbSpriteNodeName
            };
            var texture = ResourceLoader.Load<Texture2D>(imagePath);
            if (texture != null)
            {
                sprite.Texture = texture;
                // 缩放到合适大小（充能球槽位大约 64x64）
                float scaleX = MaskOrbTargetSize / texture.GetWidth();
                float scaleY = MaskOrbTargetSize / texture.GetHeight();
                float scale = Mathf.Min(scaleX, scaleY);
                sprite.Scale = new Vector2(scale, scale);
                MainFile.Logger.Info($"【面具充能球渲染】创建自定义充能球：类型={ResolveOrbDebugName(__instance)}，贴图={imagePath}，原始尺寸={texture.GetWidth()}x{texture.GetHeight()}，目标边长={MaskOrbTargetSize}，内层缩放={scale:0.####}");
            }

            // 对空槽位设置半透明
            if (__instance is EmptyMaskOrb)
            {
                sprite.Modulate = new Color(1f, 1f, 1f, 0.4f);
            }

            __result = sprite;
            return false; // 跳过原方法
        }
    }

    /// <summary>
    /// Patch NOrb.UpdateVisuals()：
    /// 面具球只展示一个层数数字，避免新版原生充能球把第二行数值标签露出来。
    /// </summary>
    [HarmonyPatch(typeof(NOrb), nameof(NOrb.UpdateVisuals))]
    public static class NOrbUpdateVisualsPatch
    {
        /// <summary>
        /// 面具球是静态贴图，直接接管刷新，避免原生刷新去绑定 SpineSprite。
        /// </summary>
        public static bool Prefix(NOrb __instance)
        {
            if (!TryHandleMaskOrbVisuals(__instance))
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 接管面具球刷新，避免原生 NOrb 绑定静态贴图为 Spine 动画。
    /// </summary>
    /// <param name="orbNode">原生充能球节点。</param>
    /// <returns>已处理面具球时返回 true，其他充能球返回 false。</returns>
    private static bool TryHandleMaskOrbVisuals(NOrb orbNode)
    {
        if (orbNode.Model is not MaskOrb and not EmptyMaskOrb)
        {
            return false;
        }

        Control? visualContainer = orbNode.GetNodeOrNull<Control>("%VisualContainer");
        if (visualContainer == null)
        {
            return true;
        }

        Sprite2D sprite = GetOrCreateMaskSprite(visualContainer);
        string imagePath = orbNode.Model is MaskOrb maskOrb
            ? ResolveMaskImagePath(maskOrb)
            : ResolveEmptySlotImagePath();
        ConfigureMaskSprite(sprite, imagePath, orbNode.Model);
        NormalizeMaskOrbNativeUi(orbNode);
        return true;
    }

    /// <summary>
    /// 获取或创建面具球静态贴图节点。
    /// </summary>
    /// <param name="visualContainer">原生充能球视觉容器。</param>
    /// <returns>面具球贴图节点。</returns>
    private static Sprite2D GetOrCreateMaskSprite(Control visualContainer)
    {
        Sprite2D? sprite = visualContainer.GetNodeOrNull<Sprite2D>(MaskOrbSpriteNodeName);
        if (sprite != null)
        {
            return sprite;
        }

        sprite = new Sprite2D
        {
            Name = MaskOrbSpriteNodeName
        };
        visualContainer.AddChild(sprite);
        return sprite;
    }

    /// <summary>
    /// 配置面具球静态贴图。
    /// </summary>
    /// <param name="sprite">面具球贴图节点。</param>
    /// <param name="imagePath">贴图资源路径。</param>
    /// <param name="orb">面具球模型。</param>
    private static void ConfigureMaskSprite(Sprite2D sprite, string imagePath, OrbModel orb)
    {
        var texture = ResourceLoader.Load<Texture2D>(imagePath);
        if (texture != null)
        {
            sprite.Texture = texture;
            float scaleX = MaskOrbTargetSize / texture.GetWidth();
            float scaleY = MaskOrbTargetSize / texture.GetHeight();
            float scale = Mathf.Min(scaleX, scaleY);
            sprite.Scale = new Vector2(scale, scale);
            MainFile.Logger.Info($"【面具充能球渲染】刷新静态面具球：类型={ResolveOrbDebugName(orb)}，贴图={imagePath}，原始尺寸={texture.GetWidth()}x{texture.GetHeight()}，目标边长={MaskOrbTargetSize}，内层缩放={scale:0.####}");
        }

        sprite.Position = Vector2.Zero;
        sprite.Visible = true;
        sprite.Modulate = orb is EmptyMaskOrb
            ? new Color(1f, 1f, 1f, 0.4f)
            : Colors.White;
    }

    /// <summary>
    /// 整理面具球原生UI，只保留面具层数和空槽轮廓各自该显示的部分。
    /// </summary>
    /// <param name="orbNode">原生充能球节点。</param>
    private static void NormalizeMaskOrbNativeUi(NOrb orbNode)
    {
        bool isFilledMaskOrb = orbNode.Model is MaskOrb;

        TextureRect? outline = orbNode.GetNodeOrNull<TextureRect>("%Outline");
        if (outline != null)
        {
            outline.Visible = !isFilledMaskOrb;
        }

        Control? labelContainer = orbNode.GetNodeOrNull<Control>("%LabelContainer");
        if (labelContainer != null)
        {
            labelContainer.Visible = isFilledMaskOrb;
        }

        MegaLabel? passiveAmount = orbNode.GetNodeOrNull<MegaLabel>("%PassiveAmount");
        if (passiveAmount != null)
        {
            passiveAmount.Visible = isFilledMaskOrb;
            if (orbNode.Model is MaskOrb maskOrb)
            {
                passiveAmount.SetTextAutoSize(maskOrb.MaskAmount.ToString("0"));
            }
        }

        Control? evokeAmount = orbNode.GetNodeOrNull<Control>("%EvokeAmount");
        if (evokeAmount != null)
        {
            evokeAmount.Visible = false;
        }
    }

    /// <summary>
    /// Patch OrbModel.Icon getter：
    /// 对面具系充能球返回对应的面具纹理，避免加载不存在的默认路径。
    /// </summary>
    [HarmonyPatch(typeof(OrbModel), nameof(OrbModel.Icon), MethodType.Getter)]
    public static class IconPatch
    {
        /// <summary>
        /// 前置拦截：如果是面具系充能球，返回自定义纹理。
        /// </summary>
        public static bool Prefix(OrbModel __instance, ref CompressedTexture2D __result)
        {
            if (!IsMaskOrbType(__instance))
            {
                return true; // 非面具球，走原逻辑
            }

            string imagePath;
            if (__instance is MaskOrb maskOrb)
            {
                imagePath = ResolveMaskImagePath(maskOrb);
            }
            else
            {
                imagePath = ResolveEmptySlotImagePath();
            }

            // 尝试加载为 CompressedTexture2D（Godot 导入后的 .png 会变成 CompressedTexture2D）
            var texture = ResourceLoader.Load<CompressedTexture2D>(imagePath);
            __result = texture;
            return false; // 跳过原方法
        }
    }

    /// <summary>
    /// Patch OrbModel.PlayChannelSfx()：
    /// 对面具系充能球跳过音效播放（1代也是空实现）。
    /// </summary>
    [HarmonyPatch(typeof(OrbModel), nameof(OrbModel.PlayChannelSfx))]
    public static class PlayChannelSfxPatch
    {
        /// <summary>
        /// 前置拦截：如果是面具系充能球，跳过音效播放。
        /// </summary>
        public static bool Prefix(OrbModel __instance)
        {
            if (!IsMaskOrbType(__instance))
            {
                return true; // 非面具球，走原逻辑
            }

            // 面具充能球不播放音效
            return false;
        }
    }

    /// <summary>
    /// Patch OrbModel.HoverTips getter：
    /// 对面具系充能球返回对应面具 Power 的具体名称和描述，
    /// 而不是通用的"面具"描述。
    /// </summary>
    [HarmonyPatch(typeof(OrbModel), nameof(OrbModel.HoverTips), MethodType.Getter)]
    public static class HoverTipsPatch
    {
        /// <summary>
        /// 前置拦截：如果是面具系充能球，返回对应面具Power的hover tip。
        /// </summary>
        public static bool Prefix(OrbModel __instance, ref IEnumerable<IHoverTip> __result)
        {
            if (__instance is not MaskOrb maskOrb)
            {
                // EmptyMaskOrb 和非面具球走原逻辑
                if (__instance is not EmptyMaskOrb)
                {
                    return true;
                }

                // EmptyMaskOrb 使用 orbs 表的通用描述
                var emptyTitle = new LocString("orbs", "EMPTY_MASK_ORB.title");
                var emptyDesc = new LocString("orbs", "EMPTY_MASK_ORB.description");
                __result = new List<IHoverTip> { new HoverTip(emptyTitle, emptyDesc) };
                return false;
            }

            // 从 MaskPowerKey 推导出 Power 的 localization entry
            string powerLocEntry = ResolvePowerLocEntry(maskOrb.MaskPowerKey);

            if (string.IsNullOrEmpty(powerLocEntry))
            {
                // 找不到对应的 Power entry，使用通用描述
                return true;
            }

            // 使用面具 Power 的 title 和 smartDescription（带层数信息）
            var title = new LocString("powers", powerLocEntry + ".title");
            var descKey = powerLocEntry + ".smartDescription";
            var desc = LocString.Exists("powers", descKey)
                ? new LocString("powers", descKey)
                : new LocString("powers", powerLocEntry + ".description");

            // 注入层数变量
            desc.Add("Amount", ResolveHoverAmount(maskOrb));

            // 加载面具图标作为 hover tip 图标
            string imagePath = ResolveMaskImagePath(maskOrb);
            var icon = ResourceLoader.Load<Texture2D>(imagePath);

            __result = new List<IHoverTip> { new HoverTip(title, desc, icon) };
            return false;
        }
    }

    /// <summary>
    /// 从 MaskPowerKey（完整类名）推导出 Power 的 localization entry。
    /// 例如："ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks.BaTePower" → "SHENGZHUSTS2MOD-BA_TE_POWER"
    /// </summary>
    private static string ResolvePowerLocEntry(string maskPowerKey)
    {
        if (string.IsNullOrEmpty(maskPowerKey))
        {
            return "";
        }

        if (PowerLocEntryCache.TryGetValue(maskPowerKey, out string? cachedEntry))
        {
            return cachedEntry;
        }

        // 提取类名（最后一段）
        string className = maskPowerKey;
        int lastDot = maskPowerKey.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < maskPowerKey.Length - 1)
        {
            className = maskPowerKey[(lastDot + 1)..];
        }

        // 将 PascalCase 转为 UPPER_SNAKE_CASE
        // BaTePower → BA_TE_POWER
        string slugified = System.Text.RegularExpressions.Regex.Replace(
            className, "([A-Za-z0-9])([A-Z])", "$1_$2").ToUpperInvariant();

        // 加上 mod 前缀
        string entry = $"SHENGZHUSTS2MOD-{slugified}";
        PowerLocEntryCache[maskPowerKey] = entry;
        return entry;
    }

    private static int ResolveHoverAmount(MaskOrb maskOrb)
    {
        if (maskOrb.MaskPowerKey != (typeof(TaLaPower).FullName ?? typeof(TaLaPower).Name))
        {
            return maskOrb.MaskAmount;
        }

        var taLaPower = maskOrb.Owner?.Creature?.Powers.OfType<TaLaPower>().FirstOrDefault();
        if (taLaPower == null || taLaPower.Amount <= 0)
        {
            return maskOrb.MaskAmount;
        }

        return taLaPower.Amount;
    }
}
