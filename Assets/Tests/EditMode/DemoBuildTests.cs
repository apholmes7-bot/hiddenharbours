using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class DemoBuildTests
    {
        readonly List<GameObject> _hosts = new();
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        GameObject Host()
        {
            var host = new GameObject("DemoGateFixture");
            host.SetActive(false);
            _hosts.Add(host);
            return host;
        }

        static void Field(object host, string name, object value)
            => host.GetType().GetField(name, Private).SetValue(host, value);

        [SetUp]
        public void SetUp()
        {
            DemoBuild.Override = null;
            RegionBuildAvailability.Override = null;
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var host in _hosts) Object.DestroyImmediate(host);
            _hosts.Clear();
            DemoBuild.Override = null;
            RegionBuildAvailability.Override = null;
            GameServices.Reset();
        }

        [Test]
        public void DefaultBuildKeepsShortcuts()
        {
#if DEMO_BUILD
            Assert.IsTrue(DemoBuild.Enabled);
            Assert.IsFalse(DemoBuild.DevShortcutsAllowed);
#else
            Assert.IsFalse(DemoBuild.Enabled);
            Assert.IsTrue(DemoBuild.DevShortcutsAllowed);
#endif
        }

        [TestCase(false, "Neighbour", true)]
        [TestCase(true, "Neighbour", false)]
        [TestCase(true, "Island", true)]
        [TestCase(true, "Assets/Scenes/Island.unity", true)]
        [TestCase(true, "Arrival", true)]
        [TestCase(true, "Assets/Scenes/Arrival.unity", true)]
        [TestCase(true, "island", false)]
        [TestCase(true, "Other/Arrival.unity", false)]
        [TestCase(true, null, false)]
        [TestCase(true, "", false)]
        public void OnlyListedRegionsAreIncludedInTheDemo(bool demo, string scene, bool expected)
            => Assert.AreEqual(expected, DemoBuild.IncludesScene(demo, scene,
                new[] { "Assets/Scenes/Island.unity", "Assets/Scenes/Arrival.unity" }));

        [Test]
        public void MissingOrEmptyDemoSceneListsNeverIncludeADestination()
        {
            Assert.IsFalse(DemoBuild.IncludesScene(true, "Island", null));
            Assert.IsFalse(DemoBuild.IncludesScene(true, "Island", Array.Empty<string>()));
            Assert.IsFalse(DemoBuild.IncludesScene(true, "Island", new[] { "", null, " " }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TheSharedAvailabilityQueryHonoursDemoScope(bool demo)
        {
            DemoBuild.Override = demo;
            RegionBuildAvailability.Override = (_, __) => true;
            foreach (string path in DemoBuild.ScenePaths())
            {
                Assert.IsTrue(RegionBuildAvailability.IsAvailable("", path), path);
                Assert.IsTrue(RegionBuildAvailability.IsAvailable("", Path.GetFileNameWithoutExtension(path)), path);
            }
            Assert.AreEqual(!demo, RegionBuildAvailability.IsAvailable("region.neighbour", "Neighbour"));
        }

        [Test]
        public void PlayerBuildSelectsTheIslandAndItsOpeningOnlyWhenRequested()
        {
            Type builder = Type.GetType("HiddenHarbours.Tools.Editor.PlayerBuild, HiddenHarbours.Tools.Editor", true);
            MethodInfo select = builder.GetMethod("ScenePaths", BindingFlags.Public | BindingFlags.Static);
            var full = (string[])select.Invoke(null, new object[] { false });
            var demo = (string[])select.Invoke(null, new object[] { true });
            Assert.AreEqual(6, full.Length);
            CollectionAssert.AreEqual(new[] { "Assets/_Project/Scenes/StPeters.unity",
                "Assets/_Project/Scenes/EastWater.unity" }, demo);
            CollectionAssert.AreEqual(DemoBuild.ScenePaths(), demo);
            Assert.AreEqual(full[0], demo[0]);
            foreach (string path in demo)
            {
                CollectionAssert.Contains(full, path);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), path);
            }
            demo[0] = "changed";
            CollectionAssert.AreEqual(DemoBuild.ScenePaths(), (string[])select.Invoke(null, new object[] { true }));
            Assert.AreEqual(full[0], DemoBuild.ScenePaths()[0]);
            CollectionAssert.AreEqual(full, (string[])select.Invoke(null, new object[] { false }));
        }

        [Test]
        public void EveryDemoRegionRefusesEveryDestinationOutsideTheSceneList()
        {
            DemoBuild.Override = true;
            RegionBuildAvailability.Override = (_, __) => true;
            var map = WorldMap.LoadShipped();
            Assert.IsNotNull(map);
            var sources = map.Regions.Where(r => DemoBuild.IncludesScene(r.SceneName)).ToArray();
            Assert.AreEqual(DemoBuild.ScenePaths().Length, sources.Length);
            var destinations = AssetDatabase.FindAssets("t:RegionDef", new[] { "Assets/_Project/Data/Regions" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<RegionDef>).ToArray();
            CollectionAssert.IsSubsetOf(map.Regions, destinations);
            var names = DemoBuild.ScenePaths().Select(Path.GetFileNameWithoutExtension).ToArray();
            foreach (var source in sources)
            {
                GameServices.CurrentRegionId = source.Id;
                var loader = Host().AddComponent<RegionSceneLoader>();
                foreach (var target in destinations)
                {
                    Assert.IsNotNull(target);
                    bool included = names.Contains(target.SceneName);
                    Assert.AreEqual(included, RegionBuildAvailability.IsAvailable(target.Id, target.SceneName),
                        source.SceneName + " -> " + target.SceneName);
                    if (!included)
                    {
                        Assert.IsFalse(loader.Preload(target), target.SceneName);
                        Assert.IsNull(loader.PreloadingSceneName);
                    }
                }
            }
        }

        static MethodInfo ArgumentParser()
        {
            Type builder = Type.GetType("HiddenHarbours.Tools.Editor.PlayerBuild, HiddenHarbours.Tools.Editor", true);
            var parse = builder.GetMethod("ParseArguments", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(parse);
            return parse;
        }

        static List<string> BuildArguments() => new List<string>
        {
            "-buildOutput", "unused-output", "-buildProduct", "Test player",
            "-buildCommit", new string('a', 40), "-buildMainCommit", new string('b', 40)
        };

        [TestCase(false, false, "None")]
        [TestCase(true, false, "None")]
        [TestCase(false, true, "Lz4HC")]
        [TestCase(true, true, "Lz4HC")]
        public void BuildArgumentsAcceptDemoWithoutStartingABuild(bool demo, bool development, string compression)
        {
            var args = BuildArguments();
            args.AddRange(new[] { "-buildCompression", compression });
            if (development) args.Add("-buildDevelopment");
            if (demo) args.Add("-buildDemo");
            object request = ArgumentParser().Invoke(null, new object[] { args.ToArray() });
            object Value(string name) => request.GetType().GetField(name).GetValue(request);
            Assert.AreEqual(demo, Value("Demo"));
            Assert.AreEqual(development, Value("Development"));
            Assert.AreEqual(compression == "Lz4HC", Value("Lz4HC"));
            Assert.AreEqual("unused-output", Value("Output"));
            Assert.AreEqual("Test player", Value("Product"));
            Assert.AreEqual(new string('a', 40), Value("Commit"));
            Assert.AreEqual(new string('b', 40), Value("MainCommit"));
        }

        [TestCase("-buildOutput")]
        [TestCase("-buildProduct")]
        [TestCase("-buildCommit")]
        [TestCase("-buildMainCommit")]
        public void DemoArgumentsStillRequireEverySuppliedValue(string option)
        {
            var args = BuildArguments();
            args.RemoveAt(args.IndexOf(option) + 1);
            args.Add("-buildDemo");
            var error = Assert.Throws<TargetInvocationException>(() =>
                ArgumentParser().Invoke(null, new object[] { args.ToArray() }));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
            StringAssert.Contains(option, error.InnerException.Message);
        }

        [Test]
        public void DemoArgumentsStillRejectUnknownCompression()
        {
            var args = BuildArguments();
            args.AddRange(new[] { "-buildDemo", "-buildCompression", "Unknown" });
            var error = Assert.Throws<TargetInvocationException>(() =>
                ArgumentParser().Invoke(null, new object[] { args.ToArray() }));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
            StringAssert.Contains("-buildCompression", error.InnerException.Message);
        }

        [Test]
        public void SelectingTheDemoDoesNotEndIt()
        {
            int endings = 0;
            Action<DemoEnded> ended = _ => endings++;
            EventBus.Subscribe(ended);
            try
            {
                DemoBuild.Override = true;
                Assert.IsFalse(DemoBuild.DevShortcutsAllowed);
                DemoBuild.IncludesScene("Neighbour");
                DemoBuild.Override = false;
                Assert.AreEqual(0, endings);
            }
            finally { EventBus.Unsubscribe(ended); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FastTideIsInertInTheDemo(bool demo)
        {
            var clock = Host().AddComponent<GameClock>();
            GameServices.Clock = clock;
            var fast = Host().AddComponent<DevFastTide>();
            Field(fast, "_enabled", true);
            Field(fast, "_fastScale", 10f);
            DemoBuild.Override = demo;
            typeof(DevFastTide).GetMethod("Update", Private).Invoke(fast, null);
            Assert.AreEqual(demo ? 1f : 10f, clock.TimeScale);
            DemoBuild.Override = true;
            typeof(DevFastTide).GetMethod("Update", Private).Invoke(fast, null);
            Assert.AreEqual(1f, clock.TimeScale, "an already-applied preview also releases its speed");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WaterComparisonCannotBeToggledInTheDemo(bool demo)
        {
            var host = Host();
            host.AddComponent<SpriteRenderer>();
            var surface = host.AddComponent<DisplacedWaterSurface>();
            Assert.IsNotNull(surface);
            bool before = surface.Displaced;
            DemoBuild.Override = demo;
            surface.SetDisplaced(!before);
            Assert.AreEqual(demo ? before : !before, surface.Displaced);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GrassPreviewWalkerIsInertInTheDemo(bool demo)
        {
            var walker = Host().AddComponent<GrassDevWalker>();
            Field(walker, "_speed", 2f);
            DemoBuild.Override = demo;
            walker.Step(Vector2.right, 1f);
            Assert.AreEqual(demo ? Vector3.zero : new Vector3(2, 0, 0), walker.transform.position);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GrassPreviewWindIsInertInTheDemo(bool demo)
        {
            int windId = Shader.PropertyToID("_WindWorld");
            Vector4 before = Shader.GetGlobalVector(windId);
            var sentinel = new Vector4(7, 8, 9, 10);
            try
            {
                Shader.SetGlobalVector(windId, sentinel);
                var wind = Host().AddComponent<GrassDevWind>();
                DemoBuild.Override = demo;
                typeof(GrassDevWind).GetMethod("Update", Private).Invoke(wind, null);
                Assert.AreEqual(demo, Shader.GetGlobalVector(windId) == sentinel);
            }
            finally { Shader.SetGlobalVector(windId, before); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RotationPreviewShortcutsAreInertInTheDemo(bool demo)
        {
            var host = Host();
            var directional = host.AddComponent<DirectionalBoatSprite>();
            var rig = host.AddComponent<BoatRotationTestRig>();
            Field(rig, "_directional", directional);
            Field(rig, "_autoYawOn", false);
            directional.Mode = DirectionalBoatSprite.RotationMode.SnapDirectional;
            DemoBuild.Override = demo;
            rig.HandleShortcuts(true, true);
            Assert.AreEqual(demo ? DirectionalBoatSprite.RotationMode.SnapDirectional
                : DirectionalBoatSprite.RotationMode.SmoothRotateSingle, directional.Mode);
            Assert.AreEqual(!demo, (bool)typeof(BoatRotationTestRig).GetField("_autoYawOn", Private).GetValue(rig));
        }
    }
}
