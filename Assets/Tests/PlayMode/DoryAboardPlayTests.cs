using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>THE DORY, ABOARD — the owner's ruling of 2026-09-09, played.</b>
    ///
    /// <para><i>"accept the deck feel. she can walk while standing in the dory in its narrow deck, sits
    /// at the helm with e and rows only from that position."</i></para>
    ///
    /// <para>Three things, and each is measured on the SHIPPED dory rather than on a rectangle invented
    /// here: (a) she walks her sole and never leaves the floor her rig drew; (b) E at that seat seats her
    /// and hands the hull over, and a second E stands her back up on it; (c) the oars answer from the
    /// seat and from nowhere else.</para>
    ///
    /// <para><b>⚠ Her floor is a POLYGON, and this fixture probes the polygon.</b> #806 was a world-axis
    /// SQUARE 6.95× her deck, and the reason it survived so long is that every fixture asked "is she
    /// within some distance of the middle?" — a question a square and a hull answer the same way. So the
    /// containment test here is <see cref="DeckAreaMath.Contains"/> against her own 22-point outline, and
    /// the abeam case asserts BY NAME that her coarse walkable BOX would have let her stand where her
    /// planking does not.</para>
    ///
    /// <para><b>⚠ She is drawn at 40°, so this fixture draws her at 40°.</b> A hull with no presenter
    /// reads the PLAN VIEW, where the projection degenerates to a pure rotation, height is invisible, and
    /// a seat 0.31 m up a thwart lands exactly on the sole beneath it — a sea nobody plays. A bare
    /// <see cref="DirectionalBoatSprite"/> with no sheets is the cheapest honest presenter: no facing grid
    /// to quantise her heading, and the artwork's own bake elevation on the read every station goes
    /// through. The harness asserts both, because if either slipped every case below would still pass
    /// while measuring something else.</para>
    ///
    /// <para>⚠ This fixture builds its own world and loads no scene, so it never touches the player's
    /// savegame.</para>
    /// </summary>
    public class DoryAboardPlayTests
    {
        /// <summary>Her sidecar's bake — the elevation the iso kit is drawn at.</summary>
        private const float BakeElevationDeg = 40f;

        /// <summary>⚠ Bow SOUTH-WEST, and deliberately on no axis at all. A deck read in world axes is
        /// right at heading 0 and wrong everywhere else (#806, #789); on this heading her keel runs
        /// diagonally across the screen and her beam runs the other way, so nothing below can be passing
        /// because the boat happens to point up.</summary>
        private const float HeadingDegrees = 215f;

        /// <summary>How far off her own outline a sample may be and still count as standing on her.
        /// <see cref="DeckAreaMath.Contains"/> documents that a point exactly ON an edge may read either
        /// way, and the clamp's whole job is to put her exactly on one — so an edge read is settled by
        /// distance instead. A millimetre is not a way off a 0.45 m boat.</summary>
        private const float OnTheEdge = 1e-3f;

        /// <summary>The frame step this fixture PINS, so one frame is worth the same slice of her clock
        /// on every machine. <b>⚠ Unpinned it is not.</b> With nothing to draw, headless CI turned over a
        /// frame every <b>0.36 ms</b> on 2026-09-09 — 46× less walking per frame than a played one — and
        /// a 240-frame walk down a 3.6 m deck covered 0.22 m. A frame is not a unit of time.</summary>
        private const float FrameSeconds = 1f / 60f;

        /// <summary>The net under every walk below: far more frames than any budget here asks for under
        /// the pin (a second of her clock is 60 of them), and far too few to reach on a runner that is
        /// honouring it. If this ever bites, the pin stopped taking — and it says so by name rather than
        /// letting a leg quietly go short and redden the assertion after it.</summary>
        private const int FrameSafetyCap = 20000;

        private const string DoryDeckPath = "Assets/_Project/Data/Boats/Decks/DoryIso.asset";

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
        private ControlSwitcher _switcher;
        private DeckWalkController _walk;
        private BoatController _boat;
        private DevBoatInput _helmInput;
        private BoatHullDef _hull;
        private BoatDeckDef _deck;
        private DeckArea _floor;
        private GameObject _playerGo;
        private GameConfig _config;

        /// <summary>How many times a case has actually asked her outline where she is. A "she never left
        /// her deck" case that sampled nothing passed by saying nothing.</summary>
        private int _samples;

        /// <summary>What the clock was before this fixture pinned it. Both are STATICS shared with every
        /// other test in the run, so both go back exactly as found.</summary>
        private float _timeScaleBefore;
        private float _captureBefore;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            StandableSurfaces.Clear();
            InteractionGate.Reset();
            _samples = 0;

            // ⚠ PIN THE FRAME STEP, because the walk steps speed × Time.deltaTime and a frame buys
            // HARDWARE, not time. Capture time makes every frame advance her clock by exactly
            // FrameSeconds on any machine — a played one or a headless runner doing 2,800 fps.
            // timeScale is pinned with it: it is a static, several plate fixtures in this suite freeze
            // it at zero, and one that leaked would stop him dead on a deck he is supposed to walk.
            _timeScaleBefore = Time.timeScale;
            _captureBefore = Time.captureDeltaTime;
            Time.timeScale = 1f;
            Time.captureDeltaTime = FrameSeconds;

            GameServices.Environment = new FixedTide { Level = StPetersBuilder.TideMean };

            var terrainGo = Spawn("TidalTerrain");
            var terrain = terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);
            GameServices.TidalTerrain = terrain;

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(_config);
            GameServices.Config = _config;

            _playerGo = Spawn("Player");
            _playerGo.AddComponent<SpriteRenderer>();
            var playerWalk = _playerGo.AddComponent<PlayerWalkController>();
            _walk = _playerGo.AddComponent<DeckWalkController>();
            _playerGo.AddComponent<DeckRiderVisual>();
            GameServices.PlayerTransform = _playerGo.transform;

            var boatGo = Spawn("Boat");
            boatGo.transform.position = StPetersBuilder.DoryMooredPos;
            // Compass degrees run CW from north and transform.up is her bow, so z is the negated heading.
            boatGo.transform.rotation = Quaternion.Euler(0f, 0f, -HeadingDegrees);
            _boat = boatGo.AddComponent<BoatController>();
            _helmInput = boatGo.AddComponent<DevBoatInput>();

            // ⚠ RESOLVED, never added. BoatController carries [RequireComponent(typeof(BoatMooring))],
            // so adding the controller already put her rope on her; an AddComponent here would make a
            // SECOND one and the switcher would hand the painter to an instance nobody is watching.
            BoatMooring[] ropes = boatGo.GetComponents<BoatMooring>();
            Assert.AreEqual(1, ropes.Length,
                "harness: she must carry exactly ONE BoatMooring — the one her controller requires");

            _hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _hull.Id = "boat.dory";
            _hull.LengthMeters = StPetersBuilder.DoryLengthMetres;
            _hull.DraughtMeters = 0.3f;
            _hull.CameraWorldHeightMeters = 14f;
            _hull.Propulsion = PropulsionType.Oars;
            _spawned.Add(_hull);
            _boat.SetHull(_hull);
            _boat.enabled = false; _helmInput.enabled = false;

            // ⚠ Her ARTWORK's elevation, published through the seam every station read goes through. No
            // sheets, so there is no facing grid to snap 215° back to 180° and turn this whole suite into
            // a fixture about a boat pointing south.
            var picture = boatGo.AddComponent<DirectionalBoatSprite>();
            picture.enabled = false;
            picture.Configure(null, null, 0f, null,
                              DirectionalBoatSprite.RotationMode.SnapDirectional,
                              facingsAreCounterClockwise: false,
                              bakeElevationDegrees: BakeElevationDeg);
            Assert.AreEqual(BakeElevationDeg,
                            DeckWalkController.BakeElevationDegreesOf(boatGo.transform), 1e-3f,
                "harness: she must be drawn at her own bake. A plan view makes the projection a pure " +
                "rotation, hides the height of her thwart, and quietly turns every case below into a " +
                "measurement of a sea nobody plays");
            Assert.AreEqual(HeadingDegrees,
                            DeckWalkController.DrawnHeadingDegreesOf(boatGo.transform), 1e-2f,
                "harness: her picture must be drawn where her bow points — if this ever snaps to a " +
                "facing cell then the deck below is secretly on another heading");

            // Her authored deck: the narrow lane the owner ruled she walks, imported from her rig.
            _deck = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatDeckDef>(DoryDeckPath);
            Assert.IsNotNull(_deck, $"the authored deck {DoryDeckPath} must exist");
            Assert.IsTrue(_deck.HasWalkableDeck(),
                "premise: she has a measured floor. Without one the walk falls back to a greybox " +
                "rectangle and the polygon this fixture probes is not the shape she is clamped to");
            Assert.IsTrue(_deck.HasHelmStation,
                "premise: since 2026-09-09 the dory publishes her own helm station (her after thwart). " +
                "Without it the switcher takes the shared tuned offset and none of this is her seat");
            Assert.AreEqual(1, _deck.Areas.Length,
                "premise: ONE walkable area — her sole. A second would make 'inside her floor' below " +
                "ambiguous about which floor");
            _floor = _deck.Areas[0];
            Assert.AreEqual("floor", _floor.Id, "premise: her sole is the area named floor");
            boatGo.AddComponent<BoatDeckAreas>().Configure(_deck);

            // ⚠ The dock zone is the ARRIVAL's berth on the far SOUTH face, exactly as the region authors
            // it, and there is no StandablePlatform anywhere: she lies in open water here. So a press
            // cannot fall through the deck ladder to a step ashore, and every E below is about her helm.
            var dockZone = Spawn("DockZone");
            dockZone.transform.position = StPetersBuilder.DockZonePos;
            var disembark = Spawn("Disembark");
            disembark.transform.position = StPetersBuilder.DisembarkPos;

            _switcher = Spawn("Switcher").AddComponent<ControlSwitcher>();
            _switcher.Configure(playerWalk, _boat, _helmInput, dockZone.transform,
                                StPetersBuilder.DockZoneRadius, disembark.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = _captureBefore;   // exactly as found, whatever it was
            Time.timeScale = _timeScaleBefore;
            StandableSurfaces.Clear();
            InteractionGate.Reset();
            GameServices.PlayerTransform = null;
            GameServices.Config = null;
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- the cases ---------------------------------------------------------------------------

        /// <summary>
        /// ⭐ <b>(a) She walks her deck, and her deck is the lane her rig drew.</b> Driven through the
        /// input seam (ADR 0043) rather than by calling the walk's own API, so what is under test is the
        /// thing a key press actually reaches: a held direction, read once per frame in <c>Update</c>,
        /// stepped and clamped in hull metres.
        ///
        /// <para><b>Two legs, and the second is the one with teeth.</b> Forward along her keel she has
        /// 3.6 m to cover, so a fixture that only walked that way would pass on any long box. Then hard
        /// to STARBOARD, up where she narrows: her coarse walkable box is 0.45 m wide from stem to
        /// transom, her planking at that station is not, and the case asserts by name that the box would
        /// have let her stand where the outline stops her. That gap is #806, one hull further in.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator SheWalksHerNarrowDeck_AndItIsHerOutlineThatStopsHer()
        {
            yield return BoardHer();

            var held = new HeldDeckIntents();
            _walk.ConfigureDeckInput(held);

            _walk.SnapToDeckLocal(Vector2.zero);        // amidships on the centreline, her widest water
            yield return null;
            AssertHeIsOnHerFloor("her start, amidships");

            Vector2 start = _walk.DeckLocalPosition;
            yield return WalkUntil(held, new Vector2(0f, 1f),
                                   () => _walk.DeckLocalPosition.y >= 1.2f, 4f, "toward her bow");

            Assert.Greater(held.Reads, 0,
                "the deck walk never asked its source — the input seam is not wired into Update, and " +
                "nothing below is being driven the way a key press drives it");
            Assert.Greater(_walk.DeckLocalPosition.y - start.y, 1.0f,
                $"he never walked forward (he is at {_walk.DeckLocalPosition}, from {start}) — a " +
                "'stayed inside her deck' case about somebody who never moved is vacuous");

            // Now shove him at the rail, up where the planking has closed in.
            Vector2 beforeAbeam = _walk.DeckLocalPosition;
            yield return WalkFor(held, new Vector2(1f, 0f), 1f, "hard to starboard");

            Vector2 at = _walk.DeckLocalPosition;
            Assert.Greater(at.x - beforeAbeam.x, 0.02f,
                $"he never tried to go abeam (he is at {at}, from {beforeAbeam}) — the clamp below is " +
                "not being asked anything");

            Assert.IsTrue(_walk.TryDeckBox(_boat.transform, out Vector2 centre, out Vector2 half),
                          "harness: her coarse walkable box");
            var boxEdge = new Vector2(centre.x + half.x, at.y);
            Assert.IsFalse(OnHerFloor(boxEdge, out float _),
                $"harness: her box (centre {centre}, half {half}) reaches {boxEdge} at this station and " +
                "her planking must not — if the box and the outline agree here the case below cannot " +
                "tell them apart and proves nothing");
            Assert.Less(at.x, boxEdge.x - 0.02f,
                $"he was stopped by her BOX, not by her planking: he is standing at {at}, out at the " +
                $"box edge {boxEdge}, which is over the side of a hull that has narrowed to a lane by " +
                "here. This is #806 exactly — a world-axis rectangle where a measured outline belongs");

            Assert.Greater(_samples, 60, "harness: her outline was barely asked");
        }

        /// <summary>
        /// ⭐ <b>(b) E at the thwart SITS him, and hands the hull over.</b> The owner's words —
        /// <i>"sits at the helm with e"</i>. One press seats him at her published station and makes him
        /// the pilot of this hull in the arbiter every other system asks (<c>HelmSlot</c>, #642); a
        /// second press stands him back up, on the seat, with the hull free again.
        ///
        /// <para><b>⚠ "At the seat" is a WORLD place, not a deck-frame one.</b> Her station carries a
        /// height — the thwart is 0.31 m up — and the deck walk's own bookkeeping is a point on the SOLE.
        /// Standing back up from a seat lands him on the sole beneath it, which is the same drawn spot
        /// and a different pair of hull metres; asserting the deck coordinate would be asserting he never
        /// sat down. So the bar is where he is DRAWN, against the switcher's own
        /// <see cref="ControlSwitcher.HelmWorldPosition"/>, and it is held for a frame afterwards because
        /// the deck walk re-projects him every tick and could quietly drag him off it.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator EAtHerThwart_SeatsHim_AndASecondEStandsHimBackUpOnIt()
        {
            yield return BoardHer();
            yield return WalkToHerThwart();

            Assert.IsTrue(_switcher.WithinHelmReach(),
                $"standing on the sole under her after thwart, the fisher is at her helm (he is at " +
                $"{_playerGo.transform.position}, her helm is at {_switcher.HelmWorldPosition})");
            Assert.IsNull(GameServices.Helm.PilotedHull,
                "premise: nobody is piloting her yet — she is a boat being stood in");

            Assert.IsTrue(_switcher.BeginInteract(), "E at the helm must do something");
            yield return SettleAnyMove();

            Assert.AreEqual(ControlMode.Aboard, _switcher.Mode, "one press and he is seated at the helm");
            Assert.AreSame(_boat, GameServices.Helm.PilotedHull,
                "…and the arbiter names THIS hull as the one he is piloting — the same question the " +
                "oars, the cutaway and the instruments ask, so they cannot disagree about it");
            AssertHeIsAtTheHelm("seated");

            Assert.IsTrue(_switcher.BeginInteract(), "a second E must stand him up again");
            yield return SettleAnyMove();

            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode, "…back on her deck, not ashore");
            Assert.IsNull(GameServices.Helm.PilotedHull,
                "…and the helm is free again: standing up is giving the hull back");
            AssertHeIsAtTheHelm("standing again");

            yield return null;      // one full tick of the deck walk, which re-projects him every frame
            AssertHeIsAtTheHelm("a frame later, with the walk running");
            AssertHeIsOnHerFloor("standing at her thwart");
        }

        /// <summary>
        /// ⭐ <b>(c) She rows only from the seat.</b> The owner's ruling — <i>"rows only from that
        /// position"</i> — as the only thing that can be observed about it: the oar registers on the
        /// hull, which are what <c>ApplyOarDrive</c> turns into thrust and what the row animator turns
        /// into a swinging loom. Standing on her sole a full-ahead stroke puts NOTHING on them; seated,
        /// the same stroke puts it all on.
        ///
        /// <para><b>⚠ The stroke is driven through <see cref="DevBoatInput.DriveOars"/>, not through the
        /// component's Update, and that is deliberate.</b> Two gates stand in series here: the seat rule
        /// itself, and <c>ControlSwitcher</c>'s <c>_boatInput.enabled</c>, which is off until the helm is
        /// taken. Pressing W and watching the boat would vary both at once and could not tell them apart
        /// — and headless CI has no keyboard to press anyway. So the fixture varies ONE gate, the helm
        /// slot, and observes the ONE thing that gate guards.</para>
        ///
        /// <para><b>⚠ And every drive is asserted in the SAME frame it was made</b>, with no yield in
        /// between: once the helm is taken the input component is enabled, and its own <c>Update</c>
        /// re-drives the oars from a keyboard that is not there on the very next tick.</para>
        ///
        /// <para>The last movement is the one that matters most for feel: with a stroke standing on the
        /// registers, the seat is taken away and the SAME drive is made again. The oars go to zero — the
        /// gate WRITES a zero stroke rather than declining to write, so a rower who stands up cannot
        /// leave the dory pulling herself along with nobody on the thwart.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator HerOarsAnswerFromTheSeatAndFromNowhereElse()
        {
            yield return BoardHer();
            yield return WalkToHerThwart();

            // STANDING at the seat — as close to rowing as a stander ever gets.
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode, "premise: he is standing on her sole");
            Assert.IsFalse(_helmInput.RowingStationManned,
                "premise: standing beside the thwart is not sitting on it");

            _helmInput.DriveOars(1f, 1f, false);
            Assert.AreEqual(0f, _boat.LeftOar, 1e-6f, "a stander pulled her port oar");
            Assert.AreEqual(0f, _boat.RightOar, 1e-6f, "a stander pulled her starboard oar");
            Assert.AreEqual(0f, BoatController.OarThrust(_boat.LeftOar, _boat.RightOar, _hull.OarPower),
                            1e-6f, "…and the hull was driven by it");

            // SEATED — the same stroke, the one gate moved.
            Assert.IsTrue(_switcher.BeginInteract(), "E at the helm must seat him");
            yield return SettleAnyMove();
            Assert.AreEqual(ControlMode.Aboard, _switcher.Mode, "premise: he is on the thwart now");
            Assert.IsTrue(_helmInput.RowingStationManned, "premise: the arbiter agrees that he is");

            _helmInput.DriveOars(1f, 1f, false);
            float seatedThrust = BoatController.OarThrust(_boat.LeftOar, _boat.RightOar, _hull.OarPower);
            Assert.AreEqual(1f, _boat.LeftOar, 1e-6f, "seated, her port oar must take the stroke");
            Assert.AreEqual(1f, _boat.RightOar, 1e-6f, "seated, her starboard oar must take the stroke");
            Assert.Greater(seatedThrust, 0f,
                "seated, a full-ahead stroke must actually drive her — a gate that refuses both ways is " +
                "a boat that never rows, and this half is what says the fixture can tell them apart");

            // STANDING UP MID-STROKE — the registers must be WIPED, not merely left alone.
            GameServices.Helm.SetPilotedHull(null);
            Assert.IsFalse(_helmInput.RowingStationManned, "harness: the seat was taken away");

            _helmInput.DriveOars(1f, 1f, false);
            Assert.AreEqual(0f, _boat.LeftOar, 1e-6f,
                "she kept rowing after the rower stood up: the gate declined to write instead of " +
                "writing a zero, so the last stroke is still standing on the hull");
            Assert.AreEqual(0f, _boat.RightOar, 1e-6f, "…the same on her starboard oar");
            Assert.AreEqual(0f, BoatController.OarThrust(_boat.LeftOar, _boat.RightOar, _hull.OarPower),
                            1e-6f, "…and she is still being driven by it");
        }

        // ---- the rig -----------------------------------------------------------------------------

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        /// <summary>Put him aboard the way the shipping verb does, from alongside her.</summary>
        private IEnumerator BoardHer()
        {
            _playerGo.transform.position = _boat.transform.position;
            yield return null;
            Assert.IsTrue(_switcher.BeginInteract(), "he must be able to board from alongside");
            yield return SettleAnyMove();
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode, "premise: he is on deck");
            Assert.IsFalse(_switcher.CanStepAshore(),
                "premise: she lies in open water here — no planks and no berth in reach, so E cannot " +
                "fall through to a step ashore and every press below is about her helm");
        }

        /// <summary>
        /// Stand him on the sole beneath HER AFTER THWART — the seat her rig publishes, read off her own
        /// deck asset rather than written down here.
        ///
        /// <para><b>⚠ And then check he is standing where the fixture thinks he is.</b> The walk clamps
        /// him to her floor every tick, so a seat that fell outside it would be silently dragged inboard
        /// and every assertion after this would be about a spot nobody asked for. Her floor is 0.45 m
        /// wide amidships and tapers to 0.14 m at the ends, so this is not a formality.</para>
        /// </summary>
        private IEnumerator WalkToHerThwart()
        {
            Vector3 station = _deck.HelmStationLocalMeters;
            var seat = new Vector2(station.x, station.y);

            _walk.SnapToDeckLocal(seat);
            yield return null;

            Assert.AreEqual(seat.x, _walk.DeckLocalPosition.x, 0.05f,
                $"harness: the deck clamp moved him abeam of her seat (he is at {_walk.DeckLocalPosition})");
            Assert.AreEqual(seat.y, _walk.DeckLocalPosition.y, 0.05f,
                $"harness: the deck clamp moved him along her keel (he is at {_walk.DeckLocalPosition})");
            AssertHeIsOnHerFloor("the sole beneath her thwart");
        }

        private IEnumerator SettleAnyMove()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (_switcher.IsBoardingMove && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
        }

        /// <summary>
        /// Hold a DECK direction (x abeam to starboard, y along her keel toward the bow) until
        /// <paramref name="arrived"/> or <paramref name="budgetSeconds"/> of HER CLOCK have gone by,
        /// checking her outline every frame on the way.
        ///
        /// <para><b>⚠ The budget is SECONDS, and it used to be frames.</b> That is what reddened this
        /// case on its first CI run: the walk steps <c>speed × Time.deltaTime</c>, a headless runner with
        /// nothing to draw gave <c>Time.deltaTime</c> as <b>0.36 ms</b>, and 240 frames down a 3.6 m deck
        /// left him 0.22 m from where he started. Nothing was wrong with the deck, the clamp or the seam
        /// — the fixture had budgeted in a unit that measures the machine. So the budget is now the same
        /// clock the walk reads, accumulated frame by frame; SetUp pins that step so the frame count
        /// stays sane, and <see cref="FrameSafetyCap"/> is the net for the day the pin stops taking.</para>
        ///
        /// <para><b>⚠ The intent is expressed in her deck frame and handed over in WORLD axes</b>, which
        /// is the frame the seam speaks: <c>StepOnDeckPolygon</c> un-projects what it is given through
        /// her drawn heading and her bake. Handing it (0, 1) directly would walk him up the SCREEN, which
        /// on this heading crosses her keel at 35° — and the case would then be about a boat pointing
        /// north, which is the whole family of defects #806 and #789 came out of.</para>
        ///
        /// <para>⚠ The set lands on the NEXT frame's read (one read per frame, in Update), so the first
        /// yield after a set buys nothing and is spent before anything is counted.</para>
        /// </summary>
        private IEnumerator WalkUntil(HeldDeckIntents held, Vector2 deckDirection,
                                      System.Func<bool> arrived, float budgetSeconds, string leg)
        {
            held.Walk(WorldDirectionFor(deckDirection));
            yield return null;

            float walked = 0f;
            int frames = 0;
            bool there = arrived();
            while (!there && walked < budgetSeconds && frames < FrameSafetyCap)
            {
                yield return null;
                walked += Time.deltaTime;
                frames++;
                AssertHeIsOnHerFloor(leg);
                there = arrived();
            }

            held.Walk(Vector2.zero);
            yield return null;
            AssertHeIsOnHerFloor(leg + ", at rest");

            Assert.Less(frames, FrameSafetyCap,
                $"the frame step is not pinned: {frames} frames bought only {walked:F3} s of her clock " +
                $"walking {leg} (a frame is {Time.deltaTime * 1000f:F3} ms). This is the net under the " +
                "budget, not the bar — read it as 'the clock, not the deck'");
            Assert.IsTrue(there,
                $"he never got there walking {leg}: {walked:F2} s at the walk's own speed, over " +
                $"{frames} frames, left him at {_walk.DeckLocalPosition}");
        }

        /// <summary>Hold a DECK direction for a fixed slice of HER CLOCK — the shove that is supposed
        /// to get nowhere. Same seam, same one-frame delay, same check every frame, and seconds rather
        /// than frames for the reason spelled out on <see cref="WalkUntil"/>: a shove budgeted in frames
        /// is a shove whose length is set by the runner, and a shove that never happened cannot be
        /// stopped by her planking or by anything else.</summary>
        private IEnumerator WalkFor(HeldDeckIntents held, Vector2 deckDirection, float seconds, string leg)
        {
            held.Walk(WorldDirectionFor(deckDirection));
            yield return null;

            float pushed = 0f;
            int frames = 0;
            while (pushed < seconds && frames < FrameSafetyCap)
            {
                yield return null;
                pushed += Time.deltaTime;
                frames++;
                AssertHeIsOnHerFloor(leg);
            }

            held.Walk(Vector2.zero);
            yield return null;
            AssertHeIsOnHerFloor(leg + ", at rest");

            Assert.GreaterOrEqual(pushed, seconds,
                $"the shove {leg} was cut short at {frames} frames ({pushed:F3} s of her clock): the " +
                "frame step is not pinned, and nothing below can claim to have stopped him");
        }

        /// <summary>A deck direction as the world-axis direction the input seam speaks — through HER
        /// drawn heading and HER bake, never a constant.</summary>
        private Vector2 WorldDirectionFor(Vector2 deckDirection)
            => DeckAreaMath.DeckToWorld(deckDirection, 0f,
                                        DeckWalkController.DrawnHeadingDegreesOf(_boat.transform),
                                        DeckWalkController.BakeElevationDegreesOf(_boat.transform))
                           .normalized;

        /// <summary>
        /// Is this deck-frame point on her floor? <b>The polygon is asked — never a radius, never the
        /// box.</b> A point exactly on an edge is documented to read either way, and the clamp's job is
        /// to put him exactly on one, so an edge read is settled by distance to the outline instead.
        /// </summary>
        private bool OnHerFloor(Vector2 deckPoint, out float outsideBy)
        {
            if (DeckAreaMath.Contains(_floor.Outline, _floor.Bounds, deckPoint))
            {
                outsideBy = 0f;
                return true;
            }

            DeckAreaMath.ClosestPointOnOutline(_floor.Outline, deckPoint, out float sqr);
            outsideBy = Mathf.Sqrt(sqr);
            return outsideBy <= OnTheEdge;
        }

        private void AssertHeIsOnHerFloor(string where)
        {
            _samples++;
            Vector2 p = _walk.DeckLocalPosition;
            if (OnHerFloor(p, out float outsideBy)) return;
            Assert.Fail($"walking {where} took him off her deck: hull-local {p} is {outsideBy:F3} m " +
                        $"outside her floor outline (her bounds are {_floor.Bounds}). He is over the " +
                        "side of a 0.45 m boat");
        }

        /// <summary>He is standing (or sitting) AT her helm — the drawn place, against the switcher's
        /// own answer for where that is.</summary>
        private void AssertHeIsAtTheHelm(string when)
        {
            float off = Vector2.Distance(_playerGo.transform.position, _switcher.HelmWorldPosition);
            Assert.Less(off, 0.05f,
                $"{when}, he is {off:F3} m from her helm ({_playerGo.transform.position} against " +
                $"{_switcher.HelmWorldPosition}) — E is supposed to put him ON the seat, not near it");
        }
    }
}
