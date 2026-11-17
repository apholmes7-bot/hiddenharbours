using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The first placed wash stations, as committed: each scene holds the station its text patch placed
    /// (a re-plan is empty, so the scene, the prefab and the placement rows agree), each prefab is a working
    /// station of a shipped washing Def, and the interact ids are distinct.
    /// </summary>
    public class WashStationPlacementTests
    {
        private static readonly Dictionary<string, WashStationPlacement.PrefabFacts> Facts = new Dictionary<string, WashStationPlacement.PrefabFacts>();

        private static WashStationPlacement.PrefabFacts FactsOf(string path)
        {
            if (!Facts.TryGetValue(path, out WashStationPlacement.PrefabFacts f))
                Facts[path] = f = WashStationPlacement.PrefabFacts.Read(Directory.GetCurrentDirectory(), path);
            return f;
        }

        private static IEnumerable<string> Scenes() => WashStationPlacement.Sites.Select(s => s.Scene).Distinct();

        [TestCaseSource(nameof(Scenes))]
        public void TheCommittedSceneHoldsItsStations_ARePlanIsEmpty(string scenePath)
        {
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText(scenePath));
            List<LayerPatch> patches = WashStationPlacement.Plan(scene, scenePath, FactsOf);
            Assert.IsNotEmpty(patches);
            foreach (LayerPatch p in patches)
                Assert.IsTrue(p.IsEmpty, $"re-planning {scenePath} would change it:\n{p.Summary()}");
        }

        [Test]
        public void EachPrefabIsAStationOfAShippedWashingDef()
        {
            var shipped = AssetDatabase.FindAssets("t:" + nameof(WashStationDef))
                .Select(g => AssetDatabase.LoadAssetAtPath<WashStationDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null).ToDictionary(d => d.Id);
            foreach (string path in WashStationPlacement.Sites.Select(s => s.Prefab).Distinct())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                var point = prefab.GetComponent<WashStationPoint>();
                Assert.IsNotNull(point, path + " carries no WashStationPoint");
                Assert.IsTrue(shipped.TryGetValue(point.StationId, out WashStationDef def), $"{path}: no shipped '{point.StationId}'");
                Assert.IsTrue(def.Washes, $"{path}: '{point.StationId}' does not wash");
                Assert.Greater(point.ReachMeters, 0f, path);
                Assert.IsNotNull(prefab.GetComponent<Collider2D>(), path + ": the player should stand at it, not in it");
                Assert.IsNotNull(prefab.GetComponent<SpriteRenderer>(), path);
            }
        }

        [Test]
        public void TheShowerIsAShower_TheWashstandIsABasin()
        {
            Assert.AreEqual("washstation.sink",
                AssetDatabase.LoadAssetAtPath<GameObject>(WashStationPlacement.WashStandPrefab).GetComponent<WashStationPoint>().StationId);
            Assert.AreEqual("washstation.shower",
                AssetDatabase.LoadAssetAtPath<GameObject>(WashStationPlacement.WharfShowerPrefab).GetComponent<WashStationPoint>().StationId);
        }

        [Test]
        public void InteractIdsAreDistinctFixtureIds()
        {
            var ids = WashStationPlacement.Sites.Select(s => s.InteractId).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
            foreach (string id in ids) StringAssert.StartsWith("fixture.", id);
        }

        [Test]
        public void TheWashstandStandsUpstairsInGinnysCottage()
        {
            WashStationPlacement.Site site = WashStationPlacement.Sites.Single(s => s.InteractId == WashStationPlacement.GinnyWashstandId);
            Assert.IsTrue(site.IsInterior);
            Assert.AreEqual(1, site.Level, "the player's room is upstairs");
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText(site.Scene));
            Vector2 world = WashStationPlacement.InteriorWorld(scene, site, out Doc parent, out _);
            Doc parentGo = scene.Require(parent.FieldRef("m_GameObject"), "furniture root");
            Assert.AreEqual(StPetersInteriors.UpperPropsChildName, parentGo.Field("m_Name"),
                            "it hangs under the upper storey's furniture, so it registers only upstairs");
            Vector2 cottage = WashStationPlacement.WorldOf(scene, scene.TransformOf(
                scene.ChildNamed(scene.TransformOf(scene.RootNamed(site.BuildingRoot)), site.Building)));
            Assert.Less(Vector2.Distance(world, cottage), 8f, "within the cottage's own few metres");
        }
    }
}
