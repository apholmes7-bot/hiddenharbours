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

            /// <summary>The point this fitting turns about, in rig metres. For a front wheel this is
            /// the hub centre, which is ALSO a point on its own vertical steer axis (the rig models
            /// no kingpin offset, caster or scrub radius) — so ONE pivot serves both rotations, and
            /// the fitting needs no articulation machinery beyond a single local rotation.</summary>
            public readonly Vector3 Pivot;

            public Axis(string slot, string probe, VehicleFitmentMotion motion,
                        VehicleFitmentSide side, int sideSign, Vector3 pivot)
            {
                Slot = slot; Probe = probe; Motion = motion; Side = side;
                SideSign = sideSign; Pivot = pivot;
            }
        }

        /// <summary>One road vehicle: where its rig and sidecar are, and what installs it.</summary>
        public readonly struct Vehicle
        {
            /// <summary>Stable key — the sidecar's own <c>variant</c>, so the file and the table cannot
            /// drift apart.</summary>
            public readonly string Key;
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
                           string meshAssetPath = null, string meshId = null,
                           string faceBuilderName = null, RigHullExtraction extraction = null,
                           IReadOnlyList<Axis> axes = null,
                           IReadOnlyList<string> bodyMustNotMove = null,
                           string vehicleDefPath = null, string vehicleId = null, string label = null)
            {
                Key = key; ScriptPath = scriptPath; SidecarPath = sidecarPath; GlobalName = globalName;
                MeshAssetPath = meshAssetPath; MeshId = meshId;
                FaceBuilderName = faceBuilderName;
                Extraction = extraction;
                Axes = axes ?? Array.Empty<Axis>();
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
                bodyMustNotMove: new[] { "{roll:0.25}", "{steer:1}" },
                vehicleDefPath: "Assets/_Project/Data/Vehicles/Dually3500.asset",
                vehicleId: "vehicle.dually_3500",
                label: "Dually 3500"),

            // ⭐ THE OTTER 8x8 — the second vehicle, and the first amphibian. INTAKE ONLY: her rig
            // and sidecar are landed and hash-verified, and she carries no bake fields because she
            // is not baked (see NotBaked for the reason, which is a hard blocker rather than a
            // decision). When she is baked, this entry grows the same fields the Dually's has.
            new Vehicle(
                "otter8x8",
                "docs/art/rigs/otter-iso-kit/amphibIsoRig.js",
                SidecarFolder + "/amphibIsoRig.otter8x8.gameplay.json",
                "AmphibIso",
                vehicleId: "vehicle.otter_8x8",
                label: "Otter 8x8"),
        };


        /// <summary>
        /// Vehicles that currently have a baked mesh — a <c>VehicleMeshDef</c> and its wheel fittings
        /// committed under <c>Assets/_Project/Data/Vehicles/</c>, produced by
        /// <c>VehicleMeshAssetBaker</c>.
        /// </summary>
        public static readonly IReadOnlyList<string> Baked = new[] { "dually3500" };

        /// <summary>
        /// ⭐ <b>Registered refusals: a vehicle that is not baked, and why.</b> The coverage test reads
        /// this, so nothing can be quietly left out — a vehicle in neither <see cref="Baked"/> nor here
        /// fails.
        ///
        /// <para><b>Empty, and that is the good outcome.</b> It held one entry from #548 until
        /// 2026-08-17: the Dually, blocked on an architecture ruling rather than on any technical
        /// obstacle. The ruling was given (lead-architect, on #548) and ADR 0035 records it, so the
        /// entry went away rather than being reworded — which is what that entry said should happen
        /// to it.</para>
        ///
        /// <para>A new drop that is in neither list fails <c>VehicleRigFleetTests</c>. That is the
        /// whole point of the table: art arrives by PR, and this is the thing that stops one arriving
        /// unnoticed.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> NotBaked =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["otter8x8"] =
                    "INTAKE ONLY, and blocked on something no vehicle-side change can fix: HER " +
                    "PALETTE DOES NOT FIT THE FACET SHADER.\n\n" +
                    "Measured in the repo's own V8, 2026-08-17 (OtterIsoKitProbeTests pins it): she " +
                    "declares 22 materials and her faces reference SEVENTEEN of them. `_RampMeta` is " +
                    "a float4[16] — a real uniform array, guarded in VehicleMeshDef.IsUsable, " +
                    "HullMeshDef.IsUsable and IsoFacetHullRenderer.Configure — so a bake would " +
                    "produce a def that is 'not usable' and she would be refused at install.\n\n" +
                    "⚠️ THE TRICK THAT SAVED THE DUALLY AND THE ZODIAC DOES NOT SAVE HER. Both of " +
                    "those declared more materials than they used (17 declared / 16 used, and 18 / " +
                    "14), so filtering the table to the USED set brought them under the cap without " +
                    "changing a pixel — a ramp no face references cannot colour one. The Otter uses " +
                    "seventeen in EVERY build: wheeled, tracked and afloat all reference exactly 17, " +
                    "and the five she leaves out (trim, canvas, glass, glow, plus whichever of " +
                    "alloy/track the other build uses) are already excluded. She is one over, " +
                    "genuinely.\n\n" +
                    "So the fix is a DECISION, not a pass: either widen `_RampMeta` past 16 (it " +
                    "costs uniform space on every hull in the fleet, and the number is load-bearing " +
                    "in three guards), or ask the art director to merge two of her materials. That " +
                    "is an ADR-level call and an art-side conversation, and it is not this table's " +
                    "to make. Everything else about her bake is READY: the extraction shape is the " +
                    "Dually's exactly (a generator with a private build/makeMats), her azimuth is " +
                    "measured COUNTER-CLOCKWISE off her own front-axle anchors on exact 45° steps, " +
                    "and her articulation splits cleanly (rollL/rollR, 188 faces a side, perfectly " +
                    "disjoint by side).",
            };
    }
}
#endif
