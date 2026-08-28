using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>THE IMPORT SETTINGS FOR THE BAKED NOTEBOOK KIT</b> — Single-mode sprites and a centre pivot.
    ///
    /// <para><b>It is deliberately small, because most of the job is already done.</b>
    /// <see cref="ArtImportPipeline"/> is a postprocessor over everything under
    /// <c>Assets/_Project/Art/</c> — which is where <see cref="NotebookKit.ArtFolder"/> lives —
    /// and it already stamps PPU 32, point filtering, no compression, no mipmaps and
    /// <c>alphaIsTransparency</c>. Re-setting those here would give the kit a second, competing
    /// definition of the import lock, and a second definition is how imports rot. So this ASSERTS
    /// them and owns only what is genuinely its own: sprite mode, border and pivot.</para>
    ///
    /// <para><b>⭐ EVERY BORDER IS ZERO, ON PURPOSE — and the two pieces that look like they want a
    /// 9-slice are flagged rather than guessed.</b> The bubble kit declared its own
    /// <c>slices.corner</c>, so <c>BubbleKitImporter</c> had a validated number to use. This kit
    /// declares none. Two pieces would plausibly stretch:
    /// <list type="bullet">
    ///   <item><b>The paper stocks.</b> A 9-slice stretches the middle, and the middle of ruled stock
    ///   is the RULE PITCH — stretching it puts the lines off the 10 px lattice the whole layout is
    ///   built on. Paper wants tiling at an exact multiple of the pitch, not slicing, and 34 is not a
    ///   multiple of 10, so even the tile size is a question for the art lane.</item>
    ///   <item><b>The selection pill.</b> Its ends are drawn by <c>highlight()</c> at whatever width
    ///   the row needs. The presenter should not stretch this one at all: the kit is explicit that the
    ///   cove gold IS <c>BubbleKit.GOLD</c> — "one selection vocabulary across the talking UI and the
    ///   book UI" — so the shipped pill reuses the bubble kit's ALREADY 9-sliced gold tile, and the
    ///   notebook's own <c>select.*</c> pieces stay fixed-size tiles for the taste board.</item>
    /// </list>
    /// Inventing corner numbers for either would be exactly the magic number CLAUDE.md rule 6
    /// forbids. When the art lane declares them, <see cref="BorderFor"/> is where they land.</para>
    ///
    /// <para><b>No sprite slicing, and no SpriteAtlas.</b> Every piece bakes to its own PNG at its own
    /// size, so each is spriteMode <b>Single</b> and <c>LoadAssetAtPath&lt;Sprite&gt;</c> resolves it
    /// directly. <see cref="Verify"/> catches an atlas claiming them by the one test that needs no
    /// atlas API: a packed sprite's texture is the atlas page, so its dimensions stop matching its
    /// own rect.</para>
    ///
    /// <para><b>Idempotent.</b> Nothing is written unless a setting actually differs.</para>
    /// </summary>
    public static class NotebookKitImporter
    {
        /// <summary>Unity's sprite border is <c>(left, bottom, right, top)</c>. Getting that order
        /// wrong is silent — the sprite simply stretches the wrong edges — so it is named once here
        /// and never re-derived at a call site.</summary>
        static Vector4 Border(int left, int bottom, int right, int top) =>
            new Vector4(left, bottom, right, top);

        /// <summary>
        /// The 9-slice border for a piece. Zero for every piece today — see the class remarks for why
        /// the two stretch candidates are flagged rather than guessed. This is the seam the art lane's
        /// declared corners drop into.
        /// </summary>
        public static Vector4 BorderFor(string piece) => Border(0, 0, 0, 0);

        public static string AssetPathFor(string piece) =>
            $"{NotebookKit.ArtFolder}/{piece}.png";

        /// <summary>
        /// Apply import settings to every baked piece. Returns how many were actually changed — zero
        /// on a converged re-run.
        /// </summary>
        public static int ImportAll(out int missing)
        {
            missing = 0;
            int changed = 0;

            foreach (string piece in NotebookKit.Pieces)
            {
                string path = AssetPathFor(piece);
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[NotebookKitImporter] '{path}' is not on disk — run the bake " +
                                     "first (Hidden Harbours ▸ Art ▸ Bake Notebook Kit).");
                    missing++;
                    continue;
                }

                if (ImportOne(piece, path)) changed++;
            }

            if (changed > 0) AssetDatabase.SaveAssets();
            return changed;
        }

        /// <summary>Returns true when this piece's importer actually needed changing.</summary>
        static bool ImportOne(string piece, string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[NotebookKitImporter] '{path}' has no TextureImporter.");
                return false;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            Vector4 wantBorder = BorderFor(piece);
            bool dirty = false;

            // ⚠️ THE DUAL-API CLOBBER. The mode is written on the SETTINGS OBJECT, never on the
            // importer property — measured on the 4060 during #561, 2026-08-17.
            // TextureImporterSettings carries spriteMode too; ReadTextureSettings above captured the
            // OLD mode, and SetTextureSettings below applies the WHOLE object LAST — so
            // `importer.spriteImportMode = Single` here would be clobbered straight back by our own
            // settings write, and every piece verified red. Two APIs write one field; the one applied
            // last must be the one carrying the value.
            if (settings.spriteMode != (int)SpriteImportMode.Single)
            {
                settings.spriteMode = (int)SpriteImportMode.Single;
                dirty = true;
            }

            if (settings.spriteBorder != wantBorder)
            {
                settings.spriteBorder = wantBorder;
                dirty = true;
            }

            // Centre pivot on every piece. The presenter positions by RectTransform rather than by
            // sprite pivot, so this is about being DETERMINISTIC rather than about placement: the
            // postprocessor picks a pivot by path category, and "UI" is not one of its categories.
            if (settings.spriteAlignment != (int)SpriteAlignment.Center)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                dirty = true;
            }

            if (!dirty) return false;

            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// Assert every piece imported the way the kit needs. Returns true only if all of them did.
        ///
        /// <para>Checks the settings this file owns AND the ones <see cref="ArtImportPipeline"/> owns —
        /// the second group is not this file's to set, but it IS this file's to notice.</para>
        /// </summary>
        public static bool Verify(bool logEachPass = false)
        {
            bool allOk = true;
            int checkedCount = 0;

            foreach (string piece in NotebookKit.Pieces)
            {
                string path = AssetPathFor(piece);
                if (!File.Exists(path))
                {
                    Debug.LogError($"[NotebookKitImporter] VERIFY: '{path}' missing on disk.");
                    allOk = false;
                    continue;
                }

                checkedCount++;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[NotebookKitImporter] VERIFY: '{path}' has no TextureImporter.");
                    allOk = false;
                    continue;
                }

                var errors = new List<string>();

                if (importer.spriteImportMode != SpriteImportMode.Single)
                    errors.Add($"spriteImportMode is {importer.spriteImportMode}, expected Single");

                // ---- the import lock, which ArtImportPipeline owns and this only witnesses --------
                if (importer.spritePixelsPerUnit != NotebookKit.PixelsPerUnit)
                    errors.Add($"PPU is {importer.spritePixelsPerUnit}, expected " +
                               $"{NotebookKit.PixelsPerUnit} — is this folder still under " +
                               $"{ArtImportPipeline.ArtRoot}?");
                if (importer.filterMode != FilterMode.Point)
                    errors.Add($"filterMode is {importer.filterMode}, expected Point");
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    errors.Add($"compression is {importer.textureCompression}, expected Uncompressed");
                if (importer.mipmapEnabled)
                    errors.Add("mipmaps are on");

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);

                Vector4 wantBorder = BorderFor(piece);
                if (settings.spriteBorder != wantBorder)
                    errors.Add($"border is {settings.spriteBorder}, expected {wantBorder}");

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    errors.Add("does not resolve as a Sprite");
                }
                else
                {
                    // ⚠️ THE ATLAS CHECK, without touching a SpriteAtlas API: a packed sprite's
                    // texture is the atlas PAGE, so its dimensions stop matching its own rect.
                    if (sprite.texture != null &&
                        (sprite.texture.width != (int)sprite.rect.width ||
                         sprite.texture.height != (int)sprite.rect.height))
                        errors.Add(
                            $"sprite rect {sprite.rect.width}×{sprite.rect.height} does not fill its " +
                            $"texture {sprite.texture.width}×{sprite.texture.height} — something is " +
                            "packing these into an atlas, which this kit does not want");

                    if (Mathf.Abs(sprite.pixelsPerUnit - NotebookKit.PixelsPerUnit) > 0.01f)
                        errors.Add($"sprite PPU is {sprite.pixelsPerUnit}");

                    if (sprite.border != wantBorder)
                        errors.Add($"sprite border is {sprite.border}, expected {wantBorder}");
                }

                if (errors.Count > 0)
                {
                    Debug.LogError($"[NotebookKitImporter] VERIFY: '{piece}' — {string.Join("; ", errors)}.");
                    allOk = false;
                    continue;
                }

                if (logEachPass)
                    Debug.Log($"[NotebookKitImporter] VERIFY OK: {piece} " +
                              $"({sprite.rect.width}×{sprite.rect.height}).");
            }

            bool complete = checkedCount == NotebookKit.Pieces.Length;
            if (!complete)
                Debug.LogError($"[NotebookKitImporter] VERIFY: checked {checkedCount} of " +
                               $"{NotebookKit.Pieces.Length} pieces.");

            return allOk && complete;
        }
    }
}
