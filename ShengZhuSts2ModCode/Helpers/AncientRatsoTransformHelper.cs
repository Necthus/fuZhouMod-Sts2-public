using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 先古拉苏变化辅助类：负责让古老牙齿把黑手帮·拉苏变化为先古卡拉苏。
/// </summary>
public static class AncientRatsoTransformHelper
{
    /// <summary>
    /// 判断玩家是否可由古老牙齿触发拉苏变化。
    /// </summary>
    /// <param name="player">待检查玩家。</param>
    /// <returns>可触发时返回 true。</returns>
    public static bool CanTransformRatso(Player? player)
    {
        return player?.Character is ShengZhu && HasTargetRatso(player);
    }

    /// <summary>
    /// 为古老牙齿设置圣主专属预览。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="setupPreview">古老牙齿本体提供的预览设置方法。</param>
    /// <returns>设置成功时返回 true。</returns>
    public static bool TrySetupPreview(Player? player, Action<CardModel, CardModel> setupPreview)
    {
        if (player?.Character is not ShengZhu)
        {
            return false;
        }

        CardModel? target = SelectTargetRatso(player);
        if (target == null)
        {
            return false;
        }

        CardModel replacement = CreateAncientRatso(player, target);
        setupPreview(target, replacement);
        MainFile.Logger.Info($"【古老牙齿·拉苏】设置预览：{target.Id.Entry} -> {replacement.Id.Entry}，升级={target.IsUpgraded}，附魔={target.Enchantment?.Id.Entry ?? "无"}。");
        return true;
    }

    /// <summary>
    /// 执行黑手帮·拉苏到先古拉苏的变化。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>异步任务。</returns>
    public static async Task TransformRatso(Player? player)
    {
        if (player?.Character is not ShengZhu)
        {
            return;
        }

        CardModel? target = SelectTargetRatso(player);
        if (target == null)
        {
            MainFile.Logger.Info("【古老牙齿·拉苏】未找到可变化的黑手帮·拉苏。");
            return;
        }

        CardModel replacement = CreateAncientRatso(player, target);
        string oldTitle = target.Title;
        string newTitle = replacement.Title;
        await CardCmd.Transform(target, replacement);
        MainFile.Logger.Info($"【古老牙齿·拉苏】变化完成：{oldTitle} -> {newTitle}，升级={replacement.IsUpgraded}，附魔={replacement.Enchantment?.Id.Entry ?? "无"}。");
    }

    /// <summary>
    /// 按升级优先、同级无附魔优先的规则选择要变化的黑手帮·拉苏。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>选中的黑手帮·拉苏；没有时返回 null。</returns>
    private static CardModel? SelectTargetRatso(Player player)
    {
        List<CardModel> candidates = player.Deck.Cards
            .Where(card => card.GetType() == typeof(BlackHandRatso))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        int bestUpgradeScore = candidates.Min(card => card.IsUpgraded ? 0 : 1);
        List<CardModel> upgradeGroup = candidates
            .Where(card => (card.IsUpgraded ? 0 : 1) == bestUpgradeScore)
            .ToList();

        int bestEnchantScore = upgradeGroup.Min(card => card.Enchantment == null ? 0 : 1);
        List<CardModel> finalGroup = upgradeGroup
            .Where(card => (card.Enchantment == null ? 0 : 1) == bestEnchantScore)
            .ToList();

        int selectedIndex = SelectStableTargetIndex(player, finalGroup);
        return finalGroup[selectedIndex];
    }

    /// <summary>
    /// 判断玩家牌组中是否存在可变化的黑手帮·拉苏；只做检查，不推进局内随机源。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>存在可变化目标时返回 true。</returns>
    private static bool HasTargetRatso(Player player)
    {
        return player.Deck.Cards.Any(card => card.GetType() == typeof(BlackHandRatso));
    }

    /// <summary>
    /// 为同优先级候选选择稳定下标；用于预览和实际变化，避免联机中 UI 检查消耗局内随机源。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="candidates">同优先级候选卡。</param>
    /// <returns>稳定候选下标。</returns>
    private static int SelectStableTargetIndex(Player player, IReadOnlyList<CardModel> candidates)
    {
        if (candidates.Count <= 1)
        {
            return 0;
        }

        List<CardModel> deckCards = player.Deck.Cards.ToList();
        string candidateKeys = string.Join(
            ";",
            candidates.Select(card =>
                $"{deckCards.IndexOf(card)}:{card.Id.Entry}:{card.IsUpgraded}:{card.Enchantment?.Id.Entry ?? "无"}:{card.Enchantment?.Amount ?? 0}"));

        return StableRandomHelper.StableIndex(
            player,
            candidates.Count,
            "AncientRatsoTransformHelper.SelectTargetRatso",
            candidateKeys);
    }

    /// <summary>
    /// 创建先古拉苏，并继承旧拉苏的升级与附魔。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="oldCard">被变化的黑手帮·拉苏。</param>
    /// <returns>可用于变化的新拉苏。</returns>
    private static CardModel CreateAncientRatso(Player player, CardModel oldCard)
    {
        CardModel replacement = player.RunState.CreateCard<AncientRatso>(player);
        if (oldCard.IsUpgraded && replacement.IsUpgradable)
        {
            CardCmd.Upgrade(replacement, CardPreviewStyle.None);
        }

        CopyEnchantment(oldCard, replacement);
        return replacement;
    }

    /// <summary>
    /// 将旧卡附魔复制到新卡，复制失败时只记日志，避免古老牙齿流程中断。
    /// </summary>
    /// <param name="oldCard">旧卡。</param>
    /// <param name="replacement">新卡。</param>
    private static void CopyEnchantment(CardModel oldCard, CardModel replacement)
    {
        if (oldCard.Enchantment == null)
        {
            return;
        }

        try
        {
            EnchantmentModel enchantment = (EnchantmentModel)oldCard.Enchantment.MutableClone();
            CardCmd.Enchant(enchantment, replacement, enchantment.Amount);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【古老牙齿·拉苏】复制附魔失败：旧卡={oldCard.Id.Entry}，新卡={replacement.Id.Entry}，附魔={oldCard.Enchantment.Id.Entry}，原因={ex.Message}。");
        }
    }
}
