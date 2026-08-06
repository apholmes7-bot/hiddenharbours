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
                Hull = directional, Boat = boat, HullDef = hullDef,
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
