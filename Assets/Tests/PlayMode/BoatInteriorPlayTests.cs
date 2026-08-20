using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>GOING BELOW, AND COMING BACK</b> — the half of ADR 0038's runtime that EditMode cannot reach:
    /// the components' own lifecycle (a plain MonoBehaviour fires no <c>OnEnable</c> in EditMode, so the
    /// door's self-registration and the cabin's survival of a root toggle can only be proved here), the
    /// cue running off the real clock, and the offer arriving on the seam the popup already draws.
    ///
    /// <para><b>Three traps this fixture is built around.</b> Frames are not time — in batchmode
    /// <c>yield return null</c> can elapse in under a millisecond, so every wait here is on ELAPSED
    /// <c>Time.time</c> and the budget is derived from the door's own <c>CueSeconds</c> rather than
    /// mirrored as a constant. Virtual keypresses are undeliverable in this project, so the press is
    /// driven through the component API — which is the same path <c>Interact</c> takes. And
    /// <c>EventBus.Clear&lt;T&gt;()</c> UNSUBSCRIBES everything, so the subscription this fixture makes
    /// is always taken after the clear, never before.</para>
    /// </summary>
    public class BoatInteriorPlayTests
    {
        private const float DeckRoll = 5f;
        private const float HeavePixels = 1.6f;
        private const float PitchLift = 0.02f;

        private readonly List<Object> _spawned = new();
        private readonly List<InteractOfferChanged> _offers = new();

        private GameObject _boat;
        private BoatInterior _interior;
        private BoatCabinDoor _door;
        private SpriteRenderer _exterior;
        private SpriteRenderer _room;
        private DirectionalBoatSprite _hullSprite;
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            Interactables.Clear();
            InteractVerb.Reset();
            InteractOffer.Reset();
            InteractionGate.Reset();

            _offers.Clear();
            EventBus.Clear<InteractOfferChanged>();
            EventBus.Subscribe<InteractOfferChanged>(_offers.Add);

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.InteriorRockScale = GameConfig.DefaultInteriorRockScale;
            _spawned.Add(_config);
            GameServices.Config = _config;

            // ⚠ An AudioListener, because a listener-less play scene logs a warning EVERY frame.
            Spawn("Listener").AddComponent<AudioListener>();

            BuildBoat();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<InteractOfferChanged>();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractOffer.Reset();
            InteractionGate.Reset();
            GameServices.Config = null;

            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();
        }

        // =====================================================================================
        //  THE LIFECYCLE HOOKS — the whole reason these tests are in PlayMode
        // =====================================================================================

        [UnityTest]
        public IEnumerator TheDoorClaimsHerCandidacyOnEnable_AndRelinquishesItOnDisable()
        {
            yield return null;
            Assert.AreEqual(1, Interactables.Count, "OnEnable registers the door on the interact seam");

            _door.enabled = false;
            yield return null;
            Assert.AreEqual(0, Interactables.Count,
                            "…and OnDisable relinquishes it, so a door on a boat you are nowhere near " +
                            "offers nothing");

            _door.enabled = true;
            yield return null;
            Assert.AreEqual(1, Interactables.Count);
        }

        // =====================================================================================
        //  THE ENTRY SEAM — the offer the #592 popup already draws
        // =====================================================================================

        [UnityTest]
        public IEnumerator StandingAtTheThresholdOffersTheWayIn_ThroughTheExistingSeam()
        {
            yield return null;
            _offers.Clear();

            // The switcher's own actor, on deck, at the door. The verb resolves and publishes; nothing
            // here invents a second prompt path.
            InteractVerb.PublishCandidate(OnDeckAt(_door.transform.position), 90f);

            Assert.IsTrue(InteractOffer.Current.Has, "the door should be the standing offer");
            Assert.AreEqual(InteractOfferSource.Fixture, InteractOffer.Current.Source);
            Assert.AreEqual(_door.Id, InteractOffer.Current.Id);
            Assert.AreEqual("Go below", InteractOffer.Current.Label);
            Assert.IsNotEmpty(_offers, "…and the change is published, which is what the popup rides");

            // Walk away: the offer must go, or the popup names a doorway the player has left.
            InteractVerb.PublishCandidate(OnDeckAt(new Vector2(40f, 0f)), 90f);
            Assert.IsFalse(InteractOffer.Current.Has);
        }

        [UnityTest]
        public IEnumerator AModalOwnsTheKey_SoTheCabinOffersNothingBehindIt()
        {
            yield return null;

            InteractionGate.IsBlocked = true;
            InteractVerb.PublishCandidate(OnDeckAt(_door.transform.position), 90f);
            Assert.IsFalse(InteractOffer.Current.Has,
                           "the gate is checked once, by the verb — this door does not hold a second opinion");

            InteractionGate.IsBlocked = false;
            InteractVerb.PublishCandidate(OnDeckAt(_door.transform.position), 90f);
            Assert.IsTrue(InteractOffer.Current.Has);
        }

        // =====================================================================================
        //  THE ROUND TRIP — press, cue, swap; press, cue, back out
        // =====================================================================================

        [UnityTest]
        public IEnumerator TheRoundTrip_EnterSwapsTheLayers_AndComingOutSwapsThemBack()
        {
            yield return null;

            Assert.IsTrue(_interior.ExactlyOneLayerOn);
            Assert.IsTrue(_exterior.enabled);
            Assert.IsFalse(_room.enabled);

            Assert.IsTrue(_door.TryUse(), "the press starts the leaf");
            Assert.IsFalse(_interior.IsInside, "…and the room does not appear under the player's hand");

            yield return WaitForCue();

            Assert.IsTrue(_interior.IsInside);
            Assert.IsFalse(_exterior.enabled, "off, not sorted behind");
            Assert.IsTrue(_room.enabled);
            Assert.IsTrue(_interior.ExactlyOneLayerOn, "the two are never co-visible");
            Assert.AreEqual("Come out", _door.VerbLabel);

            Assert.IsTrue(_door.TryUse());
            yield return WaitForCue();

            Assert.IsFalse(_interior.IsInside);
            Assert.IsTrue(_exterior.enabled);
            Assert.IsFalse(_room.enabled);
            Assert.AreEqual(0, _interior.Level, "and the level resets on the way out");
        }

        // =====================================================================================
        //  4 · THE REGION HOP — the interior shares the hull's lifetime
        // =====================================================================================

        [UnityTest]
        public IEnumerator TheCabinSurvivesARootToggle_BecauseTheBoatTravelsWithYou()
        {
            // Root-toggling IS how a region hop works: the coordinator deactivates every root of the
            // scene you leave, so "disabled" happens constantly and does not mean "gone". A cabin that
            // reset itself in OnEnable/OnDisable would put the player back out on deck at every region
            // boundary they crossed while below — which is exactly what ADR 0038 proposal 4 rules out.
            yield return null;

            _door.TryUse();
            yield return WaitForCue();
            Assert.IsTrue(_interior.IsInside);

            _boat.SetActive(false);
            yield return null;
            _boat.SetActive(true);
            yield return null;

            Assert.IsTrue(_interior.IsInside, "the player is still below after the hop");
            Assert.IsTrue(_room.enabled, "…and the room is still what is drawn");
            Assert.IsFalse(_exterior.enabled);
            Assert.IsTrue(_interior.ExactlyOneLayerOn);
        }

        [UnityTest]
        public IEnumerator ARootToggleDoesNotStrandTheDoorMidCue()
        {
            yield return null;

            _door.TryUse();
            _boat.SetActive(false);
            yield return null;
            _boat.SetActive(true);

            yield return WaitForCue();
            Assert.IsFalse(_door.IsCueing, "a leaf that was moving finishes moving");
        }

        // =====================================================================================
        //  1 · MOTION — the room rides her own pose, and the scale reaches ONE draw
        // =====================================================================================

        [UnityTest]
        public IEnumerator TheRoomLeansWithTheHull_ByTheComfortFraction()
        {
            const float tilt = 8f;
            const float ride = 0.5f;

            yield return null;
            _hullSprite.VisualTiltDegrees = tilt;
            _hullSprite.DrawnRideMeters = ride;

            _door.TryUse();
            yield return WaitForCue();
            yield return null;   // one LateUpdate with the room on and the hull leaning

            float scale = GameServices.InteriorRockScale;
            Assert.AreEqual(tilt * scale, RoomLeanDegrees(), 1e-3f,
                            "the room takes the hull's own lean, scaled by the comfort clamp");
            Assert.AreEqual(ride * scale, _room.transform.position.y, 1e-3f,
                            "…and the same fraction of the ride she reports having drawn");

            // ⚠ Rule 5's boundary, asserted rather than asserted-about: the hull is posed identically at
            // every scale. The clamp is a filter on one DRAW.
            Assert.AreEqual(tilt, _hullSprite.VisualTiltDegrees, 1e-5f);
            Assert.AreEqual(ride, _hullSprite.DrawnRideMeters, 1e-5f);
        }

        [UnityTest]
        public IEnumerator TheAccessibilityFloorIsDeadFlat_AndTheBoatStillRocks()
        {
            const float tilt = 8f;
            const float ride = 0.5f;

            _config.InteriorRockScale = 0f;

            yield return null;
            _hullSprite.VisualTiltDegrees = tilt;
            _hullSprite.DrawnRideMeters = ride;

            _door.TryUse();
            yield return WaitForCue();
            yield return null;

            Assert.AreEqual(0f, RoomLeanDegrees(), 1e-4f, "0 is DEAD flat, not very nearly level");
            Assert.AreEqual(0f, _room.transform.position.y, 1e-4f);

            Assert.AreEqual(tilt, _hullSprite.VisualTiltDegrees, 1e-5f,
                            "the hull is unmoved by a setting that belongs to one draw");
        }

        [UnityTest]
        public IEnumerator SteppingOutPutsTheRoomBackExactlyAsBuilt()
        {
            yield return null;
            _hullSprite.VisualTiltDegrees = 8f;
            _hullSprite.DrawnRideMeters = 0.5f;

            _door.TryUse();
            yield return WaitForCue();
            yield return null;

            _door.TryUse();
            yield return WaitForCue();
            yield return null;

            Assert.AreEqual(0f, RoomLeanDegrees(), 1e-4f);
            Assert.AreEqual(Vector3.zero, _room.transform.localPosition,
                            "a room nobody is in is byte-identical to one this component never touched");
        }

        // =====================================================================================
        //  the fixture
        // =====================================================================================

        /// <summary>
        /// Wait out the door's cue on the CLOCK the cue is driven by. Frames are not time in batchmode —
        /// sixty of them can elapse in under a millisecond — and the budget is taken from the door's own
        /// <see cref="BoatCabinDoor.CueSeconds"/> rather than mirrored here, so a re-measured cue does not
        /// need a second edit.
        /// </summary>
        private IEnumerator WaitForCue()
        {
            float deadline = Time.time + _door.CueSeconds + 0.2f;
            while (_door.IsCueing && Time.time < deadline) yield return null;
            yield return null;   // one more, so the swap's LateUpdate has run
        }

        private float RoomLeanDegrees()
        {
            float z = _room.transform.localEulerAngles.z;
            return z > 180f ? z - 360f : z;
        }

        private static InteractActor OnDeckAt(Vector2 position)
            => new InteractActor(position, Vector2.zero, InteractContext.OnDeck);

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private void BuildBoat()
        {
            _boat = Spawn("PlayTestHull");

            // A DirectionalBoatSprite is how BoatHullPresenterHost.Resolve finds a presenter with no
            // skinner in the scene. Left DISABLED on purpose: this fixture wants the seam's tilt/ride
            // channels, not the sprite's own LateUpdate, and Resolve searches inactive components.
            _hullSprite = _boat.AddComponent<DirectionalBoatSprite>();
            _hullSprite.enabled = false;

            _exterior = NewRenderer("House");
            _room = NewRenderer("Room");

            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();
            def.Id = "interior.play_test_hull";
            def.PixelsPerMetre = 32;
            def.Levels = new[]
            {
                new BoatInteriorLevel
                {
                    Id = "house_sole",
                    SoleZMeters = 1.78f,
                    Outline = new[]
                    {
                        new Vector2(-1f, -2f), new Vector2(1f, -2f),
                        new Vector2(1f, 2f), new Vector2(-1f, 2f),
                    },
                },
            };
            def.Door = new BoatInteriorDoor
            {
                Id = "entry",
                Side = "aft",
                ClearWidthMeters = 0.72f,
                ThresholdPoint = new Vector3(0f, -2.1f, 1.78f),
                Mechanism = BoatInteriorDoorMechanism.Sliding,
                CueFrames = 8,
                CueMillisecondsPerFrame = 70f,
                CueReversedOnExit = true,
            };
            _spawned.Add(def);

            _interior = _boat.AddComponent<BoatInterior>();
            _interior.Configure(def, _exterior, _room, null, _room.transform, _boat.transform,
                                System.Array.Empty<Sprite>(), 8, true, 0f,
                                DeckRoll, HeavePixels, PitchLift);

            var doorGo = new GameObject("CabinDoor");
            doorGo.transform.SetParent(_boat.transform, false);
            _door = doorGo.AddComponent<BoatCabinDoor>();
            _door.Configure(_interior, "fixture.boat.play_test.cabin_door", -1, 1.2f,
                            "Go below", "Come out");
        }

        private SpriteRenderer NewRenderer(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_boat.transform, false);
            return go.AddComponent<SpriteRenderer>();
        }
    }
}
