using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Curses.GrandMageDad;

/// <summary>
/// 小玉诅咒：抽到后弃 1 张牌，并向抽牌堆顶塞入 1 张晕眩。
/// </summary>
public class CurseJade : GrandMageDadCurseCard
{
    /// <summary>
    /// 抽到时执行小玉的干扰效果。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="card">被抽到的卡牌。</param>
    /// <param name="fromHandDraw">是否来自手牌抽牌。</param>
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (!ReferenceEquals(card, this) || Owner?.Creature == null || Owner.PlayerCombatState == null)
        {
            return;
        }

        await DiscardFirstOtherHandCard();
        await CardPileCmdHelper.AddToCombatAndPreview<Dazed>(Owner.Creature, PileType.Draw, 1, true, CardPilePosition.Top);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【大法师老爹诅咒】【小玉】抽到触发：玩家={Owner.NetId}，弃1张牌并向抽牌堆顶加入1张晕眩。");
    }

    /// <summary>
    /// 稳定丢弃第一张非自身手牌，避免联机客户端各自弹选择框导致分叉。
    /// </summary>
    private async Task DiscardFirstOtherHandCard()
    {
        if (Owner?.PlayerCombatState == null)
        {
            return;
        }

        CardModel? discardCard = Owner.PlayerCombatState.Hand.Cards.FirstOrDefault(card => !ReferenceEquals(card, this));
        if (discardCard == null)
        {
            return;
        }

        await CardPileCmd.Add(discardCard, PileType.Discard);
    }
}
