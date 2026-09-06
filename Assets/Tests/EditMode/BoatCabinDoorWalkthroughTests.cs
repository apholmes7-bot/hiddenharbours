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
    /// <see cref="BoatCabinDoor.TryWalkThrough"/> handed a hull-local metre. The walkers that supply that
    /// metre every tick are proved in PlayMode, where they exist; asserting them here would assert
    /// something the shipped game never does.</para>
    ///
    /// <para><b>⚠ The band is DATA, so the numbers below are derived and not typed.</b> The fixture's
    /// door states a 0.72 m opening — the cape islander's and the lobster boat's own measured clear
    /// width — which makes the band 0.36 m and the release 0.72 m. Both are read off
    /// <see cref="BoatCabinThreshold"/> rather than mirrored, so a re-measured doorway does not need a
    /// second edit here.</para>
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
        /// against a 0.72 m release radius.</summary>
        private static readonly Vector2 WellClear = new(0f, 0f);

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
        public void TheBandIsHalfTheMeasuredOpening_AndTheReleaseIsAWholeOne()
        {
            Rig rig = NewRig();
            BoatInteriorDoor door = rig.Door.Door;

            Assert.AreEqual(ClearWidth * 0.5f, BoatCabinThreshold.BandRadiusMetres(door), 1e-6f,
                            "standing in the doorway is standing within half its own width of the sill");
            Assert.AreEqual(ClearWidth, BoatCabinThreshold.ReleaseRadiusMetres(door), 1e-6f,
                            "and one whole width of daylight is the nearest she is unambiguously clear");
            Assert.Greater(BoatCabinThreshold.ReleaseRadiusMetres(door),
                           BoatCabinThreshold.BandRadiusMetres(door),
                           "the gap between the two IS the hysteresis; without it the sill strobes");
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
        public void BetweenTheBandAndTheRelease_SheIsNeitherInTheDoorwayNorClearOfIt()
        {
            // The hysteresis gap, asserted as the thing it is: a ring in which nothing changes. A walker
            // resting here has neither crossed nor re-armed, which is why the sill cannot flicker.
            Rig rig = NewRig();
            BoatInteriorDoor door = rig.Door.Door;
            Vector2 between = Doorway + new Vector2(0.5f, 0f);   // 0.36 < 0.5 < 0.72

            Assert.IsFalse(BoatCabinThreshold.IsInBand(door, between));
            Assert.IsFalse(BoatCabinThreshold.IsClearOfBand(door, between));
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
            Assert.IsFalse(BoatCabinThreshold.IsClearOfBand(door, WellClear),
                           "and nothing may ARM a threshold that cannot be crossed");
        }

        [Test]
        public void TheThresholdMathIsNullSafe_BecauseAHullMayHaveNoDoor()
        {
            Assert.AreEqual(Vector2.zero, BoatCabinThreshold.PointOf(null));
            Assert.AreEqual(0f, BoatCabinThreshold.BandRadiusMetres(null));
            Assert.AreEqual(0f, BoatCabinThreshold.ReleaseRadiusMetres(null));
            Assert.IsFalse(BoatCabinThreshold.HasBand(null));
            Assert.IsFalse(BoatCabinThreshold.IsInBand(null, Vector2.zero));
            Assert.IsFalse(BoatCabinThreshold.IsClearOfBand(null, Vector2.zero));
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

            // IN. One tick clear of the doorway arms her approach; the next, standing in it, spends it.
            Assert.IsFalse(rig.Door.TryWalkThrough(WellClear), "clear of it is not through it");
            Assert.IsTrue(rig.Door.PassageIsArmed);
            Assert.IsTrue(rig.Door.TryWalkThrough(Doorway), "she walks in");
            Assert.IsTrue(rig.Interior.IsInside);
            Assert.IsFalse(rig.Door.IsCueing, "no cue was spent — a hole in a wall costs nothing");
            Assert.IsTrue(rig.Door.IsOpen, "…and the door she walked through is still open");

            // OUT, through the same doorway, on the same terms.
            Assert.IsFalse(rig.Door.TryWalkThrough(WellClear));
            Assert.IsTrue(rig.Door.TryWalkThrough(Doorway), "she walks out");
            Assert.IsFalse(rig.Interior.IsInside);
        }

        [Test]
        public void AClosedDoorIsAWall_TheThresholdRefusesHerBothWays()
        {
            Rig rig = NewRig();

            // OUTSIDE, shut: standing in the doorway does nothing at all.
            rig.Door.TryWalkThrough(WellClear);
            Assert.IsTrue(rig.Door.PassageIsArmed, "she is clear of it, so the approach is a live one");
            Assert.IsFalse(rig.Door.TryWalkThrough(Doorway), "a shut door is a wall");
            Assert.IsFalse(rig.Interior.IsInside);

            // INSIDE, shut behind her: the same wall, from the other side.
            Open(rig);
            WalkIn(rig);
            Close(rig);

            rig.Door.TryWalkThrough(WellClear);
            Assert.IsFalse(rig.Door.TryWalkThrough(Doorway), "…and it is a wall from the sole too");
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
            rig.Door.TryWalkThrough(WellClear);          // arm her approach

            Assert.IsTrue(rig.Door.TryUse(), "press it shut");
            rig.Door.Tick(rig.Door.CueSeconds * 0.5f);
            Assert.IsTrue(rig.Door.IsCueing);
            Assert.IsTrue(rig.Door.IsOpen, "the state has not flipped yet — the leaf is still swinging");

            Assert.IsFalse(rig.Door.TryWalkThrough(Doorway),
                           "a door that is still moving is not a doorway you walk through");
            Assert.IsFalse(rig.Interior.IsInside);
        }

        // =====================================================================================
        //  4 · THE LATCH — one approach is one crossing, and the sill does not strobe
        // =====================================================================================

        [Test]
        public void OneApproachIsOneCrossing_SoStandingOnTheSillDoesNotFlickerTheLevel()
        {
            // The failure this guards is a frame-rate one: she lands a centimetre from the threshold she
            // just crossed, and an unlatched doorway would put her straight back out, and back in, at
            // sixty crossings a second.
            Rig rig = NewRig();
            Open(rig);
            rig.Door.TryWalkThrough(WellClear);

            Assert.IsTrue(rig.Door.TryWalkThrough(Doorway), "the crossing she asked for");
            Assert.IsTrue(rig.Interior.IsInside);

            for (int tick = 0; tick < 60; tick++)
                Assert.IsFalse(rig.Door.TryWalkThrough(Doorway),
                               $"tick {tick}: she is standing where she came in, not crossing again");

            Assert.IsTrue(rig.Interior.IsInside, "a whole second on the sill, and she is still below");
        }

        [Test]
        public void SheMustGetClearOfTheDoorwayBeforeItWillTakeHerAgain()
        {
            Rig rig = NewRig();
            Open(rig);
            rig.Door.TryWalkThrough(WellClear);
            rig.Door.TryWalkThrough(Doorway);
            Assert.IsTrue(rig.Interior.IsInside);

            // Inside the hysteresis ring: nearer than a whole clear width, so the approach is still spent.
            Vector2 halfAStepBack = Doorway + new Vector2(0.5f, 0f);
            Assert.IsFalse(rig.Door.TryWalkThrough(halfAStepBack));
            Assert.IsFalse(rig.Door.PassageIsArmed, "0.5 m is not clear of a 0.72 m release");
            Assert.IsFalse(rig.Door.TryWalkThrough(Doorway));
            Assert.IsTrue(rig.Interior.IsInside);

            // A whole width of daylight, and the doorway is a fresh one.
            Assert.IsFalse(rig.Door.TryWalkThrough(WellClear));
            Assert.IsTrue(rig.Door.PassageIsArmed);
            Assert.IsTrue(rig.Door.TryWalkThrough(Doorway), "now she may come back out");
            Assert.IsFalse(rig.Interior.IsInside);
        }

        [Test]
        public void ThePassageStartsDisarmed_BecauseTheArrivalOpensHerInADoorway()
        {
            // A threshold is on the sole's edge by construction, and the game's first frame stands the
            // passenger in Armand's. An armed latch would walk her out of his cabin before she moved.
            Rig rig = NewRig();
            Assert.IsFalse(rig.Door.PassageIsArmed);

            rig.Door.SetOpen(true);
            Assert.IsTrue(rig.Interior.TryEnter(0));

            Assert.IsFalse(rig.Door.TryWalkThrough(Doorway),
                           "she is standing in an open doorway and she stays where she is");
            Assert.IsTrue(rig.Interior.IsInside);
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
            rig.Door.TryWalkThrough(WellClear);
            Assert.IsFalse(rig.Door.TryWalkThrough(Doorway), "the walk-in must red with the band zeroed");
            Assert.IsFalse(rig.Interior.IsInside);

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

        /// <summary>Walk her across the open threshold: one tick clear of it to arm the approach, one in
        /// it to spend it. Exactly what a walker does, two frames apart.</summary>
        private static void WalkIn(Rig rig)
        {
            rig.Door.TryWalkThrough(WellClear);
            Assert.IsTrue(rig.Door.TryWalkThrough(Doorway));
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
