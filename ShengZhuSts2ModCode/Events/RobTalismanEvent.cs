using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Events;

/// <summary>
/// 抢夺符咒事件：正气被符咒数量吸引而来，玩家需要付出代价逃脱，或直接选择精英战斗。
/// </summary>
public class RobTalismanEvent : CustomEventModel
{
    /// <summary>
    /// 抢夺符咒事件在联机时走共享事件投票，避免一人开战后其他选择也被强行带入战斗。
    /// </summary>
    public override bool IsShared => true;

    /// <summary>
    /// 抢夺符咒事件使用从 1 代迁移来的事件图。
    /// </summary>
    public override string? CustomInitialPortraitPath => $"{MainFile.ResPath}/images/events/shengzhusts2mod-rob_talisman_event.png";

    /// <summary>
    /// 事件只允许圣主且至少拥有 1 个符咒时进入事件池。
    /// </summary>
    /// <param name="runState">当前爬塔状态。</param>
    /// <returns>满足抢夺符咒触发条件时返回 true。</returns>
    public override bool IsAllowed(IRunState runState)
    {
        return runState.Players.Any(player => player.Character is ShengZhu && RobTalismanEventHelper.CountOwnedTalismans(player) > 0);
    }

    /// <summary>
    /// 生成事件初始选项，根据冒牌符咒和金币数量控制锁定状态。
    /// </summary>
    /// <returns>当前页事件选项。</returns>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        bool hasFakeTalisman = SharedEventVoteHelper.AllParticipantsMatch(Owner, IsRobTalismanParticipant, RobTalismanEventHelper.HasFakeTalisman);
        bool hasEnoughGold = SharedEventVoteHelper.AllParticipantsMatch(Owner, IsRobTalismanParticipant, player => player.Gold >= RobTalismanEventHelper.GoldEscapeThreshold);

        return
        [
            new EventOption(this, StartEliteCombat, OptionKey("START_ELITE_COMBAT")),
            hasFakeTalisman
                ? new EventOption(this, EscapeWithFakeTalisman, OptionKey("FAKE_TALISMAN"))
                : new EventOption(this, null, OptionKey("FAKE_TALISMAN_LOCKED")),
            new EventOption(this, LoseRandomTalisman, OptionKey("LOSE_TALISMAN")),
            new EventOption(this, EndureDamage, OptionKey("ENDURE_DAMAGE")),
            hasEnoughGold
                ? new EventOption(this, EscapeWithGold, OptionKey("GOLD_ESCAPE"))
                : new EventOption(this, null, OptionKey("GOLD_ESCAPE_LOCKED"))
        ];
    }

    /// <summary>
    /// 交出 1 张冒牌符咒来骗过对方。
    /// </summary>
    private async Task EscapeWithFakeTalisman()
    {
        if (!TryGetPersonalCostOwner("交出冒牌符咒", "FAKE_TALISMAN", out Player player)) return;

        await RobTalismanEventHelper.RemoveOneFakeTalisman(player);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.FAKE_TALISMAN.description"));
    }

    /// <summary>
    /// 失去全部金币，并获得悔恨。
    /// </summary>
    private async Task EscapeWithGold()
    {
        if (!TryGetPersonalCostOwner("大撒币逃走", "GOLD_ESCAPE", out Player player)) return;

        await RobTalismanEventHelper.LoseAllGoldAndGainRegret(player);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.GOLD_ESCAPE.description"));
    }

    /// <summary>
    /// 随机失去 1 个已拥有符咒。
    /// </summary>
    private async Task LoseRandomTalisman()
    {
        if (!TryGetPersonalCostOwner("舍弃符咒", "LOSE_TALISMAN", out Player player)) return;

        await RobTalismanEventHelper.RemoveRandomOwnedTalisman(player);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.LOSE_TALISMAN.description"));
    }

    /// <summary>
    /// 受到伤害，并随机获得 2 张当前游戏诅咒。
    /// </summary>
    private async Task EndureDamage()
    {
        if (!TryGetPersonalCostOwner("硬扛伤害", "ENDURE_DAMAGE", out Player player)) return;

        await RobTalismanEventHelper.TakeDamageAndGainRandomCurses(player);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.ENDURE_DAMAGE.description"));
    }

    /// <summary>
    /// 直接进入当前章节随机精英战斗，按精英战斗正常结算奖励。
    /// </summary>
    private async Task StartEliteCombat()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        bool started = await RobTalismanEventHelper.StartRandomEliteCombat(Owner);
        if (!started)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_ELITE.description"));
        }
    }

    /// <summary>
    /// 生成当前事件选项文本键。
    /// </summary>
    /// <param name="optionName">选项名称。</param>
    /// <returns>完整本地化键。</returns>
    private string OptionKey(string optionName)
    {
        return $"{Id.Entry}.pages.INITIAL.options.{optionName}";
    }

    /// <summary>
    /// 获取当前抢夺符咒事件副本应该结算个人代价的玩家；共享事件中非本地玩家副本只结束事件，不重复改状态。
    /// </summary>
    /// <param name="actionName">当前个人代价名称，用于日志。</param>
    /// <param name="pageName">结算完成后的事件页名称。</param>
    /// <param name="player">需要结算个人代价的玩家。</param>
    /// <returns>拿到需要结算的玩家时返回 true。</returns>
    private bool TryGetPersonalCostOwner(string actionName, string pageName, out Player player)
    {
        player = Owner!;
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return false;
        }

        if (!IsRobTalismanParticipant(Owner))
        {
            MainFile.Logger.Info($"【抢夺符咒】共享事件{actionName}跳过：玩家={Owner.NetId}/{Owner.Character.Id.Entry}，原因=不是需要结算的圣主符咒持有者。");
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.{pageName}.description"));
            return false;
        }

        if (!SharedEventVoteHelper.ShouldExecuteLocalPlayerAction(Owner, $"抢夺符咒-{actionName}"))
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.{pageName}.description"));
            return false;
        }

        return true;
    }

    /// <summary>
    /// 判断玩家是否参与抢夺符咒事件的个人代价结算。
    /// </summary>
    /// <param name="player">待判断玩家。</param>
    /// <returns>圣主且至少拥有 1 个符咒时返回 true。</returns>
    private static bool IsRobTalismanParticipant(Player player)
    {
        return player.Character is ShengZhu && RobTalismanEventHelper.CountOwnedTalismans(player) > 0;
    }
}
