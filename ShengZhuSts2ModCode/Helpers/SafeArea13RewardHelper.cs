using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Masks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 最"安全"的十三区奖励辅助类：集中处理缺失符咒、缺失面具、药水栏、本体药水和本体遗物奖励。
/// </summary>
public static class SafeArea13RewardHelper
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
    /// 判断玩家是否还有未拥有的符咒。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>存在未拥有符咒时返回 true。</returns>
    public static bool HasMissingTalisman(Player? player)
    {
        return GetMissingTalismanTypes(player).Count > 0;
    }

    /// <summary>
    /// 判断玩家是否还有未拥有的面具；若已有无尽黑暗，则按一代逻辑视为不缺面具。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>存在未拥有面具时返回 true。</returns>
    public static bool HasMissingMask(Player? player)
    {
        return GetMissingMaskTypes(player).Count > 0;
    }

    /// <summary>
    /// 发放一个未拥有符咒，并尝试发放一瓶本体随机药水。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    public static async Task GiveMissingTalismanAndPotion(Player player)
    {
        RelicModel? talisman = CreateRandomMissingTalisman(player);
        if (talisman != null)
        {
            await RelicCmd.Obtain(talisman, player);
            MainFile.Logger.Info($"【最安全的十三区】发放符咒：{talisman.Id.Entry}");
            TalismanAwakeningHelper.CheckAndTriggerAwakening(player);
        }
        else
        {
            MainFile.Logger.Info("【最安全的十三区】没有可发放的缺失符咒。");
        }

        await GiveRandomBaseGamePotion(player);
    }

    /// <summary>
    /// 发放一个未拥有面具，药水栏增加 2，并尝试发放一瓶本体随机药水。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    public static async Task GiveMissingMaskExpandAndPotion(Player player)
    {
        CardModel? maskCard = CreateRandomMissingMask(player);
        if (maskCard != null)
        {
            await CardPileCmd.Add(maskCard, PileType.Deck);
            MainFile.Logger.Info($"【最安全的十三区】发放面具：{maskCard.Id.Entry}");
            EndlessDarknessHelper.CheckAndTriggerAwakening(player);
        }
        else
        {
            MainFile.Logger.Info("【最安全的十三区】没有可发放的缺失面具。");
        }

        player.AddToMaxPotionCount(2);
        MainFile.Logger.Info("【最安全的十三区】药水栏位增加：2");
        await GiveRandomBaseGamePotion(player);
    }

    /// <summary>
    /// 增加玩家最大生命值。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="amount">增加数量。</param>
    public static async Task GainMaxHp(Player player, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        await CreatureCmd.GainMaxHp(player.Creature, amount);
        MainFile.Logger.Info($"【最安全的十三区】最大生命值增加：{amount}");
    }

    /// <summary>
    /// 发放指定数量的本体非 Boss 遗物。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="count">发放数量。</param>
    public static async Task GiveBaseGameRelics(Player player, int count)
    {
        for (int i = 0; i < count; i++)
        {
            RelicModel? relic = PullRandomBaseGameRelic(player);
            if (relic == null)
            {
                MainFile.Logger.Info("【最安全的十三区】没有可发放的本体普通遗物。");
                return;
            }

            await RelicCmd.Obtain(relic, player);
            MainFile.Logger.Info($"【最安全的十三区】发放本体遗物：{relic.Id.Entry}");
        }
    }

    /// <summary>
    /// 获取玩家未拥有的符咒类型。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>未拥有符咒类型列表。</returns>
    private static List<Type> GetMissingTalismanTypes(Player? player)
    {
        if (player == null)
        {
            return [];
        }

        return TalismanTypes
            .Where(talismanType => !player.Relics.Any(relic => relic.GetType() == talismanType))
            .ToList();
    }

    /// <summary>
    /// 获取玩家未拥有的面具类型。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>未拥有面具类型列表。</returns>
    private static List<Type> GetMissingMaskTypes(Player? player)
    {
        if (player?.Deck?.Cards == null)
        {
            return [];
        }

        if (player.Deck.Cards.Any(card => card is EndlessDarkness))
        {
            return [];
        }

        return MaskCardTypes
            .Where(maskType => !player.Deck.Cards.Any(card => card.GetType() == maskType))
            .ToList();
    }

    /// <summary>
    /// 创建一个随机未拥有符咒遗物。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>可发放的符咒遗物；没有时返回 null。</returns>
    private static RelicModel? CreateRandomMissingTalisman(Player player)
    {
        List<Type> missingTypes = GetMissingTalismanTypes(player);
        if (missingTypes.Count == 0)
        {
            return null;
        }

        Type chosenType = StableRandomHelper.PickByStableHash(
            player,
            missingTypes,
            "SafeArea13RewardHelper.MissingTalisman",
            talismanType => talismanType.FullName ?? talismanType.Name) ?? missingTypes[0];
        return ModelDb.AllRelics.FirstOrDefault(relic => relic.GetType() == chosenType)?.ToMutable();
    }

    /// <summary>
    /// 创建一个随机未拥有面具牌。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>可加入牌组的面具牌；没有时返回 null。</returns>
    private static CardModel? CreateRandomMissingMask(Player player)
    {
        List<Type> missingTypes = GetMissingMaskTypes(player);
        if (missingTypes.Count == 0)
        {
            return null;
        }

        Type chosenType = StableRandomHelper.PickByStableHash(
            player,
            missingTypes,
            "SafeArea13RewardHelper.MissingMask",
            maskType => maskType.FullName ?? maskType.Name) ?? missingTypes[0];
        CardModel? prototype = ModelDb.AllCards.FirstOrDefault(card => card.GetType() == chosenType);
        return prototype == null ? null : player.RunState.CreateCard(prototype, player);
    }

    /// <summary>
    /// 尝试发放一瓶本体随机药水。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    private static async Task GiveRandomBaseGamePotion(Player player)
    {
        PotionModel? potion = CreateRandomBaseGamePotion(player);
        if (potion == null)
        {
            MainFile.Logger.Info("【最安全的十三区】没有可发放的本体药水。");
            return;
        }

        PotionProcureResult result = await PotionCmd.TryToProcure(potion, player);
        if (result.success)
        {
            MainFile.Logger.Info($"【最安全的十三区】发放本体药水：{potion.Id.Entry}");
        }
        else
        {
            MainFile.Logger.Info($"【最安全的十三区】本体药水发放失败：{potion.Id.Entry}，原因={result.failureReason}");
        }
    }

    /// <summary>
    /// 创建一瓶随机本体药水，排除废弃池、事件池和令牌池。
    /// </summary>
    /// <returns>可发放的药水；没有时返回 null。</returns>
    private static PotionModel? CreateRandomBaseGamePotion(Player player)
    {
        List<PotionModel> candidates = ModelDb.AllPotions
            .Where(IsBaseGamePotion)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        PotionModel chosen = StableRandomHelper.PickByStableHash(
            player,
            candidates,
            "SafeArea13RewardHelper.BaseGamePotion",
            potion => potion.Id.Entry) ?? candidates[0];
        return chosen.ToMutable();
    }

    /// <summary>
    /// 从玩家遗物抓取袋中抽取一个本体非 Boss 遗物。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>可发放的遗物；没有时返回 null。</returns>
    private static RelicModel? PullRandomBaseGameRelic(Player player)
    {
        if (player.RunState is RunState runState)
        {
            player.PopulateRelicGrabBagIfNecessary(runState.Rng.UpFront);
        }

        RelicRarity firstRarity = RollBaseRelicRarity(player);
        foreach (RelicRarity rarity in OrderedRarities(firstRarity))
        {
            RelicModel? pulled = player.RelicGrabBag.PullFromFront(rarity, IsBaseGameNormalRelic, player.RunState);
            if (pulled != null)
            {
                return pulled.ToMutable();
            }
        }

        return CreateFallbackBaseGameRelic(player);
    }

    /// <summary>
    /// 当遗物抓取袋不可用时，从本体遗物原型中兜底创建一个非 Boss 遗物。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>可发放的遗物；没有时返回 null。</returns>
    private static RelicModel? CreateFallbackBaseGameRelic(Player player)
    {
        List<RelicModel> candidates = ModelDb.AllRelics
            .Where(IsBaseGameNormalRelic)
            .Where(relic => !player.Relics.Any(owned => owned.Id == relic.Id))
            .Where(relic => relic.IsAllowed(player.RunState))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        RelicModel chosen = StableRandomHelper.PickByStableHash(
            player,
            candidates,
            "SafeArea13RewardHelper.FallbackBaseGameRelic",
            relic => relic.Id.Entry) ?? candidates[0];
        return chosen.ToMutable();
    }

    /// <summary>
    /// 按一代十三区事件权重投出本体遗物稀有度：普通 50%，罕见 35%，稀有 15%。
    /// </summary>
    /// <returns>本次优先尝试的遗物稀有度。</returns>
    private static RelicRarity RollBaseRelicRarity(Player player)
    {
        int roll = StableRandomHelper.StableIndex(player, 100, "SafeArea13RewardHelper.BaseRelicRarity");
        if (roll < 50)
        {
            return RelicRarity.Common;
        }

        if (roll < 85)
        {
            return RelicRarity.Uncommon;
        }

        return RelicRarity.Rare;
    }

    /// <summary>
    /// 按一代权重选中第一稀有度后，依次尝试剩余普通稀有度。
    /// </summary>
    /// <param name="firstRarity">第一尝试稀有度。</param>
    /// <returns>稀有度尝试顺序。</returns>
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

    /// <summary>
    /// 判断药水是否属于本体常规药水。
    /// </summary>
    /// <param name="potion">待判断药水。</param>
    /// <returns>属于本体常规药水时返回 true。</returns>
    private static bool IsBaseGamePotion(PotionModel potion)
    {
        string? namespaceName = potion.GetType().Namespace;
        string poolName = potion.Pool.GetType().Name;

        return namespaceName == "MegaCrit.Sts2.Core.Models.Potions"
               && poolName is not "DeprecatedPotionPool"
               && poolName is not "EventPotionPool"
               && poolName is not "TokenPotionPool";
    }

    /// <summary>
    /// 判断遗物是否属于本体普通、罕见或稀有遗物。
    /// </summary>
    /// <param name="relic">待判断遗物。</param>
    /// <returns>属于本体非 Boss 遗物时返回 true。</returns>
    private static bool IsBaseGameNormalRelic(RelicModel relic)
    {
        return relic.GetType().Namespace == "MegaCrit.Sts2.Core.Models.Relics"
               && relic.Rarity is RelicRarity.Common or RelicRarity.Uncommon or RelicRarity.Rare;
    }
}
