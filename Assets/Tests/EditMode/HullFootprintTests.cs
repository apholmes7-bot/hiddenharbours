using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>The hull as a SHAPE, not as a dot with a radius.</b> These hold <see cref="HullFootprint"/>
    /// to the one thing the circle model it replaces could not do: know which way a boat is lying.
    ///
    /// <para>The case that matters most is <see cref="AHullLyingAthwart_ReachesIntoWaterHerCentreNeverEnters"/>
    /// — a boat moored across a fairway. Her centre stands well to one side and her stern is in the
    /// channel, and a circle of her half-beam reports the first fact while hiding the second. That is
    /// not a hypothetical: it is the St Peters starter dory, and it is why this type exists.</para>
    /// </summary>
    public class HullFootprintTests
    {
        // The two hulls this repo actually measures things against.
        private const float DoryLength = 4.5f, DoryHalfBeam = 0.85f;
        private const float CapeLength = 12.9f, CapeHalfBeam = 2.4f;

        // =============================================================================================
        //  1. the outline is where her ENDS are
        // =============================================================================================

        [Test]
        public void HerOutlineReachesHalfHerLength_FromHerCentre_NotHalfHerBeam()
        {
            // Bow north — the identity rotation every builder-placed hull carries.
            var her = HullFootprint.FromHeading(new Vector2(215f, 3.15f), 0f, DoryLength, DoryHalfBeam);

            AssertV2(new Vector2(215f, 3.15f + DoryLength * 0.5f), her.BowPoint,
                     "her bow is half a length ahead of her centre");
            AssertV2(new Vector2(215f, 3.15f - DoryLength * 0.5f), her.SternPoint,
                     "her stern is half a length astern of her centre");

            // The whole point: the circle model would have put her nearest edge 0.85 m away.
            Assert.AreEqual(0f, her.DistanceTo(new Vector2(215f, 1.5f)), 1e-4f,
                "a point 1.65 m south of her centre is INSIDE a 4.5 m boat lying north — the model " +
                "that reported 0.80 m of clear water there is the one this type replaces");
        }

        [Test]
        public void ACompassHeadingPointsHerBowTheWayTheRestOfTheRepoMeans()
        {
            // 0 = north, 90 = east, clockwise — ArrivalPilot.CompassOf / DeckAreaMath.
            AssertV2(new Vector2(0f, 1f),
                     HullFootprint.FromHeading(Vector2.zero, 0f, 10f, 1f).BowDirection, "0° is NORTH");
            AssertV2(new Vector2(1f, 0f),
                     HullFootprint.FromHeading(Vector2.zero, 90f, 10f, 1f).BowDirection, "90° is EAST");
            AssertV2(new Vector2(0f, -1f),
                     HullFootprint.FromHeading(Vector2.zero, 180f, 10f, 1f).BowDirection, "180° is SOUTH");

            // Starboard is the bow turned 90° clockwise: lying north, starboard is east.
            AssertV2(new Vector2(1f, 0f),
                     HullFootprint.FromHeading(Vector2.zero, 0f, 10f, 1f).StarboardDirection,
                     "lying north, her starboard side is to the EAST");
        }

        [Test]
        public void DistanceToAPoint_IsZeroInside_AndTheBoxDistanceOutside()
        {
            var her = HullFootprint.FromHeading(Vector2.zero, 0f, 10f, 2f);   // 10 × 4, lying north

            Assert.IsTrue(her.Contains(new Vector2(1.9f, 4.9f)), "just inside the starboard bow corner");
            Assert.AreEqual(0f, her.DistanceTo(new Vector2(1.9f, 4.9f)), 1e-4f);

            Assert.AreEqual(1f, her.DistanceTo(new Vector2(3f, 0f)), 1e-4f, "abeam: 3 − 2");
            Assert.AreEqual(2f, her.DistanceTo(new Vector2(0f, 7f)), 1e-4f, "ahead: 7 − 5");
            // Off a CORNER it is the diagonal, which is the part a per-axis test gets wrong.
            Assert.AreEqual(5f, her.DistanceTo(new Vector2(2f + 3f, 5f + 4f)), 1e-4f,
                            "off the bow corner it is √(3² + 4²), not 3 and not 4");

            AssertV2(new Vector2(2f, 5f), her.ClosestPoint(new Vector2(5f, 9f)),
                     "the nearest point on her outline is that corner");
        }

        // =============================================================================================
        //  2. ⭐ THE CASE THE CIRCLE MODEL COULD NOT SEE
        // =============================================================================================

        /// <summary>
        /// ⭐ A hull moored ACROSS a fairway, against a hull running down it. Both models agree about
        /// where her centre is; only this one knows where her stern is.
        /// </summary>
        [Test]
        public void AHullLyingAthwart_ReachesIntoWaterHerCentreNeverEnters()
        {
            // A dory moored bow-north 4.0 m off a fairway's centre-line, against a cape running west
            // down it. 4.0 m is chosen to be the interesting distance: far enough that a CIRCLE of her
            // half-beam stands clear of the cape's, near enough that her STERN is in the channel. Below
            // ~3.25 m the two models agree (both call it a collision) and the difference is invisible.
            var dory = HullFootprint.FromHeading(new Vector2(215f, 4f), 0f, DoryLength, DoryHalfBeam);
            var cape = HullFootprint.FromHeading(new Vector2(215f, 0f), 270f, CapeLength, CapeHalfBeam);

            // The circle model's answer, reproduced here so the difference is stated rather than claimed:
            float asCircles = Vector2.Distance(dory.Center, cape.Center) - CapeHalfBeam - DoryHalfBeam;
            Assert.Greater(asCircles, 0f,
                "the model being replaced reports clear water here — that is the bug, held in place " +
                "so this test fails loudly if someone 'fixes' it back");

            float asOutlines = dory.SignedGapTo(cape);
            Assert.Less(asOutlines, 0f,
                $"her stern reaches y = {dory.SternPoint.y:F2} and the cape's port side is at " +
                $"y = {cape.Center.y + CapeHalfBeam:F2} — these two hulls OVERLAP, and the circle " +
                $"model called it {asCircles:F2} m of clear water");

            // …and the overlap is exactly the two edges' interpenetration, not a vague negative.
            Assert.AreEqual(-(CapeHalfBeam - dory.SternPoint.y), asOutlines, 1e-3f,
                            "the depth is the cape's port side minus the dory's stern");

            Assert.AreEqual(0f, dory.DistanceTo(cape), 1e-4f,
                            "the unsigned form floors an overlap at zero");
        }

        // =============================================================================================
        //  3. the ordinary geometry, so the interesting case is not the only one held
        // =============================================================================================

        [Test]
        public void TwoHullsLyingParallel_AreSeparatedByTheirBeams()
        {
            var a = HullFootprint.FromHeading(new Vector2(0f, 0f), 0f, CapeLength, CapeHalfBeam);
            var b = HullFootprint.FromHeading(new Vector2(9f, 0f), 0f, CapeLength, CapeHalfBeam);
            Assert.AreEqual(9f - 2f * CapeHalfBeam, a.SignedGapTo(b), 1e-4f,
                            "side by side, the gap is the separation less both half-beams — the one " +
                            "arrangement the circle model happened to get right");
        }

        [Test]
        public void TwoHullsInLineAhead_AreSeparatedByTheirLengths()
        {
            var a = HullFootprint.FromHeading(new Vector2(0f, 0f), 0f, 10f, 2f);
            var b = HullFootprint.FromHeading(new Vector2(0f, 14f), 0f, 10f, 2f);
            Assert.AreEqual(4f, a.SignedGapTo(b), 1e-4f, "14 − 5 − 5");
        }

        /// <summary>
        /// ⚠ The adversarial one: two long thin hulls crossing in a plus, where <b>no corner of either
        /// lies inside the other</b>. A "is any vertex contained?" overlap test passes this happily and
        /// reports clear water through the middle of a collision. It is here because that is the cheap
        /// wrong implementation, and this is the shape that catches it.
        /// </summary>
        [Test]
        public void TwoHullsCrossingInAPlus_Overlap_ThoughNoCornerIsInsideTheOther()
        {
            var alongY = HullFootprint.FromHeading(Vector2.zero, 0f, 20f, 1f);    // tall and thin
            var alongX = HullFootprint.FromHeading(Vector2.zero, 90f, 20f, 1f);   // wide and thin

            for (int i = 0; i < 4; i++)
            {
                Assert.IsFalse(alongX.Contains(alongY.Corner(i)), "no corner of one is inside the other");
                Assert.IsFalse(alongY.Contains(alongX.Corner(i)), "no corner of one is inside the other");
            }
            Assert.Less(alongY.SignedGapTo(alongX), 0f, "…and yet they plainly overlap");
        }

        [Test]
        public void TheGapIsSymmetric_WhicheverHullIsAsked()
        {
            var a = HullFootprint.FromHeading(new Vector2(3f, -2f), 37f, CapeLength, CapeHalfBeam);
            var b = HullFootprint.FromHeading(new Vector2(-6f, 11f), 211f, DoryLength, DoryHalfBeam);
            Assert.AreEqual(a.SignedGapTo(b), b.SignedGapTo(a), 1e-4f);
        }

        // =============================================================================================
        //  4. rigid motion — held as TWO tests on purpose
        // =============================================================================================

        /// <summary>Translating both hulls together cannot change the water between them.</summary>
        [Test]
        public void TranslatingBothHulls_LeavesTheGapAlone()
        {
            var a = HullFootprint.FromHeading(new Vector2(1f, 2f), 20f, CapeLength, CapeHalfBeam);
            var b = HullFootprint.FromHeading(new Vector2(8f, -3f), 115f, DoryLength, DoryHalfBeam);
            float before = a.SignedGapTo(b);

            var shift = new Vector2(-417.25f, 88.5f);
            var a2 = HullFootprint.FromHeading(a.Center + shift, 20f, CapeLength, CapeHalfBeam);
            var b2 = HullFootprint.FromHeading(b.Center + shift, 115f, DoryLength, DoryHalfBeam);
            Assert.AreEqual(before, a2.SignedGapTo(b2), 1e-3f);
        }

        /// <summary>
        /// ⚠ And ROTATING both about a common point cannot either — the half a translation test cannot
        /// reach. A model that quietly measured axis-aligned bounds would pass the translation above and
        /// fail here, which is the whole reason these are two tests.
        /// </summary>
        [Test]
        public void RotatingBothHullsAboutAPoint_LeavesTheGapAlone()
        {
            var pivot = new Vector2(4f, 4f);
            var a = HullFootprint.FromHeading(new Vector2(1f, 2f), 20f, CapeLength, CapeHalfBeam);
            var b = HullFootprint.FromHeading(new Vector2(8f, -3f), 115f, DoryLength, DoryHalfBeam);
            float before = a.SignedGapTo(b);

            foreach (float turn in new[] { 17f, 90f, 143.5f, 270f })
            {
                var a2 = HullFootprint.FromHeading(Spin(a.Center, pivot, turn), 20f + turn,
                                                   CapeLength, CapeHalfBeam);
                var b2 = HullFootprint.FromHeading(Spin(b.Center, pivot, turn), 115f + turn,
                                                   DoryLength, DoryHalfBeam);
                Assert.AreEqual(before, a2.SignedGapTo(b2), 1e-3f,
                                $"the same two hulls, both turned {turn}° about a point");
            }
        }

        /// <summary>Turn a point about a pivot by a CLOCKWISE compass turn, matching the heading sense.</summary>
        private static Vector2 Spin(Vector2 p, Vector2 pivot, float clockwiseDegrees)
        {
            float r = -clockwiseDegrees * Mathf.Deg2Rad;      // screen-CCW is the negative of compass
            Vector2 d = p - pivot;
            return pivot + new Vector2(d.x * Mathf.Cos(r) - d.y * Mathf.Sin(r),
                                       d.x * Mathf.Sin(r) + d.y * Mathf.Cos(r));
        }

        // =============================================================================================
        //  5. house rules
        // =============================================================================================

        [Test]
        public void ANonFiniteInput_DegradesToSomethingUsable_RatherThanPoisoningTheNumber()
        {
            var her = HullFootprint.FromBowDirection(new Vector2(float.NaN, 3f), Vector2.zero,
                                                     float.NaN, DoryHalfBeam);
            AssertV2(new Vector2(0f, 3f), her.Center, "NaN reads as 0");
            AssertV2(new Vector2(0f, 1f), her.BowDirection,
                     "a degenerate bow direction falls back to NORTH — the identity rotation");
            Assert.AreEqual(0f, her.HalfLength, 1e-4f);
            Assert.IsFalse(float.IsNaN(her.DistanceTo(new Vector2(10f, 10f))));
        }

        [Test]
        public void ANegativeOrZeroSizedHull_IsAPoint_AndNotAnError()
        {
            var her = HullFootprint.FromHeading(new Vector2(5f, 5f), 45f, -3f, -1f);
            Assert.AreEqual(0f, her.HalfLength, 1e-4f);
            Assert.AreEqual(0f, her.HalfBeam, 1e-4f);
            Assert.AreEqual(5f, her.DistanceTo(new Vector2(5f, 10f)), 1e-4f, "a point, 5 m away");
        }

        // ---- one helper, so a Vector2 comparison reports WHICH axis moved -----------------------------

        private static void AssertV2(Vector2 expected, Vector2 actual, string what = "")
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, what + " (x)");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, what + " (y)");
        }
    }
}
