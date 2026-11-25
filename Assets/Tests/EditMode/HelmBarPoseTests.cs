using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The helm bar's pose against the rig's own arithmetic. The jetski rig turns her handlebars in
    /// the column's frame (hbXf in jetSkiIsoRig.js); the game turns the baked dead-ahead fitting by
    /// one rotation about the column axis. These hold that the two land on the same points.
    /// </summary>
    public class HelmBarPoseTests
    {
        // The rig's column: raked 28 degrees aft of straight up is a stand-in; any rake must agree.
        static Vector3 Axis(float rakeDegrees) =>
            new Vector3(0f, -Mathf.Sin(rakeDegrees * Mathf.Deg2Rad), Mathf.Cos(rakeDegrees * Mathf.Deg2Rad));

        // hbXf(deg) from the rig, relative to the pivot: p = (across, toward the rider, up the column).
        static Vector3 RigPoint(Vector3 p, float degrees, Vector3 n)
        {
            float th = degrees * Mathf.Deg2Rad, c = Mathf.Cos(th), s = Mathf.Sin(th);
            float a = p.x * c - p.y * s, b = p.x * s + p.y * c;
            var q = new Vector3(0f, -n.z, n.y);
            return new Vector3(a, b * q.y + p.z * n.y, b * q.z + p.z * n.z);
        }

        [Test]
        public void TheGamesRotation_LandsEveryPointWhereTheRigDrawsIt()
        {
            var points = new[] { new Vector3(0.3f, 0f, 0.2f), new Vector3(-0.3f, 0.05f, 0.18f), new Vector3(0f, 0f, 0.25f) };
            foreach (float rake in new[] { 0f, 20f, 35f })
                foreach (float deg in new[] { -35f, -10f, 0f, 17f, 35f })
                    foreach (Vector3 p in points)
                    {
                        Vector3 n = Axis(rake);
                        Vector3 baked = RigPoint(p, 0f, n);
                        Vector3 want = RigPoint(p, deg, n);
                        Vector3 got = HelmBarPose.Rotation(deg, n) * baked;
                        Assert.Less((got - want).magnitude, 1e-4f, $"rake {rake}, {deg} degrees, point {p}");
                    }
        }

        [Test]
        public void AZeroAxis_TurnsStraightUp_AndZeroDegreesIsTheBakedPose()
        {
            Assert.AreEqual(Quaternion.identity, HelmBarPose.Rotation(0f, Axis(30f)));
            Vector3 turned = HelmBarPose.Rotation(90f, Vector3.zero) * Vector3.right;
            Assert.Less((turned - RigPoint(Vector3.right, 90f, Vector3.forward)).magnitude, 1e-4f);
        }

        [Test]
        public void TheHelmIsClampedToTheLock_AndTheBarFollowsAtABoundedRate()
        {
            Assert.AreEqual(35f, HelmBarPose.DegreesForHelm(2f, 35f));
            Assert.AreEqual(-17.5f, HelmBarPose.DegreesForHelm(-0.5f, 35f));
            Assert.AreEqual(0f, HelmBarPose.DegreesForHelm(1f, -5f));
            Assert.AreEqual(4.8f, HelmBarPose.Step(0f, 35f, 240f, 0.02f), 1e-5f);
            Assert.AreEqual(35f, HelmBarPose.Step(34f, 35f, 240f, 0.02f));
        }
    }
}
