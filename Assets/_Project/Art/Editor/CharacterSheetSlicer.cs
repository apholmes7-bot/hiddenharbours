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
    /// <c>Assets/_Project/Art/Characters/Iso/</c> (the player at the root, the cast one subfolder each,
    /// every anim the rig declares). Mirrors <see cref="FoliageSheetSlicer"/>:
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
    /// <para><b>Body sheets come in TWO cells, and each carries its own ground inset.</b> The ordinary
    /// locomotion cell is <see cref="DefaultCell"/> — 64 × 92, ground contact 10 px up — as
    /// of the pass-6 kit (it was 64 × 88 at pass 1). The rig-6.5 OFF-DECK anims (swim, tread, sleep,
    /// drive) ship at <see cref="OffDeckCell"/> — 64 × 88, ground contact <b>8</b> px up —
    /// which is that same 92-cell re-windowed 2 rows top and 2 bottom. Mixed cells are FINE and the
    /// inset moving with the cell is the whole point: both land the SAME ground-contact point, so a
    /// character that walks on the 92 sheet and treads water on the 88 one never hops.
    /// <see cref="CellFor"/> is the one place that decides, and it is the only thing that should.</para>
    ///
    /// <para>⚠️ <b>The inset is not a constant.</b> It was written as one — a single
    /// <c>GroundInsetPx</c> "on every sheet regardless of cell size" — which held only while
    /// exactly one cell existed at a time, and was silently wrong the moment a second arrived: crop two
    /// rows off the BOTTOM of a cell and ground contact has two fewer pixels beneath it. It is
    /// <c>H − pivotY</c> per cell (ADR 0026), and it lives on <see cref="CharacterCell"/> beside
    /// the size for that reason.</para>
    ///
    /// <para><see cref="CellOverrides"/> stays as the per-STEM escape hatch — empty of body sheets
    /// today — because the incoming <c>Rod_*</c> overlay sheets need a bigger canvas again: the
    /// per-sheet capability is the point, not the entries.</para>
    ///
    /// <para><b>This tool also slices the CAST, one subfolder per preset</b>
    /// (<c>Iso/ginny/Ginny_idle.png</c>, …). The folder scan is recursive and the grid rule is the
    /// same for all of them — a preset's body is a different SHAPE, never a different cell.</para>
    ///
    /// <para><b>Pivot = ground contact, one rule for any cell:
    /// <c>(Width/2, Height − GroundInsetPx)</c> in TOP-LEFT canvas coordinates — i.e. the
    /// cell's own inset above its own bottom, on the centreline.</b> Unity normalizes pivots from the
    /// <b>BOTTOM-LEFT</b>, so the Unity pivot is <c>(0.5, GroundInsetPx/Height)</c>:
    /// <c>(0.5, 10/92 ≈ 0.1087)</c> on a locomotion sheet and <c>(0.5, 8/88 ≈ 0.0909)</c> on an
    /// off-deck one. ⚠️ Getting this inverted plants the character ~72 px into the ground;
    /// <c>CharacterIsoSheetSliceTests</c> asserts it in PIXELS against each sheet's own cell, so the
    /// one rule holds for any future cell size.</para>
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

        /// <summary>Default cell width in source pixels — the character rig's own cell.</summary>
        public const int CellW = 64;

        /// <summary>
        /// Default cell height in source pixels. <b>92 as of the pass-6 kit (2026-08-02); it was 88.</b>
        /// </summary>
        public const int CellH = 92;

        /// <summary>
        /// Ground contact sits this many pixels above the cell bottom, on <b>every</b> sheet regardless
        /// of cell size. This single constant is what keeps sheets of different cell heights planted on
        /// the same ground line.
        ///
        /// <para>⚠️ <b>10 as of pass 6; it was 8. It is not a taste knob</b> — it is <c>H − pivotY</c>
        /// read off the rig, and the rig moved 88 − 80 = 8 to <b>92 − 82 = 10</b> when the cell grew.
        /// Left at 8 alongside the new cell height, every character would stand two pixels into the
        /// ground on sheets that slice, import and dimension-test as perfectly valid.
        /// <c>CharacterSlicerMatchesRigTests</c> cross-checks this pair against the LIVE rig's
        /// geometry, so it is measured rather than believed — the arrangement ADR 0026 asks for.</para>
        /// </summary>
        public const int GroundInsetPx = 10;

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
        /// the plain 64 × 92 cell. The incoming <c>Rod_*</c> overlay sheets are the bigger canvas again
        /// and will register here — deleting the mechanism would only mean rebuilding it next PR.</para>
        /// </summary>
        /// <summary>
        /// <b>One sheet family's cell: its size AND where ground contact sits inside it.</b> The two
        /// travel together because they are not independent — re-window a cell and the inset moves
        /// with it — and keeping them apart is precisely how a sheet slices, imports and
        /// dimension-tests as perfectly valid while planting the character off the ground.
        ///
        /// <para>⚠️ <b>The inset is NOT a constant across cell sizes</b>, which the old single
        /// <see cref="GroundInsetPx"/> quietly assumed. It is <c>H − pivotY</c> read off the rig
        /// (ADR 0026), so a cell cropped at the BOTTOM carries a smaller inset: the 6.5 off-deck cell is
        /// the 92-cell re-windowed 2 rows off the top and 2 off the bottom, and those two bottom rows
        /// are two pixels the ground point no longer has beneath it — 10 becomes 8.</para>
        /// </summary>
        public readonly struct CharacterCell
        {
            /// <summary>Cell width in source pixels.</summary>
            public readonly int Width;

            /// <summary>Cell height in source pixels.</summary>
            public readonly int Height;

            /// <summary>Ground contact this many px above the cell bottom — the rig's
            /// <c>H − pivotY</c>.</summary>
            public readonly int GroundInsetPx;

            public CharacterCell(int width, int height, int groundInsetPx)
            {
                Width = width;
                Height = height;
                GroundInsetPx = groundInsetPx;
            }

            /// <summary>The cell as a size, for the grid maths.</summary>
            public Vector2Int Size => new Vector2Int(Width, Height);

            /// <summary>
            /// Ground-contact pivot, normalized BOTTOM-LEFT the way Unity stores it:
            /// <c>(0.5, GroundInsetPx / Height)</c> — ADR 0026's <c>(H − pivotY)/H</c>.
            /// </summary>
            public Vector2 UnityPivot => new Vector2(0.5f, GroundInsetPx / (float)Height);
        }

        /// <summary>
        /// The rig's own locomotion cell — 64 × 92, ground contact 10 px up. Pinned to the LIVE
        /// rig by <c>CharacterSlicerMatchesRigTests</c>, so this pair is measured, not declared.
        /// </summary>
        public static readonly CharacterCell DefaultCell = new CharacterCell(CellW, CellH, GroundInsetPx);

        /// <summary>
        /// <b>The 6.5 OFF-DECK cell — 64 × 88, ground contact 8 px up.</b> The four anims a
        /// character plays with its feet off the ground (swim, tread, sleep, drive) ship at the rig's
        /// 92-cell re-windowed 2 rows top and 2 bottom; the sidecar <c>OffDeck_mounts.json</c> states
        /// the cell and the pivot in words.
        ///
        /// <para><b>The drop's bytes import VERBATIM.</b> Padding these back to 92 to "unify the cell"
        /// would mean editing generated art, and the mount sidecar's <c>waterRowLane</c> values are lane
        /// px in the <b>88</b> cell — they would all silently become wrong by two.</para>
        ///
        /// <para>⚠️ <b>A DECLARATION that no test can cross-check yet.</b> Every other cell
        /// here is pinned to the live rig, but the repo's <c>characterIsoRig6.js</c> is rev <b>6.0</b>
        /// (H = 92) while these sheets are rev 6.5 — the 6.5 source did not ship with the drop (it
        /// is an open owner ask). Until it lands, 88/8 rests on the sidecar plus a measurement recorded
        /// in the import PR: the sidecar's own <c>waterZ</c> (metres) and <c>waterRowLane</c> (px)
        /// over-determine the pivot row across 20 preset × anim pairs, and a least-squares fit puts
        /// it at <b>80.14</b> — i.e. inset 8 — with a 0.26 px rms residual against
        /// integer-rounded lane values. When the 6.5 rig lands, extend
        /// <c>CharacterSlicerMatchesRigTests</c> to pin this pair the same way.</para>
        /// </summary>
        public static readonly CharacterCell OffDeckCell = new CharacterCell(64, 88, 8);

        /// <summary>
        /// The anim suffixes baked at <see cref="OffDeckCell"/>. Matched by SUFFIX rather than listed
        /// per file because the family is a property of the ANIM, not of the character: all ten cast
        /// presets bake all four, and a preset added later inherits the rule with no edit here.
        /// </summary>
        public static readonly string[] OffDeckAnimSuffixes = { "_swim", "_tread", "_sleep", "_drive" };

        /// <summary>
        /// Per-sheet cell, by exact file stem — the escape hatch for a single sheet whose canvas
        /// matches neither family rule.
        ///
        /// <para>A declaration, not a guess: a sheet width is usually a whole number of several
        /// plausible cell widths, so the grid genuinely cannot be recovered from the pixels. Declaring
        /// it here — and validating it in <see cref="SliceOne"/> — is how a wrong grid becomes
        /// a loud failure instead of a few hundred plausible-looking wrong sprites.</para>
        ///
        /// <para><b>Empty today, and deliberately still here.</b> It used to carry the three
        /// 128 × 128 rod poses; the art director split the rod out of the body. The incoming
        /// <c>Rod_*</c> overlay sheets are the bigger canvas again and will register here —
        /// deleting the mechanism would only mean rebuilding it next PR.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, CharacterCell> CellOverrides =
            new Dictionary<string, CharacterCell>
            {
                // (Rod overlay sheets land here when they import — see the note above.)
            };

        /// <summary>
        /// The authored cell for a sheet stem: an exact override first, then the off-deck anim family,
        /// then the rig's ordinary locomotion cell.
        /// </summary>
        public static CharacterCell CellFor(string stem)
        {
            if (!string.IsNullOrEmpty(stem))
            {
                if (CellOverrides.TryGetValue(stem, out var exact)) return exact;

                foreach (string suffix in OffDeckAnimSuffixes)
                    if (stem.EndsWith(suffix, StringComparison.Ordinal))
                        return OffDeckCell;
            }

            return DefaultCell;
        }

        /// <summary>The authored cell SIZE for a sheet stem.</summary>
        public static Vector2Int CellSizeFor(string stem) => CellFor(stem).Size;

        /// <summary>
        /// Ground-contact pivot for a cell, normalized bottom-left. Takes the whole
        /// <see cref="CharacterCell"/> — never a bare size — because the inset is part of the
        /// cell and a size alone cannot answer the question.
        /// </summary>
        public static Vector2 GroundPivotFor(CharacterCell cell) => cell.UnityPivot;

        // ---- entry points -------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Slice Iso Character Sheets", priority = 203)]
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
            CharacterCell cellSpec = CellFor(stem);
            Vector2Int cell = cellSpec.Size;

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
            Vector2 pivot = GroundPivotFor(cellSpec);

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

            SpriteRect[] rects = BuildRects(stem, cols, cellSpec, existingIds);
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
        /// name. The pass-6 rows DO run clockwise (fixed at source — see the class remarks), but the
        /// names stay row-index anyway: a name that encodes a facing convention breaks the day the
        /// convention moves, and this lane has re-learned that twice. Row order is the consumer's
        /// business, read from the def, never from a sprite name.</para>
        ///
        /// <para><paramref name="existingIds"/> maps an already-sliced slice name to the spriteID it
        /// already carries; those are re-used so a re-bake of unchanged art is a no-op on the
        /// <c>.meta</c>. Only genuinely new names get a fresh GUID.</para>
        /// </summary>
        public static SpriteRect[] BuildRects(string stem, int cols, CharacterCell cellSpec,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            Vector2Int cell = cellSpec.Size;
            Vector2 pivot = GroundPivotFor(cellSpec);
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
