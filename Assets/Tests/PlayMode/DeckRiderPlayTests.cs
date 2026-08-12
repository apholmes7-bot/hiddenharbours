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
    /// <b>The character rides the boat, and the pilot is on screen</b> — over the LIVE runtime lifecycle,
    /// which is the only place these claims can be made. <c>OnEnable</c> does not fire in EditMode, and
    /// every assertion here is about what happens when a mode CHANGES: who is drawing, what stance they
    /// hold, and whether anything is left frozen behind.
    ///
    /// <para>The headline is <see cref="TakingTheHelm_DRAWSThePilot_RatherThanHidingThem"/>. Before this,
    /// <c>ControlSwitcher.ApplyPlayerFor</c> hid the sprite outright at the helm — there was no helmsman
    /// figure on any boat, and the dory rowed with disembodied oars. The regression these guard is subtle
    /// and total: the player becoming INVISIBLE, which is what "rider child off AND body renderer off"
    /// looks like from the driver's seat. Every transition here re-asserts that somebody is drawing.</para>
    ///
    /// <para>The RIDE itself is measured in EditMode where it belongs (<c>DeckRideMathTests</c> for the
    /// pose, <c>BoatRockPhasePublishTests</c> for the real wave chain against a scripted clock). What is
    /// proven here is the WIRING: that a leaning hull reaches the character's visual at all, and that the
    /// lean lands on the CHILD transform rather than on the player's own, which
    /// <see cref="DeckWalkController"/> stomps upright every frame by design.</para>
    ///
    /// <para>Frame counts are not time: the rig is stepped with real yields and no assertion depends on
    /// how many frames a transition took.</para>
    /// </summary>
    public class DeckRiderPlayTests
    {
        private readonly List<Object> _spawned = new();
        private readonly object _seaOwner = new();

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
            DisplacedSea.Clear(_seaOwner);   // never leak an active sea into another fixture
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            InteractionGate.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // ---- the rig -----------------------------------------------------------------------------

        private sealed class Rig
        {
            public ControlSwitcher Switcher;
            public DeckRiderVisual Rider;
            public SpriteRenderer Body;         // the player's own renderer (ashore, and the mirror source)
            public SpriteRenderer RiderSr;      // the child that leans
            public Transform PlayerTransform;
            public IsoCharacterSprite Character;
            public DirectionalBoatSprite Hull;  // the tilt hook a transform-rock hull writes
            public Transform HullVisual;        // the counter-rotated child the hull is drawn into
            public BoatController Boat;
            public BoatHullDef HullDef;
        }

        private GameObject NewGo(string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            _spawned.Add(g);
            return g;
        }

        private Sprite NewCell()
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
        }

        /// <summary>
        /// The shipped player + boat wiring, as <c>PersistentCoreBuilder</c> assembles it: a rider child
        /// under the player, a boat wearing a directional skin, and a hull def whose VISUAL says how she is
        /// driven. <paramref name="rowed"/> gives her oar sheets — the data the pilot stance is read off.
        /// A dock zone sits on the boat so stepping ashore is available without authored terrain.
        /// </summary>
        private Rig NewRig(bool rowed, float helmReach = 3f)
        {
            var playerGo = NewGo("Player", new Vector3(0.5f, 0f, 0f));
            var walk = playerGo.AddComponent<PlayerWalkController>();   // auto-adds Rigidbody2D + SpriteRenderer
            var body = playerGo.GetComponent<SpriteRenderer>();
            var deckWalk = playerGo.AddComponent<DeckWalkController>();
            deckWalk.enabled = false;
            var character = playerGo.AddComponent<IsoCharacterSprite>();

            var riderGo = new GameObject("DeckRider");
            riderGo.transform.SetParent(playerGo.transform, false);
            var riderSr = riderGo.AddComponent<SpriteRenderer>();
            riderSr.enabled = false;
            var rider = playerGo.AddComponent<DeckRiderVisual>();
            rider.Configure(riderSr, body, character);

            var boatGo = NewGo("Boat", Vector3.zero);
            var boat = boatGo.AddComponent<BoatController>();           // auto-adds Rigidbody2D + capsule
            var input = boatGo.AddComponent<DevBoatInput>();

            var visualChild = new GameObject("Visual");
            visualChild.transform.SetParent(boatGo.transform, false);
            var hullSr = visualChild.AddComponent<SpriteRenderer>();
            var directional = boatGo.AddComponent<DirectionalBoatSprite>();
            directional.Configure(new Sprite[8], hullSr);

            var visual = ScriptableObject.CreateInstance<BoatVisualDef>();
            visual.Id = rowed ? "visual.test_dory" : "visual.test_skiff";
            visual.Facings = new Sprite[8];
            for (int i = 0; i < 8; i++) visual.Facings[i] = NewCell();
            if (rowed)
            {
                visual.OarColumnCount = 10;
                visual.OarPort = new Sprite[80];
                visual.OarStar = new Sprite[80];
                for (int i = 0; i < 80; i++) { visual.OarPort[i] = NewCell(); visual.OarStar[i] = NewCell(); }
                Assert.IsTrue(visual.HasOarSheets(), "harness: the rowed hull must actually wear oars");
            }
            _spawned.Add(visual);

            var hullDef = ScriptableObject.CreateInstance<BoatHullDef>();
            hullDef.Id = rowed ? "boat.test_dory" : "boat.test_skiff";
            hullDef.DisplayName = "Test Hull";
            hullDef.MassKg = 400f;
            hullDef.ForwardDrag = 60f;
            hullDef.LateralDrag = 200f;
            hullDef.DraughtMeters = 0.35f;
            hullDef.Visual = visual;
            _spawned.Add(hullDef);
            boat.SetHull(hullDef);

            walk.enabled = true; boat.enabled = false; input.enabled = false;   // on-foot start

            // A dock on the boat, so "step ashore" is legal without authored tidal terrain under her.
            var dock = NewGo("Dock", Vector3.zero);

            var swGo = NewGo("Switcher", Vector3.zero);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dock.transform, zoneRadius: 6f, disembarkPoint: null);
            // The helm sits at the tiller; the reach is opened up so the test can take it from where
            // boarding lands the player rather than having to drive the deck walk.
            sw.ConfigureHelm(new Vector2(0f, -1.3f), helmReach);

            return new Rig
            {
                Switcher = sw, Rider = rider, Body = body, RiderSr = riderSr,
                PlayerTransform = playerGo.transform, Character = character,
                Hull = directional, HullVisual = visualChild.transform, Boat = boat, HullDef = hullDef,
            };
        }

        /// <summary>Somebody is drawing the character. The one state that must never occur is NEITHER.</summary>
        private static void AssertSomethingDrawsThePlayer(Rig r, string when)
            => Assert.IsTrue(r.Body.enabled || r.RiderSr.enabled,
                             $"{when}: the player has become INVISIBLE — no renderer is drawing them");

        // ---- the headline: the pilot exists --------------------------------------------------------

        [UnityTest]
        public IEnumerator TakingTheHelm_DRAWSThePilot_RatherThanHidingThem()
        {
            var r = NewRig(rowed: true);
            yield return null;                                    // Awake / OnEnable across the rig

            Assert.IsTrue(r.Body.enabled, "ashore, the player's own renderer draws — unchanged by all this");
            Assert.IsFalse(r.RiderSr.enabled);

            Assert.IsTrue(r.Switcher.TryInteract(), "boarding within reach lands the player on deck");
            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode);
            yield return null;
            AssertSomethingDrawsThePlayer(r, "on deck");
            Assert.IsTrue(r.RiderSr.enabled, "on deck the RIDER draws, because it is the one that can lean");
            Assert.IsFalse(r.Body.enabled, "exactly one figure, not two");

            Assert.IsTrue(r.Switcher.TryInteract(), "at the helm spot, E takes the helm");
            Assert.AreEqual(ControlMode.Aboard, r.Switcher.Mode);
            yield return null;

            AssertSomethingDrawsThePlayer(r, "at the helm");
            Assert.IsTrue(r.RiderSr.enabled, "THE PILOT IS DRAWN — the whole point of the change");
            Assert.IsTrue(r.Rider.IsDrawing);
            Assert.IsFalse(r.Body.enabled, "and still exactly one figure");
        }

        [UnityTest]
        public IEnumerator TakingTheHelm_SEATSThePilotOnIt_RatherThanWhereverEWasPressed()
        {
            // E fires anywhere inside the helm reach, so a DRAWN pilot would otherwise stand up to that far
            // off the tiller — slop the hidden sprite used to conceal.
            var r = NewRig(rowed: true, helmReach: 3f);
            yield return null;

            Assert.IsTrue(r.Switcher.TryInteract(), "board");
            yield return null;
            Assert.IsTrue(r.Switcher.TryInteract(), "take the helm");
            yield return null;

            Assert.Less(Vector2.Distance(r.PlayerTransform.position, r.Switcher.HelmWorldPosition), 0.2f,
                        "the pilot stands ON the helm");
        }

        [UnityTest]
        public IEnumerator APulledHull_PutsHerPilotOnTheLooms()
        {
            var r = NewRig(rowed: true);
            yield return null;

            Assert.IsTrue(r.Switcher.TryInteract(), "board");
            yield return null;
            Assert.AreEqual(CharacterStance.Balance, r.Rider.RequestedStance,
                            "on a working deck the fisher braces");

            Assert.IsTrue(r.Switcher.TryInteract(), "take the helm");
            yield return null;
            Assert.AreEqual(CharacterStance.Oars, r.Rider.RequestedStance,
                            "a hull wearing oar sheets rows — read off her own visual asset, not from code");
            Assert.AreEqual(CharacterStance.Oars, r.Character.Stance,
                            "and the character presenter is the one told about it");
            Assert.IsTrue(r.Character.IsHeadingHeld,
                          "a pilot faces where the HULL points — motion cannot say, they are standing still");
        }

        [UnityTest]
        public IEnumerator ASteeredHull_PutsHerPilotAtTheWheel()
        {
            var r = NewRig(rowed: false);
            yield return null;

            Assert.IsTrue(r.Switcher.TryInteract(), "board");
            yield return null;
            Assert.IsTrue(r.Switcher.TryInteract(), "take the helm");
            yield return null;

            Assert.AreEqual(CharacterStance.Helm, r.Rider.RequestedStance,
                            "no oars wired, so she is steered");
        }

        // ---- the ride reaches the character's VISUAL, not their transform --------------------------

        [UnityTest]
        public IEnumerator ALeaningHullLeansItsPassenger_OnTheVISUALChild_NeverThePlayersOwnTransform()
        {
            var r = NewRig(rowed: true);
            yield return null;
            r.Rider.ConfigureRide(rideStrength: 1f, deckRollDegrees: 5f, deckHeavePixels: 1.6f,
                                  deckPitchLiftMeters: 0.02f, pixelsPerUnit: 32f, footing: 0.5f);

            Assert.IsTrue(r.Switcher.TryInteract(), "board");
            yield return null;

            // This hull carries no BoatWaveMotion, so the rider takes the documented fallback: the tilt the
            // hull applies to her own visual. Setting it directly is exactly what the transform rock writes.
            r.Hull.VisualTiltDegrees = 8f;
            yield return null;

            Assert.AreEqual(8f * 0.5f, r.Rider.Pose.RollDegrees, 1e-3f,
                            "a braced passenger takes her share of the hull's lean");
            Assert.AreEqual(8f * 0.5f, r.RiderSr.transform.localEulerAngles.z, 1e-2f,
                            "and the lean lands on the rider CHILD");
            Assert.AreEqual(0f, Quaternion.Angle(r.PlayerTransform.rotation, Quaternion.identity), 1e-3f,
                            "the player's own transform stays bolt upright — DeckWalkController's stomp is " +
                            "by design and this works WITH it");

            // …and a hull that stops leaning puts them back square, with nothing frozen.
            r.Hull.VisualTiltDegrees = 0f;
            yield return null;
            Assert.AreEqual(0f, r.Rider.Pose.RollDegrees, 1e-4f);
        }

        [UnityTest]
        public IEnumerator RideStrengthZero_RestoresTheOldBoltUprightRead_LiveOnTheRig()
        {
            var r = NewRig(rowed: true);
            yield return null;
            r.Rider.ConfigureRide(rideStrength: 0f, deckRollDegrees: 5f, deckHeavePixels: 1.6f,
                                  deckPitchLiftMeters: 0.02f, pixelsPerUnit: 32f, footing: 1f);

            Assert.IsTrue(r.Switcher.TryInteract(), "board");
            r.Hull.VisualTiltDegrees = 12f;
            yield return null;

            Assert.AreEqual(0f, r.Rider.Pose.RollDegrees, 0f, "the owner's A/B is level, not nearly level");
            Assert.AreEqual(0f, Quaternion.Angle(r.RiderSr.transform.localRotation, Quaternion.identity), 1e-3f);
        }

        // ---- nothing is left behind ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator SteppingAshore_HandsTheFigureBack_AndLeavesNoFrozenLean()
        {
            // A tight helm reach so E on deck steps ashore (at the dock) instead of taking the helm.
            var r = NewRig(rowed: true, helmReach: 0.1f);
            yield return null;

            Assert.IsTrue(r.Switcher.TryInteract(), "board");
            yield return null;
            r.Hull.VisualTiltDegrees = 9f;
            yield return null;
            Assert.IsTrue(r.RiderSr.enabled, "the rider is drawing on deck");
            Assert.AreNotEqual(0f, r.Rider.Pose.RollDegrees, "…and leaning");

            Assert.IsTrue(r.Switcher.TryInteract(), "at the dock, E steps ashore");
            Assert.AreEqual(ControlMode.OnFoot, r.Switcher.Mode);
            yield return null;

            AssertSomethingDrawsThePlayer(r, "back ashore");
            Assert.IsTrue(r.Body.enabled, "the player's own renderer draws again");
            Assert.IsFalse(r.RiderSr.enabled, "and the rider stands down");
            Assert.AreEqual(0f, Quaternion.Angle(r.RiderSr.transform.localRotation, Quaternion.identity), 1e-3f,
                            "no lean left frozen on the child");
            Assert.AreEqual(CharacterStance.Free, r.Character.Stance, "no stance left held either");
            Assert.IsFalse(r.Character.IsHeadingHeld, "and the facing is back on motion");
        }

        /// <summary>A scripted sea, so the hull genuinely rocks rather than waiting on the weather.</summary>
        private sealed class RoughSea : IEnvironmentService
        {
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                new Vector2(6f, 3f), Vector2.zero, tideHeight: 0f,
                HiddenHarbours.Core.SeaState.Moderate, visibility: 1f, seaState01: 0.4f);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        [UnityTest]
        public IEnumerator DisablingTheHullsWaveMotion_PutsHerPassengerBackSquare()
        {
            // The OnDisable claim, live — a region hop disables the hull's wave motion, and the fisher must
            // not be left frozen mid-lean. EditMode cannot make this claim: the editor does not run the
            // enable/disable callbacks for runtime scripts outside play mode. Here the callback really
            // fires, and the assertion is end-to-end: the RIDER goes level, not merely a published field.
            GameServices.Environment = new RoughSea();

            var r = NewRig(rowed: true);
            // Give her a real rock grid so BoatWaveMotion drives the frame path (the shipping dory's).
            r.Hull.ConfigureRock(new Sprite[8 * 8], 8);
            Assert.IsTrue(r.Hull.HasRockGrid, "harness: the rock grid must gate ON");
            var wave = r.Boat.gameObject.AddComponent<BoatWaveMotion>();
            wave.Configure(r.HullVisual, new SpriteHullPresenter(r.Hull));
            yield return null;

            Assert.IsTrue(r.Switcher.TryInteract(), "board");

            // Let the animator settle onto the live sea and start cycling. Bounded, and it says so if the
            // harness never rocks rather than silently proving nothing.
            bool rocked = false;
            for (int f = 0; f < 120 && !rocked; f++)
            {
                yield return null;
                rocked = wave.IsRocking && Mathf.Abs(r.Rider.Pose.RollDegrees) > 1e-4f;
            }
            Assert.IsTrue(rocked, "harness: a sea state of 0.4 must actually rock her and lean the fisher");

            wave.enabled = false;      // OnDisable — the same path a region hop takes
            yield return null;

            Assert.IsFalse(wave.IsRocking, "a disabled hull reports a level deck, never its last frame");
            Assert.AreEqual(0f, r.Rider.Pose.RollDegrees, 1e-4f, "and her passenger stands square again");
            Assert.AreEqual(0f, Quaternion.Angle(r.RiderSr.transform.localRotation, Quaternion.identity),
                            1e-3f, "with nothing frozen on the child transform");
        }

        // ---- THE DECK GOES UP AND DOWN, AND SO DOES SHE (owner, 2026-08-11) ------------------------
        //
        // "The character stays static in space with a rock animation but is not fixed to the same point
        // on the bobbing boat deck." The rock CYCLE reached her (that is everything above); the
        // DISPLACED RIDE — the sea lifting the whole boat, metres of it against the cycle's pixel or
        // two — did not reach her at all, so she held her physics-root altitude while the drawn deck
        // travelled past her. The laws live in EditMode (DeckRiderDisplacedRideTests, against a real
        // mesh chain and a scripted sea); what these two prove is the same thing over the LIVE loop —
        // that the hull's publication and the rider's read land in the right order in a real frame,
        // and that a sprite hull publishes the transform lift she applied herself.

        /// <summary>Give the rig a wave-coupled SPRITE hull on a live sea: a real rock grid (so
        /// BoatWaveMotion drives the frame path the shipped dory uses) and a real BoatWaveMotion
        /// wired through the presenter seam.</summary>
        private BoatWaveMotion GiveHerASeaToRideOn(Rig r)
        {
            GameServices.Environment = new RoughSea();
            r.Hull.ConfigureRock(new Sprite[8 * 8], 8);
            Assert.IsTrue(r.Hull.HasRockGrid, "harness: the rock grid must gate ON");
            var wave = r.Boat.gameObject.AddComponent<BoatWaveMotion>();
            wave.Configure(r.HullVisual, new SpriteHullPresenter(r.Hull));
            return wave;
        }

        /// <summary>The rider's lift, split into the two channels it is made of — the rock cycle
        /// recomputed from the phase the hull published (pure, stateless maths, pinned by
        /// <c>DeckRideMathTests</c>), and the DISPLACED RIDE read straight off the hull. Both are read
        /// after a frame has been drawn, so all three numbers belong to the same LateUpdate.</summary>
        private static void SplitTheLift(Rig r, BoatWaveMotion wave, out float rock, out float ride)
        {
            rock = DeckRideMath.Ride(wave.RockPhaseDegrees, 5f, 1.6f, 0.02f, 32f, 0.6f, 1f).LiftMeters;
            ride = r.Rider.HullDrawnRideMeters;
        }

        [UnityTest]
        public IEnumerator OnDeck_TheFisherRidesTheHullsDisplacedHEAVE_NotOnlyItsRock()
        {
            // THE DEFECT, over the live loop. Pre-fix the rider's lift is the rock term alone and the
            // ride term is simply absent from it, however far the boat heaves.
            var r = NewRig(rowed: true, helmReach: 0.1f);      // tight reach: stay ON THE DECK
            var wave = GiveHerASeaToRideOn(r);
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(1.5f, 0.6f));
            yield return null;

            r.Rider.ConfigureRide(rideStrength: 1f, deckRollDegrees: 5f, deckHeavePixels: 1.6f,
                                  deckPitchLiftMeters: 0.02f, pixelsPerUnit: 32f, footing: 0.6f,
                                  hullRide: 1f);
            Assert.IsTrue(r.Switcher.TryInteract(), "board");
            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode);

            // Bounded by the SEA, not by a frame count (headless frames are not wall time): sail until
            // she is genuinely riding, and say so if the harness never made her.
            int riding = 0;
            for (int f = 0; f < 400 && riding < 20; f++)
            {
                yield return null;
                SplitTheLift(r, wave, out float rock, out float ride);

                Assert.AreEqual(rock + ride, r.Rider.Pose.LiftMeters, 1e-3f,
                    "the fisher's lift is the deck's rock PLUS the ride the hull reports having drawn " +
                    "— read from the hull, never sampled a second time");
                Assert.AreEqual(r.Rider.Pose.LiftMeters, r.RiderSr.transform.localPosition.y, 1e-3f,
                    "…and it reaches the CHILD transform that is actually drawn");

                if (Mathf.Abs(ride) > 0.05f) riding++;
            }
            Assert.GreaterOrEqual(riding, 20,
                "the harness never lifted the boat more than 5 cm, so the identity above was vacuous — " +
                "check the displaced sea is published and the sea state is rough enough.");
        }

        [UnityTest]
        public IEnumerator OnDeck_WithNoDisplacedSea_TheFisherIsExactlyAsBefore()
        {
            // The negative control, live: with nothing published there is no ride to take, the hull
            // reports exactly 0, and the rider's lift is the rock cycle and nothing else — the picture
            // that shipped before any of this.
            var r = NewRig(rowed: true, helmReach: 0.1f);
            var wave = GiveHerASeaToRideOn(r);                // …but NO DisplacedSea.Publish
            yield return null;

            r.Rider.ConfigureRide(rideStrength: 1f, deckRollDegrees: 5f, deckHeavePixels: 1.6f,
                                  deckPitchLiftMeters: 0.02f, pixelsPerUnit: 32f, footing: 0.6f,
                                  hullRide: 1f);
            Assert.IsTrue(r.Switcher.TryInteract(), "board");

            bool rocked = false;
            for (int f = 0; f < 200; f++)
            {
                yield return null;
                SplitTheLift(r, wave, out float rock, out float ride);
                Assert.AreEqual(0f, ride, 0f,
                    "displaced sea OFF: the hull draws no ride, so she publishes exactly 0");
                Assert.AreEqual(rock, r.Rider.Pose.LiftMeters, 1e-3f,
                    "…and her passenger's lift is the rock cycle, untouched");
                rocked |= wave.IsRocking && Mathf.Abs(rock) > 1e-5f;
            }
            Assert.IsTrue(rocked,
                "harness: the hull never rocked at all, so 'the rock cycle is untouched' proved nothing");
        }

        // ---- ORIENTATION: the hull turns, the figure does not spin (owner playtest 2026-08-07) ------
        //
        // "The sprite doesn't follow the orientation of the boat, they slowly lose reference and spin
        // horizontally." Two separate leaks of the hull's rotation into the picture, and the tests below
        // drive a hull through sustained turning because both were INVISIBLE at small angles and total by
        // half a turn. Neither test asserts an angle it imposed itself: each reads the hull's LIVE drawn
        // heading and asserts a RELATION against it, so nothing here depends on physics leaving the boat
        // exactly where the turn put her.

        /// <summary>
        /// Turn the boat through at least <paramref name="minTotalDegrees"/> of heading, a step per frame,
        /// calling <paramref name="check"/> after each step with the hull's live drawn heading. Fails as a
        /// HARNESS error (not a defect) if the hull never actually came about.
        ///
        /// <para><b>The turn is written to the TRANSFORM with the body un-simulated, and that is a
        /// measurement decision, not a shortcut.</b> Everything under test runs in LateUpdate (the two
        /// upright stomps, the rider's pose, the hull's drawn heading) and is therefore correct at the
        /// moment the frame is DRAWN. A test resuming in Update reads the frame in between — and a physics
        /// step landing there moves the boat AFTER the stomp and BEFORE the read, so a perfectly square
        /// figure measures a fraction of a degree out. That reading is an artefact of where the coroutine
        /// stands in the frame, not of the picture, and chasing it with a tolerance would have hidden the
        /// real defect underneath it. Driving the rotation from the coroutine itself removes the ambiguity:
        /// nothing moves the hull between the LateUpdate that squares the figure and the assertion that
        /// reads it. Presentation does not care where the rotation came from.</para>
        ///
        /// <para><b>…and it lets the frame SETTLE before reading, which is the same kind of decision.</b> In
        /// the GAME a hull's rotation is written by PHYSICS, and FixedUpdate always precedes Update, so the
        /// rider states this frame's heading and the presenter draws it in the same frame — no lag. A test
        /// coroutine writes it from its own Update slot, and where that slot sits relative to the rider's
        /// execution order is Unity's business, not the contract's: read after a single frame and you can
        /// catch the pipeline mid-flush and measure a whole 45° facing bucket of "lag" the game never has.
        /// Settling removes the ambiguity, and it costs only frames — the loop is bounded by TURN, so the
        /// claim is unchanged.</para>
        /// </summary>
        private static IEnumerator TurnTheHull(Rig r, float minTotalDegrees,
                                               System.Action<float> check, float stepDegrees = 6f)
        {
            var rb = r.Boat.GetComponent<Rigidbody2D>();
            if (rb != null) rb.simulated = false;
            Transform boat = r.Boat.transform;
            float turned = 0f;
            float last = r.Hull.DrawnHeadingDegrees();
            // Bounded by TURN, never by frames: the loop measures how far she has come round, so the
            // assertion cannot quietly depend on how many frames a headless run happens to give it.
            for (int guard = 0; guard < 2000 && turned < minTotalDegrees; guard++)
            {
                boat.rotation = Quaternion.Euler(0f, 0f, boat.eulerAngles.z - stepDegrees);
                // The DRAWN heading of the rotation just written — a live read off the transform, so this is
                // the number the rider will state and the picture will settle on.
                float now = r.Hull.DrawnHeadingDegrees();
                yield return null;
                yield return null;
                yield return null;
                turned += Mathf.Abs(Mathf.DeltaAngle(last, now));
                last = now;
                check(now);
            }
            Assert.GreaterOrEqual(turned, minTotalDegrees,
                                  $"harness: the hull only came round {turned:F0}° — the drift this guards " +
                                  "against needs real turning to show, so a still hull proves nothing");
        }

        /// <summary>
        /// Give the rig's boat a MEASURED deck — one rectangular walkable area in hull metres — so the deck
        /// walk takes its polygon path rather than the greybox rectangle.
        ///
        /// <para><b>Why the tests that care about the deck FRAME must have one.</b> The two paths differ in
        /// exactly the way these tests are about. On the measured path the fisher's position is authoritative
        /// in HULL metres, so a heading change moves them not at all and their bearing on the deck is a fact
        /// about the boat. The greybox fallback instead holds a fixed WORLD-axis offset from the hull and
        /// derives the deck position from it each tick, so its deck position genuinely rotates as she
        /// turns — the fisher does not ride that deck round. Every measured hull in the fleet carries
        /// authored polygons (M2-37), so the polygon path is the one to pin.</para>
        /// </summary>
        private BoatDeckDef GiveHerAMeasuredDeck(Rig r)
        {
            var deck = ScriptableObject.CreateInstance<BoatDeckDef>();
            deck.Id = "deck.test_rect";
            deck.LoaMeters = 5f;
            var area = new DeckArea
            {
                Id = "sole",
                Kind = DeckAreaKind.Deck,
                Outline = new[]
                {
                    new Vector2(-0.7f, -1.6f), new Vector2(0.7f, -1.6f),
                    new Vector2(0.7f, 1.6f), new Vector2(-0.7f, 1.6f),
                },
                HeightPlane = Vector3.zero,
                Bounds = new Vector4(-0.7f, -1.6f, 0.7f, 1.6f),
            };
            deck.Areas = new[] { area };
            deck.WalkCenter = Vector2.zero;
            deck.WalkHalfExtents = new Vector2(0.7f, 1.6f);
            _spawned.Add(deck);
            Assert.IsTrue(deck.HasWalkableDeck(), "harness: the authored deck must be walkable");
            BoatDeckAreas.Write(r.Boat.gameObject, deck);
            return deck;
        }

        [UnityTest]
        public IEnumerator AtTheHelm_ATurningHullNeverRotatesThePilot_HoweverFarSheComesRound()
        {
            // THE DEFECT. The switcher DISABLES DeckWalkController at the helm, and that stomp was the only
            // thing keeping the player's world rotation square while parented to the hull's rotating physics
            // body. Hidden pilot, no symptom; #445 drew the pilot, and they lay over further with every
            // degree she turned — past 90° the fisher is lying on their side, which is the owner's
            // "spin horizontally". Pre-fix this fails within the first few steps.
            var r = NewRig(rowed: false);
            yield return null;

            Assert.IsTrue(r.Switcher.TryInteract(), "board");
            yield return null;
            Assert.IsTrue(r.Switcher.TryInteract(), "take the helm");
            yield return null;
            Assert.IsTrue(r.RiderSr.enabled, "harness: the pilot must be DRAWN, or there is nothing to spin");

            // No wave motion on this rig, so the ride pose is level and the drawn figure must be square.
            yield return TurnTheHull(r, minTotalDegrees: 540f, check: _ =>
            {
                Assert.AreEqual(0f, Quaternion.Angle(r.RiderSr.transform.rotation, Quaternion.identity), 0.01f,
                                "the DRAWN pilot is screen-square: their rotation is the ride pose and " +
                                "nothing else — never the hull's rotation inherited through the parent");
                Assert.AreEqual(0f, Quaternion.Angle(r.PlayerTransform.rotation, Quaternion.identity), 0.01f,
                                "and the player object itself stays upright at the helm, not only on deck");
            });
        }

        [UnityTest]
        public IEnumerator OnDeck_AStandingFisherTurnsWITHTheHull_AndKeepsTheirBearingOnHer()
        {
            // The FACING half. IsoCharacterSprite reads its facing off localPosition, which measures the
            // parent frame's ROTATION as motion: a hull coming about moves a motionless fisher in her frame
            // with no step taken, so the "velocity" is an artefact of the turn, its direction sweeps round,
            // and the facing chases it. The rider therefore states the facing instead — the fisher's deck
            // bearing plus the hull's DRAWN heading — so a standing fisher keeps a fixed bearing on the boat
            // however far she turns.
            var r = NewRig(rowed: true, helmReach: 0.1f);   // tight helm reach: stay ON THE DECK
            GiveHerAMeasuredDeck(r);                        // the fleet's path, where the deck frame is real
            yield return null;

            Assert.IsTrue(r.Switcher.TryInteract(), "board");
            yield return null;
            Assert.AreEqual(ControlMode.OnDeck, r.Switcher.Mode);
            Assert.IsTrue(r.Character.IsHeadingHeld,
                          "on deck the facing is STATED, because the frame under the fisher rotates");

            float bearing = r.Rider.DeckBearingDegrees;
            yield return TurnTheHull(r, minTotalDegrees: 720f, check: drawnHeading =>
            {
                Assert.AreEqual(bearing, r.Rider.DeckBearingDegrees, 1e-3f,
                                "nobody walked, so the fisher's bearing ON THE DECK never moved");
                Assert.AreEqual(0f, Mathf.DeltaAngle(
                                    HiddenHarbours.Core.DeckRiderFacingMath.CompassHeading(drawnHeading, bearing),
                                    r.Character.HeadingDegrees), 0.01f,
                                "and their compass facing is that bearing carried round by the hull — the " +
                                "DRAWN heading, exactly, with nothing accumulated");
                Assert.AreEqual(CharacterGait.Idle, r.Character.Gait,
                                "a fisher standing on a turning deck is STANDING — the turn must not be " +
                                "measured as a stride and drawn as a walk");
            });
        }

        [UnityTest]
        public IEnumerator OnDeck_WalkingTheDeck_StillFacesTheWalk()
        {
            // The fix must not have pinned the facing so hard that walking stopped turning them. Move the
            // fisher across the deck in the hull's own frame and the bearing must follow the step.
            var r = NewRig(rowed: true, helmReach: 0.1f);
            GiveHerAMeasuredDeck(r);
            yield return null;
            Assert.IsTrue(r.Switcher.TryInteract(), "board");
            yield return null;

            var deck = r.PlayerTransform.GetComponent<DeckWalkController>();
            Assert.IsTrue(deck.enabled, "harness: the deck walk drives the fisher while OnDeck");

            // Walk to STARBOARD in the hull's frame. SnapTo takes a boat-relative world offset, and this
            // hull is drawn bow-north (heading 0), so +x world IS +x abeam.
            float before = r.Rider.DeckBearingDegrees;
            for (int i = 1; i <= 12; i++)
            {
                deck.SnapTo(new Vector2(0.06f * i, 0.4f));
                yield return null;
            }

            Assert.AreNotEqual(before, r.Rider.DeckBearingDegrees,
                               "a fisher who crosses the deck is looking where they are going");
            Assert.AreEqual(90f, Mathf.Abs(Mathf.DeltaAngle(r.Rider.DeckBearingDegrees, 0f)), 25f,
                            "…and that is roughly abeam to starboard, the way they were moving");
        }

        [UnityTest]
        public IEnumerator SteppingAshore_HandsBackTheGaitToo_NotOnlyTheFacing()
        {
            // Both holds are one claim and must be released together: an on-foot fisher whose gait was left
            // stated at 0 would glide about the island in the idle pose.
            var r = NewRig(rowed: true, helmReach: 0.1f);
            yield return null;

            Assert.IsTrue(r.Switcher.TryInteract(), "board");
            yield return null;
            Assert.IsTrue(r.Character.IsSpeedHeld, "aboard, the deck states the honest travelling speed");

            Assert.IsTrue(r.Switcher.TryInteract(), "at the dock, E steps ashore");
            yield return null;

            Assert.IsFalse(r.Character.IsHeadingHeld, "ashore the facing is back on motion");
            Assert.IsFalse(r.Character.IsSpeedHeld, "and so is the gait");
        }

        [UnityTest]
        public IEnumerator TearingTheRiderDown_MidVoyage_NeverLeavesAnInvisiblePlayer()
        {
            // The one unrecoverable state. A region hop, a pooled player or a disabled component must all
            // hand the picture back to the body renderer rather than leaving nobody drawing.
            var r = NewRig(rowed: true);
            yield return null;

            Assert.IsTrue(r.Switcher.TryInteract(), "board");
            yield return null;
            Assert.IsTrue(r.RiderSr.enabled);

            r.Rider.enabled = false;                 // OnDisable → StandDown
            yield return null;

            AssertSomethingDrawsThePlayer(r, "with the rider torn down on deck");
            Assert.IsTrue(r.Body.enabled, "the body renderer takes the picture back");
            Assert.IsFalse(r.RiderSr.enabled);
        }

        [UnityTest]
        public IEnumerator ARigWithNoRiderChild_KeepsTheOldBehaviourExactly()
        {
            // Older scenes and every existing test build a player with no rider. They must be untouched:
            // visible ashore and on deck, hidden at the helm — the rule ControlSwitcher has always had.
            var r = NewRig(rowed: true);
            Object.Destroy(r.Rider);                 // no rider component at all
            yield return null;

            Assert.IsTrue(r.Body.enabled, "ashore");
            Assert.IsTrue(r.Switcher.TryInteract(), "board");
            yield return null;
            Assert.IsTrue(r.Body.enabled, "on deck, the body renderer draws as it always did");

            Assert.IsTrue(r.Switcher.TryInteract(), "take the helm");
            yield return null;
            Assert.IsFalse(r.Body.enabled, "and at the helm the figure is hidden, as it always was");
        }
    }
}
