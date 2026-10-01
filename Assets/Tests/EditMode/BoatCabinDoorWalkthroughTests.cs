using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>An OPEN door is walked through, not pressed through</b> — the owner's 2026-08-28 ruling, held
    /// to sentence by sentence: <i>"a door is closed until its opened, a player can walk through freely
    /// when opened, a player can close/open when inside."</i>
    ///
    /// <para><b>Why this fixture is EditMode.</b> Everything the ruling says is a rule about STATE and a
    /// rule about a POINT, and neither needs a frame: the cue is driven through
    /// <see cref="BoatCabinDoor.Tick"/> on a clock this fixture owns, and the passage is
    /// <see cref="BoatCabinDoor.TryWalkThrough"/> handed a hull-local metre, the key she holds and her
    /// tick. The walkers that supply those every tick are proved in PlayMode, where they exist; asserting
    /// them here would assert something the shipped game never does.</para>
    ///
    /// <para><b>⚠ The band is DATA, so the numbers below are derived and not typed.</b> The fixture's
    /// door states a 0.72 m opening — the cape islander's and the lobster boat's own measured clear
    /// width — which makes the band 0.36 m and the pull's reach 0.72 m. Both are read off
    /// <see cref="BoatCabinThreshold"/> rather than mirrored, so a re-measured doorway does not need a
    /// second edit here. The doorway crosses her on its wall line, the sole's aft edge, 0.1 m inside
    /// the threshold, with her key held through it (owner ruling D1, 2026-09-30); the first tests hold
    /// the fixture's points to that line.</para>
    /// </summary>
    public class BoatCabinDoorWalkthroughTests
    {
        private const float DeckRoll = 5f;
        private const float HeavePixels = 1.6f;
        private const float PitchLift = 0.02f;

        /// <summary>The fixture door's own opening. Every distance in this file is a multiple of it.</summary>
        private const float ClearWidth = 0.72f;

        /// <summary>Where her doorway is, in the hull's own metres — the def's threshold with its sill
        /// height dropped, which is what <see cref="BoatCabinThreshold.PointOf"/> does.</summary>
        private static readonly Vector2 Doorway = new(0f, -2.1f);

        /// <summary>Somewhere unambiguously not in the doorway — the middle of the sole, 2.1 m off
        /// against a 0.36 m band and a 0.72 m pull.</summary>
        private static readonly Vector2 WellClear = new(0f, 0f);

        /// <summary>Where the doorway's wall line crosses its axis: the house sole's aft edge, 0.1 m inside
        /// the threshold (<see cref="BoatCabinThreshold.TryWallLine"/>; held to it below).</summary>
        private static readonly Vector2 OnTheLine = new(0f, -2f);

        /// <summary>The key held through the doorway, in the hull's own frame: into the room from the deck…</summary>
        private static readonly Vector2 In = Vector2.up;

        /// <summary>…and out of it from the sole.</summary>
        private static readonly Vector2 Out = Vector2.down;

        /// <summary>One of her walker's ticks.</summary>
        private const float Tick = 1f / 60f;

        /// <summary>The owner's S6 (2026-09-30): no two crossings within this long.</summary>
        private const float NoTwoCrossingsWithinSeconds = 0.15f;

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            GameServices.Config = null;
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
        }

        // =====================================================================================
        //  1 · THE BAND — pure geometry, and it is the doorway's own measurement
        // =====================================================================================

        [Test]
        public void TheBandIsHalfTheMeasuredOpening_AndThePullReachesAWholeOne()
        {
            Rig rig = NewRig();
            BoatInteriorDoor door = rig.Door.Door;

            Assert.AreEqual(ClearWidth * 0.5f, BoatCabinThreshold.BandRadiusMetres(door), 1e-6f,
                            "standing in the doorway is standing within half its own width of the sill");
            Assert.AreEqual(ClearWidth * door.PullReach, BoatCabinThreshold.PullReachMetres(door), 1e-6f,
                            "D1 (a)'s pull reaches the door's own count of its clear widths");

            // The owner's D1 (a), 2026-09-30: "within one clear width … within 30° of the doorway's axis",
            // as fields beside the measured width, and those are their values on a door that states none.
            Assert.AreEqual(1f, BoatInteriorDoor.DefaultPullReachClearWidths, "one clear width");
            Assert.AreEqual(30f, BoatInteriorDoor.DefaultPullConeDegrees, "within 30° of the axis");
            door.PullReachClearWidths = 0f;
            door.PullConeDegrees = 0f;
            Assert.AreEqual(BoatInteriorDoor.DefaultPullReachClearWidths, door.PullReach);
            Assert.AreEqual(BoatInteriorDoor.DefaultPullConeDegrees, door.PullCone);
        }

        [Test]
        public void TheBandIsAboutTheThresholdPoint_AndTheSillHeightIsNotInIt()
        {
            Rig rig = NewRig();
            BoatInteriorDoor door = rig.Door.Door;

            Assert.AreEqual(Doorway, BoatCabinThreshold.PointOf(door),
                            "a band is a place on a FLOOR — the height says which floor, and only that");
            Assert.IsTrue(BoatCabinThreshold.IsInBand(door, Doorway));
            Assert.IsTrue(BoatCabinThreshold.IsInBand(door, Doorway + new Vector2(0.35f, 0f)),
                          "just inside the 0.36 m band");
            Assert.IsFalse(BoatCabinThreshold.IsInBand(door, Doorway + new Vector2(0.37f, 0f)),
                           "…and just outside it");
        }

        [Test]
        public void TheWallLineIsTheSolesOwnEdge_AndItsNormalIsTheWayOut()
        {
            // Where the passage crosses her (owner ruling D1, 2026-09-30): the one place both walkers can
            // stand, so a crossing there moves her no farther than their own clamps do. The fixture's
            // points are this line's; were it elsewhere, every test below would be asserting elsewhere.
            Rig rig = NewRig();
            BoatInteriorDoor door = rig.Door.Door;

            Assert.IsTrue(BoatCabinThreshold.TryWallLine(rig.Def, door, out int level, out Vector2 onLine,
                                                         out Vector2 outward));
            Assert.AreEqual(0, level, "the room it opens is the house sole");
            Assert.AreEqual(0f, Vector2.Distance(OnTheLine, onLine), 1e-6f,
                            "the sole's aft edge, 0.1 m inside the threshold");
            Assert.AreEqual(0f, Vector2.Distance(Out, outward), 1e-6f, "and out of the room is aft");
            Assert.IsTrue(BoatCabinThreshold.IsInBand(door, onLine),
                          "standing on the line is standing in the doorway");
            Assert.AreEqual(level, rig.Door.RoomLevelIndex, "the door says the same of itself");
            Assert.IsTrue(rig.Door.ThresholdIsWalkable);
        }

        [Test]
        public void ADoorNobodyMeasuredHasNoBandAtAll_AndSaysSo()
        {
            // FALSE here is data, not a small band: a sidecar that never measured the leaf has not
            // described an opening, and a zero-radius band would make the threshold silently unwalkable
            // rather than declaring it.
            Rig rig = NewRig();
            rig.Def.Door.ClearWidthMeters = 0f;
            BoatInteriorDoor door = rig.Door.Door;

            Assert.IsFalse(BoatCabinThreshold.HasBand(door));
            Assert.AreEqual(0f, BoatCabinThreshold.BandRadiusMetres(door));
            Assert.IsFalse(BoatCabinThreshold.IsInBand(door, Doorway),
                           "not even standing exactly on the sill");
            Assert.AreEqual(0f, BoatCabinThreshold.PullReachMetres(door),
                            "and nothing may pull her toward a threshold that cannot be crossed");
            Assert.IsFalse(BoatCabinThreshold.IsInThePull(door, Out, true, Doorway, In));
        }

        [Test]
        public void TheThresholdMathIsNullSafe_BecauseAHullMayHaveNoDoor()
        {
            Assert.AreEqual(Vector2.zero, BoatCabinThreshold.PointOf(null));
            Assert.AreEqual(0f, BoatCabinThreshold.BandRadiusMetres(null));
            Assert.AreEqual(0f, BoatCabinThreshold.PullReachMetres(null));
            Assert.IsFalse(BoatCabinThreshold.HasBand(null));
            Assert.IsFalse(BoatCabinThreshold.IsInBand(null, Vector2.zero));
            Assert.IsFalse(BoatCabinThreshold.TryWallLine(null, null, out _, out _, out _));
            Assert.IsFalse(BoatCabinThreshold.IsHeldThrough(null, Out, true, In));
            Assert.IsFalse(BoatCabinThreshold.IsInThePull(null, Out, true, Vector2.zero, In));
            Assert.IsFalse(BoatCabinThreshold.IsLeaningThrough(null, Out, In));
            Assert.IsFalse(BoatCabinThreshold.IsAgainstFurnitureJustInside(null, null, Vector2.zero));
            Assert.AreEqual(In, BoatCabinThreshold.Steer(null, OnTheLine, Out, true, Vector2.zero, In),
                            "no door bends no key");
        }

        // =====================================================================================
        //  2 · THE STATE — "a door is closed until its opened"
        // =====================================================================================

        [Test]
        public void ADoorIsClosedUntilItIsOpened()
        {
            Rig rig = NewRig();

            Assert.IsFalse(rig.Door.IsOpen, "the ruling's first sentence, on a door nobody has touched");
            Assert.IsTrue(rig.Door.WouldOpen);
            Assert.AreEqual("Open the door", rig.Door.VerbLabel);
        }

        [Test]
        public void ThePressMovesTheLeaf_AndTheRoomDoesNotAppearUnderTheHand()
        {
            Rig rig = NewRig();

            Assert.IsTrue(rig.Door.TryUse());
            Assert.IsTrue(rig.Door.IsCueing);
            Assert.IsFalse(rig.Door.IsOpen, "the leaf is still moving; it is not yet an opening");
            Assert.IsFalse(rig.Interior.IsInside);

            rig.Door.Tick(rig.Door.CueSeconds + 0.01f);

            Assert.IsFalse(rig.Door.IsCueing);
            Assert.IsTrue(rig.Door.IsOpen, "the cue ends on a door that is STANDING open");
            Assert.IsFalse(rig.Interior.IsInside,
                           "…and opening a door is not walking through it — that is the whole fix");
        }

        [Test]
        public void ThePressWorksFromInside_AndReadsTheLeafRatherThanTheOccupant()
        {
            // The ruling's third sentence. WouldOpen never asks where she is standing, which is what
            // makes one press behave identically on both sides of one doorway.
            Rig rig = NewRig();
            Open(rig);
            WalkIn(rig);
            Assert.IsTrue(rig.Interior.IsInside);

            Assert.IsTrue(rig.Door.IsOpen);
            Assert.IsFalse(rig.Door.WouldOpen, "she is inside, and the door she is looking at is open");
            Assert.AreEqual("Close the door", rig.Door.VerbLabel);

            Assert.IsTrue(rig.Door.TryUse(), "the press is taken from the sole");
            rig.Door.Tick(rig.Door.CueSeconds + 0.01f);

            Assert.IsFalse(rig.Door.IsOpen, "she has closed it behind her");
            Assert.IsTrue(rig.Interior.IsInside, "…and closing a door does not evict her");
        }

        [Test]
        public void ASetLeafIsHowADoorIsFound_NotHowItIsWorked()
        {
            // The arrival's one caller: Armand is aboard in fair weather and his aft door is already
            // standing open when the game opens. No cue, because nobody worked it.
            Rig rig = NewRig();

            rig.Door.SetOpen(true);
            Assert.IsTrue(rig.Door.IsOpen);
            Assert.IsFalse(rig.Door.IsCueing, "a door that has been PLACED is not a door that is moving");

            rig.Door.TryUse();
            Assert.IsTrue(rig.Door.IsCueing);
            rig.Door.SetOpen(false);
            Assert.IsFalse(rig.Door.IsCueing, "and placing one cancels a leaf that was mid-swing");
        }

        // =====================================================================================
        //  3 · THE PASSAGE — "a player can walk through freely when opened"
        // =====================================================================================

        [Test]
        public void AnOpenDoorIsWalkedThrough_BothWays_WithNoPressAndNoCue()
        {
            Rig rig = NewRig();
            Open(rig);

            // IN. On her way to the wall line with the key held in is not through it; on it, she goes.
            Assert.IsFalse(rig.Door.TryWalkThrough(Doorway + Out * 0.1f, In, Tick), "short of it is not through it");
            Assert.IsTrue(rig.Door.TryWalkThrough(OnTheLine, In, Tick), "she walks in");
            Assert.IsTrue(rig.Interior.IsInside);
            Assert.IsFalse(rig.Door.IsCueing, "no cue was spent — a hole in a wall costs nothing");
            Assert.IsTrue(rig.Door.IsOpen, "…and the door she walked through is still open");

            // OUT, through the same doorway, on the same terms, once it has settled behind her.
            Settle(rig);
            Assert.IsTrue(rig.Door.TryWalkThrough(OnTheLine, Out, Tick), "she walks out");
            Assert.IsFalse(rig.Interior.IsInside);
        }

        [Test]
        public void AClosedDoorIsAWall_TheThresholdRefusesHerBothWays()
        {
            Rig rig = NewRig();

            // OUTSIDE, shut: on its line with the key held in does nothing at all.
            Assert.IsTrue(rig.Door.PassageIsSettled, "nothing has crossed, so the doorway is a live one");
            Assert.IsFalse(rig.Door.TryWalkThrough(OnTheLine, In, Tick), "a shut door is a wall");
            Assert.IsFalse(rig.Interior.IsInside);

            // INSIDE, shut behind her: the same wall, from the other side.
            Open(rig);
            WalkIn(rig);
            Close(rig);
            Settle(rig);

            Assert.IsFalse(rig.Door.TryWalkThrough(OnTheLine, Out, Tick), "…and it is a wall from the sole too");
            Assert.IsTrue(rig.Interior.IsInside, "she is still below, which is where she shut it");
        }

        [Test]
        public void ALeafHalfwayAcrossIsNotAnOpening()
        {
            // ⚠ Asked on the way SHUT, deliberately. On the way open IsOpen is still false and that gate
            // would answer first; mid-close the door is open AND moving, so this reaches the IsCueing
            // gate and nothing else can be what refused her.
            Rig rig = NewRig();
            Open(rig);

            Assert.IsTrue(rig.Door.TryUse(), "press it shut");
            rig.Door.Tick(rig.Door.CueSeconds * 0.5f);
            Assert.IsTrue(rig.Door.IsCueing);
            Assert.IsTrue(rig.Door.IsOpen, "the state has not flipped yet — the leaf is still swinging");

            Assert.IsFalse(rig.Door.TryWalkThrough(OnTheLine, In, Tick),
                           "a door that is still moving is not a doorway you walk through");
            Assert.IsFalse(rig.Interior.IsInside);
        }

        // =====================================================================================
        //  4 · THE SETTLE — one crossing is one, and the sill does not strobe
        // =====================================================================================

        [Test]
        public void OneApproachIsOneCrossing_SoStandingOnTheSillDoesNotFlickerTheLevel()
        {
            // The failure this guards is a frame-rate one: she lands on the line she just crossed, and a
            // doorway that took her whatever she held would put her straight back out, and back in, at
            // sixty crossings a second. It takes her for a key held THROUGH it from her side, and no other.
            Rig rig = NewRig();
            Open(rig);

            Assert.IsTrue(rig.Door.TryWalkThrough(OnTheLine, In, Tick), "the crossing she asked for");
            Assert.IsTrue(rig.Interior.IsInside);

            for (int tick = 0; tick < 60; tick++)
                Assert.IsFalse(rig.Door.TryWalkThrough(OnTheLine, In, Tick),
                               $"tick {tick}: she is still walking in, not crossing again");

            // The owner's S6: idle 5 s gives no crossing.
            for (int tick = 0; tick < 5 * 60; tick++)
                Assert.IsFalse(rig.Door.TryWalkThrough(OnTheLine, Vector2.zero, Tick),
                               $"tick {tick}: standing on the sill with no key is not walking through it");

            Assert.IsTrue(rig.Interior.IsInside, "six seconds on the sill, and she is still below");
        }

        [Test]
        public void TheDoorwayTakesHerBackOnlyOnceItHasSettled_AndThenOnTheTickItHas()
        {
            // The owner's S6 (2026-09-30): no two crossings within 0.15 s, and crossing back needs the key
            // reversed. The settle is the door's own (BoatInteriorDoor.CrossingSettle), counted on her
            // walker's ticks, and it replaces the old "get a clear width away first".
            Rig rig = NewRig();
            float settle = rig.Def.Door.CrossingSettle;
            Assert.GreaterOrEqual(settle, NoTwoCrossingsWithinSeconds, "the door's settle honours S6");
            Open(rig);
            WalkIn(rig);
            Assert.IsFalse(rig.Door.PassageIsSettled, "she has just crossed");

            float since = 0f;
            while (!rig.Door.TryWalkThrough(OnTheLine, Out, Tick))
            {
                since += Tick;
                Assert.IsTrue(rig.Interior.IsInside);
                Assert.Less(since, settle + Tick, "once it has settled, the reversed key takes her back");
            }
            since += Tick;

            Assert.GreaterOrEqual(since, settle - 1e-5f, "never before the door's own settle");
            Assert.IsFalse(rig.Interior.IsInside, "she has turned back out");
            Assert.IsFalse(rig.Door.PassageIsSettled, "and that crossing starts the settle afresh");
        }

        [Test]
        public void TheSettleIsTheDoorsOwnData_AndADoorThatStatesNoneHasTheDefault()
        {
            Rig rig = NewRig();
            rig.Def.Door.CrossingSettleSeconds = 0.4f;
            Open(rig);
            WalkIn(rig);

            float since = 0f;
            while (!rig.Door.PassageIsSettled)
            {
                rig.Door.TryWalkThrough(WellClear, Vector2.zero, Tick);
                since += Tick;
                Assert.Less(since, 1f, "the settle ends");
            }
            Assert.AreEqual(0.4f, since, Tick, "the door's own settle, to a tick");

            rig.Def.Door.CrossingSettleSeconds = 0f;
            Assert.AreEqual(BoatInteriorDoor.DefaultCrossingSettleSeconds, rig.Def.Door.CrossingSettle,
                            "a door that states none settles in the default");
        }

        [Test]
        public void TheArrivalOpensHerInADoorway_AndSheStaysUntilSheHoldsAKeyThroughIt()
        {
            // The game's first frame stands the passenger in Armand's doorway, on the sole, his door
            // standing open. No key, no crossing: she stays where she is. The key held out walks her out on
            // the tick she holds it (the owner's S5): nothing has crossed since the wiring.
            Rig rig = NewRig();
            rig.Door.SetOpen(true);
            Assert.IsTrue(rig.Interior.TryEnter(0));
            Assert.IsTrue(rig.Door.PassageIsSettled, "a wiring settles the doorway: nothing has crossed");

            for (int tick = 0; tick < 5 * 60; tick++)
                Assert.IsFalse(rig.Door.TryWalkThrough(OnTheLine, Vector2.zero, Tick),
                               $"tick {tick}: she is standing in an open doorway and she stays where she is");
            Assert.IsFalse(rig.Door.TryWalkThrough(OnTheLine, In, Tick),
                           "nor does a key held back into the room take her out of it");
            Assert.IsTrue(rig.Interior.IsInside);

            Assert.IsTrue(rig.Door.TryWalkThrough(OnTheLine, Out, Tick), "the key held out walks her out at once");
            Assert.IsFalse(rig.Interior.IsInside);
        }

        [Test]
        public void ARebuiltDoor_TakesHerAtOnce_WhereverHerFirstStepIs()
        {
            // ⭐ Cause 1 of the 2026-09-18 fleet report: a swap re-wires the door and leaves her at the helm,
            // on sixteen hulls nearer the threshold than one clear width, where the old latch never re-armed.
            // A wiring settles the doorway, and there is no ring left to start in: her key and the wall line
            // are all it asks.
            Rig rig = NewRig();
            Vector2 besideTheDoor = Doorway + new Vector2(0.5f, 0f);
            Assert.IsFalse(BoatCabinThreshold.IsInBand(rig.Door.Door, besideTheDoor));

            Open(rig);
            Assert.IsFalse(rig.Door.TryWalkThrough(besideTheDoor, In, Tick), "beside the doorway is not through it");
            Assert.IsTrue(rig.Door.TryWalkThrough(OnTheLine, In, Tick), "…and her next step, onto its line, takes her in");
            Assert.IsTrue(rig.Interior.IsInside);

            // A rebuild mid-settle (the swap's teardown lets her out, and the door is wired again) settles it.
            Assert.IsFalse(rig.Door.PassageIsSettled);
            Assert.IsTrue(rig.Interior.TryExit());
            rig.Door.Configure(rig.Interior, "fixture.boat.walkthrough_test.cabin_door", -1, 1.2f,
                               "Open the door", "Close the door");
            Assert.IsTrue(rig.Door.PassageIsSettled, "a wiring settles the doorway — the arrival relies on it");
            Open(rig);
            Assert.IsFalse(rig.Door.TryWalkThrough(besideTheDoor, In, Tick));
            Assert.IsTrue(rig.Door.TryWalkThrough(OnTheLine, In, Tick), "and she walks in through the rebuilt door");
            Assert.IsTrue(rig.Interior.IsInside);
        }

        // =====================================================================================
        //  4b · THE LINE — where she crosses, and the key that crosses her
        // =====================================================================================

        [Test]
        public void AKeyHeldThroughIsOneWithinTheDoorsCrossingCone_AndNoKeyIsNone()
        {
            Rig rig = NewRig();
            Open(rig);
            float cone = rig.Def.Door.CrossingCone;

            Assert.IsFalse(rig.Door.TryWalkThrough(OnTheLine, Vector2.zero, Tick), "no key, no crossing");
            Assert.IsFalse(rig.Door.TryWalkThrough(OnTheLine, Rotate(In, cone + 5f), Tick), "outside the cone");
            Assert.IsFalse(rig.Door.TryWalkThrough(OnTheLine, Out, Tick), "nor out onto the deck she stands on");
            Assert.IsFalse(rig.Interior.IsInside);
            Assert.IsTrue(rig.Door.TryWalkThrough(OnTheLine, Rotate(In, cone - 5f), Tick), "within it, she goes");
            Assert.IsTrue(rig.Interior.IsInside);
        }

        [Test]
        public void SheIsTakenFromAsNearTheLineAsHerFloorLetsHerStand_AndNotWhileSheIsStillWalkingToIt()
        {
            // On five of the fleet's hulls the deck ends at the sill's outer lip, 0.07 m short of the room:
            // she can stand in the doorway with the key held in and never reach the line. A tick that took
            // her no nearer it is her floor stopping her, and that is as near as she can get
            // (BoatCabinThreshold.IsPressedShort).
            Rig rig = NewRig();
            Open(rig);
            Vector2 onTheLip = Doorway;   // 0.1 m short of the line, in the band

            Assert.IsFalse(rig.Door.TryWalkThrough(onTheLip + Out * 0.05f, In, Tick), "on her way");
            Assert.IsFalse(rig.Door.TryWalkThrough(onTheLip, In, Tick), "still on her way: 5 cm nearer this tick");
            Assert.IsFalse(rig.Interior.IsInside);
            Assert.IsTrue(rig.Door.TryWalkThrough(onTheLip, In, Tick), "stopped by her floor short of it, she goes in");
            Assert.IsTrue(rig.Interior.IsInside);
        }

        [Test]
        public void ATickThatSlidesHerOffTheBandsEdgeOntoTheLine_StillTakesHer_AndOnlyThatOneTick()
        {
            // Her floor's clamp slides her along the wall in the tick she meets it, and at 30 or 60 Hz that
            // tick can end just past the band's edge: whether the doorway took her would hang on the frame
            // rate. One tick's grace, and no more.
            Rig rig = NewRig();
            Open(rig);
            BoatInteriorDoor door = rig.Door.Door;
            float band = BoatCabinThreshold.BandRadiusMetres(door);
            Vector2 inTheBand = Doorway + new Vector2(band - 0.06f, 0.05f);   // on the deck, 5 cm short
            Vector2 offItsEdge = OnTheLine + new Vector2(band + 0.01f, 0f);   // on the line, just outside
            Assert.IsTrue(BoatCabinThreshold.IsInBand(door, inTheBand));
            Assert.IsFalse(BoatCabinThreshold.IsInBand(door, offItsEdge));

            Assert.IsFalse(rig.Door.TryWalkThrough(inTheBand, In, Tick));
            Assert.IsTrue(rig.Door.TryWalkThrough(offItsEdge, In, Tick),
                          "the tick that slid her off the band's edge onto the line takes her");
            Assert.IsTrue(rig.Interior.IsInside);

            // …and never two: a tick out of the band after a tick out of it is not in the doorway.
            Rig again = NewRig();
            Open(again);
            Vector2 alsoOff = Doorway + new Vector2(band + 0.01f, 0.07f);     // off the band, 3 cm short
            Assert.IsFalse(BoatCabinThreshold.IsInBand(door, alsoOff));
            Assert.IsFalse(again.Door.TryWalkThrough(inTheBand, In, Tick));
            Assert.IsFalse(again.Door.TryWalkThrough(alsoOff, In, Tick), "short of the line, off the band");
            Assert.IsFalse(again.Door.TryWalkThrough(offItsEdge, In, Tick), "and a second tick off it");
            Assert.IsFalse(again.Interior.IsInside);
        }

        // =====================================================================================
        //  4c · THE PULL — D1 (a): the key within 30° of the way through is carried through
        // =====================================================================================

        [Test]
        public void D1a_AKeyWithinTheConeAndTheReach_IsSteeredAtTheOpening_AndNoOtherKeyIsBent()
        {
            Rig rig = NewRig();
            Open(rig);
            WalkIn(rig);   // she is on the sole: the way through is out
            BoatInteriorDoor door = rig.Door.Door;
            Vector2 inside = OnTheLine + new Vector2(0.3f, 0.2f);   // 0.42 m from the threshold
            Vector2 farther = OnTheLine + new Vector2(0f, BoatCabinThreshold.PullReachMetres(door) + 0.1f);
            Vector2 within = Rotate(Out, door.PullCone - 5f);
            Vector2 wider = Rotate(Out, door.PullCone + 5f);

            Vector2 steered = rig.Door.SteerHeld(inside, within);
            Assert.AreEqual(0f, Vector2.Angle(steered, OnTheLine - inside), 1e-3f,
                            "carried at the opening's centre on its wall line");
            Assert.AreEqual(within.magnitude, steered.magnitude, 1e-5f, "at her key's own length");
            Assert.IsTrue(rig.Door.CarriesHeld(inside, within));

            Assert.AreEqual(wider, rig.Door.SteerHeld(inside, wider), "outside the cone, nothing is bent");
            Assert.AreEqual(within, rig.Door.SteerHeld(farther, within), "farther off, nothing is bent");
            Assert.IsFalse(rig.Door.CarriesHeld(farther, within));

            Close(rig);
            Assert.AreEqual(within, rig.Door.SteerHeld(inside, within), "and a shut door pulls nobody");
        }

        // =====================================================================================
        //  5 · THE FALLBACK — a door with no measured opening keeps the old press-through
        // =====================================================================================

        [Test]
        public void ADoorWithNoMeasuredOpeningKeepsThePressThrough_AndNamesItself()
        {
            // ⭐ THE NEGATIVE CONTROL the charter asks for, and it runs both ways: with the band zeroed
            // the WALK must refuse, and the press must carry her exactly as it did before the ruling. A
            // hull left with a door that opened onto nothing would be a room unreachable for good.
            Rig rig = NewRig();
            rig.Def.Door.ClearWidthMeters = 0f;

            Assert.IsFalse(rig.Door.ThresholdIsWalkable);
            rig.Door.SetOpen(true);
            Assert.IsFalse(rig.Door.TryWalkThrough(OnTheLine, In, Tick),
                           "the walk-in must red with the band zeroed, even through the open door");
            Assert.IsFalse(rig.Interior.IsInside);
            rig.Door.SetOpen(false);

            LogAssert.Expect(LogType.Warning, new Regex("states no ClearWidthMeters"));
            Assert.IsTrue(rig.Door.TryUse());
            rig.Door.Tick(rig.Door.CueSeconds + 0.01f);
            Assert.IsTrue(rig.Interior.IsInside, "the press carries her through, as it did before 08-28");

            Assert.IsTrue(rig.Door.TryUse());
            rig.Door.Tick(rig.Door.CueSeconds + 0.01f);
            Assert.IsFalse(rig.Interior.IsInside, "…and back out again");
        }

        [Test]
        public void AnUnmeasuredCueOpensAtOnce()
        {
            // A door whose def states no cue is a leaf nobody has animated. The STATE must still land —
            // the feature cannot wait on art that may never come.
            Rig rig = NewRig();
            rig.Def.Door.CueFrames = 0;

            Assert.AreEqual(0f, rig.Door.CueSeconds);
            Assert.IsTrue(rig.Door.TryUse());
            Assert.IsFalse(rig.Door.IsCueing);
            Assert.IsTrue(rig.Door.IsOpen, "no frames to play, so the leaf is simply open");
        }

        // =====================================================================================
        //  6 · THE FLEET — every measured doorway is somewhere she can actually stand
        // =====================================================================================

        [Test]
        public void EveryMeasuredDoorwayIsReachableFromHerOwnWalkableDeck()
        {
            // ⭐⭐ THE GUARD THIS WHOLE CHANGE NEEDS, and it exists because the failure it catches is
            // SILENT. While the crossing was a press, where the doorway sat relative to the deck did not
            // matter: you pressed from anywhere within reach (1.2 m or better) and the swap carried you.
            // Now the crossing is a WALK, so the threshold must be a point the deck clamp will actually
            // let her stand on — within the band, which is half her own clear width and as little as
            // 0.24 m on the inshore lobsters. A hull whose doorway fell outside her walkable deck would
            // ship a cabin nobody can enter, with no error, no refused press and nothing in the log:
            // just a room the player walks past. One assertion over the whole fleet is the only honest
            // way to know it has not happened.
            //
            // ⚠ Reads the SHIPPED assets and mutates none of them (a run that rewrites a boat def is a
            // run that dirties the working tree behind you).
            var report = new StringBuilder();
            int measured = 0;
            float worstGap = 0f;
            string worstHull = "none";

            foreach (string guid in AssetDatabase.FindAssets("t:BoatVisualDef"))
            {
                var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (visual == null || visual.Interior == null || visual.Deck == null) continue;

                BoatInteriorDoor door = visual.Interior.Door;
                if (door == null || !BoatCabinThreshold.HasBand(door)) continue;
                if (!visual.Deck.HasWalkableDeck()) continue;

                measured++;
                Vector2 doorway = BoatCabinThreshold.PointOf(door);
                float band = BoatCabinThreshold.BandRadiusMetres(door);
                float gap = DistanceToWalkableDeck(visual.Deck, doorway);

                if (gap > worstGap) { worstGap = gap; worstHull = visual.name; }
                report.AppendLine(
                    $"  {visual.name,-36} band {band:F3} m   doorway {gap:F3} m from her walkable deck");

                Assert.Less(gap, band,
                    $"{visual.name}: her doorway {doorway} is {gap:F3} m outside every walkable deck " +
                    $"area, against a band of {band:F3} m. She can never stand in her own threshold, so " +
                    "her cabin is unreachable — the door would open onto a room the walk cannot cross.");
            }

            Assert.Greater(measured, 0,
                           "no measured hull carries both a deck and a doorway — this guard is asleep");

            Debug.Log($"[cabin-doorways] {measured} measured hulls, worst gap {worstGap:F3} m " +
                      $"({worstHull}):\n{report}");
        }

        [Test]
        public void EveryMeasuredDoorwaysWallLineStandsInItsBand_SoHerRoomCanLetHerOutAgain()
        {
            // ⭐⭐ THE OTHER HALF, and the one that would TRAP the player rather than merely shut her out.
            // The doorway crosses her on its wall line, and only while she stands in its band (owner ruling
            // D1, 2026-09-30). From the room's side that line is the sole's own edge, the nearest she can
            // walk to the deck. Were it farther from the threshold than the band reaches, she could stand
            // on it only outside the doorway, the doorway could never take her out, and she would be below
            // decks for good — with the door standing open in front of her. (The release radius this guard
            // checked before went with the latch it released.)
            //
            // ⚠ Reads the SHIPPED assets and mutates none of them.
            var report = new StringBuilder();
            int measured = 0;
            float tightest = float.PositiveInfinity;
            string tightestHull = "none";

            foreach (string guid in AssetDatabase.FindAssets("t:BoatVisualDef"))
            {
                var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (visual == null || visual.Interior == null) continue;

                BoatInteriorDoor door = visual.Interior.Door;
                if (door == null || !BoatCabinThreshold.HasBand(door)) continue;

                measured++;
                Assert.IsTrue(BoatCabinThreshold.TryWallLine(visual.Interior, door, out int level,
                                                             out Vector2 onLine, out _),
                    $"{visual.name}: her doorway is measured but stands in no wall at its sill's height, so " +
                    "the walk can never cross it and the door is back on the press.");

                float band = BoatCabinThreshold.BandRadiusMetres(door);
                float fromTheSill = Vector2.Distance(onLine, BoatCabinThreshold.PointOf(door));
                float margin = band - fromTheSill;
                if (margin < tightest) { tightest = margin; tightestHull = visual.name; }
                report.AppendLine(
                    $"  {visual.name,-36} band {band:F3} m   wall line {fromTheSill:F3} m from the sill   " +
                    $"margin {margin:F3} m   ('{visual.Interior.Levels[level].Id}')");

                Assert.Less(fromTheSill, band,
                    $"{visual.name}: her '{visual.Interior.Levels[level].Id}' wall line stands " +
                    $"{fromTheSill:F3} m from her own doorway, against a band of {band:F3} m. She can reach " +
                    "that line only outside the doorway, so once she walks in it can never take her out.");
            }

            Assert.Greater(measured, 0, "no measured hull reached this guard — it is asleep");

            Debug.Log($"[cabin-wall-lines] {measured} measured hulls, tightest margin {tightest:F3} m " +
                      $"({tightestHull}):\n{report}");
        }

        /// <summary>How far <paramref name="point"/> is from the nearest place a walker may stand, in the
        /// hull's own metres — 0 when it is ON the deck. WASHBOARDS do not count: a side deck is somewhere
        /// she climbs onto, deliberately not part of the free walk, and a doorway reachable only from the
        /// gunwale is not reachable.</summary>
        private static float DistanceToWalkableDeck(BoatDeckDef deck, Vector2 point)
        {
            float best = float.PositiveInfinity;
            foreach (DeckArea area in deck.Areas)
            {
                if (area == null || !area.IsUsable() || area.Kind != DeckAreaKind.Deck) continue;
                if (DeckAreaMath.Contains(area.Outline, point)) return 0f;

                DeckAreaMath.ClosestPointOnOutline(area.Outline, point, out float sqrDistance);
                best = Mathf.Min(best, Mathf.Sqrt(sqrDistance));
            }
            return best;
        }

        // =====================================================================================
        //  the fixture
        // =====================================================================================

        private struct Rig
        {
            public BoatInteriorDef Def;
            public BoatInterior Interior;
            public BoatCabinDoor Door;
        }

        /// <summary>Press her open and run the leaf out — said once so a re-measured cue does not need an
        /// edit per test.</summary>
        private static void Open(Rig rig)
        {
            Assert.IsTrue(rig.Door.TryUse());
            rig.Door.Tick(rig.Door.CueSeconds + 0.01f);
            Assert.IsTrue(rig.Door.IsOpen);
        }

        private static void Close(Rig rig)
        {
            Assert.IsTrue(rig.Door.TryUse());
            rig.Door.Tick(rig.Door.CueSeconds + 0.01f);
            Assert.IsFalse(rig.Door.IsOpen);
        }

        /// <summary>Walk her in across the open threshold: onto its wall line with the key held in — what a
        /// walker does on the tick she reaches it.</summary>
        private static void WalkIn(Rig rig)
        {
            Assert.IsTrue(rig.Door.TryWalkThrough(OnTheLine, In, Tick));
        }

        /// <summary>Let the doorway settle behind her: her walker's ticks, well clear of it and holding no
        /// key, for as long as the door's own settle and no longer.</summary>
        private static void Settle(Rig rig)
        {
            for (float waited = 0f; !rig.Door.PassageIsSettled; waited += Tick)
            {
                Assert.Less(waited, rig.Def.Door.CrossingSettle + Tick, "the settle is the door's own");
                rig.Door.TryWalkThrough(WellClear, Vector2.zero, Tick);
            }
        }

        /// <summary><paramref name="v"/> turned <paramref name="degrees"/> anticlockwise.</summary>
        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        private Sprite[] DummyCells(int n)
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            var cells = new Sprite[n];
            for (int i = 0; i < n; i++)
            {
                cells[i] = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
                cells[i].name = $"cell_{i}";
                _spawned.Add(cells[i]);
            }
            return cells;
        }

        /// <summary>
        /// A cabin and her door, wired the way the placement pass wires them, with a doorway whose
        /// measurement is the fleet's own: a 0.72 m opening on the aft face.
        /// </summary>
        private Rig NewRig()
        {
            var made = ScriptableObject.CreateInstance<BoatInteriorDef>();
            made.Id = "interior.walkthrough_test";
            made.PixelsPerMetre = 32;
            made.Levels = new[]
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
            made.Door = new BoatInteriorDoor
            {
                Id = "entry",
                Side = "aft",
                ClearWidthMeters = ClearWidth,
                ClearHeightMeters = 1.85f,
                ThresholdPoint = new Vector3(Doorway.x, Doorway.y, 1.78f),
                Mechanism = BoatInteriorDoorMechanism.Sliding,
                CueFrames = 8,
                CueMillisecondsPerFrame = 70f,
                CueReversedOnExit = true,
            };
            _spawned.Add(made);

            var root = new GameObject("WalkthroughTestHull");
            _spawned.Add(root);

            var exterior = new GameObject("House").AddComponent<SpriteRenderer>();
            exterior.transform.SetParent(root.transform, false);

            var room = new GameObject("Room").AddComponent<SpriteRenderer>();
            room.transform.SetParent(root.transform, false);

            var interior = root.AddComponent<BoatInterior>();
            interior.Configure(made, exterior, room, null, room.transform, root.transform,
                               DummyCells(8), 8, true, 0f, DeckRoll, HeavePixels, PitchLift);

            var door = new GameObject("CabinDoor").AddComponent<BoatCabinDoor>();
            door.transform.SetParent(root.transform, false);
            door.Configure(interior, "fixture.boat.walkthrough_test.cabin_door", -1, 1.2f,
                           "Open the door", "Close the door");

            return new Rig { Def = made, Interior = interior, Door = door };
        }
    }
}
