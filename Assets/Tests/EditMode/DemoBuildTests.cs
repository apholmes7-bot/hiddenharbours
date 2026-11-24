using System;
using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        [TestCase(true, "island", false)]
        [TestCase(true, "", false)]
        public void OnlyTheBootRegionIsIncludedInTheDemo(bool demo, string scene, bool expected)
            => Assert.AreEqual(expected, DemoBuild.IncludesScene(demo, scene, "Assets/Scenes/Island.unity"));

        [TestCase(false)]
        [TestCase(true)]
        public void TheSharedAvailabilityQueryHonoursDemoScope(bool demo)
        {
            DemoBuild.Override = demo;
            RegionBuildAvailability.Override = (_, __) => true;
            string boot = SceneUtility.GetScenePathByBuildIndex(0);
            Assert.IsNotEmpty(boot);
            Assert.IsTrue(RegionBuildAvailability.IsAvailable("region.boot", boot));
            Assert.AreEqual(!demo, RegionBuildAvailability.IsAvailable("region.neighbour", "Neighbour"));
        }

        [Test]
        public void PlayerBuildSelectsOneSceneOnlyWhenRequested()
        {
            Type builder = Type.GetType("HiddenHarbours.Tools.Editor.PlayerBuild, HiddenHarbours.Tools.Editor", true);
            MethodInfo select = builder.GetMethod("ScenePaths", BindingFlags.Public | BindingFlags.Static);
            var full = (string[])select.Invoke(null, new object[] { false });
            var demo = (string[])select.Invoke(null, new object[] { true });
            Assert.AreEqual(6, full.Length);
            CollectionAssert.AreEqual(new[] { "Assets/_Project/Scenes/StPeters.unity" }, demo);
            Assert.AreEqual(full[0], demo[0]);
            demo[0] = "changed";
            CollectionAssert.AreEqual(full, (string[])select.Invoke(null, new object[] { false }));
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
            var surface = Host().AddComponent<DisplacedWaterSurface>();
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
