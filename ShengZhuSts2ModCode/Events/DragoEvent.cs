using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Events;

/// <summary>
/// 恶魔小龙事件：让未合作的圣主选择是否与恶魔小龙合作。
/// </summary>
public class DragoEvent : CustomEventModel
{
    /// <summary>
    /// 恶魔小龙事件在联机时走共享事件投票，保证接受合作和拒绝确认不会各选各的。
    /// </summary>
    public override bool IsShared => true;

    /// <summary>
    /// 使用一代迁移来的事件图。
    /// </summary>
    public override string? CustomInitialPortraitPath => $"{MainFile.ResPath}/images/events/shengzhusts2mod-drago_event.png";

    /// <summary>
    /// 事件只允许圣主且尚未持有合作遗物时进入普通事件池。
    /// </summary>
    /// <param name="runState">当前爬塔状态。</param>
    /// <returns>允许进入事件池时返回 true。</returns>
    public override bool IsAllowed(IRunState runState)
    {
        return StoryEventReplacementConfigService.IsStoryEventAllowed(runState, "恶魔小龙")
            && runState.Players.Any(player => player.Character is ShengZhu && !DragoEventHelper.HasCollaboration(player));
    }

    /// <summary>
    /// 初始页：接受合作或拒绝一次。
    /// </summary>
    /// <returns>当前页事件选项。</returns>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new EventOption(this, AcceptCollaboration, OptionKey("INITIAL", "ACCEPT")),
            new EventOption(this, ShowDeclineConfirm, OptionKey("INITIAL", "DECLINE"))
        ];
    }

    /// <summary>
    /// 接受合作，获得合作遗物并结束事件。
    /// </summary>
    private async Task AcceptCollaboration()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        if (!IsDragoParticipant(Owner))
        {
            MainFile.Logger.Info($"【恶魔小龙事件】共享事件接受合作跳过：玩家={Owner.NetId}/{Owner.Character.Id.Entry}，原因=不是未合作的圣主。");
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.ACCEPTED.description"));
            return;
        }

        await DragoEventHelper.AcceptCollaboration(Owner);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.ACCEPTED.description"));
    }

    /// <summary>
    /// 第一次拒绝后进入一代确认页：老爹警告玩家别乱拒绝。
    /// </summary>
    private Task ShowDeclineConfirm()
    {
        SetEventState(
            L10NLookup($"{Id.Entry}.pages.CONFIRM.description"),
            [
                new EventOption(this, ReturnToInitial, OptionKey("CONFIRM", "RECONSIDER")),
                // 标记为必定杀死玩家，让游戏原生事件 UI 按危险选项渲染。
                new EventOption(this, DieAfterDecline, OptionKey("CONFIRM", "DECLINE_AGAIN")).ThatWillKillPlayerIf(_ => true)
            ]);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 回到初始页继续选择。
    /// </summary>
    private Task ReturnToInitial()
    {
        SetEventState(
            L10NLookup($"{Id.Entry}.pages.INITIAL.description"),
            GenerateInitialOptions());
        return Task.CompletedTask;
    }

    /// <summary>
    /// 第二次拒绝按一代逻辑直接死亡。
    /// </summary>
    private async Task DieAfterDecline()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        if (!IsDragoParticipant(Owner))
        {
            MainFile.Logger.Info($"【恶魔小龙事件】共享事件第二次拒绝跳过：玩家={Owner.NetId}/{Owner.Character.Id.Entry}，原因=不是未合作的圣主。");
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.DEAD.description"));
            return;
        }

        MainFile.Logger.Info("【恶魔小龙事件】玩家第二次拒绝合作，按一代逻辑直接死亡。");
        await CreatureCmd.Kill(Owner.Creature, true);
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.DEAD.description"));
    }

    /// <summary>
    /// 生成选项本地化键。
    /// </summary>
    /// <param name="pageName">页面名。</param>
    /// <param name="optionName">选项名。</param>
    /// <returns>完整本地化键。</returns>
    private string OptionKey(string pageName, string optionName)
    {
        return $"{Id.Entry}.pages.{pageName}.options.{optionName}";
    }

    /// <summary>
    /// 判断玩家是否参与恶魔小龙事件的个人结算。
    /// </summary>
    /// <param name="player">待判断玩家。</param>
    /// <returns>未持有合作遗物的圣主玩家返回 true。</returns>
    private static bool IsDragoParticipant(Player player)
    {
        return player.Character is ShengZhu && !DragoEventHelper.HasCollaboration(player);
    }
}
