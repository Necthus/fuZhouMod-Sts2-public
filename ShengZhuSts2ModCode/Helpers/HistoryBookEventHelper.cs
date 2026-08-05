using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Services;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 岁月史书事件奖励辅助类：处理十二符咒、十面具和残卷发放。
/// </summary>
internal static class HistoryBookEventHelper
{
    /// <summary>
    /// 每种被岁月史书收回的符咒提供的最大生命。
    /// </summary>
    private const int MaxHpPerRemovedTalisman = 12;

    /// <summary>
    /// 每种被岁月史书收回的面具提供的最大生命。
    /// </summary>
    private const int MaxHpPerRemovedMaskType = 5;

    /// <summary>
    /// 十二符咒遗物类型列表。
    /// </summary>
    private static readonly Type[] TalismanTypes =
    [
        typeof(RatTalisman),
        typeof(OxTalisman),
        typeof(TigerTalisman),
        typeof(RabbitTalisman),
        typeof(DragonTalisman),
        typeof(SnakeTalisman),
        typeof(HorseTalisman),
        typeof(SheepTalisman),
        typeof(MonkeyTalisman),
        typeof(RoosterTalisman),
        typeof(DogTalisman),
        typeof(PigTalisman)
    ];

    /// <summary>
    /// 十种基础面具牌类型列表。
    /// </summary>
    private static readonly Type[] MaskCardTypes =
    [
        typeof(NiJiaMask),
        typeof(LaZuoMask),
        typeof(SaMoMask),
        typeof(BaTeMask),
        typeof(KaBoMask),
        typeof(LeiSuMask),
        typeof(ManNiMask),
        typeof(MingTaMask),
        typeof(YiKaMask),
        typeof(TaLaMask)
    ];

    /// <summary>
    /// 将玩家已有符咒交给历史，按种类获得最大生命，然后重新获得十二符咒。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>被收回的符咒种类数。</returns>
    public static async Task<int> RewriteTalismans(Player player)
    {
        List<RelicModel> ownedTalismans = player.Relics
            .Where(relic => TalismanTypes.Contains(relic.GetType()))
            .ToList();

        foreach (RelicModel relic in ownedTalismans)
        {
            await RelicCmd.Remove(relic);
            MainFile.Logger.Info($"【岁月史书】收回旧符咒：玩家={player.NetId}，符咒={relic.Id.Entry}。");
        }

        if (ownedTalismans.Count > 0)
        {
            await CreatureCmd.GainMaxHp(player.Creature, ownedTalismans.Count * MaxHpPerRemovedTalisman);
        }

        foreach (Type talismanType in TalismanTypes)
        {
            RelicModel? prototype = ModelDb.AllRelics.FirstOrDefault(relic => relic.GetType() == talismanType);
            if (prototype == null)
            {
                MainFile.Logger.Info($"【岁月史书】未找到符咒原型：类型={talismanType.Name}。");
                continue;
            }

            await RelicCmd.Obtain(prototype.ToMutable(), player);
            MainFile.Logger.Info($"【岁月史书】发放新符咒：玩家={player.NetId}，符咒={prototype.Id.Entry}。");
        }

        TalismanAwakeningHelper.CheckAndTriggerAwakening(player);
        return ownedTalismans.Count;
    }

    /// <summary>
    /// 将玩家已有面具交给历史，按种类获得最大生命，然后重新获得十张基础面具。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    /// <returns>被收回的面具种类数。</returns>
    public static async Task<int> RewriteMasks(Player player)
    {
        List<CardModel> removedMasks = [];
        foreach (Type maskType in MaskCardTypes)
        {
            CardModel? target = player.Deck.Cards
                .Where(card => card.GetType() == maskType)
                .OrderBy(card => card.IsUpgraded)
                .FirstOrDefault();

            if (target == null)
            {
                continue;
            }

            DeckMutationService.RemoveCardFromDeck(player, target, false, "岁月史书收回旧面具");
            removedMasks.Add(target);
            MainFile.Logger.Info($"【岁月史书】收回旧面具：玩家={player.NetId}，面具={target.Id.Entry}，升级={target.IsUpgraded}。");
        }

        if (removedMasks.Count > 0)
        {
            await CreatureCmd.GainMaxHp(player.Creature, removedMasks.Count * MaxHpPerRemovedMaskType);
        }

        EndlessDarknessHelper.BeginDelayedAwakeningCheck(player);
        try
        {
            foreach (Type maskType in MaskCardTypes)
            {
                CardModel? prototype = ModelDb.AllCards.FirstOrDefault(card => card.GetType() == maskType);
                if (prototype == null)
                {
                    MainFile.Logger.Info($"【岁月史书】未找到面具原型：类型={maskType.Name}。");
                    continue;
                }

                CardModel card = (CardModel)prototype.ToMutable();
                DeckMutationService.AddCardToDeck(player, card, false, "岁月史书发放十面具");
                MainFile.Logger.Info($"【岁月史书】发放新面具：玩家={player.NetId}，面具={card.Id.Entry}。");
            }
        }
        finally
        {
            EndlessDarknessHelper.EndDelayedAwakeningCheck(player);
        }

        EndlessDarknessHelper.CheckAndTriggerAwakening(player);
        return removedMasks.Count;
    }

    /// <summary>
    /// 发放岁月史书残卷。
    /// </summary>
    /// <param name="player">目标玩家。</param>
    public static async Task GiveFragment(Player player)
    {
        if (player.GetRelic<HistoryBookFragment>() != null)
        {
            MainFile.Logger.Info($"【岁月史书】玩家已拥有残卷，跳过重复发放：玩家={player.NetId}。");
            return;
        }

        RelicModel? fragment = ModelDb.AllRelics.FirstOrDefault(relic => relic is HistoryBookFragment)?.ToMutable();
        if (fragment == null)
        {
            MainFile.Logger.Info("【岁月史书】未找到岁月史书残卷原型，无法发放。");
            return;
        }

        await RelicCmd.Obtain(fragment, player);
        MainFile.Logger.Info($"【岁月史书】发放岁月史书残卷：玩家={player.NetId}。");
    }
}
