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
    /// Grid-slicer for the FISHING KIT sheets under <c>Assets/_Project/Art/Fishing/Iso/</c> —
    /// the fight-critical rigs of Rod Fishing v2 wave 3: the parametric fish
    /// (<c>Fish_&lt;species&gt;_&lt;state&gt;</c>), the rod bobber (<c>Bobber_&lt;state&gt;</c>)
    /// and the rod overlay (<c>Rod_&lt;tier&gt;_&lt;state&gt;</c>). Sibling of
    /// <see cref="CharacterSheetSlicer"/>, and a SEPARATE tool on purpose: that slicer's whole
    /// contract is "8 rows × one ground-contact pivot rule for every png under its root", and none
    /// of these three kits pivots on ground contact — the fish pivots on THE WATER-SURFACE POINT
    /// under its body centre, the bobber on THE WATERLINE, the rod on THE GRIP. Folding three
    /// water/grip pivots into the character tool would turn its one rule into a table of
    /// exceptions; a sibling keeps both tools honest.
    ///
    /// <para><b>Sheet contract</b> (what <c>FishingKitBaker</c> writes, sliced untouched): rows
    /// from the TOP are direction rows — 8 for fish and rod, <b>1 for the bobber</b>, which is a
    /// state sprite with no direction axis at all; N columns = the state's frames, always DERIVED
    /// from the texture width. Cell size and pivot are per KIT (by stem prefix), declared in
    /// <see cref="Kits"/> — a sheet width is a whole number of several plausible cell widths, so
    /// the grid genuinely cannot be recovered from the pixels, and a wrong entry must fail loudly
    /// (validated in <see cref="SliceOne"/>) instead of slicing plausible garbage.</para>
    ///
    /// <para><b>Pivots are the rigs' own, restated here as the contract under test</b> (the slicer
    /// runs without a script host; <c>FishingKitBakeTests</c> cross-checks these constants against
    /// the live rigs' <c>pivot</c> globals on every CI run, so drift is loud):
    /// fish (32,38), bobber (8,12), rod (56,72) — all TOP-LEFT cell px. Unity normalizes pivots
    /// from the BOTTOM-LEFT, so the stored pivot is <c>(x/W, (H−y)/H)</c>; getting that inversion
    /// wrong is silent and plants every sprite off its waterline/grip.</para>
    ///
    /// <para>Rows are emitted by the baker with each rig's MEASURED azimuth convention already
    /// applied, so row <c>d</c> genuinely depicts heading <c>45°·d</c> — but slices are still
    /// named by ROW INDEX (<c>&lt;stem&gt;_d&lt;row&gt;_f&lt;col&gt;</c>), never by compass name:
    /// a slice name states geometry, not semantics, which is what kept the character re-bake a
    /// data change. The bobber keeps the same scheme with a constant <c>d0</c>.</para>
    ///
    /// <para>Import + slicing ONLY. This tool builds no presenter, no Def asset, no prefab, and
    /// touches nothing outside <see cref="FishingIsoRoot"/>.</para>
    /// </summary>
    public static class FishingSheetSlicer
    {
        /// <summary>The only folder this tool slices. We never touch textures outside it.</summary>
        public const string FishingIsoRoot = "Assets/_Project/Art/Fishing/Iso/";

        /// <summary>One kit's slice geometry: cell size, direction-row count, and the rig's own
        /// pivot in TOP-LEFT cell px.</summary>
        public readonly struct KitSpec
        {
            public readonly Vector2Int Cell;
            public readonly int Rows;
            public readonly Vector2Int PivotTopLeftPx;

            public KitSpec(int cellW, int cellH, int rows, int pivotX, int pivotY)
            {
                Cell = new Vector2Int(cellW, cellH);
                Rows = rows;
                PivotTopLeftPx = new Vector2Int(pivotX, pivotY);
            }

            /// <summary>Normalized bottom-left pivot: <c>(x/W, (H−y)/H)</c>.</summary>
            public Vector2 NormalizedPivot =>
                new Vector2(PivotTopLeftPx.x / (float)Cell.x,
                            (Cell.y - PivotTopLeftPx.y) / (float)Cell.y);
        }

        /// <summary>
        /// The kit manifest, by stem prefix. A declaration, not a guess — see the class remarks.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, KitSpec> Kits =
            new Dictionary<string, KitSpec>(StringComparer.Ordinal)
            {
                // FishIso: 64×64, pivot (32,38) = the water-surface point under the body centre.
                ["Fish_"] = new KitSpec(64, 64, rows: 8, pivotX: 32, pivotY: 38),

                // RodBobber: 16×22, pivot (8,12) = the waterline point; ONE row — not directional.
                ["Bobber_"] = new KitSpec(16, 22, rows: 1, pivotX: 8, pivotY: 12),

                // RodIso: 112×112, pivot (56,72) = the grip centre, pinned to handR every pose.
                ["Rod_"] = new KitSpec(112, 112, rows: 8, pivotX: 56, pivotY: 72),
            };

        /// <summary>The kit a stem belongs to, or null for a stranger (which must fail, not
        /// guess).</summary>
        public static KitSpec? KitFor(string stem)
        {
            foreach (var kv in Kits)
                if (stem.StartsWith(kv.Key, StringComparison.Ordinal)) return kv.Value;
            return null;
        }

        // ---- entry points -------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Slice Fishing Kit Sheets")]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int skipped, out int failed);
            Debug.Log($"[FishingSheetSlicer] Sliced {n} fishing kit sheet(s) " +
                      $"({skipped} skipped, {failed} failed).");
        }

        /// <summary>
        /// Batch entry point for <c>-executeMethod</c>. Refreshes first so freshly-copied PNGs
        /// import before we reach for their importers, then exits non-zero if any sheet failed so
        /// a headless bake fails loudly instead of committing a half-sliced sheet.
        /// </summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int skipped, out int failed);
                Debug.Log($"[FishingSheetSlicer] (batch) Sliced {n} fishing kit sheet(s) " +
                          $"({skipped} skipped, {failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[FishingSheetSlicer] {failed} sheet(s) failed to slice — see errors above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[FishingSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---- the work -----------------------------------------------------------------------

        public static int SliceAll(out int skipped, out int failed)
        {
            skipped = 0;
            failed = 0;

            if (!Directory.Exists(FishingIsoRoot))
            {
                Debug.LogWarning($"[FishingSheetSlicer] No folder at '{FishingIsoRoot}' — nothing to slice.");
                return 0;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { FishingIsoRoot.TrimEnd('/') });
            int sliced = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(FishingIsoRoot, StringComparison.Ordinal)) continue;
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                switch (SliceOne(path))
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

        private static SliceResult SliceOne(string path)
        {
            string stem = Path.GetFileNameWithoutExtension(path);
            KitSpec? kitOrNull = KitFor(stem);
            if (kitOrNull == null)
            {
                Debug.LogError($"[FishingSheetSlicer] '{path}' matches no kit prefix " +
                               $"({string.Join(", ", Kits.Keys)}) — refusing to guess a grid.");
                return SliceResult.Failed;
            }

            return SliceSheet(path, kitOrNull.Value) ? SliceResult.Sliced : SliceResult.Failed;
        }

        /// <summary>
        /// Slice one sheet with a known kit spec — the shared engine behind this tool and its
        /// storage-kit sibling (<see cref="CatchStorageSheetSlicer"/>), extracted so the
        /// validation and idempotency rules exist exactly once. Returns false (after logging)
        /// on any refusal.
        /// </summary>
        public static bool SliceSheet(string path, KitSpec kit)
        {
            string stem = Path.GetFileNameWithoutExtension(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[FishingSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return false;
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[FishingSheetSlicer] '{path}' failed to load as Texture2D — skipping.");
                return false;
            }

            // Frame count derives from the ART; a width that isn't a whole number of cells is a
            // broken export (or a wrong kit entry) and must fail loudly, not slice garbage.
            if (tex.width % kit.Cell.x != 0 || tex.width < kit.Cell.x)
            {
                Debug.LogError($"[FishingSheetSlicer] '{path}' is {tex.width} px wide — not a whole " +
                               $"number of {kit.Cell.x} px cells. Not slicing.");
                return false;
            }
            if (tex.height != kit.Rows * kit.Cell.y)
            {
                Debug.LogError($"[FishingSheetSlicer] '{path}' is {tex.height} px tall but this kit's " +
                               $"sheets are {kit.Rows} row(s) × {kit.Cell.y} px = {kit.Rows * kit.Cell.y}. " +
                               "Not slicing — fix the export (or the kit entry).");
                return false;
            }

            int cols = tex.width / kit.Cell.x;

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // Re-use existing spriteIDs so a re-bake of unchanged art is a no-op on the .meta —
            // the same idempotency rule CharacterSheetSlicer carries (churned IDs defeat the
            // stable name→fileID mapping and rewrite every .meta for nothing).
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(stem, cols, kit, existingIds);
            dp.SetSpriteRects(rects);

            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[FishingSheetSlicer] Sliced '{stem}' → {rects.Length} sprites " +
                      $"({kit.Rows} row(s) × {cols} frames of {kit.Cell.x}×{kit.Cell.y}, " +
                      $"pivot {kit.NormalizedPivot}).");
            return true;
        }

        /// <summary>
        /// Build the grid of <see cref="SpriteRect"/>s. Rows are direction rows (top row first),
        /// columns are frames; Unity's sprite rects are BOTTOM-origin while the sheet's row 0 is
        /// the TOP row, so <c>y = (rows−1−r) · cellH</c>. Names are
        /// <c>&lt;stem&gt;_d&lt;row&gt;_f&lt;col&gt;</c> — row INDEX, never a compass name.
        /// </summary>
        public static SpriteRect[] BuildRects(string stem, int cols, in KitSpec kit,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            Vector2 pivot = kit.NormalizedPivot;
            var rects = new SpriteRect[kit.Rows * cols];
            for (int r = 0; r < kit.Rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    string name = $"{stem}_d{r}_f{c}";
                    rects[r * cols + c] = new SpriteRect
                    {
                        name = name,
                        spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                                   ? id
                                   : GUID.Generate(),
                        rect = new Rect(c * kit.Cell.x, (kit.Rows - 1 - r) * kit.Cell.y,
                                        kit.Cell.x, kit.Cell.y),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        border = Vector4.zero,
                    };
                }
            }
            return rects;
        }
    }
}
#endif
