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
    /// Slices and IMPORT-LOCKS the <b>Rock Px</b> kit under <see cref="RockPxCatalog.KitRoot"/> —
    /// 225 sheets × four co-registered channels in <c>tex/</c>, and 28 seat decals × two channels in
    /// <c>seat/</c>.
    ///
    /// <para>Every number comes from the kit's own JSON (<see cref="RockPxCatalog.Load"/>,
    /// <see cref="RockPxCatalog.LoadSidecar"/>, <see cref="RockPxCatalog.LoadSeats"/>) — cell,
    /// columns, tide rows and each variant's bottom-centre ground-contact pivot. Nothing about the
    /// geometry is hardcoded here.</para>
    ///
    /// <para><b>⚠ WHY THIS KIT CANNOT BE A <see cref="SpriteSheetSlicer"/> MANIFEST ROW</b>, and why
    /// it is a second slicer rather than a branch inside <see cref="RockSheetSlicer"/>: the manifest
    /// writes ONE pivot per sheet, and a rock sheet's pivot is per variant COLUMN. Pass two's slicer
    /// reads pass two's contract — different schema, different axes (its rows are DRESS, these are
    /// TIDE), different channel set — so it is followed, not forked into. This file is the same
    /// shape as <see cref="RockSheetSlicer"/> and <see cref="TreeSheetSlicer"/>: menu + batch entry,
    /// <c>SliceAll</c> → <c>SliceOne</c>, a channel-aware <c>ApplyImportSettings</c>, a
    /// <c>BuildRects</c> that re-uses existing spriteIDs, and a <c>VerifyAll</c>.</para>
    ///
    /// <para><b>The pivot is the GROUND CONTACT, not the bottom of the cell</b> — the near flank is
    /// drawn BELOW the contact point (5–18 px across this drop), so both
    /// <c>ArtImportPipeline.PivotFor</c>'s Center default and the tempting BottomCenter are wrong
    /// here. It is read from the sidecar's <c>anchors.footprint.ground</c> and normalised per ADR
    /// 0026's <c>(x/W, (H − y)/H)</c>. ⚠ The last four columns are MIRRORS and take the mirrored
    /// pivot (<see cref="RockPxCatalog.PivotPxForColumn"/>).</para>
    ///
    /// <para><b>Two of the four channels are DATA.</b> <c>_mask</c>, <c>_normal</c> and the decals'
    /// <c>_blend</c> import linear with <c>alphaIsTransparency</c> off, exactly as
    /// <see cref="TreeSheetSlicer.ApplyImportSettings"/> does it — sRGB-decoding a number channel
    /// bends every value on the way in, and alpha-transparency bleeds its RGB into the margin.</para>
    ///
    /// <para><b>This slicer wires nothing.</b> No prefab, no placement, no shader: it makes the kit
    /// import correctly and stops there.</para>
    /// </summary>
    public static class RockPxSheetSlicer
    {
        public static string KitRoot => RockPxCatalog.KitRoot;

        // ---- entry points ---------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Dev/Slice Rock Px Kit Sheets", priority = 143)]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int failed);
            Debug.Log($"[RockPxSheetSlicer] Sliced {n} Rock Px asset(s) ({failed} failed).");
        }

        /// <summary>Batch entry point for <c>-executeMethod</c> — exits non-zero on any failure so a
        /// headless import fails loudly instead of committing a half-sliced kit.</summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int failed);
                Debug.Log($"[RockPxSheetSlicer] (batch) Sliced {n} asset(s) ({failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[RockPxSheetSlicer] {failed} asset(s) failed — see errors above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[RockPxSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Hidden Harbours/Dev/Verify Rock Px Kit Slices", priority = 157)]
        public static void VerifyAllMenu()
        {
            VerifyAll(false, out int n);
            Debug.Log($"[RockPxSheetSlicer] Verified {n} Rock Px asset(s) on disk.");
        }

        // ---- the work -------------------------------------------------------------------------

        /// <summary>
        /// Slice + import-lock every Rock Px asset on disk. A PNG the contract does not claim is left
        /// alone and REPORTED, never guessed at — a sheet with no contract has no cell and no pivots,
        /// and guessing either is how a rock ends up floating.
        /// </summary>
        public static int SliceAll(out int failed)
        {
            failed = 0;

            if (!Directory.Exists(KitRoot))
            {
                Debug.LogError($"[RockPxSheetSlicer] No folder at '{KitRoot}' — the kit is missing.");
                failed = 1;
                return 0;
            }

            RockPxCatalog.Contract contract = RockPxCatalog.Load();
            if (contract == null) { failed = 1; return 0; }

            Dictionary<string, RockPxCatalog.Sidecar> sidecars = RockPxCatalog.LoadAllSidecars();
            if (sidecars == null) { failed = 1; return 0; }

            RockPxCatalog.SeatSet seats = RockPxCatalog.LoadSeats();
            if (seats == null) { failed = 1; return 0; }

            // stem (channel suffix stripped) → the sheet it names. Built from the CONTRACT's own
            // (form × its stones × dress) product, so a stray PNG cannot masquerade as a sheet by
            // being named like one.
            var byStem = new Dictionary<string, SheetKey>(StringComparer.Ordinal);
            foreach (RockPxCatalog.FormEntry form in contract.Forms)
            foreach (string stone in form.Stones)
            foreach (string dress in RockPxCatalog.Dress)
                byStem[RockPxCatalog.StemFor(form.Key, stone, dress)] = new SheetKey(form, stone);

            var decalByStem = new Dictionary<string, RockPxCatalog.SeatDecal>(StringComparer.Ordinal);
            foreach (RockPxCatalog.SeatDecal d in seats.Decals)
                if (!string.IsNullOrEmpty(d.Stem)) decalByStem[d.Stem] = d;

            int sliced = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D",
                                                            new[] { KitRoot.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(KitRoot, StringComparison.Ordinal)) continue;
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                string fileStem = Path.GetFileNameWithoutExtension(path);
                RockPxCatalog.Channel channel = RockPxCatalog.ChannelOf(fileStem);
                string baseStem = StripSuffix(fileStem, RockPxCatalog.SuffixFor(channel));

                if (path.StartsWith(RockPxCatalog.SeatDir, StringComparison.Ordinal))
                {
                    if (!decalByStem.TryGetValue(baseStem, out RockPxCatalog.SeatDecal decal))
                    {
                        Debug.LogError(
                            $"[RockPxSheetSlicer] '{fileStem}' is under {RockPxCatalog.SeatDir} but " +
                            $"no decal in {RockPxCatalog.SeatsFileName} claims it. Not slicing.");
                        failed++;
                        continue;
                    }

                    if (SliceDecal(path, fileStem, decal, channel)) sliced++;
                    else failed++;
                    continue;
                }

                if (!byStem.TryGetValue(baseStem, out SheetKey key))
                {
                    Debug.LogError(
                        $"[RockPxSheetSlicer] '{fileStem}' is under {RockPxCatalog.TexDir} but no " +
                        $"(form × stone × dress) in {RockPxCatalog.ContractFileName} claims it. Not " +
                        "slicing — a sheet with no contract has no cell and no pivots.");
                    failed++;
                    continue;
                }

                List<RockPxCatalog.VariantEntry> variants =
                    sidecars[key.Form.Key].For(key.Stone);

                if (variants == null || variants.Count != RockPxCatalog.VariantCols)
                {
                    Debug.LogError(
                        $"[RockPxSheetSlicer] '{fileStem}': {RockPxCatalog.SidecarPath(key.Form.Key)} " +
                        $"has {(variants == null ? "NO" : variants.Count.ToString())} variant(s) for " +
                        $"stone '{key.Stone}', not {RockPxCatalog.VariantCols}. Not slicing — the " +
                        "pivot is the sidecar's ground contact and there is nothing here to read it " +
                        "from. This is an UPSTREAM defect: re-export the sidecar, do not hand-write " +
                        "anchors.");
                    failed++;
                    continue;
                }

                if (SliceOne(path, fileStem, key.Form, variants, channel)) sliced++;
                else failed++;
            }

            AssetDatabase.SaveAssets();
            return sliced;
        }

        /// <summary>One (form, stone) sheet identity — the pair the sidecar is keyed by.</summary>
        public readonly struct SheetKey
        {
            public readonly RockPxCatalog.FormEntry Form;
            public readonly string Stone;
            public SheetKey(RockPxCatalog.FormEntry form, string stone) { Form = form; Stone = stone; }
        }

        /// <summary>Slice + import-lock ONE sheet against its form entry and that stone's
        /// variants.</summary>
        public static bool SliceOne(string path, string stem, RockPxCatalog.FormEntry form,
                                    List<RockPxCatalog.VariantEntry> variants,
                                    RockPxCatalog.Channel channel)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[RockPxSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return false;
            }

            ApplyImportSettings(importer, channel);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[RockPxSheetSlicer] '{path}' failed to load as Texture2D — skipping.");
                return false;
            }

            if (tex.width != form.SheetW || tex.height != form.SheetH)
            {
                Debug.LogError(
                    $"[RockPxSheetSlicer] '{stem}' is {tex.width}×{tex.height} but the contract says " +
                    $"{form.SheetW}×{form.SheetH} ({RockPxCatalog.SheetCols} col(s) × " +
                    $"{RockPxCatalog.SheetRows} tide row(s) of {form.CellW}×{form.CellH}). Not " +
                    $"slicing — re-bake, do not edit {RockPxCatalog.ContractFileName}.");
                return false;
            }

            if (tex.width > RockPxCatalog.ImportSizeCap || tex.height > RockPxCatalog.ImportSizeCap)
            {
                Debug.LogError(
                    $"[RockPxSheetSlicer] '{stem}' is {tex.width}×{tex.height}, over the " +
                    $"{RockPxCatalog.ImportSizeCap} px import cap — Unity has already DOWNSCALED it " +
                    "and the sprite count would still come out right. Not slicing.");
                return false;
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // Re-use existing spriteIDs so a re-slice of unchanged art is a no-op on the .meta.
            // Fresh GUIDs on every run rewrite every spriteID — pure diff noise, and across 956
            // files it would bury anything real (the lesson of the 43 tree metas).
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(stem, form, variants, existingIds);
            dp.SetSpriteRects(rects);

            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[RockPxSheetSlicer] Sliced '{stem}' → {rects.Length} sprite(s) " +
                      $"({RockPxCatalog.SheetCols} col(s) × {RockPxCatalog.SheetRows} tide row(s) of " +
                      $"{form.CellW}×{form.CellH}), per-variant ground-contact pivots, " +
                      $"{channel} channel.");
            return true;
        }

        /// <summary>
        /// A seat decal is a SINGLE sprite, not a grid — one skirt, drawn over the rock on the rock's
        /// own pivot. It still needs the custom pivot, because a decal aligned Center would sit off
        /// the contact point by exactly the overhang the rock is pivoted to cancel.
        /// </summary>
        public static bool SliceDecal(string path, string stem, RockPxCatalog.SeatDecal decal,
                                      RockPxCatalog.Channel channel)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[RockPxSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return false;
            }

            ApplyImportSettings(importer, channel);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[RockPxSheetSlicer] '{path}' failed to load as Texture2D — skipping.");
                return false;
            }

            if (tex.width != decal.CellW || tex.height != decal.CellH)
            {
                Debug.LogError(
                    $"[RockPxSheetSlicer] decal '{stem}' is {tex.width}×{tex.height} but " +
                    $"{RockPxCatalog.SeatsFileName} says {decal.CellW}×{decal.CellH}. Not slicing — a " +
                    "decal that is not its rock's cell cannot share its pivot.");
                return false;
            }

            importer.spriteImportMode = SpriteImportMode.Single;

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            importer.SetTextureSettings(s);
            importer.spritePivot = RockPxCatalog.NormalizedPivot(decal);

            importer.SaveAndReimport();

            Debug.Log($"[RockPxSheetSlicer] Sliced decal '{stem}' → 1 sprite of " +
                      $"{decal.CellW}×{decal.CellH}, pivot {importer.spritePivot}, " +
                      $"{channel} channel.");
            return true;
        }

        /// <summary>
        /// The kit's import lock. Starts from <see cref="ArtImportPipeline.ApplyLockedSettings"/>
        /// (PPU 32, Point, uncompressed, mips off — the canon in one place) and then says the two
        /// things this kit knows that the canon cannot: the size cap, and that half its channels are
        /// NUMBERS.
        ///
        /// <para><b>⚠ <c>ApplyLockedSettings</c> also runs on FIRST IMPORT via
        /// <see cref="ArtImportPipeline"/>, before this menu is ever touched</b> — that is what keeps
        /// a fresh clone (which has no <c>.meta</c> for a brand-new PNG) from defaulting to
        /// mip-mapped compressed sheets. It is taught the same data-channel rule so the two paths
        /// cannot disagree; this method then repeats it rather than assuming, because a menu run must
        /// be correct on a meta that was written by something else.</para>
        /// </summary>
        public static void ApplyImportSettings(TextureImporter importer,
                                               RockPxCatalog.Channel channel)
        {
            ArtImportPipeline.ApplyLockedSettings(importer, importer.assetPath);

            importer.maxTextureSize = RockPxCatalog.ImportSizeCap;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);

            bool colour = RockPxCatalog.IsColourChannel(channel);
            s.sRGBTexture = colour;          // data channels are NUMBERS, not colour
            s.alphaIsTransparency = colour;
            s.alphaSource = TextureImporterAlphaSource.FromInput;

            // ⚠ FullRect, not Tight. A tight mesh trims to the alpha bbox per rect, and mask/normal
            // are then trimmed to THEIR bbox — the four channels stop being co-registered, which is
            // the one property the whole kit rests on.
            s.spriteMeshType = SpriteMeshType.FullRect;

            // Leave the sheet-level default honest rather than Center, which is what
            // ArtImportPipeline picks and is wrong for every variant in this kit.
            s.spriteAlignment = (int)SpriteAlignment.Custom;

            importer.SetTextureSettings(s);
        }

        /// <summary>
        /// The grid: 8 COLS × 3 tide ROWS, row-major from the TOP-LEFT (Unity's rects are
        /// bottom-origin, so row 0 maps to the highest Y — the convention every slicer here uses).
        ///
        /// <para>Names state GEOMETRY — <c>&lt;stem&gt;_c&lt;col&gt;_r&lt;row&gt;</c> is "column 5,
        /// row 2" and nothing else. ⚠ It is deliberately NOT pass two's <c>_v&lt;col&gt;_d&lt;row&gt;</c>:
        /// here <c>v</c> would lie (cols 4–7 are MIRRORS of 0–3, not four more variants) and
        /// <c>d</c> would lie twice over (rows are TIDE; dress is a different sheet). Which variant
        /// a column draws is <see cref="RockPxCatalog.VariantForColumn"/> and which tide a row is
        /// lives in <see cref="RockPxCatalog.Tides"/> — a name that claims a meaning is a name that
        /// can lie.</para>
        /// </summary>
        public static SpriteRect[] BuildRects(string stem, RockPxCatalog.FormEntry form,
                                              List<RockPxCatalog.VariantEntry> variants,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            var rects = new SpriteRect[RockPxCatalog.SheetRows * RockPxCatalog.SheetCols];
            for (int r = 0; r < RockPxCatalog.SheetRows; r++)
            {
                for (int c = 0; c < RockPxCatalog.SheetCols; c++)
                {
                    // ⚠ This COLUMN's ground contact — the variant it draws, mirrored for cols 4–7.
                    RockPxCatalog.VariantEntry variant =
                        variants[RockPxCatalog.VariantForColumn(c)];
                    Vector2 pivot =
                        RockPxCatalog.NormalizedPivot(variant, form.CellW, form.CellH, c);

                    string name = $"{stem}_c{c}_r{r}";
                    rects[r * RockPxCatalog.SheetCols + c] = new SpriteRect
                    {
                        name = name,
                        spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                                   ? id
                                   : GUID.Generate(),
                        rect = new Rect(c * form.CellW,
                                        (RockPxCatalog.SheetRows - 1 - r) * form.CellH,
                                        form.CellW, form.CellH),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        border = Vector4.zero,
                    };
                }
            }
            return rects;
        }

        // ---- verification -----------------------------------------------------------------------

        /// <summary>
        /// Assert every Rock Px asset ON DISK imported the way this kit needs. Unlike pass two,
        /// a MISSING file IS a failure — this kit ships its pixels, so an absent sheet means the
        /// intake lost one. <paramref name="checkedCount"/> reports how many were examined.
        /// </summary>
        public static bool VerifyAll(bool logEachPass, out int checkedCount)
        {
            checkedCount = 0;

            RockPxCatalog.Contract contract = RockPxCatalog.Load();
            if (contract == null) return false;

            Dictionary<string, RockPxCatalog.Sidecar> sidecars = RockPxCatalog.LoadAllSidecars();
            if (sidecars == null) return false;

            RockPxCatalog.SeatSet seats = RockPxCatalog.LoadSeats();
            if (seats == null) return false;

            bool allOk = true;

            foreach (RockPxCatalog.FormEntry form in contract.Forms)
            foreach (string stone in form.Stones)
            foreach (string dress in RockPxCatalog.Dress)
            foreach (RockPxCatalog.Channel channel in RockPxCatalog.SheetChannels)
            {
                string path = RockPxCatalog.SheetPath(form.Key, stone, dress, channel);
                string stem = Path.GetFileNameWithoutExtension(path);

                if (!File.Exists(path))
                {
                    Debug.LogError($"[RockPxSheetSlicer] VERIFY: '{stem}' is missing from disk.");
                    allOk = false;
                    continue;
                }

                checkedCount++;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[RockPxSheetSlicer] VERIFY: '{stem}' has no TextureImporter.");
                    allOk = false;
                    continue;
                }

                bool ok = CheckCommon(stem, importer, channel);
                ok &= Check(stem, "Multiple-mode",
                            importer.spriteImportMode == SpriteImportMode.Multiple);

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                int expected = RockPxCatalog.SheetCols * RockPxCatalog.SheetRows;
                ok &= Check(stem, $"{expected} sprite(s), got {sprites.Length}",
                            sprites.Length == expected);

                List<RockPxCatalog.VariantEntry> variants = sidecars[form.Key].For(stone);
                if (variants == null || variants.Count != RockPxCatalog.VariantCols)
                {
                    Debug.LogError(
                        $"[RockPxSheetSlicer] VERIFY: '{stem}' has no 4-variant sidecar block for " +
                        $"stone '{stone}' — pivots cannot be checked, or written.");
                    allOk = false;
                    continue;
                }

                foreach (Sprite sprite in sprites)
                {
                    int c = ColumnOf(sprite.name, stem);
                    if (c < 0) continue;
                    RockPxCatalog.VariantEntry v = variants[RockPxCatalog.VariantForColumn(c)];
                    Vector2Int p = RockPxCatalog.PivotPxForColumn(v, form.CellW, c);
                    var want = new Vector2(p.x, form.CellH - p.y);
                    ok &= Check(sprite.name,
                                $"ground-contact pivot {want}, got {sprite.pivot}",
                                Mathf.Abs(sprite.pivot.x - want.x) < 0.01f &&
                                Mathf.Abs(sprite.pivot.y - want.y) < 0.01f);
                }

                if (!ok) allOk = false;
                else if (logEachPass)
                    Debug.Log($"[RockPxSheetSlicer] VERIFY OK: {stem} = {sprites.Length} sprite(s) " +
                              $"of {form.CellW}×{form.CellH}, per-variant ground pivots.");
            }

            foreach (RockPxCatalog.SeatDecal decal in seats.Decals)
            foreach (RockPxCatalog.Channel channel in RockPxCatalog.SeatChannels)
            {
                string path = decal.Path(channel);
                string stem = Path.GetFileNameWithoutExtension(path);

                if (!File.Exists(path))
                {
                    Debug.LogError($"[RockPxSheetSlicer] VERIFY: decal '{stem}' is missing from disk.");
                    allOk = false;
                    continue;
                }

                checkedCount++;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[RockPxSheetSlicer] VERIFY: decal '{stem}' has no TextureImporter.");
                    allOk = false;
                    continue;
                }

                bool ok = CheckCommon(stem, importer, channel);
                ok &= Check(stem, "Single-mode",
                            importer.spriteImportMode == SpriteImportMode.Single);

                Vector2 want = RockPxCatalog.NormalizedPivot(decal);
                ok &= Check(stem, $"normalised pivot {want}, got {importer.spritePivot}",
                            Mathf.Abs(importer.spritePivot.x - want.x) < 0.001f &&
                            Mathf.Abs(importer.spritePivot.y - want.y) < 0.001f);

                if (!ok) allOk = false;
                else if (logEachPass)
                    Debug.Log($"[RockPxSheetSlicer] VERIFY OK: decal {stem}, pivot {want}.");
            }

            Debug.Log($"[RockPxSheetSlicer] VERIFY: checked {checkedCount} asset(s) on disk — " +
                      (allOk ? "ALL PASS" : "FAILURES PRESENT"));
            return allOk;
        }

        static bool CheckCommon(string stem, TextureImporter importer,
                                RockPxCatalog.Channel channel)
        {
            bool colour = RockPxCatalog.IsColourChannel(channel);

            bool ok = Check(stem, "Compression None",
                            importer.textureCompression == TextureImporterCompression.Uncompressed);
            ok &= Check(stem, "crunch off", !importer.crunchedCompression);
            ok &= Check(stem, $"PPU {ArtImportPipeline.PixelsPerUnit}",
                        Mathf.Approximately(importer.spritePixelsPerUnit,
                                            ArtImportPipeline.PixelsPerUnit));
            ok &= Check(stem, "Point filter", importer.filterMode == FilterMode.Point);
            ok &= Check(stem, "mips off", !importer.mipmapEnabled);
            ok &= Check(stem, $"maxTextureSize {RockPxCatalog.ImportSizeCap}",
                        importer.maxTextureSize == RockPxCatalog.ImportSizeCap);

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            ok &= Check(stem, $"sRGB {colour} ({channel} channel)", s.sRGBTexture == colour);
            ok &= Check(stem, $"alphaIsTransparency {colour} ({channel} channel)",
                        s.alphaIsTransparency == colour);
            ok &= Check(stem, "FullRect mesh (channels stay co-registered)",
                        s.spriteMeshType == SpriteMeshType.FullRect);
            return ok;
        }

        /// <summary>The sheet column a slice name encodes, or -1. Names are
        /// <c>&lt;stem&gt;_c&lt;col&gt;_r&lt;row&gt;</c>.</summary>
        public static int ColumnOf(string spriteName, string stem)
        {
            string prefix = stem + "_c";
            if (spriteName == null || !spriteName.StartsWith(prefix, StringComparison.Ordinal))
                return -1;
            int r = spriteName.IndexOf("_r", prefix.Length, StringComparison.Ordinal);
            if (r < 0) return -1;
            return int.TryParse(spriteName.Substring(prefix.Length, r - prefix.Length), out int c) &&
                   c >= 0 && c < RockPxCatalog.SheetCols
                   ? c : -1;
        }

        /// <summary>The tide row a slice name encodes, or -1.</summary>
        public static int RowOf(string spriteName, string stem)
        {
            string prefix = stem + "_c";
            if (spriteName == null || !spriteName.StartsWith(prefix, StringComparison.Ordinal))
                return -1;
            int r = spriteName.IndexOf("_r", prefix.Length, StringComparison.Ordinal);
            if (r < 0) return -1;
            return int.TryParse(spriteName.Substring(r + 2), out int row) &&
                   row >= 0 && row < RockPxCatalog.SheetRows
                   ? row : -1;
        }

        static string StripSuffix(string stem, string suffix) =>
            !string.IsNullOrEmpty(suffix) && stem.EndsWith(suffix, StringComparison.Ordinal)
            ? stem.Substring(0, stem.Length - suffix.Length)
            : stem;

        static bool Check(string who, string what, bool ok)
        {
            if (!ok) Debug.LogError($"[RockPxSheetSlicer] VERIFY: '{who}' — expected {what}.");
            return ok;
        }
    }
}
#endif
