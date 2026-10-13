using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WaveMemoryEnvironmentTests
    {
        [Test]
        public void History_UsesTheActiveAsset_ClockScale_AndDevOverride()
        {
            GameServices.Reset();
            var go = new GameObject("Wind history test");
            go.SetActive(false);
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var asset = ScriptableObject.CreateInstance<WindProfileDef>();
            try
            {
                config.SecondsPerDay = 2377f;
                asset.Profile = WindProfile.CoddleCove;
                var profile = asset.Profile;
                profile.PrevailingDirectionDeg = 123f;
                asset.Profile = profile;
                var environment = go.AddComponent<EnvironmentService>();
                Set(environment, "_config", config);
                Set(environment, "_windProfileAsset", asset);
                foreach (double time in new[] { -600.25, 0.0, 750.125 })
                {
                    var expected = WeatherModel.SampleWind(time, environment.WorldSeed,
                        config.SecondsPerDay / 24.0, profile);
                    var wind = environment.WaveWindAt(time);
                    Assert.AreEqual(expected.x, wind.Wind.x);
                    Assert.AreEqual(expected.y, wind.Wind.y);
                    Assert.AreEqual(WeatherModel.SeaState01(expected.magnitude), wind.SeaState01);
                }
                Assert.AreEqual(environment.Sample().WindVector, environment.WaveWindAt(0).Wind);
                environment.DevForceSeaState = true;
                environment.DevSeaState01 = 1f;
                Assert.AreEqual(1f, environment.WaveWindAt(-500).SeaState01);
                environment.DevSeaState01 = 0f;
                Assert.AreEqual(Vector2.zero, environment.WaveWindAt(-500).Wind);
                var field = config.WaveField;
                Assert.AreEqual(0f, WaveMemory.TrainsAt(environment, 0, in field).TotalAmplitude);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(config);
                GameServices.Reset();
            }
        }

        private static void Set(EnvironmentService environment, string field, object value)
            => typeof(EnvironmentService).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(environment, value);
    }
}
