using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters.Mocks;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Character;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Events;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Relics;

internal partial class Program
{
    // The console fixture has no Godot localization tables. Supply the keys
    // used by the real event options without changing their callbacks.
    static bool FixtureEventTextExists(string key, ref bool __result)
    {
        __result = key.StartsWith("SHENGZHUSTS2MOD-PAN_KU_BOX_EVENT.", StringComparison.Ordinal);
        return false;
    }

    static async Task NovemberCompatibilityRegression()
    {
        // Leave the mock combat from the preceding tests before entering an event.
        AccessTools.Field(typeof(CombatManager), "_turnState").SetValue(CombatManager.Instance, null);
        var player = Player.CreateForNewRun<ShengZhu>(UnlockState.all, 1);
        var run = RunState.CreateForTest(players: [player], acts: [], seed: "SHENGZHU-01131");
        // BaseLib normally adds these models to the game's canonical relic list.
        var talismanTypes = (Type[])AccessTools.Field(typeof(TalismanLocator), "AllTalismanTypes").GetValue(null)!;
        var getModel = AccessTools.Method(typeof(ModelDb), "Get", [typeof(Type)]);
        AccessTools.Field(typeof(ModelDb), "_allRelics").SetValue(null, ModelDb.AllRelics
            .Concat(talismanTypes.Select(t => (RelicModel)getModel.Invoke(null, [t])!))
            .Append(ModelDb.Relic<PanKuBox>()).Distinct().ToArray());
        var room = new CombatRoom(ModelDb.Encounter<MockMonsterEncounter>().ToMutable(), run);
        var original = new RelicReward(ModelDb.Relic<BurningBlood>().ToMutable(), player);
        var rewards = new RewardsSet(player).EmptyForRoom(room).WithCustomRewards([original]);
        await rewards.GenerateWithoutOffering().WaitAsync(TimeSpan.FromSeconds(10));
        Assert(rewards.Rewards.Single() is LinkedRewardSet linked &&
            linked.Rewards.Count == 2 && ReferenceEquals(linked.Rewards[0], original) &&
            linked.Rewards[1] is RelicReward { Relic: not null },
            "Combat reward hook did not create the relic/talisman choice");
        Console.WriteLine("PASS native post-combat rewards generate relic/talisman choice");

        int originalMaxHp = player.Creature.MaxHp;
        var story = (PanKuBoxEvent)ModelDb.Event<PanKuBoxEvent>().ToMutable();
        var exists = AccessTools.Method(typeof(LocString), nameof(LocString.Exists), [typeof(string), typeof(string)]);
        var fixture = AccessTools.Method(typeof(Program), nameof(FixtureEventTextExists));
        Harmony.Patch(exists, prefix: new HarmonyMethod(fixture));
        try
        {
            await story.BeginEvent(player, null, false).WaitAsync(TimeSpan.FromSeconds(10));
            Assert(story.CurrentOptions[0].TextKey.EndsWith(".PAY_MAX_HP"), "Wrong Ancient Seal option");
            await story.CurrentOptions[0].Chosen().WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            Harmony.Unpatch(exists, fixture);
        }
        Assert(story.IsFinished && player.Creature.MaxHp == originalMaxHp - 10 &&
            player.Creature.CurrentHp == originalMaxHp - 10 && player.GetRelic<PanKuBox>() != null,
            "Ancient Seal failed to deduct ten max HP, grant Pan Ku Box, or finish");
        Console.WriteLine("PASS Ancient Seal max HP option grants Pan Ku Box and finishes");

        var restored = Player.FromSerializable(player.ToSerializable());
        Assert(restored.Character is ShengZhu && restored.GetRelic<PanKuBox>() != null &&
            restored.GetRelic<TalismanLocator>() != null && restored.Deck.Cards.Count == player.Deck.Cards.Count &&
            restored.Creature.MaxHp == player.Creature.MaxHp,
            "ShengZhu player save data did not round-trip");
        Console.WriteLine("PASS native save restoration resolves ShengZhu, deck, relics and HP");
    }
}
