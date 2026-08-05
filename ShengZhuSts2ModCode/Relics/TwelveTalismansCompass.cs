using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Rewards;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

/// <summary>
/// 十二符咒罗盘：符咒探测仪的古之民上位遗物。
/// 保留探测仪的挡位切换和抢夺符咒事件风险，并增强遗物奖励里的符咒选择权。
/// </summary>
public class TwelveTalismansCompass : TalismanLocator
{
    /// <summary>
    /// 最大生命补偿奖励数值。
    /// </summary>
    private const int MaxHpRewardAmount = 5;

    /// <summary>
    /// 每次最多展示的缺失符咒候选数量。
    /// </summary>
    private const int MaxTalismanOptionCount = 3;

    /// <summary>
    /// 所有十二符咒遗物类型列表。
    /// </summary>
    private static readonly Type[] AllTalismanTypes =
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
    /// 遗物奖励在展示阶段已经生成了内部遗物，但公开的 ClaimedRelic 只有领取后才有值。
    /// </summary>
    private static readonly FieldInfo? RelicRewardRelicField = typeof(RelicReward).GetField("_relic", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 遗物稀有度：古之民上位遗物。
    /// </summary>
    public override RelicRarity Rarity => RelicRarity.Ancient;

    /// <summary>
    /// 奖励阶段前置改写：尝试把普通遗物奖励改造成罗盘的互斥多选奖励。
    /// </summary>
    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        return TryInjectCompassChoice(player, rewards, room);
    }

    /// <summary>
    /// 奖励阶段后置改写：再次兜底尝试注入罗盘的互斥多选奖励。
    /// </summary>
    public override bool TryModifyRewardsLate(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        return TryInjectCompassChoice(player, rewards, room);
    }

    /// <summary>
    /// 向奖励中额外注入“最多三个缺失符咒，不足则用最大生命补偿”的三选一。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="rewards">当前奖励列表。</param>
    /// <param name="room">当前房间。</param>
    /// <returns>成功注入时返回 true。</returns>
    private bool TryInjectCompassChoice(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player == null || rewards == null)
        {
            return false;
        }

        if (Owner == null || player.NetId != Owner.NetId)
        {
            MainFile.Logger.Info($"【十二符咒罗盘】跳过非持有者奖励改写：奖励玩家={player.NetId}，持有者={Owner?.NetId.ToString() ?? "空"}。");
            return false;
        }

        if (room is not CombatRoom combatRoom)
        {
            MainFile.Logger.Info($"【十二符咒罗盘】当前奖励不是战斗结算，跳过符咒多选：房间={room?.GetType().Name ?? "空"}");
            return false;
        }

        if (combatRoom.ParentEventId != null)
        {
            MainFile.Logger.Info($"【十二符咒罗盘】当前战斗来自事件，跳过符咒多选：事件={combatRoom.ParentEventId}");
            return false;
        }

        if (ContainsCompassChoice(rewards))
        {
            return false;
        }

        if (ShouldOfferBossTalismanReward(player, combatRoom))
        {
            return TryInjectBossCompassReward(player, rewards, room);
        }

        if (rewards.Count == 0)
        {
            return false;
        }

        int relicRewardIndex = FindFirstNormalRelicRewardIndex(rewards);
        if (relicRewardIndex < 0)
        {
            MainFile.Logger.Info("【十二符咒罗盘】本次战斗结算没有可替换的普通遗物奖励。");
            return false;
        }

        if (rewards[relicRewardIndex] is not RelicReward normalRelicReward)
        {
            return false;
        }

        RelicModel? normalRewardRelic = GetRewardRelic(normalRelicReward);
        if (normalRewardRelic == null)
        {
            MainFile.Logger.Info("【十二符咒罗盘】无法读取原本遗物奖励，跳过符咒多选。");
            return false;
        }

        List<RelicModel> talismanOptions = GetStableMissingTalismans(player, normalRewardRelic, room, MaxTalismanOptionCount);
        List<Reward> linkedRewards = [];

        foreach (RelicModel talismanOption in talismanOptions)
        {
            linkedRewards.Add(new RelicReward(talismanOption, player));
        }

        while (linkedRewards.Count < MaxTalismanOptionCount)
        {
            linkedRewards.Add(new MaxHpReward(MaxHpRewardAmount, player));
        }

        LinkedRewardSet linkedRewardSet = new(linkedRewards, player);
        rewards.Insert(relicRewardIndex + 1, linkedRewardSet);
        MainFile.Logger.Info($"【十二符咒罗盘】已在战斗结算遗物奖励后追加三选一罗盘奖励：原遗物={normalRewardRelic.Id.Entry}，符咒候选数={talismanOptions.Count}，最大生命补位数={MaxTalismanOptionCount - talismanOptions.Count}，已拥有符咒数={CountOwnedTalismans(player)}");
        Flash();
        return true;
    }

    /// <summary>
    /// 向 Boss 战结算中追加罗盘三选一奖励：最多三个缺失符咒，不足则用最大生命补偿。
    /// </summary>
    /// <param name="player">当前奖励玩家。</param>
    /// <param name="rewards">当前奖励列表。</param>
    /// <param name="room">当前房间。</param>
    /// <returns>成功追加时返回 true。</returns>
    private bool TryInjectBossCompassReward(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        string rewardContext = GetBossTalismanRewardContext(player, room);
        List<RelicModel> talismanOptions = GetStableMissingTalismans(player, rewardContext, room, MaxTalismanOptionCount);
        List<Reward> linkedRewards = [];

        foreach (RelicModel talismanOption in talismanOptions)
        {
            linkedRewards.Add(new RelicReward(talismanOption, player));
        }

        while (linkedRewards.Count < MaxTalismanOptionCount)
        {
            linkedRewards.Add(new MaxHpReward(MaxHpRewardAmount, player));
        }

        LinkedRewardSet linkedRewardSet = new(linkedRewards, player);
        rewards.Add(linkedRewardSet);
        MainFile.Logger.Info($"【十二符咒罗盘】Boss战后已追加三选一罗盘奖励：玩家={player.NetId}，符咒候选数={talismanOptions.Count}，候选ID=[{string.Join(",", talismanOptions.Select(talisman => talisman.Id.Entry))}]，最大生命补位数={MaxTalismanOptionCount - talismanOptions.Count}，已拥有符咒数={CountOwnedTalismans(player)}，上下文={rewardContext}。");
        Flash();
        return true;
    }

    /// <summary>
    /// 获取稳定排序后的缺失符咒候选。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="normalRewardRelic">本次原本掉落的遗物。</param>
    /// <param name="room">当前房间。</param>
    /// <param name="maxCount">最多返回数量。</param>
    /// <returns>稳定符咒候选列表。</returns>
    private static List<RelicModel> GetStableMissingTalismans(Player player, RelicModel normalRewardRelic, AbstractRoom? room, int maxCount)
    {
        return GetStableMissingTalismans(player, normalRewardRelic.Id.ToString(), room, maxCount);
    }

    /// <summary>
    /// 获取稳定排序后的缺失符咒候选。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="rewardContext">奖励上下文；Boss奖励没有原遗物时使用固定上下文。</param>
    /// <param name="room">当前房间。</param>
    /// <param name="maxCount">最多返回数量。</param>
    /// <returns>稳定符咒候选列表。</returns>
    private static List<RelicModel> GetStableMissingTalismans(Player player, string rewardContext, AbstractRoom? room, int maxCount)
    {
        return GetMissingTalismanTypes(player)
            .OrderBy(talismanType => GetStableTalismanScore(player, rewardContext, room, talismanType))
            .Take(maxCount)
            .Select(CreateTalisman)
            .Where(talisman => talisman != null)
            .Cast<RelicModel>()
            .ToList();
    }

    /// <summary>
    /// 获取玩家未拥有的符咒类型。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>缺失符咒类型列表。</returns>
    private static List<Type> GetMissingTalismanTypes(Player player)
    {
        return AllTalismanTypes
            .Where(talismanType => !player.Relics.Any(relic => relic.GetType() == talismanType))
            .ToList();
    }

    /// <summary>
    /// 为一个缺失符咒计算稳定分数，分数越小越靠前。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="rewardContext">奖励上下文。</param>
    /// <param name="room">当前房间。</param>
    /// <param name="talismanType">候选符咒类型。</param>
    /// <returns>稳定哈希分数。</returns>
    private static uint GetStableTalismanScore(Player player, string rewardContext, AbstractRoom? room, Type talismanType)
    {
        uint hash = 2166136261u;
        AddStableHash(ref hash, player.RunState.Rng.StringSeed);
        AddStableHash(ref hash, player.NetId.ToString());
        AddStableHash(ref hash, player.RunState.CurrentActIndex.ToString());
        AddStableHash(ref hash, player.RunState.ActFloor.ToString());
        AddStableHash(ref hash, player.RunState.TotalFloor.ToString());
        AddStableHash(ref hash, player.RunState.CurrentMapCoord?.col.ToString() ?? "NO_COL");
        AddStableHash(ref hash, player.RunState.CurrentMapCoord?.row.ToString() ?? "NO_ROW");
        AddStableHash(ref hash, room?.RoomType.ToString() ?? "NO_ROOM");
        AddStableHash(ref hash, rewardContext);
        AddStableHash(ref hash, talismanType.FullName ?? talismanType.Name);
        return hash;
    }

    /// <summary>
    /// 使用固定 FNV-1a 算法追加字符串，避免 .NET 字符串哈希在不同进程中变化。
    /// </summary>
    /// <param name="hash">当前哈希值。</param>
    /// <param name="value">要追加的文本。</param>
    private static void AddStableHash(ref uint hash, string value)
    {
        foreach (char character in value)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        hash ^= '|';
        hash *= 16777619u;
    }

    /// <summary>
    /// 按符咒类型创建可发放的遗物实例。
    /// </summary>
    /// <param name="talismanType">符咒类型。</param>
    /// <returns>可发放的遗物实例；找不到原型时返回 null。</returns>
    private static RelicModel? CreateTalisman(Type talismanType)
    {
        return ModelDb.AllRelics.FirstOrDefault(relic => relic.GetType() == talismanType)?.ToMutable();
    }

    /// <summary>
    /// 统计玩家已拥有的符咒数量。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>已拥有符咒数量。</returns>
    private static int CountOwnedTalismans(Player player)
    {
        return AllTalismanTypes.Count(talismanType => player.Relics.Any(relic => relic.GetType() == talismanType));
    }

    /// <summary>
    /// 查找第一个可替换的普通遗物奖励位置。
    /// </summary>
    /// <param name="rewards">当前奖励列表。</param>
    /// <returns>奖励下标；找不到时返回 -1。</returns>
    private static int FindFirstNormalRelicRewardIndex(IReadOnlyList<Reward> rewards)
    {
        for (int i = 0; i < rewards.Count; i++)
        {
            if (rewards[i] is not RelicReward relicReward)
            {
                continue;
            }

            RelicModel? rewardRelic = GetRewardRelic(relicReward);
            if (rewardRelic != null && !IsTalismanRelic(rewardRelic))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 判断奖励列表中是否已经包含罗盘互斥选择，避免前后置奖励钩子重复注入。
    /// </summary>
    /// <param name="rewards">当前奖励列表。</param>
    /// <returns>已经包含罗盘选择时返回 true。</returns>
    private static bool ContainsCompassChoice(IEnumerable<Reward> rewards)
    {
        foreach (Reward reward in rewards)
        {
            if (reward is MaxHpReward)
            {
                return true;
            }

            if (reward is RelicReward relicReward && IsTalismanRelic(GetRewardRelic(relicReward)))
            {
                return true;
            }

            if (reward is LinkedRewardSet linkedRewardSet)
            {
                foreach (Reward childReward in linkedRewardSet.Rewards)
                {
                    if (childReward is MaxHpReward)
                    {
                        return true;
                    }

                    if (childReward is RelicReward linkedRelicReward && IsTalismanRelic(GetRewardRelic(linkedRelicReward)))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 获取遗物奖励当前展示的遗物。
    /// </summary>
    /// <param name="relicReward">遗物奖励。</param>
    /// <returns>展示中的遗物。</returns>
    private static RelicModel? GetRewardRelic(RelicReward relicReward)
    {
        return RelicRewardRelicField?.GetValue(relicReward) as RelicModel;
    }

    /// <summary>
    /// 判断指定遗物是否为十二符咒之一。
    /// </summary>
    /// <param name="relicModel">待判断遗物。</param>
    /// <returns>是符咒遗物时返回 true。</returns>
    private static bool IsTalismanRelic(RelicModel? relicModel)
    {
        if (relicModel == null)
        {
            return false;
        }

        return AllTalismanTypes.Contains(relicModel.GetType());
    }
}
