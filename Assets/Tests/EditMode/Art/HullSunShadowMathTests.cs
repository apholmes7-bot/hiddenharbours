using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class HullSunShadowMathTests
    {
        [TestCase(8f)] [TestCase(12f)] [TestCase(17f)] [TestCase(19.5f)]
        public void Projection_UsesThePublishedSunAndProfile(float hour)
        {
            var day = Resources.Load<DayNightProfile>("DayNightProfile");
            var profile = Resources.Load<SpriteShadowProfile>("SpriteShadowProfile");
            float e = DayNightMath.SunElevation(hour, day.SunriseHour, day.SunsetHour);
            Vector2 d = DayNightMath.ShadowDirection(hour, day.SunriseHour, day.SunsetHour, day.ShadowSouthBias, day.ShadowNoonLift);
            float length = DayNightMath.ShadowLength(e, profile.LengthAtNoon, profile.LengthAtHorizon, profile.MaxLength);
            Vector4 p = HullSunShadowMath.Projection(d, length, 40, 0, 0, 32);
            Vector3 actual = HullSunShadowMath.Project(new Vector3(0, 0, 2), Matrix4x4.identity, p, 0);
            Vector2 expected = d * (length * 2 * Mathf.Cos(40 * Mathf.Deg2Rad));
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-5f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-5f));
            Assert.That(actual.z, Is.Zero);
        }

        [Test]
        public void Waterline_IsFixed()
        {
            Vector4 p = HullSunShadowMath.Projection(Vector2.right, 3, 40, 0, 0, 32);
            Vector3 actual = HullSunShadowMath.Project(new Vector3(2, 4, 0), Matrix4x4.identity, p, 0);
            Assert.That(actual.x, Is.EqualTo(2f));
            Assert.That(actual.y, Is.EqualTo(4 * Mathf.Sin(40 * Mathf.Deg2Rad)).Within(1e-6f));
        }

        [Test]
        public void BelowWater_DoesNotCastBackwards()
        {
            Vector4 p = HullSunShadowMath.Projection(Vector2.right, 3, 40, 0, 0, 32);
            Assert.That(HullSunShadowMath.Project(new Vector3(0, 0, -2), Matrix4x4.identity, p, 0), Is.EqualTo(Vector3.zero));
        }

        [TestCase(16, 1.3f, 5f, -3f)] [TestCase(32, 4.7f, -7f, 2f)]
        public void Pose_UsesPreViewGeometry(int ppu, float heading, float roll, float pitch)
        {
            Vector3 point = new Vector3(1, 2, 3);
            Vector3 r = Quaternion.AngleAxis(heading * 45, Vector3.forward) *
                (Quaternion.AngleAxis(pitch, Vector3.right) * (Quaternion.AngleAxis(roll, Vector3.up) * point));
            Vector2 d = new Vector2(0.6f, 0.8f);
            Vector4 projection = HullSunShadowMath.Projection(d, 2, 40, 6, 2, ppu);
            Vector3 actual = HullSunShadowMath.Project(point, HullSunShadowMath.Pose(heading, 40, roll, pitch), projection, 2f / ppu);
            float c = Mathf.Cos(40 * Mathf.Deg2Rad), s = Mathf.Sin(40 * Mathf.Deg2Rad);
            float h = Mathf.Max(0, r.z + 4f / (ppu * c));
            Assert.That(actual.x, Is.EqualTo(r.x + d.x * 2 * c * h).Within(1e-5f));
            Assert.That(actual.y, Is.EqualTo(r.y * s + 2f / ppu + d.y * 2 * c * h).Within(1e-5f));
        }

        [TestCase(6f)] [TestCase(20f)] [TestCase(20.5f)] [TestCase(22.5f)]
        public void SunAtOrBelowHorizon_IsDisabled(float hour)
        {
            var day = Resources.Load<DayNightProfile>("DayNightProfile");
            float e = DayNightMath.SunElevation(hour, day.SunriseHour, day.SunsetHour);
            float strength = DayNightMath.ShadowStrength(hour, day.SunriseHour, day.SunsetHour, 0, day.OvercastFadesShadow);
            Assert.That(e, Is.LessThanOrEqualTo(0f), "The real float horizon, not a double-precision model.");
            Assert.That(HullSunShadowMath.IsActive(e, strength, 1), Is.False);
        }

        [TestCase(23)] [TestCase(100)] [TestCase(111)]
        public void OwnIdBlock_IsExcluded(int id)
            => Assert.That(HullSunShadowMath.IsOwnId(id / 255f, 23, 100, 12), Is.True);

        [Test]
        public void NeighbourId_IsNotExcluded()
        {
            Assert.That(HullSunShadowMath.IsOwnId(99 / 255f, 23, 100, 12), Is.False);
            Assert.That(HullSunShadowMath.IsOwnId(112 / 255f, 23, 100, 12), Is.False);
            Assert.That(HullSunShadowMath.IsOwnId(0, 23, 0, 12), Is.False);
        }

        [Test]
        public void ProjectedBounds_ContainEveryCorner()
        {
            var source = new Bounds(Vector3.one, new Vector3(3, 5, 7));
            var pose = HullSunShadowMath.Pose(1.7f, 40, 8, -4);
            var p = HullSunShadowMath.Projection(new Vector2(-0.8f, 0.6f), 3, 40, 6, 2, 16);
            Bounds result = HullSunShadowMath.ProjectBounds(source, pose, p, 0.125f);
            Vector3 min = source.min, max = source.max;
            for (int i = 0; i < 8; i++)
                Assert.That(result.Contains(HullSunShadowMath.Project(new Vector3((i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z), pose, p, 0.125f)), Is.True);
            for (int x = 0; x <= 4; x++) for (int y = 0; y <= 4; y++) for (int z = 0; z <= 4; z++)
                Assert.That(result.Contains(HullSunShadowMath.Project(new Vector3(Mathf.Lerp(min.x,max.x,x/4f),
                    Mathf.Lerp(min.y,max.y,y/4f),Mathf.Lerp(min.z,max.z,z/4f)),pose,p,0.125f)),Is.True,
                    "Bounds also cover clamped points inside a box crossing the waterline.");
        }
    }
}
