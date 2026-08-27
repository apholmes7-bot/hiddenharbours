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

            Assert.AreEqual(2f, got.x, 1e-4f, "she should be standing against the starboard side");
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
            // A 0.02 m slot between two blocks: every push out of one lands inside the other.
            BoatInteriorLevel level = Room(Block("port", -0.51f, 0f, "wall"),
                                           Block("starboard", 0.51f, 0f, "wall"));
            var safe = new Vector2(0f, -1.5f);

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
        /// ⚠️ <b>THE CUTAWAY GAP, PINNED AS A RULE RATHER THAN AS A NUMBER.</b> The cape's rig has never
        /// been through a cutaway pass, so her hull mesh carries no level tags and her house cannot open —
        /// the player below sees the room drawn over her closed house (the accepted overdraw). That is an
        /// upstream art item, named in this lane's PR, and it must not be papered over here.
        ///
        /// <para>What IS asserted is the invariant that survives the day the tags land: every tag her mesh
        /// carries must name a level her interior def declares. Vacuously true today, and the moment
        /// somebody bakes her cutaway with a rig id (<c>house</c>) where the def says <c>house_sole</c>,
        /// this goes red instead of the cut silently answering None forever.</para>
        /// </summary>
        [Test]
        public void EveryCutawayTagOnHerHull_NamesALevelHerInteriorDefDeclares()
        {
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                "Assets/_Project/Data/Boats/Visuals/CapeIslanderIso.asset");
            Assert.IsNotNull(visual, "the cape's visual def must exist");
            Assert.IsNotNull(visual.HullMesh, "…and she is a mesh hull");

            BoatInteriorDef def = Cape();
            HullMeshDef.LevelTag[] tags = visual.HullMesh.LevelTags;

            if (tags == null || tags.Length == 0)
            {
                Assert.Pass("Her rig has no cutaway pass yet, so BoatCutaway answers Cut.None on her and " +
                            "the room draws over her closed house. Upstream art item; the join rule below " +
                            "starts biting the day the tags arrive.");
                return;
            }

            foreach (HullMeshDef.LevelTag tag in tags)
            {
                if (string.IsNullOrEmpty(tag.DeckId)) continue;
                Assert.IsNotNull(def.Level(tag.DeckId),
                    $"her mesh declares cutaway level '{tag.DeckId}', which her interior def does not " +
                    "declare — the join is by the def's own level ID, so a cut named in the rig's " +
                    "vocabulary silently opens nothing");
            }
        }
    }
}
