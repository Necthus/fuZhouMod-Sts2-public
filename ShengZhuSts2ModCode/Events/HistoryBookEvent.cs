using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Events;

/// <summary>
/// 岁月史书事件：提供重写十二符咒、重写十面具和撕下残卷三条路线。
/// </summary>
public class HistoryBookEvent : CustomEventModel
{
    /// <summary>
    /// 岁月史书事件在联机时使用共享事件投票，确保所有玩家选同一条路线。
    /// </summary>
    public override bool IsShared => true;

    /// <summary>
    /// 使用塔2圣主美术素材中的岁月史书背景图。
    /// </summary>
    public override string? CustomInitialPortraitPath => $"{MainFile.ResPath}/images/events/history_book_event.png";

    /// <summary>
    /// 圣主跑团遵循选人事件勾选和幕数；其它角色保留原有自然出现条件。
    /// </summary>
    /// <param name="runState">当前跑团状态。</param>
    /// <returns>当前跑团配置允许时返回 true。</returns>
    public override bool IsAllowed(IRunState runState)
    {
        return StoryEventReplacementConfigService.IsStoryEventAllowed(runState, "岁月史书");
    }

    /// <summary>
    /// 生成初始事件选项。
    /// </summary>
    /// <returns>事件选项列表。</returns>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, RewriteTalismans, OptionKey("REWRITE_TALISMANS")),
            new EventOption(this, RewriteMasks, OptionKey("REWRITE_MASKS")),
            new EventOption(this, TearFragment, OptionKey("TEAR_FRAGMENT")),
            new EventOption(this, Leave, OptionKey("LEAVE"))
        ];
    }

    /// <summary>
    /// 选择重写十二符咒。
    /// </summary>
    /// <returns>异步任务。</returns>
    private async Task RewriteTalismans()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        int removedCount = await HistoryBookEventHelper.RewriteTalismans(Owner);
        SetEventFinished(BuildResultDescription("REWRITE_TALISMANS", removedCount));
    }

    /// <summary>
    /// 选择重写十面具。
    /// </summary>
    /// <returns>异步任务。</returns>
    private async Task RewriteMasks()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        int removedCount = await HistoryBookEventHelper.RewriteMasks(Owner);
        SetEventFinished(BuildResultDescription("REWRITE_MASKS", removedCount));
    }

    /// <summary>
    /// 选择撕下岁月史书残卷。
    /// </summary>
    /// <returns>异步任务。</returns>
    private async Task TearFragment()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        await HistoryBookEventHelper.GiveFragment(Owner);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.TEAR_FRAGMENT.description"));
    }

    /// <summary>
    /// 直接离开事件。
    /// </summary>
    /// <returns>异步任务。</returns>
    private Task Leave()
    {
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.LEFT.description"));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 生成选项本地化键。
    /// </summary>
    /// <param name="optionName">选项名。</param>
    /// <returns>完整本地化键。</returns>
    private string OptionKey(string optionName)
    {
        return $"{Id.Entry}.pages.INITIAL.options.{optionName}";
    }

    /// <summary>
    /// 生成带被收回数量变量的结果描述。
    /// </summary>
    /// <param name="pageName">事件页面名。</param>
    /// <param name="count">被收回的种类数。</param>
    /// <returns>事件结果描述。</returns>
    private LocString BuildResultDescription(string pageName, int count)
    {
        LocString description = L10NLookup($"{Id.Entry}.pages.{pageName}.description");
        description.Add("Count", count);
        return description;
    }
}
