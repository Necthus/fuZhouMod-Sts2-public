using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// 无尽黑暗觉醒检查补丁：拦截 CardPile.AddInternal 方法，
/// 当面具牌被加入牌组（Deck类型）时检查是否集齐10种面具。
/// </summary>
[HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
public static class EndlessDarknessPatch
{
    /// <summary>
    /// 正在执行无尽黑暗觉醒检查的玩家，防止同一玩家重入时重复触发。
    /// </summary>
    private static readonly HashSet<Player> AwakeningPlayers = [];

    /// <summary>
    /// 后置补丁：卡牌加入牌组后，若为面具牌且牌组类型为Deck，则触发觉醒检查。
    /// </summary>
    public static void Postfix(CardPile __instance, CardModel card)
    {
        // 只关心 Deck 类型的牌堆（主牌组）
        if (__instance.Type != PileType.Deck)
        {
            return;
        }

        // 只关心面具牌
        if (!IsMaskCard(card))
        {
            return;
        }

        // 找到拥有这个牌组的玩家
        var player = FindOwnerPlayer(card);
        if (player == null)
        {
            return;
        }

        if (!AwakeningPlayers.Add(player))
        {
            return;
        }

        try
        {
            EndlessDarknessHelper.CheckAndTriggerAwakening(player);
        }
        finally
        {
            AwakeningPlayers.Remove(player);
        }
    }

    /// <summary>
    /// 判断卡牌是否为面具牌。
    /// </summary>
    private static bool IsMaskCard(CardModel card)
    {
        return card is BaseMaskCard or TaLaMask;
    }

    /// <summary>
    /// 通过卡牌的 Owner 属性找到对应的玩家。
    /// </summary>
    private static Player? FindOwnerPlayer(CardModel card)
    {
        return card.Owner;
    }
}
