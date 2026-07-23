using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Rod Fishing v2 Wave 3 — the surface-fight choreography maths (<see cref="RodFightMotionMath"/>).
    /// Pins: closed-form determinism (same (seed, t) → same point, no state to drift), the on-screen
    /// CHARACTER of each authored movement pattern (darter flips, bulldog stays close, circler sweeps,
    /// thrasher oscillates), the roam bound, and the steer-alignment read the fight maths consumes
    /// (sign, clamp, dead-zone, NaN safety).
    /// </summary>
    public class RodFightMotionMathTests
    {
        private const int Seed = 424242;
        private const float Radius = 2.5f;

        [Test]
        public void SameSeedAndTime_SamePoint_EveryPattern()
        {
            foreach (RodFightMovement p in System.Enum.GetValues(typeof(RodFightMovement)))
            {
                for (float t = 0f; t < 10f; t += 0.37f)
                {
                    Assert.AreEqual(RodFightMotionMath.Offset(p, Seed, t, Radius),
                                    RodFightMotionMath.Offset(p, Seed, t, Radius),
                                    $"{p}: Offset must be a pure function of (seed, t)");
                    Assert.AreEqual(RodFightMotionMath.DartDir(p, Seed, t),
                                    RodFightMotionMath.DartDir(p, Seed, t),
                                    $"{p}: DartDir must be a pure function of (seed, t)");
                }
            }
        }

        [Test]
        public void Offset_StaysInsideTheRoamRadius_EveryPattern()
        {
            foreach (RodFightMovement p in System.Enum.GetValues(typeof(RodFightMovement)))
                for (float t = 0f; t < 30f; t += 0.11f)
                    Assert.LessOrEqual(RodFightMotionMath.Offset(p, Seed, t, Radius).magnitude,
                                       Radius + 1e-4f, $"{p}: she roams a bounded disc");
        }

        [Test]
        public void DartDir_IsAlwaysUnit_EveryPattern()
        {
            foreach (RodFightMovement p in System.Enum.GetValues(typeof(RodFightMovement)))
                for (float t = 0f; t < 15f; t += 0.13f)
                    Assert.AreEqual(1f, RodFightMotionMath.DartDir(p, Seed, t).magnitude, 1e-3f,
                                    $"{p}: the dart is a unit direction (the steer read never vanishes)");
        }

        [Test]
        public void SurfaceFight_OpensAtTheAnchor_EveryPattern()
        {
            // The surface ramp: every pattern swims OUT from the line's entry point — nobody pops onto
            // their curve on the crossing frame.
            foreach (RodFightMovement p in System.Enum.GetValues(typeof(RodFightMovement)))
            {
                Assert.AreEqual(Vector2.zero, RodFightMotionMath.Offset(p, Seed, 0f, Radius),
                                $"{p}: t = 0 reads as the anchor itself");
                float early = RodFightMotionMath.Offset(p, Seed, 0.05f, Radius).magnitude;
                Assert.Less(early, Radius * 0.05f / RodFightMotionMath.SurfaceRampSeconds + 1e-4f,
                            $"{p}: moments after the crossing she is still beside the entry point");
            }
        }

        [Test]
        public void Darter_SnapsDirection_BetweenSegments()
        {
            // The dart direction is piecewise-constant per segment and changes across segment borders —
            // the "short, sharp direction flips" read. Sample the dart in consecutive segments.
            int flips = 0, segments = 10;
            for (int i = 0; i < segments; i++)
            {
                float tA = (i + 0.5f) * RodFightMotionMath.DarterSegmentSeconds;
                float tB = (i + 1.5f) * RodFightMotionMath.DarterSegmentSeconds;
                Vector2 a = RodFightMotionMath.DartDir(RodFightMovement.Darter, Seed, tA);
                Vector2 b = RodFightMotionMath.DartDir(RodFightMovement.Darter, Seed, tB);
                if (Vector2.Dot(a, b) < 0.95f) flips++;
            }
            Assert.GreaterOrEqual(flips, segments / 2,
                "a darter should change direction at most segment borders (hash-picked points)");
        }

        [Test]
        public void Bulldog_StaysCloser_AndTurnsSlower_ThanTheDarter()
        {
            // He digs in: a shrunken roam disc…
            float bulldogMax = 0f;
            for (float t = 0f; t < 30f; t += 0.05f)
                bulldogMax = Mathf.Max(bulldogMax,
                    RodFightMotionMath.Offset(RodFightMovement.Bulldog, Seed, t, Radius).magnitude);
            Assert.LessOrEqual(bulldogMax, Radius * RodFightMotionMath.BulldogRadiusFraction + 1e-3f,
                "a bulldog never travels past his fraction of the roam disc");

            // …and dogged, longer segments (fewer direction changes over the same stretch).
            Assert.Greater(RodFightMotionMath.BulldogSegmentSeconds, RodFightMotionMath.DarterSegmentSeconds,
                "bulldog segments are the long, reluctant ones (the constant relationship the read rests on)");
        }

        [Test]
        public void Circler_SweepsAContinuousArc()
        {
            // Consecutive dart directions rotate steadily — no snaps: the tangent turns with the lap.
            for (float t = 0.5f; t < 6f; t += 0.25f)
            {
                Vector2 a = RodFightMotionMath.DartDir(RodFightMovement.Circler, Seed, t);
                Vector2 b = RodFightMotionMath.DartDir(RodFightMovement.Circler, Seed, t + 0.25f);
                Assert.Greater(Vector2.Dot(a, b), 0.5f,
                    "a circler's dart rotates smoothly — nearby times point nearby directions");
            }
            // And she actually comes around: a quarter-lap apart the tangents have visibly turned.
            Vector2 t0 = RodFightMotionMath.DartDir(RodFightMovement.Circler, Seed, 1f);
            Vector2 t1 = RodFightMotionMath.DartDir(RodFightMovement.Circler, Seed,
                                                    1f + RodFightMotionMath.CirclerRevSeconds * 0.5f);
            Assert.Less(Vector2.Dot(t0, t1), 0f, "half a lap turns the dart right around");
        }

        [Test]
        public void Thrasher_OscillatesSideToSide()
        {
            // Pure screen-X thrash: y stays 0, x sweeps both signs within one period, and the dart is
            // strictly left/right, flipping twice a swing.
            bool sawLeft = false, sawRight = false;
            for (float t = 0f; t < RodFightMotionMath.ThrasherPeriodSeconds; t += 0.02f)
            {
                Vector2 off = RodFightMotionMath.Offset(RodFightMovement.Thrasher, Seed, t + 0.01f, Radius);
                Assert.AreEqual(0f, off.y, 1e-5f, "the thrash is side-to-side");
                Vector2 dart = RodFightMotionMath.DartDir(RodFightMovement.Thrasher, Seed, t + 0.01f);
                Assert.AreEqual(0f, dart.y, 1e-5f);
                if (dart.x < 0f) sawLeft = true; else sawRight = true;
            }
            Assert.IsTrue(sawLeft && sawRight, "one full swing darts both ways");
        }

        // ---- the steer alignment read -------------------------------------------------------

        [Test]
        public void SteerAlignment_CounterIsMinusOne_WithIsPlusOne()
        {
            Assert.AreEqual(-1f, RodFightMotionMath.SteerAlignment(Vector2.left, Vector2.right, 0.1f), 1e-4f,
                "steering OPPOSITE her dart is the full counter (−1)");
            Assert.AreEqual(+1f, RodFightMotionMath.SteerAlignment(Vector2.right, Vector2.right, 0.1f), 1e-4f,
                "steering WITH her is the full mistake (+1)");
            Assert.AreEqual(0f, RodFightMotionMath.SteerAlignment(Vector2.up, Vector2.right, 0.1f), 1e-4f,
                "perpendicular steer is neutral");
        }

        [Test]
        public void SteerAlignment_DeadzoneAndDegenerateInputs_ReadNeutral()
        {
            Assert.AreEqual(0f, RodFightMotionMath.SteerAlignment(new Vector2(0.05f, 0f), Vector2.right, 0.3f),
                "a pointer resting on the character (inside the dead-zone) is neutral, never a penalty");
            Assert.AreEqual(0f, RodFightMotionMath.SteerAlignment(Vector2.right, Vector2.zero, 0.1f),
                "a degenerate dart reads neutral");
            Assert.AreEqual(0f, RodFightMotionMath.SteerAlignment(
                new Vector2(float.NaN, 1f), Vector2.right, 0.1f), "NaN-safe");
            float a = RodFightMotionMath.SteerAlignment(new Vector2(100f, 0f), Vector2.right, 0.1f);
            Assert.That(a, Is.InRange(-1f, 1f), "always clamped to the −1..+1 the fight maths expects");
        }

        [Test]
        public void BadInputs_AreSafe()
        {
            foreach (RodFightMovement p in System.Enum.GetValues(typeof(RodFightMovement)))
            {
                Assert.AreEqual(Vector2.zero, RodFightMotionMath.Offset(p, Seed, float.NaN, Radius));
                Assert.AreEqual(Vector2.zero, RodFightMotionMath.Offset(p, Seed, 5f, -1f),
                    $"{p}: a non-positive radius pins her to the anchor");
                Assert.AreEqual(1f, RodFightMotionMath.DartDir(p, Seed, float.NaN).magnitude, 1e-3f,
                    $"{p}: even a NaN time yields a usable unit dart");
            }
        }
    }
}
