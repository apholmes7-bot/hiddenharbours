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
    /// Slices and IMPORT-LOCKS the shop sheets under <see cref="ShopKit.ShopsRoot"/> — the
    /// <c>Shopfront_&lt;key&gt;.png</c> shells and the <c>ShopLevel_&lt;key&gt;_&lt;level&gt;.png</c>
    /// floor plans <c>ShopBakeMenu</c> writes. Sibling of <see cref="VillageBuildingSheetSlicer"/> and
    /// <see cref="InteriorSheetSlicer"/>, and every number comes from <c>shops.contract.json</c>, which
    /// the same bake wrote.
    ///
    /// <para><b>Why this file exists at all, and what its absence looked like.</b> The bake shipped
    /// before its slicer, and the gap did not read as a gap: the sheets landed, Unity's own automatic
    /// slicing gave each one <b>eight</b> sprite rects (one per alpha island, and there are eight
    /// facings), and the names even came out <c>Shopfront_postOffice_0…7</c>. Eight named sprites on an
    /// eight-facing sheet is what a working slice looks like from a distance. Measured, the rects were
    /// alpha-trimmed bounding boxes of wildly different sizes — 340×393 next to 442×455 on one sheet —
    /// every pivot at <c>(0,0)</c>, and the sheet-level cap left at Unity's default 2048 while the
    /// widest sheet is 3984 px. So the kit would have been placed at the wrong scale, on the wrong
    /// origin, from a silently half-resolution texture. The lesson is the one the repo already carries
    /// as <i>Multiple mode is not the same as sliced</i>; this is the third kit to meet it.</para>
    ///
    /// <para><b>Three settings are load-bearing and all three fail silently:</b></para>
    /// <list type="bullet">
    ///   <item>🔴 <b>The pivot is the ground CENTRE, not bottom-centre.</b>
    ///   <see cref="ArtImportPipeline"/> defaults everything under <c>/buildings/</c> to
    ///   <see cref="SpriteAlignment.BottomCenter"/> — and this kit lives under
    ///   <c>Sprites/Buildings/Shops/</c>, so it inherits that. <see cref="ShopKit.BelowGroundPad"/> is
    ///   how far into the dirt that puts each build; on this set it is 160–224 px, <b>5.0–7.0 m</b>.</item>
    ///   <item>🔴 <b>The cap must be LIFTED, not asserted.</b> This is the first kit whose sheets pass
    ///   2048 (<see cref="ShopKit.ImportSizeCap"/> = 4096 and the widest sheet is 3984), so the village
    ///   kit's "pin it at the cap and never lift" is exactly wrong here — pinned at 2048 Unity downscales
    ///   and the sprite COUNT still comes out right. The cap is stamped BEFORE the texture is read,
    ///   because a read taken before the reimport reports the downscaled size.</item>
    ///   <item><b>A stray PNG is a FAILURE, not a skip.</b> A sheet the contract does not claim has no
    ///   cell and no pivot, and guessing either is how a shop ends up standing somewhere it was not
    ///   placed.</item>
    /// </list>
    ///
    /// <para>Mesh type stays <see cref="SpriteMeshType.FullRect"/> for the same reason the village kit's
    /// does: no vertex-evaluated shader here, and a quad is the cheaper mesh.</para>
    /// </summary>
    public static class ShopSheetSlicer
    {
        public static string ShopsRoot => ShopKit.ShopsRoot;

        // ---- entry points ---------------------------------------------------------------------

        // 203 keeps the Import run unbroken next to the village buildings at 202. Unity draws a
        // separator at any priority gap over 10.
        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Slice Shop Sheets", priority = 203)]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int failed);
            if (failed > 0)
                Debug.LogError($"[ShopSheetSlicer] Sliced {n} sheet(s), {failed} FAILED — see above.");
            else
                Debug.Log($"[ShopSheetSlicer] Sliced {n} shop sheet(s). " +
                          "Next: rebuild the regions that place them.");
        }

        /// <summary>Batch entry point for <c>-executeMethod</c> — exits non-zero on any failure, so a
        /// headless run fails loudly instead of committing a half-sliced kit.</summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int failed);
                Debug.Log($"[ShopSheetSlicer] (batch) Sliced {n} sheet(s) ({failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[ShopSheetSlicer] {failed} sheet(s) failed — see above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShopSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---- the work -------------------------------------------------------------------------

        /// <summary>
        /// Slice + import-lock every sheet the contract claims, in both directions: a PNG with no entry
        /// fails, and an entry with no PNG fails. Returns how many were sliced.
        /// </summary>
        public static int SliceAll(out int failed)
        {
            failed = 0;

            if (!Directory.Exists(ShopsRoot))
            {
                Debug.LogWarning(
                    $"[ShopSheetSlicer] No folder at '{ShopsRoot}' — nothing to slice. Run " +
                    "Hidden Harbours ▸ Art ▸ Bake Shops (shells + interiors) first.");
                return 0;
            }

            ShopKit.Contract contract = ShopKit.Load();
            if (contract == null) { failed = 1; return 0; }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ShopsRoot.TrimEnd('/') });
            int sliced = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(ShopsRoot, StringComparison.Ordinal)) continue;
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                string stem = Path.GetFileNameWithoutExtension(path);
                ShopKit.Entry entry = ShopKit.EntryForStem(contract, stem);
                if (entry == null)
                {
                    Debug.LogError(
                        $"[ShopSheetSlicer] '{stem}' is under {ShopsRoot} but no entry in " +
                        $"{ShopKit.ContractFileName} claims it. Not slicing — a sheet with no contract " +
                        "has no cell and no pivot. Re-bake, or move the stray sheet out.");
                    failed++;
                    continue;
                }

                seen.Add(stem);
                if (SliceOne(path, stem, entry)) sliced++;
                else failed++;
            }

            foreach (var entry in ShopKit.AllEntries(contract))
            {
                if (seen.Contains(ShopKit.StemFor(entry))) continue;
                Debug.LogError(
                    $"[ShopSheetSlicer] {ShopKit.ContractFileName} claims '{ShopKit.StemFor(entry)}' but " +
                    $"{ShopKit.SheetPathFor(entry)} is not there. Re-bake.");
                failed++;
            }

            AssetDatabase.SaveAssets();
            return sliced;
        }

        /// <summary>Slice + import-lock ONE sheet against its contract entry.</summary>
        public static bool SliceOne(string path, string stem, ShopKit.Entry entry)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[ShopSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return false;
            }

            // ⚠️⚠️ THE CAP MUST BE LIFTED **AND REIMPORTED** BEFORE THE TEXTURE IS READ, and the second
            // half of that is the part that bites. maxTextureSize is a power-of-two ceiling, so a
            // 3984 px sheet needs a 4096 import; Unity's default is 2048 and the sheet arrives at HALF
            // SIZE. Writing the field is not enough — the importer holds it in memory until something
            // reimports, so a Texture2D loaded straight after still reports the downscale. Measured on
            // the first run of this slicer, which is worth recording because it failed in the SAFE
            // direction: Shopfront_generalStore came back 2048×546 against a contract of 3878×1034 and
            // the size check below refused the slice. Had the cap been pinned rather than lifted (the
            // village kit's rule, right for a cottage) the rects would have been cut against a half-size
            // sheet and every shop would stand at half scale on a thrown-away pivot.
            int needed = Mathf.NextPowerOfTwo(Mathf.Max(entry.sheetW, entry.sheetH));
            if (needed > ShopKit.ImportSizeCap)
            {
                Debug.LogError(
                    $"[ShopSheetSlicer] '{stem}' is {entry.sheetW}×{entry.sheetH} and needs a {needed} px " +
                    $"import, over the kit's {ShopKit.ImportSizeCap} px cap. Re-pack into more rows or " +
                    "dial the build's size down rather than raising the cap — it is the ONE number the " +
                    "pack, this importer and the verify all read.");
                return false;
            }

            int capBefore = importer.maxTextureSize;
            ApplyImportSettings(importer);
            if (capBefore < needed)
            {
                Debug.Log($"[ShopSheetSlicer] '{stem}' is {entry.sheetW}×{entry.sheetH} but the importer " +
                          $"capped at {capBefore} — reimporting at {ShopKit.ImportSizeCap} px so the " +
                          "sheet arrives at native resolution.");
                importer.SaveAndReimport();
            }

            // Load AFTER the reimport above — a texture read taken before it reports the downscale.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[ShopSheetSlicer] '{path}' failed to load as Texture2D.");
                return false;
            }

            if (tex.width != entry.sheetW || tex.height != entry.sheetH)
            {
                Debug.LogError(
                    $"[ShopSheetSlicer] '{stem}' imported {tex.width}×{tex.height} but the contract says " +
                    $"{entry.sheetW}×{entry.sheetH} ({entry.cols}×{entry.rows} cells of " +
                    $"{entry.cellW}×{entry.cellH}). Not slicing — re-bake, do not edit " +
                    $"{ShopKit.ContractFileName}.");
                return false;
            }

            if (entry.cellW <= 0 || entry.cellH <= 0)
            {
                Debug.LogError($"[ShopSheetSlicer] '{stem}' has a {entry.cellW}×{entry.cellH} cell in the " +
                               "contract. Not slicing.");
                return false;
            }

            int cols = tex.width / entry.cellW;
            int rows = tex.height / entry.cellH;
            if (cols != entry.cols || rows != entry.rows)
            {
                Debug.LogError(
                    $"[ShopSheetSlicer] '{stem}' divides into {cols}×{rows} cells of " +
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
            // every run rewrite every spriteID — pure diff noise that buries real changes.
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(entry, existingIds);
            dp.SetSpriteRects(rects);

            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[ShopSheetSlicer] Sliced '{stem}' → {rects.Length} facing(s) " +
                      $"({entry.rows} row(s) × {entry.cols} of {entry.cellW}×{entry.cellH}), " +
                      $"ground-centre pivot {ShopKit.NormalizedPivot(entry)} " +
                      $"({ShopKit.BelowGroundPad(entry)} px of art below the ground line — bottom-centre " +
                      "would sink it by that much).");
            return true;
        }

        /// <summary>
        /// The kit's import lock. Starts from <see cref="ArtImportPipeline.ApplyLockedSettings"/> (PPU 32,
        /// Point, uncompressed, mips off — the canon in one place) and overrides only what canon gets
        /// wrong for this kit: the cap is LIFTED to <see cref="ShopKit.ImportSizeCap"/>, and the
        /// sheet-level alignment stops claiming bottom-centre.
        /// </summary>
        public static void ApplyImportSettings(TextureImporter importer)
        {
            ArtImportPipeline.ApplyLockedSettings(importer, importer.assetPath);

            // ⚠️ LIFT, not pin. The village kit's rule is the opposite ("never lift; the union crop makes
            // 2048 hold") and it holds for a cottage. It does not hold for the restaurant — see
            // ShopKit.ImportSizeCap, where the alternative was measured rather than waved away.
            importer.maxTextureSize = ShopKit.ImportSizeCap;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);

            // FullRect: no vertex-evaluated shader on a building, and a quad is the cheaper mesh.
            s.spriteMeshType = SpriteMeshType.FullRect;

            s.sRGBTexture = true;
            s.alphaIsTransparency = true;
            s.alphaSource = TextureImporterAlphaSource.FromInput;

            // The real pivot is set per SPRITE RECT (BuildRects). Leave the sheet-level default honest
            // rather than BottomCenter, which is what ArtImportPipeline picks for /buildings/ and is
            // wrong for every sheet in this kit by 5–7 metres.
            s.spriteAlignment = (int)SpriteAlignment.Custom;

            importer.SetTextureSettings(s);
        }

        /// <summary>
        /// The grid, row-major from the TOP-LEFT cell (Unity's rects are bottom-origin, so row 0 maps to
        /// the highest Y — the convention every slicer in the repo uses). Names are
        /// <c>&lt;stem&gt;_d&lt;facing&gt;</c>, facing <c>i</c> depicting the rig's <c>i</c>-th cell
        /// because the bake already applied the measured azimuth correction.
        ///
        /// <para>Cells past <see cref="ShopKit.Entry.facings"/> are not emitted, and on this kit that is
        /// not a corner case: the general store shell packs 7×2 = <b>14</b> cells for 8 facings and the
        /// restaurant 6×2 = 12. A sprite over an empty cell is a transparent rect a placement tool would
        /// happily offer.</para>
        /// </summary>
        public static SpriteRect[] BuildRects(ShopKit.Entry entry,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            Vector2 pivot = ShopKit.NormalizedPivot(entry);

            var rects = new List<SpriteRect>(entry.facings);
            for (int r = 0; r < entry.rows; r++)
            {
                for (int c = 0; c < entry.cols; c++)
                {
                    int facing = r * entry.cols + c;
                    if (facing >= entry.facings) break;

                    string name = ShopKit.SpriteNameFor(entry, facing);
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
        /// count, the ground-centre pivot on every facing, the locked PPU/filter/compression, and — the
        /// one only a cell-size assert can see — no silent downscale. Returns true only if every sheet
        /// passes.
        ///
        /// <para>Exposed so the batch entry point and the EditMode suite share ONE definition of
        /// "imported correctly", the arrangement <see cref="VillageBuildingSheetSlicer.VerifyAll"/>
        /// established.</para>
        /// </summary>
        public static bool VerifyAll(bool logEachPass)
        {
            ShopKit.Contract contract = ShopKit.Load();
            if (contract == null) return false;

            bool allOk = true;
            int checkedCount = 0, claimed = 0;

            foreach (var entry in ShopKit.AllEntries(contract))
            {
                claimed++;
                string path = ShopKit.SheetPathFor(entry);
                string stem = ShopKit.StemFor(entry);

                if (!File.Exists(path))
                {
                    Debug.LogError($"[ShopSheetSlicer] VERIFY: '{path}' missing on disk.");
                    allOk = false;
                    continue;
                }
                checkedCount++;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[ShopSheetSlicer] VERIFY: '{stem}' has no TextureImporter.");
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
                ok &= Check(stem, "sRGB ON (this sheet is colour, not data)", ShopKit.SRgbOf(importer));

                // Both directions: the sheet must fit the kit's cap, AND the importer must be lifted to
                // admit it. Either alone passes while the texture arrives at half size.
                ok &= Check(stem, $"the {ShopKit.ImportSizeCap} px cap, lifted and honoured " +
                                  $"(importer {importer.maxTextureSize}, sheet {entry.sheetW}×{entry.sheetH})",
                            importer.maxTextureSize >= entry.sheetW &&
                            importer.maxTextureSize >= entry.sheetH &&
                            entry.sheetW <= ShopKit.ImportSizeCap &&
                            entry.sheetH <= ShopKit.ImportSizeCap);

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                ok &= Check(stem, $"{entry.facings} facing sprite(s), got {sprites.Length}",
                            sprites.Length == entry.facings);

                // The load-bearing pair. A bottom-centre pivot and a downscaled sheet both pass every
                // other check on this list.
                Vector2 expectedPivot = ShopKit.PivotPixels(entry);
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
                    Debug.Log($"[ShopSheetSlicer] VERIFY OK: {stem} = {sprites.Length} facing(s) of " +
                              $"{entry.cellW}×{entry.cellH}, pivot {expectedPivot}.");
            }

            Debug.Log($"[ShopSheetSlicer] VERIFY: checked {checkedCount} sheet(s) of {claimed} claimed — " +
                      (allOk ? "ALL PASS" : "FAILURES PRESENT"));
            return allOk && checkedCount == claimed && claimed > 0;
        }

        public static void VerifyAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                if (!VerifyAll(logEachPass: true))
                {
                    Debug.LogError("[ShopSheetSlicer] VERIFY FAILED — see mismatches above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShopSheetSlicer] verify threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        static bool Check(string who, string what, bool ok)
        {
            if (!ok) Debug.LogError($"[ShopSheetSlicer] VERIFY: '{who}' — expected {what}.");
            return ok;
        }
    }
}
#endif
