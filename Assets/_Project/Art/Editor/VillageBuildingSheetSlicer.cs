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
    /// Slices and IMPORT-LOCKS the village building sheets under
    /// <see cref="VillageBuildingKit.BuildingsRoot"/> — <c>Village_&lt;build&gt;.png</c>, baked by
    /// <c>VillageBuildingBakeMenu</c>.
    ///
    /// <para>Every number comes from the kit's own <c>Buildings.json</c>
    /// (<see cref="VillageBuildingKit.Load"/>) — cell, grid, and the ground-centre pivot. There is no
    /// manifest here to drift from the rig, which is the whole point of writing the contract in the same
    /// bake. Sibling of <see cref="TreeSheetSlicer"/> and <see cref="ShorePlantSheetSlicer"/>.</para>
    ///
    /// <para><b>Two settings are load-bearing and one of them fails SILENTLY:</b></para>
    /// <list type="bullet">
    ///   <item>🔴 <b>The pivot is the ground CENTRE, not bottom-centre — and the gap is METRES.</b>
    ///   <see cref="ArtImportPipeline"/> defaults everything under <c>/buildings/</c> to
    ///   <see cref="SpriteAlignment.BottomCenter"/> — right for the loose greybox cottage sprites next
    ///   door, and wrong here by the whole near HALF of the footprint, which the ¾ camera projects below
    ///   the footprint's centre. Measured on the M1 set: <b>104–209 px, 3.3–6.5 m</b>. Nothing throws;
    ///   the building simply stands that far into the dirt, and it reads as an art bug rather than an
    ///   import one. <see cref="VillageBuildingKit.BelowGroundPad"/> is the number.</item>
    ///   <item><b>The 2048 import cap is asserted, not assumed.</b> Over it Unity imports the sheet
    ///   SILENTLY DOWNSCALED and the sprite COUNT still comes out right, so the only thing that catches
    ///   it is a cell-size assert. <see cref="VillageBuildingKit.ImportSizeCap"/> says why this kit
    ///   never lifts the cap.</item>
    /// </list>
    ///
    /// <para>Mesh type stays <see cref="SpriteMeshType.FullRect"/> — the tree kit needs Tight because its
    /// wind shader evaluates a bend curve per vertex, and a building has no such shader. FullRect is also
    /// the cheaper quad, which matters when a village is a few dozen of these.</para>
    /// </summary>
    public static class VillageBuildingSheetSlicer
    {
        /// <summary>The only folder this tool touches.</summary>
        public static string BuildingsRoot => VillageBuildingKit.BuildingsRoot;

        // ---- entry points ---------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Slice Village Building Sheets", priority = 202)]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int failed);
            if (failed > 0)
                Debug.LogError($"[VillageBuildingSheetSlicer] Sliced {n} sheet(s), {failed} FAILED — " +
                               "see the errors above.");
            else
                Debug.Log($"[VillageBuildingSheetSlicer] Sliced {n} building sheet(s). " +
                          "Next: Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Build Village " +
                          "Building Prefabs.");
        }

        /// <summary>Batch entry point for <c>-executeMethod</c> — exits non-zero on any failure so a
        /// headless bake fails loudly instead of committing a half-sliced kit.</summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int failed);
                Debug.Log($"[VillageBuildingSheetSlicer] (batch) Sliced {n} sheet(s) ({failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[VillageBuildingSheetSlicer] {failed} sheet(s) failed — see above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VillageBuildingSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---- the work -------------------------------------------------------------------------

        /// <summary>
        /// Slice + import-lock every sheet the contract claims. Returns the number sliced and reports how
        /// many failed. A PNG in the kit folder that the contract does not name is a FAILURE, not a skip:
        /// with no contract entry it has no cell and no pivot, and guessing either is how a building ends
        /// up standing somewhere it was not placed.
        /// </summary>
        public static int SliceAll(out int failed)
        {
            failed = 0;

            if (!Directory.Exists(BuildingsRoot))
            {
                Debug.LogWarning(
                    $"[VillageBuildingSheetSlicer] No folder at '{BuildingsRoot}' — nothing to slice. " +
                    "Run Hidden Harbours ▸ Art ▸ Bake Village Buildings first.");
                return 0;
            }

            VillageBuildingKit.Contract contract = VillageBuildingKit.Load();
            if (contract == null) { failed = 1; return 0; }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D",
                                                      new[] { BuildingsRoot.TrimEnd('/') });
            int sliced = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(BuildingsRoot, StringComparison.Ordinal)) continue;
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                string stem = Path.GetFileNameWithoutExtension(path);
                VillageBuildingKit.Entry entry = VillageBuildingKit.EntryForStem(contract, stem);
                if (entry == null)
                {
                    Debug.LogError(
                        $"[VillageBuildingSheetSlicer] '{stem}' is under {BuildingsRoot} but no entry in " +
                        $"{VillageBuildingKit.ContractFileName} claims it. Not slicing — a sheet with no " +
                        "contract has no cell and no pivot, and guessing either is how a building ends " +
                        "up planted in the wrong place. Re-bake the kit, or move the stray sheet out.");
                    failed++;
                    continue;
                }

                seen.Add(entry.key);
                if (SliceOne(path, stem, entry)) sliced++;
                else failed++;
            }

            // The other direction: a contract entry with no sheet on disk. Silence here would mean a
            // placement tool offering a building whose art is missing.
            foreach (var entry in contract.buildings)
            {
                if (seen.Contains(entry.key)) continue;
                Debug.LogError(
                    $"[VillageBuildingSheetSlicer] {VillageBuildingKit.ContractFileName} claims " +
                    $"'{entry.key}' but {VillageBuildingKit.SheetPath(entry.key)} is not there. Re-bake.");
                failed++;
            }

            AssetDatabase.SaveAssets();
            return sliced;
        }

        /// <summary>Slice + import-lock ONE sheet against its contract entry.</summary>
        public static bool SliceOne(string path, string stem, VillageBuildingKit.Entry entry)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[VillageBuildingSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return false;
            }

            // Stamp the import settings BEFORE reading the texture: the cap and mode changes force a
            // reimport, and a texture read taken before that is stale. (Same rule SpriteSheetSlicer
            // follows for its maxTextureSize lift — "load AFTER the reimport".)
            ApplyImportSettings(importer);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[VillageBuildingSheetSlicer] '{path}' failed to load as Texture2D.");
                return false;
            }

            if (tex.width != entry.sheetW || tex.height != entry.sheetH)
            {
                Debug.LogError(
                    $"[VillageBuildingSheetSlicer] '{stem}' is {tex.width}×{tex.height} but the contract " +
                    $"says {entry.sheetW}×{entry.sheetH} ({entry.cols}×{entry.rows} cells of " +
                    $"{entry.cellW}×{entry.cellH}). Not slicing — re-bake, do not edit " +
                    $"{VillageBuildingKit.ContractFileName}.");
                return false;
            }
            if (tex.width > VillageBuildingKit.ImportSizeCap ||
                tex.height > VillageBuildingKit.ImportSizeCap)
            {
                Debug.LogError(
                    $"[VillageBuildingSheetSlicer] '{stem}' is {tex.width}×{tex.height}, over the " +
                    $"{VillageBuildingKit.ImportSizeCap} px import cap — Unity has already DOWNSCALED it " +
                    "and the sprite count would still come out right. Not slicing.");
                return false;
            }
            if (entry.cellW <= 0 || entry.cellH <= 0)
            {
                Debug.LogError($"[VillageBuildingSheetSlicer] '{stem}' has a {entry.cellW}×{entry.cellH} " +
                               "cell in the contract. Not slicing.");
                return false;
            }

            int cols = tex.width / entry.cellW;
            int rows = tex.height / entry.cellH;
            if (cols != entry.cols || rows != entry.rows)
            {
                Debug.LogError(
                    $"[VillageBuildingSheetSlicer] '{stem}' divides into {cols}×{rows} cells of " +
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

            SpriteRect[] rects = BuildRects(entry, existingIds);
            dp.SetSpriteRects(rects);

            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[VillageBuildingSheetSlicer] Sliced '{stem}' → {rects.Length} facing(s) " +
                      $"({entry.rows} row(s) × {entry.cols} of {entry.cellW}×{entry.cellH}), " +
                      $"ground-centre pivot {VillageBuildingKit.NormalizedPivot(entry)} " +
                      $"({VillageBuildingKit.BelowGroundPad(entry)} px of art below the ground line — " +
                      "bottom-centre would sink it by that much).");
            return true;
        }

        /// <summary>
        /// The kit's import lock. Starts from <see cref="ArtImportPipeline.ApplyLockedSettings"/> (PPU 32,
        /// Point, uncompressed, mips off — the canon in one place) and overrides only what canon gets
        /// wrong for a sliced building sheet: the cap is pinned, and the sheet-level alignment stops
        /// claiming bottom-centre.
        /// </summary>
        public static void ApplyImportSettings(TextureImporter importer)
        {
            ArtImportPipeline.ApplyLockedSettings(importer, importer.assetPath);

            // Never lift the cap for this kit — see VillageBuildingKit.ImportSizeCap.
            importer.maxTextureSize = VillageBuildingKit.ImportSizeCap;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);

            // FullRect, deliberately: no vertex-evaluated shader here (contrast TreeSheetSlicer, whose
            // wind bend needs Tight), and a quad is the cheaper mesh for a village of these.
            s.spriteMeshType = SpriteMeshType.FullRect;

            // The albedo IS colour, unlike the tree kit's mask/normal channels.
            s.sRGBTexture = true;
            s.alphaIsTransparency = true;
            s.alphaSource = TextureImporterAlphaSource.FromInput;

            // The real pivot is set per SPRITE RECT (BuildRects). Leave the sheet-level default honest
            // rather than BottomCenter, which is what ArtImportPipeline picks for /buildings/ and is
            // wrong for every sheet in this kit.
            s.spriteAlignment = (int)SpriteAlignment.Custom;

            importer.SetTextureSettings(s);
        }

        /// <summary>
        /// The grid, row-major from the TOP-LEFT cell (Unity's rects are bottom-origin, so row 0 maps to
        /// the highest Y — the convention every slicer in the repo uses). Names are
        /// <c>&lt;stem&gt;_d&lt;facing&gt;</c>, facing <c>i</c> depicting +45°·i because the bake already
        /// applied the measured convention.
        ///
        /// <para>Cells past <see cref="VillageBuildingKit.Entry.facings"/> are not emitted: a grid can be
        /// wider than the facing count (8 facings in a 3×3 grid leaves one empty), and a sprite over an
        /// empty cell is a transparent rect that a placement tool would happily offer.</para>
        /// </summary>
        public static SpriteRect[] BuildRects(VillageBuildingKit.Entry entry,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            Vector2 pivot = VillageBuildingKit.NormalizedPivot(entry);
            string stem = VillageBuildingKit.StemFor(entry.key);

            var rects = new List<SpriteRect>(entry.facings);
            for (int r = 0; r < entry.rows; r++)
            {
                for (int c = 0; c < entry.cols; c++)
                {
                    int facing = r * entry.cols + c;
                    if (facing >= entry.facings) break;

                    string name = VillageBuildingKit.SpriteNameFor(entry.key, facing);
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
        /// count, the ground-centre pivot on every facing, the locked PPU/filter/compression and no
        /// silent downscale. Returns true only if every sheet passes.
        ///
        /// <para>Exposed so the batch entry point and the EditMode suite share ONE definition of
        /// "imported correctly" — the same arrangement <see cref="TreeSheetSlicer.VerifyAll"/> has.</para>
        /// </summary>
        public static bool VerifyAll(bool logEachPass)
        {
            VillageBuildingKit.Contract contract = VillageBuildingKit.Load();
            if (contract == null) return false;

            bool allOk = true;
            int checkedCount = 0;

            foreach (var entry in contract.buildings)
            {
                string path = VillageBuildingKit.SheetPath(entry.key);
                string stem = VillageBuildingKit.StemFor(entry.key);

                if (!File.Exists(path))
                {
                    Debug.LogError($"[VillageBuildingSheetSlicer] VERIFY: '{path}' missing on disk.");
                    allOk = false;
                    continue;
                }
                checkedCount++;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[VillageBuildingSheetSlicer] VERIFY: '{stem}' has no TextureImporter.");
                    allOk = false;
                    continue;
                }

                bool ok = true;
                ok &= Check(stem, "Multiple-mode",
                            importer.spriteImportMode == SpriteImportMode.Multiple);
                ok &= Check(stem, $"PPU {ArtImportPipeline.PixelsPerUnit}",
                            Mathf.Approximately(importer.spritePixelsPerUnit,
                                                ArtImportPipeline.PixelsPerUnit));
                ok &= Check(stem, "Point filter", importer.filterMode == FilterMode.Point);
                ok &= Check(stem, "Compression None",
                            importer.textureCompression == TextureImporterCompression.Uncompressed);
                ok &= Check(stem, "mips off", !importer.mipmapEnabled);
                ok &= Check(stem, "sRGB ON (this sheet is colour, not data)",
                            VillageBuildingKit.SRgbOf(importer));
                ok &= Check(stem, $"inside the {VillageBuildingKit.ImportSizeCap} px cap",
                            importer.maxTextureSize >= entry.sheetW &&
                            importer.maxTextureSize >= entry.sheetH &&
                            entry.sheetW <= VillageBuildingKit.ImportSizeCap &&
                            entry.sheetH <= VillageBuildingKit.ImportSizeCap);

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                ok &= Check(stem, $"{entry.facings} facing sprite(s), got {sprites.Length}",
                            sprites.Length == entry.facings);

                // The load-bearing one. A bottom-centre pivot passes every other check on this list.
                Vector2 expectedPivot = VillageBuildingKit.PivotPixels(entry);
                foreach (var sprite in sprites)
                {
                    ok &= Check(sprite.name,
                                $"ground-centre pivot {expectedPivot}, got {sprite.pivot}",
                                Mathf.Abs(sprite.pivot.x - expectedPivot.x) < 0.01f &&
                                Mathf.Abs(sprite.pivot.y - expectedPivot.y) < 0.01f);
                    ok &= Check(sprite.name,
                                $"cell {entry.cellW}×{entry.cellH}, got " +
                                $"{sprite.rect.width}×{sprite.rect.height} (a sheet over the import cap " +
                                "arrives downscaled with the right sprite COUNT)",
                                Mathf.RoundToInt(sprite.rect.width) == entry.cellW &&
                                Mathf.RoundToInt(sprite.rect.height) == entry.cellH);
                }

                if (!ok) allOk = false;
                else if (logEachPass)
                    Debug.Log($"[VillageBuildingSheetSlicer] VERIFY OK: {stem} = {sprites.Length} " +
                              $"facing(s) of {entry.cellW}×{entry.cellH}, pivot {expectedPivot}.");
            }

            Debug.Log($"[VillageBuildingSheetSlicer] VERIFY: checked {checkedCount} sheet(s) of " +
                      $"{contract.buildings.Length} claimed — " + (allOk ? "ALL PASS" : "FAILURES PRESENT"));
            return allOk && checkedCount == contract.buildings.Length;
        }

        public static void VerifyAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                if (!VerifyAll(logEachPass: true))
                {
                    Debug.LogError("[VillageBuildingSheetSlicer] VERIFY FAILED — see mismatches above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VillageBuildingSheetSlicer] verify threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        static bool Check(string who, string what, bool ok)
        {
            if (!ok) Debug.LogError($"[VillageBuildingSheetSlicer] VERIFY: '{who}' — expected {what}.");
            return ok;
        }
    }
}
#endif
