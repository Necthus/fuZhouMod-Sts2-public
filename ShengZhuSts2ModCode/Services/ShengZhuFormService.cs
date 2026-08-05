using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using System.Runtime.CompilerServices;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

/// <summary>
/// 圣主形态服务：统一管理持有鼠符咒时的人形态、石像形态、圣主石像能力和形态切换限制。
/// </summary>
public static class ShengZhuFormService
{
    /// <summary>
    /// 已执行过战斗开局形态初始化的玩家记录，避免联机多名圣主时同一场战斗重复初始化。
    /// </summary>
    private static readonly HashSet<string> CombatStartFormAppliedKeys = [];

    /// <summary>
    /// 判断玩家是否是可以切换形态的圣主。
    /// </summary>
    /// <param name="player">待检查玩家。</param>
    /// <returns>玩家是圣主且持有鼠符咒时返回 true。</returns>
    public static bool CanUseFormSwitch(Player? player)
    {
        return player?.Character is ShengZhu && player.GetRelic<RatTalisman>() != null;
    }

    /// <summary>
    /// 判断玩家是否处于鼠符咒觉醒形态。
    /// 非圣主角色保持旧逻辑：只要持有鼠符咒，就继续享受鼠符咒共鸣。
    /// </summary>
    /// <param name="player">待检查玩家。</param>
    /// <returns>当前应享受鼠符咒共鸣/觉醒时返回 true。</returns>
    public static bool HasAwakenedRatForm(Player? player)
    {
        RatTalisman? rat = player?.GetRelic<RatTalisman>();
        if (rat == null)
        {
            return false;
        }

        return player?.Character is not ShengZhu || rat.IsHumanForm;
    }

    /// <summary>
    /// 判断圣主当前是否显示人形态。
    /// </summary>
    /// <param name="player">待检查玩家。</param>
    /// <returns>圣主持有鼠符咒且当前为人形态时返回 true。</returns>
    public static bool IsHumanForm(Player? player)
    {
        return player?.Character is ShengZhu && player.GetRelic<RatTalisman>()?.IsHumanForm == true;
    }

    /// <summary>
    /// 按当前鼠符咒规则初始化战斗开局状态。
    /// </summary>
    /// <param name="player">圣主玩家。</param>
    /// <returns>异步任务。</returns>
    public static async Task ApplyCombatStartForm(Player player)
    {
        if (player.Character is not ShengZhu)
        {
            return;
        }

        if (!TryMarkCombatStartFormApplied(player))
        {
            MainFile.Logger.Info($"【联机同步】【圣主形态】战斗开局形态初始化跳过：玩家={player.NetId}，原因=本场战斗已初始化过。");
            return;
        }

        RatTalisman? rat = player.GetRelic<RatTalisman>();
        LogCombatStartFormState("初始化前", player, rat);
        if (rat == null)
        {
            await ApplyStatueForm(player, null, true);
            await SyncRatResonanceIndicatorPower(player);
            LogCombatStartFormState("初始化后", player, rat);
            return;
        }

        rat.ResetFormStateForNewCombat();
        await RemoveStatueForm(player, rat);
        await SyncRatResonanceIndicatorPower(player);
        MainFile.Logger.Info($"【圣主形态】战斗开始：玩家={player.NetId}，持有鼠符咒，固定以人形态进入战斗。");
        ShengZhuTrueFormVisualService.RefreshEverywhere(player);
        ShengZhuFormSwitchButtonService.RefreshAll();
        LogCombatStartFormState("初始化后", player, rat);
    }

    /// <summary>
    /// 请求当前玩家切换到另一种形态。
    /// </summary>
    /// <param name="player">发起切换的玩家。</param>
    /// <returns>异步任务。</returns>
    public static async Task RequestSwitchForPlayer(Player player)
    {
        RatTalisman? rat = player.GetRelic<RatTalisman>();
        if (rat == null)
        {
            MainFile.Logger.Info($"【圣主形态】请求切换失败：玩家={player.NetId} 没有鼠符咒。");
            return;
        }

        await Networking.RelicInteractionSyncService.RequestRatFormSwitch(rat, !rat.IsHumanForm);
    }

    /// <summary>
    /// 执行联机同步后的形态切换。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="targetHumanForm">目标是否为人形态。</param>
    /// <param name="choiceContext">玩家选择上下文。</param>
    /// <returns>异步任务。</returns>
    public static async Task RunSyncedSwitch(Player player, bool targetHumanForm, PlayerChoiceContext choiceContext)
    {
        RatTalisman? rat = player.GetRelic<RatTalisman>();
        if (player.Character is not ShengZhu || rat == null)
        {
            MainFile.Logger.Info($"【圣主形态】同步切换跳过：玩家={player.NetId} 不是持有鼠符咒的圣主。");
            return;
        }

        if (rat.HasSwitchedFormThisTurn)
        {
            MainFile.Logger.Info($"【圣主形态】同步切换跳过：玩家={player.NetId} 本回合已经切换过形态。");
            ShengZhuFormSwitchButtonService.RefreshAll();
            return;
        }

        if (rat.IsHumanForm == targetHumanForm)
        {
            MainFile.Logger.Info($"【圣主形态】同步切换跳过：玩家={player.NetId} 已经处于目标形态，目标人形={targetHumanForm}。");
            ShengZhuFormSwitchButtonService.RefreshAll();
            return;
        }

        bool oldHumanForm = rat.IsHumanForm;
        if (targetHumanForm)
        {
            await RemoveStatueForm(player, rat);
            rat.SetHumanForm(true);
        }
        else
        {
            rat.SetHumanForm(false);
            await ApplyStatueForm(player, rat, false);
        }

        await SyncRatResonanceIndicatorPower(player);
        rat.MarkFormSwitchedThisTurn();
        MainFile.Logger.Info($"【联机同步】【圣主形态】玩家={player.NetId}，动作=切换形态，旧形态={(oldHumanForm ? "人形" : "石像")}，新形态={(targetHumanForm ? "人形" : "石像")}，本回合已用切换=是。");
        ShengZhuTrueFormVisualService.RefreshEverywhere(player);
        ShengZhuFormSwitchButtonService.RefreshAll();
    }

    /// <summary>
    /// 鼠符咒被获得后，圣主默认进入人形态。
    /// </summary>
    /// <param name="player">获得鼠符咒的玩家。</param>
    /// <param name="rat">鼠符咒实例。</param>
    /// <returns>异步任务。</returns>
    public static async Task ApplyRatObtainedForm(Player player, RatTalisman rat)
    {
        if (player.Character is not ShengZhu)
        {
            return;
        }

        await RemoveStatueForm(player, rat);
        rat.SetHumanForm(true);
        rat.ResetFormSwitchForTurn();
        await SyncRatResonanceIndicatorPower(player);
        MainFile.Logger.Info($"【圣主形态】获得鼠符咒：玩家={player.NetId} 默认进入人形态。");
        ShengZhuTrueFormVisualService.RefreshEverywhere(player);
        ShengZhuFormSwitchButtonService.RefreshAll();
    }

    /// <summary>
    /// 鼠符咒被移除后，圣主失去主动切换资格并回到石像外观。
    /// </summary>
    /// <param name="player">失去鼠符咒的玩家。</param>
    /// <returns>异步任务。</returns>
    public static async Task ApplyRatRemovedForm(Player player)
    {
        if (player.Character is not ShengZhu)
        {
            return;
        }

        await ApplyStatueForm(player, null, false);
        await SyncRatResonanceIndicatorPower(player);
        MainFile.Logger.Info($"【圣主形态】失去鼠符咒：玩家={player.NetId} 回到石像形态。");
        ShengZhuTrueFormVisualService.RefreshEverywhere(player);
        ShengZhuFormSwitchButtonService.RefreshAll();
    }

    /// <summary>
    /// 重置玩家本回合形态切换次数。
    /// </summary>
    /// <param name="player">当前回合开始的玩家。</param>
    public static void ResetTurnSwitch(Player player)
    {
        RatTalisman? rat = player.GetRelic<RatTalisman>();
        if (rat == null)
        {
            return;
        }

        rat.ResetFormSwitchForTurn();
        ShengZhuFormSwitchButtonService.RefreshAll();
        ShengZhuFormSwitchButtonService.RefreshAllAfterProcessFrame();
    }

    /// <summary>
    /// 同步虎鼠共鸣指示器能力：人形态显示，石像形态或缺少虎符咒时移除，避免能力栏误导玩家。
    /// </summary>
    /// <param name="player">需要同步的玩家。</param>
    /// <returns>异步任务。</returns>
    public static async Task SyncRatResonanceIndicatorPower(Player? player)
    {
        if (player?.Creature == null)
        {
            return;
        }

        TigerRatResonanceIndicatorPower? indicator = player.Creature.GetPower<TigerRatResonanceIndicatorPower>();
        bool shouldHaveIndicator = HasAwakenedRatForm(player) && player.GetRelic<TigerTalisman>() != null;
        if (shouldHaveIndicator)
        {
            if (indicator == null)
            {
                await PowerCmd.Apply<TigerRatResonanceIndicatorPower>(player.Creature, 1, player.Creature, null);
                MainFile.Logger.Info($"【圣主形态】同步虎鼠共鸣指示器：玩家={player.NetId}，当前人形态且持有虎符咒，已显示指示器。");
            }

            return;
        }

        if (indicator != null)
        {
            await PowerCmd.Remove(indicator);
            MainFile.Logger.Info($"【圣主形态】同步虎鼠共鸣指示器：玩家={player.NetId}，当前不享受鼠符咒共鸣，已移除指示器。");
        }
    }

    /// <summary>
    /// 给玩家施加圣主石像形态。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="rat">鼠符咒实例；没有鼠符咒时传 null。</param>
    /// <param name="applyTurnStartEffects">是否立刻执行一次石像回合开始效果。</param>
    /// <returns>异步任务。</returns>
    private static async Task ApplyStatueForm(Player player, RatTalisman? rat, bool applyTurnStartEffects)
    {
        if (player.Creature == null)
        {
            return;
        }

        bool alreadyHadStatue = player.Creature.HasPower<ShenZhuStatuePower>();
        int statueThorns = rat?.SavedStatueThornsAmount ?? ShenZhuStatuePower.InitialThorns;
        statueThorns = Math.Max(ShenZhuStatuePower.InitialThorns, statueThorns);

        ShenZhuStatuePower? statuePower = player.Creature.GetPower<ShenZhuStatuePower>();
        if (statuePower == null)
        {
            statuePower = await PowerCmd.Apply<ShenZhuStatuePower>(player.Creature, 1, player.Creature, null);
        }

        if (!alreadyHadStatue)
        {
            await PowerCmd.Apply<ThornsPower>(player.Creature, statueThorns, player.Creature, null);
            MainFile.Logger.Info($"【圣主形态】施加石像形态：玩家={player.NetId}，恢复石像来源荆棘={statueThorns}。");
        }

        if (statuePower != null)
        {
            if (!alreadyHadStatue || rat != null)
            {
                statuePower.RestoreStatueState(statueThorns, rat?.SavedStatueTurnCount ?? 0);
            }

            if (applyTurnStartEffects)
            {
                await statuePower.ApplyPlayerTurnStartEffects();
            }
        }
    }

    /// <summary>
    /// 移除玩家的圣主石像形态，并只扣掉石像来源的荆棘。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="rat">鼠符咒实例，用于保存石像来源状态。</param>
    /// <returns>异步任务。</returns>
    private static async Task RemoveStatueForm(Player player, RatTalisman rat)
    {
        if (player.Creature == null)
        {
            return;
        }

        ShenZhuStatuePower? statuePower = player.Creature.GetPower<ShenZhuStatuePower>();
        if (statuePower == null)
        {
            return;
        }

        int statueThornsAmount = Math.Max(0, statuePower.StatueThornsAmount);
        rat.SaveStatueState(statueThornsAmount, statuePower.StatueTurnCount);
        await RemoveStatueThorns(player, statueThornsAmount);
        await PowerCmd.Remove(statuePower);
        MainFile.Logger.Info($"【圣主形态】移除石像形态：玩家={player.NetId}，保存石像来源荆棘={statueThornsAmount}，石像计数={statuePower.StatueTurnCount}。");
    }

    /// <summary>
    /// 只扣除来自圣主石像的荆棘层数，保留其他来源的荆棘。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <param name="statueThornsAmount">石像来源荆棘层数。</param>
    /// <returns>异步任务。</returns>
    private static async Task RemoveStatueThorns(Player player, int statueThornsAmount)
    {
        if (statueThornsAmount <= 0 || player.Creature == null)
        {
            return;
        }

        ThornsPower? thornsPower = player.Creature.GetPower<ThornsPower>();
        if (thornsPower == null || thornsPower.Amount <= 0)
        {
            return;
        }

        int removeAmount = Math.Min(statueThornsAmount, (int)thornsPower.Amount);
        if (removeAmount <= 0)
        {
            return;
        }

        if (thornsPower.Amount <= removeAmount)
        {
            await PowerCmd.Remove(thornsPower);
        }
        else
        {
            await PowerCmd.ModifyAmount(thornsPower, -removeAmount, player.Creature, null);
        }

        MainFile.Logger.Info($"【圣主形态】扣除石像来源荆棘：玩家={player.NetId}，扣除={removeAmount}，扣除前总荆棘={thornsPower.Amount}。");
    }

    /// <summary>
    /// 标记玩家本场战斗已经执行过圣主形态初始化。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>本次是首次初始化时返回 true。</returns>
    private static bool TryMarkCombatStartFormApplied(Player player)
    {
        string key = BuildCombatStartFormKey(player);
        if (CombatStartFormAppliedKeys.Count > 256)
        {
            CombatStartFormAppliedKeys.Clear();
        }

        return CombatStartFormAppliedKeys.Add(key);
    }

    /// <summary>
    /// 生成战斗开局形态初始化去重键。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>同一场战斗同一名玩家稳定相同的键。</returns>
    private static string BuildCombatStartFormKey(Player player)
    {
        object? combatState = player.Creature?.CombatState;
        if (combatState != null)
        {
            return $"{RuntimeHelpers.GetHashCode(combatState):X8}:{player.NetId}";
        }

        string seed = player.RunState?.Rng.StringSeed ?? "无种子";
        string act = player.RunState?.CurrentActIndex.ToString() ?? "无章节";
        string floor = player.RunState?.TotalFloor.ToString() ?? "无楼层";
        return $"{seed}:{act}:{floor}:{player.NetId}";
    }

    /// <summary>
    /// 打印战斗开局形态初始化前后的关键状态。
    /// </summary>
    /// <param name="stage">日志阶段。</param>
    /// <param name="player">目标玩家。</param>
    /// <param name="rat">鼠符咒实例。</param>
    private static void LogCombatStartFormState(string stage, Player player, RatTalisman? rat)
    {
        bool hasStatue = player.Creature?.HasPower<ShenZhuStatuePower>() == true;
        decimal thorns = player.Creature?.GetPower<ThornsPower>()?.Amount ?? 0;
        decimal block = player.Creature?.Block ?? 0;
        string currentForm = rat == null ? "无鼠符咒" : (rat.IsHumanForm ? "人形" : "石像");
        MainFile.Logger.Info($"【联机同步】【圣主形态】战斗开局形态{stage}：玩家={player.NetId}，当前形态={currentForm}，石像Power={hasStatue}，荆棘={thorns}，护甲={block}。");
    }
}
