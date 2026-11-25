using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using static HiddenHarbours.Tests.Art.EditMode.ShallowsFloorShaderTests;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE SPLAT WARP</b> (terrain-fidelity 3b, seat ruling t16, option B): the splats' half-metre texels
    /// stair-step every painted edge, so the six splat reads sample a point moved by the meander, under one
    /// texel. One helper in the shared include (<c>TSA_WarpUV</c>); the ground and the floor in the shallows
    /// both pass its uv to <c>TSA_GroundWeights</c>, so the floor under the sea still reads the ground at the
    /// terrain's point (<c>ShallowsFloorShaderTests</c> pins that parity statement for statement).
    ///
    /// <para>Read from the sources: CI has no GPU. How the warped edges LOOK is the granted slot's plates.</para>
    ///
    /// <para><b>On the base</b> (4585b97e) the include has no <c>TSA_WarpUV</c>: the three source tests fail at
    /// their first assert; the mix's bound is arithmetic and holds on any base.</para>
    /// </summary>
    public sealed class TerrainSplatWarpTests
    {
        private const string ShaderDir = "Assets/_Project/Art/Shaders";
        private const string IncludePath = ShaderDir + "/Include/TerrainSplatAlbedo.hlsl";
        private const string TerrainPath = ShaderDir + "/HiddenHarboursTerrainSplat.shader";
        private const string Helper = "float2 TSA_WarpUV(float2 wpx, float2 uv)";
        private const string Weights = "void TSA_GroundWeights(";
        private const string TerrainFrag = "half4 frag (Varyings i) : SV_Target";
        private const string FloorHelper = "half4 ShallowsFloorBed(";

        /// <summary>t16: "amplitude under one texel (at most 0.9 texel of the 0.5 m splat)".</summary>
        private const double MostTexels = 0.9;

        /// <summary>The meander, as <c>TSA_GroundWeights</c> reads it: the band ladder's two octaves (the
        /// 16 m wiggle on salt 3, the 6 m detail on salt 7) over the ground's pixel grid.</summary>
        private static readonly string[] Meander =
        {
            "floatwig=TSA_VNoise(wpx/max(_BandWiggleScale,1e-3),3.0)*2.0-1.0;",
            "floatwigD=TSA_VNoise(wpx/max(_BandDetailScale,1e-3),7.0)*2.0-1.0;",
        };

        [Test]
        public void TheAmplitude_IsUnderOneTexel()
        {
            Match m = Regex.Match(Code(IncludePath),
                @"static\s+const\s+float\s+TSA_SPLAT_WARP_TEXELS\s*=\s*([0-9]*\.?[0-9]+)\s*;");
            Assert.IsTrue(m.Success, "the warp's amplitude is one named constant in the include");
            double texels = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.That(texels, Is.GreaterThan(0.0).And.LessThanOrEqualTo(MostTexels),
                "the warp moves an edge inside its own texel, never across a category");
        }

        [Test]
        public void TheWarp_IsDrivenByTheMeander_AndNothingElse()
        {
            string include = Code(IncludePath);
            string helper = Squash(Body(include, Helper));
            string weights = Squash(Body(include, Weights));
            foreach (string octave in Meander)
            {
                StringAssert.Contains(octave, helper, "the warp reads the meander: " + octave);
                StringAssert.Contains(octave, weights, "the same octave the band ladder reads: " + octave);
            }
            // The two octaves mixed into the two axes, scaled to the splat's own texel.
            StringAssert.Contains("float2shift=(0.5*TSA_SPLAT_WARP_TEXELS)*float2(wig+wigD,wig-wigD);", helper);
            StringAssert.Contains("_SplatA.GetDimensions(sw,sh);", helper);
            StringAssert.Contains("returnuv+shift/float2((float)sw,(float)sh);", helper);

            // No texture is read for the warp, and the include declares none of its own.
            foreach (string read in new[] { "SAMPLE_TEXTURE", "TSA_SAMPLE_SPLAT", "TSA_SAMPLE_KIT", "LOAD_TEXTURE", ".Sample(", ".Load(" })
                Assert.AreEqual(-1, helper.IndexOf(read, StringComparison.Ordinal), "the warp reads no texture: " + read);
            Assert.AreEqual(0, Count(include, @"(?<![A-Za-z0-9_])(TEXTURE2D\w*|SAMPLER)\s*\("),
                "the include declares no texture or sampler; the includer declares the six splats");
        }

        /// <summary>The mix (a + b, a - b) / 2 of two octaves each in [-1, 1] is at most one amplitude long:
        /// |shift|^2 = (a^2 + b^2) * T^2 / 2 &lt;= T^2. Swept, so the bound is a figure and not a claim.</summary>
        [Test]
        public void TheMix_IsAtMostOneAmplitudeLong()
        {
            double longest = 0.0;
            for (int i = 0; i <= 200; i++)
            for (int j = 0; j <= 200; j++)
            {
                double a = -1.0 + i / 100.0, b = -1.0 + j / 100.0;
                double x = 0.5 * MostTexels * (a + b), y = 0.5 * MostTexels * (a - b);
                longest = Math.Max(longest, Math.Sqrt(x * x + y * y));
            }
            Assert.That(longest, Is.LessThanOrEqualTo(MostTexels + 1e-9), "the shift's length, at the octaves' extremes");
            Assert.That(longest, Is.GreaterThan(MostTexels - 1e-6), "the bound is reached, so the sweep covers the extremes");
        }

        /// <summary>The warped coordinate reaches the six splat reads and nothing else: in the weights its
        /// only uses are the splat reads, and in both frags the warped name is declared once and used once.</summary>
        [Test]
        public void OnlyTheSplatReads_TakeTheWarpedUV()
        {
            string include = Code(IncludePath);
            string weights = Body(include, Weights);
            int uses = Count(weights, @"(?<![A-Za-z0-9_.])uv(?![A-Za-z0-9_])");
            int reads = Count(weights, @"TSA_SAMPLE_SPLAT\(\s*_Splat[A-F]\s*,\s*sampler_Splat[A-F]\s*,\s*uv\s*\)");
            Assert.AreEqual(6, reads, "the six splat reads");
            Assert.AreEqual(reads, uses, "the weights use uv for the splat reads only");

            string terrainCode = Body(Code(TerrainPath), TerrainFrag);
            string floorCode = Body(Code(WaterPath), FloorHelper);
            string terrain = Squash(terrainCode);
            string floor = Squash(floorCode);
            foreach ((string name, string body) in new[] { ("the ground", terrainCode), ("the floor", floorCode) })
            {
                Assert.AreEqual(2, Count(body, @"(?<![A-Za-z0-9_])suv(?![A-Za-z0-9_])"),
                    name + ": the warped uv is declared once and passed to the weights once; the height, the hem, " +
                    "the wet band, the macro drift and the relight stay on the true uv");
            }
            // The ground's shore re-read takes the same law at its own point (t16, one law or none: one).
            StringAssert.Contains("float2weightWPX=floor(weightWP*ppm)/ppm;", terrain);
            StringAssert.Contains("TSA_GroundWeights(weightWP,weightWPX,TSA_WarpUV(weightWPX,weightUV),weightE,w,intensity);", terrain);
            // The pixel-true blend reads the ground a third time, at the art texel's centre, by the same law
            // (terrain blends; TerrainPixelTrueBlendTests holds that read). A moved premise: two on the base.
            StringAssert.Contains("TSA_GroundWeights(texelWP,texelWPX,TSA_WarpUV(texelWPX,texelUV),texelE,w,intensity);", terrain);
            Assert.AreEqual(3, Count(terrain, @"TSA_WarpUV\("),
                "the ground warps its first read, its shore re-read and its texel re-read, no fourth");
            Assert.AreEqual(1, Count(floor, @"TSA_WarpUV\("), "the floor warps its one read");
        }
    }
}
