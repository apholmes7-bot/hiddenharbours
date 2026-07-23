using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Rod Fishing v2, Wave 2 — the player-held DEPTH as a catch-roll weight
    /// (design/rod-fishing-v2-brainstorm.md §2.3 "why it matters", §6.1; <see cref="CatchResolver"/> +
    /// <see cref="DepthDropMath.SpeciesDepthAffinity"/>). Pins:
    ///  • determinism — same seed, same context, same pick, with or without a depth (rule 5; the roll's
    ///    structure is unchanged: one NextDouble per resolve);
    ///  • neutrality — a depth-less context and depth-unauthored species roll EXACTLY as the legacy
    ///    overload (the resolver's balance is not rewritten);
    ///  • direction — holding just off the floor shifts picks toward the Bottom species, holding
    ///    mid-column toward the Midwater species (a weight, never a wall: the "wrong" fish still comes
    ///    up sometimes).
    /// </summary>
    public class DepthDropResolverTests
    {
        private static readonly DepthDropSettings S = DepthDropSettings.Default;
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishSpeciesDef MakeFish(string id, FishDepthBand bands, FishFlags flags)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = id; f.Category = FishCategory.InshoreGroundfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Handline | Gear.Longline | Gear.Jig;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f;
            f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            f.DepthBands = bands; f.BehaviorFlags = flags;
            _spawned.Add(f);
            return f;
        }

        private static CatchContext CtxAtDepth(float heldDepthM, float floorM)
            => new CatchContext("region.coddle_cove", 0f, 12f, Season.HighSummer, Gear.Jig, heldDepthM, floorM);

        // ---- determinism --------------------------------------------------------------------

        [Test]
        public void SameSeed_SameDepth_SamePicks()
        {
            var pool = new[]
            {
                MakeFish("fish.halibut", FishDepthBand.Deep, FishFlags.Bottom),
                MakeFish("fish.mackerel", FishDepthBand.Midwater, FishFlags.None),
                MakeFish("fish.neutral", FishDepthBand.None, FishFlags.None),
            };
            const float floor = 40f;
            CatchContext ctx = CtxAtDepth(floor - S.BottomSweetWindowMeters * 0.5f, floor);

            var a = new System.Random(1234);
            var b = new System.Random(1234);
            for (int i = 0; i < 200; i++)
            {
                FishSpeciesDef fa = CatchResolver.Resolve(pool, in ctx, S, a);
                FishSpeciesDef fb = CatchResolver.Resolve(pool, in ctx, S, b);
                Assert.AreSame(fa, fb, $"same seed must pick the same species (iteration {i})");
            }
        }

        [Test]
        public void DepthlessContext_RollsExactlyAsTheLegacyOverload()
        {
            // The additive growth must not disturb a single legacy roll: a context built with the 5-arg
            // constructor picks the SAME sequence through both overloads for the same seed.
            var pool = new[]
            {
                MakeFish("fish.a", FishDepthBand.Deep, FishFlags.Bottom),
                MakeFish("fish.b", FishDepthBand.Midwater, FishFlags.None),
            };
            CatchContext legacy = new CatchContext("region.coddle_cove", 0f, 12f, Season.HighSummer, Gear.Jig);
            Assert.IsFalse(legacy.HasDepth, "the 5-arg constructor marks the context depth-less");

            var a = new System.Random(77);
            var b = new System.Random(77);
            for (int i = 0; i < 200; i++)
                Assert.AreSame(CatchResolver.Resolve(pool, in legacy, a),
                               CatchResolver.Resolve(pool, in legacy, S, b),
                               $"legacy and depth overloads must agree on a depth-less context (iteration {i})");
        }

        [Test]
        public void DepthUnauthoredPool_IsUnmovedByDepth()
        {
            // Species with no bands and no flags: the depth game weights nothing — same picks as depth-less.
            var pool = new[]
            {
                MakeFish("fish.a", FishDepthBand.None, FishFlags.None),
                MakeFish("fish.b", FishDepthBand.None, FishFlags.None),
            };
            CatchContext atFloor = CtxAtDepth(39.5f, 40f);
            CatchContext depthless = new CatchContext("region.coddle_cove", 0f, 12f, Season.HighSummer, Gear.Jig);

            var a = new System.Random(9);
            var b = new System.Random(9);
            for (int i = 0; i < 200; i++)
                Assert.AreSame(CatchResolver.Resolve(pool, in depthless, S, a),
                               CatchResolver.Resolve(pool, in atFloor, S, b),
                               $"unauthored species must roll identically at any depth (iteration {i})");
        }

        // ---- direction (the species-targeting tactic) ---------------------------------------

        [Test]
        public void EffectiveWeight_ShiftsTheIntendedDirection()
        {
            var halibut = MakeFish("fish.halibut", FishDepthBand.Deep, FishFlags.Bottom);
            var mackerel = MakeFish("fish.mackerel", FishDepthBand.Midwater, FishFlags.None);
            const float floor = 40f;                                  // 40 m of water: the floor is Deep zone
            CatchContext offFloor = CtxAtDepth(floor - S.BottomSweetWindowMeters * 0.5f, floor);
            CatchContext midColumn = CtxAtDepth((S.InshoreMaxMeters + S.MidwaterMaxMeters) * 0.5f, floor);

            Assert.Greater(CatchResolver.EffectiveWeight(halibut, in offFloor, in S),
                           CatchResolver.EffectiveWeight(halibut, in midColumn, in S),
                           "the bottom fish weighs more just off the floor than mid-column");
            Assert.Greater(CatchResolver.EffectiveWeight(mackerel, in midColumn, in S),
                           CatchResolver.EffectiveWeight(mackerel, in offFloor, in S),
                           "the midwater fish weighs more mid-column than at the floor");
            Assert.Greater(CatchResolver.EffectiveWeight(halibut, in offFloor, in S),
                           CatchResolver.EffectiveWeight(mackerel, in offFloor, in S),
                           "off the floor, the bottom fish out-weighs the midwater fish");
            Assert.Greater(CatchResolver.EffectiveWeight(mackerel, in midColumn, in S),
                           CatchResolver.EffectiveWeight(halibut, in midColumn, in S),
                           "mid-column, the midwater fish out-weighs the bottom fish");
        }

        [Test]
        public void HeldDepth_ShiftsThePicks_TowardTheTargetedSpecies_Deterministically()
        {
            var pool = new[]
            {
                MakeFish("fish.halibut", FishDepthBand.Deep, FishFlags.Bottom),
                MakeFish("fish.mackerel", FishDepthBand.Midwater, FishFlags.None),
            };
            const float floor = 40f;
            CatchContext offFloor = CtxAtDepth(floor - S.BottomSweetWindowMeters * 0.5f, floor);
            CatchContext midColumn = CtxAtDepth((S.InshoreMaxMeters + S.MidwaterMaxMeters) * 0.5f, floor);

            // Seeded → the counts are exact and reproducible, not statistical hope (rule 5).
            int CountBottomPicks(in CatchContext ctx)
            {
                var rng = new System.Random(4242);
                int bottom = 0;
                for (int i = 0; i < 400; i++)
                    if (ReferenceEquals(CatchResolver.Resolve(pool, in ctx, S, rng), pool[0])) bottom++;
                return bottom;
            }

            int bottomAtFloor = CountBottomPicks(in offFloor);
            int bottomMidColumn = CountBottomPicks(in midColumn);

            Assert.Greater(bottomAtFloor, bottomMidColumn,
                "holding just off the floor must shift picks toward the bottom species");
            Assert.Greater(bottomAtFloor, 200, "off the floor the bottom fish should dominate the split");
            Assert.Less(bottomMidColumn, 200, "mid-column the midwater fish should dominate the split");
            Assert.Greater(bottomMidColumn, 0,
                "…but the off-band fish still comes up sometimes — a weight, never a wall");
        }
    }
}
