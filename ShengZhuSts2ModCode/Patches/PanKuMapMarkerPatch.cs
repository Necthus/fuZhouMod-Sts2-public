using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 潘库宝盒地图标记补丁：把恶魔门节点的任务标记换成地狱之门，并在门上显示固定奖励卦象。
/// </summary>
public static class PanKuMapMarkerPatch
{
    /// <summary>
    /// 地图界面保存的运行状态字段，用于在新楼层地图加载后找到玩家遗物。
    /// </summary>
    private static readonly FieldInfo? RunStateField = typeof(NMapScreen).GetField("_runState", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 地狱之门地图标记图片路径。
    /// </summary>
    private const string HellGateImagePath = "res://ShengZhuSts2Mod/images/ui/map/panku_hell_gate.png";

    /// <summary>
    /// 卦象标签节点名，避免重复创建。
    /// </summary>
    private const string BaguaLabelName = "ShengZhuSts2ModPanKuBaguaLabel";

    /// <summary>
    /// 地图设置完成后刷新潘库宝盒标记，修复跨层后没有重新标记地狱之门的问题。
    /// </summary>
    [HarmonyPatch(typeof(NMapScreen), "SetMap")]
    public static class SetMapPatch
    {
        /// <summary>
        /// 后置补丁：地图点创建完成后，让玩家身上的潘库宝盒重新扫描当前地图。
        /// </summary>
        /// <param name="__instance">地图界面节点。</param>
        /// <param name="map">当前章节地图。</param>
        public static void Postfix(NMapScreen __instance, ActMap map)
        {
            RefreshPanKuBoxMarks(__instance, map);
        }
    }

    /// <summary>
    /// 普通地图节点准备完成后刷新潘库恶魔门视觉。
    /// </summary>
    [HarmonyPatch(typeof(NNormalMapPoint), "_Ready")]
    public static class ReadyPatch
    {
        /// <summary>
        /// 后置补丁：原版节点和任务标记初始化完成后，再套潘库视觉。
        /// </summary>
        public static void Postfix(NNormalMapPoint __instance)
        {
            RefreshPanKuMarker(__instance);
        }
    }

    /// <summary>
    /// 原版任务标记显隐变化后刷新潘库恶魔门视觉。
    /// </summary>
    [HarmonyPatch(typeof(NNormalMapPoint), "RefreshMarkedIconVisibility")]
    public static class RefreshMarkedIconVisibilityPatch
    {
        /// <summary>
        /// 后置补丁：保持原版显隐逻辑，再按潘库宝盒覆盖图标和卦象。
        /// </summary>
        public static void Postfix(NNormalMapPoint __instance)
        {
            RefreshPanKuMarker(__instance);
        }
    }

    /// <summary>
    /// 刷新所有玩家身上的潘库宝盒地图标记。
    /// </summary>
    /// <param name="mapScreen">地图界面节点。</param>
    /// <param name="map">当前章节地图。</param>
    private static void RefreshPanKuBoxMarks(NMapScreen mapScreen, ActMap map)
    {
        try
        {
            if (RunStateField?.GetValue(mapScreen) is not IRunState runState)
            {
                MainFile.Logger.Info("【潘库宝盒】地图刷新时未找到运行状态，跳过恶魔门补标。");
                return;
            }

            int refreshed = 0;
            foreach (var player in runState.Players)
            {
                PanKuBox? panKuBox = player.GetRelic<PanKuBox>();
                if (panKuBox == null)
                {
                    continue;
                }

                panKuBox.RefreshMapMarks(map);
                refreshed++;
            }

            if (refreshed > 0)
            {
                MainFile.Logger.Info($"【潘库宝盒】地图加载后刷新恶魔门标记：宝盒数量={refreshed}。");
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【潘库宝盒】地图刷新补标失败：{ex}。");
        }
    }

    /// <summary>
    /// 刷新单个地图节点的潘库恶魔门标记。
    /// </summary>
    /// <param name="mapPointNode">地图节点 UI。</param>
    private static void RefreshPanKuMarker(NNormalMapPoint mapPointNode)
    {
        TextureRect? questIcon = mapPointNode.GetNodeOrNull<TextureRect>("%QuestIcon");
        if (questIcon == null || mapPointNode.Point == null)
        {
            return;
        }

        var panKuBoxes = mapPointNode.Point.Quests.OfType<PanKuBox>().ToList();
        PanKuBox? panKuBox = panKuBoxes.FirstOrDefault(box => box.TryGetMarkedKind(mapPointNode.Point.coord, out _))
            ?? panKuBoxes.FirstOrDefault();
        if (panKuBox == null)
        {
            RemoveBaguaLabel(questIcon);
            return;
        }

        Texture2D? hellGateTexture = ResourceLoader.Load<Texture2D>(HellGateImagePath);
        if (hellGateTexture == null)
        {
            MainFile.Logger.Info($"【潘库宝盒】地狱之门地图标记图片加载失败：{HellGateImagePath}。");
        }
        else
        {
            questIcon.Texture = hellGateTexture;
        }

        questIcon.Visible = true;
        Label baguaLabel = GetOrCreateBaguaLabel(questIcon);
        if (panKuBox.TryGetMarkedKind(mapPointNode.Point.coord, out PanKuDemonQiKind kind))
        {
            baguaLabel.Text = PanKuDemonQiHelper.GetBaguaSymbol(kind);
            baguaLabel.Visible = true;
        }
        else
        {
            baguaLabel.Visible = false;
        }
    }

    /// <summary>
    /// 获取或创建卦象标签。
    /// </summary>
    /// <param name="questIcon">任务标记图标。</param>
    /// <returns>卦象标签。</returns>
    private static Label GetOrCreateBaguaLabel(TextureRect questIcon)
    {
        Label? label = questIcon.GetNodeOrNull<Label>(BaguaLabelName);
        if (label != null)
        {
            return label;
        }

        label = new Label
        {
            Name = BaguaLabelName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        label.OffsetLeft = -30f;
        label.OffsetTop = -30f;
        label.OffsetRight = 6f;
        label.OffsetBottom = 6f;
        label.AddThemeFontSizeOverride("font_size", 24);
        label.AddThemeColorOverride("font_color", new Color(1f, 0.84f, 0.35f, 1f));
        label.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.02f, 0f, 0.95f));
        label.AddThemeConstantOverride("outline_size", 6);
        questIcon.AddChild(label);
        return label;
    }

    /// <summary>
    /// 移除潘库卦象标签，避免非潘库任务标记残留。
    /// </summary>
    /// <param name="questIcon">任务标记图标。</param>
    private static void RemoveBaguaLabel(TextureRect questIcon)
    {
        Label? label = questIcon.GetNodeOrNull<Label>(BaguaLabelName);
        if (label == null)
        {
            return;
        }

        questIcon.RemoveChild(label);
        label.QueueFree();
    }
}
