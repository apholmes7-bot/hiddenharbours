using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The drawn oar holds its blade over the water</b> (<see cref="DoryOarMeshPose.Levelled"/>).
    ///
    /// <para>The oars are fittings on the hull's posed mesh, so they inherit its canned roll, and the
    /// rig buries the blade through the drive and skims it a few centimetres over still water on the
    /// recovery. Either way the far blade was drawn under the sea and only a dark loom showed. The
    /// levelled pose puts the blade point back at the height it would have with the hull level, held
    /// no lower than a floor over the design waterline. These tests pin that law, that it is a no-op
    /// when there is nothing to do, and that it never touches the logical pose the splash and the
    /// stroke timing read.</para>
    /// </summary>
    public class DoryOarLevelledTests
    {
        const float Reach = 1.45f;
        static readonly Vector3 StarLock = new Vector3(0.62f, 0.1f, 0.35f);
        static readonly Vector3 PortLock = new Vector3(-0.62f, 0.1f, 0.35f);

        // The hull's roll as RigToWorld applies it: a positive roll lowers +x (starboard).
        static Vector3 Roll(Vector3 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector3(v.x * c + v.z * s, v.y, -v.x * s + v.z * c);
        }

        static float BladeZ(int side, DoryOarMeshPose.Pose pose, Vector3 oarlock, float roll)
        {
            Vector3 d = DoryOarMeshPose.Direction(side, pose.SweepDegrees, pose.DipDegrees);
            return Roll(oarlock, roll).z + Reach * Roll(d, roll).z;
        }

        static float LevelBladeZ(int side, DoryOarMeshPose.Pose pose, Vector3 oarlock)
            => oarlock.z + Reach * DoryOarMeshPose.Direction(side, pose.SweepDegrees, pose.DipDegrees).z;

        [Test]
        public void NothingToDo_ReturnsThePoseItself()
        {
            DoryOarMeshPose.Pose p = DoryOarMeshPose.Resting;
            foreach (var got in new[]
                     {
                         DoryOarMeshPose.Levelled(1, p, StarLock, 0f, Reach, 1f),
                         DoryOarMeshPose.Levelled(1, p, StarLock, 4.7f, Reach, 0f, 0.1f, 0.2f),
                         DoryOarMeshPose.Levelled(1, p, StarLock, 4.7f, 0f, 1f, 0.1f, 0.2f),
                     })
            {
                Assert.AreEqual(p.SweepDegrees, got.SweepDegrees, 0f);
                Assert.AreEqual(p.DipDegrees, got.DipDegrees, 0f);
            }
        }

        [TestCase(4.7f)]
        [TestCase(2.3f)]
        [TestCase(-4.7f)]
        public void TheBladePoint_StandsWhereItWouldWithTheHullLevel(float roll)
        {
            for (int i = 0; i <= 8; i++)
            {
                DoryOarMeshPose.Pose p = DoryOarMeshPose.Row(i / 8f);
                foreach (int side in new[] { 1, -1 })
                {
                    Vector3 lk = side > 0 ? StarLock : PortLock;
                    DoryOarMeshPose.Pose lv = DoryOarMeshPose.Levelled(side, p, lk, roll, Reach, 1f);
                    Assert.AreEqual(LevelBladeZ(side, p, lk), BladeZ(side, lv, lk, roll), 1e-4f,
                        $"side {side} t {i / 8f} roll {roll}: the levelled blade point must stand at its level height.");
                }
            }
        }

        [Test]
        public void APositiveRoll_LiftsTheStarboardBlade_AndLowersThePortOne()
        {
            DoryOarMeshPose.Pose p = DoryOarMeshPose.Resting;
            float roll = 4.7f;
            float starRaw = BladeZ(1, p, StarLock, roll);
            float starLv = BladeZ(1, DoryOarMeshPose.Levelled(1, p, StarLock, roll, Reach, 1f), StarLock, roll);
            float portRaw = BladeZ(-1, p, PortLock, roll);
            float portLv = BladeZ(-1, DoryOarMeshPose.Levelled(-1, p, PortLock, roll, Reach, 1f), PortLock, roll);
            Assert.Greater(starLv, starRaw + 0.05f, "the rolled-down starboard blade is lifted back out");
            Assert.Less(portLv, portRaw - 0.05f, "the rolled-up port blade is brought back down");
        }

        [Test]
        public void PortAndStarboard_AreMirrorImages()
        {
            DoryOarMeshPose.Pose p = DoryOarMeshPose.Row(0.3f);
            DoryOarMeshPose.Pose s = DoryOarMeshPose.Levelled(1, p, StarLock, 3f, Reach, 1f, 0.1f, 0.2f);
            DoryOarMeshPose.Pose q = DoryOarMeshPose.Levelled(-1, p, PortLock, -3f, Reach, 1f, 0.1f, 0.2f);
            Assert.AreEqual(s.SweepDegrees, q.SweepDegrees, 1e-4f);
            Assert.AreEqual(s.DipDegrees, q.DipDegrees, 1e-4f);
        }

        [Test]
        public void HalfWeight_IsHalfWayBetween()
        {
            DoryOarMeshPose.Pose p = DoryOarMeshPose.Resting;
            DoryOarMeshPose.Pose full = DoryOarMeshPose.Levelled(1, p, StarLock, 4f, Reach, 1f);
            DoryOarMeshPose.Pose half = DoryOarMeshPose.Levelled(1, p, StarLock, 4f, Reach, 0.5f);
            Assert.AreEqual((p.DipDegrees + full.DipDegrees) * 0.5f, half.DipDegrees, 1e-4f);
            Assert.AreEqual((p.SweepDegrees + full.SweepDegrees) * 0.5f, half.SweepDegrees, 1e-4f);
        }

        [Test]
        public void DrawnBladeZ_IsHeldAtTheFloor_AndNeverFalls()
        {
            const float Wl = 0.2f, Floor = 0.1f;
            Assert.AreEqual(Wl + Floor, DoryOarMeshPose.DrawnBladeZ(Wl - 0.4f, Wl, Floor), 1e-6f);
            Assert.AreEqual(Wl + 0.5f, DoryOarMeshPose.DrawnBladeZ(Wl + 0.5f, Wl, Floor), 1e-6f, "over the floor it is its own");
            Assert.AreEqual(Wl - 0.06f, DoryOarMeshPose.DrawnBladeZ(Wl - 0.4f, Wl, -0.06f), 1e-6f, "a floor under the line");
            Assert.AreEqual(Wl - 0.4f, DoryOarMeshPose.DrawnBladeZ(Wl - 0.4f, Wl, float.NegativeInfinity), 1e-6f, "no floor");
            float prev = float.NegativeInfinity;
            for (float z = Wl - 0.5f; z <= Wl + 0.5f; z += 0.002f)
            {
                float d = DoryOarMeshPose.DrawnBladeZ(z, Wl, Floor);
                Assert.GreaterOrEqual(d, prev);
                Assert.GreaterOrEqual(d, Wl + Floor - 1e-6f);
                prev = d;
            }
        }

        [Test]
        public void TheWholeStroke_IsDrawnWithTheBladeOffTheFloor_AtAnyRoll()
        {
            const float Wl = 0.2f, Floor = 0.1f;
            foreach (float roll in new[] { 0f, 4.7f, -4.7f })
                for (int i = 0; i < 16; i++)
                {
                    DoryOarMeshPose.Pose p = DoryOarMeshPose.Row(i / 16f);
                    foreach (int side in new[] { 1, -1 })
                    {
                        Vector3 lk = side > 0 ? StarLock : PortLock;
                        DoryOarMeshPose.Pose lv = DoryOarMeshPose.Levelled(side, p, lk, roll, Reach, 1f, Wl, Floor);
                        float want = Mathf.Max(LevelBladeZ(side, p, lk), Wl + Floor);
                        Assert.AreEqual(want, BladeZ(side, lv, lk, roll), 1e-4f, $"side {side} t {i / 16f} roll {roll}");
                    }
                }
        }

        [Test]
        public void TheBottomOfTheDrive_IsDrawnShallower()
        {
            const float Wl = 0.2f, Floor = 0.1f;
            DoryOarMeshPose.Pose p = DoryOarMeshPose.Row(0f); // dip 28.5, the bottom of the drive
            Assume.That(LevelBladeZ(1, p, StarLock), Is.LessThan(Wl + Floor));
            DoryOarMeshPose.Pose lv = DoryOarMeshPose.Levelled(1, p, StarLock, 0f, Reach, 1f, Wl, Floor);
            Assert.Less(lv.DipDegrees, p.DipDegrees);
            Assert.AreEqual(p.SweepDegrees, lv.SweepDegrees, 1e-3f, "the stroke's bearing is kept");
        }

        [Test]
        public void TheLogicalPose_IsNeverChanged()
        {
            // The splash and the stroke timing read PortPose / StarboardPose; only the drawn copy is
            // levelled. Levelled is pure, so the input stays as it was.
            DoryOarMeshPose.Pose p = DoryOarMeshPose.Row(0.6f);
            float sweep = p.SweepDegrees, dip = p.DipDegrees;
            DoryOarMeshPose.Levelled(1, p, StarLock, 4.7f, Reach, 1f, 0.1f, 0.2f);
            Assert.AreEqual(sweep, p.SweepDegrees, 0f);
            Assert.AreEqual(dip, p.DipDegrees, 0f);
        }

        [Test]
        public void TheDorysStation_ShipsLevellingAndAFloor()
        {
            RowingStationConfig d = RowingStationConfig.Default;
            Assert.AreEqual(1f, d.RockLevelling, 0f);
            Assert.Greater(d.RockLevelReachMeters, 0f);
            Assert.Greater(d.BladeFloorMeters, 0f);
        }
    }
}
