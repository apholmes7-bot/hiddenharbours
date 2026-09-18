using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>THE PASSENGER IS DRAWN AS HER MESH, THE WHOLE WAY IN.</b> The owner, playtest 2026-09-18:
    /// <i>"character was not mesh on intro boat."</i> The player has been a skinned mesh aboard since the
    /// switch shipped ON (ADR 0044 d), but the arrival wrote her stance and holds straight onto the SPRITE
    /// drawer, and the deck rider that poses the mesh was never told she was aboard — so it stood down and
    /// the sheets drew her all the way to the wharf.
    ///
    /// <para><b>The seam under test.</b> The arrival now states one thing per frame through Core's
    /// <see cref="ICarriedFigure"/> — this hull, this stand point, this stance, this heading, this speed —
    /// and the live figure decides how she is drawn. Three guards, by name, asked on EVERY frame of the
    /// passage below decks and on deck rather than once at the end:</para>
    /// <list type="bullet">
    /// <item><b>(a)</b> the mesh figure is the enabled renderer, and neither sprite is;</item>
    /// <item><b>(b)</b> her held heading reaches the mesh: the figure's yaw is her held compass heading
    /// less the heading his hull was DRAWN at, in the skin's own sign;</item>
    /// <item><b>(c)</b> after the step ashore the holds are released on the live figure — at the press,
    /// and again once she is standing on the wharf and the sprite has her back.</item>
    /// </list>
    ///
    /// <para><b>⚠ Her rig is built BEFORE the arrival begins.</b> The arrival resolves the figure it talks
    /// to inside <c>TryBegin</c>, seats her in the same call and publishes the first
    /// <see cref="CabinEntered"/> there too, so a rig wired afterwards would be one the arrival never met
    /// and a rider that never heard which cabin she is in.</para>
    ///
    /// <para><b>⚠ The switch is set ON here, not inherited.</b> <c>GameConfig.MeshCharacter</c> is the
    /// owner's to flip. This fixture is about the path it selects, so it states the switch rather than
    /// reading whatever the default is that week.</para>
    ///
    /// <para><b>⚠ Guard (b) pairs a figure with the hull it was placed against.</b> The figure is placed in
    /// LateUpdate from a deck bearing composed in Update, and a test resumes between the two: after the
    /// frame's Updates, before its LateUpdates. So the yaw read on this resume was composed against the hull
    /// heading read on the PREVIOUS resume (the physics root does not move between a frame's Update and its
    /// LateUpdate), and the heading the sheets report is the one that frame held (a hold is only taken up in
    /// the sheets' own LateUpdate). Reading both off the same resume would compare a figure with a hull one
    /// physics step on, and call a turning boat a broken seam.</para>
    ///
    /// <para><b>Not here:</b> the skipper (the cast lane's, #863) and rig 7's pose debts (helm and oars
    /// clips). Driven through component APIs and waited on STATES with wall-clock ceilings, for
    /// <c>IntroCabinPassagePlayTests</c>' reasons, whose journey and helpers this borrows.</para>
    /// </summary>
    public class IntroPassengerMeshFigurePlayTests
    {
        private const float TimeoutSeconds = 90f;

        /// <summary>How far the figure's yaw may sit from the one her held heading and his drawn heading
        /// compose to. Both sides come from the same two floats through two wraps, so the honest difference
        /// is float noise, millidegrees; a degree is room for that and nothing else. A figure still facing
        /// the sheets' OLD heading, or facing along the hull, misses by tens of degrees.</summary>
        private const float YawToleranceDegrees = 1f;

        /// <summary>The least turn she must be SEEN to make, below decks and again on deck, before "the
        /// yaw followed her heading" means anything: a figure frozen at one bearing agrees with a frozen
        /// heading on every frame. Her walks take her toward his door and away from it, and across, so an
        /// eighth of a turn is a floor with plenty of room under what she does.</summary>
        private const float LeastSweepDegrees = 45f;

        /// <summary>The fewest watched frames in each place for the per-frame guards to have been asked
        /// there at all.</summary>
        private const int LeastFramesWatched = 5;

        /// <summary>The player's own art: the sheets she was drawn with and the skin that makes her a
        /// mesh. The shipped def, so the claim is about HER and not about a fixture's stand-in.</summary>
        private const string FisherVisualPath = "Assets/_Project/Data/Characters/FisherIso.asset";

        private sealed class FakeSave : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = new SaveData();
            public int Saves;
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool v) && v;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() => Saves++;
        }

        /// <summary>The widest an angle has swung from the first one seen: how far she was seen to turn.</summary>
        private sealed class Sweep
        {
            private bool _seen;
            private float _first;
            public float Widest { get; private set; }

            public void See(float degrees)
            {
                if (!_seen) { _seen = true; _first = degrees; return; }
                Widest = Mathf.Max(Widest, Mathf.Abs(Mathf.DeltaAngle(_first, degrees)));
            }
        }

        private GameObject _root;
        private GameObject _player;
        private ArrivalOpening _opening;
        private BoatOwnerDef _skipper;
        private GameConfig _config;
        private CharacterVisualDef _visual;

        // Her rig, wired the way the player's is: the body renderer and its sheets, the rider child, the rider.
        private SpriteRenderer _body;
        private SpriteRenderer _riderSprite;
        private IsoCharacterSprite _sheets;
        private DeckRiderVisual _rider;

        // The watch: guards (a) and (b), asked after every frame the journey yields.
        private bool _watching;
        private bool _havePriorHull;
        private float _priorHullHeading;
        private int _framesBelow;
        private int _framesOnDeck;
        private Sweep _headingBelow;
        private Sweep _headingOnDeck;
        private Sweep _yawBelow;
        private Sweep _yawOnDeck;

        // The same open water, straight run and tie-up as IntroCabinPassagePlayTests.
        private static readonly Vector2 Start = new Vector2(0f, 60f);
        private static readonly Vector2 Berth = new Vector2(0f, 0f);
        private const float BerthHeading = 180f;
        private static readonly Vector2 Ashore = new Vector2(3f, 0f);

        /// <summary>The shipped come-alongside at tightened numbers, copied from
        /// <c>IntroCabinPassagePlayTests</c> so the approach behaves exactly as there.</summary>
        private static BerthPilot.Settings FixtureAlongside()
        {
            BerthPilot.Settings s = BerthPilot.Settings.Default;
            s.BerthingSpeedMetresPerSecond = 2f;
            s.SetRateMetresPerSecond = 0.8f;
            s.GateStandoffMetres = 1.5f;
            s.GateCaptureMetres = 8f;
            return s;
        }

        [SetUp]
        public void SetUp()
        {
            _watching = false;
            _havePriorHull = false;
            _framesBelow = 0;
            _framesOnDeck = 0;
            _headingBelow = new Sweep();
            _headingOnDeck = new Sweep();
            _yawBelow = new Sweep();
            _yawOnDeck = new Sweep();

            _root = new GameObject("IntroPassengerMeshFixture");
            _root.AddComponent<AudioListener>();   // one listener, or a full suite logs on every frame

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.MeshCharacter = true;
            GameServices.Config = _config;

            _player = new GameObject("Player");
            _player.transform.SetParent(_root.transform);
            _player.transform.position = new Vector3(999f, 999f, 0f);
            GameServices.PlayerTransform = _player.transform;

            _visual = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(FisherVisualPath);
            _body = _player.AddComponent<SpriteRenderer>();
            _sheets = _player.AddComponent<IsoCharacterSprite>();
            _sheets.Configure(_visual);
            var riderGo = new GameObject("DeckRider");
            riderGo.transform.SetParent(_player.transform, false);
            _riderSprite = riderGo.AddComponent<SpriteRenderer>();
            _riderSprite.enabled = false;
            _rider = _player.AddComponent<DeckRiderVisual>();
            _rider.Configure(_riderSprite, _body, _sheets);

            _skipper = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatOwnerDef>(
                "Assets/_Project/Data/Boats/Skippers/StPetersArrivalSkipper.asset");

            MooringCleats.Clear();
            var bollard = new GameObject("Bollard");
            bollard.transform.SetParent(_root.transform);
            bollard.transform.position = new Vector3(Ashore.x + 2f, Ashore.y, 0f);
            bollard.AddComponent<ShoreCleat>().Configure("fixture.bollard", elevationMeters: 1.5f);
        }

        [TearDown]
        public void TearDown()
        {
            _watching = false;
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            MooringCleats.Clear();
            Interactables.Clear();
            GameServices.Save = null;
            GameServices.PlayerTransform = null;
            GameServices.Config = null;
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
            GameServices.Reset();
        }

        // =============================================================================================
        //  ⭐⭐ THE ARRIVAL, BELOW AND ON DECK, AND THE STEP ASHORE
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>SHE IS HER MESH BELOW, HER MESH ON DECK, TURNED BY HER HELD HEADING, AND HERSELF AGAIN
        /// ASHORE.</b> The whole passage from his cabin to the wharf, with guards (a) and (b) asked after
        /// every frame and guard (c) at the press and on the planks. See the class doc for each by name.
        /// </summary>
        [UnityTest]
        public IEnumerator TheArrival_DrawsHerAsHerMesh_BelowAndOnDeck_TurnedByHerHeldHeading_AndHandsHerBackAshore()
        {
            Assert.IsNotNull(_skipper, "the arrival skipper asset is missing");
            Assert.IsNotNull(_visual, $"the player's art def is missing at {FisherVisualPath}");
            Assert.IsNotNull(_visual.Skin, "the player's art def carries no skin, so there is no mesh to draw");
            Assert.IsTrue(_visual.Skin.DrawsAsMesh(CharacterSkinStateMap.Balance),
                          "her skin does not draw the deck brace ('balance') as a mesh, and the brace is " +
                          "what she stands in the whole way in; this would be measuring the sheets");

            ArrivalOpening opening = Build();
            Assert.IsTrue(opening.TryBegin(), "the opening refused to begin");
            Assert.IsNotNull(opening.Boat, "no boat was spawned");
            Assert.IsNotNull(opening.Cabin, "his hull carries no cabin to start her in");
            Assert.IsTrue(opening.IsBelowDecks, "a new game did not start her below. " + Where());

            yield return null;
            yield return null;

            IsoFacetHullRenderer facets = opening.Boat.GetComponentInChildren<IsoFacetHullRenderer>(true);
            Assert.IsTrue(facets != null && facets.PosedMesh != null,
                          "his hull is not drawn as a facet mesh, so there is no facet pass for her figure " +
                          "to stand in and this fixture would be asking a question the hull cannot answer");
            Assert.IsNotNull(_player.GetComponent<DeckRiderMeshPresenter>(),
                             "the rider never put a mesh presenter on her: it was not told she is aboard. " +
                             Where());

            // ---- below decks: step into his cabin and across it -------------------------------------
            _watching = true;
            yield return StepClearOfHisDoorway(opening);
            yield return WalkAgainstHisDoor(away: 1f, across: 0f, seconds: 0.4f);
            yield return WalkAgainstHisDoor(away: 0f, across: 1f, seconds: 0.35f);
            yield return WalkAgainstHisDoor(away: 0f, across: -1f, seconds: 0.35f);

            // ---- up through his open door, and about his deck ---------------------------------------
            yield return ComeUpOnDeck(opening);
            yield return WalkAgainstHisDoor(away: 1f, across: 0f, seconds: 0.35f);
            yield return WalkAgainstHisDoor(away: 0f, across: 1f, seconds: 0.35f);
            yield return WalkAgainstHisDoor(away: 0f, across: -1f, seconds: 0.35f);

            // ---- alongside, still watched ------------------------------------------------------------
            yield return Until(() => opening.Current == ArrivalOpening.Phase.Moored, "tied up");
            yield return Until(() => opening.CanStepAshore, "offered the step ashore");
            _watching = false;

            Assert.GreaterOrEqual(_framesBelow, LeastFramesWatched,
                                  "too few frames were watched BELOW for the guards to have been asked there");
            Assert.GreaterOrEqual(_framesOnDeck, LeastFramesWatched,
                                  "too few frames were watched ON DECK for the guards to have been asked there");
            Assert.Greater(_headingBelow.Widest, LeastSweepDegrees,
                           $"below decks her held heading only swung {_headingBelow.Widest:F1}°, so guard (b) " +
                           "compared a still figure with a still heading");
            Assert.Greater(_yawBelow.Widest, LeastSweepDegrees,
                           $"below decks the mesh only turned {_yawBelow.Widest:F1}° while she walked about " +
                           "his cabin");
            Assert.Greater(_headingOnDeck.Widest, LeastSweepDegrees,
                           $"on deck her held heading only swung {_headingOnDeck.Widest:F1}°, so guard (b) " +
                           "compared a still figure with a still heading");
            Assert.Greater(_yawOnDeck.Widest, LeastSweepDegrees,
                           $"on deck the mesh only turned {_yawOnDeck.Widest:F1}° while she walked his deck");

            // ---- (c) the step ashore hands her back -------------------------------------------------
            Assert.IsTrue(opening.StepAshore(), "the offered step ashore was refused. " + Where());
            AssertHerHoldsAreReleased("at the press");

            yield return Until(() => opening.Current == ArrivalOpening.Phase.HandedOver, "handed over");
            yield return null;
            AssertHerHoldsAreReleased("on the wharf");

            var presenter = _player.GetComponent<DeckRiderMeshPresenter>();
            Assert.IsFalse(presenter != null && presenter.DrawsInsteadOfSprite,
                           "she is standing on the wharf and the mesh presenter still claims her");
            Assert.IsTrue(presenter == null || presenter.Figure == null || !presenter.Figure.Visible,
                          "she is ashore and her deck figure is still showing");
            Assert.IsTrue(Draws(_body), "she is ashore and her own sprite is not drawing her — she is invisible");
            Assert.IsFalse(_rider.SpriteSuppressed, "the rider still claims to hold her sprite off, ashore");
        }

        // =============================================================================================
        //  the guards
        // =============================================================================================

        /// <summary>
        /// ⭐ Guards (a) and (b), asked after every frame the journey yields. Each assert is a branch, not a
        /// formatted message built every frame (rule 7 holds in fixtures too: the text is only made on the
        /// frame that fails).
        /// </summary>
        private void Watch()
        {
            if (!_watching || _opening == null || _opening.Boat == null) return;

            if (_havePriorHull)
            {
                var presenter = _player.GetComponent<DeckRiderMeshPresenter>();
                if (presenter == null)
                    Assert.Fail("her rig lost its mesh presenter during the arrival. " + Where());

                // (a) the mesh figure is the enabled renderer, and the sprite is not.
                if (!presenter.DrawsInsteadOfSprite)
                    Assert.Fail("(a) the mesh presenter stood down during the arrival: \"" +
                                presenter.NotDrawingReason + "\". " + Where());
                IsoCharacterFigureRenderer figure = presenter.Figure;
                if (figure == null || !figure.Visible || !figure.gameObject.activeInHierarchy)
                    Assert.Fail("(a) the presenter says it draws her, but its figure is not showing. " + Where());
                if (Draws(_body) || Draws(_riderSprite))
                    Assert.Fail($"(a) a sprite is drawing her as well as the mesh (body {Draws(_body)}, " +
                                $"rider {Draws(_riderSprite)}): two of her. " + Where());
                if (!_rider.SpriteSuppressed)
                    Assert.Fail("(a) the rider does not report her sprite held off while the mesh draws. " + Where());
                if (!_rider.IsCarried)
                    Assert.Fail("(a) the arrival stopped carrying her before she stepped ashore. " + Where());

                // (b) her held heading reaches the mesh.
                if (!_sheets.IsHeadingHeld || !_sheets.IsSpeedHeld)
                    Assert.Fail($"(b) her heading and speed are not held (heading {_sheets.IsHeadingHeld}, " +
                                $"speed {_sheets.IsSpeedHeld}), so the arrival is not stating them. " + Where());
                float held = _sheets.HeadingDegrees;
                float sign = _visual.Skin.AzimuthCounterClockwise ? -1f : 1f;
                float expected = sign * Mathf.Repeat(held - _priorHullHeading, 360f);
                float yaw = presenter.FigureYawDegrees;
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, expected)) > YawToleranceDegrees)
                    Assert.Fail($"(b) the mesh is yawed {yaw:F2}°, but her held heading {held:F2}° on his hull " +
                                $"drawn at {_priorHullHeading:F2}° puts her at {expected:F2}°. " + Where());

                if (_opening.IsBelowDecks)
                {
                    _framesBelow++;
                    _headingBelow.See(held);
                    _yawBelow.See(yaw);
                }
                else
                {
                    _framesOnDeck++;
                    _headingOnDeck.See(held);
                    _yawOnDeck.See(yaw);
                }
            }

            _priorHullHeading = HisDrawnHeading();
            _havePriorHull = true;
        }

        /// <summary>(c): nothing is carrying her, her stance is free, and neither hold is on.</summary>
        private void AssertHerHoldsAreReleased(string when)
        {
            Assert.IsFalse(_rider.IsCarried, $"(c) {when}, the rider still says she is carried. " + Where());
            Assert.IsFalse(_sheets.IsHeadingHeld, $"(c) {when}, her heading is still held. " + Where());
            Assert.IsFalse(_sheets.IsSpeedHeld, $"(c) {when}, her speed is still held. " + Where());
            Assert.AreEqual(CharacterStance.Free, _sheets.Stance,
                            $"(c) {when}, she is still braced. " + Where());
        }

        /// <summary>Whether a sprite is putting her on screen at all: enabled, not forced off, and live in
        /// the hierarchy. Hiding a sprite either way hides her, so the guard asks the question and not the
        /// method.</summary>
        private static bool Draws(SpriteRenderer sprite)
            => sprite != null && sprite.enabled && !sprite.forceRenderingOff
               && sprite.gameObject.activeInHierarchy;

        /// <summary>His hull's DRAWN heading, read the way the rider and the deck walk read it: the host's
        /// presenter, else the one resolved off the root.</summary>
        private float HisDrawnHeading()
        {
            var host = _opening.Boat.GetComponent<BoatHullPresenterHost>();
            IBoatHullPresenter hull = host != null ? host.Presenter : null;
            if (hull == null) hull = BoatHullPresenterHost.Resolve(_opening.Boat.gameObject);
            return hull != null
                ? hull.DrawnHeadingDegrees()
                : DirectionalBoatSprite.HeadingDegreesFromBow(_opening.Boat.transform.up);
        }

        // =============================================================================================
        //  the journey (IntroCabinPassagePlayTests' helpers, each watching after every frame)
        // =============================================================================================

        private ArrivalOpening Build()
        {
            GameServices.Save = new FakeSave();

            var go = new GameObject("ArrivalOpening");
            go.transform.SetParent(_root.transform);
            go.SetActive(false);
            var opening = go.AddComponent<ArrivalOpening>();
            opening.Configure(_skipper, new[] { Start, Berth }, Berth, BerthHeading, Ashore,
                              channelBedElevation: -4f);
            opening.ConfigurePilot(ArrivalPilot.Settings.Default);
            opening.ConfigureAlongside(FixtureAlongside());
            go.SetActive(true);
            _opening = opening;
            return opening;
        }

        private IEnumerator Until(Func<bool> reached, string what)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!reached() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Watch();
            }
            Assert.IsTrue(reached(), $"the passage never {what} within {TimeoutSeconds:F0} s. " + Where());
        }

        private string Where()
        {
            if (_opening == null) return "There is no opening.";
            string boat = _opening.Boat == null
                ? "no boat"
                : $"she is at ({_opening.Boat.transform.position.x:F1}, " +
                  $"{_opening.Boat.transform.position.y:F1}) making " +
                  $"{_opening.Boat.Velocity.magnitude:F2} m/s";
            return $"[{_opening.Current}/{_opening.Pilotage}] {boat}; the player is " +
                   (_opening.IsBelowDecks
                        ? $"BELOW at sole {_opening.CabinLocalPosition}"
                        : "ON DECK") +
                   $" at {_player.transform.position}.";
        }

        /// <summary>
        /// Walk her for <paramref name="seconds"/> of wall clock along a direction stated against his
        /// door: <paramref name="away"/> parts of "away from it", <paramref name="across"/> parts of "across
        /// it", both re-read off the door's LIVE position every frame (it rides his hull). Neither part ever
        /// closes on the door, so no walk here takes her through it by accident; the one crossing each way is
        /// <see cref="ComeUpOnDeck"/>'s. Below decks the cabin walk, on deck the deck walk.
        /// </summary>
        private IEnumerator WalkAgainstHisDoor(float away, float across, float seconds)
        {
            BoatCabinDoor door = _opening.CabinDoor;
            Assert.IsNotNull(door, "his cabin has no door to walk against");
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                Vector2 fromDoor = (Vector2)_player.transform.position - (Vector2)door.transform.position;
                Vector2 out1 = fromDoor.sqrMagnitude > 1e-6f ? fromDoor.normalized : Vector2.down;
                Vector2 side = new Vector2(-out1.y, out1.x);
                Vector2 input = out1 * away + side * across;

                if (_opening.IsBelowDecks) _opening.WalkTheCabin(input, Time.deltaTime);
                else                       _opening.WalkTheDeck(input, Time.deltaTime);
                yield return null;
                Watch();
            }
        }

        /// <summary>Walk her to the threshold, steering by the door's LIVE position, until she changes
        /// floors (IntroCabinPassagePlayTests' walk, watched).</summary>
        private IEnumerator SheWalksThroughHisOpenDoor(ArrivalOpening opening)
        {
            BoatCabinDoor door = opening.CabinDoor;
            Assert.IsNotNull(door, "her cabin has no door to walk through");
            Assert.IsTrue(door.IsOpen, "this walks through an OPEN door; nothing here presses one");

            yield return StepClearOfHisDoorway(opening);

            bool startedBelow = opening.IsBelowDecks;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (opening.IsBelowDecks == startedBelow && Time.realtimeSinceStartup < deadline)
            {
                Vector2 toDoor = (Vector2)door.transform.position - (Vector2)_player.transform.position;
                Vector2 heading = toDoor.sqrMagnitude > 1e-6f ? toDoor.normalized : Vector2.up;

                if (opening.IsBelowDecks) opening.WalkTheCabin(heading, Time.deltaTime);
                else                      opening.WalkTheDeck(heading, Time.deltaTime);
                yield return null;
                Watch();
            }

            yield return null;
            Watch();
        }

        /// <summary>One approach is one crossing, and the latch starts disarmed with her in his doorway,
        /// so an approach begins with a step clear of it (IntroCabinPassagePlayTests' reasoning).</summary>
        private IEnumerator StepClearOfHisDoorway(ArrivalOpening opening)
        {
            BoatCabinDoor door = opening.CabinDoor;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!door.PassageIsArmed && Time.realtimeSinceStartup < deadline)
            {
                Vector2 away = (Vector2)_player.transform.position - (Vector2)door.transform.position;
                Vector2 heading = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.down;

                if (opening.IsBelowDecks) opening.WalkTheCabin(heading, Time.deltaTime);
                else                      opening.WalkTheDeck(heading, Time.deltaTime);
                yield return null;
                Watch();
            }

            Assert.IsTrue(door.PassageIsArmed,
                          "she could not get a doorway's width clear of his sill. " + Where());
        }

        /// <summary>Come up the way the player does since the 2026-08-28 ruling: a WALK through his open
        /// door, no press.</summary>
        private IEnumerator ComeUpOnDeck(ArrivalOpening opening)
        {
            yield return SheWalksThroughHisOpenDoor(opening);
            Assert.IsFalse(opening.IsBelowDecks, "she never came up through his open door. " + Where());
            yield return null;
            Watch();
        }
    }
}
