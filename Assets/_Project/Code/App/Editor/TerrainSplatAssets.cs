#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Asset plumbing for the five painted splat maps (ADR 0028 PR 2 addendum; the fourth arrived
    /// with terrain kit v2, the fifth with v3's reef beds) — the paths the
    /// builder wires, blank minting, the DATA-texture importer, and the commit that mirrors the
    /// height brush's <c>CommitTexture()</c> flow (encode → write → reimport → RELOAD from disk
    /// before anything re-wires a reference, because a reimport invalidates the in-memory object).
    ///
    /// <para><b>⚠ The import settings are LOAD-BEARING.</b> A splat channel is a blend WEIGHT and
    /// a ladder POSITION, not a colour: an sRGB import would gamma-warp every painted value the
    /// shader reads (0.5 "base" would arrive as ~0.21). <see cref="ConfigureImporter"/> pins
    /// linear + readable + uncompressed on every commit, and an EditMode test pins the committed
    /// PNGs to the same settings so the trap cannot land silently.</para>
    /// </summary>
    public static class TerrainSplatAssets
    {
        public const string Dir = "Assets/_Project/Data/Terrain";

        /// <summary>The St Peters splat stem — <c>StPetersSplatA/B/C/D/E.png</c>, the EXACT paths
        /// <c>StPetersBuilder</c> loads when it wires <c>TerrainSplatSurface.ConfigureSplat</c>
        /// (a pin test holds the two spellings together).
        ///
        /// <para>Also the DEFAULT stem for every overload below that does not take one — see the
        /// per-region note on <see cref="PathOf(int, string)"/> for why the default exists and why it
        /// is not the shape to build a second region on.</para></summary>
        public const string StPetersBaseName = "StPetersSplat";

        /// <summary>
        /// The i-th splat texture's asset path (0=A 1=B 2=C 3=D 4=E) for <b>St Peters</b>.
        ///
        /// <para>⚠ This overload is the one that existed when St Peters was the only painted region, and
        /// it is kept so its callers do not have to change. It is not the one a NEW region uses — see
        /// <see cref="PathOf(int, string)"/>.</para>
        /// </summary>
        public static string PathOf(int textureIndex) => PathOf(textureIndex, StPetersBaseName);

        /// <summary>
        /// The i-th splat texture's asset path for the region whose splat maps are stemmed
        /// <paramref name="baseName"/> — e.g. <c>"NineMileCreekSplat"</c> →
        /// <c>Data/Terrain/NineMileCreekSplatA.png</c>.
        ///
        /// <para><b>Why this parameter had to exist.</b> A splat map is per-REGION data: it covers one
        /// region's world rect at that region's own texel grid, exactly as a seabed bake does. Until now
        /// every entry point here resolved to the St Peters stem as a <i>constant</i>, so a second
        /// painted region had nowhere to put its ground — pointing Nine Mile Creek at this pipeline
        /// would have had it painting over the island's maps, at the island's resolution, on the
        /// island's world frame. That is not a defect anybody had hit yet; it is the wall the moment a
        /// second coast is painted, which is what makes it worth removing before the paint rather than
        /// during it.</para>
        ///
        /// <para>Stems are APPEND-ONLY and belong beside the region they name, the same discipline the
        /// seabed bakes follow.</para>
        /// </summary>
        public static string PathOf(int textureIndex, string baseName) =>
            Dir + "/" + baseName + TerrainSplatBrush.TextureSuffixes[textureIndex] + ".png";

        /// <summary>True when every St Peters splat PNG exists as an imported asset.</summary>
        public static bool AllExist() => AllExist(StPetersBaseName);

        /// <summary>True when every splat PNG for <paramref name="baseName"/> exists as an imported
        /// asset. <inheritdoc cref="PathOf(int, string)"/></summary>
        public static bool AllExist(string baseName)
        {
            for (int i = 0; i < TerrainSplatBrush.TextureCount; i++)
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(PathOf(i, baseName)) == null) return false;
            return true;
        }

        /// <summary>
        /// Load the splat textures, MINTING any that are missing as all-zero (fully
        /// transparent black = "nothing painted, height bands only" — the shader's contract) at
        /// <paramref name="texels"/>, the SAME grid as the region's height map. Fills
        /// <paramref name="textures"/> and <paramref name="pixels"/> (working CPU buffers).
        /// Returns false only if an import failed.
        /// </summary>
        public static bool LoadOrCreate(Vector2Int texels, Texture2D[] textures, Color[][] pixels) =>
            LoadOrCreate(texels, textures, pixels, StPetersBaseName);

        /// <summary>
        /// <inheritdoc cref="LoadOrCreate(Vector2Int, Texture2D[], Color[][])"/>
        ///
        /// <para>For the region stemmed <paramref name="baseName"/> — <inheritdoc cref="PathOf(int, string)"/></para>
        /// </summary>
        public static bool LoadOrCreate(Vector2Int texels, Texture2D[] textures, Color[][] pixels,
                                        string baseName)
        {
            if (!AssetDatabase.IsValidFolder(Dir))
            {
                string parent = Path.GetDirectoryName(Dir).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(Dir));
            }

            for (int i = 0; i < TerrainSplatBrush.TextureCount; i++)
            {
                string path = PathOf(i, baseName);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null)
                {
                    WritePng(path, texels.x, texels.y, null);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    ConfigureImporter(path);
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                if (tex == null)
                {
                    Debug.LogError($"[TerrainSplatAssets] could not create/load '{path}'.");
                    return false;
                }
                if (!tex.isReadable)
                {
                    // A hand-imported PNG without the data settings — fix it rather than failing.
                    ConfigureImporter(path);
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                textures[i] = tex;
                pixels[i] = tex.GetPixels();
            }
            return true;
        }

        /// <summary>
        /// Persist the working buffers (the height brush's CommitTexture flow, once per splat map):
        /// re-encode each buffer to its source PNG, reimport with the data settings, then RELOAD
        /// the assets from disk into <paramref name="textures"/> — the reimport invalidates the
        /// old in-memory objects, so every downstream reference must be re-wired from the fresh
        /// loads, never the stale ones.
        /// </summary>
        public static void Commit(Texture2D[] textures, Color[][] pixels) =>
            Commit(textures, pixels, StPetersBaseName);

        /// <summary>
        /// <inheritdoc cref="Commit(Texture2D[], Color[][])"/>
        ///
        /// <para><paramref name="baseName"/> is only ever the FALLBACK path: a texture that is already an
        /// asset is written back to wherever it actually lives, so a mis-passed stem cannot redirect a
        /// commit onto another region's maps. <inheritdoc cref="PathOf(int, string)"/></para>
        /// </summary>
        public static void Commit(Texture2D[] textures, Color[][] pixels, string baseName)
        {
            for (int i = 0; i < TerrainSplatBrush.TextureCount; i++)
            {
                if (textures[i] == null || pixels[i] == null) continue;
                string path = AssetDatabase.GetAssetPath(textures[i]);
                if (string.IsNullOrEmpty(path)) path = PathOf(i, baseName);
                WritePng(path, textures[i].width, textures[i].height, pixels[i]);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                ConfigureImporter(path);
                textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Discard un-committed working edits: force a reimport from the on-disk PNG bytes (the
        /// in-memory asset may carry SetPixels the owner regrets) and reload textures + buffers.
        /// The single-step "revert to committed" the height brush never had.
        /// </summary>
        public static void RevertToCommitted(Texture2D[] textures, Color[][] pixels) =>
            RevertToCommitted(textures, pixels, StPetersBaseName);

        /// <summary>
        /// <inheritdoc cref="RevertToCommitted(Texture2D[], Color[][])"/>
        ///
        /// <para>For the region stemmed <paramref name="baseName"/> — <inheritdoc cref="PathOf(int, string)"/></para>
        /// </summary>
        public static void RevertToCommitted(Texture2D[] textures, Color[][] pixels, string baseName)
        {
            for (int i = 0; i < TerrainSplatBrush.TextureCount; i++)
            {
                string path = PathOf(i, baseName);
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                pixels[i] = textures[i] != null && textures[i].isReadable ? textures[i].GetPixels() : null;
            }
        }

        /// <summary>
        /// Import a splat PNG as DATA (the sibling of the tool's height importer): <b>LINEAR</b>
        /// (sRGBTexture off — weights, not colour; the gamma trap named in the class doc),
        /// CPU-readable, no mips, Clamp wrap, Bilinear filter, uncompressed so every painted byte
        /// survives exactly.
        /// </summary>
        public static void ConfigureImporter(string pngPath)
        {
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;          // linear — a channel is a weight, not a colour
            importer.isReadable = true;            // the brush edits the pixels in place
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        /// <summary>Encode + write an RGBA32 PNG; null <paramref name="pixels"/> writes all-zero
        /// (transparent black — the shader's "unpainted" value).</summary>
        private static void WritePng(string path, int width, int height, Color[] pixels)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            if (pixels == null)
            {
                var fill = new Color[width * height];   // Color's default is (0,0,0,0) — exactly "unpainted"
                tex.SetPixels(fill);
            }
            else tex.SetPixels(pixels);
            tex.Apply(false, false);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
#endif
