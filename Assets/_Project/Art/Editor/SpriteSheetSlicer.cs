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
    /// General, manifest-driven grid slicer for the non-foliage sprite sheets in the art director's
    /// environment/VFX drop (see <c>Art/imported-assets.md</c>). Where <see cref="FoliageSheetSlicer"/>
    /// keys tiers off a filename suffix under one folder, this drives off an explicit
    /// <see cref="Sheets"/> manifest of <c>(assetPath, cols, rows, cellW, cellH, alignment, pivot)</c> so
    /// each sheet's exact grid + pivot is spelled out and dimension-guarded.
    ///
    /// <para>What it bakes (equal-cell grids that <see cref="ArtImportPipeline"/> deliberately does not —
    /// the postprocessor only stamps the pixel-art import lock + a Single-mode default pivot):</para>
    /// <list type="bullet">
    ///   <item><b>Shoreline finds</b> — <c>Sprites/Shore/Finds/*</c>, each 2 cols (variant a/b) × 2 rows
    ///         (wet/dry) → 4 sprites, centre pivot.</item>
    ///   <item><b>Seaweed clumps</b> — <c>Sprites/Shore/Seaweed*</c>, each 3 cols (float-a · float-b ·
    ///         beached) × 1 row → 3 sprites, centre pivot.</item>
    ///   <item><b>Deck fish tray</b> — <c>Sprites/Gear/FishTray</c>, 5 cols (fill 0..4) × 3 rows
    ///         (lobster / crab / mixed) → 15 sprites, bottom-centre pivot.</item>
    /// </list>
    ///
    /// <para>Slices are named <c>&lt;FileStem&gt;_&lt;index&gt;</c>, row-major from the <b>top-left</b> cell
    /// (Unity's sprite rects are bottom-origin, so the top row maps to the highest Y). This matches the
    /// repo's <c>PlayerHaul_0..N</c> / flower-sheet scheme so sheet loaders sort predictably.</para>
    ///
    /// <para>Import/slice only — this never wires a sprite into a scene, prefab, or spawner. Sheets not in
    /// the manifest are untouched; a sheet whose on-disk dimensions don't match its manifest cell grid
    /// fails loudly (no silent garbage slice).</para>
    /// </summary>
    public static class SpriteSheetSlicer
    {
        /// <summary>One sheet in the drop: its asset path, cell grid, and pivot.</summary>
        private readonly struct SheetSpec
        {
            public readonly string AssetPath;
            public readonly int Cols, Rows, CellW, CellH;
            public readonly SpriteAlignment Alignment;
            public readonly Vector2 Pivot;

            public SheetSpec(string assetPath, int cols, int rows, int cellW, int cellH,
                             SpriteAlignment alignment, Vector2 pivot)
            {
                AssetPath = assetPath; Cols = cols; Rows = rows; CellW = cellW; CellH = cellH;
                Alignment = alignment; Pivot = pivot;
            }

            public int Count => Cols * Rows;
            public int SheetW => Cols * CellW;
            public int SheetH => Rows * CellH;
            public string Stem => Path.GetFileNameWithoutExtension(AssetPath);
        }

        private const string Root = "Assets/_Project/Art/";
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Bottom = new Vector2(0.5f, 0f);

        // The iso-dory waterline pivot: the art director's README fixes the anchor at (80, 88) measured
        // from each 160×156 cell's TOP-LEFT (the hull's waterline contact point). Unity pivots are
        // normalized from the BOTTOM-left, so the bottom-origin y is (156−88)=68 → (80/160, 68/156) =
        // ≈(0.5, 0.4359). Every heading/frame slice shares it so a heading- or rock-frame swap never
        // shifts the boat (README: "so a heading- or frame-swap never shifts the boat").
        private static readonly Vector2 DoryWaterline = new Vector2(80f / 160f, 68f / 156f);

        // The art director's README, as data. Cell sizes are verbatim from Art/imported-assets.md.
        // NOTE: CatchSparkle (VFX/CatchSparkle.png) is intentionally absent — it already shipped sliced
        // in an earlier PR; re-slicing here would rewrite its .meta (new sprite GUIDs) and break refs.
        private static readonly SheetSpec[] Sheets =
        {
            // ---- Shoreline finds: 2 cols (variant a/b) × 2 rows (wet/dry) → 4 each, centre pivot ----
            new SheetSpec(Root + "Sprites/Shore/Finds/Bone.png",         2, 2, 22, 12, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/CrabMoult.png",    2, 2, 20, 16, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/Driftwood.png",    2, 2, 32, 16, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/GullFeather.png",  2, 2, 22, 14, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/Mussel.png",       2, 2, 18, 14, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/Oyster.png",       2, 2, 22, 16, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/Periwinkle.png",   2, 2, 16, 14, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/SandDollar.png",   2, 2, 16, 16, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/Scallop.png",      2, 2, 18, 16, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/SeaGlass.png",     2, 2, 14, 12, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/SoftShellClam.png",2, 2, 18, 14, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/Finds/Starfish.png",     2, 2, 20, 20, SpriteAlignment.Center, Centre),

            // ---- Seaweed clumps: 3 cols (float-a · float-b · beached) × 1 row → 3 each, centre pivot --
            new SheetSpec(Root + "Sprites/Shore/SeaweedWisp.png",   3, 1, 12, 8,  SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/SeaweedClump.png",  3, 1, 20, 14, SpriteAlignment.Center, Centre),
            new SheetSpec(Root + "Sprites/Shore/SeaweedMat.png",    3, 1, 32, 24, SpriteAlignment.Center, Centre),

            // ---- Deck fish tray: 5 cols (fill 0..4) × 3 rows (lobster/crab/mixed) → 15, bottom-centre --
            new SheetSpec(Root + "Sprites/Gear/FishTray.png",       5, 3, 32, 24, SpriteAlignment.BottomCenter, Bottom),

            // ---- Iso dory (player boat, wave-coupled rock) — custom waterline pivot on every slice ----
            //   DoryIso: 8 cols × 1 row → 8 static hull headings (N,NE,E,SE,S,SW,W,NW; index = heading).
            //   DoryIsoRock: 8 cols (rock frame 0..7) × 8 rows (heading) → 64; index = heading×8 + frame
            //     (row-major from top-left, per BuildRects), i.e. heading = index/8, frame = index%8.
            new SheetSpec(Root + "Boats/DoryIso.png",     8, 1, 160, 156, SpriteAlignment.Custom, DoryWaterline),
            new SheetSpec(Root + "Boats/DoryIsoRock.png", 8, 8, 160, 156, SpriteAlignment.Custom, DoryWaterline),
        };

        // ---- entry points -------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Slice Environment + VFX Sheets")]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int skipped, out int failed);
            Debug.Log($"[SpriteSheetSlicer] Sliced {n} sheet(s) ({skipped} skipped, {failed} failed).");
        }

        /// <summary>
        /// Batch entry point for <c>-executeMethod</c>. Refreshes so any freshly-copied PNGs import first,
        /// slices every manifest sheet, then exits non-zero if any failed so headless/CI bakes fail loudly
        /// instead of committing a half-sliced sheet.
        /// </summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int skipped, out int failed);
                Debug.Log($"[SpriteSheetSlicer] (batch) Sliced {n} sheet(s) " +
                          $"({skipped} skipped, {failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[SpriteSheetSlicer] {failed} sheet(s) failed to slice — see errors above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpriteSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Batch verifier for <c>-executeMethod</c>: loads every manifest sheet with
        /// <see cref="AssetDatabase.LoadAllAssetsAtPath"/> (Multiple-mode sheets return null from
        /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> — LoadAllAssets is the rule) and asserts the per-sheet
        /// slice count and pivot. Exits non-zero on any mismatch so a bad bake fails loudly.
        /// </summary>
        public static void VerifyAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                bool ok = VerifyAll(logEachPass: true);
                if (!ok)
                {
                    Debug.LogError("[SpriteSheetSlicer] VERIFY FAILED — see mismatches above.");
                    EditorApplication.Exit(1);
                }
                else
                {
                    Debug.Log("[SpriteSheetSlicer] VERIFY PASSED — all environment/VFX sheets sliced correctly.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpriteSheetSlicer] verify threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Assert every manifest sheet imports Multiple-mode with the expected sprite count and pivot.
        /// Returns true only if every sheet passes.
        /// </summary>
        public static bool VerifyAll(bool logEachPass)
        {
            bool allOk = true;
            int checkedCount = 0;
            foreach (var spec in Sheets)
            {
                if (!File.Exists(spec.AssetPath))
                {
                    Debug.LogError($"[SpriteSheetSlicer] VERIFY: '{spec.AssetPath}' missing on disk.");
                    allOk = false;
                    continue;
                }

                checkedCount++;

                var importer = AssetImporter.GetAtPath(spec.AssetPath) as TextureImporter;
                if (importer == null || importer.spriteImportMode != SpriteImportMode.Multiple)
                {
                    Debug.LogError($"[SpriteSheetSlicer] VERIFY: '{spec.AssetPath}' is not Multiple-mode.");
                    allOk = false;
                    continue;
                }

                var sprites = AssetDatabase.LoadAllAssetsAtPath(spec.AssetPath).OfType<Sprite>().ToArray();
                if (sprites.Length != spec.Count)
                {
                    Debug.LogError($"[SpriteSheetSlicer] VERIFY: '{spec.Stem}' has {sprites.Length} sprites, " +
                                   $"expected {spec.Count} ({spec.Cols}×{spec.Rows}).");
                    allOk = false;
                    continue;
                }

                bool pivotOk = true;
                float expX = spec.Pivot.x * spec.CellW;
                float expY = spec.Pivot.y * spec.CellH;
                foreach (var s in sprites)
                {
                    if (Mathf.Abs(s.pivot.x - expX) > 0.01f || Mathf.Abs(s.pivot.y - expY) > 0.01f)
                    {
                        Debug.LogError($"[SpriteSheetSlicer] VERIFY: '{s.name}' pivot {s.pivot} " +
                                       $"expected ({expX},{expY}).");
                        pivotOk = false;
                    }
                }
                if (!pivotOk) { allOk = false; continue; }

                if (logEachPass)
                    Debug.Log($"[SpriteSheetSlicer] VERIFY OK: {spec.Stem} = {sprites.Length} sprites " +
                              $"({spec.Cols}×{spec.Rows} of {spec.CellW}×{spec.CellH}, {spec.Alignment}).");
            }

            Debug.Log($"[SpriteSheetSlicer] VERIFY: checked {checkedCount} sheet(s) — " +
                      (allOk ? "ALL PASS" : "FAILURES PRESENT"));
            return allOk && checkedCount == Sheets.Length;
        }

        // ---- the work -----------------------------------------------------------------------------

        /// <summary>
        /// Slice every manifest sheet. Returns the number sliced; reports how many were skipped (not on
        /// disk yet) and how many failed (dimension mismatch / no importer).
        /// </summary>
        public static int SliceAll(out int skipped, out int failed)
        {
            skipped = 0;
            failed = 0;
            int sliced = 0;
            foreach (var spec in Sheets)
            {
                switch (SliceOne(spec))
                {
                    case SliceResult.Sliced:  sliced++;  break;
                    case SliceResult.Skipped: skipped++; break;
                    case SliceResult.Failed:  failed++;  break;
                }
            }
            AssetDatabase.SaveAssets();
            return sliced;
        }

        private enum SliceResult { Sliced, Skipped, Failed }

        private static SliceResult SliceOne(SheetSpec spec)
        {
            if (!File.Exists(spec.AssetPath))
            {
                Debug.LogWarning($"[SpriteSheetSlicer] '{spec.AssetPath}' not on disk yet — skipping.");
                return SliceResult.Skipped;
            }

            var importer = AssetImporter.GetAtPath(spec.AssetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[SpriteSheetSlicer] '{spec.AssetPath}' has no TextureImporter — skipping.");
                return SliceResult.Failed;
            }

            // Guard the sheet size: a re-export that drifted the grid must fail loudly, not slice garbage.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.AssetPath);
            if (tex == null)
            {
                Debug.LogError($"[SpriteSheetSlicer] '{spec.AssetPath}' failed to load as Texture2D — skipping.");
                return SliceResult.Failed;
            }
            if (tex.width != spec.SheetW || tex.height != spec.SheetH)
            {
                Debug.LogError(
                    $"[SpriteSheetSlicer] '{spec.AssetPath}' is {tex.width}×{tex.height} but the manifest " +
                    $"expects {spec.SheetW}×{spec.SheetH} ({spec.Cols}×{spec.Rows} of {spec.CellW}×{spec.CellH}). " +
                    "Not slicing — fix the export or the manifest entry.");
                return SliceResult.Failed;
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            SpriteRect[] rects = BuildRects(spec);
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

            Debug.Log($"[SpriteSheetSlicer] Sliced '{spec.Stem}' → {rects.Length} sprites " +
                      $"({spec.Cols}×{spec.Rows} of {spec.CellW}×{spec.CellH}, {spec.Alignment}).");
            return SliceResult.Sliced;
        }

        /// <summary>
        /// Build the grid of <see cref="SpriteRect"/>s, row-major from the TOP-LEFT cell. Unity's rects are
        /// bottom-origin, so the top row (r=0) maps to the highest Y = (Rows-1)*CellH.
        /// </summary>
        private static SpriteRect[] BuildRects(SheetSpec spec)
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
                        name = $"{spec.Stem}_{index}",
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
