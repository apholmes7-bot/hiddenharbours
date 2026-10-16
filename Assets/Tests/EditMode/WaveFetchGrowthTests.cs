using System;
using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WaveFetchGrowthTests
    {
        public static WaveFetchSettings ShippedFetch()
        {
            var s = WaveFetchSettings.Default;
            s.Strength = 0.8f;
            return s;
        }

        public static WaveFieldSettings Field()
        {
            var s = WaveFieldSettings.Default;
            s.SpectrumBlend = 1f;
            s.SpectrumBinCount = 8;
            s.SpectrumLadderMinWavelengthMeters = 5f;
            s.SpectrumLadderMaxWavelengthMeters = 30f;
            s.HeightFromFetch = true;
            s.SeaFetchKilometres = 25f;
            return s;
        }

        [Test]
        public void LeeWater_HasOnlyShortTrainsAtTheBeach()
        {
            var s = ShippedFetch();
            var field = Field();
            var trains = WaveMath.TrainsFrom(new Vector2(10, 0), 1f, in field);
            for (int i = 0; i < trains.Count; i++)
            {
                float e = WaveFetch.TrainEnvelope01(0f, trains[i].Wavelength, in s);
                if (trains[i].Wavelength >= 8.5f) Assert.AreEqual(0f, e, "long train at beach");
                else Assert.Greater(e, 0f, "short chop at beach");
            }
            Assert.AreEqual(1f, WaveFetch.TrainEnvelope01(8f / 96f, 5f, in s));
            Assert.Less(WaveFetch.TrainEnvelope01(8f / 96f, 30f, in s), 0.02f);
        }

        [Test]
        public void OpenWater_IsTodaysSea_BitForBit()
        {
            var s = ShippedFetch();
            var field = Field();
            foreach (float speed in new[] { 0f, 2.3f, 5f, 10f })
            {
                var trains = WaveMath.TrainsFrom(new Vector2(speed, speed * 0.3f), speed / 14f, in field);
                for (int i = 0; i < 100; i++)
                {
                    var pos = new Vector2(i * 1.73f - 70f, i * -0.91f);
                    double time = i * 100003.17;
                    var old = Before(pos, time, in trains);
                    var full = new WaveFetchSample(1f, in s);
                    SameBits(old, WaveMath.Sample(pos, time, in trains, full));
                    s.Strength = 0f;
                    var off = new WaveFetchSample(0f, in s);
                    SameBits(old, WaveMath.Sample(pos, time, in trains, off));
                    s.Strength = 0.8f;
                }
            }
        }

        private static void SameBits(WaveSample a, WaveSample b)
        {
            Assert.AreEqual(BitConverter.SingleToInt32Bits(a.Height), BitConverter.SingleToInt32Bits(b.Height));
            Assert.AreEqual(BitConverter.SingleToInt32Bits(a.Slope.x), BitConverter.SingleToInt32Bits(b.Slope.x));
            Assert.AreEqual(BitConverter.SingleToInt32Bits(a.Slope.y), BitConverter.SingleToInt32Bits(b.Slope.y));
            Assert.AreEqual(BitConverter.SingleToInt32Bits(a.CrestFactor), BitConverter.SingleToInt32Bits(b.CrestFactor));
        }

        // Frozen e4762df0 arithmetic, independently evaluated without the new payload.
        private static WaveSample Before(Vector2 pos, double time, in WaveTrains trains)
        {
            float h = 0, sx = 0, sy = 0, total = 0;
            float p = trains.CrestSharpening;
            for (int i = 0; i < trains.Count; i++)
            {
                var t = trains[i]; float a = t.Amplitude; total += a;
                if (a <= 0f) continue;
                float k = (2f * Mathf.PI) / t.Wavelength;
                double travel = (double)t.Direction.x * pos.x + (double)t.Direction.y * pos.y
                              - (double)t.PhaseSpeed * time;
                double theta = k * travel + t.PhaseOffset;
                theta -= Math.Floor(theta / (2.0 * Math.PI)) * (2.0 * Math.PI);
                float sin = Mathf.Sin((float)theta), cos = Mathf.Cos((float)theta);
                float v = (sin + 1f) * 0.5f;
                h += a * (2f * Mathf.Pow(v, p) - 1f);
                float slope = a * p * Mathf.Pow(v, p - 1f) * cos * k;
                sx += slope * t.Direction.x; sy += slope * t.Direction.y;
            }
            float crest = total > WaveMath.GlassAmplitudeMeters ? Mathf.Pow(Mathf.Clamp01(h / total), p) : 0f;
            return new WaveSample(h, new Vector2(sx, sy), crest);
        }

        [Test]
        public void TheBoatRidesTheDrawnLee()
        {
            var s = ShippedFetch(); var field = Field();
            var animator = new WaveFieldAnimator();
            var smoothing = WaveFieldAnimatorSettings.Default;
            foreach (float speed in new[] { 0f, 2.3f, 5f, 10f })
            {
                var trains = animator.Tick(0.016f, 350.0, new Vector2(speed, speed * 0.7f), speed / 14f,
                    in field, in smoothing);
                var packed = WaveFieldBridge.Pack(in trains);
                foreach (float scale in new[] { 1f, 2.8f })
                for (int j = 0; j <= 96; j++)
                {
                    var pos = new Vector2(j, j * 0.31f);
                    var fetch = new WaveFetchSample(j / 96f, in s);
                    var cpu = WaveMath.Sample(pos * scale, 0.0, in trains, fetch);
                    var gpu = WaveFieldBridge.ShaderTwinSample(pos, in packed, scale, fetch);
                    Assert.AreEqual(cpu.Height, gpu.Height, 1e-3f);
                    Assert.AreEqual(cpu.Slope.x * scale, gpu.Slope.x, 1e-3f);
                    Assert.AreEqual(cpu.Slope.y * scale, gpu.Slope.y, 1e-3f);
                    Assert.AreEqual(cpu.CrestFactor, gpu.CrestFactor, 1e-3f);
                }
            }
        }

        [Test]
        public void ThePerTrainLaw_HasOneTwin_InEachShader()
        {
            string water = Read("Art/Shaders/HiddenHarboursWater.shader");
            string foam = Read("Art/Shaders/HiddenHarboursFoamBufferAdvect.shader");
            string law = @"float FetchTrainEnvelope01\(.*?\n            }";
            string a = Regex.Match(water, law, RegexOptions.Singleline).Value;
            string b = Regex.Match(foam, law, RegexOptions.Singleline).Value;
            Assert.IsNotEmpty(a); Assert.AreEqual(a, b);
            string cpu = Read("Code/Core/Environment/WaveFetch.cs");
            string c = Regex.Match(cpu, @"public static float TrainEnvelope01\(.*?\n        }", RegexOptions.Singleline).Value;
            c = c.Substring(c.IndexOf('{'));
            a = a.Substring(a.IndexOf('{'));
            c = c.Replace("settings.Strength", "_WaveFetchParams.x")
                 .Replace("GrowthShape(in settings)", "_WaveFetchGrowth")
                 .Replace("MarchSteps", "FETCH_MARCH_STEPS")
                 .Replace("MinStepMeters", "0.05").Replace("settings.StepMeters", "_WaveFetchParams.y")
                 .Replace("MinGateBand", "0.001").Replace("settings.LeeFloor", "_WaveFetchParams.z")
                 .Replace("GrowthPower(in settings)", "_WaveFetchGrowthPower")
                 .Replace("FullStrength(in settings)", "_WaveFetchFullStrength")
                 .Replace("Band01(fetch01, settings.Bands)", "FetchBand01(fetch01)")
                 .Replace("Mathf.Clamp01", "saturate").Replace("Mathf.Max", "max")
                 .Replace("Mathf.Min", "min").Replace("Mathf.Lerp", "lerp")
                 .Replace("Mathf.Pow", "pow").Replace("SmoothstepEdge", "smoothstep")
                 .Replace("Vector4", "float4");
            Assert.AreEqual(Canonical(c), Canonical(a), "complete curve expression parity");
            StringAssert.Contains("amplitude *= FetchFactorAt(fetch, i)", water);
            StringAssert.Contains("FetchDominantEnvelope01(FetchMarch01(worldXY))", foam);
            Assert.AreEqual(1, Regex.Matches(water, "float waveFetch01 = FetchMarch01\\(worldXY\\)").Count);
        }

        [Test]
        public void TheMarchFollowsOneWind_OnTheCpuAndTheGpu()
        {
            string services = Read("Code/Core/Services/GameServices.cs");
            string bridge = Read("Code/Art/WaveFieldBridge.cs");
            StringAssert.Contains("FetchWindAt(environment, clock.TotalSeconds, in field)", services);
            StringAssert.Contains("WaveMemory.ShortWind(history, time, in field).Wind", services);
            StringAssert.Contains("GameServices.FetchWindAt(env, time, in field)", bridge);
            StringAssert.Contains("WaveFetch.Upwind(windVector)", bridge);
            StringAssert.Contains("Vector2 upwind = Upwind(windVector)", Read("Code/Core/Environment/WaveFetch.cs"));
        }

        [Test]
        public void StPetersLeeBerth_LongTrainStaysBelowEightPercent()
        {
            // e4762df0 StPetersSeabed_HeightTex.png, mean tide, wind toward 40 degrees.
            // Waterline (-201.25,9.75); 20 m downwind (-185.9291,22.6058).
            // 24 x 4 m march at PPU 32: clear fraction 0.1673190746, from the committed PNG.
            var s = ShippedFetch();
            Assert.Less(WaveFetch.TrainEnvelope01(0.167319075f, 30f, in s), 0.08f);
            Assert.AreEqual(1f, WaveFetch.TrainEnvelope01(0.167319075f, 5f, in s));
        }

        [Test]
        public void MissingGrowthData_UsesDefaults_AndStaysFinite()
        {
            var zero = default(WaveFetchSettings); zero.Strength = 0.8f;
            zero.StepMeters = 4f; zero.LeeFloor = 0.25f;
            var normal = ShippedFetch();
            for (int i = 0; i <= 96; i++)
            for (int j = 5; j <= 30; j++)
                Assert.AreEqual(WaveFetch.TrainEnvelope01(i / 96f, j, in normal),
                    WaveFetch.TrainEnvelope01(i / 96f, j, in zero));
        }

        private sealed class CountingBed : ITidalTerrain
        {
            public int Calls;
            public float ElevationAt(Vector2 position) { Calls++; return 5f; }
        }

        [Test]
        public void SpectralPayload_MarchesOnce_AndOffOrCalmNeverMarches()
        {
            var s = ShippedFetch(); var bed = new CountingBed();
            var fetch = WaveFetch.At(Vector2.zero, Vector2.right, 0f, bed, in s);
            for (int i = 0; i < 8; i++) fetch.ForWavelength(5f + i * 4f);
            Assert.AreEqual(24, bed.Calls);
            bed.Calls = 0;
            var calm = WaveFetch.At(Vector2.zero, Vector2.zero, 0f, bed, in s);
            Assert.AreEqual(1f, calm.ForWavelength(30f));
            s.Strength = 0f;
            var off = WaveFetch.At(Vector2.zero, Vector2.right, 0f, bed, in s);
            Assert.AreEqual(1f, off.ForWavelength(30f));
            Assert.AreEqual(0, bed.Calls);
        }

        [Test]
        public void SpectralLee_WhitecapCeilingFallsWithoutRenormalizing()
        {
            var s = ShippedFetch(); var field = Field();
            var trains = WaveMath.TrainsFrom(new Vector2(10, 0), 1f, in field);
            float sum = 0;
            for (int i = 0; i < trains.Count; i++)
                sum += trains[i].Amplitude * WaveFetch.TrainEnvelope01(0f, trains[i].Wavelength, in s);
            Assert.Less(sum / trains.TotalAmplitude, 0.1f);
            var fetch = new WaveFetchSample(0f, in s);
            for (int i = 0; i < 300; i++)
                Assert.LessOrEqual(WaveMath.Sample(new Vector2(i, 0), i * 0.13, in trains, fetch).CrestFactor,
                    Mathf.Pow(sum / trains.TotalAmplitude, trains.CrestSharpening) + 1e-6f);
        }

        [Test]
        public void TheFringeLee_KeepsOpenWaterAndLightWind_ToTheBit()
        {
            var s = ShippedFetch();
            Vector4 lee = WaveFetch.FringeLee(in s);
            // The lee plates' light wind, 2.3 m/s: roughness is the wind over 12 m/s, the sea state the
            // weather's band axis. Both sit under the ceilings, so the fringe keeps them everywhere.
            Assert.LessOrEqual(2.3f / 12f, lee.x);
            Assert.LessOrEqual(HiddenHarbours.Environment.WeatherModel.SeaState01(2.3f), lee.y);
            for (int i = 0; i <= 64; i++)
            for (int j = 0; j <= 100; j++)
            {
                float reading = i / 64f, factor = j / 100f;
                float kept = WaveFetch.FringeLeeReading(reading, lee.x, WaveFetch.FringeLeeOpen01(factor, lee.z));
                if (factor >= lee.z || reading <= lee.x) Assert.AreEqual(reading, kept);
                else Assert.That(kept, Is.InRange(lee.x, reading));
            }
            // A dead lee reads the ceiling; an unpublished open factor reads open water; zero fields use the defaults.
            Assert.AreEqual(lee.x, WaveFetch.FringeLeeReading(0.8f, lee.x, WaveFetch.FringeLeeOpen01(0f, lee.z)));
            Assert.AreEqual(1f, WaveFetch.FringeLeeOpen01(0f, 0f));
            var zero = default(WaveFetchSettings);
            Assert.AreEqual(WaveFetch.FringeLee(WaveFetchSettings.Default), WaveFetch.FringeLee(in zero));
        }

        [Test]
        public void TheFringeLee_HasOneTwin_AndTheFringeReadsIt()
        {
            string water = Read("Art/Shaders/HiddenHarboursWater.shader");
            string cpu = Read("Code/Core/Environment/WaveFetch.cs");
            string gpuOpen = Group(water, @"float FringeLeeOpen01\(float fetchFactor\)\s*\{\s*return (.*?);");
            string cpuOpen = Group(cpu, @"float FringeLeeOpen01\(float fetchFactor, float openFactor\)\s*=> (.*?);");
            Assert.AreEqual(Canonical(gpuOpen),
                Canonical(cpuOpen.Replace("openFactor", "_WaveFetchFringe.z").Replace("Mathf.Clamp01", "saturate")));
            string gpuRead = Group(water, @"float FringeLeeReading\(float reading, float ceiling, float open01\)\s*\{\s*return (.*?);");
            string cpuRead = Group(cpu, @"float FringeLeeReading\(float reading, float ceiling, float open01\)\s*=> (.*?);");
            Assert.AreEqual(Canonical(gpuRead), Canonical(cpuRead.Replace("Mathf.Lerp", "lerp")));
            // The fringe's density and swash fade keep the open sea's formulas, with the reading as a parameter.
            Assert.AreEqual(Canonical(Group(water, @"float FoamDensity\(\)\s*\{(.*?)\}").Replace("_Roughness", "roughness")),
                Canonical(Group(water, @"float FoamDensityAt\(float roughness\)\s*\{(.*?)\}")));
            Assert.AreEqual(Canonical(Group(water, @"float SwashSeaStateGate\(\)\s*\{(.*?)\}").Replace("_Chop", "sea01")),
                Canonical(Group(water, @"float SwashSeaStateGateAt\(float sea01\)\s*\{(.*?)\}")));
            string code = Canonical(water);
            foreach (string line in new[] {
                "float fringeOpen = FringeLeeOpen01(waveFetchEnv);",
                "float fringeRoughness = FringeLeeReading(_Roughness, _WaveFetchFringe.x, fringeOpen);",
                "float fringeSea = FringeLeeReading(saturate(_Chop), _WaveFetchFringe.y, fringeOpen);",
                "swashGate *= fringeSea < saturate(_Chop) ? SwashSeaStateGateAt(fringeSea) / max(SwashSeaStateGate(), 1e-3) : 1.0;",
                "float foamField = saturate(churn + foamEdge * 0.5 + fringeRoughness * 0.4);",
                "float bandGate = saturate(foamEdge + fringeRoughness * 0.4);",
                "float dens = FoamDensityAt(fringeRoughness);",
                "float capDens = FoamDensity();" })
                Assert.AreEqual(1, Regex.Matches(code, Regex.Escape(Canonical(line))).Count, line);
        }

        [Test]
        public void TheBridge_PublishesTheFringeLee()
        {
            var s = ShippedFetch();
            try
            {
                WaveFieldBridge.PublishFetchGlobals(in s, new Vector2(10f, 0f));
                Assert.AreEqual(WaveFetch.FringeLee(in s), Shader.GetGlobalVector("_WaveFetchFringe"));
            }
            finally { WaveFieldBridge.PublishFetchOff(); }
        }

        private static string Group(string text, string pattern)
        {
            string value = Regex.Match(text, pattern, RegexOptions.Singleline).Groups[1].Value;
            Assert.IsNotEmpty(value, pattern);
            return value;
        }

        private static string Canonical(string text)
        {
            text = Regex.Replace(text, @"(?<=\d)f\b", "");
            text = Regex.Replace(text, @"(?<![\w.])(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?", m =>
                double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)
                    .ToString("G", System.Globalization.CultureInfo.InvariantCulture));
            return Regex.Replace(text, @"\s+", "");
        }

        private static string Read(string path)
            => File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Assets/_Project", path));
    }
}
