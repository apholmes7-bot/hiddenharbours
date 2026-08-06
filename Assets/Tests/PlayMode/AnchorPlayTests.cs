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
    /// The ground tackle on the LIVE physics loop, over the real Core seams — the half EditMode
    /// structurally cannot see (the restraint is a rigidbody constraint, and the drop gate reads the tide
    /// and the painted seabed through <see cref="GameServices"/>):
    ///
    ///   • THE DROP GATE ON A LIVE TIDE — the same spot anchors at low water and finds no bottom at high
    ///     water, because the depth is recomputed from the clock every time (rule 5). Dry flats refuse
    ///     with "aground", not with a hold, and a longer rode reaches where a short one could not.
    ///   • SHE HOLDS — a stiff beam wind pins an anchored hull inside her swing circle for good, while the
    ///     identical un-anchored hull sails away. The restraint is doing the work, not the hull drag.
    ///   • SHE DRAGS — a making tide climbs past the rode, the hook loses the bottom, and she goes: off
    ///     her berth, but slowly, and far less far than the same boat with no hook down at all. The ebb
    ///     hands the bottom back and she brings up where she fetched to.
    ///
    /// <para><b>Time, not frames.</b> Headless frames run as fast as the machine allows, so every wait
    /// here is on elapsed <see cref="Time.time"/> — never on a frame count.</para>
    /// </summary>
    public class AnchorPlayTests
    {
        /// <summary>A flat painted seabed at a settable elevation (m above chart datum).</summary>
        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation = -4f;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        /// <summary>
        /// A deterministic tide + wind. The water level is a genuine function of <c>totalSeconds</c> — the
        /// same seam the whole game reads — so advancing the clock IS the tide making.
        ///
        /// <para><see cref="Sample"/> reports <c>TideHeight = 0</c> and <c>SeaState01 = 0</c> on purpose:
        /// the first keeps <see cref="BoatController"/>'s placeholder flat-seabed grounding read out of
        /// the way (a separate, pre-existing model that does NOT go through the terrain), the second
        /// zeroes the seakeeping force — so what these tests measure is the anchor and the wind.</para>
        /// </summary>
        private sealed class TidalEnv : IEnvironmentService
        {
            public Vector2 Wind = Vector2.zero;
            public Vector2 Current = Vector2.zero;
            public float BaseLevel;            // water level (m above datum) at t = 0
            public float RisePerSecond;        // a making tide

            public int WorldSeed => 7;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Wind, Current, 0f, SeaState.Calm, 1f, 0f);
            public float TideHeightAt(double totalSeconds) => WaterLevelAt(totalSeconds);
            public float WaterLevelAt(double totalSeconds) => BaseLevel + RisePerSecond * (float)totalSeconds;
        }

        /// <summary>A clock that either stands at a set time (so a test can place the tide exactly) or
        /// runs with real time (so the tide genuinely makes across physics steps).</summary>
        private sealed class TestClock : IGameClock
        {
            public double Fixed;
            public bool Live;
            public double LiveOrigin;

            public double TotalSeconds => Live ? Time.timeAsDouble - LiveOrigin : Fixed;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;

            public void GoLive()
            {
                LiveOrigin = Time.timeAsDouble;
                Live = true;
            }
        }

        private readonly List<Object> _spawned = new();
        private FlatTerrain _terrain;
        private TidalEnv _env;
        private TestClock _clock;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            HelmKeyCapture.Reset();
            EventBus.Clear<ControlModeChanged>();
            _terrain = new FlatTerrain { Elevation = -4f };   // 4 m of water at mean level
            _env = new TidalEnv { BaseLevel = 0f };
            _clock = new TestClock();
            GameServices.TidalTerrain = _terrain;
            GameServices.Environment = _env;
            GameServices.Clock = _clock;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<ControlModeChanged>();
            InteractionGate.Reset();
            HelmKeyCapture.Reset();
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

        /// <summary>A hull with an explicit rode, a shallow draught and real windage.</summary>
        private BoatHullDef Hull(float rode, float windExposure = 150f)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = "boat.anchor_play";
            h.DisplayName = "Anchor Test Dory";
            h.MassKg = 400f;
            h.ForwardDrag = 60f;
            h.LateralDrag = 200f;
            h.WindExposure = windExposure;      // she catches the breeze — the anchor has work to do
            h.DraughtMeters = 0.35f;
            h.RodeMeters = rode;
            _spawned.Add(h);
            return h;
        }

        /// <summary>Build a boat and hand back her tackle. The anchor and its dev key are runtime-spawned
        /// by <see cref="BoatController"/>, so this also proves the self-install: no builder re-run, no
        /// prefab edit, every already-built scene grows the hook on load.</summary>
        private BoatAnchor NewBoat(BoatHullDef hull, Vector3 pos, out Rigidbody2D rb)
        {
            var go = NewGo("Boat", pos);
            var boat = go.AddComponent<BoatController>();   // auto-adds Rigidbody2D + capsule + mooring
            rb = go.GetComponent<Rigidbody2D>();
            boat.SetHull(hull);
            var anchor = go.GetComponent<BoatAnchor>();
            Assert.IsNotNull(anchor, "BoatController must self-install the ground tackle in play mode");
            Assert.IsNotNull(go.GetComponent<DevAnchorInput>(), "…and the greybox key that works it");
            return anchor;
        }

        /// <summary>Wait real seconds of fixed steps (a frame count is not time).</summary>
        private static IEnumerator WaitSeconds(float seconds)
        {
            float deadline = Time.time + seconds;
            while (Time.time < deadline) yield return new WaitForFixedUpdate();
        }

        /// <summary>
        /// How far past the swing circle a reading may legitimately sit. The positional clamp runs inside
        /// <c>BoatAnchor.FixedUpdate</c>; Box2D then integrates ONE more step with the wind's fresh
        /// acceleration on it, so a sample taken between frames can be a couple of centimetres beyond the
        /// clamp without the rode having stretched. Generous enough to be honest, far too small to hide a
        /// boat that is actually sailing away.
        /// </summary>
        private const float ClampSampleTolerance = 0.05f;

        // ================= 1. the drop gate, on a live tide =================

        [UnityTest]
        public IEnumerator DropGate_HonoursDepthVsRode_AcrossTheTide()
        {
            BoatHullDef hull = Hull(rode: 6f, windExposure: 0f);   // no wind: this test is about the gate
            BoatAnchor anchor = NewBoat(hull, Vector3.zero, out Rigidbody2D rb);
            yield return null;                                     // Awake/OnEnable across the rig

            // --- LOW WATER: 1.8 m over a −4 m bottom. The rode reaches easily. ---
            _clock.Fixed = 0.0;
            _env.BaseLevel = -2.2f;
            Assert.AreEqual(1.8f, anchor.DepthOverAnchor, 1e-3f,
                "depth must come from the painted seabed under the live tide, not from a local guess");
            Assert.AreEqual(AnchorDrop.Set, anchor.TryDrop(), "she anchors where the rode reaches bottom");
            Assert.AreEqual(AnchorState.Set, anchor.State);
            Assert.Less(Vector2.Distance(anchor.DropPoint, rb.position), 1e-2f, "the hook goes down where she is");

            anchor.Weigh();
            Assert.AreEqual(AnchorState.Stowed, anchor.State);

            // --- HIGH WATER, SAME SPOT: 6.2 m over the same bottom. The 6 m rode no longer reaches. ---
            _env.BaseLevel = 2.2f;
            Assert.AreEqual(AnchorDrop.NoBottom, anchor.TryDrop(),
                "the very same spot finds no bottom at high water — the depth is recomputed, never stored");
            Assert.AreEqual(AnchorState.Stowed, anchor.State,
                "a refusal changes NOTHING: no drop point, no state, nothing lost");

            // --- THE FLATS at low water: dry ground. You do not anchor aground. ---
            _terrain.Elevation = 1.6f;                             // the sandbar crest
            _env.BaseLevel = -2.2f;
            Assert.AreEqual(AnchorDrop.Aground, anchor.TryDrop());
            Assert.AreEqual(AnchorState.Stowed, anchor.State);

            // --- A LONGER RODE reaches where the short one could not (P2: bigger hull, deeper anchorage). ---
            _terrain.Elevation = -4f;
            _env.BaseLevel = 2.2f;                                 // 6.2 m — refused above on a 6 m rode
            hull.RodeMeters = 30f;                                 // the lobster boat's ground tackle
            Assert.AreEqual(AnchorDrop.Set, anchor.TryDrop(),
                "the rode is DATA: a hull that carries more line anchors where a dory cannot");
        }

        [UnityTest]
        public IEnumerator DropGate_WithNoSeabedPainted_FindsNoBottom()
        {
            GameServices.TidalTerrain = null;                      // "open water" — no height map
            BoatAnchor anchor = NewBoat(Hull(rode: 180f, windExposure: 0f), Vector3.zero, out _);
            yield return null;

            Assert.AreEqual(AnchorDrop.NoBottom, anchor.TryDrop(),
                "with no seabed authored there is no bottom to reach — the tackle refuses rather than " +
                "pretending to hold (the mirror of the crossing gate's never-falsely-block default)");
            Assert.AreEqual(AnchorState.Stowed, anchor.State);
        }

        // ================= 2. she holds =================

        [UnityTest]
        public IEnumerator Anchored_SheHoldsInsideHerSwingCircle_UnderAStiffWind()
        {
            _clock.Fixed = 0.0;
            _env.BaseLevel = 0f;                                   // 4 m of water over the −4 m bottom
            _env.Wind = new Vector2(12f, 0f);                      // a stiff breeze, due east

            BoatHullDef hull = Hull(rode: 6f);
            BoatAnchor anchor = NewBoat(hull, Vector3.zero, out Rigidbody2D rb);

            // The control: the identical hull, the same wind, no hook down. Well clear of the first.
            NewBoat(hull, new Vector3(0f, 60f, 0f), out Rigidbody2D freeRb);
            Vector2 freeStart = freeRb.position;

            yield return null;
            Assert.AreEqual(AnchorDrop.Set, anchor.TryDrop());

            float swing = anchor.SwingRadius;
            Assert.AreEqual(Mathf.Sqrt(36f - 16f), swing, 1e-2f,
                "√(rode² − depth²): 6 m of rode in 4 m of water is ~4.47 m of swing");

            yield return WaitSeconds(5f);                          // a long beam wind, on real fixed steps

            float held = Vector2.Distance(rb.position, anchor.DropPoint);
            float ranAway = Vector2.Distance(freeRb.position, freeStart);

            Assert.LessOrEqual(held, swing + GameServices.Anchor.RodeGiveMeters + ClampSampleTolerance,
                "the rode is INEXTENSIBLE: she can never sit past her swing circle plus its tiny give");
            Assert.Greater(held, swing * 0.5f,
                "…and she really is out at the end of it — a wind this stiff must reach the limit, or " +
                "this test would pass on a boat that never moved at all");
            Assert.AreEqual(AnchorState.Set, anchor.State, "she is still holding — the tide has not moved");
            Assert.Greater(ranAway, swing * 2f,
                "the un-anchored control sails clean away in the same wind, so it is the RESTRAINT " +
                "holding her, not the hull's own drag");
        }

        // ================= 3. she drags =================

        [UnityTest]
        public IEnumerator ARisingTide_TakesTheBottomFromTheHook_AndSheDrags()
        {
            _clock.Fixed = 0.0;
            _env.BaseLevel = 0f;                                   // 4 m of water: a 6 m rode holds
            _env.Wind = new Vector2(12f, 0f);

            BoatHullDef hull = Hull(rode: 6f);
            BoatAnchor anchor = NewBoat(hull, Vector3.zero, out Rigidbody2D rb);
            NewBoat(hull, new Vector3(0f, 60f, 0f), out Rigidbody2D freeRb);   // the control
            Vector2 freeStart = freeRb.position;

            yield return null;
            Assert.AreEqual(AnchorDrop.Set, anchor.TryDrop());
            yield return WaitSeconds(1f);
            Assert.AreEqual(AnchorState.Set, anchor.State, "holding while the water is inside her rode");

            float swing = anchor.SwingRadius;
            Vector2 dropPoint = anchor.DropPoint;

            // --- The tide makes. At 1 m of level a second, 4 m of depth passes a 6 m rode in ~2 s. ---
            _env.RisePerSecond = 1f;
            _clock.GoLive();

            float deadline = Time.time + 10f;                      // elapsed time, never a frame count
            while (anchor.State != AnchorState.Dragging && Time.time < deadline)
                yield return new WaitForFixedUpdate();

            Assert.AreEqual(AnchorState.Dragging, anchor.State,
                "past the rode the hook loses the bottom — the cozy-with-teeth failure");
            Assert.Greater(anchor.DepthOverAnchor, hull.RodeMeters,
                "…and it happened because the water is genuinely over her rode, not for some other reason");

            // --- Dragging is not holding: she leaves her swing circle. But she is not free either. ---
            yield return WaitSeconds(5f);

            float dragged = Vector2.Distance(rb.position, dropPoint);
            float ranAway = Vector2.Distance(freeRb.position, freeStart);

            Assert.Greater(dragged, swing + GameServices.Anchor.RodeGiveMeters + ClampSampleTolerance,
                "a dragging anchor does not hold her — she is past the circle that used to bound her");
            Assert.Greater(ranAway, dragged * 1.5f,
                "…but the tackle still checks her: the same boat with no hook down goes much further");
            Assert.Greater(rb.linearVelocity.magnitude, 0f, "she is genuinely creeping, not stopped dead");
        }

        [UnityTest]
        public IEnumerator TheEbb_HandsTheBottomBack_AndSheBringsUpWhereSheFetchedTo()
        {
            _clock.Fixed = 0.0;
            _env.BaseLevel = 0f;                                   // 4 m: a 6 m rode holds
            _env.Wind = new Vector2(12f, 0f);

            BoatAnchor anchor = NewBoat(Hull(rode: 6f), Vector3.zero, out Rigidbody2D rb);
            yield return null;
            Assert.AreEqual(AnchorDrop.Set, anchor.TryDrop());

            // The flood runs her rode under: 10.5 m over the same bottom is well past 6 m.
            _env.BaseLevel = 6.5f;
            yield return WaitSeconds(0.5f);
            Assert.AreEqual(AnchorState.Dragging, anchor.State);

            yield return WaitSeconds(3f);                          // let her genuinely fetch off her berth
            Vector2 draggedTo = rb.position;
            Assert.Greater(Vector2.Distance(draggedTo, Vector2.zero), 1f,
                "she must actually have moved for 'she brings up where she fetched to' to mean anything");

            // The ebb hands the bottom back and the hook bites again — WHERE SHE HAS FETCHED TO, not at
            // the berth she lost. Dragging costs you your spot, not your anchor.
            _env.BaseLevel = 0f;
            yield return WaitSeconds(0.5f);

            Assert.AreEqual(AnchorState.Set, anchor.State, "the ebb hands the bottom back and she brings up");
            Assert.Less(Vector2.Distance(anchor.DropPoint, draggedTo), 1.5f,
                "the new hold is where she fetched to…");
            Assert.Greater(Vector2.Distance(anchor.DropPoint, Vector2.zero), 1f,
                "…and it is emphatically not the berth she started from");
        }

        // ========= 4. the dev key's gate (headless drops key events; the GATE is what a test can see) =====

        [UnityTest]
        public IEnumerator TheAnchorKey_LivesOnlyFromTheBoat_AndStandsDownUnderADialogue()
        {
            BoatAnchor anchor = NewBoat(Hull(rode: 6f, windExposure: 0f), Vector3.zero, out _);
            var input = anchor.GetComponent<DevAnchorInput>();
            yield return null;

            Assert.AreEqual(UnityEngine.InputSystem.Key.R, input.AnchorKey,
                "R for RODE — the ledger's free letter (C is claimed by InputSystem_Actions' Crouch, " +
                "which a code-only 'Key.' grep does not see)");

            input.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
            Assert.IsFalse(input.AnchorKeyLive, "you do not drop the hook while standing ashore");

            input.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
            Assert.IsTrue(input.AnchorKeyLive, "at the helm the key works the tackle");

            input.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            Assert.IsTrue(input.AnchorKeyLive, "and from her deck too — anchoring is a boat job either way");

            try
            {
                InteractionGate.IsBlocked = true;
                Assert.IsFalse(input.AnchorKeyLive, "a modal dialogue owns the keyboard");
            }
            finally { InteractionGate.Reset(); }

            try
            {
                HelmKeyCapture.Begin();
                Assert.IsFalse(input.AnchorKeyLive,
                    "naming a waypoint 'ROCK LEDGE' must not drop the hook — R is a letter");
            }
            finally { HelmKeyCapture.Reset(); }

            Assert.IsTrue(input.AnchorKeyLive, "and it comes back when the field lets go");
        }
    }
}
