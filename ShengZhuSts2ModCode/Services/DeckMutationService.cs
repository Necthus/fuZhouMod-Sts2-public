using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>
/// 主牌组修改服务：集中处理觉醒等流程里的主牌组加牌、删牌和 RunState 注册。
/// </summary>
public static class DeckMutationService
{
    /// <summary>
    /// 将指定卡牌加入玩家主牌组，并保持当前项目原有的 RunState 注册顺序。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="card">要加入主牌组的卡牌。</param>
    /// <param name="triggerCallbacks">是否触发牌堆内部回调。</param>
    /// <param name="reason">调用原因，用于日志排查。</param>
    public static void AddCardToDeck(Player? player, CardModel? card, bool triggerCallbacks, string reason)
    {
        if (player?.Deck?.Cards == null || card == null)
        {
            MainFile.Logger.Info($"【牌组修改】加入主牌组失败：原因={reason}，玩家或卡牌为空。");
            return;
        }

        if (player.RunState is RunState runState)
        {
            runState.AddCard(card, player);
        }

        player.Deck.AddInternal(card, player.Deck.Cards.Count, triggerCallbacks);
        MainFile.Logger.Info($"【牌组修改】加入主牌组：原因={reason}，卡牌={card.Id.Entry}，触发回调={triggerCallbacks}，牌组数量={player.Deck.Cards.Count}。");
    }

    /// <summary>
    /// 从玩家主牌组移除指定卡牌；卡牌不在牌组中时只记录日志并跳过。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="card">要移除的卡牌。</param>
    /// <param name="triggerCallbacks">是否触发牌堆内部回调。</param>
    /// <param name="reason">调用原因，用于日志排查。</param>
    public static void RemoveCardFromDeck(Player? player, CardModel? card, bool triggerCallbacks, string reason)
    {
        if (player?.Deck?.Cards == null || card == null)
        {
            MainFile.Logger.Info($"【牌组修改】移除主牌组失败：原因={reason}，玩家或卡牌为空。");
            return;
        }

        if (!player.Deck.Cards.Contains(card))
        {
            MainFile.Logger.Info($"【牌组修改】移除主牌组跳过：原因={reason}，卡牌={card.Id.Entry} 已不在主牌组。");
            return;
        }

        player.Deck.RemoveInternal(card, triggerCallbacks);
        MainFile.Logger.Info($"【牌组修改】移除主牌组：原因={reason}，卡牌={card.Id.Entry}，触发回调={triggerCallbacks}，牌组数量={player.Deck.Cards.Count}。");
    }
}
