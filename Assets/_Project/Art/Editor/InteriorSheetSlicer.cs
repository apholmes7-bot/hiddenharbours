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
    /// Slices and IMPORT-LOCKS the interior kit's sheets — <c>Interior_&lt;room&gt;.png</c> and
    /// <c>InteriorProp_&lt;prop&gt;.png</c> — baked by <c>InteriorBakeMenu</c>. Sibling of
    /// <see cref="VillageBuildingSheetSlicer"/>.
    ///
    /// <para>Every number comes from the kit's own <c>Interiors.json</c> — cell, grid, and the
    /// floor-centre pivot. There is no manifest here to drift from the rig, which is the whole point of
    /// writing the contract in the same bake.</para>
    ///
    /// <para><b>Two settings are load-bearing and both fail SILENTLY:</b></para>
    /// <list type="bullet">
    ///   <item>🔴 <b>The pivot is the FLOOR CENTRE, not bottom-centre.</b> <see cref="ArtImportPipeline"/>
    ///   has per-folder defaults and none of them know what a room is; under the ¾ camera the near half
    ///   of the floor projects below the room's ground centre, so bottom-centre stands the room that far
    ///   into the dirt. Nothing throws; it reads as an art bug.
    ///   <see cref="InteriorKit.BelowGroundPad"/> is the number.</item>
    ///   <item>🔴 <b>NATIVE RESOLUTION IS ASSERTED, not assumed.</b> Over the importer's cap Unity
    ///   DOWNSCALES the sheet and the sprite COUNT still comes out right, so the only thing that catches
    ///   it is a cell-size assert against the contract. The room rig's native cell is 1180×900 and eight
    ///   of those uncropped would be 9440 px wide, so this kit is one bad crop away from the trap at all
    ///   times. <see cref="VerifyAll"/> checks every sliced rect against the contract's cell, on every
    ///   sheet, room and prop alike.</item>
    /// </list>
    ///
    /// <para>Mesh type stays <see cref="SpriteMeshType.FullRect"/>: neither rooms nor props have a
    /// vertex-evaluated shader (contrast the tree kit's wind bend, which needs Tight), and a quad is the
    /// cheaper mesh when a room is one big sprite and a village is a few dozen of them.</para>
    /// </summary>
    public static class InteriorSheetSlicer
    {
        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Slice Interior Sheets", priority = 203)]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int failed);
            if (failed > 0)
                Debug.LogError($"[InteriorSheetSlicer] Sliced {n} sheet(s), {failed} FAILED — see the " +
                               "errors above.");
            else
                Debug.Log($"[InteriorSheetSlicer] Sliced {n} interior sheet(s). Next: rebuild St Peters " +
                          "(Hidden Harbours ▸ Dev ▸ Build St Peters) and press Play.");
        }

        /// <summary>Batch entry point for <c>-executeMethod</c> — exits non-zero on any failure so a
        /// headless bake fails loudly instead of committing a half-sliced kit.</summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int failed);
                Debug.Log($"[InteriorSheetSlicer] (batch) Sliced {n} sheet(s) ({failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[InteriorSheetSlicer] {failed} sheet(s) failed — see above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[InteriorSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Slice + import-lock every sheet the contract claims. A PNG in a kit folder that the contract
        /// does not name is a FAILURE, not a skip: with no contract entry it has no cell and no pivot,
        /// and guessing either is how a room ends up standing somewhere it was not placed.
        /// </summary>
        public static int SliceAll(out int failed)
        {
            failed = 0;

            InteriorKit.Contract contract = InteriorKit.Load();
            if (contract == null) { failed = 1; return 0; }

            int sliced = 0;
            sliced += SliceFolder(InteriorKit.RoomsRoot, stem => InteriorKit.RoomForStem(contract, stem),
                                  InteriorKit.RoomStemFor, InteriorKit.RoomSpriteNameFor,
                                  contract.rooms, ref failed);
            sliced += SliceFolder(InteriorKit.PropsRoot, stem => InteriorKit.PropForStem(contract, stem),
                                  InteriorKit.PropStemFor, InteriorKit.PropSpriteNameFor,
                                  contract.props, ref failed);

            AssetDatabase.SaveAssets();
            return sliced;
        }

        static int SliceFolder(string root, Func<string, InteriorKit.Entry> entryForStem,
                               Func<string, string> stemFor, Func<string, int, string> spriteNameFor,
                               InteriorKit.Entry[] claimed, ref int failed)
        {
            if (!Directory.Exists(root))
            {
                if (claimed != null && claimed.Length > 0)
                {
                    Debug.LogError($"[InteriorSheetSlicer] The contract claims {claimed.Length} sheet(s) " +
                                   $"under '{root}' but the folder is not there. Re-bake.");
                    failed++;
                }
                return 0;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { root.TrimEnd('/') });
            int sliced = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(root, StringComparison.Ordinal)) continue;
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                string stem = Path.GetFileNameWithoutExtension(path);
                InteriorKit.Entry entry = entryForStem(stem);
                if (entry == null)
                {
                    Debug.LogError(
                        $"[InteriorSheetSlicer] '{stem}' is under {root} but no entry in " +
                        $"{InteriorKit.ContractFileName} claims it. Not slicing — a sheet with no " +
                        "contract has no cell and no pivot, and guessing either is how a room ends up " +
                        "in the wrong place. Re-bake the kit, or move the stray sheet out.");
                    failed++;
                    continue;
                }

                seen.Add(entry.key);
                if (SliceOne(path, stem, entry, spriteNameFor)) sliced++;
                else failed++;
            }

            // The other direction: a contract entry with no sheet on disk.
            if (claimed != null)
                foreach (var entry in claimed)
                {
                    if (seen.Contains(entry.key)) continue;
                    Debug.LogError($"[InteriorSheetSlicer] {InteriorKit.ContractFileName} claims " +
                                   $"'{entry.key}' but {root}{stemFor(entry.key)}.png is not there. " +
                                   "Re-bake.");
                    failed++;
                }

            return sliced;
        }

        /// <summary>Slice + import-lock ONE sheet against its contract entry.</summary>
        public static bool SliceOne(string path, string stem, InteriorKit.Entry entry,
                                    Func<string, int, string> spriteNameFor)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[InteriorSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return false;
            }

            // Stamp the import settings BEFORE reading the texture: the cap and mode changes force a
            // reimport, and a texture read taken before that is stale.
            ApplyImportSettings(importer);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[InteriorSheetSlicer] '{path}' failed to load as Texture2D.");
                return false;
            }

            if (tex.width != entry.sheetW || tex.height != entry.sheetH)
            {
                Debug.LogError(
                    $"[InteriorSheetSlicer] '{stem}' is {tex.width}×{tex.height} but the contract says " +
                    $"{entry.sheetW}×{entry.sheetH} ({entry.cols}×{entry.rows} cells of " +
                    $"{entry.cellW}×{entry.cellH}). Not slicing — re-bake, do not edit " +
                    $"{InteriorKit.ContractFileName}.");
                return false;
            }
            if (tex.width > InteriorKit.ImportSizeCap || tex.height > InteriorKit.ImportSizeCap)
            {
                Debug.LogError(
                    $"[InteriorSheetSlicer] '{stem}' is {tex.width}×{tex.height}, over the " +
                    $"{InteriorKit.ImportSizeCap} px import cap — Unity has already DOWNSCALED it and " +
                    "the sprite count would still come out right. Not slicing. The bake solves the grid " +
                    "against this same constant, so a sheet arriving over it means the two disagree: " +
                    "raise InteriorKit.ImportSizeCap (one number; the pack, the lock and the verify all " +
                    "follow it) and re-bake.");
                return false;
            }
            if (entry.cellW <= 0 || entry.cellH <= 0)
            {
                Debug.LogError($"[InteriorSheetSlicer] '{stem}' has a {entry.cellW}×{entry.cellH} cell " +
                               "in the contract. Not slicing.");
                return false;
            }

            int cols = tex.width / entry.cellW;
            int rows = tex.height / entry.cellH;
            if (cols != entry.cols || rows != entry.rows)
            {
                Debug.LogError(
                    $"[InteriorSheetSlicer] '{stem}' divides into {cols}×{rows} cells of " +
                    $"{entry.cellW}×{entry.cellH} but the contract declares {entry.cols}×{entry.rows}. " +
                    "Not slicing — the grid and the cell disagree, so one of them is stale.");
                return false;
            }

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // Re-use existing spriteIDs so re-slicing unchanged art is a no-op on the .meta. Fresh GUIDs
            // every run rewrite every spriteID — pure diff noise that buries the owner's real changes.
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(entry, spriteNameFor, existingIds);
            dp.SetSpriteRects(rects);

            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[InteriorSheetSlicer] Sliced '{stem}' → {rects.Length} facing(s) " +
                      $"({entry.rows} row(s) × {entry.cols} of {entry.cellW}×{entry.cellH}), " +
                      $"floor-centre pivot {InteriorKit.NormalizedPivot(entry)} " +
                      $"({InteriorKit.BelowGroundPad(entry)} px of art below the floor line — " +
                      "bottom-centre would sink it by that much).");
            return true;
        }

        /// <summary>
        /// The kit's import lock. Starts from <see cref="ArtImportPipeline.ApplyLockedSettings"/>
        /// (PPU 32, Point, uncompressed, mips off — the canon in one place) and overrides only what
        /// canon cannot know about a room sheet: the cap is PINNED to the same constant the pack was
        /// solved against, and the sheet-level alignment stops claiming any preset corner.
        /// </summary>
        public static void ApplyImportSettings(TextureImporter importer)
        {
            ArtImportPipeline.ApplyLockedSettings(importer, importer.assetPath);

            // The cap the BAKE solved the grid against. One constant, so the two cannot disagree — the
            // #352 lesson, structural rather than remembered.
            importer.maxTextureSize = InteriorKit.ImportSizeCap;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);

            s.spriteMeshType = SpriteMeshType.FullRect;
            s.sRGBTexture = true;                 // the albedo IS colour, not data
            s.alphaIsTransparency = true;
            s.alphaSource = TextureImporterAlphaSource.FromInput;

            // The real pivot is set per SPRITE RECT (BuildRects). Leave the sheet-level default honest
            // rather than any corner preset, all of which are wrong for a floor centre.
            s.spriteAlignment = (int)SpriteAlignment.Custom;

            importer.SetTextureSettings(s);
        }

        /// <summary>
        /// The grid, row-major from the TOP-LEFT cell (Unity's rects are bottom-origin, so row 0 maps to
        /// the highest Y — the convention every slicer in the repo uses). Facing <c>i</c> depicts
        /// +45°·i because the bake already applied the measured convention.
        ///
        /// <para>Cells past <see cref="InteriorKit.Entry.facings"/> are not emitted: a grid can be wider
        /// than the facing count, and a sprite over an empty cell is a transparent rect a placement tool
        /// would happily offer.</para>
        /// </summary>
        public static SpriteRect[] BuildRects(InteriorKit.Entry entry,
                                              Func<string, int, string> spriteNameFor,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            Vector2 pivot = InteriorKit.NormalizedPivot(entry);

            var rects = new List<SpriteRect>(entry.facings);
            for (int r = 0; r < entry.rows; r++)
            {
                for (int c = 0; c < entry.cols; c++)
                {
                    int facing = r * entry.cols + c;
                    if (facing >= entry.facings) break;

                    string name = spriteNameFor(entry.key, facing);
                    rects.Add(new SpriteRect
                    {
                        name = name,
                        spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                                   ? id
                                   : GUID.Generate(),
                        rect = new Rect(c * entry.cellW, (entry.rows - 1 - r) * entry.cellH,
                                        entry.cellW, entry.cellH),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        border = Vector4.zero,
                    });
                }
            }
            return rects.ToArray();
        }

        // ---- verification ---------------------------------------------------------------------

        /// <summary>
        /// Assert every contract sheet imported the way this kit needs: Multiple-mode, the right facing
        /// count, the floor-centre pivot on every facing, the locked PPU/filter/compression, and — the
        /// one that cannot be inferred from anything else — <b>native resolution</b>. Returns true only
        /// if every sheet passes.
        ///
        /// <para>Exposed so the batch entry point and the EditMode suite share ONE definition of
        /// "imported correctly".</para>
        /// </summary>
        public static bool VerifyAll(bool logEachPass)
        {
            InteriorKit.Contract contract = InteriorKit.Load();
            if (contract == null) return false;

            bool ok = true;
            int n = 0;
            ok &= VerifySet(contract.rooms, InteriorKit.RoomSheetPath, InteriorKit.RoomStemFor,
                            InteriorKit.RoomSpriteNameFor, logEachPass, ref n);
            ok &= VerifySet(contract.props, InteriorKit.PropSheetPath, InteriorKit.PropStemFor,
                            InteriorKit.PropSpriteNameFor, logEachPass, ref n);

            int claimed = (contract.rooms?.Length ?? 0) + (contract.props?.Length ?? 0);
            Debug.Log($"[InteriorSheetSlicer] VERIFY: checked {n} sheet(s) of {claimed} claimed — " +
                      (ok ? "ALL PASS" : "FAILURES PRESENT"));
            return ok && n == claimed;
        }

        static bool VerifySet(InteriorKit.Entry[] entries, Func<string, string> sheetPath,
                              Func<string, string> stemFor, Func<string, int, string> spriteNameFor,
                              bool logEachPass, ref int checkedCount)
        {
            if (entries == null) return true;
            bool allOk = true;

            foreach (var entry in entries)
            {
                string path = sheetPath(entry.key);
                string stem = stemFor(entry.key);

                if (!File.Exists(path))
                {
                    Debug.LogError($"[InteriorSheetSlicer] VERIFY: '{path}' missing on disk.");
                    allOk = false;
                    continue;
                }
                checkedCount++;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[InteriorSheetSlicer] VERIFY: '{stem}' has no TextureImporter.");
                    allOk = false;
                    continue;
                }

                bool ok = true;
                ok &= Check(stem, "Multiple-mode", importer.spriteImportMode == SpriteImportMode.Multiple);
                ok &= Check(stem, $"PPU {ArtImportPipeline.PixelsPerUnit}",
                            Mathf.Approximately(importer.spritePixelsPerUnit,
                                                ArtImportPipeline.PixelsPerUnit));
                ok &= Check(stem, "Point filter", importer.filterMode == FilterMode.Point);
                ok &= Check(stem, "Compression None",
                            importer.textureCompression == TextureImporterCompression.Uncompressed);
                ok &= Check(stem, "mips off", !importer.mipmapEnabled);
                ok &= Check(stem, "sRGB ON (this sheet is colour, not data)",
                            InteriorKit.SRgbOf(importer));
                ok &= Check(stem, $"inside the {InteriorKit.ImportSizeCap} px cap",
                            importer.maxTextureSize >= entry.sheetW &&
                            importer.maxTextureSize >= entry.sheetH &&
                            entry.sheetW <= InteriorKit.ImportSizeCap &&
                            entry.sheetH <= InteriorKit.ImportSizeCap);

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                ok &= Check(stem, $"{entry.facings} facing sprite(s), got {sprites.Length}",
                            sprites.Length == entry.facings);

                // The two load-bearing ones. A bottom-centre pivot and a silently downscaled sheet both
                // pass every other check on this list.
                Vector2 expectedPivot = InteriorKit.PivotPixels(entry);
                foreach (var sprite in sprites)
                {
                    ok &= Check(sprite.name, $"floor-centre pivot {expectedPivot}, got {sprite.pivot}",
                                Mathf.Abs(sprite.pivot.x - expectedPivot.x) < 0.01f &&
                                Mathf.Abs(sprite.pivot.y - expectedPivot.y) < 0.01f);
                    ok &= Check(sprite.name,
                                $"NATIVE cell {entry.cellW}×{entry.cellH}, got " +
                                $"{sprite.rect.width}×{sprite.rect.height} (a sheet over the import cap " +
                                "arrives downscaled with the right sprite COUNT — this assert is the " +
                                "only thing that sees it)",
                                Mathf.RoundToInt(sprite.rect.width) == entry.cellW &&
                                Mathf.RoundToInt(sprite.rect.height) == entry.cellH);
                }

                // Names, not array order: LoadAllAssetsAtPath promises no ordering.
                for (int f = 0; f < entry.facings; f++)
                {
                    string wanted = spriteNameFor(entry.key, f);
                    ok &= Check(stem, $"a sprite named '{wanted}'",
                                sprites.Any(s => string.Equals(s.name, wanted, StringComparison.Ordinal)));
                }

                if (!ok) allOk = false;
                else if (logEachPass)
                    Debug.Log($"[InteriorSheetSlicer] VERIFY OK: {stem} = {sprites.Length} facing(s) " +
                              $"of {entry.cellW}×{entry.cellH}, pivot {expectedPivot}.");
            }

            return allOk;
        }

        public static void VerifyAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                if (!VerifyAll(logEachPass: true))
                {
                    Debug.LogError("[InteriorSheetSlicer] VERIFY FAILED — see mismatches above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[InteriorSheetSlicer] verify threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        static bool Check(string who, string what, bool ok)
        {
            if (!ok) Debug.LogError($"[InteriorSheetSlicer] VERIFY: '{who}' — expected {what}.");
            return ok;
        }
    }
}
#endif
