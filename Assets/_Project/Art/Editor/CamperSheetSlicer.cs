using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Slices the baked camper sheets into named sprites and locks their import settings.
    ///
    /// <para>Sheet layout is <b>columns = door-swing frames, rows = facings</b>, and the sprite name
    /// carries both: <c>camper_clipper_enter_d6_s3</c> is the 26-footer at sheet facing 6, three
    /// frames into the door cue. A <c>rest</c> sheet has one column and drops the frame index, so its
    /// names end at the facing — <c>camper_clipper_rest_d6</c> — which is what makes the parked frame
    /// the unique match for the <c>_d&lt;facing&gt;</c> suffix every kit in this repo bakes to.</para>
    /// </summary>
    public static class CamperSheetSlicer
    {
        [MenuItem("Hidden Harbours/Art/Slice Camper Sheets", priority = 75)]
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

            string msg = $"[CamperSheetSlicer] {sliced} sheets sliced, {failed} failed.";
            if (failed > 0) Debug.LogError(msg); else Debug.Log(msg);
        }

        public static CamperKit.Contract LoadContract()
        {
            string abs = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName,
                                      CamperKit.ContractPath);
            if (!File.Exists(abs))
            {
                Debug.LogError($"[CamperSheetSlicer] No contract at {CamperKit.ContractPath}. Bake " +
                               "the kit first — slicing against a stale or absent contract is how a " +
                               "sheet gets rects that do not match its pixels.");
                return null;
            }
            return JsonUtility.FromJson<CamperKit.Contract>(File.ReadAllText(abs));
        }

        public static bool Slice(CamperKit.SheetEntry sheet)
        {
            string path = $"{CamperKit.OutputFolder}/{sheet.key}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[CamperSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return false;
            }

            // ⚠️ THE CAP FIRST, before anything reads the texture. A sheet over the importer's
            // maxTextureSize imports DOWNSCALED, and the downscale is SILENT: the rects are authored
            // in source pixels, the reimport refits them to the smaller texture, and they come back
            // alpha-trimmed with the pivot thrown away. The sprite COUNT still looks right.
            int needed = Mathf.NextPowerOfTwo(Mathf.Max(sheet.sheetW, sheet.sheetH));
            if (needed > CamperKit.ImportSizeCap)
            {
                Debug.LogError($"[CamperSheetSlicer] '{sheet.key}' is {sheet.sheetW}×{sheet.sheetH}, " +
                               $"needing a {needed} px import over the kit's " +
                               $"{CamperKit.ImportSizeCap} px cap. Re-bake with fewer cue frames " +
                               "rather than raising the cap. Not slicing.");
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
            // run rewrites every spriteID in every .meta — pure diff noise that buries real changes,
            // and sprite references resolve by internalID anyway.
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
        /// </summary>
        public static void LockImportSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = CamperKit.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
        }

        /// <summary>
        /// One rect per (facing row, swing column), row-major from the TOP-LEFT cell — the order the
        /// baker blits. Unity's rects are BOTTOM-origin, so row 0 maps to the highest y.
        /// </summary>
        public static SpriteRect[] BuildRects(CamperKit.SheetEntry sheet,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            var pivot = sheet.NormalisedPivot;
            var rects = new List<SpriteRect>(sheet.columns * sheet.rows);

            for (int facing = 0; facing < sheet.rows; facing++)
                for (int frame = 0; frame < sheet.columns; frame++)
                {
                    string name = sheet.SpriteName(facing, frame);
                    rects.Add(new SpriteRect
                    {
                        name = name,
                        spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                                   ? id : GUID.Generate(),
                        rect = new Rect(frame * sheet.cellW,
                                        (sheet.rows - 1 - facing) * sheet.cellH,
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
        /// importer reports exactly what you asked for. This is the check that tells the two apart.</para>
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Verify Camper Slices", priority = 152)]
        public static void VerifyAll()
        {
            var contract = LoadContract();
            if (contract == null) return;

            var problems = new List<string>();
            foreach (var sheet in contract.sheets)
            {
                string path = $"{CamperKit.OutputFolder}/{sheet.key}.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) { problems.Add($"{sheet.key}: not imported"); continue; }

                if (tex.width != sheet.sheetW || tex.height != sheet.sheetH)
                    problems.Add($"{sheet.key}: imported {tex.width}×{tex.height} but the contract " +
                                 $"baked {sheet.sheetW}×{sheet.sheetH} — a SILENT downscale, which " +
                                 "invalidates every rect on the sheet.");

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                int expected = sheet.columns * sheet.rows;
                if (sprites.Length != expected)
                    problems.Add($"{sheet.key}: {sprites.Length} sprites, expected {expected}. " +
                                 "Multiple-mode with no rects reads as 'imported' everywhere else.");

                foreach (var s in sprites)
                {
                    var want = sheet.NormalisedPivot;
                    var got = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
                    if (Mathf.Abs(got.x - want.x) > 1e-3f || Mathf.Abs(got.y - want.y) > 1e-3f)
                    {
                        problems.Add($"{sheet.key}/{s.name}: pivot {got} but the contract says {want}. " +
                                     "The pivot is the ground-centre of the camper's BODY footprint, " +
                                     "tongue excluded; a wrong one parks it in the dirt with no error.");
                        break;
                    }
                }
            }

            if (problems.Count == 0)
                Debug.Log($"[CamperSheetSlicer] All {contract.sheets.Length} sheets verified: native " +
                          "resolution, full rect count, contract pivots.");
            else
                Debug.LogError($"[CamperSheetSlicer] {problems.Count} problems:\n  " +
                               string.Join("\n  ", problems));
        }
    }
}
