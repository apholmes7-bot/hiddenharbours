using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The oars under power are STOWED (owner ruling 2026-11-22: "the oars will stay stowed lying
    /// lengthwise against the hull").</b> The placement rule, provable without a scene: under power
    /// the stow's pose and a shift out of the tholes; rowed, exactly what the rowing machine always
    /// drew and no shift at all, so the owner-approved rowed dory cannot move.
    /// </summary>
    public class DoryOarStowTests
    {
        static readonly OarStowConfig Stow = OarStowConfig.Default;
        static readonly Vector3 PortTholes = new Vector3(-0.7f, -1.09f, 0.59f);

        [Test]
        public void UnderPower_TheOarIsDrawnAtTheStowsPose_ShiftedOutOfTheTholes()
        {
            DoryOarMeshPose.Pose pose = DoryOarMeshLayer.Placement(
                motorRunning: true, hasStow: true, in Stow, DoryOarMeshLayer.PortSide, PortTholes,
                DoryOarMath.RestingColumn, 3.4f, out Vector3 shift);

            Assert.AreEqual(Stow.SweepDegrees, pose.SweepDegrees, 1e-6f);
            Assert.AreEqual(Stow.DipDegrees, pose.DipDegrees, 1e-6f);
            Assert.That((shift - (Stow.PortLockLocalMeters - PortTholes)).magnitude, Is.LessThan(1e-6f),
                "the whole fitting is carried from its tholes to the stowed lock point.");
        }

        [Test]
        public void UnderPower_TheStowedOarIsDrawnNearerTheCamera_OnlyAlongTheViewAxis()
        {
            Vector3 toward = new Vector3(0.2f, -0.6f, 0.77f).normalized;
            DoryOarMeshLayer.Placement(true, true, in Stow, DoryOarMeshLayer.PortSide, PortTholes,
                                       DoryOarMath.RestingColumn, 0f, out Vector3 lifted, toward);
            DoryOarMeshLayer.Placement(true, true, in Stow, DoryOarMeshLayer.PortSide, PortTholes,
                                       DoryOarMath.RestingColumn, 0f, out Vector3 flat);

            Assert.That(((lifted - flat) - toward * Stow.CameraLiftMeters).magnitude, Is.LessThan(1e-6f),
                "the depth lift is the stow's lift, along the view axis and nowhere else.");
        }

        [Test]
        public void Rowed_TheViewAxisChangesNothing()
        {
            DoryOarMeshLayer.Placement(false, true, in Stow, DoryOarMeshLayer.PortSide, PortTholes,
                                       3, 1.5f, out Vector3 shift, Vector3.back);
            Assert.AreEqual(Vector3.zero, shift, "a rowed oar is never lifted: the rowed dory is untouched.");
        }

        [Test]
        public void Rowed_ThePlacementIsTheRowingMachinesOwn_WithNoShift(
            [Values(0, 3, 7)] int column, [Values(0f, 2.5f, 6.75f)] float phase)
        {
            DoryOarMeshPose.Pose pose = DoryOarMeshLayer.Placement(
                motorRunning: false, hasStow: true, in Stow, DoryOarMeshLayer.PortSide, PortTholes,
                column, phase, out Vector3 shift);
            DoryOarMeshPose.Pose rowed = DoryOarMeshLayer.PoseFor(column, phase);

            Assert.AreEqual(rowed.SweepDegrees, pose.SweepDegrees, 0f, "rowed: the rowing machine's sweep, exactly.");
            Assert.AreEqual(rowed.DipDegrees, pose.DipDegrees, 0f, "rowed: the rowing machine's dip, exactly.");
            Assert.AreEqual(Vector3.zero, shift, "rowed: the oar never leaves its tholes.");
        }

        [Test]
        public void Rowed_ShippedAndTrailingOars_AreUnchangedToo()
        {
            foreach (int column in new[] { DoryOarMath.RestingColumn, DoryOarMath.TrailingColumn })
            {
                DoryOarMeshPose.Pose pose = DoryOarMeshLayer.Placement(
                    false, true, in Stow, DoryOarMeshLayer.StarboardSide, PortTholes, column, 0f, out Vector3 shift);
                DoryOarMeshPose.Pose rowed = DoryOarMeshLayer.PoseFor(column, 0f);
                Assert.AreEqual(rowed.SweepDegrees, pose.SweepDegrees, 0f);
                Assert.AreEqual(rowed.DipDegrees, pose.DipDegrees, 0f);
                Assert.AreEqual(Vector3.zero, shift);
            }
        }

        [Test]
        public void UnderPower_WithNoStowConfigured_TheOarsShipInTheTholes_AsBefore()
        {
            DoryOarMeshPose.Pose pose = DoryOarMeshLayer.Placement(
                motorRunning: true, hasStow: false, in Stow, DoryOarMeshLayer.PortSide, PortTholes,
                DoryOarMath.RestingColumn, 0f, out Vector3 shift);

            Assert.AreEqual(DoryOarMeshPose.RestingSweepDegrees, pose.SweepDegrees, 1e-6f);
            Assert.AreEqual(Vector3.zero, shift);
        }

        [Test]
        public void TheStarboardStow_MirrorsThePort_AcrossTheCentreline()
        {
            Vector3 port = Stow.LockLocalMeters(DoryOarMeshLayer.PortSide);
            Vector3 star = Stow.LockLocalMeters(DoryOarMeshLayer.StarboardSide);
            Assert.AreEqual(-port.x, star.x, 1e-6f);
            Assert.AreEqual(port.y, star.y, 1e-6f);
            Assert.AreEqual(port.z, star.z, 1e-6f);

            Vector3 portDir = DoryOarMeshPose.Direction(DoryOarMeshLayer.PortSide, Stow.SweepDegrees, Stow.DipDegrees);
            Vector3 starDir = DoryOarMeshPose.Direction(DoryOarMeshLayer.StarboardSide, Stow.SweepDegrees, Stow.DipDegrees);
            Assert.AreEqual(-portDir.x, starDir.x, 1e-6f, "the two oars mirror, blade for blade.");
            Assert.AreEqual(portDir.y, starDir.y, 1e-6f);
        }

        [Test]
        public void TheDefaultStow_LiesForeAndAft_BladeForward_AndLevel()
        {
            Vector3 dir = DoryOarMeshPose.Direction(DoryOarMeshLayer.PortSide, Stow.SweepDegrees, Stow.DipDegrees);
            Assert.That(dir.y, Is.GreaterThan(Mathf.Cos(10f * Mathf.Deg2Rad)),
                "lengthwise: within ten degrees of fore and aft, blade toward the bow.");
            Assert.AreEqual(0f, dir.z, 1e-6f, "lying level across the thwarts.");
            Assert.That(dir.x, Is.GreaterThan(0f),
                "the port blade angles in toward the centreline, where her bow narrows.");
        }
    }
}
