using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Networking;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Patches;

/// <summary>
/// Harmony Patch：在遗物UI收到鼠标输入时，直接拦截符咒遗物的右键点击。
/// 用源头输入拦截替代信号连接，避免遗物节点信号没有正确分发导致右键失效。
/// </summary>
[HarmonyPatch(typeof(NClickableControl), nameof(NClickableControl._GuiInput))]
public static class RelicRightClickPatch
{
    /// <summary>
    /// RelicModel 内部用于通知遗物计数器刷新的受保护方法。
    /// 右键符咒会改内部状态，主动调用它可以让UI立刻显示最新状态。
    /// </summary>
    private static readonly MethodInfo? InvokeDisplayAmountChangedMethod =
        typeof(RelicModel).GetMethod("InvokeDisplayAmountChanged", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 当前正在处理右键逻辑的遗物，防止同一遗物连续右键重复进入异步流程。
    /// </summary>
    private static readonly ConditionalWeakTable<RelicModel, ProcessingMarker> ProcessingRelics = new();

    /// <summary>
    /// 在按钮控件处理鼠标输入后，判断当前控件是否为遗物格子，并处理符咒右键逻辑。
    /// </summary>
    /// <param name="__instance">收到输入的按钮控件。</param>
    /// <param name="inputEvent">Godot输入事件。</param>
    public static void Postfix(NClickableControl __instance, InputEvent inputEvent)
    {
        if (__instance is not NRelicInventoryHolder holder
            || inputEvent is not InputEventMouseButton mouseButton
            || mouseButton.ButtonIndex != MouseButton.Right
            || !mouseButton.Pressed)
        {
            return;
        }

        RelicModel? relic = holder.Relic?.Model;
        if (relic == null)
        {
            MainFile.Logger.Info("【符咒右键】捕获到遗物格子右键，但遗物模型为空。");
            return;
        }

        MainFile.Logger.Info($"【符咒右键】捕获遗物右键：遗物={relic.Id.Entry}，类型={relic.GetType().Name}");

        if (relic is not IRelicRightClickable && relic is not ITalismanCooldownResettable)
        {
            MainFile.Logger.Info($"【遗物右键】遗物不是可右键遗物，已忽略：遗物={relic.Id.Entry}");
            return;
        }

        if (holder.Inventory == null)
        {
            MainFile.Logger.Info($"【符咒右键】遗物栏实例为空，无法处理右键：遗物={relic.Id.Entry}");
            return;
        }

        if (!CanHandleLocalRightClick(relic))
        {
            return;
        }

        if (ProcessingRelics.TryGetValue(relic, out _))
        {
            MainFile.Logger.Info($"【符咒右键】遗物右键正在处理中，忽略重复点击：遗物={relic.Id.Entry}");
            return;
        }

        ProcessingRelics.Add(relic, new ProcessingMarker());
        _ = HandleRightClickAsync(holder.Inventory, relic);
    }

    /// <summary>
    /// 处理右键点击逻辑：
    /// 1. 如果兔符咒处于刷新模式，且点击的是其他冷却中的符咒，则刷新该符咒CD。
    /// 2. 否则，正常触发该遗物自身的右键效果。
    /// </summary>
    private static async Task HandleRightClickAsync(NRelicInventory inventory, RelicModel clickedRelic)
    {
        try
        {
            RabbitTalisman? rabbit = inventory.RelicNodes
                .Select(n => n.Relic.Model)
                .OfType<RabbitTalisman>()
                .FirstOrDefault(r => r.IsInRefreshMode);

            MainFile.Logger.Info($"【符咒右键】开始处理：目标={clickedRelic.Id.Entry}，兔符咒刷新模式={(rabbit == null ? "未开启" : "已开启")}");

            if (rabbit != null && clickedRelic != rabbit && clickedRelic is ITalismanCooldownResettable { IsOnCooldown: true })
            {
                MainFile.Logger.Info($"【符咒右键】尝试使用兔符咒刷新目标冷却：目标={clickedRelic.Id.Entry}");
                if (await RelicInteractionSyncService.RequestRabbitRefreshTarget(rabbit, clickedRelic))
                {
                    NotifyDisplayAmountChanged(clickedRelic);
                    NotifyDisplayAmountChanged(rabbit);
                    MainFile.Logger.Info($"【符咒右键】兔符咒刷新请求已受理：目标={clickedRelic.Id.Entry}");
                    LogTalismanInteractionState(clickedRelic, "兔符咒刷新请求后");
                    LogTalismanInteractionState(rabbit, "兔符咒刷新请求后");
                    return;
                }

                MainFile.Logger.Info($"【符咒右键】兔符咒刷新未生效，将继续尝试目标自身右键效果：目标={clickedRelic.Id.Entry}");
            }
            else if (rabbit != null && clickedRelic != rabbit)
            {
                MainFile.Logger.Info($"【符咒右键】兔符咒刷新模式已开启，但目标不在冷却中，继续执行目标自身右键：目标={clickedRelic.Id.Entry}");
            }

            if (clickedRelic is IRelicRightClickable clickable)
            {
                MainFile.Logger.Info($"【符咒右键】提交遗物自身右键请求：目标={clickedRelic.Id.Entry}，类型={clickedRelic.GetType().Name}");
                await clickable.OnRightClick(new BlockingPlayerChoiceContext());
                NotifyDisplayAmountChanged(clickedRelic);
                LogTalismanInteractionState(clickedRelic, "右键请求后");
                MainFile.Logger.Info($"【符咒右键】遗物自身右键请求已提交：目标={clickedRelic.Id.Entry}");
                return;
            }

            MainFile.Logger.Info($"【符咒右键】目标不可直接右键，且兔符咒未刷新成功：目标={clickedRelic.Id.Entry}");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"【符咒右键】处理右键异常：目标={clickedRelic.Id.Entry}，异常={ex}");
        }
        finally
        {
            ProcessingRelics.Remove(clickedRelic);
            MainFile.Logger.Info($"【符咒右键】处理结束，已释放右键锁：目标={clickedRelic.Id.Entry}");
        }
    }

    /// <summary>
    /// 通知遗物计数器刷新，方便右键激发、取消激发、刷新冷却后立刻看到UI变化。
    /// </summary>
    private static void NotifyDisplayAmountChanged(RelicModel relic)
    {
        if (InvokeDisplayAmountChangedMethod == null)
        {
            MainFile.Logger.Info($"【符咒右键】无法找到计数器刷新方法：目标={relic.Id.Entry}");
            return;
        }

        InvokeDisplayAmountChangedMethod.Invoke(relic, null);
        MainFile.Logger.Info($"【符咒右键】已通知计数器刷新：目标={relic.Id.Entry}，显示值={relic.DisplayAmount}");
    }

    /// <summary>
    /// 判断本端是否允许处理该遗物右键，联机时不能本地改队友遗物状态。
    /// </summary>
    private static bool CanHandleLocalRightClick(RelicModel relic)
    {
        RunManager runManager = RunManager.Instance;
        if (runManager.NetService.Type == NetGameType.Singleplayer)
        {
            return true;
        }

        if (relic.Owner == null)
        {
            MainFile.Logger.Info($"【符咒右键】联机时遗物持有者为空，已跳过：遗物={relic.Id.Entry}。");
            return false;
        }

        if (runManager.NetService.NetId == relic.Owner.NetId)
        {
            return true;
        }

        MainFile.Logger.Info($"【符咒右键】联机时跳过非本端玩家遗物：遗物={relic.Id.Entry}，持有者={relic.Owner.NetId}，本端={runManager.NetService.NetId}。");
        return false;
    }

    /// <summary>
    /// 打印符咒交互后的激发与冷却状态，方便排查右键是否真正改变了符咒状态。
    /// </summary>
    private static void LogTalismanInteractionState(RelicModel relic, string actionName)
    {
        if (relic is not ITalismanInteractionState state)
        {
            MainFile.Logger.Info($"【符咒右键】{actionName}状态：目标={relic.Id.Entry}，该遗物未提供符咒交互状态。");
            return;
        }

        MainFile.Logger.Info($"【符咒右键】{actionName}状态：目标={relic.Id.Entry}，是否激活={(state.IsActivated ? "是" : "否")}，是否冷却={(state.CooldownTurnsRemaining > 0 ? "是" : "否")}，冷却回合={state.CooldownTurnsRemaining}");
    }

    /// <summary>
    /// ConditionalWeakTable 使用的空标记对象。
    /// </summary>
    private sealed class ProcessingMarker
    {
    }
}
