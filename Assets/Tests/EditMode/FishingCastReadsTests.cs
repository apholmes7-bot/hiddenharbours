using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The presenter-wave PUBLISHES from <see cref="FishingController"/> — the reads the rod/line/bobber
    /// presentation runs on: the live wind-back charge + aim (per tick, so the castBack sheet can
    /// scrub), the cast's flight progress, the resting bobber's far end through Waiting/Bite (tracking a
    /// walking angler), the legacy fight's far end, the weighted path's raw rig depth — and the pinned
    /// neutrality everywhere else (the v2 fight keeps FishOffset as its ONLY far-end read; results and
    /// idle publish nothing). Plus seeded determinism over the whole new surface.
    /// </summary>
    public class FishingCastReadsTests
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
        private readonly List<FishingState> _published = new();

        private void OnState(FishingStateChanged e) => _published.Add(e.State);

        [SetUp]
        public void SetUp()
        {
            _published.Clear();
            EventBus.Clear<FishingStateChanged>();
            EventBus.Subscribe<FishingStateChanged>(OnState);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<FishingStateChanged>(OnState);
            EventBus.Clear<FishingStateChanged>();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishSpeciesDef MakeFish(string id)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = id; f.Category = FishCategory.InshoreGroundfish;
            f.RegionIds = new[] { "region.coddle_cove" };
            f.AllowedGear = Gear.Handline | Gear.Longline;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f;
            f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        private FishingController MakeController(int seed)
        {
            var go = new GameObject("CastReads");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(new FakeHold(), new[] { MakeFish("fish.legacy") }, "region.coddle_cove",
                        Gear.Handline, seed);
            return c;
        }

        // ---- the wind-back ---------------------------------------------------------------------

        [Test]
        public void TheWindBack_PublishesEveryTick_ChargeGrows_AimPointsAwayFromTheDrag()
        {
            var c = MakeController(seed: 7);
            Vector2 a = c.transform.position;

            c.Tick(0.02f, true, a + new Vector2(0f, -0.5f), true);    // press — the wind-back starts
            int afterPress = _published.Count;
            Assert.GreaterOrEqual(afterPress, 1, "the press itself publishes WindBack");
            float earlyCharge = _published[_published.Count - 1].CastCharge01;

            c.Tick(0.02f, true, a + new Vector2(0f, -2.0f), true);    // drag further behind
            c.Tick(0.02f, true, a + new Vector2(0f, -3.5f), true);
            Assert.Greater(_published.Count, afterPress, "every held tick publishes (the scrub read)");

            FishingState deep = _published[_published.Count - 1];
            Assert.AreEqual(FishingPhase.WindBack, deep.Phase);
            Assert.Greater(deep.CastCharge01, earlyCharge, "winding further back loads the rod further");
            Assert.Greater(deep.CastAimY, 0f, "dragged BELOW the angler, the cast aims ABOVE (opposite)");
            Assert.AreEqual(0f, deep.CastAimX, 1e-3f, "a straight-down drag aims straight up");
            Assert.AreEqual(0f, deep.FishOffsetX, "the fight-only read stays neutral (pinned contract)");
            Assert.AreEqual(0f, deep.RigDepthM, "no rig in the water yet");
        }

        // ---- the cast flight -------------------------------------------------------------------

        [Test]
        public void TheCast_PublishesFlightProgress_TheFarEndFliesOutToTheLanding()
        {
            var c = MakeController(seed: 7);
            FlickGestures.Flick(c);
            Assert.AreEqual(FishingPhase.Cast, c.Phase, "the harness flick must fly");
            Vector2 landing = c.LastCast.LandingPoint - (Vector2)c.transform.position;

            _published.Clear();
            float prevCharge = -1f;
            float prevReach = -1f;
            while (c.Phase == FishingPhase.Cast)
            {
                c.Tick(0.02f, false);
                if (c.Phase != FishingPhase.Cast) break;
                FishingState s = _published[_published.Count - 1];
                Assert.GreaterOrEqual(s.CastCharge01, prevCharge, "flight progress is monotonic");
                float reach = new Vector2(s.CastAimX, s.CastAimY).magnitude;
                Assert.GreaterOrEqual(reach, prevReach - 1e-4f, "the far end flies OUT, never back");
                Assert.LessOrEqual(reach, landing.magnitude + 1e-3f, "never past the landing point");
                prevCharge = s.CastCharge01;
                prevReach = reach;
            }
            Assert.Greater(prevCharge, 0.5f, "the flight was actually published while in the air");

            // Touchdown: Waiting carries the bobber's resting far end — the landing point exactly.
            FishingState waiting = _published[_published.Count - 1];
            Assert.AreEqual(FishingPhase.Waiting, waiting.Phase);
            Assert.AreEqual(landing.x, waiting.CastAimX, 1e-3f);
            Assert.AreEqual(landing.y, waiting.CastAimY, 1e-3f);
        }

        [Test]
        public void TheWait_PublishesPerTick_AndTheAimTracksAWalkingAngler()
        {
            var c = MakeController(seed: 7);
            FlickGestures.CastLine(c);
            Assert.AreEqual(FishingPhase.Waiting, c.Phase);
            Vector2 landing = c.LastCast.LandingPoint;

            _published.Clear();
            c.Tick(0.02f, false);
            Assert.AreEqual(1, _published.Count, "Waiting publishes every tick now (the live line)");

            // The angler strolls a metre east — the published aim must swing, because it is measured
            // from the LIVE transform each publish (the walking-angler rule the fight already follows).
            c.transform.position += new Vector3(1f, 0f, 0f);
            c.Tick(0.02f, false);
            FishingState moved = _published[_published.Count - 1];
            Assert.AreEqual(landing.x - c.transform.position.x, moved.CastAimX, 1e-3f);
            Assert.AreEqual(landing.y - c.transform.position.y, moved.CastAimY, 1e-3f);
        }

        // ---- the legacy fight ---------------------------------------------------------------------

        [Test]
        public void TheLegacyFight_CarriesTheBobberFarEnd_TheV2ContractUntouched()
        {
            var c = MakeController(seed: 999);
            FlickGestures.CastLine(c);
            for (float t = 0f; c.Phase != FishingPhase.Fighting && t < 30f; t += 0.05f)
                c.Tick(0.05f, false);
            Assert.AreEqual(FishingPhase.Fighting, c.Phase, "a species with no Def fights the legacy fight");

            FishingState s = c.State;
            Vector2 expected = c.LastCast.LandingPoint - (Vector2)c.transform.position;
            Assert.AreEqual(expected.x, s.CastAimX, 1e-3f, "she fights AT the bobber — the line has " +
                "somewhere to point even in the legacy fight");
            Assert.AreEqual(expected.y, s.CastAimY, 1e-3f);
            Assert.AreEqual(0f, s.FishOffsetX, "the fight-only FishOffset contract is untouched here");
        }

        // ---- the weighted/depth path ----------------------------------------------------------------

        [Test]
        public void TheDrop_PublishesRawRigDepth_AndNoCastAim()
        {
            var c = MakeController(seed: 21);
            c.ConfigureDepthDrop(rigWeightKg: 2f, waterColumnMeters: 5f);

            c.Tick(0.02f, true);    // press — the weighted rig drops, no pointer needed
            c.Tick(0.02f, false);
            Assert.AreEqual(FishingPhase.Sinking, c.Phase);

            float prevDepth = 0f;
            for (float t = 0f; c.Phase == FishingPhase.Sinking && t < 30f; t += 0.05f)
            {
                c.Tick(0.05f, false);
                FishingState s = c.State;
                Assert.GreaterOrEqual(s.RigDepthM, prevDepth, "the raw depth read falls monotonically");
                Assert.AreEqual(0f, s.CastAimX, "the weighted path has no cast-path far end");
                Assert.AreEqual(0f, s.CastAimY);
                prevDepth = s.RigDepthM;
            }
            Assert.AreEqual(FishingPhase.Waiting, c.Phase, "the rig bottomed out into the wait");
            Assert.AreEqual(5f, c.State.RigDepthM, 0.2f, "resting on the 5 m floor, the read says so");
            Assert.IsTrue(c.State.SlackWindowOpen, "…with the hit-bottom slack tell");
        }

        // ---- determinism over the whole new surface --------------------------------------------------

        [Test]
        public void SameSeed_SameGesture_SameCastReads_BitForBit()
        {
            List<(FishingPhase, float, float, float, float)> Run()
            {
                _published.Clear();
                var c = MakeController(seed: 90210);
                FlickGestures.CastLine(c);
                for (int i = 0; i < 100; i++) c.Tick(0.05f, false);
                var trace = new List<(FishingPhase, float, float, float, float)>();
                foreach (FishingState s in _published)
                    trace.Add((s.Phase, s.CastCharge01, s.CastAimX, s.CastAimY, s.RigDepthM));
                return trace;
            }

            var first = Run();
            var second = Run();
            Assert.AreEqual(first.Count, second.Count, "the seeded runs publish in lockstep");
            for (int i = 0; i < first.Count; i++)
                Assert.AreEqual(first[i], second[i], $"cast reads diverged at publish {i}");
        }
    }
}
