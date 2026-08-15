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

        /// <summary>
        /// Non-null when this hull's mesh is baked and wired but the visual must STAY on the sprite
        /// variant, with the reason. Null means the bake flips her.
        ///
        /// <para><b>Why a hull can be mesh-ready and not mesh.</b> Some hulls wear sprite overlays —
        /// the dory's oars, the outboards on the punt and the two skiffs — and those are baked PER
        /// FACING CELL. A mesh hull rotates continuously, so there is no cell to look up and
        /// <c>BoatHullSkinner.ApplyMesh</c> drops them (deliberately, with a warning). Flipping these
        /// hulls would therefore mean a dory that cannot row, which is a visible regression and not a
        /// trade worth making for a smoother hull.</para>
        ///
        /// <para>The mesh is still baked and still wired into <c>HullMesh</c>, because the wiring is
        /// inert while the variant is Sprite (<c>ShouldPresentMesh</c> gates on the variant alone) and
        /// it makes the eventual flip a one-field change once the overlays have meshes of their own.
        /// PROVEN, not assumed: <c>PilotableFleetPlayTests</c> caught this the moment the flip went in
        /// — four failures, "the dory has her oars: expected not null, but was null".</para>
        /// </summary>
        public readonly string OverlayBlockedReason;

        /// <summary>
        /// Non-null when this hull is ONE OF SEVERAL out of a generator rig, naming which one. Null
        /// — the case for every hull baked before 2026-08-13 — means the rig's static <c>F</c> array,
        /// and the extractor takes the code path it always has.
        ///
        /// <para>See <see cref="RigHullExtraction"/> for why this is a separate type from the
        /// fitting's <see cref="RigPropExtraction"/> rather than a reuse of it.</para>
        /// </summary>
        public readonly RigHullExtraction Extraction;

        public bool FlipsToMesh => OverlayBlockedReason == null;

        /// <summary>True when this entry names one variant of a rig that generates several — so
        /// several entries in this table legitimately share one <see cref="ScriptPath"/>.</summary>
        public bool IsVariant => Extraction != null && Extraction.IsVariant;

        public FleetHull(string key, string scriptPath, string globalName, string meshAssetPath,
                         string meshId, string[] visualAssetPaths, string[] visualIds,
                         bool hasBakedSheet, string label, string overlayBlockedReason = null,
                         RigHullExtraction extraction = null)
        {
            Extraction = extraction;
            Key = key;
            ScriptPath = scriptPath;
            GlobalName = globalName;
            MeshAssetPath = meshAssetPath;
            MeshId = meshId;
            VisualAssetPaths = visualAssetPaths;
            VisualIds = visualIds;
            HasBakedSheet = hasBakedSheet;
            Label = label;
            OverlayBlockedReason = overlayBlockedReason;
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
    /// <para><b>THREE families now, and each is a different kind of entry.</b> Six hulls were already
    /// in the game drawn from baked 32-facing sprite sheets; for them the bake is a conversion and
    /// they keep their compass so the owner can toggle. Five had never been in Unity at all — the
    /// upper fleet the ADR was written for, where a sheet set was never an option (the tanker's would
    /// be measured in gigabytes) — and for them the mesh is the first and only picture. The
    /// eighteenth-of-a-file family arrived with ADR 0022 phase 8: <c>lobsterBoatVariantsIsoRig.js</c>
    /// builds EIGHTEEN boats from one source, so those rows carry a
    /// <see cref="FleetHull.Extraction"/> naming which one they mean. See
    /// <see cref="LobsterVariantFleet"/>, which owns their list and their names.</para>
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

        /// <summary>
        /// ⚠️ <b>NOTHING IS BLOCKED ANY MORE (ADR 0022 phase 7 complete).</b> This constant is gone,
        /// and the field it fed (<see cref="FleetHull.OverlayBlockedReason"/>) is now null on every
        /// hull in the table.
        ///
        /// <para>The block existed because a hull's sprite overlays are baked one cell per facing and
        /// a mesh hull turns continuously, so <c>BoatHullSkinner.ApplyMesh</c> dropped them — flipping
        /// those hulls would have shipped a dory that cannot row and four skiffs with no engines.
        /// Phase 7 removed the cause rather than the symptom: the dory's oars became
        /// <c>HullPropMeshDef</c>s (#285), and this change did the same for both outboard rigs
        /// (<c>puntIsoRig</c>'s own tiller engine, <c>skiffMotorRig</c>'s remote-steer four-stroke,
        /// two paint builds each). Every one is posed at runtime through <c>IHullPropRenderer</c> from
        /// the SAME state machine its sprite twin used, and adjudicated in pixels against the rig's
        /// own renderer.</para>
        ///
        /// <para>The field stays on <see cref="FleetHull"/> deliberately: it is how the next hull that
        /// arrives wearing something unbaked declares itself, instead of being flipped and losing it
        /// silently. The coverage test still enforces the pairing in both directions.</para>
        /// </summary>
        static FleetHull Sheeted(string key, string rig, string global, string name, string snake,
                                 string label, string overlayBlocked, params string[] visualAssets) =>
            new FleetHull(key, $"{Rigs}/{rig}", global, $"{Meshes}/{name}HullMesh.asset",
                          $"hullmesh.{snake}", visualAssets.Select(v => $"{Visuals}/{v}.asset").ToArray(),
                          Array.Empty<string>(), hasBakedSheet: true, label, overlayBlocked);

        static FleetHull MeshOnly(string key, string rig, string global, string name, string snake,
                                  string label) =>
            new FleetHull(key, $"{Rigs}/{rig}", global, $"{Meshes}/{name}HullMesh.asset",
                          $"hullmesh.{snake}", new[] { $"{Visuals}/{name}.asset" },
                          new[] { $"visual.{snake}" }, hasBakedSheet: false, label);

        /// <summary>
        /// One cell of a GENERATOR rig — mesh-only like the upper fleet, plus the one thing that
        /// makes it a different kind of entry: a <see cref="RigHullExtraction"/> naming WHICH hull
        /// this row means out of the several the file builds.
        ///
        /// <para>Every name comes off <see cref="LobsterVariant"/> rather than being spelled here,
        /// because eighteen rows are exactly the number where one hand-typed mismatch between a mesh
        /// path and a visual id survives review.</para>
        /// </summary>
        static FleetHull Variant(LobsterVariant v) =>
            new FleetHull(v.Key, LobsterVariantFleet.ScriptPath, LobsterVariantFleet.GlobalName,
                          $"{Meshes}/{v.AssetName}HullMesh.asset", v.MeshId,
                          new[] { $"{Visuals}/{v.AssetName}.asset" }, new[] { v.VisualId },
                          hasBakedSheet: false, v.Label, overlayBlockedReason: null,
                          extraction: v.Extraction);

        /// <summary>
        /// <b>The hulls whose rig file makes exactly one boat</b> — in size order, which is also
        /// roughly the order the owner meets them and the order the dev picker walks. Size order
        /// matters here for one practical reason: it is the order in which the mesh path gets HARDER
        /// (more faces, longer straight edges, larger flat panels), so a bake that starts failing
        /// tends to fail from the bottom of this list up.
        ///
        /// <para>Kept as a list of its own because it is the CONTROL SET: these eleven were baked
        /// before generators existed, and <c>RigMeshVariantExtractionTests</c> proves the variant
        /// work left their path bit-for-bit alone by sweeping exactly this list.</para>
        ///
        /// <para>⚠️ Declared BEFORE <see cref="Hulls"/> on purpose — static field initialisers run in
        /// textual order, and <see cref="Hulls"/> reads this one.</para>
        /// </summary>
        public static readonly IReadOnlyList<FleetHull> OneHullPerRig = new[]
        {
            // ---- the six with baked sheets: a CONVERSION, and they keep their compass -----------
            // Each of these already renders in game from a 32-facing sheet. Wiring the mesh does not
            // retire the sheet: the sprite half stays populated on purpose, because that is the
            // owner's A/B (V at the helm) and it is the only way a regression in the mesh path is
            // visible by eye rather than by test.

            // UNBLOCKED by phase 7: her oars became meshes, so she is the first hull whose sprite
            // overlay crossed over rather than being dropped. The boat he starts in rows continuously.
            Sheeted("dory", "doryIsoRig.js", "DoryIso", "DoryIso", "dory_iso",
                    "dory (T0, ~4.3 m — the boat he starts in)", null, "DoryIso"),

            // ONE hull, TWO visuals: basic and upgraded differ by engine, not by planking — and the
            // engine is now a fitting per build (hullprop.punt_motor_basic / _upgraded), which is
            // what UNBLOCKED her. Her tiller outboard is her own rig's, not the skiffs' at another
            // size: own cell, own ±32°.
            Sheeted("punt", "puntIsoRig.js", "PuntIso", "PuntIso", "punt_iso",
                    "punt (T1, ~5.2 m — the golden master, and a real purchasable boat)",
                    null, "PuntIsoBasic", "PuntIsoUpgraded"),

            Sheeted("consoleSkiff", "consoleIsoRig.js", "ConsoleIso", "ConsoleIso", "console_iso",
                    "console skiff (~7.0 m, aluminium)", null, "ConsoleSkiff"),

            // Likewise one hull, two visuals: the twin differs by a second outboard — the SAME
            // fitting instantiated at ±0.34 m, so the twin needed no art and no bake of its own.
            Sheeted("sportSkiff", "sportSkiffIsoRig.js", "SportSkiffIso", "SportSkiffIso",
                    "sport_skiff_iso", "sport skiff (~7.0 m, glass — single and twin)",
                    null, "SportSkiffSingle", "SportSkiffTwin"),

            // The biggest hull that wears NO sprite overlay, so she is the one sheeted boat phase 6
            // can actually flip — and therefore the owner's second A/B, after the lobster.
            Sheeted("capeIslander", "capeIslanderIsoRig.js", "CapeIslanderIso", "CapeIslanderIso",
                    "cape_islander_iso", "Cape Islander (T2, ~12.8 m — the hub workboat)",
                    null, "CapeIslanderIso"),

            // Phase 4's hull: the first mesh end-to-end, and the one the owner A/B'd.
            Sheeted("lobsterBoat", "lobsterBoatIsoRig.js", "LobsterBoatIso", "LobsterBoatIso",
                    "lobster_boat_iso", "lobster boat (T3, ~12.0 m — the first mesh hull)",
                    null, "LobsterBoatIso"),

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
        /// <b>Every hull the baker knows how to bake</b> — the eleven above, then the lobster
        /// generator's eighteen.
        ///
        /// <para>The eighteen are appended as a BLOCK rather than interleaved into the size order,
        /// and the reason is practical: they span 8.6–14.6 m and would scatter through the middle of
        /// the list, hiding the fact that one rig file makes all of them. Within the block the rig's
        /// own axis order is kept, so this list, <c>FleetFlotationTableTests.Authored</c> and
        /// <c>FleetDeckOccupancyTableTests.Authored</c> all read in the same order and a diff of one
        /// lines up against a diff of the others.</para>
        /// </summary>
        public static readonly IReadOnlyList<FleetHull> Hulls = BuildFleet();

        static IReadOnlyList<FleetHull> BuildFleet()
        {
            var fleet = new List<FleetHull>(OneHullPerRig);
            foreach (LobsterVariant v in LobsterVariantFleet.All) fleet.Add(Variant(v));
            return fleet;
        }

        /// <summary>The hulls that are one cell of a generator rig — today, the eighteen lobster
        /// variants. Several fixtures need them separately from the eleven, because the questions
        /// that can be asked of them differ (a variant's extraction is non-null; her sidecar is
        /// named for the hull rather than for the rig).</summary>
        public static IEnumerable<FleetHull> VariantHulls
        {
            get { foreach (FleetHull h in Hulls) if (h.IsVariant) yield return h; }
        }

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
                    "An OUTBOARD, not a hull: it mounts on both 7 m skiffs and rides their rock. It " +
                    "has no ROCK block of its own and no hull to present, so it has no place in a " +
                    "hull-mesh table — it is baked as an articulated FITTING instead (ADR 0022 phase " +
                    "7, HullPropFleet: hullprop.skiff_motor_work / _sport).",

                // ⚠️ lobsterBoatVariantsIsoRig.js WAS HERE, and its entry said "delete this when the
                // 18 HullMeshDefs and deck defs land". They landed; it is deleted. Her eighteen are
                // in Hulls above, via LobsterVariantFleet.
                //
                // Two things that entry recorded are worth keeping, because they are still the
                // reasons the bake is shaped the way it is: her faces come from a private
                // facesFor(V) driven by resolve(opts) rather than from a static `F` (hence
                // RigHullExtraction + the `variantFaces` reconstruction, ADR 0022 phase 8), and her
                // 12 paints move no vertex, so ONE mesh per cell serves every scheme. Both are now
                // asserted rather than commented — RigMeshVariantExtractionTests and
                // LobsterVariantRigTests respectively.

                // ---- the 2026-08-12 fleet pack's remaining three rigs, imported 2026-08-14 -------
                // All three are IMPORTED but NOT YET BAKED. They are here rather than in Hulls
                // because a rig lands in one PR and her HullMeshDef, deck Def and visual land in the
                // next — the chain LobsterVariantRigTests describes (a committed sidecar needs a
                // deck Def, which needs a visual that wears it). Importing the rig first is what
                // makes the sidecars' `derivedFromRigSha256` resolvable at all: each of these three
                // files' LF bytes hash to exactly the SHA its sidecar pins, verified on import.

                ["zodiacIsoRig.js"] =
                    "Coast-guard RHIB, TWO builds off one rig (hurricane 7.28 m / frc 6.66 m over " +
                    "the tubes). Imported 2026-08-14 so her sidecar's derivedFromRigSha256 " +
                    "(66e5a977…) resolves against a committed source; her flotation is authored and " +
                    "no longer provisional (FleetFlotationTableTests, docs/design/fleet-flotation.md " +
                    "§4). What is outstanding is the bake itself — 2 HullMeshDefs and their deck " +
                    "defs. Delete this entry when they land.",

                ["sportSkiffMk2IsoRig.js"] =
                    "The RESHAPED 7.0 m sport skiff — a SECOND hull under a new id " +
                    "(hullmesh.sport_skiff_mk2_iso), not a replacement for hullmesh.sport_skiff_iso. " +
                    "⚠️ She ships from art as `sportSkiffIsoRig.js` and installs the SAME global " +
                    "(`SportSkiffIso`) as the committed 366-line rig, so she is filed here under a " +
                    "Mk2 name (the sternTrawlerMk2 precedent) rather than overwriting a shipped " +
                    "hull's source. docs/art/rigs/** is read-only to us, so the global collision is " +
                    "flagged upstream, not patched: a bake loads ONE rig into a fresh host, so it is " +
                    "latent — but anything that ever loads two rigs into one host would get the " +
                    "wrong boat silently. Outstanding: her bake. Delete this entry when it lands.",

                ["sportFisherIsoRig2.js"] =
                    "TWO battlewagons off one rig (53' convertible 16.2 m / 90' skybridge 27.4 m) — " +
                    "a genuinely different model from the sport skiff, NOT her v2, despite the two " +
                    "arriving in one drop. She was outside the 21-hull flotation table entirely; the " +
                    "owner ruled her IN on 2026-08-14 and her two rows were derived by the doc's own " +
                    "method (docs/design/fleet-flotation.md §6). Outstanding: her bake. Delete this " +
                    "entry when it lands.",
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
