using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// DOCK-FIRST rod fishing — the owner-blocking on-foot fixes, pinned:
    ///
    ///  • <b>the cast lands in WATER</b>: on foot the flick can point inland, so a dry landing is
    ///    cozy-clamped to the farthest wet point of the cast arc (the longest cast that still splashes),
    ///    and a fully-dry arc is a "no water that way" short-cast reset — Idle, no penalty, no stuck
    ///    state (CastWaterMath + FishingController.ClampCastToWater);
    ///  • <b>the weighted drop refuses a DRY spot</b>: standing on planks/land there is no column under
    ///    the rig — the drop stays Idle with a notice instead of instantly "bottoming out" on the boards;
    ///  • <b>the region follows the player</b>: the travel-aware GameServices.CurrentRegionId (written by
    ///    the active region's anchor) overrides the controller's authored region at cast time, so fishing
    ///    the St Peters shore rolls St Peters' pool even though the persistent rig was authored elsewhere;
    ///  • <b>the dev bootstrap's arbitration</b> (DevRegionBootstrap.ShouldSeed) seeds only in the editor
    ///    and only when no persistent core is live.
    ///
    /// All pure/EditMode: injected dt, injected water probes, fakes over the Core seams — no play-mode
    /// lifecycle, no wall clock (the established Fishing test conventions).
    /// </summary>
    public class FishingOnFootTests
    {
        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 6;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FakeEnv : IEnvironmentService
        {
            public float WaterLevel;    // flat surface — the terrain shapes the wet/dry line
            public int WorldSeed => 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => WaterLevel;
            public float WaterLevelAt(double totalSeconds) => WaterLevel;
        }

        /// <summary>Water south of the shoreline, land at/north of it (a straight east-west coast).</summary>
        private sealed class FakeShore : ITidalTerrain
        {
            public float ShoreY = 2f;
            public float ElevationAt(Vector2 worldPos) => worldPos.y >= ShoreY ? 5f : -5f;
        }

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<FishingStateChanged>();
            EventBus.Clear<FishCaught>();
            EventBus.Clear<DevNotice>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<FishingStateChanged>();
            EventBus.Clear<FishCaught>();
            EventBus.Clear<DevNotice>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishSpeciesDef MakeFish(string id, params string[] regions)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = id; f.Category = FishCategory.InshoreGroundfish;
            f.RegionIds = regions;
            f.AllowedGear = Gear.Handline | Gear.Longline | Gear.Jig;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f;
            f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        private FishingController MakeController(string regionId, Gear gear, params FishSpeciesDef[] fish)
        {
            var go = new GameObject("Angler");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(new FakeHold(), fish, regionId, gear, seed: 7);
            return c;
        }

        // ---- CastWaterMath (pure) ------------------------------------------------------------

        [Test]
        public void ClampToWater_WetLanding_KeepsTheFullDistance()
        {
            bool ok = CastWaterMath.TryClampToWater(Vector2.zero, Vector2.up, 10f, 32,
                                                    p => true, out float d);
            Assert.IsTrue(ok);
            Assert.AreEqual(10f, d, 1e-4f, "open water — the cast stands as flicked");
        }

        [Test]
        public void ClampToWater_PicksTheFarthestWetPointOfTheArc()
        {
            // Water only in the band y ∈ [3, 4] along a northward cast of 10 m: the clamp must land at
            // the FARTHEST wet probe (the longest cast that still splashes), not the nearest.
            bool ok = CastWaterMath.TryClampToWater(Vector2.zero, Vector2.up, 10f, 32,
                                                    p => p.y >= 3f && p.y <= 4f, out float d);
            Assert.IsTrue(ok, "part of the arc is wet — the cast clamps, it doesn't fail");
            Assert.LessOrEqual(d, 4f + 1e-4f, "landed inside the wet band");
            Assert.GreaterOrEqual(d, 4f - 10f / 32f - 1e-4f, "…at its FAR edge (within one probe step)");
        }

        [Test]
        public void ClampToWater_FullyDryArc_IsFalse()
        {
            bool ok = CastWaterMath.TryClampToWater(Vector2.zero, Vector2.up, 10f, 32,
                                                    p => false, out _);
            Assert.IsFalse(ok, "no water anywhere on the arc — the cozy no-water reset");
        }

        [Test]
        public void ClampToWater_DegenerateInputs_AreFalse_NotThrows()
        {
            Assert.IsFalse(CastWaterMath.TryClampToWater(Vector2.zero, Vector2.up, 10f, 32, null, out _),
                "null probe");
            Assert.IsFalse(CastWaterMath.TryClampToWater(Vector2.zero, Vector2.up, 0f, 32, p => true, out _),
                "zero distance");
            Assert.IsFalse(CastWaterMath.TryClampToWater(Vector2.zero, Vector2.up, -3f, 32, p => true, out _),
                "negative distance");
            Assert.IsFalse(CastWaterMath.TryClampToWater(Vector2.zero, new Vector2(float.NaN, 1f), 10f, 32,
                p => true, out _), "NaN direction");
            Assert.IsFalse(CastWaterMath.TryClampToWater(new Vector2(float.NaN, 0f), Vector2.up, 10f, 32,
                p => true, out _), "NaN anchor");
        }

        // ---- the controller's land-in-water rule (the dock/shore cast) -----------------------

        [Test]
        public void CastTowardLand_ClampsToTheWaterInsideTheArc()
        {
            GameServices.Environment = new FakeEnv { WaterLevel = 0f };
            GameServices.TidalTerrain = new FakeShore { ShoreY = 2f };   // land starts 2 m north

            var c = MakeController("region.coddle_cove", Gear.Handline,
                                   MakeFish("fish.cod", "region.coddle_cove"));

            // The shared clean flick casts NORTH ~9.75 m — well past the shoreline at y=2. The landing
            // must clamp back into the water short of the shore, and the cast still fishes (Waiting).
            FlickGestures.CastLine(c);
            Assert.AreEqual(FishingPhase.Waiting, c.Phase, "the clamped cast still fishes");
            Assert.IsTrue(c.LastCast.IsCast);
            Assert.Less(c.LastCast.LandingPoint.y, 2f, "the line landed in the WATER, not on the land");
            Assert.Greater(c.LastCast.DistanceMetres, 0f);
            Assert.Less(c.LastCast.DistanceMetres, 9.75f, "…shorter than the unclamped flick");
        }

        [Test]
        public void CastWithNoWaterAnywhereOnTheArc_IsACozyReset_NeverAStuckState()
        {
            GameServices.Environment = new FakeEnv { WaterLevel = 0f };
            GameServices.TidalTerrain = new FakeShore { ShoreY = -100f };   // ALL land, everywhere

            var notices = new List<DevNotice>();
            void OnNotice(DevNotice n) => notices.Add(n);
            EventBus.Subscribe<DevNotice>(OnNotice);
            try
            {
                var c = MakeController("region.coddle_cove", Gear.Handline,
                                       MakeFish("fish.cod", "region.coddle_cove"));
                FlickGestures.Flick(c);
                Assert.AreEqual(FishingPhase.Idle, c.Phase,
                    "no water that way — the gesture resolves to a short-cast reset (Idle, no penalty)");
                Assert.IsFalse(c.LastCast.IsCast, "nothing counts as having flown");
                Assert.IsTrue(notices.Count > 0, "the player got the on-screen 'no water' tell (DevNotice)");

                // No stuck state: face the water (no terrain = open water) and the next flick flies.
                GameServices.TidalTerrain = null;
                FlickGestures.CastLine(c);
                Assert.AreEqual(FishingPhase.Waiting, c.Phase, "the very next cast fishes normally");
            }
            finally
            {
                EventBus.Unsubscribe<DevNotice>(OnNotice);
            }
        }

        [Test]
        public void CastWithNoAuthoredBathymetry_IsUnchanged_OpenWaterPosture()
        {
            // No terrain service (EditMode rigs, legacy scenes): every existing behaviour stands.
            var c = MakeController("region.coddle_cove", Gear.Handline,
                                   MakeFish("fish.cod", "region.coddle_cove"));
            FlickGestures.CastLine(c);
            Assert.AreEqual(FishingPhase.Waiting, c.Phase);
            Assert.AreEqual(9.75f, c.LastCast.DistanceMetres, 0.01f, "the unclamped flick distance");
        }

        // ---- the weighted drop on a dry spot -------------------------------------------------

        [Test]
        public void WeightedDrop_OnADrySpot_RefusesCozily_StaysIdle()
        {
            var c = MakeController("region.coddle_cove", Gear.Jig,
                                   MakeFish("fish.cod", "region.coddle_cove"));
            c.ConfigureDepthDrop(rigWeightKg: 1.0f, waterColumnMeters: 0f);   // planks — no column

            var notices = new List<DevNotice>();
            void OnNotice(DevNotice n) => notices.Add(n);
            EventBus.Subscribe<DevNotice>(OnNotice);
            try
            {
                c.Tick(0.05f, true);
                Assert.AreEqual(FishingPhase.Idle, c.Phase,
                    "no water under the rig — the drop refuses instead of 'bottoming out' on the boards");
                Assert.IsTrue(notices.Count > 0, "…with the on-screen tell");

                // No stuck state: over real water the same rig drops normally.
                c.Tick(0.05f, false);   // release the edge
                c.ConfigureDepthDrop(rigWeightKg: 1.0f, waterColumnMeters: 8f);
                c.Tick(0.05f, true);
                Assert.AreEqual(FishingPhase.Sinking, c.Phase, "over water the drop starts as ever");
            }
            finally
            {
                EventBus.Unsubscribe<DevNotice>(OnNotice);
            }
        }

        // ---- the travel-aware region (fish where you actually are) ---------------------------

        [Test]
        public void CatchRegion_FollowsTheReportedCurrentRegion_NotTheAuthoredFallback()
        {
            // A species that bites ONLY at St Peters, on a controller AUTHORED for the cove (the
            // persistent rig's reality — built once, sailed everywhere).
            var stPetersFish = MakeFish("fish.cod", "region.st_peters");

            // Without a reported region the authored fallback stands: the cove pool is empty → NoBite.
            var c1 = MakeController("region.coddle_cove", Gear.Handline, stPetersFish);
            FlickGestures.CastLine(c1);
            float t = 0f;
            while (c1.Phase == FishingPhase.Waiting && t < 30f) { c1.Tick(0.05f, false); t += 0.05f; }
            Assert.AreEqual(FishingPhase.NoBite, c1.Phase,
                "no region reported → the authored cove region rolls (and this fish isn't there)");

            // The active region's anchor reports St Peters (the travel seam) → the SAME rig catches it.
            GameServices.CurrentRegionId = "region.st_peters";
            var c2 = MakeController("region.coddle_cove", Gear.Handline, stPetersFish);
            FlickGestures.CastLine(c2);
            t = 0f;
            while (c2.Phase == FishingPhase.Waiting && t < 30f) { c2.Tick(0.05f, false); t += 0.05f; }
            Assert.AreEqual(FishingPhase.Bite, c2.Phase,
                "the reported current region wins at cast time — fishing works where the player IS");
        }

        // ---- the dev bootstrap's arbitration -------------------------------------------------

        [Test]
        public void DevBootstrap_SeedsOnlyInTheEditor_AndOnlyWithoutALiveCore()
        {
            Assert.IsTrue(DevRegionBootstrap.ShouldSeed(coreReady: false, isEditor: true),
                "played directly in the editor with no core → seed the dev core");
            Assert.IsFalse(DevRegionBootstrap.ShouldSeed(coreReady: true, isEditor: true),
                "the persistent core travelled in → never seed a second player");
            Assert.IsFalse(DevRegionBootstrap.ShouldSeed(coreReady: false, isEditor: false),
                "never in a build — owner-iteration affordance only");
            Assert.IsFalse(DevRegionBootstrap.ShouldSeed(coreReady: true, isEditor: false), "never in a build");
        }
    }
}
