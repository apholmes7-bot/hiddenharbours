using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>Nothing near the beach slides along it</b> (owner clip review 2026-11-15: "All pulsing should be with
    /// the waves and move perpendicular to the shoreline").
    ///
    /// <para>🔴 <b>What these would have caught.</b> The measured pulse on the St Peters clip ran 8-9 m/s
    /// along the wash. It had three sources, each tested here: (1) the bore's clock read the offshore train's
    /// phase at the break-line point, so an oblique swell reached each metre of beach at a different moment;
    /// (2) the swell's own light was drawn at its offshore heading all the way to the waterline; (3) the
    /// field-driven caustics took their curvature from four RAW wave-field taps around a yielded centre, which
    /// focused the oblique offshore crests into light running along the shallows. That last one survived
    /// zeroing every foam layer and the swell sample, and was found by returning the colour at successive
    /// points of the composite.</para>
    /// </summary>
    public class ShoreSurfPhaseTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";

        static string Src() => File.ReadAllText(ShaderPath, Encoding.UTF8).Replace("\r\n", "\n");

        static string[] Materials()
        {
            var list = new System.Collections.Generic.List<string> { "Assets/_Project/Art/Materials/Water.mat" };
            list.AddRange(Directory.GetFiles("Assets/_Project/Art/Materials/WaterPresets", "*.mat"));
            return list.ToArray();
        }

        // ---- (1) the refracted bore clock -----------------------------------------------------------------

        static WaveTrain ObliqueTrain() =>
            new WaveTrain(new Vector2(0.6f, -0.8f), 40f, 0.6f, 0.3f, 9.81f);

        [Test]
        public void TheRefractedBore_ReachesEveryPointOfAStraightBeach_Together()
        {
            BreakerSettings s = BreakerSettings.Default;
            s.BorePhaseJitterDegrees = 0f;                  // the jitter is tested on its own below
            Assert.AreEqual(1f, s.BoreRefraction, 0f, "the shipped refraction is full: no alongshore phase");
            WaveTrain train = ObliqueTrain();
            const float travel = 2.5f;
            for (double t = 0.0; t < 20.0; t += 0.37)
            {
                float a = BreakerMath.BoreClockPhaseDegrees(in train, new Vector2(-30f, 5f), travel, in s, 1f, t);
                for (float x = -20f; x <= 30f; x += 7f)
                {
                    float b = BreakerMath.BoreClockPhaseDegrees(in train, new Vector2(x, 5f), travel, in s, 1f, t);
                    Assert.AreEqual(0f, Mathf.DeltaAngle(a, b), 1e-3f,
                        $"t={t:F2}: x={x} on the beach is at {b:F2} deg while x=-30 is at {a:F2} deg");
                }
            }
        }

        [Test]
        public void UnrefractedBore_IsTheOffshoreHeading_AsBefore()
        {
            BreakerSettings s = BreakerSettings.Default;
            s.BoreRefraction = 0f;
            s.BorePhaseJitterDegrees = 0f;
            WaveTrain train = ObliqueTrain();
            var p = new Vector2(12f, -3f);
            Assert.AreEqual(BreakerMath.BorePhaseDegrees(in train, p, 1.5f, 1f, 4.0),
                            BreakerMath.BoreClockPhaseDegrees(in train, p, 1.5f, in s, 1f, 4.0), 1e-3f);
        }

        [Test]
        public void TheJitter_IsStatic_SoItCanNeverTravel()
        {
            BreakerSettings s = BreakerSettings.Default;
            Assert.Greater(s.BorePhaseJitterDegrees, 0f, "the shipped jitter is on");
            WaveTrain train = ObliqueTrain();
            for (float x = -40f; x <= 40f; x += 9f)
            {
                var p = new Vector2(x, 2f);
                // The offset between two points never changes with time: the same at t and t + anything.
                float d0 = Mathf.DeltaAngle(BreakerMath.BoreClockPhaseDegrees(in train, p, 2f, in s, 1f, 0.0),
                                            BreakerMath.BoreClockPhaseDegrees(in train, Vector2.zero, 2f, in s, 1f, 0.0));
                float d1 = Mathf.DeltaAngle(BreakerMath.BoreClockPhaseDegrees(in train, p, 2f, in s, 1f, 13.7),
                                            BreakerMath.BoreClockPhaseDegrees(in train, Vector2.zero, 2f, in s, 1f, 13.7));
                Assert.AreEqual(d0, d1, 1e-2f, $"the jitter at x={x} moved with time");
                Assert.LessOrEqual(Mathf.Abs(BreakerMath.BorePhaseJitterDegrees(p, in s)),
                                   s.BorePhaseJitterDegrees + 1e-4f);
            }
        }

        [Test]
        public void TheFetchProbe_ShipsSeawardOfThePixel()
        {
            Assert.Greater(BreakerSettings.Default.BoreFetchProbeMeters, 0f,
                "the drawn wash reads its fetch seaward of the pixel (the lee-strip notches)");
            string src = Src();
            StringAssert.Contains("_BreakerRefract", src, "the shader reads the bridge's refraction vector");
        }

        // ---- (2) + (3) the swell's light and the caustics yield near the shore ----------------------------

        [Test]
        public void TheSwell_KeepsAllItsLightOffshore_AndYieldsToTheWaterline()
        {
            const float yield = 1f, shoal = 1.5f;
            Assert.AreEqual(1f, WaterSurface.SwellShoreKeep(0f, 3f, yield, shoal), 0f, "offshore is untouched");
            Assert.AreEqual(1f, WaterSurface.SwellShoreKeep(0f, shoal, yield, shoal), 0f);
            Assert.AreEqual(0f, WaterSurface.SwellShoreKeep(0f, 0f, yield, shoal), 0f, "nothing at the waterline");
            Assert.AreEqual(0f, WaterSurface.SwellShoreKeep(1f, 2.5f, yield, shoal), 0f, "nothing in the break");
            float prev = -1f;
            for (float d = 0f; d <= 2f * shoal; d += 0.05f)
            {
                float k = WaterSurface.SwellShoreKeep(0f, d, yield, shoal);
                Assert.GreaterOrEqual(k, prev - 1e-6f, "the swell's light grows monotonically seaward");
                prev = k;
            }
            // shoalDepth 0 is the break gate alone (the previous shader).
            Assert.AreEqual(1f - 0.4f, WaterSurface.SwellShoreKeep(0.4f, 0f, 1f, 0f), 1e-6f);
            Assert.AreEqual(1f, WaterSurface.SwellShoreKeep(0.7f, 0.1f, 0f, shoal), 0f, "yield 0 keeps everything");
        }

        [Test]
        public void TheShader_YieldsTheSwellAndTheCausticTaps_ThroughOneGate()
        {
            string src = Src();
            StringAssert.Contains("float SwellShoreKeep(float surfBreaking01, float depthM)", src);
            StringAssert.Contains("float swellKeep = SwellShoreKeep(surfBreaking, depth);", src);
            StringAssert.Contains("chx1 *= swellKeep; chx0 *= swellKeep; chy1 *= swellKeep; chy0 *= swellKeep;", src,
                "the caustics' curvature taps must read the same yielded field as the centre");
            int taps = src.IndexOf("chx1 *= swellKeep;", System.StringComparison.Ordinal);
            int use = src.IndexOf("caustic = HHCausticPattern(caustic, waveHeight", System.StringComparison.Ordinal);
            Assert.Greater(use, taps, "the taps are yielded before the curvature reads them");
            StringAssert.Contains(
                "_SwellShoalDepth   (\"Surf: depth (m) over which the offshore swell's light yields to the " +
                "shore-normal surf (0 = off)\", Float) = 1.5", src);
            StringAssert.DoesNotContain("DBG-LOCAL", src, "a local debug line was left in the shader");
        }

        [Test]
        public void EveryWaterMaterial_SerializesTheShoalBand_AtTheShaderDefault()
        {
            foreach (string mat in Materials())
            {
                string yaml = File.ReadAllText(mat, Encoding.UTF8).Replace("\r\n", "\n");
                StringAssert.Contains("    - _SwellShoalDepth: 1.5\n", yaml, mat);
                StringAssert.Contains("    - _SurfSwellYield: 1\n", yaml, mat);
            }
        }
    }
}
