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
    /// <para><b>⚠️ The cell size is NOT one constant — it is per-sheet.</b> The locomotion sheets
    /// (idle/walk/run) are <b>64 × 88</b>. The rod poses (<c>Fisher_hold</c>, <c>Fisher_cast_short</c>,
    /// <c>Fisher_cast_long</c>) are <b>128 × 128</b> — the same character on a bigger canvas, padded
    /// +32 px each side and +40 px on top to give the rod arc and the flying lure headroom. The figure
    /// and the <b>ground line do not move</b>: measured across all 208 rod cells, the boots bottom out
    /// at y = 117–121 and the boot centre sits on the cell centreline, exactly as on the 64 × 88 sheets.
    /// Cell size cannot be derived from the pixels alone (both widths divide 1280 evenly), so it is
    /// declared per sheet in <see cref="CellOverrides"/> and validated hard against the art.</para>
    ///
    /// <para><b>Pivot = ground contact, and it is ONE rule for both cell sizes:
    /// <c>(cellW/2, cellH − 8)</c> in TOP-LEFT canvas coordinates — i.e. always 8 px above the cell
    /// bottom, on the centreline.</b> Unity normalizes pivots from the <b>BOTTOM-LEFT</b>, so the Unity
    /// pivot is <c>(0.5, 8/cellH)</c>: <c>(0.5, 8/88 ≈ 0.0909)</c> for the locomotion sheets and
    /// <c>(0.5, 8/128 = 0.0625)</c> for the rod sheets. ⚠️ Getting this inverted plants the character
    /// ~72 px (locomotion) or ~112 px (rod) into the ground; <c>CharacterIsoSheetSliceTests</c> asserts
    /// it in pixels for both.</para>
    ///
    /// <para>⚠️⚠️ <b>THE DIRECTION ORDER IN THE README IS MISLABELLED — DO NOT "FIX" IT HERE.</b>
    /// The README claims the rows run <c>N NE E SE S SW W NW</c> (clockwise). They do not. The rig
    /// bakes <b>COUNTER-CLOCKWISE</b> and labels clockwise — exactly like every iso BOAT kit in this
    /// project (see the iso-art-baked-counter-clockwise finding behind PR #212). The <b>true</b> row
    /// order is <c>N · NW · W · SW · S · SE · E · NE</c>: row <c>i</c> depicts heading <c>−45°·i</c>.
    /// Proven two independent ways — (a) the rig's own projection math (<c>th = dir*PI/4</c> with a CCW
    /// rotation matrix, camera on the −y side, face decals at +y), and (b) face-skin pixel counts per
    /// row (row 0 ≈ 0 face pixels → facing away = N; row 4 peaks → facing the viewer = S).
    /// The rod sheets confirm the same order: per-row face-skin counts are
    /// hold <c>[15,18,42,47,90,57,27,19]</c>, cast_long <c>[12,17,37,43,75,52,22,16]</c>,
    /// cast_short <c>[13,18,39,43,77,56,23,16]</c> — minimum at row 0, peak at row 4.
    /// ⚠️ Note the rows 2–3 vs 6–7 asymmetry: the rig gives the hold/cast poses a built-in
    /// <c>yaw:16</c>, so face-CENTROID tests are skewed on these three sheets. Use face-pixel
    /// <i>counts</i> for the N/S determination, not centroids.</para>
    ///
    /// <para>Because of that, slices are named by <b>ROW INDEX</b> — <c>&lt;Stem&gt;_d&lt;row&gt;_f&lt;col&gt;</c>
    /// — and <b>never</b> by a compass name. Baking a compass label into a sprite name would hard-code
    /// the lie into the asset database, where the boat kits already taught us it is expensive to undo.
    /// A forthcoming <c>CharacterVisualDef</c> will carry a <c>FacingsAreCounterClockwise</c> flag
    /// (mirroring <c>BoatVisualDef</c>, PR #212) and the heading→cell math lives in
    /// <c>HiddenHarbours.Core.IsoFacing</c> — this tool depends on neither.</para>
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
        /// <para>This is a declaration, not a guess: 1280 px is a whole number of both 64 px and 128 px
        /// cells, so the grid genuinely cannot be recovered from the pixels. Declaring it here — and
        /// validating it in <see cref="SliceOne"/> — is how a wrong grid becomes a loud failure instead
        /// of 160 plausible-looking wrong sprites.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, Vector2Int> CellOverrides =
            new Dictionary<string, Vector2Int>
            {
                // Rod poses: same figure, +32 px each side and +40 px on top for the rod arc / lure.
                { "Fisher_hold",       new Vector2Int(128, 128) },
                { "Fisher_cast_short", new Vector2Int(128, 128) },
                { "Fisher_cast_long",  new Vector2Int(128, 128) },
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

            SpriteRect[] rects = BuildRects(stem, cols, cell);
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
        /// </summary>
        public static SpriteRect[] BuildRects(string stem, int cols, Vector2Int cell)
        {
            Vector2 pivot = GroundPivotFor(cell);
            var rects = new SpriteRect[DirectionRows * cols];
            for (int r = 0; r < DirectionRows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = c * cell.x;
                    float y = (DirectionRows - 1 - r) * cell.y; // top row → top of the bottom-origin sheet
                    rects[r * cols + c] = new SpriteRect
                    {
                        name = $"{stem}_d{r}_f{c}",
                        spriteID = GUID.Generate(),
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
