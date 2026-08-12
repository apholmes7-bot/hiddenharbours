using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The one interact verb, JOINED UP</b> (M2-39) — the real migrated component registering itself in
    /// a running scene, a real <see cref="ControlSwitcher"/> deciding what a press is for, and a real key.
    ///
    /// <para><b>What only PlayMode can show.</b> The decision rule is EditMode's
    /// (<c>InteractVerbTests</c> owns the filters, the ladder, and the all-permutations determinism claim).
    /// What is left is the wiring, and it is the part a seam actually fails at:
    /// <list type="bullet">
    ///   <item><b>The non-regression.</b> With nothing registered, <c>BeginInteract</c> boards, takes the
    ///   helm and steps ashore exactly as it did before the seam existed. This is the claim that made it
    ///   safe to land the verb inside the player controller at all, so it is asserted, not assumed.</item>
    ///   <item><b>The precedence.</b> Boarding and the helm still win where both would apply — the
    ///   deliberate, flagged-for-review ordering choice — and the verb takes only the presses that used to
    ///   do nothing.</item>
    ///   <item><b>Scene-scoped registration</b>, through <see cref="WetBucketPoint"/>, which is the real
    ///   thing this PR migrated: a component that used to read <c>F</c> in its own <c>Update</c> and now
    ///   registers a candidate instead.</item>
    ///   <item><b>One press, one action</b>, with the key held down — the cast-latch lesson (#181) as a
    ///   pin rather than a promise.</item>
    /// </list></para>
    ///
    /// <para><b>Why the doubles below are plain objects and not test MonoBehaviours.</b> Unity mints a
    /// MonoScript per .cs file by filename, so a MonoBehaviour declared as a second (or nested) class in a
    /// test file has none and <c>AddComponent&lt;T&gt;</c> is not reliable for it. The seam being
    /// POCO-shaped means nothing is lost: a candidate is an interface, so a plain object registers exactly
    /// as a component does — and the component half of the contract is proved by the real registrant
    /// rather than by a stand-in for it.</para>
    ///
    /// <para><b>Headless-safe by construction.</b> Nothing here renders, reads pixels or needs a camera —
    /// CI runs with a null graphics device. The one test that needs a REAL key press follows the
    /// <c>HelmDashPlayTests.ARealKeyPress</c> precedent and <see cref="Assert.Ignore(string)"/>s itself
    /// when an unfocused batchmode runner will not deliver a virtual press; everything it would have
    /// proved about dispatch is also proved through the switcher's own entry point, which is not
    /// input-shaped.</para>
    /// </summary>
    public class InteractVerbPlayTests
    {
        private static readonly Vector3 BoatPos = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 AshorePos = new Vector3(20f, 0f, 0f);   // far outside board reach
        private static readonly Vector3 FarAwayPos = new Vector3(60f, 0f, 0f);

        // The dock zone is parked far off ON PURPOSE. InDockZone() is half of CanStepAshore(), so a dock
        // sitting on the boat would make E mean "step ashore" from anywhere on the deck and a deck
        // candidate could never be reached. No terrain or environment is wired either, which makes
        // BoatCrossing.DepthAt read +∞ — open water, the other half of CanStepAshore() answered false. So
        // on this rig, on deck and away from the helm, E is a press with nothing to do: exactly the case
        // the verb is allowed to take.
        private static readonly Vector3 DockPos = new Vector3(500f, 0f, 0f);
        private const float DockZoneRadius = 3.5f;

        /// <summary>A candidate as a plain object (see the class note on why this is not a component).</summary>
        private sealed class Fake : IInteractable
        {
            public string Id { get; set; } = "test.candidate";
            public Vector2 WorldPosition { get; set; }
            public float ReachMeters { get; set; } = 1.5f;
            public int Priority { get; set; } = InteractPriority.Fixture;
            public InteractContext Contexts { get; set; } = InteractContext.OnFoot;
            public bool RequiresFacing { get; set; }
            public bool IsAvailable { get; set; } = true;
            public int Calls;
            public void Interact(in InteractActor actor) => Calls++;
        }

        private readonly List<Object> _spawned = new();
        private readonly List<InteractPerformed> _performed = new();
        private readonly List<InteractCandidateChanged> _candidates = new();

        private PlayerWalkController _walk;
        private Rigidbody2D _playerBody;
        private ControlSwitcher _switcher;
        private BoatController _boat;
        private Keyboard _testKeyboard;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();

            _performed.Clear();
            _candidates.Clear();
            EventBus.Clear<InteractPerformed>();
            EventBus.Clear<InteractCandidateChanged>();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Subscribe<InteractPerformed>(_performed.Add);
            EventBus.Subscribe<InteractCandidateChanged>(_candidates.Add);

            // The player, on foot and well clear of the boat.
            var playerGo = Spawn("Player");
            playerGo.AddComponent<SpriteRenderer>();
            _walk = playerGo.AddComponent<PlayerWalkController>();
            _playerBody = playerGo.GetComponent<Rigidbody2D>();     // RequireComponent adds it
            StandAt(AshorePos);

            // The boat, at the origin, with her own input parked (this suite never drives her).
            var boatGo = Spawn("Dory");
            boatGo.transform.position = BoatPos;
            _boat = boatGo.AddComponent<BoatController>();
            var boatInput = boatGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory_interact_test";
            hull.DraughtMeters = 0.3f;
            hull.CameraWorldHeightMeters = 14f;
            hull.Propulsion = PropulsionType.Oars;
            _spawned.Add(hull);
            _boat.SetHull(hull);
            _boat.enabled = false;
            boatInput.enabled = false;

            // The switcher, wired to that berth. The boarding MOVE is OFF throughout: this suite is about
            // which verb a press IS, never about how long the fisher takes to climb — and the instant path
            // is the one every pre-move caller of TryInteract still gets.
            var dockZone = Spawn("DockZone");
            dockZone.transform.position = DockPos;
            var disembark = Spawn("Disembark");
            disembark.transform.position = DockPos;
            _switcher = Spawn("Switcher").AddComponent<ControlSwitcher>();
            _switcher.Configure(_walk, _boat, boatInput, dockZone.transform, DockZoneRadius, disembark.transform);
            _switcher.ConfigureBoardingMove(false, 3f, 0.8f, 0.55f, 0.35f);
            _switcher.ConfigureInteractVerb(true, InteractResolver.DefaultFacingArcDegrees);
        }

        [TearDown]
        public void TearDown()
        {
            if (_testKeyboard != null && _testKeyboard.added) InputSystem.RemoveDevice(_testKeyboard);
            _testKeyboard = null;

            EventBus.Clear<InteractPerformed>();
            EventBus.Clear<InteractCandidateChanged>();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            GameServices.Reset();

            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        /// <summary>Put the fisher somewhere. The BODY is moved as well as the transform, or the next
        /// physics step would drag her back to where her rigidbody still thinks she is (the
        /// <c>PierDisembarkPlayTests</c> convention).</summary>
        private void StandAt(Vector3 pos)
        {
            if (_playerBody != null) _playerBody.position = pos;
            _walk.transform.position = pos;
        }

        /// <summary>A registered plain candidate at a world position.</summary>
        private Fake Candidate(string id, Vector3 pos, float reach = 1.5f,
                               int priority = InteractPriority.Fixture,
                               InteractContext where = InteractContext.OnFoot)
        {
            var c = new Fake
            {
                Id = id,
                WorldPosition = pos,
                ReachMeters = reach,
                Priority = priority,
                Contexts = where,
            };
            Interactables.Register(c);
            return c;
        }

        /// <summary>The real migrated fixture, live in the scene, so its own OnEnable is what registers
        /// it. Placed away from the boat so the switcher's own branches have nothing to say.</summary>
        private WetBucketPoint RealSeawaterSpot(Vector3 pos)
        {
            var go = Spawn("WetBucketSpot");
            go.transform.position = pos;
            return go.AddComponent<WetBucketPoint>();
        }

        // =====================================================================================
        //  THE NON-REGRESSION — the claim that made this safe to land in the player controller
        // =====================================================================================

        [UnityTest]
        public IEnumerator WithNothingRegistered_TheInteractKeyDoesExactlyWhatItAlwaysDid()
        {
            Assert.AreEqual(0, Interactables.Count, "the registry starts empty, as a region with no fixtures does");
            yield return null;

            // On foot, in reach → board (onto the deck).
            StandAt(BoatPos + new Vector3(1f, 0f, 0f));
            Assert.IsTrue(_switcher.BeginInteract(), "E boards");
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode);

            // On deck, at the helm → take the helm.
            _switcher.ConfigureHelm(Vector2.zero, 0.9f);
            StandAt(BoatPos);
            Assert.IsTrue(_switcher.BeginInteract(), "E takes the helm");
            Assert.AreEqual(ControlMode.Aboard, _switcher.Mode);

            // At the helm → step back onto the deck, always.
            Assert.IsTrue(_switcher.BeginInteract(), "E steps back onto the deck");
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode);

            // On deck, away from the helm, over nothing standable → nothing happens, exactly as before.
            _switcher.ConfigureHelm(new Vector2(0f, -50f), 0.9f);
            StandAt(BoatPos);
            Assert.IsFalse(_switcher.BeginInteract(),
                           "and a press with nothing to do is still a press with nothing to do");
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode);
        }

        [UnityTest]
        public IEnumerator TheVerbCanBeSwitchedOffEntirely_AndThenTheRegistryIsInert()
        {
            var spot = Candidate("test.spot", AshorePos);
            _switcher.ConfigureInteractVerb(false, InteractResolver.DefaultFacingArcDegrees);
            yield return null;

            StandAt(AshorePos);
            Assert.IsFalse(_switcher.BeginInteract(), "the A/B escape hatch: E is board / helm / step ashore only");
            Assert.AreEqual(0, spot.Calls);
            Assert.IsEmpty(_performed);
        }

        // =====================================================================================
        //  DISPATCH — the press reaches exactly one candidate
        // =====================================================================================

        [UnityTest]
        public IEnumerator TheMigratedFixtureRegistersItself_AndTakesThePress_WithNoKeyOfItsOwn()
        {
            // WetBucketPoint is the real migration: it used to own an Update() reading F and a hand-asserted
            // "the ranges don't overlap". It now has neither.
            var spot = RealSeawaterSpot(AshorePos);
            yield return null;

            Assert.AreEqual(1, Interactables.Count, "it registered itself (OnEnable), with no installer");
            Assert.AreEqual("fixture.st_peters.wet_bucket", spot.Id);
            Assert.AreEqual(InteractContext.OnFoot, spot.Contexts, "on foot only — what StallReach's gate was");
            Assert.IsFalse(spot.RequiresFacing, "omnidirectional, as the pre-verb proximity behaviour was");

            StandAt(AshorePos);
            Assert.IsTrue(_switcher.BeginInteract(), "the press was spent");
            Assert.AreEqual(1, _performed.Count);
            Assert.AreEqual("fixture.st_peters.wet_bucket", _performed[0].Id, "…on the seawater spot");
            Assert.AreEqual(ControlMode.OnFoot, _switcher.Mode, "and nothing about the control mode moved");
        }

        [UnityTest]
        public IEnumerator TheMigratedFixtureKeepsItsOldReach_AndLeavesTheRegistryWhenTornDown()
        {
            var spot = RealSeawaterSpot(AshorePos);
            yield return null;
            Assert.AreEqual(4f, spot.ReachMeters, 1e-4f,
                            "still StallGate's 4 m — the migration moved the binding, not the edge");

            // Just outside it: the press is not spent (and the switcher has nothing to say out here either).
            StandAt(AshorePos + new Vector3(4.5f, 0f, 0f));
            Assert.IsFalse(_switcher.BeginInteract());
            Assert.IsEmpty(_performed);

            // Just inside it.
            StandAt(AshorePos + new Vector3(3.5f, 0f, 0f));
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.AreEqual(1, _performed.Count);

            // Scene-scoped: disabled means gone, so a torn-down region cannot leave the verb acting on
            // something that is no longer loaded.
            spot.enabled = false;
            yield return null;
            Assert.AreEqual(0, Interactables.Count, "relinquished on disable, like every other Core registry");
            Assert.IsFalse(_switcher.BeginInteract());
            Assert.AreEqual(1, _performed.Count, "still just the one");

            spot.enabled = true;
            yield return null;
            Assert.AreEqual(1, Interactables.Count, "…and it comes back with the component");
        }

        [UnityTest]
        public IEnumerator ExactlyOneCandidateActs_EvenWithSeveralInReach()
        {
            var near = Candidate("test.near", AshorePos + new Vector3(0.3f, 0f, 0f), reach: 3f);
            var far = Candidate("test.far", AshorePos + new Vector3(2f, 0f, 0f), reach: 3f);
            yield return null;

            StandAt(AshorePos);
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.AreEqual(1, near.Calls, "the nearest one");
            Assert.AreEqual(0, far.Calls, "and only it — 'exactly one candidate' is the acceptance criterion");
        }

        [UnityTest]
        public IEnumerator ADeckCandidate_IsOfferedOnTheDeckAndNotAshore()
        {
            var deckThing = Candidate("test.deck_pail", BoatPos, reach: 2f, where: InteractContext.OnDeck);
            _switcher.ConfigureHelm(new Vector2(0f, -50f), 0.9f);   // the helm is nowhere near
            yield return null;

            // Ashore beside the boat: the deck pail is not ours, and boarding is what E means.
            StandAt(BoatPos + new Vector3(1f, 0f, 0f));
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode, "E boarded");
            Assert.AreEqual(0, deckThing.Calls);

            // Now on deck, away from the helm and over nothing standable — the press had nothing to do
            // before the seam, and the deck candidate is what it now does.
            StandAt(BoatPos);
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.AreEqual(1, deckThing.Calls, "on deck, the verb reaches a deck candidate");
        }

        // =====================================================================================
        //  PRECEDENCE — the deliberate ordering choice, pinned so a change to it is visible
        // =====================================================================================

        [UnityTest]
        public IEnumerator BoardingStillWins_WhenBothWouldApply()
        {
            // The stated cost of consulting the verb LAST (see ControlSwitcher.TryInteractCandidate): a
            // candidate inside board reach of a boardable boat does not get the press. Deliberate, legible,
            // and flagged for lead-architect — pinned here so that if the ordering is ever revisited, this
            // is the test that says so out loud rather than a behaviour that quietly flips.
            Vector3 beside = BoatPos + new Vector3(1f, 0f, 0f);
            var spot = Candidate("test.spot", beside, reach: 2f);
            yield return null;

            StandAt(beside);
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode, "boarding took the press");
            Assert.AreEqual(0, spot.Calls, "…and the candidate did not also fire — one press, one action");
            Assert.IsEmpty(_performed, "nothing was performed through the verb");
        }

        [UnityTest]
        public IEnumerator TakingTheHelmStillWins_OverADeckCandidateStandingAtIt()
        {
            var atHelm = Candidate("test.helm_thing", BoatPos, reach: 2f, where: InteractContext.OnDeck);
            _switcher.ConfigureHelm(Vector2.zero, 0.9f);
            yield return null;

            StandAt(BoatPos + new Vector3(1f, 0f, 0f));
            Assert.IsTrue(_switcher.BeginInteract(), "board");
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode);

            StandAt(BoatPos);                          // standing on the helm spot
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.AreEqual(ControlMode.Aboard, _switcher.Mode, "the helm is a station and it keeps the press");
            Assert.AreEqual(0, atHelm.Calls);
        }

        // =====================================================================================
        //  THE STAND-DOWNS
        // =====================================================================================

        [UnityTest]
        public IEnumerator AnOlderConsumersClaim_StandsTheVerbDown_ButNotBoarding()
        {
            // The double-fire this closes is real at St Peters: Aunt Ginny stands 1.80 m from her freezer
            // against WorldInteractor's 1.8 m radius. Standing next to a neighbour must not spend one press
            // twice — but it must still let you step aboard your own dory.
            var spot = Candidate("test.spot", AshorePos);
            yield return null;

            InteractActionClaim.IsClaimed = true;
            StandAt(AshorePos);
            Assert.IsFalse(_switcher.BeginInteract(), "the verb stood down");
            Assert.AreEqual(0, spot.Calls);

            StandAt(BoatPos + new Vector3(1f, 0f, 0f));
            Assert.IsTrue(_switcher.BeginInteract(), "…while boarding is untouched by the claim");
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode);
        }

        [UnityTest]
        public IEnumerator TheModalGate_StopsTheVerbLikeEverythingElseWorldSide()
        {
            var spot = Candidate("test.spot", AshorePos);
            yield return null;

            InteractionGate.IsBlocked = true;
            StandAt(AshorePos);
            Assert.IsFalse(_switcher.BeginInteract());
            Assert.AreEqual(0, spot.Calls, "a dialogue that is up owns the key outright");

            InteractionGate.Reset();
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.AreEqual(1, spot.Calls);
        }

        // =====================================================================================
        //  THE DIEGETIC HIGHLIGHT — the signal, driven by the live component loop
        // =====================================================================================

        [UnityTest]
        public IEnumerator TheCandidateSignal_TracksTheFisherAndIsPublishedOnChangeOnly()
        {
            // No screen-space prompt anywhere in this test — the affordance IS this signal (M2-39), and
            // art-pipeline's outline is its only consumer.
            Candidate("test.spot", AshorePos, reach: 2f);
            StandAt(FarAwayPos);
            yield return null;
            yield return null;
            _candidates.Clear();

            StandAt(AshorePos);
            yield return null;
            yield return null;
            Assert.AreEqual(1, _candidates.Count, "walking into reach lights exactly one candidate");
            Assert.AreEqual("test.spot", _candidates[0].Id);
            Assert.IsTrue(_candidates[0].Has);

            // Several frames of standing still: the driver resolves every frame, but publishes nothing.
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(1, _candidates.Count, "no per-frame event traffic (rule 7)");

            StandAt(FarAwayPos);
            yield return null;
            yield return null;
            Assert.AreEqual(2, _candidates.Count);
            Assert.IsFalse(_candidates[1].Has, "walking away drops the outline");
        }

        [UnityTest]
        public IEnumerator TheHighlightGoesOutWhenTheSwitcherIsTornDown()
        {
            Candidate("test.spot", AshorePos, reach: 2f);
            StandAt(AshorePos);
            yield return null;
            yield return null;
            Assert.AreEqual("test.spot", InteractVerb.CurrentCandidateId);

            _switcher.enabled = false;
            yield return null;
            Assert.IsNull(InteractVerb.CurrentCandidateId,
                          "nothing else republishes it, so a torn-down driver must put it out itself");
        }

        // =====================================================================================
        //  ONE PRESS, ONE ACTION — the cast-latch lesson (#181), pinned rather than promised
        // =====================================================================================

        [UnityTest]
        public IEnumerator ARealKeyPress_ReachesTheVerb_AndAHeldKeyFiresExactlyOnce()
        {
            // WHY THIS MATTERS. FishingController starts a cast on the RISING EDGE of "held", so any verb
            // that can end while its key is still down must require a real release or it re-triggers the
            // bug #181 closed. This verb sidesteps that whole class by being a PRESS: its driver reads
            // wasPressedThisFrame, so there is no held state to latch and a key kept down cannot re-fire.
            // That is a design constraint on the seam, so it is asserted against a real device.
            var spot = Candidate("test.spot", AshorePos);
            StandAt(AshorePos);
            _testKeyboard = InputSystem.AddDevice<Keyboard>();
            yield return null;

            // Direct state write (not a queued event): an unfocused batchmode app drops keyboard EVENTS for
            // non-background devices, but InputState.Change lands regardless.
            InputState.Change(_testKeyboard, new KeyboardState(Key.E));
            yield return null;
            if (!_testKeyboard.eKey.isPressed)
                Assert.Ignore("virtual key press not deliverable here (unfocused headless input) — the " +
                              "dispatch path itself is covered by the BeginInteract tests above");

            Assert.AreEqual(1, spot.Calls, "the press reached the candidate through the real key");

            // Hold it down for several more frames: still exactly one action.
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(1, spot.Calls, "a held interact key does not re-fire — there is no latch to get wrong");

            // Release and press again: a fresh edge is a fresh action.
            InputState.Change(_testKeyboard, new KeyboardState());
            yield return null;
            InputState.Change(_testKeyboard, new KeyboardState(Key.E));
            yield return null;
            Assert.AreEqual(2, spot.Calls, "and a real second press is a second action");
        }
    }
}
