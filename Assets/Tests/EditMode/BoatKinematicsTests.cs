using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The Core heading seam's pure math (ADR 0007): the compass-bearing convention, relative-bearing
    /// wrap, and the FromBow snapshot. All engine-light + deterministic, so it tests without the
    /// physics step — and one cross-check pins the bearing convention to the wind widget so a compass
    /// and the wind arrow can never silently disagree on which way is North.
    /// </summary>
    public class BoatKinematicsTests
    {
        // ---- BearingDegrees: 0 = N (+Y), 90 = E (+X), clockwise -------------------------------

        [Test]
        public void Bearing_CardinalDirections_MatchCompass()
        {
            Assert.AreEqual(0f,   BoatKinematics.BearingDegrees(Vector2.up),    1e-3f, "North is +Y → 0°");
            Assert.AreEqual(90f,  BoatKinematics.BearingDegrees(Vector2.right), 1e-3f, "East is +X → 90°");
            Assert.AreEqual(180f, BoatKinematics.BearingDegrees(Vector2.down),  1e-3f, "South is -Y → 180°");
            Assert.AreEqual(270f, BoatKinematics.BearingDegrees(Vector2.left),  1e-3f, "West is -X → 270°");
        }

        [Test]
        public void Bearing_Diagonal_IsClockwise()
        {
            Assert.AreEqual(45f, BoatKinematics.BearingDegrees(new Vector2(1f, 1f)), 1e-3f, "NE → 45°");
            // Magnitude must not matter — only direction.
            Assert.AreEqual(45f, BoatKinematics.BearingDegrees(new Vector2(8f, 8f)), 1e-3f, "scale-invariant");
        }

        [Test]
        public void Bearing_AlwaysInZeroTo360()
        {
            for (int deg = 0; deg < 360; deg += 7)
            {
                // Build a unit vector at this compass bearing and round-trip it.
                float rad = deg * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)); // (x=sin, y=cos) = the bearing convention
                float b = BoatKinematics.BearingDegrees(dir);
                Assert.That(b, Is.InRange(0f, 360f), $"bearing stays in [0,360) for {deg}°");
                Assert.AreEqual(deg % 360, b, 1e-2f, $"round-trips the {deg}° bearing");
            }
        }

        [Test]
        public void Bearing_ZeroVector_FallsBackToNorth_NeverNaN()
        {
            Assert.AreEqual(0f, BoatKinematics.BearingDegrees(Vector2.zero), 0f, "a zero vector has no direction → 0 fallback");
            Assert.IsFalse(float.IsNaN(BoatKinematics.BearingDegrees(Vector2.zero)), "never NaN");
        }

        [Test]
        public void Bearing_MatchesWindReadoutCardinalConvention()
        {
            // The compass (Core) and the wind widget (UI) must share one definition of North/East.
            // For a spread of directions, the Core bearing floored into 16 points equals WindReadout's
            // independent cardinal — proof they agree (the seam's whole point: one dial for both).
            foreach (var dir in new[]
            {
                Vector2.up, Vector2.right, Vector2.down, Vector2.left,
                new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -2f), new Vector2(-3f, -1f),
            })
            {
                float bearing = BoatKinematics.BearingDegrees(dir);
                int idx = Mathf.RoundToInt(bearing / 22.5f) % 16;
                string[] compass16 =
                {
                    "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
                    "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"
                };
                Assert.AreEqual(compass16[idx], WindReadout.Cardinal(dir),
                    $"Core bearing and WindReadout must agree for {dir}");
            }
        }

        // ---- RelativeBearingDegrees: signed shortest angle, wraps cleanly ---------------------

        [Test]
        public void RelativeBearing_SignedShortestAngle()
        {
            // Target clockwise of the reference → positive (to starboard).
            Assert.AreEqual(20f,  BoatKinematics.RelativeBearingDegrees(350f, 10f), 1e-3f, "across North, CW → +20");
            Assert.AreEqual(-20f, BoatKinematics.RelativeBearingDegrees(10f, 350f), 1e-3f, "across North, CCW → -20");
            Assert.AreEqual(90f,  BoatKinematics.RelativeBearingDegrees(0f, 90f),  1e-3f, "due East off a North heading → +90");
            Assert.AreEqual(0f,   BoatKinematics.RelativeBearingDegrees(123f, 123f), 1e-3f, "same bearing → 0");
        }

        [Test]
        public void RelativeBearing_StaysWithinHalfTurn()
        {
            for (int a = 0; a < 360; a += 13)
                for (int b = 0; b < 360; b += 17)
                {
                    float rel = BoatKinematics.RelativeBearingDegrees(a, b);
                    Assert.That(rel, Is.InRange(-180f, 180f), $"relative bearing wraps into [-180,180) for ({a},{b})");
                }
        }

        // ---- FromBow / struct snapshot --------------------------------------------------------

        [Test]
        public void FromBow_FillsHeadingVelocityAndSog()
        {
            var k = BoatKinematics.FromBow(Vector2.right, new Vector2(3f, 4f));
            Assert.IsTrue(k.HasBoat, "FromBow always describes a real boat");
            Assert.AreEqual(90f, k.HeadingDegrees, 1e-3f, "bow +X → heading 90 (East)");
            Assert.AreEqual(new Vector2(3f, 4f), k.Velocity);
            Assert.AreEqual(5f, k.SpeedOverGround, 1e-4f, "SOG is |velocity| (3,4 → 5)");
        }

        [Test]
        public void FromBow_CourseOverGround_DiffersFromHeading_WhenSet()
        {
            // Bow points North but the hull is swept East by current/wind — the crabbing read.
            var k = BoatKinematics.FromBow(Vector2.up, new Vector2(2f, 2f));
            Assert.AreEqual(0f,  k.HeadingDegrees, 1e-3f, "pointing North");
            Assert.AreEqual(45f, k.CourseOverGroundDegrees, 1e-3f, "but travelling NE");
            Assert.AreEqual(45f, BoatKinematics.RelativeBearingDegrees(k.HeadingDegrees, k.CourseOverGroundDegrees), 1e-3f,
                "the set is 45° to starboard of the heading");
        }

        [Test]
        public void None_IsTheNoBoatSnapshot()
        {
            Assert.IsFalse(BoatKinematics.None.HasBoat, "None means there is no active boat");
            Assert.AreEqual(0f, BoatKinematics.None.SpeedOverGround, 0f);
            Assert.AreEqual(default(BoatKinematics).HasBoat, BoatKinematics.None.HasBoat, "None == default");
        }
    }
}
