using Godot;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

/// <summary>
/// 符咒失去表现辅助类：负责播放符咒被抢走时的图标飞离动画和音效。
/// </summary>
public static class TalismanLossVisualHelper
{
    /// <summary>
    /// 默认遗物图标尺寸，遗物栏节点不可用时作为兜底。
    /// </summary>
    private static readonly Vector2 DefaultIconSize = new(64f, 64f);

    /// <summary>
    /// 播放指定符咒被抢走的表现；表现失败时只记录日志，不阻塞真实遗物移除。
    /// </summary>
    /// <param name="talisman">即将失去的符咒遗物。</param>
    public static async Task PlayAsync(RelicModel talisman)
    {
        try
        {
            NGlobalUi? globalUi = NRun.Instance?.GlobalUi;
            Control? vfxContainer = globalUi?.AboveTopBarVfxContainer;
            if (globalUi?.RelicInventory == null || vfxContainer == null)
            {
                MainFile.Logger.Info("【抢夺符咒】未找到全局遗物栏或顶部特效层，跳过失去符咒动画。");
                return;
            }

            NRelicInventoryHolder? holder = FindRelicHolder(globalUi.RelicInventory, talisman);
            TextureRect icon = CreateFloatingIcon(talisman, holder);
            vfxContainer.AddChild(icon);
            icon.GlobalPosition = GetStartPosition(holder);

            PlayLossSound(TmpSfx.relicGet);
            await PlayLossTween(icon, vfxContainer);
            MainFile.Logger.Info($"【抢夺符咒】失去符咒动画播放完成：{talisman.Id.Entry}");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【抢夺符咒】失去符咒动画播放失败，将继续移除符咒：{talisman.Id.Entry}，异常={ex}");
        }
    }

    /// <summary>
    /// 在遗物栏中找到即将失去的符咒节点。
    /// </summary>
    /// <param name="inventory">当前全局遗物栏。</param>
    /// <param name="talisman">即将失去的符咒遗物。</param>
    /// <returns>匹配的遗物节点；找不到时返回 null。</returns>
    private static NRelicInventoryHolder? FindRelicHolder(NRelicInventory inventory, RelicModel talisman)
    {
        return inventory.RelicNodes.FirstOrDefault(node => ReferenceEquals(node.Relic.Model, talisman));
    }

    /// <summary>
    /// 复制遗物图标，生成用于动画的临时悬浮节点。
    /// </summary>
    /// <param name="talisman">即将失去的符咒遗物。</param>
    /// <param name="holder">遗物栏中对应的节点。</param>
    /// <returns>用于播放动画的临时图标。</returns>
    private static TextureRect CreateFloatingIcon(RelicModel talisman, NRelicInventoryHolder? holder)
    {
        Vector2 iconSize = GetIconSize(holder);
        TextureRect icon = new()
        {
            Texture = talisman.Icon,
            Size = iconSize,
            PivotOffset = iconSize * 0.5f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 1000
        };

        icon.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        return icon;
    }

    /// <summary>
    /// 获取动画起点，优先使用遗物栏中真实图标位置。
    /// </summary>
    /// <param name="holder">遗物栏中对应的节点。</param>
    /// <returns>全局坐标系下的动画起点。</returns>
    private static Vector2 GetStartPosition(NRelicInventoryHolder? holder)
    {
        TextureRect? sourceIcon = holder?.Relic.Icon;
        if (sourceIcon != null && GodotObject.IsInstanceValid(sourceIcon))
        {
            return sourceIcon.GlobalPosition;
        }

        return new Vector2(48f, 48f);
    }

    /// <summary>
    /// 获取遗物图标尺寸，节点不可用时使用默认尺寸。
    /// </summary>
    /// <param name="holder">遗物栏中对应的节点。</param>
    /// <returns>图标尺寸。</returns>
    private static Vector2 GetIconSize(NRelicInventoryHolder? holder)
    {
        TextureRect? sourceIcon = holder?.Relic.Icon;
        if (sourceIcon == null || !GodotObject.IsInstanceValid(sourceIcon) || sourceIcon.Size == Vector2.Zero)
        {
            return DefaultIconSize;
        }

        return sourceIcon.Size;
    }

    /// <summary>
    /// 播放三段式失去动画：遗物栏抖动提醒、稳定飞向中央、中央放大淡出。
    /// </summary>
    /// <param name="icon">临时悬浮图标。</param>
    /// <param name="vfxContainer">顶部特效层。</param>
    private static async Task PlayLossTween(TextureRect icon, Control vfxContainer)
    {
        try
        {
            Vector2 start = icon.GlobalPosition;
            Vector2 center = vfxContainer.GetViewportRect().Size * 0.5f - icon.Size * 0.5f;
            Tween tween = icon.CreateTween();

            tween.TweenProperty(icon, "scale", Vector2.One * 1.12f, 0.08f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(icon, "rotation_degrees", 5f, 0.08f);
            tween.TweenProperty(icon, "global_position", start + new Vector2(8f, 0f), 0.05f);
            tween.Parallel().TweenProperty(icon, "rotation_degrees", -5f, 0.05f);
            tween.TweenProperty(icon, "global_position", start + new Vector2(-8f, 0f), 0.05f);
            tween.Parallel().TweenProperty(icon, "rotation_degrees", 5f, 0.05f);
            tween.TweenCallback(Callable.From(() => PlayLossSound(TmpSfx.relicGet)));
            tween.TweenProperty(icon, "global_position", start + new Vector2(8f, 0f), 0.05f);
            tween.Parallel().TweenProperty(icon, "rotation_degrees", -5f, 0.05f);
            tween.TweenProperty(icon, "global_position", start + new Vector2(-8f, 0f), 0.05f);
            tween.Parallel().TweenProperty(icon, "rotation_degrees", 4f, 0.05f);
            tween.TweenProperty(icon, "global_position", start + new Vector2(5f, 0f), 0.05f);
            tween.Parallel().TweenProperty(icon, "rotation_degrees", -3f, 0.05f);
            tween.TweenProperty(icon, "global_position", start, 0.10f);
            tween.Parallel().TweenProperty(icon, "rotation_degrees", 0f, 0.10f);
            tween.Parallel().TweenProperty(icon, "scale", Vector2.One, 0.10f);
            tween.TweenCallback(Callable.From(() =>
            {
                icon.GlobalPosition = start;
                icon.RotationDegrees = 0f;
                icon.Scale = Vector2.One;
            }));
            tween.TweenProperty(icon, "global_position", center, 0.85f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.InOut);
            tween.TweenCallback(Callable.From(() => PlayLossSound(TmpSfx.cardExhaust)));
            tween.TweenProperty(icon, "modulate:a", 0f, 0.45f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(icon, "scale", Vector2.One * 3.20f, 0.45f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);

            await icon.ToSignal(tween, Tween.SignalName.Finished);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(icon))
            {
                icon.QueueFree();
            }
        }
    }

    /// <summary>
    /// 播放失去符咒音效；正式音效不可用时再尝试调试音效，失败只记日志并继续流程。
    /// </summary>
    /// <param name="streamName">本体临时音效文件名。</param>
    private static void PlayLossSound(string streamName)
    {
        try
        {
            SfxCmd.Play(streamName);
            NDebugAudioManager.Instance?.Play(streamName);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【抢夺符咒】播放失去符咒音效失败：音效={streamName}，异常={ex.Message}");
        }
    }
}
