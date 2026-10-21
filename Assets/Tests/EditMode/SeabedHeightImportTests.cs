using System.IO;
using System.Text;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>REGISTER ROW 32 — the painted seabed's height PNG, asserted at the format Unity LOADS.</b>
    ///
    /// <para>Rows 9 + 10 shipped <c>R16</c> for the BAKED seabed because eight bits could not hold a
    /// shoreline: at Nine Mile Creek's spring low the drawn waterline missed the sim's contour by
    /// <b>28.7 cm RMS</b>, and the value quantum was 96 % of it (§41). The PAINTED path — ADR 0014's
    /// hand-authored map, whose bytes <c>PaintedTidalTerrain</c> also decodes so render == sim by
    /// construction — was <b>eight bits</b> until ADR 0046 (terrain pass 9 PR 4): <c>TerrainPaintTool</c>'s
    /// two writers now write <c>R16</c> through one encoder, <c>PaintedHeightPng</c>, which also names
    /// <c>R16</c> in the importer's Standalone override. The two committed maps stay 8-bit files until
    /// someone paints or re-exports them.</para>
    ///
    /// <para><b>Why this guard reads the IMPORTER and not the tool.</b> The tool's write is only half the
    /// chain. A texture importer can silently hand Unity something narrower than the file contains —
    /// compress it, drop it to 8 bits, strip readability — and every test that checks what was WRITTEN
    /// stays green over a texture the sim reads at a coarser quantum than anyone intended. That is the
    /// same shape as the <c>SetPixels32</c> trap §41 records for the baked path, one layer further out:
    /// <b>a field in the file is not a field the object carries.</b></para>
    ///
    /// <para><b>So this asserts the LOADED object</b> — <see cref="Texture2D.format"/>,
    /// <see cref="Texture2D.isReadable"/>, mip count, wrap and filter — and reports the elevation quantum
    /// those bits actually buy at each map's own range. Each map must load at <b>no fewer bits than its
    /// own file holds</b> (the PNG's IHDR bit depth): eight for the committed maps today, sixteen for any
    /// map the tool has written since ADR 0046. What it will not allow is the widening being silently
    /// undone by the importer. <see cref="AMapTheToolWrites_IsLoadedAtSixteenBits"/> proves the tool's
    /// side: what it writes is a sixteen-bit file that Unity loads as <c>R16</c>.</para>
    /// </summary>
    public class SeabedHeightImportTests
    {
        /// <summary>The committed painted-seabed maps. ⚠️ Named by REGION, not by "painted" — a search
        /// for the latter finds neither, which is how an earlier pass concluded no painted asset was
        /// committed at all and wrote it into the register twice.</summary>
        static readonly string[] HeightMaps =
        {
            "Assets/_Project/Data/Terrain/StPetersSeabed_HeightTex.png",
            "Assets/_Project/Data/Terrain/NineMileCreekSeabed_HeightTex.png",
        };

        /// <summary>Bits per channel the loaded format actually carries in R. Only the formats this
        /// pipeline can produce are listed; an unlisted one is a fail, not a guess.</summary>
        static int RedBits(TextureFormat f)
        {
            switch (f)
            {
                case TextureFormat.R8:        return 8;
                case TextureFormat.Alpha8:    return 8;
                case TextureFormat.RGB24:     return 8;
                case TextureFormat.RGBA32:    return 8;
                case TextureFormat.ARGB32:    return 8;
                case TextureFormat.BGRA32:    return 8;
                case TextureFormat.R16:       return 16;
                case TextureFormat.RG32:      return 16;
                case TextureFormat.RGBA64:    return 16;
                case TextureFormat.RHalf:     return 11;   // binary16 mantissa+implicit, near 1.0
                case TextureFormat.RFloat:    return 24;
                default:                      return -1;
            }
        }

        [Test]
        public void EverySeabedHeightMap_IsLoadedAtTheFormatItWasWrittenAt_AndIsReadable()
        {
            var report = new StringBuilder();
            report.AppendLine("ROW 32 — the painted seabed height maps, as UNITY LOADS THEM");
            report.AppendLine("map                              |    loaded format | bits | mips | " +
                              "readable | sRGB | quantum over its own range");

            int checkedMaps = 0;
            foreach (string path in HeightMaps)
            {
                if (!File.Exists(path))
                {
                    report.AppendLine($"{Path.GetFileName(path),-32} | (not committed — skipped)");
                    continue;
                }
                checkedMaps++;

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(tex, $"{path} is committed but does not load as a Texture2D");
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"{path} has no TextureImporter");

                int bits = RedBits(tex.format);
                // The map's own elevation range, from the PaintedHeightMap asset beside it.
                string assetPath = path.Replace("_HeightTex.png", ".asset");
                float lo = -6f, hi = 6f;
                var map = AssetDatabase.LoadAssetAtPath<HiddenHarbours.World.PaintedHeightMap>(assetPath);
                if (map != null) { lo = map.MinElevation; hi = map.MaxElevation; }
                int codes = bits > 0 ? (1 << bits) - 1 : 1;
                float quantumCm = (hi - lo) / codes * 100f;

                report.AppendLine(
                    $"{Path.GetFileName(path),-32} | {tex.format,16} | {bits,4} | {tex.mipmapCount,4} | " +
                    $"{tex.isReadable,8} | {importer.sRGBTexture,4} | {quantumCm,6:0.000} cm " +
                    $"over {lo:0.#}..{hi:0.#} m");

                // ---- what the SIM needs, whatever the bit depth ----------------------------------
                Assert.Greater(bits, 0,
                    $"{Path.GetFileName(path)} loads as {tex.format}, which this guard does not know how " +
                    "to read a red-channel bit depth from. Add it to RedBits rather than assuming — an " +
                    "unrecognised format is exactly where a silent narrowing would hide.");

                Assert.IsTrue(tex.isReadable,
                    $"{Path.GetFileName(path)} must be CPU-readable: PaintedTidalTerrain calls GetPixels() " +
                    "on it, and without this the sim reads the region as open water (ADR 0014).");

                Assert.IsFalse(importer.sRGBTexture,
                    $"{Path.GetFileName(path)} must import LINEAR — the R channel is metres of elevation, " +
                    "not colour. An sRGB curve on it bends the seabed.");

                Assert.AreEqual(1, tex.mipmapCount,
                    $"{Path.GetFileName(path)} must have no mips — a mipped height map hands the shader a " +
                    "different seabed at a different zoom.");

                Assert.AreEqual(TextureWrapMode.Clamp, tex.wrapMode,
                    $"{Path.GetFileName(path)} must clamp: a repeating height map wraps the coast.");

                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression,
                    $"{Path.GetFileName(path)} must import UNCOMPRESSED. Block compression on a height " +
                    "map is the silent narrowing this guard exists to catch — DXT would quantise the " +
                    "elevation to a handful of levels per block and no other test would notice.");

                // ---- ⭐ THE ROW'S OWN CLAIM: the importer must not narrow what the tool wrote ------
                int fileBits = PngBitDepth(path);
                Assert.GreaterOrEqual(bits, fileBits,
                    $"⭐ ROW 32: {Path.GetFileName(path)} is a {fileBits}-bit file LOADED at {tex.format} " +
                    $"({bits} bits in R) with the editor on {EditorUserBuildSettings.activeBuildTarget}. " +
                    "The IMPORTER threw away precision the file contained — the widening undone one layer " +
                    "further out than anyone would look.");
            }

            report.AppendLine();
            report.AppendLine("  Eight bits is what the committed maps still hold: over a 12 m range that is");
            report.AppendLine("  4.7 cm of elevation, which §41 measured as 67 cm of DRAWN EDGE on the 0.035");
            report.AppendLine("  shelf that spring low bares. The tool writes sixteen since ADR 0046, and a");
            report.AppendLine("  map widens on its first stroke; this guard makes sure the import settings");
            report.AppendLine("  cannot quietly reverse that.");
            TestContext.WriteLine(report.ToString());

            Assert.Greater(checkedMaps, 0,
                "⭐ NO HEIGHT MAP WAS CHECKED. Both paths are missing, so this test passed over nothing — " +
                "which is the false green that let 'no painted asset is committed' into the register. If " +
                "the maps moved, fix the paths; do not let this go quietly green.");
        }

        /// <summary>
        /// ⭐ ADR 0046 §8: what the tool WRITES is a sixteen-bit file, and Unity LOADS it at sixteen bits. A map
        /// baked by the tool's export (<see cref="TerrainPaintTool.BakeAnalyticCoast"/>, whose write a stroke's
        /// commit shares) into a temp asset: the PNG's own header must say 16-bit greyscale, the importer must
        /// NAME <c>R16</c> for Standalone, uncompressed, and the loaded texture must carry sixteen bits in R —
        /// with every data-texture setting the committed maps are held to above. <b>On the base</b> the tool
        /// wrote an 8-bit file (IHDR bit depth 8) with no Standalone override, and this fails on its first
        /// assertion.
        /// </summary>
        [Test]
        public void AMapTheToolWrites_IsLoadedAtSixteenBits()
        {
            const string mapPath = "Assets/TempSeabedImportMap.asset";
            const string pngPath = "Assets/TempSeabedImportMap_HeightTex.png";
            try
            {
                var map = ScriptableObject.CreateInstance<PaintedHeightMap>();
                AssetDatabase.CreateAsset(map, mapPath);
                map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(mapPath);
                PaintedHeightMap baked = TerrainPaintTool.BakeAnalyticCoast(
                    map, new ITidalTerrain[] { new Slope() }, Vector2.zero, new Vector2(64f, 48f),
                    new Vector2Int(32, 24));
                Assert.IsNotNull(baked, "the tool's bake returned no map.");
                Assert.IsNotNull(baked.HeightTexture, "the baked map binds no height texture.");
                Assert.AreEqual(pngPath, AssetDatabase.GetAssetPath(baked.HeightTexture),
                    "the tool wrote its PNG somewhere other than the map's _HeightTex sibling.");

                // The file.
                int fileBits = PngBitDepth(pngPath);
                Assert.AreEqual(16, fileBits, $"the tool wrote a {fileBits}-bit height PNG.");
                Assert.AreEqual(0, PngColourType(pngPath), "the tool's height PNG is not greyscale.");

                // The importer names sixteen bits, rather than leaving the format to Automatic.
                var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                Assert.IsNotNull(importer, $"{pngPath} has no TextureImporter.");
                TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
                Assert.IsTrue(standalone.overridden, "the height PNG carries no Standalone override.");
                Assert.AreEqual(TextureImporterFormat.R16, standalone.format,
                    "the height PNG's Standalone override does not name R16.");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, standalone.textureCompression,
                    "the height PNG's Standalone override compresses it.");
                Assert.GreaterOrEqual(standalone.maxTextureSize, 32,
                    "the Standalone override's size cap would rescale the map.");

                // The object Unity loads.
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                Assert.IsNotNull(tex, $"{pngPath} does not load as a Texture2D.");
                Assert.AreEqual(16, RedBits(tex.format),
                    $"the tool's 16-bit height PNG LOADS as {tex.format} with the editor on " +
                    $"{EditorUserBuildSettings.activeBuildTarget}: the sim and the shader would read it at " +
                    $"{RedBits(tex.format)} bits.");
                Assert.AreEqual(32, tex.width, "the importer rescaled the map's width.");
                Assert.AreEqual(24, tex.height, "the importer rescaled the map's height.");
                Assert.IsTrue(tex.isReadable, "the tool's height PNG imported unreadable.");
                Assert.IsFalse(importer.sRGBTexture, "the tool's height PNG imported as sRGB.");
                Assert.AreEqual(1, tex.mipmapCount, "the tool's height PNG imported with mips.");
                Assert.AreEqual(TextureWrapMode.Clamp, tex.wrapMode, "the tool's height PNG does not clamp.");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression,
                    "the tool's height PNG imports compressed on the Default platform.");
                Debug.Log($"[SeabedHeightImport] a map the tool writes: a {fileBits}-bit greyscale PNG, " +
                          $"Standalone override {standalone.format}/{standalone.textureCompression}, loaded as " +
                          $"{tex.format} on {EditorUserBuildSettings.activeBuildTarget}.");
            }
            finally
            {
                AssetDatabase.DeleteAsset(mapPath);
                AssetDatabase.DeleteAsset(pngPath);
                GameServices.Reset();
            }
        }

        /// <summary>A gentle slope inside the default map range (−4 … +6 m), asymmetric in both axes.</summary>
        private sealed class Slope : ITidalTerrain
        {
            public float ElevationAt(Vector2 p) => -1f + 0.05f * p.x + 0.02f * p.y;
        }

        /// <summary>The PNG's own bit depth per channel, from its IHDR chunk (byte 24).</summary>
        static int PngBitDepth(string path) => PngHeader(path)[24];

        /// <summary>The PNG's colour type, from its IHDR chunk (byte 25; 0 = greyscale).</summary>
        static int PngColourType(string path) => PngHeader(path)[25];

        /// <summary>The first 29 bytes of a PNG: the signature and the IHDR chunk. A file that is not a PNG
        /// (an LFS pointer that was never checked out, say) fails here by name, not as some bit depth.</summary>
        static byte[] PngHeader(string path)
        {
            var head = new byte[29];
            int n = 0;
            using (FileStream fs = File.OpenRead(path))
            {
                while (n < head.Length)
                {
                    int read = fs.Read(head, n, head.Length - n);
                    if (read <= 0) break;
                    n += read;
                }
            }
            Assert.AreEqual(head.Length, n, $"{path} is too short to be a PNG.");
            Assert.IsTrue(head[0] == 0x89 && head[1] == (byte)'P' && head[2] == (byte)'N' && head[3] == (byte)'G',
                $"{path} is not a PNG — an LFS pointer that was never checked out?");
            return head;
        }

        /// <summary>
        /// The decode the sim runs must be the decode the shader runs. Both take a NORMALIZED float and
        /// the same min/max, so widening the texture needs no decoder change — the finding that made
        /// row 32 a two-literal job instead of a rewrite. This pins that they still agree.
        /// </summary>
        [Test]
        public void TheSimAndTheShader_DecodeAHeightTheSameWay()
        {
            const float Lo = -6f, Hi = 6f;
            for (int code = 0; code <= 255; code += 17)
            {
                float r01 = code / 255f;
                float sim = HiddenHarbours.World.PaintedHeightField.DecodeElevation(r01, Lo, Hi);
                float shader = Mathf.Lerp(Lo, Hi, r01);          // lerp(_HeightMin, _HeightMax, tex.r)
                Assert.AreEqual(shader, sim, 1e-5f,
                    $"at code {code} the sim decodes {sim:0.0000} m and the shader {shader:0.0000} m. " +
                    "ADR 0014's whole point is that these are the same bytes read the same way; if they " +
                    "drift, the boat grounds where the water looks deep.");
            }

            // And the same statement at SIXTEEN bits, which is what the tool writes since ADR 0046: the
            // decoders take a float, so neither side needed touching — only the write and the import.
            for (int code = 0; code <= 65535; code += 4369)
            {
                float r01 = code / 65535f;
                Assert.AreEqual(Mathf.Lerp(Lo, Hi, r01),
                                HiddenHarbours.World.PaintedHeightField.DecodeElevation(r01, Lo, Hi), 1e-5f,
                    "the decode must be bit-depth agnostic — this is why row 32 is a change to the WRITE " +
                    "and the IMPORT, and not to the decoder on either side.");
            }
            TestContext.WriteLine("  sim and shader decode identically at 8 and at 16 bits — the decoder is " +
                                  "bit-depth agnostic, so row 32 touches only the write and the import.");
        }
    }
}
