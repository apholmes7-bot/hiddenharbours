#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The VILLAGE BUILDING KIT — what M1 needs built out of the two parametric building rigs, and the
    /// schema of the contract the bake writes beside the sheets.
    ///
    /// <para>The building rigs and <c>BuildingRigBaker</c> have existed since 2026-07-23 and bake
    /// correctly, but <b>nothing consumed their output</b>: no contract, no slicer, no placement path.
    /// This file is the first half of that consumer chain — the kit's identity — and it is the sibling
    /// of <see cref="TreeKitCatalog"/> in every structural respect. <see cref="VillageBuildingCatalog"/>
    /// is the placement half.</para>
    ///
    /// <para><b>Serializer == parser.</b> <c>VillageBuildingBakeMenu</c> writes <see cref="Contract"/>
    /// with <see cref="JsonUtility.ToJson(object,bool)"/> and <see cref="Load"/> reads it back with
    /// <see cref="JsonUtility.FromJson{T}"/>, so a bake and its consumers cannot drift apart. Every
    /// number in it comes from <c>BuildingBakeResult</c>, which read it from the rig — cell, crop,
    /// pivot, footprint metres and the measured facing convention are never restated here.</para>
    ///
    /// <para><b>⚠️ THE PIVOT IS DATA, NOT A CONSTANT — uniquely among this repo's kits.</b> Every other
    /// sheet pins its pivot with a named const (<c>DoryWaterline</c>, <c>PuntOrigin</c>) because the kit
    /// fixes the cell. Here <c>BuildingRigBaker</c> tight-crops to the union of the eight silhouettes,
    /// so the cell — and with it the pivot — depends on the build: a cannery crops differently from a
    /// shack. Baking a build and hard-coding its pivot in C# would be wrong the first time it was
    /// re-baked at a different size.</para>
    ///
    /// <para><b>⚠️ A building does NOT pivot bottom-centre, and the gap is METRES, not pixels.</b> The
    /// rig's pivot is the ground CENTRE (<c>{x:cx, y:groundY}</c> in both rigs) — the centre of the
    /// footprint, not its near edge. Under the ¾ camera the whole NEAR HALF of the footprint projects
    /// <i>below</i> that centre (a ground-plane point at <c>−y</c> lands at
    /// <c>groundY + y·sin40°·32</c>), and any porch, eave overhang and foundation that reach past the
    /// wall box add to it. Measured across the five M1 builds: <b>104–209 px, i.e. 3.3–6.5 m</b>.</para>
    ///
    /// <para><see cref="ArtImportPipeline"/> defaults anything under <c>/buildings/</c> to
    /// <see cref="SpriteAlignment.BottomCenter"/> — correct for the loose hand-drawn cottage sprites
    /// beside this kit, and here it would stand every building several metres into the dirt. Nothing
    /// throws; it reads as an art bug. <see cref="VillageBuildingSheetSlicer"/> overrides it per sheet
    /// from this contract, and <see cref="BelowGroundPad"/> is the number.</para>
    ///
    /// <para>The tree kit hits the same class of trap with its root flare, but for a different reason
    /// and at a hundredth of the scale (8–13 px): a tree's pivot is a chosen ROW just under the trunk
    /// foot, whereas a building's is a projected POINT in the middle of its footprint. That is also why
    /// the two kits normalise their pivots differently — see <see cref="NormalizedPivot"/>.</para>
    /// </summary>
    public static class VillageBuildingKit
    {
        /// <summary>
        /// The kit's own folder — a SUBFOLDER of the greybox building art rather than a sibling inside
        /// it, so this tool and the pre-existing loose <c>Buildings/*.png</c> (Cottage, LighthouseIso,
        /// the Nine Mile Creek houses) never see each other's sheets. The same separation
        /// <see cref="TreeKitCatalog.TreesRoot"/> keeps from the loose foliage drop.
        ///
        /// <para>It is also outside the reach of <c>BuildingBakeMenu</c>, whose 12-preset batch writes
        /// <c>HouseIso_*</c>/<c>WharfBuildingIso_*</c> into the parent folder. That batch is a
        /// worst-case bake exercise (it deliberately includes the cannery); this kit is what M1 ships.
        /// Keeping them in different folders means neither can be mistaken for the other's orphan.</para>
        /// </summary>
        public const string BuildingsRoot = "Assets/_Project/Art/Sprites/Buildings/Village/";

        public const string ContractFileName = "Buildings.json";

        public static string ContractPath => BuildingsRoot + ContractFileName;

        /// <summary>Sheet stem prefix. Every sheet this kit owns starts with it, so a stray file in the
        /// folder is recognisable as a stranger rather than guessed at.</summary>
        public const string StemPrefix = "Village_";

        /// <summary>
        /// The DEFAULT importer texture cap. Over it Unity imports SILENTLY DOWNSCALED and the sprite
        /// COUNT still comes out right, so only a cell-size or pivot assert catches it.
        ///
        /// <para>This kit deliberately does NOT lift the cap the way <c>SpriteSheetSlicer</c> does for
        /// the 3648 px hull sheets, because a 4096 texture is the first thing a later mobile port would
        /// have to undo (CLAUDE.md rule 7). Instead the BAKE is solved for this number: the request
        /// carries it as <c>BuildingBakeRequest.MaxSheetDimension</c> so
        /// <c>BuildingRigBaker.ChooseGrid</c> adds a row rather than emitting a sheet the import would
        /// shrink.</para>
        ///
        /// <para>⚠️ <b>That is not a precaution — it is a measured fix.</b> The first real bake of this
        /// set, solved against the baker's own 4096 limit, produced sheets 2800–3876 px wide: entirely
        /// legal for the baker, and every one of them would have imported downscaled while still
        /// reporting the right number of sprites. The cap a kit imports at and the cap its pack is
        /// solved for have to be the same number.</para>
        /// </summary>
        public const int ImportSizeCap = 2048;

        /// <summary>Facings every build bakes. 8 at 45° is the ADR-0006 recipe and what the rigs are
        /// drawn for; the union crop is what makes eight of them affordable.</summary>
        public const int Facings = 8;

        public static string SheetPath(string buildKey) => BuildingsRoot + StemFor(buildKey) + ".png";

        /// <summary>Sheet stem for a build key, e.g. <c>Village_school</c>.</summary>
        public static string StemFor(string buildKey) => StemPrefix + buildKey;

        /// <summary>The sprite name for one facing of one build, e.g. <c>Village_school_d4</c>.</summary>
        public static string SpriteNameFor(string buildKey, int facing) =>
            $"{StemFor(buildKey)}_d{facing}";

        // =================================================================================
        // WHAT M1 NEEDS — the one hand-written table in the chain
        // =================================================================================

        /// <summary>
        /// One build the kit bakes: which rig, and the options to hand <c>render()</c>.
        ///
        /// <para>A build is either one of the rig's own PRESETS (<see cref="Preset"/> set) or a DIALLED
        /// set of axes (<see cref="Dialled"/> set) — never both. The distinction matters to the baker:
        /// a preset gets the "did the preset actually apply?" tripwire against the rig's PRESETS table,
        /// a dialled build gets the same byte-comparison against the rig default.</para>
        /// </summary>
        public readonly struct Build
        {
            /// <summary>Stable key — the sheet stem, the prefab name and the contract key. Append-only.</summary>
            public readonly string Key;

            /// <summary>What the owner sees in a dropdown or the scene hierarchy.</summary>
            public readonly string Label;

            /// <summary>Catalog rig key — <c>"house"</c> or <c>"wharfBuilding"</c>.</summary>
            public readonly string RigKey;

            /// <summary>The rig's own preset name, or null for a dialled build.</summary>
            public readonly string Preset;

            /// <summary>The dialled axes, or null for a preset build. Keys are <c>BuildingAxes</c>
            /// keys — the rig's real option spelling, grep-verified against the rig source by
            /// <c>BuildingAxesTests</c>, which is what keeps a silently-ignored key out of here.</summary>
            public readonly IReadOnlyDictionary<string, object> Dialled;

            /// <summary>Why this build is in the M1 set, in one line — read by nobody and worth
            /// keeping anyway, since "which of these is the school" is the question a future reader
            /// arrives with.</summary>
            public readonly string Why;

            Build(string key, string label, string rigKey, string preset,
                  IReadOnlyDictionary<string, object> dialled, string why)
            {
                Key = key; Label = label; RigKey = rigKey; Preset = preset;
                Dialled = dialled; Why = why;
            }

            public static Build FromPreset(string key, string label, string rigKey, string preset,
                                           string why)
                => new Build(key, label, rigKey, preset, null, why);

            public static Build FromDialled(string key, string label, string rigKey,
                                            Dictionary<string, object> dialled, string why)
                => new Build(key, label, rigKey, null, dialled, why);

            public bool IsPreset => Preset != null;
        }

        /// <summary>
        /// The M1 building set: <b>three clapboard houses, a one-room school and a general store</b> —
        /// exactly the village <c>docs/design/world-and-regions.md</c> §6 gives St Peters, and the five
        /// entries <c>docs/art/asset-manifest.md</c> lists as the P1 "slice gap". Nothing else: the
        /// wharf rig's sheds and the cannery are M2 work and are baked (uncommitted) by
        /// <c>BuildingBakeMenu</c>'s worst-case batch instead.
        ///
        /// <para><b>Why two of the houses are presets and three builds are dialled.</b> The house rig
        /// ships five presets, but only <c>whiteFarmhouse</c> and <c>redSaltbox</c> are clapboard —
        /// <c>shingleCottage</c> and <c>dormerCape</c> are shingle and <c>gothicRevival</c> is a
        /// two-tone show house. The canon asks for clapboard, so the third house is dialled rather
        /// than substituted, and the school and the store have no preset at all. The rig source is not
        /// edited to add them (ADR 0021 §5: his file is what runs) — <c>BuildingBakeRequest.FromOptions</c>
        /// exists for exactly this.</para>
        ///
        /// <para><b>⚠️ These option values are the one hand-written thing in the chain, and unknown
        /// keys fail SILENTLY.</b> Both rigs resolve options as <c>opts[k] != null ? opts[k] :
        /// fallback</c>, so a misspelled key renders something else with no error — the recorded
        /// worked example is <c>winD</c> (the rig's internal field) versus <c>winDensity</c> (the
        /// option). Every key below is therefore a <c>BuildingAxes</c> key, and <c>BuildingAxesTests</c>
        /// already greps every one of those out of the rig source. <c>VillageBuildingSetTests</c> then
        /// asserts this table uses nothing else.</para>
        /// </summary>
        public static readonly Build[] M1Set =
        {
            // ---- the one-room school: the opening's teaching anchor -----------------------------
            // Small enough to read as ONE room (size 0.15 → 6.4 × 7.6 m), white clapboard, and mostly
            // windows — a schoolroom is lit from the sides. One chimney for the stove, no porch, no
            // dormers. The rig has no belfry axis, so it does not pretend to one.
            Build.FromDialled("school", "One-room school", "house", new Dictionary<string, object>
            {
                ["era"] = "colonial",
                ["shape"] = "gable",
                ["siding"] = "clapboard",
                ["body"] = "white",
                ["roof"] = "asphaltGrey",
                ["size"] = 0.15,
                ["windows"] = "sixOverSix",
                ["winDensity"] = 0.85,
                ["attic"] = "gable",
                ["porch"] = "none",
                ["dormers"] = 0,
                ["chimneys"] = 1,
                ["bay"] = false,
                ["weather"] = 0.30,
            }, "world-and-regions §6: the school where the aunt teaches the compass and hand skills"),

            // ---- the general store: basic gear, the clam licence, gas in a can -------------------
            // Bigger than a house room (size 0.45 → 7.1 × 8.9 m), a FRONT porch to shelter the step,
            // and a bay — which is the rig's shop window. Metal roof and cream paint so it reads as
            // the one commercial building in a row of houses.
            Build.FromDialled("generalStore", "General store", "house", new Dictionary<string, object>
            {
                ["era"] = "colonial",
                ["shape"] = "gable",
                ["siding"] = "clapboard",
                ["body"] = "cream",
                ["roof"] = "metal",
                ["size"] = 0.45,
                ["windows"] = "twoOverTwo",
                ["winDensity"] = 0.70,
                ["attic"] = "gable",
                ["porch"] = "front",
                ["dormers"] = 0,
                ["chimneys"] = 1,
                ["bay"] = true,
                ["weather"] = 0.35,
            }, "world-and-regions §6: the general store for basic gear; fuel-and-refuelling §3 sells " +
               "gas in a can over its counter"),

            // ---- the three clapboard houses -----------------------------------------------------

            Build.FromPreset("whiteFarmhouse", "White farmhouse", "house", "whiteFarmhouse",
                "the biggest of the three — an ell with a wrap porch, the one with a family in it"),

            Build.FromPreset("redSaltbox", "Red saltbox", "house", "redSaltbox",
                "the plainest — red clapboard, no porch, the long north roof of a saltbox"),

            // The third house has no clapboard preset to take, so it is dialled: the smallest and the
            // most weathered of the three, so the row reads as houses of different ages.
            Build.FromDialled("sageCottage", "Sage cottage", "house", new Dictionary<string, object>
            {
                ["era"] = "plain",
                ["shape"] = "gable",
                ["siding"] = "clapboard",
                ["body"] = "sage",
                ["roof"] = "asphaltBrown",
                ["size"] = 0.25,
                ["windows"] = "twoOverTwo",
                ["winDensity"] = 0.50,
                ["attic"] = "gable",
                ["porch"] = "front",
                ["dormers"] = 0,
                ["chimneys"] = 1,
                ["bay"] = false,
                ["weather"] = 0.55,
            }, "the third clapboard house the canon asks for; no rig preset is clapboard AND small"),
        };

        /// <summary>The build with this key, or null.</summary>
        public static Build? FindBuild(string key)
        {
            foreach (var b in M1Set)
                if (string.Equals(b.Key, key, StringComparison.Ordinal)) return b;
            return null;
        }

        // =================================================================================
        // the contract schema
        // =================================================================================

        [Serializable]
        public sealed class Contract
        {
            public string note;
            public string writtenBy;

            /// <summary>Pixels per metre the sheets were baked at — the rigs' own <c>PX</c>, not the
            /// import default, so the two cannot disagree in silence.</summary>
            public int ppu;

            public int facings;
            public string conventionNote;
            public string pivotNote;

            public Entry[] buildings;
        }

        [Serializable]
        public sealed class Entry
        {
            /// <summary>The <see cref="Build.Key"/> this entry was baked from.</summary>
            public string key;

            public string label;

            /// <summary>Catalog rig key, its source file and the global it installs — recorded so a
            /// re-bake against a changed rig is visible in the diff.</summary>
            public string rig;
            public string rigScript;
            public string rigGlobal;

            /// <summary>Sheet file name (no folder).</summary>
            public string sheet;

            /// <summary>The rig preset this came from, or empty for a dialled build.</summary>
            public string preset;

            public bool fromPreset;

            /// <summary>The EXACT options expression <c>render()</c> was handed. The audit trail: if a
            /// build ever looks wrong, this says what it was actually asked to draw.</summary>
            public string optionsJs;

            public int facings;
            public int cols;
            public int rows;

            /// <summary>The CROPPED cell every facing is packed at.</summary>
            public int cellW;
            public int cellH;

            /// <summary>The rig's native cell, before the union crop.</summary>
            public int nativeCellW;
            public int nativeCellH;

            /// <summary>Crop origin in the native cell (top-left space) — what the pivot shifted by.</summary>
            public int cropX;
            public int cropY;

            /// <summary>The GROUND CENTRE, in cropped-cell px from the cell's TOP-LEFT.</summary>
            public float pivotX;
            public float pivotY;

            /// <summary>The Unity sprite pivot: normalised, BOTTOM-origin. Equals
            /// <c>(pivotX / cellW, (cellH − pivotY) / cellH)</c> — see
            /// <see cref="NormalizedPivot"/>.</summary>
            public float unityPivotX;
            public float unityPivotY;

            public int sheetW;
            public int sheetH;

            /// <summary>The building's true footprint in metres, as the RIG reports it (and as the
            /// azimuth probe cross-checked against the rendered silhouette). The honest-scale number:
            /// at PPU 32 the drawn thing has to be this wide.</summary>
            public float footprintWidthMetres;
            public float footprintLengthMetres;

            /// <summary>The MEASURED facing convention. The correction is already applied to the
            /// sheet, so cell <c>i</c> genuinely depicts +45°·i.</summary>
            public string convention;

            /// <summary>Per-facing door anchor in cropped-cell px, top-left origin — <c>doorX[i]</c>
            /// pairs with <c>doorY[i]</c>. Two parallel arrays rather than a struct array because
            /// <see cref="JsonUtility"/> will not round-trip a nested array of objects.
            ///
            /// <para>Used for one thing today: <see cref="FrontFacing"/> derives which cell shows the
            /// door instead of anybody declaring it.</para></summary>
            public float[] doorX;
            public float[] doorY;

            public long pngBytes;
            public long runtimeBytesRgba32;

            /// <summary>Fraction of the native cell area the union crop removed.</summary>
            public float cropSaving;
        }

        // =================================================================================
        // reading it back
        // =================================================================================

        /// <summary>The committed contract, or null with a logged error if it is missing/unparseable.</summary>
        public static Contract Load()
        {
            if (!File.Exists(ContractPath))
            {
                Debug.LogError(
                    $"[VillageBuildingKit] No contract at '{ContractPath}'. Run " +
                    "Hidden Harbours ▸ Art ▸ Bake Village Buildings — the sheets and this file are " +
                    "written by the same bake and are only meaningful together.");
                return null;
            }

            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(ContractPath));
            if (contract?.buildings == null || contract.buildings.Length == 0)
            {
                Debug.LogError($"[VillageBuildingKit] '{ContractPath}' parsed but carries no buildings.");
                return null;
            }
            return contract;
        }

        /// <summary>One build's entry, or null if the bake did not cover it.</summary>
        public static Entry Find(Contract contract, string key)
        {
            if (contract?.buildings == null) return null;
            foreach (var e in contract.buildings)
                if (string.Equals(e.key, key, StringComparison.Ordinal)) return e;
            return null;
        }

        /// <summary>The entry a sheet stem belongs to, or null for a stranger (which must fail, not
        /// be guessed at).</summary>
        public static Entry EntryForStem(Contract contract, string stem)
        {
            if (contract?.buildings == null) return null;
            foreach (var e in contract.buildings)
                if (string.Equals(stem, StemFor(e.key), StringComparison.Ordinal)) return e;
            return null;
        }

        /// <summary>Every sheet path the contract claims, in bake order.</summary>
        public static string[] AllSheetPaths(Contract contract)
        {
            if (contract?.buildings == null) return Array.Empty<string>();
            var paths = new string[contract.buildings.Length];
            for (int i = 0; i < paths.Length; i++) paths[i] = SheetPath(contract.buildings[i].key);
            return paths;
        }

        /// <summary>
        /// The Unity sprite pivot for an entry: normalised, BOTTOM-origin, from the GROUND CENTRE.
        ///
        /// <para>⚠️ The y term is <c>(cellH − pivotY)/cellH</c> — the hull/prop convention
        /// (<c>RigGeometry.UnityNormalisedPivot</c>), <b>not</b> the tree kit's <c>pad/cellH</c>. That
        /// difference is settled and MEASURED (ADR 0026 + <c>RigPivotConventionProbe</c>): a rig's
        /// <c>pivot</c> is a CONTINUOUS point whose origin is the cell's top-left corner, and a
        /// building's ground centre is a projected POINT like a hull's, not a chosen ROW like a tree's
        /// trunk foot. Do not unify them.</para>
        /// </summary>
        public static Vector2 NormalizedPivot(Entry e) =>
            new Vector2(e.pivotX / e.cellW, (e.cellH - e.pivotY) / e.cellH);

        /// <summary>The pivot in cell px from the rect's own bottom-left, which is what
        /// <c>Sprite.pivot</c> reports.</summary>
        public static Vector2 PivotPixels(Entry e) =>
            new Vector2(e.pivotX, e.cellH - e.pivotY);

        /// <summary>
        /// How many rows of art sit BELOW the ground centre: the near half of the footprint as the ¾
        /// camera projects it, plus whatever porch, eave and foundation reach past the wall box.
        ///
        /// <para><b>This is exactly the distance a bottom-centre pivot would sink this building into the
        /// ground</b> — 104–209 px (3.3–6.5 m) across the five M1 builds, which is why the slicer
        /// overrides <see cref="ArtImportPipeline"/>'s <c>/buildings/</c> default rather than trusting
        /// it.</para>
        /// </summary>
        public static int BelowGroundPad(Entry e) => Mathf.Max(0, e.cellH - Mathf.RoundToInt(e.pivotY));

        /// <summary>
        /// Which facing shows the DOOR — measured, not declared: the cell whose door anchor sits
        /// LOWEST on screen (largest y in the rigs' top-left space) is the one facing the camera.
        ///
        /// <para>Both rigs put the main door on the <c>+Y</c> gable and <c>+Y</c> projects away from
        /// the camera, so cell 0 is the building's BACK. Deriving the front from the baked anchors
        /// rather than reasoning it out is the difference between a measurement and the kind of
        /// declaration that has been wrong five times in this repo.</para>
        ///
        /// <para>Falls back to 0 when the contract predates the door anchors, so a stale contract
        /// gives a back view rather than an exception.</para>
        /// </summary>
        public static int FrontFacing(Entry e)
        {
            if (e?.doorY == null || e.doorY.Length == 0) return 0;
            int best = 0;
            for (int i = 1; i < e.doorY.Length; i++)
                if (e.doorY[i] > e.doorY[best]) best = i;
            return best;
        }

        /// <summary>Read the sprite mesh type an importer will use — it lives on
        /// <see cref="TextureImporterSettings"/>, not on the importer itself, which is an easy thing
        /// to look for in the wrong place.</summary>
        public static SpriteMeshType MeshTypeOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.spriteMeshType;
        }

        /// <summary>sRGB also lives on <see cref="TextureImporterSettings"/> — same indirection.</summary>
        public static bool SRgbOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.sRGBTexture;
        }

        /// <summary>Total runtime texture cost of the kit, in MiB of RGBA32 — reported rather than
        /// presumed, so the atlas question is answered with a number.</summary>
        public static float TotalRuntimeMib(Contract contract)
        {
            if (contract?.buildings == null) return 0f;
            long bytes = 0;
            foreach (var e in contract.buildings) bytes += e.runtimeBytesRgba32;
            return bytes / 1024f / 1024f;
        }

        /// <summary>Total committed PNG size of the kit, in MiB — what the repo actually carries.</summary>
        public static float TotalPngMib(Contract contract)
        {
            if (contract?.buildings == null) return 0f;
            long bytes = 0;
            foreach (var e in contract.buildings) bytes += e.pngBytes;
            return bytes / 1024f / 1024f;
        }
    }
}
#endif
