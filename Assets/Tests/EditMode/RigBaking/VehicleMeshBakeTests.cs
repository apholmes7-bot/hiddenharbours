using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>The committed vehicle bake, checked against the rig it came from (ADR 0035).</b>
    ///
    /// <para>The mesh sub-assets are opaque binary blobs, so what can actually be reviewed is their
    /// STRUCTURE: that every declared fitting exists, that the wheels were genuinely lifted OUT of
    /// the body rather than left frozen in it, and that the chassis the controller solves against is
    /// the chassis the rig draws. Those are the claims that would be silently wrong if the bake went
    /// subtly astray, and each already has been once during this PR.</para>
    /// </summary>
    public class VehicleMeshBakeTests
    {
        static VehicleRigFleet.Vehicle Dually => VehicleRigFleet.Get("dually3500");

        static VehicleMeshDef LoadMesh()
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(Dually.MeshAssetPath);
            Assert.That(def, Is.Not.Null,
                $"{Dually.MeshAssetPath} did not load as a VehicleMeshDef. Re-run " +
                "Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake ALL road-vehicle meshes.");
            return def;
        }

        /// <summary>Every vehicle in the coverage table is either baked or carries a reason — and the
        /// Dually is now BAKED, with the asset actually on disk to prove it.</summary>
        [Test]
        public void TheDuallyIsBaked_AndHerAssetsAreOnDisk()
        {
            Assert.That(VehicleRigFleet.Baked, Contains.Item("dually3500"));
            Assert.That(VehicleRigFleet.NotBaked.ContainsKey("dually3500"), Is.False,
                "she cannot be both baked and excused.");

            VehicleMeshDef def = LoadMesh();
            Assert.That(def.IsUsable(), Is.True,
                "the committed def is not usable — she would be REFUSED at install and never drawn. " +
                "Check the ramp count against the facet shader's 16.");
            Assert.That(def.Id, Is.EqualTo(Dually.MeshId));
        }

        /// <summary>
        /// ⭐ <b>The wheels are genuinely OUT of the body.</b> All six fittings exist, each with its
        /// own mesh and a pivot away from the origin.
        ///
        /// <para>This is the assertion that would have caught the real defect of 2026-08-17: a static
        /// initialisation slip left the axis list empty, the body took all 1153 faces, and the bake
        /// reported a clean partition. A truck whose wheels are welded on is exactly the silently
        /// wrong bake this fixture exists to prevent.</para>
        /// </summary>
        [Test]
        public void AllSixFittingsWereLiftedOutOfTheBody_WithTheirOwnMeshesAndPivots()
        {
            VehicleMeshDef def = LoadMesh();

            Assert.That(def.Wheels, Is.Not.Null.And.Length.EqualTo(6),
                "she has four roll groups (the rear two are DUAL PAIRS on one axis each) plus two " +
                "steering knuckles. A count other than six means an axis was dropped or doubled.");

            var slots = new List<string>();
            foreach (VehicleFitment f in def.Wheels)
            {
                Assert.That(f.Prop, Is.Not.Null, $"fitting '{f.Slot}' has no baked mesh def.");
                Assert.That(f.Prop.IsUsable(), Is.True, $"fitting '{f.Slot}' is not usable.");
                Assert.That(f.Prop.Mesh.vertexCount, Is.GreaterThan(0),
                    $"fitting '{f.Slot}' baked an EMPTY mesh — its pose axis claimed no faces.");
                Assert.That(f.Prop.PivotLocalMeters, Is.Not.EqualTo(Vector3.zero),
                    $"fitting '{f.Slot}' turns about the vehicle ORIGIN, which swings it through the " +
                    "truck. Its pivot must be its own hub centre.");
                slots.Add(f.Slot);
            }

            Assert.That(slots, Is.EquivalentTo(new[]
                { "WheelFL", "WheelFR", "WheelRL", "WheelRR", "KnuckleFL", "KnuckleFR" }));
        }

        /// <summary>
        /// The front wheels steer AND roll; the rear only roll; the knuckles only steer. Sides are
        /// assigned by the sign of the pivot's x, so a swapped pair would steer the wrong wheel by
        /// the Ackermann inner angle — a 5° error that only shows at lock.
        /// </summary>
        [Test]
        public void EachFittingTakesTheRightMotionOnTheRightSide()
        {
            VehicleMeshDef def = LoadMesh();

            foreach (VehicleFitment f in def.Wheels)
            {
                VehicleFitmentMotion expected =
                    f.Slot.StartsWith("Knuckle") ? VehicleFitmentMotion.SteerOnly
                    : f.Slot.StartsWith("WheelF") ? VehicleFitmentMotion.SteerAndRoll
                    : VehicleFitmentMotion.RollOnly;
                Assert.That(f.Motion, Is.EqualTo(expected), $"'{f.Slot}' takes the wrong motion.");

                bool left = f.Slot.EndsWith("L");
                Assert.That(f.Side, Is.EqualTo(left ? VehicleFitmentSide.Left : VehicleFitmentSide.Right),
                    $"'{f.Slot}' is on the wrong side.");
                Assert.That(f.Prop.PivotLocalMeters.x, left ? Is.LessThan(0f) : Is.GreaterThan(0f),
                    $"'{f.Slot}' claims to be on the {(left ? "street" : "curb")} side but its hub is " +
                    "on the other one. Rig +x is the curb side.");
            }
        }

        /// <summary>
        /// The chassis on the def is the chassis the RIG draws — read off it at bake time, never
        /// typed. Checked against the sidecar's independently-published numbers, which is a genuine
        /// cross-check: the two documents were written by different passes.
        /// </summary>
        [Test]
        public void TheChassisMatchesTheRigAndTheSidecarAgrees()
        {
            VehicleMeshDef def = LoadMesh();

            Assert.That(def.WheelbaseMeters, Is.EqualTo(4.30f).Within(1e-3f));
            Assert.That(def.FrontTrackMeters, Is.EqualTo(1.80f).Within(1e-3f));
            Assert.That(def.WheelRadiusMeters, Is.EqualTo(0.42f).Within(1e-3f));
            Assert.That(def.FrontAxleY, Is.EqualTo(2.18f).Within(1e-3f));
            Assert.That(def.RearAxleY, Is.EqualTo(-2.12f).Within(1e-3f));
            Assert.That(def.MaxInnerSteerDegrees, Is.EqualTo(30f).Within(1e-3f));
            Assert.That(def.MaxOuterSteerDegrees, Is.EqualTo(24.94f).Within(0.01f));
            Assert.That(def.SuspensionTravelFrontMeters, Is.EqualTo(0.09f).Within(1e-4f));
            Assert.That(def.SuspensionTravelRearMeters, Is.EqualTo(0.11f).Within(1e-4f));
        }

        /// <summary>
        /// ⚠️ <b>The azimuth convention, and it is the single most load-bearing field on the asset.</b>
        /// Flip it and she drives backwards at east and west. Measured at bake time from the rig's own
        /// front-axle abeam pair, confirmed by her centreline hitch→hood pair — never from the
        /// silhouette taper, which carries no signal on a box.
        /// </summary>
        [Test]
        public void TheAzimuthConventionIsCounterClockwise_AsBothRigOraclesMeasured()
        {
            VehicleMeshDef def = LoadMesh();
            Assert.That(def.AzimuthCounterClockwise, Is.True,
                "she baked CLOCKWISE. Both of the rig's analytic oracles say counter-clockwise " +
                "(abeam ground bearing exactly −90.00°, nose 202.24 px west at a quarter turn), so " +
                "this asset disagrees with the art it came from and she will drive stern-first at " +
                "E/W. Re-bake rather than editing the field.");
            Assert.That(def.ZeroHeadingDegrees, Is.EqualTo(0f),
                "her contract's facing_note says dir 0 (N) shows the TAIL — she faces north there, " +
                "the same zero every boat rig uses.");
            Assert.That(def.ElevationDeg, Is.EqualTo(40f).Within(0.01f),
                "she must be projected through the same camera as the boat fleet, or a truck and a " +
                "boat in one scene are drawn from different elevations.");
        }

        /// <summary>
        /// The ramp table fits the facet shader's <c>float4[16]</c>, with the rig's default ramp
        /// FIRST — the packer resolves an unknown material name to index 0, so a reordering silently
        /// recolours every face whose material it cannot find.
        ///
        /// <para>She declares seventeen materials and her faces reference sixteen; the odd one out is
        /// <c>glow</c>, the lit variant of her lamps, which belongs to a night pass this mesh does
        /// not carry. Dropping it is what makes her fit at all.</para>
        /// </summary>
        [Test]
        public void TheRampTableFitsTheShader_AndTheDefaultRampIsIndexZero()
        {
            VehicleMeshDef def = LoadMesh();
            Assert.That(def.Ramps.Length, Is.EqualTo(16),
                "her faces reference exactly sixteen materials. Seventeen would not fit the facet " +
                "shader's _RampMeta and IsUsable() would refuse her outright.");
            foreach (HullMeshDef.Ramp r in def.Ramps)
                Assert.That(r.Colors, Is.Not.Null.And.Not.Empty, "an empty ramp renders flat magenta.");
        }

        /// <summary>
        /// The vehicle the WORLD places exists, wears this mesh, and carries the owner-ruled id.
        /// <c>vehicle.*</c> ids are append-only once shipped (CLAUDE.md §5), so this pins the name.
        /// </summary>
        [Test]
        public void TheVehicleDefExists_AndWearsTheBakedMesh()
        {
            var vehicle = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Vehicles.VehicleDef>(
                Dually.VehicleDefPath);
            Assert.That(vehicle, Is.Not.Null,
                $"{Dually.VehicleDefPath} is missing — nothing can place or drive her. Re-run the " +
                "vehicle bake, which creates it.");
            Assert.That(vehicle.Id, Is.EqualTo("vehicle.dually_3500"),
                "her id is owner-ruled and append-only.");
            Assert.That(vehicle.Mesh, Is.Not.Null, "she wears no mesh.");
            Assert.That(vehicle.Mesh.Id, Is.EqualTo(Dually.MeshId));
            Assert.That(vehicle.IsUsable(), Is.True);
        }

        /// <summary>Every fitting asset the def points at is actually committed — a def referencing a
        /// mesh nobody committed loads as a null and draws a truck with no wheels.</summary>
        [Test]
        public void EveryFittingAssetIsCommitted()
        {
            VehicleMeshDef def = LoadMesh();
            foreach (VehicleFitment f in def.Wheels)
            {
                string path = AssetDatabase.GetAssetPath(f.Prop);
                Assert.That(path, Is.Not.Empty, $"'{f.Slot}' is not a committed asset at all.");
                Assert.That(File.Exists(path), Is.True, $"'{f.Slot}' points at {path}, which is not on disk.");
            }
        }
    }
}
