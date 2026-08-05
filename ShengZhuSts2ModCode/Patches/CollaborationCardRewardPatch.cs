using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 合作奖励补丁：已废弃，改为在 CollaborationRelic 中实现 TryModifyCardRewardOptions 方法。
/// 原 Postfix 方案存在问题：在 Hook 系统外追加卡牌，导致亮片等遗物无法附魔龙小组卡。
/// 新方案：让合作遗物参与游戏 Hook 流程，确保所有后续效果（亮片、蛋类等）能正常作用。
/// 保留此文件仅用于记录历史方案，Patch 已禁用。
/// </summary>
// [HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForReward), typeof(Player), typeof(int), typeof(CardCreationOptions))]
public static class CollaborationCardRewardPatch
{
    /// <summary>
    /// 每个战斗卡牌奖励额外追加的龙小组卡牌数量。
    /// </summary>
    private const int ExtraTeamJackieCards = 1;

    /// <summary>
    /// 【已废弃】工厂生成原本奖励后，按一代逻辑在末尾追加龙小组卡。
    /// 此方法已不再使用，功能已迁移到 CollaborationRelic.TryModifyCardRewardOptions。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="options">卡牌生成选项。</param>
    /// <param name="__result">原本生成结果。</param>
    public static void Postfix(Player player, CardCreationOptions options, ref IEnumerable<CardCreationResult> __result)
    {
        // 此方法已被禁用，不再执行任何逻辑
        // 功能已迁移到 CollaborationRelic.TryModifyCardRewardOptions
    }
}
