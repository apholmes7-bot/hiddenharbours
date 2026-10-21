using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class WaveMemoryEnvironmentTests
    {
        private sealed class Shoal : ITidalTerrain
        {
            public float ElevationAt(Vector2 p) => .04f * p.x - 3f;
        }

        private sealed class SurfWind : IEnvironmentService
        {
            public double TideTime;
            public int WorldSeed => 7;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(new Vector2(6, 0),
                Vector2.zero, 0, SeaState.Moderate, 1, .55f);
            public float TideHeightAt(double t) => 0;
            public float WaterLevelAt(double t) { TideTime = t; return 0; }
        }

        [Test]
        public void Surf_PublishedTravelIsAppliedOnce_WhileTideKeepsWorldTime()
        {
            GameServices.Reset();
            var wind = new SurfWind();
            GameServices.Environment = wind;
            GameServices.TidalTerrain = new Shoal();
            try
            {
                var field = GameServices.WaveField;
                var smooth = WaveFieldAnimatorSettings.Default;
                var raw = WaveMath.TrainsFrom(new Vector2(6, 0), .55f, in field);
                var animator = new WaveFieldAnimator();
                var method = typeof(BoatController).GetMethod("SurfUnderTheHull",
                    BindingFlags.Static | BindingFlags.NonPublic);
                for (int tick = 1; tick <= 150; tick++)
                {
                    double now = tick * .02; // 3 s of play / 2.4 in-game minutes.
                    var baked = animator.Tick(.02f, now, new Vector2(6, 0), .55f, in field, in smooth);
                    var pos = new Vector2(52.25f, 0);
                    var fallback = (SurfState)method.Invoke(null,
                        new object[] { pos, now, raw, field.Gravity, 1f, false });
                    var live = (SurfState)method.Invoke(null,
                        new object[] { pos, now, baked, field.Gravity, 1f, true });
                    Assert.IsTrue(live.IsWorking);
                    Assert.AreEqual(now, wind.TideTime);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(fallback.BorePhaseDegrees,
                        live.BorePhaseDegrees)), Is.LessThan(.001f), "phase at tick " + tick);
                    Assert.That(live.Bore01, Is.EqualTo(fallback.Bore01).Within(.0001f));
                    Assert.That(live.BirthEnergy01, Is.EqualTo(fallback.BirthEnergy01).Within(.0001f));
                    Assert.That(live.RunUpMeters, Is.EqualTo(fallback.RunUpMeters).Within(.0001f));
                }
            }
            finally { GameServices.Reset(); }
        }

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
