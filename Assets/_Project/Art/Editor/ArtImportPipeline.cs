#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// VS-23 — the locked Unity import settings for Hidden Harbours pixel art, applied automatically
    /// to every texture under <c>Assets/_Project/Art/</c>. This is the authoritative enforcement of
    /// the Art &amp; Audio Bible §9.2: Sprite (2D/UI), <b>PPU=32</b>, <b>Point</b> filter, <b>Compression
    /// None</b>, mips off, sRGB on, consistent pivots (feet for characters, hull centre for boats),
    /// Clamp wrap (Repeat for tiling water). It is the convention referenced by Art/README.md.
    ///
    /// <para>Why a postprocessor and not a hand-authored .preset: a Preset YAML must be generated from a
    /// live importer in-editor (fragile to hand-write) and a designer has to remember to apply it. This
    /// stamps the lock on <b>first import</b> of any art texture, so no agent can forget — while still
    /// respecting later <i>manual</i> tweaks (we only stamp when <see cref="AssetImporter.importSettingsMissing"/>
    /// is true, i.e. the asset has no .meta yet). Use the menu command below to (re)apply the lock to
    /// pre-existing or hand-tuned assets on demand.</para>
    ///
    /// <para>Scope: this enforces the standard <see cref="TextureImporter"/> path (exported PNG/TGA/…,
    /// which per the bible §9.1 are the shipped sprites). Direct <c>.aseprite</c>/<c>.psd</c> imports use
    /// their own ScriptedImporters; we emit a one-time reminder for those and document the manual
    /// settings in Art/README.md.</para>
    /// </summary>
    public sealed class ArtImportPipeline : AssetPostprocessor
    {
        /// <summary>All authored art lives under here; we never touch textures outside it.</summary>
        public const string ArtRoot = "Assets/_Project/Art/";

        /// <summary>The locked scale standard (canon). 1 sprite-pixel × 32 = 1 metre.</summary>
        public const float PixelsPerUnit = 32f;

        // ---- automatic enforcement (first import) -------------------------------------------------

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot, StringComparison.Ordinal)) return;
            var imp = assetImporter as TextureImporter;
            if (imp == null) return;

            // Only stamp defaults on FIRST import (no .meta yet). After that we respect any deliberate
            // per-asset overrides an artist made in the Inspector — the lock is a floor, not a cage.
            if (!imp.importSettingsMissing) return;

            ApplyLockedSettings(imp, assetPath);
        }

        void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith(ArtRoot, StringComparison.Ordinal)) return;
            if (assetImporter is TextureImporter) return;          // handled by OnPreprocessTexture
            if (!assetImporter.importSettingsMissing) return;       // first import only

            // .aseprite / .psd use dedicated ScriptedImporters we don't drive programmatically. Nudge the
            // author toward the locked settings rather than silently importing them blurry/compressed.
            string lower = assetPath.ToLowerInvariant();
            if (lower.EndsWith(".aseprite") || lower.EndsWith(".ase") || lower.EndsWith(".psd") || lower.EndsWith(".psb"))
            {
                Debug.Log($"[ArtImportPipeline] '{assetPath}' uses its own importer — set PPU=32, " +
                          "Filter=Point, Compression=None, Mip Maps off in its Inspector (see Art/README.md §Import lock).");
            }
        }

        // ---- the locked settings (single source of truth) -----------------------------------------

        /// <summary>
        /// Apply the canon import lock (bible §9.2) to a texture importer. Shared by the auto-postprocessor
        /// and the menu command so there is exactly one definition of "the locked settings".
        /// </summary>
        public static void ApplyLockedSettings(TextureImporter imp, string path)
        {
            imp.textureType = TextureImporterType.Sprite;       // Sprite (2D and UI)
            imp.spritePixelsPerUnit = PixelsPerUnit;            // PPU = 32 — the scale standard
            imp.filterMode = FilterMode.Point;                 // crisp pixels, no blur
            imp.textureCompression = TextureImporterCompression.Uncompressed; // exact pixels/colour
            imp.mipmapEnabled = false;                          // 2D: no minification blur
            imp.npotScale = TextureImporterNPOTScale.None;      // keep authored dimensions honest

            // Sprite-level settings (alignment/pivot/wrap/sRGB) go through TextureImporterSettings.
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.sRGBTexture = true;                               // colour art is sRGB
            s.alphaIsTransparency = true;                       // clean edges on transparent sprites
            s.spriteMeshType = SpriteMeshType.FullRect;         // predictable pixel-art quads
            s.spriteExtrude = 1;

            // Wrap: Clamp, except tiling water/parallax which repeats.
            bool tiling = IsTiling(path);
            s.wrapMode = tiling ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            // Pivot by category: feet for characters, hull centre for boats, centre otherwise.
            s.spriteAlignment = (int)PivotFor(path);

            imp.SetTextureSettings(s);
        }

        /// <summary>Tiling textures (animated water, parallax bands) wrap with Repeat.</summary>
        static bool IsTiling(string path)
        {
            string p = path.ToLowerInvariant();
            return p.Contains("/water") || p.Contains("_tiling") || p.Contains("_repeat") || p.Contains("_tile.");
        }

        /// <summary>
        /// Folder-driven default pivot. Characters pivot on the feet (so they plant on the ground grid);
        /// standing foliage (trees) and iso buildings pivot on their base (so they plant on the ground);
        /// boats pivot on the hull centre (rotation point); everything else centres.
        /// (Sliced sheets — e.g. the flowers under <c>/foliage/flowers/</c> — set their own per-tier pivots
        /// via the sheet slicers, so this Single-mode default doesn't govern them.)
        /// </summary>
        static SpriteAlignment PivotFor(string path)
        {
            string p = path.ToLowerInvariant();
            if (p.Contains("/characters/")) return SpriteAlignment.BottomCenter; // feet
            if (p.Contains("/foliage/"))    return SpriteAlignment.BottomCenter; // tree trunk on the ground
            if (p.Contains("/buildings/"))  return SpriteAlignment.BottomCenter; // building foot on the ground
            if (p.Contains("/boats/"))      return SpriteAlignment.Center;        // hull centre
            return SpriteAlignment.Center;
        }

        // ---- on-demand (re)application for pre-existing / hand-tuned assets -----------------------

        [MenuItem("Hidden Harbours/Art/Apply Locked Import Settings to Selection")]
        static void ApplyToSelection()
        {
            var objs = Selection.objects;
            int n = 0;
            foreach (var o in objs)
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (string.IsNullOrEmpty(path) || !path.StartsWith(ArtRoot, StringComparison.Ordinal)) continue;
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                ApplyLockedSettings(imp, path);
                imp.SaveAndReimport();
                n++;
            }
            Debug.Log(n > 0
                ? $"[ArtImportPipeline] Applied locked import settings to {n} texture(s)."
                : "[ArtImportPipeline] No textures under Assets/_Project/Art/ were selected.");
        }

        [MenuItem("Hidden Harbours/Art/Apply Locked Import Settings to Selection", isValidateFunction: true)]
        static bool ApplyToSelectionValidate() => Selection.objects.Length > 0;
    }
}
#endif
