using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// The room-fixture placement step, as committed (kitchens and bathrooms plan, section 3): each scene
    /// holds the fixtures its text patch placed (a re-plan is empty, so the scene, the prefabs and the rows
    /// agree), each interact id is in its scene once, each interior fixture lands on its planned model point,
    /// each prefab is one interactable of a shipped Def, and R1 places the pilot building's bathroom and
    /// nothing else. The first two wash stations (#1273) are rows of the same step.
    /// </summary>
    public class InteriorFixturePlacementTests
    {
        private static readonly Dictionary<string, InteriorFixturePlacement.PrefabFacts> Facts =
            new Dictionary<string, InteriorFixturePlacement.PrefabFacts>();

        private static InteriorFixturePlacement.PrefabFacts FactsOf(string path)
        {
            if (!Facts.TryGetValue(path, out InteriorFixturePlacement.PrefabFacts f))
                Facts[path] = f = InteriorFixturePlacement.PrefabFacts.Read(Directory.GetCurrentDirectory(), path);
            return f;
        }

        private static readonly Dictionary<string, SceneYaml> Parsed = new Dictionary<string, SceneYaml>();

        private static SceneYaml Scene(string path) =>
            Parsed.TryGetValue(path, out SceneYaml s) ? s : Parsed[path] = SceneYaml.Parse(File.ReadAllText(path));

        private static IEnumerable<string> Scenes() => InteriorFixturePlacement.Sites.Select(s => s.Scene).Distinct();

        private static IEnumerable<string> PilotIds() =>
            InteriorFixturePlacement.PlacedRows().Select(r => r.Id);

        /// <summary>Every prefab the step can stand, placed or not yet.</summary>
        private static IEnumerable<string> BathroomPrefabs() => new[]
        {
            WashStationPlacement.WashStandPrefab, WashStationPlacement.WharfShowerPrefab,
            InteriorFixturePlacement.ToiletPrefab, InteriorFixturePlacement.ShowerPrefab,
            InteriorFixturePlacement.BathPrefab, InteriorFixturePlacement.OuthousePrefab,
        };

        // ---- the committed scenes ------------------------------------------------------------------

        [TestCaseSource(nameof(Scenes))]
        public void TheCommittedSceneHoldsItsFixtures_ARePlanIsEmpty(string scenePath)
        {
            List<LayerPatch> patches = InteriorFixturePlacement.Plan(Scene(scenePath), scenePath, FactsOf);
            Assert.IsNotEmpty(patches);
            foreach (LayerPatch p in patches)
                Assert.IsTrue(p.IsEmpty, $"re-planning {scenePath} would change it:\n{p.Summary()}");
        }

        [Test]
        public void EachInteractIdIsInItsSceneOnce()
        {
            foreach (InteriorFixturePlacement.Site site in InteriorFixturePlacement.Sites)
            {
                string text = File.ReadAllText(site.Scene).Replace("\r\n", "\n");
                int n = Regex.Matches(text, "(?m)^      value: " + Regex.Escape(site.InteractId) + "$").Count;
                Assert.AreEqual(1, n, $"{site.InteractId} in {site.Scene}");
            }
        }

        [Test]
        public void InteractIdsAreDistinctFixtureIds()
        {
            var ids = InteriorFixturePlacement.Sites.Select(s => s.InteractId).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
            foreach (string id in ids) StringAssert.StartsWith("fixture.", id);
        }

        [Test]
        public void EachInteriorFixtureLandsOnItsPlannedPoint()
        {
            foreach (InteriorFixturePlacement.Site site in InteriorFixturePlacement.Sites.Where(s => s.IsInterior))
            {
                SceneYaml scene = Scene(site.Scene);
                InteriorFootprint footprint = InteriorFixturePlacement.FootprintOf(scene, site, out Doc parent, out _);
                Vector2 world = footprint.ModelToWorld(site.At);
                Vector2 back = footprint.WorldToModel(world);
                Assert.Less(Vector2.Distance(back, site.At), 0.001f, $"{site.InteractId}: model {site.At} came back {back}");
                Doc parentGo = scene.Require(parent.FieldRef("m_GameObject"), "furniture root");
                Assert.AreEqual(site.Level > 0 ? StPetersInteriors.UpperPropsChildName : StPetersInteriors.PropsChildName,
                                parentGo.Field("m_Name"), $"{site.InteractId} hangs under its own level's furniture");
            }
        }

        [Test]
        public void TheWashstandStandsUpstairsInGinnysCottage()
        {
            InteriorFixturePlacement.Site site = InteriorFixturePlacement.Sites.Single(s => s.InteractId == WashStationPlacement.GinnyWashstandId);
            Assert.IsTrue(site.IsInterior);
            Assert.AreEqual(1, site.Level, "the player's room is upstairs");
            SceneYaml scene = Scene(site.Scene);
            Vector2 world = InteriorFixturePlacement.InteriorWorld(scene, site, out _, out _);
            Vector2 cottage = InteriorFixturePlacement.WorldOf(scene, scene.TransformOf(
                scene.ChildNamed(scene.TransformOf(scene.RootNamed(site.BuildingRoot)), site.Building)));
            Assert.Less(Vector2.Distance(world, cottage), 8f, "within the cottage's own few metres");
        }

        // ---- the R1 pilot --------------------------------------------------------------------------

        [Test]
        public void R1PlacesGinnysBathroom_AndNothingElse()
        {
            var placed = InteriorFixturePlacement.PlacedRows().ToList();
            CollectionAssert.AreEquivalent(
                new[] { RoomFixturePlan.Kind.Toilet, RoomFixturePlan.Kind.BathroomSink, RoomFixturePlan.Kind.Shower },
                placed.Select(r => r.Kind), "Ginny's cottage plans a toilet, a basin and a shower");
            foreach (RoomFixturePlan.Row r in placed)
            {
                Assert.AreEqual(InteriorFixturePlacement.PilotBuildingKey, r.BuildingKey);
                Assert.AreEqual(0, r.Level, "the bathroom is on the ground floor");
            }
            Assert.AreEqual(2 + placed.Count, InteriorFixturePlacement.Sites.Length,
                            "the two first wash stations plus the pilot bathroom");
        }

        [Test]
        public void ThePilotIdsAreThePlansIds()
        {
            CollectionAssert.AreEquivalent(new[]
            {
                "fixture.st_peters.ginny_cottage.toilet",
                "fixture.st_peters.ginny_cottage.bathroom_sink",
                "fixture.st_peters.ginny_cottage.shower",
            }, PilotIds());
        }

        [Test]
        public void ASlotNamesItsGameObject()
        {
            Assert.AreEqual("BathroomSink", InteriorFixturePlacement.NameOf("bathroom_sink"));
            Assert.AreEqual("Toilet", InteriorFixturePlacement.NameOf("toilet"));
            Assert.AreEqual("PrepCounter", InteriorFixturePlacement.NameOf("prep_counter"));
        }

        [Test]
        public void EveryKindHasAPrefabOnDisk()
        {
            foreach (RoomFixturePlan.Kind kind in System.Enum.GetValues(typeof(RoomFixturePlan.Kind)))
            {
                string path = InteriorFixturePlacement.PrefabFor(kind);
                Assert.IsTrue(File.Exists(path), $"{kind}: {path}");
            }
        }

        // ---- plan, apply, plan again ----------------------------------------------------------------

        [Test]
        public void ANewRowPlansOnce_ThenRePlansEmpty()
        {
            // A bath that is not placed anywhere, stood in Ginny's cottage in memory only: the add path, once.
            var extra = new InteriorFixturePlacement.Site(InteriorFixturePlacement.StPetersScene,
                InteriorFixturePlacement.BathPrefab, "TestBath", "fixture.st_peters.test.bath", new Vector2(0f, -1f),
                "a test row", StPetersGinnyPlot.RootName, StPetersGinnyPlot.CottageKey, level: 0);
            var sites = InteriorFixturePlacement.Sites.Append(extra).ToList();
            string path = InteriorFixturePlacement.StPetersScene;
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText(path));
            List<LayerPatch> first = InteriorFixturePlacement.Plan(scene, path, FactsOf, sites);
            Assert.IsTrue(first.Any(p => !p.IsEmpty), "the new row adds an instance");
            foreach (LayerPatch p in first) scene = p.Apply(scene);
            List<LayerPatch> again = InteriorFixturePlacement.Plan(scene, path, FactsOf, sites);
            foreach (LayerPatch p in again) Assert.IsTrue(p.IsEmpty, "re-planning the applied text:\n" + p.Summary());
            string text = scene.Render();
            Assert.AreEqual(1, Regex.Matches(text, "(?m)^      value: fixture\\.st_peters\\.test\\.bath$").Count);
        }

        // ---- the prefabs ---------------------------------------------------------------------------

        [TestCaseSource(nameof(BathroomPrefabs))]
        public void EachPrefabIsOneInteractableOfAShippedDef(string path)
        {
            var shipped = AssetDatabase.FindAssets("t:" + nameof(WashStationDef))
                .Select(g => AssetDatabase.LoadAssetAtPath<WashStationDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null).ToDictionary(d => d.Id);
            InteriorFixturePlacement.PrefabFacts facts = FactsOf(path);   // refuses zero or two _id components
            Assert.Greater(facts.Point, 0L);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            Assert.IsNotNull(prefab.GetComponentInChildren<SpriteRenderer>(), path + " draws nothing");
            var wash = prefab.GetComponent<WashStationPoint>();
            var seat = prefab.GetComponent<SitPoint>();
            Assert.IsTrue(wash != null ^ seat != null, path + ": one interactable, a wash station or a seat");
            string stationId = wash != null ? wash.StationId : seat.StationId;
            Assert.IsTrue(shipped.TryGetValue(stationId, out WashStationDef def), $"{path}: no shipped '{stationId}'");
            if (wash != null) Assert.IsTrue(def.Washes, $"{path}: '{stationId}' does not wash");
            else Assert.IsTrue(def.SitOnly, $"{path}: '{stationId}' is not a seat");
            Assert.Greater(wash != null ? wash.ReachMeters : seat.ReachMeters, 0f, path);
        }

        [Test]
        public void TheBathroomPrefabsAreTheirKinds()
        {
            Assert.AreEqual("washstation.toilet",
                AssetDatabase.LoadAssetAtPath<GameObject>(InteriorFixturePlacement.ToiletPrefab).GetComponent<SitPoint>().StationId);
            Assert.AreEqual("washstation.shower",
                AssetDatabase.LoadAssetAtPath<GameObject>(InteriorFixturePlacement.ShowerPrefab).GetComponent<WashStationPoint>().StationId);
            Assert.AreEqual("washstation.bath",
                AssetDatabase.LoadAssetAtPath<GameObject>(InteriorFixturePlacement.BathPrefab).GetComponent<WashStationPoint>().StationId);
            Assert.AreEqual("washstation.sink",
                AssetDatabase.LoadAssetAtPath<GameObject>(WashStationPlacement.WashStandPrefab).GetComponent<WashStationPoint>().StationId);
            Assert.AreEqual("washstation.shower",
                AssetDatabase.LoadAssetAtPath<GameObject>(WashStationPlacement.WharfShowerPrefab).GetComponent<WashStationPoint>().StationId);
        }

        [Test]
        public void TheOuthouseDefStandsAsTheOuthousePrefab()
        {
            var outhouse = AssetDatabase.FindAssets("t:" + nameof(WashroomDef))
                .Select(g => AssetDatabase.LoadAssetAtPath<WashroomDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Single(d => d != null && d.Id == "washroom.outhouse");
            Assert.IsNotNull(outhouse.Prefab, "washroom.outhouse has a prefab");
            Assert.AreEqual(InteriorFixturePlacement.OuthousePrefab, AssetDatabase.GetAssetPath(outhouse.Prefab));
            var seat = outhouse.Prefab.GetComponent<SitPoint>();
            Assert.IsNotNull(seat, "the outhouse seats");
            Assert.IsTrue(outhouse.Fixtures.Any(f => f != null && f.Id == seat.StationId), "its seat is one of its fixtures");
            Assert.IsNotNull(outhouse.Prefab.GetComponent<Collider2D>(), "the booth is solid");
        }
    }
}
