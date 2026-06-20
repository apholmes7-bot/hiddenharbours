using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Weather must also be deterministic from (seed, time) — same guarantees as the tide.</summary>
    public class WeatherModelTests
    {
        private static GameConfig Config() => ScriptableObject.CreateInstance<GameConfig>();

        [Test]
        public void Weather_IsReproducible()
        {
            var cfg = Config();
            WeatherModel.Sample(5000.0, 42, cfg, out var w1, out var s1, out var v1);
            WeatherModel.Sample(5000.0, 42, cfg, out var w2, out var s2, out var v2);
            Assert.AreEqual(w1, w2);
            Assert.AreEqual(s1, s2);
            Assert.AreEqual(v1, v2);
        }

        [Test]
        public void Visibility_StaysInRange()
        {
            var cfg = Config();
            for (int i = 0; i < 500; i++)
            {
                WeatherModel.Sample(i * 1000.0, 7, cfg, out _, out _, out float v);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
        }

        [Test]
        public void DifferentSeeds_GenerallyDiffer()
        {
            var cfg = Config();
            int differing = 0;
            for (int i = 0; i < 60; i++)
            {
                WeatherModel.Sample(i * 3000.0, 1, cfg, out var a, out _, out _);
                WeatherModel.Sample(i * 3000.0, 2, cfg, out var b, out _, out _);
                if ((a - b).sqrMagnitude > 0.0001f) differing++;
            }
            Assert.Greater(differing, 45, "Two different world seeds should mostly produce different weather.");
        }

        [Test]
        public void SeaFromWind_RisesWithStrength()
        {
            Assert.AreEqual(SeaState.Glass, WeatherModel.SeaFromWind(0f));
            Assert.AreEqual(SeaState.Storm, WeatherModel.SeaFromWind(25f));
            Assert.LessOrEqual((int)WeatherModel.SeaFromWind(3f), (int)WeatherModel.SeaFromWind(9f));
        }
    }
}
