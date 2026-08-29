using System.IO;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The BEAM WAVE RELIEF (owner mandate, 2026-08-28): <i>"the spotlight over the water is just one uniform
    /// shape with a gentle gradient... it should highlight the water at crests and be shadowed at the valleys
    /// of waves unless the proper light angle exposes them."</i>
    ///
    /// <para>His sentence has three clauses and each one is a test here: crests catch the light, valleys lose
    /// it, and the LIGHT ANGLE decides how much. The third is the one that makes this physics rather than
    /// decoration — it is not a special case in the code, it is what <c>N·L</c> does when the light is a POINT
    /// at a HEIGHT instead of the sun at infinity.</para>
    ///
    /// <para><b>The load-bearing guarantee is the NEGATIVE one:</b> on flat water the relief is <b>exactly</b>
    /// 1, so a searchlight sweeping a dead-calm sea leaves the sacred glass mirror precisely as it is today.
    /// That is asserted bit-exactly, over a sweep of lamp geometries, rather than eyeballed — because "the
    /// calm still looks right" is not a thing a screenshot can prove.</para>
    /// </summary>
    public sealed class BeamWaveReliefTests
    {
        private const float MinElev = LightMath.BeamReliefMinElevation;
        private const float MaxGain = 2.5f;

        // A lamp geometry sweep wide enough that "exactly 1" cannot be a coincidence of one arrangement:
        // near and far, low and high, left and right, and straight overhead.
        private static readonly Vector3[] LampGeometries =
        {
            new Vector3(0f, 0f, 2.5f),          // straight overhead
            new Vector3(12f, 0f, 2.5f),         // out along +x, a raking beam
            new Vector3(-30f, 7f, 0.4f),        // far and very low: the hardest rake
            new Vector3(3f, -4f, 40f),          // absurdly high: nearly a downward flood
            new Vector3(60f, 60f, 1f),          // the far corner of a long throw
            new Vector3(0.01f, 0.01f, 0.01f),   // effectively AT the lamp
        };

        // ------------------------------------------------------------------------------------------------
        // 1. THE GLASS-CALM GUARANTEE — the mirror survives a searchlight playing across it.
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void FlatWater_IsExactlyNeutral_AtEveryLampGeometry()
        {
            foreach (Vector3 g in LampGeometries)
            {
                float relief = LightMath.WaveReliefFactor(0f, 0f, g.x, g.y, g.z, MinElev, MaxGain);
                Assert.AreEqual(1f, relief, 0f,
                    $"zero slope must divide out to EXACTLY 1 (lamp {g}) — the calm is sacred, and an " +
                    "epsilon here would be a beam that faintly re-shades a mirror.");
            }
        }

        [Test]
        public void FlatWater_IsExactlyNeutral_EvenBelowTheGrazingFloor()
        {
            // A lamp at (or under) the elevation floor is the case where a naive implementation stops
            // cancelling: it clamps the numerator's elevation but not the divisor's. Same floored lz in both
            // places is what keeps this exact.
            for (float h = 0f; h <= 0.2f; h += 0.01f)
            {
                float relief = LightMath.WaveReliefFactor(0f, 0f, 25f, 0f, h, MinElev, MaxGain);
                Assert.AreEqual(1f, relief, 0f, $"flat water under a lamp {h} m up must still be exactly 1");
            }
        }

        [Test]
        public void StrengthZero_IsABitExactPassthrough()
        {
            foreach (float relief in new[] { 0f, 0.25f, 1f, 1.7f, MaxGain })
                Assert.AreEqual(1f, LightMath.ApplyReliefStrength(relief, 0f), 0f,
                    "the dial at 0 must be the shipped flat cone EXACTLY — the #680 strength-dial precedent");
        }

        [Test]
        public void StrengthOne_IsTheReliefItself()
        {
            foreach (float relief in new[] { 0f, 0.25f, 1f, 1.7f, MaxGain })
                Assert.AreEqual(relief, LightMath.ApplyReliefStrength(relief, 1f), 1e-6f);
        }

        // ------------------------------------------------------------------------------------------------
        // 2. HIS SENTENCE, CLAUSE BY CLAUSE.
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void AFaceTiltedIntoTheBeam_IsLit_AndTheBackSlopeIsShadowed()
        {
            // Lamp out along +x, low enough to rake. A face whose downhill side looks at the lamp has a
            // NEGATIVE x-slope (the normal's ground component is minus the gradient).
            const float lampX = 20f, lampY = 0f, lampH = 2.5f;
            float lit = LightMath.WaveReliefFactor(-0.35f, 0f, lampX, lampY, lampH, MinElev, MaxGain);
            float flat = LightMath.WaveReliefFactor(0f, 0f, lampX, lampY, lampH, MinElev, MaxGain);
            float shadowed = LightMath.WaveReliefFactor(+0.35f, 0f, lampX, lampY, lampH, MinElev, MaxGain);

            Assert.Greater(lit, flat, "a face turned INTO the beam must catch more than flat water");
            Assert.Less(shadowed, flat, "the back slope behind the crest must fall into shadow");
            Assert.Greater(lit - shadowed, 0.5f,
                $"the two faces of one wave must read as genuinely different light, not a hairline " +
                $"(lit {lit:F3} vs shadowed {shadowed:F3})");
        }

        [Test]
        public void AFaceTurnedFullyAway_GoesDark_AndNeverNegative()
        {
            float relief = LightMath.WaveReliefFactor(4f, 0f, 30f, 0f, 0.3f, MinElev, MaxGain);
            Assert.AreEqual(0f, relief, 1e-6f, "a facet turned away from the lamp receives no direct light");
            Assert.GreaterOrEqual(relief, 0f, "and it must never go negative — that would DARKEN the sea");
        }

        [Test]
        public void TheGain_IsBoundedByMaxGain()
        {
            // A steep facet square-on to a grazing lamp is the runaway case.
            float relief = LightMath.WaveReliefFactor(-6f, 0f, 40f, 0f, 0.05f, MinElev, MaxGain);
            Assert.LessOrEqual(relief, MaxGain + 1e-6f, "crest sparkle must stay bounded by the max-gain dial");
        }

        /// <summary>
        /// <b>"...unless the proper light angle exposes them."</b> The clause that makes this physics. A LOW
        /// lamp must separate crest from trough far harder than a HIGH one — and the high lamp must flatten
        /// the sea back toward the uniform disc the owner complained about. Both arms are measured on the SAME
        /// real wave field, so neither can pass by construction.
        /// </summary>
        [Test]
        public void AGrazingLamp_SeparatesCrestFromTrough_FarMoreThanAHighLampDoes()
        {
            WaveTrains trains = RealSea();
            float lowSpread = ReliefSpreadAcrossTheField(trains, lampHeight: 1.0f);
            float highSpread = ReliefSpreadAcrossTheField(trains, lampHeight: 60f);

            // Thresholds set from Measure_ReliefSpreadAgainstLampHeight, not from taste: on this sea the
            // measured spreads are 1.093 (1 m) and 0.044 (60 m) - a ratio of 24.6. The bars below sit well
            // inside that so an ordinary retune does not redden them, while a beam that stopped caring about
            // its own height would fail on the ratio immediately.
            Assert.Greater(lowSpread, highSpread * 8f,
                $"a raking beam must expose the wave shape a high one flattens - low spread {lowSpread:F3} " +
                $"vs high spread {highSpread:F3}. If these converge, the angle has stopped mattering and " +
                "the beam is the uniform disc the owner complained about.");
            Assert.Less(highSpread, 0.15f,
                $"a lamp nearly overhead must read close to flat (spread {highSpread:F3}, measured 0.044)");
            Assert.Greater(lowSpread, 0.6f,
                $"a raking lamp must genuinely model the sea (spread {lowSpread:F3}, measured 1.093)");
        }

        /// <summary>
        /// The limit that proves the angle term is real physics and not a fudge: push the lamp far enough away
        /// and it becomes the SUN - a direction at infinity - so the whole directional part of the relief must
        /// vanish, leaving only the area foreshortening a tilted facet always suffers
        /// (<c>1 - 1/sqrt(1 + |slope|^2)</c>). Measured, that limit lands at 0.0279 against a computed floor of
        /// 0.0273. A model that kept any angular dependence out there would not converge on that number.
        /// </summary>
        [Test]
        public void ALampPushedToInfinity_BecomesTheSun_AndOnlyForeshorteningRemains()
        {
            WaveTrains trains = RealSea();
            float farSpread = ReliefSpreadAcrossTheField(trains, lampHeight: 1000f);

            float steepest = 0f;
            for (int i = 0; i < 512; i++)
            {
                WaveSample s = WaveMath.Sample(new Vector2(i * (16f / 512f), 0f), 0d, in trains);
                steepest = Mathf.Max(steepest, s.Slope.magnitude);
            }
            float geometricFloor = 1f - 1f / Mathf.Sqrt(1f + steepest * steepest);

            Assert.AreEqual(geometricFloor, farSpread, 0.01f,
                $"at infinity the relief must reduce to pure foreshortening: spread {farSpread:F4} vs the " +
                $"geometric floor {geometricFloor:F4}");
        }

        /// <summary>
        /// The consequence nobody has to author: the elevation to a FIXED-height lamp falls off with distance,
        /// so the far end of one beam rakes harder than its near end. This is why the pool of light stops
        /// reading as a stamped ellipse.
        /// </summary>
        [Test]
        public void TheFarEndOfOneThrow_RakesHarderThanItsNearEnd()
        {
            const float slope = -0.3f, lampH = 3f;
            float near = LightMath.WaveReliefFactor(slope, 0f, 3f, 0f, lampH, MinElev, MaxGain);
            float far = LightMath.WaveReliefFactor(slope, 0f, 45f, 0f, lampH, MinElev, MaxGain);
            Assert.Greater(far, near,
                $"the same facet must catch the beam harder at the far end of the throw (near {near:F3}, " +
                $"far {far:F3}) — the incidence is grazing out there and steep underfoot");
        }

        [Test]
        public void OnARealSea_TheReliefIsNeitherDeadNorSaturated()
        {
            // The measurement that catches an inert dial: over a real field the relief must actually vary,
            // and must not be pinned at either rail. (The breaking-waves lane shipped a dial that could
            // never fire; measuring the distribution is how that gets caught before the owner's eye does.)
            WaveTrains trains = RealSea();
            float min = float.MaxValue, max = float.MinValue;
            int distinct = 0;
            float last = float.NaN;
            for (int i = 0; i < 256; i++)
            {
                var pos = new Vector2(i * 0.37f, i * 0.11f);
                WaveSample s = WaveMath.Sample(pos, 0d, in trains);
                float r = LightMath.WaveReliefFactor(s.Slope.x, s.Slope.y,
                                                     40f - pos.x, 5f - pos.y, 2.5f, MinElev, MaxGain);
                min = Mathf.Min(min, r);
                max = Mathf.Max(max, r);
                if (!Mathf.Approximately(r, last)) { distinct++; last = r; }
            }

            Assert.Greater(distinct, 200, "the relief must vary continuously over the sea, not sit on a step");
            Assert.Less(min, 0.9f, $"some water must fall into shadow (min {min:F3})");
            Assert.Greater(max, 1.1f, $"some water must catch the light (max {max:F3})");
        }

        // ------------------------------------------------------------------------------------------------
        // 3. THE TWIN AND THE SLOT COUNT — guards against the shader drifting away from the C# reference.
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void TheShader_CarriesTheSameReliefExpression_AsTheCSharpReference()
        {
            string src = WaterShaderSource();
            StringAssert.Contains("float BeamRelief(float2 slopeXY, float3 toLamp)", src,
                "the HLSL twin of LightMath.WaveReliefFactor must exist by that name");
            StringAssert.Contains("float lz = max(L.z, max(_BeamReliefMinElevation, 1e-4));", src,
                "the elevation must be floored ONCE — floor it twice, differently, and flat water stops " +
                "cancelling to exactly 1");
            StringAssert.Contains("return clamp((lz - sDotL) * invN / lz, 0.0, max(_BeamReliefMaxGain, 1.0));", src,
                "the relief expression itself must match the C# reference, term for term");
            StringAssert.Contains("relief = 1.0 + (BeamRelief(waveSlopeXY, toLamp) - 1.0) * reliefStrength;", src,
                "the strength blend must be the exact-passthrough form, not a lerp that rounds");
        }

        [Test]
        public void TheShader_SkipsTheRelief_WhenNoLampHeightIsPublished()
        {
            StringAssert.Contains("if (lightPos.z > 1e-4 && reliefStrength > 0.001)", WaterShaderSource(),
                "a lamp with no published height must fall back to the flat ADR 0016 cone, EXACTLY — that " +
                "is what keeps a legacy publisher and a bare material looking like they do today");
        }

        [Test]
        public void TheShaderSlotCount_MatchesTheBridge()
        {
            StringAssert.Contains($"#define WATER_MAX_LIGHTS {WaterLightBridge.MaxLights}", WaterShaderSource(),
                "the shader's array bound and WaterLightBridge.MaxLights must be the same number, or the " +
                "bridge publishes slots the shader never reads (or reads slots it never wrote)");
        }

        [Test]
        public void TheWaterSumsTheArrayOrTheSingleton_ButNeverBoth()
        {
            string src = WaterShaderSource();
            StringAssert.Contains("if (n <= 0)", src,
                "count 0 must fall back to the legacy singleton path");
            // The primary lamp is published to BOTH the array (for the water) and the singleton (for the
            // decor path). If the water ever summed them together it would count that lamp twice.
            int arrayAdds = CountOccurrences(src, "total += BoatLightWeight(_WaterLightPos[i]");
            int singletonReturns = CountOccurrences(src, "return BoatLightWeight(_BoatLightPos");
            Assert.AreEqual(1, arrayAdds, "exactly one array accumulation");
            Assert.AreEqual(1, singletonReturns, "exactly one singleton fallback, and it RETURNS (never adds)");
        }

        // ------------------------------------------------------------------------------------------------
        // Helpers.
        // ------------------------------------------------------------------------------------------------

        /// <summary>A real, moderate sea from the shipped derivation — not a hand-made slope.</summary>
        private static WaveTrains RealSea()
        {
            WaveFieldSettings settings = WaveFieldSettings.Default;
            WaveTrains trains = WaveMath.TrainsFrom(new Vector2(9f, 3f), 0.55f, in settings);
            Assert.Greater(trains.Count, 0, "the sea under test must actually have waves");
            return trains;
        }

        /// <summary>
        /// How widely the relief swings across a real field for a lamp at <paramref name="lampHeight"/> —
        /// the measurable form of "does the beam model the sea or flatten it". Sampled along a line the
        /// waves actually cross, with the lamp fixed so only its ELEVATION differs between arms.
        /// </summary>
        private static float ReliefSpreadAcrossTheField(in WaveTrains trains, float lampHeight)
        {
            // The lamp sits directly ABOVE THE CENTRE of a short patch, so HEIGHT is the only thing that
            // differs between the two arms. (An earlier draft ran the samples 128 m downrange of a fixed
            // lamp, which meant the "high" arm was still looking along the sea at a shallow angle — the
            // test was comparing two grazing beams and calling one of them overhead.)
            const float patchMetres = 16f;
            const int samples = 512;
            float lampX = patchMetres * 0.5f;
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < samples; i++)
            {
                var pos = new Vector2(i * (patchMetres / samples), 0f);
                WaveSample s = WaveMath.Sample(pos, 0d, in trains);
                float r = LightMath.WaveReliefFactor(s.Slope.x, s.Slope.y,
                                                     lampX - pos.x, 0f, lampHeight,
                                                     MinElev, MaxGain);
                min = Mathf.Min(min, r);
                max = Mathf.Max(max, r);
            }
            return max - min;
        }

        /// <summary>
        /// Not an assertion — a MEASUREMENT, printed so the thresholds above are set from what the shipped
        /// model actually does rather than from what seemed reasonable. Kept because it is also the cheapest
        /// way to see, later, that a retune has not quietly flattened the beam.
        /// </summary>
        [Test]
        public void Measure_ReliefSpreadAgainstLampHeight()
        {
            WaveTrains trains = RealSea();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("lamp height (m) -> relief spread over a real sea");
            foreach (float h in new[] { 0.5f, 1f, 2.5f, 5f, 10f, 20f, 60f, 200f, 1000f })
                sb.AppendLine($"  {h,7:F1}  ->  {ReliefSpreadAcrossTheField(trains, h):F4}");
            float maxAbsSlope = 0f;
            for (int i = 0; i < 512; i++)
            {
                WaveSample s = WaveMath.Sample(new Vector2(i * (16f / 512f), 0f), 0d, in trains);
                maxAbsSlope = Mathf.Max(maxAbsSlope, s.Slope.magnitude);
            }
            sb.AppendLine($"  steepest slope on this sea: {maxAbsSlope:F4}");
            sb.AppendLine($"  geometric floor 1-1/sqrt(1+s^2) = {1f - 1f / Mathf.Sqrt(1f + maxAbsSlope * maxAbsSlope):F4}");
            Debug.Log(sb.ToString());
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int n = 0, at = 0;
            while ((at = haystack.IndexOf(needle, at, System.StringComparison.Ordinal)) >= 0) { n++; at += needle.Length; }
            return n;
        }

        private static string WaterShaderSource()
        {
            string path = Path.Combine(Application.dataPath,
                "_Project", "Art", "Shaders", "HiddenHarboursWater.shader");
            Assert.IsTrue(File.Exists(path), $"the water shader must be readable at {path}");
            return File.ReadAllText(path, System.Text.Encoding.UTF8);
        }
    }
}
