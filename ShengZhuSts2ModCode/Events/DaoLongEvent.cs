using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Events;

/// <summary>
/// 刀龙黑气事件：将圣主牌组中的周、拉苏、阿奋和泰山压顶加持为甘、文、崔、山。
/// </summary>
public class DaoLongEvent : CustomEventModel
{
    /// <summary>
    /// 刀龙黑气事件在联机时走共享事件投票，避免玩家各自选择后把全队拉进不同结果。
    /// </summary>
    public override bool IsShared => true;

    /// <summary>
    /// 刀龙黑气事件使用 Mod 目录下的事件图，避免默认事件图路径指向游戏根目录。
    /// </summary>
    public override string? CustomInitialPortraitPath => $"{MainFile.ResPath}/images/events/shengzhusts2mod-dao_long_event.png";

    /// <summary>
    /// 事件只在圣主且牌组存在可加持目标时进入事件池。
    /// </summary>
    /// <param name="runState">当前爬塔状态。</param>
    /// <returns>允许进入事件池时返回 true。</returns>
    public override bool IsAllowed(IRunState runState)
    {
        return StoryEventReplacementConfigService.IsStoryEventAllowed(runState, "刀龙黑气")
            && runState.Players.Any(player => player.Character is ShengZhu && DaoLongBlackQiHelper.HasEligibleCards(player));
    }

    /// <summary>
    /// 生成事件初始选项；正常情况下有目标牌时只能接受，无目标牌时只能离开。
    /// </summary>
    /// <returns>当前页事件选项。</returns>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        Player? canonicalPlayer = SharedEventVoteHelper.GetCanonicalPlayer(Owner);
        bool canAcceptDarkChi = SharedEventVoteHelper.AllParticipantsMatch(Owner, IsDaoLongParticipant, DaoLongBlackQiHelper.HasEligibleCards);
        EventOption eliteCombatOption = DaoLongEventCombatHelper.HasFirstActEliteEncounters(canonicalPlayer)
            ? new EventOption(this, StartEliteCombat, OptionKey("INITIAL", "START_ELITE_COMBAT"))
            : new EventOption(this, null, OptionKey("INITIAL", "START_ELITE_COMBAT_LOCKED"));

        if (canAcceptDarkChi)
        {
            return
            [
                new EventOption(this, AcceptDarkChi, OptionKey("ACCEPT")),
                eliteCombatOption,
                new EventOption(this, null, OptionKey("DECLINE_LOCKED"))
            ];
        }

        return
        [
            new EventOption(this, null, OptionKey("ACCEPT_LOCKED")),
            eliteCombatOption,
            new EventOption(this, LeaveWithoutTargets, OptionKey("LEAVE"))
        ];
    }

    /// <summary>
    /// 接受刀龙黑气加持，执行牌组替换并结束事件。
    /// </summary>
    private async Task AcceptDarkChi()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_TARGET.description"));
            return;
        }

        if (!IsDaoLongParticipant(Owner) || !DaoLongBlackQiHelper.HasEligibleCards(Owner))
        {
            MainFile.Logger.Info($"【刀龙黑气事件】共享事件接受加持跳过：玩家={Owner.NetId}/{Owner.Character.Id.Entry}，原因=不是圣主或没有可加持目标。");
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.ACCEPTED.description"));
            return;
        }

        int transformedCount = await DaoLongBlackQiHelper.TransformDeck(Owner);
        MainFile.Logger.Info($"【刀龙黑气事件】接受加持完成，替换数量={transformedCount}");
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.ACCEPTED.description"));
    }

    /// <summary>
    /// 进入第一层精英战斗；战斗胜利后按普通精英奖励流程直接继续。
    /// </summary>
    private async Task StartEliteCombat()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        bool started = await DaoLongEventCombatHelper.StartFirstActEliteCombat(Owner);
        if (!started)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_ELITE.description"));
        }
    }

    /// <summary>
    /// 没有可加持目标时离开事件。
    /// </summary>
    private Task LeaveWithoutTargets()
    {
        MainFile.Logger.Info("【刀龙黑气事件】没有可加持目标，玩家离开。");
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_TARGET.description"));
        return Task.CompletedTask;
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
    /// 生成指定事件页的选项文本键。
    /// </summary>
    /// <param name="pageName">页面名。</param>
    /// <param name="optionName">选项名称。</param>
    /// <returns>完整本地化键。</returns>
    private string OptionKey(string pageName, string optionName)
    {
        return $"{Id.Entry}.pages.{pageName}.options.{optionName}";
    }

    /// <summary>
    /// 判断玩家是否参与刀龙黑气事件的个人牌组结算。
    /// </summary>
    /// <param name="player">待判断玩家。</param>
    /// <returns>圣主玩家返回 true。</returns>
    private static bool IsDaoLongParticipant(Player player)
    {
        return player.Character is ShengZhu;
    }
}
