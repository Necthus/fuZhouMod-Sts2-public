using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 生命之杯事件辅助类：集中处理事件选项的治疗、遗物发放和固定诅咒代价。
/// </summary>
public static class LifeChaliceEventHelper
{
    /// <summary>
    /// 选择离开时回复到满生命，不触发生命之杯的溢出转最大生命规则。
    /// </summary>
    /// <param name="player">当前事件玩家。</param>
    public static async Task LeaveAndHealToFull(Player player)
    {
        if (player.Creature == null)
        {
            MainFile.Logger.Info("【生命之杯事件】离开治疗失败：玩家没有有效生物对象。");
            return;
        }

        if (player.Creature.IsDead)
        {
            MainFile.Logger.Info($"【生命之杯事件】离开治疗跳过：玩家={player.NetId}，玩家已死亡。");
            return;
        }

        int missingHp = Math.Max(0, player.Creature.MaxHp - player.Creature.CurrentHp);
        if (missingHp > 0)
        {
            await CreatureCmd.Heal(player.Creature, missingHp);
        }

        MainFile.Logger.Info($"【生命之杯事件】选择离开：玩家={player.NetId}，回复={missingHp}，结果={player.Creature.CurrentHp}/{player.Creature.MaxHp}。");
    }

    /// <summary>
    /// 选择取走杯子时获得生命之杯，并加入本体固定诅咒贪婪和羞耻。
    /// </summary>
    /// <param name="player">当前事件玩家。</param>
    public static async Task TakeChalice(Player player)
    {
        if (player.GetRelic<LifeChalice>() != null)
        {
            MainFile.Logger.Info($"【生命之杯事件】玩家已持有生命之杯，跳过重复发放和诅咒代价：玩家={player.NetId}。");
            return;
        }

        RelicModel? lifeChalice = ModelDb.AllRelics.FirstOrDefault(relic => relic is LifeChalice)?.ToMutable();
        if (lifeChalice == null)
        {
            MainFile.Logger.Info($"【生命之杯事件】未找到生命之杯原型，无法发放：玩家={player.NetId}。");
            return;
        }

        await RelicCmd.Obtain(lifeChalice, player);
        await AddFixedBaseGameCurses(player);
        MainFile.Logger.Info($"【生命之杯事件】取走杯子完成：玩家={player.NetId}，获得生命之杯，并加入贪婪和羞耻。");
    }

    /// <summary>
    /// 加入本体固定诅咒贪婪和羞耻，不从当前 Mod 自定义诅咒中随机选择。
    /// </summary>
    /// <param name="player">当前事件玩家。</param>
    private static async Task AddFixedBaseGameCurses(Player player)
    {
        CardModel[] curses =
        [
            ModelDb.Card<Greed>(),
            ModelDb.Card<Shame>()
        ];

        await CardPileCmd.AddCursesToDeck(curses, player);
        MainFile.Logger.Info($"【生命之杯事件】已加入固定本体诅咒：玩家={player.NetId}，诅咒={string.Join(", ", curses.Select(curse => curse.Id.Entry))}。");
    }
}
