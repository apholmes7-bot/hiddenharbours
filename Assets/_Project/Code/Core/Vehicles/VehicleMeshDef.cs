using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A rig-extracted 3D ROAD VEHICLE, as a committed asset (ADR 0035).</b> The sibling of
    /// <see cref="HullMeshDef"/> for things with wheels: the same facet render payload — mesh, the
    /// rig's palette ramps and lighting constants, its dither matrix, its cell geometry — plus the
    /// per-artwork facts a controller needs to POSE and DRIVE one.
    ///
    /// <para><b>Why a separate type rather than more fields on <see cref="HullMeshDef"/>.</b> The
    /// lead-architect's ruling on #548, and it is a ruling about what the fields MEAN rather than
    /// about tidiness. A hull def carries <c>RestingDraftMeters</c>, <c>WatertightDeckHeightMeters</c>,
    /// <c>WatertightHalfBeamMeters</c> and three rock amplitudes — every one of them an answer to
    /// "how does the sea move this?", and every one of them meaningless on a truck. A vehicle carries
    /// a wheelbase, a track, a lock angle and a suspension travel, which are meaningless on a boat.
    /// Folding the two together would give both a majority of fields that must be left at zero, and
    /// "0 = not applicable" is exactly the shape that lets a consumer read a draft off a truck and
    /// get a plausible answer.</para>
    ///
    /// <para><b>Why it lives in Core.</b> The same reasoning <see cref="HullMeshDef"/> gives: the
    /// Vehicles module poses her and the Art module draws her, neither may reference the other
    /// (CLAUDE.md rule 4), so the data they share lives where both can see it. It contains no URP
    /// type — Art converts it to its own runtime setup on install.</para>
    ///
    /// <para>⚠️ <b><see cref="AzimuthCounterClockwise"/> is MEASURED, never assumed — and a vehicle
    /// is the case where assuming would most likely be wrong.</b> The hull baker's pixel probe finds
    /// the bow BY TAPER (the narrower end), which is a fact about boats. A crew-cab dually is a box:
    /// her taper signal is meaningless, and the same heuristic has already been measured wrong on
    /// eighteen lobster hulls at a taper ratio of 1.040. So <see cref="Tools.RigBaking"/>'s vehicle
    /// baker takes the ANALYTIC answer from the rig's own front-axle abeam pair and refuses to bake
    /// on the taper alone. Measured 2026-08-17 on the Dually: bearing exactly −90.00° at a quarter
    /// turn, confirmed independently by her hitch→hood centreline pair (nose 202.24 px west) —
    /// counter-clockwise, the same convention as every boat rig.</para>
    /// </summary>
    public class VehicleMeshDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): vehiclemesh.snake_case.")]
        public string Id = "vehiclemesh.unnamed";

        [Tooltip("The rig source this was extracted from (repo-relative), for provenance and re-bakes.")]
        public string SourceRigPath = "";

        [Tooltip("The rig expression that produced the face list. A vehicle rig is a GENERATOR — her " +
                 "faces come from a private build(resolve({})) rather than a static F array — so " +
                 "which expression, at which pose, is part of what this asset means.")]
        public string SourceFaceBuilder = "";

        [Header("Geometry (extracted by RigMeshExtractor, built by RigMeshBuilder)")]
        [Tooltip("The BODY mesh — everything that does not articulate. RigMeshBuilder layout: flat " +
                 "per-face normals, UV0 = (materialId, faceBias b, depthBias db, 0).\n\n" +
                 "⚠️ The wheels are NOT in here. They articulate, so they are lifted out as fittings " +
                 "(see Wheels below) and this mesh would draw a second, static copy of them if the " +
                 "bake did not exclude them. Measured on the Dually: 661 body faces, 412 wheel and " +
                 "80 steering-knuckle faces, summing to her 1153 exactly.")]
        public Mesh Mesh;

        [Header("Shading (the rig's own pipeline, verbatim)")]
        [Tooltip("Palette ramp + offset per rig material (max 16 — the facet shader's _RampMeta).\n\n" +
                 "⚠️ A vehicle rig's MATS is an OBJECT KEYED BY NAME, not the index-ordered array the " +
                 "boat fleet's 'MATS order IS the baked material index' law assumes. The order here is " +
                 "the order the extractor resolved, with the rig's own default ramp ('paint') first — " +
                 "which is what the face packer requires, since it resolves an unknown material name " +
                 "to index 0.")]
        public HullMeshDef.Ramp[] Ramps = Array.Empty<HullMeshDef.Ramp>();

        [Tooltip("The rig's key light LN, normalised, in the rig's own right-handed frame.")]
        public Vector3 LightN = Vector3.forward;
        public float Gain = 1f;
        public float Bias = 0f;

        [Tooltip("The rig's 4×4 ordered-dither thresholds, already (v+0.5)/16, row-major [x*4+y].")]
        public float[] Bayer16 = Array.Empty<float>();

        public Color32 Keyline = new Color32(0, 0, 0, 255);

        [Header("Cell (the rig's screen geometry)")]
        [Tooltip("Cell pivot in pixels from the cell's TOP-LEFT — the rig's screen origin.")]
        public Vector2 PivotPx;
        public int PxPerMetre = 32;
        public int CellW, CellH;
        [Tooltip("The rig's bake elevation (degrees above the horizon; 40, the same camera the boat " +
                 "fleet is projected through — so a truck and a boat in one scene agree).")]
        public float ElevationDeg = 40f;

        [Header("Pose facts (per-artwork; MEASURED — never tuned, never transcribed)")]
        [Tooltip("MEASURED azimuth convention: true = the rig's dir argument turns the vehicle " +
                 "COUNTER-CLOCKWISE (dir d depicts compass heading −45°·d), so the compass→dir " +
                 "mapping negates.\n\n" +
                 "⚠️ Load-bearing: flipping it drives her backwards at E/W. Taken from the rig's own " +
                 "front-axle abeam pair, NOT from the silhouette taper — see the class doc.")]
        public bool AzimuthCounterClockwise = true;

        [Tooltip("The compass heading (degrees) the rig's dir 0 depicts. 0 for the Dually: her " +
                 "contract's facing_note says dir 0 (N) shows the TAIL, i.e. she faces north/away, " +
                 "which is the same zero every boat rig uses.")]
        public float ZeroHeadingDegrees = 0f;

        [Header("Chassis (read off the rig — the numbers the controller solves against)")]
        [Tooltip("Axle separation in metres (G.axF − G.axR). The kinematic bicycle model's whole " +
                 "length scale: yaw rate is v·tan(δ)/wheelbase.")]
        [Min(0.01f)] public float WheelbaseMeters = 1f;

        [Tooltip("Front track in metres (2·G.frontWX) — the Ackermann split's other term.")]
        [Min(0f)] public float FrontTrackMeters;

        [Tooltip("Rolling radius in metres (G.wheelR). One revolution covers 2π·r, so the wheel's " +
                 "roll rate is v/(2π·r) REVOLUTIONS per second.\n\n" +
                 "⚠️ NOT v/r. The rig's roll axes are in revolutions, cyclic with period 1 — the " +
                 "'angular velocity' reading is 2π ≈ 6.3× too fast, which is not subtle.")]
        [Min(0.01f)] public float WheelRadiusMeters = 0.42f;

        [Tooltip("Front and rear axle y, in rig metres (G.axF, G.axR). Kept as well as the wheelbase " +
                 "because the suspension's dz(y) interpolates between them by name.")]
        public float FrontAxleY, RearAxleY;

        [Tooltip("Peak steer of the INNER front wheel at full lock, degrees (the rig's STEER_MAX).")]
        [Min(0f)] public float MaxInnerSteerDegrees;

        [Tooltip("Peak steer of the OUTER front wheel at full lock, degrees — the Ackermann partner " +
                 "of the inner angle, so both front wheels stay tangent to one turn centre. Read off " +
                 "the rig (steer.maxOuterDeg), never re-derived at a call site.")]
        [Min(0f)] public float MaxOuterSteerDegrees;

        [Tooltip("Suspension travel in metres, front and rear (the rig's TF/TR). The rig applies " +
                 "dz(y) = −(susF·TF)·t − (susR·TR)·(1−t), t = (y − rearAxleY)/wheelbase, to the WHOLE " +
                 "body group and not to the wheels — so anything parented to the body (a rider, a " +
                 "crate in the bed) takes the same correction, and t extrapolates past both axles.")]
        [Min(0f)] public float SuspensionTravelFrontMeters, SuspensionTravelRearMeters;

        [Tooltip("Where the driver stands to open her door, in rig metres (+x curb side, +y nose) — the " +
                 "gameplay sidecar's INTERACT 'drive' reach_point. Used both to get IN and to be put " +
                 "DOWN on getting out, so the two can never drift apart.\n\n" +
                 "Measured art, not feel: the art side derives it to sit outside the door leaf's swept " +
                 "disc, so it is no more the owner's to tune than the wheelbase is. (0,0) means this " +
                 "machine publishes no driver's door and cannot be got into — the honest answer for a " +
                 "def baked before the field existed, and one VehicleDoor refuses rather than putting " +
                 "the player inside the cab wall.")]
        public Vector2 DriveDoorLocal;

        [Header("Wheels (the articulated fittings lifted out of the body mesh)")]
        [Tooltip("Every part that moves relative to the body, with the motion it takes. Written by " +
                 "the baker; the driver poses each one every LateUpdate.")]
        public VehicleFitment[] Wheels = Array.Empty<VehicleFitment>();

        /// <summary>
        /// The geometric turn radius at full lock, measured to the REAR AXLE CENTRE, in metres —
        /// derived from this def's own Ackermann angles rather than transcribed.
        ///
        /// <para><b>Why derived.</b> The Dually's sidecar publishes 8.29 m; her rig's own published
        /// lock angles give <b>8.3478 m</b> (and 10.198 m for the outer front wheel path against the
        /// sidecar's 10.15). The two disagree by 0.7% — a rounding artefact in a hand-transcription,
        /// harmless in itself, but a magic number at a call site is how it stops being harmless.
        /// This is the same quantity the sidecar means, computed from the numbers the rig actually
        /// poses the wheels at, so it cannot drift from what the player sees.</para>
        ///
        /// <para>The identity: <c>cot(δ) = (cot(inner) + cot(outer))/2</c> for the bicycle-model
        /// centre angle δ, and <c>R = wheelbase·cot(δ)</c> — which is identically
        /// <c>wheelbase/tan(inner) + track/2</c>. Verified against the rig 2026-08-17: the two
        /// front-wheel turn centres differ by exactly the 1.800 m front track, so the published pair
        /// really is Ackermann-consistent.</para>
        /// </summary>
        public float FullLockTurnRadiusMeters =>
            VehicleSteeringMath.TurnRadiusMeters(
                VehicleSteeringMath.BicycleSteerDegrees(MaxInnerSteerDegrees, MaxOuterSteerDegrees),
                WheelbaseMeters);

        /// <summary>
        /// True when this def carries everything the render path needs — a mesh, at least one
        /// non-empty ramp (≤ the shader's 16), a full 4×4 dither matrix, sane cell geometry and a
        /// chassis that can actually be driven. Mirrors <see cref="HullMeshDef.IsUsable"/>: an
        /// incomplete def must be REFUSED rather than drawn half-shaded, because a missing ramp
        /// renders as flat magenta on a vehicle the owner is driving.
        /// </summary>
        public bool IsUsable()
        {
            if (Mesh == null || Mesh.vertexCount == 0) return false;
            if (Ramps == null || Ramps.Length == 0 || Ramps.Length > 16) return false;
            for (int i = 0; i < Ramps.Length; i++)
                if (Ramps[i].Colors == null || Ramps[i].Colors.Length == 0) return false;
            if (Bayer16 == null || Bayer16.Length != 16) return false;
            if (PxPerMetre <= 0 || CellW <= 0 || CellH <= 0) return false;
            return WheelbaseMeters > 0f && WheelRadiusMeters > 0f;
        }
    }

    /// <summary>
    /// How one fitting on a vehicle moves relative to her body.
    ///
    /// <para><b>These are MEASURED categories, not a description of what a wheel is.</b> Each was
    /// found by building the rig's face list at two poses and keeping what moved — the technique
    /// <see cref="HullPropMeshDef.FixedMesh"/> documents. On the Dually that partitions her 1153
    /// faces exactly: 661 body, 103 per wheel × 4 roll groups, 40 per steering knuckle × 2.</para>
    /// </summary>
    public enum VehicleFitmentMotion
    {
        /// <summary>A front wheel: it yaws about its own vertical axis AND rolls about its axle.
        /// Both rotations pass through the hub centre, so they compose into one rotation about one
        /// pivot — which is why this needs no articulation machinery beyond
        /// <see cref="IHullPropRenderer"/>'s single local rotation.</summary>
        SteerAndRoll = 0,

        /// <summary>A front steering knuckle — the fender lip, hub cover and mudflap that swing with
        /// the corner but do not turn with the tyre. Steer only.</summary>
        SteerOnly = 1,

        /// <summary>A rear wheel (on the Dually, a dual pair driven by one roll axis). Roll only.</summary>
        RollOnly = 2,
    }

    /// <summary>Which side of the machine a fitting is on — the Ackermann split gives the two front
    /// wheels DIFFERENT angles, so a fitting has to know which one it takes.</summary>
    public enum VehicleFitmentSide
    {
        /// <summary>Street side (rig −x), the driver's side on this left-hand-drive truck.</summary>
        Left = 0,
        /// <summary>Curb side (rig +x).</summary>
        Right = 1,
    }

    /// <summary>
    /// One articulated part of a vehicle: the baked fitting, and which motion it takes.
    ///
    /// <para>The mesh itself rides <see cref="HullPropMeshDef"/> — deliberately reused rather than
    /// duplicated. That type is not really "a boat part": it is <i>a rig-baked rigid body with a
    /// pivot and a local rotation</i>, which is exactly what a wheel is, and it already carries the
    /// two-pose FixedMesh technique, the ramp table, the cell and the pivot. Reusing it means the
    /// Art side needs no new renderer at all — <c>IsoFacetPropRenderer</c> poses a wheel and an
    /// outboard through one seam. The name is a historical accident and renaming it is a repo-wide
    /// churn that would buy nothing; see ADR 0035.</para>
    /// </summary>
    [Serializable]
    public struct VehicleFitment
    {
        [Tooltip("Instance name — 'WheelFL', 'KnuckleFR'. Names the attachment slot, so a re-skin " +
                 "reconfigures in place rather than accumulating wheels.")]
        public string Slot;

        [Tooltip("The baked fitting: mesh, ramps, cell, and the point it turns about.")]
        public HullPropMeshDef Prop;

        public VehicleFitmentMotion Motion;
        public VehicleFitmentSide Side;
    }
}
