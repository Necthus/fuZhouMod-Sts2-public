using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;

/// <summary>
/// 无尽黑暗能力：每回合开始时，消耗牌堆中每有10张黑影兵团牌，获得1层影噬。
/// </summary>
public class EndlessDarknessPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 每10张消耗堆黑影兵团牌提供1层影噬。
    /// </summary>
    private const int ShadowKhanCardsPerDominion = 10;

    /// <summary>
    /// 记录本能力已经按消耗堆黑影兵团数量补过的影噬层数，避免每回合重复累加旧档位。
    /// </summary>
    private int _dominionGrantedByExhaust;

    /// <summary>
    /// 无尽黑暗属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 无尽黑暗自身不靠层数显示强度。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>
    /// 玩家回合开始时，根据消耗堆黑影兵团牌数量获得影噬。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null || Owner.IsDead || player.Creature != Owner)
        {
            return;
        }

        int shadowKhanCount = CountExhaustedShadowKhanCards(player);
        int expectedDominion = shadowKhanCount / ShadowKhanCardsPerDominion;
        int dominionGain = expectedDominion - _dominionGrantedByExhaust;
        if (dominionGain <= 0)
        {
            return;
        }

        _dominionGrantedByExhaust = expectedDominion;
        Flash();
        await PowerCmd.Apply<DominionPower>(Owner, dominionGain, Owner, null);
        MainFile.Logger.Info($"【无尽黑暗】回合开始触发：消耗堆黑影兵团={shadowKhanCount}张，应有影噬成长={expectedDominion}，本次补充影噬={dominionGain}。");
    }

    /// <summary>
    /// 统计玩家消耗牌堆中的黑影兵团牌数量。
    /// </summary>
    private static int CountExhaustedShadowKhanCards(Player player)
    {
        IReadOnlyList<CardModel>? cards = player.PlayerCombatState?.ExhaustPile.Cards;
        return cards?.Count(card => card is BaseShadowKhanCard) ?? 0;
    }
}
