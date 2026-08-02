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
    /// Slices and IMPORT-LOCKS the Shoreline Plant sheets under
    /// <see cref="ShorePlantCatalog.PlantsRoot"/> — the albedo plus its <c>_light</c> and
    /// <c>_tide</c> siblings, baked to order by <c>ShorePlantBaker</c>.
    ///
    /// <para>Every number comes from the kit's own contract
    /// (<see cref="ShorePlantCatalog.Load"/>) — union cell, sheet dimensions and the ground-contact
    /// pivot. Nothing is hardcoded here, which is the whole point of the contract shipping alongside
    /// the rig.</para>
    ///
    /// <para><b>🔴 sRGB OFF on <c>_light</c> and <c>_tide</c>.</b> They are DATA, not colour. Leave
    /// sRGB on and every channel is gamma-mangled while the sprite still looks perfectly fine —
    /// nothing catches it but a numeric assert. <c>state.B</c> in particular is a FLAG (255 on strap
    /// pixels); gamma it and the shader's strap branch starts firing on the wrong pixels, which
    /// reads as an art bug about rims appearing on grass. <see cref="ArtImportPipeline"/> stamps
    /// sRGB ON for everything, so this is an override, not a default.</para>
    ///
    /// <para><b>⚠ WHY THIS KIT CANNOT BE A <see cref="SpriteSheetSlicer"/> MANIFEST ROW.</b> That
    /// manifest writes ONE pivot per sheet and knows nothing about a three-channel sheet family. A
    /// plant sheet's pivot is per SPECIES (each sheet is one species, so one pivot serves the sheet),
    /// but the cell size differs per species and per growth stage, and the two data channels need
    /// their own colour-space treatment. Same reason <see cref="TreeSheetSlicer"/> and
    /// <see cref="RockSheetSlicer"/> exist beside the manifest.</para>
    ///
    /// <para><b>The pivot is the GROUND CONTACT — the holdfast or root crown — not the bottom of the
    /// cell.</b> The drape pad projects BELOW it (a drained kelp folds onto its rock and puddles
    /// downslope), so <c>ArtImportPipeline.PivotFor</c>'s default and the tempting BottomCenter are
    /// both wrong here by that overhang — measured 9–42 px = 0.28–1.31 m across the sixteen species.
    /// Read from the contract's <c>pivot</c>, normalised per ADR 0026's <c>(x/W, (H − y)/H)</c>.</para>
    ///
    /// <para><b>⭐ ONE pivot per species, ACROSS EVERY TIDE STATE.</b> The cell is unioned over
    /// 4 variants × 2 seasons × 5 tides precisely so the runtime's constant tide-state swaps are
    /// anchored by construction. The slicer must therefore write the SAME pivot to every column of a
    /// tide-axis sheet — a per-column pivot here would reintroduce the 1 px hop the union cell was
    /// bought to prevent.</para>
    /// </summary>
    public static class ShorePlantSheetSlicer
    {
        public static string PlantsRoot => ShorePlantCatalog.PlantsRoot;

        // ---- entry points ---------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Dev/Slice Shore Plant Sheets", priority = 145)]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int failed);
            Debug.Log($"[ShorePlantSheetSlicer] Sliced {n} plant sheet(s) ({failed} failed).");
        }

        /// <summary>Batch entry point for <c>-executeMethod</c> — exits non-zero on any failure so a
        /// headless bake fails loudly instead of committing a half-sliced set.</summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int failed);
                Debug.Log($"[ShorePlantSheetSlicer] (batch) Sliced {n} sheet(s) ({failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[ShorePlantSheetSlicer] {failed} sheet(s) failed — see above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShorePlantSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---- the work -------------------------------------------------------------------------

        /// <summary>
        /// Slice + import-lock every plant sheet on disk. A file whose stem the contract does not
        /// claim is left alone and reported, never guessed at. Zero sheets is NOT a failure — this
        /// kit bakes to order, so an empty folder is its normal resting state.
        /// </summary>
        public static int SliceAll(out int failed)
        {
            failed = 0;

            if (!Directory.Exists(PlantsRoot))
            {
                Debug.Log($"[ShorePlantSheetSlicer] No folder at '{PlantsRoot}' — nothing baked yet.");
                return 0;
            }

            ShorePlantCatalog.Contract contract = ShorePlantCatalog.Load();
            if (contract == null) { failed = 1; return 0; }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PlantsRoot.TrimEnd('/') });
            int sliced = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(PlantsRoot, StringComparison.Ordinal)) continue;
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                string fileStem = Path.GetFileNameWithoutExtension(path);
                var channel = ShorePlantCatalog.ChannelForFileStem(fileStem, out string sheetStem);
                string speciesKey = ShorePlantCatalog.SpeciesForStem(sheetStem);
                ShorePlantCatalog.SpeciesEntry entry =
                    speciesKey == null ? null : contract.Find(speciesKey);

                if (entry == null)
                {
                    Debug.LogError(
                        $"[ShorePlantSheetSlicer] '{fileStem}' is under {PlantsRoot} but no species " +
                        "in the contract claims it. Not slicing — a sheet with no contract has no " +
                        "cell and no pivot, and guessing either is how a plant ends up floating " +
                        "above its own holdfast.");
                    failed++;
                    continue;
                }

                if (SliceOne(path, fileStem, entry, channel)) sliced++;
                else failed++;
            }

            if (sliced == 0 && failed == 0)
                Debug.Log("[ShorePlantSheetSlicer] No plant sheets on disk yet — this kit bakes to " +
                          "order (Hidden Harbours ▸ Dev ▸ Bake Shore Plant Sheets).");

            AssetDatabase.SaveAssets();
            return sliced;
        }

        /// <summary>Slice + import-lock ONE sheet against its contract entry.</summary>
        public static bool SliceOne(string path, string fileStem,
                                    ShorePlantCatalog.SpeciesEntry entry,
                                    ShorePlantCatalog.Channel channel)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[ShorePlantSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return false;
            }

            ApplyImportSettings(importer, channel);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[ShorePlantSheetSlicer] '{path}' failed to load — skipping.");
                return false;
            }

            if (tex.width > ShorePlantCatalog.ImportSizeCap ||
                tex.height > ShorePlantCatalog.ImportSizeCap)
            {
                Debug.LogError(
                    $"[ShorePlantSheetSlicer] '{fileStem}' is {tex.width}×{tex.height}, over the " +
                    $"{ShorePlantCatalog.ImportSizeCap} px import cap — Unity has already " +
                    "DOWNSCALED it and the sprite count would still come out right. Not slicing.");
                return false;
            }

            // The sheet must be a whole number of union cells on both axes, and must match ONE of
            // the two axes the contract publishes. A sheet that is neither is a bake this slicer
            // does not understand — the cell would divide cleanly and the rects would still be
            // wrong.
            bool tideAxis = tex.width == entry.TideSheetW && tex.height == entry.TideSheetH;
            bool variantAxis = tex.width == entry.VariantSheetW && tex.height == entry.VariantSheetH;
            if (!tideAxis && !variantAxis)
            {
                Debug.LogError(
                    $"[ShorePlantSheetSlicer] '{fileStem}' is {tex.width}×{tex.height} but the " +
                    $"contract's {entry.Key} sheets are {entry.TideSheetW}×{entry.TideSheetH} " +
                    $"(tide axis) or {entry.VariantSheetW}×{entry.VariantSheetH} (variant axis), " +
                    $"from a {entry.CellW}×{entry.CellH} union cell. Not slicing — re-bake, do not " +
                    $"edit {ShorePlantCatalog.ContractFileName}.");
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
            // GUIDs on every run rewrite every spriteID — pure diff noise that buries the owner's
            // real changes (the lesson of the 43 tree metas).
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(fileStem, cols, rows, entry, existingIds);
            dp.SetSpriteRects(rects);

            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[ShorePlantSheetSlicer] Sliced '{fileStem}' → {rects.Length} sprite(s) " +
                      $"({rows} sway row(s) × {cols} {(tideAxis ? "tide" : "variant")} col(s) of " +
                      $"{entry.CellW}×{entry.CellH}), ground-contact pivot " +
                      $"{ShorePlantCatalog.NormalizedPivot(entry)} " +
                      $"(drape {ShorePlantCatalog.DrapeMetres(entry):F2} m below it), " +
                      $"sRGB {(ShorePlantCatalog.IsColourChannel(channel) ? "ON" : "OFF")}.");
            return true;
        }

        /// <summary>
        /// The kit's import lock, per channel. Starts from
        /// <see cref="ArtImportPipeline.ApplyLockedSettings"/> (PPU 32, Point, uncompressed, mips
        /// off — the canon in one place) and then overrides the things that canon gets wrong for a
        /// three-channel kit: colour space, the alpha flag on data channels, and the pivot mode.
        /// </summary>
        public static void ApplyImportSettings(TextureImporter importer,
                                               ShorePlantCatalog.Channel channel)
        {
            ArtImportPipeline.ApplyLockedSettings(importer, importer.assetPath);

            // Never lift the cap for this kit — see ShorePlantCatalog.ImportSizeCap.
            importer.maxTextureSize = ShorePlantCatalog.ImportSizeCap;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);

            // FullRect, not Tight. A tight mesh drops the fully-transparent interior of a frond
            // family — and the two data channels are sampled THROUGH the albedo's alpha by the
            // sprite-light consumer, so all three must share one geometry or the channels shear
            // apart at the silhouette.
            s.spriteMeshType = SpriteMeshType.FullRect;

            // 🔴 The two data channels are NUMBERS, not colour.
            bool colour = ShorePlantCatalog.IsColourChannel(channel);
            s.sRGBTexture = colour;

            // alphaIsTransparency premultiplies the colour bleed at the alpha edge — right for the
            // albedo, wrong for masks whose A IS the coverage signal and whose B is a flag.
            s.alphaIsTransparency = colour;
            s.alphaSource = TextureImporterAlphaSource.FromInput;

            // Pivot is set per SPRITE RECT (BuildRects) because it is per species; leave the
            // sheet-level default honest rather than the /Sprites/ Center that ArtImportPipeline
            // picks, which is wrong for every plant in this kit.
            s.spriteAlignment = (int)SpriteAlignment.Custom;

            importer.SetTextureSettings(s);
        }

        /// <summary>
        /// The grid: axis COLS × sway ROWS, row-major from the TOP-LEFT (Unity's rects are
        /// bottom-origin, so row 0 maps to the highest Y — the convention every slicer here uses).
        ///
        /// <para>Names state GEOMETRY — <c>&lt;stem&gt;_c&lt;col&gt;_f&lt;frame&gt;</c> is "column 2,
        /// sway frame 1" and nothing else. WHICH tide state or variant column 2 is lives in
        /// <see cref="ShorePlantCatalog.TideKeys"/> and the sheet's own stem, for the same reason
        /// every other kit here splits the two: a name that claims a meaning is a name that can
        /// lie.</para>
        ///
        /// <para>⭐ Every rect gets the SAME pivot — the species' one ground contact. See the class
        /// remarks: that is the entire purpose of the union cell.</para>
        /// </summary>
        public static SpriteRect[] BuildRects(string stem, int cols, int rows,
                                              ShorePlantCatalog.SpeciesEntry entry,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            Vector2 pivot = ShorePlantCatalog.NormalizedPivot(entry);
            var rects = new SpriteRect[rows * cols];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    string name = $"{stem}_c{c}_f{r}";
                    rects[r * cols + c] = new SpriteRect
                    {
                        name = name,
                        spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                                   ? id
                                   : GUID.Generate(),
                        rect = new Rect(c * entry.CellW,
                                        (rows - 1 - r) * entry.CellH,
                                        entry.CellW, entry.CellH),
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
        /// Assert every plant sheet ON DISK imported the way this kit needs. Sheets the owner has
        /// not baked are not failures — this kit bakes to order. Returns true if every sheet present
        /// passes; <paramref name="checkedCount"/> reports how many were actually examined.
        /// </summary>
        public static bool VerifyAll(bool logEachPass, out int checkedCount)
        {
            checkedCount = 0;
            ShorePlantCatalog.Contract contract = ShorePlantCatalog.Load();
            if (contract == null) return false;

            if (!Directory.Exists(PlantsRoot))
            {
                Debug.Log($"[ShorePlantSheetSlicer] VERIFY: no folder at '{PlantsRoot}'.");
                return true;
            }

            bool allOk = true;

            foreach (string path in Directory.GetFiles(PlantsRoot, "*.png", SearchOption.TopDirectoryOnly)
                                             .Select(p => p.Replace('\\', '/'))
                                             .OrderBy(p => p, StringComparer.Ordinal))
            {
                string fileStem = Path.GetFileNameWithoutExtension(path);
                var channel = ShorePlantCatalog.ChannelForFileStem(fileStem, out string sheetStem);
                string speciesKey = ShorePlantCatalog.SpeciesForStem(sheetStem);
                ShorePlantCatalog.SpeciesEntry entry =
                    speciesKey == null ? null : contract.Find(speciesKey);
                if (entry == null) continue;               // reported by SliceAll, not here

                checkedCount++;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[ShorePlantSheetSlicer] VERIFY: '{fileStem}' has no importer.");
                    allOk = false;
                    continue;
                }

                bool colour = ShorePlantCatalog.IsColourChannel(channel);
                bool ok = true;
                ok &= Check(fileStem, "Multiple-mode",
                            importer.spriteImportMode == SpriteImportMode.Multiple);
                ok &= Check(fileStem, "Compression None",
                            importer.textureCompression == TextureImporterCompression.Uncompressed);
                ok &= Check(fileStem, $"PPU {ArtImportPipeline.PixelsPerUnit}",
                            Mathf.Approximately(importer.spritePixelsPerUnit,
                                                ArtImportPipeline.PixelsPerUnit));
                ok &= Check(fileStem, "Point filter", importer.filterMode == FilterMode.Point);
                ok &= Check(fileStem, "mips off", !importer.mipmapEnabled);
                // 🔴 The one that only a numeric assert can catch.
                ok &= Check(fileStem, $"sRGB {(colour ? "ON" : "OFF")}", SRgbOf(importer) == colour);

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    int expected = (tex.width / entry.CellW) * (tex.height / entry.CellH);
                    ok &= Check(fileStem, $"{expected} sprite(s), got {sprites.Length}",
                                sprites.Length == expected);
                }

                // ⭐ ONE pivot for the whole sheet — the union-cell invariant, checked on every rect.
                Vector2 want = ShorePlantCatalog.PivotPixels(entry);
                foreach (var sprite in sprites)
                    ok &= Check(sprite.name,
                                $"ground-contact pivot {want}, got {sprite.pivot}",
                                Mathf.Abs(sprite.pivot.x - want.x) < 0.01f &&
                                Mathf.Abs(sprite.pivot.y - want.y) < 0.01f);

                if (!ok) allOk = false;
                else if (logEachPass)
                    Debug.Log($"[ShorePlantSheetSlicer] VERIFY OK: {fileStem} = " +
                              $"{sprites.Length} sprite(s) of {entry.CellW}×{entry.CellH}, " +
                              $"sRGB {(colour ? "ON" : "OFF")}.");
            }

            Debug.Log($"[ShorePlantSheetSlicer] VERIFY: checked {checkedCount} sheet(s) on disk — " +
                      (allOk ? "ALL PASS" : "FAILURES PRESENT"));
            return allOk;
        }

        /// <summary>sRGB lives on <see cref="TextureImporterSettings"/>, not the importer — reading
        /// it off the importer directly is how a gamma-mangled data channel gets waved through.</summary>
        public static bool SRgbOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.sRGBTexture;
        }

        static bool Check(string who, string what, bool ok)
        {
            if (!ok) Debug.LogError($"[ShorePlantSheetSlicer] VERIFY: '{who}' — expected {what}.");
            return ok;
        }
    }
}
#endif
