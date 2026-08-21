#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Slices the baked dooryard sheets into named sprites and locks their import settings.
    ///
    /// <para>Layout is <b>columns = facings, one row</b>, and the sprite name carries the facing:
    /// <c>Yard_picketPanel_d2</c> is a picket panel at the third baked cell, which the contract's
    /// measured table says faces EAST. A placer picks a column through <see cref="YardCatalog"/>;
    /// nothing picks one by eye.</para>
    /// </summary>
    public static class YardSheetSlicer
    {
        [MenuItem("Hidden Harbours/Art/Slice Yard Kit Sheets", priority = 77)]
        public static void SliceAll()
        {
            var contract = YardKit.Load();
            if (contract == null) return;

            int sliced = 0, failed = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var e in contract.pieces)
                {
                    if (Slice(e)) sliced++; else failed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            string msg = $"[YardSheetSlicer] {sliced} sheet(s) sliced, {failed} failed.";
            if (failed > 0) Debug.LogError(msg); else Debug.Log(msg);
        }

        public static bool Slice(YardKit.Entry e)
        {
            string path = YardKit.SheetPath(e.key);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[YardSheetSlicer] '{path}' has no TextureImporter — skipping. Bake " +
                               "before slicing.");
                return false;
            }

            // ⚠️ THE CAP FIRST, before anything reads the texture. A sheet over the importer's
            // maxTextureSize imports DOWNSCALED, and the downscale is SILENT: the rects are authored in
            // source pixels, the reimport refits them to the smaller texture, and they come back
            // alpha-trimmed with the pivot thrown away. The sprite COUNT still looks right.
            // ⚠️ Writing maxTextureSize does NOTHING until the asset is REIMPORTED.
            int needed = Mathf.NextPowerOfTwo(Mathf.Max(e.sheetW, e.sheetH));
            if (needed > YardKit.ImportSizeCap)
            {
                Debug.LogError($"[YardSheetSlicer] '{e.key}' is {e.sheetW}×{e.sheetH}, needing a " +
                               $"{needed} px import over the kit's {YardKit.ImportSizeCap} px cap. The " +
                               "rig's native buffer is 470 px wide, so a sheet this size means the rig " +
                               "or the len dial changed — re-measure rather than raising the cap. Not " +
                               "slicing.");
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

            // ⚠️ Re-use the spriteID any already-sliced name carries. Generating fresh GUIDs on every run
            // rewrites every spriteID in every .meta — pure diff noise that buries real changes, and
            // sprite references resolve by internalID rather than by these anyway.
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(gr => gr.Key, gr => gr.First().spriteID);

            var rects = BuildRects(e, existingIds);
            dp.SetSpriteRects(rects);

            var nameIds = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIds?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// The locked import settings for every sheet in this family: PPU 32, Point filter, no
        /// compression, no mips. Scale is honest — a sprite's pixels are its metres × 32, so a fence
        /// panel stands the length the rig drew it and the placer's tiling arithmetic is in metres.
        /// </summary>
        public static void LockImportSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = YardKit.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
        }

        /// <summary>
        /// One rect per facing column, left to right. Unity's rects are BOTTOM-origin and the baker
        /// blits from the top, so the single row sits at y 0.
        /// </summary>
        public static SpriteRect[] BuildRects(YardKit.Entry e,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            var pivot = e.NormalisedPivot;
            int rows = Mathf.Max(1, e.rows);
            var rects = new List<SpriteRect>(e.columns * rows);

            for (int row = 0; row < rows; row++)
                for (int col = 0; col < e.columns; col++)
                {
                    string name = e.SpriteName(col);
                    rects.Add(new SpriteRect
                    {
                        name = name,
                        spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                                   ? id : GUID.Generate(),
                        rect = new Rect(col * e.cellW, (rows - 1 - row) * e.cellH, e.cellW, e.cellH),
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
        /// importer reports exactly what you asked for. This is the check that tells the two apart.
        /// </para>
        /// </summary>
        [MenuItem("Hidden Harbours/Dev/Verify Yard Kit Slices", priority = 155)]
        public static void VerifyAll()
        {
            var problems = Verify(out int checkedSheets);
            if (problems.Count == 0)
                Debug.Log($"[YardSheetSlicer] All {checkedSheets} sheet(s) verified: native resolution, " +
                          "full rect count, contract pivots, locked import settings.");
            else
                Debug.LogError($"[YardSheetSlicer] {problems.Count} problem(s):\n  " +
                               string.Join("\n  ", problems));
        }

        /// <summary>The verification, as data — so a test can assert on it instead of scraping a log.
        /// </summary>
        public static List<string> Verify(out int checkedSheets)
        {
            checkedSheets = 0;
            var problems = new List<string>();
            var contract = YardKit.Load();
            if (contract == null)
            {
                problems.Add("no contract");
                return problems;
            }

            foreach (var e in contract.pieces)
            {
                checkedSheets++;
                string path = YardKit.SheetPath(e.key);

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) { problems.Add($"{e.key}: not imported ({path})"); continue; }

                if (tex.width != e.sheetW || tex.height != e.sheetH)
                    problems.Add($"{e.key}: imported {tex.width}×{tex.height} but the contract baked " +
                                 $"{e.sheetW}×{e.sheetH} — a SILENT downscale, which invalidates every " +
                                 "rect on the sheet.");

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { problems.Add($"{e.key}: no TextureImporter"); continue; }

                if (!Mathf.Approximately(importer.spritePixelsPerUnit, YardKit.PixelsPerUnit))
                    problems.Add($"{e.key}: PPU {importer.spritePixelsPerUnit}, expected " +
                                 $"{YardKit.PixelsPerUnit} — every panel would tile at the wrong length, " +
                                 "and the placer's metres are the rig's metres.");
                if (importer.filterMode != FilterMode.Point)
                    problems.Add($"{e.key}: filter {importer.filterMode}, expected Point");
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    problems.Add($"{e.key}: compression {importer.textureCompression}, expected Uncompressed");
                if (importer.mipmapEnabled)
                    problems.Add($"{e.key}: mipmaps enabled");
                if (importer.spriteImportMode != SpriteImportMode.Multiple)
                    problems.Add($"{e.key}: spriteImportMode {importer.spriteImportMode}, expected Multiple");

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                int expected = e.columns * Mathf.Max(1, e.rows);
                if (sprites.Length != expected)
                {
                    problems.Add($"{e.key}: {sprites.Length} sprites, expected {expected}. " +
                                 "Multiple-mode with no rects reads as 'imported' everywhere else.");
                    continue;
                }

                for (int facing = 0; facing < e.facings; facing++)
                {
                    string want = e.SpriteName(facing);
                    var s = sprites.FirstOrDefault(sp => string.Equals(sp.name, want, StringComparison.Ordinal));
                    if (s == null) { problems.Add($"{e.key}: no sprite named '{want}'"); continue; }

                    if (Mathf.RoundToInt(s.rect.width) != e.cellW ||
                        Mathf.RoundToInt(s.rect.height) != e.cellH)
                        problems.Add($"{e.key}/{want}: rect {s.rect.width}×{s.rect.height}, contract cell " +
                                     $"{e.cellW}×{e.cellH}");

                    var wantPivot = e.NormalisedPivot;
                    var got = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
                    if (Mathf.Abs(got.x - wantPivot.x) > 1e-3f || Mathf.Abs(got.y - wantPivot.y) > 1e-3f)
                        problems.Add($"{e.key}/{want}: pivot {got} but the contract says {wantPivot}. " +
                                     "The pivot is the piece's ground centre; a bottom-centre one buries " +
                                     "it in the lawn without any error.");
                }
            }

            return problems;
        }
    }
}
#endif
