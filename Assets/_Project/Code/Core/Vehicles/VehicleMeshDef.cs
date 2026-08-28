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

        [Tooltip("Front track in metres (2·G.frontWX) — the Ackermann split's other term.\n\n" +
                 "On a SKID-STEERED machine there is no front axle to be the front of: this is her " +
                 "one track (the Otter's G.trackWidth, 1.26), and it is the differential's lever " +
                 "arm rather than an Ackermann term. Read it as TrackWidthMeters there.")]
        [Min(0f)] public float FrontTrackMeters;

        /// <summary>
        /// <b>The lateral separation of a pair of wheels</b>, in metres — the same measurement
        /// <see cref="FrontTrackMeters"/> holds, named for what a skid machine uses it as.
        ///
        /// <para>An alias rather than a second field, deliberately. One physical distance, measured
        /// off the rig once: an Ackermann machine levers her steer split against it and a skid
        /// machine levers her track split against it, but a def carrying BOTH numbers is a def whose
        /// two copies can drift, and there is no reading of the geometry under which they should
        /// differ. (The Otter has four axles at one track width — <c>WHEELS.track_width_m</c>, with
        /// <c>axle_spacing_m</c> published separately and used by nothing here.)</para>
        /// </summary>
        public float TrackWidthMeters => FrontTrackMeters;

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

        [Tooltip("Where the driver SITS AND IS SEEN, in rig metres (+x curb side, +y nose, +z up) — " +
                 "the reference point of her seat cushion, off the gameplay sidecar's own SEATS block. " +
                 "The Otter's is her front bench, seat_ref (0, 0.36, 0.76), the one the centre " +
                 "handlebar is aimed at.\n\n" +
                 "⚠️ PUBLISH IT ONLY FOR A SEAT IN THE OPEN — a cockpit, not a cab. This is not 'does " +
                 "she have a seat': nearly every machine does. The Dually has two buckets and a bench, " +
                 "and hers stay UNPUBLISHED, because her sidecar calls them a CAB — a seated interior " +
                 "with a liner, a roof panel and glass that is opaque at 32 px/m. A figure drawn there " +
                 "would be a fisher standing on the roofline. The Otter's sidecar calls hers a COCKPIT, " +
                 "'an open tub with two benches, not a room', and a driver in one is genuinely on " +
                 "screen.\n\n" +
                 "(0,0,0) = not published, and it is the honest answer for a hard-cab machine as much " +
                 "as for a def baked before the field existed. Both keep their driver hidden, exactly " +
                 "as every machine did before this field.\n\n" +
                 "Measured art, not feel (rule 6): it changes when the rig re-bakes and at no other " +
                 "time.")]
        public Vector3 DriverSeatLocal;

        /// <summary>
        /// <b>Does this machine show her driver?</b> Decided by the ART — a machine that publishes an
        /// open seat has somewhere a person is visibly sat, and one that does not, does not.
        ///
        /// <para>The same shape as <see cref="Floats"/> beside it, and for the same reason: the
        /// question "can she do this?" is answered by the geometry she actually carries, never by her
        /// <see cref="HiddenHarbours.Core.VehicleKind"/> and never by a rule in code. An open tub
        /// draws her driver; a hard cab does not; the day a hard-cab amphibian arrives, nothing here
        /// needs to learn about her — she simply publishes no open seat.</para>
        /// </summary>
        public bool ShowsDriver => DriverSeatLocal != Vector3.zero;

        [Header("Flotation (amphibious rigs only — every field 0 means she does not swim)")]
        [Tooltip("How far the WHOLE machine drops onto her waterline afloat, in rig metres (the " +
                 "rig's G.sinkMax; the Otter's 0.52).\n\n" +
                 "⚠️ MEASURED as an EXACT RIGID TRANSLATION, which is the single most useful fact " +
                 "about an amphibian: OtterIsoKitProbeTests measured every vertex's displacement at " +
                 "float=1 against one common offset and found a max deviation of 0. So floating her " +
                 "is a runtime Z offset on the DRY mesh — no second bake, no afloat variant, no " +
                 "reshape — and it is linear, so a partial float is a partial offset.")]
        [Min(0f)] public float FloatSinkMeters;

        [Tooltip("How much water she needs under her to float CLEAR of the bottom, in metres — her " +
                 "draft above the keel at full float (the Otter's 0.28: keel at rig z 0.24, sunk " +
                 "0.52, so 0.28 of her is below the surface).\n\n" +
                 "This is the swim threshold, and it is an ART fact rather than a feel one: it is " +
                 "the depth at which her hull genuinely takes her weight. The band either side of it " +
                 "— the hysteresis that stops the water's edge being a flicker line — IS feel, and " +
                 "lives on VehicleDef.")]
        [Min(0f)] public float FloatDraftMeters;

        [Tooltip("The WATERTIGHT clamp's line while AFLOAT: height above rig z = 0 (metres) of the " +
                 "lowest point the sea could come aboard over. The Otter's 0.82 — her transom top, " +
                 "which her sidecar publishes as downflooding.lowest_gunwale_point.\n\n" +
                 "⚠️ NOT her cockpit floor. That sits at rig z 0.44, i.e. 0.08 m BELOW the outside " +
                 "water once she is sunk 0.52 — and her sidecar says so, and says it is correct: " +
                 "'the tub is a sealed moulding, the floor is a pan inside it. Do not drain it.' A " +
                 "clamp aimed at the floor would be aimed below the waterline and would mean " +
                 "nothing. 0 = clamp off, which is what a ROAD vehicle carries and what the vehicle " +
                 "path has passed since #560.")]
        [Min(0f)] public float WatertightDeckHeightMeters;

        [Tooltip("The watertight clamp's half-beam reach while AFLOAT, in rig ground metres — the " +
                 "Otter's 0.687, off her sidecar's own waterline polygon at float 1.\n\n" +
                 "Her tires stand proud of that to 0.78 and her tracks to 0.83; the waterline " +
                 "polygon is what her HULL presents to the sea, and washing over a tire is correct " +
                 "(only the top 0.12 m of each 0.64 m tire shows afloat). Generous is a touch drier; " +
                 "too small re-opens far-rail flooding — the coordinator's run adjudicates it in " +
                 "pixels, as it does for the hull fleet.")]
        [Min(0f)] public float WatertightHalfBeamMeters;

        /// <summary>True when this rig carries the flotation facts an amphibian needs — a sink to
        /// pose her at and a draft to decide the swim on. A def with neither is a machine that
        /// cannot swim, whatever her <see cref="VehicleKind"/> says, and the drive model reads THIS
        /// rather than assuming the kind implies the geometry.</summary>
        public bool Floats => FloatSinkMeters > 0f && FloatDraftMeters > 0f;

        [Header("Hard points (the art's own solid box — placement and collision)")]
        [Tooltip("The low corner of the machine's solid bounding box, in rig metres (+x curb side, " +
                 "+y nose, +z up) — the gameplay sidecar's own BODY.collider_bbox, read at bake time.")]
        public Vector3 ColliderMinMeters;

        [Tooltip("The high corner of the same box.\n\n" +
                 "⚠️ It is the box the ART says is solid, and it is deliberately NOT the mesh's " +
                 "bounds. Every sidecar in the fleet carves parts out of it in as many words — the " +
                 "cabover's mirrors reach 2.60 m over against a 2.14 m body and are excluded, her " +
                 "mudflaps are rubber ('sweep them, do not collide them'), and a flatbed's headboard " +
                 "is published as a separate addition rather than folded in. A box taken off the " +
                 "mesh would collide with all of it.\n\n" +
                 "Min == Max means no box was published, which VehicleMeshDef.HasCollider reads as " +
                 "'unknown' rather than 'zero-sized'. Measured art, not feel (rule 6).")]
        public Vector3 ColliderMaxMeters;

        /// <summary>
        /// True when this def carries the art's solid box. The same shape as <see cref="Floats"/>
        /// and <see cref="ShowsDriver"/> beside it: the question is answered by the geometry she
        /// actually publishes, never by her <see cref="VehicleKind"/>.
        ///
        /// <para>A def baked before this field existed reads false, which is exactly what it was:
        /// nothing on the vehicle path built a collider from a def, and a consumer must fall back
        /// rather than collide with a zero-sized box at the origin.</para>
        /// </summary>
        public bool HasCollider =>
            ColliderMaxMeters.x > ColliderMinMeters.x &&
            ColliderMaxMeters.y > ColliderMinMeters.y &&
            ColliderMaxMeters.z > ColliderMinMeters.z;

        [Header("Doors (what the player works, and where the art says to stand)")]
        [Tooltip("Every INTERACT entry the art publishes that moves a fitting, with the fittings it " +
                 "moves. Empty on a machine with no worked openings.\n\n" +
                 "⚠️ Not one per fitting: the art publishes one 'barn' handle and a reefer has two " +
                 "leaves, one 'gear' crank and a trailer has shoes and legs. The player reaches for " +
                 "the handle that was drawn.")]
        public VehicleDoorGroup[] DoorGroups = Array.Empty<VehicleDoorGroup>();

        /// <summary>The group with this id, or false. Named lookup rather than an index because the
        /// ids are the art's own and a caller should ask for the one it means.</summary>
        public bool TryGetDoorGroup(string id, out VehicleDoorGroup group)
        {
            if (DoorGroups != null)
                for (int i = 0; i < DoorGroups.Length; i++)
                    if (string.Equals(DoorGroups[i].Id, id, StringComparison.Ordinal))
                    { group = DoorGroups[i]; return true; }
            group = default;
            return false;
        }

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

        /// <summary>A rear wheel (on the Dually, a dual pair driven by one roll axis). Roll only.
        ///
        /// <para>On a semi's tandem and a 53-ft trailer's, ONE roll axis drives two axles and the
        /// two are separated by an axle-station window rather than by a probe — so a rear fitting
        /// there is one axle of a pair, not one wheel. See <c>VehicleRigFleet.Axis.YMin</c>.</para>
        /// </summary>
        RollOnly = 2,

        /// <summary>
        /// ⭐ <b>A DOOR</b> — a leaf, a hood, or a whole tilting cab: one rigid body swinging about
        /// one published hinge, from shut to its published sweep.
        ///
        /// <para><b>Every one of these was measured before it was declared</b>, over the WHOLE vertex
        /// set of the moved faces — the landing gear's law, because a deviation helper that skips
        /// unmoved vertices measures the moved subset and will call a telescope rigid. Each returned
        /// a radius error of 0, an invariant-coordinate error of 0, and an angle spread of
        /// <b>0.000000</b>, with the recovered angle equal to the sidecar's published sweep to four
        /// decimals. A door that does not measure that way is NOT one of these — see the rollup
        /// (a different BUILD, not a pose) and the liftgate (a four-bar linkage).</para>
        ///
        /// <para>The hinge rides <see cref="VehicleFitment.HingeAxis"/> and
        /// <see cref="HullPropMeshDef.PivotLocalMeters"/>; the sweep rides
        /// <see cref="VehicleFitment.SweepDegrees"/>.</para>
        /// </summary>
        HingeRotation = 3,

        /// <summary>
        /// ⭐ <b>A part that SLIDES</b> — the van's curb-side door and a trailer's landing-gear
        /// shoes: an exact rigid translation at every pose, along a path the rig publishes.
        ///
        /// <para>Measured deviation <b>0 at every sample</b> on both. The van's is two-phase (she
        /// pops 0.085 m outboard, then runs 1.16 m aft), the shoes' is linear, and one sampled path
        /// carries both — see <see cref="VehicleFitment.SlidePath"/>. This is the shape the landing
        /// gear as a whole failed to be, which is why the shoes are here and the legs are
        /// <see cref="DiscreteStates"/>.</para>
        /// </summary>
        Slide = 4,

        /// <summary>
        /// ⚠️ <b>A part that is NOT rigid, baked at each end of its travel rather than faked.</b>
        ///
        /// <para>The trailer's landing-gear LEGS: 8 tubes whose top two vertices are pinned at
        /// z 1.120 while their bottoms rise 0.130 → 0.910. They neither rotate nor translate — they
        /// shorten. No pivot and no offset reproduces that, so the honest answer is a baked mesh at
        /// each end and a swap, and the swap lands at the END of the crank rather than half-way
        /// through it, which is also what a hand-cranked leg looks like.</para>
        ///
        /// <para>The meshes ride <see cref="VehicleFitment.StateProps"/>, one per named state.
        /// <see cref="VehicleFitment.Prop"/> is the first of them, so anything reading a fitting's
        /// mesh without knowing about states still gets a real one.</para>
        /// </summary>
        DiscreteStates = 5,
    }

    /// <summary>
    /// Which of the rig's own axes a <see cref="VehicleFitmentMotion.HingeRotation"/> turns about.
    /// Two occur in the fleet, and the sidecars name them in exactly these words.
    /// </summary>
    public enum VehicleHingeAxis
    {
        /// <summary>Not a hinge — what every wheel, knuckle, slide and state-swapped part carries.</summary>
        None = 0,

        /// <summary>The sidecars' <c>"kind": "vertical"</c> — rig +z, out of the road. A cab door, a
        /// van barn, a reefer's rear leaf: it swings in plan and its z never changes.</summary>
        Vertical = 1,

        /// <summary>The sidecars' <c>"kind": "x_axis"</c> — rig +x, the curb-side lateral. A hood or
        /// a tilting cab: it swings in elevation about a transverse pin and its x never changes.</summary>
        Lateral = 2,
    }

    /// <summary>Which side of the machine a fitting is on — the Ackermann split gives the two front
    /// wheels DIFFERENT angles, so a fitting has to know which one it takes.</summary>
    public enum VehicleFitmentSide
    {
        /// <summary>Street side (rig −x), the driver's side on this left-hand-drive truck.</summary>
        Left = 0,
        /// <summary>Curb side (rig +x).</summary>
        Right = 1,

        /// <summary>
        /// Neither — one assembly spanning the centreline. A trailer's landing gear is a leg each
        /// side, a crossbrace between them and a crank on the street side, raised by ONE axis as one
        /// body: splitting it by centroid would cut the crossbrace in half. Appended rather than
        /// folded into <see cref="Left"/> because the side a fitting claims is read as a fact — the
        /// Ackermann split hands the front wheels different angles by it, and the bake tests assert
        /// a fitting's hub really is on the side it names.
        /// </summary>
        Centre = 2,
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

        // ---- doors ---------------------------------------------------------------------------

        [Tooltip("For a HingeRotation fitting: which of the rig's axes the hinge turns about. The " +
                 "pin itself is the fitting's own PivotLocalMeters.\n\n" +
                 "None on everything else — a wheel's axes come from its motion, not from here.")]
        public VehicleHingeAxis HingeAxis;

        [Tooltip("For a HingeRotation fitting: the SIGNED sweep from shut to fully open, degrees, " +
                 "as the art publishes it.\n\n" +
                 "⚠️ THE FULL SWEEP, NOT THE SHORT WAY ROUND. A reefer's barn door is 255°, which " +
                 "reaches the same pose as −105° and gets there through an entirely different fan: " +
                 "the published keep_clear sweeps to FULL OUTBOARD (|x| 2.37 m) at 180° before " +
                 "folding back along the sides. A door animated the short way misses that volume " +
                 "completely — it would swing through whatever is parked alongside and arrive " +
                 "looking correct.")]
        public float SweepDegrees;

        [Tooltip("For a Slide fitting: where the part sits at each sampled pose fraction, measured " +
                 "off the rig at bake time and asserted to be an exact rigid translation at every " +
                 "sample. Interpolated between samples, which reproduces a piecewise-linear path " +
                 "exactly — the van's slide pops outboard before it runs aft, so no single vector " +
                 "describes it.")]
        public VehicleSlideSample[] SlidePath;

        [Tooltip("For a DiscreteStates fitting: one baked mesh per named state, in state order. " +
                 "The part is not rigid, so it is baked at each end of its travel and swapped " +
                 "rather than posed. StateNames[i] names StateProps[i].")]
        public HullPropMeshDef[] StateProps;

        [Tooltip("The state names, parallel to StateProps — 'down'/'up' on a landing gear's legs. " +
                 "Named rather than indexed so a caller asks for the state it means.")]
        public string[] StateNames;

        [Tooltip("⭐ The slot this fitting HANGS OFF, or empty for the body.\n\n" +
                 "One case in the fleet and it is load-bearing: a cabover's cab TILTS, and her two " +
                 "doors are cut out of that cab. The bake claims the doors first (so the tilting " +
                 "cab's own mesh excludes them, exactly as the steer axes leave the tyres to the " +
                 "roll axes), which would otherwise leave two doors hanging in the air when the cab " +
                 "goes over. Her sidecar says so in as many words — the door keep-clear arc 'RIDES " +
                 "THE TILT: a tilted cab carries its door arcs with it'.\n\n" +
                 "The driver composes parent-then-child, so a door on a tilted cab is its own swing " +
                 "applied within the cab's.")]
        public string ParentSlot;

        /// <summary>The part's position along its <see cref="SlidePath"/> at pose fraction
        /// <paramref name="t"/>, in rig metres. Clamped at both ends, and linear between samples —
        /// which is exact, because the samples were taken at the path's own corners.</summary>
        public Vector3 SlideOffsetAt(float t)
        {
            if (SlidePath == null || SlidePath.Length == 0) return Vector3.zero;
            if (t <= SlidePath[0].T) return SlidePath[0].OffsetMeters;

            for (int i = 1; i < SlidePath.Length; i++)
            {
                if (t > SlidePath[i].T) continue;
                float span = SlidePath[i].T - SlidePath[i - 1].T;
                float u = span <= 0f ? 1f : (t - SlidePath[i - 1].T) / span;
                return Vector3.Lerp(SlidePath[i - 1].OffsetMeters, SlidePath[i].OffsetMeters, u);
            }
            return SlidePath[SlidePath.Length - 1].OffsetMeters;
        }

        /// <summary>The index of a named state, or −1. Callers that get −1 must leave the fitting
        /// where it is rather than falling back to state 0 — a door asked for a state it does not
        /// have is a wiring bug, and snapping it shut hides one.</summary>
        public int StateIndex(string name)
        {
            if (StateNames == null) return -1;
            for (int i = 0; i < StateNames.Length; i++)
                if (string.Equals(StateNames[i], name, StringComparison.Ordinal)) return i;
            return -1;
        }
    }

    /// <summary>Which tunable paces a group — a door's hand or a gear's crank. They are separate
    /// numbers because a hand crank is not a door, and the kit's coupling discipline (couple, then
    /// wind the legs up before rolling) leans on the crank taking time.</summary>
    public enum VehicleDoorWork
    {
        Door = 0,
        LandingGear = 1,
    }

    /// <summary>
    /// <b>One thing the player works, and where they stand to work it</b> — the sidecar's own
    /// <c>INTERACT</c> entry, resolved to the fittings it moves.
    ///
    /// <para>A group rather than a fitting because the two do not correspond: the art publishes one
    /// <c>barn</c> interaction and a reefer has two leaves, one <c>gear</c> crank and a trailer has
    /// shoes and legs. The player reaches for the handle the art drew, and the fittings follow.</para>
    ///
    /// <para>⚠️ <b>The reach point is a REQUEST, not a promise</b> — the sidecars' own
    /// <c>_interact_notes</c> say so, and several add that a point is "NOT tested against ground
    /// colliders". It is where the art would like the player to stand; whether they can is the
    /// world's business, and a consumer that cannot honour it should say so rather than teleporting
    /// anyone.</para>
    /// </summary>
    [Serializable]
    public struct VehicleDoorGroup
    {
        [Tooltip("The sidecar's own INTERACT id — 'barn', 'slide', 'hood', 'tilt', 'gear'. Kept " +
                 "verbatim so a reader can find it in the art's document.")]
        public string Id;

        [Tooltip("The fitting slots this works, in the order the art lists them. A reefer's 'barn' " +
                 "moves two leaves; a trailer's 'gear' moves the shoes and the legs together.")]
        public string[] Slots;

        [Tooltip("Where the art would like the player to stand, in rig metres (+x curb, +y nose). " +
                 "Meaningful only when HasReachPoint — see the class doc on why it is a request.")]
        public Vector2 ReachPointLocal;

        [Tooltip("False when the art published no numeric point for this interaction. The trailer " +
                 "kit's 'couple' entry, for instance, carries PROSE there — 'the ACT is the tractor " +
                 "backing on' — because the act belongs to the other vehicle. Reading that as (0,0) " +
                 "would put a handle at the machine's own origin.")]
        public bool HasReachPoint;

        [Tooltip("Which tunable paces this group.")]
        public VehicleDoorWork Work;
    }

    /// <summary>One sample of a sliding part's path — where it sits at a given pose fraction.
    ///
    /// <para>A PATH rather than one offset because the van's slide is two-phase: she pops outboard
    /// before she runs aft, so no single vector describes her. Sampled off the rig at bake time and
    /// asserted to be an exact rigid translation at each sample, then interpolated — a measurement,
    /// not a model.</para>
    /// </summary>
    [Serializable]
    public struct VehicleSlideSample
    {
        [Tooltip("The pose fraction this offset was measured at (0 = shut or down, 1 = open or up).")]
        [Range(0f, 1f)] public float T;

        [Tooltip("Where the part sits at that fraction, as an offset in rig metres from where it " +
                 "was baked.")]
        public Vector3 OffsetMeters;
    }
}
