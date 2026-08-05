using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 恶魔小龙事件辅助类：集中处理合作遗物发放和一代兜底特殊奖励。
/// </summary>
public static class DragoEventHelper
{
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
    /// 判断玩家是否已经持有合作遗物。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>已持有合作遗物时返回 true。</returns>
    public static bool HasCollaboration(Player? player)
    {
        return player?.Relics.Any(relic => relic is CollaborationRelic) == true;
    }

    /// <summary>
    /// 一代事件接受合作：未持有合作时获得合作；已持有时走特殊奖励兜底。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    public static async Task AcceptCollaboration(Player player)
    {
        if (!HasCollaboration(player))
        {
            RelicModel? collaboration = ModelDb.AllRelics.FirstOrDefault(relic => relic is CollaborationRelic)?.ToMutable();
            if (collaboration != null)
            {
                await RelicCmd.Obtain(collaboration, player);
                MainFile.Logger.Info("【恶魔小龙事件】获得合作遗物。");
            }
            return;
        }

        await GiveSpecialReward(player);
    }

    /// <summary>
    /// 发放一代保留的特殊奖励：优先随机未拥有符咒；符咒全齐后按 50/35/15 给本体普通遗物。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    private static async Task GiveSpecialReward(Player player)
    {
        RelicModel? missingTalisman = CreateRandomMissingTalisman(player);
        if (missingTalisman != null)
        {
            await RelicCmd.Obtain(missingTalisman, player);
            MainFile.Logger.Info($"【恶魔小龙事件】合作已拥有，改为发放未拥有符咒：{missingTalisman.Id.Entry}。");
            return;
        }

        RelicModel? baseRelic = CreateRandomBaseGameRelic(player);
        if (baseRelic != null)
        {
            await RelicCmd.Obtain(baseRelic, player);
            MainFile.Logger.Info($"【恶魔小龙事件】符咒全齐，改为发放本体遗物：{baseRelic.Id.Entry}。");
        }
    }

    /// <summary>
    /// 创建随机未拥有符咒遗物。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>可发放符咒；没有时返回 null。</returns>
    private static RelicModel? CreateRandomMissingTalisman(Player player)
    {
        List<Type> missingTypes = TalismanTypes
            .Where(talismanType => !player.Relics.Any(relic => relic.GetType() == talismanType))
            .ToList();
        if (missingTypes.Count == 0)
        {
            return null;
        }

        Type chosenType = StableRandomHelper.PickByStableHash(
            player,
            missingTypes,
            "DragoEventHelper.MissingTalisman",
            talismanType => talismanType.FullName ?? talismanType.Name) ?? missingTypes[0];
        return ModelDb.AllRelics.FirstOrDefault(relic => relic.GetType() == chosenType)?.ToMutable();
    }

    /// <summary>
    /// 按一代权重创建一个本体普通遗物。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>可发放本体遗物；没有时返回 null。</returns>
    private static RelicModel? CreateRandomBaseGameRelic(Player player)
    {
        RelicRarity firstRarity = RollBaseRelicRarity(player);
        foreach (RelicRarity rarity in OrderedRarities(firstRarity))
        {
            List<RelicModel> candidates = ModelDb.AllRelics
                .Where(relic => relic.GetType().Namespace == "MegaCrit.Sts2.Core.Models.Relics")
                .Where(relic => relic.Rarity == rarity)
                .Where(relic => !player.Relics.Any(owned => owned.Id == relic.Id))
                .Where(relic => relic.IsAllowed(player.RunState))
                .ToList();
            if (candidates.Count > 0)
            {
                return StableRandomHelper.PickByStableHash(
                    player,
                    candidates,
                    "DragoEventHelper.BaseGameRelic",
                    relic => relic.Id.Entry,
                    rarity.ToString())?.ToMutable();
            }
        }

        return null;
    }

    /// <summary>
    /// 一代特殊奖励的本体遗物稀有度权重：普通50%，罕见35%，稀有15%。
    /// </summary>
    /// <returns>本次优先稀有度。</returns>
    private static RelicRarity RollBaseRelicRarity(Player player)
    {
        int roll = StableRandomHelper.StableIndex(player, 100, "DragoEventHelper.BaseRelicRarity");
        if (roll < 50)
        {
            return RelicRarity.Common;
        }

        return roll < 85 ? RelicRarity.Uncommon : RelicRarity.Rare;
    }

    /// <summary>
    /// 按首选稀有度开始，依次尝试其余常规稀有度。
    /// </summary>
    /// <param name="firstRarity">首选稀有度。</param>
    /// <returns>尝试顺序。</returns>
    private static IEnumerable<RelicRarity> OrderedRarities(RelicRarity firstRarity)
    {
        yield return firstRarity;

        foreach (RelicRarity rarity in new[] { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare })
        {
            if (rarity != firstRarity)
            {
                yield return rarity;
            }
        }
    }
}
