#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Grid-slicer for every sheet the ISO rig pack and its later kits bake — the wharf structure, its
    /// dressing, the village services, the shoreline finds, the boatyard and the deck-loop kit's five
    /// families. Mirrors <see cref="CharacterSheetSlicer"/>:
    /// <see cref="ArtImportPipeline"/> stamps the pixel-art import lock (PPU 32, Point, Uncompressed,
    /// Clamp, alphaIsTransparency) on first import, and this tool adds the Multiple-mode grid and the
    /// per-slice pivot that the postprocessor deliberately does not.
    ///
    /// <para><b>Every number here is READ FROM THE COMMITTED CONTRACT</b> — cell size, pivot, and the
    /// sheet's cols × rows. Nothing is derived from the pixels and nothing is hard-coded per file. The
    /// contracts are the same oracle the bakers assert against, so a sheet and its slice grid
    /// cannot disagree unless the PNG on disk is stale — which is a dimension mismatch, and fails
    /// loudly below rather than slicing garbage.</para>
    ///
    /// <para><b>⚠️ THE CELL IS PER SHEET, NOT PER KIT.</b> Unlike the character sheets (one 64×92 cell
    /// for everything) every piece in this pack has its own cell and its own pivot — 205 distinct cells
    /// across the ten families, from an 11×8 whelk to a 757×592 float set. There is no default to
    /// fall back on, so a sheet whose key is absent from its contract is not sliced at all.</para>
    ///
    /// <para><b>Pivot conversion, the easy silent mistake.</b> The contracts record the pivot in
    /// TOP-LEFT canvas pixels (the rigs' screen origin); Unity normalises from the BOTTOM-LEFT. So it is
    /// <c>(pivotX / cellW, (cellH − pivotY) / cellH)</c> — ADR 0026's formula, the same one
    /// <c>RigGeometry.UnityNormalisedPivot</c> and the rock kit use. Inverted, a piece plants itself
    /// through the deck by twice its own height and nothing reports an error.</para>
    ///
    /// <para><b>Slice names are GEOMETRIC:</b> <c>&lt;Stem&gt;_&lt;index&gt;</c>, row-major from the
    /// top-left cell, exactly as <see cref="SpriteSheetSlicer"/> names every other sheet in the repo. A
    /// name states WHICH CELL, never which way the piece looks — this lane has shipped mislabelled
    /// compass art five times, and the families here do NOT share a handedness: five turn
    /// counter-clockwise and the deck-loop kit's four directional ones turn clockwise. What an index
    /// MEANS is per family and lives in the remarks below, read from the contract, never from a name.</para>
    ///
    /// <list type="bullet">
    ///   <item><b>wharfIso · wharfDecor · utilityIso</b> — 8 cells, <c>index = facing</c>.</item>
    ///   <item><b>shoreFinds</b> — 24 cells, <c>index = variant × 8 + lieAngle</c>. Note the contract's
    ///         <c>sheetAxes</c> prose describes the LOGICAL axes; the committed grids are 24×1 and 12×2,
    ///         so the flat index is what bridges the two. One sheet per find per state.</item>
    ///   <item><b>shipyardIso</b> — 8 cells, <c>index = facing</c>, also counter-clockwise (MEASURED:
    ///         its <c>project()</c> matches wharfIso's at all 8 dirs). ⚠️ Unlike the others it is the
    ///         first family whose grids are <b>not one row</b> — every key packs 3×3 or 2×4, because no
    ///         yard or part fits 8 facings across under the 4096 cap. A 3×3 grid therefore has a NINTH
    ///         SLOT that is not a facing; <see cref="BuildRects"/> emits exactly <c>cells</c> rects and
    ///         leaves it alone. Keys also bake at DIFFERENT px/m (three sites step down to 16/12/8), so
    ///         two shipyard sheets do not share an atlas grid.</item>
    ///   <item><b>deckGear · trap · fishTray2 · buoyIso</b> — 8 cells, <c>index = facing</c>, and
    ///         <b>CLOCKWISE</b>: the first family in this file where cell <c>i</c> depicts heading
    ///         <c>+45°·i</c> rather than <c>−45°·i</c>. Nothing in the SHEET distinguishes the two, so
    ///         a reader must take the convention from the contract's <c>azimuth</c> (which is what
    ///         <c>IsoPackSprites.FacingForHeading</c> does) and never from the fact that the other five
    ///         families here turn the other way.
    ///         <br/>The three families' key vocabularies differ (<c>DeckGear.KINDS</c>,
    ///         <c>TrapIso.BUILDS</c>, <c>BuoyIso.FLEET</c>) but that is the BAKER's problem: to this
    ///         slicer a key is a sheet stem, and <c>buoyIso</c>'s eight fleet colour schemes over one
    ///         10×32 geometry are eight ordinary sheets — same cell, same grid, different paint.
    ///         They are NOT collapsed into one sheet, though 98.8–99.0% of each scheme's pixels are
    ///         shared with its own <c>dir</c> 0: a packer may collapse a near-symmetric row, a slicer
    ///         may not assume one was.</item>
    ///   <item><b>trapFauna</b> — <b>1 cell</b>, and the index is nothing at all. What a pot comes up
    ///         holding is not directional (<c>render(kind, opts)</c> takes no <c>dir</c>), so the KIND
    ///         is the sheet and the sheet is a single 1×1 cell. Its contract states <c>facings: 1</c>
    ///         rather than omitting the axis, and <see cref="CellsPerSheet"/> reads it like any other
    ///         facing count — an absent field would default to 0 and slice nothing. The six kinds are
    ///         six different cell sizes (11×8 to 18×20), so they could not share one grid even if there
    ///         were an axis to share it on.</item>
    /// </list>
    ///
    /// <para>Import + slicing ONLY. This builds no Def asset, no prefab and no presenter, and touches
    /// nothing outside the folders in <see cref="Families"/>.</para>
    /// </summary>
    public static class IsoPackSheetSlicer
    {
        /// <summary>
        /// One family: where its sheets land, where its contract is committed, and — for a family that
        /// shares a KIT contract with siblings — which section of that file is its own.
        ///
        /// <para>A section is not cosmetic. Five families registered against one file with no section
        /// would each slice against the first family's cells: a real grid, a real pivot, and the wrong
        /// ones, on every sheet.</para>
        /// </summary>
        public readonly struct Family
        {
            public readonly string Key, Folder, ContractPath, Section;

            /// <summary>A family whose contract sits beside its own sheets — the pack's original five.</summary>
            public Family(string key, string folder, string contract)
                : this(key, folder, folder + contract, null) { }

            /// <summary>A family whose contract lives elsewhere and/or holds more than one family.</summary>
            public Family(string key, string folder, string contractPath, string section)
            {
                Key = key; Folder = folder; ContractPath = contractPath; Section = section;
            }

            /// <summary>The contract file, for a log line — with the section when there is one.</summary>
            public string ContractLabel =>
                string.IsNullOrEmpty(Section) ? ContractPath : $"{ContractPath}#{Section}";
        }

        public const string SpritesRoot = "Assets/_Project/Art/Sprites/";

        /// <summary>The deck-loop kit's one contract, governing five families in five folders.</summary>
        public const string DeckLoopKitContract = SpritesRoot + "DeckGear/deckLoopKit.contract.json";

        public static readonly IReadOnlyList<Family> Families = new[]
        {
            new Family("wharfIso",   SpritesRoot + "Wharf/Iso/",      "wharfIsoRig.contract.json"),
            new Family("wharfDecor", SpritesRoot + "Wharf/Decor/",    "wharfDecorRig.contract.json"),
            new Family("utilityIso", SpritesRoot + "Utility/",        "utilityIsoRig.contract.json"),
            new Family("shoreFinds", SpritesRoot + "Shore/FindsIso/", "shoreFindsRig.contract.json"),
            new Family("shipyardIso", SpritesRoot + "Shipyard/Iso/",  "shipyardIsoRig.contract.json"),

            // ---- the deck-loop kit: five families, one contract, one folder each ------------------
            // The folders are per family and NOT per contract: 24 sheets in a single folder would make
            // every family's slice pass walk the other four's PNGs and warn about each one, and any
            // future stem collision between two families would slice one against the other's grid.
            new Family("deckGear",  SpritesRoot + "DeckGear/Gear/",  DeckLoopKitContract, "deckGear"),
            new Family("trap",      SpritesRoot + "DeckGear/Traps/", DeckLoopKitContract, "trap"),
            new Family("fishTray2", SpritesRoot + "DeckGear/Trays/", DeckLoopKitContract, "tray"),
            new Family("buoyIso",   SpritesRoot + "DeckGear/Buoys/", DeckLoopKitContract, "buoy"),
            new Family("trapFauna", SpritesRoot + "DeckGear/Fauna/", DeckLoopKitContract, "trapFauna"),
        };

        // ---- the contract, as JsonUtility sees it ---------------------------------------------------
        // A second, minimal reader of the same files that HiddenHarbours.Tools.RigBaking's
        // IsoPackContract reads. NOT an oversight: Tools.RigBaking.Editor references Art.Editor and not
        // the other way round, so the type cannot be shared without inverting that dependency. The rock
        // kit splits the same way (RockIsoCatalog here, RockIsoBaker there) — and per
        // CharacterIsoSheetSliceTests, two independent readers of one contract is a cross-check rather
        // than a cost. What must NOT be duplicated is the numbers, and none are: everything comes from
        // the file.

        [Serializable]
        private sealed class SheetPlan { public int cols, rows, sheetW, sheetH; }

        [Serializable]
        private sealed class Cell
        {
            public string key;
            public int cellW, cellH, pivotX, pivotY;
            public SheetPlan sheet;
        }

        [Serializable]
        private sealed class Contract
        {
            public string rig;
            public int importSizeCap, count, facings, variants;
            // The contracts carry array-valued documentation as JSON STRINGS so JsonUtility can hold it.
            public string lieAngles, states;
            public List<Cell> cells;
        }

        // A KIT contract — one file, several families. Parsed by a SECOND reader rather than a widened
        // `Contract`, because the shapes collide outright: `families` is a JSON string of names on
        // wharfIso and an array of objects here, and JsonUtility does not tolerate that.
        [Serializable]
        private sealed class KitFamily
        {
            public string key;
            public int facings, count;
            public List<Cell> cells;
        }

        [Serializable]
        private sealed class KitContract
        {
            public string kit;
            public int importSizeCap;
            public List<KitFamily> families;
        }

        /// <summary>
        /// Cells on one packed sheet: <c>facings</c> for the three directional families, and
        /// <c>lieAngles × variants</c> = 24 for the finds, whose axes are lie angle and variant.
        ///
        /// <para>Derived, never stated: the finds contract carries no <c>facings</c> field at all, and
        /// reading the missing field back as JsonUtility's default 0 is what would silently turn a
        /// 24-cell sheet into a no-cell one.</para>
        /// </summary>
        private static int CellsPerSheet(Contract c) =>
            c.facings > 0 ? c.facings : JsonStringArray(c.lieAngles).Count * c.variants;

        /// <summary>Weathering states — one SHEET per find per state. Empty for the directional families.</summary>
        private static IReadOnlyList<string> States(Contract c) => JsonStringArray(c.states);

        /// <summary>Parse a contract's <c>"[\"wet\",\"dry\"]"</c> JSON-string field.</summary>
        private static IReadOnlyList<string> JsonStringArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
            return json.Trim().TrimStart('[').TrimEnd(']')
                       .Split(',')
                       .Select(s => s.Trim().Trim('"'))
                       .Where(s => s.Length > 0)
                       .ToArray();
        }

        /// <summary>One sheet to slice: its path, its grid, and the pivot every slice carries.</summary>
        private readonly struct SheetSpec
        {
            public readonly string AssetPath, Key;
            public readonly int Cols, Rows, CellW, CellH, Cells, ImportSizeCap;
            public readonly Vector2 Pivot;

            public SheetSpec(string assetPath, string key, int cols, int rows, int cellW, int cellH,
                             int cells, int importSizeCap, Vector2 pivot)
            {
                AssetPath = assetPath; Key = key; Cols = cols; Rows = rows;
                CellW = cellW; CellH = cellH; Cells = cells; ImportSizeCap = importSizeCap; Pivot = pivot;
            }

            public int SheetW => Cols * CellW;
            public int SheetH => Rows * CellH;
            public string Stem => Path.GetFileNameWithoutExtension(AssetPath);
        }

        /// <summary>
        /// ADR 0026's conversion, in one place: a TOP-LEFT-origin contract pivot to Unity's
        /// BOTTOM-LEFT-normalised one.
        /// </summary>
        public static Vector2 NormalisedPivot(int cellW, int cellH, int pivotX, int pivotY) =>
            new Vector2(pivotX / (float)cellW, (cellH - pivotY) / (float)cellH);

        // ---- entry points ---------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Slice Iso Rig Pack Sheets", priority = 211)]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int skipped, out int failed);
            Debug.Log($"[IsoPackSheetSlicer] Sliced {n} iso-pack sheet(s) " +
                      $"({skipped} skipped, {failed} failed).");
        }

        /// <summary>
        /// Batch entry point for <c>-executeMethod</c>. Refreshes first so freshly-baked PNGs import
        /// before we reach for their importers, then exits non-zero if any sheet failed — a half-sliced
        /// sheet must not be committed as a success.
        /// </summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int skipped, out int failed);
                Debug.Log($"[IsoPackSheetSlicer] (batch) Sliced {n} iso-pack sheet(s) " +
                          $"({skipped} skipped, {failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[IsoPackSheetSlicer] {failed} sheet(s) failed to slice — see errors above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[IsoPackSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Assert every sheet on disk imported Multiple-mode with the right slice count and pivot.
        /// The count alone is not enough: an over-cap sheet imports DOWNSCALED with the count still
        /// correct, and only the cell-size/pivot check catches it.
        /// </summary>
        public static void VerifyAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                if (VerifyAll(logEachPass: true))
                {
                    Debug.Log("[IsoPackSheetSlicer] VERIFY PASSED.");
                    return;
                }
                Debug.LogError("[IsoPackSheetSlicer] VERIFY FAILED — see mismatches above.");
                EditorApplication.Exit(1);
            }
            catch (Exception e)
            {
                Debug.LogError($"[IsoPackSheetSlicer] verify threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---- planning -------------------------------------------------------------------------------

        /// <summary>
        /// Every sheet this family expects to find on disk, with the grid its contract plans for it.
        ///
        /// <para>Sheets are matched by STEM against the contract's keys — <c>&lt;key&gt;</c> for the
        /// directional families, <c>&lt;key&gt;_&lt;state&gt;</c> for the finds, which ship one sheet per
        /// find per weathering state. A PNG in the folder whose stem matches nothing is left alone (it
        /// is not this tool's), and a contract key with no PNG is simply not yet baked.</para>
        /// </summary>
        public static List<string> PlannedSheetPaths(Family fam)
        {
            var outp = new List<string>();
            var c = LoadContract(fam);
            if (c == null) return outp;

            var states = States(c);
            foreach (var cell in c.cells)
            {
                if (states.Count == 0) { outp.Add($"{fam.Folder}{cell.key}.png"); continue; }
                foreach (string s in states) outp.Add($"{fam.Folder}{cell.key}_{s}.png");
            }
            return outp;
        }

        private static Contract LoadContract(Family fam)
        {
            string path = fam.ContractPath;
            if (!File.Exists(path))
            {
                Debug.LogError($"[IsoPackSheetSlicer] '{fam.Key}' contract missing at '{path}'. " +
                               "The slice grid comes from the contract; there is nothing to slice against.");
                return null;
            }

            string json = File.ReadAllText(path);
            Contract c = string.IsNullOrEmpty(fam.Section)
                ? JsonUtility.FromJson<Contract>(json)
                : ReadKitSection(json, fam);

            if (c?.cells == null || c.cells.Count == 0)
            {
                Debug.LogError($"[IsoPackSheetSlicer] '{fam.ContractLabel}' parsed but carries no cells.");
                return null;
            }
            if (c.cells.Count != c.count)
            {
                Debug.LogError($"[IsoPackSheetSlicer] '{fam.ContractLabel}' declares count={c.count} but " +
                               $"carries {c.cells.Count} cells — it is mid-edit. Not slicing against it.");
                return null;
            }
            return c;
        }

        /// <summary>
        /// One family of a kit contract, flattened into the shape the rest of this file speaks. The
        /// cap is the KIT's (the whole kit imports at one number, so a bake and an import cannot
        /// disagree); the cells, the count and the facing axis are the FAMILY's.
        /// </summary>
        private static Contract ReadKitSection(string json, Family fam)
        {
            var kit = JsonUtility.FromJson<KitContract>(json);
            var section = kit?.families?.FirstOrDefault(
                f => string.Equals(f.key, fam.Section, StringComparison.Ordinal));

            if (section == null)
            {
                Debug.LogError($"[IsoPackSheetSlicer] '{fam.ContractPath}' carries no family " +
                               $"'{fam.Section}'. It has: " +
                               string.Join(", ", kit?.families?.Select(f => f.key) ?? Enumerable.Empty<string>()) +
                               ". Not slicing — an unresolved section would silently take the first " +
                               "family's cells and slice every sheet against the wrong grid.");
                return null;
            }

            return new Contract
            {
                rig = fam.Key,
                importSizeCap = kit.importSizeCap,
                count = section.count,
                facings = section.facings,
                cells = section.cells,
            };
        }

        /// <summary>
        /// Every PNG on disk under this family's folder, paired with the contract cell that governs it.
        /// </summary>
        private static List<SheetSpec> Specs(Family fam)
        {
            var specs = new List<SheetSpec>();
            var c = LoadContract(fam);
            if (c == null || !Directory.Exists(fam.Folder)) return specs;

            var byKey = c.cells.GroupBy(x => x.key).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            int cellsPerSheet = CellsPerSheet(c);
            if (cellsPerSheet <= 0)
            {
                Debug.LogError($"[IsoPackSheetSlicer] '{fam.Key}' contract declares neither facings nor " +
                               "lieAngles × variants, so a sheet's cell count is unknown. Not slicing.");
                return specs;
            }

            foreach (string path in Directory.GetFiles(fam.Folder, "*.png", SearchOption.TopDirectoryOnly)
                                             .Select(p => p.Replace('\\', '/'))
                                             .OrderBy(p => p, StringComparer.Ordinal))
            {
                string stem = Path.GetFileNameWithoutExtension(path);

                // Longest-prefix match, so `RopeScrap_wet` resolves to `RopeScrap` and a find whose name
                // is a prefix of another's cannot steal its sheets.
                Cell cell = null;
                string matched = null;
                foreach (var kv in byKey)
                {
                    if (stem != kv.Key && !stem.StartsWith(kv.Key + "_", StringComparison.Ordinal)) continue;
                    if (matched != null && kv.Key.Length <= matched.Length) continue;
                    matched = kv.Key; cell = kv.Value;
                }

                if (cell == null)
                {
                    Debug.LogWarning($"[IsoPackSheetSlicer] '{path}' matches no key in {fam.ContractLabel} — " +
                                     "leaving it alone. A sheet with no contract cell has no grid.");
                    continue;
                }

                if (cell.sheet == null || cell.sheet.cols <= 0 || cell.sheet.rows <= 0)
                {
                    Debug.LogError($"[IsoPackSheetSlicer] '{matched}' carries no sheet plan in " +
                                   $"{fam.ContractLabel}. Not slicing '{stem}'.");
                    continue;
                }

                // A grid SMALLER than the cell count cannot hold the sheet — that is a real
                // plan-vs-bake disagreement. A LARGER one is legitimate: shipyardIso's ruled plans
                // are ragged by design (3×3 for 8 facings — the cap forces near-square grids, and
                // the ninth slot is deliberately empty). BuildRects emits exactly cellsPerSheet
                // row-major rects and never touches the trailing slots, so the padding is inert.
                if (cell.sheet.cols * cell.sheet.rows < cellsPerSheet)
                {
                    Debug.LogError($"[IsoPackSheetSlicer] '{matched}' plans a {cell.sheet.cols}×" +
                                   $"{cell.sheet.rows} grid = {cell.sheet.cols * cell.sheet.rows} slots, " +
                                   $"which cannot hold a {fam.Key} sheet's {cellsPerSheet} cells. " +
                                   "Not slicing — the plan and the bake disagree.");
                    continue;
                }

                specs.Add(new SheetSpec(path, matched, cell.sheet.cols, cell.sheet.rows,
                                        cell.cellW, cell.cellH, cellsPerSheet, c.importSizeCap,
                                        NormalisedPivot(cell.cellW, cell.cellH, cell.pivotX, cell.pivotY)));
            }
            return specs;
        }

        // ---- the work -------------------------------------------------------------------------------

        /// <summary>Slice every baked sheet in every registered family.</summary>
        public static int SliceAll(out int skipped, out int failed)
        {
            skipped = 0;
            failed = 0;
            int sliced = 0;

            foreach (var fam in Families)
            {
                var specs = Specs(fam);
                if (specs.Count == 0)
                {
                    Debug.Log($"[IsoPackSheetSlicer] {fam.Key}: no sheets on disk under '{fam.Folder}' " +
                              "— nothing to slice (bake first).");
                    continue;
                }

                foreach (var spec in specs)
                {
                    switch (SliceOne(spec))
                    {
                        case SliceResult.Sliced: sliced++; break;
                        case SliceResult.Skipped: skipped++; break;
                        case SliceResult.Failed: failed++; break;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            return sliced;
        }

        /// <summary>Slice one family only — what a single bake menu item calls after its own bake.</summary>
        public static int SliceFamily(string familyKey, out int skipped, out int failed)
        {
            skipped = 0;
            failed = 0;
            int sliced = 0;

            foreach (var fam in Families)
            {
                if (!string.Equals(fam.Key, familyKey, StringComparison.Ordinal)) continue;
                foreach (var spec in Specs(fam))
                {
                    switch (SliceOne(spec))
                    {
                        case SliceResult.Sliced: sliced++; break;
                        case SliceResult.Skipped: skipped++; break;
                        case SliceResult.Failed: failed++; break;
                    }
                }
                AssetDatabase.SaveAssets();
                return sliced;
            }

            Debug.LogError($"[IsoPackSheetSlicer] '{familyKey}' is not an iso-pack family. Known: " +
                           string.Join(", ", Families.Select(f => f.Key)) + ".");
            failed++;
            return 0;
        }

        private enum SliceResult { Sliced, Skipped, Failed }

        private static SliceResult SliceOne(SheetSpec spec)
        {
            if (!File.Exists(spec.AssetPath))
            {
                Debug.LogWarning($"[IsoPackSheetSlicer] '{spec.AssetPath}' not on disk — skipping.");
                return SliceResult.Skipped;
            }

            var importer = AssetImporter.GetAtPath(spec.AssetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[IsoPackSheetSlicer] '{spec.AssetPath}' has no TextureImporter — skipping.");
                return SliceResult.Failed;
            }

            // ⚠️ Lift the cap FIRST, before anything reads the texture. Unity's default maxTextureSize is
            // 2048 and this pack's biggest sheets are 3784 px, so they would import DOWNSCALED — and a
            // downscale is SILENT: the rects are authored in source pixels, the reimport refits them to
            // the smaller texture, and they come back alpha-trimmed with the pivot thrown away. The
            // sprite COUNT still looks right, which is why only the pivot assert catches it.
            int needed = Mathf.NextPowerOfTwo(Mathf.Max(spec.SheetW, spec.SheetH));
            if (needed > spec.ImportSizeCap)
            {
                // The bake cap and the IMPORT cap are the same number by construction; if they ever part,
                // the sheet is over budget and re-packing is the fix, not a bigger texture.
                Debug.LogError($"[IsoPackSheetSlicer] '{spec.Stem}' is {spec.SheetW}×{spec.SheetH} and " +
                               $"needs a {needed} px import, over its family's {spec.ImportSizeCap} px " +
                               "cap. Re-pack into more rows rather than raising the cap. Not slicing.");
                return SliceResult.Failed;
            }
            if (importer.maxTextureSize < needed)
            {
                Debug.Log($"[IsoPackSheetSlicer] '{spec.Stem}' is {spec.SheetW}×{spec.SheetH} but the " +
                          $"importer caps at {importer.maxTextureSize} — raising maxTextureSize to " +
                          $"{needed} so the sheet imports at native res.");
                importer.maxTextureSize = needed;
                importer.SaveAndReimport();
            }

            // Load AFTER the reimport above — a mid-build import invalidates any texture read before it.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.AssetPath);
            if (tex == null)
            {
                Debug.LogError($"[IsoPackSheetSlicer] '{spec.AssetPath}' failed to load as Texture2D — skipping.");
                return SliceResult.Failed;
            }
            if (tex.width != spec.SheetW || tex.height != spec.SheetH)
            {
                Debug.LogError(
                    $"[IsoPackSheetSlicer] '{spec.AssetPath}' is {tex.width}×{tex.height} but its " +
                    $"contract plans {spec.SheetW}×{spec.SheetH} ({spec.Cols}×{spec.Rows} of " +
                    $"{spec.CellW}×{spec.CellH}). The sheet and the contract disagree — re-bake, or " +
                    "regenerate the contract. Not slicing.");
                return SliceResult.Failed;
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // ⚠️ Re-use the spriteID any already-sliced name carries. GUID.Generate() on every run makes a
            // re-bake rewrite every spriteID in every .meta — pure diff noise that buries the owner's
            // real changes. Same fix as CharacterSheetSlicer (#218) and SpriteSheetSlicer: re-slicing
            // unchanged art produces a byte-identical .meta.
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(spec.Stem, spec.Cols, spec.Rows, spec.Cells,
                                            spec.CellW, spec.CellH, spec.Pivot, existingIds);
            dp.SetSpriteRects(rects);

            // Keep name→fileID stable across future reimports (mirrors the package's own slicer) so any
            // later reference to a slice survives a re-bake.
            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[IsoPackSheetSlicer] Sliced '{spec.Stem}' → {rects.Length} sprites " +
                      $"({spec.Cols}×{spec.Rows} of {spec.CellW}×{spec.CellH}, pivot {spec.Pivot}).");
            return SliceResult.Sliced;
        }

        /// <summary>
        /// Build the grid of <see cref="SpriteRect"/>s, row-major from the TOP-LEFT cell. Unity's rects
        /// are bottom-origin, so the top row (r = 0) maps to the highest Y.
        ///
        /// <para>Exactly <paramref name="cells"/> rects are emitted, which may be fewer than
        /// <c>cols × rows</c> if a plan ever leaves a ragged tail — a slice for a cell the bake never
        /// wrote would be a fully transparent sprite that still counts.</para>
        /// </summary>
        public static SpriteRect[] BuildRects(string stem, int cols, int rows, int cells,
                                              int cellW, int cellH, Vector2 pivot,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            var rects = new SpriteRect[cells];
            for (int i = 0; i < cells; i++)
            {
                string name = $"{stem}_{i}";
                rects[i] = new SpriteRect
                {
                    name = name,
                    spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                               ? id
                               : GUID.Generate(),
                    rect = CellRect(i, cols, rows, cellW, cellH),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    border = Vector4.zero,
                };
            }
            return rects;
        }

        /// <summary>
        /// Where cell <paramref name="index"/> sits on the sheet: row-major from the TOP-LEFT, expressed
        /// in Unity's BOTTOM-origin rect space.
        ///
        /// <para>The <c>(rows − 1 − r)</c> is the whole flip, and it is the one piece of this slicer that
        /// can be wrong while everything still imports. Without it the grid reads bottom-up: a
        /// directional sheet then pairs every cell with the wrong facing, and a finds sheet pairs every
        /// lie angle with the wrong variant. Both are complete, plausible, wrong sheets.</para>
        ///
        /// <para>Pure arithmetic and public, so a test pins the flip without needing a texture, an
        /// importer, or the sprite-editor assembly.</para>
        /// </summary>
        public static Rect CellRect(int index, int cols, int rows, int cellW, int cellH)
        {
            int c = index % cols, r = index / cols;
            return new Rect(c * cellW, (rows - 1 - r) * cellH, cellW, cellH);
        }

        // ---- verify ---------------------------------------------------------------------------------

        /// <summary>Assert every sheet on disk sliced to the contract's count, cell and pivot.</summary>
        public static bool VerifyAll(bool logEachPass)
        {
            bool allOk = true;
            int n = 0;

            foreach (var fam in Families)
            {
                foreach (var spec in Specs(fam))
                {
                    n++;
                    var importer = AssetImporter.GetAtPath(spec.AssetPath) as TextureImporter;
                    if (importer == null || importer.spriteImportMode != SpriteImportMode.Multiple)
                    {
                        Debug.LogError($"[IsoPackSheetSlicer] VERIFY: '{spec.Stem}' is not Multiple-mode.");
                        allOk = false;
                        continue;
                    }

                    // ⚠️ LoadAllAssetsAtPath, never LoadAssetAtPath<Sprite> — a Multiple-mode sheet
                    // returns null from the latter.
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(spec.AssetPath).OfType<Sprite>().ToArray();
                    if (sprites.Length != spec.Cells)
                    {
                        Debug.LogError($"[IsoPackSheetSlicer] VERIFY: '{spec.Stem}' has {sprites.Length} " +
                                       $"sprites, expected {spec.Cells}.");
                        allOk = false;
                        continue;
                    }

                    float expX = spec.Pivot.x * spec.CellW, expY = spec.Pivot.y * spec.CellH;
                    bool ok = true;
                    foreach (var s in sprites)
                    {
                        if (Mathf.Abs(s.rect.width - spec.CellW) > 0.01f ||
                            Mathf.Abs(s.rect.height - spec.CellH) > 0.01f)
                        {
                            Debug.LogError($"[IsoPackSheetSlicer] VERIFY: '{s.name}' is " +
                                           $"{s.rect.width}×{s.rect.height}, expected " +
                                           $"{spec.CellW}×{spec.CellH} — the classic silent downscale.");
                            ok = false;
                        }
                        if (Mathf.Abs(s.pivot.x - expX) > 0.01f || Mathf.Abs(s.pivot.y - expY) > 0.01f)
                        {
                            Debug.LogError($"[IsoPackSheetSlicer] VERIFY: '{s.name}' pivot {s.pivot} " +
                                           $"expected ({expX},{expY}).");
                            ok = false;
                        }
                    }
                    if (!ok) { allOk = false; continue; }

                    if (logEachPass)
                        Debug.Log($"[IsoPackSheetSlicer] VERIFY OK: {spec.Stem} = {sprites.Length} " +
                                  $"sprites ({spec.Cols}×{spec.Rows} of {spec.CellW}×{spec.CellH}).");
                }
            }

            Debug.Log($"[IsoPackSheetSlicer] VERIFY: checked {n} sheet(s) — " +
                      (allOk ? "ALL PASS" : "FAILURES PRESENT"));
            return allOk;
        }
    }
}
#endif
