#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Grid-slicer for the 8-direction ISO CHARACTER sheets under
    /// <c>Assets/_Project/Art/Characters/Iso/</c> (Fisher, Ginny, Skipper — idle/walk/run).
    /// Mirrors <see cref="FoliageSheetSlicer"/>: <see cref="ArtImportPipeline"/> stamps the pixel-art
    /// import lock (PPU 32, Point, Uncompressed, Clamp, alphaIsTransparency) on first import and this
    /// tool adds the Multiple-mode grid + the per-slice ground-contact pivot that the postprocessor
    /// deliberately does not do.
    ///
    /// <para><b>Sheet contract</b> (art director's README, re-verified against the pixels):
    /// cell <b>64 × 88</b>; <b>8 rows = directions</b> (row 0 at the TOP of the canvas), <b>N columns =
    /// animation frames</b>; cell (r,c) at source <c>sx = c*64, sy = r*88</c> from the top-left.
    /// Frame counts are <b>idle 6 · walk 8 · run 6</b>, but this tool <i>derives</i> the column count
    /// from the texture width rather than hard-coding it per file, so a re-export with a different
    /// frame count still slices correctly (and a width that is not a multiple of 64 fails loudly).</para>
    ///
    /// <para><b>Pivot = ground contact.</b> The README states <c>(32, 80)</c> in TOP-LEFT canvas
    /// coordinates. Unity normalizes pivots from the <b>BOTTOM-LEFT</b>, so the correct Unity pivot is
    /// <c>(32/64, (88-80)/88) = (0.5, 8/88 ≈ 0.0909)</c> — confirmed by measuring the feet bottom
    /// landing at y ≈ 80–83 from the top. ⚠️ Getting this inverted plants every character ~72 px
    /// (≈ 2.25 m at PPU 32) into the ground; <c>CharacterIsoSheetSliceTests</c> asserts it in pixels.</para>
    ///
    /// <para>⚠️⚠️ <b>THE DIRECTION ORDER IN THE README IS MISLABELLED — DO NOT "FIX" IT HERE.</b>
    /// The README claims the rows run <c>N NE E SE S SW W NW</c> (clockwise). They do not. The rig
    /// bakes <b>COUNTER-CLOCKWISE</b> and labels clockwise — exactly like every iso BOAT kit in this
    /// project (see the iso-art-baked-counter-clockwise finding behind PR #212). The <b>true</b> row
    /// order is <c>N · NW · W · SW · S · SE · E · NE</c>: row <c>i</c> depicts heading <c>−45°·i</c>.
    /// Proven two independent ways — (a) the rig's own projection math (<c>th = dir*PI/4</c> with a CCW
    /// rotation matrix, camera on the −y side, face decals at +y), and (b) face-skin pixel counts per
    /// row (row 0 = 0 face pixels → facing away = N; row 4 = 60 → facing the viewer = S; rows 1–3 face
    /// screen-LEFT and rows 5–7 face screen-RIGHT in near-perfect mirror pairs).</para>
    ///
    /// <para>Because of that, slices are named by <b>ROW INDEX</b> — <c>&lt;Stem&gt;_d&lt;row&gt;_f&lt;col&gt;</c>
    /// — and <b>never</b> by a compass name. Baking a compass label into a sprite name would hard-code
    /// the lie into the asset database, where the boat kits already taught us it is expensive to undo.
    /// A forthcoming <c>CharacterVisualDef</c> will carry a <c>FacingsAreCounterClockwise</c> flag
    /// (mirroring <c>BoatVisualDef</c>, PR #212) and the heading→cell math is being promoted to
    /// <c>HiddenHarbours.Core.IsoFacing</c> — neither exists yet; this tool depends on neither.</para>
    ///
    /// <para>Import + slicing ONLY. This tool builds no presenter, no Def asset, no prefab, and touches
    /// nothing outside <see cref="IsoCharactersRoot"/>.</para>
    /// </summary>
    public static class CharacterSheetSlicer
    {
        /// <summary>The only folder this tool slices. We never touch textures outside it.</summary>
        public const string IsoCharactersRoot = "Assets/_Project/Art/Characters/Iso/";

        /// <summary>Cell width in source pixels.</summary>
        public const int CellW = 64;

        /// <summary>Cell height in source pixels.</summary>
        public const int CellH = 88;

        /// <summary>
        /// Rows are directions, and there are always eight of them. (Which row is which compass heading
        /// is the counter-clockwise question documented on the class — not this tool's business.)
        /// </summary>
        public const int DirectionRows = 8;

        /// <summary>Ground-contact pivot, normalized bottom-left. README (32,80) top-left → (0.5, 8/88).</summary>
        public static readonly Vector2 GroundPivot = new Vector2(32f / CellW, (CellH - 80f) / CellH);

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
            // export and must fail loudly rather than slice garbage.
            if (tex.width % CellW != 0 || tex.width < CellW)
            {
                Debug.LogError($"[CharacterSheetSlicer] '{path}' is {tex.width} px wide — not a whole " +
                               $"number of {CellW} px cells. Not slicing.");
                return SliceResult.Failed;
            }
            if (tex.height != DirectionRows * CellH)
            {
                Debug.LogError($"[CharacterSheetSlicer] '{path}' is {tex.height} px tall but an iso " +
                               $"character sheet must be {DirectionRows} direction rows × {CellH} px = " +
                               $"{DirectionRows * CellH}. Not slicing — fix the export.");
                return SliceResult.Failed;
            }

            int cols = tex.width / CellW;

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            SpriteRect[] rects = BuildRects(stem, cols);
            dp.SetSpriteRects(rects);

            // Keep name→fileID stable across future reimports (mirrors the package's own slicer) so any
            // later reference to a slice survives a re-bake.
            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[CharacterSheetSlicer] Sliced '{stem}' → {rects.Length} sprites " +
                      $"({DirectionRows} direction rows × {cols} frames of {CellW}×{CellH}, " +
                      $"ground pivot {GroundPivot}).");
            return SliceResult.Sliced;
        }

        /// <summary>
        /// Build the grid of <see cref="SpriteRect"/>s. Rows are directions, columns are frames.
        /// Unity's sprite rects are BOTTOM-origin while the sheet's row 0 is the TOP row, so
        /// <c>y = (DirectionRows-1-r) * CellH</c>.
        ///
        /// <para>Names are <c>&lt;stem&gt;_d&lt;row&gt;_f&lt;col&gt;</c> — row INDEX, never a compass
        /// name. See the class remarks: the rows are baked counter-clockwise but the README labels them
        /// clockwise, so any compass name written here would be wrong for six of the eight rows.</para>
        /// </summary>
        public static SpriteRect[] BuildRects(string stem, int cols)
        {
            var rects = new SpriteRect[DirectionRows * cols];
            for (int r = 0; r < DirectionRows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = c * CellW;
                    float y = (DirectionRows - 1 - r) * CellH; // top row → top of the bottom-origin sheet
                    rects[r * cols + c] = new SpriteRect
                    {
                        name = $"{stem}_d{r}_f{c}",
                        spriteID = GUID.Generate(),
                        rect = new Rect(x, y, CellW, CellH),
                        alignment = SpriteAlignment.Custom,
                        pivot = GroundPivot,
                        border = Vector4.zero,
                    };
                }
            }
            return rects;
        }
    }
}
#endif
