using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Boarding control-mode machine (step 2). Covers: boarding in the dock zone enables the boat &amp;
    /// disables walking (and disembarking does the reverse); INTERACT only transitions in the right
    /// zone; and the camera handoff signals (ControlModeChanged + the boat's ActiveBoatChanged) fire
    /// per mode. Driven through the public API + EventBus — no play-mode lifecycle needed.
    /// </summary>
    public class ControlSwitcherTests
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

        // Wire exposed terrain (ground at/above the water line → standable land) so disembark is allowed.
        private void WireExposedLand() =>
            (GameServices.TidalTerrain, GameServices.Environment) =
            (new FlatTerrain { Elevation = 0.2f }, new FlatEnv { Level = 0f });   // depth -0.2 m ≤ 0 = land

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

        // A fully-wired switcher, started on foot (boat controller/input disabled), dock zone at (0,-12) r=3.
        private (ControlSwitcher sw, PlayerWalkController walk, BoatController boat, DevBoatInput input, GameObject playerGo, GameObject boatGo)
            Build(Vector3 playerPos, Vector3 boatPos)
        {
            var playerGo = NewGo("Player", playerPos);
            var walk = playerGo.AddComponent<PlayerWalkController>(); // auto-adds Rigidbody2D + SpriteRenderer
            var boatGo = NewGo("Boat", boatPos);
            var boat = boatGo.AddComponent<BoatController>();          // auto-adds Rigidbody2D
            var input = boatGo.AddComponent<DevBoatInput>();          // requires BoatController

            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory"; hull.CameraWorldHeightMeters = 14f;
            _spawned.Add(hull);
            boat.SetHull(hull);

            walk.enabled = true; boat.enabled = false; input.enabled = false; // on-foot start

            var dock = NewGo("DockZone", new Vector3(0f, -12f, 0f));
            var disembark = NewGo("Disembark", new Vector3(0f, -10.5f, 0f));
            var swGo = NewGo("Switcher", Vector3.zero);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dock.transform, 3f, disembark.transform);
            return (sw, walk, boat, input, playerGo, boatGo);
        }

        [Test]
        public void Board_InZone_EnablesBoat_DisablesWalk_AndHandsCameraToBoat()
        {
            var (sw, walk, boat, input, _, _) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);

            bool ok = sw.TryInteract();

            Assert.IsTrue(ok, "boarding in-zone should succeed");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            Assert.IsFalse(walk.enabled, "walking is disabled aboard");
            Assert.IsTrue(boat.enabled, "the boat controller is enabled aboard");
            Assert.IsTrue(input.enabled, "the boat input is enabled aboard");

            Assert.AreEqual(1, _boatEvents.Count, "boarding zooms the camera to the boat (ActiveBoatChanged)");
            Assert.AreEqual("boat.dory", _boatEvents[0].BoatId);
            Assert.AreEqual(14f, _boatEvents[0].CameraWorldHeightMeters, 1e-4f);
            Assert.AreEqual(1, _modeEvents.Count, "boarding retargets the camera (ControlModeChanged)");
            Assert.AreEqual(ControlMode.Aboard, _modeEvents[0].Mode);
        }

        [Test]
        public void Board_OutOfZone_DoesNothing()
        {
            var (sw, walk, boat, _, _, _) = Build(new Vector3(-4.5f, 2.5f, 0f), new Vector3(0f, -13.8f, 0f)); // up by the cottage

            bool ok = sw.TryInteract();

            Assert.IsFalse(ok, "can't board from far away — boarding needs you within reach of the boat");
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);
            Assert.IsTrue(walk.enabled);
            Assert.IsFalse(boat.enabled);
            Assert.AreEqual(0, _modeEvents.Count);
            Assert.AreEqual(0, _boatEvents.Count);
        }

        [Test]
        public void Disembark_BoatInZone_EnablesWalk_DisablesBoat_MovesPlayer_AndRetargets()
        {
            WireExposedLand();   // the dock sits on standable land so stepping off is allowed
            var (sw, walk, boat, input, playerGo, _) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            sw.TryInteract(); // board
            _modeEvents.Clear(); _boatEvents.Clear();

            bool ok = sw.TryInteract(); // boat is at (0,-13.8), within r=3 of the dock (0,-12) and over land

            Assert.IsTrue(ok, "disembarking with the boat in the dock zone should succeed");
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);
            Assert.IsTrue(walk.enabled, "walking is re-enabled on foot");
            Assert.IsFalse(boat.enabled, "the boat controller is disabled on foot");
            Assert.IsFalse(input.enabled);
            Assert.AreEqual(-10.5f, playerGo.transform.position.y, 1e-4f, "player is placed at the disembark point");
            Assert.AreEqual(0f, playerGo.transform.position.x, 1e-4f);
            Assert.AreEqual(1, _modeEvents.Count);
            Assert.AreEqual(ControlMode.OnFoot, _modeEvents[0].Mode);
        }

        [Test]
        public void Disembark_BoatOutOfZone_DoesNothing()
        {
            var (sw, _, boat, _, _, boatGo) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            sw.TryInteract(); // board
            _modeEvents.Clear(); _boatEvents.Clear();

            boatGo.transform.position = new Vector3(0f, -30f, 0f); // sailed far out
            bool ok = sw.TryInteract();

            Assert.IsFalse(ok, "can't dock from out at sea");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            Assert.IsTrue(boat.enabled);
            Assert.AreEqual(0, _modeEvents.Count);
        }
    }
}
