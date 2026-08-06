using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>Mooring lines on the live physics loop</b> (M2-38): a made-fast line genuinely holds a hull
    /// against wind and tide, a slack one still lets her range, a falling tide out-runs a short line and
    /// the loop slips, and casting off gives her back to the sea.
    ///
    /// <para>The point of running these in PlayMode rather than against the pure helpers is that the
    /// constraint has to survive the real integrator: the drift force is applied every fixed step and the
    /// rope restrains the RESULT (rule 5 — the sim keeps computing; the rope is never a freeze). A pure
    /// test cannot show that difference.</para>
    ///
    /// <para><b>Frame counts are not time.</b> Every wait below is real fixed steps, and every duration is
    /// accumulated from <see cref="Time.fixedDeltaTime"/> rather than assumed from an iteration count.
    /// The tide is injected through a Core-contract double, so nothing here depends on the real
    /// environment service or on wall-clock.</para>
    /// </summary>
    public class MooringLinePlayTests
    {
        /// <summary>A deterministic sea with a settable water level and a steady breeze — the two inputs
        /// the mooring reads. No RNG anywhere.</summary>
        private sealed class TestSea : IEnvironmentService
        {
            public Vector2 Wind = new Vector2(8f, 0f);   // a stiff breeze blowing due EAST, away from the wharf
            public float Level;                          // metres above chart datum

            public int WorldSeed => 7;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Wind, Vector2.zero, Level, SeaState.Calm, 1f, 0f);
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        /// <summary>A hull cleat that rides the water, like the real <see cref="BoatCleats"/> one: it
        /// tracks the boat's transform and its elevation is the live water level plus its freeboard.</summary>
        private sealed class FloatingCleat : IMooringCleat
        {
            private readonly Transform _boat;
            private readonly TestSea _sea;
            private readonly float _freeboard;
            private readonly Vector2 _offset;

            public FloatingCleat(Transform boat, TestSea sea, float freeboard, Vector2 offset = default)
            {
                _boat = boat; _sea = sea; _freeboard = freeboard; _offset = offset;
            }

            public string Id => "boat.test.bow_1";
            public CleatSide Side => CleatSide.Boat;
            public Vector2 WorldPosition => _boat != null ? (Vector2)_boat.position + _offset : _offset;
            public float ElevationMeters => MooringLineMath.BoatCleatElevation(_sea.Level, _freeboard);
        }

        /// <summary>A bollard: it does not move, and its height does not change with the tide. That
        /// asymmetry against <see cref="FloatingCleat"/> is the whole mechanic.</summary>
        private sealed class Bollard : IMooringCleat
        {
            private readonly Vector2 _pos;
            private readonly float _elevation;
            public Bollard(Vector2 pos, float elevation) { _pos = pos; _elevation = elevation; }
            public string Id => "wharf.test.bollard_0";
            public CleatSide Side => CleatSide.Shore;
            public Vector2 WorldPosition => _pos;
            public float ElevationMeters => _elevation;
        }

        // St Peters' real geometry: the pier deck measured at +5.35 m, the tide swinging ±2.2 m.
        private const float DeckElevation = 5.35f;
        private const float HighWater = 2.2f;
        private const float LowWater = -2.2f;
        private const float CleatFreeboard = 0.6f;

        private readonly List<Object> _spawned = new();
        private TestSea _sea;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            MooringCleats.Clear();
            CastActionClaim.Reset();
            EventBus.Clear<MooringLineChanged>();
            _sea = new TestSea { Level = HighWater };
            GameServices.Environment = _sea;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<MooringLineChanged>();
            MooringCleats.Clear();
            CastActionClaim.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private GameObject NewGo(string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            _spawned.Add(g);
            return g;
        }

        /// <summary>A boat that the wind will work on, with her mooring dormant until a line is made fast.</summary>
        private (BoatMooring mooring, Rigidbody2D rb, Transform tf) NewBoat(Vector3 at)
        {
            var go = NewGo("Boat", at);
            var boat = go.AddComponent<BoatController>();      // auto-adds Rigidbody2D + collider + mooring
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.moor_test";
            hull.DisplayName = "Mooring Test Dory";
            hull.MassKg = 400f;
            hull.ForwardDrag = 60f;
            hull.LateralDrag = 200f;
            hull.WindExposure = 30f;                          // she catches the breeze
            hull.DraughtMeters = 0.35f;
            _spawned.Add(hull);
            boat.SetHull(hull);
            // Nobody at the helm: the controller's manned force pass stays off, and the sea works her
            // through the mooring's own drift tick — which is exactly the un-frozen behaviour rule 5 wants.
            boat.enabled = false;

            return (go.GetComponent<BoatMooring>(), go.GetComponent<Rigidbody2D>(), go.transform);
        }

        /// <summary>Run real fixed steps until <paramref name="seconds"/> of simulated time have passed —
        /// accumulated from the real step, never inferred from an iteration count.</summary>
        private static IEnumerator RunSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }
        }

        // ============ 1. a made-fast line HOLDS her ============

        [UnityTest]
        public IEnumerator MadeFast_HoldsHerCleatInsideTheReach_AgainstAStiffBreeze()
        {
            var bollard = new Bollard(new Vector2(0f, 0f), DeckElevation);
            var boat = NewBoat(new Vector3(1f, 0f, 0f));
            yield return null;                                    // Awake/OnEnable across the rig

            var cleat = new FloatingCleat(boat.tf, _sea, CleatFreeboard);
            // A 4 m line at high water: ~3.1 m of reach across the water once the drop has taken its
            // share. Deliberately SHORT, so the constraint is genuinely load-bearing in this test — with
            // the default 9 m scope she could not reach the limit in the time available and the assertion
            // would pass whether the rope worked or not.
            Assert.IsTrue(boat.mooring.MakeFast(cleat, bollard, 4f), "the loop caught");
            float reach = boat.mooring.HorizontalReachMetres;
            Assert.Greater(reach, 0f, "a 4 m line still reaches across at high water");

            // Drive her hard offshore ON TOP of the breeze: unchecked, this puts her tens of metres out.
            boat.rb.linearVelocity = new Vector2(12f, 0f);
            yield return RunSeconds(4f);

            float held = Vector2.Distance(cleat.WorldPosition, bollard.WorldPosition);
            Assert.LessOrEqual(held, reach + 0.5f,
                               $"a made-fast line must hold her CLEAT inside the reach the tide leaves " +
                               $"({reach:0.00} m), not let her drive away downwind (she is at {held:0.00} m)");
            Assert.AreEqual(MooringState.MadeFastToCleat, boat.mooring.State, "…and she is still tied");
        }

        [UnityTest]
        public IEnumerator MadeFast_TheSeaKeepsWorkingHer_TheRopeIsARestraintNotAFreeze()
        {
            var bollard = new Bollard(Vector2.zero, DeckElevation);
            var boat = NewBoat(new Vector3(0.2f, 0f, 0f));
            yield return null;

            var cleat = new FloatingCleat(boat.tf, _sea, CleatFreeboard);
            boat.mooring.MakeFast(cleat, bollard, 9f);

            Vector2 start = boat.tf.position;
            yield return RunSeconds(3f);
            Vector2 end = boat.tf.position;

            // SLACK SCOPE PERMITS MOTION: she started well inside the circle, so the wind must have moved
            // her. A rope that froze the hull the moment it was made fast would be the bug (rule 5).
            Assert.Greater(Vector2.Distance(start, end), 0.25f,
                           "an idle hull on a slack line still SETS with the weather — the sim keeps " +
                           "computing and the rope only restrains the result");
        }

        // ============ 2. the tide law, on the live loop ============

        [UnityTest]
        public IEnumerator FallingTide_EatsAShortLinesReach_UntilTheLoopSlips()
        {
            var bollard = new Bollard(Vector2.zero, DeckElevation);
            var boat = NewBoat(new Vector3(1f, 0f, 0f));
            yield return null;

            var cleat = new FloatingCleat(boat.tf, _sea, CleatFreeboard);

            // Snug her up at HIGH water — 4 m looks perfectly seamanlike right now.
            Assert.IsTrue(boat.mooring.MakeFast(cleat, bollard, 4f));
            Assert.Greater(boat.mooring.HorizontalReachMetres, 0f,
                           "at high water a 4 m line still reaches across the water");

            var slips = new List<MooringLineChanged>();
            void OnLine(MooringLineChanged e) { if (e.Event == MooringLineEvent.Slipped) slips.Add(e); }
            EventBus.Subscribe<MooringLineChanged>(OnLine);
            try
            {
                // …and the tide goes out from under her.
                _sea.Level = LowWater;
                yield return new WaitForFixedUpdate();

                Assert.AreEqual(0f, boat.mooring.HorizontalReachMetres, 1e-4f,
                                "the drop alone has eaten the whole line — she is HANGING on it");

                // The overload has to be SUSTAINED: one snatching wave must not cost the boat, so nothing
                // happens inside the grace period.
                Assert.AreEqual(MooringState.MadeFastToCleat, boat.mooring.State,
                                "she does not go the instant the line comes up hard");

                yield return RunSeconds(MooringLineSettings.Default.SlipGraceSeconds + 1f);

                Assert.AreEqual(MooringState.Stowed, boat.mooring.State,
                                "past the working load, for longer than the grace, the loop slips");
                Assert.AreEqual(1, slips.Count, "…and it says so exactly once");
                Assert.Greater(slips[0].Load01, 1f, "the load that finally lost her is reported");
            }
            finally { EventBus.Unsubscribe<MooringLineChanged>(OnLine); }
        }

        [UnityTest]
        public IEnumerator AWellPaidOutLine_RidesTheSameEbbOut()
        {
            var bollard = new Bollard(Vector2.zero, DeckElevation);
            var boat = NewBoat(new Vector3(1f, 0f, 0f));
            yield return null;

            var cleat = new FloatingCleat(boat.tf, _sea, CleatFreeboard);
            boat.mooring.MakeFast(cleat, bollard, MooringLineSettings.Default.DefaultScopeMetres);

            float reachAtHigh = boat.mooring.HorizontalReachMetres;

            _sea.Level = LowWater;
            yield return RunSeconds(MooringLineSettings.Default.SlipGraceSeconds + 2f);

            Assert.AreEqual(MooringState.MadeFastToCleat, boat.mooring.State,
                            "the starting scope must survive an ordinary ebb — the teeth are for the " +
                            "player's own choice to snug her up, not for mooring at all");
            Assert.Greater(boat.mooring.HorizontalReachMetres, 0f, "…and she is never left hanging");
            Assert.Less(boat.mooring.HorizontalReachMetres, reachAtHigh,
                        "…but she IS drawn in as the water goes — the tell the player learns to read");
        }

        [UnityTest]
        public IEnumerator SlackeningBeforeTheEbb_SavesHer()
        {
            var bollard = new Bollard(Vector2.zero, DeckElevation);
            var boat = NewBoat(new Vector3(1f, 0f, 0f));
            yield return null;

            var cleat = new FloatingCleat(boat.tf, _sea, CleatFreeboard);
            boat.mooring.MakeFast(cleat, bollard, 4f);          // the snug line that would be lost

            // The seamanship: pay out scope before the tide does its work.
            for (int i = 0; i < 6; i++) boat.mooring.AdjustScope(+1);
            Assert.AreEqual(10f, boat.mooring.ScopeMetres, 1e-3f, "six steps of a metre each");

            _sea.Level = LowWater;
            yield return RunSeconds(MooringLineSettings.Default.SlipGraceSeconds + 2f);

            Assert.AreEqual(MooringState.MadeFastToCleat, boat.mooring.State,
                            "the same tide that took the snug line leaves the slackened one alone — " +
                            "which is the whole reward P1 is teaching");
        }

        // ============ 3. cast off ============

        /// <summary>
        /// Cast-off frees her, proved as an A/B on the SAME shove: a hull driven hard away from the
        /// bollard is stopped by the line, and once the line is gone the identical shove carries her clear.
        /// The push is explicit rather than left to the breeze because a stowed mooring no longer runs a
        /// drift tick at all — asserting on wind here would be testing nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator CastOff_GivesHerBackToTheSea()
        {
            var bollard = new Bollard(Vector2.zero, DeckElevation);
            var boat = NewBoat(new Vector3(1f, 0f, 0f));
            yield return null;

            var cleat = new FloatingCleat(boat.tf, _sea, CleatFreeboard);
            boat.mooring.MakeFast(cleat, bollard, 9f);
            float reachWhileTied = boat.mooring.HorizontalReachMetres;

            // (a) TIED: shove her hard offshore and the line checks her.
            boat.rb.linearVelocity = new Vector2(12f, 0f);
            yield return RunSeconds(3f);
            float heldAt = Vector2.Distance(cleat.WorldPosition, bollard.WorldPosition);
            Assert.LessOrEqual(heldAt, reachWhileTied + 0.5f,
                               "even driven hard, a made-fast hull is checked at the end of her line");

            // (b) CAST OFF: the identical shove, and now nothing is holding her.
            Assert.IsTrue(boat.mooring.CastOff());
            Assert.AreEqual(MooringState.Stowed, boat.mooring.State);
            Assert.IsNull(boat.mooring.BoatCleat, "the line's ends are let go with it");

            boat.rb.linearVelocity = new Vector2(12f, 0f);
            yield return RunSeconds(3f);

            Assert.Greater(Vector2.Distance(cleat.WorldPosition, bollard.WorldPosition),
                           reachWhileTied + 1f,
                           "cast off, she is free to leave the circle the line had held her inside of");
        }
    }
}
