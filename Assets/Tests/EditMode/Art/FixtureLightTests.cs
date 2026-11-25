using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class FixtureLightTests
    {
        [Test]
        public void SheCarriesNoLampUntilSwitched()
        {
            var configBefore = GameServices.Config;
            var playerBefore = GameServices.PlayerTransform;
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var player = new GameObject("FixtureWalker");
            var host = new GameObject("FixtureWalkerLights");
            try
            {
                GameServices.Config = config;
                GameServices.PlayerTransform = player.transform;
                InteractActorProbe.Set(InteractActor.For(Vector2.zero, Vector2.up, ControlMode.OnFoot));
                var lights = host.AddComponent<WalkerLights>();
                Step(lights, "Update");
                Assert.IsFalse(lights.Lantern.enabled, "a new walker must not burn an automatic lantern");
                Assert.IsFalse(lights.Beam.IsOn);
                Assert.IsFalse(lights.Beam.Light.enabled);
                lights.Beam.Toggle();
                Step(lights, "Update");
                Assert.IsTrue(lights.Beam.IsOn);
                Assert.IsTrue(lights.Beam.Light.enabled);
                Assert.IsFalse(lights.Lantern.enabled);
                Assert.IsTrue(lights.Beam.transform.IsChildOf(player.transform), "retain the shadow carrier");

                var legacy = typeof(GameConfig).GetField("WalkerAutoLantern");
                Assert.IsNotNull(legacy, "the default-off way back is explicit config");
                legacy.SetValue(config, true);
                Step(lights, "Update");
                Assert.IsTrue(lights.Lantern.enabled);
                lights.Beam.SetOn(false);
                Step(lights, "Update");
                Assert.IsFalse(lights.Beam.Light.enabled);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(config);
                GameServices.Config = configBefore;
                GameServices.PlayerTransform = playerBefore;
                InteractActorProbe.Clear();
                WalkerLights.ResetInstallForTests();
            }
        }

        [Test]
        public void EveryFixtureKind_HasItsOwnColourTemperature()
        {
            var kelvin = typeof(LightPresets).GetMethod("TemperatureKelvin", new[] { typeof(LightPresets.Kind) });
            Assert.IsNotNull(kelvin, "one fixture-to-Kelvin table is required");
            var convert = typeof(LightPresets).GetMethod("KelvinToColor", new[] { typeof(float) });
            Assert.IsNotNull(convert);
            var kinds = new[] { LightPresets.Kind.WindowGlow, LightPresets.Kind.Lightpost,
                LightPresets.Kind.Worklight, LightPresets.Kind.Floodlight, LightPresets.Kind.Lantern,
                LightPresets.Kind.Headlamp, LightPresets.Kind.HungLantern };
            var expected = new[] { 2700f, 2100f, 4300f, 5600f, 1800f, 6500f, 1800f };
            var expectedRgb = new[] { new Vector3(1f, .654f, .343f), new Vector3(1f, .568f, .093f),
                new Vector3(1f, .835f, .700f), new Vector3(1f, .938f, .884f), new Vector3(1f, .495f, 0f),
                new Vector3(1f, .997f, .981f), new Vector3(1f, .495f, 0f) };
            for (int i = 0; i < kinds.Length; i++)
            {
                Assert.AreEqual(expected[i], (float)kelvin.Invoke(null, new object[] { kinds[i] }), kinds[i].ToString());
                Color c = (Color)convert.Invoke(null, new object[] { expected[i] });
                Assert.Less(Vector3.Distance(expectedRgb[i], Rgb(c)), .025f, "independent colour reference");
                Assert.AreEqual(c, LightPresets.For(kinds[i]).Color, kinds[i].ToString());
                Assert.AreEqual(c, (Color)convert.Invoke(null, new object[] { expected[i] }));
            }
            for (int i = 0; i < 4; i++)
                for (int j = i + 1; j < 4; j++)
                    Assert.Greater(Vector3.Distance(Rgb(LightPresets.For(kinds[i]).Color),
                                                   Rgb(LightPresets.For(kinds[j]).Color)), .08f);
        }

        [Test]
        public void OnlyFlamesFlicker_AndTheFlickerIsDeterministic()
        {
            foreach (LightPresets.Kind kind in Enum.GetValues(typeof(LightPresets.Kind)))
            {
                float amount = LightPresets.For(kind).FlickerAmount;
                float expected = kind == LightPresets.Kind.Lantern ? .28f : kind == LightPresets.Kind.HungLantern ? .34f : 0f;
                Assert.AreEqual(expected, amount, 1e-6f, kind.ToString());
                float lo = 1f, hi = 0f;
                for (int i = 0; i < 120; i++)
                {
                    float t = 17.25f + i / 30f;
                    float a = LightMath.Flicker(731, t, amount, 1f);
                    Assert.AreEqual(a, LightMath.Flicker(731, t, amount, 1f));
                    Assert.That(a, Is.InRange(1f - amount, 1f));
                    lo = Mathf.Min(lo, a); hi = Mathf.Max(hi, a);
                }
                if (expected > 0f) Assert.Greater(hi - lo, .15f, "the flame must visibly breathe");
                else Assert.AreEqual(1f, lo);
            }
            Assert.AreEqual(0f, BoatLampPresets.For(HullLampKind.CabinGlow).FlickerAmount,
                "an electric cabin is steady");

            var go = new GameObject("DeterministicFitting");
            try
            {
                var lamp = go.AddComponent<SceneLight>();
                lamp.FlickerAmount = .34f;
                float tickHz = (float)typeof(SceneLight).GetField("_refreshHz",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lamp);
                Assert.Greater(tickHz, 0f);
                var tickRead = new System.Text.StringBuilder(
                    $"SceneLight tick={tickHz:R} Hz; flicker speed=1; 12000 intervals from t=1950; seeds=0,1,41,73,731,12345,-1,2147483647\n");
                foreach (float amount in new[] { .28f, .34f })
                {
                    float largestStep = 0f, atTime = 0f;
                    int atSeed = 0;
                    foreach (int tickSeed in new[] { 0, 1, 41, 73, 731, 12345, -1, int.MaxValue })
                    {
                        float previous = LightMath.Flicker(tickSeed, 1950f, amount, 1f);
                        for (int tick = 1; tick <= 12000; tick++)
                        {
                            float time = 1950f + tick / tickHz;
                            float current = LightMath.Flicker(tickSeed, time, amount, 1f);
                            float change = Mathf.Abs(current - previous);
                            if (change > largestStep)
                            { largestStep = change; atTime = time; atSeed = tickSeed; }
                            previous = current;
                        }
                    }
                    tickRead.AppendLine($"amount={amount:R}, largest_tick_change={largestStep:R}, at_time={atTime:R}, seed={atSeed}");
                }
                TestContext.Progress.WriteLine(tickRead.ToString());
                string readPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                    "../Evidence~/lights/pr1/flame-tick-jumps.txt"));
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(readPath));
                System.IO.File.WriteAllText(readPath, tickRead.ToString());
                var sample = typeof(SceneLight).GetMethod("FlickerAt", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(sample, "the caller samples the stable identity with world seed and clock time");
                float a = (float)sample.Invoke(lamp, new object[] { 41, 123.25d });
                Assert.AreEqual(a, (float)sample.Invoke(lamp, new object[] { 41, 123.25d }));
                Assert.AreNotEqual(a, (float)sample.Invoke(lamp, new object[] { 73, 123.25d }));
                var identity = typeof(SceneLight).GetMethod("StableFittingSeed", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(identity);
                int seed = (int)identity.Invoke(null, new object[] { go.transform });
                go.transform.SetAsFirstSibling();
                Assert.AreEqual(seed, (int)identity.Invoke(null, new object[] { go.transform }),
                    "scene/root load order must not alter a fitting's phase");
                go.name = "AnotherFitting";
                Assert.AreNotEqual(seed, (int)identity.Invoke(null, new object[] { go.transform }),
                    "authored fitting identities have separate phases");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static Vector3 Rgb(Color c) => new Vector3(c.r, c.g, c.b);

        [Test]
        public void SwitchingOffClearsHerDecorBeam_WithoutErasingABoatsPublication()
        {
            string[] vectors = { "_BoatLightPos", "_BoatLightDir", "_BoatLightParams", "_BoatLightParams2" };
            var saved = Array.ConvertAll(vectors, Shader.GetGlobalVector);
            Color savedColor = Shader.GetGlobalColor("_BoatLightColor");
            var go = new GameObject("SwitchedHeadlamp");
            var boat = new GameObject("OtherPublisher");
            try
            {
                var lamp = go.AddComponent<Headlamp>();
                lamp.Configure(1.55f); lamp.SetOn(true);
                WalkerLights.SetOwnsDecorLightForTests(true);
                Assert.IsTrue(lamp.PublishDecorLampIfOwned());
                Assert.Greater(Shader.GetGlobalColor("_BoatLightColor").maxColorComponent, 0f);
                lamp.SetOn(false); WalkerLights.SetOwnsDecorLightForTests(false);
                Step(lamp, "LateUpdate");
                Assert.AreEqual(0f, Shader.GetGlobalColor("_BoatLightColor").maxColorComponent,
                    "no active boat must not leave her old beam in the trees");

                lamp.SetOn(true); WalkerLights.SetOwnsDecorLightForTests(true);
                Assert.IsTrue(lamp.PublishDecorLampIfOwned());
                lamp.SetOn(false); WalkerLights.SetOwnsDecorLightForTests(false);
                var spotlight = boat.AddComponent<BoatSpotlight>();
                typeof(BoatSpotlight).GetMethod("PublishWaterLight", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(spotlight, new object[] { .7f, new Vector3(11, 12, 0), Vector2.up });
                Vector4 boatPosition = Shader.GetGlobalVector("_BoatLightPos");
                Assert.AreEqual(11f, boatPosition.x);
                Assert.AreEqual(12f, boatPosition.y);
                Color boatColor = Shader.GetGlobalColor("_BoatLightColor");
                Step(lamp, "LateUpdate");
                Assert.AreEqual(boatPosition, Shader.GetGlobalVector("_BoatLightPos"));
                Assert.AreEqual(boatColor, Shader.GetGlobalColor("_BoatLightColor"));
            }
            finally
            {
                Object.DestroyImmediate(go); Object.DestroyImmediate(boat);
                for (int i = 0; i < vectors.Length; i++) Shader.SetGlobalVector(vectors[i], saved[i]);
                Shader.SetGlobalColor("_BoatLightColor", savedColor);
                WalkerLights.ResetInstallForTests();
            }
        }

        static void Step(object value, string method) => value.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, null);
    }
}
