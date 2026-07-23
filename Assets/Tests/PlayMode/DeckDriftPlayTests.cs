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
    /// Rod Fishing v2 Wave 4 — <b>leave-the-helm drift</b>, live over the real runtime lifecycle
    /// (boats-and-navigation.md §3 "leave the helm, work the rail"; design §4.1 — you fish UNMANNED):
    /// boarding puts the player ON DECK with the helm unattended, and the sea must keep working the
    /// hull — the bug this wave fixes was the deck mode suppressing the whole force pass (controller
    /// disabled + mooring stowed = a hull frozen in glass while you fish). Proves, on the live
    /// physics loop: (1) an on-deck hull genuinely drifts downwind while the helm stays unmanned
    /// (BoatController stays DISABLED — the "enabled == manned" read other layers rely on);
    /// (2) the deck-walking player rides the drifting hull (parented to the physics root); and
    /// (3) the deck-walk publishes the live <see cref="DeckStance"/> frame the deck-angle fight term
    /// reads, and clears it when deck-walking ends. Wind is injected via a Core-contract double
    /// (deterministic direction); frame counts are not time, so the run waits on real fixed steps.
    /// </summary>
    public class DeckDriftPlayTests
    {
        private sealed class WindyEnv : IEnvironmentService
        {
            public Vector2 Wind = new Vector2(6f, 0f);   // a stiff breeze blowing due EAST
            public int WorldSeed => 42;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Wind, Vector2.zero, 0f, SeaState.Calm, 1f, 0f);   // calm sea: drift = wind shove only
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            GameServices.Environment = new WindyEnv();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            InteractionGate.Reset();
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

        [UnityTest]
        public IEnumerator OnDeck_TheUnmannedHullDriftsDownwind_AndTheDeckStanceIsLive()
        {
            // ---- rig: the ControlSwitcherTests harness, on the live physics loop ----------------
            var playerGo = NewGo("Player", new Vector3(0.5f, 0f, 0f));
            var walk = playerGo.AddComponent<PlayerWalkController>();   // auto-adds Rigidbody2D + SpriteRenderer
            var deckWalk = playerGo.AddComponent<DeckWalkController>();
            deckWalk.enabled = false;

            var boatGo = NewGo("Boat", Vector3.zero);
            var boat = boatGo.AddComponent<BoatController>();           // auto-adds Rigidbody2D + capsule + mooring
            var input = boatGo.AddComponent<DevBoatInput>();
            var rb = boatGo.GetComponent<Rigidbody2D>();

            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.drift_test"; hull.DisplayName = "Drift Test Dory";
            hull.MassKg = 400f;
            hull.ForwardDrag = 60f; hull.LateralDrag = 200f;
            hull.WindExposure = 30f;                                    // she catches the breeze
            hull.DraughtMeters = 0.35f;
            _spawned.Add(hull);

            walk.enabled = true; boat.enabled = false; input.enabled = false;   // on-foot start

            var swGo = NewGo("Switcher", Vector3.zero);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dockZone: null, zoneRadius: 3f, disembarkPoint: null);

            yield return null;                                          // Awake/OnEnable across the rig
            boat.SetHull(hull);

            // ---- board: OnFoot → OnDeck (the helm is never taken) -------------------------------
            Assert.IsTrue(sw.TryInteract(), "boarding within reach must land the player on the deck");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode);
            Assert.IsFalse(boat.enabled, "the helm stays UNMANNED on deck — enabled == manned is a contract");

            yield return null;                                          // one Update: the deck-walk publishes
            Assert.IsTrue(DeckStance.IsActive,
                "deck-walking must publish the live DeckStance frame (the deck-angle fight term's read)");
            Assert.IsTrue(DeckStance.TryGet(out DeckStanceState stance));
            // The stance was published on the last Update; the hull may have taken a physics step since —
            // near, not bit-equal, is the honest live-frame assertion here.
            Assert.Less(Vector2.Distance((Vector2)boatGo.transform.position, stance.HullPosition), 0.25f,
                "the stance frame rides the hull's live position");
            Assert.AreEqual(deckWalk.DeckHalfExtents, stance.DeckHalfExtents,
                "the stance rectangle is the very deck the player walks");

            // ---- the sea works the unmanned hull: she must genuinely drift downwind -------------
            Vector2 startBoat = rb.position;
            Vector2 startPlayer = playerGo.transform.position;
            for (int i = 0; i < 100; i++)                               // ~2 s of fixed steps
                yield return new WaitForFixedUpdate();

            float driftedEast = rb.position.x - startBoat.x;
            Assert.Greater(driftedEast, 0.05f,
                $"an unmanned hull with the crew on deck must SET downwind (drifted {driftedEast:F3} m east) — " +
                "a deck that freezes the sea is the bug this wave fixes");
            Assert.IsFalse(boat.enabled, "…while the helm stays unmanned throughout");
            Assert.Less(Mathf.Abs(rb.position.y - startBoat.y), 0.5f,
                "a calm-sea, current-free drift stays essentially downwind (sanity)");

            // The deck-walking player is carried WITH the hull (parented to the physics root).
            float playerCarried = playerGo.transform.position.x - startPlayer.x;
            Assert.Greater(playerCarried, driftedEast * 0.5f,
                "the deck-walking player must ride the drifting hull, not be left behind in the water");

            // ---- stepping ashore ends the stance (the dock must read NO stance) -----------------
            // Take the helm instead of disembarking (no land under us out here): the deck-walk
            // disables → the published stance must clear with it.
            sw.ConfigureHelm(Vector2.zero, 99f);                        // the whole deck counts as the helm spot
            Assert.IsTrue(sw.TryInteract(), "taking the helm from the deck");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            yield return null;
            Assert.IsFalse(DeckStance.IsActive,
                "leaving the deck must clear the published stance — off-deck fishing reads none (dock parity)");
        }
    }
}
