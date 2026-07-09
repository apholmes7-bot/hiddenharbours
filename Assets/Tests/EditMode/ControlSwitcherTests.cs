using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The THREE-STATE control machine (trap arc Build 5 — the owner's on-deck control state):
    /// OnFoot ⇄ OnDeck ⇄ Aboard(at the helm). Covers: boarding lands ON THE DECK (walkable, boat
    /// un-driven, player riding the physics root); the helm is a STATION (walk to the spot + E to pilot,
    /// E again to step back); disembark happens from the deck under the standable-step-off rules; and the
    /// camera handoff signals fire per transition (ControlModeChanged each hop; the boat's
    /// ActiveBoatChanged only when the helm is taken). Driven through the public API + EventBus — no
    /// play-mode lifecycle needed.
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
            playerGo.AddComponent<DeckWalkController>().enabled = false; // the deck walk (Build 5)
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

        // ---- board → ON DECK (not the helm) --------------------------------------------------

        [Test]
        public void Board_InReach_LandsOnDeck_NotTheHelm()
        {
            var (sw, walk, boat, input, playerGo, boatGo) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);

            bool ok = sw.TryInteract();

            Assert.IsTrue(ok, "boarding in reach should succeed");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "boarding lands ON THE DECK (Build 5), not the helm");
            Assert.IsFalse(walk.enabled, "the on-foot walk is off on deck (the deck controller drives)");
            Assert.IsFalse(boat.enabled, "the boat is NOT driven from the deck — the helm is a station");
            Assert.IsFalse(input.enabled, "steering input is dead unless at the helm");
            Assert.IsTrue(playerGo.GetComponent<SpriteRenderer>().enabled, "the deckhand is visible on deck");
            Assert.IsFalse(playerGo.GetComponent<Rigidbody2D>().simulated,
                "the player's physics is off on deck (transform-driven; the hull collider must not fight it)");
            Assert.AreSame(boatGo.transform, playerGo.transform.parent,
                "the deck-walking player rides the boat's PHYSICS ROOT (its drift carries them)");
            Assert.IsTrue(playerGo.GetComponent<DeckWalkController>().enabled, "the deck walk drives on deck");

            Assert.AreEqual(0, _boatEvents.Count, "no boat framing on boarding — that arrives when the helm is taken");
            Assert.AreEqual(1, _modeEvents.Count, "boarding retargets the camera (ControlModeChanged)");
            Assert.AreEqual(ControlMode.OnDeck, _modeEvents[0].Mode);
        }

        [Test]
        public void Board_OutOfReach_DoesNothing()
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

        // ---- the helm is a station: walk to it + E to pilot, E again to step back ------------

        [Test]
        public void TakeHelm_AtTheHelmSpot_EnablesSteering_AndHandsCameraToBoat()
        {
            var (sw, walk, boat, input, playerGo, _) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            sw.TryInteract(); // board → deck
            _modeEvents.Clear(); _boatEvents.Clear();

            playerGo.transform.position = sw.HelmWorldPosition;   // walk to the tiller
            Assert.IsTrue(sw.WithinHelmReach(), "standing at the helm spot");
            bool ok = sw.TryInteract();

            Assert.IsTrue(ok, "taking the helm at the spot should succeed");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode, "Aboard now means AT THE HELM");
            Assert.IsTrue(boat.enabled, "the boat controller drives at the helm");
            Assert.IsTrue(input.enabled, "steering input is live at the helm");
            Assert.IsFalse(walk.enabled, "walking is off at the helm");
            Assert.IsFalse(playerGo.GetComponent<DeckWalkController>().enabled, "deck walking is off at the helm");
            Assert.IsFalse(playerGo.GetComponent<SpriteRenderer>().enabled, "the figure hands over to the boat picture");

            Assert.AreEqual(1, _boatEvents.Count, "taking the helm zooms the camera to the boat (ActiveBoatChanged)");
            Assert.AreEqual("boat.dory", _boatEvents[0].BoatId);
            Assert.AreEqual(14f, _boatEvents[0].CameraWorldHeightMeters, 1e-4f);
            Assert.AreEqual(1, _modeEvents.Count);
            Assert.AreEqual(ControlMode.Aboard, _modeEvents[0].Mode);
        }

        [Test]
        public void TakeHelm_AwayFromTheHelmSpot_Refused()
        {
            // Boat far from the dock over open water (no terrain wired → not land), so the only deck action
            // in reach would be the helm — and the player isn't at it.
            var (sw, _, boat, _, _, _) = Build(new Vector3(50f, 50f, 0f), new Vector3(51f, 50f, 0f));
            sw.TryInteract(); // board → deck (lands amidships, a step away from the helm)
            _modeEvents.Clear();

            Assert.IsFalse(sw.WithinHelmReach(), "the board spot is NOT within helm reach — you must walk to it");
            bool ok = sw.TryInteract();

            Assert.IsFalse(ok, "E away from the helm (and with no step-off) does nothing");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "still on deck");
            Assert.IsFalse(boat.enabled, "the boat stays un-driven");
            Assert.AreEqual(0, _modeEvents.Count);
        }

        [Test]
        public void LeaveHelm_StepsBackOntoTheDeck()
        {
            var (sw, walk, boat, input, playerGo, boatGo) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            sw.TryInteract();                                       // board → deck
            playerGo.transform.position = sw.HelmWorldPosition;
            sw.TryInteract();                                       // take the helm
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            _modeEvents.Clear(); _boatEvents.Clear();

            bool ok = sw.TryInteract();                             // step back from the tiller

            Assert.IsTrue(ok, "stepping back from the helm is always allowed");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode);
            Assert.IsFalse(boat.enabled, "steering is dropped");
            Assert.IsFalse(input.enabled);
            Assert.IsFalse(walk.enabled, "still not the on-foot walk — the deck controller drives");
            Assert.IsTrue(playerGo.GetComponent<SpriteRenderer>().enabled, "the deckhand reappears");
            Assert.IsTrue(playerGo.GetComponent<DeckWalkController>().enabled);
            Assert.AreSame(boatGo.transform, playerGo.transform.parent, "still riding the physics root");
            Assert.AreEqual(1, _modeEvents.Count);
            Assert.AreEqual(ControlMode.OnDeck, _modeEvents[0].Mode);
        }

        // ---- disembark happens FROM THE DECK, under the standable-step-off rules --------------

        [Test]
        public void Disembark_FromDeck_AtTheDock_LandsOnThePlanks()
        {
            WireExposedLand();   // the dock sits on standable land so stepping off is allowed
            var (sw, walk, boat, input, playerGo, _) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            sw.TryInteract(); // board → deck (lands amidships, away from the helm spot)
            _modeEvents.Clear(); _boatEvents.Clear();

            Assert.IsFalse(sw.WithinHelmReach(), "not at the helm, so E means 'step ashore' here");
            bool ok = sw.TryInteract(); // boat is at (0,-13.8), within r=3 of the dock (0,-12) and over land

            Assert.IsTrue(ok, "disembarking from the deck with the boat in the dock zone should succeed");
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);
            Assert.IsTrue(walk.enabled, "walking is re-enabled on foot");
            Assert.IsFalse(boat.enabled, "the boat controller is disabled on foot");
            Assert.IsFalse(input.enabled);
            Assert.IsNull(playerGo.transform.parent, "ashore the player stands free of the boat");
            Assert.IsTrue(playerGo.GetComponent<Rigidbody2D>().simulated, "physics is restored ashore");
            Assert.AreEqual(-10.5f, playerGo.transform.position.y, 1e-4f, "player is placed at the disembark point");
            Assert.AreEqual(0f, playerGo.transform.position.x, 1e-4f);
            Assert.AreEqual(1, _modeEvents.Count);
            Assert.AreEqual(ControlMode.OnFoot, _modeEvents[0].Mode);
        }

        [Test]
        public void Disembark_OverOpenWater_Refused_StaysOnDeck()
        {
            var (sw, _, boat, _, _, boatGo) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            sw.TryInteract(); // board → deck
            _modeEvents.Clear(); _boatEvents.Clear();

            boatGo.transform.position = new Vector3(0f, -30f, 0f); // drifted far out (open water, no land wired)
            bool ok = sw.TryInteract();

            Assert.IsFalse(ok, "can't step off over open water");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "still on deck");
            Assert.AreEqual(0, _modeEvents.Count);
        }

        // ---- the full loop: walk → board → helm → sail → deck → step ashore -------------------

        [Test]
        public void FullLoop_SignalsFirePerTransition()
        {
            WireExposedLand();
            var (sw, _, _, _, playerGo, boatGo) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));

            sw.TryInteract();                                       // board → OnDeck
            playerGo.transform.position = sw.HelmWorldPosition;
            sw.TryInteract();                                       // deck → helm
            sw.TryInteract();                                       // helm → deck
            playerGo.transform.position = boatGo.transform.position + new Vector3(0f, 1.2f, 0f); // walk clear
            sw.TryInteract();                                       // deck → ashore

            Assert.AreEqual(ControlMode.OnFoot, sw.Mode, "the loop lands back on foot");
            Assert.AreEqual(4, _modeEvents.Count, "one ControlModeChanged per hop");
            Assert.AreEqual(ControlMode.OnDeck, _modeEvents[0].Mode);
            Assert.AreEqual(ControlMode.Aboard, _modeEvents[1].Mode);
            Assert.AreEqual(ControlMode.OnDeck, _modeEvents[2].Mode);
            Assert.AreEqual(ControlMode.OnFoot, _modeEvents[3].Mode);
            Assert.AreEqual(1, _boatEvents.Count, "the boat framing arrives exactly once — when the helm is taken");
        }

        // ---- the deck clamp maths (pure) ------------------------------------------------------

        [Test]
        public void DeckWalk_ClampToDeck_KeepsThePlayerOnTheDeck()
        {
            Vector2 center = new Vector2(0f, 0.2f);
            Vector2 half = new Vector2(0.7f, 1.6f);

            Assert.AreEqual(new Vector2(0f, 0.2f), DeckWalkController.ClampToDeck(new Vector2(0f, 0.2f), center, half),
                "inside the deck stays put");
            Assert.AreEqual(new Vector2(0.7f, 0.2f), DeckWalkController.ClampToDeck(new Vector2(5f, 0.2f), center, half),
                "east of the rail clamps to the rail");
            Assert.AreEqual(new Vector2(-0.7f, -1.4f), DeckWalkController.ClampToDeck(new Vector2(-9f, -9f), center, half),
                "a far corner clamps to the deck corner");
        }

        [Test]
        public void DeckWalk_Step_MovesAndClamps_DiagonalNotFaster()
        {
            Vector2 center = Vector2.zero;
            Vector2 half = new Vector2(1f, 2f);

            // A straight step moves speed*dt.
            Vector2 s1 = DeckWalkController.Step(Vector2.zero, Vector2.up, 2f, 0.5f, center, half);
            Assert.AreEqual(new Vector2(0f, 1f), s1);

            // A diagonal input is magnitude-clamped (not √2 faster).
            Vector2 s2 = DeckWalkController.Step(Vector2.zero, new Vector2(1f, 1f), 2f, 0.5f, center, half);
            Assert.AreEqual(1f, s2.magnitude, 1e-4f, "diagonals aren't faster");

            // Walking off the bow clamps at the deck edge.
            Vector2 s3 = DeckWalkController.Step(new Vector2(0f, 1.9f), Vector2.up, 10f, 1f, center, half);
            Assert.AreEqual(2f, s3.y, 1e-4f, "clamped at the bow rail");
        }
    }
}
