#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>THE ROAD VEHICLES — what the repo has, and what it does with each of them.</b>
    ///
    /// <para>The sibling of <see cref="HullMeshFleet"/> for things with wheels, and it exists because
    /// that table CANNOT cover them. Its coverage test scans <c>docs/art/rigs/</c> for rigs containing
    /// the signal <c>rollA</c> — a hull's sea-rock amplitude — and the Dually's rig has zero
    /// occurrences of it, correctly: a truck does not rock on a swell. So a vehicle rig dropped into
    /// the repo is <b>invisible</b> to the hull coverage law and would go silently unbaked, which is
    /// precisely the failure that law was written to prevent. Adding a vehicle to
    /// <see cref="HullMeshFleet.NotHulls"/> would be the wrong fix: it would assert that a truck is a
    /// boat-shaped rig we chose not to bake, and it would still not be scanned.</para>
    ///
    /// <para><b>The signal here is the SIDECAR, not the rig.</b> A road vehicle declares itself in its
    /// gameplay sidecar's top-level <c>"kind": "road_vehicle"</c> — art's own word for what the thing
    /// is, rather than a substring this file guesses at. Boat sidecars carry no top-level <c>kind</c>
    /// at all, so the two populations do not overlap.</para>
    ///
    /// <para><b>Every vehicle is either BAKED or carries a REASON.</b> A new drop that is neither fails
    /// <c>VehicleRigFleetTests</c>, which is the whole point of the table — art arrives by PR and this
    /// is the thing that stops one arriving unnoticed.</para>
    /// </summary>
    public static class VehicleRigFleet
    {
        /// <summary>
        /// Where a road vehicle's gameplay sidecar lives — a subfolder of the hulls', repo-relative.
        ///
        /// <para><b>⚠ A SUBFOLDER, AND IT IS LOAD-BEARING.</b> The first draft of this import put the
        /// Dually's sidecar straight into <c>docs/art/rigs/gameplay/</c> beside the boats', and it
        /// reddened five tests in <c>DeckSidecarImportParityTests</c> — that fixture enumerates
        /// <b>every</b> <c>*.gameplay.json</c> in that folder and requires each one to parse as a boat
        /// deck with a committed <c>BoatDeckDef</c> behind it. That law is right and has been
        /// working; the folder simply means "boat deck sidecars", which is why the reader is called
        /// <see cref="DeckSidecarReader"/>.</para>
        ///
        /// <para>A vehicle sidecar is a different document — <c>CAB</c>, <c>CARGO</c>,
        /// <c>THRESHOLD</c>, <c>WHEELS</c>, and the drop's own <c>_excluded</c> block says
        /// <i>"WASHBOARD: not a hull"</i> and <i>"CLEATS: not a hull"</i>. So it goes one level down
        /// rather than becoming an exception carved into a law that is otherwise literally true.
        /// <c>Directory.GetFiles</c> is not recursive, so the two populations stay separate by
        /// construction and neither fixture needs to know about the other.</para>
        /// </summary>
        public const string SidecarFolder = "docs/art/rigs/gameplay/vehicles";

        /// <summary>The top-level <c>kind</c> that marks a sidecar as this table's business.
        ///
        /// <para>⚠️ <b>Kept for the Dually, but it is NO LONGER the whole population.</b> The Otter
        /// ships <c>"kind": "amphibious_xtv"</c>, so a scan looking only for this string would not
        /// see her — and an amphibian landing unnoticed is precisely the failure this table exists
        /// to prevent. Ask <see cref="HiddenHarbours.Core.VehicleKinds"/> instead, which is the ONE
        /// place a shipped token becomes a kind.</para></summary>
        public const string RoadVehicleKind = "road_vehicle";

        /// <summary>
        /// Is this sidecar's top-level <c>kind</c> a vehicle at all? The population test, and the
        /// only one — boat sidecars carry no top-level <c>kind</c>, so the two cannot overlap.
        /// </summary>
        public static bool IsVehicleKindToken(string token) =>
            HiddenHarbours.Core.VehicleKinds.IsVehicleToken(token);

        /// <summary>
        /// <b>One pose axis that lifts a fitting out of the body mesh.</b>
        ///
        /// <para>A baked mesh is static geometry at ONE pose, so every part that articulates has to
        /// become its own mesh — otherwise the body draws a second, frozen copy of it. This says
        /// WHICH part by naming a probe pose: the faces that move when this axis alone moves are
        /// that fitting's. Measured, never transcribed.</para>
        ///
        /// <para>⚠️ <b>The order of these in <see cref="Vehicle.Axes"/> is load-bearing.</b> Each
        /// axis claims only what no earlier one took, so the SPECIFIC axes come first: a steer axis
        /// moves the wheel AND its knuckle, so the per-wheel roll axes must take the tyres before
        /// the steer axes are asked what is left over.</para>
        /// </summary>
        public readonly struct Axis
        {
            /// <summary>Instance name — "WheelFL". Names the attachment slot and the asset.</summary>
            public readonly string Slot;

            /// <summary>The probe pose, as a JS object literal — <c>{wFL:0.25}</c>.
            /// ⚠️ A roll axis is CYCLIC with period 1: <c>{wFL:1}</c> ties with rest EXACTLY and
            /// reads as a dead axis. Probe at a quarter.</summary>
            public readonly string Probe;

            public readonly VehicleFitmentMotion Motion;
            public readonly VehicleFitmentSide Side;

            /// <summary>−1 / +1 keeps only the faces whose centroid is on that side of the
            /// centreline; 0 keeps everything the axis moves. A steer axis moves both front corners
            /// at once and needs the filter; a per-wheel roll axis does not.</summary>
            public readonly int SideSign;

            /// <summary>
            /// ⭐ <b>Fore-aft station window (centroid y, rig metres) — the filter <see cref="SideSign"/>
            /// cannot be.</b> Defaults to the whole machine, so a vehicle that does not need it says
            /// nothing.
            ///
            /// <para><b>Why it exists.</b> The Dually exports a roll axis PER WHEEL (<c>{wFL:0.25}</c>),
            /// so one probe isolates one wheel and no window is needed. The Otter exports roll per
            /// <b>SIDE</b> only — <c>rollL</c>/<c>rollR</c>, because a skid-steer machine drives a side
            /// as one unit — so a single probe moves FOUR wheels that share a side, and no x-filter can
            /// tell them apart. The window separates them by axle station instead.</para>
            ///
            /// <para>⚠️ Windows must not overlap, or two fittings claim the same wheel and the partition
            /// assert fires. Measured on the Otter: her four stations sit at y = ±1.02, ±0.34 and the
            /// geometry that actually rolls spans only ±0.122 about each, so a ±0.30 window clears its
            /// neighbour by a wide margin and captures exactly 47 faces apiece.</para>
            /// </summary>
            public readonly float YMin, YMax;

            /// <summary>The point this fitting turns about, in rig metres. For a front wheel this is
            /// the hub centre, which is ALSO a point on its own vertical steer axis (the rig models
            /// no kingpin offset, caster or scrub radius) — so ONE pivot serves both rotations, and
            /// the fitting needs no articulation machinery beyond a single local rotation.</summary>
            public readonly Vector3 Pivot;

            /// <summary>
            /// ⭐ <b>Material filter — the third claim dimension, and the only one that separates the
            /// landing gear.</b> Keeps only the faces whose rig material is named here; null keeps
            /// everything the axis moves.
            ///
            /// <para><b>Why neither of the other two works here.</b> Raising a trailer's gear moves
            /// 24 faces that are <b>16 rigid <c>iron</c> shoes and 8 telescoping <c>galv</c> leg
            /// tubes</b>, interleaved in space: same side, same station, one stacked directly above
            /// the other. No <see cref="SideSign"/> and no <see cref="YMin"/> window can tell them
            /// apart. The rig already distinguishes them — it paints them differently — so the split
            /// is read off the art rather than invented.</para>
            /// </summary>
            public readonly string[] Materials;

            /// <summary>For <see cref="VehicleFitmentMotion.HingeRotation"/>: which axis, and how far.
            /// ⚠️ The FULL published sweep, signed — a reefer barn is ∓255°, not the ±105° that
            /// reaches the same pose the short way round.</summary>
            public readonly VehicleHingeAxis HingeAxis;
            public readonly float SweepDegrees;

            /// <summary>For <see cref="VehicleFitmentMotion.Slide"/>: the pose fractions to sample the
            /// path at. Put a sample at every CORNER and the interpolation between them is exact — the
            /// van's slide has two, where her outboard pop starts overlapping her run aft (t = 0.1)
            /// and where the pop finishes (t = 1/6).</summary>
            public readonly float[] SlideSampleTs;

            /// <summary>For <see cref="VehicleFitmentMotion.DiscreteStates"/>: the state names and the
            /// JS pose literal each is baked at, parallel arrays. The face INDICES come from
            /// <see cref="Probe"/> as usual, so every state must be a pose of the SAME build — which
            /// is exactly why the rollup cannot be one of these.</summary>
            public readonly string[] StateNames, StatePoses;

            /// <summary>The slot this fitting hangs off, or null for the body — see
            /// <see cref="VehicleFitment.ParentSlot"/>. The cabover's two doors hang off her tilting
            /// cab.</summary>
            public readonly string ParentSlot;

            public Axis(string slot, string probe, VehicleFitmentMotion motion,
                        VehicleFitmentSide side, int sideSign, Vector3 pivot,
                        float yMin = float.NegativeInfinity,
                        float yMax = float.PositiveInfinity,
                        string[] materials = null,
                        VehicleHingeAxis hingeAxis = VehicleHingeAxis.None,
                        float sweepDegrees = 0f,
                        float[] slideSampleTs = null,
                        string[] stateNames = null, string[] statePoses = null,
                        string parentSlot = null)
            {
                Slot = slot; Probe = probe; Motion = motion; Side = side;
                SideSign = sideSign; Pivot = pivot;
                YMin = yMin; YMax = yMax;
                Materials = materials;
                HingeAxis = hingeAxis; SweepDegrees = sweepDegrees;
                SlideSampleTs = slideSampleTs;
                StateNames = stateNames; StatePoses = statePoses;
                ParentSlot = parentSlot;
            }

            /// <summary>Anything the player opens, as opposed to anything the road turns. Asked in one
            /// place so the bake report and the coverage tests agree on what a door is.</summary>
            public bool IsDoor =>
                Motion == VehicleFitmentMotion.HingeRotation ||
                Motion == VehicleFitmentMotion.Slide ||
                Motion == VehicleFitmentMotion.DiscreteStates;
        }

        /// <summary>
        /// ⭐ <b>Where a vehicle's chassis numbers come from, in her own rig's words.</b> Every entry
        /// is a JS expression evaluated against the widened rig host.
        ///
        /// <para><b>Why this is declared per vehicle rather than read by the baker.</b> The first
        /// version read <c>G.axF</c>, <c>G.axR</c>, <c>G.frontWX</c>, <c>steer.*</c> and
        /// <c>travel.*</c> directly — which is the DUALLY's vocabulary, not "a vehicle's". The Otter
        /// publishes an axle ARRAY (<c>G.axY</c>) and a single <c>G.wheelX</c>, and exports neither
        /// <c>steer</c> nor <c>travel</c> at all, because a skid-steer machine has no steering axle
        /// and her rig models no suspension travel. Asking her rig the Dually's questions returns
        /// <c>undefined</c>, and the wheelbase of an eight-wheeler is not a thing to guess at.</para>
        ///
        /// <para>⚠️ <b>Expressions are FULLY QUALIFIED</b> — they are not prefixed with the global the
        /// way <see cref="RigHullExtraction.FaceExpression"/> is, because several of them are compound
        /// (<c>AmphibIso.G.axY[0] - AmphibIso.G.axY[3]</c>) and a prefix would qualify only the first
        /// term and silently read a global that does not exist.</para>
        ///
        /// <para>A machine with no steering axle and no modelled travel leaves the last four at their
        /// default <c>"0"</c>. That is a measured zero — her sidecar's <c>_excluded</c> block says
        /// <i>steering_geometry: not modelled</i> — rather than an absent number.</para>
        /// </summary>
        public sealed class VehicleChassisSource
        {
            public string Wheelbase, FrontTrack, WheelRadius, FrontAxleY, RearAxleY;
            public string MaxInnerDeg = "0", MaxOuterDeg = "0";
            public string TravelFront = "0", TravelRear = "0";
        }

        /// <summary>
        /// One thing the player works, declared as the art's own <c>INTERACT</c> id and the fitting
        /// slots it moves. The reach point is NOT here — the bake reads that out of the sidecar, so
        /// the handle stays where the art put it and cannot drift into a table.
        /// </summary>
        public sealed class DoorGroupSpec
        {
            /// <summary>
            /// ⚠️ <b>Where the art publishes a FORMULA instead of a point.</b> Null for the ordinary
            /// case, where the sidecar gives a literal and the bake reads it.
            ///
            /// <para>The trailer kit has to: ONE sidecar serves FOUR bodies of different lengths, so
            /// its reach points are written parametrically — <c>[-1.7, gearY, 0]</c> for the gear
            /// crank and <c>[0, -L/2-1.6, 0]</c> for the rear doors. Those are not numbers and the
            /// reader is right to refuse them; resolving them per body is this field, and
            /// <c>RoadFleetDoorTests</c> recomputes each from the sidecar's own published
            /// <c>L</c> and kingpin so the arithmetic cannot drift from the formula it came from.</para>
            /// </summary>
            public Vector2? ReachPointOverride;

            /// <summary>The sidecar's own INTERACT id, verbatim — 'slide', 'barn', 'doors', 'hood',
            /// 'tilt', 'gear'. ⚠️ Not guessable: the van calls her rear leaves 'barn' and the trailer
            /// kit calls its own 'doors'.</summary>
            public string Id;

            /// <summary>The fitting slots it moves, in the art's own order.</summary>
            public string[] Slots;

            /// <summary>Which tunable paces it.</summary>
            public VehicleDoorWork Work = VehicleDoorWork.Door;
        }

        /// <summary>One road vehicle: where its rig and sidecar are, and what installs it.</summary>
        public readonly struct Vehicle
        {
            /// <summary>Stable key — the sidecar's own <c>variant</c>, so the file and the table cannot
            /// drift apart.
            ///
            /// <para>⚠️ <b>Except on a CONTAINER rig, where one sidecar describes several bodies.</b>
            /// The trailer kit ships one rig, one sidecar and FOUR towed bodies, and its variant is
            /// the plural <c>trailers-x4</c>. Those four keys are <c>variant</c> + <see cref="Pick"/>
            /// instead, because a key has to name one registered body.</para></summary>
            public readonly string Key;

            /// <summary>
            /// ⭐⭐ <b>WHICH body inside a multi-body rig — and null for every rig that has only one.</b>
            ///
            /// <para><b>The trap this exists for.</b> A container rig resolves a body from its opts
            /// (<c>resolve({body:…})</c>) and MEASURED 2026-08-27, <c>trailerIsoRig.js</c> FALLS BACK
            /// silently: an unknown id returns the default, <c>reefer53</c>. So a mistyped pick does
            /// not throw — it bakes the wrong trailer, and the result is a plausible trailer, which
            /// is why nothing downstream catches it. Exactly the <c>byId</c> failure the sport
            /// fisher's hulls carry.</para>
            ///
            /// <para>⚠️ <b>Anything cached per rig FILE replays the first body's answer onto the rest.</b>
            /// Cache, group and key by <b>(ScriptPath, Pick)</b> — never by file alone. Measured cost
            /// of getting it wrong here: filtering the ramp table at <c>reefer53</c> drops
            /// <c>wood</c>, which only the flatbed deck names, and the face packer resolves an
            /// unknown material to index 0 — so both flatbeds would have shipped with their planked
            /// decks painted body colour.</para>
            /// </summary>
            public readonly string Pick;
            /// <summary>Repo-relative path to the rig <c>.js</c>.</summary>
            public readonly string ScriptPath;
            /// <summary>Repo-relative path to the gameplay sidecar.</summary>
            public readonly string SidecarPath;
            /// <summary>The global the rig's IIFE installs.</summary>
            public readonly string GlobalName;

            /// <summary>Where the baked <see cref="VehicleMeshDef"/> is written.</summary>
            public readonly string MeshAssetPath;
            /// <summary>The baked def's stable id (<c>vehiclemesh.snake_case</c>), append-only.</summary>
            public readonly string MeshId;

            /// <summary>
            /// The rig's private face builder — <c>build</c>. Named separately from
            /// <see cref="Extraction"/> because the articulation probes CALL it directly at several
            /// poses, while the extraction's field is a whole call expression.
            /// </summary>
            public readonly string FaceBuilderName;

            /// <summary>
            /// How to reach the rig's face list. A vehicle rig is a GENERATOR — it exports no static
            /// <c>F</c> — so the private builder is widened onto the global and called at the rest
            /// pose. ⚠️ The inner <c>resolve</c> must be QUALIFIED: the shim widens symbols onto the
            /// global, it does not put them in scope, and an unqualified call dies with
            /// <c>ReferenceError: resolve is not defined</c>.
            /// </summary>
            public readonly RigHullExtraction Extraction;

            /// <summary>The pose axes that lift her fittings out — see <see cref="Axis"/> for why
            /// their order matters.</summary>
            public readonly IReadOnlyList<Axis> Axes;

            /// <summary>Her chassis numbers, in her own rig's vocabulary — see
            /// <see cref="VehicleChassisSource"/> for why this is not the baker's business.</summary>
            public readonly VehicleChassisSource ChassisSource;

            /// <summary>
            /// ⭐ <b>Two CENTRELINE anchors, aft and fore — the second azimuth oracle.</b> The first
            /// is her front-axle abeam pair, which every DRIVEN vehicle rig publishes under the same
            /// two names; this one has to be declared because the names are each rig's own. The
            /// Dually runs stern-to-bow as <c>hitch</c>→<c>hoodLatch</c>; the Otter, a boat with
            /// wheels, as <c>transom</c>→<c>bow</c>; a cabover has no hood at all and runs
            /// <c>rollup</c>→<c>tiltLatch</c>; a towed body <c>rear</c>→<c>kingpin</c>.
            ///
            /// <para>The point of a SECOND oracle is that it is independent: if the two disagree the
            /// baker refuses rather than picking one, because a mirrored heading map is the kind of
            /// wrong that looks fine until she drives backwards.</para>
            /// </summary>
            public readonly string AzimuthAftAnchor, AzimuthForeAnchor;

            /// <summary>
            /// ⚠️ <b>The ABEAM pair, and it is not <c>wheelFL</c>/<c>wheelFR</c> on everything.</b>
            /// The first azimuth oracle wants two hubs at one screen y and opposite screen x —
            /// which the whole road pack publishes under the front-axle names, because they all
            /// have a front axle.
            ///
            /// <para><b>A towed body has neither.</b> No hood, no front axle: <c>wheelFL</c> is
            /// simply absent from her <c>anchors()</c>, and the admissibility gate that would have
            /// caught it reads <c>undefined.y</c> and throws instead. Hers are the axle hubs,
            /// <c>wheelL</c>/<c>wheelR</c>. Defaulted so every driven machine says nothing.</para>
            /// </summary>
            public readonly string AzimuthAbeamLeftAnchor, AzimuthAbeamRightAnchor;

            /// <summary>
            /// ⭐⭐ <b>The pose the articulation probes measure FROM — and on a container rig it
            /// carries the body.</b>
            ///
            /// <para>Every probe in <c>VehicleMeshAssetBaker.Partition</c> is "what moved between
            /// rest and this axis", and rest was <c>resolve({})</c>. On <c>trailerIsoRig.js</c> that
            /// is <c>reefer53</c> — so a flatbed's wheel probe would have compared the DEFAULT
            /// body's face list against the default body's, and claimed the reefer's wheels for the
            /// flatbed's fitting. Same <c>(file, pick)</c> trap as
            /// <see cref="Pick"/>, one layer further in, and it does not throw either: the face
            /// counts agree because it is the same body twice.</para>
            ///
            /// <para>A JS object literal. <c>{}</c> — the default — is every single-body rig, whose
            /// probes are byte-identical to what they always were.</para>
            /// </summary>
            public readonly string RestPose;

            /// <summary>
            /// Which body's block inside a CONTAINER gameplay sidecar carries this one's geometry —
            /// the trailer set puts each towed body's <c>BODY</c> under <c>bodies.&lt;pick&gt;</c>.
            /// Empty = the root, which is every sidecar that describes one machine.
            /// See <see cref="VehicleSidecarFacts.Read"/>.
            /// </summary>
            public readonly string SidecarBodyScope;

            /// <summary>What the player works on this machine, and which fittings each handle moves.
            /// Empty for a machine with no worked openings.</summary>
            public readonly IReadOnlyList<DoorGroupSpec> DoorGroups;

            /// <summary>
            /// ⭐ <b>Probe poses under which the BODY must not move at all</b> — the independent
            /// check that every articulating face was claimed by some fitting.
            ///
            /// <para>The partition check ("body + fittings = the rig's face count") cannot see the
            /// failure this catches: a vehicle whose <see cref="Axes"/> came out empty partitions
            /// perfectly, with the body simply taking everything, and bakes a truck whose wheels are
            /// welded on. These are the MASTER axes — <c>{roll:0.25}</c>, <c>{steer:1}</c> — so they
            /// cover every wheel at once and do not need to be kept in step with the per-wheel list.</para>
            /// </summary>
            public readonly IReadOnlyList<string> BodyMustNotMove;

            /// <summary>Where the <c>VehicleDef</c> the world places lives. Empty = the bake produces
            /// a mesh and nothing wears it (a vehicle that is art-only, so far).</summary>
            public readonly string VehicleDefPath;

            /// <summary>Her stable gameplay id — <c>vehicle.snake_case</c>, append-only once
            /// shipped. Owner-ruled per vehicle; the Dually is <c>vehicle.dually_3500</c>.</summary>
            public readonly string VehicleId;

            /// <summary>Human-readable, for the created asset and for log lines. Never parsed.</summary>
            public readonly string Label;

            public Vehicle(string key, string scriptPath, string sidecarPath, string globalName,
                           string pick = null,
                           string meshAssetPath = null, string meshId = null,
                           string faceBuilderName = null, RigHullExtraction extraction = null,
                           IReadOnlyList<Axis> axes = null,
                           VehicleChassisSource chassisSource = null,
                           string azimuthAftAnchor = null, string azimuthForeAnchor = null,
                           IReadOnlyList<string> bodyMustNotMove = null,
                           string vehicleDefPath = null, string vehicleId = null, string label = null,
                           string azimuthAbeamLeftAnchor = null, string azimuthAbeamRightAnchor = null,
                           string restPose = null, string sidecarBodyScope = null,
                           IReadOnlyList<DoorGroupSpec> doorGroups = null)
            {
                Key = key; ScriptPath = scriptPath; SidecarPath = sidecarPath; GlobalName = globalName;
                Pick = pick;
                MeshAssetPath = meshAssetPath; MeshId = meshId;
                FaceBuilderName = faceBuilderName;
                Extraction = extraction;
                Axes = axes ?? Array.Empty<Axis>();
                ChassisSource = chassisSource;
                AzimuthAftAnchor = azimuthAftAnchor; AzimuthForeAnchor = azimuthForeAnchor;
                AzimuthAbeamLeftAnchor = azimuthAbeamLeftAnchor ?? "wheelFL";
                AzimuthAbeamRightAnchor = azimuthAbeamRightAnchor ?? "wheelFR";
                RestPose = string.IsNullOrEmpty(restPose) ? "{}" : restPose;
                SidecarBodyScope = sidecarBodyScope ?? "";
                DoorGroups = doorGroups ?? Array.Empty<DoorGroupSpec>();
                BodyMustNotMove = bodyMustNotMove ?? Array.Empty<string>();
                VehicleDefPath = vehicleDefPath; VehicleId = vehicleId; Label = label;
            }
        }

        /// <summary>The one vehicle by key. Throws rather than returning a default — a bake asked for
        /// a vehicle that is not in the table is a typo, not an empty result.</summary>
        public static Vehicle Get(string key)
        {
            foreach (Vehicle v in Vehicles)
                if (string.Equals(v.Key, key, StringComparison.Ordinal)) return v;
            throw new KeyNotFoundException(
                $"No road vehicle '{key}' in VehicleRigFleet.Vehicles. Known: " +
                string.Join(", ", System.Linq.Enumerable.Select(Vehicles, x => x.Key)));
        }

        // ⚠️ DECLARED BEFORE `Vehicles`, AND THAT IS NOT STYLE. C# runs static field
        // initialisers in DECLARATION ORDER, so a `Vehicles` declared first would capture
        // this array while it was still null — and the constructor's `axes ?? Empty` would
        // turn that into a vehicle with NO articulation, which bakes a truck whose wheels are
        // frozen into her body. Measured 2026-08-17: it did exactly that, and the partition
        // assert PASSED (body 1153 = 1153) because the body had simply taken everything.
        // The guard in VehicleMeshAssetBaker.Partition now catches it on its own; this order
        // stops it happening.
        /// <summary>
        /// ⭐ <b>The Dually's articulation, in the order the split must ask about it.</b>
        ///
        /// <para><b>The four roll axes first.</b> Each takes one tyre-and-hub group; the two rear
        /// entries each drive a DUAL PAIR, which is why there are four axes for six wheels. Measured
        /// 2026-08-17: 103 faces apiece, disjoint, and their union is exactly what the master
        /// <c>roll</c> moves (412).</para>
        ///
        /// <para><b>Then the two steer axes.</b> <c>steer</c> moves 286 faces — both front corners —
        /// so by the time it is asked, the 206 tyre faces are already claimed and each side's entry
        /// finds only its 40-face knuckle: the fender lip, hub cover and mudflap that swing with the
        /// corner but do not turn with the tyre. Listing steer FIRST would swallow both front wheels
        /// and leave the roll axes empty (the baker fails loudly on that rather than shipping it).</para>
        ///
        /// <para><b>Every pivot is a hub centre</b>, read off the rig's own <c>G</c> rather than
        /// typed here — see the note on <see cref="Axis.Pivot"/> for why one point serves both the
        /// steer and the roll. The rear pair's pivot x is the mean of the inner and outer wheels';
        /// for a rotation about the axle its x is arbitrary, and the mean is the honest label.</para>
        /// </summary>
        static readonly Axis[] DuallyAxes =
        {
            new Axis("WheelFL", "{wFL:0.25}", VehicleFitmentMotion.SteerAndRoll,
                     VehicleFitmentSide.Left, 0, new Vector3(-0.90f, 2.18f, 0.42f)),
            new Axis("WheelFR", "{wFR:0.25}", VehicleFitmentMotion.SteerAndRoll,
                     VehicleFitmentSide.Right, 0, new Vector3(0.90f, 2.18f, 0.42f)),
            new Axis("WheelRL", "{wRL:0.25}", VehicleFitmentMotion.RollOnly,
                     VehicleFitmentSide.Left, 0, new Vector3(-0.885f, -2.12f, 0.42f)),
            new Axis("WheelRR", "{wRR:0.25}", VehicleFitmentMotion.RollOnly,
                     VehicleFitmentSide.Right, 0, new Vector3(0.885f, -2.12f, 0.42f)),

            new Axis("KnuckleFL", "{steer:1}", VehicleFitmentMotion.SteerOnly,
                     VehicleFitmentSide.Left, -1, new Vector3(-0.90f, 2.18f, 0.42f)),
            new Axis("KnuckleFR", "{steer:1}", VehicleFitmentMotion.SteerOnly,
                     VehicleFitmentSide.Right, +1, new Vector3(0.90f, 2.18f, 0.42f)),
        };

        /// <summary>
        /// ⭐ <b>The Otter's articulation — eight wheels claimed by TWO probes and a station window.</b>
        ///
        /// <para><b>Why this does not look like the Dually's.</b> She is a SKID STEER: there is no
        /// steer axle at all, and her rig exports roll per <b>side</b> (<c>rollL</c>/<c>rollR</c>)
        /// rather than per wheel, because driving a side as one unit IS her drivetrain. So one probe
        /// moves four wheels, and <see cref="Axis.SideSign"/> cannot separate them — they share a
        /// side. <see cref="Axis.YMin"/>/<see cref="Axis.YMax"/> separate them by axle station.</para>
        ///
        /// <para><b>Measured 2026-08-19, in the repo's own V8.</b> <c>rollL</c> and <c>rollR</c> move
        /// 188 faces each, perfectly disjoint, and their union is exactly what the master
        /// <c>roll</c> moves (376). Each ±0.30 station window then takes exactly <b>47</b> of them,
        /// eight groups covering all 376 with no overlap and nothing left over. Her stations sit
        /// 0.68 m apart and the rolling geometry spans ±0.122 about each, so the windows clear
        /// their neighbours by a wide margin.</para>
        ///
        /// <para>⚠️ <b>What actually rolls is small, and that is the rig, not a mistake.</b> Her tyre
        /// is a fixed 14-segment tube whose tread is a procedural <c>lugTex</c> keyed on roll — and
        /// the mesh path drops procedural <c>tex</c> entirely. The only geometry that moves is the
        /// five wheel bolts and one index stud per hub. The shipped Dually is built the same way
        /// (a <c>treadTex</c> tube plus lugs, hand-holes and a notch), so this is the established
        /// behaviour of the vehicle mesh path rather than something new here.</para>
        ///
        /// <para><b>Every pivot is the axle centre</b> — <c>G.wheelX</c> = 0.63 and <c>G.wheelR</c> =
        /// 0.32, read off the rig rather than typed, and the station y is <c>G.axY</c> exactly. The
        /// solved centre of rotation reproduces (y, z) = (station, 0.3200) to four decimals. For a
        /// RollOnly fitting the pivot's x is arbitrary — the rotation is about the axle — so the
        /// axle centre is the honest label even though the bolts themselves sit outboard at 0.80.</para>
        /// </summary>
        static readonly Axis[] OtterAxes = BuildOtterAxes();

        /// <summary>Eight entries that differ only by side and station, so they are generated from
        /// the rig's own axle table rather than typed out eight times — a transcription slip in one
        /// of eight near-identical literals is exactly the kind of thing that bakes a wheel in the
        /// wrong place and looks almost right.</summary>
        static Axis[] BuildOtterAxes()
        {
            // G.axY, bow to stern, and G.wheelX / G.wheelR — the rig's own numbers.
            float[] axY = { 1.02f, 0.34f, -0.34f, -1.02f };
            const float WheelX = 0.63f, WheelR = 0.32f;

            // Half-window. Stations are 0.68 apart and the rolling geometry spans ±0.122, so this
            // sits comfortably between "captures the whole wheel" and "never reaches its neighbour".
            const float Half = 0.30f;

            var axes = new Axis[8];
            for (int i = 0; i < 4; i++)
            {
                axes[i] = new Axis(
                    $"WheelL{i + 1}", "{rollL:0.25}", VehicleFitmentMotion.RollOnly,
                    VehicleFitmentSide.Left, -1, new Vector3(-WheelX, axY[i], WheelR),
                    axY[i] - Half, axY[i] + Half);
                axes[4 + i] = new Axis(
                    $"WheelR{i + 1}", "{rollR:0.25}", VehicleFitmentMotion.RollOnly,
                    VehicleFitmentSide.Right, +1, new Vector3(WheelX, axY[i], WheelR),
                    axY[i] - Half, axY[i] + Half);
            }
            return axes;
        }

        // =============================================================================================
        //  THE ROAD FLEET's articulation (kit drop 2026-08-27) — five rigs and four towed bodies
        //  that share ONE shape, so they are GENERATED from each rig's own numbers rather than
        //  typed out nine times. A transcription slip in one of nine near-identical literals bakes
        //  a wheel in the wrong place and looks almost right; RoadFleetBakeTests asserts every
        //  number below against the rig that published it.
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The measured half-window that separates one axle station from its neighbour</b>, in
        /// rig metres — the Otter's <see cref="Axis.YMin"/>/<see cref="Axis.YMax"/> filter, on a
        /// truck.
        ///
        /// <para><b>Where the number comes from.</b> Measured 2026-08-27 on the two semis and the two
        /// 53-ft trailers: their tandem stations sit <b>1.20 m apart</b> and the geometry that
        /// actually rolls spans <b>±0.31</b> about each, so any half-window from ±0.32 (captures the
        /// whole wheel) to ±0.88 (never reaches the neighbour's geometry) works.</para>
        ///
        /// <para>0.55 is chosen inside that band deliberately: 2×0.55 = 1.10 against a 1.20 spacing,
        /// so neighbouring windows leave a <b>0.10 m gap</b> rather than overlapping. A face that
        /// fell in a gap is caught LOUDLY — <c>BodyMustNotMove</c>'s master-roll probe reports it as
        /// unclaimed — while a face in an overlap is claimed silently by whichever axis is listed
        /// first. When both failure modes are available, take the one that shouts.</para>
        /// </summary>
        const float StationHalfWindow = 0.55f;

        /// <summary>
        /// The articulation of one WHEELED road rig: two steered front corners, a rear axle that is
        /// either single or a tandem, and the two steering knuckles that are what is left of
        /// <c>steer</c> once the front tyres are claimed.
        /// </summary>
        /// <param name="frontWX">half the front track (the rig's <c>G.frontWX</c>).</param>
        /// <param name="rearWX">the rear hub's nominal x — the MEAN of the dual pair's inner and
        /// outer wheels. For a rotation about the axle its x is arbitrary, and the mean is the
        /// honest label (the Dually's precedent).</param>
        /// <param name="rearStations">the rear axle's station centres, FORE to AFT. One entry is a
        /// straight truck; two is a tandem sharing one axis per side, which needs the windows.</param>
        static Axis[] BuildRoadAxes(float frontWX, float axF, float rearWX, float wheelR,
                                    params float[] rearStations)
        {
            var axes = new List<Axis>(4 + rearStations.Length * 2);

            // ⚠️ THE FRONT ROLL AXES FIRST, and the order is the whole plan: `steer` moves the wheel
            // AND its knuckle, so with the tyres already claimed each steer axis finds only its
            // 40-face knuckle. Listing steer first swallows both front corners.
            axes.Add(new Axis("WheelFL", "{wFL:0.25}", VehicleFitmentMotion.SteerAndRoll,
                              VehicleFitmentSide.Left, 0, new Vector3(-frontWX, axF, wheelR)));
            axes.Add(new Axis("WheelFR", "{wFR:0.25}", VehicleFitmentMotion.SteerAndRoll,
                              VehicleFitmentSide.Right, 0, new Vector3(frontWX, axF, wheelR)));

            // The rear. ⚠️ On a semi ONE probe moves TWO axles — a tandem side rides one axis and no
            // side filter can separate them, because they share a side. The station window can.
            // Slots are numbered only when there is more than one, so a straight truck keeps the
            // Dually's plain WheelRL/WheelRR.
            for (int i = 0; i < rearStations.Length; i++)
            {
                string suffix = rearStations.Length > 1 ? (i + 1).ToString() : "";
                float y = rearStations[i];
                bool windowed = rearStations.Length > 1;

                axes.Add(new Axis($"WheelRL{suffix}", "{wRL:0.25}", VehicleFitmentMotion.RollOnly,
                                  VehicleFitmentSide.Left, 0, new Vector3(-rearWX, y, wheelR),
                                  windowed ? y - StationHalfWindow : float.NegativeInfinity,
                                  windowed ? y + StationHalfWindow : float.PositiveInfinity));
                axes.Add(new Axis($"WheelRR{suffix}", "{wRR:0.25}", VehicleFitmentMotion.RollOnly,
                                  VehicleFitmentSide.Right, 0, new Vector3(rearWX, y, wheelR),
                                  windowed ? y - StationHalfWindow : float.NegativeInfinity,
                                  windowed ? y + StationHalfWindow : float.PositiveInfinity));
            }

            // And the knuckles, LAST, with a side filter: steer moves both front corners at once.
            axes.Add(new Axis("KnuckleFL", "{steer:1}", VehicleFitmentMotion.SteerOnly,
                              VehicleFitmentSide.Left, -1, new Vector3(-frontWX, axF, wheelR)));
            axes.Add(new Axis("KnuckleFR", "{steer:1}", VehicleFitmentMotion.SteerOnly,
                              VehicleFitmentSide.Right, +1, new Vector3(frontWX, axF, wheelR)));

            return axes.ToArray();
        }

        /// <summary>
        /// The articulation of one TOWED body: her axle group, and her landing gear.
        ///
        /// <para><b>No steer, and that is a measurement rather than an omission</b> —
        /// <c>trailerIsoRig.js</c> resolves no <c>steer</c> axis at all and publishes no
        /// <c>steer</c> block, which is the same fact <c>VehicleKinds.IsDrivable(TowedBody)</c> is
        /// written from. She is not a truck whose steering went unmodelled; she is dragged.</para>
        ///
        /// <para>⚠️⚠️ <b>AND NO LANDING GEAR, and that is the one deliberate deferral in this bake.</b>
        /// The plan was a fitting: the drop's probe fixture measured <c>gear</c> 1 → 0 as moving 24
        /// faces with a per-vertex deviation of <b>0</b>, which reads as an exact rigid translation —
        /// one mesh at two positions, the way the Otter's <c>float</c> is.</para>
        ///
        /// <para><b>It is not one.</b> Re-measured 2026-08-27 in the repo's own V8 <i>without</i>
        /// skipping the vertices that did not move — which is what the probe's deviation helper does,
        /// correctly for what it measures and misleadingly for what its name says. Of the 24 faces:
        /// <b>16 <c>iron</c> shoe faces DO translate rigidly by exactly [0, 0, 0.78]</b>, and the
        /// other <b>8 <c>galv</c> faces are the leg tubes, which TELESCOPE</b> — their top two
        /// vertices are pinned at z 1.120 while their bottoms rise 0.130 → 0.910. The leg shortens;
        /// it does not move. 16 of the 96 vertices stay exactly where they are.</para>
        ///
        /// <para>So one mesh plus an offset cannot reproduce it: applied to the whole set it would
        /// lift the leg tops off the frame, and applied to the shoes alone it would slide them up
        /// inside a leg still drawn at full extension. The gear therefore bakes INTO THE BODY at
        /// <c>gear:1</c> — PARKED, sand shoes grounded, which is the rig's own default, what
        /// <c>frame.at_rest</c> declares, and what a placed trailer is. Raising the legs needs a
        /// second body mesh or a two-part split, and that belongs with the coupling loop rather than
        /// being approximated here. <c>RoadFleetBakeTests</c> pins the telescope in both directions,
        /// so the day the rig makes it rigid the deferral is lifted rather than forgotten.</para>
        ///
        /// <para>⚠️ Which is also why <c>{gear:0}</c> is NOT in these bodies' <c>BodyMustNotMove</c>:
        /// those 24 faces move under it and the body is meant to keep them.</para>
        /// </summary>
        /// <param name="stations">her axle station centres, FORE to AFT.</param>
        static Axis[] BuildTrailerAxes(float wheelX, float wheelR, float gearY,
                                       params float[] stations)
        {
            var axes = new List<Axis>(stations.Length * 2 + 2);

            for (int i = 0; i < stations.Length; i++)
            {
                string suffix = stations.Length > 1 ? (i + 1).ToString() : "";
                float y = stations[i];
                bool windowed = stations.Length > 1;

                axes.Add(new Axis($"WheelL{suffix}", "{wL:0.25}", VehicleFitmentMotion.RollOnly,
                                  VehicleFitmentSide.Left, 0, new Vector3(-wheelX, y, wheelR),
                                  windowed ? y - StationHalfWindow : float.NegativeInfinity,
                                  windowed ? y + StationHalfWindow : float.PositiveInfinity));
                axes.Add(new Axis($"WheelR{suffix}", "{wR:0.25}", VehicleFitmentMotion.RollOnly,
                                  VehicleFitmentSide.Right, 0, new Vector3(wheelX, y, wheelR),
                                  windowed ? y - StationHalfWindow : float.NegativeInfinity,
                                  windowed ? y + StationHalfWindow : float.PositiveInfinity));
            }

            // ---- ⭐ THE LANDING GEAR, SPLIT — PR 2's deferral, lifted ---------------------------
            //
            // PR 2 measured it and refused to fake it: raising the legs moves 24 faces of which 16
            // translate rigidly by exactly [0, 0, 0.78] and 8 do not move at all at the top while
            // their bottoms rise 0.130 → 0.910. One mesh plus an offset cannot be both, so the whole
            // assembly was baked into the body at parked and the raise was deferred here.
            //
            // Re-measured 2026-08-27 for this PR, and the split falls out of the ART: the 16 rigid
            // faces are the `iron` sand shoes and the 8 that telescope are the `galv` leg tubes. No
            // side filter and no station window separates them — they are stacked on one another —
            // so the claim is made on MATERIAL, which is the rig's own distinction.
            //
            //   · the SHOES ride down and up as one body: a Slide fitting, linear, deviation 0 at
            //     every quarter (measured [0,0,0.195/0.390/0.585/0.780]).
            //   · the LEGS are baked at both ends and swapped: a DiscreteStates fitting. Eight faces
            //     twice is the whole cost of not lying about a telescope.
            //
            // ⚠️ T RUNS THE OTHER WAY FROM `gear`. The rig bakes at gear:1 (parked, shoes grounded,
            // and what a placed trailer is), so the fitting's t = 0 IS gear 1 and t = 1 is gear 0.
            // The probe pose is therefore {gear:0} — fully raised — for both.
            axes.Add(new Axis("LandingGearShoes", "{gear:0}", VehicleFitmentMotion.Slide,
                              VehicleFitmentSide.Centre, 0, new Vector3(0f, gearY, 0f),
                              materials: new[] { "iron" }, slideSampleTs: LinearEnds));

            axes.Add(new Axis("LandingGearLegs", "{gear:0}", VehicleFitmentMotion.DiscreteStates,
                              VehicleFitmentSide.Centre, 0, new Vector3(0f, gearY, 0f),
                              materials: new[] { "galv" },
                              stateNames: new[] { "down", "up" },
                              statePoses: new[] { "{gear:1}", "{gear:0}" }));

            return axes.ToArray();
        }

        /// <summary>
        /// A reefer's rear barn doors — the only doors in the towed set; the flatbeds clamp
        /// <c>barnL</c>/<c>barnR</c> to zero and measure exactly that.
        ///
        /// <para>⚠️⚠️ <b>∓255°, NOT ±105°.</b> The two reach the identical pose — 255 wraps to −105 —
        /// and a naive measurement reports the short one, because an angle read through
        /// <c>atan2</c> comes back in (−180, 180]. Taking it would be a silent, plausible bug: the
        /// door would arrive looking correct having swept through entirely the wrong volume. The
        /// published <c>keep_clear</c> says which way it really goes — the fan passes FULL OUTBOARD
        /// at 180° (|x| 2.37 m, wider than the trailer) before folding back along the sides, so a
        /// leaf animated the short way sweeps through whatever is parked alongside.</para>
        /// </summary>
        static Axis[] ReeferBarns(float hingeY) => new[]
        {
            Hinged("BarnL", "{barnL:1}", VehicleFitmentSide.Left,
                   VehicleHingeAxis.Vertical, new Vector3(-1.19f, hingeY, 0f), -255f),
            Hinged("BarnR", "{barnR:1}", VehicleFitmentSide.Right,
                   VehicleHingeAxis.Vertical, new Vector3(1.19f, hingeY, 0f), +255f),
        };

        // ---- the five road rigs' own numbers, off each rig's `G` ---------------------------------
        // frontWX · axF · mean(dualXi,dualXo) · wheelR · then the rear station(s), fore to aft.
        // The semis publish their tandem stations as G.tandA (fore) and G.tandB (aft); the straight
        // trucks publish a single G.axR. Asserted against the rigs in RoadFleetBakeTests.

        // =============================================================================================
        //  THE DOORS (PR 3a) — every hinge below is the art's own, and every one of them was
        //  MEASURED before it was declared.
        //
        //  The measurement is the landing gear's law applied to doors: rotate every vertex of the
        //  moved faces back about the published hinge and look at what is left. A door that is one
        //  rigid leaf leaves NOTHING — radius error 0, invariant-coordinate error 0, and an angle
        //  spread of 0.000000 with the recovered angle equal to the sidecar's own swing_deg to four
        //  decimals. Measured 2026-08-27 in the repo's own V8; RoadFleetDoorTests re-measures every
        //  one of them against the rig rather than restating the numbers.
        //
        //  ⚠️ TWO PARTS OF THIS DROP MEASURE OTHERWISE AND ARE NOT HERE. The ROLLUP is not a pose at
        //  all — at rollup:1 the face count CHANGES, so it is a different build, and mid-travel its
        //  slats fan rather than moving as one (deviation 0.48 at a quarter, 0.96 at a half). The
        //  LIFTGATE is a four-bar linkage: its published arm_pivot misses the platform by 0.65 m of
        //  radius error across a 287° angle spread, `unfold` moves only 6 of its 36 faces, `lower`
        //  leaves 24 vertices pinned while the rest move, and splitting by material does not
        //  separate it — "parallel arms", as its own phase text says. Both need a whole-BODY
        //  two-state bake, which changes the extraction rather than adding a fitting, and both are
        //  PR 3c. Declared here so neither is mistaken for an oversight.
        // =============================================================================================

        /// <summary>A handle and the leaves it works. The ids are the ART's, not ours, and they do
        /// not rhyme: the van calls her rear pair <c>barn</c> and the trailer kit calls its own
        /// <c>doors</c>. Copying one onto the other silently produces a machine with a handle nobody
        /// can find, so each is written from its own sidecar.</summary>
        static DoorGroupSpec Group(string id, params string[] slots) =>
            new DoorGroupSpec { Id = id, Slots = slots };

        /// <summary>The gear crank, at the point her sidecar's formula gives for THIS body:
        /// <c>[-1.7, gearY, 0]</c>, street side, where gearY is the leg station (kingpinY − 2.00).</summary>
        static DoorGroupSpec GearGroup(float gearY) =>
            new DoorGroupSpec
            {
                Id = "gear",
                Slots = new[] { "LandingGearShoes", "LandingGearLegs" },
                Work = VehicleDoorWork.LandingGear,
                ReachPointOverride = new Vector2(-1.7f, gearY),
            };

        /// <summary>The reefer's rear-door handle, at her sidecar's <c>[0, -L/2-1.6, 0]</c> — well
        /// aft of the leaves' own 1.175 m keep-clear fan, which is the point of standing there.</summary>
        static DoorGroupSpec ReeferDoorsGroup(float bodyLength) =>
            new DoorGroupSpec
            {
                Id = "doors",
                Slots = new[] { "BarnL", "BarnR" },
                ReachPointOverride = new Vector2(0f, -bodyLength / 2f - 1.6f),
            };

        // ⚠️ `drive` and `ride` ARE the cab doors. The art hangs getting in off the leaf you open
        // to do it, and every truck in the pack publishes them at `door_l` and `door_r`. They are
        // declared below so the leaves can be POSED; their HANDLES stay with the seat flow that
        // already owns them (VehicleDoor / ControlSwitcher) rather than this PR growing a second
        // way into a cab.

        /// <summary>One rigid leaf on one published hinge. <paramref name="pin"/> is any point on
        /// the axis — a vertical hinge's z and a lateral hinge's x are both arbitrary, because the
        /// rotation is about a LINE, so both are written 0 rather than given a fictitious
        /// precision.</summary>
        static Axis Hinged(string slot, string probe, VehicleFitmentSide side,
                           VehicleHingeAxis axis, Vector3 pin, float sweepDeg,
                           string parentSlot = null) =>
            new Axis(slot, probe, VehicleFitmentMotion.HingeRotation, side, 0, pin,
                     hingeAxis: axis, sweepDegrees: sweepDeg, parentSlot: parentSlot);

        /// <summary>The corners of the van's slide path. Her outboard pop and her run aft OVERLAP:
        /// the pop ramps over t ∈ [0, 1/6] and the run aft over t ∈ [0.1, 1], so the path has two
        /// corners rather than one and a sample at each makes the interpolation exact. Measured, and
        /// <c>RoadFleetDoorTests</c> checks the polyline against the rig at three off-sample poses —
        /// which is the assertion that the sample set is sufficient rather than merely present.</summary>
        static readonly float[] VanSlideCorners = { 0f, 0.1f, 1f / 6f, 1f };

        /// <summary>The gear's shoes translate linearly, so its two ends are its only corners.</summary>
        static readonly float[] LinearEnds = { 0f, 1f };

        /// <summary>Wheels first, doors after — the claim order is the whole plan, exactly as it is
        /// for steer-after-roll. It matters most on the cabover, where the tilt moves the entire cab
        /// INCLUDING her door leaves: taking the doors first leaves the tilting cab's own mesh
        /// without them, and they ride it back via <see cref="Axis.ParentSlot"/>.</summary>
        static Axis[] WithDoors(Axis[] wheels, params Axis[] doors)
        {
            var all = new List<Axis>(wheels.Length + doors.Length);
            all.AddRange(wheels);
            all.AddRange(doors);
            return all.ToArray();
        }

        // The van joined the baked set the day her re-stamped sidecar landed (2026-08-27, the same
        // day it was asked): numbers off her own WHEELS block — frontWX 0.82, axF 2.20, single rear
        // axle at −1.76 with the same track, wheelR 0.36.
        //
        // ⭐ SIX openings, the most in the fleet: two cab doors, two rear barns, the curb-side
        // slider and a stub clamshell hood. ⚠️ Her hood hinge is published by her RIG and not by her
        // sidecar (`rotX(p, 1.74, G.hoodZc, …)` at 42°) — the rig is the authority on geometry, and
        // the measurement confirms it exactly, so it is read rather than invented.
        static readonly Axis[] HightopVanAxes = WithDoors(
            BuildRoadAxes(0.82f, 2.20f, 0.82f, 0.36f, -1.76f),
            Hinged("DoorFL", "{dFL:1}", VehicleFitmentSide.Left,
                   VehicleHingeAxis.Vertical, new Vector3(-0.98f, 1.66f, 0f), -62f),
            Hinged("DoorFR", "{dFR:1}", VehicleFitmentSide.Right,
                   VehicleHingeAxis.Vertical, new Vector3(0.98f, 1.66f, 0f), +62f),
            Hinged("BarnL", "{barnL:1}", VehicleFitmentSide.Left,
                   VehicleHingeAxis.Vertical, new Vector3(-0.98f, -2.92f, 0f), -96f),
            Hinged("BarnR", "{barnR:1}", VehicleFitmentSide.Right,
                   VehicleHingeAxis.Vertical, new Vector3(0.98f, -2.92f, 0f), +96f),
            new Axis("SlideDoor", "{slide:1}", VehicleFitmentMotion.Slide,
                     VehicleFitmentSide.Right, 0, new Vector3(1.01f, -0.62f, 1.33f),
                     slideSampleTs: VanSlideCorners),
            Hinged("Hood", "{hood:1}", VehicleFitmentSide.Centre,
                   VehicleHingeAxis.Lateral, new Vector3(0f, 1.74f, 1.28f), +42f));

        // ⭐ The CABOVER's cab TILTS — 237 faces, the largest fitting in the fleet, and her two doors
        // are cut out of it. They are claimed FIRST and hang off it; see WithDoors and ParentSlot.
        static readonly Axis[] CaboverBoxAxes = WithDoors(
            BuildRoadAxes(0.78f, 2.62f, 0.71f, 0.334f, -1.50f),
            Hinged("DoorL", "{dL:1}", VehicleFitmentSide.Left,
                   VehicleHingeAxis.Vertical, new Vector3(-0.94f, 2.96f, 0f), -65f,
                   parentSlot: "CabTilt"),
            Hinged("DoorR", "{dR:1}", VehicleFitmentSide.Right,
                   VehicleHingeAxis.Vertical, new Vector3(0.94f, 2.96f, 0f), +65f,
                   parentSlot: "CabTilt"),
            Hinged("CabTilt", "{tilt:1}", VehicleFitmentSide.Centre,
                   VehicleHingeAxis.Lateral, new Vector3(0f, 3.20f, 0.50f), -38f));

        static readonly Axis[] ConvBoxAxes = WithDoors(
            BuildRoadAxes(0.86f, 3.20f, 0.77f, 0.45f, -2.90f),
            Hinged("DoorL", "{dL:1}", VehicleFitmentSide.Left,
                   VehicleHingeAxis.Vertical, new Vector3(-0.98f, 2.38f, 0f), -65f),
            Hinged("DoorR", "{dR:1}", VehicleFitmentSide.Right,
                   VehicleHingeAxis.Vertical, new Vector3(0.98f, 2.38f, 0f), +65f),
            Hinged("Hood", "{hood:1}", VehicleFitmentSide.Centre,
                   VehicleHingeAxis.Lateral, new Vector3(0f, 4.08f, 0.60f), -70f));

        static readonly Axis[] AeroSemiAxes = WithDoors(
            BuildRoadAxes(0.84f, 2.95f, 0.75f, 0.50f, -1.70f, -2.90f),
            Hinged("DoorL", "{dL:1}", VehicleFitmentSide.Left,
                   VehicleHingeAxis.Vertical, new Vector3(-1.18f, 1.77f, 0f), -65f),
            Hinged("DoorR", "{dR:1}", VehicleFitmentSide.Right,
                   VehicleHingeAxis.Vertical, new Vector3(1.18f, 1.77f, 0f), +65f),
            Hinged("Hood", "{hood:1}", VehicleFitmentSide.Centre,
                   VehicleHingeAxis.Lateral, new Vector3(0f, 4.02f, 0.55f), -72f));

        static readonly Axis[] ClassicSemiAxes = WithDoors(
            BuildRoadAxes(0.84f, 3.45f, 0.75f, 0.50f, -1.60f, -2.80f),
            Hinged("DoorL", "{dL:1}", VehicleFitmentSide.Left,
                   VehicleHingeAxis.Vertical, new Vector3(-1.18f, 1.67f, 0f), -65f),
            Hinged("DoorR", "{dR:1}", VehicleFitmentSide.Right,
                   VehicleHingeAxis.Vertical, new Vector3(1.18f, 1.67f, 0f), +65f),
            Hinged("Hood", "{hood:1}", VehicleFitmentSide.Centre,
                   VehicleHingeAxis.Lateral, new Vector3(0f, 4.42f, 0.55f), -70f));

        // ---- the four towed bodies ---------------------------------------------------------------
        // One rig, one G: wheelR 0.50, mean(dualXi 0.60, dualXo 0.90) = 0.75, gearAft 2.00 behind
        // the kingpin. The stations and the kingpin are per BODY — the pups carry one axle at
        // −2.90 and a kingpin at 3.365; the 53s a tandem at −5.50/−6.70 and a kingpin at 7.175.

        // ⚠️ FOUR arrays now, not two: the reefers carry barn doors and the flatbeds do not, and the
        // hinge is at −L/2 + 0.02 so the pup's and the 53's differ. gearY is kingpinY − G.gearAft:
        // 3.365 − 2.00 on the pups, 7.175 − 2.00 on the 53s.

        static readonly Axis[] TrailerFlatbed28Axes =
            BuildTrailerAxes(0.75f, 0.50f, 3.365f - 2.00f, -2.90f);

        static readonly Axis[] TrailerFlatbed53Axes =
            BuildTrailerAxes(0.75f, 0.50f, 7.175f - 2.00f, -5.50f, -6.70f);

        static readonly Axis[] TrailerReefer28Axes =
            WithDoors(TrailerFlatbed28Axes, ReeferBarns(-8.53f / 2f + 0.02f));

        static readonly Axis[] TrailerReefer53Axes =
            WithDoors(TrailerFlatbed53Axes, ReeferBarns(-16.15f / 2f + 0.02f));

        /// <summary>
        /// Every road vehicle whose rig and sidecar are committed. Being here means the drop has
        /// LANDED and is hash-verified — it does <b>not</b> mean it is baked to a mesh. What is baked
        /// is <see cref="Baked"/>; why anything is not is <see cref="NotBaked"/>.
        /// </summary>
        public static readonly IReadOnlyList<Vehicle> Vehicles = new[]
        {
            new Vehicle(
                "dually3500",
                "docs/art/rigs/dually-iso-kit/vehicleIsoRig.js",
                SidecarFolder + "/vehicleIsoRig.dually3500.gameplay.json",
                "VehicleIso",
                meshAssetPath: "Assets/_Project/Data/Vehicles/Meshes/Dually3500VehicleMesh.asset",
                meshId: "vehiclemesh.dually_3500",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ `VehicleIso.resolve`, QUALIFIED. WidenExportedLiteral puts `build` on the
                    // GLOBAL; it does not put the closure's other privates in scope, so an
                    // unqualified `resolve({})` dies with "resolve is not defined" — which reads
                    // like the rig lacking the symbol rather than the shim missing.
                    FaceExpression = "build(VehicleIso.resolve({}))",
                    ExtraSymbols = new[] { "build" },
                },
                axes: DuallyAxes,
                chassisSource: new VehicleChassisSource
                {
                    Wheelbase = "VehicleIso.G.axF - VehicleIso.G.axR",
                    FrontTrack = "VehicleIso.G.frontWX * 2",
                    WheelRadius = "VehicleIso.G.wheelR",
                    FrontAxleY = "VehicleIso.G.axF",
                    RearAxleY = "VehicleIso.G.axR",
                    MaxInnerDeg = "VehicleIso.steer.maxInnerDeg",
                    MaxOuterDeg = "VehicleIso.steer.maxOuterDeg",
                    TravelFront = "VehicleIso.travel.F",
                    TravelRear = "VehicleIso.travel.R",
                },
                azimuthAftAnchor: "hitch", azimuthForeAnchor: "hoodLatch",
                bodyMustNotMove: new[] { "{roll:0.25}", "{steer:1}" },
                vehicleDefPath: "Assets/_Project/Data/Vehicles/Dually3500.asset",
                vehicleId: "vehicle.dually_3500",
                label: "Dually 3500"),

            // ⭐ THE OTTER 8x8 — the second vehicle, and the first amphibian. BAKED since #558's
            // material merge landed (2026-08-19): the art side folded the cockpit `mat` into `mesh`
            // and she paints 16 ramps instead of 17, so the facet shader takes her and
            // VehicleMeshDef.IsUsable stops refusing her.
            //
            // ⚠️ HER CANOPY BUILDS ARE STILL OVER THE CAP and are deliberately not baked: fitting
            // `screen` or `bimini` brings in `canvas` and `glass`, measured 17 for either alone and
            // 18 for both — which is also the `harbourHaul` PRESET. The base machine (plus tracked,
            // afloat, night and hatch, all measured 16) is what ships. A canopy Otter needs its own
            // ruling, and OtterIsoKitProbeTests pins both halves so neither can drift unnoticed.
            new Vehicle(
                "otter8x8",
                "docs/art/rigs/otter-iso-kit/amphibIsoRig.js",
                SidecarFolder + "/amphibIsoRig.otter8x8.gameplay.json",
                "AmphibIso",
                meshAssetPath: "Assets/_Project/Data/Vehicles/Meshes/Otter8x8VehicleMesh.asset",
                meshId: "vehiclemesh.otter_8x8",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ `AmphibIso.resolve`, QUALIFIED — same reason as the Dually's: the shim widens
                    // `build` onto the global but does not put the closure's other privates in scope.
                    FaceExpression = "build(AmphibIso.resolve({}))",
                    ExtraSymbols = new[] { "build" },
                },
                axes: OtterAxes,
                chassisSource: new VehicleChassisSource
                {
                    // Her axles are an ARRAY, bow to stern, so the wheelbase is first-to-last:
                    // 1.02 - (-1.02) = 2.04, which is her sidecar's WHEELS.axles_y span.
                    Wheelbase = "AmphibIso.G.axY[0] - AmphibIso.G.axY[3]",
                    // One wheelX serves all four axles — her tracks are parallel, unlike the
                    // Dually's front/rear split. 0.63 * 2 = 1.26 = sidecar WHEELS.track_width_m.
                    FrontTrack = "AmphibIso.G.wheelX * 2",
                    WheelRadius = "AmphibIso.G.wheelR",
                    FrontAxleY = "AmphibIso.G.axY[0]",
                    RearAxleY = "AmphibIso.G.axY[3]",
                    // Steer and travel stay "0": she is a SKID STEER with no steering axle
                    // (her sidecar's _excluded block says so in as many words) and her rig models
                    // no suspension travel. Measured zeros, not missing numbers.
                },
                // Her hull's two ends, both on the centreline — the boat vocabulary, because she is
                // a boat with wheels. transom sits at G.tailY, bow at G.bowY.
                azimuthAftAnchor: "transom", azimuthForeAnchor: "bow",
                // The master roll covers all eight wheels at once, so this does not need to be kept
                // in step with the eight per-wheel entries. She has no steer axis to check.
                bodyMustNotMove: new[] { "{roll:0.25}" },
                vehicleDefPath: "Assets/_Project/Data/Vehicles/Otter8x8.asset",
                vehicleId: "vehicle.otter_8x8",
                label: "Otter 8x8"),

            // =====================================================================================
            //  ⭐ THE ROAD FLEET — kit drop of 2026-08-27, six kits, NINE registered bodies.
            //
            //  Registered means LANDED and hash-verified. None of them is baked: this PR is intake
            //  and measurement only, so every one carries a NotBaked reason below.
            // =====================================================================================

            new Vehicle(
                "hightopVan",
                "docs/art/rigs/road-fleet-kit/hightop-van/vanIsoRig.js",
                SidecarFolder + "/vanIsoRig.hightopVan.gameplay.json",
                "VanIso",
                meshAssetPath: "Assets/_Project/Data/Vehicles/Meshes/HightopVanVehicleMesh.asset",
                meshId: "vehiclemesh.hightop_van",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ `VanIso.resolve`, QUALIFIED — the shim widens `build` onto the GLOBAL and
                    // does not put the closure's other privates in scope.
                    FaceExpression = "build(VanIso.resolve({}))",
                    ExtraSymbols = new[] { "build" },
                },
                axes: HightopVanAxes,
                chassisSource: new VehicleChassisSource
                {
                    Wheelbase = "VanIso.G.axF - VanIso.G.axR",
                    FrontTrack = "VanIso.G.frontWX * 2",
                    WheelRadius = "VanIso.G.wheelR",
                    FrontAxleY = "VanIso.G.axF",
                    RearAxleY = "VanIso.G.axR",
                    MaxInnerDeg = "VanIso.steer.maxInnerDeg",
                    MaxOuterDeg = "VanIso.steer.maxOuterDeg",
                    TravelFront = "VanIso.travel.F",
                    TravelRear = "VanIso.travel.R",
                },
                azimuthAftAnchor: "hitch", azimuthForeAnchor: "hoodLatch",
                vehicleDefPath: "Assets/_Project/Data/Vehicles/HightopVan.asset",
                vehicleId: "vehicle.hightop_van",
                bodyMustNotMove: new[] { "{roll:0.25}", "{steer:1}", "{dFL:1,dFR:1,barnL:1,barnR:1,slide:1,hood:1}" },
                doorGroups: new[] { Group("slide", "SlideDoor"), Group("barn", "BarnL", "BarnR"),
                                    Group("hood", "Hood"),
                                    Group("drive", "DoorFL"), Group("ride", "DoorFR") },
                label: "Hightop Van"),

            new Vehicle(
                "caboverBox",
                "docs/art/rigs/road-fleet-kit/boxtruck-cabover/boxIsoRig.js",
                SidecarFolder + "/boxIsoRig.caboverBox.gameplay.json",
                "BoxIso",
                meshAssetPath: "Assets/_Project/Data/Vehicles/Meshes/CaboverBoxVehicleMesh.asset",
                meshId: "vehiclemesh.cabover_box",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ `BoxIso.resolve`, QUALIFIED — the shim widens `build` onto the GLOBAL and
                    // does not put the closure's other privates in scope.
                    FaceExpression = "build(BoxIso.resolve({}))",
                    ExtraSymbols = new[] { "build" },
                },
                axes: CaboverBoxAxes,
                chassisSource: new VehicleChassisSource
                {
                    Wheelbase = "BoxIso.G.axF - BoxIso.G.axR",
                    FrontTrack = "BoxIso.G.frontWX * 2",
                    WheelRadius = "BoxIso.G.wheelR",
                    FrontAxleY = "BoxIso.G.axF",
                    RearAxleY = "BoxIso.G.axR",
                    MaxInnerDeg = "BoxIso.steer.maxInnerDeg",
                    MaxOuterDeg = "BoxIso.steer.maxOuterDeg",
                    TravelFront = "BoxIso.travel.F",
                    TravelRear = "BoxIso.travel.R",
                },
                // ⚠️ NOT hoodLatch. A cabover's cab sits OVER the engine and tilts as a whole, so
                // she has no hood at all — asking for one reads `undefined.x` at the admissibility
                // gate, which is a throw rather than a wrong answer, but only because the gate runs.
                azimuthAftAnchor: "rollup", azimuthForeAnchor: "tiltLatch",
                vehicleDefPath: "Assets/_Project/Data/Vehicles/CaboverBox.asset",
                vehicleId: "vehicle.cabover_box",
                bodyMustNotMove: new[] { "{roll:0.25}", "{steer:1}", "{dL:1,dR:1,tilt:1}" },
                doorGroups: new[] { Group("tilt", "CabTilt"),
                                    Group("drive", "DoorL"), Group("ride", "DoorR") },
                label: "Cabover Box Truck"),

            new Vehicle(
                "convBox",
                "docs/art/rigs/road-fleet-kit/boxtruck-conventional/convBoxIsoRig.js",
                SidecarFolder + "/convBoxIsoRig.convBox.gameplay.json",
                "ConvBoxIso",
                meshAssetPath: "Assets/_Project/Data/Vehicles/Meshes/ConvBoxVehicleMesh.asset",
                meshId: "vehiclemesh.conv_box",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ `ConvBoxIso.resolve`, QUALIFIED — the shim widens `build` onto the GLOBAL and
                    // does not put the closure's other privates in scope.
                    FaceExpression = "build(ConvBoxIso.resolve({}))",
                    ExtraSymbols = new[] { "build" },
                },
                axes: ConvBoxAxes,
                chassisSource: new VehicleChassisSource
                {
                    Wheelbase = "ConvBoxIso.G.axF - ConvBoxIso.G.axR",
                    FrontTrack = "ConvBoxIso.G.frontWX * 2",
                    WheelRadius = "ConvBoxIso.G.wheelR",
                    FrontAxleY = "ConvBoxIso.G.axF",
                    RearAxleY = "ConvBoxIso.G.axR",
                    MaxInnerDeg = "ConvBoxIso.steer.maxInnerDeg",
                    MaxOuterDeg = "ConvBoxIso.steer.maxOuterDeg",
                    TravelFront = "ConvBoxIso.travel.F",
                    TravelRear = "ConvBoxIso.travel.R",
                },
                azimuthAftAnchor: "rollup", azimuthForeAnchor: "hoodLatch",
                // ⚠️ Her cell is 448×352 at pivot (224,214), not the pack's 384×320 — she is 9.6 m
                // long. Nothing here says so: the extractor reads W/H/pivot off HER global, which is
                // what makes a per-vehicle cell free. It is asserted in RoadFleetBakeTests so a bake
                // that silently took the pack's cell (and cropped her tail) cannot pass.
                vehicleDefPath: "Assets/_Project/Data/Vehicles/ConvBox.asset",
                vehicleId: "vehicle.conv_box",
                bodyMustNotMove: new[] { "{roll:0.25}", "{steer:1}", "{dL:1,dR:1,hood:1}" },
                doorGroups: new[] { Group("hood", "Hood"),
                                    Group("drive", "DoorL"), Group("ride", "DoorR") },
                label: "Conventional Box Truck"),

            new Vehicle(
                "aeroSemi",
                "docs/art/rigs/road-fleet-kit/semi-aero/aeroSemiIsoRig.js",
                SidecarFolder + "/aeroSemiIsoRig.aeroSemi.gameplay.json",
                "AeroSemiIso",
                meshAssetPath: "Assets/_Project/Data/Vehicles/Meshes/AeroSemiVehicleMesh.asset",
                meshId: "vehiclemesh.aero_semi",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ `AeroSemiIso.resolve`, QUALIFIED — the shim widens `build` onto the GLOBAL and
                    // does not put the closure's other privates in scope.
                    FaceExpression = "build(AeroSemiIso.resolve({}))",
                    ExtraSymbols = new[] { "build" },
                },
                axes: AeroSemiAxes,
                chassisSource: new VehicleChassisSource
                {
                    // ⚠️ Her `axR` is the TANDEM CENTRE (the rig says so in its own comment), not an
                    // axle — which is exactly what a kinematic bicycle model wants for a tandem, and
                    // is why the wheelbase reads 5.25 rather than to either station.
                    Wheelbase = "AeroSemiIso.G.axF - AeroSemiIso.G.axR",
                    FrontTrack = "AeroSemiIso.G.frontWX * 2",
                    WheelRadius = "AeroSemiIso.G.wheelR",
                    FrontAxleY = "AeroSemiIso.G.axF",
                    RearAxleY = "AeroSemiIso.G.axR",
                    MaxInnerDeg = "AeroSemiIso.steer.maxInnerDeg",
                    MaxOuterDeg = "AeroSemiIso.steer.maxOuterDeg",
                    TravelFront = "AeroSemiIso.travel.F",
                    TravelRear = "AeroSemiIso.travel.R",
                },
                azimuthAftAnchor: "fifthWheel", azimuthForeAnchor: "hoodLatch",
                vehicleDefPath: "Assets/_Project/Data/Vehicles/AeroSemi.asset",
                vehicleId: "vehicle.aero_semi",
                bodyMustNotMove: new[] { "{roll:0.25}", "{steer:1}", "{dL:1,dR:1,hood:1}" },
                doorGroups: new[] { Group("hood", "Hood"),
                                    Group("drive", "DoorL"), Group("ride", "DoorR") },
                label: "Aero Sleeper Semi"),

            new Vehicle(
                "classicSemi",
                "docs/art/rigs/road-fleet-kit/semi-classic/classicSemiIsoRig.js",
                SidecarFolder + "/classicSemiIsoRig.classicSemi.gameplay.json",
                "ClassicSemiIso",
                meshAssetPath: "Assets/_Project/Data/Vehicles/Meshes/ClassicSemiVehicleMesh.asset",
                meshId: "vehiclemesh.classic_semi",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ `ClassicSemiIso.resolve`, QUALIFIED — the shim widens `build` onto the GLOBAL and
                    // does not put the closure's other privates in scope.
                    FaceExpression = "build(ClassicSemiIso.resolve({}))",
                    ExtraSymbols = new[] { "build" },
                },
                axes: ClassicSemiAxes,
                chassisSource: new VehicleChassisSource
                {
                    Wheelbase = "ClassicSemiIso.G.axF - ClassicSemiIso.G.axR",
                    FrontTrack = "ClassicSemiIso.G.frontWX * 2",
                    WheelRadius = "ClassicSemiIso.G.wheelR",
                    FrontAxleY = "ClassicSemiIso.G.axF",
                    RearAxleY = "ClassicSemiIso.G.axR",
                    MaxInnerDeg = "ClassicSemiIso.steer.maxInnerDeg",
                    MaxOuterDeg = "ClassicSemiIso.steer.maxOuterDeg",
                    TravelFront = "ClassicSemiIso.travel.F",
                    TravelRear = "ClassicSemiIso.travel.R",
                },
                azimuthAftAnchor: "fifthWheel", azimuthForeAnchor: "hoodLatch",
                vehicleDefPath: "Assets/_Project/Data/Vehicles/ClassicSemi.asset",
                vehicleId: "vehicle.classic_semi",
                bodyMustNotMove: new[] { "{roll:0.25}", "{steer:1}", "{dL:1,dR:1,hood:1}" },
                doorGroups: new[] { Group("hood", "Hood"),
                                    Group("drive", "DoorL"), Group("ride", "DoorR") },
                label: "Classic Long-Nose Semi"),

            // ---- the TRAILER SET: ONE rig, ONE sidecar, FOUR towed bodies --------------------
            // Four entries rather than one, because Baked/NotBaked, the mesh, the cell and the
            // ramp filter are all PER BODY — the pups take the 384×320 road cell and the 53s a
            // 640×480 one. See Vehicle.Pick for why (file, pick) is the key and a per-file cache
            // is a bug.
            //
            // ⚠️⚠️ FOUR PLACES CARRY THE PICK, and every one of them fails SILENTLY without it,
            // because `trailerIsoRig.js` falls back to reefer53 rather than throwing:
            //   1. Extraction.FaceExpression  — which body's faces are baked;
            //   2. Extraction.HullScope       — which body's CELL and pivot (384×320 vs 640×480);
            //   3. Extraction.ViewOptions     — which body the azimuth anchors are read for;
            //   4. RestPose                   — which body the articulation probes measure from.
            // Miss any one and the result is a plausible trailer.
            //
            // ⚠️ NO VehicleDef, DELIBERATELY. Every field on that asset is a DRIVEN machine's —
            // top speed, acceleration, steering authority, camera height — and a towed body has
            // none of them (VehicleKinds.IsDrivable(TowedBody) is false by explicit switch). PR 2
            // bakes what she looks like; what a towed body needs to be placed and coupled is PR 3's
            // to design, and inventing a half-used def here would be the "0 = not applicable" shape
            // VehicleMeshDef's own class doc refuses.

            new Vehicle(
                "trailerFlatbed28",
                "docs/art/rigs/road-fleet-kit/trailers/trailerIsoRig.js",
                SidecarFolder + "/trailerIsoRig.trailers.gameplay.json",
                "TrailerIso",
                pick: "flatbed28",
                meshAssetPath:
                    "Assets/_Project/Data/Vehicles/Meshes/TrailerFlatbed28VehicleMesh.asset",
                meshId: "vehiclemesh.trailer_flatbed_28",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ The BODY IS NAMED. `resolve({})` defaults to reefer53 on this rig, so an
                    // extraction that omitted the pick would bake four identical reefer53s and each
                    // would look like a perfectly good trailer.
                    FaceExpression = "build(TrailerIso.resolve({body:'flatbed28'}))",
                    // `cellOf` is RECONSTRUCTED (RigMeshSymbols.Reconstructions): the rig publishes
                    // cellFor/pivotFor as two calls and a bare {W,H,cx,gy} record, and the extractor
                    // reads one object with W/H/pivot/defaultElev on it.
                    ExtraSymbols = new[] { "build", "cellOf" },
                    HullScope = "cellOf('flatbed28')",
                    ViewOptions = "{body:'flatbed28'}",
                },
                axes: TrailerFlatbed28Axes,
                chassisSource: TrailerChassis("flatbed28"),
                // A towed body has no hood and no front axle, so neither of the road pack's anchor
                // pairs exists. Her aft→fore runs tail to kingpin; her abeam pair is the axle hubs.
                azimuthAftAnchor: "rear", azimuthForeAnchor: "kingpin",
                azimuthAbeamLeftAnchor: "wheelL", azimuthAbeamRightAnchor: "wheelR",
                restPose: "{body:'flatbed28'}",
                sidecarBodyScope: "flatbed28",
                // ⭐ `{gear:0}` IS BACK. PR 2 left it out because the gear was baked INTO the body
                // — it telescopes, and one mesh plus an offset could not carry it. PR 3a splits it
                // on MATERIAL into rigid shoes and state-swapped legs, so the body must now keep
                // not one face of it. The deferral is lifted rather than forgotten.
                bodyMustNotMove: new[] { "{roll:0.25}", "{gear:0}" },
                doorGroups: new[] { GearGroup(3.365f - 2.00f) },
                label: "Flatbed Trailer 28 ft"),

            new Vehicle(
                "trailerFlatbed53",
                "docs/art/rigs/road-fleet-kit/trailers/trailerIsoRig.js",
                SidecarFolder + "/trailerIsoRig.trailers.gameplay.json",
                "TrailerIso",
                pick: "flatbed53",
                meshAssetPath:
                    "Assets/_Project/Data/Vehicles/Meshes/TrailerFlatbed53VehicleMesh.asset",
                meshId: "vehiclemesh.trailer_flatbed_53",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    FaceExpression = "build(TrailerIso.resolve({body:'flatbed53'}))",
                    ExtraSymbols = new[] { "build", "cellOf" },
                    HullScope = "cellOf('flatbed53')",
                    ViewOptions = "{body:'flatbed53'}",
                },
                axes: TrailerFlatbed53Axes,
                chassisSource: TrailerChassis("flatbed53"),
                azimuthAftAnchor: "rear", azimuthForeAnchor: "kingpin",
                azimuthAbeamLeftAnchor: "wheelL", azimuthAbeamRightAnchor: "wheelR",
                restPose: "{body:'flatbed53'}",
                sidecarBodyScope: "flatbed53",
                bodyMustNotMove: new[] { "{roll:0.25}", "{gear:0}" },
                doorGroups: new[] { GearGroup(7.175f - 2.00f) },
                label: "Flatbed Trailer 53 ft"),

            new Vehicle(
                "trailerReefer28",
                "docs/art/rigs/road-fleet-kit/trailers/trailerIsoRig.js",
                SidecarFolder + "/trailerIsoRig.trailers.gameplay.json",
                "TrailerIso",
                pick: "reefer28",
                meshAssetPath:
                    "Assets/_Project/Data/Vehicles/Meshes/TrailerReefer28VehicleMesh.asset",
                meshId: "vehiclemesh.trailer_reefer_28",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    FaceExpression = "build(TrailerIso.resolve({body:'reefer28'}))",
                    ExtraSymbols = new[] { "build", "cellOf" },
                    HullScope = "cellOf('reefer28')",
                    ViewOptions = "{body:'reefer28'}",
                },
                axes: TrailerReefer28Axes,
                chassisSource: TrailerChassis("reefer28"),
                azimuthAftAnchor: "rear", azimuthForeAnchor: "kingpin",
                azimuthAbeamLeftAnchor: "wheelL", azimuthAbeamRightAnchor: "wheelR",
                restPose: "{body:'reefer28'}",
                sidecarBodyScope: "reefer28",
                bodyMustNotMove: new[] { "{roll:0.25}", "{gear:0}", "{barnL:1,barnR:1}" },
                doorGroups: new[] { GearGroup(3.365f - 2.00f), ReeferDoorsGroup(8.53f) },
                label: "Reefer Trailer 28 ft"),

            // ⚠️ THE RIG'S OWN DEFAULT BODY. She is the one a missing pick silently produces, so she
            // is also the one whose bake proves nothing about the other three — the geometry hash in
            // TrailerIsoKitProbeTests is what separates them, not this entry.
            new Vehicle(
                "trailerReefer53",
                "docs/art/rigs/road-fleet-kit/trailers/trailerIsoRig.js",
                SidecarFolder + "/trailerIsoRig.trailers.gameplay.json",
                "TrailerIso",
                pick: "reefer53",
                meshAssetPath:
                    "Assets/_Project/Data/Vehicles/Meshes/TrailerReefer53VehicleMesh.asset",
                meshId: "vehiclemesh.trailer_reefer_53",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    FaceExpression = "build(TrailerIso.resolve({body:'reefer53'}))",
                    ExtraSymbols = new[] { "build", "cellOf" },
                    HullScope = "cellOf('reefer53')",
                    ViewOptions = "{body:'reefer53'}",
                },
                axes: TrailerReefer53Axes,
                chassisSource: TrailerChassis("reefer53"),
                azimuthAftAnchor: "rear", azimuthForeAnchor: "kingpin",
                azimuthAbeamLeftAnchor: "wheelL", azimuthAbeamRightAnchor: "wheelR",
                restPose: "{body:'reefer53'}",
                sidecarBodyScope: "reefer53",
                bodyMustNotMove: new[] { "{roll:0.25}", "{gear:0}", "{barnL:1,barnR:1}" },
                doorGroups: new[] { GearGroup(7.175f - 2.00f), ReeferDoorsGroup(16.15f) },
                label: "Reefer Trailer 53 ft"),
        };

        /// <summary>
        /// ⭐ <b>A towed body's chassis, in the trailer rig's own words</b> — one expression set with
        /// the body substituted, because all four really do share a vocabulary and typing it four
        /// times is four chances to point one of them at another body's numbers.
        ///
        /// <para><b>What "wheelbase" means on something with no front axle.</b> The rig publishes it
        /// directly as <c>kingpinToAxleCentre</c> — the distance from the coupling to the centre of
        /// her axle group, which is the length scale her off-tracking is solved on, exactly as a
        /// truck's axle separation is. 6.265 m on the pups, 13.275 on the 53s.</para>
        ///
        /// <para><b>And her "front axle" is the KINGPIN.</b> The suspension pivots there — the
        /// coupling plane holds 1.18 m while the tail drops — so the front reference travels
        /// <b>zero</b> and the rear travels the rig's one published group travel. That is not a
        /// placeholder pair: it is the same <c>dz(y)</c> the def's tooltip describes, transcribed
        /// for a machine whose front end is held up by a tractor.</para>
        ///
        /// <para>Steer stays <c>"0"</c> on both angles. Measured, not missing:
        /// <c>trailerIsoRig.js</c> resolves no <c>steer</c> axis and exports no <c>steer</c> block,
        /// and the kit's README says <i>"No steering — towed bodies"</i>.</para>
        /// </summary>
        static VehicleChassisSource TrailerChassis(string pick) => new VehicleChassisSource
        {
            Wheelbase = $"TrailerIso.BODIES['{pick}'].kingpinToAxleCentre",
            // Her track is the outer duals — she has one axle width, not a front and a rear.
            FrontTrack = "TrailerIso.G.dualXo * 2",
            WheelRadius = "TrailerIso.G.wheelR",
            FrontAxleY = $"TrailerIso.BODIES['{pick}'].kingpinY",
            // The centre of the axle GROUP: one station on a pup, the mean of two on a 53.
            RearAxleY = "(function(a){var s=0;for(var i=0;i<a.length;i++)s+=a[i];return s/a.length;})" +
                        $"(TrailerIso.BODIES['{pick}'].axles)",
            TravelFront = "0",
            TravelRear = "TrailerIso.travel.group",
        };


        /// <summary>
        /// Vehicles that currently have a baked mesh — a <c>VehicleMeshDef</c> and its wheel fittings
        /// committed under <c>Assets/_Project/Data/Vehicles/</c>, produced by
        /// <c>VehicleMeshAssetBaker</c>.
        /// </summary>
        public static readonly IReadOnlyList<string> Baked = new[]
        {
            "dually3500", "otter8x8",
            // The road fleet — ALL NINE of the drop's bodies. Eight baked in PR 2 (#671); the
            // hightop van joined the day her re-stamped sidecar landed (2026-08-27, same day asked).
            "caboverBox", "convBox", "hightopVan", "aeroSemi", "classicSemi",
            "trailerFlatbed28", "trailerFlatbed53", "trailerReefer28", "trailerReefer53",
        };

        /// <summary>
        /// ⭐ <b>Registered refusals: a vehicle that is not baked, and why.</b> The coverage test reads
        /// this, so nothing can be quietly left out — a vehicle in neither <see cref="Baked"/> nor here
        /// fails.
        ///
        /// <para><b>EMPTY, and that is the steady state to defend.</b> Three entries have lived here
        /// and all three left the way an entry here should — deleted, not reworded (the last: the
        /// hightop van's stamp refusal, discharged 2026-08-27 when upstream's re-stamp landed).</para>
        ///
        /// <para>The <b>Dually</b> held one from #548 until 2026-08-17, blocked on an architecture
        /// ruling rather than any technical obstacle; the ruling was given (lead-architect, on #548)
        /// and ADR 0035 records it.</para>
        ///
        /// <para>The <b>Otter</b> held one from #558 until 2026-08-19, blocked on something no
        /// vehicle-side change could fix: she painted 17 colour ramps against the facet shader's
        /// <c>float4[16]</c> <c>_RampMeta</c>, and unlike the Dually and the zodiac she used every
        /// one she declared, so filtering to the used set could not save her. It was an ART fix, and
        /// the art side made it — the cockpit <c>mat</c> folded into <c>mesh</c>, one face moved by
        /// ≤ 3/255 — so she measures 16 and bakes. ⚠️ Her CANOPY builds are still over (17 with
        /// <c>screen</c> or <c>bimini</c>, 18 with both) and that limit lives in
        /// <c>OtterIsoKitProbeTests</c> and on her <see cref="Vehicles"/> entry, not here: this table
        /// is about whole vehicles that are not baked, and she is.</para>
        ///
        /// <para>A new drop that is in neither list fails <c>VehicleRigFleetTests</c>. That is the
        /// whole point of the table: art arrives by PR, and this is the thing that stops one arriving
        /// unnoticed.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> NotBaked =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Three entries have lived here and all three left the way an entry here should —
                // deleted, not reworded. The last was the hightop van's stamp refusal
                // (2026-08-27, discharged the same day when upstream's re-stamp landed and her
                // full digest matched her rig — see the git history of SidecarHashRefused).
            };

        /// <summary>
        /// ⚠️⚠️ <b>REGISTERED, BUT HER SIDECAR DOES NOT PIN HER RIG — so her geometry may not be
        /// read.</b> A named, reasoned ledger, exactly like <see cref="NotBaked"/>, because the
        /// alternative is worse in both directions: a red test nobody may merge, or a hash law
        /// quietly loosened until it stops catching the thing it exists for.
        ///
        /// <para><b>What a mismatch means.</b> <c>derivedFromRigSha256</c> is the sidecar's claim
        /// that its polygons, thresholds and colliders were cut from THIS rig. A sidecar whose
        /// numbers came from a different shape is worse than no sidecar, so the repo's rule is a
        /// refusal rather than a warning. That rule does not change here — an entry in this table
        /// records the refusal, it does not excuse it, and
        /// <c>VehicleRigFleetTests</c> additionally forbids anything listed here from being
        /// <see cref="Baked"/>.</para>
        ///
        /// <para><b>⚠️ The fix is NEVER to re-stamp the sidecar here.</b> <c>docs/art/rigs/**</c> is
        /// the art director's lane; a hash corrected on our side is a hash that comes back wrong on
        /// the next regeneration, and re-stamping a bad stamp fakes freshness — the one thing the
        /// pin exists to prevent. It goes back upstream.</para>
        ///
        /// <para>The day a re-stamped sidecar lands, the test that reads this goes RED on "this is
        /// no longer refused" and the entry gets deleted. That is the same shape as
        /// <see cref="NotBaked"/> and it is why both are tables rather than comments.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> SidecarHashRefused =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // The hightop van lived here from #668's intake (her stamp shared exactly the first
                // 16 hex digits with her rig's true LF hash and diverged after — a stamping defect,
                // proved by her own contract.json carrying the correct value). Upstream's re-stamp
                // landed 2026-08-27: the delivered sidecar differs from the committed one by the
                // stamp line ALONE, and the new stamp full-digest-matches the rig. Entry deleted per
                // this table's own law: discharged, not reworded.
            };
    }
}
#endif
