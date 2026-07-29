using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// BAIT &amp; TACKLE (owner's ruling 2026-07-25) — what's on the hook steers what bites. Pins the
    /// owner's two rules: the pairings are a WEIGHT and never a wall (the wrong kit catches less, never
    /// nothing), and "no bait, no bait-fishing — but tackle still works", which is what guarantees a
    /// player with an empty bait box is never stranded.
    /// </summary>
    public class BaitAndTackleTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishSpeciesDef Fish(string id, LureTag favored = LureTag.None, float spawnWeight = 1f)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id;
            f.DisplayName = id;
            f.FavoredLures = favored;
            f.SpawnWeight = spawnWeight;
            // MUST be set: RegionAllowed returns FALSE for a null region list, so a fresh species
            // belongs nowhere and CatchResolver.Matches filters it out of every roll. (That cost me a
            // debugging round — the weighting looked broken when in fact nothing was being rolled at all.)
            f.RegionIds = new[] { TestRegion };
            _spawned.Add(f);
            return f;
        }

        private const string TestRegion = "region.test";

        private static CatchContext Ctx(LureTag lure = LureTag.None, params string[] baitFavours)
            => new CatchContext(TestRegion, 0f, 12f, Season.HighSummer, Gear.Handline,
                                CatchContext.NoDepth, 0f, lure,
                                baitFavours != null && baitFavours.Length > 0 ? baitFavours : null);

        // Fields, not properties: an `in` argument needs an lvalue, and a property returning a fresh
        // struct each call isn't one (CS8156).
        private static readonly BaitTackleSettings Kit = BaitTackleSettings.Default;
        private static readonly DepthDropSettings Depth = DepthDropSettings.Default;

        // ---- nothing on the hook = exactly the roll that shipped before this existed ---------------

        [Test]
        public void NoBaitNoTackle_IsTheUntouchedRoll()
        {
            var cod = Fish("fish.atlantic_cod", LureTag.Spoon);
            var ctx = Ctx();
            Assert.AreEqual(1f, CatchResolver.BaitAffinity(cod, in ctx, in Kit), 1e-6f,
                "a bare hook must not weight anything");
            Assert.AreEqual(1f, CatchResolver.LureAffinity(cod, in ctx, in Kit), 1e-6f);
            Assert.AreEqual(cod.SpawnWeight, CatchResolver.EffectiveWeight(cod, in ctx, in Depth, in Kit), 1e-6f,
                "with nothing on the hook the species' weight is its authored SpawnWeight, untouched");
        }

        // ---- weights, never walls ------------------------------------------------------------------

        [Test]
        public void TheWrongKit_CatchesLess_ButNeverNothing()
        {
            var haddock = Fish("fish.haddock", LureTag.SoftBait);

            // Bait meant for something else, and a lure it doesn't chase — the worst possible choice.
            var wrong = Ctx(LureTag.Feather, "fish.mackerel");
            float w = CatchResolver.EffectiveWeight(haddock, in wrong, in Depth, in Kit);

            Assert.Greater(w, 0f,
                "the wrong bait and the wrong lure must still leave a real chance — a beginner with " +
                "the wrong kit has to be able to catch SOMETHING, or the system is a wall");
            Assert.Less(w, haddock.SpawnWeight,
                "…but it must genuinely cost you, or choosing well is pointless");
        }

        [Test]
        public void TheRightBait_BeatsTheWrongOne_Decisively()
        {
            var haddock = Fish("fish.haddock");
            float right = CatchResolver.EffectiveWeight(haddock, Ctx(LureTag.None, "fish.haddock"), in Depth, in Kit);
            float wrong = CatchResolver.EffectiveWeight(haddock, Ctx(LureTag.None, "fish.mackerel"), in Depth, in Kit);

            Assert.Greater(right, wrong * 4f,
                "picking the right bait has to be worth several times the wrong one — this is the " +
                "strongest targeting tool the player has, and it must feel like it");
        }

        [Test]
        public void BaitIsPrecise_TackleIsBroad()
        {
            // The owner's realism: a fish that wants FOOD refuses the wrong food outright, while a
            // curious fish will still hit a lure it doesn't love. So the wrong-bait penalty must bite
            // harder than the wrong-lure one — that asymmetry is the whole reason to carry both.
            var cod = Fish("fish.atlantic_cod", LureTag.Spoon);
            float wrongBait = CatchResolver.BaitAffinity(cod, Ctx(LureTag.None, "fish.mackerel"), in Kit);
            float wrongLure = CatchResolver.LureAffinity(cod, Ctx(LureTag.Feather), in Kit);

            Assert.Less(wrongBait, wrongLure,
                "the wrong bait must cost more than the wrong lure — bait TARGETS, tackle COVERS");
        }

        [Test]
        public void BaitAndTackle_StackIndependently()
        {
            var cod = Fish("fish.atlantic_cod", LureTag.Spoon);
            float both = CatchResolver.EffectiveWeight(cod, Ctx(LureTag.Spoon, "fish.atlantic_cod"), in Depth, in Kit);
            float baitOnly = CatchResolver.EffectiveWeight(cod, Ctx(LureTag.None, "fish.atlantic_cod"), in Depth, in Kit);
            float lureOnly = CatchResolver.EffectiveWeight(cod, Ctx(LureTag.Spoon), in Depth, in Kit);

            Assert.Greater(both, baitOnly, "the right lure on top of the right bait is better than bait alone");
            Assert.Greater(both, lureOnly, "…and better than the lure alone");
        }

        // ---- the pairings actually TARGET (the point of the whole feature) -------------------------

        [Test]
        public void TheRightKit_PullsTheFishYouWanted_OutOfAMixedPool()
        {
            // The player's promise: choose mackerel gear, catch mackerel. Run the real resolver over a
            // mixed pool many times and check the targeting actually lands.
            var cod = Fish("fish.atlantic_cod", LureTag.Spoon);
            var mackerel = Fish("fish.mackerel", LureTag.Feather | LureTag.Spinner);
            var pool = new[] { cod, mackerel };

            // ONE rng per arm, drawn many times — NOT a fresh Random per iteration with sequential
            // seeds. .NET's first NextDouble correlates with the seed, so 400 sequential seeds sample a
            // narrow band of the range rather than all of it, and the rarer fish never comes up at all.
            var feathersRng = new System.Random(1234);
            var jigRng = new System.Random(1234);
            int mackerelOnFeathers = 0, mackerelOnAJig = 0;
            for (int i = 0; i < 2000; i++)
            {
                if (CatchResolver.Resolve(pool, Ctx(LureTag.Feather), in Depth, in Kit, feathersRng) == mackerel)
                    mackerelOnFeathers++;
                if (CatchResolver.Resolve(pool, Ctx(LureTag.Spoon), in Depth, in Kit, jigRng) == mackerel)
                    mackerelOnAJig++;
            }

            Assert.Greater(mackerelOnFeathers, mackerelOnAJig,
                "feathers must genuinely bring up more mackerel than a cod jig does — if the pairings " +
                "don't change what you actually catch, the realism is decoration");
            Assert.Greater(mackerelOnAJig, 0,
                "…but the jig must still take the odd mackerel: a weight, never a wall");
        }

        // ---- the wallet, and the owner's "no bait, no bait-fishing" rule --------------------------

        [Test]
        public void TheTackleBox_SpendsBaitDown_AndStopsAtEmpty()
        {
            var save = new SaveData();
            TackleBox.AddBait(save, "bait.capelin", 2);
            Assert.AreEqual(2, TackleBox.BaitCount(save, "bait.capelin"));

            Assert.IsTrue(TackleBox.TrySpendBait(save, "bait.capelin"));
            Assert.IsTrue(TackleBox.TrySpendBait(save, "bait.capelin"));
            Assert.AreEqual(0, TackleBox.BaitCount(save, "bait.capelin"));

            Assert.IsFalse(TackleBox.TrySpendBait(save, "bait.capelin"),
                "an empty box must refuse rather than go negative — this is the gate the rod reads");
            Assert.AreEqual(0, TackleBox.BaitCount(save, "bait.capelin"), "…and must not have gone below zero");
        }

        [Test]
        public void TackleIsOwnedNotConsumed_AndGrantingTwiceIsHarmless()
        {
            var save = new SaveData();
            Assert.IsFalse(TackleBox.OwnsTackle(save, "tackle.spoon"));
            TackleBox.GrantTackle(save, "tackle.spoon");
            TackleBox.GrantTackle(save, "tackle.spoon");
            Assert.IsTrue(TackleBox.OwnsTackle(save, "tackle.spoon"));
            Assert.AreEqual(1, save.OwnedGear.Count, "owning a lure twice is meaningless — no duplicates");

            Assert.IsTrue(TackleBox.RemoveTackle(save, "tackle.spoon"), "the lost-rig seam works");
            Assert.IsFalse(TackleBox.OwnsTackle(save, "tackle.spoon"));
        }

        [Test]
        public void TheWalletIsNullSafe_ForPreBootAndEditModeRigs()
        {
            Assert.AreEqual(0, TackleBox.BaitCount(null, "bait.capelin"));
            Assert.IsFalse(TackleBox.HasBait(null, "bait.capelin"));
            Assert.IsFalse(TackleBox.TrySpendBait(null, "bait.capelin"));
            Assert.IsFalse(TackleBox.OwnsTackle(null, "tackle.spoon"));
            Assert.DoesNotThrow(() => TackleBox.AddBait(null, "bait.capelin", 1));
            Assert.DoesNotThrow(() => TackleBox.GrantTackle(null, "tackle.spoon"));

            var save = new SaveData();
            Assert.DoesNotThrow(() => TackleBox.AddBait(save, null, 1));
            Assert.DoesNotThrow(() => TackleBox.AddBait(save, "bait.capelin", -5));
            Assert.AreEqual(0, TackleBox.BaitCount(save, "bait.capelin"), "a negative grant adds nothing");
        }

        // ---- the shipped tuning stays coherent ----------------------------------------------------

        [Test]
        public void TheDefaultTuning_IsWeightsNotWalls()
        {
            var k = BaitTackleSettings.Default;
            Assert.GreaterOrEqual(k.BaitFavourBoost, 1f, "a boost never punishes");
            Assert.GreaterOrEqual(k.LureFavourBoost, 1f);
            Assert.Greater(k.WrongBaitDamp01, 0f, "the wrong bait must never zero a species out");
            Assert.Greater(k.WrongLureDamp01, 0f, "…nor the wrong lure");
            Assert.Less(k.WrongBaitDamp01, k.WrongLureDamp01,
                "bait is the precise tool and tackle the broad one — the damps must reflect that");
            Assert.Greater(k.BaitFavourBoost, k.LureFavourBoost,
                "choosing bait is the more deliberate act, so it must pay more than choosing a lure");
        }
    }
}
