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
    /// Runtime integration for the two playtest boat-handling fixes (the bits the editor can't cover):
    ///
    ///   • FIX 1 — boat controls stop after returning to a scene. After a region hop, something must
    ///     re-enable the active boat's controller + input to match the persisted mode, or the helm goes
    ///     dead. <see cref="ControlSwitcher.ReassertControlMode"/> (which the App RegionTravelCoordinator
    ///     calls on every arrival) restores it — proven here by blanking the controllers (as a scene
    ///     toggle would) and asserting the re-assert brings the controller + input back live, keeps the
    ///     walk frozen, and re-raises the camera signals. Run for both an Oars hull (the Dory) and an
    ///     Engine hull (the Punt). (That an *enabled* controller drives the rigidbody is covered by
    ///     RowingIntegrationPlayTests; the enable-flag wiring is the part this re-bind fix owns.)
    ///
    ///   • FIX 2 — disembark near land parks the boat where it's left (no drift): a making-way boat is
    ///     brought to rest on step-off so it can't coast off / strand itself.
    ///
    /// Mirrors RowingIntegrationPlayTests' minimal in-code harness (no greybox-scene dependency); the
    /// pure logic is covered headless in BoatRebindAndDisembarkTests (EditMode).
    /// </summary>
    public class ControlRebindPlayTests
    {
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
            GameServices.Reset();
            InteractionGate.Reset();
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

        private BoatHullDef NewHull(string id, PropulsionType propulsion)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id; h.DisplayName = "Test Hull"; h.Propulsion = propulsion;
            h.MassKg = 100f;
            h.OarPower = 400f; h.OarLateralOffset = 0.6f; h.OarBraceDrag = 400f;
            h.EnginePower = 1200f; h.RudderAuthority = 600f;
            h.ForwardDrag = 40f; h.LateralDrag = 200f; h.WindExposure = 0f;
            h.DraughtMeters = 0.3f; h.HoldUnits = 6; h.CrewSlots = 1;
            h.CameraWorldHeightMeters = 14f;
            _spawned.Add(h);
            return h;
        }

        // ---- FIX 1: the helm is live again after a re-assert, for both propulsion types ----------

        [UnityTest]
        public IEnumerator ReassertControlMode_AfterToggleBlanksControl_RestoresHelm_Oars()
        {
            yield return Helm_LiveAfterReassert(PropulsionType.Oars);
        }

        [UnityTest]
        public IEnumerator ReassertControlMode_AfterToggleBlanksControl_RestoresHelm_Engine()
        {
            yield return Helm_LiveAfterReassert(PropulsionType.Engine);
        }

        private IEnumerator Helm_LiveAfterReassert(PropulsionType propulsion)
        {
            var dock = new GameObject("DockZone"); dock.transform.position = Vector3.zero; _spawned.Add(dock);
            var disembark = new GameObject("DisembarkPoint"); disembark.transform.position = new Vector3(0f, 1f, 0f); _spawned.Add(disembark);

            var playerGo = new GameObject("Player");
            playerGo.transform.position = dock.transform.position;     // in the board zone
            var walk = playerGo.AddComponent<PlayerWalkController>();
            _spawned.Add(playerGo);

            var boatGo = new GameObject("Boat");
            boatGo.transform.position = new Vector3(0f, -2f, 0f);
            var boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();
            _spawned.Add(boatGo);
            yield return null;                                         // Awakes cache the rigidbodies
            boat.SetHull(NewHull("boat.rebind", propulsion));
            boat.enabled = false; input.enabled = false;

            var sw = new GameObject("ControlSwitcher").AddComponent<ControlSwitcher>();
            _spawned.Add(sw.gameObject);
            // boatInput passed so the re-assert toggles it back on with the controller (the input behaviour
            // just flips enabled with the mode).
            sw.Configure(walk, boat, input, dock.transform, 3.5f, disembark.transform);

            Assert.IsTrue(sw.TryInteract(), "boards in-zone");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);

            // Simulate the scene-toggle bug: the active boat's control is blanked while the mode stays Aboard.
            boat.enabled = false; input.enabled = false; walk.enabled = true;

            var modeEvents = new List<ControlModeChanged>();
            var boatEvents = new List<ActiveBoatChanged>();
            void OnMode(ControlModeChanged e) => modeEvents.Add(e);
            void OnBoat(ActiveBoatChanged e) => boatEvents.Add(e);
            EventBus.Subscribe<ControlModeChanged>(OnMode);
            EventBus.Subscribe<ActiveBoatChanged>(OnBoat);
            try
            {
                // The fix: re-assert on arrival (what RegionTravelCoordinator.ApplyArrival calls). This is
                // the re-bind CONTRACT — after a scene return the active boat's controller + input are live
                // again to match the persisted Aboard mode, the walk stays frozen, and the camera reframes.
                // (That an *enabled* controller drives the rigidbody is covered by RowingIntegrationPlayTests;
                // here we prove the re-assert restores the enabled wiring + signals deterministically.)
                sw.ReassertControlMode();
                Assert.IsTrue(boat.enabled, $"[{propulsion}] the controller is live again after the re-assert (helm restored)");
                Assert.IsTrue(input.enabled, $"[{propulsion}] and so is its input");
                Assert.IsFalse(walk.enabled, $"[{propulsion}] walking stays frozen aboard");
                Assert.AreEqual(ControlMode.Aboard, sw.Mode, $"[{propulsion}] still aboard after the return");
                Assert.IsTrue(modeEvents.Exists(e => e.Mode == ControlMode.Aboard),
                    $"[{propulsion}] ControlModeChanged(Aboard) re-raised so the camera retargets the boat");
                Assert.IsTrue(boatEvents.Exists(e => e.BoatId == "boat.rebind"),
                    $"[{propulsion}] ActiveBoatChanged re-raised so the camera reframes to the hull");
            }
            finally
            {
                EventBus.Unsubscribe<ControlModeChanged>(OnMode);
                EventBus.Unsubscribe<ActiveBoatChanged>(OnBoat);
            }
            yield return null;
        }

        // ---- FIX 2: disembark near land parks the boat where it's left ---------------------------

        [UnityTest]
        public IEnumerator Disembark_OnLand_ParksTheMakingWayBoat()
        {
            // EXPOSED tidal terrain so the boat counts as 'over land' away from the dock (the tightened
            // rule: only standable land, never submerged water). Ground 0.2 m, water level 0 → depth -0.2 m.
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 0.2f };
            GameServices.Environment = new FlatEnv { Level = 0f };   // depth -0.2 m ≤ 0 = standable land

            var dock = new GameObject("DockZone"); dock.transform.position = Vector3.zero; _spawned.Add(dock);
            var disembark = new GameObject("DisembarkPoint"); disembark.transform.position = new Vector3(0f, 1f, 0f); _spawned.Add(disembark);

            var playerGo = new GameObject("Player");
            playerGo.transform.position = dock.transform.position;    // standing in the board zone
            var walk = playerGo.AddComponent<PlayerWalkController>();
            _spawned.Add(playerGo);

            var boatGo = new GameObject("Boat");
            boatGo.transform.position = new Vector3(0f, -2f, 0f);      // moored just off the dock
            var boat = boatGo.AddComponent<BoatController>();
            _spawned.Add(boatGo);
            yield return null;                                        // Awakes cache the rigidbodies
            boat.SetHull(NewHull("boat.park", PropulsionType.Oars));
            boat.enabled = false;

            var sw = new GameObject("ControlSwitcher").AddComponent<ControlSwitcher>();
            _spawned.Add(sw.gameObject);
            sw.Configure(walk, boat, null, dock.transform, 3.5f, disembark.transform);

            Assert.IsTrue(sw.TryInteract(), "boards in-zone");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);

            // Sail off far from the dock, up onto an exposed flat, still making way.
            boatGo.transform.position = new Vector3(40f, 40f, 0f);
            var rb = boatGo.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(40f, 40f);
            rb.linearVelocity = new Vector2(3f, -2f);
            rb.angularVelocity = 25f;

            Assert.IsTrue(sw.OnLand(), "the boat is over standable land (an exposed flat)");
            Assert.IsTrue(sw.TryInteract(), "disembark succeeds onto land, away from the dock");
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);

            // The player HOLDS the rope on disembark: the boat is brought to rest and tethered to the player
            // (who stepped off at the boat). With dead-calm weather (zero wind/current) it sits quiet on the
            // slack rope and doesn't coast off / drift away.
            Assert.AreEqual(Vector2.zero, rb.linearVelocity, "the boat is brought to rest on disembark (held)");
            Assert.AreEqual(0f, rb.angularVelocity, 1e-4f);
            Vector2 parkedAt = rb.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.Less((rb.position - parkedAt).magnitude, 0.01f, "it stays put on the slack rope in calm weather");
        }
    }
}
