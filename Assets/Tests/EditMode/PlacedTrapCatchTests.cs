using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The trap's DETERMINISTIC catch resolution (trap-fishing arc Build 3, rule 5). Reuses the existing
    /// CatchResolver (no new roller); this asserts (a) reproducibility for the same seed, (b) the stable
    /// hash gives the same seed across "runs" (so save→load lands the identical catch), and (c) bait
    /// soft-weights the roll toward its favoured species without gating either out.
    /// </summary>
    public class PlacedTrapCatchTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // A trap-caught species that matches the shared test context (region, all-year, all-day, wide tide).
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

        [Test]
        public void SameSeed_SameInputs_YieldsIdenticalCatch()
        {
            var pool = new List<FishSpeciesDef> { MakeSpecies("fish.lobster", "Lobster", 40), MakeSpecies("fish.rock_crab", "Crab", 12) };
            var ctx = TrapContext();

            int seed = StableHash.TrapCatchSeed(worldSeed: 1234, "trap.lobster#1", placementGameTimeSeconds: 5000.0);

            CatchItem? a = PlacedTrapCatch.Resolve(pool, in ctx, baitFavours: null, favourMultiplier: 1, new System.Random(seed));
            CatchItem? b = PlacedTrapCatch.Resolve(pool, in ctx, baitFavours: null, favourMultiplier: 1, new System.Random(seed));

            Assert.IsTrue(a.HasValue && b.HasValue, "both resolve a catch");
            Assert.AreEqual(a.Value.SpeciesId, b.Value.SpeciesId, "same seed → same species");
            Assert.AreEqual(a.Value.WeightKg, b.Value.WeightKg, 1e-6f, "same seed → same size roll");
        }

        [Test]
        public void StableHash_IsReproducible_AcrossFreshComputes()
        {
            // The seed is a pure function of the placement facts — the "save→load" guarantee at the hash level:
            // a later recompute (a fresh process would call this again) yields the same seed, so the same catch.
            int s1 = StableHash.TrapCatchSeed(4242, "trap.lobster#77.3", 123456.75);
            int s2 = StableHash.TrapCatchSeed(4242, "trap.lobster#77.3", 123456.75);
            Assert.AreEqual(s1, s2, "the stable hash reproduces the same seed for the same facts");

            // Different facts → (essentially always) a different seed.
            Assert.AreNotEqual(s1, StableHash.TrapCatchSeed(4243, "trap.lobster#77.3", 123456.75), "seed varies with worldSeed");
            Assert.AreNotEqual(s1, StableHash.TrapCatchSeed(4242, "trap.lobster#77.4", 123456.75), "seed varies with instance id");
            Assert.AreNotEqual(s1, StableHash.TrapCatchSeed(4242, "trap.lobster#77.3", 123456.76), "seed varies with placement time");
        }

        [Test]
        public void Bait_SoftWeights_TowardFavouredSpecies_ButBothStayPossible()
        {
            // Two equally-weighted species. Herring favours lobster; over many seeds the lobster share should
            // rise well above the unbaited ~50%, but crab must still land sometimes (a nudge, not a gate).
            var lobster = MakeSpecies("fish.lobster", "Lobster", 40);
            var crab = MakeSpecies("fish.rock_crab", "Crab", 12);
            var pool = new List<FishSpeciesDef> { lobster, crab };
            var ctx = TrapContext();
            var herringFavours = new List<string> { "fish.lobster" };

            int lobsterBaited = 0, crabBaited = 0, lobsterUnbaited = 0;
            const int trials = 400;
            for (int i = 0; i < trials; i++)
            {
                var baited = PlacedTrapCatch.Resolve(pool, in ctx, herringFavours, PlacedTrapCatch.BaitFavourMultiplier, new System.Random(i));
                var plain = PlacedTrapCatch.Resolve(pool, in ctx, baitFavours: null, favourMultiplier: 1, new System.Random(i));
                if (baited.HasValue && baited.Value.SpeciesId == "fish.lobster") lobsterBaited++;
                if (baited.HasValue && baited.Value.SpeciesId == "fish.rock_crab") crabBaited++;
                if (plain.HasValue && plain.Value.SpeciesId == "fish.lobster") lobsterUnbaited++;
            }

            Assert.Greater(lobsterBaited, lobsterUnbaited, "herring leans the roll toward lobster vs unbaited");
            Assert.Greater(lobsterBaited, crabBaited, "with herring, lobster lands more often than crab");
            Assert.Greater(crabBaited, 0, "crab is still possible — bait nudges, it doesn't gate");
        }

        [Test]
        public void CrabBait_LeansCrab_TheOtherWay()
        {
            var lobster = MakeSpecies("fish.lobster", "Lobster", 40);
            var crab = MakeSpecies("fish.rock_crab", "Crab", 12);
            var pool = new List<FishSpeciesDef> { lobster, crab };
            var ctx = TrapContext();
            var scrapFavours = new List<string> { "fish.rock_crab" };

            int crabCount = 0, lobsterCount = 0;
            for (int i = 0; i < 400; i++)
            {
                var c = PlacedTrapCatch.Resolve(pool, in ctx, scrapFavours, PlacedTrapCatch.BaitFavourMultiplier, new System.Random(i));
                if (c.HasValue && c.Value.SpeciesId == "fish.rock_crab") crabCount++;
                if (c.HasValue && c.Value.SpeciesId == "fish.lobster") lobsterCount++;
            }
            Assert.Greater(crabCount, lobsterCount, "fish-scrap leans crab (the opposite lean from herring)");
            Assert.Greater(lobsterCount, 0, "lobster still possible");
        }

        [Test]
        public void EmptyPool_YieldsNothing()
        {
            var ctx = TrapContext();
            Assert.IsNull(PlacedTrapCatch.Resolve(new List<FishSpeciesDef>(), in ctx, null, 1, new System.Random(1)));
            Assert.IsNull(PlacedTrapCatch.Resolve(null, in ctx, null, 1, new System.Random(1)));
        }

        [Test]
        public void ContextGatesOutEverything_YieldsNothing()
        {
            // A species that only bites on Trap in coddle_cove — but the context uses the wrong region.
            var pool = new List<FishSpeciesDef> { MakeSpecies("fish.lobster", "Lobster", 40) };
            var wrongRegion = new CatchContext("region.elsewhere", 2f, 12f, Season.HighSummer, Gear.Trap);
            Assert.IsNull(PlacedTrapCatch.Resolve(pool, in wrongRegion, null, 1, new System.Random(1)),
                "no species matches the context → no catch");
        }

        [Test]
        public void BuildWeightedPool_RepeatsFavouredSpecies()
        {
            var lobster = MakeSpecies("fish.lobster", "Lobster", 40);
            var crab = MakeSpecies("fish.rock_crab", "Crab", 12);
            var pool = new List<FishSpeciesDef> { lobster, crab };

            var weighted = PlacedTrapCatch.BuildWeightedPool(pool, new List<string> { "fish.lobster" }, 3);
            int lobsterCopies = weighted.FindAll(f => f.Id == "fish.lobster").Count;
            int crabCopies = weighted.FindAll(f => f.Id == "fish.rock_crab").Count;
            Assert.AreEqual(3, lobsterCopies, "the favoured species appears 3x (multiplier 3)");
            Assert.AreEqual(1, crabCopies, "the unfavoured species appears once");

            var noNudge = PlacedTrapCatch.BuildWeightedPool(pool, null, 3);
            Assert.AreEqual(2, noNudge.Count, "no favours → the base pool, unchanged");
        }
    }
}
