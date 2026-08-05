using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Events;

/// <summary>
/// 生命之杯事件：玩家在遗迹深处选择离开获得祝福，或取走杯子获得遗物和诅咒。
/// </summary>
public class LifeChaliceEvent : CustomEventModel
{
    /// <summary>
    /// 生命之杯事件使用专属事件背景图。
    /// </summary>
    public override string? CustomInitialPortraitPath => $"{MainFile.ResPath}/images/events/life_chalice_event.png";

    /// <summary>
    /// 至少有一名玩家未持有生命之杯时允许事件自然出现；实际奖励只处理事件 Owner。
    /// </summary>
    /// <param name="runState">当前爬塔状态。</param>
    /// <returns>仍有玩家可获得生命之杯时返回 true。</returns>
    public override bool IsAllowed(IRunState runState)
    {
        return runState.Players.Any(player => player.GetRelic<LifeChalice>() == null);
    }

    /// <summary>
    /// 生成事件初始选项。
    /// </summary>
    /// <returns>当前页事件选项。</returns>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, Leave, OptionKey("LEAVE")),
            new EventOption(this, TakeChalice, OptionKey("TAKE_CHALICE"), BuildTakeChaliceHoverTips())
        ];
    }

    /// <summary>
    /// 生成取走杯子选项的悬浮提示，展示将获得的遗物和两张固定诅咒。
    /// </summary>
    /// <returns>取走杯子选项悬浮提示列表。</returns>
    private static IEnumerable<IHoverTip> BuildTakeChaliceHoverTips()
    {
        return HoverTipFactory.FromRelic<LifeChalice>()
            .Concat(HoverTipFactory.FromCardWithCardHoverTips<Greed>())
            .Concat(HoverTipFactory.FromCardWithCardHoverTips<Shame>());
    }

    /// <summary>
    /// 就此离开，回复到满生命。
    /// </summary>
    private async Task Leave()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        await LifeChaliceEventHelper.LeaveAndHealToFull(Owner);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.LEFT.description"));
    }

    /// <summary>
    /// 取走杯子，获得生命之杯并加入固定本体诅咒。
    /// </summary>
    private async Task TakeChalice()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        await LifeChaliceEventHelper.TakeChalice(Owner);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.TAKE_CHALICE.description"));
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
}
