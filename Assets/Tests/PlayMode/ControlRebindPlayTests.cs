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
    ///     calls on every arrival) restores it — proven here by re-asserting after the controllers were
    ///     blanked (as a scene toggle would) and then driving the live rigidbody with oar input. Run for
    ///     both an Oars hull (the Dory) and an Engine hull (the Punt).
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
        public IEnumerator ReassertControlMode_AfterToggleBlanksControl_HelmDrivesLiveAgain_Oars()
        {
            yield return Helm_LiveAfterReassert(PropulsionType.Oars);
        }

        [UnityTest]
        public IEnumerator ReassertControlMode_AfterToggleBlanksControl_HelmDrivesLiveAgain_Engine()
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
            // boatInput passed so the re-assert toggles it; we still drive the physics directly below so a
            // keyboard read can't interfere (the input behaviour just flips enabled with the mode).
            sw.Configure(walk, boat, input, dock.transform, 3.5f, disembark.transform);

            Assert.IsTrue(sw.TryInteract(), "boards in-zone");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);

            // Simulate the scene-toggle bug: the active boat's control is blanked while the mode stays Aboard.
            boat.enabled = false; input.enabled = false; walk.enabled = true;

            // The fix: re-assert on arrival (what RegionTravelCoordinator.ApplyArrival calls).
            sw.ReassertControlMode();
            Assert.IsTrue(boat.enabled, $"[{propulsion}] the controller is live again after the re-assert");
            Assert.IsTrue(input.enabled, $"[{propulsion}] and so is its input");
            Assert.IsFalse(walk.enabled, $"[{propulsion}] walking stays frozen aboard");

            // Prove the live controller actually drives the rigidbody (the helm isn't merely 'enabled').
            // Assert on VELOCITY GAINED under sustained input — the unambiguous "the helm is live and
            // applying thrust" signal, robust to the CI machine's physics step count (a hard distance
            // threshold is too timing-sensitive). A small displacement check backs it up. We re-issue the
            // input every fixed step so the controller's FixedUpdate always reads a live command.
            var rb = boatGo.GetComponent<Rigidbody2D>();
            Vector2 start = rb.position;
            for (int i = 0; i < 40; i++)
            {
                if (propulsion == PropulsionType.Oars) boat.SetOarInput(1f, 1f, false); // both oars ahead
                else boat.SetControl(1f, 0f);                                            // full throttle ahead
                yield return new WaitForFixedUpdate();
            }
            Assert.Greater(rb.linearVelocity.magnitude, 0.05f,
                $"[{propulsion}] the re-enabled helm builds way (applies thrust → control restored, not dead)");
            Assert.Greater((rb.position - start).magnitude, 0.01f,
                $"[{propulsion}] …and the boat actually moves off the mark");
        }

        // ---- FIX 2: disembark near land parks the boat where it's left ---------------------------

        [UnityTest]
        public IEnumerator Disembark_NearShore_ParksTheMakingWayBoat()
        {
            // Shoaled tidal terrain so the boat counts as 'near shore' away from the dock.
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 0.2f };
            GameServices.Environment = new FlatEnv { Level = 0.6f };   // depth 0.4 m ≤ 1.5 shore threshold

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

            // Sail off far from the dock, up against a shore, still making way.
            boatGo.transform.position = new Vector3(40f, 40f, 0f);
            var rb = boatGo.GetComponent<Rigidbody2D>();
            rb.position = new Vector2(40f, 40f);
            rb.linearVelocity = new Vector2(3f, -2f);
            rb.angularVelocity = 25f;

            Assert.IsTrue(sw.NearShore(), "the boat is near a shoaled shore");
            Assert.IsTrue(sw.TryInteract(), "disembark succeeds near land, away from the dock");
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);

            // PARK WHERE LEFT: stopped on step-off, and one physics tick later it hasn't coasted off.
            Assert.AreEqual(Vector2.zero, rb.linearVelocity, "the boat is brought to rest on disembark");
            Assert.AreEqual(0f, rb.angularVelocity, 1e-4f);
            Vector2 parkedAt = rb.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.Less((rb.position - parkedAt).magnitude, 0.01f, "and it stays parked where it was left (no drift)");
        }
    }
}
