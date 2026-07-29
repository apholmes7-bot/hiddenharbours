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
    /// Slices and IMPORT-LOCKS the Acadian tree sheets under
    /// <see cref="TreeKitCatalog.TreesRoot"/> — <c>&lt;Species&gt;_&lt;stage&gt;_&lt;season&gt;</c>
    /// plus its <c>_mask</c> and <c>_normal</c> siblings, baked by <c>TreeRigBaker</c>.
    ///
    /// <para>Every number comes from the kit's own <c>Trees.json</c>
    /// (<see cref="TreeKitCatalog.Load"/>) — cell, columns, and the trunk-foot pivot. There is no
    /// manifest here to drift from the rig, which is the whole point of writing the contract in the
    /// same bake.</para>
    ///
    /// <para><b>Four settings are load-bearing and two of them fail SILENTLY:</b></para>
    /// <list type="bullet">
    ///   <item><b>Mesh Type <see cref="SpriteMeshType.Tight"/>.</b> The wind shader shapes its sway
    ///   as <c>bendW = smoothstep(_TrunkAnchor,1,uv.y)²</c> in the VERTEX stage. A FullRect sprite is
    ///   a four-vertex quad, so that is only evaluated at uv.y 0 and 1 and the rasteriser
    ///   interpolates linearly: the squaring collapses and <c>_TrunkAnchor</c> becomes inert for
    ///   every value. All 43 of the old hand-drawn trees shipped that way (PR #297). Pinned by
    ///   <c>WindBendTessellationTests</c> for the old set and <c>TreeSheetImportTests</c> for
    ///   these.</item>
    ///   <item>🔴 <b>sRGB OFF on <c>_mask</c> and <c>_normal</c>.</b> They are DATA, not colour.
    ///   Leave sRGB on and the channels get gamma-mangled — the sprite still looks fine, so nothing
    ///   catches it but a numeric assert. <see cref="ArtImportPipeline"/> stamps sRGB <b>on</b> for
    ///   everything under Art/, so this override is not optional.</item>
    ///   <item><b>Compression None</b> on all three. A block-compressed mask is a wrong mask, and a
    ///   block-compressed normal is worse.</item>
    ///   <item><b>The pivot is the TRUNK FOOT.</b> <c>ArtImportPipeline.PivotFor</c> defaults
    ///   anything under <c>/foliage/</c> to <see cref="SpriteAlignment.BottomCenter"/> — correct for
    ///   the old single-sprite trees, and <b>wrong here by each species' own flare pad</b> (13–23 px,
    ///   ≈0.4–0.7 m at PPU 32). It would read as an art bug, not an import bug.</item>
    /// </list>
    ///
    /// <para>Sibling of <see cref="CatchStorageSheetSlicer"/> with its own root and its own rules
    /// rather than a row in someone else's manifest: this is the only kit in the repo with
    /// per-channel colour-space settings and a per-species pivot, and the shared engine
    /// (<c>FishingSheetSlicer.SliceSheet</c>) writes one pivot per kit, not per sheet.</para>
    /// </summary>
    public static class TreeSheetSlicer
    {
        /// <summary>The only folder this tool touches.</summary>
        public static string TreesRoot => TreeKitCatalog.TreesRoot;

        // ---- entry points ---------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Slice Acadian Tree Sheets", priority = 52)]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int failed);
            Debug.Log($"[TreeSheetSlicer] Sliced {n} tree sheet(s) ({failed} failed).");
        }

        /// <summary>Batch entry point for <c>-executeMethod</c> — exits non-zero on any failure so
        /// a headless bake fails loudly instead of committing a half-sliced set.</summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int failed);
                Debug.Log($"[TreeSheetSlicer] (batch) Sliced {n} tree sheet(s) ({failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[TreeSheetSlicer] {failed} sheet(s) failed — see errors above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TreeSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---- the work -------------------------------------------------------------------------

        /// <summary>
        /// Slice + import-lock every sheet the contract claims. Returns the number sliced and
        /// reports how many failed. A sheet the contract does not name is left alone and reported,
        /// never guessed at.
        /// </summary>
        public static int SliceAll(out int failed)
        {
            failed = 0;

            if (!Directory.Exists(TreesRoot))
            {
                Debug.LogWarning($"[TreeSheetSlicer] No folder at '{TreesRoot}' — nothing to slice.");
                return 0;
            }

            TreeKitCatalog.Contract contract = TreeKitCatalog.Load();
            if (contract == null) { failed = 1; return 0; }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TreesRoot.TrimEnd('/') });
            int sliced = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(TreesRoot, StringComparison.Ordinal)) continue;
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                string stem = Path.GetFileNameWithoutExtension(path);
                TreeKitCatalog.Entry entry = TreeKitCatalog.EntryForStem(contract, stem);
                if (entry == null)
                {
                    Debug.LogError(
                        $"[TreeSheetSlicer] '{stem}' is under {TreesRoot} but no entry in " +
                        $"{TreeKitCatalog.ContractFileName} claims it. Not slicing — a sheet with " +
                        "no contract has no cell and no pivot, and guessing either is how a tree " +
                        "ends up planted in the wrong place.");
                    failed++;
                    continue;
                }

                if (SliceOne(path, stem, entry)) sliced++;
                else failed++;
            }

            AssetDatabase.SaveAssets();
            return sliced;
        }

        /// <summary>Slice + import-lock ONE sheet against its contract entry.</summary>
        public static bool SliceOne(string path, string stem, TreeKitCatalog.Entry entry)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[TreeSheetSlicer] '{path}' has no TextureImporter — skipping.");
                return false;
            }

            TreeKitCatalog.Channel channel = TreeKitCatalog.ChannelOf(stem);

            // Stamp the import settings BEFORE reading the texture: the colour-space flip alone
            // forces a reimport, and a texture read taken before it is stale. (The same rule
            // SpriteSheetSlicer follows for its maxTextureSize lift — "load AFTER the reimport".)
            ApplyImportSettings(importer, channel);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[TreeSheetSlicer] '{path}' failed to load as Texture2D — skipping.");
                return false;
            }

            if (tex.width != entry.sheetW || tex.height != entry.sheetH)
            {
                Debug.LogError(
                    $"[TreeSheetSlicer] '{stem}' is {tex.width}×{tex.height} but the contract says " +
                    $"{entry.sheetW}×{entry.sheetH} ({entry.cellW}×{entry.cellH} cells). Not " +
                    "slicing — re-bake, do not edit Trees.json.");
                return false;
            }
            if (tex.width > TreeKitCatalog.ImportSizeCap || tex.height > TreeKitCatalog.ImportSizeCap)
            {
                Debug.LogError(
                    $"[TreeSheetSlicer] '{stem}' is {tex.width}×{tex.height}, over the " +
                    $"{TreeKitCatalog.ImportSizeCap} px import cap — Unity has already DOWNSCALED " +
                    "it and the sprite count would still come out right. Not slicing.");
                return false;
            }

            int cols = tex.width / entry.cellW;
            int rows = tex.height / entry.cellH;

            importer.spriteImportMode = SpriteImportMode.Multiple;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            // Re-use existing spriteIDs so a re-bake of unchanged art is a no-op on the .meta.
            // Fresh GUIDs on every run rewrote every spriteID in 43 .meta files the last time
            // somebody skipped this — pure diff noise that buries the owner's real changes.
            var existingIds = dp.GetSpriteRects()
                                .GroupBy(r => r.name)
                                .ToDictionary(g => g.Key, g => g.First().spriteID);

            SpriteRect[] rects = BuildRects(stem, cols, rows, entry, existingIds);
            dp.SetSpriteRects(rects);

            var nameIdDp = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIdDp?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            dp.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[TreeSheetSlicer] Sliced '{stem}' → {rects.Length} sprite(s) " +
                      $"({rows} row(s) × {cols} variant(s) of {entry.cellW}×{entry.cellH}), " +
                      $"trunk-foot pivot {TreeKitCatalog.NormalizedPivot(entry)} " +
                      $"(pad {entry.nearFlarePad}, anchor {entry.trunkAnchor:F4}), " +
                      $"sRGB {(TreeKitCatalog.IsColourChannel(channel) ? "ON" : "OFF")}.");
            return true;
        }

        /// <summary>
        /// The tree kit's import lock, per channel. Starts from
        /// <see cref="ArtImportPipeline.ApplyLockedSettings"/> (PPU 32, Point, uncompressed, mips
        /// off — the canon in one place) and then overrides the three things that canon gets wrong
        /// for this kit: mesh type, colour space, and the alpha flag on data channels.
        /// </summary>
        public static void ApplyImportSettings(TextureImporter importer,
                                               TreeKitCatalog.Channel channel)
        {
            // The shared floor first, so PPU/filter/compression are never restated here.
            ArtImportPipeline.ApplyLockedSettings(importer, importer.assetPath);

            // Never lift the cap for this kit — see TreeKitCatalog.ImportSizeCap.
            importer.maxTextureSize = TreeKitCatalog.ImportSizeCap;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;

            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);

            // ⚠️ Tight, not FullRect: the bend curve is evaluated per VERTEX (see the class remarks).
            s.spriteMeshType = SpriteMeshType.Tight;

            // 🔴 The two data channels are NUMBERS, not colour.
            bool colour = TreeKitCatalog.IsColourChannel(channel);
            s.sRGBTexture = colour;

            // alphaIsTransparency premultiplies the colour bleed at the alpha edge — right for the
            // albedo, wrong for a mask whose A IS the coverage signal and for a normal map whose
            // RGB must survive untouched next to a transparent pixel.
            s.alphaIsTransparency = colour;
            s.alphaSource = TextureImporterAlphaSource.FromInput;

            // Pivot is set per SPRITE RECT (BuildRects), not here, because it is per species — but
            // leave the sheet-level default honest rather than BottomCenter, which is what
            // ArtImportPipeline picks for /foliage/ and is wrong for every tree in this kit.
            s.spriteAlignment = (int)SpriteAlignment.Custom;

            importer.SetTextureSettings(s);
        }

        /// <summary>
        /// The grid, row-major from the TOP-LEFT cell (Unity's rects are bottom-origin, so row 0
        /// maps to the highest Y — the same convention every slicer in the repo uses). Names are
        /// <c>&lt;stem&gt;_v&lt;variant&gt;</c> for the single-sway-row sheets this bake produces,
        /// and gain a <c>_f&lt;frame&gt;</c> term only if a future bake adds sway rows — so today's
        /// names never have to change.
        /// </summary>
        public static SpriteRect[] BuildRects(string stem, int cols, int rows,
                                              TreeKitCatalog.Entry entry,
                                              IReadOnlyDictionary<string, GUID> existingIds = null)
        {
            Vector2 pivot = TreeKitCatalog.NormalizedPivot(entry);
            var rects = new SpriteRect[rows * cols];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    string name = rows == 1 ? $"{stem}_v{c}" : $"{stem}_v{c}_f{r}";
                    rects[r * cols + c] = new SpriteRect
                    {
                        name = name,
                        spriteID = existingIds != null && existingIds.TryGetValue(name, out var id)
                                   ? id
                                   : GUID.Generate(),
                        rect = new Rect(c * entry.cellW, (rows - 1 - r) * entry.cellH,
                                        entry.cellW, entry.cellH),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        border = Vector4.zero,
                    };
                }
            }
            return rects;
        }

        // ---- verification ---------------------------------------------------------------------

        /// <summary>
        /// Assert every contract sheet imported the way this kit needs: Multiple-mode, the right
        /// slice count, the trunk-foot pivot, Tight mesh, the right colour space and no lossy
        /// compression. Returns true only if every sheet passes. Exposed so the batch entry point
        /// and the EditMode suite share one definition of "imported correctly".
        /// </summary>
        public static bool VerifyAll(bool logEachPass)
        {
            TreeKitCatalog.Contract contract = TreeKitCatalog.Load();
            if (contract == null) return false;

            bool allOk = true;
            int checkedCount = 0;

            foreach (var entry in contract.trees)
            foreach (string season in entry.seasons)
            foreach (var channel in TreeKitCatalog.Channels)
            {
                string path = TreeKitCatalog.SheetPath(entry.species, entry.stage, season, channel);
                string stem = Path.GetFileNameWithoutExtension(path);

                if (!File.Exists(path))
                {
                    Debug.LogError($"[TreeSheetSlicer] VERIFY: '{path}' missing on disk.");
                    allOk = false;
                    continue;
                }
                checkedCount++;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[TreeSheetSlicer] VERIFY: '{stem}' has no TextureImporter.");
                    allOk = false;
                    continue;
                }

                bool ok = true;
                ok &= Check(stem, "Multiple-mode",
                            importer.spriteImportMode == SpriteImportMode.Multiple);
                ok &= Check(stem, "Mesh Type Tight (the bend curve needs intermediate vertices)",
                            TreeKitCatalog.MeshTypeOf(importer) == SpriteMeshType.Tight);
                ok &= Check(stem, $"sRGB {(TreeKitCatalog.IsColourChannel(channel) ? "ON" : "OFF")}",
                            SRgbOf(importer) == TreeKitCatalog.IsColourChannel(channel));
                ok &= Check(stem, "Compression None",
                            importer.textureCompression == TextureImporterCompression.Uncompressed);
                ok &= Check(stem, $"PPU {ArtImportPipeline.PixelsPerUnit}",
                            Mathf.Approximately(importer.spritePixelsPerUnit,
                                                ArtImportPipeline.PixelsPerUnit));
                ok &= Check(stem, "Point filter", importer.filterMode == FilterMode.Point);
                ok &= Check(stem, "mips off", !importer.mipmapEnabled);

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                int expected = (entry.sheetW / entry.cellW) * (entry.sheetH / entry.cellH);
                ok &= Check(stem, $"{expected} sprite(s), got {sprites.Length}",
                            sprites.Length == expected);

                Vector2 expectedPivot = TreeKitCatalog.PivotPixels(entry);
                foreach (var sprite in sprites)
                    ok &= Check(sprite.name,
                                $"trunk-foot pivot {expectedPivot}, got {sprite.pivot}",
                                Mathf.Abs(sprite.pivot.x - expectedPivot.x) < 0.01f &&
                                Mathf.Abs(sprite.pivot.y - expectedPivot.y) < 0.01f);

                if (!ok) allOk = false;
                else if (logEachPass)
                    Debug.Log($"[TreeSheetSlicer] VERIFY OK: {stem} = {sprites.Length} sprite(s) " +
                              $"of {entry.cellW}×{entry.cellH}, pivot {expectedPivot}, " +
                              $"sRGB {SRgbOf(importer)}.");
            }

            int expectedSheets = contract.trees.Length * TreeKitCatalog.Channels.Length;
            Debug.Log($"[TreeSheetSlicer] VERIFY: checked {checkedCount} sheet(s) of " +
                      $"{expectedSheets} claimed — " + (allOk ? "ALL PASS" : "FAILURES PRESENT"));
            return allOk && checkedCount == expectedSheets;
        }

        public static void VerifyAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                if (!VerifyAll(logEachPass: true))
                {
                    Debug.LogError("[TreeSheetSlicer] VERIFY FAILED — see mismatches above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TreeSheetSlicer] verify threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>sRGB lives on <see cref="TextureImporterSettings"/>, not the importer — the
        /// same easy-to-miss indirection as the mesh type.</summary>
        public static bool SRgbOf(TextureImporter importer)
        {
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            return s.sRGBTexture;
        }

        static bool Check(string who, string what, bool ok)
        {
            if (!ok) Debug.LogError($"[TreeSheetSlicer] VERIFY: '{who}' — expected {what}.");
            return ok;
        }
    }
}
#endif
