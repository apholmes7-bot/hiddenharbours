using System;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WaveSwellTests
    {
        public static WaveFieldSettings Field()
        {
            var s = WaveFieldSettings.Default;
            s.SpectrumBlend = 0.65f; s.SpectrumBinCount = 8;
            s.HeightFromFetch = true; s.SeaFetchKilometres = 25f;
            return s;
        }

        public static WaveTrains Sea(Vector2 wind, float state, int seed = 12345, double time = 0.0)
        {
            var settings = Field();
            var windSea = WaveMath.TrainsFrom(wind, state, in settings);
            return SwellMath.Compose(in windSea, SwellClimateSettings.Default, seed, time, 1800f, settings.Gravity);
        }

        static void SameTrain(WaveTrain a, WaveTrain b)
        {
            Assert.AreEqual(a.Direction, b.Direction);
            foreach (var pair in new[] { (a.Wavelength, b.Wavelength), (a.Amplitude, b.Amplitude),
                (a.PhaseOffset, b.PhaseOffset), (a.PhaseSpeed, b.PhaseSpeed) })
                Assert.AreEqual(BitConverter.SingleToInt32Bits(pair.Item1), BitConverter.SingleToInt32Bits(pair.Item2));
        }

        [Test]
        public void TheSwell_IgnoresTheLocalWind()
        {
            var calm = Sea(Vector2.zero, 0f);
            foreach (var wind in new[] { Vector2.up * 14f, Vector2.left * 10f, new Vector2(6f, -5.3f) })
            {
                var sea = Sea(wind, 1f);
                for (int i = 6; i < 8; i++) SameTrain(calm[i], sea[i]);
            }
            var crossed = Sea(Vector2.right * 8f, 0.6f);
            Assert.Less(Mathf.Abs(Vector2.Dot(crossed[0].Direction, crossed[6].Direction)), 0.95f);
        }

        [Test]
        public void TheSwell_IsAFunctionOfSeedAndTime()
        {
            var first = Sea(Vector2.right, 0.3f, 718, 4321.75);
            Sea(Vector2.up, 1f, 99, 9999999.0);
            var repeat = Sea(Vector2.right, 0.3f, 718, 4321.75);
            for (int i = 6; i < 8; i++) SameTrain(first[i], repeat[i]);
            Assert.AreNotEqual(first[6].Direction, Sea(Vector2.right, 0.3f, 719, 4321.75)[6].Direction);
            Assert.AreNotEqual(first[6].Amplitude, Sea(Vector2.right, 0.3f, 718, 5321.75)[6].Amplitude);
        }

        [Test]
        public void TheSwell_DriftsOverDays_WithoutChangingDispersion()
        {
            var a = Sea(Vector2.zero, 0f, 12345, 0.0);
            var b = Sea(Vector2.zero, 0f, 12345, 1800.0);
            var near = Sea(Vector2.zero, 0f, 12345, 0.01);
            Assert.Less((a[6].Direction - near[6].Direction).magnitude, 0.00001f);
            Assert.AreNotEqual(a[6].Direction, b[6].Direction);
            for (int i = 6; i < 8; i++)
            {
                Assert.AreEqual(a[i].Wavelength, b[i].Wavelength);
                Assert.AreEqual(a[i].PhaseSpeed, b[i].PhaseSpeed);
                Assert.AreEqual(Mathf.Sqrt(9.81f * a[i].Wavelength / (2f * Mathf.PI)), a[i].PhaseSpeed);
            }
        }

        [Test]
        public void ZeroSwellHeight_KeepsTheSixWindSlotsBitForBit()
        {
            var settings = Field(); var climate = SwellClimateSettings.Default; climate.HeightMeters = 0f;
            foreach (float speed in new[] { 0f, 2.3f, 5f, 10f })
            {
                var wind = WaveMath.TrainsFrom(new Vector2(speed, speed * 0.3f), speed / 14f, in settings);
                var sea = SwellMath.Compose(in wind, in climate, 5, 30.0, 1800f, settings.Gravity);
                Assert.AreEqual(8, sea.Count);
                for (int i = 0; i < 6; i++) SameTrain(wind[i], sea[i]);
                Assert.AreEqual(0f, sea[6].Amplitude); Assert.AreEqual(0f, sea[7].Amplitude);
                var retained = new WaveTrain[6];
                for (int i = 0; i < 6; i++) retained[i] = wind[i];
                var before = WaveTrains.From(retained, 6, wind.CrestSharpening, 0);
                for (int i = 0; i < 30; i++)
                {
                    var position = new Vector2(i * 1.7f, -i * 0.9f);
                    var a = WaveMath.Sample(position, i * 10.25, in before);
                    var b = WaveMath.Sample(position, i * 10.25, in sea);
                    Assert.AreEqual(BitConverter.SingleToInt32Bits(a.Height), BitConverter.SingleToInt32Bits(b.Height));
                    Assert.AreEqual(a.Slope, b.Slope); Assert.AreEqual(a.CrestFactor, b.CrestFactor);
                }
            }
        }

        public sealed class Wall : ITidalTerrain
        {
            public float ElevationAt(Vector2 p) => p.x < 0f ? 8f : -8f;
        }

        [Test]
        public void SwellShelter_StopsAtLand_WhileTheWindLeeIsOpen()
        {
            var climate = SwellClimateSettings.Default; var settings = WaveFetchSettings.Default;
            var bed = new Wall(); var at = new Vector2(12f, 0f);
            for (int tide = -22; tide <= 22; tide++)
            {
                float level = tide * 0.1f;
                Assert.AreEqual(0f, SwellMath.ShelterAt(at, Vector2.right, level, bed, in climate));
                Assert.AreEqual(1f, WaveFetch.Fetch01(at, Vector2.left, level, bed, in settings));
                Assert.AreEqual(1f, SwellMath.ShelterAt(at, Vector2.left, level, bed, in climate));
            }
        }

        [Test]
        public void SwellShelter_StopsAtDistantLand_InsteadOfGrowingAgainInItsLee()
        {
            var climate = SwellClimateSettings.Default; var settings = WaveFetchSettings.Default;
            var at = new Vector2(72f, 0f); var bed = new Wall();
            Assert.Greater(WaveFetch.Fetch01(at, Vector2.right, 0f, bed, in settings), 0.5f);
            Assert.AreEqual(0f, SwellMath.ShelterAt(at, Vector2.right, 0f, bed, in climate));
            Assert.AreEqual(1f, SwellMath.ShelterAt(at, Vector2.right, 0f, null, in climate));
        }

        [Test]
        public void GlassIsStillSacred_InShelteredWater_WithAnOceanSwell()
        {
            var sea = Sea(Vector2.zero, 0f);
            var fetch = WaveFetchSample.Open.WithSwell(0f);
            for (int i = 0; i < sea.Count; i++) Assert.AreEqual(0f, sea[i].Amplitude * fetch.ForTrain(i, sea[i].Wavelength));
            for (int i = 0; i < 100; i++)
            {
                var sample = WaveMath.Sample(new Vector2(i * 1.7f, -i * 0.9f), i * 10.25, in sea, fetch);
                Assert.AreEqual(0f, sample.Height); Assert.AreEqual(Vector2.zero, sample.Slope);
                Assert.AreEqual(0f, sample.CrestFactor);
            }
        }

        [Test]
        public void GlassCalm_OnTheOpenCoast_CarriesOnlyTheTwoSwellTrains()
        {
            var sea = Sea(Vector2.zero, 0f);
            for (int i = 0; i < 6; i++) Assert.AreEqual(0f, sea[i].Amplitude);
            Assert.Greater(sea[6].Amplitude, 0f); Assert.Greater(sea[7].Amplitude, 0f);
            Assert.AreEqual(6, sea.DominantIndex);
            Assert.AreEqual(SwellMath.Height(SwellClimateSettings.Default, 12345, 0.0, 1800f) * 0.5f,
                sea.TotalAmplitude, 0.00000001f);
        }

        [Test]
        public void SwellFetch_BypassesWindGrowth_EvenWithTheWindFetchDialOff()
        {
            var settings = WaveFetchSettings.Default;
            var lee = new WaveFetchSample(0f, in settings).WithSwell(1f);
            Assert.AreEqual(1f, lee.ForTrain(6, 40f)); Assert.AreEqual(1f, lee.ForTrain(7, 32.4f));
            var off = WaveFetchSample.Open.WithSwell(0f);
            Assert.AreEqual(0f, off.ForTrain(6, 40f)); Assert.AreEqual(1f, off.ForTrain(0, 30f));
            var uniform = ((WaveFetchSample)0.25f).WithSwell(1f);
            Assert.AreEqual(0.25f, uniform.ForTrain(0, 30f));
            Assert.AreEqual(1f, uniform.ForTrain(6, 40f)); Assert.AreEqual(1f, uniform.Uniform);
        }

        [Test]
        public void TheBoreSet_KeepsTheSwell_WhenWindFetchIsZero()
        {
            var trains = new WaveTrain[8];
            trains[6] = new WaveTrain(Vector2.right, 40f, 0.1f, Mathf.PI * 0.5f, 9.81f);
            trains[7] = new WaveTrain(Vector2.right, 32.4f, 0.05f, Mathf.PI * 0.5f, 9.81f);
            var field = WaveTrains.From(trains, 8, 2.6f, 6);
            var settings = WaveFetchSettings.Default;
            var fetch = new WaveFetchSample(0f, in settings).WithSwell(1f);
            float period = BreakerMath.PeriodSeconds(field.Dominant);
            Assert.AreEqual(1f, BreakerMath.BoreBirthEnergy01(in field, Vector2.zero, 0f, 90f, period, fetch, 1f));
            Assert.AreEqual(0f, BreakerMath.BoreBirthEnergy01(in field, Vector2.zero, 0f, 90f, period, 0f, 1f));
            Assert.AreEqual(0f, BreakerMath.BoreBirthEnergy01(in field, Vector2.zero, 0f, 90f, period,
                fetch.WithSwell(0f), 1f));
        }

        public sealed class Coast : ITidalTerrain
        {
            public float ElevationAt(Vector2 p) => p.x * 0.05f;
        }

        [Test]
        public void AnOceanSwell_CanBreakAtAShore_InTheLocalWindsLee()
        {
            var settings = Field(); var climate = SwellClimateSettings.Default;
            climate.FromBearingDegrees = 270f; climate.DirectionDriftDegrees = 0f;
            var wind = WaveMath.TrainsFrom(Vector2.left * 0.5f, 0.02f, in settings);
            var sea = SwellMath.Compose(in wind, in climate, 12345, 0.0, 1800f, settings.Gravity);
            var bed = new Coast(); var at = new Vector2(-1f, 0f); var fetchSettings = WaveFetchSettings.Default;
            fetchSettings.Strength = 0.8f;
            var fetch = WaveFetch.At(at, Vector2.left, 0f, bed, in fetchSettings)
                .WithSwell(SwellMath.ShelterAt(at, sea[6].Direction, 0f, bed, in climate));
            Assert.AreEqual(0f, fetch.Fraction); Assert.AreEqual(1f, fetch.ForTrain(6, 40f));
            var breakers = BreakerSettings.Default;
            var contour = BreakerMath.ContourFor(sea.Dominant, 0f, in breakers);
            var surf = BreakerMath.SurfAt(at, 0f, bed, in contour, fetch, in sea, settings.Gravity, in breakers);
            Assert.Greater(surf.Breaking01, 0f); Assert.Greater(surf.ShorewardDirection.sqrMagnitude, 0.5f);
        }

        [Test]
        public void AuthoredGlassBasins_StaySilent_AtEveryTideAndOceanBearing()
        {
            var basin = new SwellShelterSettings { Harbour = new Vector4(0f, 0f, 25f, 16f), FeatherMeters = 8f };
            var climate = SwellClimateSettings.Default;
            foreach (float tide in new[] { -100f, -2.2f, 0f, 2.2f, 100f })
            for (int bearing = 0; bearing < 360; bearing += 5)
            {
                climate.FromBearingDegrees = bearing;
                var direction = SwellMath.Direction(in climate, 12345, 0.0, 1800f);
                Assert.AreEqual(0f, SwellMath.ShelterAt(Vector2.zero, direction, tide, null, in climate, in basin));
            }
            Assert.AreEqual(0f, basin.TransmissionAt(new Vector2(25f, 0f)));
            Assert.AreEqual(0.5f, basin.TransmissionAt(new Vector2(29f, 0f)));
            Assert.AreEqual(1f, basin.TransmissionAt(new Vector2(33f, 0f)));
            Assert.AreEqual(1f, default(SwellShelterSettings).TransmissionAt(Vector2.zero));
        }

        [Test]
        public void SwellEvaluation_AllocatesZeroBytesAfterWarmup()
        {
            var settings = Field(); var climate = SwellClimateSettings.Default; var bed = new Wall();
            var wind = WaveMath.TrainsFrom(Vector2.right * 8f, 0.6f, in settings);
            float sum = 0f;
            for (int i = 0; i < 1000; i++) SwellMath.Compose(in wind, in climate, 12345, i, 1800f, settings.Gravity);
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                var sea = SwellMath.Compose(in wind, in climate, 12345, i, 1800f, settings.Gravity);
                float shelter = SwellMath.ShelterAt(new Vector2(12f, 0f), sea[6].Direction, 0f, bed, in climate);
                sum += WaveMath.Sample(Vector2.zero, i, in sea, WaveFetchSample.Open.WithSwell(shelter)).Height;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            Assert.AreEqual(0, allocated); Assert.IsFalse(float.IsNaN(sum));
        }
    }
}
