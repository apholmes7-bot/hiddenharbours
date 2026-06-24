using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Two boat-handling fixes from the owner's playtest, exercised through the pure/public API (no
    /// play-mode lifecycle):
    ///
    ///   • FIX 1 — boat controls stop after returning to a scene. After a region hop the persistent
    ///     switcher carries the control MODE across the toggle, but nothing re-enabled the active boat's
    ///     controller + input to match it, so a re-activated region (especially a RETURN trip) could leave
    ///     the helm dead. <see cref="ControlSwitcher.ReassertControlMode"/> (called by
    ///     <see cref="RegionTravelCoordinator.ApplyArrival"/>) makes boat OR foot control reliably live
    ///     after EVERY arrival — for both the rowed Dory and the engine Punt.
    ///
    ///   • FIX 2 — disembark anywhere near land/shore (not only at the dock), and the boat PARKS where it's
    ///     left (no drift). Covers the pure near-shore-by-depth rule and that disembarking stops the boat.
    /// </summary>
    public class BoatRebindAndDisembarkTests
    {
        // A flat tidal terrain + environment so OnLand()'s exposed-terrain depth read is deterministic.
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
        private readonly List<ControlModeChanged> _modeEvents = new();
        private readonly List<ActiveBoatChanged> _boatEvents = new();
        private void OnMode(ControlModeChanged e) => _modeEvents.Add(e);
        private void OnBoat(ActiveBoatChanged e) => _boatEvents.Add(e);

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _modeEvents.Clear(); _boatEvents.Clear();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Subscribe<ControlModeChanged>(OnMode);
            EventBus.Subscribe<ActiveBoatChanged>(OnBoat);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<ControlModeChanged>(OnMode);
            EventBus.Unsubscribe<ActiveBoatChanged>(OnBoat);
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private GameObject NewGo(string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            _spawned.Add(g);
            return g;
        }

        private BoatHullDef Hull(string id, PropulsionType propulsion)
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = id; hull.CameraWorldHeightMeters = 14f; hull.Propulsion = propulsion;
            hull.DraughtMeters = 0.3f;
            _spawned.Add(hull);
            return hull;
        }

        // A fully-wired switcher started ON FOOT (boat + input disabled), dock at (0,-12) r=3.
        private (ControlSwitcher sw, PlayerWalkController walk, BoatController boat, DevBoatInput input, GameObject boatGo)
            Build(Vector3 playerPos, Vector3 boatPos, BoatHullDef hull)
        {
            var playerGo = NewGo("Player", playerPos);
            var walk = playerGo.AddComponent<PlayerWalkController>();
            var boatGo = NewGo("Boat", boatPos);
            var boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();
            boat.SetHull(hull);

            walk.enabled = true; boat.enabled = false; input.enabled = false;

            var dock = NewGo("DockZone", new Vector3(0f, -12f, 0f));
            var disembark = NewGo("Disembark", new Vector3(0f, -10.5f, 0f));
            var swGo = NewGo("Switcher", Vector3.zero);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dock.transform, 3f, disembark.transform);
            return (sw, walk, boat, input, boatGo);
        }

        // ---- FIX 1: re-assert control mode after a scene return ----------------------------------

        [Test]
        public void ReassertControlMode_WhenAboard_ReEnablesBoatAndInput_AndKeepsWalkFrozen()
        {
            var (sw, walk, boat, input, _) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                   Hull("boat.dory", PropulsionType.Oars));
            sw.TryInteract(); // board → Aboard, controllers enabled
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);

            // Simulate the scene-toggle blanking the boat control (the bug: a re-activated region leaves
            // the helm dead) — disable the controllers WITHOUT touching the switcher's mode.
            boat.enabled = false; input.enabled = false; walk.enabled = true;
            _modeEvents.Clear(); _boatEvents.Clear();

            sw.ReassertControlMode();

            Assert.IsTrue(boat.enabled, "the active boat's controller is re-enabled on return (helm live)");
            Assert.IsTrue(input.enabled, "and its input is re-enabled");
            Assert.IsFalse(walk.enabled, "walking stays frozen while aboard");
            Assert.AreEqual(1, _boatEvents.Count, "the camera reframes to the boat on return (ActiveBoatChanged)");
            Assert.AreEqual("boat.dory", _boatEvents[0].BoatId);
            Assert.AreEqual(1, _modeEvents.Count);
            Assert.AreEqual(ControlMode.Aboard, _modeEvents[0].Mode);
        }

        [Test]
        public void ReassertControlMode_WhenOnFoot_KeepsBoatDisabled_AndWalkLive()
        {
            var (sw, walk, boat, input, _) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                   Hull("boat.dory", PropulsionType.Oars));
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);
            // Pretend the toggle wrongly left the boat enabled.
            boat.enabled = true; input.enabled = true;
            _modeEvents.Clear();

            sw.ReassertControlMode();

            Assert.IsFalse(boat.enabled, "on foot the boat controller stays disabled");
            Assert.IsFalse(input.enabled);
            Assert.IsTrue(walk.enabled, "and walking is live");
            Assert.AreEqual(1, _modeEvents.Count);
            Assert.AreEqual(ControlMode.OnFoot, _modeEvents[0].Mode);
        }

        [Test]
        public void ApplyArrival_AboardReturnTrip_LeavesTheHelmLive_ForOarsAndEngine()
        {
            // The owner's exact repro: sail region→region→back; confirm helm works on return for BOTH boats.
            foreach (var propulsion in new[] { PropulsionType.Oars, PropulsionType.Engine })
            {
                var (sw, _, boat, input, boatGo) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                         Hull("boat.test", propulsion));
                sw.TryInteract(); // board in region A

                var gw = NewGo("GwAnchor", Vector3.zero).AddComponent<RegionAnchor>();
                gw.Configure("region.b", NewGo("GwArr", new Vector3(100f, 0f, 0f)).transform,
                             NewGo("GwDock", new Vector3(100f, 0f, 0f)).transform,
                             NewGo("GwDis", new Vector3(100f, 0f, 0f)).transform);
                var cove = NewGo("CoveAnchor", Vector3.zero).AddComponent<RegionAnchor>();
                cove.Configure("region.a", NewGo("CoveArr", new Vector3(0f, -13.8f, 0f)).transform,
                               NewGo("CoveDock", new Vector3(0f, -12f, 0f)).transform,
                               NewGo("CoveDis", new Vector3(0f, -10.5f, 0f)).transform);

                // Simulate the scene toggle blanking the boat control on each hop (the bug).
                boat.enabled = false; input.enabled = false;
                RegionTravelCoordinator.ApplyArrival(null, boatGo.transform, sw, gw);   // A → B
                Assert.IsTrue(boat.enabled, $"[{propulsion}] helm live on arrival in B");
                Assert.IsTrue(input.enabled);

                boat.enabled = false; input.enabled = false;
                RegionTravelCoordinator.ApplyArrival(null, boatGo.transform, sw, cove); // B → A (RETURN)
                Assert.IsTrue(boat.enabled, $"[{propulsion}] helm live again on the RETURN to A");
                Assert.IsTrue(input.enabled);
                Assert.AreEqual(ControlMode.Aboard, sw.Mode, $"[{propulsion}] still aboard across the round trip");
            }
        }

        // ---- FIX 2: disembark only onto LAND (board from anywhere); the boat is held on step-off -----

        [Test]
        public void IsStandableLandByDepth_OnlyExposedGroundIsLand()
        {
            // The tightened rule (owner playtest): only EXPOSED ground (depth ≤ 0 — the ground is at/above the
            // water line) is standable land. Merely-shallow but still-submerged water is NOT land — you can't
            // step off onto water.
            Assert.IsTrue(ControlSwitcher.IsStandableLandByDepth(0f), "right at the water line counts as land (bared)");
            Assert.IsTrue(ControlSwitcher.IsStandableLandByDepth(-0.3f), "ground proud of the water is land");
            Assert.IsFalse(ControlSwitcher.IsStandableLandByDepth(0.4f), "shallow-but-submerged water (0.4 m) is NOT land");
            Assert.IsFalse(ControlSwitcher.IsStandableLandByDepth(4f), "deep water is open sea, not land");
        }

        [Test]
        public void Disembark_OnLandAwayFromDock_Succeeds_AndStepsOffAtTheBoat()
        {
            // Tidal terrain EXPOSED so the ground under the boat is bared → standable land, far from the dock.
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 0.2f };
            GameServices.Environment = new FlatEnv { Level = 0f }; // depth -0.2 m ≤ 0 = land

            var (sw, walk, boat, input, boatGo) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                        Hull("boat.dory", PropulsionType.Oars));
            sw.TryInteract(); // board (within reach of the boat)
            boatGo.transform.position = new Vector3(40f, 40f, 0f); // sailed far from the dock, up onto a flat

            Assert.IsFalse(sw.InDockZone(), "the boat is nowhere near the dock");
            Assert.IsTrue(sw.OnLand(), "but it's over standable land (an exposed flat)");
            Assert.IsTrue(sw.CanInteract(), "so disembark is allowed here");

            bool ok = sw.TryInteract(); // disembark away from the dock
            Assert.IsTrue(ok, "disembarking onto land (away from the dock) succeeds");
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);
            Assert.IsTrue(walk.enabled);
            Assert.IsFalse(boat.enabled);
            Assert.IsFalse(input.enabled);
            // Stepped off at the boat (not teleported back to the far dock's disembark point).
            Assert.AreEqual(40f, walk.transform.position.x, 1e-4f, "the fisher steps off where the boat is");
            Assert.AreEqual(40f, walk.transform.position.y, 1e-4f);
        }

        [Test]
        public void Disembark_OverShallowButSubmergedWater_IsRefused()
        {
            // The owner's tightened rule: merely-shallow water that's still SUBMERGED is NOT land. The old
            // "shallow-but-submerged depth" allowance is gone — you can't step off onto water.
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 0.2f };
            GameServices.Environment = new FlatEnv { Level = 0.6f }; // depth 0.4 m > 0 → still submerged water

            var (sw, _, boat, _, boatGo) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                 Hull("boat.dory", PropulsionType.Oars));
            sw.TryInteract(); // board
            boatGo.transform.position = new Vector3(40f, 40f, 0f); // over shallow but still-submerged water

            Assert.IsFalse(sw.OnLand(), "shallow-but-submerged water is not standable land");
            Assert.IsFalse(sw.CanInteract(), "so disembark is refused over water");
            Assert.IsFalse(sw.TryInteract());
            Assert.AreEqual(ControlMode.Aboard, sw.Mode, "still aboard — can't step off onto water");
        }

        [Test]
        public void Disembark_OutInDeepWater_StillRefused()
        {
            // Deep water everywhere → not over land, not at the dock → can't get off (no stranding mid-sea).
            GameServices.TidalTerrain = new FlatTerrain { Elevation = -10f };
            GameServices.Environment = new FlatEnv { Level = 0f }; // depth 10 m

            var (sw, _, boat, _, boatGo) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                 Hull("boat.dory", PropulsionType.Oars));
            sw.TryInteract(); // board
            boatGo.transform.position = new Vector3(0f, -60f, 0f); // far out in deep water

            Assert.IsFalse(sw.OnLand(), "deep open water is not land");
            Assert.IsFalse(sw.CanInteract(), "so disembark is refused out at sea");
            Assert.IsFalse(sw.TryInteract());
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            Assert.IsTrue(boat.enabled, "still piloting");
        }

        [Test]
        public void Board_FromAnywhereWithinReach_Succeeds_EvenAwayFromTheDock()
        {
            // Board-from-anywhere (owner playtest): boarding is gated by proximity to the boat, not a dock
            // zone. Place the player right beside a boat far from any dock and confirm she boards.
            var (sw, _, boat, input, _) = Build(new Vector3(50f, 50f, 0f), new Vector3(51f, 50f, 0f),
                                                Hull("boat.dory", PropulsionType.Oars));
            Assert.IsFalse(sw.InDockZone(), "nowhere near the dock");
            Assert.IsTrue(sw.WithinBoardReach(), "but the player is right beside the boat");
            Assert.IsTrue(sw.TryInteract(), "boarding succeeds from anywhere within reach of the boat");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            Assert.IsTrue(boat.enabled);
            Assert.IsTrue(input.enabled);
        }

        [Test]
        public void Disembark_HoldsTheBoat_BroughtToRest()
        {
            // The boat is coasting; disembarking holds the rope (boat tethered to the player) and brings it to
            // rest so it sits quiet under the player's hand (parks where left in calm weather).
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 0.2f };
            GameServices.Environment = new FlatEnv { Level = 0f };   // exposed land + dead-calm sample

            var (sw, _, boat, _, boatGo) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                 Hull("boat.dory", PropulsionType.Oars));
            sw.TryInteract(); // board
            boatGo.transform.position = new Vector3(40f, 40f, 0f);
            var rb = boatGo.GetComponent<Rigidbody2D>();
            rb.linearVelocity = new Vector2(3f, -2f); // making way
            rb.angularVelocity = 30f;

            sw.TryInteract(); // disembark onto land → holds the rope

            Assert.AreEqual(Vector2.zero, rb.linearVelocity, "the boat is brought to rest on step-off (held quiet)");
            Assert.AreEqual(0f, rb.angularVelocity, 1e-4f, "and stops spinning");
        }

        [Test]
        public void Stop_ZeroesVelocity_AndClearsInput()
        {
            var boatGo = NewGo("Boat", Vector3.zero);
            var boat = boatGo.AddComponent<BoatController>();
            boat.SetHull(Hull("boat.dory", PropulsionType.Oars));
            var rb = boatGo.GetComponent<Rigidbody2D>();
            rb.linearVelocity = new Vector2(5f, 5f);
            rb.angularVelocity = 90f;

            boat.Stop();

            Assert.AreEqual(Vector2.zero, rb.linearVelocity);
            Assert.AreEqual(0f, rb.angularVelocity, 1e-4f);
        }
    }
}
