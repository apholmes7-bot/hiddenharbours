using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The boat-rotation prototype's pure facing math (<see cref="DirectionalBoatSprite"/>). These guard the
    /// owner's exact worry — that the snap must land on the RIGHT facing with no off-by-one at the bucket
    /// boundaries — and that the math generalises to any facing count (4 now, 8/16 when art exists). All
    /// engine-light, deterministic, no physics step.
    ///
    /// Convention under test: heading is a compass bearing (0 = North/+Y, 90 = East/+X, clockwise); the
    /// facing array is laid out CLOCKWISE from the zero heading; element 0 of a 4-way boat is North.
    /// </summary>
    public class DirectionalBoatSpriteTests
    {
        private const int N4 = 4;   // N, E, S, W
        private const int N8 = 8;   // N, NE, E, SE, S, SW, W, NW
        private const float Zero = 0f;

        // ---- HeadingDegreesFromBow: the bow vector -> compass bearing -------------------------

        [Test]
        public void HeadingFromBow_CardinalDirections_MatchCompass()
        {
            Assert.AreEqual(0f,   DirectionalBoatSprite.HeadingDegreesFromBow(Vector2.up),    1e-3f, "bow +Y = North = 0");
            Assert.AreEqual(90f,  DirectionalBoatSprite.HeadingDegreesFromBow(Vector2.right), 1e-3f, "bow +X = East = 90");
            Assert.AreEqual(180f, DirectionalBoatSprite.HeadingDegreesFromBow(Vector2.down),  1e-3f, "bow -Y = South = 180");
            Assert.AreEqual(270f, DirectionalBoatSprite.HeadingDegreesFromBow(Vector2.left),  1e-3f, "bow -X = West = 270");
        }

        [Test]
        public void HeadingFromBow_ZeroVector_FallsBackToNorth_NeverNaN()
        {
            float h = DirectionalBoatSprite.HeadingDegreesFromBow(Vector2.zero);
            Assert.AreEqual(0f, h, 0f, "no direction -> North fallback");
            Assert.IsFalse(float.IsNaN(h), "never NaN");
        }

        [Test]
        public void HeadingFromBow_AlwaysInZeroTo360()
        {
            for (int deg = 0; deg < 360; deg += 11)
            {
                float rad = deg * Mathf.Deg2Rad;
                var bow = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)); // bearing convention (x=sin, y=cos)
                float h = DirectionalBoatSprite.HeadingDegreesFromBow(bow);
                Assert.That(h, Is.InRange(0f, 360f), $"bearing in [0,360) for {deg}");
                Assert.AreEqual(deg % 360, h, 1e-2f, $"round-trips {deg}");
            }
        }

        // ---- HeadingToFacingIndex (N=4): the cardinal facings ---------------------------------

        [Test]
        public void Facing4_CardinalHeadings_HitTheirOwnFacing()
        {
            // Array order is [N, E, S, W] (CW from North).
            Assert.AreEqual(0, DirectionalBoatSprite.HeadingToFacingIndex(0f,   N4, Zero), "0 (N) -> 0");
            Assert.AreEqual(1, DirectionalBoatSprite.HeadingToFacingIndex(90f,  N4, Zero), "90 (E) -> 1");
            Assert.AreEqual(2, DirectionalBoatSprite.HeadingToFacingIndex(180f, N4, Zero), "180 (S) -> 2");
            Assert.AreEqual(3, DirectionalBoatSprite.HeadingToFacingIndex(270f, N4, Zero), "270 (W) -> 3");
            Assert.AreEqual(0, DirectionalBoatSprite.HeadingToFacingIndex(360f, N4, Zero), "360 wraps to N (0)");
        }

        [Test]
        public void Facing4_NearCardinal_SnapsToNearest()
        {
            // Just inside each bucket either side of a cardinal -> still that cardinal's facing.
            Assert.AreEqual(0, DirectionalBoatSprite.HeadingToFacingIndex(44f,  N4, Zero), "44 -> N");
            Assert.AreEqual(0, DirectionalBoatSprite.HeadingToFacingIndex(316f, N4, Zero), "316 -> N");
            Assert.AreEqual(1, DirectionalBoatSprite.HeadingToFacingIndex(46f,  N4, Zero), "46 -> E");
            Assert.AreEqual(1, DirectionalBoatSprite.HeadingToFacingIndex(134f, N4, Zero), "134 -> E");
            Assert.AreEqual(2, DirectionalBoatSprite.HeadingToFacingIndex(179f, N4, Zero), "179 -> S");
            Assert.AreEqual(3, DirectionalBoatSprite.HeadingToFacingIndex(269f, N4, Zero), "269 -> W");
        }

        [Test]
        public void Facing4_ExactBucketBoundaries_ResolveDeterministically_NoTie()
        {
            // The owner's worry: a heading dead on a bucket edge must NOT be an ambiguous tie. The rule is
            // half-up = snap to the NEXT facing clockwise. 45 -> E(1), 135 -> S(2), 225 -> W(3), 315 -> N(0).
            Assert.AreEqual(1, DirectionalBoatSprite.HeadingToFacingIndex(45f,  N4, Zero), "45 edge -> E (half-up)");
            Assert.AreEqual(2, DirectionalBoatSprite.HeadingToFacingIndex(135f, N4, Zero), "135 edge -> S (half-up)");
            Assert.AreEqual(3, DirectionalBoatSprite.HeadingToFacingIndex(225f, N4, Zero), "225 edge -> W (half-up)");
            Assert.AreEqual(0, DirectionalBoatSprite.HeadingToFacingIndex(315f, N4, Zero), "315 edge -> N (half-up, wraps)");
        }

        // ---- HeadingToFacingIndex (N=8): generalises to more facings ---------------------------

        [Test]
        public void Facing8_EightCardinalAndIntercardinalHeadings_MapOneToOne()
        {
            // Array order [N, NE, E, SE, S, SW, W, NW] (CW from North), step 45.
            for (int i = 0; i < N8; i++)
            {
                float heading = i * 45f;
                Assert.AreEqual(i, DirectionalBoatSprite.HeadingToFacingIndex(heading, N8, Zero),
                    $"{heading} -> facing {i}");
            }
        }

        [Test]
        public void Facing8_ExactBucketBoundaries_SnapNextClockwise()
        {
            // Step 45, so the half-edges are at 22.5, 67.5, ... -> half-up snaps to the next facing CW.
            Assert.AreEqual(1, DirectionalBoatSprite.HeadingToFacingIndex(22.5f,  N8, Zero), "22.5 edge -> NE (1)");
            Assert.AreEqual(2, DirectionalBoatSprite.HeadingToFacingIndex(67.5f,  N8, Zero), "67.5 edge -> E (2)");
            Assert.AreEqual(0, DirectionalBoatSprite.HeadingToFacingIndex(337.5f, N8, Zero), "337.5 edge -> N (0, wraps)");
        }

        // ---- Robustness: wrap, range, custom zero heading, determinism -------------------------

        [Test]
        public void Facing_ResultAlwaysInRange_AcrossFullCircle_AndNegativeHeadings()
        {
            foreach (int count in new[] { N4, N8, 16 })
                for (float deg = -720f; deg <= 720f; deg += 3.5f)
                {
                    int idx = DirectionalBoatSprite.HeadingToFacingIndex(deg, count, Zero);
                    Assert.That(idx, Is.InRange(0, count - 1), $"index in [0,{count}) for heading {deg}, count {count}");
                }
        }

        [Test]
        public void Facing_NonZeroZeroHeading_OffsetsTheLayout()
        {
            // If element 0 is drawn for East (zeroHeading 90), then an East heading must pick element 0.
            Assert.AreEqual(0, DirectionalBoatSprite.HeadingToFacingIndex(90f,  N4, 90f), "E heading, zero=E -> 0");
            Assert.AreEqual(1, DirectionalBoatSprite.HeadingToFacingIndex(180f, N4, 90f), "S heading, zero=E -> 1");
            Assert.AreEqual(3, DirectionalBoatSprite.HeadingToFacingIndex(0f,   N4, 90f), "N heading, zero=E -> 3 (one CCW)");
        }

        [Test]
        public void Facing_IsDeterministic_SameInputsSameIndex()
        {
            for (int rep = 0; rep < 3; rep++)
                for (float deg = 0f; deg < 360f; deg += 13f)
                    Assert.AreEqual(
                        DirectionalBoatSprite.HeadingToFacingIndex(deg, N4, Zero),
                        DirectionalBoatSprite.HeadingToFacingIndex(deg, N4, Zero),
                        $"deterministic at {deg}");
        }

        [Test]
        public void Facing_DegenerateCount_DoesNotThrow_ReturnsZero()
        {
            Assert.AreEqual(0, DirectionalBoatSprite.HeadingToFacingIndex(123f, 0, Zero), "count 0 -> 0, no divide-by-zero");
            Assert.AreEqual(0, DirectionalBoatSprite.HeadingToFacingIndex(123f, -3, Zero), "negative count -> 0");
        }
    }
}
