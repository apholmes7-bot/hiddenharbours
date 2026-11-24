using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// Region / scene-load logic (VS-22): the pure pieces of the additive region path — RegionDef
    /// accessors, the RegionRegistry lookup, and the RegionTravel decisions. No Unity scene loading is
    /// exercised here (that needs play mode + Build Settings); these guard the logic the loader/passage
    /// lean on.
    /// </summary>
    public class RegionTests
    {
        private readonly List<Object> _spawned = new();

        private RegionDef MakeRegion(string id, string sceneName, string unlock = "")
        {
            var r = ScriptableObject.CreateInstance<RegionDef>();
            r.Id = id;
            r.SceneName = sceneName;
            r.UnlockFlag = unlock;
            _spawned.Add(r);
            return r;
        }

        [TearDown]
        public void TearDown()
        {
            RegionBuildAvailability.Override = null;
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- RegionDef ----------------------------------------------------------------------

        [TestCase(false)]
        [TestCase(true)]
        public void BuildAvailability_UsesTheEditorOverride(bool included)
        {
            string askedId = null, askedScene = null;
            RegionBuildAvailability.Override = (id, scene) =>
            {
                askedId = id; askedScene = scene;
                return included;
            };
            Assert.AreEqual(included, RegionBuildAvailability.IsAvailable("region.neighbour", "Neighbour"));
            Assert.AreEqual("region.neighbour", askedId);
            Assert.AreEqual("Neighbour", askedScene);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void BuildAvailability_NoSceneCannotBeIncluded(string scene)
        {
            RegionBuildAvailability.Override = (_, __) => true;
            Assert.IsFalse(RegionBuildAvailability.IsAvailable("region.neighbour", scene));
        }

        [Test]
        public void BuildAvailability_MissingSceneIsFalseWithoutAnOverride()
        {
            Assert.IsFalse(RegionBuildAvailability.IsAvailable("region.missing", "MissingSceneForAvailabilityTest"));
        }

        [Test]
        public void BuildAvailability_EnabledBuildScenesAreAvailable()
        {
            int count = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
            Assert.Greater(count, 0);
            for (int i = 0; i < count; i++)
            {
                string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                Assert.IsTrue(RegionBuildAvailability.IsAvailable("", path), path);
                Assert.IsTrue(RegionBuildAvailability.IsAvailable("", System.IO.Path.GetFileNameWithoutExtension(path)), path);
            }
        }

        [Test]
        public void BuildAvailability_DisabledSceneIsNotAvailableInEditMode()
        {
            var original = UnityEditor.EditorBuildSettings.scenes;
            var enabled = System.Array.Find(original, scene => scene.enabled &&
                !UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isLoaded);
            Assert.IsNotNull(enabled);
            try
            {
                UnityEditor.EditorBuildSettings.scenes = new[]
                {
                    new UnityEditor.EditorBuildSettingsScene(enabled.path, false)
                };
                Assert.IsFalse(RegionBuildAvailability.IsAvailable("", enabled.path));
                Assert.IsFalse(RegionBuildAvailability.IsAvailable("", System.IO.Path.GetFileNameWithoutExtension(enabled.path)));
            }
            finally { UnityEditor.EditorBuildSettings.scenes = original; }
        }

        [Test]
        public void Preload_AbsentNeighbourDoesNotStartOrAnnounceTravel()
        {
            var host = new GameObject("AvailabilityLoader");
            _spawned.Add(host);
            var loader = host.AddComponent<RegionSceneLoader>();
            var region = MakeRegion("region.absent", "AbsentNeighbour");
            RegionBuildAvailability.Override = (_, __) => false;
            int refusals = 0;
            System.Action<RegionTravelRefused> refused = _ => refusals++;
            EventBus.Subscribe(refused);
            try
            {
                Assert.IsFalse(loader.Preload(region));
                Assert.IsFalse(loader.Preload(region));
                Assert.IsNull(loader.PreloadingSceneName);
                Assert.AreEqual(0, refusals, "warming is not a travel request");
            }
            finally { EventBus.Unsubscribe(refused); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TravelTo_AbsentNeighbourRefusesByIdOrDef(bool byId)
        {
            var host = new GameObject("AvailabilityLoader");
            _spawned.Add(host);
            var loader = host.AddComponent<RegionSceneLoader>();
            var region = MakeRegion("region.absent", "AbsentNeighbour");
            typeof(RegionSceneLoader).GetField("_registry",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(loader, new RegionRegistry(new[] { region }));
            RegionBuildAvailability.Override = (_, __) => false;
            var refusals = new List<RegionTravelRefused>();
            EventBus.Subscribe<RegionTravelRefused>(refusals.Add);
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                    new System.Text.RegularExpressions.Regex("Could not load scene 'AbsentNeighbour'"));
                if (byId) loader.TravelTo(region.Id);
                else Assert.IsFalse(loader.Travel(region));
                Assert.AreEqual(1, refusals.Count);
                Assert.AreEqual(region.Id, refusals[0].RegionId);
                Assert.AreEqual(region.SceneName, refusals[0].SceneName);
                Assert.IsNull(loader.PreloadingSceneName);
            }
            finally { EventBus.Unsubscribe<RegionTravelRefused>(refusals.Add); }
        }

        [Test]
        public void RegionDef_HasScene_ReflectsSceneName()
        {
            Assert.IsTrue(MakeRegion("region.nine_mile_creek", "NineMileCreek").HasScene);
            Assert.IsFalse(MakeRegion("region.nowhere", "").HasScene);
            Assert.IsFalse(MakeRegion("region.blank", "   ").HasScene, "whitespace is not a scene");
        }

        [Test]
        public void RegionDef_RequiresUnlock_ReflectsUnlockFlag()
        {
            Assert.IsFalse(MakeRegion("region.coddle_cove", "Greybox").RequiresUnlock);
            Assert.IsTrue(MakeRegion("region.ironbound", "Ironbound", "flag.ironbound").RequiresUnlock);
        }

        // ---- RegionRegistry -----------------------------------------------------------------

        [Test]
        public void Registry_FindsByStableId()
        {
            var nineMileCreek = MakeRegion("region.nine_mile_creek", "NineMileCreek");
            var cove = MakeRegion("region.coddle_cove", "Greybox");
            var reg = new RegionRegistry(new[] { nineMileCreek, cove });

            Assert.AreEqual(2, reg.Count);
            Assert.IsTrue(reg.Contains("region.nine_mile_creek"));
            Assert.IsTrue(reg.TryGet("region.coddle_cove", out var got));
            Assert.AreSame(cove, got);
            Assert.AreSame(nineMileCreek, reg.Get("region.nine_mile_creek"));
        }

        [Test]
        public void Registry_UnknownId_IsMiss()
        {
            var reg = new RegionRegistry(new[] { MakeRegion("region.nine_mile_creek", "NineMileCreek") });
            Assert.IsFalse(reg.Contains("region.banks"));
            Assert.IsFalse(reg.TryGet("region.banks", out var got));
            Assert.IsNull(got);
            Assert.IsNull(reg.Get("region.banks"));
            Assert.IsFalse(reg.TryGet(null, out _), "null id is a graceful miss");
        }

        [Test]
        public void Registry_SkipsNullsBlankIdsAndDuplicates()
        {
            var first = MakeRegion("region.dup", "SceneA");
            var dup   = MakeRegion("region.dup", "SceneB");        // same id — ignored (first wins)
            var blank = MakeRegion("", "SceneC");                  // blank id — skipped
            var reg = new RegionRegistry(new[] { first, null, dup, blank });

            Assert.AreEqual(1, reg.Count, "only the first valid, unique-id region is kept");
            Assert.AreSame(first, reg.Get("region.dup"));
        }

        [Test]
        public void Registry_NullInput_IsEmpty()
        {
            var reg = new RegionRegistry(null);
            Assert.AreEqual(0, reg.Count);
            Assert.IsFalse(reg.Contains("anything"));
        }

        // ---- RegionTravel -------------------------------------------------------------------

        [Test]
        public void Travel_CanLoad_RequiresARegionWithAScene()
        {
            Assert.IsTrue(RegionTravel.CanLoad(MakeRegion("region.nine_mile_creek", "NineMileCreek")));
            Assert.IsFalse(RegionTravel.CanLoad(null));
            Assert.IsFalse(RegionTravel.CanLoad(MakeRegion("region.nowhere", "")));
        }

        [Test]
        public void Travel_IsAlreadyHere_MatchesCurrentScene()
        {
            var nineMileCreek = MakeRegion("region.nine_mile_creek", "NineMileCreek");
            Assert.IsTrue(RegionTravel.IsAlreadyHere("NineMileCreek", nineMileCreek));
            Assert.IsFalse(RegionTravel.IsAlreadyHere("Greybox", nineMileCreek));
            Assert.IsFalse(RegionTravel.IsAlreadyHere("", nineMileCreek), "no current scene → not 'here'");
        }

        [Test]
        public void Travel_ShouldLoad_OnlyWhenLoadableAndElsewhere()
        {
            var nineMileCreek = MakeRegion("region.nine_mile_creek", "NineMileCreek");
            var cove = MakeRegion("region.coddle_cove", "Greybox");

            // From the cove, Nine Mile Creek is loadable.
            Assert.IsTrue(RegionTravel.ShouldLoad("Greybox", nineMileCreek));
            // Already in Nine Mile Creek → don't reload it.
            Assert.IsFalse(RegionTravel.ShouldLoad("NineMileCreek", nineMileCreek));
            // A region with no scene is never loadable.
            Assert.IsFalse(RegionTravel.ShouldLoad("NineMileCreek", MakeRegion("region.nowhere", "")));
            // Null target → no-op.
            Assert.IsFalse(RegionTravel.ShouldLoad("NineMileCreek", null));
            // Round-trip back to the cove from Nine Mile Creek is loadable.
            Assert.IsTrue(RegionTravel.ShouldLoad("NineMileCreek", cove));
        }

        [Test]
        public void Travel_RoundTrip_CoveNineMileCreekCove_IsLoadableEachLeg_NoReloadInPlace()
        {
            // The VS-22 round trip the passages drive: each hop into another region loads, and asking to
            // "travel" to the region you're already standing in is a no-op (the coordinator just re-shows
            // a region that's already loaded — no decision to load).
            var cove = MakeRegion("region.coddle_cove", "Greybox");
            var nineMileCreek = MakeRegion("region.nine_mile_creek", "NineMileCreek");

            Assert.IsTrue(RegionTravel.ShouldLoad("Greybox", nineMileCreek), "outbound: cove → Nine Mile Creek");
            Assert.IsFalse(RegionTravel.ShouldLoad("NineMileCreek", nineMileCreek), "in Nine Mile Creek, don't reload Nine Mile Creek");
            Assert.IsTrue(RegionTravel.ShouldLoad("NineMileCreek", cove), "return: Nine Mile Creek → cove");
            Assert.IsFalse(RegionTravel.ShouldLoad("Greybox", cove), "back in the cove, don't reload the cove");
        }
    }
}
