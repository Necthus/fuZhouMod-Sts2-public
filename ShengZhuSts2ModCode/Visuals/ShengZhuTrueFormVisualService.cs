using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

/// <summary>
/// 圣主真身视觉服务：根据鼠符咒持有状态切换石板外观和真身外观。
/// </summary>
public static class ShengZhuTrueFormVisualService
{
    private const string StatueNodeName = "%ShengZhuStatueVisual";
    private const string TrueFormNodeName = "%ShengZhuTrueFormVisual";
    private const string IdleStartedMetaName = "shengzhu_true_form_idle_started";
    private static readonly FieldInfo? MerchantPlayersField = typeof(NMerchantRoom).GetField("_players", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 刷新指定玩家在战斗房间里的圣主外观。
    /// </summary>
    /// <param name="player">需要刷新的玩家。</param>
    public static void Refresh(Player? player)
    {
        if (player?.Character is not ShengZhu)
        {
            return;
        }

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        if (creatureNode == null)
        {
            return;
        }

        Refresh(creatureNode, ShengZhuFormService.IsHumanForm(player));
    }

    /// <summary>
    /// 刷新指定玩家在当前可见房间里的圣主外观。
    /// </summary>
    /// <param name="player">需要刷新的玩家。</param>
    public static void RefreshEverywhere(Player? player)
    {
        if (player?.Character is not ShengZhu)
        {
            return;
        }

        Refresh(player);
        RefreshRestSite(player);
        RefreshMerchant(player);
    }

    /// <summary>
    /// 刷新战斗房间里所有圣主玩家的真身显示状态。
    /// </summary>
    public static void RefreshAll()
    {
        if (NCombatRoom.Instance == null)
        {
            return;
        }

        foreach (NCreature creatureNode in NCombatRoom.Instance.CreatureNodes)
        {
            Player? player = creatureNode.Entity?.Player;
            if (player?.Character is ShengZhu)
            {
                Refresh(creatureNode, ShengZhuFormService.IsHumanForm(player));
            }
        }
    }

    /// <summary>
    /// 刷新单个战斗节点的真身显示状态。
    /// </summary>
    /// <param name="creatureNode">战斗内角色节点。</param>
    /// <param name="showTrueForm">是否显示真身。</param>
    public static void Refresh(NCreature? creatureNode, bool showTrueForm)
    {
        if (creatureNode?.Entity?.Player?.Character is not ShengZhu)
        {
            return;
        }

        RefreshVisualRoot(creatureNode.Visuals, showTrueForm);
    }

    /// <summary>
    /// 刷新休息处里的圣主外观。
    /// </summary>
    /// <param name="restSiteCharacter">休息处角色节点。</param>
    public static void Refresh(NRestSiteCharacter? restSiteCharacter)
    {
        Player? player = restSiteCharacter?.Player;
        if (player?.Character is not ShengZhu)
        {
            return;
        }

        RefreshVisualRoot(restSiteCharacter, ShengZhuFormService.IsHumanForm(player));
    }

    /// <summary>
    /// 刷新商店里的圣主外观。
    /// </summary>
    /// <param name="merchantCharacter">商店角色节点。</param>
    /// <param name="player">商店角色节点对应的玩家。</param>
    public static void Refresh(NMerchantCharacter? merchantCharacter, Player? player)
    {
        if (player?.Character is not ShengZhu)
        {
            return;
        }

        RefreshVisualRoot(merchantCharacter, ShengZhuFormService.IsHumanForm(player));
    }

    /// <summary>
    /// 刷新商店房间里的所有圣主玩家外观。
    /// </summary>
    /// <param name="merchantRoom">商店房间节点。</param>
    public static void Refresh(NMerchantRoom? merchantRoom)
    {
        if (merchantRoom == null)
        {
            return;
        }

        IReadOnlyList<Player>? players = GetMerchantPlayers(merchantRoom);
        if (players == null)
        {
            return;
        }

        for (int i = 0; i < players.Count && i < merchantRoom.PlayerVisuals.Count; i++)
        {
            Refresh(merchantRoom.PlayerVisuals[i], players[i]);
        }
    }

    /// <summary>
    /// 判断节点是否是圣主自定义石板/真身展示节点。
    /// </summary>
    /// <param name="node">待检查节点。</param>
    /// <returns>同时存在石板和真身节点时返回 true。</returns>
    public static bool HasShengZhuVisuals(Node? node)
    {
        return node?.GetNodeOrNull<Node2D>(StatueNodeName) != null
               && node.GetNodeOrNull<Node2D>(TrueFormNodeName) != null;
    }

    /// <summary>
    /// 刷新当前休息处里指定玩家的外观。
    /// </summary>
    /// <param name="player">需要刷新的玩家。</param>
    private static void RefreshRestSite(Player player)
    {
        NRestSiteRoom? restSiteRoom = NRestSiteRoom.Instance;
        if (restSiteRoom == null)
        {
            return;
        }

        foreach (NRestSiteCharacter restSiteCharacter in restSiteRoom.Characters)
        {
            if (ReferenceEquals(restSiteCharacter.Player, player))
            {
                Refresh(restSiteCharacter);
            }
        }
    }

    /// <summary>
    /// 刷新当前商店里指定玩家的外观。
    /// </summary>
    /// <param name="player">需要刷新的玩家。</param>
    private static void RefreshMerchant(Player player)
    {
        NMerchantRoom? merchantRoom = NMerchantRoom.Instance;
        if (merchantRoom == null)
        {
            return;
        }

        IReadOnlyList<Player>? players = GetMerchantPlayers(merchantRoom);
        if (players == null)
        {
            return;
        }

        for (int i = 0; i < players.Count && i < merchantRoom.PlayerVisuals.Count; i++)
        {
            if (ReferenceEquals(players[i], player))
            {
                Refresh(merchantRoom.PlayerVisuals[i], player);
            }
        }
    }

    /// <summary>
    /// 读取商店房间内部的玩家顺序，用来匹配商店角色节点。
    /// </summary>
    /// <param name="merchantRoom">商店房间节点。</param>
    /// <returns>商店房间里的玩家列表。</returns>
    private static IReadOnlyList<Player>? GetMerchantPlayers(NMerchantRoom merchantRoom)
    {
        IReadOnlyList<Player>? players = MerchantPlayersField?.GetValue(merchantRoom) as IReadOnlyList<Player>;
        if (players == null)
        {
            MainFile.Logger.Info("【圣主真身】未找到商店玩家列表，跳过商店外观刷新。");
        }

        return players;
    }

    /// <summary>
    /// 按鼠符咒状态切换石板和真身两个节点。
    /// </summary>
    /// <param name="visualRoot">包含石板和真身节点的根节点。</param>
    /// <param name="showTrueForm">是否显示真身。</param>
    private static void RefreshVisualRoot(Node? visualRoot, bool showTrueForm)
    {
        Node2D? statueVisual = visualRoot?.GetNodeOrNull<Node2D>(StatueNodeName);
        Node2D? trueFormVisual = visualRoot?.GetNodeOrNull<Node2D>(TrueFormNodeName);
        if (statueVisual == null || trueFormVisual == null)
        {
            MainFile.Logger.Info("【圣主真身】未找到石板或真身视觉节点，跳过外观切换。");
            return;
        }

        statueVisual.Visible = !showTrueForm;
        trueFormVisual.Visible = showTrueForm;

        if (showTrueForm)
        {
            StartIdleAnimation(trueFormVisual);
        }
    }

    /// <summary>
    /// 启动最小版待机动画：腿脚保持站定，上半身呼吸，头部、手臂和尾巴小幅摆动。
    /// </summary>
    /// <param name="trueFormVisual">真身根节点。</param>
    private static void StartIdleAnimation(Node2D trueFormVisual)
    {
        if (trueFormVisual.HasMeta(IdleStartedMetaName))
        {
            return;
        }

        trueFormVisual.SetMeta(IdleStartedMetaName, true);
        StartUpperBodyBreathing(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_Torso"), 24f, 1.45f);
        StartUpperBodyBreathing(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_ChestCrystal"), 24f, 1.35f);
        StartUpperBodyBreathing(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_HeadNeck"), 24f, 1.35f);
        StartUpperBodyBreathing(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_LeftUpperArm"), 20f, 1.55f);
        StartUpperBodyBreathing(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_LeftForearmHand"), 16f, 1.65f);
        StartUpperBodyBreathing(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_RightUpperArm"), 20f, 1.50f);
        StartUpperBodyBreathing(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_RightForearmHand"), 16f, 1.60f);
        StartRotationLoop(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_Torso"), -1.0f, 2.0f);
        StartRotationLoop(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_HeadNeck"), 1.1f, 1.9f);
        StartRotationLoop(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_LeftUpperArm"), -3.0f, 1.9f);
        StartRotationLoop(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_LeftForearmHand"), -2.2f, 1.8f);
        StartRotationLoop(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_RightUpperArm"), 3.0f, 1.85f);
        StartRotationLoop(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_RightForearmHand"), 2.2f, 1.75f);
        StartRotationLoop(trueFormVisual.GetNodeOrNull<Node2D>("%Bone_Tail"), 2.5f, 2.0f);
    }

    /// <summary>
    /// 创建上半身呼吸循环，只移动腰部以上骨骼，避免脚部跟着上下悬浮。
    /// </summary>
    /// <param name="node">需要参与呼吸的上半身骨骼节点。</param>
    /// <param name="offsetY">局部坐标里的纵向移动距离。</param>
    /// <param name="seconds">单段动画时间。</param>
    private static void StartUpperBodyBreathing(Node2D? node, float offsetY, double seconds)
    {
        if (node == null)
        {
            return;
        }

        StartPositionLoop(node, node.Position, offsetY, seconds);
    }

    /// <summary>
    /// 创建局部位置循环。
    /// </summary>
    /// <param name="node">要移动的节点。</param>
    /// <param name="basePosition">节点原始位置。</param>
    /// <param name="offsetY">纵向移动距离。</param>
    /// <param name="seconds">单段动画时间。</param>
    private static void StartPositionLoop(Node2D node, Vector2 basePosition, float offsetY, double seconds)
    {
        Tween tween = node.CreateTween().SetLoops();
        tween.TweenProperty(node, "position", basePosition + new Vector2(0f, -offsetY), seconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(node, "position", basePosition, seconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    /// <summary>
    /// 创建小幅旋转循环。
    /// </summary>
    /// <param name="node">要旋转的骨骼节点。</param>
    /// <param name="degrees">旋转角度。</param>
    /// <param name="seconds">单段动画时间。</param>
    private static void StartRotationLoop(Node2D? node, float degrees, double seconds)
    {
        if (node == null)
        {
            return;
        }

        float baseRotation = node.RotationDegrees;
        Tween tween = node.CreateTween().SetLoops();
        tween.TweenProperty(node, "rotation_degrees", baseRotation + degrees, seconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(node, "rotation_degrees", baseRotation - degrees, seconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(node, "rotation_degrees", baseRotation, seconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }
}
