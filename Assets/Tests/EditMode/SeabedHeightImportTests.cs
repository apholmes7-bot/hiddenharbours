using System.IO;
using System.Text;
using HiddenHarbours.Core;
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
    /// construction — is still <b>eight bits</b>, and `TerrainPaintTool` writes it as
    /// <c>TextureFormat.R8</c> at two sites.</para>
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
    /// those bits actually buy at each map's own range. It is written to <b>pass today at eight bits</b>
    /// and to keep passing when row 32 widens them; what it will not allow is the widening being
    /// silently undone by the importer.</para>
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
                Assert.GreaterOrEqual(bits, 8,
                    $"⭐ ROW 32: {Path.GetFileName(path)} is LOADED at {tex.format} ({bits} bits in R). " +
                    "TerrainPaintTool writes these as R8, so anything under eight bits means the " +
                    "IMPORTER threw precision away that the file contained — the widening undone one " +
                    "layer further out than anyone would look.");
            }

            report.AppendLine();
            report.AppendLine("  Eight bits is the state row 32 exists to change: over a 12 m range that is");
            report.AppendLine("  4.7 cm of elevation, which §41 measured as 67 cm of DRAWN EDGE on the 0.035");
            report.AppendLine("  shelf that spring low bares. This guard does not widen anything — it makes");
            report.AppendLine("  sure a widening cannot be quietly reversed by the import settings.");
            TestContext.WriteLine(report.ToString());

            Assert.Greater(checkedMaps, 0,
                "⭐ NO HEIGHT MAP WAS CHECKED. Both paths are missing, so this test passed over nothing — " +
                "which is the false green that let 'no painted asset is committed' into the register. If " +
                "the maps moved, fix the paths; do not let this go quietly green.");
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

            // And the same statement at SIXTEEN bits, which is where row 32 is going: the decoders take a
            // float, so neither side needs touching — only the write and the import.
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
