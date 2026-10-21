using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards;
using ShengZhuSts2Mod.ShengZhuSts2ModCode.Cards.TeamJackie;

internal partial class Program
{
    static int _attackContextsDisposed;
    static void CountAttackContextDisposal() => _attackContextsDisposed++;
    // Use the real damage/power hooks; only UI, logging and saves are isolated
    // by the console fixture. Comparing seeded runs preserves random damage.
    static async Task<(int[][] Hits, int Vigor, int Followup)> VigorCase(
        string kind, bool upgraded, bool combo, int vigor, int x = 3, int hp = 100)
    {
        var (player, state, enemies) = Combat(2);
        CardModel card = kind switch
        {
            "meteor" => state.CreateCard<MeteorHitsMountain>(player),
            "tiger" => state.CreateCard<OneTigerTwoSheep>(player),
            _ => state.CreateCard<DrunkenFist>(player)
        };
        if (upgraded) CardCmd.Upgrade(card, CardPreviewStyle.None);
        if (card is AhFuCard) AccessTools.Field(typeof(AhFuCard), "_afuComboActive").SetValue(card, combo);
        if (card is DrunkenFist) card.EnergyCost.CapturedXValue = x;
        if (vigor > 0)
            await PowerCmd.Apply<VigorPower>(new BlockingPlayerChoiceContext(), player.Creature, vigor, player.Creature, null);
        var hits = enemies.Select(_ => new List<int>()).ToArray();
        for (int i = 0; i < enemies.Count; i++)
        {
            int index = i;
            enemies[i].SetCurrentHpInternal(hp);
            enemies[i].CurrentHpChanged += (oldHp, newHp) => hits[index].Add(oldHp - newHp);
        }
        await OnPlay(card, Play(card, enemies[0]));
        int remaining = player.Creature.GetPower<VigorPower>()?.Amount ?? 0;
        var cardHits = hits.Select(h => h.ToArray()).ToArray();
        // Use a surviving enemy so a leaked or newly granted Vigor is observable.
        if (enemies[1].IsAlive)
        {
            int before = enemies[1].CurrentHp;
            var strike = state.CreateCard<StrikeIronclad>(player);
            await OnPlay(strike, Play(strike, enemies[1]));
            return (cardHits, remaining, before - enemies[1].CurrentHp);
        }
        return (cardHits, remaining, -1);
    }

    static async Task VerifyMultiHitVigor()
    {
        Harmony.Patch(AccessTools.Method(typeof(AttackContext), nameof(AttackContext.DisposeAsync)),
            prefix: new HarmonyMethod(typeof(Program), nameof(CountAttackContextDisposal)));
        foreach (bool upgraded in new[] { false, true })
        foreach (bool combo in new[] { false, true })
        foreach (string kind in new[] { "meteor", "tiger" })
        {
            var basic = await VigorCase(kind, upgraded, combo, 0);
            var buffed = await VigorCase(kind, upgraded, combo, 4);
            int count = kind == "meteor" ? 3 : combo ? 3 : 2;
            int damage = kind == "meteor" ? (upgraded ? 3 : 2) : (upgraded ? 7 : 5);
            Assert(basic.Hits[0].SequenceEqual(Enumerable.Repeat(damage, count)), $"{kind} base hits changed");
            Assert(buffed.Hits[0].SequenceEqual(Enumerable.Repeat(damage + 4, count)),
                $"{kind} all hits must receive Vigor: {string.Join(',', buffed.Hits[0])}");
            Assert(buffed.Hits[1].Length == 0, $"{kind} unexpectedly became an area attack");
            int granted = kind == "meteor" && combo ? (upgraded ? 6 : 4) : 0;
            Assert(buffed.Vigor == granted && basic.Vigor == granted, $"{kind} consumed wrong Vigor or lost combo reward");
            Assert(buffed.Followup == 6 + granted, $"{kind} leaked old Vigor to followup or lost new Vigor");
            Console.WriteLine($"PASS vigor {kind} upgraded={upgraded} combo={combo}: all {count} hits +4; remaining={granted}");
        }
        foreach (bool upgraded in new[] { false, true })
        foreach (int x in new[] { 0, 1, 3 })
        {
            var basic = await VigorCase("drunken", upgraded, false, 0, x);
            var buffed = await VigorCase("drunken", upgraded, false, 4, x);
            for (int enemy = 0; enemy < 2; enemy++)
            {
                Assert(basic.Hits[enemy].Length == x && buffed.Hits[enemy].Length == x, "Drunken X hit count changed");
                Assert(basic.Hits[enemy].All(h => h >= (upgraded ? 8 : 6) && h <= (upgraded ? 12 : 10)), "Drunken random range changed");
                Assert(buffed.Hits[enemy].SequenceEqual(basic.Hits[enemy].Select(h => h + 4)), "Every Drunken hit must receive Vigor on every enemy");
            }
            Assert(buffed.Vigor == (x == 0 ? 4 : 0), "Drunken must consume Vigor only when it attacks");
            Assert(buffed.Followup == (x == 0 ? 10 : 6), "Drunken leaked Vigor to next card");
            Console.WriteLine($"PASS vigor drunken upgraded={upgraded} X={x}: random damage, both enemies, consumption and followup");
        }
        foreach (string kind in new[] { "meteor", "drunken" })
        {
            int disposedBefore = _attackContextsDisposed;
            var lethal = await VigorCase(kind, false, false, 4, hp: 1);
            Assert(lethal.Hits[0].Length == 1, $"{kind} continued hitting dead target");
            if (kind == "meteor") Assert(lethal.Vigor == 0, "Meteor did not consume Vigor with another enemy alive");
            else
            {
                // Killing all enemies ends combat and native hooks stop modifying
                // powers. Check scope disposal rather than forcing post-combat state.
                Assert(lethal.Followup == -1 && _attackContextsDisposed == disposedBefore + 1,
                    "Drunken did not dispose its attack context after killing all enemies");
            }
            Console.WriteLine($"PASS vigor {kind}: lethal target/last-enemy early exit completes correctly");
        }
    }
}
