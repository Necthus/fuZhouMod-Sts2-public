using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.Curses.GrandMageDad;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.GrandMageDad;

namespace ShengZhuSts2Mod.ShengZhuSts2ModCode.Monsters.GrandMageDad;

/// <summary>
/// 大法师老爹行动服务：负责构建固定循环状态机和执行每个行动的基础效果。
/// </summary>
internal static class GrandMageDadMoveService
{
    private static readonly Type[] CurseTypes =
    [
        typeof(CurseBlack),
        typeof(CurseJackie),
        typeof(CurseJade),
        typeof(CurseTohru)
    ];

    private const int ThunderDamage = 9;
    private const int ThunderHits = 3;
    private const int SuppressionDamage = 35;
    private const int TalismanPursuitDamage = 20;
    private const int BaguaDamage = 10;
    private const int BaguaHits = 5;
    private const int ChantBlock = 200;
    private const int ChantArtifact = 6;
    private const int ExecuteDamage = 999;
    private const int DamageCapPerTurn = 250;

    /// <summary>
    /// 创建大法师老爹的第一版固定行动循环。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <returns>怪物行动状态机。</returns>
    public static MonsterMoveStateMachine CreateStateMachine(MonsterModel monster)
    {
        MoveState thunder = CreateThunderCombo(monster);
        MoveState suppression = CreateSuppressionSeal(monster);
        MoveState pursuit = CreateTalismanPursuit(monster);
        MoveState bagua = CreateBaguaBarrage(monster);
        MoveState chant = CreateUltimateSealChant(monster);
        MoveState execute = CreateUltimateSealExecute(monster);

        thunder.FollowUpState = suppression;
        suppression.FollowUpState = pursuit;
        pursuit.FollowUpState = bagua;
        bagua.FollowUpState = chant;
        chant.FollowUpState = execute;
        execute.FollowUpState = thunder;

        return new MonsterMoveStateMachine(
            new[] { thunder, suppression, pursuit, bagua, chant, execute },
            thunder);
    }

    /// <summary>
    /// 创建雷符连打行动。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <returns>行动状态。</returns>
    private static MoveState CreateThunderCombo(MonsterModel monster)
    {
        return new MoveState(
            GrandMageDadMoveIds.ThunderCombo,
            async targets =>
            {
                await ExecuteSecondaryIntent(monster, GrandMageDadMoveIds.SecondaryCurseJackie);
                await AttackAllPlayers(monster, targets, ThunderDamage, ThunderHits);
                await LogMove(monster, GrandMageDadMoveIds.ThunderCombo, targets);
            },
            new MultiAttackIntent(ThunderDamage, ThunderHits));
    }

    /// <summary>
    /// 创建镇邪法印行动。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <returns>行动状态。</returns>
    private static MoveState CreateSuppressionSeal(MonsterModel monster)
    {
        return new MoveState(
            GrandMageDadMoveIds.SuppressionSeal,
            async targets =>
            {
                await ExecuteSecondaryIntent(monster, GrandMageDadMoveIds.SecondaryDefendArtifact);
                await AttackAllPlayers(monster, targets, SuppressionDamage, 1);
                await ApplyToPlayers<WeakPower>(monster, targets, 2);
                await ApplyToPlayers<VulnerablePower>(monster, targets, 2);
                await LogMove(monster, GrandMageDadMoveIds.SuppressionSeal, targets);
            },
            new SingleAttackIntent(SuppressionDamage),
            new DebuffIntent(true));
    }

    /// <summary>
    /// 创建符咒剥夺追击行动；封存符咒会在后续阶段接入。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <returns>行动状态。</returns>
    private static MoveState CreateTalismanPursuit(MonsterModel monster)
    {
        return new MoveState(
            GrandMageDadMoveIds.TalismanPursuit,
            async targets =>
            {
                await ExecuteSecondaryIntent(monster, GrandMageDadMoveIds.SecondaryRandomCurse);
                await AttackAllPlayers(monster, targets, TalismanPursuitDamage, 1);
                await LogMove(monster, GrandMageDadMoveIds.TalismanPursuit, targets);
            },
            new SingleAttackIntent(TalismanPursuitDamage),
            new DebuffIntent(false));
    }

    /// <summary>
    /// 创建八卦连轰行动。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <returns>行动状态。</returns>
    private static MoveState CreateBaguaBarrage(MonsterModel monster)
    {
        return new MoveState(
            GrandMageDadMoveIds.BaguaBarrage,
            async targets =>
            {
                await ExecuteSecondaryIntent(monster, GrandMageDadMoveIds.SecondarySealTalisman);
                await AttackAllPlayers(monster, targets, BaguaDamage, BaguaHits);
                await LogMove(monster, GrandMageDadMoveIds.BaguaBarrage, targets);
            },
            new MultiAttackIntent(BaguaDamage, BaguaHits));
    }

    /// <summary>
    /// 创建终极封印吟唱行动。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <returns>行动状态。</returns>
    private static MoveState CreateUltimateSealChant(MonsterModel monster)
    {
        return new MoveState(
            GrandMageDadMoveIds.UltimateSealChant,
            async targets =>
            {
                await ExecuteSecondaryIntent(monster, GrandMageDadMoveIds.SecondaryDoubleCurse);
                SetChantState(monster, true);
                await PowerCmd.Apply<GrandMageChantShieldPower>(monster.Creature, ChantBlock, monster.Creature, null);
                await CreatureCmd.GainBlock(monster.Creature, ChantBlock, ValueProp.Unpowered, null);
                await PowerCmd.Apply<ArtifactPower>(monster.Creature, ChantArtifact, monster.Creature, null);
                await LogMove(monster, GrandMageDadMoveIds.UltimateSealChant, targets);
            },
            new DefendIntent(),
            new BuffIntent());
    }

    /// <summary>
    /// 创建终极封印处决行动；第一版用高额攻击占位，后续接吟唱护盾判定。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <returns>行动状态。</returns>
    private static MoveState CreateUltimateSealExecute(MonsterModel monster)
    {
        return new MoveState(
            GrandMageDadMoveIds.UltimateSealExecute,
            async targets =>
            {
                SetSecondaryIntent(monster, GrandMageDadMoveIds.SecondaryNone);
                if (CanExecuteUltimateSeal(monster))
                {
                    await AttackAllPlayers(monster, targets, ExecuteDamage, 1);
                    ShengZhuLogHelper.VerboseCombatInfo(() => "【大法师老爹】吟唱护盾仍在，终极封印处决生效。");
                }
                else
                {
                    ShengZhuLogHelper.VerboseCombatInfo(() => "【大法师老爹】吟唱护盾已破或未处于吟唱状态，跳过终极封印处决。");
                }

                SetChantState(monster, false);
                await RemoveChantShieldPower(monster);
                await LogMove(monster, GrandMageDadMoveIds.UltimateSealExecute, targets);
            },
            new SingleAttackIntent(ExecuteDamage));
    }

    /// <summary>
    /// 确保老爹战斗开局核心能力已经挂载。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <returns>异步任务。</returns>
    public static async Task EnsureOpeningPowers(MonsterModel monster)
    {
        Creature creature = monster.Creature;
        if (creature == null || creature.IsDead || !creature.IsAlive)
        {
            return;
        }

        if (!creature.HasPower<GrandMageDamageCapPower>())
        {
            await PowerCmd.Apply<GrandMageDamageCapPower>(creature, DamageCapPerTurn, creature, null);
        }

        if (!creature.HasPower<GrandMageBlessingPower>())
        {
            await PowerCmd.Apply<GrandMageBlessingPower>(creature, 2, creature, null);
        }

        if (!creature.HasPower<GrandMageCounterPouncePower>())
        {
            int playerCount = Math.Max(1, creature.CombatState?.Players.Count ?? 1);
            int dynamicHpBonus = 0;
            int pounceStrength = Math.Max(0, dynamicHpBonus / 30 / playerCount);
            GrandMageCounterPouncePower? pouncePower = await PowerCmd.Apply<GrandMageCounterPouncePower>(creature, pounceStrength, creature, null);
            pouncePower?.Configure(dynamicHpBonus, playerCount);
            if (pounceStrength > 0)
            {
                await PowerCmd.Apply<StrengthPower>(creature, pounceStrength, creature, null);
            }
        }
    }

    /// <summary>
    /// 执行当前回合副意图；阶段三先接入诅咒相关副意图，其余副意图只记录日志。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <param name="secondaryIntentCode">副意图代码。</param>
    /// <returns>异步任务。</returns>
    private static async Task ExecuteSecondaryIntent(MonsterModel monster, string secondaryIntentCode)
    {
        SetSecondaryIntent(monster, secondaryIntentCode);
        switch (secondaryIntentCode)
        {
            case GrandMageDadMoveIds.SecondaryCurseJackie:
                await AddCurseToAllPlayers(monster, typeof(CurseJackie), 1);
                break;
            case GrandMageDadMoveIds.SecondaryRandomCurse:
                await AddRandomCursesToAllPlayers(monster, 1, false, secondaryIntentCode);
                break;
            case GrandMageDadMoveIds.SecondaryDoubleCurse:
                await AddRandomCursesToAllPlayers(monster, 2, true, secondaryIntentCode);
                break;
            default:
                ShengZhuLogHelper.VerboseCombatInfo(() => $"【联机同步】【大法师老爹】副意图={secondaryIntentCode}，阶段三暂不执行额外效果。");
                break;
        }
    }

    /// <summary>
    /// 记录当前副意图，方便存档和后续双意图 UI 读取。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <param name="secondaryIntentCode">副意图代码。</param>
    private static void SetSecondaryIntent(MonsterModel monster, string secondaryIntentCode)
    {
        if (monster.CombatState?.Encounter is GrandMageDadEncounter encounter)
        {
            encounter.State.SecondaryIntentCode = secondaryIntentCode;
        }
    }

    /// <summary>
    /// 向所有存活玩家弃牌堆加入指定老爹诅咒。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <param name="curseType">诅咒牌类型。</param>
    /// <param name="count">每名玩家加入数量。</param>
    /// <returns>异步任务。</returns>
    private static async Task AddCurseToAllPlayers(MonsterModel monster, Type curseType, int count)
    {
        foreach (Player player in GetLivingPlayers(monster))
        {
            for (int i = 0; i < count; i++)
            {
                await AddCurseToPlayer(monster, player, curseType);
            }
        }
    }

    /// <summary>
    /// 向所有存活玩家弃牌堆加入随机老爹诅咒。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <param name="count">每名玩家加入数量。</param>
    /// <param name="distinct">是否要求同一名玩家本次不重复。</param>
    /// <param name="scope">稳定随机作用域。</param>
    /// <returns>异步任务。</returns>
    private static async Task AddRandomCursesToAllPlayers(MonsterModel monster, int count, bool distinct, string scope)
    {
        foreach (Player player in GetLivingPlayers(monster))
        {
            List<Type> remainingTypes = GetOrderedCurseTypes();
            for (int i = 0; i < count && remainingTypes.Count > 0; i++)
            {
                Type? curseType = StableRandomHelper.PickByStableHash(
                    player,
                    remainingTypes,
                    scope,
                    type => type.Name,
                    i.ToString(),
                    monster.CombatState?.RoundNumber.ToString() ?? "0");
                if (curseType == null)
                {
                    continue;
                }

                await AddCurseToPlayer(monster, player, curseType);
                if (distinct)
                {
                    remainingTypes.Remove(curseType);
                }
            }
        }
    }

    /// <summary>
    /// 向指定玩家弃牌堆加入一张老爹诅咒。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <param name="player">目标玩家。</param>
    /// <param name="curseType">诅咒牌类型。</param>
    /// <returns>异步任务。</returns>
    private static async Task AddCurseToPlayer(MonsterModel monster, Player player, Type curseType)
    {
        CardModel? prototype = ModelDb.AllCards.FirstOrDefault(card => card.GetType() == curseType);
        if (prototype == null)
        {
            MainFile.Logger.Info($"【联机异常兜底】【大法师老爹】副意图塞诅咒失败：未找到卡牌原型={curseType.Name}。");
            return;
        }

        CardModel curse = monster.CombatState.CreateCard(prototype, player);
        await CardPileCmdHelper.AddGeneratedCardToCombat(curse, PileType.Discard, false);
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【联机同步】【大法师老爹】副意图塞诅咒：玩家={player.NetId}，卡牌={curse.Id.Entry}，目标牌堆=弃牌堆。");
    }

    /// <summary>
    /// 获取按类型名稳定排序的老爹诅咒类型。
    /// </summary>
    /// <returns>老爹诅咒类型列表。</returns>
    private static List<Type> GetOrderedCurseTypes()
    {
        return CurseTypes.OrderBy(type => type.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// 获取当前战斗所有存活玩家，按联机 ID 稳定排序。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <returns>存活玩家列表。</returns>
    private static IReadOnlyList<Player> GetLivingPlayers(MonsterModel monster)
    {
        return monster.CombatState?.Players
            .Where(player => player.Creature is { IsAlive: true, IsDead: false })
            .OrderBy(player => player.NetId)
            .ToList() ?? [];
    }

    /// <summary>
    /// 更新老爹遭遇里的吟唱状态。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <param name="isChanting">是否正在吟唱。</param>
    private static void SetChantState(MonsterModel monster, bool isChanting)
    {
        if (monster.CombatState?.Encounter is not GrandMageDadEncounter encounter)
        {
            return;
        }

        encounter.State.IsChanting = isChanting;
        encounter.State.ChantShieldAmount = isChanting ? ChantBlock : 0;
    }

    /// <summary>
    /// 判断吟唱处决是否可以发动。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <returns>护盾仍在且处于吟唱状态时返回 true。</returns>
    private static bool CanExecuteUltimateSeal(MonsterModel monster)
    {
        if (monster.Creature == null || monster.Creature.Block <= 0)
        {
            return false;
        }

        bool isChanting = monster.CombatState?.Encounter is GrandMageDadEncounter encounter && encounter.State.IsChanting;
        return isChanting && monster.Creature.HasPower<GrandMageChantShieldPower>();
    }

    /// <summary>
    /// 移除吟唱标记能力。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <returns>异步任务。</returns>
    private static async Task RemoveChantShieldPower(MonsterModel monster)
    {
        GrandMageChantShieldPower? power = monster.Creature?.GetPower<GrandMageChantShieldPower>();
        if (power != null)
        {
            await PowerCmd.Remove(power);
        }
    }

    /// <summary>
    /// 对所有存活玩家执行攻击；多段攻击按段数依次结算。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <param name="targets">原生传入的玩家目标。</param>
    /// <param name="damage">每段伤害。</param>
    /// <param name="hitCount">攻击段数。</param>
    /// <returns>异步任务。</returns>
    private static async Task AttackAllPlayers(MonsterModel monster, IReadOnlyList<Creature> targets, int damage, int hitCount)
    {
        IReadOnlyList<Creature> livingTargets = targets.Where(target => target.IsAlive && !target.IsDead).ToList();
        if (livingTargets.Count == 0)
        {
            return;
        }

        for (int i = 0; i < hitCount; i++)
        {
            await CreatureCmd.Damage(new BlockingPlayerChoiceContext(), livingTargets, damage, ValueProp.Move, monster.Creature, null, null);
        }
    }

    /// <summary>
    /// 对所有存活玩家施加指定能力。
    /// </summary>
    /// <typeparam name="TPower">能力类型。</typeparam>
    /// <param name="monster">老爹怪物模型。</param>
    /// <param name="targets">原生传入的玩家目标。</param>
    /// <param name="amount">施加层数。</param>
    /// <returns>异步任务。</returns>
    private static async Task ApplyToPlayers<TPower>(MonsterModel monster, IReadOnlyList<Creature> targets, int amount)
        where TPower : PowerModel
    {
        foreach (Creature target in targets.Where(target => target.IsAlive && !target.IsDead))
        {
            await PowerCmd.Apply<TPower>(target, amount, monster.Creature, null);
        }
    }

    /// <summary>
    /// 写入行动日志，方便单人和联机排查当前循环是否一致。
    /// </summary>
    /// <param name="monster">老爹怪物模型。</param>
    /// <param name="moveId">行动 ID。</param>
    /// <param name="targets">本次行动目标。</param>
    /// <returns>异步任务。</returns>
    private static Task LogMove(MonsterModel monster, string moveId, IReadOnlyList<Creature> targets)
    {
        string targetNames = string.Join(",", targets.Select(target => target.Name));
        ShengZhuLogHelper.VerboseCombatInfo(() => $"【联机同步】【大法师老爹】动作={moveId}，目标=[{targetNames}]，怪物={monster.Id.Entry}。");
        return Task.CompletedTask;
    }
}
