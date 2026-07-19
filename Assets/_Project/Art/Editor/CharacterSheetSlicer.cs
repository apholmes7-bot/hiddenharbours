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
    /// Grid-slicer for the 8-direction ISO CHARACTER sheets under
    /// <c>Assets/_Project/Art/Characters/Iso/</c> (Fisher, Ginny, Skipper — idle/walk/run, plus the
    /// Fisher's rod hold/cast poses). Mirrors <see cref="FoliageSheetSlicer"/>:
    /// <see cref="ArtImportPipeline"/> stamps the pixel-art import lock (PPU 32, Point, Uncompressed,
    /// Clamp, alphaIsTransparency) on first import and this tool adds the Multiple-mode grid + the
    /// per-slice ground-contact pivot that the postprocessor deliberately does not do.
    ///
    /// <para><b>Sheet contract.</b> Always <b>8 rows = directions</b> (row 0 at the TOP of the canvas)
    /// and <b>N columns = animation frames</b>; cell (r,c) at source <c>sx = c*cellW, sy = r*cellH</c>
    /// from the top-left. The frame count is always <i>derived</i> from the texture width rather than
    /// hard-coded per file, so a re-export with a different frame count still slices correctly (and a
    /// width that is not a whole number of cells fails loudly).</para>
    ///
    /// <para><b>All twelve body sheets are now the SAME cell: 64 × 88.</b> They used to disagree —
    /// the rod poses (<c>Fisher_hold</c>, <c>Fisher_cast_short</c>, <c>Fisher_cast_long</c>) were
    /// 128 × 128, because the rod and the flying lure were baked INTO the body and needed the headroom.
    /// The art director has since split the rod out into its own overlay sheet, so the body sheets are
    /// uniformly the plain character cell. <see cref="CellOverrides"/> is deliberately kept — and is
    /// simply empty of body sheets today — because the incoming <c>Rod_*</c> overlay sheets need a
    /// bigger canvas again: the per-sheet capability is the point, not the entries.</para>
    ///
    /// <para><b>Pivot = ground contact, one rule for any cell size:
    /// <c>(cellW/2, cellH − 8)</c> in TOP-LEFT canvas coordinates — i.e. always 8 px above the cell
    /// bottom, on the centreline.</b> Unity normalizes pivots from the <b>BOTTOM-LEFT</b>, so the Unity
    /// pivot is <c>(0.5, 8/cellH)</c> = <c>(0.5, 8/88 ≈ 0.0909)</c> on every body sheet.
    /// ⚠️ Getting this inverted plants the character ~72 px into the ground;
    /// <c>CharacterIsoSheetSliceTests</c> asserts it in PIXELS, so the one rule still holds for any
    /// future cell size.</para>
    ///
    /// <para>✅ <b>THE COUNTER-CLOCKWISE BAKE IS FIXED AT SOURCE — these rows now run CLOCKWISE.</b>
    /// The rig used to rotate the model counter-clockwise while LABELLING the rows clockwise, so row
    /// <c>i</c> depicted heading <c>−45°·i</c> and the row called 'E' was really a fisher facing WEST —
    /// the same defect as the iso BOAT kits (PR #212). The art director corrected the rig itself
    /// (<c>th = −dir·45°</c>) and re-baked all twelve body sheets, so the true order is now the
    /// labelled one: <c>N · NE · E · SE · S · SW · W · NW</c>, row <c>i</c> depicts <c>+45°·i</c>.
    /// Measured, not believed: per-row face-skin centroids on the re-baked art put rows 1–3 on the
    /// screen RIGHT and rows 5–7 on the screen LEFT. Rows 0/4 (N/S) are their own mirrors and cannot
    /// discriminate — which is exactly why the original defect hid for so long.
    /// <c>CharacterVisualDef.FacingsAreCounterClockwise</c> is therefore <b>false</b> for these kits.</para>
    ///
    /// <para>⚠️ <b>The BOAT sheets were NOT re-baked and are still counter-clockwise</b> —
    /// <c>BoatVisualLibraryBuilder.IsoSheetsAreCounterClockwise</c> stays <c>true</c>. The flag is
    /// per-artwork DATA precisely so two art lineages can genuinely disagree; do not "unify" them.</para>
    ///
    /// <para>Slices are still named by <b>ROW INDEX</b> — <c>&lt;Stem&gt;_d&lt;row&gt;_f&lt;col&gt;</c> —
    /// and <b>never</b> by a compass name, even now that a compass label would finally be truthful. A
    /// slice name states GEOMETRY (which cell of the grid), not SEMANTICS (which way it looks), and that
    /// is precisely what let this re-bake land as a one-line data change rather than an asset-database
    /// migration. The heading→cell math lives in <c>HiddenHarbours.Core.IsoFacing</c> — this tool
    /// depends on neither it nor the flag.</para>
    ///
    /// <para>Import + slicing ONLY. This tool builds no presenter, no Def asset, no prefab, and touches
    /// nothing outside <see cref="IsoCharactersRoot"/>.</para>
    /// </summary>
    public static class CharacterSheetSlicer
    {
        /// <summary>The only folder this tool slices. We never touch textures outside it.</summary>
        public const string IsoCharactersRoot = "Assets/_Project/Art/Characters/Iso/";

        /// <summary>Default cell width in source pixels — the locomotion (idle/walk/run) sheets.</summary>
        public const int CellW = 64;

        /// <summary>Default cell height in source pixels — the locomotion (idle/walk/run) sheets.</summary>
        public const int CellH = 88;

        /// <summary>
        /// Ground contact sits this many pixels above the cell bottom, on <b>every</b> sheet regardless
        /// of cell size. This single constant is what keeps the 64 × 88 and 128 × 128 sheets planted on
        /// the same ground line.
        /// </summary>
        public const int GroundInsetPx = 8;

        /// <summary>
        /// Rows are directions, and there are always eight of them. (Which row is which compass heading
        /// is the counter-clockwise question documented on the class — not this tool's business.)
        /// </summary>
        public const int DirectionRows = 8;

        /// <summary>
        /// Per-sheet cell size, by file stem. Anything not listed uses the default
        /// <see cref="CellW"/> × <see cref="CellH"/> locomotion cell.
        ///
        /// <para>A declaration, not a guess: a sheet width is usually a whole number of several
        /// plausible cell widths, so the grid genuinely cannot be recovered from the pixels. Declaring
        /// it here — and validating it in <see cref="SliceOne"/> — is how a wrong grid becomes a loud
        /// failure instead of a few hundred plausible-looking wrong sprites.</para>
        ///
        /// <para><b>Empty today, and deliberately still here.</b> It used to carry the three 128 × 128
        /// rod poses; the art director split the rod out of the body, so all twelve BODY sheets are now
        /// the plain 64 × 88 cell. The incoming <c>Rod_*</c> overlay sheets are the bigger canvas again
        /// and will register here — deleting the mechanism would only mean rebuilding it next PR.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, Vector2Int> CellOverrides =
            new Dictionary<string, Vector2Int>
            {
                // (Rod overlay sheets land here when they import — see the note above.)
            };

        /// <summary>The authored cell size for a sheet stem.</summary>
        public static Vector2Int CellSizeFor(string stem) =>
            CellOverrides.TryGetValue(stem, out var cell) ? cell : new Vector2Int(CellW, CellH);

        /// <summary>
        /// Ground-contact pivot for a given cell size, normalized bottom-left:
        /// <c>(0.5, GroundInsetPx / cellH)</c>. One rule, both cell sizes.
        /// </summary>
        public static Vector2 GroundPivotFor(Vector2Int cell) =>
            new Vector2(0.5f, GroundInsetPx / (float)cell.y);

        // ---- entry points -------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Slice Iso Character Sheets")]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int skipped, out int failed);
            Debug.Log($"[CharacterSheetSlicer] Sliced {n} iso character sheet(s) " +
                      $"({skipped} skipped, {failed} failed).");
        }

        /// <summary>
        /// Batch entry point for <c>-executeMethod</c>. Refreshes first so freshly-copied PNGs import
        /// before we reach for their importers (a mid-build import invalidates in-memory refs), then
        /// exits non-zero if any sheet failed so a headless bake fails loudly instead of committing a
        /// half-sliced sheet.
        /// </summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int skipped, out int failed);
                Debug.Log($"[CharacterSheetSlicer] (batch) Sliced {n} iso character sheet(s) " +
                          $"({skipped} skipped, {failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[CharacterSheetSlicer] {failed} sheet(s) failed to slice — see errors above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CharacterSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---- the work -----------------------------------------------------------------------------

        /// <summary>
        /// Slice every PNG under <see cref="IsoCharactersRoot"/>. Returns the number sliced; reports how
        /// many were skipped and how many failed.
        /// </summary>
        public static int SliceAll(out int skipped, out int failed)
        {
            skipped = 0;
            failed = 0;

            if (!Directory.Exists(IsoCharactersRoot))
            {
                Debug.LogWarning($"[CharacterSheetSlicer] No folder at '{IsoCharactersRoot}' — nothing to slice.");
                return 0;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { IsoCharactersRoot.TrimEnd('/') });
            int sliced = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(IsoCharactersRoot, StringComparison.Ordinal)) continue;
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
            Vector2Int cell = CellSizeFor(stem);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[CharacterSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return SliceResult.Failed;
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[CharacterSheetSlicer] '{path}' failed to load as Texture2D — skipping.");
                return SliceResult.Failed;
            }

            // Derive the frame count from the ART, never from a per-file constant. A re-export with a
            // different frame count still slices; a width that isn't a whole number of cells is a broken
            // export (or a wrong CellOverrides entry) and must fail loudly rather than slice garbage.
            if (tex.width % cell.x != 0 || tex.width < cell.x)
            {
                Debug.LogError($"[CharacterSheetSlicer] '{path}' is {tex.width} px wide — not a whole " +
                               $"number of {cell.x} px cells. Not slicing.");
                return SliceResult.Failed;
            }
            if (tex.height != DirectionRows * cell.y)
            {
                Debug.LogError($"[CharacterSheetSlicer] '{path}' is {tex.height} px tall but an iso " +
                               $"character sheet must be {DirectionRows} direction rows × {cell.y} px = " +
                               $"{DirectionRows * cell.y}. Not slicing — fix the export (or the " +
                               $"CellOverrides entry for '{stem}').");
                return SliceResult.Failed;
            }

            int cols = tex.width / cell.x;
            Vector2 pivot = GroundPivotFor(cell);

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // ⚠️ Re-use the spriteID any already-sliced name carries. GUID.Generate() on every run made
            // a re-bake rewrite every spriteID in every .meta — churning all nine already-merged sheets
            // for no reason, and defeating the stable name→fileID mapping this tool sets below. Slicing
            // is idempotent now: re-running over unchanged art produces a byte-identical .meta.
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(stem, cols, cell, existingIds);
            dp.SetSpriteRects(rects);

            // Keep name→fileID stable across future reimports (mirrors the package's own slicer) so any
            // later reference to a slice survives a re-bake.
            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[CharacterSheetSlicer] Sliced '{stem}' → {rects.Length} sprites " +
                      $"({DirectionRows} direction rows × {cols} frames of {cell.x}×{cell.y}, " +
                      $"ground pivot {pivot}).");
            return SliceResult.Sliced;
        }

        /// <summary>
        /// Build the grid of <see cref="SpriteRect"/>s. Rows are directions, columns are frames.
        /// Unity's sprite rects are BOTTOM-origin while the sheet's row 0 is the TOP row, so
        /// <c>y = (DirectionRows-1-r) * cell.y</c>.
        ///
        /// <para>Names are <c>&lt;stem&gt;_d&lt;row&gt;_f&lt;col&gt;</c> — row INDEX, never a compass
        /// name. See the class remarks: the rows are baked counter-clockwise but the README labels them
        /// clockwise, so any compass name written here would be wrong for six of the eight rows.</para>
        ///
        /// <para><paramref name="existingIds"/> maps an already-sliced slice name to the spriteID it
        /// already carries; those are re-used so a re-bake of unchanged art is a no-op on the
        /// <c>.meta</c>. Only genuinely new names get a fresh GUID.</para>
        /// </summary>
        public static SpriteRect[] BuildRects(string stem, int cols, Vector2Int cell,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            Vector2 pivot = GroundPivotFor(cell);
            var rects = new SpriteRect[DirectionRows * cols];
            for (int r = 0; r < DirectionRows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = c * cell.x;
                    float y = (DirectionRows - 1 - r) * cell.y; // top row → top of the bottom-origin sheet
                    string name = $"{stem}_d{r}_f{c}";
                    rects[r * cols + c] = new SpriteRect
                    {
                        name = name,
                        spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                                   ? id
                                   : GUID.Generate(),
                        rect = new Rect(x, y, cell.x, cell.y),
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
