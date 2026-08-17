#if UNITY_EDITOR
using System;
using System.Collections.Generic;

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

        /// <summary>The top-level <c>kind</c> that marks a sidecar as this table's business.</summary>
        public const string RoadVehicleKind = "road_vehicle";

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

            public Vehicle(string key, string scriptPath, string sidecarPath, string globalName)
            {
                Key = key; ScriptPath = scriptPath; SidecarPath = sidecarPath; GlobalName = globalName;
            }
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
                "VehicleIso"),
        };

        /// <summary>
        /// Vehicles that currently have a baked mesh. <b>Empty, on purpose</b> — see
        /// <see cref="NotBaked"/> for the reason, which is an architecture decision and not a
        /// technical obstacle.
        /// </summary>
        public static readonly IReadOnlyList<string> Baked = Array.Empty<string>();

        /// <summary>
        /// ⭐ <b>Registered refusals: a vehicle that is not baked, and why.</b> The coverage test reads
        /// this, so nothing can be quietly left out — a vehicle in neither <see cref="Baked"/> nor here
        /// fails.
        ///
        /// <para><b>The Dually's bake is BLOCKED ON A DECISION, NOT ON A DIFFICULTY.</b> That distinction
        /// is the whole content of this entry, and it was measured rather than assumed —
        /// <c>DuallyIsoKitProbeTests</c> runs the rig in the repo's own V8 and proves the mesh path
        /// reaches it.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> NotBaked =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dually3500"] =
                    "NOT a technical obstacle — the mesh path demonstrably reaches her. " +
                    "DuallyIsoKitProbeTests measures, in the repo's own V8, that she is a GENERATOR of " +
                    "exactly the shape RigHullExtraction already serves (the lobster pack and the " +
                    "zodiac): no static F, faces from a private build(resolve({})) that returns 200+ " +
                    "faces in the {v, mat, b, db} shape the packer reads, and a MATS whose first key " +
                    "is 'paint', which is the default-ramp-first ordering the face packer requires. " +
                    "The one real difference from every hull is that her MATS is an OBJECT KEYED BY " +
                    "NAME rather than an index-ordered array, so the fleet's 'MATS order IS the baked " +
                    "material index' law does not transfer and a Reconstructions entry supplies it.\n" +
                    "\n" +
                    "What is missing is an ARCHITECTURE RULING the art side cannot make: a truck is " +
                    "not a hull, so she cannot take HullMeshDef (Core/Boats) without making 'boat' " +
                    "mean 'anything with a mesh'. She needs a vehicle def and a home module — proposed " +
                    "HiddenHarbours.Vehicles, Core-mediated per rule 4, with a NEW 'vehicle.*' id " +
                    "family that is append-only once shipped. That is an ADR and lead-architect " +
                    "sign-off (CLAUDE.md §3 rule 4, §6), and importing source is not a licence to wire " +
                    "content (rule 8). So the drop lands here, verified and guarded, and the bake " +
                    "follows the ruling.\n" +
                    "\n" +
                    "⚠ One art limit the owner should rule on before the controller is built: the rig " +
                    "models NO STEERING. The front wheels roll but never yaw, so a turning truck is a " +
                    "yaw on the whole sprite.",
            };
    }
}
#endif
