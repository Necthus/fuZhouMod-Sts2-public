using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Events;

/// <summary>
/// 远古的封印事件：让玩家以最大生命或诅咒为代价获得潘库宝盒。
/// </summary>
public class PanKuBoxEvent : CustomEventModel
{
    /// <summary>
    /// 滴血解谜需要失去的最大生命。
    /// </summary>
    private const int MaxHpLoss = 10;

    /// <summary>
    /// 接纳暗影时随机获得的诅咒数量。
    /// </summary>
    private const int RandomCurseCount = 1;

    /// <summary>
    /// 潘库宝盒事件在联机时走原版共享事件投票，保证所有玩家最终执行同一个选项。
    /// </summary>
    public override bool IsShared => true;

    /// <summary>
    /// 使用一代迁移来的潘库宝盒事件图。
    /// </summary>
    public override string? CustomInitialPortraitPath => $"{MainFile.ResPath}/images/events/panku_box.png";

    /// <summary>
    /// 遵循选人事件勾选和幕数，并要求至少一名玩家尚未持有潘库宝盒。
    /// </summary>
    /// <param name="runState">当前爬塔状态。</param>
    /// <returns>任意玩家未持有潘库宝盒时返回 true。</returns>
    public override bool IsAllowed(IRunState runState)
    {
        return StoryEventReplacementConfigService.IsStoryEventAllowed(runState, "远古封印")
            && runState.Players.Any(player => player.Relics.All(relic => relic is not PanKuBox));
    }

    /// <summary>
    /// 生成初始事件选项。
    /// </summary>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        bool canLoseMaxHp = CanAllPlayersNeedingPanKuBoxLoseMaxHp();
        return
        [
            canLoseMaxHp
                ? new EventOption(this, PayMaxHp, OptionKey("PAY_MAX_HP"))
                : new EventOption(this, null, OptionKey("PAY_MAX_HP_LOCKED")),
            new EventOption(this, GainCurses, OptionKey("GAIN_CURSES")),
            new EventOption(this, Leave, OptionKey("LEAVE"))
        ];
    }

    /// <summary>
    /// 失去最大生命获得潘库宝盒。
    /// </summary>
    private async Task PayMaxHp()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        if (!NeedsPanKuBox(Owner))
        {
            MainFile.Logger.Info("【远古的封印】玩家已持有潘库宝盒，共享事件中跳过最大生命代价。");
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.PAY_MAX_HP.description"));
            return;
        }

        await CreatureCmd.LoseMaxHp(new BlockingPlayerChoiceContext(), Owner.Creature, MaxHpLoss, false);
        await GivePanKuBox();
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.PAY_MAX_HP.description"));
    }

    /// <summary>
    /// 获得一张诅咒并获得潘库宝盒。
    /// </summary>
    private async Task GainCurses()
    {
        if (Owner == null)
        {
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.NO_OWNER.description"));
            return;
        }

        if (!NeedsPanKuBox(Owner))
        {
            MainFile.Logger.Info("【远古的封印】玩家已持有潘库宝盒，共享事件中跳过诅咒代价。");
            SetEventFinished(L10NLookup($"{Id.Entry}.pages.GAIN_CURSES.description"));
            return;
        }

        IReadOnlyList<CardModel> curses = PickRandomCurses(Owner, RandomCurseCount);
        await CardPileCmd.AddCursesToDeck(curses, Owner);
        MainFile.Logger.Info($"【远古的封印】接纳暗影：随机诅咒={string.Join(", ", curses.Select(curse => curse.Id.Entry))}。");
        await GivePanKuBox();
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.GAIN_CURSES.description"));
    }

    /// <summary>
    /// 直接离开事件。
    /// </summary>
    private Task Leave()
    {
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.LEFT.description"));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发放潘库宝盒遗物；若已持有则只记录日志。
    /// </summary>
    private async Task GivePanKuBox()
    {
        if (Owner == null || Owner.GetRelic<PanKuBox>() != null)
        {
            MainFile.Logger.Info("【远古的封印】玩家已持有潘库宝盒，跳过重复发放。");
            return;
        }

        RelicModel? panKuBox = ModelDb.AllRelics.FirstOrDefault(relic => relic is PanKuBox)?.ToMutable();
        if (panKuBox == null)
        {
            MainFile.Logger.Info("【远古的封印】未找到潘库宝盒原型，无法发放。");
            return;
        }

        await RelicCmd.Obtain(panKuBox, Owner);
        MainFile.Logger.Info("【远古的封印】获得潘库宝盒。");
    }

    /// <summary>
    /// 判断所有需要获得潘库宝盒的玩家是否都能支付最大生命，保证共享事件各端选项锁定状态一致。
    /// </summary>
    /// <returns>所有相关玩家都能支付时返回 true。</returns>
    private bool CanAllPlayersNeedingPanKuBoxLoseMaxHp()
    {
        List<Player> players = GetPlayersNeedingPanKuBox();
        return players.Count > 0 && players.All(player => player.Creature?.MaxHp > MaxHpLoss);
    }

    /// <summary>
    /// 获取当前队伍中还没有潘库宝盒的玩家。
    /// </summary>
    /// <returns>需要获得潘库宝盒的玩家列表。</returns>
    private List<Player> GetPlayersNeedingPanKuBox()
    {
        return Owner?.RunState?.Players.Where(NeedsPanKuBox).ToList() ?? [];
    }

    /// <summary>
    /// 判断玩家是否还需要获得潘库宝盒。
    /// </summary>
    /// <param name="player">待判断玩家。</param>
    /// <returns>玩家未持有潘库宝盒时返回 true。</returns>
    private static bool NeedsPanKuBox(Player? player)
    {
        return player != null && player.GetRelic<PanKuBox>() == null;
    }

    /// <summary>
    /// 从当前游戏诅咒卡池中随机抽取指定数量诅咒；候选不足时尽量抽取已有候选。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="count">需要抽取的诅咒数量。</param>
    /// <returns>被抽中的诅咒原型列表。</returns>
    private static IReadOnlyList<CardModel> PickRandomCurses(Player player, int count)
    {
        List<CardModel> candidates = ModelDb.CardPool<CurseCardPool>().AllCards.ToList();
        List<CardModel> chosenCurses = [];

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int index = player.RunState.Rng.Niche.NextInt(candidates.Count);
            chosenCurses.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        return chosenCurses;
    }

    /// <summary>
    /// 生成选项本地化键。
    /// </summary>
    /// <param name="optionName">选项名称。</param>
    /// <returns>完整本地化键。</returns>
    private string OptionKey(string optionName)
    {
        return $"{Id.Entry}.pages.INITIAL.options.{optionName}";
    }
}
