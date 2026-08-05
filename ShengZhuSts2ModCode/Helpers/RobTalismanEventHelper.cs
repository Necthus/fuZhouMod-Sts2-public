using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Visuals;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;

/// <summary>
/// 抢夺符咒事件辅助类：集中处理事件触发判定、事件代价、随机诅咒和精英战斗入口。
/// </summary>
public static class RobTalismanEventHelper
{
    /// <summary>
    /// 抢夺符咒事件每个已拥有符咒带来的事件替换概率。
    /// </summary>
    public const float ChancePerTalisman = 0.03f;

    /// <summary>
    /// 大撒币逃走选项需要的最低金币数。
    /// </summary>
    public const int GoldEscapeThreshold = 200;

    /// <summary>
    /// 硬扛伤害时受到的伤害值。
    /// </summary>
    public const int EndureDamageAmount = 10;

    /// <summary>
    /// 硬扛伤害时随机获得的诅咒数量。
    /// </summary>
    public const int RandomCurseCount = 2;

    /// <summary>
    /// 抢夺符咒精英强化的基础生命值增量。完整公式：20 + 进阶数 * 5 + 人数 * 20。
    /// </summary>
    public const int RobTalismanEliteBaseHpBonus = 20;

    /// <summary>
    /// 抢夺符咒精英强化中每 1 进阶提供的生命值增量。完整公式：20 + 进阶数 * 5 + 人数 * 20。
    /// </summary>
    public const int RobTalismanEliteAscensionHpBonus = 5;

    /// <summary>
    /// 抢夺符咒精英强化中每 1 名玩家提供的生命值增量。完整公式：20 + 进阶数 * 5 + 人数 * 20。
    /// </summary>
    public const int RobTalismanElitePlayerHpBonus = 20;

    /// <summary>
    /// 抢夺符咒精英强化固定增加的力量值。
    /// </summary>
    public const int RobTalismanEliteStrengthBonus = 2;

    /// <summary>
    /// 所有十二符咒遗物类型列表。
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
    /// 统计玩家当前拥有的符咒数量。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>已拥有符咒数量。</returns>
    public static int CountOwnedTalismans(Player? player)
    {
        if (player == null)
        {
            return 0;
        }

        return TalismanTypes.Count(talismanType => player.Relics.Any(relic => relic.GetType() == talismanType));
    }

    /// <summary>
    /// 判断玩家牌组中是否有冒牌符咒。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>存在冒牌符咒时返回 true。</returns>
    public static bool HasFakeTalisman(Player? player)
    {
        return FindFakeTalisman(player) != null;
    }

    /// <summary>
    /// 交出一张冒牌符咒：优先移除未升级版本，保留玩家更好的牌。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    public static async Task RemoveOneFakeTalisman(Player player)
    {
        CardModel? fakeTalisman = FindFakeTalisman(player);
        if (fakeTalisman == null)
        {
            MainFile.Logger.Info("【抢夺符咒】未找到可交出的冒牌符咒。");
            return;
        }

        await CardPileCmd.RemoveFromDeck(fakeTalisman);
        MainFile.Logger.Info($"【抢夺符咒】已交出冒牌符咒：升级={fakeTalisman.IsUpgraded}");
    }

    /// <summary>
    /// 大撒币逃走：失去全部金币并获得 1 张悔恨。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    public static async Task LoseAllGoldAndGainRegret(Player player)
    {
        int gold = player.Gold;
        await PlayerCmd.LoseGold(gold, player);
        await CardPileCmd.AddCurseToDeck<MegaCrit.Sts2.Core.Models.Cards.Regret>(player);
        MainFile.Logger.Info($"【抢夺符咒】大撒币逃走：失去金币={gold}，获得悔恨。");
    }

    /// <summary>
    /// 随机失去一个已拥有符咒。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    public static async Task RemoveRandomOwnedTalisman(Player player)
    {
        List<RelicModel> ownedTalismans = player.Relics
            .Where(relic => TalismanTypes.Contains(relic.GetType()))
            .ToList();

        if (ownedTalismans.Count == 0)
        {
            MainFile.Logger.Info("【抢夺符咒】没有可失去的符咒。");
            return;
        }

        RelicModel talisman = StableRandomHelper.PickByStableHash(
            player,
            ownedTalismans,
            "RobTalismanEventHelper.RemoveRandomOwnedTalisman",
            relic => relic.Id.Entry) ?? ownedTalismans[0];
        await TalismanLossVisualHelper.PlayAsync(talisman);
        await RelicCmd.Remove(talisman);
        MainFile.Logger.Info($"【抢夺符咒】随机失去符咒：玩家={player.NetId}，候选=[{string.Join(",", ownedTalismans.Select(relic => relic.Id.Entry))}]，选中={talisman.Id.Entry}，随机源=StableRandomHelper。");
    }

    /// <summary>
    /// 硬扛伤害：受到 10 点伤害，并从当前游戏诅咒池随机获得 2 张诅咒。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    public static async Task TakeDamageAndGainRandomCurses(Player player)
    {
        await CreatureCmd.Damage(
            new BlockingPlayerChoiceContext(),
            player.Creature,
            EndureDamageAmount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);

        IReadOnlyList<CardModel> curses = PickRandomCurses(player, RandomCurseCount);
        await CardPileCmd.AddCursesToDeck(curses, player);
        MainFile.Logger.Info($"【抢夺符咒】硬扛伤害：受到伤害={EndureDamageAmount}，随机诅咒={string.Join(", ", curses.Select(curse => curse.Id.Entry))}");
    }

    /// <summary>
    /// 进入当前可遇到范围内的随机精英战斗，战斗正常结算精英奖励。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>成功进入战斗时返回 true。</returns>
    public static async Task<bool> StartRandomEliteCombat(Player player)
    {
        if (!SharedEventVoteHelper.ShouldExecuteLocalGlobalAction(player, "抢夺符咒"))
        {
            return true;
        }

        Player canonicalPlayer = SharedEventVoteHelper.GetCanonicalPlayer(player) ?? player;
        List<EncounterModel> eliteEncounters = GetAvailableEliteEncounters(canonicalPlayer);
        if (eliteEncounters.Count == 0)
        {
            MainFile.Logger.Info("【抢夺符咒】当前可遇到章节范围内没有可用精英战斗。");
            return false;
        }

        EncounterModel? selectedEncounter = StableRandomHelper.PickByStableHash(
            canonicalPlayer,
            eliteEncounters,
            "RobTalismanEventHelper.RandomEliteCombat",
            encounter => encounter.Id.Entry);
        if (selectedEncounter == null)
        {
            MainFile.Logger.Info($"【抢夺符咒】稳定随机未选出精英战：候选数量={eliteEncounters.Count}。");
            return false;
        }

        EncounterModel encounter = selectedEncounter.ToMutable();
        CombatRoom combatRoom = new(encounter, player.RunState)
        {
            ShouldResumeParentEventAfterCombat = false
        };

        RecordCombatReplayInitialStateIfNeeded();
        MainFile.Logger.Info($"【联机同步】来源=抢夺符咒，玩家={player.NetId}/{player.Character.Id.Entry}，动作=进入随机精英战，章节范围=1-{player.RunState.CurrentActIndex + 1}，候选数={eliteEncounters.Count}，候选ID=[{string.Join(",", eliteEncounters.Select(elite => elite.Id.Entry))}]，选中={encounter.Id.Entry}，随机源=StableRandomHelper，战后恢复父事件=false。");
        await RunManager.Instance.EnterRoomWithoutExitingCurrentRoom(combatRoom, fadeToBlack: true);
        await ApplyRobTalismanEliteBuff(combatRoom, player);
        return true;
    }

    /// <summary>
    /// 给抢夺符咒事件【开战吧】生成的出场精英怪添加专属强化，后续召唤出的爪牙不会被处理。
    /// </summary>
    /// <param name="combatRoom">刚进入的精英战斗房间。</param>
    /// <param name="player">触发事件的玩家。</param>
    private static async Task ApplyRobTalismanEliteBuff(CombatRoom combatRoom, Player player)
    {
        if (combatRoom?.CombatState == null || player?.Creature == null)
        {
            MainFile.Logger.Info("【抢夺符咒】精英强化失败：战斗房间或玩家为空。");
            return;
        }

        int totalHpBonus = CalculateRobTalismanEliteHpBonus(player);
        int ascensionLevel = Math.Max(0, player.RunState.AscensionLevel);
        int playerCount = Math.Max(1, player.RunState.Players.Count);
        List<Creature> targets = GetRobTalismanEliteTargets(combatRoom);
        MainFile.Logger.Info($"【抢夺符咒】精英强化计算：公式=20 + 进阶数({ascensionLevel}) * 5 + 人数({playerCount}) * 20，总生命增加={totalHpBonus}，总力量增加={RobTalismanEliteStrengthBonus}，目标数量={targets.Count}。");

        for (int i = 0; i < targets.Count; i++)
        {
            Creature target = targets[i];
            int hpBonus = CalculateEvenlyDistributedEliteBonus(totalHpBonus, targets.Count, i);
            int strengthBonus = CalculateEvenlyDistributedEliteBonus(RobTalismanEliteStrengthBonus, targets.Count, i);
            int oldMaxHp = target.MaxHp;
            int oldCurrentHp = target.CurrentHp;
            if (hpBonus > 0)
            {
                await CreatureCmd.GainMaxHp(target, hpBonus);
            }
            int newMaxHp = target.MaxHp;
            int newCurrentHp = target.CurrentHp;
            if (strengthBonus > 0)
            {
                await PowerCmd.Apply<StrengthPower>(target, strengthBonus, player.Creature, null);
            }
            RobTalismanElitePower? power = await PowerCmd.Apply<RobTalismanElitePower>(target, hpBonus, player.Creature, null);
            power?.SetFormulaDetails(ascensionLevel, playerCount, strengthBonus, totalHpBonus);
            MainFile.Logger.Info($"【抢夺符咒】已强化出场精英：目标={target.Name}，序号={i + 1}/{targets.Count}，最大生命={oldMaxHp}+{hpBonus}=>{newMaxHp}，当前生命={oldCurrentHp}+{hpBonus}=>{newCurrentHp}，力量增加={strengthBonus}。");
        }
    }

    /// <summary>
    /// 将精英强化总数值按目标数量均分；不能整除的余数按出场顺序前置分配，保证总数不丢。
    /// </summary>
    /// <param name="totalBonus">需要分配的总数值。</param>
    /// <param name="targetCount">参与分配的目标数量。</param>
    /// <param name="targetIndex">当前目标在列表中的序号，从 0 开始。</param>
    /// <returns>当前目标应该获得的数值。</returns>
    public static int CalculateEvenlyDistributedEliteBonus(int totalBonus, int targetCount, int targetIndex)
    {
        if (totalBonus <= 0 || targetCount <= 0 || targetIndex < 0)
        {
            return 0;
        }

        int baseBonus = totalBonus / targetCount;
        int remainder = totalBonus % targetCount;
        return baseBonus + (targetIndex < remainder ? 1 : 0);
    }

    /// <summary>
    /// 计算抢夺符咒精英强化的生命值增量：20 + 进阶数 * 5 + 人数 * 20。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>本次应该增加的生命值。</returns>
    private static int CalculateRobTalismanEliteHpBonus(Player player)
    {
        int ascensionLevel = Math.Max(0, player.RunState.AscensionLevel);
        int playerCount = Math.Max(1, player.RunState.Players.Count);
        return RobTalismanEliteBaseHpBonus
            + ascensionLevel * RobTalismanEliteAscensionHpBonus
            + playerCount * RobTalismanElitePlayerHpBonus;
    }

    /// <summary>
    /// 获取本次抢夺符咒精英战中可强化的初始出场敌人。
    /// </summary>
    /// <param name="combatRoom">当前精英战斗房间。</param>
    /// <returns>应该获得抢夺符咒强化的敌人列表。</returns>
    private static List<Creature> GetRobTalismanEliteTargets(CombatRoom combatRoom)
    {
        return combatRoom.CombatState.Enemies
            .Where(enemy => ShouldBuffRobTalismanElite(enemy, combatRoom))
            .ToList();
    }

    /// <summary>
    /// 判断指定敌人是否应该获得抢夺符咒精英强化。
    /// </summary>
    /// <param name="enemy">候选敌人。</param>
    /// <param name="combatRoom">当前精英战斗房间。</param>
    /// <returns>应该强化时返回 true。</returns>
    private static bool ShouldBuffRobTalismanElite(Creature enemy, CombatRoom combatRoom)
    {
        if (enemy == null || !enemy.IsMonster || enemy.Monster == null)
        {
            MainFile.Logger.Info("【抢夺符咒】跳过精英强化候选：目标为空或不是怪物。");
            return false;
        }

        if (!enemy.IsAlive)
        {
            MainFile.Logger.Info($"【抢夺符咒】跳过精英强化候选：目标={enemy.Name}，原因=目标已死亡。");
            return false;
        }

        if (!enemy.CanReceivePowers)
        {
            MainFile.Logger.Info($"【抢夺符咒】跳过精英强化候选：目标={enemy.Name}，原因=无法接收能力。");
            return false;
        }

        if (IsRobTalismanMinionLike(enemy))
        {
            MainFile.Logger.Info($"【抢夺符咒】跳过精英强化候选：目标={enemy.Name}，原因=疑似爪牙或召唤物，怪物={enemy.Monster.Id.Entry}，槽位={enemy.SlotName ?? "空"}。");
            return false;
        }

        if (!combatRoom.Encounter.HaveMonstersBeenGenerated || !combatRoom.Encounter.MonstersWithSlots.Any(monsterWithSlot => monsterWithSlot.Item1 == enemy.Monster))
        {
            MainFile.Logger.Info($"【抢夺符咒】跳过精英强化候选：目标={enemy.Name}，原因=不是本场初始出场怪。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 通过怪物 Id 和槽位名做保守兜底，避免把明确标成爪牙、召唤物、随从的小怪当成精英本体强化。
    /// </summary>
    /// <param name="enemy">候选敌人。</param>
    /// <returns>疑似爪牙或召唤物时返回 true。</returns>
    private static bool IsRobTalismanMinionLike(Creature enemy)
    {
        string monsterId = enemy.Monster?.Id.Entry ?? string.Empty;
        string slotName = enemy.SlotName ?? string.Empty;
        return ContainsMinionKeyword(monsterId) || ContainsMinionKeyword(slotName);
    }

    /// <summary>
    /// 判断文本里是否包含常见爪牙、召唤物、随从关键词。
    /// </summary>
    /// <param name="text">需要检查的怪物 Id 或槽位名。</param>
    /// <returns>包含爪牙关键词时返回 true。</returns>
    private static bool ContainsMinionKeyword(string text)
    {
        return text.Contains("minion", StringComparison.OrdinalIgnoreCase)
            || text.Contains("summon", StringComparison.OrdinalIgnoreCase)
            || text.Contains("spawn", StringComparison.OrdinalIgnoreCase)
            || text.Contains("helper", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 事件内手动进入战斗时补录战斗回放初始状态，避免出牌时回放记录器没有初始快照而报错。
    /// </summary>
    private static void RecordCombatReplayInitialStateIfNeeded()
    {
        if (!RunManager.Instance.CombatReplayWriter.IsEnabled)
        {
            MainFile.Logger.Info("【抢夺符咒】战斗回放记录器未启用，跳过补录初始状态。");
            return;
        }

        RunManager.Instance.CombatReplayWriter.RecordInitialState(RunManager.Instance.ToSave(null));
        MainFile.Logger.Info("【抢夺符咒】已为事件精英战斗补录回放初始状态。");
    }

    /// <summary>
    /// 根据当前章节获取可随机遭遇的精英池：第 1 层只取 1 层，第 2 层取 1-2 层，第 3 层取 1-3 层。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>可用于开战选项的精英遭遇列表。</returns>
    private static List<EncounterModel> GetAvailableEliteEncounters(Player player)
    {
        int actCount = Math.Clamp(player.RunState.CurrentActIndex + 1, 1, player.RunState.Acts.Count);
        return player.RunState.Acts
            .Take(actCount)
            .SelectMany(act => act.AllEliteEncounters)
            .OrderBy(encounter => encounter.Id.Entry, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 查找玩家牌组中的一张冒牌符咒，优先返回未升级版本。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>找到的冒牌符咒；没有时返回 null。</returns>
    private static CardModel? FindFakeTalisman(Player? player)
    {
        return player?.Deck?.Cards
            .Where(card => card is FakeTalisman)
            .OrderBy(card => card.IsUpgraded)
            .FirstOrDefault();
    }

    /// <summary>
    /// 从当前游戏诅咒卡池中随机抽取指定数量诅咒；候选不足时尽量抽取已有候选。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="count">需要抽取的诅咒数量。</param>
    /// <returns>被抽中的诅咒原型列表。</returns>
    private static IReadOnlyList<CardModel> PickRandomCurses(Player player, int count)
    {
        List<CardModel> candidates = ModelDb.CardPool<CurseCardPool>().AllCards
            .OrderBy(card => card.Id.Entry, StringComparer.Ordinal)
            .ToList();
        List<CardModel> chosenCurses = [];

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int index = StableRandomHelper.StableIndex(
                player,
                candidates.Count,
                "RobTalismanEventHelper.PickRandomCurses",
                i.ToString(),
                string.Join(",", candidates.Select(card => card.Id.Entry)));
            chosenCurses.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        return chosenCurses;
    }
}
