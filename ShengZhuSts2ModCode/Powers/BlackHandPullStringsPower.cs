using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 捞人能力：回合开始时从弃牌堆选择牌回到手中。
/// </summary>
public class BlackHandPullStringsPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 捞人属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 捞人使用计数器显示每回合可捞回的张数。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 玩家回合开始时，从弃牌堆中选择指定数量的牌加入手牌。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">当前回合玩家。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner?.Player == null || player != Owner.Player || player.PlayerCombatState == null)
        {
            return;
        }

        if (player.PlayerCombatState.Hand.Cards.Count >= 10)
        {
            return;
        }

        List<CardModel> discardCards = player.PlayerCombatState.DiscardPile.Cards.ToList();
        if (discardCards.Count == 0)
        {
            return;
        }

        int remainingHandSlots = 10 - player.PlayerCombatState.Hand.Cards.Count;
        int maxSelectable = Math.Min(Amount, Math.Min(discardCards.Count, remainingHandSlots));
        if (maxSelectable <= 0)
        {
            return;
        }

        CardSelectorPrefs prefs = new CardSelectorPrefs(
            new LocString("powers", Id.Entry + ".selectionScreenPrompt"),
            1,
            maxSelectable);
        IReadOnlyList<CardModel> selectedCards = (await CardSelectCmd.FromSimpleGrid(choiceContext, discardCards, player, prefs)).ToList();

        foreach (CardModel selectedCard in selectedCards)
        {
            // 先授予黑手帮身份，再移动到手牌，避免卡面进手牌时先刷新出旧描述。
            BlackHandCardHelper.GrantBlackHandCardTagForCombat(selectedCard);
            await CardPileCmd.Add(selectedCard, PileType.Hand);
        }

        MainFile.Logger.Info($"【捞人】回合开始捞回卡牌：玩家={player.NetId}，选择数量={selectedCards.Count}，上限={maxSelectable}。");
    }
}
