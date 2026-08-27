using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE ROAD FLEET's committed bake — eight of the drop's nine bodies (PR 2 of 4).</b>
    ///
    /// <para>The sibling of <see cref="VehicleMeshBakeTests"/>, which covers the Dually. Mesh
    /// sub-assets are opaque binary blobs, so what is reviewable is their STRUCTURE: that every
    /// fitting exists and was genuinely lifted OUT of the body, that each body took HER OWN cell and
    /// chassis rather than the pack's, and that the facts a face list cannot carry — the drive door,
    /// the solid box, the flotation — are the ones her sidecar publishes.</para>
    ///
    /// <para><b>Everything measured here is measured against the RIG, in the repo's own V8.</b> A
    /// test that restated the numbers would be a second transcription agreeing with the first, which
    /// is the failure mode this project has shipped five mirrored boats through.</para>
    /// </summary>
    public class RoadFleetBakeTests
    {
        /// <summary>The facet shader's <c>_RampMeta</c> is a <c>float4[16]</c> — over it a def is not
        /// usable and the vehicle is unplaceable.</summary>
        const int ShaderRampCap = 16;

        /// <summary>One baked body and what it must have come out as. Slots and counts are what the
        /// articulation split produced; the CELL is the one number a bake most easily takes from the
        /// wrong place, because every rig in the pack publishes a plausible one.</summary>
        public sealed class Baked
        {
            public string Key, Global;
            public int CellW, CellH, PivotX, PivotY;
            public int Faces, BodyFaces;
            public string[] Slots;
            public int UsedRamps;

            /// <summary>Which body of a container rig, or null.</summary>
            public string Pick;

            /// <summary>False for a towed body — she carries a mesh and no VehicleDef; see the
            /// deliberate deferral on her VehicleRigFleet entry.</summary>
            public bool WearsAVehicleDef = true;

            public override string ToString() => Key;
        }

        // Face counts and cells are RoadFleetKitProbeTests'/TrailerIsoKitProbeTests' measurements,
        // re-asserted here against what actually got baked. Body faces are the arithmetic those
        // fixtures make available: total − (roll groups + knuckles).
        static readonly Baked[] Bodies =
        {
            new Baked
            {
                Key = "caboverBox", Global = "BoxIso",
                CellW = 384, CellH = 320, PivotX = 192, PivotY = 214,
                Faces = 1090, BodyFaces = 1090 - 348 - 80, UsedRamps = 16,
                Slots = new[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR", "KnuckleFL", "KnuckleFR" },
            },
            new Baked
            {
                Key = "convBox", Global = "ConvBoxIso",
                // ⚠️ NOT the pack's 384×320. She is 9.6 m long and takes her own larger cell, which
                // the extractor reads off HER global — the assertion that a bake did not quietly
                // take the road cell and crop her tail.
                CellW = 448, CellH = 352, PivotX = 224, PivotY = 214,
                Faces = 1211, BodyFaces = 1211 - 412 - 80, UsedRamps = 16,
                Slots = new[] { "WheelFL", "WheelFR", "WheelRL", "WheelRR", "KnuckleFL", "KnuckleFR" },
            },
            new Baked
            {
                Key = "aeroSemi", Global = "AeroSemiIso",
                CellW = 384, CellH = 320, PivotX = 192, PivotY = 214,
                Faces = 1538, BodyFaces = 1538 - 714 - 80, UsedRamps = 15,
                // EIGHT fittings, not six: her rear is a TANDEM on one axis per side, split by
                // station window into two axles apiece.
                Slots = new[] { "WheelFL", "WheelFR", "WheelRL1", "WheelRL2", "WheelRR1", "WheelRR2",
                                "KnuckleFL", "KnuckleFR" },
            },
            new Baked
            {
                Key = "classicSemi", Global = "ClassicSemiIso",
                CellW = 384, CellH = 320, PivotX = 192, PivotY = 214,
                Faces = 1625, BodyFaces = 1625 - 714 - 80, UsedRamps = 16,
                Slots = new[] { "WheelFL", "WheelFR", "WheelRL1", "WheelRL2", "WheelRR1", "WheelRR2",
                                "KnuckleFL", "KnuckleFR" },
            },

            // ---- the four towed bodies. No steer, so no knuckles; the landing gear stays in the
            //      body (it telescopes — see TheLandingGearTelescopes… below).
            new Baked
            {
                Key = "trailerFlatbed28", Global = "TrailerIso", Pick = "flatbed28",
                CellW = 384, CellH = 320, PivotX = 192, PivotY = 214,
                Faces = 643, BodyFaces = 643 - 254, UsedRamps = 12,
                Slots = new[] { "WheelL", "WheelR" }, WearsAVehicleDef = false,
            },
            new Baked
            {
                Key = "trailerFlatbed53", Global = "TrailerIso", Pick = "flatbed53",
                CellW = 640, CellH = 480, PivotX = 320, PivotY = 300,
                Faces = 1119, BodyFaces = 1119 - 508, UsedRamps = 12,
                Slots = new[] { "WheelL1", "WheelL2", "WheelR1", "WheelR2" }, WearsAVehicleDef = false,
            },
            new Baked
            {
                Key = "trailerReefer28", Global = "TrailerIso", Pick = "reefer28",
                CellW = 384, CellH = 320, PivotX = 192, PivotY = 214,
                Faces = 656, BodyFaces = 656 - 254, UsedRamps = 12,
                Slots = new[] { "WheelL", "WheelR" }, WearsAVehicleDef = false,
            },
            new Baked
            {
                Key = "trailerReefer53", Global = "TrailerIso", Pick = "reefer53",
                CellW = 640, CellH = 480, PivotX = 320, PivotY = 300,
                Faces = 1067, BodyFaces = 1067 - 508, UsedRamps = 12,
                Slots = new[] { "WheelL1", "WheelL2", "WheelR1", "WheelR2" }, WearsAVehicleDef = false,
            },
        };

        static VehicleRigFleet.Vehicle Entry(Baked b) => VehicleRigFleet.Get(b.Key);

        static VehicleMeshDef LoadMesh(Baked b)
        {
            VehicleRigFleet.Vehicle v = Entry(b);
            var def = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(v.MeshAssetPath);
            Assert.That(def, Is.Not.Null,
                $"{v.MeshAssetPath} did not load as a VehicleMeshDef. Re-run " +
                "Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake ALL road-vehicle meshes.");
            return def;
        }

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        // =============================================================================================
        //  1. WHAT IS BAKED, AND WHAT IS NOT
        // =============================================================================================

        [Test]
        public void EveryRoadFleetBodyIsBaked_AndHerAssetsAreOnDisk([ValueSource(nameof(Bodies))] Baked b)
        {
            Assert.That(VehicleRigFleet.Baked, Contains.Item(b.Key));
            Assert.That(VehicleRigFleet.NotBaked.ContainsKey(b.Key), Is.False,
                $"'{b.Key}' cannot be both baked and excused.");

            VehicleMeshDef def = LoadMesh(b);
            Assert.That(def.Id, Is.EqualTo(Entry(b).MeshId), "her mesh id is append-only.");
            Assert.That(def.IsUsable(), Is.True,
                $"'{b.Key}' baked a def that is not usable — she would be REFUSED at install and " +
                "never drawn. The usual cause is a ramp count over the facet shader's 16.");
        }

        /// <summary>
        /// ⚠️⚠️ <b>The hightop van is the ONE body of her drop that is not baked, and the reason is
        /// not a bake problem.</b> Her sidecar's <c>derivedFromRigSha256</c> does not pin her rig, so
        /// the geometry this bake now reads out of that document — her drive door, her solid box, her
        /// seats — may have been cut from a different shape.
        ///
        /// <para>Asserted in BOTH directions, which is the shape that keeps a ledger from rotting:
        /// she is refused AND she is out of <see cref="VehicleRigFleet.Baked"/>, and the day the
        /// re-stamp lands <c>VehicleRigFleetTests</c> goes red on "this is no longer refused" and
        /// both entries get deleted rather than reworded.</para>
        /// </summary>
        [Test]
        public void TheVanAloneIsUnbaked_AndOnlyBecauseHerSidecarDoesNotPinHerRig()
        {
            Assert.That(VehicleRigFleet.SidecarHashRefused.Keys, Is.EquivalentTo(new[] { "hightopVan" }),
                "the refusal ledger changed. Every entry blocks a bake, so an addition needs its " +
                "measurement and a removal needs the re-stamp that justified it.");

            Assert.That(VehicleRigFleet.NotBaked.Keys, Is.EquivalentTo(new[] { "hightopVan" }),
                "something other than the van is unbaked. The other eight bodies of the road-fleet " +
                "drop are baked by this PR; if one came back out, its reason belongs in NotBaked.");

            Assert.That(VehicleRigFleet.Baked, Does.Not.Contain("hightopVan"));

            // And the bake itself refuses her, rather than relying on a table nobody consults.
            var refused = Assert.Throws<InvalidOperationException>(
                () => VehicleMeshAssetBaker.Bake(VehicleRigFleet.Get("hightopVan")),
                "Bake() accepted a vehicle whose sidecar does not pin her rig. The ledger keeps her " +
                "out of Baked, but a direct call bypasses it — and what the bake reads from a " +
                "sidecar is exactly what a stale one gets wrong.");
            StringAssert.Contains("does not pin her rig", refused.Message);
        }

        // =============================================================================================
        //  2. THE ARTICULATION — the wheels are genuinely OUT of the body
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Every fitting exists, carries its own mesh and turns about its own hub.</b>
        ///
        /// <para>This is the assertion that catches the real defect of 2026-08-17: a static
        /// initialisation slip left the axis list empty, the body took all 1153 faces, and the bake
        /// reported a clean partition. A truck whose wheels are welded on partitions perfectly.</para>
        /// </summary>
        [Test]
        public void EveryFittingWasLiftedOutOfTheBody_WithItsOwnMeshAndPivot(
            [ValueSource(nameof(Bodies))] Baked b)
        {
            VehicleMeshDef def = LoadMesh(b);

            Assert.That(def.Wheels, Is.Not.Null.And.Length.EqualTo(b.Slots.Length),
                $"'{b.Key}' baked {def.Wheels?.Length ?? 0} fittings, not {b.Slots.Length}. On the " +
                "semis and the 53-ft trailers a rear side is a TANDEM split into two by station " +
                "window, so a count that dropped to one per side means the windows stopped " +
                "separating the axles.");

            Assert.That(def.Wheels.Select(f => f.Slot).ToArray(), Is.EquivalentTo(b.Slots));

            foreach (VehicleFitment f in def.Wheels)
            {
                Assert.That(f.Prop, Is.Not.Null, $"fitting '{f.Slot}' has no baked mesh def.");
                Assert.That(f.Prop.IsUsable(), Is.True, $"fitting '{f.Slot}' is not usable.");
                Assert.That(f.Prop.Mesh.vertexCount, Is.GreaterThan(0),
                    $"fitting '{f.Slot}' baked an EMPTY mesh — its pose axis claimed no faces.");
                Assert.That(f.Prop.PivotLocalMeters, Is.Not.EqualTo(Vector3.zero),
                    $"fitting '{f.Slot}' turns about the vehicle ORIGIN, which swings it through the " +
                    "machine. Its pivot must be its own hub centre.");
                Assert.That(f.Prop.CellW, Is.EqualTo(b.CellW),
                    $"fitting '{f.Slot}' baked at a different cell from her body — they are drawn " +
                    "through one camera and must agree by construction.");
            }
        }

        /// <summary>
        /// The body kept exactly what does not articulate. Asserted as a COUNT against the rig's own
        /// face list minus what the fittings took, so a fitting silently claiming body geometry (or
        /// the reverse) shows up as a number rather than as a picture nobody looked at.
        /// </summary>
        [Test]
        public void TheBodyKeptExactlyWhatDoesNotArticulate([ValueSource(nameof(Bodies))] Baked b)
        {
            VehicleMeshDef def = LoadMesh(b);
            VehicleRigFleet.Vehicle v = Entry(b);

            int fittingTris = def.Wheels.Sum(f => f.Prop.Mesh.triangles.Length / 3);
            int bodyTris = def.Mesh.triangles.Length / 3;

            Assert.That(bodyTris, Is.GreaterThan(0), "the body baked empty.");
            Assert.That(fittingTris, Is.GreaterThan(0),
                "the fittings baked empty — every articulating face ended up welded into the body.");

            // ⭐ The conservation check, through the SAME builder the bake used: extract her whole
            // face list, build it unsplit, and require the split meshes to add up to it. Triangles
            // rather than faces because RigMeshBuilder fans each face — but the total is conserved,
            // and that is exactly what the partition assert is about. Overlapping groups draw a
            // wheel twice; a gap drops geometry, and both are invisible in a picture.
            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = RigMeshExtractor.ExtractFrom(
                host, v.ScriptPath, v.GlobalName, hull: v.Extraction);

            Assert.That(data.Faces.Count, Is.EqualTo(b.Faces),
                $"'{b.Key}' now builds a different number of faces than the drop measured. Every " +
                "number in this fixture was taken at that face list — re-measure, do not nudge.");

            RigMeshBuild whole = RigMeshBuilder.Build(data, $"{b.Key}WholeRigCheck");
            try
            {
                int rigTris = whole.Mesh.triangles.Length / 3;
                Assert.That(bodyTris + fittingTris, Is.EqualTo(rigTris),
                    $"'{b.Key}': body {bodyTris} + fittings {fittingTris} triangles against the " +
                    $"unsplit rig's {rigTris}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(whole.Mesh);
            }
        }

        /// <summary>Front wheels steer AND roll, rears only roll, knuckles only steer; and the side a
        /// fitting claims is the side its hub is actually on. A swapped pair steers the wrong wheel
        /// by the Ackermann inner angle — a 5° error that only shows at lock.</summary>
        [Test]
        public void EachFittingTakesTheRightMotionOnTheRightSide([ValueSource(nameof(Bodies))] Baked b)
        {
            VehicleMeshDef def = LoadMesh(b);

            foreach (VehicleFitment f in def.Wheels)
            {
                VehicleFitmentMotion expected =
                    f.Slot.StartsWith("Knuckle", StringComparison.Ordinal) ? VehicleFitmentMotion.SteerOnly
                    : f.Slot.StartsWith("WheelF", StringComparison.Ordinal) ? VehicleFitmentMotion.SteerAndRoll
                    : VehicleFitmentMotion.RollOnly;
                Assert.That(f.Motion, Is.EqualTo(expected), $"'{f.Slot}' takes the wrong motion.");

                // ⚠️ A towed body has no steer axis at all, so nothing on her may claim one.
                if (b.Pick != null)
                    Assert.That(f.Motion, Is.EqualTo(VehicleFitmentMotion.RollOnly),
                        $"'{f.Slot}' on a TOWED body claims a steering motion. She is dragged — the " +
                        "rig resolves no steer axis and publishes no lock angles, which is the " +
                        "measurement VehicleKinds.IsDrivable(TowedBody) is written from.");

                // Left/Right is read off the slot's own trailing side letter, before any digit.
                char side = f.Slot.TrimEnd('1', '2', '3', '4')[^1];
                Assert.That(f.Side,
                    Is.EqualTo(side == 'L' ? VehicleFitmentSide.Left : VehicleFitmentSide.Right),
                    $"'{f.Slot}' is on the wrong side.");
                Assert.That(f.Prop.PivotLocalMeters.x, side == 'L' ? Is.LessThan(0f) : Is.GreaterThan(0f),
                    $"'{f.Slot}' claims the {(side == 'L' ? "street" : "curb")} side but its hub is on " +
                    "the other one. Rig +x is the curb side.");
            }
        }

        /// <summary>
        /// ⭐⭐ <b>Every pivot and every station window this repo DECLARES is the rig's own number.</b>
        ///
        /// <para><c>VehicleRigFleet</c> types the axle stations, the track half-widths and the wheel
        /// radii as C# literals — the Otter's precedent, and the right one, because generating eight
        /// near-identical entries from a table beats writing them out. But a typed number is a second
        /// transcription, and this is the test that keeps it honest: each is compared against the
        /// value the rig publishes, in the repo's own V8.</para>
        /// </summary>
        [Test]
        public void EveryDeclaredPivotIsTheRigsOwnNumber([ValueSource(nameof(Bodies))] Baked b)
        {
            VehicleRigFleet.Vehicle v = Entry(b);
            using IRigScriptHost host = Host(b);

            float wheelR = (float)host.EvaluateNumber($"{b.Global}.G.wheelR");

            foreach (VehicleRigFleet.Axis a in v.Axes)
            {
                Assert.That(a.Pivot.z, Is.EqualTo(wheelR).Within(1e-4f),
                    $"'{a.Slot}' turns about z {a.Pivot.z}, but the rig's rolling radius is {wheelR}. " +
                    "A hub off its own axle height lifts or buries the wheel.");

                float expectedX = b.Pick != null
                    // A towed body's wheels are the dual pair's mean; she has no front axle.
                    ? (float)host.EvaluateNumber($"({b.Global}.G.dualXi + {b.Global}.G.dualXo) / 2")
                    : a.Slot.Contains("F")
                        ? (float)host.EvaluateNumber($"{b.Global}.G.frontWX")
                        : (float)host.EvaluateNumber($"({b.Global}.G.dualXi + {b.Global}.G.dualXo) / 2");

                Assert.That(Mathf.Abs(a.Pivot.x), Is.EqualTo(expectedX).Within(1e-4f),
                    $"'{a.Slot}' sits {Mathf.Abs(a.Pivot.x)} m off the centreline; the rig puts that " +
                    $"hub at {expectedX}.");
            }

            // And every declared station is one the rig actually has geometry at.
            float[] measured = RigStations(host, b).ToArray();
            foreach (float station in DeclaredRearStations(v))
                Assert.That(measured.Any(m => Mathf.Abs(m - station) <= 0.011f), Is.True,
                    $"'{b.Key}' declares a rear axle station at y = {station}, which the rig's own " +
                    $"rolling geometry does not sit at (it has {string.Join(", ", measured)}). A " +
                    "window centred off its axle takes part of a wheel and leaves the rest in the " +
                    "body.");
        }

        /// <summary>
        /// ⚠️ <b>The station windows separate the tandems, with no overlap and no gap.</b> Measured
        /// through the rig rather than trusted: on the semis and the 53-ft trailers ONE roll axis
        /// moves two axles, and no side filter can separate them because they share a side.
        ///
        /// <para>An overlap makes two fittings claim one wheel; a gap leaves faces unclaimed, which
        /// the baker's <c>BodyMustNotMove</c> probe reports rather than shipping. Both are checked
        /// here at the declared window rather than only in the baker, so a window edited to a value
        /// that happens to still partition — but no longer clears its neighbour's geometry —
        /// fails.</para>
        /// </summary>
        [Test]
        public void TheStationWindowsClaimExactlyOneAxleEachWithNoGap(
            [ValueSource(nameof(Bodies))] Baked b)
        {
            VehicleRigFleet.Vehicle v = Entry(b);
            using IRigScriptHost host = Host(b);

            foreach (string probe in v.Axes.Select(a => a.Probe).Distinct())
            {
                VehicleRigFleet.Axis[] sharing = v.Axes.Where(a => a.Probe == probe).ToArray();
                int moved = (int)host.EvaluateNumber($"__movedSet({probe}).length");

                int claimed = 0;
                foreach (VehicleRigFleet.Axis a in sharing)
                    claimed += (int)host.EvaluateNumber(
                        $"__windowCount({probe}, {JsNumber(a.SideSign)}, " +
                        $"{JsNumber(a.YMin)}, {JsNumber(a.YMax)})");

                Assert.That(claimed, Is.EqualTo(moved),
                    $"'{b.Key}': the {sharing.Length} fitting(s) on probe {probe} claim {claimed} of " +
                    $"the {moved} faces it moves. Fewer means a GAP — geometry that articulates and " +
                    "belongs to no fitting, which would be baked frozen into the body. More means an " +
                    "OVERLAP, and a wheel drawn twice.");
            }
        }

        // =============================================================================================
        //  3. THE CELL, THE CHASSIS AND THE AZIMUTH — each body's OWN, never the pack's
        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>Each body baked at HER cell.</b> Three different cells ship in this drop — the
        /// pack's 384×320, the conventional box's 448×352 and the 53-footers' 640×480 — and every one
        /// of the nine rigs publishes a plausible cell, so a bake reading the wrong one produces a
        /// picture that is merely mis-framed rather than obviously broken.
        ///
        /// <para>⚠️ The trailer rig's GLOBAL carries the LONG cell, because a 16.15 m trailer needs
        /// it. So a per-file read gives the two pups a 640×480 cell and a pivot 86 px below their
        /// ground row — the fourth place in this drop where a missing pick does not throw.</para>
        /// </summary>
        [Test]
        public void EachBodyBakedAtHerOwnCell([ValueSource(nameof(Bodies))] Baked b)
        {
            VehicleMeshDef def = LoadMesh(b);

            Assert.That(def.CellW, Is.EqualTo(b.CellW), $"'{b.Key}' baked at the wrong cell WIDTH.");
            Assert.That(def.CellH, Is.EqualTo(b.CellH), $"'{b.Key}' baked at the wrong cell HEIGHT.");
            Assert.That(def.PivotPx.x, Is.EqualTo(b.PivotX).Within(1e-3f));
            Assert.That(def.PivotPx.y, Is.EqualTo(b.PivotY).Within(1e-3f));
            Assert.That(def.PxPerMetre, Is.EqualTo(32), "the pack bakes at 32 px = 1 m.");
            Assert.That(def.ElevationDeg, Is.EqualTo(40f).Within(0.01f),
                "she must be projected through the same camera as the boat fleet, or a truck and a " +
                "boat in one scene are drawn from different elevations.");
        }

        /// <summary>The chassis on the def is the chassis the RIG draws, read off it at bake time and
        /// never typed. Compared here against the rig's own expressions, evaluated fresh.</summary>
        [Test]
        public void TheChassisIsTheRigsOwn([ValueSource(nameof(Bodies))] Baked b)
        {
            VehicleRigFleet.Vehicle v = Entry(b);
            VehicleMeshDef def = LoadMesh(b);
            using IRigScriptHost host = Host(b);

            VehicleRigFleet.VehicleChassisSource src = v.ChassisSource;
            Assert.That(src, Is.Not.Null, $"'{b.Key}' declares no ChassisSource.");

            void Same(string expr, float actual, string what) =>
                Assert.That(actual, Is.EqualTo((float)host.EvaluateNumber(expr)).Within(1e-3f),
                    $"'{b.Key}' baked a {what} the rig does not publish — `{expr}`.");

            Same(src.Wheelbase, def.WheelbaseMeters, "wheelbase");
            Same(src.FrontTrack, def.FrontTrackMeters, "front track");
            Same(src.WheelRadius, def.WheelRadiusMeters, "wheel radius");
            Same(src.FrontAxleY, def.FrontAxleY, "front axle y");
            Same(src.RearAxleY, def.RearAxleY, "rear axle y");
            Same(src.MaxInnerDeg, def.MaxInnerSteerDegrees, "inner lock");
            Same(src.MaxOuterDeg, def.MaxOuterSteerDegrees, "outer lock");
            Same(src.TravelFront, def.SuspensionTravelFrontMeters, "front travel");
            Same(src.TravelRear, def.SuspensionTravelRearMeters, "rear travel");

            if (b.Pick != null)
            {
                Assert.That(def.MaxInnerSteerDegrees, Is.EqualTo(0f),
                    "a towed body baked a steering lock. She has no steer axis — the zero is a " +
                    "measurement, and a non-zero one means somebody gave a trailer Ackermann angles.");
                Assert.That(def.SuspensionTravelFrontMeters, Is.EqualTo(0f),
                    "her FRONT travel is non-zero. A trailer's suspension pivots at the KINGPIN — the " +
                    "coupling plane holds 1.18 m while the tail drops — so the front reference " +
                    "travels zero by construction, and a value here means the geometry changed kind.");
                Assert.That(def.SuspensionTravelRearMeters, Is.GreaterThan(0f),
                    "her REAR travel is zero, so nothing about her would move under load at all.");
            }
        }

        /// <summary>
        /// ⚠️ <b>Counter-clockwise on all eight</b> — the single most load-bearing field on the asset.
        /// Flip it and she drives backwards at east and west. Measured at bake time from the rig's own
        /// anchors and re-measured here, including the pair a TOWED body actually has: she publishes
        /// no <c>wheelFL</c> and no <c>hoodLatch</c> at all, so the road pack's names would read
        /// <c>undefined</c>.
        /// </summary>
        [Test]
        public void AzimuthIsCounterClockwise_ByTheAnchorsThisBodyActuallyHas(
            [ValueSource(nameof(Bodies))] Baked b)
        {
            VehicleRigFleet.Vehicle v = Entry(b);
            VehicleMeshDef def = LoadMesh(b);
            using IRigScriptHost host = Host(b);

            Assert.That(def.AzimuthCounterClockwise, Is.True,
                $"'{b.Key}' baked CLOCKWISE. Every analytic oracle on this pack says otherwise, so " +
                "the asset disagrees with the art it came from and she will drive stern-first at " +
                "E/W. Re-bake rather than editing the field.");
            Assert.That(def.ZeroHeadingDegrees, Is.EqualTo(0f));

            string opts = string.IsNullOrEmpty(v.Extraction.ViewOptions) ? "{}" : v.Extraction.ViewOptions;
            string l = v.AzimuthAbeamLeftAnchor, r = v.AzimuthAbeamRightAnchor;

            Assert.That(host.EvaluateBool(
                    $"(function(a){{return !!a.{l} && !!a.{r} && !!a.{v.AzimuthAftAnchor} && " +
                    $"!!a.{v.AzimuthForeAnchor};}})({b.Global}.anchors(0,{opts}))"),
                Is.True,
                $"'{b.Key}' does not publish all four anchors the bake reads — {l}/{r} abeam and " +
                $"{v.AzimuthAftAnchor}→{v.AzimuthForeAnchor} centreline. ⚠️ An oracle that quietly " +
                "stops applying leaves ONE, and one oracle is a coin flip on mirroring.");

            // The ground-plane un-squash: screen depth is scaled by sin(elevation), and only the
            // correct divisor lands the eight headings on exact 45° steps.
            string sin = Math.Sin(40.0 * Math.PI / 180.0)
                             .ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            Assert.That(host.EvaluateNumber(
                    $"(function(a){{return Math.atan2((a.{r}.y-a.{l}.y)/{sin}," +
                    $"a.{r}.x-a.{l}.x)*180/Math.PI;}})({b.Global}.anchors(2,{opts}))"),
                Is.EqualTo(-90d).Within(1e-6),
                "the abeam ground bearing at a quarter turn is no longer −90.00°.");

            Assert.That(host.EvaluateNumber(
                    $"(function(a){{return a.{v.AzimuthForeAnchor}.x - a.{v.AzimuthAftAnchor}.x;}})" +
                    $"({b.Global}.anchors(2,{opts}))"),
                Is.LessThan(0d),
                "the centreline nose dx went positive at a quarter turn, which reads CLOCKWISE and " +
                "disagrees with the abeam pair.");
        }

        // =============================================================================================
        //  4. THE PALETTE — and the night lamp the slot-reuse ruling rests on
        // =============================================================================================

        [Test]
        public void TheRampTableFitsTheShader_AndTheDefaultRampIsIndexZero(
            [ValueSource(nameof(Bodies))] Baked b)
        {
            VehicleMeshDef def = LoadMesh(b);

            Assert.That(def.Ramps.Length, Is.EqualTo(b.UsedRamps),
                $"'{b.Key}' baked a different number of ramps. Under, and something stopped painting; " +
                "over, and she is unplaceable.");
            Assert.That(def.Ramps.Length, Is.LessThanOrEqualTo(ShaderRampCap),
                $"'{b.Key}' is over the facet shader's float4[16] _RampMeta. Do NOT widen it — 16 is " +
                "a fleet law guarded in three places; measure which ramps could merge and take it " +
                "upstream, the way the Otter's cockpit mat was folded into her mesh.");
            foreach (HullMeshDef.Ramp ramp in def.Ramps)
                Assert.That(ramp.Colors, Is.Not.Null.And.Not.Empty,
                    "an empty ramp renders flat magenta on a vehicle the owner is driving.");
        }

        /// <summary>
        /// ⭐⭐ <b>THE NIGHT-LAMP SLOT-REUSE GUARD (ruled on #668, 2026-08-27).</b>
        ///
        /// <para><c>head</c> (the unlit lens) and <c>glow</c> (the lit one) are two forms of ONE lamp.
        /// Each build on its own paints 15 or 16 ramps and fits; a single mesh carrying both would be
        /// SEVENTEEN on the two box trucks and the classic semi, one over the shader's
        /// <c>float4[16]</c>.</para>
        ///
        /// <para>The ruling was slot reuse rather than a merge — the two ramps are visibly different
        /// colours (a grey-blue lens against a green-white glow) and merging them would lose the night
        /// read, which is the point of the axis — and <b>the whole ruling rests on one measured fact:
        /// no build ever names both.</b> This is that fact, asserted over every build of every rig in
        /// the pack rather than over the day and night poses alone. If a build ever names both, the
        /// pack needs a real merge or a new ruling, and it must not be a bake that finds out.</para>
        /// </summary>
        [Test]
        public void NoBuildEverNamesBothTheUnlitAndTheLitLamp(
            [ValueSource(nameof(RoadRigsWithBuilds))] RoadRigBuilds r)
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(r.RigPath)), r.Global, new[] { "build", "makeMats" }, r.RigPath));
            host.Execute(RoadFleetKitProbeTests.Helpers(r.Global));

            foreach (string build in r.Builds)
            {
                bool head = host.EvaluateBool($"__used({build}).indexOf('head') >= 0");
                bool glow = host.EvaluateBool($"__used({build}).indexOf('glow') >= 0");

                Assert.That(head && glow, Is.False,
                    $"{r.Global} build {build} names BOTH 'head' and 'glow'. The slot-reuse ruling " +
                    "on #668 rests entirely on those two never co-occurring — one _RampMeta slot " +
                    "carries whichever the build names, with no colour merged and no uniform " +
                    "widened. A build that names both needs 17 slots and there are 16.");

                Assert.That(host.EvaluateNumber($"__used({build}).length"),
                    Is.LessThanOrEqualTo((double)ShaderRampCap),
                    $"{r.Global} build {build} is over the facet shader's cap on its own.");
            }
        }

        // =============================================================================================
        //  5. THE FACTS A FACE LIST DOES NOT CARRY
        // =============================================================================================

        /// <summary>
        /// ⚠️⚠️ <b>EVERY BODY IN THIS DROP DECLARES THAT SHE DOES NOT FLOAT, and the declaration is
        /// the point.</b>
        ///
        /// <para>This is the Otter's trap, inverted. In #581 the baker wrote geometry and chassis
        /// only, so a freshly-baked amphibian carried <c>FloatSink = 0</c>, <c>Floats</c> read false,
        /// and she drove into the water and never floated — with every existing test green, because
        /// they build their defs in code and none read the asset.</para>
        ///
        /// <para>Here zero is the CORRECT answer: a box truck sinks. So it is pinned as INTENDED
        /// rather than left as a default nobody looked at — the bake writes the flotation block from
        /// the sidecar on every run, and this asserts both that the numbers are zero AND that the
        /// sidecar genuinely publishes no <c>FLOAT</c> block. Without the second half the two cases
        /// are the same bytes.</para>
        /// </summary>
        [Test]
        public void SheDeclaresSheDoesNotFloat_AndHerSidecarIsWhySheDoesNot(
            [ValueSource(nameof(Bodies))] Baked b)
        {
            VehicleMeshDef def = LoadMesh(b);

            Assert.That(def.Floats, Is.False,
                $"'{b.Key}' reports that she floats. She is a road vehicle or a towed body; nothing " +
                "in this drop swims, and a def that says otherwise would put her in the water.");
            Assert.That(def.FloatSinkMeters, Is.EqualTo(0f));
            Assert.That(def.FloatDraftMeters, Is.EqualTo(0f));
            Assert.That(def.WatertightHalfBeamMeters, Is.EqualTo(0f),
                "a non-zero watertight half-beam turns the AFLOAT clamp on for a machine that never " +
                "is.");
            Assert.That(def.WatertightDeckHeightMeters, Is.EqualTo(0f));

            // …and the zero is MEASURED: her sidecar publishes no flotation at all.
            object root = DeckSidecarJson.Parse(File.ReadAllText(Full(Entry(b).SidecarPath)));
            Assert.That(DeckSidecarJson.Member(root, "FLOAT"), Is.Null,
                $"'{b.Key}'s sidecar now publishes a FLOAT block, so her four zeros are no longer a " +
                "measured absence — they are numbers the bake declined to read. Either she has " +
                "become an amphibian or the block is misplaced; both need looking at.");
        }

        /// <summary>
        /// <b>The drive door is the point her sidecar publishes, and the driver stays hidden.</b>
        ///
        /// <para>Read here by navigating the sidecar independently of the reader the bake uses — the
        /// parser is shared, the navigation is not, because navigation is the half that can be wrong
        /// about which block it is standing in.</para>
        ///
        /// <para>⚠️ <b>Every truck in this pack keeps her driver hidden, and that is the ART's call
        /// rather than a gap.</b> Their seats live inside a <c>CAB</c> — a room with a liner, a roof
        /// panel and glass that is opaque at 32 px/m — so a figure drawn there would be standing on
        /// the roofline. The Otter's <c>drive</c> interaction happens at a seat in her open cockpit
        /// and hers IS published. The bake reads that distinction off the documents rather than being
        /// told it per vehicle.</para>
        /// </summary>
        [Test]
        public void TheDriveDoorIsHerSidecarsAndHerDriverStaysHidden([ValueSource(nameof(Bodies))] Baked b)
        {
            VehicleMeshDef def = LoadMesh(b);
            object root = DeckSidecarJson.Parse(File.ReadAllText(Full(Entry(b).SidecarPath)));

            List<object> interact = DeckSidecarJson.AsArray(DeckSidecarJson.Member(root, "INTERACT"));
            Assert.That(interact, Is.Not.Null, $"'{b.Key}'s sidecar publishes no INTERACT block.");

            object drive = interact.FirstOrDefault(e =>
                string.Equals(DeckSidecarJson.String(DeckSidecarJson.Member(e, "id")), "drive",
                              StringComparison.Ordinal));

            if (b.Pick != null)
            {
                Assert.That(drive, Is.Null,
                    "a TOWED body publishes a 'drive' interaction. She is dragged; if the art side " +
                    "gave her a driver's position, VehicleKind.TowedBody needs revisiting rather " +
                    "than the bake quietly starting to read one.");
                Assert.That(def.DriveDoorLocal, Is.EqualTo(Vector2.zero),
                    "a towed body baked a drive door. VehicleDoor reads Vector2.zero as 'no door " +
                    "published', which is the honest answer for something with no cab.");
                Assert.That(def.ShowsDriver, Is.False);
                return;
            }

            Assert.That(drive, Is.Not.Null, $"'{b.Key}' publishes no way in.");
            List<object> reach = DeckSidecarJson.AsArray(DeckSidecarJson.Member(drive, "reach_point"));
            Assert.That(reach, Is.Not.Null.And.Count.GreaterThanOrEqualTo(2),
                "her drive reach_point is not a numeric point.");

            Assert.That(def.DriveDoorLocal.x, Is.EqualTo(DeckSidecarJson.Float(reach[0])).Within(1e-4f),
                "her drive door has drifted from INTERACT[id=drive].reach_point. It is used both to " +
                "get IN and to be put DOWN on getting out, so a wrong one puts the fisher in the " +
                "cab wall at both ends.");
            Assert.That(def.DriveDoorLocal.y, Is.EqualTo(DeckSidecarJson.Float(reach[1])).Within(1e-4f));
            Assert.That(def.DriveDoorLocal, Is.Not.EqualTo(Vector2.zero),
                "her drive door baked to zero, which VehicleDoor reads as 'not published' — a " +
                "refusal wearing the shape of a value.");

            // The seat: a CAB keeps its driver hidden. Asserted against WHY, not just against zero.
            Assert.That(DeckSidecarJson.Member(root, "CAB"), Is.Not.Null,
                $"'{b.Key}'s sidecar no longer describes a CAB. If her seats moved into the open, " +
                "she should start drawing her driver — and this test should be the one that says so.");
            Assert.That(DeckSidecarJson.Member(root, "SEATS"), Is.Null,
                $"'{b.Key}' now publishes a root SEATS array beside her CAB. The bake reads a driver " +
                "seat exactly when the drive interaction happens at one of those, so this would " +
                "silently start drawing a fisher inside a room.");
            Assert.That(def.ShowsDriver, Is.False,
                $"'{b.Key}' draws her driver. Her sidecar calls her compartment a CAB — a seated " +
                "interior with a liner and a roof panel — so a figure there stands on the roofline.");
        }

        /// <summary>
        /// The solid box is the art's own, and on a container sidecar it is read from THIS body's
        /// block. ⚠️ The box is deliberately not the mesh's bounds: every sidecar in the fleet carves
        /// parts out of it in as many words — mirrors that reach 2.60 m over a 2.14 m body, mudflaps
        /// that are rubber ("sweep them, do not collide them"), a flatbed headboard published as a
        /// separate addition.
        /// </summary>
        [Test]
        public void TheColliderBoxIsTheOneHerSidecarPublishes([ValueSource(nameof(Bodies))] Baked b)
        {
            VehicleMeshDef def = LoadMesh(b);
            object root = DeckSidecarJson.Parse(File.ReadAllText(Full(Entry(b).SidecarPath)));

            // ⚠️ Navigated to THIS body — the trailer sidecar carries four, under bodies.<pick>.
            object scope = root;
            if (b.Pick != null)
            {
                scope = DeckSidecarJson.Member(DeckSidecarJson.Member(root, "bodies"), b.Pick);
                Assert.That(scope, Is.Not.Null,
                    $"the trailer sidecar has no bodies.{b.Pick} block, so a bake reading it would " +
                    "have taken another trailer's box.");
            }

            object bbox = DeckSidecarJson.Member(
                DeckSidecarJson.Member(scope, "BODY"), "collider_bbox");
            Assert.That(bbox, Is.Not.Null, $"'{b.Key}' publishes no BODY.collider_bbox.");

            Assert.That(def.HasCollider, Is.True,
                $"'{b.Key}' baked no solid box although her sidecar publishes one.");

            void Range(string axis, float min, float max)
            {
                List<object> pair = DeckSidecarJson.AsArray(DeckSidecarJson.Member(bbox, axis));
                Assert.That(pair, Is.Not.Null.And.Count.GreaterThanOrEqualTo(2));
                Assert.That(min, Is.EqualTo(DeckSidecarJson.Float(pair[0])).Within(1e-4f),
                    $"'{b.Key}' collider {axis} low corner.");
                Assert.That(max, Is.EqualTo(DeckSidecarJson.Float(pair[1])).Within(1e-4f),
                    $"'{b.Key}' collider {axis} high corner.");
            }

            Range("x", def.ColliderMinMeters.x, def.ColliderMaxMeters.x);
            Range("y", def.ColliderMinMeters.y, def.ColliderMaxMeters.y);
            Range("z", def.ColliderMinMeters.z, def.ColliderMaxMeters.z);
        }

        // =============================================================================================
        //  6. THE DEFERRAL — recorded so it is lifted rather than forgotten
        // =============================================================================================

        /// <summary>
        /// ⚠️⚠️ <b>THE LANDING GEAR TELESCOPES, so it is baked INTO the body and raising it is PR 3's.</b>
        ///
        /// <para>The plan was a fitting. <c>TrailerIsoKitProbeTests</c> measures <c>gear</c> 1 → 0 as
        /// 24 faces at a per-vertex deviation of <b>0</b>, which reads as an exact rigid translation —
        /// one mesh at two positions, the way the Otter's <c>float</c> is. That fixture's deviation
        /// helper SKIPS vertices that did not move, which is right for what it measures and
        /// misleading about what its name says.</para>
        ///
        /// <para><b>Measured without the skip:</b> 16 <c>iron</c> shoe faces DO translate rigidly by
        /// exactly [0, 0, 0.78]; the other 8 <c>galv</c> faces are the leg tubes, and their top two
        /// vertices are PINNED at z 1.120 while their bottoms rise 0.130 → 0.910. The leg shortens.
        /// 16 of the 96 vertices never move at all.</para>
        ///
        /// <para>So one mesh plus an offset cannot reproduce it, and neither half works alone: applied
        /// to the whole set it lifts the leg tops off the frame; applied to the shoes it slides them
        /// up inside a leg still drawn at full extension. The gear bakes into the body at
        /// <c>gear:1</c> — parked, shoes grounded, the rig's own default and what a placed trailer is
        /// — and the raise needs a second body mesh or a two-part split, in the PR that couples her.</para>
        ///
        /// <para><b>Asserted in both directions.</b> The day the art side makes the whole assembly
        /// rigid, this goes red on "it is rigid now" and the deferral is lifted rather than
        /// outliving its reason.</para>
        /// </summary>
        [Test]
        public void TheLandingGearTelescopes_SoItIsBakedIntoTheBodyRatherThanLiftedOut()
        {
            Baked pup = Bodies.First(x => x.Key == "trailerFlatbed28");
            using IRigScriptHost host = Host(pup);

            Assert.That(host.EvaluateNumber("__movedSet({gear:0}).length"), Is.EqualTo(24d),
                "the landing gear changed size.");

            Assert.That(host.EvaluateNumber("__pinnedVerts({gear:0})"), Is.EqualTo(16d),
                "the landing gear no longer has vertices that stay put while others move. If the " +
                "whole assembly now translates rigidly, it CAN be lifted out as one fitting with an " +
                "offset — take that deliberately: add the axis back to " +
                "VehicleRigFleet.BuildTrailerAxes, put {gear:0} back in BodyMustNotMove, and delete " +
                "this test's reason for existing.");

            Assert.That(host.EvaluateNumber("__rigidDeviation({gear:0})"), Is.GreaterThan(0.5d),
                "measured over EVERY vertex of the moved faces rather than only the ones that " +
                "moved, the gear is now within half a metre of a rigid translation — which is what " +
                "the two-part story above says it is not.");

            // And no fitting claims it: it is in the body, deliberately.
            foreach (Baked b in Bodies.Where(x => x.Pick != null))
            {
                VehicleRigFleet.Vehicle v = Entry(b);
                Assert.That(v.Axes.Any(a => a.Probe.Contains("gear")), Is.False,
                    $"'{b.Key}' lifts the landing gear out as a fitting.");
                Assert.That(v.BodyMustNotMove.Any(m => m.Contains("gear")), Is.False,
                    $"'{b.Key}' asserts the body does not move under the gear axis. It does — the " +
                    "gear is baked into it on purpose.");
            }
        }

        /// <summary>
        /// ⭐ <b>The four towed bodies really are four different meshes.</b> The whole hazard of a
        /// container rig is that a missing pick produces a plausible trailer, so identity is compared
        /// rather than counted: two of these differ by only 13 faces.
        /// </summary>
        [Test]
        public void TheFourTowedBodiesBakedFourDifferentMeshes()
        {
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Baked b in Bodies.Where(x => x.Pick != null))
            {
                VehicleMeshDef def = LoadMesh(b);
                Bounds bounds = def.Mesh.bounds;
                string signature =
                    $"{def.Mesh.vertexCount}/{def.Mesh.triangles.Length}/" +
                    $"{bounds.center:F4}/{bounds.size:F4}/{def.CellW}x{def.CellH}";

                Assert.That(seen.ContainsKey(signature), Is.False,
                    $"'{b.Key}' baked geometry indistinguishable from '" +
                    (seen.TryGetValue(signature, out string other) ? other : "?") +
                    "'. Either the rig collapsed two bodies, or a pick did not reach one of the four " +
                    "places that carry it — the face expression, the cell scope, the view options or " +
                    "the rest pose — and both calls fell back to reefer53.");
                seen[signature] = b.Key;
            }
        }

        [Test]
        public void EveryFittingAssetIsCommitted([ValueSource(nameof(Bodies))] Baked b)
        {
            foreach (VehicleFitment f in LoadMesh(b).Wheels)
            {
                string path = AssetDatabase.GetAssetPath(f.Prop);
                Assert.That(path, Is.Not.Empty, $"'{f.Slot}' is not a committed asset at all.");
                Assert.That(File.Exists(path), Is.True,
                    $"'{f.Slot}' points at {path}, which is not on disk — it would load as null and " +
                    "draw a machine with no wheels.");
            }
        }

        /// <summary>
        /// The four road vehicles wear a <see cref="HiddenHarbours.Vehicles.VehicleDef"/> and the four
        /// towed bodies deliberately do not — every field on that asset is a DRIVEN machine's, and
        /// what a trailer needs to be placed and coupled is PR 3's to design.
        /// </summary>
        [Test]
        public void OnlyTheDrivenBodiesWearAVehicleDef([ValueSource(nameof(Bodies))] Baked b)
        {
            VehicleRigFleet.Vehicle v = Entry(b);

            if (!b.WearsAVehicleDef)
            {
                Assert.That(v.VehicleDefPath, Is.Null.Or.Empty,
                    $"'{b.Key}' is a towed body and now names a VehicleDef. If PR 3 gave her one, " +
                    "this expectation moves with it — but a def full of top speeds and steering " +
                    "authority she can never use is the '0 = not applicable' shape VehicleMeshDef's " +
                    "own class doc refuses.");
                return;
            }

            var def = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Vehicles.VehicleDef>(v.VehicleDefPath);
            Assert.That(def, Is.Not.Null,
                $"{v.VehicleDefPath} is missing — nothing can place or drive her.");
            Assert.That(def.Id, Is.EqualTo(v.VehicleId), "her id is append-only (CLAUDE.md §5).");
            Assert.That(def.Mesh, Is.Not.Null, "she wears no mesh.");
            Assert.That(def.Mesh.Id, Is.EqualTo(v.MeshId));
            Assert.That(def.IsUsable(), Is.True);
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        /// <summary>One rig and every build the game could place her in — the population the night-lamp
        /// guard sweeps. Per rig because the axes are per rig: a van has a sliding door and a roof
        /// height, a semi has skirts or a visor.</summary>
        public sealed class RoadRigBuilds
        {
            public string Global, RigPath;
            public string[] Builds;
            public override string ToString() => Global;
        }

        static readonly RoadRigBuilds[] RoadRigsWithBuilds =
        {
            new RoadRigBuilds
            {
                Global = "VanIso",
                RigPath = "docs/art/rigs/road-fleet-kit/hightop-van/vanIsoRig.js",
                Builds = new[] { "{}", "{night:true}", "{roof:'low'}", "{windows:true}",
                                 "{dFL:1,dFR:1,slide:1,barnL:1,barnR:1,hood:1}",
                                 "{mirrors:false,mudflaps:false,hitch:false}" },
            },
            new RoadRigBuilds
            {
                Global = "BoxIso",
                RigPath = "docs/art/rigs/road-fleet-kit/boxtruck-cabover/boxIsoRig.js",
                Builds = new[] { "{}", "{night:true}", "{rollup:1}", "{gate:1}", "{tilt:1}",
                                 "{dL:1,dR:1}", "{liftgate:false}", "{mirrors:false,mudflaps:false}" },
            },
            new RoadRigBuilds
            {
                Global = "ConvBoxIso",
                RigPath = "docs/art/rigs/road-fleet-kit/boxtruck-conventional/convBoxIsoRig.js",
                Builds = new[] { "{}", "{night:true}", "{rollup:1}", "{gate:1}", "{hood:1}",
                                 "{dL:1,dR:1}", "{liftgate:false}", "{fairing:false}" },
            },
            new RoadRigBuilds
            {
                Global = "AeroSemiIso",
                RigPath = "docs/art/rigs/road-fleet-kit/semi-aero/aeroSemiIsoRig.js",
                Builds = new[] { "{}", "{night:true}", "{dL:1,dR:1}", "{hood:1}", "{skirts:false}",
                                 "{mirrors:false,mudflaps:false}" },
            },
            new RoadRigBuilds
            {
                Global = "ClassicSemiIso",
                RigPath = "docs/art/rigs/road-fleet-kit/semi-classic/classicSemiIsoRig.js",
                Builds = new[] { "{}", "{night:true}", "{dL:1,dR:1}", "{hood:1}", "{visor:false}",
                                 "{mirrors:false,mudflaps:false}" },
            },
        };

        /// <summary>The rear axle stations this vehicle DECLARES — the y of every rear roll fitting,
        /// deduplicated.</summary>
        static IEnumerable<float> DeclaredRearStations(VehicleRigFleet.Vehicle v) =>
            v.Axes.Where(a => a.Motion == VehicleFitmentMotion.RollOnly)
                  .Select(a => a.Pivot.y)
                  .Distinct();

        /// <summary>The axle stations the RIG actually has rolling geometry at, rounded to a
        /// centimetre — clustered on a 0.5 m gap, the same way the two probe fixtures do it.</summary>
        static IEnumerable<float> RigStations(IRigScriptHost host, Baked b)
        {
            string probe = b.Pick != null ? "{wL:0.25}" : "{wRL:0.25}";
            string csv = host.EvaluateString($"__stationCentres({probe})");
            return string.IsNullOrEmpty(csv)
                ? Array.Empty<float>()
                : csv.Split(',').Select(s =>
                    float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
        }

        static string JsNumber(float f) =>
            float.IsPositiveInfinity(f) ? "Infinity"
            : float.IsNegativeInfinity(f) ? "-Infinity"
            : f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        static string JsNumber(int i) =>
            i.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// A host with this body's rig widened by the repo's OWN shim, its rest pose installed as the
        /// base of every probe, and the measuring apparatus on top.
        ///
        /// <para>⚠️ The rest pose is what carries the BODY on a container rig — the same merge the
        /// baker's partition does, for the same reason: <c>resolve({})</c> on the trailer rig is
        /// <c>reefer53</c>, so a probe written without it measures the default trailer four times and
        /// agrees with itself every time.</para>
        /// </summary>
        static IRigScriptHost Host(Baked b)
        {
            VehicleRigFleet.Vehicle v = Entry(b);
            var symbols = new List<string> { "build", "makeMats" };

            IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(v.ScriptPath)), b.Global, symbols, v.ScriptPath));

            host.Execute($@"
                var __R = {b.Global};
                function __base(){{ return {v.RestPose}; }}
                function __pose(o){{ var s = __base(); for (var k in o) s[k] = o[k]; return s; }}
                function __faces(o){{ return __R.build(__R.resolve(__pose(o))); }}

                function __differs(p, q){{
                  for (var k = 0; k < p.length; k++)
                    for (var c = 0; c < 3; c++) if (p[k][c] !== q[k][c]) return true;
                  return false;
                }}
                function __movedSet(pose){{
                  var A = __faces({{}}), B = __faces(pose), out = [];
                  for (var i = 0; i < A.length; i++) if (__differs(A[i].v, B[i].v)) out.push(i);
                  return out;
                }}
                function __centroid(i, axis){{
                  var p = __faces({{}})[i].v, c = 0;
                  for (var k = 0; k < p.length; k++) c += p[k][axis];
                  return c / p.length;
                }}
                // The baker's own claim rule: this axis's side filter AND its station window.
                function __windowCount(pose, sideSign, yMin, yMax){{
                  var set = __movedSet(pose), n = 0;
                  for (var i = 0; i < set.length; i++) {{
                    if (sideSign !== 0) {{
                      var cx = __centroid(set[i], 0);
                      if (sideSign < 0 ? cx >= 0 : cx < 0) continue;
                    }}
                    var cy = __centroid(set[i], 1);
                    if (cy < yMin || cy > yMax) continue;
                    n++;
                  }}
                  return n;
                }}
                function __stationCentres(pose){{
                  var set = __movedSet(pose), rows = {{}};
                  for (var i = 0; i < set.length; i++) {{
                    var y = Math.round(__centroid(set[i], 1) * 100) / 100;
                    rows[y] = 1;
                  }}
                  var ys = Object.keys(rows).map(Number).sort(function(p,q){{ return p-q; }});
                  if (!ys.length) return '';
                  var out = [], cur = [ys[0]];
                  for (var j = 1; j < ys.length; j++) {{
                    if (ys[j] - ys[j-1] > 0.5) {{ out.push(cur); cur = []; }}
                    cur.push(ys[j]);
                  }}
                  out.push(cur);
                  return out.map(function(st){{
                    return Math.round((st[0] + st[st.length-1]) / 2 * 100) / 100;
                  }}).join(',');
                }}
                // ⚠️ Vertices of the MOVED faces that did not themselves move. The probe fixtures'
                // deviation helper skips these; counting them is what separates a rigid translation
                // from a telescope.
                function __pinnedVerts(pose){{
                  var A = __faces({{}}), B = __faces(pose), set = __movedSet(pose), n = 0;
                  for (var g = 0; g < set.length; g++) {{
                    var p = A[set[g]].v, q = B[set[g]].v;
                    for (var k = 0; k < p.length; k++)
                      if (Math.abs(q[k][0]-p[k][0]) < 1e-12 &&
                          Math.abs(q[k][1]-p[k][1]) < 1e-12 &&
                          Math.abs(q[k][2]-p[k][2]) < 1e-12) n++;
                  }}
                  return n;
                }}
                // Max deviation from ONE common offset over EVERY vertex of the moved faces —
                // no skipping. 0 = a genuine rigid translation of the whole assembly.
                function __rigidDeviation(pose){{
                  var A = __faces({{}}), B = __faces(pose), set = __movedSet(pose);
                  var dx = null, worst = 0;
                  for (var g = 0; g < set.length; g++) {{
                    var p = A[set[g]].v, q = B[set[g]].v;
                    for (var k = 0; k < p.length; k++) {{
                      var d = [q[k][0]-p[k][0], q[k][1]-p[k][1], q[k][2]-p[k][2]];
                      if (dx === null) dx = d;
                      for (var c = 0; c < 3; c++) worst = Math.max(worst, Math.abs(d[c]-dx[c]));
                    }}
                  }}
                  return dx === null ? -1 : worst;
                }}");

            return host;
        }
    }
}
