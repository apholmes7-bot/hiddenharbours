using System;
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
    /// <b>THE IMPORT SETTINGS FOR THE BAKED BUBBLE KIT</b> — Single-mode sprites, centre pivot, and
    /// the 9-slice borders the two tiles need.
    ///
    /// <para><b>It is deliberately small, because most of the job is already done.</b>
    /// <see cref="ArtImportPipeline"/> is a postprocessor over everything under
    /// <c>Assets/_Project/Art/</c> — which is where <see cref="DialoguePresenter.ArtFolder"/> lives —
    /// and it already stamps PPU 32, point filtering, no compression, no mipmaps and
    /// <c>alphaIsTransparency</c>. Re-setting those here would give the kit a second, competing
    /// definition of the import lock. So this ASSERTS them instead: if the postprocessor's reach ever
    /// changes, the bubble kit goes red rather than quietly importing soft-filtered.</para>
    ///
    /// <para><b>No sprite slicing, and no SpriteAtlas.</b> Every piece bakes to its own PNG at its
    /// own size, so each is spriteMode <b>Single</b> and <c>LoadAssetAtPath&lt;Sprite&gt;</c>
    /// resolves it directly — none of the Multiple-mode trap that makes the camper's sheets need
    /// <c>LoadAllAssetsAtPath</c>. Fifteen pieces at 18×18 and smaller do not want packing, and an
    /// atlas would put the crisp 1:1 draw at the mercy of the packer's padding.
    /// <see cref="Verify"/> checks that no atlas has claimed them, by the one test that needs no
    /// atlas API: a packed sprite's <c>texture</c> is the atlas page, so its dimensions stop
    /// matching the PNG's.</para>
    ///
    /// <para><b>Idempotent.</b> Nothing is written unless a setting actually differs, so a re-run
    /// converges and leaves no <c>.meta</c> churn.</para>
    /// </summary>
    public static class BubbleKitImporter
    {
        /// <summary>Unity's sprite border is <c>(left, bottom, right, top)</c>. Getting that order
        /// wrong is silent — the panel simply stretches the wrong edges — so it is named once here
        /// and never re-derived at a call site.</summary>
        static Vector4 Border(int left, int bottom, int right, int top) =>
            new Vector4(left, bottom, right, top);

        /// <summary>
        /// The 9-slice border for a piece, or zero for the fixed-size stamps.
        ///
        /// <para>The panel's corner is the kit's own declared <c>slices.corner</c> — checked against
        /// the contract by <see cref="BubbleKitContract.Disagreements"/> before a bake is allowed, so
        /// this is a validated number rather than a transcribed one.</para>
        /// </summary>
        public static Vector4 BorderFor(string piece)
        {
            if (piece == DialogueBubbleKit.PiecePanel)
            {
                int c = DialogueBubbleKit.PanelCorner;
                return Border(c, c, c, c);
            }
            if (piece == DialogueBubbleKit.PieceGold)
            {
                // Both axes: its two consumers are different heights (chip 12, pill 13).
                int c = DialogueBubbleKit.GoldCorner;
                return Border(c, c, c, c);
            }
            return Vector4.zero;
        }

        public static string AssetPathFor(string piece) =>
            $"{DialoguePresenter.ArtFolder}/{piece}.png";

        // ---- entry points --------------------------------------------------------------------

        /// <summary>
        /// Apply import settings to every baked piece. Returns how many were actually changed —
        /// zero on a converged re-run.
        /// </summary>
        public static int ImportAll(out int missing)
        {
            missing = 0;
            int changed = 0;

            foreach (string piece in DialogueBubbleKit.Pieces)
            {
                string path = AssetPathFor(piece);
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[BubbleKitImporter] '{path}' is not on disk — run the bake " +
                                     "first (Hidden Harbours ▸ Art ▸ Bake Dialogue Bubble Kit).");
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
                Debug.LogError($"[BubbleKitImporter] '{path}' has no TextureImporter.");
                return false;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            Vector4 wantBorder = BorderFor(piece);
            bool dirty = false;

            // ⚠️ The mode is written on the SETTINGS OBJECT, not the importer property — measured on
            // the 4060, 2026-08-17. TextureImporterSettings carries spriteMode too, ReadTextureSettings
            // above captured the OLD mode, and SetTextureSettings below applies the WHOLE object last —
            // so `importer.spriteImportMode = Single` here was clobbered straight back to Multiple by
            // our own settings write, and every piece verified red twice. Two APIs write one field;
            // the one applied last must be the one that carries the value.
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
        /// <para>Checks the settings this file owns AND the ones <see cref="ArtImportPipeline"/>
        /// owns — the second group is not this file's to set, but it IS this file's to notice.</para>
        /// </summary>
        public static bool Verify(bool logEachPass = false)
        {
            bool allOk = true;
            int checkedCount = 0;

            foreach (string piece in DialogueBubbleKit.Pieces)
            {
                string path = AssetPathFor(piece);
                if (!File.Exists(path))
                {
                    Debug.LogError($"[BubbleKitImporter] VERIFY: '{path}' missing on disk.");
                    allOk = false;
                    continue;
                }

                checkedCount++;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[BubbleKitImporter] VERIFY: '{path}' has no TextureImporter.");
                    allOk = false;
                    continue;
                }

                var errors = new List<string>();

                if (importer.spriteImportMode != SpriteImportMode.Single)
                    errors.Add($"spriteImportMode is {importer.spriteImportMode}, expected Single");

                // ---- the import lock, which ArtImportPipeline owns and this only witnesses -----
                if (importer.spritePixelsPerUnit != DialogueBubbleKit.PixelsPerUnit)
                    errors.Add($"PPU is {importer.spritePixelsPerUnit}, expected " +
                               $"{DialogueBubbleKit.PixelsPerUnit} — is this folder still under " +
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
                    // texture is the atlas PAGE, so its dimensions stop matching the sprite's own
                    // rect. Cheap, and it fails for the reason we actually care about.
                    if (sprite.texture != null &&
                        (sprite.texture.width != (int)sprite.rect.width ||
                         sprite.texture.height != (int)sprite.rect.height))
                        errors.Add(
                            $"sprite rect {sprite.rect.width}×{sprite.rect.height} does not fill its " +
                            $"texture {sprite.texture.width}×{sprite.texture.height} — something is " +
                            "packing these into an atlas, which this kit does not want");

                    if (Mathf.Abs(sprite.pixelsPerUnit - DialogueBubbleKit.PixelsPerUnit) > 0.01f)
                        errors.Add($"sprite PPU is {sprite.pixelsPerUnit}");

                    if (sprite.border != wantBorder)
                        errors.Add($"sprite border is {sprite.border}, expected {wantBorder}");
                }

                if (errors.Count > 0)
                {
                    Debug.LogError($"[BubbleKitImporter] VERIFY: '{piece}' — {string.Join("; ", errors)}.");
                    allOk = false;
                    continue;
                }

                if (logEachPass)
                    Debug.Log($"[BubbleKitImporter] VERIFY OK: {piece} " +
                              $"({sprite.rect.width}×{sprite.rect.height}, border {wantBorder}).");
            }

            bool complete = checkedCount == DialogueBubbleKit.Pieces.Length;
            if (!complete)
                Debug.LogError($"[BubbleKitImporter] VERIFY: checked {checkedCount} of " +
                               $"{DialogueBubbleKit.Pieces.Length} pieces.");

            return allOk && complete;
        }
    }
}
