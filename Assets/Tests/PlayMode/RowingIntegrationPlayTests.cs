using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// On-water integration for the rowed dory, built from a MINIMAL in-code harness (no dependence on
    /// the greybox scene layout):
    ///   (1) the full board → row → disembark loop driven through the Core control seam
    ///       (<see cref="ControlSwitcher"/> + <see cref="ControlModeChanged"/>), proving a per-oar-table
    ///       left-only stroke drives the live rigidbody and yaws the bow the specced way; and
    ///   (2) a determinism guard — identical per-oar inputs over identical fixed steps produce identical
    ///       motion, so there is no hidden RNG in the rowing force path (rule 5; ADR 0003/charter).
    /// The pure thrust/yaw/mapping math is covered headless in EditMode (BoatHandlingTests); this covers
    /// the runtime physics + control wiring the editor can't. Environment is left null (GameServices.Reset),
    /// so wind/current/tide are zero and the motion under test is pure rowing.
    /// </summary>
    public class RowingIntegrationPlayTests
    {
        // Minimal exposed-land fakes so a disembark step can pass the disembark-only-on-land gate. Wired
        // only right before the disembark (the rowing motion under test stays env-free).
        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FlatEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();              // null environment → zero wind/current/tide (deterministic rowing)
            InteractionGate.Reset();           // no modal dialogue blocking the Interact seam
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            InteractionGate.Reset();
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // A rowed hull tuned for brisk, clearly-readable motion (mass 100 kg → rb mass 1).
        private BoatHullDef NewOarHull(string id)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id; h.DisplayName = "Test Oar Hull";
            h.Propulsion = PropulsionType.Oars;
            h.MassKg = 100f;
            h.OarPower = 400f; h.OarLateralOffset = 0.6f; h.OarBraceDrag = 400f;
            h.ForwardDrag = 40f; h.LateralDrag = 200f; h.WindExposure = 0f;
            h.DraughtMeters = 0.3f; h.HoldUnits = 6; h.CrewSlots = 1;
            h.CameraWorldHeightMeters = 14f;
            _spawned.Add(h);
            return h;
        }

        private (GameObject go, BoatController boat) NewBoat(Vector3 pos)
        {
            var go = new GameObject("Boat");
            go.transform.position = pos;
            var boat = go.AddComponent<BoatController>();   // RequireComponent → Rigidbody2D + CapsuleCollider2D
            _spawned.Add(go);
            return (go, boat);
        }

        // ---- (1) the on-water loop: board → row (per-oar table) → disembark ------------------

        [UnityTest]
        public IEnumerator OnWaterLoop_BoardRowDisembark_LeftStrokeYawsToStarboard()
        {
            var dock = new GameObject("DockZone"); dock.transform.position = Vector3.zero; _spawned.Add(dock);
            var disembark = new GameObject("DisembarkPoint"); disembark.transform.position = new Vector3(0f, 1f, 0f); _spawned.Add(disembark);

            var playerGo = new GameObject("Player");
            playerGo.transform.position = dock.transform.position;       // standing in the board zone
            var walk = playerGo.AddComponent<PlayerWalkController>();    // RequireComponent → Rigidbody2D + SpriteRenderer
            _spawned.Add(playerGo);

            var (boatGo, boat) = NewBoat(new Vector3(0f, -2f, 0f));      // moored just off the dock
            var hull = NewOarHull("boat.loop");
            yield return null;                                          // Awakes cache the rigidbodies
            boat.SetHull(hull);
            boat.enabled = false;                                       // start moored, like the greybox

            var sw = new GameObject("ControlSwitcher").AddComponent<ControlSwitcher>();
            _spawned.Add(sw.gameObject);
            // boatInput = null: we drive the per-oar input directly via SetOarInput below (the table is
            // covered headless by OarStateFor's EditMode tests), so no DevBoatInput keyboard read can
            // overwrite it. ControlSwitcher null-guards the boat-input behaviour.
            sw.Configure(walk, boat, null, dock.transform, 3.5f, disembark.transform);

            var modeEvents = new List<ControlModeChanged>();
            void OnMode(ControlModeChanged e) => modeEvents.Add(e);
            EventBus.Subscribe<ControlModeChanged>(OnMode);
            try
            {
                Assert.AreEqual(ControlMode.OnFoot, sw.Mode, "the loop starts on foot");

                // --- BOARD: the player is within reach of the boat (board from anywhere) ---
                Assert.IsTrue(sw.WithinBoardReach(), "the player is within reach of the boat");
                Assert.IsTrue(sw.TryInteract(), "boarding succeeds within reach");
                Assert.AreEqual(ControlMode.Aboard, sw.Mode, "now aboard");
                Assert.IsTrue(boat.enabled, "the boat controller drives while aboard");
                Assert.IsTrue(modeEvents.Exists(e => e.Mode == ControlMode.Aboard),
                    "ControlModeChanged(Aboard) fired on the Core control seam");

                // --- ROW: the per-oar table maps a left-only stroke (W+A) to (port +1, starboard 0) ---
                var (left, right) = DevBoatInput.OarStateFor(ahead: true, astern: false, portKey: true, stbdKey: false);
                Assert.AreEqual(1f, left, 1e-4f, "W+A maps to a port-oar-only forward stroke (left +1)");
                Assert.AreEqual(0f, right, 1e-4f, "…with the starboard oar idle (0)");

                var rb = boatGo.GetComponent<Rigidbody2D>();
                Vector2 startPos = rb.position;
                for (int i = 0; i < 16; i++)
                {
                    boat.SetOarInput(left, right, false);
                    yield return new WaitForFixedUpdate();
                }

                // Spec (#35): left oar forward → bow swings RIGHT / stern left = clockwise = negative ω.
                // (The sign of the driven rotation is robust regardless of how far it turned.)
                Assert.Less(rb.angularVelocity, 0f, "a left-only stroke yaws the bow to starboard (clockwise), the specced way");
                Assert.Greater((rb.position - startPos).magnitude, 0.02f, "the stroke drives the boat off the mooring (makes way)");

                // --- DISEMBARK: park the boat at the mooring (over standable land) and step off ---
                GameServices.TidalTerrain = new FlatTerrain { Elevation = 0.2f };  // ground bared (exposed)
                GameServices.Environment = new FlatEnv { Level = 0f };             // depth -0.2 m ≤ 0 = land
                boat.SetOarInput(0f, 0f, false);                         // ship the oars
                rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f;
                boatGo.transform.position = dock.transform.position;     // teleport both transform + body to the mooring
                rb.position = dock.transform.position;
                Assert.IsTrue(sw.OnLand(), "the parked boat is over standable land");
                Assert.IsTrue(sw.TryInteract(), "disembarking succeeds onto land");
                Assert.AreEqual(ControlMode.OnFoot, sw.Mode, "back on foot");
                Assert.IsFalse(boat.enabled, "the boat controller is released on foot");
                Assert.IsTrue(modeEvents.Exists(e => e.Mode == ControlMode.OnFoot),
                    "ControlModeChanged(OnFoot) fired on disembark");
            }
            finally { EventBus.Unsubscribe<ControlModeChanged>(OnMode); }
        }

        // ---- (2) determinism: same inputs + dt → identical motion (no hidden RNG) ------------

        [UnityTest]
        public IEnumerator RowingPhysics_SameInputsAndDt_ProduceIdenticalMotion()
        {
            // Two identical boats, far apart so their hull colliders never interact, driven by the SAME
            // per-oar schedule in lockstep over the same fixed steps. With no RNG in the rowing force path
            // they must move identically. (This guards rule 5 for one session; not a cross-platform bit
            // claim — the charter says boat physics need not be bit-deterministic across runs.)
            var (goA, boatA) = NewBoat(new Vector3(0f, 0f, 0f));
            var (goB, boatB) = NewBoat(new Vector3(1000f, 0f, 0f));
            var hull = NewOarHull("boat.determinism");
            yield return null;                                          // Awakes cache the rigidbodies
            boatA.SetHull(hull); boatB.SetHull(hull);
            var rbA = goA.GetComponent<Rigidbody2D>();
            var rbB = goB.GetComponent<Rigidbody2D>();
            Vector2 startA = rbA.position, startB = rbB.position;

            // A representative motion that exercises thrust, yaw, astern, and brace. Front-loaded with a
            // clean straight-ahead push (no yaw) so the net displacement is unambiguously non-vacuous.
            var schedule = new (float l, float r, bool brace, int steps)[]
            {
                (1f,  1f,  false, 20),   // both oars ahead — straight, clear forward displacement
                (1f,  0f,  false, 12),   // port-only — adds a starboard yaw
                (-1f, -1f, false, 8),    // both oars astern
                (1f, -1f,  true,  6),    // a braced pivot
            };

            foreach (var phase in schedule)
                for (int i = 0; i < phase.steps; i++)
                {
                    boatA.SetOarInput(phase.l, phase.r, phase.brace);
                    boatB.SetOarInput(phase.l, phase.r, phase.brace);
                    yield return new WaitForFixedUpdate();
                }

            Vector2 dispA = rbA.position - startA, dispB = rbB.position - startB;
            Assert.Greater(dispA.magnitude, 0.1f, "the schedule must produce real motion (a non-vacuous determinism check)");

            // Tight enough to expose any RNG in the force path (that would diverge by metres), loose
            // enough to ignore sub-millimetre float noise.
            Assert.AreEqual(dispA.x, dispB.x, 1e-3f, "X displacement is identical for identical inputs (no hidden RNG)");
            Assert.AreEqual(dispA.y, dispB.y, 1e-3f, "Y displacement is identical");
            Assert.AreEqual(rbA.rotation, rbB.rotation, 1e-3f, "heading is identical");
            Assert.AreEqual(rbA.linearVelocity.x, rbB.linearVelocity.x, 1e-3f, "X velocity is identical");
            Assert.AreEqual(rbA.linearVelocity.y, rbB.linearVelocity.y, 1e-3f, "Y velocity is identical");
            Assert.AreEqual(rbA.angularVelocity, rbB.angularVelocity, 1e-3f, "angular velocity is identical");
        }
    }
}
