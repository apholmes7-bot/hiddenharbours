using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The BOARDING MOVE</b> (owner ask 2026-08-06: "you push E and get teleported; the character
    /// should jump/climb/whatever to get onto the boat"). E no longer repositions the fisher — it sends
    /// them walking to the hull's rail and over it, and the state machine flips when they LAND.
    ///
    /// <para>What these pin is the part a screenshot cannot argue with: <b>the move changes WHEN the
    /// transition happens and never WHETHER it may</b>. So: nothing has flipped while the fisher is in
    /// the air; the same gates that refuse a board at the key-press refuse it at the rail; a hull that
    /// stops being boardable mid-arc is never boarded; and the interact key is held for the length of the
    /// move and given back on landing, having taken only what it took.</para>
    ///
    /// <para>Driven through <see cref="ControlSwitcher.TickBoardingMove"/> in explicit seconds rather than
    /// through a frame pump: the move is measured in seconds, and a test that counted frames would be
    /// pinning the machine that ran it. <see cref="ControlSwitcher.TryInteract"/> itself is unchanged and
    /// still transitions instantly — that contract is covered by <c>ControlSwitcherTests</c>, and one test
    /// here re-states it deliberately.</para>
    /// </summary>
    public class BoardingMoveTests
    {
        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

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
        private void OnMode(ControlModeChanged e) => _modeEvents.Add(e);

        private void WireExposedLand() =>
            (GameServices.TidalTerrain, GameServices.Environment) =
            (new FlatTerrain { Elevation = 0.2f }, new FlatEnv { Level = 0f });

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            _modeEvents.Clear();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Subscribe<ControlModeChanged>(OnMode);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<ControlModeChanged>(OnMode);
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            InteractionGate.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private GameObject NewGo(string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            _spawned.Add(g);
            return g;
        }

        private const float Dt = 1f / 60f;
        private const float Vault = 0.5f;

        private sealed class Rig
        {
            public ControlSwitcher Switcher;
            public PlayerWalkController Walk;
            public BoatController Boat;
            public DevBoatInput Input;
            public GameObject PlayerGo;
            public GameObject BoatGo;
        }

        /// <summary>The greybox rig: player within board reach of the boat, a dock zone at (0,−12) r=3 and
        /// its disembark point on the planks at (0,−10.5). <paramref name="withDeckWalk"/> off is the
        /// older wiring — a rig with no deck walk, which has no rail to find and must degrade to the
        /// instant transition rather than to a broken move.</summary>
        private Rig Build(Vector3 playerPos, Vector3 boatPos, bool withDeckWalk = true)
        {
            var playerGo = NewGo("Player", playerPos);
            var walk = playerGo.AddComponent<PlayerWalkController>();   // auto-adds Rigidbody2D + SpriteRenderer
            if (withDeckWalk) playerGo.AddComponent<DeckWalkController>().enabled = false;

            var boatGo = NewGo("Boat", boatPos);
            var boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();

            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory"; hull.CameraWorldHeightMeters = 14f;
            _spawned.Add(hull);
            boat.SetHull(hull);

            walk.enabled = true; boat.enabled = false; input.enabled = false;

            var dock = NewGo("DockZone", new Vector3(0f, -12f, 0f));
            var disembark = NewGo("Disembark", new Vector3(0f, -10.5f, 0f));
            var swGo = NewGo("Switcher", Vector3.zero);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dock.transform, 3f, disembark.transform);
            sw.ConfigureBoardingMove(enabled: true, approachSpeed: 3f, approachMaxSeconds: 0.8f,
                                     vaultSeconds: Vault, vaultHopMeters: 0.35f);
            return new Rig { Switcher = sw, Walk = walk, Boat = boat, Input = input,
                             PlayerGo = playerGo, BoatGo = boatGo };
        }

        /// <summary>Run the move to its landing in real seconds. Guarded so a move that never ends fails
        /// as a hang-free assertion rather than spinning the test runner.</summary>
        private static void RunTheMoveOut(ControlSwitcher sw, float limitSeconds = 5f)
        {
            float spent = 0f;
            while (sw.IsBoardingMove && spent < limitSeconds)
            {
                sw.TickBoardingMove(Dt);
                spent += Dt;
            }
            Assert.IsFalse(sw.IsBoardingMove, $"the move never landed within {limitSeconds:0.#} s");
        }

        // ---- E starts a MOVE, and nothing has happened yet ------------------------------------

        [Test]
        public void PressingE_StartsAMove_AndFlipsNothingYet()
        {
            var r = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            Vector3 stoodAt = r.PlayerGo.transform.position;

            Assert.IsTrue(r.Switcher.BeginInteract(), "E within reach starts the boarding move");

            Assert.IsTrue(r.Switcher.IsBoardingMove, "the fisher is on their way aboard");
            Assert.AreEqual(ControlMode.OnFoot, r.Switcher.Mode,
                "…and is NOT aboard yet — the mode flips when they land, not when they set off");
            Assert.AreEqual(0, _modeEvents.Count, "so nothing downstream has been told they're aboard");
            Assert.IsNull(r.PlayerGo.transform.parent, "airborne, they ride nobody's deck");
            Assert.IsFalse(r.Walk.enabled, "the walk keys are dead — the MOVE owns the body");
            Assert.IsFalse(r.PlayerGo.GetComponent<Rigidbody2D>().simulated,
                "and its physics is parked, so nothing can fight the arc");
            Assert.IsTrue(r.PlayerGo.GetComponent<SpriteRenderer>().enabled,
                "the fisher is visible for the whole move — that is the entire point of it");
            Assert.Less(Vector3.Distance(stoodAt, r.PlayerGo.transform.position), 1e-4f,
                "the first frame is where they stood");
        }

        [Test]
        public void TheFisherActuallyTravels_AndLandsOnTheDeck()
        {
            var r = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            Vector3 stoodAt = r.PlayerGo.transform.position;
            r.Switcher.BeginInteract();

            // Halfway through, they are neither where they stood nor yet in the state machine's care.
            RunSome(r.Switcher, 0.25f);
            Assert.IsTrue(r.Switcher.IsBoardingMove, "still crossing");
            Assert.Greater(Vector3.Distance(r.PlayerGo.transform.position, stoodAt), 0.1f,
                "the fisher has left the spot they pressed E on — this is a move, not a wait");
            Assert.Greater(r.Switcher.BoardingMoveProgress, 0f);
            Assert.Less(r.Switcher.BoardingMoveProgress, 1f);

            RunTheMoveOut(r.Switcher);

            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode, "landing aboard is what ends the move");
            Assert.AreSame(r.BoatGo.transform, r.PlayerGo.transform.parent, "…and now they ride her");
            Assert.AreEqual(1, _modeEvents.Count, "exactly ONE flip, at the far end");
            Assert.AreEqual(ControlMode.OnDeck, _modeEvents[0].Mode);
        }

        [Test]
        public void SteppingAshoreIsTheSameMoveMirrored_AndReachesTheLanding()
        {
            WireExposedLand();
            var r = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            r.Switcher.TryInteract();                       // aboard instantly — this test is about leaving
            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode);
            _modeEvents.Clear();

            Assert.IsTrue(r.Switcher.BeginInteract(), "E off the helm spot starts the step ashore");
            Assert.IsTrue(r.Switcher.IsBoardingMove);
            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode,
                "they have not landed, so as far as the world is concerned they are still aboard");
            Assert.IsNull(r.PlayerGo.transform.parent, "but they have let go of her");

            RunTheMoveOut(r.Switcher);

            Assert.AreEqual(ControlMode.OnFoot, r.Switcher.Mode);
            Assert.AreEqual(-10.5f, r.PlayerGo.transform.position.y, 1e-3f,
                "the arc lands them exactly on the planks Disembark() places them at");
            Assert.AreEqual(0f, r.PlayerGo.transform.position.x, 1e-3f);
            Assert.IsTrue(r.Walk.enabled, "and the walk keys come back");
            Assert.IsTrue(r.PlayerGo.GetComponent<Rigidbody2D>().simulated, "with physics restored");
        }

        // ---- the gates are law, at the key-press AND mid-arc ----------------------------------

        [Test]
        public void AnUnboardableHull_NeverStartsAMove()
        {
            var save = SaveMigration.NewGame();
            save.OwnedBoats.Add("boat.dory");               // owned but not repaired → damaged
            GameServices.Save = new FakeSaveService(save);

            var r = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));

            Assert.IsFalse(r.Switcher.BeginInteract(), "a damaged dory refuses the verb outright");
            Assert.IsFalse(r.Switcher.IsBoardingMove, "no move to watch — she needs the shipwright first");
            Assert.AreEqual(ControlMode.OnFoot, r.Switcher.Mode);
        }

        [Test]
        public void AHullThatGoesUnboardableMidArc_IsNeverBoarded()
        {
            var save = SaveMigration.NewGame();
            save.OwnedBoats.Add("boat.dory");
            RepairLedger.MarkRepaired(save, "boat.dory");   // she is sound when the fisher sets off…
            GameServices.Save = new FakeSaveService(save);

            var r = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            Vector3 stoodAt = r.PlayerGo.transform.position;
            Assert.IsTrue(r.Switcher.BeginInteract());
            RunSome(r.Switcher, 0.2f);
            Assert.IsTrue(r.Switcher.IsBoardingMove, "mid-arc");

            // …and stops being sound under them. (The gate reads the live save, so this is the same
            // change the shipwright's ledger makes, run backwards.)
            save.RepairedBoats.Remove("boat.dory");
            Assert.IsFalse(r.Switcher.BoardableNow(), "the gate has closed");

            r.Switcher.TickBoardingMove(Dt);

            Assert.IsFalse(r.Switcher.IsBoardingMove, "the move is called off in mid-air");
            Assert.AreEqual(ControlMode.OnFoot, r.Switcher.Mode, "she is not boarded");
            Assert.AreEqual(0, _modeEvents.Count, "and nothing downstream was ever told otherwise");
            Assert.Less(Vector3.Distance(stoodAt, r.PlayerGo.transform.position), 1e-4f,
                "the fisher is put back down where they set off from");
            Assert.IsTrue(r.Walk.enabled, "with their own legs back");
            Assert.IsTrue(r.PlayerGo.GetComponent<Rigidbody2D>().simulated);
        }

        [Test]
        public void AStepOffThatStopsBeingStandableMidArc_PutsThemBackOnTheDeck()
        {
            WireExposedLand();
            var r = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            r.Switcher.TryInteract();                       // aboard
            _modeEvents.Clear();
            Assert.IsTrue(r.Switcher.BeginInteract(), "the step ashore begins over land");

            // She drifts out over open water while the fisher is in the air (no terrain → not land, and
            // well outside the dock zone).
            GameServices.TidalTerrain = null;
            r.BoatGo.transform.position = new Vector3(0f, -40f, 0f);
            Assert.IsFalse(r.Switcher.CanStepAshore(), "there is nothing standable under her any more");

            RunTheMoveOut(r.Switcher);

            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode, "so the fisher never left");
            Assert.AreEqual(0, _modeEvents.Count);
            Assert.AreSame(r.BoatGo.transform, r.PlayerGo.transform.parent, "re-seated aboard her");
        }

        // ---- the interact key: held for the move, and always handed back ----------------------

        [Test]
        public void TheMoveHoldsTheInteractKey_AndHandsItBackOnLanding()
        {
            var r = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            Assert.IsFalse(InteractionGate.IsBlocked, "nothing owns the key to start with");

            r.Switcher.BeginInteract();
            Assert.IsTrue(InteractionGate.IsBlocked,
                "a fisher in mid-air owns the interact key — the deck-gear gates read exactly this flag");
            Assert.IsFalse(r.Switcher.BeginInteract(), "so a second E does nothing while the first is flying");

            RunTheMoveOut(r.Switcher);
            Assert.IsFalse(InteractionGate.IsBlocked, "and gives it straight back on landing");
        }

        // (Tearing the switcher down mid-vault must also hand the key back — that one needs OnDisable,
        // so it lives in BoardingMovePlayTests where the component lifecycle actually runs.)

        [Test]
        public void AKeyAlreadyHeldElsewhereIsNeitherClaimedNorHandedBack()
        {
            var r = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            InteractionGate.IsBlocked = true;               // a dialogue panel already owns the key

            r.Switcher.BeginInteract();
            RunTheMoveOut(r.Switcher);

            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode, "the move still ran and landed");
            Assert.IsTrue(InteractionGate.IsBlocked,
                "…and gave back only what it took — the modal still owns the key IT raised");
        }

        // ---- degrading, and the unchanged instant path ----------------------------------------

        [Test]
        public void WithTheMoveSwitchedOff_EIsTheOldInstantBoard()
        {
            var r = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            r.Switcher.ConfigureBoardingMove(enabled: false, approachSpeed: 3f, approachMaxSeconds: 0.8f,
                                             vaultSeconds: Vault, vaultHopMeters: 0.35f);

            Assert.IsTrue(r.Switcher.BeginInteract());

            Assert.IsFalse(r.Switcher.IsBoardingMove, "no move — the owner's A/B is the old snap exactly");
            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode);
            Assert.IsFalse(InteractionGate.IsBlocked, "and nothing was ever held");
        }

        [Test]
        public void ARigWithNoDeckWalk_FallsStraightThroughToTheInstantBoard()
        {
            // No DeckWalkController means no authored rail to find. Absence is data: board as before
            // rather than arc to a rail that cannot be located.
            var r = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f), withDeckWalk: false);

            Assert.IsTrue(r.Switcher.BeginInteract());
            Assert.IsFalse(r.Switcher.IsBoardingMove);
            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode);
        }

        [Test]
        public void TheHelmIsAStep_NotABoardingMove()
        {
            var r = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            r.Switcher.TryInteract();                       // aboard
            r.PlayerGo.transform.position = r.Switcher.HelmWorldPosition;
            _modeEvents.Clear();

            Assert.IsTrue(r.Switcher.BeginInteract(), "E at the tiller takes the helm");

            Assert.IsFalse(r.Switcher.IsBoardingMove, "crossing to the helm is a step WITHIN the boat");
            Assert.AreEqual(ControlMode.Aboard, r.Switcher.Mode);
            Assert.IsTrue(r.Switcher.BeginInteract(), "and stepping back off it is too");
            Assert.IsFalse(r.Switcher.IsBoardingMove);
            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode);
        }

        [Test]
        public void OutOfReach_NoMoveAndNoTransition()
        {
            var r = Build(new Vector3(-4.5f, 2.5f, 0f), new Vector3(0f, -13.8f, 0f));   // up by the cottage

            Assert.IsFalse(r.Switcher.BeginInteract(), "you still cannot board a boat you are nowhere near");
            Assert.IsFalse(r.Switcher.IsBoardingMove);
            Assert.AreEqual(ControlMode.OnFoot, r.Switcher.Mode);
            Assert.IsTrue(r.Walk.enabled, "and the fisher keeps their legs");
        }

        [Test]
        public void ARegionHopMidArc_SettlesTheFisherRatherThanStrandingThem()
        {
            var r = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            r.Switcher.BeginInteract();
            RunSome(r.Switcher, 0.2f);
            Assert.IsTrue(r.Switcher.IsBoardingMove);

            r.Switcher.ReassertControlMode();               // what the travel coordinator calls on arrival

            Assert.IsFalse(r.Switcher.IsBoardingMove, "an arc has no meaning across a scene hop");
            Assert.IsFalse(InteractionGate.IsBlocked);
            Assert.AreEqual(ControlMode.OnFoot, r.Switcher.Mode, "they had not boarded, so they are ashore");
            Assert.IsTrue(r.Walk.enabled, "and in control of themselves");
        }

        private static void RunSome(ControlSwitcher sw, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt) sw.TickBoardingMove(Dt);
        }
    }
}
