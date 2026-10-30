using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
#if UNITY_5_3_OR_NEWER
using Unity.Profiling;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Gains 2 (ADR 0027 #7 amendment): a water type's silt on the EnvironmentService itself. The rain a type
    /// remembers reaches Art through Core (<see cref="IVisibilitySource"/> and <see cref="IWaveWindSource"/>,
    /// read by <see cref="WaterRainSignal"/>), and it is the rain the rings draw. Pinned:
    /// <list type="bullet">
    /// <item>the environment's visibility at any time is the weather's, and at now the sample's;</item>
    /// <item>the rain signal at now is the rings' rain;</item>
    /// <item>the silt is pure in (worldSeed, gameTime): a jump reads what playing to it would, another
    /// service with the same seed reads the same, and the seed reaches the rain;</item>
    /// <item>the slow tick on the EnvironmentService itself allocates nothing;</item>
    /// <item>Core depends on no feature module, and Art never depends on Environment.</item>
    /// </list>
    /// </summary>
    public class WaterSiltEnvironmentTests
    {
        private const double Day = GameConfig.DefaultSecondsPerDay;
        private const string CoreAsmdef = "Assets/_Project/Code/Core/HiddenHarbours.Core.asmdef";
        private const string ArtAsmdef = "Assets/_Project/Code/Art/HiddenHarbours.Art.asmdef";
        private const string CoreModule = "HiddenHarbours.Core";
        private const string ArtModule = "HiddenHarbours.Art";
        private const string EnvironmentModule = "HiddenHarbours.Environment";

        private readonly List<Object> _made = new List<Object>();
        private GameConfig _config;
        private EnvironmentService _env;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _made.Add(_config);
            _env = MakeEnvironment(12345);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
                if (made != null) Object.DestroyImmediate(made);
            _made.Clear();
            GameServices.Reset();
        }

        [Test]
        public void VisibilityAt_IsTheWeathersVisibility_BitForBit()
        {
            _config.BaseFogBias = 0.6f;   // fog enough that the visibility moves
            foreach (double t in new[] { -7200.5, 0.0, 333.25, 3.0 * Day + 0.125 })
            {
                WeatherModel.Sample(t, _env.WorldSeed, _config, WindProfile.CoddleCove, out _, out _, out float vis);
                SameBits(vis, _env.VisibilityAt(t), $"t {t}");
            }
            SameBits(_env.Sample().Visibility, _env.VisibilityAt(0.0), "at now (no clock: 0)");
            EnvironmentService bare = MakeEnvironment(12345);
            Set(bare, "_config", null);
            SameBits(1f, bare.VisibilityAt(42.0), "no config: clear air");
        }

        [Test]
        public void RainSignal_AtNow_IsTheRingsRain()
        {
            Wet(_env);
            float[] d = RingDials();
            EnvironmentSample now = _env.Sample();
            float rings = AmbientParticleMath.RainIntensity(now.Visibility, now.SeaState01, d[0], d[1], d[2], d[3], d[4]);
            Assert.Greater(rings, 0f, "precondition: the rings are raining now");
            var rain = new WaterRainSignal();
            rain.Bind(_env, _env, d[0], d[1], d[2], d[3], d[4]);
            Assert.IsTrue(rain.IsBound);
            SameBits(rings, rain.ValueAt(0.0), "the silt's rain at now");
        }

        [Test]
        public void Silt_SameSeedSameTime_NoHistory()
        {
            Wet(_env);
            WaterTypeDef creek = MakeCreek();
            double target = 4.6 * Day + 17.25;

            WaterSurface played = MakeSurface(creek);
            for (double t = 0.0; t < target; t += Day / 48.0) SampleType(played, t, _env);
            SampleType(played, target, _env);
            WaterSurface jumped = MakeSurface(creek);
            SampleType(jumped, target, _env);
            EnvironmentService twinEnv = MakeEnvironment(_env.WorldSeed);
            Wet(twinEnv);
            WaterSurface twin = MakeSurface(creek);
            SampleType(twin, target, twinEnv);

            Vector4 sigma = Get<Vector4>(played, "_waterTypeSigma");
            Vector4 body = Get<Vector4>(played, "_waterTypeBody");
            Assert.Greater(sigma.w, creek.Water.Stain + creek.Water.DrySilt, "precondition: the window held rain");
            SameBits(sigma, Get<Vector4>(jumped, "_waterTypeSigma"), "a jump against a play-through (sigma)");
            SameBits(body, Get<Vector4>(jumped, "_waterTypeBody"), "a jump against a play-through (body)");
            SameBits(sigma, Get<Vector4>(twin, "_waterTypeSigma"), "the same seed on another service (sigma)");
            SameBits(body, Get<Vector4>(twin, "_waterTypeBody"), "the same seed on another service (body)");

            // And the seed reaches it: in a fog that comes and goes, another world's rain differs.
            EnvironmentService other = MakeEnvironment(777);
            Wet(other);
            _config.BaseFogBias = 0.45f;   // after Wet: visibility now spans the rain's murk gate
            float[] d = RingDials();
            var here = new WaterRainSignal();
            here.Bind(_env, _env, d[0], d[1], d[2], d[3], d[4]);
            var there = new WaterRainSignal();
            there.Bind(other, other, d[0], d[1], d[2], d[3], d[4]);
            int differ = 0;
            for (int i = 0; i < 96; i++)
            {
                double t = i * Day / 12.0;   // every two game hours over eight days
                if (BitConverter.SingleToInt32Bits(here.ValueAt(t)) != BitConverter.SingleToInt32Bits(there.ValueAt(t)))
                    differ++;
            }
            Assert.Greater(differ, 0, "the world seed never reached the rain");
        }

        /// <summary>Acceptance 5 on the EnvironmentService itself: sixty-four ticks of the 8 Hz slow tick,
        /// each reading the silt's whole window from the weather, allocate nothing.</summary>
        [Test]
        public void SiltSlowTick_OnTheRealEnvironment_AllocatesNothing()
        {
            Wet(_env);
            WaterTypeDef creek = MakeCreek();
            WaterSurface surface = MakeSurface(creek);
            var tick = (Action<double, IEnvironmentService>)Delegate.CreateDelegate(
                typeof(Action<double, IEnvironmentService>), surface,
                typeof(WaterSurface).GetMethod("SampleWaterType", BindingFlags.NonPublic | BindingFlags.Instance));
            IEnvironmentService env = _env;
            tick(0.0, env);   // warm every path once
            using (var probe = new AllocationProbe())
            {
                probe.RequirePositiveControl();
                probe.Begin();
                for (int i = 1; i <= 64; i++) tick(i * 0.125, env);
                long allocations = probe.End();
                Assert.AreEqual(0, allocations, "the water type's slow tick allocated (rule 7)");
            }
            Assert.Greater(Get<Vector4>(surface, "_waterTypeSigma").w, creek.Water.Stain + creek.Water.DrySilt,
                "precondition: the ticks read rain that adds silt");
        }

        /// <summary>Acceptance 4: the rain reaches Art through Core, and Core depends on no feature module. An asmdef
        /// names a module by the module's name or by "GUID:" and the guid in that module's asmdef .meta, so Core's
        /// and Art's asmdefs are read whole against every module's name and guid.</summary>
        [Test]
        public void Core_DependsOnNoFeatureModule_AndArtReadsTheRainThroughCore()
        {
            Dictionary<string, string> modules = ModuleGuids();
            foreach (string module in new[] { CoreModule, ArtModule, EnvironmentModule })
                Assert.IsTrue(modules.ContainsKey(module), $"precondition: the asmdef scan did not find {module}");

            string coreText = File.ReadAllText(CoreAsmdef);
            foreach (KeyValuePair<string, string> module in modules)
                if (module.Key != CoreModule)
                    Assert.IsFalse(Names(coreText, module.Key, module.Value), $"Core's asmdef names {module.Key}");
            string artText = File.ReadAllText(ArtAsmdef);
            Assert.IsFalse(Names(artText, EnvironmentModule, modules[EnvironmentModule]),
                "Art's asmdef names Environment");
            Assert.IsTrue(Names(artText, CoreModule, modules[CoreModule]), "Art's asmdef no longer names Core");

            Assembly core = typeof(IVisibilitySource).Assembly;
            Assert.AreEqual(CoreModule, core.GetName().Name);
            Assert.AreSame(core, typeof(IWorldTimeSignal).Assembly);
            Assert.AreSame(core, typeof(IWaveWindSource).Assembly);
            Assert.IsTrue(typeof(IVisibilitySource).IsAssignableFrom(typeof(EnvironmentService)),
                "the environment no longer reports its visibility through Core");
            Assert.IsTrue(typeof(IWaveWindSource).IsAssignableFrom(typeof(EnvironmentService)),
                "the environment no longer reports its wind through Core");
        }

        // ===== helpers ================================================================================

        /// <summary>Thick weather and a rough sea: visibility at most 0.5 (inside the rain's murk) and the
        /// dev sea state (editor only) at 0.8, so the rain's sea band is open.</summary>
        private void Wet(EnvironmentService env)
        {
            _config.BaseFogBias = 1f;
            env.DevForceSeaState = true;
            env.DevSeaState01 = 0.8f;
        }

        private EnvironmentService MakeEnvironment(int seed)
        {
            var go = new GameObject("WaterSiltEnvironmentTests.Environment");
            _made.Add(go);
            go.SetActive(false);
            var env = go.AddComponent<EnvironmentService>();
            Set(env, "_config", _config);
            Set(env, "_worldSeed", seed);
            return env;
        }

        private WaterSurface MakeSurface(WaterTypeDef type)
        {
            var go = new GameObject("WaterSiltEnvironmentTests.Surface", typeof(MeshRenderer));
            _made.Add(go);
            go.SetActive(false);
            var surface = go.AddComponent<WaterSurface>();
            Set(surface, "_renderer", go.GetComponent<MeshRenderer>());
            Set(surface, "_waterType", type);
            return surface;
        }

        /// <summary>Nine Mile Creek's shape of water, held in a Def as a region's surface would hold it.</summary>
        private WaterTypeDef MakeCreek()
        {
            var def = ScriptableObject.CreateInstance<WaterTypeDef>();
            _made.Add(def);
            WaterTypeValues creek = WaterTypeValues.Default;
            creek.Stain = 0.35f;
            creek.Algae = 0.05f;
            creek.DrySilt = 0.05f;
            creek.SiltResponse = 0.8f;
            creek.BodyStrength = 0.5f;
            def.Id = "water.test_creek";
            def.Water = creek;
            return def;
        }

        /// <summary>The rings' own rain dials, read from a fresh surface: the silt remembers the rain the
        /// rings draw.</summary>
        private static float[] RingDials()
        {
            var go = new GameObject("WaterSiltEnvironmentTests.Rings", typeof(MeshRenderer));
            go.SetActive(false);
            try
            {
                var surface = go.AddComponent<WaterSurface>();
                return new[]
                {
                    Get<float>(surface, "_rainBaselineIntensity"), Get<float>(surface, "_rainSeaStateWeight"),
                    Get<float>(surface, "_rainVisOnset"), Get<float>(surface, "_rainVisFull"),
                    Get<float>(surface, "_rainSeaOnset"),
                };
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void SampleType(WaterSurface surface, double now, IEnvironmentService env)
            => typeof(WaterSurface).GetMethod("SampleWaterType", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(surface, new object[] { now, env });

        /// <summary>Every HiddenHarbours module's name, keyed to the guid in its asmdef's .meta: the two ways one
        /// asmdef can name another. A folder or file Unity skips is never opened.</summary>
        private static Dictionary<string, string> ModuleGuids()
        {
            var modules = new Dictionary<string, string>(StringComparer.Ordinal);
            var folders = new Stack<string>(new[] { "Assets", "Packages" });
            while (folders.Count > 0)
            {
                string folder = folders.Pop();
                if (!Directory.Exists(folder)) continue;
                foreach (string sub in Directory.GetDirectories(folder))
                    if (!UnitySkips(sub)) folders.Push(sub);
                foreach (string asmdef in Directory.GetFiles(folder, "*.asmdef"))
                {
                    if (UnitySkips(asmdef)) continue;
                    Match name = Regex.Match(File.ReadAllText(asmdef), @"""name""\s*:\s*""([^""]+)""");
                    Assert.IsTrue(name.Success, $"precondition: {asmdef} has no name");
                    string module = name.Groups[1].Value;
                    if (!module.StartsWith("HiddenHarbours.", StringComparison.Ordinal)) continue;
                    Match guid = Regex.Match(File.ReadAllText(asmdef + ".meta"), @"^guid:\s*([0-9a-fA-F]{32})\s*$",
                        RegexOptions.Multiline);
                    Assert.IsTrue(guid.Success, $"precondition: {asmdef}.meta holds no 32-digit guid");
                    Assert.IsFalse(modules.ContainsKey(module), $"precondition: two asmdefs are named {module}");
                    modules.Add(module, guid.Groups[1].Value);
                }
            }
            return modules;
        }

        /// <summary>Whether Unity skips a folder or file by its name: one that starts with '.' or ends with '~'.</summary>
        private static bool UnitySkips(string path)
        {
            string name = Path.GetFileName(path);
            return name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith("~", StringComparison.Ordinal);
        }

        /// <summary>Whether an asmdef's text names a module, by its quoted name or by "GUID:" and its guid.</summary>
        private static bool Names(string asmdefText, string module, string guid)
            => asmdefText.IndexOf("\"" + module + "\"", StringComparison.OrdinalIgnoreCase) >= 0
                || asmdefText.IndexOf("GUID:" + guid, StringComparison.OrdinalIgnoreCase) >= 0;

        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static T Get<T>(object target, string field)
            => (T)target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static void SameBits(float expected, float got, string what)
            => Assert.AreEqual(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(got),
                $"{what}: {expected:R} vs {got:R}");

        private static void SameBits(Vector4 expected, Vector4 got, string what)
        {
            for (int i = 0; i < 4; i++) SameBits(expected[i], got[i], $"{what} [{i}]");
        }

        /// <summary>The Art tests' FoamTransportAllocationProbe, copied: that one is internal to its assembly.
        /// GC.Alloc events on the current thread, with a positive control.</summary>
        private sealed class AllocationProbe : IDisposable
        {
#if UNITY_5_3_OR_NEWER
            private ProfilerRecorder _recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 8192,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);

            public AllocationProbe()
            {
                Assert.AreEqual(true, _recorder.Valid,
                    "GC.Alloc must be available; an unavailable counter is not zero allocation.");
                Begin();
                End();
            }

            public void Begin() { _recorder.Reset(); _recorder.Start(); }
            public long End() { _recorder.Stop(); return _recorder.Count; }
            public void Dispose() => _recorder.Dispose();
#else
            private long _before;
            public void Begin() => _before = GC.GetAllocatedBytesForCurrentThread();
            public long End() => GC.GetAllocatedBytesForCurrentThread() - _before;
            public void Dispose() { }
#endif

            public void RequirePositiveControl()
            {
                Begin();
                var known = new byte[256];
                long observed = End();
                GC.KeepAlive(known);
                Assert.Greater(observed, 0, "a known allocation must show in the recorder used for the zero proof");
                Begin();
                long empty = End();   // read before the assert, whose boxed 0 would land inside the scope
                Assert.AreEqual(0, empty, "the recorder's own empty scope must allocate nothing");
            }
        }
    }
}
