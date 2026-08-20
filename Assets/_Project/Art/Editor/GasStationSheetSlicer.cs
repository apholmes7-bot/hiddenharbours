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
    /// Slices the baked gas-station sheets into named sprites and locks their import settings.
    ///
    /// <para>Sheet layout is a GRID, filled left-to-right then top-to-bottom, one cell per baked
    /// facing: <c>dispenser_sTwin_d3</c> is the twin-slab pump seen from the south-east. Most sheets
    /// come out as the familiar single strip; only the pieces too wide for one — the storefronts and
    /// their sales floors — fold into rows, and the contract carries the column count so the slicer
    /// never has to guess which happened.</para>
    ///
    /// <para>⚠️ For a <c>fold180</c> sheet the cell index is <c>dir % 4</c>, not a snapped quarter.
    /// Those four sprites cover all eight facings because <c>d</c> and <c>d+4</c> are the same
    /// picture on those pieces — see <see cref="GasStationKit.Fold180Types"/>.</para>
    /// </summary>
    public static class GasStationSheetSlicer
    {
        [MenuItem("Hidden Harbours/Art/Slice Gas Station Sheets", priority = 79)]
        public static void SliceAll()
        {
            var contract = LoadContract();
            if (contract == null) return;

            int sliced = 0, failed = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var sheet in contract.sheets)
                {
                    if (Slice(sheet)) sliced++; else failed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            string msg = $"[GasStationSheetSlicer] {sliced} sheets sliced, {failed} failed.";
            if (failed > 0) Debug.LogError(msg); else Debug.Log(msg);
        }

        public static GasStationKit.Contract LoadContract()
        {
            string abs = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
                                      GasStationKit.ContractPath);
            if (!File.Exists(abs))
            {
                Debug.LogError($"[GasStationSheetSlicer] No contract at {GasStationKit.ContractPath}. " +
                               "Bake the kit first — slicing against a stale or absent contract is how " +
                               "a sheet gets rects that do not match its pixels.");
                return null;
            }

            return JsonUtility.FromJson<GasStationKit.Contract>(File.ReadAllText(abs));
        }

        public static bool Slice(GasStationKit.SheetEntry sheet)
        {
            string path = $"{GasStationKit.OutputFolder}/{sheet.key}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[GasStationSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return false;
            }

            // ⚠️ THE CAP FIRST, and with a REIMPORT, before anything reads the texture. Setting the
            // field alone does nothing — that is a real bug this repo has shipped twice: a sheet over
            // maxTextureSize imports DOWNSCALED, the rects are authored in source pixels, the reimport
            // refits them to the smaller texture, and they come back alpha-trimmed with the pivot
            // discarded. The sprite COUNT still matches, so only the size and pivot asserts in
            // VerifyAll catch it.
            int needed = Mathf.NextPowerOfTwo(Mathf.Max(sheet.sheetW, sheet.sheetH));
            if (needed > GasStationKit.ImportSizeCap)
            {
                Debug.LogError($"[GasStationSheetSlicer] '{sheet.key}' is {sheet.sheetW}×{sheet.sheetH}, " +
                               $"needing a {needed} px import over the kit's " +
                               $"{GasStationKit.ImportSizeCap} px cap. The baker packs to stay under it, " +
                               "so this means the contract and the PNG have come apart. Re-bake rather " +
                               "than raising the cap. Not slicing.");
                return false;
            }

            if (importer.maxTextureSize < needed)
            {
                importer.maxTextureSize = needed;
                importer.SaveAndReimport();
            }

            LockImportSettings(importer);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // ⚠️ Re-use the spriteID any already-sliced name carries. Generating fresh GUIDs on every
            // run rewrites every spriteID in every .meta — pure diff noise that buries real changes.
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(gr => gr.Key, gr => gr.First().spriteID);

            var rects = BuildRects(sheet, existingIds);
            dp.SetSpriteRects(rects);

            var nameIds = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIds?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// The locked import settings for every sheet in this kit: PPU 32, Point filter, no
        /// compression, no mips. Scale is honest — a sprite's pixels are its metres × 32.
        ///
        /// <para>⚠️ Written through the PROPERTY and never followed by a <c>SetTextureSettings</c> of a
        /// separately-read settings object. Those two APIs write one backing field and the last one
        /// applied wins, so the pattern "read settings, set the property, apply the settings" restores
        /// the OLD mode silently and deterministically on every run.</para>
        /// </summary>
        public static void LockImportSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = GasStationKit.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
        }

        /// <summary>
        /// One rect per baked cell, in the grid the baker blitted: left to right, then top to bottom.
        /// Unity's rects are BOTTOM-origin, so row 0 maps to the highest y.
        /// </summary>
        public static SpriteRect[] BuildRects(GasStationKit.SheetEntry sheet,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            var pivot = sheet.NormalisedPivot;
            var rects = new List<SpriteRect>(sheet.facings);

            for (int cell = 0; cell < sheet.facings; cell++)
            {
                string name = sheet.SpriteName(cell);
                rects.Add(new SpriteRect
                {
                    name = name,
                    spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                                   ? id : GUID.Generate(),
                    rect = new Rect(sheet.ColumnOf(cell) * sheet.cellW,
                                    (sheet.rows - 1 - sheet.RowOf(cell)) * sheet.cellH,
                                    sheet.cellW, sheet.cellH),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    border = Vector4.zero,
                });
            }

            return rects.ToArray();
        }

        /// <summary>
        /// Verifies every sheet imported at native resolution and carries the rects the contract
        /// describes.
        ///
        /// <para><b>spriteMode Multiple is NOT the same as sliced.</b> A fresh import is Multiple with
        /// ZERO rects, and every downstream <c>LoadAllAssetsAtPath</c> then returns nothing while the
        /// importer reports exactly what you asked for. This is the check that tells the two
        /// apart.</para>
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Verify Gas Station Slices", priority = 151)]
        public static void VerifyAll()
        {
            var contract = LoadContract();
            if (contract == null) return;

            Debug.Log(Verify(contract, out bool ok));
            if (!ok) Debug.LogError("[GasStationSheetSlicer] verification FAILED — see above.");
        }

        /// <summary>The verification body, split out so an EditMode test can assert on it.</summary>
        public static string Verify(GasStationKit.Contract contract, out bool ok)
        {
            var problems = new List<string>();

            foreach (var sheet in contract.sheets)
            {
                string path = $"{GasStationKit.OutputFolder}/{sheet.key}.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) { problems.Add($"{sheet.key}: not imported"); continue; }

                if (tex.width != sheet.sheetW || tex.height != sheet.sheetH)
                    problems.Add($"{sheet.key}: imported {tex.width}×{tex.height} but the contract " +
                                 $"baked {sheet.sheetW}×{sheet.sheetH} — a SILENT downscale, which " +
                                 "invalidates every rect on the sheet.");

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                if (sprites.Length != sheet.facings)
                    problems.Add($"{sheet.key}: {sprites.Length} sprites, expected {sheet.facings}. " +
                                 "Multiple-mode with no rects reads as 'imported' everywhere else.");

                foreach (var s in sprites)
                {
                    var want = sheet.NormalisedPivot;
                    var got = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
                    if (Mathf.Abs(got.x - want.x) > 1e-3f || Mathf.Abs(got.y - want.y) > 1e-3f)
                    {
                        problems.Add($"{sheet.key}/{s.name}: pivot {got} but the contract says {want}. " +
                                     "The pivot is the ground centre of the piece's own footprint; a " +
                                     "wrong one stands it in the dirt without any error — and on a " +
                                     "storefront it also breaks the seam with its own sales floor.");
                        break;
                    }
                }
            }

            ok = problems.Count == 0;
            return ok
                ? $"[GasStationSheetSlicer] All {contract.sheets.Length} sheets verified: native " +
                  "resolution, full rect count, contract pivots."
                : $"[GasStationSheetSlicer] {problems.Count} problems:\n  " +
                  string.Join("\n  ", problems);
        }
    }
}
