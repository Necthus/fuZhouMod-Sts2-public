using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.NiJia;

/// <summary>
/// 翼装飞行能力：让尼嘉-忍者团获得保留，并在每回合打出指定数量尼嘉-忍者团后获得临时影噬。
/// </summary>
public class WingsuitFlightPower : ShengZhuSts2ModPower
{
    /// <summary>
    /// 每打出几张尼嘉-忍者团获得1点临时影噬。
    /// </summary>
    private const int NiJiaNinjaPerTemporaryDominion = 2;

    /// <summary>
    /// 本回合已打出的尼嘉-忍者团数量。
    /// </summary>
    private int _niJiaNinjaPlayedThisTurn;

    /// <summary>
    /// 本回合通过翼装飞行获得、回合结束需要扣回的影噬数量。
    /// </summary>
    private int _temporaryDominionGainedThisTurn;

    /// <summary>
    /// 翼装飞行属于增益能力。
    /// </summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 翼装飞行可以叠层，每层都会提高每次触发获得的临时影噬。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 能力首次施加后，让当前手牌中已有的尼嘉-忍者团立刻获得保留。
    /// </summary>
    /// <param name="applier">施加来源。</param>
    /// <param name="cardSource">来源卡牌。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        int retainedCount = ApplyRetainToCurrentHand(Owner?.Player, "翼装飞行能力施加");
        MainFile.Logger.Info($"【联机同步】来源=翼装飞行，玩家={FormatPlayer(Owner?.Player)}，动作=能力施加后补当前手牌保留，补保留数量={retainedCount}。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 当前玩家回合开始时，重置本回合尼嘉-忍者团打出计数。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="player">当前回合玩家。</param>
    /// <returns>异步任务。</returns>
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner != null && player.Creature == Owner)
        {
            _niJiaNinjaPlayedThisTurn = 0;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 每打出2张自己的尼嘉-忍者团，本回合获得等同于翼装飞行层数的临时影噬。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="cardPlay">刚打出的牌信息。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner == null || cardPlay.Card is not NiJiaNinja || cardPlay.Card.Owner?.Creature != Owner)
        {
            return;
        }

        _niJiaNinjaPlayedThisTurn++;
        if (_niJiaNinjaPlayedThisTurn % NiJiaNinjaPerTemporaryDominion != 0)
        {
            return;
        }

        int dominionGain = Math.Max(1, Amount);
        Flash();
        await PowerCmd.Apply<DominionPower>(Owner, dominionGain, Owner, null);
        _temporaryDominionGainedThisTurn += dominionGain;
        MainFile.Logger.Info($"【联机同步】来源=翼装飞行，玩家={FormatPlayer(Owner.Player)}，动作=打出2张尼嘉-忍者团获得临时影噬，本回合尼嘉计数={_niJiaNinjaPlayedThisTurn}，翼装飞行层数={Amount}，本次影噬={dominionGain}，本回合待扣影噬={_temporaryDominionGainedThisTurn}。");
    }

    /// <summary>
    /// 玩家阵营回合结束时，扣回本回合通过翼装飞行获得的临时影噬。
    /// </summary>
    /// <param name="choiceContext">当前选择上下文。</param>
    /// <param name="side">当前结束回合的阵营。</param>
    /// <param name="participants">当前阵营参与者。</param>
    /// <returns>异步任务。</returns>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner == null || side != Owner.Side)
        {
            return;
        }

        _niJiaNinjaPlayedThisTurn = 0;
        if (_temporaryDominionGainedThisTurn <= 0)
        {
            return;
        }

        Creature owner = Owner;
        int dominionToRemove = _temporaryDominionGainedThisTurn;
        _temporaryDominionGainedThisTurn = 0;
        Flash();
        await PowerCmd.Apply<DominionPower>(owner, -dominionToRemove, owner, null);
        MainFile.Logger.Info($"【联机同步】来源=翼装飞行，玩家={FormatPlayer(owner.Player)}，动作=回合结束回收临时影噬，影噬扣回={dominionToRemove}。");
    }

    /// <summary>
    /// 给指定玩家当前手牌里的尼嘉-忍者团补上保留关键词。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="source">触发来源说明。</param>
    /// <returns>本次新增保留的卡牌数量。</returns>
    public static int ApplyRetainToCurrentHand(Player? player, string source)
    {
        if (player?.PlayerCombatState?.Hand == null)
        {
            return 0;
        }

        int retainedCount = 0;
        foreach (CardModel card in player.PlayerCombatState.Hand.Cards)
        {
            if (TryApplyRetainToNiJiaNinja(player, card, source))
            {
                retainedCount++;
            }
        }

        return retainedCount;
    }

    /// <summary>
    /// 给单张尼嘉-忍者团补上保留关键词；只处理当前玩家自己的卡牌，避免联机误影响队友。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="card">待处理卡牌。</param>
    /// <param name="source">触发来源说明。</param>
    /// <returns>本次是否实际新增了保留关键词。</returns>
    public static bool TryApplyRetainToNiJiaNinja(Player? player, CardModel? card, string source)
    {
        if (player?.Creature == null || card is not NiJiaNinja || !IsOwnerCard(player, card))
        {
            return false;
        }

        if (!player.Creature.HasPower<WingsuitFlightPower>() || card.Keywords.Contains(CardKeyword.Retain))
        {
            return false;
        }

        card.AddKeyword(CardKeyword.Retain);
        MainFile.Logger.Info($"【联机同步】来源=翼装飞行，玩家={FormatPlayer(player)}，动作=尼嘉-忍者团补保留，来源场景={source}，卡牌={card.Id.Entry}。");
        return true;
    }

    /// <summary>
    /// 判断卡牌是否属于当前玩家。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="card">待检查卡牌。</param>
    /// <returns>属于当前玩家时返回 true。</returns>
    private static bool IsOwnerCard(Player player, CardModel card)
    {
        if (card.Owner == null)
        {
            return false;
        }

        return ReferenceEquals(card.Owner, player) || card.Owner.NetId == player.NetId;
    }

    /// <summary>
    /// 格式化玩家信息，方便联机日志对比。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>玩家网络ID和角色ID。</returns>
    private static string FormatPlayer(Player? player)
    {
        return player == null ? "无玩家" : $"{player.NetId}/{player.Character.Id.Entry}";
    }
}
