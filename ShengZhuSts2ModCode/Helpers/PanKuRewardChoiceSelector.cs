using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.PanKuPreview;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 潘库奖励选择器：直接复用原生选牌界面节点，但不调用 CardSelectCmd，避免联机 Choice IDs 不同步。
/// </summary>
internal static class PanKuRewardChoiceSelector
{
    /// <summary>
    /// 选择潘库奖励类型。
    /// </summary>
    /// <param name="player">领取奖励的玩家。</param>
    /// <param name="kind">本次魔气类型。</param>
    /// <param name="canEnchant">当前是否存在可附魔目标。</param>
    /// <returns>玩家选择的奖励类型；取消时返回 None。</returns>
    public static async Task<PanKuRewardClaimType> SelectRewardType(Player player, PanKuDemonQiKind kind, bool canEnchant)
    {
        CardModel demonCard = PanKuDemonQiHelper.CreateDemonCard(player, kind);
        CardModel enchantPreview = player.RunState.CreateCard<PanKuEnchantPreviewCard>(player);
        CardModel storePreview = player.RunState.CreateCard<PanKuStoreQiPreviewCard>(player);
        if (enchantPreview is PanKuEnchantPreviewCard panKuEnchantPreview)
        {
            panKuEnchantPreview.Kind = kind;
        }

        if (storePreview is PanKuStoreQiPreviewCard panKuStorePreview)
        {
            panKuStorePreview.Kind = kind;
        }

        List<CardModel> choices = canEnchant
            ? [demonCard, enchantPreview, storePreview]
            : [demonCard, storePreview];
        CardSelectorPrefs prefs = new(new LocString("relics", "SHENGZHUSTS2MOD-PAN_KU_BOX.choicePrompt"), 0, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };

        CardModel? selected = await SelectOneCard(choices, prefs);
        if (selected == null)
        {
            return PanKuRewardClaimType.None;
        }

        if (ReferenceEquals(selected, demonCard) || PanKuDemonQiHelper.TryGetKindFromCard(selected, out _))
        {
            return PanKuRewardClaimType.DemonCard;
        }

        if (selected is PanKuStoreQiPreviewCard)
        {
            return PanKuRewardClaimType.StoreQi;
        }

        return selected is PanKuEnchantPreviewCard
            ? PanKuRewardClaimType.EnchantDeckCard
            : PanKuRewardClaimType.None;
    }

    /// <summary>
    /// 选择潘库直接附魔目标牌。
    /// </summary>
    /// <param name="player">领取奖励的玩家。</param>
    /// <param name="kind">本次魔气类型。</param>
    /// <returns>玩家选择的目标牌；取消时返回 null。</returns>
    public static Task<CardModel?> SelectEnchantTarget(Player player, PanKuDemonQiKind kind)
    {
        List<CardModel> targets = PanKuDemonQiHelper.GetEnchantTargets(player, kind);
        LocString prompt = new("relics", "SHENGZHUSTS2MOD-PAN_KU_BOX.enchantPrompt");
        prompt.Add("EnchantTargetType", PanKuDemonQiHelper.GetEnchantTargetTypeText(kind));
        CardSelectorPrefs prefs = new(prompt, 0, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };

        return SelectOneCard(targets, prefs);
    }

    /// <summary>
    /// 用游戏原生简单选牌界面选择一张牌，不经过 CardSelectCmd 的 Choice ID 同步层。
    /// </summary>
    /// <param name="cards">候选卡牌。</param>
    /// <param name="prefs">选择界面配置。</param>
    /// <returns>选中的卡牌；取消时返回 null。</returns>
    internal static async Task<CardModel?> SelectOneCard(IReadOnlyList<CardModel> cards, CardSelectorPrefs prefs)
    {
        if (cards.Count == 0)
        {
            MainFile.Logger.Info("【联机异常兜底】【潘库宝盒】原生选择界面候选为空，处理=取消。");
            return null;
        }

        if (NOverlayStack.Instance == null)
        {
            MainFile.Logger.Info("【联机异常兜底】【潘库宝盒】原生选择界面创建失败：找不到覆盖层栈，处理=取消。");
            return null;
        }

        NSimpleCardSelectScreen screen = NSimpleCardSelectScreen.Create(cards, prefs);
        NOverlayStack.Instance.Push(screen);
        return (await screen.CardsSelected()).FirstOrDefault();
    }
}
