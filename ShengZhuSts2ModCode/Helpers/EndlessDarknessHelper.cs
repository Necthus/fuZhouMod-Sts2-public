using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 无尽黑暗觉醒辅助类：检测牌组是否集齐10种面具牌，若集齐则移除10张面具牌并获得无尽黑暗。
/// </summary>
public static class EndlessDarknessHelper
{
    /// <summary>
    /// 每名玩家的无尽黑暗检查延后层数：用于卡牌变换等流程中，避免新牌刚入组就被觉醒逻辑移除。
    /// </summary>
    private static readonly Dictionary<Player, int> AwakeningCheckDelayDepthByPlayer = new();

    /// <summary>
    /// 没有明确玩家参数时，按当前异步流程隔离延后检查，避免影响其他玩家。
    /// </summary>
    private static readonly AsyncLocal<int> FallbackAwakeningCheckDelayDepth = new();

    /// <summary>
    /// 正在触发无尽黑暗觉醒的玩家，避免同一玩家重复删牌加牌。
    /// </summary>
    private static readonly HashSet<Player> AwakeningPlayers = [];

    /// <summary>
    /// 10种面具牌的类型列表。
    /// </summary>
    private static readonly Type[] RequiredMaskCardTypes =
    [
        typeof(NiJiaMask),
        typeof(LaZuoMask),
        typeof(SaMoMask),
        typeof(BaTeMask),
        typeof(KaBoMask),
        typeof(LeiSuMask),
        typeof(ManNiMask),
        typeof(MingTaMask),
        typeof(YiKaMask),
        typeof(TaLaMask)
    ];

    /// <summary>
    /// 检查玩家牌组是否集齐10种面具牌，若集齐则触发觉醒。
    /// </summary>
    /// <param name="player">要检查的玩家。</param>
    public static void CheckAndTriggerAwakening(Player? player)
    {
        if (player == null)
        {
            return;
        }

        if (GetAwakeningCheckDelayDepth(player) > 0)
        {
            MainFile.Logger.Info("【无尽黑暗】当前处于延后检查状态，本次觉醒检查跳过。");
            return;
        }

        if (AwakeningPlayers.Contains(player))
        {
            return;
        }

        var deckCards = player.Deck.Cards;
        if (deckCards == null)
        {
            return;
        }

        // 如果牌组中已有无尽黑暗，不重复触发
        if (deckCards.Any(c => c is EndlessDarkness))
        {
            return;
        }

        // 检查是否集齐10种面具牌
        foreach (var maskType in RequiredMaskCardTypes)
        {
            if (!deckCards.Any(c => c.GetType() == maskType))
            {
                return;
            }
        }

        // 集齐10种面具，触发觉醒
        TriggerAwakening(player);
    }

    /// <summary>
    /// 开始延后无尽黑暗检查。
    /// </summary>
    public static void BeginDelayedAwakeningCheck()
    {
        FallbackAwakeningCheckDelayDepth.Value++;
    }

    /// <summary>
    /// 开始延后指定玩家的无尽黑暗检查。
    /// </summary>
    /// <param name="player">要延后检查的玩家。</param>
    public static void BeginDelayedAwakeningCheck(Player? player)
    {
        if (player == null)
        {
            BeginDelayedAwakeningCheck();
            return;
        }

        AwakeningCheckDelayDepthByPlayer[player] = GetPlayerAwakeningCheckDelayDepth(player) + 1;
    }

    /// <summary>
    /// 结束延后无尽黑暗检查。
    /// </summary>
    public static void EndDelayedAwakeningCheck()
    {
        if (FallbackAwakeningCheckDelayDepth.Value > 0)
        {
            FallbackAwakeningCheckDelayDepth.Value--;
        }
    }

    /// <summary>
    /// 结束延后指定玩家的无尽黑暗检查。
    /// </summary>
    /// <param name="player">要恢复检查的玩家。</param>
    public static void EndDelayedAwakeningCheck(Player? player)
    {
        if (player == null)
        {
            EndDelayedAwakeningCheck();
            return;
        }

        int depth = GetPlayerAwakeningCheckDelayDepth(player);
        if (depth <= 1)
        {
            AwakeningCheckDelayDepthByPlayer.Remove(player);
            return;
        }

        AwakeningCheckDelayDepthByPlayer[player] = depth - 1;
    }

    /// <summary>
    /// 触发无尽黑暗觉醒：从牌组中每种面具删除1张（优先删基础版），然后加入1张无尽黑暗。
    /// </summary>
    /// <param name="player">触发觉醒的玩家。</param>
    private static void TriggerAwakening(Player player)
    {
        if (!AwakeningPlayers.Add(player))
        {
            return;
        }

        MainFile.Logger.Info("【无尽黑暗】集齐10种面具牌，触发觉醒！");

        try
        {
            var deckCards = player.Deck.Cards;
            var cardsToRemove = new List<CardModel>();

            // 每种面具找1张删除，优先删除基础版
            foreach (var maskType in RequiredMaskCardTypes)
            {
                var masksOfType = deckCards.Where(c => c.GetType() == maskType).ToList();

                // 优先选择未升级版本
                var unupgraded = masksOfType.FirstOrDefault(c => !c.IsUpgraded);
                var target = unupgraded ?? masksOfType.First();
                cardsToRemove.Add(target);
            }

            // 从牌组中移除10张面具牌，具体牌组修改统一交给服务处理。
            foreach (var card in cardsToRemove)
            {
                DeckMutationService.RemoveCardFromDeck(player, card, false, "无尽黑暗觉醒");
                MainFile.Logger.Info($"【无尽黑暗】移除面具牌：{card.GetType().Name}，升级={card.IsUpgraded}");
            }

            // 加入1张无尽黑暗，具体牌组修改统一交给服务处理。
            var endlessDarkness = (CardModel)ModelDb.Card<EndlessDarkness>().ToMutable();
            DeckMutationService.AddCardToDeck(player, endlessDarkness, false, "无尽黑暗觉醒");
            MainFile.Logger.Info("【无尽黑暗】觉醒完成，无尽黑暗已加入牌组。");
        }
        finally
        {
            AwakeningPlayers.Remove(player);
        }
    }

    /// <summary>
    /// 获取指定玩家的无尽黑暗检查延后层数。
    /// </summary>
    /// <param name="player">要读取的玩家。</param>
    /// <returns>延后层数。</returns>
    private static int GetPlayerAwakeningCheckDelayDepth(Player player)
    {
        return AwakeningCheckDelayDepthByPlayer.GetValueOrDefault(player);
    }

    /// <summary>
    /// 获取当前玩家与当前异步流程合并后的延后层数。
    /// </summary>
    /// <param name="player">要读取的玩家。</param>
    /// <returns>延后层数。</returns>
    private static int GetAwakeningCheckDelayDepth(Player player)
    {
        return Math.Max(GetPlayerAwakeningCheckDelayDepth(player), FallbackAwakeningCheckDelayDepth.Value);
    }
}
