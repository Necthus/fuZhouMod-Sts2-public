using System.Reflection;
using System.Runtime.Loader;
using HarmonyLib;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Unlocks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.ShadowKhan;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Helpers;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Powers.Masks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters.Mocks;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Localization;

internal class Program
{
    static readonly Harmony Harmony = new("ShengZhuRegression.NativeIsolation");
    static bool NoEditor(ref bool __result) { __result = false; return false; }
    static bool ConsolePrint(string text) { Console.WriteLine(text); return false; }
    static void ModEntry(Type type, ref string __result)
    {
        if (type.Assembly == typeof(PigTalisman).Assembly) __result = "SHENGZHUSTS2MOD-" + __result;
    }
    static bool NoSave(ref MegaCrit.Sts2.Core.Saves.SaveManager? __result) { __result = null; return false; }
    static bool TestText(LocString __instance, ref string __result) { __result = __instance.LocEntryKey; return false; }

    static void Assert(bool condition, string reason)
    {
        if (!condition) throw new Exception(reason);
    }

    static (Player Player, CombatState State, List<Creature> Enemies) Combat(int enemyCount = 3)
    {
        MaskManager.Clear();
        var player = Player.CreateForNewRun<Deprived>(UnlockState.all, 1);
        var run = RunState.CreateForTest(players: [player], acts: [], seed: "SHENGZHU-REGRESSION");
        var state = new CombatState(runState: run);
        state.AddPlayer(player);
        player.ResetCombatState();
        List<Creature> enemies = [];
        for (int i = 0; i < enemyCount; i++)
        {
            var enemy = state.CreateCreature(ModelDb.Monster<MockAttackMonster>().ToMutable(), CombatSide.Enemy, null);
            state.AddCreature(enemy);
            enemy.SetCurrentHpInternal(100);
            enemies.Add(enemy);
        }
        var turnType = typeof(CombatManager).Assembly.GetType("MegaCrit.Sts2.Core.Combat.CombatTurnState")!;
        var turn = Activator.CreateInstance(turnType, state)!;
        AccessTools.Property(turnType, "IsInProgress").SetValue(turn, true);
        AccessTools.Property(turnType, "IsStarting").SetValue(turn, false);
        AccessTools.Field(typeof(CombatManager), "_turnState").SetValue(CombatManager.Instance, turn);
        CombatManager.Instance.StateTracker.SetState(state);
        return (player, state, enemies);
    }

    static CardPlay Play(CardModel card, Creature? target = null) => new()
    {
        Card = card, Player = card.Owner, Target = target, ResultPile = PileType.Discard,
        Resources = default, IsAutoPlay = false, PlayIndex = 0, PlayCount = 1
    };

    static async Task OnPlay(CardModel card, CardPlay play)
    {
        if (card.Pile == null) card.Owner.PlayerCombatState!.PlayPile.AddInternal(card);
        var task = (Task)AccessTools.Method(card.GetType(), "OnPlay").Invoke(card, [new BlockingPlayerChoiceContext(), play])!;
        await task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    static async Task Main(string[] args)
    {
        string game = args.Length > 0 ? Path.GetFullPath(args[0]) : "C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64";
        AssemblyLoadContext.Default.Resolving += (_, name) => File.Exists(Path.Combine(game, name.Name + ".dll")) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game, name.Name + ".dll")) : null;
        TestMode.IsOn = true;
        // Console tests have no Godot native engine. Keep only logger environment detection isolated.
        Harmony.Patch(AccessTools.Method("MegaCrit.Sts2.Core.Logging.Logger:GetIsRunningFromGodotEditor"), prefix: new HarmonyMethod(typeof(Program), nameof(NoEditor)));
        Harmony.Patch(AccessTools.Method("MegaCrit.Sts2.Core.Logging.ConsoleLogPrinter:Print"), prefix: new HarmonyMethod(typeof(Program), nameof(ConsolePrint)));
        Harmony.Patch(AccessTools.Method(typeof(ModelDb), nameof(ModelDb.GetEntry)), postfix: new HarmonyMethod(typeof(Program), nameof(ModEntry)));
        Harmony.Patch(AccessTools.PropertyGetter(typeof(MegaCrit.Sts2.Core.Saves.SaveManager), "Instance"), prefix: new HarmonyMethod(typeof(Program), nameof(NoSave)));
        Harmony.Patch(AccessTools.Method(typeof(LocString), "GetFormattedText", Type.EmptyTypes), prefix: new HarmonyMethod(typeof(Program), nameof(TestText)));
        MegaCrit.Sts2.Core.Modding.AssemblyInfo.Init();
        var types = typeof(ModelDb).Assembly.GetTypes().Concat(typeof(PigTalisman).Assembly.GetTypes())
            .Where(t => !t.IsAbstract && !t.ContainsGenericParameters && t.IsSubclassOf(typeof(AbstractModel)) && t.GetConstructor(Type.EmptyTypes) != null).ToArray();
        AccessTools.Field(typeof(ModelDb), "_allAbstractModelSubtypes").SetValue(null, types);
        ModelDb.Init(types);
        MegaCrit.Sts2.Core.Modding.AssemblyInfo.MockTypes = types.ToDictionary(t => t, _ => ((MegaCrit.Sts2.Core.Modding.Mod?)null, true));
        MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache.Init();
        ModelDb.InitIds();
        new Harmony("ShengZhuRegression.ModPatches").PatchAll(typeof(PigTalisman).Assembly);
        Console.WriteLine("PASS all mod Harmony patches resolve on installed beta");
        var (player, state, enemies) = Combat();
        var consumed = state.CreateCard<StrikeIronclad>(player);
        player.PlayerCombatState!.Hand.AddInternal(consumed);
        var exhaust = await CardCmd.Exhaust(new BlockingPlayerChoiceContext(), consumed);
        Assert(exhaust != null && consumed.Pile?.Type == PileType.Exhaust, "Exhaust did not move the selected card");
        Console.WriteLine("PASS native CardCmd.Exhaust return type and pile transition");

        (player, state, enemies) = Combat();
        var attack = state.CreateCard<StrikeIronclad>(player);
        player.PlayerCombatState!.Hand.AddInternal(attack);
        var shadow = state.CreateCard<MingTaShadow>(player);
        await OnPlay(shadow, Play(shadow, enemies[0]));
        Assert(attack.Pile?.Type == PileType.Exhaust && enemies[0].CurrentHp < 100, "MingTa did not finish exhaustion and damage");
        Console.WriteLine("PASS MingTa consumes another attack and completes damage");

        (player, state, enemies) = Combat();
        var pig = ModelDb.Relic<PigTalisman>().ToMutable();
        player.AddRelicInternal(pig);
        player.AddRelicInternal(ModelDb.Relic<RatTalisman>().ToMutable());
        attack = state.CreateCard<StrikeIronclad>(player);
        var play = Play(attack, enemies[1]);
        await pig.BeforeCardPlayed(play);
        enemies[1].SetCurrentHpInternal(0);
        state.RemoveCreature(enemies[1]);
        await pig.AfterCardPlayed(new BlockingPlayerChoiceContext(), play);
        Assert(enemies[0].CurrentHp == 99 && enemies[2].CurrentHp == 99, "Killed middle target suppressed splash");
        Console.WriteLine("PASS killed and removed middle target still splashes both original neighbors");

        (player, state, enemies) = Combat();
        pig = ModelDb.Relic<PigTalisman>().ToMutable();
        player.AddRelicInternal(pig);
        player.AddRelicInternal(ModelDb.Relic<RatTalisman>().ToMutable());
        attack = state.CreateCard<StrikeIronclad>(player);
        var leftPlay = Play(attack, enemies[0]);
        var rightPlay = Play(attack, enemies[2]);
        await pig.BeforeCardPlayed(leftPlay);
        await pig.BeforeCardPlayed(rightPlay);
        await pig.AfterCardPlayed(new BlockingPlayerChoiceContext(), rightPlay);
        await pig.AfterCardPlayed(new BlockingPlayerChoiceContext(), leftPlay);
        Assert(enemies[0].CurrentHp == 100 && enemies[1].CurrentHp == 98 && enemies[2].CurrentHp == 100, "Nested plays overwrote splash targets");
        Console.WriteLine("PASS nested plays of the same card retain separate splash snapshots");

        (player, state, enemies) = Combat();
        var activatedPig = (PigTalisman)ModelDb.Relic<PigTalisman>().ToMutable();
        player.AddRelicInternal(activatedPig);
        activatedPig.SetActivated(true);
        enemies[0].GainBlockInternal(20);
        await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.ArtifactPower>(new BlockingPlayerChoiceContext(), enemies[0], 1, enemies[0], null);
        attack = state.CreateCard<StrikeIronclad>(player);
        await activatedPig.BeforeCardPlayed(Play(attack, enemies[0]));
        Assert(enemies[0].Block == 0 && enemies[0].CurrentHp == 100 && !enemies[0].HasPower<MegaCrit.Sts2.Core.Models.Powers.ArtifactPower>() && activatedPig.CooldownTurnsRemaining == 3, "Pig block removal caused damage or did not clear artifact");
        Console.WriteLine("PASS activated Pig removes block/artifact without damage and starts cooldown");

        (player, state, enemies) = Combat();
        player.AddRelicInternal(ModelDb.Relic<PigTalisman>().ToMutable());
        var laser = state.CreateCard<LaserEyes>(player);
        enemies[0].SetCurrentHpInternal(1);
        await OnPlay(laser, Play(laser, enemies[0]));
        Assert(enemies[0].IsDead && enemies[1].CurrentHp == 96 && enemies[2].CurrentHp == 96, $"LaserEyes failed area damage: {string.Join(',', enemies.Select(e => e.CurrentHp))}");
        Assert(enemies[1].HasPower<MegaCrit.Sts2.Core.Models.Powers.VulnerablePower>(), "LaserEyes did not apply vulnerability to survivor");
        Console.WriteLine("PASS LaserEyes kills selected target and damages/debuffs remaining enemies");

        (player, state, enemies) = Combat();
        pig = ModelDb.Relic<PigTalisman>().ToMutable();
        player.AddRelicInternal(pig);
        player.AddRelicInternal(ModelDb.Relic<RatTalisman>().ToMutable());
        laser = state.CreateCard<LaserEyes>(player);
        player.PlayerCombatState!.PlayPile.AddInternal(laser);
        play = Play(laser, enemies[1]);
        await pig.BeforeCardPlayed(play);
        await OnPlay(laser, play);
        await pig.AfterCardPlayed(new BlockingPlayerChoiceContext(), play);
        Assert(enemies.All(e => e.CurrentHp == 96), "Resonance area attack incorrectly triggered single-target splash");
        Console.WriteLine("PASS resonance area attack does not also trigger single-target splash");

        (player, state, enemies) = Combat();
        var darkness = state.CreateCard<EndlessDarkness>(player);
        await OnPlay(darkness, Play(darkness, player.Creature));
        Assert(player.Creature.Powers.OfType<BaseMaskPower>().Count() == 9 && player.Creature.HasPower<TaLaPower>(), "Ten masks were not applied");
        Assert(MaskManager.GetMaskCapacity(player.Creature) == 10, "Mask capacity is not ten");
        Assert(player.PlayerCombatState!.OrbQueue.Orbs.Count == 0, "Mask powers contaminated native orb queue");
        Console.WriteLine("PASS EndlessDarkness applies ten masks, capacity ten, no native orbs");
        await OnPlay(darkness, Play(darkness, player.Creature));
        Assert(player.Creature.Powers.OfType<BaseMaskPower>().All(p => p.Amount == 2) && MaskManager.GetMaskCapacity(player.Creature) == 10, "Recasting ten masks caused overflow or lost stacks");
        Console.WriteLine("PASS recasting ten masks stacks powers without overflow");

        (player, state, enemies) = Combat();
        darkness = state.CreateCard<EndlessDarkness>(player);
        CardCmd.Upgrade(darkness, MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.None);
        await OnPlay(darkness, Play(darkness, player.Creature));
        Assert(player.Creature.Powers.OfType<BaseMaskPower>().All(p => p.GenerateUpgraded) && player.Creature.GetPower<TaLaPower>()!.GenerateUpgraded, "Upgraded ten masks did not upgrade generation");
        Console.WriteLine("PASS upgraded ten masks preserve upgraded soldier generation");

        (player, state, enemies) = Combat();
        var rat = (RatTalisman)ModelDb.Relic<RatTalisman>().ToMutable();
        player.AddRelicInternal(rat);
        var curse = state.CreateCard<Injury>(player);
        player.PlayerCombatState!.Hand.AddInternal(curse);
        var replace = (Task)AccessTools.Method(typeof(RatTalisman), "RunSyncedReplaceCurseOrStatus").Invoke(rat, [new BlockingPlayerChoiceContext(), false])!;
        await replace.WaitAsync(TimeSpan.FromSeconds(10));
        Assert(curse.Pile?.Type == PileType.Exhaust && rat.IsOnCooldown, "Rat did not exhaust curse and start cooldown");
        Assert(player.PlayerCombatState.Hand.Cards.Any(c => c.Type == CardType.Attack && c.EnergyCost.GetWithModifiers(CostModifiers.All) == 0 && c.Keywords.Contains(CardKeyword.Exhaust)), "Rat did not generate zero-cost exhausting attack");
        Console.WriteLine("PASS RatTalisman transforms a curse into a zero-cost exhausting attack");
        Console.WriteLine("All gameplay regression scenarios passed.");
    }
}
