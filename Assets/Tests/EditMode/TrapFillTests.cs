using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The soak-to-fill math (multi-catch pots, owner ask 2026-07-13) — the pure <see cref="TrapFill"/>
    /// derivations and the list resolver <see cref="PlacedTrapCatch.ResolveMany"/>. Pins rule 5 at N:
    /// same placement facts + same haul time ⇒ the identical fill count and the identical catch list
    /// (order included), every run; the count honors <see cref="TrapDef.CapacityUnits"/> exactly (the
    /// doc-reconciled semantics: 1 at ready, capacity at <see cref="TrapDef.HoursToFullPot"/>); and a
    /// pot only ever FILLS as time passes — never un-catches. No engine state, no clock, no scene.
    /// </summary>
    public class TrapFillTests
    {
        private const int WorldSeed = 4242;
        private const string InstanceId = "trap.lobster#77.3";
        private const double PlaceTime = 5000.0;
        private const float SoakHours = 12f;
        private const float FullHours = 36f;
        private const double Hour = 3600.0;

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishSpeciesDef MakeSpecies(string id, string name, int baseValue, float spawnWeight = 1f)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = name;
            f.Category = FishCategory.Shellfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Trap;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f;
            f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 0.5f; f.MaxWeightKg = 1.5f;
            f.BaseValue = baseValue; f.SupplyElasticity = 0.2f;
            f.SpawnWeight = spawnWeight;
            _spawned.Add(f);
            return f;
        }

        private static CatchContext TrapContext()
            => new CatchContext("region.coddle_cove", tideHeight: 2f, hourOfDay: 12f, Season.HighSummer, Gear.Trap);

        // ---- Fill01 (the curve) ------------------------------------------------------------------

        [Test]
        public void Fill01_ZeroBeforeReady_ZeroAtReady_OneAtFull_ClampedBeyond()
        {
            Assert.AreEqual(0f, TrapFill.Fill01(PlaceTime, PlaceTime, SoakHours, FullHours), "just placed → 0");
            Assert.AreEqual(0f, TrapFill.Fill01(PlaceTime, PlaceTime + 6.0 * Hour, SoakHours, FullHours), "mid-soak → 0");
            Assert.AreEqual(0f, TrapFill.Fill01(PlaceTime, PlaceTime + SoakHours * Hour, SoakHours, FullHours),
                "exactly ready → the fill STARTS here (0)");
            Assert.AreEqual(0.5f, TrapFill.Fill01(PlaceTime, PlaceTime + 24.0 * Hour, SoakHours, FullHours), 1e-5f,
                "half-way between ready (12h) and full (36h) → 0.5");
            Assert.AreEqual(1f, TrapFill.Fill01(PlaceTime, PlaceTime + FullHours * Hour, SoakHours, FullHours),
                "at the full-pot mark → 1");
            Assert.AreEqual(1f, TrapFill.Fill01(PlaceTime, PlaceTime + 100.0 * Hour, SoakHours, FullHours),
                "beyond → clamped at 1 (she holds full, no overfill)");
        }

        [Test]
        public void Fill01_DegenerateWindow_IsFullAtReady_ButStillGatedBeforeIt()
        {
            // HoursToFullPot ≤ SoakHours = the documented "full the moment she's ready" collapse…
            Assert.AreEqual(1f, TrapFill.Fill01(PlaceTime, PlaceTime + SoakHours * Hour, SoakHours, SoakHours));
            Assert.AreEqual(1f, TrapFill.Fill01(PlaceTime, PlaceTime + SoakHours * Hour, SoakHours, 0f));
            // …but the readiness gate itself still holds: an unsoaked pot fills nothing.
            Assert.AreEqual(0f, TrapFill.Fill01(PlaceTime, PlaceTime + 1.0 * Hour, SoakHours, 0f));
        }

        // ---- ResolveCount (capacity semantics — the doc-reconciled CapacityUnits) ------------------

        [Test]
        public void ResolveCount_OneAtReady_CapacityAtFull()
        {
            const int capacity = 4;
            Assert.AreEqual(1, TrapFill.ResolveCount(0f, capacity, WorldSeed, InstanceId, PlaceTime),
                "a just-ready pot holds exactly her first animal");
            Assert.AreEqual(capacity, TrapFill.ResolveCount(1f, capacity, WorldSeed, InstanceId, PlaceTime),
                "a fully soaked pot holds exactly CapacityUnits — the Def field means what its doc says");
        }

        [Test]
        public void ResolveCount_AlwaysWithinOneToCapacity_AndCapacityOneIsTheOldSingleCatch()
        {
            for (float f = 0f; f <= 1f; f += 0.1f)
            {
                int n = TrapFill.ResolveCount(f, 4, WorldSeed, InstanceId, PlaceTime);
                Assert.GreaterOrEqual(n, 1, $"fill {f:0.0}: never empty once ready");
                Assert.LessOrEqual(n, 4, $"fill {f:0.0}: never over capacity");

                Assert.AreEqual(1, TrapFill.ResolveCount(f, 1, WorldSeed, InstanceId, PlaceTime),
                    $"fill {f:0.0}: a capacity-1 pot is exactly the old single-catch pot at every soak");
            }
        }

        [Test]
        public void ResolveCount_MonotonicallyFills_AsTheSoakGrows()
        {
            // The pot fills, never un-catches: for one pot, the count must be non-decreasing over time
            // (the per-slot rolls are fixed; only the fill threshold grows). Sweep several pots.
            for (int pot = 0; pot < 20; pot++)
            {
                string id = $"trap.lobster#{pot}";
                int last = 0;
                for (float f = 0f; f <= 1.001f; f += 0.05f)
                {
                    int n = TrapFill.ResolveCount(Mathf.Clamp01(f), 4, WorldSeed, id, PlaceTime);
                    Assert.GreaterOrEqual(n, last, $"pot {pot}: the count never drops as the fill grows");
                    last = n;
                }
            }
        }

        [Test]
        public void ResolveCount_MidFill_VariesAcrossPots_Deterministically()
        {
            // Mid-fill the count is a seed-varied roll per pot — not every pot the same number — and each
            // pot's number reproduces exactly on a fresh compute (the save→load guarantee at count level).
            var seen = new HashSet<int>();
            for (int pot = 0; pot < 40; pot++)
            {
                string id = $"trap.lobster#{pot}";
                int a = TrapFill.ResolveCount(0.5f, 4, WorldSeed, id, PlaceTime);
                int b = TrapFill.ResolveCount(0.5f, 4, WorldSeed, id, PlaceTime);
                Assert.AreEqual(a, b, $"pot {pot}: same facts → same count, every compute");
                seen.Add(a);
            }
            Assert.Greater(seen.Count, 1, "half-filled pots vary — the fill is a per-pot roll, not a fixed step");
        }

        // ---- ResolveMany (the deterministic list) --------------------------------------------------

        [Test]
        public void ResolveMany_SameFacts_YieldIdenticalList_OrderIncluded()
        {
            var pool = new List<FishSpeciesDef>
            {
                MakeSpecies("fish.lobster", "Lobster", 28),
                MakeSpecies("fish.rock_crab", "Crab", 4),
            };
            var ctx = TrapContext();

            var a = new List<CatchItem>();
            var b = new List<CatchItem>();
            int na = PlacedTrapCatch.ResolveMany(pool, in ctx, null, 1, 4, WorldSeed, InstanceId, PlaceTime, a);
            int nb = PlacedTrapCatch.ResolveMany(pool, in ctx, null, 1, 4, WorldSeed, InstanceId, PlaceTime, b);

            Assert.AreEqual(4, na, "four filled slots resolve four animals");
            Assert.AreEqual(na, nb, "same facts → same count");
            for (int i = 0; i < na; i++)
            {
                Assert.AreEqual(a[i].SpeciesId, b[i].SpeciesId, $"animal {i}: same species, same order");
                Assert.AreEqual(a[i].WeightKg, b[i].WeightKg, 0f, $"animal {i}: bit-identical size");
            }
        }

        [Test]
        public void ResolveMany_AnimalsRideIndependentIndexedStreams()
        {
            // With two species in the pool, a 4-animal pot should not be forced uniform: across many pots
            // at least one comes up MIXED (per-animal indexed streams, not one roll copied N times).
            var pool = new List<FishSpeciesDef>
            {
                MakeSpecies("fish.lobster", "Lobster", 28),
                MakeSpecies("fish.rock_crab", "Crab", 4),
            };
            var ctx = TrapContext();
            var results = new List<CatchItem>();

            bool sawMixed = false;
            for (int pot = 0; pot < 30 && !sawMixed; pot++)
            {
                PlacedTrapCatch.ResolveMany(pool, in ctx, null, 1, 4, WorldSeed, $"pot#{pot}", PlaceTime, results);
                for (int i = 1; i < results.Count; i++)
                    if (results[i].SpeciesId != results[0].SpeciesId) { sawMixed = true; break; }
            }
            Assert.IsTrue(sawMixed, "a pot can come up mixed — each animal rolls its own stream");
        }

        [Test]
        public void ResolveMany_BaitLeansSpeciesPerAnimal_NeverTheCount()
        {
            var pool = new List<FishSpeciesDef>
            {
                MakeSpecies("fish.lobster", "Lobster", 28),
                MakeSpecies("fish.rock_crab", "Crab", 4),
            };
            var ctx = TrapContext();
            var favours = new List<string> { "fish.lobster" };
            var baited = new List<CatchItem>();
            var plain = new List<CatchItem>();

            int lobsterBaited = 0, lobsterPlain = 0, crabBaited = 0;
            for (int pot = 0; pot < 100; pot++)
            {
                string id = $"pot#{pot}";
                int nb = PlacedTrapCatch.ResolveMany(pool, in ctx, favours, PlacedTrapCatch.BaitFavourMultiplier,
                                                     4, WorldSeed, id, PlaceTime, baited);
                int np = PlacedTrapCatch.ResolveMany(pool, in ctx, null, 1,
                                                     4, WorldSeed, id, PlaceTime, plain);
                Assert.AreEqual(np, nb, $"pot {pot}: bait leans WHAT, not HOW MANY — counts match unbaited");
                for (int i = 0; i < nb; i++)
                {
                    if (baited[i].SpeciesId == "fish.lobster") lobsterBaited++; else crabBaited++;
                    if (plain[i].SpeciesId == "fish.lobster") lobsterPlain++;
                }
            }
            Assert.Greater(lobsterBaited, lobsterPlain, "herring leans every animal's roll toward lobster");
            Assert.Greater(crabBaited, 0, "crab still lands — a nudge, not a gate (the Build-3 rule at N)");
        }

        [Test]
        public void ResolveMany_EmptyOrGatedPool_YieldsNothing()
        {
            var ctx = TrapContext();
            var results = new List<CatchItem>();
            Assert.AreEqual(0, PlacedTrapCatch.ResolveMany(new List<FishSpeciesDef>(), in ctx, null, 1, 4,
                                                           WorldSeed, InstanceId, PlaceTime, results));
            Assert.AreEqual(0, PlacedTrapCatch.ResolveMany(null, in ctx, null, 1, 4,
                                                           WorldSeed, InstanceId, PlaceTime, results));

            var pool = new List<FishSpeciesDef> { MakeSpecies("fish.lobster", "Lobster", 28) };
            var wrongRegion = new CatchContext("region.elsewhere", 2f, 12f, Season.HighSummer, Gear.Trap);
            Assert.AreEqual(0, PlacedTrapCatch.ResolveMany(pool, in wrongRegion, null, 1, 4,
                                                           WorldSeed, InstanceId, PlaceTime, results),
                "everything gates out → an empty haul, no animal half-lands");
            Assert.AreEqual(0, results.Count, "the results list is honestly empty");
        }

        // ---- the slot/animal hashes (stability across fresh computes) -------------------------------

        [Test]
        public void SlotAndAnimalHashes_AreStable_AndVaryByIndexAndChannel()
        {
            uint s1 = TrapFill.SlotHash(WorldSeed, InstanceId, PlaceTime, 1);
            Assert.AreEqual(s1, TrapFill.SlotHash(WorldSeed, InstanceId, PlaceTime, 1), "slot hash reproduces");
            Assert.AreNotEqual(s1, TrapFill.SlotHash(WorldSeed, InstanceId, PlaceTime, 2), "varies by slot");

            int c0 = TrapFill.AnimalCatchSeed(WorldSeed, InstanceId, PlaceTime, 0);
            Assert.AreEqual(c0, TrapFill.AnimalCatchSeed(WorldSeed, InstanceId, PlaceTime, 0), "catch seed reproduces");
            Assert.AreNotEqual(c0, TrapFill.AnimalCatchSeed(WorldSeed, InstanceId, PlaceTime, 1), "varies by animal");
            Assert.AreNotEqual((uint)c0, TrapFill.SlotHash(WorldSeed, InstanceId, PlaceTime, 0),
                "the fill and catch channels are distinct streams off the same lineage");
        }
    }
}
