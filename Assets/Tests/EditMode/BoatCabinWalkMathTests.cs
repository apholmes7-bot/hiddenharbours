using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE RULE ABOUT WHERE A PLAYER MAY STAND INSIDE A BOAT</b> — asserted with no scene, no hull
    /// and no play mode, which is the whole reason <see cref="BoatCabinWalkMath"/> is pure. A clamp that
    /// can only be exercised by sailing into St Peters is a clamp nobody exercises.
    ///
    /// <para>Two halves, and they are deliberately not mixed. The first is the GEOMETRY on hand-built
    /// polygons, where every expected answer can be worked out on paper. The second holds the same rules
    /// against the <b>cape islander's own committed def</b> — the room the game actually opens in — because
    /// a rule that is right about a unit square and wrong about her helm console is a rule that ships a
    /// player standing inside a wheel.</para>
    /// </summary>
    public class BoatCabinWalkMathTests
    {
        private const string CapeInteriorPath =
            "Assets/_Project/Data/Boats/Interiors/CapeIslanderIso.asset";

        /// <summary>A 4 × 4 m room about the origin, wound counter-clockwise as the sidecars wind.</summary>
        private static BoatInteriorLevel Room(params BoatInteriorObstruction[] furniture) =>
            new BoatInteriorLevel
            {
                Id = "test_sole",
                SoleZMeters = 0.5f,
                Outline = new[]
                {
                    new Vector2(-2f, -2f), new Vector2(2f, -2f),
                    new Vector2(2f, 2f), new Vector2(-2f, 2f),
                },
                Obstructions = furniture ?? System.Array.Empty<BoatInteriorObstruction>(),
            };

        /// <summary>A 1 × 1 m block centred on (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
        private static BoatInteriorObstruction Block(string id, float cx, float cy, string treatment)
            => new BoatInteriorObstruction
            {
                Id = id,
                Treatment = treatment,
                HeightAboveSoleMeters = 1f,
                Footprint = new[]
                {
                    new Vector2(cx - 0.5f, cy - 0.5f), new Vector2(cx + 0.5f, cy - 0.5f),
                    new Vector2(cx + 0.5f, cy + 0.5f), new Vector2(cx - 0.5f, cy + 0.5f),
                },
            };

        // =============================================================================================
        //  the geometry
        // =============================================================================================

        [Test]
        public void APointOnTheSole_IsLeftExactlyWhereItIs()
        {
            BoatInteriorLevel level = Room();
            var wanted = new Vector2(0.3f, -1.1f);

            Vector2 got = BoatCabinWalkMath.ClampToSole(level, wanted, Vector2.zero);

            Assert.AreEqual(wanted.x, got.x, 1e-5f, "the clamp moved a point that was already standable");
            Assert.AreEqual(wanted.y, got.y, 1e-5f, "the clamp moved a point that was already standable");
        }

        [Test]
        public void APointOffTheSole_IsPulledToTheNearestEdge_SoSheSlidesAlongTheWall()
        {
            BoatInteriorLevel level = Room();

            Vector2 got = BoatCabinWalkMath.ClampToSole(level, new Vector2(5f, 1.2f), Vector2.zero);

            // ⚠ Against the side, and a HAIR inside it — never exactly on the line. DeckAreaMath.Contains
            // reads an on-edge point either way, so "on the wall" would be a position she is outside the
            // room from about half the time. The tolerance is the clamp's own inset, not a fudge.
            Assert.AreEqual(2f, got.x, 0.01f, "she should be standing against the starboard side");
            Assert.Less(got.x, 2f, "…and strictly INSIDE it, not on the line");
            Assert.AreEqual(1.2f, got.y, 1e-4f,
                "…and NOT dragged fore-and-aft: sliding along a wall is the whole point of a nearest-" +
                "point clamp rather than a refusal");
            Assert.IsTrue(BoatCabinWalkMath.IsStandable(level, got),
                "the clamp's own answer must be somewhere she may stand");
        }

        /// <summary>⭐ Both treatments block. The def carries the sidecar's word verbatim and says the
        /// runtime rules on it; this IS that ruling, and it is asserted on both words rather than on the
        /// one the cape happens to use most.</summary>
        [TestCase("wall")]
        [TestCase("waist_block")]
        [TestCase("something_the_kit_has_not_invented_yet")]
        public void FurnitureBlocks_WhateverTheSidecarCallsIt(string treatment)
        {
            BoatInteriorLevel level = Room(Block("locker", 0f, 0f, treatment));

            Assert.IsFalse(BoatCabinWalkMath.IsStandable(level, Vector2.zero),
                $"a walker is standing inside the '{treatment}' locker");

            Vector2 got = BoatCabinWalkMath.ClampToSole(level, Vector2.zero, new Vector2(0f, -1.5f));

            Assert.IsTrue(BoatCabinWalkMath.IsStandable(level, got),
                $"the clamp left her inside the '{treatment}' locker at {got}");
        }

        [Test]
        public void AnUnmeasuredFootprint_IsNotAWall()
        {
            var half = new BoatInteriorObstruction
            {
                Id = "half_measured",
                Treatment = "wall",
                Footprint = new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, 0.5f) },
            };

            Assert.IsFalse(BoatCabinWalkMath.Blocks(half),
                "two points cannot enclose anything — a measurement that did not finish must not become " +
                "invisible furniture in the middle of a room");
            Assert.IsTrue(BoatCabinWalkMath.IsStandable(Room(half), Vector2.zero));
        }

        [Test]
        public void WedgedBetweenTwoPiecesOfFurniture_SheKeepsThePositionSheCameInWith()
        {
            // ⚠ The blocks OVERLAP by 0.2 m — every push out of one lands inside the other, and the
            // ping-pong is the point. (A GAP between them would leave a legal 2 cm slot to stand in, and
            // the clamp would rightly put her there: that version of this fixture asserted nothing.)
            BoatInteriorLevel level = Room(Block("port", -0.4f, 0f, "wall"),
                                           Block("starboard", 0.4f, 0f, "wall"));
            var safe = new Vector2(0f, -1.5f);
            Assert.IsFalse(BoatCabinWalkMath.IsStandable(level, Vector2.zero),
                "the fixture is not wedging her at all — the blocks do not overlap");

            Vector2 got = BoatCabinWalkMath.ClampToSole(level, Vector2.zero, safe);

            Assert.AreEqual(safe, got,
                "a walker who cannot be placed must not move — there is no arrangement of furniture in " +
                "which standing inside a locker is the better answer");
        }

        [Test]
        public void TheProjectionRoundTrips_AtEveryHeading_AndBothHandednesses()
        {
            var local = new Vector2(0.7f, 1.9f);
            const float soleZ = 0.72f;
            const float elevation = 40f;

            for (int heading = 0; heading < 360; heading += 45)
            {
                foreach (bool ccw in new[] { true, false })
                {
                    Vector2 world = BoatCabinWalkMath.ToWorldOffset(local, soleZ, heading, elevation, ccw);
                    Vector2 back = BoatCabinWalkMath.FromWorldOffset(world, soleZ, heading, elevation, ccw);

                    Assert.AreEqual(local.x, back.x, 1e-3f, $"x at {heading}°, ccw={ccw}");
                    Assert.AreEqual(local.y, back.y, 1e-3f, $"y at {heading}°, ccw={ccw}");
                }
            }
        }

        /// <summary>⚠ The handedness is a real degree of freedom, not a formality: getting it wrong
        /// mirrors the cabin end for end against the doorway she is walking to. This pins that the two
        /// answers actually DIFFER, so a future simplification that drops the flag goes red.</summary>
        [Test]
        public void HandednessIsNotDecorative_TheTwoConventionsDisagree()
        {
            var local = new Vector2(1f, 2f);

            Vector2 ccw = BoatCabinWalkMath.ToWorldOffset(local, 0.72f, 55f, 40f, true);
            Vector2 cw = BoatCabinWalkMath.ToWorldOffset(local, 0.72f, 55f, 40f, false);

            Assert.Greater((ccw - cw).magnitude, 0.5f,
                "the clockwise and counter-clockwise projections agree, which means the flag is being " +
                "ignored somewhere");
        }

        [Test]
        public void PressingUpScreen_WalksHerUpScreen_AtEveryHeading()
        {
            BoatInteriorLevel level = Room();
            const float elevation = 40f;

            for (int heading = 0; heading < 360; heading += 45)
            {
                Vector2 stepped = BoatCabinWalkMath.Step(level, Vector2.zero, Vector2.up,
                                                         1f, 1f, heading, elevation, true);
                Vector2 travel = BoatCabinWalkMath.ToWorldOffset(stepped, level.SoleZMeters, heading,
                                                                 elevation, true)
                                 - BoatCabinWalkMath.ToWorldOffset(Vector2.zero, level.SoleZMeters,
                                                                   heading, elevation, true);

                Assert.Greater(travel.y, 0f, $"pressing up-screen walked her DOWN-screen at {heading}°");
                Assert.AreEqual(0f, travel.x, 1e-3f,
                    $"pressing up-screen slid her sideways at {heading}° — the input transform is not the " +
                    "inverse of the projection");
            }
        }

        [Test]
        public void AStepIsMetresOfSolePerSecond_NotMetresOfScreen()
        {
            BoatInteriorLevel level = Room();

            Vector2 got = BoatCabinWalkMath.Step(level, Vector2.zero, Vector2.up,
                                                 speedMetresPerSecond: 1.4f, deltaSeconds: 0.5f,
                                                 drawnHeadingDegrees: 0f, bakeElevationDegrees: 40f,
                                                 azimuthCounterClockwise: true);

            Assert.AreEqual(0.7f, got.magnitude, 1e-3f,
                "half a second at 1.4 m/s is 0.7 m of SOLE — the foreshortening belongs to the picture, " +
                "not to the distance she walked");
        }

        [Test]
        public void NoInput_StillClamps_SoAHullSwappedUnderHerCannotStrandHerOutside()
        {
            BoatInteriorLevel level = Room();

            Vector2 got = BoatCabinWalkMath.Step(level, new Vector2(9f, 9f), Vector2.zero,
                                                 1.4f, 0.016f, 0f, 40f, true);

            Assert.IsTrue(BoatCabinWalkMath.IsStandable(level, got),
                "a walker outside the sole with no keys down stayed outside it");
        }

        [Test]
        public void TheStepIsDeterministic()
        {
            BoatInteriorLevel level = Room(Block("console", 0.4f, 0.9f, "wall"));
            var input = new Vector2(0.6f, 0.8f);

            Vector2 a = BoatCabinWalkMath.Step(level, new Vector2(0.1f, 0.2f), input, 1.4f, 0.017f,
                                               37f, 40f, true);
            Vector2 b = BoatCabinWalkMath.Step(level, new Vector2(0.1f, 0.2f), input, 1.4f, 0.017f,
                                               37f, 40f, true);

            Assert.AreEqual(a, b, "the same inputs gave two answers");
        }

        // =============================================================================================
        //  the cape islander — the room the game actually opens in
        // =============================================================================================

        private static BoatInteriorDef Cape()
        {
            var def = AssetDatabase.LoadAssetAtPath<BoatInteriorDef>(CapeInteriorPath);
            Assert.IsNotNull(def, $"the cape's committed interior def must exist at {CapeInteriorPath} — " +
                                  "it is the room the opening starts in");
            return def;
        }

        /// <summary>The level her aft door's sill walks you in onto, by the def's own heights. Not
        /// hard-coded to an index: the def is the authority, and an index typed here would be a second
        /// opinion about her levels.</summary>
        private static BoatInteriorLevel HouseSole(BoatInteriorDef def)
        {
            BoatInteriorLevel best = null;
            float bestGap = 0f;
            foreach (BoatInteriorLevel l in def.Levels)
            {
                if (l == null || !l.IsUsable()) continue;
                float gap = Mathf.Abs(l.SoleZMeters - def.Door.ThresholdPoint.z);
                if (best == null || gap < bestGap) { best = l; bestGap = gap; }
            }
            return best;
        }

        [Test]
        public void HerAftDoorSill_LandsOnHerHouseSole()
        {
            BoatInteriorDef def = Cape();

            Assert.AreEqual("house_sole", HouseSole(def).Id,
                "the opening puts the player in whichever room her sill resolves to — if that is no " +
                "longer the house, the intro cabin has quietly changed room");
        }

        /// <summary>⭐ The one that matters most: the game's very first frame must not place her inside the
        /// helm console. The start point is the door's threshold pulled onto the sole by the ordinary
        /// clamp, so this asserts the clamp on her REAL furniture rather than on a unit square.</summary>
        [Test]
        public void SheStartsSomewhereSheMayActuallyStand_OnHerOwnMeasuredSole()
        {
            BoatInteriorDef def = Cape();
            BoatInteriorLevel sole = HouseSole(def);

            Vector2 start = BoatCabinWalkMath.StartPointFor(sole, def.Door);

            Assert.IsTrue(BoatCabinWalkMath.IsStandable(sole, start),
                $"the game opens with the player standing at {start}, which is off her sole or inside " +
                "her furniture");
        }

        /// <summary>She has to be able to WORK the door she starts beside — the reach the installer derives
        /// from the leaf the kit measured, applied to the distance from her start point to the threshold,
        /// both in the sole's own metres. A start point outside that reach is a cabin with no way out until
        /// she finds it.</summary>
        [Test]
        public void SheStartsWithinReachOfHerOwnWayOut()
        {
            BoatInteriorDef def = Cape();
            BoatInteriorLevel sole = HouseSole(def);

            Vector2 start = BoatCabinWalkMath.StartPointFor(sole, def.Door);
            var threshold = new Vector2(def.Door.ThresholdPoint.x, def.Door.ThresholdPoint.y);
            float reach = Mathf.Max(1.2f, def.Door.ClearWidthMeters + 0.5f);   // the installer's own rule

            Assert.LessOrEqual(Vector2.Distance(start, threshold), reach,
                $"she opens the game {Vector2.Distance(start, threshold):F2} m from her own doorway, " +
                $"which reaches {reach:F2} m");
        }

        /// <summary>Her cabin is somewhere you can move ABOUT, not a spot you are parked on. Walked in the
        /// four screen directions from the start point, at least two must actually take her somewhere —
        /// a room where every direction is refused is a room with no walking in it.</summary>
        [Test]
        public void HerHouseHasRoomToWalkAboutIn()
        {
            BoatInteriorDef def = Cape();
            BoatInteriorLevel sole = HouseSole(def);
            Vector2 start = BoatCabinWalkMath.StartPointFor(sole, def.Door);

            int moved = 0;
            foreach (Vector2 dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                Vector2 to = BoatCabinWalkMath.Step(sole, start, dir, 1.4f, 0.25f, 0f, 40f, true);
                if (Vector2.Distance(to, start) > 0.1f) moved++;
            }

            Assert.GreaterOrEqual(moved, 2,
                $"only {moved} of four directions moved her at all from {start} — that is a doorway to " +
                "stand in, not a cabin to walk about");
        }

        /// <summary>
        /// ⭐ <b>THE CUTAWAY JOIN, AND IT ONLY BINDS THE LEVELS THAT CAN BE CUT INTO.</b> Written on
        /// 2026-08-27 as a vacuous rule waiting for her tags, it bit the moment cutaway pass 4 landed
        /// (#685) — and bit slightly too wide, which is the useful half of the story.
        ///
        /// <para><b>What the gate is actually handed.</b> <see cref="BoatCutaway"/> resolves a cut with
        /// <c>DeckIdOf(level)</c>, which returns <c>def.Levels[i].Id</c> — an INTERIOR DEF level id,
        /// always. And <see cref="HullMeshDef.CutawayForDeck"/> refuses any row whose
        /// <c>Enclosed</c> is false. So the join that must hold is exactly: <b>every ENCLOSED tag
        /// names a level her interior def declares</b>. That is the original rule, unweakened, and it
        /// is still what stops a cut named in the rig's vocabulary (<c>house</c>) from silently
        /// answering None forever against a def that says <c>house_sole</c>.</para>
        ///
        /// <para><b>Why OPEN levels are not required to join, and must not be.</b> Pass 4 tags open
        /// decks too — <c>cockpit</c> and <c>foredeck</c> here — because "this level is open, cut
        /// nothing" is a fact the cutaway needs. Their <c>DeckId</c> is never handed to the gate and
        /// could not produce a cut if it were. The tempting amendment — "must name a level of the
        /// interior def OR a walkable area of her DECK def" — was measured against the fleet and is
        /// not a rule this data can satisfy: the two vocabularies are DISJOINT and at different
        /// granularity. Her deck def splits the working deck into named polygons
        /// (<c>cockpit_sole</c>, <c>washboard_port</c>, …) while the rig names the whole open LEVEL
        /// (<c>cockpit</c>); the trawler, packet and tanker all tag <c>main_deck</c>, which none of
        /// their deck defs contains, and the tanker adds <c>poop_deck</c> against a def that says
        /// <c>poop_aft</c>. Four of the five cutaway hulls would go red on a rule invented to make
        /// this one green. The cape's <c>foredeck</c> matching a deck-def area of the same name is a
        /// coincidence, not the pattern.</para>
        ///
        /// <para>So the open arm asserts what is actually true of them: a tag carries a name, and the
        /// gate REFUSES it. That second half is not decoration — an open level reaching the table as
        /// enclosed is how you take the roof off the sky.</para>
        ///
        /// <para>This is the same shape
        /// <c>HullLevelTagBakeTests.EveryPublishedLevel_HasACeilingOrADeclaredOpenSky_AndNamesADefLevel</c>
        /// has had since #673 (<c>if (!lvl.Enclosed …) continue;</c>) — that fixture went fleet-wide
        /// green on pass 4 while this one went red, which is the tell that the rule here was the
        /// outlier and not the data.</para>
        /// </summary>
        [Test]
        public void EveryEnclosedCutawayTagOnHerHull_NamesALevelHerInteriorDefDeclares()
        {
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                "Assets/_Project/Data/Boats/Visuals/CapeIslanderIso.asset");
            Assert.IsNotNull(visual, "the cape's visual def must exist");
            Assert.IsNotNull(visual.HullMesh, "…and she is a mesh hull");

            BoatInteriorDef def = Cape();
            HullMeshDef.LevelTag[] tags = visual.HullMesh.LevelTags;

            Assert.IsNotNull(tags, "her LevelTags array is null, which no bake writes.");
            Assert.IsNotEmpty(tags,
                "her mesh carries no cutaway tags. Pass 4 landed them in #685, so an empty table now " +
                "means a bake regressed her — it is no longer the honest 'she has had no pass yet'.");

            int enclosed = 0, open = 0;
            foreach (HullMeshDef.LevelTag tag in tags)
            {
                Assert.IsNotEmpty(tag.DeckId ?? "",
                    $"her level '{tag.LevelId}' carries an EMPTY deck id. A blank joins to nothing and " +
                    "reads as 'not applicable', which is a claim no rig should make silently.");

                if (!tag.Enclosed)
                {
                    open++;
                    Assert.IsFalse(visual.HullMesh.CutawayForDeck(tag.DeckId).Opens,
                        $"'{tag.DeckId}' is an OPEN level and the gate offered a cut for it. You cannot " +
                        "take the roof off the sky, and this is the arm that catches an open deck " +
                        "reaching the table as enclosed.");
                    continue;
                }

                enclosed++;
                Assert.IsNotNull(def.Level(tag.DeckId),
                    $"her mesh declares ENCLOSED cutaway level '{tag.DeckId}', which her interior def " +
                    "does not declare — the join is by the def's own level ID, so a cut named in the " +
                    "rig's vocabulary silently opens nothing. Her def declares: " +
                    string.Join(", ", System.Linq.Enumerable.Select(
                        def.Levels ?? System.Array.Empty<BoatInteriorLevel>(),
                        l => l == null ? "<null>" : l.Id)));

                Assert.IsTrue(visual.HullMesh.CutawayForDeck(tag.DeckId).Opens,
                    $"'{tag.DeckId}' joins her def and is enclosed, and the gate STILL returned no cut.");
            }

            Assert.AreEqual(2, enclosed,
                "she has two rooms — the wheelhouse and the cuddy under the whaleback. A different " +
                "count is her rig changing shape and wants a look, not a wider assertion.");
            Assert.Greater(open, 0,
                "not one open level was examined, so the refusal arm above proved nothing.");
        }
    }
}
