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

        // ---- and what the outboard changes (§7.7) ---------------------------------------------

        /// <summary>
        /// <b>Under power the oars come in — at once, whatever the stroke machine was about to
        /// say.</b> A dory keeps her oars aboard, but a boat with a running prop does not trail them
        /// in the water beside it, and she does not wait out the 1.2 s rest grace to decide that.
        /// </summary>
        [Test]
        public void WhileTheMotorRuns_BothOarsAreShipped_WithNoGraceToWaitOut()
        {
            // The most awkward moment for this: BOTH oars mid-pull, phase mid-cycle, idle timer at
            // zero. The rowing machine would draw a stroke frame here.
            int column = DoryOarMeshLayer.ColumnForOar(
                motorRunning: true, thisOarWorking: true, phase: 3.4f, otherOarWorking: true,
                bothIdleSeconds: 0f, restGraceSeconds: DoryOarMath.DefaultRestGraceSeconds,
                strokeColumns: DoryOarMath.StrokeColumns);

            Assert.AreEqual(DoryOarMath.RestingColumn, column,
                "an engine that is running ships the oars — DoryOarMath's OWN shipped state, wired, " +
                "not a fourth pose invented for the occasion.");

            DoryOarMeshPose.Pose pose = DoryOarMeshLayer.PoseFor(column, 3.4f);
            Assert.AreEqual(DoryOarMeshPose.RestingSweepDegrees, pose.SweepDegrees, 1e-6f,
                "…stowed fore along the gunwale: the rig's own oarPose('resting'), the same pose the " +
                "sprite dory's column 8 draws, so the two paths still agree.");
        }

        /// <summary>The other direction, and the acceptance criterion in one line: cut the motor and
        /// the oars come straight back out — into exactly the state the rowing machine would have
        /// chosen, because that is the only thing this rule does when the engine is off.</summary>
        [Test]
        public void CutTheMotor_AndTheOarsComeBackOut_ToTheUnchangedRowingMachine()
        {
            const float grace = DoryOarMath.DefaultRestGraceSeconds;

            foreach ((bool working, bool otherWorking, float idle) c in new[]
            {
                (true, true, 0f),      // pulling
                (false, true, 0f),     // idle while the other side works → trailing
                (false, false, 0.2f),  // both just stopped → still trailing
                (false, false, 5f),    // …and eventually shipped, on the grace
            })
            {
                Assert.AreEqual(
                    DoryOarMath.ColumnForOar(c.working, 3.4f, c.otherWorking, c.idle, grace,
                                             DoryOarMath.StrokeColumns),
                    DoryOarMeshLayer.ColumnForOar(false, c.working, 3.4f, c.otherWorking, c.idle,
                                                  grace, DoryOarMath.StrokeColumns),
                    $"with the motor off the answer must be DoryOarMath's, untouched " +
                    $"(working {c.working}, other {c.otherWorking}, idle {c.idle}s).");
            }
        }
    }
}
