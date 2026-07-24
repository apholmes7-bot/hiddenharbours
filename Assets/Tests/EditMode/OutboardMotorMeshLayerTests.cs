using System.Collections.Generic;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests
{
    /// <summary>
    /// <b>The mesh outboard's arithmetic and wiring (ADR 0022 phase 7)</b> — the parts the pixel
    /// acceptance does not reach.
    ///
    /// <para><c>OutboardPropMeshAcceptanceTests</c> adjudicates the GEOMETRY against the rigs' own
    /// <c>renderMotor</c>. What it cannot see is the seam: that the continuous steer read agrees with
    /// the sprite path at every column it had, that a fit decides how many engines exist, and that a
    /// dropped helm still centres the engine. Those are here.</para>
    /// </summary>
    public class OutboardMotorMeshLayerTests
    {
        const int Columns = OutboardMotorMath.SteerColumns;

        /// <summary>
        /// <b>The continuous read must agree with the quantised one wherever the quantised one had an
        /// answer.</b> This is what makes the two representations of one engine the same engine: the
        /// owner's A/B compares the LOOK, and if hard-over meant a different angle on each path he
        /// would be comparing two boats.
        /// </summary>
        [Test]
        public void SteerDegreesForPosition_AgreesWithTheBakedColumns_AtEveryColumn(
            [Values(30f, 32f)] float maxSteer)
        {
            for (int col = 0; col < Columns; col++)
                Assert.AreEqual(
                    OutboardMotorMath.SteerDegreesForColumn(col, Columns, maxSteer),
                    OutboardMotorMath.SteerDegreesForPosition(col, Columns, maxSteer), 1e-4f,
                    $"column {col}: the mesh path and the sprite path must agree about where the " +
                    "engine points whenever the sprite path has an opinion.");
        }

        /// <summary>…and BETWEEN them it must be strictly monotone, which is the whole point: those
        /// are the poses the nine-cell sheet could never draw.</summary>
        [Test]
        public void SteerDegreesForPosition_IsContinuousBetweenTheColumns()
        {
            float previous = float.NegativeInfinity;
            for (float p = 0f; p <= Columns - 1; p += 0.13f)
            {
                float d = OutboardMotorMath.SteerDegreesForPosition(p, Columns, 30f);
                Assert.Greater(d, previous, $"position {p} did not advance the swivel.");
                previous = d;
            }

            // And a fractional position really is BETWEEN two columns, not snapped to one.
            float half = OutboardMotorMath.SteerDegreesForPosition(4.5f, Columns, 30f);
            Assert.AreNotEqual(OutboardMotorMath.SteerDegreesForColumn(4, Columns, 30f), half);
            Assert.AreNotEqual(OutboardMotorMath.SteerDegreesForColumn(5, Columns, 30f), half);
        }

        [Test]
        public void SteerDegreesForPosition_ClampsRatherThanExtrapolating()
        {
            Assert.AreEqual(-30f, OutboardMotorMath.SteerDegreesForPosition(-4f, Columns, 30f), 1e-4f);
            Assert.AreEqual(+30f, OutboardMotorMath.SteerDegreesForPosition(99f, Columns, 30f), 1e-4f);
            Assert.AreEqual(0f, OutboardMotorMath.SteerDegreesForPosition(float.NaN, Columns, 30f), 1e-4f,
                "a NaN accumulator must read dead ahead, never swing the engine somewhere undefined.");
            Assert.AreEqual(0f, OutboardMotorMath.SteerDegreesForPosition(0f, 1, 30f), 1e-4f,
                "a single-column sheet is dead ahead.");
        }

        /// <summary>The baked pose is dead ahead and untilted, so its rotation is the identity — the
        /// property that makes the runtime transform a pure rotation about the clamp.</summary>
        [Test]
        public void TheCanonicalPose_IsTheIdentity()
        {
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity,
                                                 OutboardMotorMeshPose.Rotation(0f, 0f)), 1e-3f);
            Assert.AreNotEqual(0f, Quaternion.Angle(Quaternion.identity,
                                                    OutboardMotorMeshPose.Rotation(12f, 0f)));
            Assert.AreNotEqual(0f, Quaternion.Angle(Quaternion.identity,
                                                    OutboardMotorMeshPose.Rotation(0f, 12f)));
        }

        [Test]
        public void TiltClamps_TheRigsOwnWay_UpOnlyAndNeverPastTiltMax()
        {
            Assert.AreEqual(0f, OutboardMotorMeshPose.ClampTilt(-20f, 40f), 1e-4f,
                "an outboard trims UP out of the water; the rigs clamp at 0 and so must we.");
            Assert.AreEqual(40f, OutboardMotorMeshPose.ClampTilt(99f, 40f), 1e-4f);
            Assert.AreEqual(0f, OutboardMotorMeshPose.ClampTilt(20f, 0f), 1e-4f,
                "a fitting with no tilt authority does not tilt.");
        }

        // ---- the fit decides how many engines, and the FITTING states the spacing ----------------

        /// <summary>
        /// <b>One fitting, three boats, two fits.</b> The skiff outboard serves the console skiff and
        /// the sport skiff single (one engine, centreline) and the sport skiff twin (two, at the
        /// ±0.34 m its rig declares). Reading the mounts unconditionally would bolt two engines onto
        /// the workboat, which is why the FIT is asked for.
        /// </summary>
        [Test]
        public void MountsForFit_TakesTheFitFromTheBoat_AndTheSpacingFromTheFitting()
        {
            var twinCapable = ScriptableObject.CreateInstance<HullPropMeshDef>();
            twinCapable.LateralMountsMeters = new[] { -0.34f, 0.34f };

            Assert.AreEqual(new[] { 0f }, twinCapable.MountsForFit(multiEngine: false),
                "a single fit puts ONE engine on the centreline, however many mounts the rig declares.");
            Assert.AreEqual(new[] { -0.34f, 0.34f }, twinCapable.MountsForFit(multiEngine: true));

            var single = ScriptableObject.CreateInstance<HullPropMeshDef>();
            Assert.AreEqual(new[] { 0f }, single.MountsForFit(multiEngine: true),
                "a twin fit on a fitting that declares no spacing must fall back to one engine, not " +
                "stack two in the same place.");

            Object.DestroyImmediate(twinCapable);
            Object.DestroyImmediate(single);
        }

        // ---- the layer, driven ------------------------------------------------------------------

        private sealed class FakeProp : IHullPropRenderer
        {
            public Quaternion LocalRotation { get; set; } = Quaternion.identity;
            public float LateralOffsetMeters { get; set; }
            public bool Visible { get; set; } = true;
            public bool IsConfigured => true;
        }

        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        [Test]
        public void Configure_WritesTheClampOffsetsOnce_AndWakesDeadAhead()
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            var layer = go.AddComponent<OutboardMotorMeshLayer>();

            var port = new FakeProp();
            var star = new FakeProp();
            layer.Configure(new IHullPropRenderer[] { port, star }, new[] { -0.34f, 0.34f },
                            boat: null, steerColumns: Columns, maxSteerDegrees: 30f,
                            maxTiltDegrees: 40f);

            Assert.IsTrue(layer.IsWired);
            Assert.AreEqual(2, layer.EngineCount, "a twin fit hangs two engines on the transom.");
            Assert.AreEqual(-0.34f, port.LateralOffsetMeters, 1e-4f);
            Assert.AreEqual(+0.34f, star.LateralOffsetMeters, 1e-4f);
            Assert.AreEqual(0f, layer.SteerDegrees, 1e-4f, "she wakes dead ahead.");
            Assert.AreEqual(0f, layer.TiltDegrees, 1e-4f,
                "trimming the leg up is not a verb the game has yet — the seam carries it, the layer " +
                "does not drive it.");
        }

        [Test]
        public void AnUnwiredLayer_PosesNothing()
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            var layer = go.AddComponent<OutboardMotorMeshLayer>();

            Assert.IsFalse(layer.IsWired, "a scene-serialised layer idles until the skinner wires it.");
            Assert.AreEqual(0, layer.EngineCount);

            // ...and one missing fitting is not half an engine.
            layer.Configure(new IHullPropRenderer[] { new FakeProp(), null }, new[] { -0.34f, 0.34f },
                            null, Columns, 30f, 40f);
            Assert.IsFalse(layer.IsWired,
                "one attached fitting out of two must not draw — a boat with half a twin is worse " +
                "than the honest failure.");
        }

        [Test]
        public void AnUnmannedHelm_ReadsDeadAhead()
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            var layer = go.AddComponent<OutboardMotorMeshLayer>();
            layer.Configure(new IHullPropRenderer[] { new FakeProp() }, new[] { 0f },
                            boat: null, steerColumns: Columns, maxSteerDegrees: 30f, maxTiltDegrees: 40f);

            Assert.IsFalse(layer.IsHelmManned);
            Assert.AreEqual(0f, layer.Helm, 1e-4f,
                "no controller = nobody driving; the engine must never sit frozen hard-over (#205).");
        }
    }
}
