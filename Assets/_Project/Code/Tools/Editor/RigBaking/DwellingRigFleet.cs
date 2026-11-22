#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>THE PARKED DWELLINGS — what the repo has, and what it does with each of them.</b>
    ///
    /// <para>The third coverage table, after <see cref="HullMeshFleet"/> (boats) and
    /// <see cref="VehicleRigFleet"/> (road vehicles), and it exists for the same reason the second one
    /// did: <b>neither of the other two can see a camper.</b></para>
    ///
    /// <list type="bullet">
    ///   <item><b>The hull law cannot.</b> Its coverage test scans <c>docs/art/rigs/</c> for rigs
    ///   containing <c>rollA</c> — a hull's sea-rock amplitude. <c>camperIsoRig.js</c> contains it zero
    ///   times, correctly: a parked trailer does not rock on a swell. So a camper drop is INVISIBLE to
    ///   it and would go silently unbaked.</item>
    ///   <item><b>The vehicle law cannot either.</b> Its population is defined by the sidecar's own
    ///   top-level <c>"kind": "road_vehicle"</c>, and the camper's sidecar declares <b>no top-level
    ///   <c>kind</c> at all</b> — measured in the repo's own V8 against the rig's
    ///   <c>gameplayGeometry()</c> generator, not just read off the shipped file. A camper is also not
    ///   a road vehicle in the sense that table means: it has no cab, no engine and nothing that
    ///   drives. Its own <c>_excluded</c> block calls it a <i>"Parked dwelling"</i>.</item>
    /// </list>
    ///
    /// <para><b>⚠ THE SIGNAL HERE IS THE FOLDER, AND THAT IS A COMPROMISE — say so rather than dress it
    /// up.</b> <see cref="VehicleRigFleet"/> keys on art's own word for what a thing is, which is the
    /// better rule. This table cannot: the drop publishes no such word. So membership is this explicit
    /// list plus the sidecar folder, and <b>the upstream ask is for the camper sidecars to carry a
    /// top-level <c>kind</c></b> (<c>"dwelling"</c> would do) so this table can key on the same kind of
    /// signal its sibling does instead of on where a file was filed.</para>
    ///
    /// <para><b>Every dwelling is either BAKED or carries a REASON.</b> A new drop that is neither
    /// fails <c>DwellingRigFleetTests</c> — art arrives by PR and this is the thing that stops one
    /// arriving unnoticed. That is the whole job of the table.</para>
    /// </summary>
    public static class DwellingRigFleet
    {
        /// <summary>
        /// Where a parked dwelling's gameplay sidecar lives — a sibling of the vehicles' subfolder,
        /// repo-relative.
        ///
        /// <para><b>⚠ A SUBFOLDER, AND IT IS LOAD-BEARING — the Dually learned this the hard way.</b>
        /// Putting these sidecars straight into <c>docs/art/rigs/gameplay/</c> beside the boats' would
        /// red <c>DeckSidecarImportParityTests</c>, which enumerates <b>every</b> <c>*.gameplay.json</c>
        /// in that folder and requires each to parse as a boat deck with a committed
        /// <c>BoatDeckDef</c> behind it. That law is right and the folder simply means "boat deck
        /// sidecars".</para>
        ///
        /// <para>A camper sidecar is a different document — <c>SOLE</c>, <c>THRESHOLD</c>,
        /// <c>STEP</c>, <c>HOOKUPS</c> — and its own <c>_excluded</c> says <i>"WASHBOARD: Not a hull.
        /// No side decks."</i> <c>Directory.GetFiles</c> is not recursive, so the three populations
        /// stay separate by construction and no fixture needs to know about the others.</para>
        /// </summary>
        public const string SidecarFolder = "docs/art/rigs/gameplay/dwellings";

        /// <summary>One parked dwelling: where its rig and sidecar are, and what installs it.</summary>
        public readonly struct Dwelling
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

            public Dwelling(string key, string scriptPath, string sidecarPath, string globalName)
            {
                Key = key; ScriptPath = scriptPath; SidecarPath = sidecarPath; GlobalName = globalName;
            }
        }

        /// <summary>Repo-relative path to the camper rig — ONE rig for both lengths, which is why the
        /// two entries below share it. The README is explicit that the variants are "the same loft
        /// re-run, not two models".</summary>
        public const string CamperRigPath = "docs/art/rigs/camper-iso-kit/camperIsoRig.js";

        /// <summary>
        /// Every parked dwelling whose rig and sidecar are committed. Being here means the drop has
        /// LANDED and is hash-verified — it does <b>not</b> mean it is baked to sheets. What is baked
        /// is <see cref="Baked"/>; why anything is not is <see cref="NotBaked"/>.
        /// </summary>
        public static readonly IReadOnlyList<Dwelling> Dwellings = new[]
        {
            new Dwelling(
                "bantam",
                CamperRigPath,
                SidecarFolder + "/camperIsoRig.bantam.gameplay.json",
                "CamperIso"),
            new Dwelling(
                "clipper",
                CamperRigPath,
                SidecarFolder + "/camperIsoRig.clipper.gameplay.json",
                "CamperIso"),
        };

        /// <summary>
        /// Dwellings that currently have baked sheets.
        ///
        /// <para>Both lengths, as of the camper bake. Each gets TWO sheets — a <c>rest</c> at the
        /// variant's own default fit-out and an <c>enter</c> carrying the door cue — laid out columns
        /// = swing frames, rows = facings. <see cref="HiddenHarbours.Art.Editor.CamperKit"/> owns the
        /// build table, <c>CamperSheetBaker</c> writes them and <c>CamperBakeTests</c> checks that this
        /// list is a claim with files behind it.</para>
        ///
        /// <para><b>Both of the traps the earlier refusal recorded are now guards in the baker rather
        /// than warnings in a comment</b>, each with a sabotage test that fires it: a compass string
        /// for <c>dir</c> trips an opaque-pixel floor (<c>MinOpaquePixelsPerCell</c>), and an awninged
        /// enter build trips a cue-motion floor (<c>MinCueDeltaPx</c>).</para>
        ///
        /// <para>⚠ <b>What the bake did NOT solve, because it is upstream art:</b> the kit still has
        /// no interior — <i>"No interior. This is the shell."</i> The room belongs to
        /// <c>camperInteriorRig.js</c> (Clipper only, catalog key <c>camperInterior</c>), which is in
        /// the repo and registered but not baked: a room in a parked dwelling has no mesh path yet
        /// (see the kit README). This sheet bake does not draw it.</para>
        /// </summary>
        public static readonly IReadOnlyList<string> Baked = new[] { "bantam", "clipper" };

        /// <summary>
        /// ⭐ <b>Registered refusals: a dwelling that is not baked, and why.</b> The coverage test reads
        /// this, so nothing can be quietly left out — a dwelling in neither <see cref="Baked"/> nor here
        /// fails.
        ///
        /// <para><b>Empty, and an entry is DELETED rather than reworded when its dwelling is baked</b> —
        /// a refusal that outlives its cause goes on explaining a blocker somebody already cleared.
        /// The camper's two entries lived here between its import and its bake; what they recorded that
        /// still matters has moved into <see cref="Baked"/> and into the baker's own guards.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> NotBaked =
            new Dictionary<string, string>(StringComparer.Ordinal);

        // =============================================================================================
        //  MESH-BAKED DWELLINGS (ADR 0060)
        // =============================================================================================

        /// <summary>Repo-relative path to the camper's room rig (Clipper only).</summary>
        public const string CamperRoomRigPath = "docs/art/rigs/camper-iso-kit/camperInteriorRig.js";

        /// <summary>Where committed <see cref="HiddenHarbours.Core.DwellingMeshDef"/> assets live.</summary>
        public const string MeshFolder = "Assets/_Project/Data/Dwellings/Meshes";

        /// <summary>
        /// One dwelling the mesh bake writes: which shell and room rigs (by <see cref="RigCatalog"/>
        /// key), which sidecars it reads, and where its <see cref="HiddenHarbours.Core.DwellingMeshDef"/>
        /// goes. Keyed by the same <see cref="Dwelling.Key"/> as the sheet table, so one dwelling has one
        /// name in both.
        /// </summary>
        public readonly struct MeshDwelling
        {
            /// <summary>The <see cref="Dwelling.Key"/> this bakes ('clipper').</summary>
            public readonly string Key;
            /// <summary>The shell's <see cref="RigCatalog"/> key ('camper').</summary>
            public readonly string ShellRigKey;
            /// <summary>The room's <see cref="RigCatalog"/> key ('camperInterior'), or empty when one rig
            /// draws shell and room (the shed).</summary>
            public readonly string RoomRigKey;
            /// <summary>Repo-relative path to the room rig.</summary>
            public readonly string RoomScriptPath;
            /// <summary>The global the room rig installs.</summary>
            public readonly string RoomGlobalName;
            /// <summary>Repo-relative path to the room sidecar: walkable levels, threshold, fixtures and
            /// the shell stamp.</summary>
            public readonly string RoomSidecarPath;
            /// <summary>The variant both rigs are asked for, as their own JS options literal.</summary>
            public readonly string ViewOptions;
            /// <summary>The def's stable id, <c>dwelling.&lt;key&gt;</c>. Append-only.</summary>
            public readonly string MeshId;
            /// <summary>Where the def is written.</summary>
            public readonly string MeshAssetPath;
            /// <summary>
            /// Why this dwelling's procedural surface detail is not carried, or empty when every textured
            /// face uses a generator the facet shader already has. A textured face the shader cannot
            /// carry is a REFUSAL unless this says why, so detail is never dropped in silence.
            /// </summary>
            public readonly string DeferredSurfaceDetail;

            public MeshDwelling(string key, string shellRigKey, string roomRigKey, string roomScriptPath,
                                string roomGlobalName, string roomSidecarPath, string viewOptions,
                                string meshId, string meshAssetPath, string deferredSurfaceDetail)
            {
                Key = key; ShellRigKey = shellRigKey; RoomRigKey = roomRigKey;
                RoomScriptPath = roomScriptPath; RoomGlobalName = roomGlobalName;
                RoomSidecarPath = roomSidecarPath; ViewOptions = viewOptions;
                MeshId = meshId; MeshAssetPath = meshAssetPath;
                DeferredSurfaceDetail = deferredSurfaceDetail;
            }
        }

        /// <summary>
        /// ⭐ <b>Dwellings with a committed mesh bake (ADR 0060 option B).</b> A dwelling may be both
        /// sheet-baked and mesh-baked while it migrates: what the world DRAWS is a separate switch
        /// (ADR 0060 section 8), not this table. Being here means the asset exists and a fresh bake of the
        /// rigs reproduces it.
        ///
        /// <para><b>The Clipper.</b> Shell <c>camper</c> plus room <c>camperInterior</c>. The Bantam has no
        /// room rig, so it stays sheet-only.</para>
        /// </summary>
        public static readonly IReadOnlyList<MeshDwelling> MeshBaked = new[]
        {
            new MeshDwelling(
                "clipper",
                shellRigKey: "camper",
                roomRigKey: "camperInterior",
                roomScriptPath: CamperRoomRigPath,
                roomGlobalName: "CamperInterior",
                roomSidecarPath: "docs/art/rigs/camper-iso-kit/camperInteriorRig.clipper.interior.json",
                viewOptions: "{variant:'clipper'}",
                meshId: "dwelling.camper_clipper",
                meshAssetPath: MeshFolder + "/CamperClipperDwellingMesh.asset",
                deferredSurfaceDetail:
                    "The camper's generators are not the facet shader's three. The shell's skin panels, " +
                    "grain, stripes and tyre tread and the room floor's 0.18 m planks are pure functions " +
                    "of (u, v) like the boat generators, but none of them matches plank, board or quilt, " +
                    "and ADR 0060 allows no shader change. The faces bake flat-shaded and the bake log " +
                    "counts them; carrying them is an ADR question, not a bake fix."),
        };

        /// <summary>The mesh entry for <paramref name="key"/>, or false.</summary>
        public static bool TryGetMeshBaked(string key, out MeshDwelling dwelling)
        {
            foreach (MeshDwelling d in MeshBaked)
                if (string.Equals(d.Key, key, StringComparison.Ordinal)) { dwelling = d; return true; }
            dwelling = default;
            return false;
        }

        /// <summary>The sheet-table entry for <paramref name="key"/>, or false.</summary>
        public static bool TryGetDwelling(string key, out Dwelling dwelling)
        {
            foreach (Dwelling d in Dwellings)
                if (string.Equals(d.Key, key, StringComparison.Ordinal)) { dwelling = d; return true; }
            dwelling = default;
            return false;
        }
    }
}
#endif
