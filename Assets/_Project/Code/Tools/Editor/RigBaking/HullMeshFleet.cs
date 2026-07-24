using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// One hull the mesh baker knows how to bake, and what to wire it to afterwards.
    ///
    /// <para>Deliberately thin, for the same reason <see cref="RigEntry"/> is: cell size, pivot,
    /// px/metre, elevation, the azimuth convention and the rock amplitudes are all read FROM THE RIG
    /// at bake time (ADR 0021 §4). Nothing here restates a fact the rig already owns — this table
    /// says only which rigs are hulls, where their output goes, and whether a baked sheet exists to
    /// keep alongside the mesh.</para>
    /// </summary>
    public readonly struct FleetHull
    {
        /// <summary>Stable catalog key, used by menu items, tests and log lines.</summary>
        public readonly string Key;

        /// <summary>Path relative to the repo root, e.g. "docs/art/rigs/doryIsoRig.js".</summary>
        public readonly string ScriptPath;

        /// <summary>The global the rig's IIFE installs, e.g. "DoryIso".</summary>
        public readonly string GlobalName;

        /// <summary>Where the <c>HullMeshDef</c> asset lives.</summary>
        public readonly string MeshAssetPath;

        /// <summary>The def's id — <c>hullmesh.snake_case</c>, append-only and stable (rule 2).</summary>
        public readonly string MeshId;

        /// <summary>
        /// The <c>BoatVisualDef</c> assets this rig's mesh belongs to. Usually one, but a rig can
        /// dress more than one boat: the punt hull serves both her basic and upgraded engine, and the
        /// sport skiff serves both the single and the twin outboard. One mesh, several visuals — the
        /// hull is the same hull.
        /// </summary>
        public readonly string[] VisualAssetPaths;

        /// <summary>
        /// The visual ids, positionally matched to <see cref="VisualAssetPaths"/>. Only consulted
        /// when a visual has to be CREATED (see <see cref="HasBakedSheet"/>); an existing visual
        /// keeps the id it already has, because ids are append-only and stable.
        /// </summary>
        public readonly string[] VisualIds;

        /// <summary>
        /// True when this hull also has baked 32-facing sprite art committed under
        /// <c>Assets/_Project/Art/Boats/</c>.
        ///
        /// <para><b>It decides which of two very different things the bake does.</b> With a sheet,
        /// the visual already exists and the bake only WIRES it — flipping the variant to Mesh while
        /// leaving the sprite compass populated, which is what makes the owner's V-key A/B possible
        /// at the helm. Without one, the mesh IS the whole picture, so the bake CREATES a mesh-only
        /// visual with an empty <c>Facings</c> array; that is what makes
        /// <c>BoatVisualDef.HasFullCompass</c> report false, so the V key says "this hull has only
        /// one look" instead of offering half an A/B that does not exist, and sprite-only overlays
        /// (oars, outboards) refuse to bind rather than draw wrongly.</para>
        /// </summary>
        public readonly bool HasBakedSheet;

        /// <summary>Human-readable, for log lines and the owner-facing report. Never parsed.</summary>
        public readonly string Label;

        public FleetHull(string key, string scriptPath, string globalName, string meshAssetPath,
                         string meshId, string[] visualAssetPaths, string[] visualIds,
                         bool hasBakedSheet, string label)
        {
            Key = key;
            ScriptPath = scriptPath;
            GlobalName = globalName;
            MeshAssetPath = meshAssetPath;
            MeshId = meshId;
            VisualAssetPaths = visualAssetPaths;
            VisualIds = visualIds;
            HasBakedSheet = hasBakedSheet;
            Label = label;
        }
    }

    /// <summary>
    /// <b>Every boat hull in the game, as mesh (ADR 0022 phase 6).</b>
    ///
    /// <para>Phases 4 and 5 each hand-wrote a menu item for one hull, which was right while the
    /// question was still "does this work at all". Phase 5 answered it — the side dragger needed
    /// <b>zero</b> changes to the baker, the shader or the seam — and the owner's ruling on the
    /// lobster A/B was "much better as a mesh, all boats will need to be a mesh". Eleven hand-written
    /// menu items is not a fleet; a table is. So the per-hull code became per-hull DATA, and the two
    /// existing menu items now look themselves up in here rather than restating their own paths.</para>
    ///
    /// <para><b>Two families, and the difference is the sheet, not the size.</b> Six hulls were
    /// already in the game drawn from baked 32-facing sprite sheets; for them the bake is a
    /// conversion and they keep their compass so the owner can toggle. Four have never been in Unity
    /// at all — the upper fleet the ADR was written for, where a sheet set was never an option (the
    /// tanker's would be measured in gigabytes) — and for them the mesh is the first and only
    /// picture.</para>
    ///
    /// <para>⚠️ <b>Importing art is not a licence to wire content</b> (rule 8, and the phrasing
    /// <see cref="RigCatalog"/> already uses). This table bakes ART. The upper fleet's gameplay
    /// numbers — mass, thrust, hold, seakeeping — live in hand-authored <c>BoatHullDef</c> assets
    /// exactly as the side dragger's do, so re-running a bake can never stomp a tuning pass, and
    /// nothing in here makes a boat purchasable.</para>
    /// </summary>
    public static class HullMeshFleet
    {
        const string Rigs = "docs/art/rigs";
        const string Meshes = "Assets/_Project/Data/Boats/HullMeshes";
        const string Visuals = "Assets/_Project/Data/Boats/Visuals";

        static FleetHull Sheeted(string key, string rig, string global, string name, string snake,
                                 string label, params string[] visualAssets) =>
            new FleetHull(key, $"{Rigs}/{rig}", global, $"{Meshes}/{name}HullMesh.asset",
                          $"hullmesh.{snake}", visualAssets.Select(v => $"{Visuals}/{v}.asset").ToArray(),
                          Array.Empty<string>(), hasBakedSheet: true, label);

        static FleetHull MeshOnly(string key, string rig, string global, string name, string snake,
                                  string label) =>
            new FleetHull(key, $"{Rigs}/{rig}", global, $"{Meshes}/{name}HullMesh.asset",
                          $"hullmesh.{snake}", new[] { $"{Visuals}/{name}.asset" },
                          new[] { $"visual.{snake}" }, hasBakedSheet: false, label);

        /// <summary>
        /// The fleet, in size order — which is also roughly the order the owner meets them, and the
        /// order the dev picker walks. Size order matters here for one practical reason: it is the
        /// order in which the mesh path gets HARDER (more faces, longer straight edges, larger flat
        /// panels), so a bake that starts failing tends to fail from the bottom of this list up.
        /// </summary>
        public static readonly IReadOnlyList<FleetHull> Hulls = new[]
        {
            // ---- the six with baked sheets: a CONVERSION, and they keep their compass -----------
            // Each of these already renders in game from a 32-facing sheet. Wiring the mesh does not
            // retire the sheet: the sprite half stays populated on purpose, because that is the
            // owner's A/B (V at the helm) and it is the only way a regression in the mesh path is
            // visible by eye rather than by test.

            Sheeted("dory", "doryIsoRig.js", "DoryIso", "DoryIso", "dory_iso",
                    "dory (T0, ~4.3 m — the boat he starts in)", "DoryIso"),

            // ONE hull, TWO visuals: basic and upgraded differ by engine, not by planking.
            Sheeted("punt", "puntIsoRig.js", "PuntIso", "PuntIso", "punt_iso",
                    "punt (T1, ~5.2 m — the golden master, and a real purchasable boat)",
                    "PuntIsoBasic", "PuntIsoUpgraded"),

            Sheeted("consoleSkiff", "consoleIsoRig.js", "ConsoleIso", "ConsoleIso", "console_iso",
                    "console skiff (~7.0 m, aluminium)", "ConsoleSkiff"),

            // Likewise one hull, two visuals: the twin differs by a second outboard.
            Sheeted("sportSkiff", "sportSkiffIsoRig.js", "SportSkiffIso", "SportSkiffIso",
                    "sport_skiff_iso", "sport skiff (~7.0 m, glass — single and twin)",
                    "SportSkiffSingle", "SportSkiffTwin"),

            Sheeted("capeIslander", "capeIslanderIsoRig.js", "CapeIslanderIso", "CapeIslanderIso",
                    "cape_islander_iso", "Cape Islander (T2, ~12.8 m — the hub workboat)",
                    "CapeIslanderIso"),

            // Phase 4's hull: the first mesh end-to-end, and the one the owner A/B'd.
            Sheeted("lobsterBoat", "lobsterBoatIsoRig.js", "LobsterBoatIso", "LobsterBoatIso",
                    "lobster_boat_iso", "lobster boat (T3, ~12.0 m — the first mesh hull)",
                    "LobsterBoatIso"),

            // ---- mesh-only: no sheet, and none was ever possible ---------------------------------
            // These are the hulls ADR 0022 was written for. Sheet-equivalent sizes are reported by
            // the bake itself rather than asserted here — the ADR's own numbers were measured, and
            // repeating them in a comment is how a comment goes stale.

            // Phase 5's hull, and the one that motivated the ADR.
            MeshOnly("sideDragger", "sideDraggerIsoRig.js", "SideDraggerIso", "SideDraggerIso",
                     "side_dragger_iso", "side dragger (T4, 25 m — the first offshore hull)"),

            MeshOnly("sternTrawler", "sternTrawlerIsoRig.js", "SternTrawlerIso", "SternTrawlerIso",
                     "stern_trawler_iso", "stern trawler (T5, ~38 m — stern ramp, gantry, net drum)"),

            // A genuinely separate rig, not a variant flag on the first: the art director shipped two
            // files. Baked as two hulls because that is what they are on disk.
            MeshOnly("sternTrawlerMk2", "sternTrawlerMk2IsoRig.js", "SternTrawlerMk2Iso",
                     "SternTrawlerMk2Iso", "stern_trawler_mk2_iso",
                     "stern trawler Mk2 (T5, ~38 m)"),

            MeshOnly("coastalPacket", "coastalPacketIsoRig.js", "CoastalPacketIso", "CoastalPacketIso",
                     "coastal_packet_iso", "coastal packet (T6, ~60 m — the first merchant hull)"),

            // ⚠️ SHE IS THE ODD ONE, AND DELIBERATELY SO: 16 px = 1 m, half the fleet standard,
            // because at 32 she would be ~3,500 px long and no sheet could hold her. The rig exposes
            // PX and the bake reads it into HullMeshDef.PxPerMetre like every other hull — the scale
            // is DATA, not a constant, which is exactly why this hull is the ADR's best argument.
            // If anything downstream assumed 32, she is the hull that finds it.
            MeshOnly("tanker", "tankerIsoRig.js", "TankerIso", "TankerIso", "tanker_iso",
                     "tanker (T7, ~110 m — the final hull, 16 px/m)"),
        };

        /// <summary>
        /// Boat-shaped rigs under <c>docs/art/rigs/</c> that this catalog deliberately does NOT bake,
        /// each with the reason. The coverage test reads this: a new rig file that is neither baked
        /// nor listed here fails, so the next art drop cannot be silently missed — which is the
        /// failure this whole table exists to prevent.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> NotHulls =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["skiffMotorRig.js"] =
                    "An OUTBOARD, not a hull: it mounts on the skiffs and rides their rock. It has no " +
                    "ROCK block of its own and is drawn as a sprite overlay bound to the visual def's " +
                    "MotorLower/MotorUpper columns, so it has no place in a hull-mesh table.",
            };

        public static FleetHull Get(string key)
        {
            foreach (var h in Hulls) if (h.Key == key) return h;
            throw new ArgumentException(
                $"No hull '{key}' in the fleet catalog. Known: {string.Join(", ", Hulls.Select(h => h.Key))}.");
        }

        /// <summary>The rig file names this catalog bakes, for the coverage test.</summary>
        public static IEnumerable<string> BakedRigFileNames =>
            Hulls.Select(h => Path.GetFileName(h.ScriptPath));
    }
}
