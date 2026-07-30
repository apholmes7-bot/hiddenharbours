#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Slices and locks the import settings of the Acadian Shrub sheets — the geometry half of the
    /// kit, run straight after <c>ShrubBaker</c> by <c>ShrubBakeMenu</c>.
    ///
    /// <para><b>⚠ THE TWO DATA CHANNELS MUST IMPORT WITH sRGB OFF.</b> <c>_light</c> packs
    /// <c>R = key · G = rim · B = depth · A = coverage</c> and <c>_calendar</c> packs
    /// <c>R = veil flag · G = ornament · B = snow · A = coverage</c>: both are NUMBERS, not colour.
    /// Leave sRGB on and Unity applies a gamma curve to them — the sprite still LOOKS fine in the
    /// inspector, the lighting is simply wrong by a curve, and nothing but a numeric assert notices.
    /// <see cref="ArtImportPipeline"/> stamps the pixel-art floor on first import; this adds the
    /// Multiple-mode grid, the root-crown pivot, and the per-channel colour space.</para>
    ///
    /// <para><b>⚠ THE PIVOT IS THE GROUND CONTACT AT THE ROOT CROWN, AND IT IS PER SPECIES.</b> It
    /// comes from the contract, never from the sprite's alpha and never from a literal — and
    /// <see cref="ArtImportPipeline"/>'s <c>/foliage/</c> default of BottomCenter is wrong for this
    /// kit, exactly as it is for the trees. <b>Every rect on a sheet gets the SAME pivot</b>: that is
    /// the entire purchase the union cell makes, and it is what stops a shrub hopping when the calendar
    /// crosses a phase boundary.</para>
    ///
    /// <para><b>⚠ SNOW IS NOT AN AXIS HERE.</b> Snow only ever CLIPS rows, so it cannot change the cell
    /// and it is outside the union by construction. A snow-laden sheet is a separate deliberate bake,
    /// which is why this slicer only ever recognises the two axes the contract publishes.</para>
    /// </summary>
    public static class ShrubSheetSlicer
    {
        public static string ShrubsRoot => ShrubCatalog.ShrubsRoot;

        // =================================================================================
        // entry points
        // =================================================================================

        [MenuItem("Hidden Harbours/Art/Slice Shrub Sheets", priority = 54)]
        public static void SliceAllMenu()
        {
            int sliced = SliceAll(out int failed);
            Debug.Log(failed == 0
                ? $"[ShrubSheetSlicer] Sliced {sliced} shrub sheet(s)."
                : $"[ShrubSheetSlicer] Sliced {sliced}, FAILED {failed} — see the errors above.");
        }

        /// <summary>Headless entry point. ⚠️ Never invoke alongside <c>-runTests</c> with
        /// <c>-quit</c> — the two race and exit 0 with <c>total=0</c>, which reads as a pass.</summary>
        public static void SliceAllFromCommandLine()
        {
            int sliced = SliceAll(out int failed);
            Debug.Log($"[ShrubSheetSlicer] headless: sliced {sliced}, failed {failed}.");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        /// <summary>
        /// Slices every shrub sheet on disk. Returns how many succeeded; <paramref name="failed"/>
        /// carries the rest.
        ///
        /// <para><b>Zero sheets is the NORMAL state of this kit</b> — it bakes to order and ships no
        /// pixels, so "nothing to slice" is reported plainly rather than as a problem.</para>
        /// </summary>
        public static int SliceAll(out int failed)
        {
            failed = 0;
            var contract = ShrubCatalog.Load();
            if (contract == null) { failed = 1; return 0; }

            string full = Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName, ShrubsRoot);
            if (!Directory.Exists(full))
            {
                Debug.Log($"[ShrubSheetSlicer] '{ShrubsRoot}' does not exist yet — this kit bakes to " +
                          "order and ships no pixels, so there is nothing to slice.");
                return 0;
            }

            int sliced = 0, seen = 0;
            foreach (string path in Directory.GetFiles(full, "*.png", SearchOption.TopDirectoryOnly)
                                             .Select(ToAssetPath)
                                             .OrderBy(p => p, System.StringComparer.Ordinal))
            {
                string fileStem = Path.GetFileNameWithoutExtension(path);
                var channel = ShrubCatalog.ChannelForFileStem(fileStem, out string sheetStem);
                string species = ShrubCatalog.SpeciesForStem(sheetStem);
                seen++;

                if (species == null)
                {
                    Debug.LogError(
                        $"[ShrubSheetSlicer] '{fileStem}' is in {ShrubsRoot} but its stem does not " +
                        "start with a known species key. A stranger in the kit folder must fail, not " +
                        "be guessed at.");
                    failed++;
                    continue;
                }

                var entry = contract.Find(species);
                if (entry == null)
                {
                    Debug.LogError(
                        $"[ShrubSheetSlicer] '{fileStem}' claims species '{species}' but the " +
                        $"contract has no such entry. Re-export {ShrubCatalog.ContractFileName}.");
                    failed++;
                    continue;
                }

                if (SliceOne(path, fileStem, entry, channel)) sliced++;
                else failed++;
            }

            if (seen == 0)
                Debug.Log($"[ShrubSheetSlicer] No PNGs in {ShrubsRoot} — this kit bakes to order and " +
                          "ships no pixels. Nothing to slice.");
            return sliced;
        }

        /// <summary>
        /// One sheet: lock the importer, verify the sheet is a whole number of union cells on one of
        /// the contract's two axes, and lay the grid with the species' single pivot.
        /// </summary>
        public static bool SliceOne(string path, string fileStem,
                                    ShrubCatalog.SpeciesEntry entry,
                                    ShrubCatalog.Channel channel)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[ShrubSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return false;
            }

            ApplyImportSettings(importer, channel);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[ShrubSheetSlicer] '{path}' failed to load — skipping.");
                return false;
            }

            // 🔴 The 2048 assert. Over the cap Unity has ALREADY downscaled the texture by the time we
            // see it, and the sprite count would still come out right — so this is the only place the
            // mistake is visible before a pivot assert much later.
            if (tex.width > ShrubCatalog.ImportSizeCap || tex.height > ShrubCatalog.ImportSizeCap)
            {
                Debug.LogError(
                    $"[ShrubSheetSlicer] '{fileStem}' is {tex.width}×{tex.height}, over the " +
                    $"{ShrubCatalog.ImportSizeCap} px import cap — Unity has already DOWNSCALED it " +
                    "and the sprite count would still come out right. Not slicing.");
                return false;
            }

            // The sheet must match ONE of the two axes the contract publishes. A sheet that is neither
            // is a bake this slicer does not understand — the cell would divide cleanly and the rects
            // would still be wrong.
            bool phaseAxis = tex.width == entry.PhaseSheetW && tex.height == entry.PhaseSheetH;
            bool variantAxis = tex.width == entry.VariantSheetW && tex.height == entry.VariantSheetH;
            if (!phaseAxis && !variantAxis)
            {
                Debug.LogError(
                    $"[ShrubSheetSlicer] '{fileStem}' is {tex.width}×{tex.height} but the contract's " +
                    $"{entry.Key} sheets are {entry.PhaseSheetW}×{entry.PhaseSheetH} (phase axis) or " +
                    $"{entry.VariantSheetW}×{entry.VariantSheetH} (variant axis), from a " +
                    $"{entry.CellW}×{entry.CellH} union cell. Not slicing — re-bake, do not edit " +
                    $"{ShrubCatalog.ContractFileName}.");
                return false;
            }

            int cols = tex.width / entry.CellW;
            int rows = tex.height / entry.CellH;

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // Re-use existing spriteIDs so a re-bake of unchanged art is a no-op on the .meta. Fresh
            // GUIDs on every run rewrite every spriteID — pure diff noise that buries the owner's real
            // changes (the lesson of the 43 tree metas).
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(fileStem, cols, rows, entry, existingIds);
            dp.SetSpriteRects(rects);

            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(
                rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[ShrubSheetSlicer] Sliced '{fileStem}' → {rects.Length} sprite(s) " +
                      $"({rows} sway row(s) × {cols} {(phaseAxis ? "phase" : "variant")} col(s) of " +
                      $"{entry.CellW}×{entry.CellH}), root-crown pivot " +
                      $"{ShrubCatalog.NormalizedPivot(entry)}, " +
                      $"sRGB {(ShrubCatalog.IsColourChannel(channel) ? "ON" : "OFF")}.");
            return true;
        }

        /// <summary>
        /// The kit's import lock, per channel. Starts from
        /// <see cref="ArtImportPipeline.ApplyLockedSettings"/> (PPU 32, Point, uncompressed, mips off —
        /// the canon in one place) and then overrides the things that canon gets wrong for a
        /// three-channel kit: colour space, the alpha flag on data channels, and the pivot mode.
        /// </summary>
        public static void ApplyImportSettings(TextureImporter importer,
                                               ShrubCatalog.Channel channel)
        {
            ArtImportPipeline.ApplyLockedSettings(importer, importer.assetPath);

            // Never lift the cap for this kit — see ShrubCatalog.ImportSizeCap.
            importer.maxTextureSize = ShrubCatalog.ImportSizeCap;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);

            // FullRect, not Tight. A tight mesh drops the fully-transparent interior of a shrub — and
            // this kit is a HOLLOW BASKET on purpose (rule 2 counts the enclosed sky holes), so its
            // interior is mostly transparent. The two data channels are also sampled THROUGH the
            // albedo's geometry by the sprite-light consumer, so all three must share one mesh or the
            // channels shear apart at the silhouette. A tight mesh per channel would give the veil,
            // which is 1 px filaments, a different outline in each.
            s.spriteMeshType = SpriteMeshType.FullRect;

            // 🔴 The two data channels are NUMBERS, not colour.
            bool colour = ShrubCatalog.IsColourChannel(channel);
            s.sRGBTexture = colour;

            // alphaIsTransparency premultiplies the colour bleed at the alpha edge — right for the
            // albedo, wrong for masks whose A IS the coverage signal and whose R is a flag.
            s.alphaIsTransparency = colour;
            s.alphaSource = TextureImporterAlphaSource.FromInput;

            // Pivot is set per SPRITE RECT (BuildRects) because it is per species; leave the
            // sheet-level default honest rather than the BottomCenter that ArtImportPipeline picks for
            // /foliage/, which is wrong for every species in this kit.
            s.spriteAlignment = (int)SpriteAlignment.Custom;

            importer.SetTextureSettings(s);
        }

        /// <summary>
        /// The grid: axis COLS × sway ROWS, row-major from the TOP-LEFT (Unity's rects are
        /// bottom-origin, so row 0 maps to the highest Y — the convention every slicer here uses).
        ///
        /// <para>Names state GEOMETRY — <c>&lt;stem&gt;_c&lt;col&gt;_f&lt;frame&gt;</c> is "column 2,
        /// sway frame 1" and nothing else. WHICH phase or variant column 2 is lives in
        /// <see cref="ShrubCatalog.PhaseKeys"/> and the sheet's own stem, for the same reason every
        /// other kit here splits the two: a name that claims a meaning is a name that can lie.</para>
        ///
        /// <para>⭐ Every rect gets the SAME pivot — the species' one root-crown ground contact. See the
        /// class remarks: that is the entire purpose of the union cell.</para>
        /// </summary>
        public static SpriteRect[] BuildRects(string stem, int cols, int rows,
                                              ShrubCatalog.SpeciesEntry entry,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            Vector2 pivot = ShrubCatalog.NormalizedPivot(entry);
            var rects = new List<SpriteRect>(cols * rows);

            for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
            {
                string name = $"{stem}_c{col}_f{row}";
                var rect = new SpriteRect
                {
                    name = name,
                    // Bottom-origin: row 0 is the TOP row of the sheet.
                    rect = new Rect(col * entry.CellW,
                                    (rows - 1 - row) * entry.CellH,
                                    entry.CellW, entry.CellH),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    border = Vector4.zero,
                };
                rect.spriteID = existingIds != null && existingIds.TryGetValue(name, out GUID id)
                    ? id : GUID.Generate();
                rects.Add(rect);
            }
            return rects.ToArray();
        }

        // =================================================================================
        // verification
        // =================================================================================

        /// <summary>
        /// Re-reads every sliced sheet from the AssetDatabase and checks it against the contract —
        /// the same check the bake menu runs, so "imported correctly" has one definition rather than a
        /// test-only one. Returns true when every sheet agrees (including when there are none).
        /// </summary>
        public static bool VerifyAll(bool logEachPass) => VerifyAll(logEachPass, out _);

        public static bool VerifyAll(bool logEachPass, out int checkedCount)
        {
            checkedCount = 0;
            var contract = ShrubCatalog.Load();
            if (contract == null) return false;

            string full = Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName, ShrubsRoot);
            if (!Directory.Exists(full)) return true;

            bool allOk = true;
            foreach (string path in Directory.GetFiles(full, "*.png", SearchOption.TopDirectoryOnly)
                                             .Select(ToAssetPath))
            {
                string fileStem = Path.GetFileNameWithoutExtension(path);
                var channel = ShrubCatalog.ChannelForFileStem(fileStem, out string sheetStem);
                string species = ShrubCatalog.SpeciesForStem(sheetStem);
                var entry = species == null ? null : contract.Find(species);
                if (entry == null) { allOk = false; continue; }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { allOk = false; continue; }

                checkedCount++;
                bool ok = true;
                ok &= Check(fileStem, "spriteMode Multiple",
                            importer.spriteImportMode == SpriteImportMode.Multiple);
                ok &= Check(fileStem, "uncompressed",
                            importer.textureCompression == TextureImporterCompression.Uncompressed);
                ok &= Check(fileStem, $"PPU {ArtImportPipeline.PixelsPerUnit}",
                            Mathf.Approximately(importer.spritePixelsPerUnit,
                                                ArtImportPipeline.PixelsPerUnit));
                ok &= Check(fileStem, "Point filter", importer.filterMode == FilterMode.Point);
                ok &= Check(fileStem, $"maxTextureSize {ShrubCatalog.ImportSizeCap}",
                            importer.maxTextureSize == ShrubCatalog.ImportSizeCap);

                // 🔴 The one that nothing else would notice.
                ok &= Check(fileStem,
                            $"sRGB {(ShrubCatalog.IsColourChannel(channel) ? "ON" : "OFF")}",
                            SRgbOf(importer) == ShrubCatalog.IsColourChannel(channel));

                ok &= Check(fileStem, "FullRect mesh", MeshTypeOf(importer) == SpriteMeshType.FullRect);

                // Every rect carries the species' ONE pivot — the union cell's whole purpose.
                Vector2 want = ShrubCatalog.NormalizedPivot(entry);
                var rects = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                if (rects.Length > 0)
                {
                    bool pivotsAgree = rects.All(sp =>
                    {
                        Vector2 got = new Vector2(sp.pivot.x / sp.rect.width,
                                                  sp.pivot.y / sp.rect.height);
                        return Mathf.Abs(got.x - want.x) < 1e-3f && Mathf.Abs(got.y - want.y) < 1e-3f;
                    });
                    ok &= Check(fileStem, $"all {rects.Length} rects on pivot {want}", pivotsAgree);

                    bool cellsAgree = rects.All(sp =>
                        Mathf.RoundToInt(sp.rect.width) == entry.CellW &&
                        Mathf.RoundToInt(sp.rect.height) == entry.CellH);
                    ok &= Check(fileStem, $"cell {entry.CellW}×{entry.CellH}", cellsAgree);
                }

                if (ok && logEachPass) Debug.Log($"[ShrubSheetSlicer] ✓ {fileStem}");
                allOk &= ok;
            }
            return allOk;
        }

        static bool Check(string stem, string what, bool ok)
        {
            if (!ok) Debug.LogError($"[ShrubSheetSlicer] {stem}: {what} — FAILED.");
            return ok;
        }

        /// <summary>The importer's colour space. Lives on <see cref="TextureImporterSettings"/>, not on
        /// the importer, which is an easy thing to look for in the wrong place.</summary>
        public static bool SRgbOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.sRGBTexture;
        }

        public static SpriteMeshType MeshTypeOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.spriteMeshType;
        }

        static string ToAssetPath(string absolute)
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            return absolute.Substring(root.Length + 1).Replace('\\', '/');
        }
    }
}
#endif
