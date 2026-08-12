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
    /// Grid-slicer for the navigation-buoy sheets. Mirrors <see cref="IsoPackSheetSlicer"/> — the
    /// pixel-art import lock (PPU 32, Point, Uncompressed, Clamp, alphaIsTransparency) is stamped by
    /// <c>ArtImportPipeline</c> on first import; this adds the Multiple-mode grid and the per-slice
    /// pivot that the postprocessor deliberately does not.
    ///
    /// <para><b>Every number is READ FROM THE COMMITTED CONTRACT</b> — the same oracle
    /// <c>NavBuoySheetBaker</c> asserts against, so a sheet and its grid cannot disagree unless the
    /// PNG on disk is stale, which is a dimension mismatch and fails loudly here rather than slicing
    /// garbage.</para>
    ///
    /// <para><b>⚠️ Why the contract is re-declared below instead of imported.</b> The asmdef edge runs
    /// <c>HiddenHarbours.Tools.RigBaking.Editor → HiddenHarbours.Art.Editor</c>, never back, so this
    /// assembly cannot see <c>NavBuoyContract</c>. Same reason <see cref="IsoPackSheetSlicer"/> keeps
    /// its own private mirror of the iso-pack contracts. The mirror is a READER of the committed
    /// JSON, not a second source of truth: every value still originates from the measurement pass,
    /// and a shape drift shows up as the dimension mismatch below.</para>
    ///
    /// <para><b>⚠️ THE PIVOT IS THE WATERLINE, NOT THE FOOT.</b> Every other kit this repo slices
    /// pivots on the ground under the object. A nav buoy floats: the contract's pivot is model
    /// <c>z = 0</c>, the still waterline, which is what the bob rides. Slice it as a ground pivot and
    /// every mark sits with its keel on the surface — visibly wrong, but plausibly enough to ship.</para>
    ///
    /// <para><b>Slice names are GEOMETRIC:</b> <c>&lt;Stem&gt;_&lt;index&gt;</c>, row-major from the
    /// top-left cell, exactly as every other sheet in the repo. A name states which CELL, never which
    /// way the mark looks — this lane has shipped mislabelled compass art five times. For this kit
    /// <c>index = facing</c> and the kit is <b>CLOCKWISE</b> (measured, −45°/dir on the ground plane),
    /// so cell <c>i</c> depicts heading <c>+45°·i</c>. Nothing in the sheet says so; read it from the
    /// contract's <c>convention</c>.</para>
    ///
    /// <para><b>⚠️ All 8 rects are always emitted, and on these sheets all 8 are real art.</b> Some
    /// marks ARE bodies of revolution and DO collapse at <c>fresh</c> — a plain can is byte-identical
    /// at cells 0/4, 1/5, 2/6, 3/7. But the kit bakes <c>working</c>, where rust takes whole facets
    /// and breaks that symmetry, so all fourteen marks have eight distinct pictures. Do not collapse
    /// rows here on the strength of the <c>fresh</c> figure.</para>
    /// </summary>
    public static class NavBuoySheetSlicer
    {
        /// <summary>⚠️ Must match <c>NavBuoyKit.SheetFolder</c> / <c>.ContractPath</c> /
        /// <c>.Facings</c>. Duplicated only because the asmdef edge forbids the import (see remarks);
        /// <c>NavBuoySlicerAgreesWithKitTests</c> pins the three against the kit so they cannot drift.</summary>
        public const string SheetFolder = "Assets/_Project/Art/Sprites/NavBuoys/Iso";
        public const string ContractPath = SheetFolder + "/navBuoyKit.contract.json";
        public const int Facings = 8;

        // ---- the contract, as JsonUtility sees it -------------------------------------------------

        [Serializable]
        public sealed class Cell
        {
            public string key, type, size;
            public int cellW, cellH, pivotX, pivotY;
            public int cols, rows, sheetW, sheetH;
        }

        [Serializable]
        private sealed class Contract
        {
            public int importSizeCap;
            public string convention;
            public List<Cell> cells = new List<Cell>();
        }

        // ---- entry points ---------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Slice Nav Buoy Sheets", priority = 212)]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int skipped, out int failed);
            Debug.Log($"[NavBuoySheetSlicer] Sliced {n} sheet(s), skipped {skipped}, failed {failed}.");
        }

        /// <summary>Batch-mode entry: non-zero exit on any failure, so a headless bake cannot report
        /// green over a sheet that did not slice.</summary>
        public static void SliceAllFromCommandLine()
        {
            int n = SliceAll(out int skipped, out int failed);
            Debug.Log($"[NavBuoySheetSlicer] Sliced {n}, skipped {skipped}, failed {failed}.");
            if (failed > 0) EditorApplication.Exit(1);
        }

        public static int SliceAll(out int skipped, out int failed)
        {
            skipped = 0; failed = 0;
            int sliced = 0;

            string abs = Path.Combine(RepoRoot, ContractPath);
            if (!File.Exists(abs))
            {
                Debug.LogError($"[NavBuoySheetSlicer] No contract at '{ContractPath}'. Run " +
                               "\"Hidden Harbours/Art/Measure Nav Buoy Kit (emit contract)\" first.");
                failed++;
                return 0;
            }

            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(abs));
            if (contract?.cells == null || contract.cells.Count == 0)
            {
                // JsonUtility returns a ZEROED object rather than throwing on a shape mismatch, so an
                // empty cell list is indistinguishable from a parse failure unless it is asserted.
                Debug.LogError($"[NavBuoySheetSlicer] '{ContractPath}' parsed to no cells.");
                failed++;
                return 0;
            }
            if (contract.importSizeCap <= 0)
            {
                Debug.LogError($"[NavBuoySheetSlicer] '{ContractPath}' has no importSizeCap. Reading a " +
                               "missing cap back as 0 would fail every sheet; refusing to guess.");
                failed++;
                return 0;
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var cell in contract.cells)
                {
                    string path = $"{SheetFolder}/NavBuoy_{cell.type}_{cell.size}.png";
                    switch (SliceOne(path, cell, contract.importSizeCap))
                    {
                        case SliceResult.Sliced: sliced++; break;
                        case SliceResult.Skipped: skipped++; break;
                        default: failed++; break;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            return sliced;
        }

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        private enum SliceResult { Sliced, Skipped, Failed }

        private static SliceResult SliceOne(string assetPath, Cell cell, int importSizeCap)
        {
            // A contract cell whose sheet was not in this bake's type list is NOT an error — the four
            // parked marks are measured into the contract precisely so they are one Build away.
            if (!File.Exists(Path.Combine(RepoRoot, assetPath))) return SliceResult.Skipped;

            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                Debug.LogError($"[NavBuoySheetSlicer] '{assetPath}' has no TextureImporter — skipping.");
                return SliceResult.Failed;
            }

            // ⚠️ Cap FIRST, before anything reads the texture. Unity's default maxTextureSize is 2048
            // and a sheet over it imports DOWNSCALED — silently: the rects are authored in source
            // pixels, the reimport refits them, and they come back alpha-trimmed with the pivot thrown
            // away. The sprite COUNT still looks right. This kit is packed so the lift never fires
            // (widest sheet 816 px), which is measured, not hoped.
            int needed = Mathf.NextPowerOfTwo(Mathf.Max(cell.sheetW, cell.sheetH));
            if (needed > importSizeCap)
            {
                Debug.LogError(
                    $"[NavBuoySheetSlicer] '{cell.key}' is {cell.sheetW}×{cell.sheetH} and needs a " +
                    $"{needed} px import, over the {importSizeCap} px cap. Re-pack into more rows " +
                    "rather than raising the cap. Not slicing.");
                return SliceResult.Failed;
            }
            if (importer.maxTextureSize < needed)
            {
                importer.maxTextureSize = needed;
                importer.SaveAndReimport();
            }

            // Load AFTER any reimport above — a mid-build import invalidates an earlier texture read.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                Debug.LogError($"[NavBuoySheetSlicer] '{assetPath}' failed to load as Texture2D.");
                return SliceResult.Failed;
            }
            if (tex.width != cell.sheetW || tex.height != cell.sheetH)
            {
                Debug.LogError(
                    $"[NavBuoySheetSlicer] '{assetPath}' is {tex.width}×{tex.height} but its contract " +
                    $"plans {cell.sheetW}×{cell.sheetH} ({cell.cols}×{cell.rows} of {cell.cellW}×" +
                    $"{cell.cellH}). Sheet and contract disagree — re-bake or re-emit. Not slicing.");
                return SliceResult.Failed;
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // ⚠️ Re-use the spriteID any already-sliced name carries. GUID.Generate() per run rewrites
            // every spriteID in every .meta — pure diff noise burying the owner's real changes. Same
            // fix as CharacterSheetSlicer (#218): re-slicing unchanged art yields a byte-identical meta.
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            string stem = Path.GetFileNameWithoutExtension(assetPath);
            Vector2 pivot = NormalisedPivot(cell.cellW, cell.cellH, cell.pivotX, cell.pivotY);
            SpriteRect[] rects = BuildRects(stem, cell, pivot, existingIds);

            dp.SetSpriteRects(rects);
            dp.GetDataProvider<ISpriteNameFileIdDataProvider>()
              ?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();
            return SliceResult.Sliced;
        }

        /// <summary>ADR 0026's conversion: a TOP-LEFT-origin contract pivot to Unity's
        /// BOTTOM-LEFT-normalised one. Inverted, every mark plants itself through the sea by twice
        /// its own height and nothing reports an error.</summary>
        public static Vector2 NormalisedPivot(int cellW, int cellH, int pivotX, int pivotY) =>
            new Vector2(pivotX / (float)cellW, (cellH - pivotY) / (float)cellH);

        /// <summary>Row-major from the TOP-LEFT cell. Unity's rects are BOTTOM-origin, so row
        /// <c>r</c> from the top sits at <c>y = sheetH − (r+1)·cellH</c>.</summary>
        public static SpriteRect[] BuildRects(string stem, Cell cell, Vector2 pivot,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            var rects = new SpriteRect[Facings];
            for (int i = 0; i < Facings; i++)
            {
                int col = i % cell.cols, rowFromTop = i / cell.cols;
                string name = $"{stem}_{i}";
                rects[i] = new SpriteRect
                {
                    name = name,
                    spriteID = existingIds != null && existingIds.TryGetValue(name, out GUID id)
                        ? id : GUID.Generate(),
                    rect = new Rect(col * cell.cellW,
                                    cell.sheetH - (rowFromTop + 1) * cell.cellH,
                                    cell.cellW, cell.cellH),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    border = Vector4.zero,
                };
            }
            return rects;
        }
    }
}
#endif
