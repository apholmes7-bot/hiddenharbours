using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The one thing the mesh oar path does differently from the sprite one (ADR 0022 phase 7):
    /// it throws the rounding away.</b>
    ///
    /// <para>Both layers run the same state machine — <see cref="DoryOarMath.ColumnForOar"/> decides
    /// working / trailing / shipped from the same accumulators with the same grace. The sprite layer
    /// then draws the column. The mesh layer reads the column only to learn WHICH STATE the oar is in
    /// and poses a working oar from the CONTINUOUS phase instead, exactly as #243 stopped the mesh
    /// hull's wave rock being quantised to frames.</para>
    ///
    /// <para>That is a two-line method, and two-line methods that discard a rounding are precisely how
    /// a "continuous" path ends up quietly stepping. These tests are the guard.</para>
    /// </summary>
    public class DoryOarMeshLayerTests
    {
        [Test]
        public void AWorkingOar_IsPosedFromTheContinuousPhase_NotTheColumn()
        {
            // Two phases INSIDE one sprite column (3.0 and 3.9 both draw column 3). If the mesh layer
            // read the column, these would be the same pose — which is the whole defect this path
            // exists to remove.
            DoryOarMeshPose.Pose a = DoryOarMeshLayer.PoseFor(3, 3.0f);
            DoryOarMeshPose.Pose b = DoryOarMeshLayer.PoseFor(3, 3.9f);

            Assert.Greater(Mathf.Abs(a.SweepDegrees - b.SweepDegrees), 1f,
                "phase 3.0 and 3.9 both draw sprite column 3, and the mesh oar must NOT: it is posed " +
                "from the continuous phase, which is the entire point of the fitting being a mesh. " +
                $"Measured sweep {a.SweepDegrees:F3}° vs {b.SweepDegrees:F3}°.");

            // …and it is the rig's own stroke at that fraction of a turn, not some rescaling of it.
            DoryOarMeshPose.Pose expected = DoryOarMeshPose.Row(3.9f / DoryOarMath.StrokeColumns);
            Assert.AreEqual(expected.SweepDegrees, b.SweepDegrees, 1e-5f);
            Assert.AreEqual(expected.DipDegrees, b.DipDegrees, 1e-5f);
        }

        [Test]
        public void TheStrokeIsContinuous_AcrossTheWrapFromTheLastColumnBackToTheFirst()
        {
            // The seam a per-column path cannot see: the end of column 7 and the start of column 0 are
            // the same instant of the rig's cycle, so the pose either side of the wrap must be within
            // one small step. A layer that reset the phase (or read the column) jumps the full sweep.
            DoryOarMeshPose.Pose before = DoryOarMeshLayer.PoseFor(7, 7.999f);
            DoryOarMeshPose.Pose after = DoryOarMeshLayer.PoseFor(0, 0.0f);

            Assert.AreEqual(after.SweepDegrees, before.SweepDegrees, 0.1f,
                "the stroke must not jump where the cycle wraps — 7.999 and 0.0 are the same catch.");
            Assert.AreEqual(after.DipDegrees, before.DipDegrees, 0.1f);
        }

        [Test]
        public void ShippedAndTrailing_AreTheRigsOwnFixedPoses_SoBothPathsAgreeExactly()
        {
            // These two states are NOT interpolated on either path: the rig defines them as constants
            // (oarPose('resting') / ('trailing')). A mesh oar that drifted off them would ship at a
            // different angle from the sprite oar, and the owner's A/B would show two different boats.
            DoryOarMeshPose.Pose shipped = DoryOarMeshLayer.PoseFor(DoryOarMath.RestingColumn, 4.2f);
            Assert.AreEqual(DoryOarMeshPose.RestingSweepDegrees, shipped.SweepDegrees, 1e-6f);
            Assert.AreEqual(DoryOarMeshPose.RestingDipDegrees, shipped.DipDegrees, 1e-6f);

            DoryOarMeshPose.Pose trailing = DoryOarMeshLayer.PoseFor(DoryOarMath.TrailingColumn, 4.2f);
            Assert.AreEqual(DoryOarMeshPose.TrailingSweepDegrees, trailing.SweepDegrees, 1e-6f);
            Assert.AreEqual(DoryOarMeshPose.TrailingDipDegrees, trailing.DipDegrees, 1e-6f);

            // …and the phase is genuinely ignored in those states, not merely unused by luck.
            DoryOarMeshPose.Pose shippedElsewhere =
                DoryOarMeshLayer.PoseFor(DoryOarMath.RestingColumn, 0.1f);
            Assert.AreEqual(shipped.SweepDegrees, shippedElsewhere.SweepDegrees, 1e-6f);
        }

        [Test]
        public void PortAndStarboard_MirrorEachOther_AboutTheCentreline()
        {
            // The rig signs oarDir's x by `side`, so the two oars are mirror images. Getting this wrong
            // is the failure that looks fine at rest and rows backwards on one side.
            var pose = DoryOarMeshPose.Row(0.3f);
            Vector3 port = DoryOarMeshPose.Direction(DoryOarMeshLayer.PortSide,
                                                     pose.SweepDegrees, pose.DipDegrees);
            Vector3 star = DoryOarMeshPose.Direction(DoryOarMeshLayer.StarboardSide,
                                                     pose.SweepDegrees, pose.DipDegrees);

            Assert.AreEqual(-star.x, port.x, 1e-5f, "the oars point to opposite beams");
            Assert.AreEqual(star.y, port.y, 1e-5f, "…but sweep fore/aft together");
            Assert.AreEqual(star.z, port.z, 1e-5f, "…and dip together");
        }
    }
}
