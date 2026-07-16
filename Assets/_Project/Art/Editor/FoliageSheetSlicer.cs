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
    /// Reusable grid-slicer for the foliage sprite sheets under
    /// <c>Assets/_Project/Art/Foliage/Flowers/</c>. The art director exports each PEI wildflower as a
    /// tiered sheet (see <c>Art/imported-assets.md</c> — "PEI wildflowers"); this bakes the equal-cell
    /// grid slices + per-tier pivots that <see cref="ArtImportPipeline"/> deliberately does <b>not</b> do
    /// (the postprocessor only stamps the pixel-art import lock and a Single-mode Center pivot).
    ///
    /// <para>Three tiers, keyed off the filename suffix, each a fixed cell grid (verified dimensions):</para>
    /// <list type="bullet">
    ///   <item><c>*Single.png</c> — 128×144 = 4 sway cols × 3 bloom-stage rows of 32×48 → 12 sprites,
    ///         <b>bottom-centre</b> pivot.</item>
    ///   <item><c>*Clump.png</c> — 192×46 = 4 sway cols × 1 row of 48×46 → 4 sprites,
    ///         <b>bottom-centre</b> pivot.</item>
    ///   <item><c>*Patch.png</c> — 176×52 = 4 sway cols × 2 variant rows of 44×26 → 8 sprites,
    ///         <b>centre</b> pivot.</item>
    /// </list>
    ///
    /// <para>Slices are named <c>&lt;FileStem&gt;_&lt;index&gt;</c>, row-major from the <b>top-left</b> cell
    /// (Unity's sprite rects are bottom-origin, so the top row maps to the highest Y). This matches the
    /// repo's <c>PlayerHaul_0..N</c> / trap-kit scheme so the sheet loaders sort predictably.</para>
    ///
    /// <para>Decor art only — this tool imports and slices; it never wires flowers into a scene, prefab,
    /// or spawner. Non-matching textures are skipped, and nothing outside <c>Foliage/Flowers/</c> is
    /// touched.</para>
    /// </summary>
    public static class FoliageSheetSlicer
    {
        /// <summary>The only folder this tool slices. We never touch textures outside it.</summary>
        public const string FlowersRoot = "Assets/_Project/Art/Foliage/Flowers/";

        /// <summary>One tier of foliage sheet: its filename suffix, cell grid, and pivot.</summary>
        private readonly struct TierSpec
        {
            public readonly string Suffix;
            public readonly int CellW, CellH, Cols, Rows;
            public readonly SpriteAlignment Alignment;
            public readonly Vector2 Pivot;

            public TierSpec(string suffix, int cellW, int cellH, int cols, int rows,
                            SpriteAlignment alignment, Vector2 pivot)
            {
                Suffix = suffix; CellW = cellW; CellH = cellH; Cols = cols; Rows = rows;
                Alignment = alignment; Pivot = pivot;
            }

            public int Count => Cols * Rows;
            public int SheetW => Cols * CellW;
            public int SheetH => Rows * CellH;
        }

        // The art director's README, as data. Order matters only in that "Single" must not prefix-match
        // a shorter suffix — the three suffixes are distinct, so a simple EndsWith is unambiguous.
        private static readonly TierSpec[] Tiers =
        {
            new TierSpec("Single", 32, 48, 4, 3, SpriteAlignment.BottomCenter, new Vector2(0.5f, 0f)),
            new TierSpec("Clump",  48, 46, 4, 1, SpriteAlignment.BottomCenter, new Vector2(0.5f, 0f)),
            new TierSpec("Patch",  44, 26, 4, 2, SpriteAlignment.Center,       new Vector2(0.5f, 0.5f)),
        };

        // ---- entry points -------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Slice Foliage Flower Sheets")]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int skipped, out int failed);
            Debug.Log($"[FoliageSheetSlicer] Sliced {n} flower sheet(s) " +
                      $"({skipped} skipped, {failed} failed).");
        }

        /// <summary>
        /// Batch entry point for <c>-executeMethod</c>. Refreshes so any freshly-copied PNGs import first,
        /// slices every matching sheet, then exits non-zero if any sheet failed so CI/headless bakes fail
        /// loudly instead of committing a half-sliced sheet.
        /// </summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int skipped, out int failed);
                Debug.Log($"[FoliageSheetSlicer] (batch) Sliced {n} flower sheet(s) " +
                          $"({skipped} skipped, {failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[FoliageSheetSlicer] {failed} sheet(s) failed to slice — see errors above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FoliageSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---- the work -----------------------------------------------------------------------------

        /// <summary>
        /// Slice every matching sheet under <see cref="FlowersRoot"/>. Returns the number sliced;
        /// reports how many were skipped (non-matching suffix / wrong dimensions) and how many failed.
        /// </summary>
        public static int SliceAll(out int skipped, out int failed)
        {
            skipped = 0;
            failed = 0;

            if (!Directory.Exists(FlowersRoot))
            {
                Debug.LogWarning($"[FoliageSheetSlicer] No folder at '{FlowersRoot}' — nothing to slice.");
                return 0;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { FlowersRoot.TrimEnd('/') });
            int sliced = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(FlowersRoot, StringComparison.Ordinal)) continue;
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                switch (SliceOne(path))
                {
                    case SliceResult.Sliced:  sliced++;   break;
                    case SliceResult.Skipped: skipped++;  break;
                    case SliceResult.Failed:  failed++;   break;
                }
            }

            AssetDatabase.SaveAssets();
            return sliced;
        }

        private enum SliceResult { Sliced, Skipped, Failed }

        private static SliceResult SliceOne(string path)
        {
            string stem = Path.GetFileNameWithoutExtension(path);

            if (!TryMatchTier(stem, out TierSpec spec))
                return SliceResult.Skipped; // not a foliage tier sheet — leave it alone

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[FoliageSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return SliceResult.Failed;
            }

            // Guard the sheet size: a re-export that drifted the grid must fail loudly, not slice garbage.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[FoliageSheetSlicer] '{path}' failed to load as Texture2D — skipping.");
                return SliceResult.Failed;
            }
            if (tex.width != spec.SheetW || tex.height != spec.SheetH)
            {
                Debug.LogError(
                    $"[FoliageSheetSlicer] '{path}' is {tex.width}×{tex.height} but the '{spec.Suffix}' tier " +
                    $"expects {spec.SheetW}×{spec.SheetH} ({spec.Cols}×{spec.Rows} of {spec.CellW}×{spec.CellH}). " +
                    "Not slicing — fix the export or the tier spec.");
                return SliceResult.Failed;
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            SpriteRect[] rects = BuildRects(stem, spec);
            dp.SetSpriteRects(rects);

            // Keep name→fileID stable across future reimports (mirrors the package's own slicer) so any
            // later reference to a slice survives a re-bake.
            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameIdDp != null)
            {
                nameIdDp.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            }

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[FoliageSheetSlicer] Sliced '{stem}' → {rects.Length} sprites " +
                      $"({spec.Cols}×{spec.Rows} of {spec.CellW}×{spec.CellH}, {spec.Alignment}).");
            return SliceResult.Sliced;
        }

        /// <summary>Match a file stem to its tier by suffix (Single / Clump / Patch).</summary>
        private static bool TryMatchTier(string stem, out TierSpec spec)
        {
            foreach (var t in Tiers)
            {
                if (stem.EndsWith(t.Suffix, StringComparison.Ordinal))
                {
                    spec = t;
                    return true;
                }
            }
            spec = default;
            return false;
        }

        /// <summary>
        /// Build the grid of <see cref="SpriteRect"/>s, row-major from the TOP-LEFT cell. Unity's rects are
        /// bottom-origin, so the top row (r=0) maps to the highest Y = (Rows-1)*CellH.
        /// </summary>
        private static SpriteRect[] BuildRects(string stem, TierSpec spec)
        {
            var rects = new SpriteRect[spec.Count];
            for (int r = 0; r < spec.Rows; r++)
            {
                for (int c = 0; c < spec.Cols; c++)
                {
                    int index = r * spec.Cols + c;
                    float x = c * spec.CellW;
                    float y = (spec.Rows - 1 - r) * spec.CellH; // top row → top of the (bottom-origin) sheet
                    rects[index] = new SpriteRect
                    {
                        name = $"{stem}_{index}",
                        spriteID = GUID.Generate(),
                        rect = new Rect(x, y, spec.CellW, spec.CellH),
                        alignment = spec.Alignment,
                        pivot = spec.Pivot,
                        border = Vector4.zero,
                    };
                }
            }
            return rects;
        }
    }
}
#endif
