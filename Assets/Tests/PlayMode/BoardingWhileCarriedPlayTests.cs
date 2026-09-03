using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// 🔴 <b>THE OWNER'S TELEPORT, REPRODUCED.</b> 2026-09-02: <i>"when exiting the boat the character
    /// doesnt always use the door and then sometimes would telport to the dory while the sprite stayed
    /// locked in the iso cabin."</i>
    ///
    /// <para><b>The mechanism, and it is not subtle once the two halves are put side by side.</b>
    /// <see cref="ArrivalOpening"/> carries the player in by POSITION and never touches
    /// <see cref="ControlSwitcher.Mode"/> — so for the whole passage the switcher believes she is
    /// <see cref="ControlMode.OnFoot"/>, standing whever her transform happens to be, with her OWN boat
    /// (the starter dory) as <c>Boat</c>. And on foot the switcher boards <b>first</b>, before the
    /// interact registry, on nothing more than
    /// <c>WithinBoardReach() &amp;&amp; BoardableNow()</c> — a plain 3.5 m radius to the dory's ROOT.</para>
    ///
    /// <para>So any press meant for the cabin door, or for the step ashore, is answered by BOARDING THE
    /// DORY the moment the cape's track brings the player's transform within 3.5 m of her. The switcher
    /// then transforms the player onto the dory while the arrival's <c>LateUpdate</c> goes on seating
    /// her, and <c>DeckRiderVisual</c> — which holds the cabin pose between <c>CabinEntered</c> and
    /// <c>CabinLeft</c> — is never told anything, so the SPRITE stays below decks. That is the owner's
    /// sentence, in order.</para>
    ///
    /// <para><b>⚠ The geometry is not marginal, it is the shipped layout.</b> St Peters puts the player
    /// down at the ratified disembark <c>(213.5, −1.9)</c>, and the committed scene moored the dory at
    /// <c>(215, 0)</c> — <b>2.42 m apart</b>, comfortably inside the 3.5 m reach. She is in range while
    /// still aboard, and stays in range standing on the planks.</para>
    ///
    /// <para><b>The fix under test:</b> the switcher listens to <c>CarriedAboardChanged</c> — which the
    /// arrival already publishes and which, before this, ONLY <c>CameraFollow</c> heard — and while she
    /// is carried, <c>WithinBoardReach()</c> and <c>CanStepAshore()</c> are false. The arrival owns both
    /// verbs for the length of the passage.</para>
    ///
    /// <para>⚠ Positions here are LITERALS rather than reads off <c>StPetersBuilder</c>, on purpose: what
    /// is under test is the geometry that triggered the defect, and it must go on being tested after the
    /// berth moves. The region's own layout is asserted in EditMode.</para>
    /// </summary>
    public class BoardingWhileCarriedPlayTests
    {
        // The St Peters arrangement that produced the bug, as measured on main.
        private static readonly Vector3 DisembarkPos = new Vector3(213.5f, -1.9f, 0f);
        private static readonly Vector3 DoryOnTheCentreLine = new Vector3(215f, 0f, 0f);   // committed scene
        private static readonly Vector3 DoryAtTheNorthFace = new Vector3(213.5f, 4.25f, 0f); // after PR #707
        private const float BoardReach = 3.5f;

        // The synthetic passage, for the journey test: she runs south down x = 0 and ties up at (0,0).
        private static readonly Vector2 RouteStart = new Vector2(0f, 60f);
        private static readonly Vector2 RouteBerth = new Vector2(0f, 0f);
        private const float BerthHeading = 180f;
        private static readonly Vector2 RouteAshore = new Vector2(3f, 0f);

        private sealed class FakeSave : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool v) && v;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() { }
        }

        private sealed class FlatBed : ITidalTerrain
        {
            public float Elevation = -4f;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FixedTide : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private readonly List<Object> _spawned = new List<Object>();
        private GameObject _root;
        private GameObject _playerGo;
        private ControlSwitcher _switcher;
        private BoatController _dory;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _root = Spawn("BoardingWhileCarriedFixture");
            _root.AddComponent<AudioListener>();

            GameServices.Environment = new FixedTide();
            GameServices.TidalTerrain = new FlatBed();
            GameServices.Save = new FakeSave();

            _playerGo = Spawn("Player");
            _playerGo.AddComponent<SpriteRenderer>();
            var walk = _playerGo.AddComponent<PlayerWalkController>();
            GameServices.PlayerTransform = _playerGo.transform;

            var doryGo = Spawn("Dory");
            doryGo.transform.position = DoryOnTheCentreLine;
            _dory = doryGo.AddComponent<BoatController>();
            var input = doryGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory"; hull.DraughtMeters = 0.3f; hull.CameraWorldHeightMeters = 14f;
            hull.LengthMeters = 4.5f; hull.Propulsion = PropulsionType.Oars;
            _spawned.Add(hull);
            _dory.SetHull(hull);
            _dory.enabled = false; input.enabled = false;

            var dockZone = Spawn("DockZone"); dockZone.transform.position = DoryOnTheCentreLine;
            var disembark = Spawn("Disembark"); disembark.transform.position = DisembarkPos;

            _switcher = Spawn("Switcher").AddComponent<ControlSwitcher>();
            _switcher.Configure(walk, _dory, input, dockZone.transform, BoardReach, disembark.transform);
        }

        [TearDown]
        public void TearDown()
        {
            // ⚠ Publish the release rather than Clear<T>()-ing: EventBus.Clear UNSUBSCRIBES every
            // listener, so a fixture that clears leaves the NEXT test's switcher deaf to the signal it
            // is about to be asserted on.
            EventBus.Publish(new CarriedAboardChanged(false));
            Interactables.Clear();
            GameServices.Save = null;
            GameServices.PlayerTransform = null;
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        /// <summary>Stand the player where the arrival would be holding her — a given distance off the
        /// dory's root, which is the only thing the reach test measures.</summary>
        private void StandHerOff(Vector3 doryPos, float metres)
        {
            _dory.transform.position = doryPos;
            _playerGo.transform.position = doryPos + new Vector3(metres, 0f, 0f);
        }

        // =============================================================================================
        //  1. 🔴 the defect itself
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>The press that was being stolen.</b> While the arrival is carrying her, E within board
        /// reach of the moored dory must do nothing at all — it is the cabin door's press, or the step
        /// ashore's, and the dory is 200 m of passage away from being hers to board.
        /// </summary>
        [UnityTest]
        public IEnumerator PressingInteractWhileCarried_NeverBoardsTheMooredDory(
            [Values(0f, 1.5f, 2.42f, 3.4f)] float standOff)
        {
            StandHerOff(DoryOnTheCentreLine, standOff);
            yield return null;

            Assert.AreEqual(ControlMode.OnFoot, _switcher.Mode, "she starts on foot (the arrival never " +
                            "changes the switcher's mode — that is the premise of the whole defect)");

            EventBus.Publish(new CarriedAboardChanged(true));      // …the arrival seats her
            yield return null;

            Vector3 wasAt = _playerGo.transform.position;
            bool moved = _switcher.BeginInteract();

            Assert.IsFalse(moved,
                $"E was answered while she is being CARRIED, {standOff:F2} m from the moored dory's " +
                "root. That press belongs to the cabin door or the step ashore; the switcher took it " +
                "and boarded her own boat out from under the arrival.");
            Assert.AreEqual(ControlMode.OnFoot, _switcher.Mode,
                "the switcher went OnDeck of the dory in the middle of somebody else's passage — this " +
                "is the owner's teleport, and DeckRiderVisual is never told, so the sprite stays in the " +
                "iso cabin");
            Assert.AreEqual(wasAt, _playerGo.transform.position,
                "her transform was moved by a press she should not have been able to make");

            Assert.IsFalse(_switcher.WithinBoardReach(),
                "while she is carried, board reach is not a question the switcher may answer yes to");
            Assert.IsFalse(_switcher.CanInteract(),
                "…and the popup must not offer BOARD either, or the player is invited to do it");
        }

        /// <summary>
        /// 🔴 And the same at the berth this arc moves her to (PR #707): the fix must be about the
        /// CARRY, not about a coordinate that happens to be far enough away this month.
        /// </summary>
        [UnityTest]
        public IEnumerator PressingInteractWhileCarried_NeverBoardsHer_AtTheNorthFaceBerthEither()
        {
            StandHerOff(DoryAtTheNorthFace, 2f);
            yield return null;

            EventBus.Publish(new CarriedAboardChanged(true));
            yield return null;

            Assert.IsFalse(_switcher.BeginInteract(), "a berth is not a fix for a press-routing bug");
            Assert.AreEqual(ControlMode.OnFoot, _switcher.Mode);
        }

        /// <summary>
        /// ⚠ <b>The listener has to exist before the signal.</b> A subscription taken lazily — on the
        /// first press, say — would pass every test above and still miss the real arrival, which
        /// publishes <c>CarriedAboardChanged(true)</c> during its own <c>TryBegin</c>, long before the
        /// player touches a key. The switcher is built and enabled in <c>SetUp</c>; this asserts it was
        /// already listening then, rather than assuming it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSwitcherIsAlreadyListening_WhenTheArrivalSeatsHer()
        {
            StandHerOff(DoryOnTheCentreLine, 2f);
            yield return null;

            Assert.IsTrue(_switcher.WithinBoardReach(), "premise: she is in reach before the carry");
            EventBus.Publish(new CarriedAboardChanged(true));

            // No frame yielded, and no press made: the gate must already have closed on the signal alone.
            Assert.IsFalse(_switcher.WithinBoardReach(),
                "the switcher only noticed it was being carried after something else prompted it — a " +
                "lazy subscription cannot hear a signal published before the first press");
            yield return null;
        }

        // =============================================================================================
        //  2. …and the fix must not brick the ordinary verb
        // =============================================================================================

        /// <summary>
        /// ⚠ The other half, and the one a one-line gate gets wrong: once she is PUT ASHORE the dory is
        /// hers again. A flag that latches on and never clears would leave the player standing on the
        /// planks beside their own boat, unable to board it, for the rest of the save.
        /// </summary>
        [UnityTest]
        public IEnumerator OnceTheArrivalPutsHerDown_SheCanBoardHerOwnBoatAgain()
        {
            StandHerOff(DoryOnTheCentreLine, 2f);
            yield return null;

            EventBus.Publish(new CarriedAboardChanged(true));
            yield return null;
            Assert.IsFalse(_switcher.WithinBoardReach(), "premise: carried");

            EventBus.Publish(new CarriedAboardChanged(false));     // …the arrival hands her over
            yield return null;

            Assert.IsTrue(_switcher.WithinBoardReach(),
                "the carry flag latched on: she is ashore and cannot board her own dory any more");
            Assert.IsTrue(_switcher.BeginInteract(), "E must board her now");
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode, "…and that is what boarding means");
        }

        /// <summary>A switcher that was never told anything behaves exactly as it always did — the gate
        /// is a NEW refusal on a new signal, not a change to the default.</summary>
        [UnityTest]
        public IEnumerator WithNoArrivalInThePicture_BoardingIsUntouched()
        {
            StandHerOff(DoryOnTheCentreLine, 2f);
            yield return null;

            Assert.IsTrue(_switcher.WithinBoardReach());
            Assert.IsTrue(_switcher.BeginInteract());
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode);
        }

        // =============================================================================================
        //  3. ⭐ the journey — the real ArrivalOpening carrying her past her own moored boat
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The whole passage, with a finger on E the entire way.</b> The tests above drive the
        /// signal by hand; this one lets the real <see cref="ArrivalOpening"/> publish it, carries the
        /// player down a route that passes within board reach of her moored dory, and presses INTERACT
        /// every single frame — which is the worst case of the owner's "sometimes".
        ///
        /// <para>Before the fix this boards the dory somewhere in the middle of the passage and the run
        /// ends OnDeck of a boat 60 m astern.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator CarriedDownAWholePassage_AFingerOnE_SheNeverBoardsTheMooredDory()
        {
            var skipper = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(
                "Assets/_Project/Data/Boats/Skippers/StPetersArrivalSkipper.asset");
            Assert.IsNotNull(skipper, "the arrival skipper def must exist for this journey");

            // Her dory lies off the track she is carried down — the shipped geometry's own problem,
            // restated at fixture scale. ⚠ MEASURED, not guessed: the passenger rides a seat on the
            // skipper's deck, not his root, and that seat passes x ≈ −1.6 here. A mooring at x = +2
            // (the obvious "2 m to starboard") therefore sits 3.60 m from her — just OUTSIDE the 3.5 m
            // reach, so the run went green while proving nothing. It is on her port hand instead, two
            // metres off the seat and 3.6 m off the skipper's own hull so the two never touch.
            var mooring = new Vector3(-3.6f, 20f, 0f);
            _dory.transform.position = mooring;
            _playerGo.transform.position = new Vector3(999f, 999f, 0f);

            var go = Spawn("ArrivalOpening");
            go.SetActive(false);
            var opening = go.AddComponent<ArrivalOpening>();
            opening.Configure(skipper, new[] { RouteStart, RouteBerth }, RouteBerth, BerthHeading,
                              RouteAshore, channelBedElevation: -4f);
            go.SetActive(true);

            Assert.IsTrue(opening.TryBegin(), "the arrival must actually start, or nothing is under test");

            float closest = float.MaxValue;
            float deadline = Time.realtimeSinceStartup + 120f;
            while (opening.Current != ArrivalOpening.Phase.Moored &&
                   Time.realtimeSinceStartup < deadline)
            {
                _switcher.BeginInteract();                       // …a finger held on E, every frame
                closest = Mathf.Min(closest,
                    Vector2.Distance(_playerGo.transform.position, mooring));

                Assert.AreEqual(ControlMode.OnFoot, _switcher.Mode,
                    $"she boarded the moored dory mid-passage — {closest:F2} m was close enough for the " +
                    "on-foot board to answer a press the arrival owned. This is the owner's teleport.");
                yield return null;
            }

            Assert.AreEqual(ArrivalOpening.Phase.Moored, opening.Current,
                "she never tied up, so the passage this test watched is not the whole passage");
            Assert.Less(closest, BoardReach,
                $"the carry never brought her within board reach of the dory (closest {closest:F2} m " +
                $"against a {BoardReach:F2} m reach), so this run could not have caught the defect even " +
                "if it were still there — move the mooring closer to the route");

            Debug.Log($"[boarding/carried] carried the whole passage with E held down; closest approach " +
                      $"to the moored dory's root was {closest:F2} m against a {BoardReach:F2} m reach.");
        }
    }
}
