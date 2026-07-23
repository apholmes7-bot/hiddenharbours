using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Rod Fishing v2, Wave 2 — the DEPTH DROP wired into <see cref="FishingController"/>
    /// (design/rod-fishing-v2-brainstorm.md §2.1/§2.3). Drives the FSM with injected dt (no wall clock,
    /// headless-safe) and asserts the owner's beats:
    ///  • weighted gear DROPS (Sinking, Depth01 climbing continuously) — no cast; light gear keeps the
    ///    legacy bobber path untouched;
    ///  • a heavier rig's Depth01 climbs faster over the same ticks (the count-the-fall read, end to end);
    ///  • CLICK re-engages the reel → Waiting at the HELD band, Depth01 frozen;
    ///  • let it run → it bottoms out at the bathymetry-capped floor: Depth01 = 1 and SlackWindowOpen
    ///    (the state RodLineMath.SlackOvershoot's presenter pops on) in the same publish;
    ///  • HOLD reels up slightly — off the floor, slack closes;
    ///  • the depth game still lands a fish through the unchanged hold/catch path.
    /// </summary>
    public class FishingDepthDropControllerTests
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

        private readonly List<Object> _spawned = new();
        private const float Dt = 0.05f;
        private static readonly DepthDropSettings S = DepthDropSettings.Default;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<FishingStateChanged>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<FishingStateChanged>();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishSpeciesDef MakeFish(string id, Gear gear)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = id; f.Category = FishCategory.InshoreGroundfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = gear;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f;
            f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        /// <summary>A controller mid-drop: Jig gear (always the depth branch), a fixed water column, and a
        /// seeded RNG — fully deterministic, no scene services.</summary>
        private FishingController MakeDropController(float rigWeightKg, float waterColumnM, out FakeHold hold,
                                                     int seed = 999)
        {
            var go = new GameObject("DepthFisher");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            hold = new FakeHold();
            c.Configure(hold, new[] { MakeFish("fish.cod", Gear.Jig | Gear.Handline) },
                        "region.coddle_cove", Gear.Jig, seed);
            c.ConfigureDepthDrop(rigWeightKg, waterColumnM);
            return c;
        }

        // ---- the branch ---------------------------------------------------------------------

        [Test]
        public void WeightedGear_DropsInsteadOfCasting()
        {
            var c = MakeDropController(0.5f, 40f, out _);
            c.Tick(Dt, true);                       // press: the weighted rig DROPS — no cast, no bobber wait
            Assert.AreEqual(FishingPhase.Sinking, c.Phase, "a weighted rig enters the water sinking (§2.1)");
            Assert.AreEqual(0f, c.State.Depth01, 0.05f, "the drop starts at the surface");
        }

        [Test]
        public void LightHandline_KeepsTheCastPath_NotTheDepthBranch()
        {
            var go = new GameObject("CastFisher");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(new FakeHold(), new[] { MakeFish("fish.cod", Gear.Handline) },
                        "region.coddle_cove", Gear.Handline, 999);
            c.ConfigureDepthDrop(S.WeightedHandlineMinKg * 0.5f, 40f);   // light rig — below the threshold

            // A pointerless press must NOT start the depth branch for light gear (and, being the flick
            // world, it can't start a gesture either — the rod just stays down).
            c.Tick(Dt, true);
            Assert.AreEqual(FishingPhase.Idle, c.Phase,
                "a light handline never drops — and a gesture needs a pointer");
            c.Tick(Dt, false);   // release, back to a clean edge

            // The flick world's way into the water: the shared gesture driver (PR #256).
            FlickGestures.CastLine(c);
            Assert.AreEqual(FishingPhase.Waiting, c.Phase, "a light handline casts (the bobber path)");
            Assert.AreEqual(0f, c.State.Depth01, "no depth game on the cast path");
            Assert.IsFalse(c.State.SlackWindowOpen);
        }

        // ---- the fall (continuous read, weight = speed) -------------------------------------

        [Test]
        public void Sinking_PublishesAContinuouslyClimbingDepth01()
        {
            var c = MakeDropController(0.5f, 40f, out _);
            c.Tick(Dt, true);

            float last = c.State.Depth01;
            for (int i = 0; i < 40; i++)
            {
                c.Tick(Dt, false);
                Assert.Greater(c.State.Depth01, last, $"Depth01 must climb every sinking tick (tick {i})");
                last = c.State.Depth01;
            }
            Assert.AreEqual(FishingPhase.Sinking, c.Phase, "still falling — 40 m is a long way down");
        }

        [Test]
        public void HeavierRig_IsDeeperAfterTheSameTicks()
        {
            var light = MakeDropController(0.25f, 40f, out _);
            var heavy = MakeDropController(1.5f, 40f, out _);
            light.Tick(Dt, true);
            heavy.Tick(Dt, true);
            for (int i = 0; i < 30; i++) { light.Tick(Dt, false); heavy.Tick(Dt, false); }

            Assert.Greater(heavy.State.Depth01, light.State.Depth01,
                "the heavy jig must be deeper after the same count — counting the fall is a real read");
        }

        // ---- click to hold the band ---------------------------------------------------------

        [Test]
        public void Click_ReengagesTheReel_AndHoldsTheBand()
        {
            var c = MakeDropController(0.5f, 40f, out _);
            c.Tick(Dt, true);
            for (int i = 0; i < 20; i++) c.Tick(Dt, false);           // fall a while
            c.Tick(Dt, true);                                          // CLICK — re-engage the reel

            Assert.AreEqual(FishingPhase.Waiting, c.Phase, "the reel engages: Waiting at the held band");
            float held = c.State.Depth01;
            Assert.Greater(held, 0f, "we stopped mid-column, not at the surface");
            Assert.Less(held, 1f, "…and short of the floor");

            for (int i = 0; i < 20; i++) c.Tick(Dt, false);           // wait, hands off
            Assert.AreEqual(held, c.State.Depth01, 1e-4f, "the held band does not drift while waiting");
            Assert.IsFalse(c.State.SlackWindowOpen, "mid-column line is not slack");
        }

        // ---- the bottom tell ----------------------------------------------------------------

        [Test]
        public void LetItRun_BottomsOut_AtTheBathymetryCappedFloor_WithTheSlackTell()
        {
            const float column = 8f;                                   // shallow spot — the seabed caps the band
            var c = MakeDropController(1.0f, column, out _);
            c.Tick(Dt, true);

            float t = 0f;
            while (c.Phase == FishingPhase.Sinking && t < 60f) { c.Tick(Dt, false); t += Dt; }

            Assert.AreEqual(FishingPhase.Waiting, c.Phase, "the bottomed rig sits waiting (reel wound on)");
            Assert.AreEqual(1f, c.State.Depth01, 1e-4f, "Depth01 reads the floor of the reachable band");
            Assert.IsTrue(c.State.SlackWindowOpen,
                "the line goes SLACK on the floor — the tell RodLineMath.SlackOvershoot pops on");

            // The floor was the seabed, not the line: 8 m at ~2.5 m/s ≈ 3.2 s, far under the 60 s guard.
            Assert.Less(t, 10f, "an 8 m column must bottom out quickly — the floor is bathymetry-capped");
        }

        [Test]
        public void ReelUpSlightly_LiftsOffTheFloor_AndClosesTheSlack()
        {
            const float column = 8f;
            var c = MakeDropController(1.0f, column, out _);
            c.Tick(Dt, true);
            float t = 0f;
            while (c.Phase == FishingPhase.Sinking && t < 60f) { c.Tick(Dt, false); t += Dt; }
            Assert.IsTrue(c.State.SlackWindowOpen, "precondition: on the floor, slack");

            c.Tick(Dt, false);                                         // settle the press edge
            c.Tick(Dt, true);                                          // HOLD — reel up slightly
            Assert.Less(c.State.Depth01, 1f, "the rig lifts off the floor");
            Assert.IsFalse(c.State.SlackWindowOpen, "off the floor the line comes taut — slack closes");
            Assert.AreEqual(FishingPhase.Waiting, c.Phase, "still fishing the band, just off the bottom");
        }

        // ---- the loop still lands -----------------------------------------------------------

        [Test]
        public void DepthGame_StillLandsAFish_ThroughTheUnchangedCatchPath()
        {
            var c = MakeDropController(1.0f, 8f, out FakeHold hold);
            c.Tick(Dt, true);
            float t = 0f;
            while (c.Phase == FishingPhase.Sinking && t < 60f) { c.Tick(Dt, false); t += Dt; }

            // Wait out the bite, hook, and pace the fight exactly as the legacy tests do.
            t = 0f;
            while (c.Phase == FishingPhase.Waiting || c.Phase == FishingPhase.Bite)
            {
                c.Tick(Dt, false);
                t += Dt;
                Assert.Less(t, 60f, "a bite must arrive at the held depth");
            }
            Assert.AreEqual(FishingPhase.Fighting, c.Phase, "the bite hooks into the (legacy) fight");

            t = 0f;
            while (c.Phase == FishingPhase.Fighting && t < 120f)
            {
                c.Tick(Dt, actionHeld: c.State.Tension01 < 0.5f);      // the forgiving pulse
                t += Dt;
            }
            Assert.AreEqual(FishingPhase.Landed, c.Phase, "the depth game feeds the same cozy landing");
            Assert.AreEqual(1, hold.UsedUnits, "exactly one fish in the hold");
        }
    }
}
