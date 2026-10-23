using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WaveSwellServiceTests
    {
        sealed class Clock : IGameClock
        {
            public double TotalSeconds => 0.0;
            public GameTime Now => default;
            public Season Season => default;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => default;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        sealed class Weather : IEnvironmentService, IWaveWindSource
        {
            public int WorldSeed => 12345;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double time) => 0f;
            public WaveWindSample WaveWindAt(double time) => new WaveWindSample(Vector2.zero, 0f);
        }

        SwellClimateDef climate;
        GameConfig config;
        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            climate = ScriptableObject.CreateInstance<SwellClimateDef>();
            config = ScriptableObject.CreateInstance<GameConfig>();
            config.WaveField = WaveSwellTests.Field();
            GameServices.Config = config; GameServices.Clock = new Clock(); GameServices.Environment = new Weather();
            GameServices.CurrentRegionId = "region.swell_test";
            GameServices.RegisterSwellClimate(GameServices.CurrentRegionId, climate,
                new SwellShelterSettings { Harbour = new Vector4(0f, 0f, 20f, 20f), FeatherMeters = 8f });
        }
        [TearDown]
        public void TearDown()
        {
            SharedWaveField.Clear(this);
            GameServices.Reset(); Object.DestroyImmediate(climate); Object.DestroyImmediate(config);
        }

        [Test]
        public void RuntimeFetch_KeepsTheHarbourGlass_WhenWindFetchIsOff()
        {
            config.WaveFetch = default;
            var wind = WaveMath.TrainsFrom(Vector2.zero, 0f, config.WaveField);
            var sea = GameServices.WithSwell(in wind, 0.0, config.WaveField.Gravity);
            SharedWaveField.Publish(this, in sea);
            var shelter = GameServices.FetchAt(Vector2.zero);
            Assert.IsTrue(shelter.HasSwell); Assert.AreEqual(1f, shelter.Fraction);
            Assert.AreEqual(0f, shelter.ForTrain(6, 40f));
            Assert.AreEqual(0f, GameServices.FetchEnvelopeAt(Vector2.zero));
            Assert.AreEqual(1f, GameServices.FetchEnvelopeAt(new Vector2(100f, 100f)));
        }

        [Test]
        public void RuntimeAnimator_AndColdFallback_UseTheSameBaySwell()
        {
            var settings = GameServices.WaveField;
            var source = (IWaveWindSource)GameServices.Environment;
            var raw = WaveMemory.TrainsAt(source, 0.0, in settings);
            var fallback = GameServices.WithSwell(in raw, 0.0, settings.Gravity);
            var animated = new WaveFieldAnimator().Tick(0.0, source, in settings);
            var at = new Vector2(100f, -40f);
            var a = WaveMath.Sample(at, 0.0, in fallback);
            var b = WaveMath.Sample(at, 0.0, in animated);
            Assert.AreEqual(a.Height, b.Height, 0.00001f);
            Assert.Less((a.Slope - b.Slope).magnitude, 0.00001f);
            Assert.AreEqual(fallback[6].Amplitude, animated[6].Amplitude);
            GameServices.RegisterSwellClimate("region.swell_next", climate);
            GameServices.CurrentRegionId = "region.swell_next";
            var next = GameServices.WithSwell(in raw, 0.0, settings.Gravity);
            WaveMemoryTests.AssertSame(in fallback, in next);
            var cold = new WaveFieldAnimator().Tick(0.0, source, in settings);
            WaveMemoryTests.AssertSame(in animated, in cold);
        }
    }
}
