using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Events;

/// <summary>
/// 西瓦手镯事件：玩家在印度神庙废墟中选择协助阿福或成龙。
/// </summary>
public class ShivaBraceletEvent : CustomEventModel
{
    /// <summary>
    /// 事件使用西瓦手镯专属事件图。
    /// </summary>
    public override string? CustomInitialPortraitPath => $"{MainFile.ResPath}/images/events/shiva_bracelet_event.png";

    /// <summary>
    /// 事件全角色可见。
    /// </summary>
    /// <param name="runState">当前爬塔状态。</param>
    /// <returns>始终返回 true。</returns>
    public override bool IsAllowed(IRunState runState)
    {
        return true;
    }

    /// <summary>
    /// 生成事件初始选项。
    /// </summary>
    /// <returns>当前页事件选项。</returns>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, AssistAhFu, OptionKey("ASSIST_AH_FU"))
                .ThatWillKillPlayerIf(player => player.Creature.CurrentHp <= ShivaBraceletEventHelper.AhFuHealthLoss),
            new EventOption(this, AssistJackie, OptionKey("ASSIST_JACKIE"))
        ];
    }

    /// <summary>
    /// 协助阿福获得手镯：失去生命并获得西瓦手镯。
    /// </summary>
    private async Task AssistAhFu()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        await ShivaBraceletEventHelper.AssistAhFu(Owner);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.ASSIST_AH_FU.description"));
    }

    /// <summary>
    /// 协助成龙击退阿福：获得最大生命值。
    /// </summary>
    private async Task AssistJackie()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        await ShivaBraceletEventHelper.AssistJackie(Owner);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.ASSIST_JACKIE.description"));
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
