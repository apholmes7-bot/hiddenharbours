using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using static HiddenHarbours.Tests.Art.EditMode.ShallowsFloorShaderTests;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>THE BLENDS, DRAWN</b> (terrain blends): where two ground materials meet, the ground shows ONE material
    /// per art texel instead of a weighted fade. <c>Include/TerrainPixelTrueBlend.hlsl</c> holds the law (the
    /// two heaviest materials, the lower index's share against a threshold that belongs to the texel); the
    /// terrain frag reads the ground once more at the texel's centre and hands the weights to it.
    ///
    /// <para>Read from the sources, and the pick's arithmetic modelled here beside the statements it models:
    /// CI has no GPU. How the drawn edges LOOK, and that they stay put under the camera, is the granted slot's
    /// plates (<c>StPetersSplatWarpPlatePlayTests.TheBlends_PlatedSoftAndDrawn</c>).</para>
    ///
    /// <para><b>On the base</b> (3e7188a4) the include does not exist and the frag has no texel read: every
    /// source test fails at its first assert; the model's arithmetic holds on any base.</para>
    /// </summary>
    public sealed class TerrainPixelTrueBlendTests
    {
        private const string ShaderDir = "Assets/_Project/Art/Shaders";
        private const string BlendPath = ShaderDir + "/Include/TerrainPixelTrueBlend.hlsl";
        private const string TerrainPath = ShaderDir + "/HiddenHarboursTerrainSplat.shader";
        private const string MaterialPath = "Assets/_Project/Art/Materials/TerrainSplat.mat";
        private const string TerrainFrag = "half4 frag (Varyings i) : SV_Target";
        private const string Pick = "void TPB_Pick(float threshold, inout float w[HH_MAT_COUNT])";
        private const string Threshold = "float TPB_Threshold(float2 texel, float2 wpx, float ragged, float clumpMetres)";
        private const string Fringe = "float TPB_Fringe(float threshold, float fringe)";
        private const string Gate = "[branch]if(_BlendPixelTrue>0.5&&TPB_Blended(w))";
        private const int Materials = 21;

        private static readonly string[] Tunables = { "_BlendPixelTrue", "_BlendFringe", "_BlendRagged", "_BlendClumpMetres" };

        // ------------------------------------------------------------------------------------------------
        //  The model: TPB_Pick and TPB_Fringe, statement for statement (ThePick_IsTheStatementsModelledHere
        //  holds the include to them).
        // ------------------------------------------------------------------------------------------------

        private static int PickOf(float threshold, float[] w)
        {
            float a = -1f, b = -1f, ia = 0f, ib = 0f;
            for (int i = 0; i < w.Length; i++)
            {
                float wi = w[i];
                if (wi > a) { b = a; ib = ia; a = wi; ia = i; }
                else if (wi > b) { b = wi; ib = i; }
            }
            float lowIsTop = ib >= ia ? 1f : 0f;                 // step(ia, ib)
            float low = Math.Min(ia, ib), high = Math.Max(ia, ib);
            float lowWeight = b + (a - b) * lowIsTop;            // lerp(b, a, lowIsTop)
            float share = lowWeight / Math.Max(a + Math.Max(b, 0f), 1e-4f);
            return (int)(share > threshold ? low : high);
        }

        private static float FringeOf(float threshold, float fringe) =>
            0.5f + (threshold - 0.5f) * Math.Min(1f, Math.Max(0f, fringe));

        private static float Constant(string name)
        {
            Match m = Regex.Match(Code(BlendPath), @"static\s+const\s+float\s+" + name + @"\s*=\s*([0-9]*\.?[0-9]+)\s*;");
            Assert.IsTrue(m.Success, name + " is one named constant in the include");
            return float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static float Margin() => Constant("TPB_THRESHOLD_MARGIN");
        private static float Pure() => Constant("TPB_PURE");

        private static float[] OrderedThresholds()
        {
            Match m = Regex.Match(Code(BlendPath), @"static\s+const\s+float\s+TPB_ORDERED\[16\]\s*=\s*\{([^}]*)\}");
            Assert.IsTrue(m.Success, "the ordered matrix is one named table of sixteen in the include");
            return m.Groups[1].Value.Split(',').Select(v => float.Parse(v.Trim(), CultureInfo.InvariantCulture)).ToArray();
        }

        [Test]
        public void ThePick_IsTheStatementsModelledHere()
        {
            string include = Code(BlendPath);
            string pick = Squash(Body(include, Pick));
            foreach (string statement in new[]
                     {
                         "if(wi>a){b=a;ib=ia;a=wi;ia=(float)i;}elseif(wi>b){b=wi;ib=(float)i;}",
                         "floatlowIsTop=step(ia,ib);",
                         "floatlow=min(ia,ib),high=max(ia,ib);",
                         "floatlowWeight=lerp(b,a,lowIsTop);",
                         "floatshare=lowWeight/max(a+max(b,0.0),1e-4);",
                         "floatpick=share>threshold?low:high;",
                         "w[k]=abs((float)k-pick)<0.5?1.0:0.0;",
                     })
                StringAssert.Contains(statement, pick, "the pick: " + statement);
            StringAssert.Contains("return0.5+(threshold-0.5)*saturate(fringe);", Squash(Body(include, Fringe)));
        }

        /// <summary>Whatever the weights, the texel shows one material whole, and it is one of the two heaviest.
        /// Swept on a fixed seed: three live materials among the twenty-one, every ordered threshold.</summary>
        [Test]
        public void ThePick_IsOneOfTheTwoHeaviest_Always()
        {
            var random = new Random(20261110);
            float[] thresholds = OrderedThresholds().Select(n => (n + 0.5f) / 16f).ToArray();
            for (int trial = 0; trial < 4000; trial++)
            {
                var w = new float[Materials];
                int i0 = random.Next(Materials), i1 = random.Next(Materials), i2 = random.Next(Materials);
                float x = (float)random.NextDouble(), y = (float)random.NextDouble() * (1f - x);
                w[i0] += x; w[i1] += y; w[i2] += 1f - x - y;
                int[] ranked = Enumerable.Range(0, Materials).OrderByDescending(i => w[i]).ThenBy(i => i).ToArray();
                foreach (float t in thresholds)
                {
                    int pick = PickOf(FringeOf(t, 0.5f), w);
                    Assert.That(pick, Is.EqualTo(ranked[0]).Or.EqualTo(ranked[1]), "one of the two heaviest takes the texel");
                }
                // A clean line at fringe 0: where the two weigh differently, the heavier one, at every threshold.
                if (Math.Abs(w[ranked[0]] - w[ranked[1]]) > 1e-4f)
                    foreach (float t in thresholds)
                        Assert.AreEqual(ranked[0], PickOf(FringeOf(t, 0f), w), "fringe 0 is the line of equal weight");
            }
        }

        /// <summary>A texel one material holds is that material at any threshold: the pick never invents a
        /// neighbour, so ground away from an edge is the ground as it was. The threshold's
        /// margin is what makes that true at the noise's extremes.</summary>
        [Test]
        public void APureTexel_IsItsMaterial_AtEveryThreshold()
        {
            for (int material = 0; material < Materials; material++)
            {
                var w = new float[Materials];
                w[material] = 1f;
                for (int k = 0; k <= 100; k++)
                {
                    float t = Margin() + (1f - 2f * Margin()) * k / 100f;
                    Assert.AreEqual(material, PickOf(FringeOf(t, 1f), w), "material " + material + " at threshold " + t);
                }
            }
            StringAssert.Contains(
                "returnclamp(lerp(ordered,clump,saturate(ragged)),TPB_THRESHOLD_MARGIN,1.0-TPB_THRESHOLD_MARGIN);",
                Squash(Body(Code(BlendPath), Threshold)), "the threshold stays inside its margin, so the sweep above is every threshold");
            Assert.That(Margin(), Is.GreaterThan(0f).And.LessThanOrEqualTo(1f / 32f), "inside the ordered matrix's own extremes");
            Assert.That(1f - Pure(), Is.LessThan(Margin()), "a texel the gate calls pure is under every threshold's reach");
        }

        /// <summary>The pattern is one picture across the line where the two materials swap rank: the lower
        /// index is drawn where ITS share is over the threshold, whichever of the two is heavier. And more of
        /// a material is never fewer of its texels: over the sixteen ordered thresholds the count that go to
        /// the lower index never falls as its share rises, from none to all.</summary>
        [Test]
        public void ThePattern_IsWholeAcrossTheSwap_AndItsDensityFollowsTheShare()
        {
            float[] thresholds = OrderedThresholds().Select(n => (n + 0.5f) / 16f).ToArray();
            const int low = 2, high = 18;   // sand under the lawn, two indices far apart
            int before = 0;
            for (int step = 0; step <= 64; step++)
            {
                float share = step / 64f;
                var w = new float[Materials];
                w[low] = share; w[high] = 1f - share;
                int drawnLow = 0;
                foreach (float t in thresholds)
                {
                    int pick = PickOf(t, w);
                    Assert.AreEqual(share > t ? low : high, pick, $"share {share} against threshold {t}");
                    if (pick == low) drawnLow++;
                }
                Assert.GreaterOrEqual(drawnLow, before, "the lower index's texels never thin as its share rises");
                before = drawnLow;
                if (step == 0) Assert.AreEqual(0, drawnLow, "none of it where it has no weight");
                if (step == 64) Assert.AreEqual(thresholds.Length, drawnLow, "all of it where it holds the texel");
            }
        }

        /// <summary>The ordered matrix is the sixteen levels once each, and every two by two block of it holds one
        /// level from each quarter of them: a quarter share is one texel in every block, a half share two, so
        /// the stipple is even at the scale of two texels and never clots.</summary>
        [Test]
        public void TheOrderedMatrix_IsSixteenLevelsOnce_EvenInEveryTwoByTwo()
        {
            float[] m = OrderedThresholds();
            Assert.AreEqual(16, m.Length);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 16).Select(i => (float)i).ToArray(), m, "each level once");
            for (int by = 0; by < 4; by += 2)
            for (int bx = 0; bx < 4; bx += 2)
            {
                int[] quarters = { (int)m[by * 4 + bx] / 4, (int)m[by * 4 + bx + 1] / 4, (int)m[(by + 1) * 4 + bx] / 4, (int)m[(by + 1) * 4 + bx + 1] / 4 };
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, quarters, $"the block at ({bx}, {by})");
            }
            StringAssert.Contains("floatordered=(TPB_ORDERED[c.y*4+c.x]+0.5)/16.0;", Squash(Body(Code(BlendPath), Threshold)));
        }

        /// <summary>The fringe scales the threshold about one half: 0 is the line of equal weight at every
        /// texel, 1 leaves the threshold as it is, and nothing leaves (0, 1).</summary>
        [Test]
        public void TheFringe_IsALineAtZero_AndTheWholeBlendAtOne()
        {
            for (int k = 0; k <= 100; k++)
            {
                float t = k / 100f;
                Assert.AreEqual(0.5f, FringeOf(t, 0f), "fringe 0");
                Assert.AreEqual(t, FringeOf(t, 1f), 1e-6f, "fringe 1");
                Assert.That(FringeOf(t, 0.5f), Is.InRange(0.25f, 0.75f), "fringe one half keeps the middle half");
            }
        }

        /// <summary>The pattern is anchored in the WORLD: the include reads the texel's index and the meander's
        /// noise on the ground's grid, and nothing of the screen, the clock, a texture or a uniform. The frag
        /// hands it the art texel this pixel lies in and reads the weights at that texel's centre, so every
        /// screen pixel of one texel reaches one answer and the camera cannot make an edge crawl.</summary>
        [Test]
        public void TheThreshold_BelongsToTheWorldsTexel_NeverTheScreen()
        {
            string include = Code(BlendPath);
            foreach (string screen in new[]
                     {
                         "positionCS", "SV_Position", "_ScreenParams", "_Time", "unity_", "ddx", "ddy", "fwidth",
                         "SAMPLE_TEXTURE", "TSA_SAMPLE_", "LOAD_TEXTURE", ".Sample(", ".Load(",
                     })
                Assert.AreEqual(-1, include.IndexOf(screen, StringComparison.Ordinal), "the blend law reads no " + screen);
            Assert.AreEqual(0, Count(include, @"(?<![A-Za-z0-9_])_[A-Z][A-Za-z0-9_]*"),
                "the include reads no uniform: its tunables are arguments, so any drawer of the ground can take the law");

            string threshold = Squash(Body(include, Threshold));
            StringAssert.Contains("int2c=int2((int)texel.x&3,(int)texel.y&3);", threshold);
            Assert.AreEqual(2, Count(threshold, @"TSA_VNoise\(wpx/"), "the clump noise is two octaves on the ground's grid");

            string frag = Squash(Body(Code(TerrainPath), TerrainFrag));
            string block = Body(frag, Gate);
            foreach (string statement in new[]
                     {
                         "float2texel=floor(weightWP*ppm);",
                         "float2texelWPX=texel/ppm;",
                         "float2texelWP=(texel+0.5)/ppm;",
                         "float2texelUV=(texelWP-_HeightWorldMin.xy)/max(_HeightWorldSize.xy,float2(1e-3,1e-3));",
                         "floattexelHeight01=SAMPLE_TEXTURE2D_LOD(_HeightTex,sampler_HeightTex,texelUV,0).r;",
                         "floattexelE=lerp(_HeightMin,_HeightMax,texelHeight01);",
                         "TSA_GroundWeights(texelWP,texelWPX,TSA_WarpUV(texelWPX,texelUV),texelE,w,intensity);",
                         "TPB_Pick(TPB_Fringe(TPB_Threshold(texel,texelWPX,_BlendRagged,_BlendClumpMetres),_BlendFringe),w);",
                     })
                StringAssert.Contains(statement, block, "the texel's read: " + statement);
            Assert.AreEqual(-1, block.IndexOf("i.", StringComparison.Ordinal), "the block reads nothing of the fragment's own");
        }

        /// <summary>OFF is the ground as it was. The law is reached through one gate and nowhere else; the gate
        /// stands after the ground's two reads and the shore lines' prepare (they take the soft weights they
        /// always have) and before the first shade; and the frag's first read is the statements the floor in the
        /// shallows is held to (<c>ShallowsFloorShaderTests.WaterAndTerrain_ShareOneSplatInclude</c>), untouched.</summary>
        [Test]
        public void Off_IsTheSoftBlend_TheLawIsBehindOneGate()
        {
            string terrain = Code(TerrainPath);
            string frag = Squash(Body(terrain, TerrainFrag));
            Assert.AreEqual(1, Count(frag, Regex.Escape(Gate)), "one gate");
            string block = Body(frag, Gate);
            string outside = frag.Replace(block, "");
            Assert.AreEqual(1, Count(outside, @"TPB_"), "outside the gate's block the frag names the law once: the gate's own test");
            Assert.AreEqual(3, Count(terrain, @"(?<![A-Za-z0-9_])_BlendPixelTrue(?![A-Za-z0-9_])"),
                "the switch is a property, a declaration and one read");

            int gate = frag.IndexOf(Gate, StringComparison.Ordinal);
            int shoreReRead = frag.IndexOf("TSA_GroundWeights(weightWP,weightWPX,TSA_WarpUV(weightWPX,weightUV),weightE,w,intensity);", StringComparison.Ordinal);
            int shorePrepare = frag.IndexOf("SL_Prepare(", StringComparison.Ordinal);
            int shade = frag.IndexOf("float3col=float3(0,0,0);", StringComparison.Ordinal);
            Assert.That(shorePrepare, Is.GreaterThan(-1).And.LessThan(gate), "the shore lines read the soft weights, before the gate");
            Assert.That(shoreReRead, Is.GreaterThan(-1).And.LessThan(gate), "the gate follows the shore re-read");
            Assert.That(shade, Is.GreaterThan(gate), "the gate stands before the first shade");
            StringAssert.Contains("float2suv=TSA_WarpUV(wpx,uv);TSA_GroundWeights(wp,wpx,suv,e,w,intensity);", frag,
                "the first read is the floor's read, as it was");
        }

        /// <summary>The kit ladder is not this law's: the pick takes the weights alone, so a channel's value still
        /// chooses the ladder step exactly as before (a lawn's mower stripes are two high values read as two tones).</summary>
        [Test]
        public void TheLadder_IsNotTouched()
        {
            string include = Code(BlendPath);
            Assert.AreEqual(0, Count(include, @"(?<![A-Za-z0-9_])intensity(?![A-Za-z0-9_])"), "the include never names the ladder");
            string block = Body(Squash(Body(Code(TerrainPath), TerrainFrag)), Gate);
            Assert.AreEqual(1, Count(block, @"(?<![A-Za-z0-9_])intensity(?![A-Za-z0-9_])"),
                "the block names the ladder once: the re-read that fills it at the texel's centre");
        }

        /// <summary>The four tunables are the material's own properties, declared in its block, and the law ships
        /// ON: the shader's default is 1 and the shipped material does not turn it off.</summary>
        [Test]
        public void TheTunables_AreTheMaterialsProperties_AndTheLawShipsOn()
        {
            string source = File.ReadAllText(TerrainPath);
            string terrain = Code(TerrainPath);
            foreach (string name in Tunables)
            {
                Assert.IsTrue(Regex.IsMatch(source, @"(?m)^\s*" + name + @"\s*\(""[^""]+"",\s*(Float|Range\(0,\s*1\))\)\s*="),
                    name + " is a property of the ground's material");
                Assert.AreEqual(1, Count(terrain, @"float\s+[^;]*(?<![A-Za-z0-9_])" + name + @"(?![A-Za-z0-9_])[^;]*;"),
                    name + " is declared once, in the material's block");
            }
            Match on = Regex.Match(source, @"(?m)^\s*_BlendPixelTrue\s*\(""[^""]+"",\s*Float\)\s*=\s*([0-9.]+)");
            Assert.IsTrue(on.Success && double.Parse(on.Groups[1].Value, CultureInfo.InvariantCulture) > 0.5,
                "the shader's default is ON");
            Assert.IsFalse(Regex.IsMatch(File.ReadAllText(MaterialPath), @"_BlendPixelTrue:\s*0(\.0+)?\s*$", RegexOptions.Multiline),
                "the shipped material does not turn the law off");
        }
    }
}
