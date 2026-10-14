using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WaveMemoryTests
    {
        public sealed class StepWind : IWaveWindSource
        {
            private readonly double _at;
            private readonly Vector2 _before, _after;
            public StepWind(double at, Vector2 before, Vector2 after)
            { _at = at; _before = before; _after = after; }
            public WaveWindSample WaveWindAt(double time)
            {
                Vector2 wind = time < _at ? _before : _after;
                return new WaveWindSample(wind, WeatherModel.SeaState01(wind.magnitude));
            }
        }

        public sealed class RegionWind : IWaveWindSource
        {
            public int Seed;
            public WindProfile Profile;
            public WaveWindSample WaveWindAt(double time)
            {
                var wind = WeatherModel.SampleWind(time, Seed, 75.0, Profile);
                return new WaveWindSample(wind, WeatherModel.SeaState01(wind.magnitude));
            }
        }

        public static void AssertSame(in WaveTrains a, in WaveTrains b)
        {
            Assert.AreEqual(a.Count, b.Count);
            Assert.AreEqual(a.DominantIndex, b.DominantIndex);
            Bits(a.CrestSharpening, b.CrestSharpening);
            for (int i = 0; i < a.Count; i++)
            {
                Bits(a[i].Direction.x, b[i].Direction.x); Bits(a[i].Direction.y, b[i].Direction.y);
                Bits(a[i].Amplitude, b[i].Amplitude); Bits(a[i].Wavelength, b[i].Wavelength);
                Bits(a[i].PhaseSpeed, b[i].PhaseSpeed); Bits(a[i].PhaseOffset, b[i].PhaseOffset);
            }
        }

        private static void Bits(float a, float b)
            => Assert.AreEqual(BitConverter.SingleToInt32Bits(a), BitConverter.SingleToInt32Bits(b));

        [Test]
        public void SameSeedSameTime_SameSea_NoHistory()
        {
            var field = ShippedWaveField.Settings();
            var wind = new RegionWind { Seed = 4242, Profile = WindProfile.CoddleCove };
            var played = new WaveFieldAnimator();
            foreach (double target in new[] { 0.0, 600.125, 5400.0, 108000.25 })
            {
                for (double t = target - 60; t < target; t += 0.137) played.Tick(t, wind, in field);
                var warm = played.Tick(target, wind, in field);
                var cold = new WaveFieldAnimator().Tick(target, wind, in field);
                AssertSame(warm, cold);
                played.Reset();
                AssertSame(cold, played.Tick(target, wind, in field));
            }
        }

        [Test]
        public void ShortTrainsTurnFirst_LongTrainsLag()
        {
            var field = ShippedWaveField.Settings();
            var wind = new StepWind(600, Vector2.right * 6f, Vector2.up * 6f);
            var before = WaveMemory.TrainsAt(wind, 600, in field);
            var after = WaveMemory.TrainsAt(wind, 635, in field);
            Assert.Greater(Vector2.Angle(before[7].Direction, after[7].Direction), 89f);
            Assert.Less(Vector2.Angle(before[0].Direction, after[0].Direction), 5f);
            Assert.Greater(Vector2.Angle(before[3].Direction, after[3].Direction), 10f);
            Assert.Less(Vector2.Angle(before[3].Direction, after[3].Direction), 35f);
        }

        [Test]
        public void ASteadyWind_ReproducesTodaysTrains_BitForBit()
        {
            foreach (var field in new[] { WaveFieldSettings.Default, ShippedWaveField.Settings() })
            foreach (var wind in new[] { Vector2.zero, new Vector2(4.125f, -1.37f), Vector2.up * 10f,
                new Vector2(BitConverter.Int32BitsToSingle(int.MinValue), 1.5f) })
            foreach (double time in new[] { -100.125, 0.0, 721.123, 108000.75 })
            {
                var source = new StepWind(0, wind, wind);
                var today = WaveMath.TrainsFrom(wind, WeatherModel.SeaState01(wind.magnitude), in field);
                AssertSame(today, WaveMemory.TrainsAt(source, time, in field));
            }
        }

        [Test]
        public void TheCrestSlide_AtTheRegionsMedianWater_IsUnderTheBar()
        {
            // Water-four finding (E): 600 s prediction 0.45 m/game s p95 at St Peters' median
            // water radius 262 m. Allow 0.60 for the finite tap grid; the old p95 was 3.55.
            const double radius = 262.0, bar = 0.60;
            var field = ShippedWaveField.Settings();
            var profile = WindProfile.CoddleCove;
            profile.PrevailingDirectionDeg = 40f; profile.DirectionWanderDeg = 40f;
            profile.MeanStrength = 2.3f; profile.StrengthVariability = 2.1f;
            profile.GustStrength = 1.3f; profile.GustVeerDeg = 12f;
            profile.CalmMaxStrength = 24f; profile.SystemStrength = 13.5f;
            var rates = new List<double>();
            foreach (int seed in new[] { 0, 1, 7, 4242, 90210 })
            {
                var source = new RegionWind { Seed = seed, Profile = profile };
                for (double t = 0.5; t < 5400; t += 0.5)
                {
                    var a = WaveMemory.TrainsAt(source, t - 0.5, in field)[0].Direction;
                    var b = WaveMemory.TrainsAt(source, t + 0.5, in field)[0].Direction;
                    double angle = Math.Abs(Math.Atan2(a.x * b.y - a.y * b.x, Vector2.Dot(a, b)));
                    rates.Add(radius * angle);
                }
            }
            rates.Sort();
            Assert.Less(rates[(int)Math.Round(0.95 * (rates.Count - 1))], bar);
        }

        [Test]
        public void TheBoatReadsThePublishedTrains()
        {
            // Pin the production consumer, including its time convention and both boot fallbacks.
            string text = File.ReadAllText("Assets/_Project/Code/Boats/BoatController.cs");
            int start = text.IndexOf("private void ApplySeakeeping(", StringComparison.Ordinal);
            string body = text.Substring(start, text.IndexOf("SeakeepingResponse response", start,
                StringComparison.Ordinal) - start);
            StringAssert.Contains("SharedWaveField.TryGet(out WaveTrains trains)", body);
            StringAssert.Contains("if (!published)", body);
            StringAssert.Contains("WaveMemory.TrainsAt(history, now, in waveField)", body);
            StringAssert.Contains("WaveMath.Sample(pos, published ? 0.0 : now, in trains", body);
            string bridge = File.ReadAllText("Assets/_Project/Code/Art/WaveFieldBridge.cs");
            StringAssert.Contains("PublishGlobals(Pack(in trains));", bridge);
            StringAssert.Contains("SharedWaveField.Publish(this, in trains);", bridge);

            var field = ShippedWaveField.Settings();
            var source = new StepWind(600, Vector2.right * 4f, Vector2.up * 10f);
            var drawn = new WaveFieldAnimator().Tick(640, source, in field);
            object publisher = new object();
            SharedWaveField.Publish(publisher, in drawn);
            try
            {
                Assert.IsTrue(SharedWaveField.TryGet(out var ridden));
                AssertSame(drawn, ridden);
                var pos = new Vector2(260f, -30f);
                Bits(WaveMath.Sample(pos, 0, in drawn).Height, WaveMath.Sample(pos, 0, in ridden).Height);
            }
            finally { SharedWaveField.Clear(publisher); }
        }

        [Test]
        public void CalmTaps_DoNotTurnTheWind_AndAnglesWrapAsVectors()
        {
            var calm = new StepWind(0, Vector2.left * 6f, Vector2.zero);
            var mean = WaveMemory.Average(calm, 15, 30, 16);
            Assert.Less(mean.Wind.x, 0f); Assert.AreEqual(0f, mean.Wind.y);
            var wrap = new StepWind(0, new Vector2(-6f, 0.1f), new Vector2(-6f, -0.1f));
            Assert.Less(WaveMemory.Average(wrap, 15, 30, 16).Wind.x, -5.9f);
        }

        [Test]
        public void GridBoundary_IsContinuous_AndNeverQueriesTheFuture()
        {
            var field = ShippedWaveField.Settings();
            var source = new PastOnlyWind { Now = 601.234 };
            WaveMemory.TrainsAt(source, source.Now, in field);
            var step = new StepWind(600, Vector2.right * 4f, Vector2.up * 10f);
            foreach (double boundary in new[] { 600.0, 607.5, 637.5, 1200.0 })
            {
                var a = WaveMemory.TrainsAt(step, boundary - 1e-6, in field);
                var b = WaveMemory.TrainsAt(step, boundary + 1e-6, in field);
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(a[i].Amplitude, b[i].Amplitude, 1e-5f);
                    Assert.Less((a[i].Direction - b[i].Direction).magnitude, 1e-5f);
                }
            }
        }

        private sealed class PastOnlyWind : IWaveWindSource
        {
            public double Now;
            public WaveWindSample WaveWindAt(double time)
            {
                Assert.LessOrEqual(time, Now);
                return new WaveWindSample(Vector2.right * 4f, 0.4f);
            }
        }

        [Test]
        public void MissingMemoryData_UsesDefaults_AndDominantFollowsTheFinishedField()
        {
            var field = ShippedWaveField.Settings();
            var wind = new StepWind(600, Vector2.right * 4f, Vector2.up * 10f);
            var expected = WaveMemory.TrainsAt(wind, 720, in field);
            field.MemoryShortSeconds = field.MemoryMiddleSeconds = field.MemoryLongSeconds = 0;
            field.MemoryShortMaxWavelengthMeters = field.MemoryMiddleMaxWavelengthMeters = 0;
            field.MemoryTapCount = 0;
            var missing = WaveMemory.TrainsAt(wind, 720, in field);
            AssertSame(expected, missing);
            for (int i = 0; i < missing.Count; i++)
                Assert.LessOrEqual(missing[i].Amplitude, missing.Dominant.Amplitude);
        }

        [Test]
        public void Sampler_AfterWarmup_AllocatesNothing()
        {
            var field = ShippedWaveField.Settings();
            var wind = new RegionWind { Seed = 4242, Profile = WindProfile.CoddleCove };
            var animator = new WaveFieldAnimator();
            for (int i = 0; i < 1000; i++) animator.Tick(i * 0.1, wind, in field);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) animator.Tick(i * 0.1, wind, in field);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, bytes);
        }
    }
}
