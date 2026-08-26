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
            ResetTheInteractSeam();
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
            ResetTheInteractSeam();
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        /// <summary>Put every static the interact seam holds back to empty. Both ends, because these are
        /// process-wide registers: a candidate left registered would follow this fixture into the next
        /// suite, and a <c>InteractionGate</c> left raised by a boarding move that is still in flight at
        /// the end of a case would silence the verb in the one after it.</summary>
        private static void ResetTheInteractSeam()
        {
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractOffer.Reset();
            InteractActorProbe.Reset();
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

        // =====================================================================================
        //  ON DECK, THE REGISTRY IS ASKED BEFORE THE STEP ASHORE (lead-architect, 2026-08-25)
        //
        //  The amended invariant: BOARDING AND THE HELM WIN OVER THE REGISTRY; STEP-ASHORE YIELDS TO A
        //  RESOLVING FIXTURE. Before it, CanStepAshore() is true from the WHOLE deck of a boat that is
        //  tied up or lying over bared ground, so BeginInteract answered every such press with the step
        //  off and no OnDeck fixture on a docked boat — a cabin door, a pail, a hauler — could ever be
        //  pressed. These pin the new rung and, just as hard, the three rungs that did NOT move.
        // =====================================================================================

        /// <summary>
        /// A candidate as a plain object rather than a test MonoBehaviour: Unity mints a MonoScript per
        /// .cs file by filename, so a second MonoBehaviour declared in this one has none and
        /// <c>AddComponent</c> is unreliable for it (<c>InteractVerbPlayTests</c>' note). Nothing is lost
        /// — the seam is an interface, so a plain object registers exactly as a component does.
        ///
        /// <para><see cref="IsAvailable"/> COUNTS its reads on purpose. <see cref="InteractResolver"/>
        /// asks each in-context candidate for it exactly once per resolve, which makes this a resolve
        /// counter — and that is how "one press, one resolve" is pinned below without reaching inside
        /// the switcher.</para>
        /// </summary>
        private sealed class Fake : IInteractable
        {
            public string Id { get; set; } = "test.deck_fixture";
            public Vector2 WorldPosition { get; set; }
            public float ReachMeters { get; set; } = 2f;
            public int Priority { get; set; } = InteractPriority.Fixture;
            public InteractContext Contexts { get; set; } = InteractContext.OnDeck;
            public bool RequiresFacing { get; set; }
            public string VerbLabel { get; set; } = "Work the thing";

            /// <summary>How many times the press has actually reached this candidate.</summary>
            public int Calls;

            /// <summary>How many times the resolver has considered it (see the class remarks).</summary>
            public int Resolves;

            public bool IsAvailable { get { Resolves++; return true; } }

            public void Interact(in InteractActor actor) => Calls++;
        }

        /// <summary>Register a candidate at a world position. Relinquished wholesale in TearDown, which is
        /// what keeps this fixture's doubles out of the next suite's registry.</summary>
        private static Fake Candidate(Vector3 at, float reach = 2f,
                                      InteractContext where = InteractContext.OnDeck)
        {
            var c = new Fake { WorldPosition = at, ReachMeters = reach, Contexts = where };
            Interactables.Register(c);
            return c;
        }

        /// <summary>Get her aboard the instant way and hand back where the fisher ends up standing —
        /// away from the tiller, which is the only place on the deck where this ladder is decided.</summary>
        private static Vector3 BoardAndStandAwayFromTheHelm(ControlSwitcher sw, GameObject playerGo)
        {
            Assert.IsTrue(sw.TryInteract(), "harness: she has to actually get aboard");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode);
            Assert.IsFalse(sw.WithinHelmReach(), "harness: the board spot is away from the tiller");
            return playerGo.transform.position;
        }

        /// <summary>The instant path — this section is about which verb a press IS, never about how long
        /// the fisher takes to climb. The MOVE route is pinned separately, below.</summary>
        private static void NoBoardingMove(ControlSwitcher sw)
            => sw.ConfigureBoardingMove(false, 3f, 0.8f, 0.55f, 0.35f);

        [Test]
        public void OnDeck_AtAWharf_ACandidateInReachTakesThePress_AndSheStaysAboard()
        {
            WireExposedLand();
            var (sw, _, _, _, playerGo, _) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            NoBoardingMove(sw);
            Vector3 onDeck = BoardAndStandAwayFromTheHelm(sw, playerGo);
            _modeEvents.Clear(); _boatEvents.Clear();

            Fake fixture = Candidate(onDeck);
            Assert.IsTrue(sw.CanStepAshore(), "harness: she is tied up, so the OLD ladder would step her off");

            Assert.IsTrue(sw.BeginInteract(), "the press was spent");

            Assert.AreEqual(1, fixture.Calls, "…on the fixture: on deck the registry is asked BEFORE the step off");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "and she is still aboard");
            Assert.AreEqual(0, _modeEvents.Count, "nothing transitioned — one press, one action");
        }

        [Test]
        public void OnDeck_AtAWharf_WithNothingRegistered_SheStepsAshoreExactlyAsBefore()
        {
            // The non-regression, and the whole reason the insertion is safe: an empty registry resolves
            // nothing, so the two branches below it run exactly as they always did.
            WireExposedLand();
            var (sw, walk, _, _, playerGo, _) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            NoBoardingMove(sw);
            BoardAndStandAwayFromTheHelm(sw, playerGo);
            _modeEvents.Clear();

            Assert.AreEqual(0, Interactables.Count, "nothing registered, as a region with no fixtures has");
            Assert.IsTrue(sw.BeginInteract(), "E steps her ashore");

            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);
            Assert.IsTrue(walk.enabled, "walking is re-enabled on foot");
            Assert.IsNull(playerGo.transform.parent, "ashore she stands free of the boat");
            Assert.AreEqual(1, _modeEvents.Count);
            Assert.AreEqual(ControlMode.OnFoot, _modeEvents[0].Mode);
        }

        [Test]
        public void OnDeck_AtTheHelm_TheHelmStillWins_OverACandidateStandingAtIt()
        {
            // The rung ABOVE the new one, pinned: taking the helm is unconditional and the registry never
            // sees the press at the tiller.
            WireExposedLand();
            var (sw, _, boat, input, playerGo, _) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            NoBoardingMove(sw);
            sw.TryInteract();                                      // board → deck
            playerGo.transform.position = sw.HelmWorldPosition;    // walk to the tiller
            Assert.IsTrue(sw.WithinHelmReach());
            Fake fixture = Candidate(sw.HelmWorldPosition);
            _modeEvents.Clear(); _boatEvents.Clear();

            Assert.IsTrue(sw.BeginInteract());

            Assert.AreEqual(ControlMode.Aboard, sw.Mode, "the helm is a station and it keeps the press");
            Assert.IsTrue(boat.enabled, "…she is being steered");
            Assert.IsTrue(input.enabled);
            Assert.AreEqual(0, fixture.Calls, "the candidate never got asked");
            Assert.AreEqual(0, fixture.Resolves, "…and was not even resolved: the helm answers first");
        }

        [Test]
        public void OnFoot_BoardingStillWins_OverACandidateAtHerFeet()
        {
            // The SHORE ladder is unchanged by this ruling, and that is a policy call worth a pin of its
            // own: the end state (boarding registers as a candidate and the resolver arbitrates it) stays
            // deferred, so this test should go red only when THAT is built.
            WireExposedLand();
            var (sw, _, _, _, playerGo, _) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            NoBoardingMove(sw);
            Fake fixture = Candidate(playerGo.transform.position, where: InteractContext.OnFoot);

            Assert.IsTrue(sw.BeginInteract());

            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "boarding took the press, exactly as it always has");
            Assert.AreEqual(0, fixture.Calls, "…and the candidate did not also fire");
        }

        [Test]
        public void WithTheVerbSwitchedOff_TheDockedDeckPressStepsAshore_ExactlyAsItDidBefore()
        {
            // The A/B and the escape hatch. The early consult goes through the same TryInteractCandidate
            // the tail one does, so one flag still restores the whole pre-seam ladder.
            WireExposedLand();
            var (sw, _, _, _, playerGo, _) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            NoBoardingMove(sw);
            Vector3 onDeck = BoardAndStandAwayFromTheHelm(sw, playerGo);
            sw.ConfigureInteractVerb(false, InteractResolver.DefaultFacingArcDegrees);
            Fake fixture = Candidate(onDeck);

            Assert.IsTrue(sw.BeginInteract());

            Assert.AreEqual(ControlMode.OnFoot, sw.Mode, "she stepped ashore, as she did before the seam existed");
            Assert.AreEqual(0, fixture.Calls);
            Assert.AreEqual(0, fixture.Resolves, "with the verb off the registry is not consulted at all");
        }

        // ---- the MOVE route, which is the other way off a deck --------------------------------

        [Test]
        public void OnDeck_AtAWharf_ACandidateInReach_StopsTheDisembarkMOVE_NotJustTheInstantPath()
        {
            // The boarding MOVE is left ON here (the shipped default): the consult sits above BOTH ways
            // off the deck, so the move must not set off either.
            WireExposedLand();
            var (sw, _, _, _, playerGo, _) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            Vector3 onDeck = BoardAndStandAwayFromTheHelm(sw, playerGo);
            Fake fixture = Candidate(onDeck);

            Assert.IsTrue(sw.BeginInteract());

            Assert.AreEqual(1, fixture.Calls, "the fixture took it");
            Assert.IsFalse(sw.IsBoardingMove, "…so the walk to the rail never set off");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode);
            Assert.IsFalse(InteractionGate.IsBlocked, "and no move is holding the interact key");
        }

        [Test]
        public void OnDeck_AtAWharf_WithNothingRegistered_TheDisembarkMoveStillSetsOff()
        {
            WireExposedLand();
            var (sw, _, _, _, playerGo, _) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            BoardAndStandAwayFromTheHelm(sw, playerGo);

            Assert.IsTrue(sw.BeginInteract(), "E off the helm spot starts the step ashore");
            Assert.IsTrue(sw.IsBoardingMove, "the fisher is walking to the rail");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "…and has not landed yet");
        }

        // ---- one press, one resolve ------------------------------------------------------------

        [Test]
        public void ADeckPress_ResolvesTheRegistryExactlyOnce_EvenWhenItFindsNothing()
        {
            // Away from the helm over open water, the press has nothing to do at all — the ONE path that
            // reaches both consult sites. The tail therefore stands down on deck, or every idle press on
            // a deck would cost two scans of the registry instead of one (rule 7).
            var (sw, _, _, _, playerGo, boatGo) = Build(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f));
            NoBoardingMove(sw);
            BoardAndStandAwayFromTheHelm(sw, playerGo);

            boatGo.transform.position = new Vector3(0f, -30f, 0f);   // drifted out: no dock, no land
            Assert.IsFalse(sw.CanStepAshore(), "harness: nothing standable to step onto");
            Assert.IsFalse(sw.WithinHelmReach(), "harness: still away from the tiller");
            Fake outOfReach = Candidate(playerGo.transform.position + new Vector3(50f, 0f, 0f));

            Assert.IsFalse(sw.BeginInteract(), "a press with nothing to do is still a press with nothing to do");

            Assert.AreEqual(0, outOfReach.Calls);
            Assert.AreEqual(1, outOfReach.Resolves,
                            "asked ONCE — the deck's early consult, not that one and the tail as well");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode);
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
