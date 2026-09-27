using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// ⭐ <b>THE PAINTED HEIGHT PNG, WRITTEN AT SIXTEEN BITS</b> (terrain pass 9, PR 4; ADR 0046).
    ///
    /// <para>The terrain paint tool's two writers — a stroke's commit and a map's creation or export —
    /// both wrote <c>TextureFormat.R8</c>: 255 codes over the map's range, 3.9 cm a step over St Peters'
    /// 10 m, and 4.3 cm over the −4 … +7 m part 1 plans, which terraces a brook reach that stands
    /// 0.10–0.14 m above its bed into two or three steps. Both now write <c>R16</c> through this one
    /// encoder: 65535 codes, 0.17 mm a step over 11 m. Nothing reads a map differently. The sim decodes
    /// <c>code / 65535</c> through <see cref="HiddenHarbours.World.PaintedHeightMap.ReadNormalizedR"/>, the
    /// shaders sample a float, and both still decode with the same <c>lerp(min, max, r)</c>.</para>
    ///
    /// <para><b>An 8-bit map widens losslessly on its first stroke.</b> Its codes read back as k / 255,
    /// and 65535 = 255 × 257, so code k is written as exactly 257·k: the same height. The two committed
    /// maps stay 8-bit until someone paints or re-exports them; PR 4 converts neither.</para>
    ///
    /// <para><b>The codes go in with SetPixelData, never SetPixels32:</b> a Color32 holds eight bits a
    /// channel and would narrow the map back to R8 inside a 16-bit texture (the trap §41 records for the
    /// baked seabed). <b>And the importer names R16 for Standalone</b> rather than leaving the format on
    /// Automatic: an importer may hand Unity a narrower format than the file holds, and then every test
    /// of what was WRITTEN stays green over a map the sim reads at eight bits
    /// (<c>SeabedHeightImportTests</c>' premise). A mobile port adds its own platform override here.</para>
    /// </summary>
    public static class PaintedHeightPng
    {
        /// <summary>The codes a 16-bit channel holds above zero. 65535 = 255 × 257, which is why an 8-bit
        /// map's code k lands on 257·k exactly.</summary>
        public const int CodeCount = 65535;

        /// <summary>The build-target group the R16 override is written for (Windows, Mac and Linux
        /// players, and the editor on any of them).</summary>
        public const string StandalonePlatform = "Standalone";

        /// <summary>PURE: normalized R (clamped to 0..1) → the 16-bit code the PNG stores,
        /// <c>round(r × 65535)</c>. Reads R only; the tool keeps G = B = R, and they are not written.</summary>
        public static ushort[] EncodeCodes(Color[] pixels)
        {
            if (pixels == null) return System.Array.Empty<ushort>();
            var codes = new ushort[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                codes[i] = (ushort)Mathf.RoundToInt(Mathf.Clamp01(pixels[i].r) * CodeCount);
            return codes;
        }

        /// <summary>A normalized-R buffer in <see cref="Texture2D.SetPixels(Color[])"/>'s order (bottom row
        /// first), as a 16-bit grayscale PNG. EncodeToPNG writes an R16 texture at bit depth 16 and flips
        /// the rows to the PNG's top-down order, exactly as it did for R8, so the import lands the same
        /// way up.</summary>
        public static byte[] EncodeR16Png(Color[] pixels, int width, int height)
        {
            if (pixels == null || width <= 0 || height <= 0 || pixels.Length != width * height)
                throw new System.ArgumentException(
                    "[PaintedHeightPng] a height PNG needs exactly width × height pixels (" + width + " × " +
                    height + "), got " + (pixels == null ? "none" : pixels.Length.ToString()) + ".");

            var tex = new Texture2D(width, height, TextureFormat.R16, false, true);
            try
            {
                tex.SetPixelData(EncodeCodes(pixels), 0);
                tex.Apply(false, false);
                return tex.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// THE WRITE both of the tool's writers make — a stroke's commit and a map's creation or export:
        /// encode <paramref name="pixels"/> at sixteen bits, write them over <paramref name="pngPath"/>,
        /// import, apply the data-texture settings with the R16 override, and hand back the texture as
        /// Unity now loads it. One path, so a test of this method is a test of what a stroke writes.
        /// </summary>
        public static Texture2D WriteAndImport(string pngPath, Color[] pixels, int width, int height)
        {
            File.WriteAllBytes(pngPath, EncodeR16Png(pixels, width, height));
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter(pngPath);   // keep isReadable/linear/R16 after the re-import
            return AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        }

        /// <summary>
        /// Import a painted-height PNG as a DATA texture the sim can decode (F1): CPU-readable, LINEAR (the
        /// R channel is metres of elevation, not colour), no mipmaps, Clamp wrap, uncompressed — the
        /// settings the committed maps import with — plus the Standalone override that NAMES R16, so the
        /// sixteen bits in the file are the sixteen bits the sim and the shader read.
        /// </summary>
        public static void ConfigureImporter(string pngPath)
        {
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;          // linear — elevation is data, not colour
            importer.isReadable = true;            // the sim MUST be able to read the codes
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed; // keep every code exact

            TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings(StandalonePlatform);
            standalone.overridden = true;
            standalone.maxTextureSize = importer.maxTextureSize;   // the default's size cap: an override must not rescale
            standalone.format = TextureImporterFormat.R16;
            standalone.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
        }
    }
}
