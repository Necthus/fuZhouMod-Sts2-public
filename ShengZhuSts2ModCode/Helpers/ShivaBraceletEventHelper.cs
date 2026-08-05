using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 西瓦手镯事件辅助类：集中处理事件选项的实际奖励和代价。
/// </summary>
public static class ShivaBraceletEventHelper
{
    /// <summary>
    /// 协助阿福需要失去的当前生命值。
    /// </summary>
    public const int AhFuHealthLoss = 10;

    /// <summary>
    /// 协助成龙获得的最大生命值。
    /// </summary>
    private const int JackieMaxHpGain = 10;

    /// <summary>
    /// 协助阿福获得手镯。生命值不足时按危险选项直接死亡，不再发放遗物。
    /// </summary>
    /// <param name="player">当前事件玩家。</param>
    public static async Task AssistAhFu(Player player)
    {
        if (player.Creature == null)
        {
            MainFile.Logger.Info("【西瓦手镯事件】协助阿福失败：玩家没有有效生物对象。");
            return;
        }

        if (player.Creature.CurrentHp <= AhFuHealthLoss)
        {
            MainFile.Logger.Info($"【西瓦手镯事件】协助阿福触发致死代价：玩家={player.NetId}，当前生命={player.Creature.CurrentHp}，代价={AhFuHealthLoss}。");
            await CreatureCmd.Kill(player.Creature, true);
            return;
        }

        await CreatureCmd.Damage(
            new BlockingPlayerChoiceContext(),
            player.Creature,
            AhFuHealthLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            player.Creature,
            null);

        await GiveShivaBracelet(player);
        MainFile.Logger.Info($"【西瓦手镯事件】协助阿福完成：玩家={player.NetId}，失去生命={AhFuHealthLoss}，获得西瓦手镯。");
    }

    /// <summary>
    /// 协助成龙击退阿福，获得最大生命值。
    /// </summary>
    /// <param name="player">当前事件玩家。</param>
    public static async Task AssistJackie(Player player)
    {
        if (player.Creature == null)
        {
            MainFile.Logger.Info("【西瓦手镯事件】协助成龙失败：玩家没有有效生物对象。");
            return;
        }

        await CreatureCmd.GainMaxHp(player.Creature, JackieMaxHpGain);
        MainFile.Logger.Info($"【西瓦手镯事件】协助成龙完成：玩家={player.NetId}，最大生命+{JackieMaxHpGain}。");
    }

    /// <summary>
    /// 发放西瓦手镯遗物；如果已经持有则跳过重复发放。
    /// </summary>
    /// <param name="player">当前事件玩家。</param>
    private static async Task GiveShivaBracelet(Player player)
    {
        if (player.GetRelic<ShivaBracelet>() != null)
        {
            MainFile.Logger.Info($"【西瓦手镯事件】玩家已持有西瓦手镯，跳过重复发放：玩家={player.NetId}。");
            return;
        }

        RelicModel? shivaBracelet = ModelDb.AllRelics.FirstOrDefault(relic => relic is ShivaBracelet)?.ToMutable();
        if (shivaBracelet == null)
        {
            MainFile.Logger.Info("【西瓦手镯事件】未找到西瓦手镯原型，无法发放。");
            return;
        }

        await RelicCmd.Obtain(shivaBracelet, player);
    }
}
