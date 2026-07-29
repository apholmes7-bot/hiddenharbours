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
    /// Slices and IMPORT-LOCKS the Rock Iso sheets under <see cref="RockIsoCatalog.RockRoot"/> —
    /// <c>&lt;Species&gt;_&lt;stone&gt;_&lt;tide&gt;.png</c>, baked to order by
    /// <c>RockIsoBaker</c>.
    ///
    /// <para>Every number comes from the kit's own <c>RockIso.json</c>
    /// (<see cref="RockIsoCatalog.Load"/>) — cell, variant columns, dress rows and each variant's
    /// bottom-centre ground-contact pivot. Nothing is hardcoded here, which is the whole point of
    /// the sidecar shipping alongside the rig.</para>
    ///
    /// <para><b>⚠ WHY THIS KIT CANNOT BE A <see cref="SpriteSheetSlicer"/> MANIFEST ROW.</b> That
    /// manifest writes ONE pivot per sheet. A rock sheet's pivot is <b>per variant COLUMN</b>: every
    /// variant is a different volume with its own ground contact, measured 7–22 px up from the cell
    /// floor across the twenty variants. One pivot for the sheet would plant one column correctly
    /// and float or sink the rest — and it would read as an art bug, not an import bug. Same reason
    /// <see cref="TreeSheetSlicer"/> exists beside the manifest.</para>
    ///
    /// <para><b>The pivot is the GROUND CONTACT, not the bottom of the cell.</b> Under the 40°
    /// camera the near flank is drawn BELOW the contact point, so
    /// <c>ArtImportPipeline.PivotFor</c>'s Center default — and the tempting BottomCenter — are both
    /// wrong here by that overhang. Read from <c>anchors.pivot</c>, normalised per ADR 0026's
    /// <c>(x/W, (H − y)/H)</c>.</para>
    ///
    /// <para>Sheets are all COLOUR — this kit ships no mask/normal/ID channel — so sRGB stays on and
    /// the shared import lock applies unchanged apart from the pivot.</para>
    /// </summary>
    public static class RockSheetSlicer
    {
        public static string RockRoot => RockIsoCatalog.RockRoot;

        // ---- entry points ---------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Slice Rock Iso Sheets", priority = 53)]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int failed);
            Debug.Log($"[RockSheetSlicer] Sliced {n} rock sheet(s) ({failed} failed).");
        }

        /// <summary>Batch entry point for <c>-executeMethod</c> — exits non-zero on any failure so a
        /// headless bake fails loudly instead of committing a half-sliced set.</summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int failed);
                Debug.Log($"[RockSheetSlicer] (batch) Sliced {n} rock sheet(s) ({failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[RockSheetSlicer] {failed} sheet(s) failed — see errors above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[RockSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---- the work -------------------------------------------------------------------------

        /// <summary>
        /// Slice + import-lock every rock sheet on disk. A sheet whose stem the contract does not
        /// claim is left alone and reported, never guessed at. Zero sheets is NOT a failure — this
        /// kit bakes to order, so an empty folder is its normal resting state.
        /// </summary>
        public static int SliceAll(out int failed)
        {
            failed = 0;

            if (!Directory.Exists(RockRoot))
            {
                Debug.Log($"[RockSheetSlicer] No folder at '{RockRoot}' — nothing baked yet.");
                return 0;
            }

            RockIsoCatalog.Contract contract = RockIsoCatalog.Load();
            if (contract == null) { failed = 1; return 0; }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { RockRoot.TrimEnd('/') });
            int sliced = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(RockRoot, StringComparison.Ordinal)) continue;
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                string stem = Path.GetFileNameWithoutExtension(path);
                RockIsoCatalog.SpeciesEntry entry = SpeciesForStem(contract, stem);
                if (entry == null)
                {
                    Debug.LogError(
                        $"[RockSheetSlicer] '{stem}' is under {RockRoot} but no species in " +
                        $"{RockIsoCatalog.ContractFileName} claims it. Not slicing — a sheet with no " +
                        "contract has no cell and no pivots, and guessing either is how a rock ends " +
                        "up floating.");
                    failed++;
                    continue;
                }

                if (SliceOne(path, stem, entry)) sliced++;
                else failed++;
            }

            if (sliced == 0 && failed == 0)
                Debug.Log($"[RockSheetSlicer] No rock sheets on disk yet — this kit bakes to order " +
                          "(Hidden Harbours ▸ Art ▸ Bake Rock Iso Sheets).");

            AssetDatabase.SaveAssets();
            return sliced;
        }

        /// <summary>
        /// The species a sheet stem belongs to, by the kit's <c>&lt;Species&gt;_&lt;stone&gt;_&lt;tide&gt;</c>
        /// naming. Matched against the CONTRACT's species and the kit's own stone/tide axes rather
        /// than by splitting on '_', so a stray file cannot masquerade as a sheet.
        /// </summary>
        public static RockIsoCatalog.SpeciesEntry SpeciesForStem(RockIsoCatalog.Contract contract,
                                                                 string stem)
        {
            foreach (var species in contract.Species)
            foreach (string stone in RockIsoCatalog.Stones)
            foreach (string tide in RockIsoCatalog.Tides)
                if (string.Equals(stem, $"{species.Key}_{stone}_{tide}", StringComparison.Ordinal))
                    return species;
            return null;
        }

        /// <summary>Slice + import-lock ONE sheet against its contract entry.</summary>
        public static bool SliceOne(string path, string stem, RockIsoCatalog.SpeciesEntry entry)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[RockSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return false;
            }

            ApplyImportSettings(importer);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[RockSheetSlicer] '{path}' failed to load as Texture2D — skipping.");
                return false;
            }

            if (tex.width != entry.SheetW || tex.height != entry.SheetH)
            {
                Debug.LogError(
                    $"[RockSheetSlicer] '{stem}' is {tex.width}×{tex.height} but the contract says " +
                    $"{entry.SheetW}×{entry.SheetH} ({entry.SheetCols} variant(s) × " +
                    $"{entry.SheetRows} dress row(s) of {entry.CellW}×{entry.CellH}). Not slicing — " +
                    "re-bake, do not edit RockIso.json.");
                return false;
            }

            if (tex.width > RockIsoCatalog.ImportSizeCap || tex.height > RockIsoCatalog.ImportSizeCap)
            {
                Debug.LogError(
                    $"[RockSheetSlicer] '{stem}' is {tex.width}×{tex.height}, over the " +
                    $"{RockIsoCatalog.ImportSizeCap} px import cap — Unity has already DOWNSCALED it " +
                    "and the sprite count would still come out right. Not slicing.");
                return false;
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // Re-use existing spriteIDs so a re-bake of unchanged art is a no-op on the .meta.
            // Fresh GUIDs on every run rewrite every spriteID — pure diff noise that buries the
            // owner's real changes (the lesson of the 43 tree metas).
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(stem, entry, existingIds);
            dp.SetSpriteRects(rects);

            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[RockSheetSlicer] Sliced '{stem}' → {rects.Length} sprite(s) " +
                      $"({entry.SheetCols} variant(s) × {entry.SheetRows} dress row(s) of " +
                      $"{entry.CellW}×{entry.CellH}), per-variant ground-contact pivots.");
            return true;
        }

        /// <summary>
        /// The rock kit's import lock. Starts from
        /// <see cref="ArtImportPipeline.ApplyLockedSettings"/> (PPU 32, Point, uncompressed, mips
        /// off — the canon in one place) and overrides only the pivot mode, because the pivot is per
        /// sprite RECT here rather than per sheet.
        /// </summary>
        public static void ApplyImportSettings(TextureImporter importer)
        {
            ArtImportPipeline.ApplyLockedSettings(importer, importer.assetPath);

            importer.maxTextureSize = RockIsoCatalog.ImportSizeCap;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);

            // All colour — no data channels in this kit, so sRGB stays ON.
            s.sRGBTexture = true;
            s.alphaIsTransparency = true;
            s.alphaSource = TextureImporterAlphaSource.FromInput;

            // Leave the sheet-level default honest rather than Center, which is what
            // ArtImportPipeline picks and is wrong for every variant in this kit.
            s.spriteAlignment = (int)SpriteAlignment.Custom;

            importer.SetTextureSettings(s);
        }

        /// <summary>
        /// The grid: variant COLS × dress ROWS, row-major from the TOP-LEFT (Unity's rects are
        /// bottom-origin, so row 0 maps to the highest Y — the convention every slicer here uses).
        ///
        /// <para>Names state GEOMETRY — <c>&lt;stem&gt;_v&lt;col&gt;_d&lt;row&gt;</c> is "variant
        /// column 0, dress row 2" and nothing else. Which dress that row IS lives in
        /// <see cref="RockIsoCatalog.Dress"/>, for the same reason every other kit here splits the
        /// two: a name that claims a meaning is a name that can lie.</para>
        /// </summary>
        public static SpriteRect[] BuildRects(string stem, RockIsoCatalog.SpeciesEntry entry,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            var rects = new SpriteRect[entry.SheetRows * entry.SheetCols];
            for (int d = 0; d < entry.SheetRows; d++)
            {
                for (int v = 0; v < entry.SheetCols; v++)
                {
                    // ⚠ The pivot is this VARIANT's ground contact, not the sheet's.
                    RockIsoCatalog.VariantEntry variant = entry.Variants[v];
                    Vector2 pivot = RockIsoCatalog.NormalizedPivot(entry, variant);

                    string name = $"{stem}_v{v}_d{d}";
                    rects[d * entry.SheetCols + v] = new SpriteRect
                    {
                        name = name,
                        spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                                   ? id
                                   : GUID.Generate(),
                        rect = new Rect(v * entry.CellW,
                                        (entry.SheetRows - 1 - d) * entry.CellH,
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
        /// Assert every rock sheet ON DISK imported the way this kit needs. Sheets the owner has not
        /// baked are not failures — this kit bakes to order. Returns true if every sheet present
        /// passes; <paramref name="checkedCount"/> reports how many were actually examined.
        /// </summary>
        public static bool VerifyAll(bool logEachPass, out int checkedCount)
        {
            checkedCount = 0;
            RockIsoCatalog.Contract contract = RockIsoCatalog.Load();
            if (contract == null) return false;

            bool allOk = true;

            foreach (var entry in contract.Species)
            foreach (string stone in RockIsoCatalog.Stones)
            foreach (string tide in RockIsoCatalog.Tides)
            {
                string path = RockIsoCatalog.SheetPath(entry.Key, stone, tide);
                if (!File.Exists(path)) continue;          // not baked — legitimate
                checkedCount++;

                string stem = Path.GetFileNameWithoutExtension(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[RockSheetSlicer] VERIFY: '{stem}' has no TextureImporter.");
                    allOk = false;
                    continue;
                }

                bool ok = true;
                ok &= Check(stem, "Multiple-mode",
                            importer.spriteImportMode == SpriteImportMode.Multiple);
                ok &= Check(stem, "Compression None",
                            importer.textureCompression == TextureImporterCompression.Uncompressed);
                ok &= Check(stem, $"PPU {ArtImportPipeline.PixelsPerUnit}",
                            Mathf.Approximately(importer.spritePixelsPerUnit,
                                                ArtImportPipeline.PixelsPerUnit));
                ok &= Check(stem, "Point filter", importer.filterMode == FilterMode.Point);
                ok &= Check(stem, "mips off", !importer.mipmapEnabled);

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                int expected = entry.SheetCols * entry.SheetRows;
                ok &= Check(stem, $"{expected} sprite(s), got {sprites.Length}",
                            sprites.Length == expected);

                foreach (var sprite in sprites)
                {
                    int v = VariantColumnOf(sprite.name, stem);
                    if (v < 0 || v >= entry.Variants.Count) continue;
                    Vector2 want = RockIsoCatalog.PivotPixels(entry, entry.Variants[v]);
                    ok &= Check(sprite.name,
                                $"ground-contact pivot {want}, got {sprite.pivot}",
                                Mathf.Abs(sprite.pivot.x - want.x) < 0.01f &&
                                Mathf.Abs(sprite.pivot.y - want.y) < 0.01f);
                }

                if (!ok) allOk = false;
                else if (logEachPass)
                    Debug.Log($"[RockSheetSlicer] VERIFY OK: {stem} = {sprites.Length} sprite(s) " +
                              $"of {entry.CellW}×{entry.CellH}, per-variant ground pivots.");
            }

            Debug.Log($"[RockSheetSlicer] VERIFY: checked {checkedCount} sheet(s) on disk — " +
                      (allOk ? "ALL PASS" : "FAILURES PRESENT"));
            return allOk;
        }

        /// <summary>The variant column a slice name encodes, or -1. Names are
        /// <c>&lt;stem&gt;_v&lt;col&gt;_d&lt;row&gt;</c>.</summary>
        public static int VariantColumnOf(string spriteName, string stem)
        {
            string prefix = stem + "_v";
            if (!spriteName.StartsWith(prefix, StringComparison.Ordinal)) return -1;
            int d = spriteName.IndexOf("_d", prefix.Length, StringComparison.Ordinal);
            if (d < 0) return -1;
            return int.TryParse(spriteName.Substring(prefix.Length, d - prefix.Length), out int v)
                   ? v : -1;
        }

        static bool Check(string who, string what, bool ok)
        {
            if (!ok) Debug.LogError($"[RockSheetSlicer] VERIFY: '{who}' — expected {what}.");
            return ok;
        }
    }
}
#endif
