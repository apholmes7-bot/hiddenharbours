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
    /// Slicer for the DRIFT WEED kit sheets under <c>Assets/_Project/Art/Sprites/Shore/Drift/</c>
    /// — the seaweed/flotsam surface-drift decor (owner drop 2026-07-23): Bladderwrack, Sugar Kelp,
    /// Eelgrass and Torn Mat, each one sheet of variant COLUMNS × 3 ramp ROWS (living / golden /
    /// bleached, top to bottom; every row is the SAME seed-locked build recoloured).
    ///
    /// <para><b>Why this is not a <see cref="SpriteSheetSlicer"/> manifest entry: the pivot is
    /// per-COLUMN, not per-sheet.</b> The kit's gameplay sidecar (<c>DriftWeed.json</c>) fixes each
    /// variant's <c>buoy</c> — the buoyancy centre (area centroid), "register the sprite to the
    /// water surface here" — and it genuinely differs cell to cell (Bladderwrack v0 (24,18) vs
    /// v1 (24,19); SugarKelp v0 (30,18) vs v2 (31,17)). A single per-sheet pivot const would float
    /// half the variants a pixel or two off their buoyancy centre, and at PPU 32 a pixel is 3 cm of
    /// permanent bob error in the future drift feature. So this tool reads the sidecar — the kit's
    /// own contract, shipped beside the art — and stamps each column's buoy as a Custom pivot on
    /// all 3 ramp rows of that column (structure is seed-stable across rows, so the buoy is too).</para>
    ///
    /// <para>Everything else mirrors the house slicing rules (<see cref="SpriteSheetSlicer"/> /
    /// PR #218): dimension-guarded against the sidecar's cell grid (a drifted re-export fails
    /// loudly, never slices garbage), idempotent spriteIDs (re-slicing unchanged art is a no-op on
    /// the <c>.meta</c> — references resolve by internalID and diff noise buries real changes),
    /// stable name→fileID pairs, slices named <c>&lt;Stem&gt;_&lt;index&gt;</c> row-major from the
    /// TOP-LEFT cell (index = rampRow×cols + variant; row 0 = living, 1 = golden, 2 = bleached).</para>
    ///
    /// <para>Import/slice only — no scene, prefab or spawner wiring. The runtime drift feature
    /// (drift/bob/snag/clump off the shared wave field) is a separate later build; snags and
    /// dragTail stay in the sidecar as data for it. <c>DriftWeedSheetSliceTests</c> holds the slice
    /// to the kit README's cell math and the sidecar's buoys, restated there as literals.</para>
    /// </summary>
    public static class DriftWeedSheetSlicer
    {
        /// <summary>The only folder this tool slices. We never touch textures outside it.</summary>
        public const string DriftRoot = "Assets/_Project/Art/Sprites/Shore/Drift/";

        /// <summary>The kit's gameplay sidecar — the authority for cell grids and buoy pivots.</summary>
        public const string SidecarPath = DriftRoot + "DriftWeed.json";

        /// <summary>The kit ships exactly 3 ramp rows (living / golden / bleached, top to bottom).</summary>
        public const int RampRows = 3;

        // ---- the sidecar model (JsonUtility: the four species are FIELDS because JsonUtility
        // cannot read a dictionary — the kit's species set is closed, and a fifth species arriving
        // would surface as a null field + an unguarded PNG, both loud) ---------------------------

        [Serializable] private class Buoy { public int[] px; }
        [Serializable] private class Variant { public int cell; public Buoy buoy; }

        [Serializable]
        private class Species
        {
            public string sheet;
            public int cellW, cellH, cols, rows;
            public Variant[] variants;
        }

        [Serializable]
        private class SpeciesSet
        {
            public Species Bladderwrack, SugarKelp, Eelgrass, TornMat;
            public IEnumerable<Species> All => new[] { Bladderwrack, SugarKelp, Eelgrass, TornMat };
        }

        [Serializable] private class Sidecar { public SpeciesSet species; }

        // ---- entry points -------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Slice Drift Weed Sheets")]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int failed);
            Debug.Log($"[DriftWeedSheetSlicer] Sliced {n} drift sheet(s) ({failed} failed).");
        }

        /// <summary>Batch entry point for <c>-executeMethod</c> — exits non-zero on any failure so
        /// a headless import fails loudly instead of committing a half-sliced sheet.</summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int failed);
                Debug.Log($"[DriftWeedSheetSlicer] (batch) Sliced {n} drift sheet(s) ({failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[DriftWeedSheetSlicer] {failed} sheet(s) failed to slice — see errors above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DriftWeedSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---- the work -----------------------------------------------------------------------

        public static int SliceAll(out int failed)
        {
            failed = 0;

            if (!File.Exists(SidecarPath))
            {
                Debug.LogError($"[DriftWeedSheetSlicer] No sidecar at '{SidecarPath}' — the sidecar IS " +
                               "the slicing contract (per-variant buoy pivots); nothing can be sliced without it.");
                failed++;
                return 0;
            }

            Sidecar sidecar;
            try
            {
                sidecar = JsonUtility.FromJson<Sidecar>(File.ReadAllText(SidecarPath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[DriftWeedSheetSlicer] '{SidecarPath}' failed to parse: {e.Message}");
                failed++;
                return 0;
            }

            int sliced = 0;
            foreach (Species sp in sidecar.species.All)
            {
                if (sp == null || string.IsNullOrEmpty(sp.sheet))
                {
                    Debug.LogError("[DriftWeedSheetSlicer] Sidecar is missing one of the four species " +
                                   "blocks (Bladderwrack / SugarKelp / Eelgrass / TornMat) — refusing to guess.");
                    failed++;
                    continue;
                }
                if (SliceOne(sp)) sliced++;
                else failed++;
            }

            // A PNG in the folder that no species claims must fail, not slip past unsliced.
            var claimed = new HashSet<string>(
                sidecar.species.All.Where(s => s != null && !string.IsNullOrEmpty(s.sheet))
                       .Select(s => s.sheet), StringComparer.OrdinalIgnoreCase);
            foreach (string png in Directory.GetFiles(DriftRoot, "*.png"))
            {
                string name = Path.GetFileName(png);
                if (!claimed.Contains(name))
                {
                    Debug.LogError($"[DriftWeedSheetSlicer] '{name}' is in {DriftRoot} but no sidecar " +
                                   "species claims it — refusing to guess a grid.");
                    failed++;
                }
            }

            AssetDatabase.SaveAssets();
            return sliced;
        }

        private static bool SliceOne(Species sp)
        {
            string assetPath = DriftRoot + sp.sheet;
            string stem = Path.GetFileNameWithoutExtension(sp.sheet);

            if (!File.Exists(assetPath))
            {
                Debug.LogError($"[DriftWeedSheetSlicer] '{assetPath}' is named by the sidecar but not on disk.");
                return false;
            }

            // Contract guards: the kit is 3 ramp rows, one seed-locked variant per column, each
            // variant carrying its buoy. Anything else means the sidecar and the bake drifted apart.
            if (sp.rows != RampRows)
            {
                Debug.LogError($"[DriftWeedSheetSlicer] '{stem}': sidecar says {sp.rows} rows; the kit " +
                               $"contract is {RampRows} ramp rows (living/golden/bleached).");
                return false;
            }
            if (sp.variants == null || sp.variants.Length != sp.cols)
            {
                Debug.LogError($"[DriftWeedSheetSlicer] '{stem}': {sp.variants?.Length ?? 0} variant(s) " +
                               $"for {sp.cols} columns — every column needs its buoy.");
                return false;
            }

            // buoy per COLUMN, indexed by the variant's own 'cell' field (never by array order).
            var buoyByCol = new Vector2Int[sp.cols];
            var seen = new bool[sp.cols];
            foreach (Variant v in sp.variants)
            {
                if (v.cell < 0 || v.cell >= sp.cols || seen[v.cell])
                {
                    Debug.LogError($"[DriftWeedSheetSlicer] '{stem}': variant cell index {v.cell} is " +
                                   $"out of range or duplicated (cols={sp.cols}).");
                    return false;
                }
                if (v.buoy?.px == null || v.buoy.px.Length != 2 ||
                    v.buoy.px[0] < 0 || v.buoy.px[0] >= sp.cellW ||
                    v.buoy.px[1] < 0 || v.buoy.px[1] >= sp.cellH)
                {
                    Debug.LogError($"[DriftWeedSheetSlicer] '{stem}' variant {v.cell}: buoy missing or " +
                                   $"outside its {sp.cellW}×{sp.cellH} cell.");
                    return false;
                }
                seen[v.cell] = true;
                buoyByCol[v.cell] = new Vector2Int(v.buoy.px[0], v.buoy.px[1]);
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[DriftWeedSheetSlicer] '{assetPath}' has no TextureImporter.");
                return false;
            }

            // Dimension guard (these sheets are far under the 2048 cap — the largest is 192×144 —
            // but a re-export that drifted the grid must fail loudly, not slice garbage).
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                Debug.LogError($"[DriftWeedSheetSlicer] '{assetPath}' failed to load as Texture2D.");
                return false;
            }
            int sheetW = sp.cols * sp.cellW, sheetH = RampRows * sp.cellH;
            if (tex.width != sheetW || tex.height != sheetH)
            {
                Debug.LogError($"[DriftWeedSheetSlicer] '{assetPath}' is {tex.width}×{tex.height} but the " +
                               $"sidecar expects {sheetW}×{sheetH} ({sp.cols}×{RampRows} of {sp.cellW}×{sp.cellH}). " +
                               "Not slicing — fix the export or the sidecar.");
                return false;
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // Idempotence: re-use the spriteID any already-sliced name carries (the PR #218 rule) —
            // a re-slice of unchanged art must be a no-op on the .meta.
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            var rects = new SpriteRect[sp.cols * RampRows];
            for (int r = 0; r < RampRows; r++)                       // r = ramp row from the TOP
            {
                for (int c = 0; c < sp.cols; c++)                    // c = variant column
                {
                    int index = r * sp.cols + c;
                    string name = $"{stem}_{index}";

                    // Buoy px are cell-local, origin TOP-left, +y down (the sidecar's frame).
                    // Unity pivots are normalized from the BOTTOM-left, so y flips: cellH − buoy.y.
                    Vector2Int buoy = buoyByCol[c];
                    var pivot = new Vector2((float)buoy.x / sp.cellW,
                                            (float)(sp.cellH - buoy.y) / sp.cellH);

                    rects[index] = new SpriteRect
                    {
                        name = name,
                        spriteID = existingIds.TryGetValue(name, out var id) ? id : GUID.Generate(),
                        rect = new Rect(c * sp.cellW,
                                        (RampRows - 1 - r) * sp.cellH,   // top row → highest Y
                                        sp.cellW, sp.cellH),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        border = Vector4.zero,
                    };
                }
            }
            dp.SetSpriteRects(rects);

            // Keep name→fileID stable across future reimports so any later reference to a slice
            // survives a re-bake (sprite refs resolve by internalID).
            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIdDp != null)
            {
                nameIdDp.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            }

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[DriftWeedSheetSlicer] Sliced '{stem}' → {rects.Length} sprites " +
                      $"({sp.cols}×{RampRows} of {sp.cellW}×{sp.cellH}, per-variant buoy pivots).");
            return true;
        }
    }
}
#endif
