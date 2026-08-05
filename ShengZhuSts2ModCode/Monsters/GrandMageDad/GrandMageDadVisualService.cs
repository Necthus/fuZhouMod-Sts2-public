using Godot;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>
/// 大法师老爹视觉服务：负责创建待机序列帧视觉，并在资源缺失时提供安全兜底。
/// </summary>
internal static class GrandMageDadVisualService
{
    private static readonly bool UseIdleAnimationByDefault = false;
    private const int IdleFrameCount = 40;
    private const int IdleFrameStep = 2;
    private const double IdleFrameIntervalSeconds = 0.066d;
    private const string IdleAnimationName = "idle";
    private const string StaticImagePath = $"{MainFile.ResPath}/images/monsters/grand_mage_dad/idle/frame_00001.png";
    private const string IdleFramePathPrefix = $"{MainFile.ResPath}/images/monsters/grand_mage_dad/idle/frame_";
    private static readonly Vector2 FallbackVisualSize = new(420f, 420f);
    private const float BoundsPadding = 24f;
    private const float IntentTopPadding = 70f;

    /// <summary>
    /// 创建大法师老爹战斗视觉节点。
    /// </summary>
    /// <returns>可被塔2战斗房间使用的怪物视觉节点。</returns>
    public static NCreatureVisuals CreateVisuals()
    {
        Node2D body = CreateBody(out Vector2 visualSize);
        NCreatureVisuals visuals = CreateBaseVisuals(visualSize);
        visuals.AddUnique(body);
        return visuals;
    }

    /// <summary>
    /// 创建包含必需节点的视觉根节点，避免原生战斗 UI 找不到坐标点。
    /// </summary>
    /// <param name="visualSize">老爹实际贴图尺寸。</param>
    /// <returns>带有边界和位置点的视觉根节点。</returns>
    private static NCreatureVisuals CreateBaseVisuals(Vector2 visualSize)
    {
        float width = Math.Max(visualSize.X, FallbackVisualSize.X);
        float height = Math.Max(visualSize.Y, FallbackVisualSize.Y);
        NCreatureVisuals visuals = new()
        {
            Name = "GrandMageDadVisuals"
        };

        Control bounds = new()
        {
            Position = new Vector2((-width * 0.5f) - BoundsPadding, -height - BoundsPadding),
            Size = new Vector2(width + BoundsPadding * 2f, height + BoundsPadding * 2f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        visuals.AddUnique(bounds, "Bounds");

        AddMarker(visuals, "CenterPos", new Vector2(0f, -height * 0.52f));
        AddMarker(visuals, "IntentPos", new Vector2(0f, -height - IntentTopPadding));
        AddMarker(visuals, "OrbPos", new Vector2(0f, -height * 0.58f));
        AddMarker(visuals, "TalkPos", new Vector2(0f, -height - 30f));

        return visuals;
    }

    /// <summary>
    /// 添加塔2视觉系统需要按唯一名查找的位置点。
    /// </summary>
    /// <param name="parent">视觉根节点。</param>
    /// <param name="name">位置点名称。</param>
    /// <param name="position">位置点坐标。</param>
    private static void AddMarker(Node parent, string name, Vector2 position)
    {
        Marker2D marker = new()
        {
            Position = position
        };
        parent.AddUnique(marker, name);
    }

    /// <summary>
    /// 创建老爹身体节点，默认使用静态贴图，避免大图序列帧常驻播放导致战斗掉帧。
    /// </summary>
    /// <param name="visualSize">实际用于计算边界和意图位置的视觉尺寸。</param>
    /// <returns>老爹身体视觉节点。</returns>
    private static Node2D CreateBody(out Vector2 visualSize)
    {
        visualSize = FallbackVisualSize;
        Node2D body = new()
        {
            Name = "Visuals"
        };

        if (UseIdleAnimationByDefault)
        {
            AnimatedSprite2D? animatedSprite = CreateIdleSprite(out Vector2 idleSize);
            if (animatedSprite != null)
            {
                visualSize = idleSize;
                body.AddChild(animatedSprite);
                return body;
            }
        }

        Sprite2D? staticSprite = CreateStaticSprite(out Vector2 staticSize);
        if (staticSprite != null)
        {
            visualSize = staticSize;
            body.AddChild(staticSprite);
            return body;
        }

        AnimatedSprite2D? fallbackAnimatedSprite = CreateIdleSprite(out Vector2 fallbackIdleSize);
        if (fallbackAnimatedSprite != null)
        {
            visualSize = fallbackIdleSize;
            body.AddChild(fallbackAnimatedSprite);
            return body;
        }

        MainFile.Logger.Info("【大法师老爹】未能加载待机序列帧和静态图，使用空白视觉节点兜底，战斗逻辑继续。");
        return body;
    }

    /// <summary>
    /// 创建待机序列帧节点。
    /// </summary>
    /// <param name="visualSize">待机帧尺寸。</param>
    /// <returns>待机动画节点；没有可用帧时返回 null。</returns>
    private static AnimatedSprite2D? CreateIdleSprite(out Vector2 visualSize)
    {
        visualSize = FallbackVisualSize;
        SpriteFrames frames = new();
        frames.AddAnimation(IdleAnimationName);
        frames.SetAnimationLoop(IdleAnimationName, true);
        frames.SetAnimationSpeed(IdleAnimationName, 1d / (IdleFrameIntervalSeconds * IdleFrameStep));

        int loaded = 0;
        int missing = 0;
        for (int i = 1; i <= IdleFrameCount; i += IdleFrameStep)
        {
            string framePath = $"{IdleFramePathPrefix}{i:00000}.png";
            Texture2D? frame = LoadTexture(framePath);
            if (frame == null)
            {
                missing++;
                continue;
            }

            frames.AddFrame(IdleAnimationName, frame);
            if (loaded == 0)
            {
                visualSize = frame.GetSize();
            }
            loaded++;
        }

        if (loaded <= 0)
        {
            MainFile.Logger.Info("【大法师老爹】未加载到任何待机帧，准备使用静态图兜底。");
            return null;
        }

        int expectedLoaded = (int)Math.Ceiling(IdleFrameCount / (double)IdleFrameStep);
        if (loaded < expectedLoaded)
        {
            MainFile.Logger.Info($"【大法师老爹】待机帧加载不完整：已加载 {loaded}/{expectedLoaded}，缺失={missing}，将使用已加载帧继续播放。");
        }
        else
        {
            MainFile.Logger.Info($"【大法师老爹】待机序列帧加载完成：{loaded}/{expectedLoaded}。");
        }

        AnimatedSprite2D sprite = new()
        {
            Name = "GrandMageDadIdleSprite",
            SpriteFrames = frames,
            Animation = IdleAnimationName,
            Position = GetFootAlignedPosition(visualSize),
            Centered = false
        };
        sprite.Play(IdleAnimationName);
        return sprite;
    }

    /// <summary>
    /// 创建静态兜底贴图节点。
    /// </summary>
    /// <param name="visualSize">静态图尺寸。</param>
    /// <returns>静态贴图节点；贴图缺失时返回 null。</returns>
    private static Sprite2D? CreateStaticSprite(out Vector2 visualSize)
    {
        visualSize = FallbackVisualSize;
        Texture2D? texture = LoadTexture(StaticImagePath);
        if (texture == null)
        {
            MainFile.Logger.Info($"【大法师老爹】静态兜底图加载失败：{StaticImagePath}");
            return null;
        }

        MainFile.Logger.Info("【大法师老爹】使用序列帧首帧作为静态视觉兜底。");
        visualSize = texture.GetSize();
        return new Sprite2D
        {
            Name = "GrandMageDadStaticSprite",
            Texture = texture,
            Position = GetFootAlignedPosition(visualSize),
            Centered = false
        };
    }

    /// <summary>
    /// 计算脚底中心对齐的位置，让图片底部落在怪物槽位原点上。
    /// </summary>
    /// <param name="visualSize">贴图尺寸。</param>
    /// <returns>贴图左上角位置。</returns>
    private static Vector2 GetFootAlignedPosition(Vector2 visualSize)
    {
        return new Vector2(-visualSize.X * 0.5f, -visualSize.Y);
    }

    /// <summary>
    /// 安全加载贴图资源，加载失败时返回 null。
    /// </summary>
    /// <param name="path">贴图资源路径。</param>
    /// <returns>贴图资源；不存在或加载失败时返回 null。</returns>
    private static Texture2D? LoadTexture(string path)
    {
        try
        {
            if (!ResourceLoader.Exists(path))
            {
                return null;
            }

            return ResourceLoader.Load<Texture2D>(path);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【大法师老爹】加载贴图异常：路径={path}，异常={ex.Message}");
            return null;
        }
    }
}
