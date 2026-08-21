using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Slices the baked boat-interior sheets into named sprites and locks their import settings.
    ///
    /// <para>Sheet layout is <b>columns = facings, rows = cells wrapped from a level-major order</b>,
    /// and the sprite name carries both: <c>lobsterInshoreHardtopFundyIso_cuddy_d3</c> is that boat's
    /// forepeak berth seen from the south-east. The runtime picks a level; heading picks a column.</para>
    ///
    /// <para><b>Every sprite on a hull's sheets carries ONE pivot — the hull's own.</b> That is the
    /// whole product: the interior composites under the exterior 1:1 because both are drawn into the
    /// same cell about the same point. <see cref="VerifyAll"/> asserts it per sprite, because a pivot
    /// that quietly reverts to the importer's default still produces a sheet full of correct-looking
    /// pictures.</para>
    /// </summary>
    public static class BoatInteriorSheetSlicer
    {
        [MenuItem("Hidden Harbours/Art/Slice Boat Interior Sheets", priority = 79)]
        public static void SliceAll()
        {
            bool ok = SliceAllInternal(out string report);
            if (ok) Debug.Log(report); else Debug.LogError(report);
        }

        [MenuItem("Hidden Harbours/Dev/Verify Boat Interior Slices", priority = 156)]
        public static void VerifyAll()
        {
            bool ok = VerifyAllInternal(out string report);
            if (ok) Debug.Log(report); else Debug.LogError(report);
        }

        public static BoatInteriorKit.Contract LoadContract()
        {
            string abs = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                      BoatInteriorKit.ContractPath);
            if (!File.Exists(abs))
            {
                Debug.LogError($"[BoatInteriorSheetSlicer] No contract at {BoatInteriorKit.ContractPath}. " +
                               "Bake the kit first — slicing against a stale or absent contract is how a " +
                               "sheet gets rects that do not match its pixels.");
                return null;
            }

            var contract = JsonUtility.FromJson<BoatInteriorKit.Contract>(File.ReadAllText(abs));
            if (contract != null && contract.schema != BoatInteriorKit.Schema)
            {
                Debug.LogError($"[BoatInteriorSheetSlicer] the contract's schema is '{contract.schema}', " +
                               $"not '{BoatInteriorKit.Schema}'. Re-bake rather than slicing against a " +
                               "shape this slicer was not written for.");
                return null;
            }
            return contract;
        }

        // ---- slicing ----------------------------------------------------------------------------

        public static bool SliceAllInternal(out string report)
        {
            var contract = LoadContract();
            if (contract == null) { report = "[BoatInteriorSheetSlicer] no contract."; return false; }

            int sliced = 0;
            var problems = new List<string>();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var sheet in contract.sheets)
                    foreach (var page in sheet.pages)
                        if (Slice(sheet, page, problems)) sliced++;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            var sb = new StringBuilder($"[BoatInteriorSheetSlicer] {sliced} page(s) sliced across " +
                                       $"{contract.sheets.Length} hull(s), {problems.Count} problem(s).");
            foreach (string p in problems) sb.Append("\n  ! " + p);
            report = sb.ToString();
            return problems.Count == 0;
        }

        public static bool Slice(BoatInteriorKit.SheetEntry sheet, BoatInteriorKit.SheetPage page,
                                 List<string> problems)
        {
            string path = page.AssetPath;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                problems.Add($"'{path}' has no TextureImporter — not imported, or not a texture.");
                return false;
            }

            // ⚠️ THE CAP FIRST, and with a SaveAndReimport, before anything reads the texture.
            //
            // A sheet over the importer's maxTextureSize imports DOWNSCALED, and the downscale is
            // SILENT: the rects are authored in source pixels, the reimport refits them to the smaller
            // texture, and they come back alpha-trimmed with the pivot thrown away — while the sprite
            // COUNT still looks exactly right. Setting the field is NOT the fix on its own; a slicer
            // that only ever handled small sheets has never exercised this path, and this kit's packet
            // pages are 2112 px wide, past the importer's 2048 default, on the very first run.
            int needed = Mathf.NextPowerOfTwo(Mathf.Max(page.sheetW, page.sheetH));
            if (needed > BoatInteriorKit.ImportSizeCap)
            {
                problems.Add($"'{page.file}' is {page.sheetW}×{page.sheetH}, needing a {needed} px " +
                             $"import over the kit's {BoatInteriorKit.ImportSizeCap} px cap. Re-bake — " +
                             "do not raise the cap here, or the bake and the import stop agreeing.");
                return false;
            }
            if (importer.maxTextureSize < needed)
            {
                importer.maxTextureSize = needed;
                importer.SaveAndReimport();
            }

            LockImportSettings(importer, sheet);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // ⚠️ Re-use the spriteID any already-sliced name carries. Fresh GUIDs on every run rewrite
            // every spriteID in every .meta — pure diff noise that buries real changes, and it breaks
            // any serialized reference that resolved by internalID.
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(gr => gr.Key, gr => gr.First().spriteID);

            SpriteRect[] rects = BuildRects(sheet, page, existingIds);
            dp.SetSpriteRects(rects);

            var nameIds = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIds?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// The locked import settings for this kit: Point filter, no compression, no mips — and
        /// <b>pixels-per-unit taken from the hull's own px/m</b>.
        ///
        /// <para>⚠️ <b>PPU is per hull and is NOT the canon 32.</b> The tanker renders at 16 px/m
        /// (ADR 0038 Proposal 2: "px/m per def, builder refuses an omission"), so her sheet at PPU 32
        /// would place a 110 m ship's cabins at 55 m — half scale, in perfect focus, with nothing to
        /// catch it but the eye. <see cref="ArtImportPipeline"/>'s lock is a floor stamped on first
        /// import, not a cage; this is the deliberate per-asset override it makes room for.</para>
        /// </summary>
        public static void LockImportSettings(TextureImporter importer, BoatInteriorKit.SheetEntry sheet)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = sheet.pixelsPerMetre;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
        }

        /// <summary>
        /// One rect per cell on this page, row-major from the TOP-LEFT — the order the baker blits.
        /// Unity's rects are BOTTOM-origin, so row 0 maps to the highest y.
        /// </summary>
        public static SpriteRect[] BuildRects(BoatInteriorKit.SheetEntry sheet,
                                              BoatInteriorKit.SheetPage page,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            Vector2 pivot = sheet.NormalisedPivot;
            var rects = new List<SpriteRect>(page.cellCount);

            for (int i = 0; i < page.cellCount; i++)
            {
                int flat = page.firstCell + i;
                int levelIndex = flat / sheet.facings;
                int facing = flat % sheet.facings;

                BoatInteriorKit.Slot(i, page.columns, out int col, out int rowFromTop);
                string name = sheet.SpriteName(levelIndex, facing);

                rects.Add(new SpriteRect
                {
                    name = name,
                    spriteID = existingIds != null && existingIds.TryGetValue(name, out GUID id)
                                   ? id : GUID.Generate(),
                    rect = new Rect(col * sheet.cellW,
                                    (page.rows - 1 - rowFromTop) * sheet.cellH,
                                    sheet.cellW, sheet.cellH),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    border = Vector4.zero,
                });
            }

            return rects.ToArray();
        }

        // ---- verification -----------------------------------------------------------------------

        /// <summary>
        /// Every page imported at native resolution, with the rects and the ONE pivot the contract
        /// describes.
        ///
        /// <para><b>spriteMode Multiple is NOT the same as sliced.</b> A fresh import is Multiple with
        /// ZERO rects, and every downstream <c>LoadAllAssetsAtPath</c> then returns nothing while the
        /// importer reports exactly what you asked for. This is the check that tells the two apart —
        /// along with the size check, which is what catches a silent downscale.</para>
        /// </summary>
        public static bool VerifyAllInternal(out string report)
        {
            var contract = LoadContract();
            if (contract == null) { report = "[BoatInteriorSheetSlicer] no contract."; return false; }

            var problems = new List<string>();
            int pages = 0, sprites = 0;

            foreach (var sheet in contract.sheets)
            {
                foreach (var page in sheet.pages)
                {
                    pages++;
                    string path = page.AssetPath;

                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex == null) { problems.Add($"{page.file}: not imported"); continue; }

                    if (tex.width != page.sheetW || tex.height != page.sheetH)
                        problems.Add($"{page.file}: imported {tex.width}×{tex.height} but the contract " +
                                     $"baked {page.sheetW}×{page.sheetH} — a SILENT downscale, which " +
                                     "invalidates every rect on the sheet.");

                    var found = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                    sprites += found.Length;

                    if (found.Length != page.cellCount)
                        problems.Add($"{page.file}: {found.Length} sprites, expected {page.cellCount}. " +
                                     "Multiple-mode with no rects reads as 'imported' everywhere else.");

                    Vector2 want = sheet.NormalisedPivot;
                    foreach (var s in found)
                    {
                        if (Mathf.RoundToInt(s.rect.width) != sheet.cellW ||
                            Mathf.RoundToInt(s.rect.height) != sheet.cellH)
                        {
                            problems.Add($"{page.file}/{s.name}: rect {s.rect.width}×{s.rect.height} " +
                                         $"but the hull's cell is {sheet.cellW}×{sheet.cellH}. The " +
                                         "interior must occupy the FULL hull cell or it does not " +
                                         "composite under her.");
                            break;
                        }

                        var got = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
                        if (Mathf.Abs(got.x - want.x) > 1e-3f || Mathf.Abs(got.y - want.y) > 1e-3f)
                        {
                            problems.Add($"{page.file}/{s.name}: pivot {got} but the hull's is {want}. " +
                                         "Every cell of every level shares the hull's one pivot; a cell " +
                                         "that does not sits her cabin off her deck.");
                            break;
                        }
                    }

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null &&
                        !Mathf.Approximately(importer.spritePixelsPerUnit, sheet.pixelsPerMetre))
                        problems.Add($"{page.file}: imported at {importer.spritePixelsPerUnit} PPU but " +
                                     $"this hull renders at {sheet.pixelsPerMetre} px/m. Two pixel grids " +
                                     "live in this kit — the tanker at 16, the fleet at 32 — and PPU is " +
                                     "how a sheet states which one it is on.");
                }
            }

            var sb = new StringBuilder(
                $"[BoatInteriorSheetSlicer] verified {contract.sheets.Length} hull(s), {pages} page(s), " +
                $"{sprites} sprite(s): {(problems.Count == 0 ? "native resolution, full rect count, one pivot per hull, per-hull PPU." : problems.Count + " problem(s):")}");
            foreach (string p in problems) sb.Append("\n  ! " + p);
            report = sb.ToString();
            return problems.Count == 0;
        }
    }
}
