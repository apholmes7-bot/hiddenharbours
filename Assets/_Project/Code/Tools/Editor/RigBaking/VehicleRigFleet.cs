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

            public Axis(string slot, string probe, VehicleFitmentMotion motion,
                        VehicleFitmentSide side, int sideSign, Vector3 pivot,
                        float yMin = float.NegativeInfinity,
                        float yMax = float.PositiveInfinity)
            {
                Slot = slot; Probe = probe; Motion = motion; Side = side;
                SideSign = sideSign; Pivot = pivot;
                YMin = yMin; YMax = yMax;
            }
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
            /// is her front-axle abeam pair, which every vehicle rig publishes under the same two
            /// names; this one has to be declared because the names are each rig's own. The Dually
            /// runs stern-to-bow as <c>hitch</c>→<c>hoodLatch</c>; the Otter, a boat with wheels,
            /// as <c>transom</c>→<c>bow</c>.
            ///
            /// <para>The point of a SECOND oracle is that it is independent: if the two disagree the
            /// baker refuses rather than picking one, because a mirrored heading map is the kind of
            /// wrong that looks fine until she drives backwards.</para>
            /// </summary>
            public readonly string AzimuthAftAnchor, AzimuthForeAnchor;

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
                           string vehicleDefPath = null, string vehicleId = null, string label = null)
            {
                Key = key; ScriptPath = scriptPath; SidecarPath = sidecarPath; GlobalName = globalName;
                Pick = pick;
                MeshAssetPath = meshAssetPath; MeshId = meshId;
                FaceBuilderName = faceBuilderName;
                Extraction = extraction;
                Axes = axes ?? Array.Empty<Axis>();
                ChassisSource = chassisSource;
                AzimuthAftAnchor = azimuthAftAnchor; AzimuthForeAnchor = azimuthForeAnchor;
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
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ `VanIso.resolve`, QUALIFIED — the shim widens `build` onto the GLOBAL and
                    // does not put the closure's other privates in scope.
                    FaceExpression = "build(VanIso.resolve({}))",
                    ExtraSymbols = new[] { "build" },
                },
                azimuthAftAnchor: "hitch", azimuthForeAnchor: "hoodLatch",
                label: "Hightop Van"),

            new Vehicle(
                "caboverBox",
                "docs/art/rigs/road-fleet-kit/boxtruck-cabover/boxIsoRig.js",
                SidecarFolder + "/boxIsoRig.caboverBox.gameplay.json",
                "BoxIso",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ `BoxIso.resolve`, QUALIFIED — the shim widens `build` onto the GLOBAL and
                    // does not put the closure's other privates in scope.
                    FaceExpression = "build(BoxIso.resolve({}))",
                    ExtraSymbols = new[] { "build" },
                },
                azimuthAftAnchor: "rollup", azimuthForeAnchor: "tiltLatch",
                label: "Cabover Box Truck"),

            new Vehicle(
                "convBox",
                "docs/art/rigs/road-fleet-kit/boxtruck-conventional/convBoxIsoRig.js",
                SidecarFolder + "/convBoxIsoRig.convBox.gameplay.json",
                "ConvBoxIso",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ `ConvBoxIso.resolve`, QUALIFIED — the shim widens `build` onto the GLOBAL and
                    // does not put the closure's other privates in scope.
                    FaceExpression = "build(ConvBoxIso.resolve({}))",
                    ExtraSymbols = new[] { "build" },
                },
                azimuthAftAnchor: "rollup", azimuthForeAnchor: "hoodLatch",
                label: "Conventional Box Truck"),

            new Vehicle(
                "aeroSemi",
                "docs/art/rigs/road-fleet-kit/semi-aero/aeroSemiIsoRig.js",
                SidecarFolder + "/aeroSemiIsoRig.aeroSemi.gameplay.json",
                "AeroSemiIso",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ `AeroSemiIso.resolve`, QUALIFIED — the shim widens `build` onto the GLOBAL and
                    // does not put the closure's other privates in scope.
                    FaceExpression = "build(AeroSemiIso.resolve({}))",
                    ExtraSymbols = new[] { "build" },
                },
                azimuthAftAnchor: "fifthWheel", azimuthForeAnchor: "hoodLatch",
                label: "Aero Sleeper Semi"),

            new Vehicle(
                "classicSemi",
                "docs/art/rigs/road-fleet-kit/semi-classic/classicSemiIsoRig.js",
                SidecarFolder + "/classicSemiIsoRig.classicSemi.gameplay.json",
                "ClassicSemiIso",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ `ClassicSemiIso.resolve`, QUALIFIED — the shim widens `build` onto the GLOBAL and
                    // does not put the closure's other privates in scope.
                    FaceExpression = "build(ClassicSemiIso.resolve({}))",
                    ExtraSymbols = new[] { "build" },
                },
                azimuthAftAnchor: "fifthWheel", azimuthForeAnchor: "hoodLatch",
                label: "Classic Long-Nose Semi"),

            // ---- the TRAILER SET: ONE rig, ONE sidecar, FOUR towed bodies --------------------
            // Four entries rather than one, because Baked/NotBaked, the mesh, the cell and the
            // ramp filter are all PER BODY — the pups take the 384×320 road cell and the 53s a
            // 640×480 one. See Vehicle.Pick for why (file, pick) is the key and a per-file cache
            // is a bug.

            new Vehicle(
                "trailerFlatbed28",
                "docs/art/rigs/road-fleet-kit/trailers/trailerIsoRig.js",
                SidecarFolder + "/trailerIsoRig.trailers.gameplay.json",
                "TrailerIso",
                pick: "flatbed28",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ The BODY IS NAMED. `resolve({})` defaults to reefer53 on this rig, so an
                    // extraction that omitted the pick would bake four identical reefer53s and each
                    // would look like a perfectly good trailer.
                    FaceExpression = "build(TrailerIso.resolve({body:'flatbed28'}))",
                    ExtraSymbols = new[] { "build" },
                },
                // A towed body has no hood and no front axle, so neither of the road pack's anchor
                // pairs exists. Her aft→fore runs tail to kingpin; her abeam pair is wheelL/wheelR
                // rather than wheelFL/wheelFR, which the bake will have to be told (PR 2).
                azimuthAftAnchor: "rear", azimuthForeAnchor: "kingpin",
                label: "Flatbed Trailer 28 ft"),

            new Vehicle(
                "trailerFlatbed53",
                "docs/art/rigs/road-fleet-kit/trailers/trailerIsoRig.js",
                SidecarFolder + "/trailerIsoRig.trailers.gameplay.json",
                "TrailerIso",
                pick: "flatbed53",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ The BODY IS NAMED. `resolve({})` defaults to reefer53 on this rig, so an
                    // extraction that omitted the pick would bake four identical reefer53s and each
                    // would look like a perfectly good trailer.
                    FaceExpression = "build(TrailerIso.resolve({body:'flatbed53'}))",
                    ExtraSymbols = new[] { "build" },
                },
                // A towed body has no hood and no front axle, so neither of the road pack's anchor
                // pairs exists. Her aft→fore runs tail to kingpin; her abeam pair is wheelL/wheelR
                // rather than wheelFL/wheelFR, which the bake will have to be told (PR 2).
                azimuthAftAnchor: "rear", azimuthForeAnchor: "kingpin",
                label: "Flatbed Trailer 53 ft"),

            new Vehicle(
                "trailerReefer28",
                "docs/art/rigs/road-fleet-kit/trailers/trailerIsoRig.js",
                SidecarFolder + "/trailerIsoRig.trailers.gameplay.json",
                "TrailerIso",
                pick: "reefer28",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ The BODY IS NAMED. `resolve({})` defaults to reefer53 on this rig, so an
                    // extraction that omitted the pick would bake four identical reefer53s and each
                    // would look like a perfectly good trailer.
                    FaceExpression = "build(TrailerIso.resolve({body:'reefer28'}))",
                    ExtraSymbols = new[] { "build" },
                },
                // A towed body has no hood and no front axle, so neither of the road pack's anchor
                // pairs exists. Her aft→fore runs tail to kingpin; her abeam pair is wheelL/wheelR
                // rather than wheelFL/wheelFR, which the bake will have to be told (PR 2).
                azimuthAftAnchor: "rear", azimuthForeAnchor: "kingpin",
                label: "Reefer Trailer 28 ft"),

            new Vehicle(
                "trailerReefer53",
                "docs/art/rigs/road-fleet-kit/trailers/trailerIsoRig.js",
                SidecarFolder + "/trailerIsoRig.trailers.gameplay.json",
                "TrailerIso",
                pick: "reefer53",
                faceBuilderName: "build",
                extraction: new RigHullExtraction
                {
                    // ⚠️ The BODY IS NAMED. `resolve({})` defaults to reefer53 on this rig, so an
                    // extraction that omitted the pick would bake four identical reefer53s and each
                    // would look like a perfectly good trailer.
                    FaceExpression = "build(TrailerIso.resolve({body:'reefer53'}))",
                    ExtraSymbols = new[] { "build" },
                },
                // A towed body has no hood and no front axle, so neither of the road pack's anchor
                // pairs exists. Her aft→fore runs tail to kingpin; her abeam pair is wheelL/wheelR
                // rather than wheelFL/wheelFR, which the bake will have to be told (PR 2).
                azimuthAftAnchor: "rear", azimuthForeAnchor: "kingpin",
                label: "Reefer Trailer 53 ft"),
        };


        /// <summary>
        /// Vehicles that currently have a baked mesh — a <c>VehicleMeshDef</c> and its wheel fittings
        /// committed under <c>Assets/_Project/Data/Vehicles/</c>, produced by
        /// <c>VehicleMeshAssetBaker</c>.
        /// </summary>
        public static readonly IReadOnlyList<string> Baked = new[] { "dually3500", "otter8x8" };

        /// <summary>
        /// ⭐ <b>Registered refusals: a vehicle that is not baked, and why.</b> The coverage test reads
        /// this, so nothing can be quietly left out — a vehicle in neither <see cref="Baked"/> nor here
        /// fails.
        ///
        /// <para><b>Empty, and that is the good outcome.</b> Two entries have lived here and both
        /// left the way an entry here should — deleted, not reworded.</para>
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
                ["hightopVan"] =
                    "PR 1 of 4 (kit intake + V8 probes) lands the art and MEASURES it; it bakes nothing, by " +
                    "design. Her bake is PR 2's, and every number it needs is pinned in " +
                    "RoadFleetKitProbeTests. ⚠️ AND SHE HAS A SECOND, HARDER BLOCKER: her gameplay " +
                    "sidecar's derivedFromRigSha256 does NOT match her rig on disk, so her geometry may not " +
                    "be read at all — see SidecarHashRefused. Measured 15 used ramps at the bake pose (fits " +
                    "the float4[16] _RampMeta), 16 across every build including night.",
                ["caboverBox"] =
                    "PR 1 of 4 (kit intake + V8 probes) lands the art and MEASURES it; it bakes nothing, by " +
                    "design. Her bake is PR 2's, and every number it needs is pinned in " +
                    "RoadFleetKitProbeTests. Measured 16 used ramps at the bake pose — it fits _RampMeta " +
                    "with ZERO headroom, and 17 if one mesh had to carry the night lamp as well. The day " +
                    "mesh is what PR 2 bakes.",
                ["convBox"] =
                    "PR 1 of 4 (kit intake + V8 probes) lands the art and MEASURES it; it bakes nothing, by " +
                    "design. Her bake is PR 2's, and every number it needs is pinned in " +
                    "RoadFleetKitProbeTests. Measured 16 used ramps at the bake pose — fits with zero " +
                    "headroom, 17 with the night lamp. ⚠️ Her cell is 448×352, NOT the pack's 384×320 road " +
                    "cell: her sidecar says so and the bake must read it rather than assuming the pack's.",
                ["aeroSemi"] =
                    "PR 1 of 4 (kit intake + V8 probes) lands the art and MEASURES it; it bakes nothing, by " +
                    "design. Her bake is PR 2's, and every number it needs is pinned in " +
                    "RoadFleetKitProbeTests. Measured 15 used ramps at the bake pose, 16 across every build " +
                    "including night. ⚠️ Her rear is a TANDEM on ONE roll axis per side (wRL/wRR each move " +
                    "238 faces = two 119-face stations at y = −2.90 and −1.70), so her fittings need the " +
                    "Otter's station windows, not the Dually's per-wheel probes.",
                ["classicSemi"] =
                    "PR 1 of 4 (kit intake + V8 probes) lands the art and MEASURES it; it bakes nothing, by " +
                    "design. Her bake is PR 2's, and every number it needs is pinned in " +
                    "RoadFleetKitProbeTests. Measured 16 used ramps at the bake pose — fits with zero " +
                    "headroom, 17 with the night lamp. Tandem rear on one axis per side, stations at y = " +
                    "−2.80 and −1.60.",
                ["trailerFlatbed28"] =
                    "PR 1 of 4 (kit intake + V8 probes) lands the art and MEASURES it; it bakes nothing, by " +
                    "design. Her bake is PR 2's, and every number it needs is pinned in " +
                    "RoadFleetKitProbeTests. Measured 8 used ramps. Single axle, 384×320 road cell. Her " +
                    "deck is the set's one new material (wood) and it is the reason the ramp table is " +
                    "unioned over all four bodies — see the trailerIsoRig.js entry in " +
                    "RigMeshSymbols.Reconstructions.",
                ["trailerFlatbed53"] =
                    "PR 1 of 4 (kit intake + V8 probes) lands the art and MEASURES it; it bakes nothing, by " +
                    "design. Her bake is PR 2's, and every number it needs is pinned in " +
                    "RoadFleetKitProbeTests. Measured 8 used ramps. ⚠️ 640×480 cell @ 320,300, and a TANDEM " +
                    "(wL/wR each move 254 faces = two 127-face stations at y = −6.70 and −5.50).",
                ["trailerReefer28"] =
                    "PR 1 of 4 (kit intake + V8 probes) lands the art and MEASURES it; it bakes nothing, by " +
                    "design. Her bake is PR 2's, and every number it needs is pinned in " +
                    "RoadFleetKitProbeTests. Measured 11 used ramps, 12 with the night pass. Single axle, " +
                    "384×320 cell, doors that swing to 255° (54 faces, 27 a side).",
                ["trailerReefer53"] =
                    "PR 1 of 4 (kit intake + V8 probes) lands the art and MEASURES it; it bakes nothing, by " +
                    "design. Her bake is PR 2's, and every number it needs is pinned in " +
                    "RoadFleetKitProbeTests. Measured 11 used ramps, 12 with the night pass. ⚠️ 640×480 " +
                    "cell @ 320,300, tandem at y = −6.70 / −5.50. She is also the rig's DEFAULT body, which " +
                    "is what makes a missing pick silently render her in the other three's place.",
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
                ["hightopVan"] =
                    "MEASURED 2026-08-27, on intake. vanIsoRig.js hashes (LF) to " +
                    "1ac7804ddb045ec3a9c9ba8797b8025547c21649d5ea70c61dc7b6e8ee048eb3; her gameplay " +
                    "sidecar pins " +
                    "1ac7804ddb045ec3cd41ab1952d33513b74fdcae64e1710b2b74c46eb2e35a41. " +
                    "⚠️ THE FIRST 16 HEX DIGITS ARE IDENTICAL AND THE REMAINING 48 ARE NOT, which is " +
                    "not something a reshaped rig does — a changed vertex changes the whole digest. " +
                    "It is a STAMPING defect in one file, and her own hightopVan.contract.json " +
                    "carries the CORRECT hash, which is the independent evidence: the kit measured " +
                    "the right rig and only the sidecar's stamp is wrong. Five of the drop's six " +
                    "sidecars pin correctly. Her rig, contract, sheets and harness are all sound " +
                    "and are committed as shipped; what is refused is READING HER SIDECAR'S " +
                    "GEOMETRY — thresholds, cargo, colliders, seats — which is exactly what PR 2's " +
                    "bake and PR 3's placement need. Re-stamp is owed UPSTREAM (art director); do " +
                    "not fix it in this repo.",
            };
    }
}
#endif
