using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The still water above the tide in the RENDER (ADR 0046), read from the shader source — CI has no GPU,
    /// so the source is where the render's arithmetic can be pinned. Four things: the two shaders that draw
    /// a waterline (the water and the tidal faces) read the one shared still-water include, and the overlay,
    /// which draws no waterline, reads no level at all; unset is a passthrough (-1e30 under every tide); each
    /// shader composes max(tide, still) at exactly ONE site — the water its depth, the face its waterline —
    /// so the swell's shore fade and the surf marches stay on the tide; and both height-scale maps are
    /// sampled at full precision, never through a half-precision texture or into a half.
    ///
    /// <para>Whether the shaders COMPILE is <c>WaterShaderCompileGuardTests</c>' and
    /// <c>TidalFaceShaderCompileGuardTests</c>' job; they force-compile both, include and all.</para>
    ///
    /// <para><b>On the base</b> the include does not exist, no shader samples <c>_HHStillTex</c>, and neither
    /// shader calls <c>StillLevelAt</c>. Three tests fail at their first assert; the precision test's
    /// per-line checks pass on the base's float <c>_HeightTex</c> reads, and it fails at its count of
    /// <c>_HHStillTex</c> declarations (none on the base).</para>
    /// </summary>
    public sealed class StillWaterShaderTests
    {
        private const string ShaderDir = "Assets/_Project/Art/Shaders";
        private const string IncludePath = ShaderDir + "/Include/StillWater.hlsl";
        private const string WaterPath = ShaderDir + "/HiddenHarboursWater.shader";
        private const string FacePath = ShaderDir + "/HiddenHarboursTidalFace.shader";
        private const string OverlayPath = ShaderDir + "/HiddenHarboursWaterOverlay.shader";
        private const string IncludeLine = "#include \"Assets/_Project/Art/Shaders/Include/StillWater.hlsl\"";

        [Test]
        public void TheTwoShadersThatDrawAWaterline_ReadTheStillWater_AndTheOverlayReadsNoLevel()
        {
            Assert.IsTrue(File.Exists(IncludePath), "the shared still-water read is " + IncludePath);
            string include = Code(IncludePath);
            StringAssert.Contains("TEXTURE2D(_HHStillTex)", include);
            StringAssert.IsMatch(@"float4\s+_HHStillRect\s*;", include);
            StringAssert.IsMatch(@"float4\s+_HHStillRange\s*;", include);
            StringAssert.IsMatch(@"float\s+StillLevelAt\s*\(\s*float2\s+\w+\s*\)", include);

            foreach (string path in new[] { WaterPath, FacePath })
                Assert.AreEqual(1, CountOf(Code(path), IncludeLine),
                    path + " includes the still water once — one level for the water and the faces it meets");

            // WaterOverlay re-composes pixels the water shader already drew (ADR 0023) and draws no waterline
            // of its own, which is why it takes no still-water read (ADR 0046 §7). The day it reads a level,
            // it must compose the still water too — this is where that day is caught.
            string overlay = Code(OverlayPath);
            foreach (string level in new[] { "_WaterLevel", "_HeightTex", "_HHSeaLevel", "_HHStill", "StillLevelAt" })
                Assert.AreEqual(0, CountOf(overlay, level),
                    OverlayPath + " reads '" + level + "': a level read in the overlay must compose the still water (ADR 0046)");
        }

        [Test]
        public void Unset_IsBelowEveryTide_SoARegionWithNoStillMapDrawsAsBefore()
        {
            string body = StillLevelAtBody();
            // In order: unbound → none; off the map's rectangle → none; code 0 → none; else the code on the
            // map's range. The C# twin is StillWaterLevels.DecodeCode01 and PaintedStillWater.StillLevelAt.
            Match unbound = Regex.Match(body, @"if\s*\(\s*_HHStillRange\.z\s*<\s*0\.5\s*\)\s*return\s+-1e30\s*;");
            Match offMap = Regex.Match(body,
                @"if\s*\(\s*any\s*\(\s*uv\s*<\s*0\.0\s*\)\s*\|\|\s*any\s*\(\s*uv\s*>\s*1\.0\s*\)\s*\)\s*return\s+-1e30\s*;");
            Match sample = Regex.Match(body, @"SAMPLE_TEXTURE2D_LOD\s*\(\s*_HHStillTex\s*,");
            Match decode = Regex.Match(body,
                @"return\s+r\s*>\s*0\.0\s*\?\s*lerp\s*\(\s*_HHStillRange\.x\s*,\s*_HHStillRange\.y\s*,\s*r\s*\)\s*:\s*-1e30\s*;");
            Assert.IsTrue(unbound.Success, "an unbound map (range.z = 0, what StillWaterGlobals publishes unset) answers -1e30");
            Assert.IsTrue(offMap.Success, "outside the map's rectangle answers -1e30");
            Assert.IsTrue(sample.Success, "the level is read from _HHStillTex at LOD 0");
            Assert.IsTrue(decode.Success, "code 0 answers -1e30; a code r > 0 decodes to lerp(min, max, r)");
            Assert.Less(unbound.Index, sample.Index, "the unbound gate comes before the sample");
            Assert.Less(offMap.Index, sample.Index, "the rectangle gate comes before the sample");
            Assert.Less(sample.Index, decode.Index);

            // -1e30 is below any tide this game has, by thirty orders of magnitude, so max(tide, -1e30) is
            // the tide itself — the composition hands every pixel of an unset region back unchanged.
            foreach (float tide in new[] { -2.2f, 0f, 0.88f, 2.2f, -1000f, 1000f })
                Assert.AreEqual(tide, Mathf.Max(tide, -1e30f), "max(" + tide + ", -1e30)");
        }

        [Test]
        public void EachShaderComposesAtOneSite_TheWaterItsDepth_AndTheFaceItsWaterline()
        {
            string water = Code(WaterPath);
            StringAssert.IsMatch(
                @"float\s+depth\s*=\s*_WaterLevel\s*-\s*elevation\s*;\s*" +
                @"depth\s*=\s*max\s*\(\s*depth\s*,\s*StillLevelAt\s*\(\s*worldXY\s*\+\s*warp\s*\)\s*-\s*elevation\s*\)\s*;",
                water, "the water's depth is the deeper of the tide's and the still water's, read at the same warped point");
            Assert.AreEqual(1, CountOf(water, "StillLevelAt("),
                "the water composes ONE depth: the vertex shore fade, the shoal read and the fetch/surf marches " +
                "stay on the tide (a pond has no swell)");

            string face = Code(FacePath);
            StringAssert.IsMatch(@"\w+\.faceWorldXY\s*=\s*TransformObjectToWorld\s*\(\s*\w+\.positionOS\s*\)\s*\.xy\s*;", face,
                "the face reads the still water at its own world XY, the frame the water reads it in");
            StringAssert.IsMatch(
                @"float\s+sea\s*=\s*max\s*\(\s*_HHSeaLevelWorld\.x\s*,\s*StillLevelAt\s*\(\s*input\.faceWorldXY\s*\)\s*\)\s*;",
                face, "the water over a face is max(tide, still)");
            StringAssert.IsMatch(@"_HHFaceTide\.x\s*\+\s*\(\s*sea\s*-\s*_HHFaceTide\.y\s*\)\s*\*\s*_HHFaceTide\.z", face,
                "and the waterline is cut at that level, not the tide's");
            Assert.AreEqual(1, CountOf(face, "StillLevelAt("), "the face composes at its waterline only");
            Assert.AreEqual(0, CountOf(face, "faceWorldY "), "no stale tide-only row is left beside the composed one");
        }

        [Test]
        public void BothHeightScaleMaps_AreSampledAtFullPrecision()
        {
            // The seabed height map and the still map are R16 (ADR 0046): a step of 11 m / 65535 = 0.17 mm.
            // A half-precision read keeps 11 bits — about 5 mm at the top of the range — so every declaration
            // is a float texture and every sample lands in a float. (The import format is
            // SeabedHeightImportTests' half of this proof.)
            var decl = new Regex(@"\bTEXTURE2D\w*\s*\(\s*(_HeightTex|_HHStillTex)\s*\)");
            var use = new Regex(@"\b(SAMPLE_TEXTURE2D\w*|LOAD_TEXTURE2D\w*|tex2D\w*)\s*\(\s*(_HeightTex|_HHStillTex)\b");
            var floatSample = new Regex(@"^\s*float\s+\w+\s*=\s*SAMPLE_TEXTURE2D(_LOD)?\s*\(\s*(_HeightTex|_HHStillTex)\s*,[^;]*\)\s*\.r\s*;\s*$");

            var sites = new List<string>();
            int stillDecl = 0, stillSamples = 0, heightSamples = 0;
            foreach (string path in Directory.GetFiles(ShaderDir, "*.*", SearchOption.AllDirectories)
                         .Where(p => p.EndsWith(".shader") || p.EndsWith(".hlsl") || p.EndsWith(".cginc"))
                         .Select(p => p.Replace('\\', '/')).OrderBy(p => p))
            {
                string[] lines = Code(path).Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string where = path + ":" + (i + 1);
                    foreach (Match m in decl.Matches(line))
                    {
                        Assert.IsTrue(m.Value.StartsWith("TEXTURE2D("),
                            where + " declares " + m.Groups[1].Value + " as '" + m.Value + "' — a height map is a float texture");
                        if (m.Groups[1].Value == "_HHStillTex") stillDecl++;
                    }
                    if (!use.IsMatch(line)) continue;
                    Assert.IsTrue(floatSample.IsMatch(line),
                        where + " reads a height-scale map outside the 'float r = SAMPLE_TEXTURE2D(...).r;' form: '" +
                        line.Trim() + "' — the value must land in a float");
                    if (line.Contains("_HHStillTex")) stillSamples++; else heightSamples++;
                    sites.Add(where);
                }
            }

            Assert.AreEqual(1, stillDecl, "the still map is declared once, in the shared include");
            Assert.GreaterOrEqual(stillSamples, 1, "the still map is sampled");
            Assert.GreaterOrEqual(heightSamples, 1, "the seabed height map is sampled");
            Debug.Log("[StillWaterShader] height-scale reads, all float: " + string.Join(", ", sites));
        }

        // ---- source helpers ------------------------------------------------------------------------------

        /// <summary>A shader file with its line comments stripped, so prose can neither satisfy nor break a guard.</summary>
        private static string Code(string path)
            => string.Join("\n", File.ReadAllText(path, Encoding.UTF8).Replace("\r", "").Split('\n').Select(line =>
            {
                int i = line.IndexOf("//", System.StringComparison.Ordinal);
                return i >= 0 ? line.Substring(0, i) : line;
            }));

        private static string StillLevelAtBody()
        {
            string include = Code(IncludePath);
            Match head = Regex.Match(include, @"float\s+StillLevelAt\s*\(\s*float2\s+\w+\s*\)\s*\{");
            Assert.IsTrue(head.Success, "StillLevelAt(float2) is defined in " + IncludePath);
            int depth = 1, i = head.Index + head.Length;
            for (; i < include.Length && depth > 0; i++)
            {
                if (include[i] == '{') depth++;
                else if (include[i] == '}') depth--;
            }
            return include.Substring(head.Index + head.Length, i - head.Index - head.Length - 1);
        }

        private static int CountOf(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += needle.Length;
            }
            return n;
        }
    }
}
