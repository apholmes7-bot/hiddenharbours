using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The head-to-pillow maths — the rule that decides which way round a sleeper lies.
    ///
    /// <para><b>Why this is worth a headless suite of its own.</b> Every way this can be wrong DRAWS: a
    /// fisher laid out along the wrong axis of her own bed is a picture, not an exception, and nothing
    /// downstream of the pose can tell it from art. Before the declaration existed the heading was one
    /// serialized number, which is right for a bed in an UNTURNED room and wrong at the other seven
    /// facings — so the case that actually shipped is the one case a spot check would have passed.</para>
    /// </summary>
    public class BedPillowTests
    {
        /// <summary>
        /// Two bearings are the same bearing. Compared as an ANGLE and not as a number, because 359.9999°
        /// and 0° are the same direction and <c>Mathf.Sin(-180°)</c> is 8.7e-8 rather than 0 — a straight
        /// numeric compare fails at exactly the facing where the sleeper is turned right round, which is
        /// the case most worth having a test for.
        /// </summary>
        static void AssertBearing(float expected, float actual, string because) =>
            Assert.AreEqual(0f, Mathf.DeltaAngle(expected, actual), 1e-3f,
                            $"{because}: expected a bearing of {expected:0.###}deg, got {actual:0.###}deg");

        /// <summary>Two directions are the same direction, componentwise and with a tolerance — a sign of
        /// zero is not a disagreement.</summary>
        static void AssertDirection(Vector2 expected, Vector2 actual, string because)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-5f, $"{because}: x");
            Assert.AreEqual(expected.y, actual.y, 1e-5f, $"{because}: y");
        }

        // =============================================================================
        //  the declaration itself
        // =============================================================================

        [Test]
        public void UndeclaredIsTheDEFAULT_SoAForgottenBedFailsRatherThanGuesses()
        {
            // The whole trip-wire: a Furnishing row, a serialized field or a default(PillowAnchor) that
            // nobody filled in must read as "nobody said", not as a plausible side.
            Assert.AreEqual(PillowSide.Undeclared, default(PillowSide));
            Assert.IsFalse(BedPillow.IsDeclared(default(PillowSide)));
            Assert.IsFalse(PillowAnchor.None.IsDeclared);
        }

        [Test]
        public void EveryDeclaredSideIsAUnitDirection_AndUndeclaredIsZero()
        {
            foreach (PillowSide side in new[]
                     { PillowSide.MinusY, PillowSide.PlusY, PillowSide.MinusX, PillowSide.PlusX })
                Assert.AreEqual(1f, BedPillow.Direction(side).magnitude, 1e-5f,
                                $"{side} must be a unit direction");

            // Zero and not (0,1): a caller that skipped IsDeclared gets an answer it cannot mistake for
            // a heading, rather than one that quietly means "north".
            AssertDirection(Vector2.zero, BedPillow.Direction(PillowSide.Undeclared),
                            "an undeclared side is no direction at all");
        }

        [Test]
        public void TheFourSidesFaceOppositeWaysInPairs()
        {
            AssertDirection(-BedPillow.Direction(PillowSide.MinusY),
                            BedPillow.Direction(PillowSide.PlusY), "+y is the opposite of -y");
            AssertDirection(-BedPillow.Direction(PillowSide.MinusX),
                            BedPillow.Direction(PillowSide.PlusX), "+x is the opposite of -x");
        }

        // =============================================================================
        //  turning a side from one frame into another
        // =============================================================================

        [Test]
        public void AQuarterTurnWalksTheSidesClockwiseOnTheGround()
        {
            // The conversion between the PROP's own frame (what the rig drew) and the ROOM's (what a
            // placement declares). Pinned against the rotation itself two tests below.
            Assert.AreEqual(PillowSide.MinusX, BedPillow.Turned(PillowSide.MinusY, 1));
            Assert.AreEqual(PillowSide.PlusY, BedPillow.Turned(PillowSide.MinusX, 1));
            Assert.AreEqual(PillowSide.PlusX, BedPillow.Turned(PillowSide.PlusY, 1));
            Assert.AreEqual(PillowSide.MinusY, BedPillow.Turned(PillowSide.PlusX, 1));
        }

        [Test]
        public void TurningWraps_BothWays_AndFourTurnsIsNoTurn()
        {
            foreach (PillowSide side in new[]
                     { PillowSide.MinusY, PillowSide.PlusY, PillowSide.MinusX, PillowSide.PlusX })
            {
                Assert.AreEqual(side, BedPillow.Turned(side, 4), "four quarter-turns is a full circle");
                Assert.AreEqual(side, BedPillow.Turned(side, -4));
                Assert.AreEqual(BedPillow.Turned(side, 1), BedPillow.Turned(side, 5));
                Assert.AreEqual(BedPillow.Turned(side, 3), BedPillow.Turned(side, -1),
                                "a turn back is a turn the other way round the circle");
            }
        }

        [Test]
        public void TurningAnUndeclaredSideIsStillUndeclared()
        {
            // Turning "nobody said" must never manufacture a side.
            Assert.AreEqual(PillowSide.Undeclared, BedPillow.Turned(PillowSide.Undeclared, 1));
            Assert.AreEqual(PillowSide.Undeclared, BedPillow.Turned(PillowSide.Undeclared, 0));
        }

        [Test]
        public void TurningASideAgreesWithActuallyRotatingItsDirection()
        {
            // ⭐ The two ways of saying the same thing, held together. Turned() is a table walk; the
            // rotation is the geometry. If they ever disagree, the content check that compares a declared
            // side against the drawn one is comparing the wrong things and would pass a wrong bed.
            foreach (PillowSide side in new[]
                     { PillowSide.MinusY, PillowSide.PlusY, PillowSide.MinusX, PillowSide.PlusX })
            {
                for (int quarters = 0; quarters < 4; quarters++)
                {
                    // A quarter-turn is TWO facings on an 8-facing kit.
                    float yaw = BedPillow.YawDegreesFor(quarters * 2, 8);
                    Vector2 rotated = BedPillow.GroundDirection(side, yaw);
                    Vector2 tabled = BedPillow.Direction(BedPillow.Turned(side, quarters));

                    Assert.AreEqual(tabled.x, rotated.x, 1e-4f, $"{side} turned {quarters}: x");
                    Assert.AreEqual(tabled.y, rotated.y, 1e-4f, $"{side} turned {quarters}: y");
                }
            }
        }

        // =============================================================================
        //  the yaw, and the one place it is really defined
        // =============================================================================

        [Test]
        public void CoresYawIsTheSameYawTheROOMSFootprintUses()
        {
            // ⭐ Core cannot reference World, so BedPillow restates InteriorFootprint.YawDegrees. This is
            // the mechanism that stops the restatement drifting: a sign flip here would turn every bed in
            // the game the wrong way and would still draw a person asleep on a bed.
            for (int facing = 0; facing < 8; facing++)
            {
                var footprint = new InteriorFootprint(Vector2.zero, 6.6f, 8.05f, facing, 8,
                                                      IsoGround.GroundDepthScale);
                Assert.AreEqual(footprint.YawDegrees, BedPillow.YawDegreesFor(facing, 8), 1e-4f,
                                $"facing {facing}");
            }
        }

        [Test]
        public void AFacingCountOfZeroIsNotADivideByZero()
        {
            Assert.AreEqual(0f, BedPillow.YawDegreesFor(3, 0), 1e-6f);
        }

        // =============================================================================
        //  the heading the sleeper lies along
        // =============================================================================

        [Test]
        public void InAnUnturnedRoom_TheHeadingIsTheCompassPointThePillowIsOn()
        {
            // 0 = north = +y, growing clockwise (IsoGround's convention, the project's everywhere).
            Assert.IsTrue(BedPillow.TryHeadingDegrees(PillowSide.PlusY, 0f, out float north));
            AssertBearing(0f, north, "a +y pillow points north");

            Assert.IsTrue(BedPillow.TryHeadingDegrees(PillowSide.PlusX, 0f, out float east));
            AssertBearing(90f, east, "a +x pillow points east");

            Assert.IsTrue(BedPillow.TryHeadingDegrees(PillowSide.MinusY, 0f, out float south));
            AssertBearing(180f, south, "a -y pillow points south");

            Assert.IsTrue(BedPillow.TryHeadingDegrees(PillowSide.MinusX, 0f, out float west));
            AssertBearing(270f, west, "a -x pillow points west");
        }

        [Test]
        public void TheKitBedInAnUnturnedRoomIsTheHEADINGThePoseUsedToHardCode()
        {
            // The one case the old fixed 180 was right about — kept as a test so the fix is provably a
            // GENERALISATION of the shipped behaviour and not a re-aiming of it. Every other facing below
            // is where it was wrong.
            Assert.IsTrue(BedPillow.TryHeadingDegrees(PillowSide.MinusY, 0f, out float heading));
            AssertBearing(180f, heading, "the kit bed in an unturned room");
        }

        [Test]
        public void TurningTheROOMTurnsTheSleeperWithIt_AtAllEightFacings()
        {
            // ⭐ THE DEFECT, stated as arithmetic. A room drawn at facing f is yawed −45°·f on the ground,
            // so a bed inside it is too — and the sleeper has to follow. A fixed heading agrees with this
            // at facing 0 and is out by 45° per facing everywhere else, which at four facings has the
            // fisher lying across the bed and at facing 4 has her head on the footboard.
            for (int facing = 0; facing < 8; facing++)
            {
                float yaw = BedPillow.YawDegreesFor(facing, 8);
                Assert.IsTrue(BedPillow.TryHeadingDegrees(PillowSide.MinusY, yaw, out float heading));

                AssertBearing(180f + 45f * facing, heading,
                              $"a -y pillow in a room at facing {facing}");
            }
        }

        [Test]
        public void AnUndeclaredBedRefusesAHeadingRatherThanOfferingOne()
        {
            // The refusal IS the feature: the caller keeps its own fallback, and no plausible-looking
            // wrong number ever enters the pose.
            Assert.IsFalse(BedPillow.TryHeadingDegrees(PillowSide.Undeclared, 123f, out float heading));
            Assert.AreEqual(0f, heading, 1e-6f);
        }

        // =============================================================================
        //  where the head lands
        // =============================================================================

        [Test]
        public void TheReachComesOffTheBakesDepthAndTheRigsInset()
        {
            // The kit bed: 2.05 m deep, pillow centre 0.32 m in from the head end.
            Assert.AreEqual(1.025f - 0.32f, BedPillow.PillowReachMetres(2.05f, 0.32f), 1e-5f);
        }

        [Test]
        public void ANonsenseDepthPutsTheHeadInTheMIDDLE_NotOffTheFoot()
        {
            // An inset deeper than the bed is half itself would otherwise reach backwards, i.e. put the
            // head at the FOOT — the very failure this file exists to stop, arrived at by arithmetic.
            Assert.AreEqual(0f, BedPillow.PillowReachMetres(0.4f, 0.9f), 1e-6f);
            Assert.AreEqual(0f, BedPillow.PillowReachMetres(-1f, 0.1f), 1e-6f);
        }

        [Test]
        public void TheHeadIsOnThePillowSide_AtEveryFacing()
        {
            // ⭐ The acceptance criterion, in its purest form: whatever the room is turned to, the head
            // ends up on the half of the bed the pillow was declared on. Checked in the BED's own frame,
            // because that is the frame the declaration is in — un-rotating is what makes "the pillow
            // side" mean the same thing at all eight facings.
            const float reach = 0.705f;                       // the kit bed's own
            var bed = new Vector2(3f, -2f);

            foreach (PillowSide side in new[]
                     { PillowSide.MinusY, PillowSide.PlusY, PillowSide.MinusX, PillowSide.PlusX })
            {
                Vector2 want = BedPillow.Direction(side);

                for (int facing = 0; facing < 8; facing++)
                {
                    float yaw = BedPillow.YawDegreesFor(facing, 8);
                    Vector2 head = BedPillow.HeadWorld(bed, side, yaw, reach,
                                                       IsoGround.GroundDepthScale);

                    // World → the bed's own frame: un-squash the depth axis, then un-rotate the yaw.
                    Vector2 ground = IsoGround.ToGround(head - bed);
                    float rad = -yaw * Mathf.Deg2Rad;
                    float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
                    var inBed = new Vector2(ground.x * c - ground.y * s, ground.x * s + ground.y * c);

                    Assert.AreEqual(reach, inBed.magnitude, 1e-3f,
                                    $"{side} at facing {facing}: the head must travel the pillow's own " +
                                    "reach, no more and no less");
                    Assert.Greater(Vector2.Dot(inBed.normalized, want), 0.999f,
                                   $"{side} at facing {facing}: the head landed on the {inBed} side of " +
                                   $"the bed, not the {want} side");
                }
            }
        }

        [Test]
        public void TheHeadOffsetIsSQUASHED_BecauseTheWorldsDepthAxisIs()
        {
            // One metre north draws sin(40°) up the screen. An offset that forgot it would put the head
            // half a metre past the head of the bed — outside the sprite it is supposed to be lying on.
            Vector2 offset = BedPillow.HeadOffsetWorld(PillowSide.PlusY, 0f, 1f,
                                                       IsoGround.GroundDepthScale);
            Assert.AreEqual(0f, offset.x, 1e-5f);
            Assert.AreEqual(IsoGround.GroundDepthScale, offset.y, 1e-5f);

            // Across the room there is nothing to squash — that axis was never foreshortened.
            Vector2 across = BedPillow.HeadOffsetWorld(PillowSide.PlusX, 0f, 1f,
                                                       IsoGround.GroundDepthScale);
            Assert.AreEqual(1f, across.x, 1e-5f);
            Assert.AreEqual(0f, across.y, 1e-5f);
        }

        [Test]
        public void AnUndeclaredBedLeavesTheHeadInTheMiddleOfTheMattress()
        {
            // Visibly not on a pillow, which is the honest picture of "nobody said" — and never an
            // exception, because a missing declaration must not cost the player their save.
            var bed = new Vector2(4f, 5f);
            AssertDirection(Vector2.zero,
                            BedPillow.HeadOffsetWorld(PillowSide.Undeclared, 0f, 0.7f, 0.643f),
                            "an undeclared side moves the head nowhere");
            AssertDirection(bed, PillowAnchor.None.HeadWorld(bed), "and leaves it on the bed's own point");
        }

        // =============================================================================
        //  the anchor: the side and the frame travelling together
        // =============================================================================

        [Test]
        public void TheAnchorAnswersTheSameAsTheLooseMaths()
        {
            // The struct is a courier, not a second implementation.
            float yaw = BedPillow.YawDegreesFor(3, 8);
            var anchor = new PillowAnchor(PillowSide.MinusY, yaw, 0.705f, IsoGround.GroundDepthScale);

            Assert.IsTrue(anchor.TryHeadingDegrees(out float viaAnchor));
            Assert.IsTrue(BedPillow.TryHeadingDegrees(PillowSide.MinusY, yaw, out float viaMaths));
            Assert.AreEqual(viaMaths, viaAnchor, 1e-5f);

            AssertDirection(BedPillow.HeadWorld(Vector2.one, PillowSide.MinusY, yaw, 0.705f,
                                                IsoGround.GroundDepthScale),
                            anchor.HeadWorld(Vector2.one), "the anchor's head position");
        }

        [Test]
        public void AnAnchorWithNoSquashUsesTheSharedBakeCamerasOwn()
        {
            // A caller with no footprint in hand (a bed standing on nothing, a test rig) must still get a
            // head on the pillow rather than one flattened onto the bed's east-west line.
            var anchor = new PillowAnchor(PillowSide.PlusY, 0f, 1f, 0f);
            Assert.AreEqual(IsoGround.GroundDepthScale, anchor.EffectiveDepthScale, 1e-6f);
            Assert.AreEqual(IsoGround.GroundDepthScale, anchor.HeadOffsetWorld.y, 1e-5f);
        }

        [Test]
        public void AnAnchorSaysWhatItIs_ForALogLine()
        {
            Assert.AreEqual("(no pillow declared)", PillowAnchor.None.ToString());
            StringAssert.Contains("MinusY",
                new PillowAnchor(PillowSide.MinusY, 0f, 0.7f, 0.643f).ToString());
        }
    }
}
