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
    /// each prefab is one interactable of a shipped Def, and the step places the pilot building's kitchen
    /// and bathroom and nothing else. The first two wash stations (#1273) are rows of the same step.
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
            foreach (InteriorFixturePlacement.Site site in InteriorFixturePlacement.Sites.Where(s => s.InteractId != null))
            {
                string text = File.ReadAllText(site.Scene).Replace("\r\n", "\n");
                int n = Regex.Matches(text, "(?m)^      value: " + Regex.Escape(site.InteractId) + "$").Count;
                Assert.AreEqual(1, n, $"{site.InteractId} in {site.Scene}");
            }
        }

        [Test]
        public void InteractIdsAreDistinctFixtureIds()
        {
            // A fixture's interact id, or a remembered container's storage.* save key (the home storage sites);
            // a site with no id (the shore lead) is a picture and claims nothing.
            var ids = InteriorFixturePlacement.Sites.Select(s => s.InteractId).Where(id => id != null).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
            foreach (string id in ids)
                Assert.IsTrue(id.StartsWith("fixture.") || StorageIds.IsStorageId(id), $"'{id}' is neither a fixture nor a storage id");
        }

        [Test]
        public void EachInteriorFixtureLandsOnItsPlannedPoint()
        {
            foreach (InteriorFixturePlacement.Site site in InteriorFixturePlacement.Sites.Where(s => s.IsInterior))
            {
                SceneYaml scene = Scene(site.Scene);
                InteriorFootprint footprint = InteriorFixturePlacement.FootprintOf(scene, site, out Doc parent, out _);
                Vector2 model = InteriorFixturePlacement.ModelPoint(footprint, site);
                Vector2 world = InteriorFixturePlacement.InteriorWorld(scene, site, out _, out _);
                Vector2 back = footprint.WorldToModel(world);
                Assert.Less(Vector2.Distance(back, model), 0.001f, $"{site.InteractId}: model {model} came back {back}");
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

        // ---- the pilot building: Ginny's cottage ----------------------------------------------------

        [Test]
        public void GinnysKitchenAndBathroomArePlaced_AndNothingElse()
        {
            var placed = InteriorFixturePlacement.PlacedRows().ToList();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    RoomFixturePlan.Kind.PrepCounter, RoomFixturePlan.Kind.Stove, RoomFixturePlan.Kind.Microwave,
                    RoomFixturePlan.Kind.KitchenSink, RoomFixturePlan.Kind.Fridge,
                    RoomFixturePlan.Kind.Toilet, RoomFixturePlan.Kind.BathroomSink, RoomFixturePlan.Kind.Shower,
                },
                placed.Select(r => r.Kind), "Ginny's cottage places a kitchen run, a kitchen sink, a fridge, a toilet, a basin and a shower");
            foreach (RoomFixturePlan.Row r in placed)
            {
                Assert.AreEqual(InteriorFixturePlacement.PilotBuildingKey, r.BuildingKey);
                Assert.AreEqual(0, r.Level, "the kitchen and bathroom are on the ground floor");
            }
            Assert.AreEqual(2 + placed.Count + HomeStoragePlacement.Sites().Count, InteriorFixturePlacement.Sites.Length,
                            "the two first wash stations plus Ginny's placed rows, plus the home storage sites");
        }

        [Test]
        public void GinnysPlacedRows_NoUnrecordedSouthPiece_AndEachIdIsInTheSceneOnce()
        {
            string text = File.ReadAllText(RoomFixturePlan.Scene).Replace("\r\n", "\n");
            foreach (RoomFixturePlan.Row r in InteriorFixturePlacement.PlacedRows().Where(r => r.BuildingKey == "ginny_cottage"))
            {
                Assert.IsFalse(RoomFixturePlan.IsSouthWall(r.Backing) && !RoomFixturePlan.SouthWallExceptions.Contains(r.Id),
                               $"{r.Id} backs onto the {r.Backing} wall and is not a recorded exception");
                // The fridge stands with no interact id (it has no verb yet), so its id is not in the scene.
                int n = Regex.Matches(text, "(?m)^      value: " + Regex.Escape(r.Id) + "$").Count;
                Assert.AreEqual(RoomFixturePlan.HasInteractId(r.Kind) ? 1 : 0, n, $"{r.Id} in {RoomFixturePlan.Scene}");
            }
        }

        [Test]
        public void APlanRowKeepsItsDistanceFromTheDoor_WhicheverWallTheFootprintsDoorIsIn()
        {
            // The plan's +y is the door end. Ginny's footprint has its door in the -y wall, so a row stood
            // there without the half turn lands at the wrong end of the room (the first look's bug).
            foreach (InteriorFixturePlacement.Site site in InteriorFixturePlacement.Sites.Where(s => s.PlanFrame))
            {
                SceneYaml scene = Scene(site.Scene);
                InteriorFootprint footprint = InteriorFixturePlacement.FootprintOf(scene, site, out _, out _);
                Vector2 world = InteriorFixturePlacement.InteriorWorld(scene, site, out _, out _);
                Vector2 model = footprint.WorldToModel(world);
                Assert.AreEqual(site.At.y, model.y * footprint.DoorSign, 0.001f, $"{site.InteractId}: toward the door");
                float toDoor = Vector2.Distance(world, footprint.DoorWorld);
                float centreToDoor = Vector2.Distance(footprint.Centre, footprint.DoorWorld);
                if (site.At.y < 0f)
                    Assert.Greater(toDoor, centreToDoor, $"{site.InteractId} is planned in the back half, so it stands farther from the door than the centre");
            }
        }

        [Test]
        public void EachBuildingsDoorCompassIsTheScenes()
        {
            // The compass a plan row's backing wall is read in comes from its building's stated door way;
            // hold that to the serialized BuildingInterior (facing and door wall), turned onto the ground.
            SceneYaml scene = Scene(RoomFixturePlan.Scene);
            float step = 360f / RoomFixturePlan.CompassPoints;
            foreach (var b in RoomFixturePlan.Rows.Select(r => (r.BuildingKey, r.BuildingRoot, r.Building, r.Door)).Distinct())
            {
                Doc go = scene.ChildNamed(scene.TransformOf(scene.RootNamed(b.BuildingRoot)), b.Building);
                Doc bi = scene.ComponentsOf(go).Single(d => d.IsScript("HiddenHarbours.World.BuildingInterior"));
                float doorSign = bi.Field("_doorOnPlusY") == "1" ? 1f : -1f;
                var footprint = new InteriorFootprint(Vector2.zero, 1f, 1f,
                    ParseInt(bi.Field("_facing")), ParseInt(bi.Field("_facings")), 1f, doorSign);
                Vector2 ground = footprint.ModelToWorld(new Vector2(0f, doorSign));
                float bearing = Mathf.Atan2(ground.x, ground.y) * Mathf.Rad2Deg;
                int points = RoomFixturePlan.CompassPoints;
                var scenes = (RoomFixturePlan.Compass)(((Mathf.RoundToInt(bearing / step) % points) + points) % points);
                Assert.AreEqual(scenes, b.Door, $"{b.BuildingKey}: the scene's door faces {scenes} (bearing {bearing:0.#})");
            }
        }

        [Test]
        public void GinnysBathroomPiecesNeverOverlap()
        {
            // The washroom is its own room now (1.75 m across), so the basin stands beside the toilet as a vanity
            // does. That each piece is still the one offered where he stands to use it, and is reachable at
            // all, is GinnyCottageRoomsTests' walk over the real floor; a fixed spacing stood in for that before.
            var rows = InteriorFixturePlacement.PlacedRows().Where(r => RoomFixturePlan.IsBathroom(r.Kind)).ToList();
            Assert.AreEqual(3, rows.Count, "Ginny's toilet, basin and shower");
            Rect Box(RoomFixturePlan.Row r)
            {
                Vector2 s = RoomFixturePlan.FootprintOf(r);
                return new Rect(r.At - s * 0.5f, s);
            }
            for (int i = 0; i < rows.Count; i++)
            for (int j = i + 1; j < rows.Count; j++)
            {
                Rect a = Box(rows[i]), b = Box(rows[j]);
                bool overlap = a.xMin < b.xMax - 1e-3f && b.xMin < a.xMax - 1e-3f && a.yMin < b.yMax - 1e-3f && b.yMin < a.yMax - 1e-3f;
                Assert.IsFalse(overlap, $"{rows[i].Id} and {rows[j].Id} overlap");
            }
        }

        [Test]
        public void ThePilotIdsAreThePlansIds()
        {
            CollectionAssert.AreEquivalent(new[]
            {
                "fixture.st_peters.ginny_cottage.prep_counter",
                "fixture.st_peters.ginny_cottage.stove",
                "fixture.st_peters.ginny_cottage.microwave",
                "fixture.st_peters.ginny_cottage.kitchen_sink",
                "fixture.st_peters.ginny_cottage.fridge",
                "fixture.st_peters.ginny_cottage.toilet",
                "fixture.st_peters.ginny_cottage.bathroom_sink",
                "fixture.st_peters.ginny_cottage.shower",
            }, PilotIds());
            CollectionAssert.AllItemsAreUnique(PilotIds().ToList());
        }

        [Test]
        public void ASlotNamesItsGameObject()
        {
            Assert.AreEqual("BathroomSink", InteriorFixturePlacement.NameOf("bathroom_sink"));
            Assert.AreEqual("Toilet", InteriorFixturePlacement.NameOf("toilet"));
            Assert.AreEqual("PrepCounter", InteriorFixturePlacement.NameOf("prep_counter"));
            Assert.AreEqual("KitchenSink", InteriorFixturePlacement.NameOf("kitchen_sink"));
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

        [Test]
        public void GinnysKitchenSinkIsAWashStation_AndHerFridgeIsTheFridgeDef_BothInTheSceneOnce()
        {
            var sinkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteriorFixturePlacement.PrefabFor(RoomFixturePlan.Kind.KitchenSink));
            Assert.AreEqual("washstation.sink", sinkPrefab.GetComponent<WashStationPoint>().StationId);
            Assert.Greater(sinkPrefab.GetComponent<WashStationPoint>().ReachMeters, 0f);
            var fridgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InteriorFixturePlacement.PrefabFor(RoomFixturePlan.Kind.Fridge));
            var store = fridgePrefab.GetComponent<KitchenStoreFixture>();
            Assert.IsNotNull(store, "the fridge prefab names its food store");
            Assert.AreEqual("kitchen_storage.fridge", store.Def.Id);
            Assert.AreEqual(0L, FactsOf(InteriorFixturePlacement.PrefabFor(RoomFixturePlan.Kind.Fridge)).Point,
                            "the fridge carries no interact id, so it stands with none");

            InteriorFixturePlacement.Site sink = InteriorFixturePlacement.Sites.Single(s => s.InteractId == "fixture.st_peters.ginny_cottage.kitchen_sink");
            InteriorFixturePlacement.Site fridge = InteriorFixturePlacement.Sites.Single(s => s.Name == "Fridge" && s.Building == StPetersGinnyPlot.CottageKey);
            Assert.IsNull(fridge.InteractId);
            Assert.AreEqual(InteriorFixturePlacement.PrefabFor(RoomFixturePlan.Kind.KitchenSink), sink.Prefab);
            Assert.AreEqual(InteriorFixturePlacement.PrefabFor(RoomFixturePlan.Kind.Fridge), fridge.Prefab);

            // One "Fridge" instance of the fridge prefab under the cottage's ground furniture root.
            SceneYaml scene = Scene(fridge.Scene);
            InteriorFixturePlacement.FootprintOf(scene, fridge, out Doc parent, out _);
            string guid = FactsOf(fridge.Prefab).Guid;
            int fridges = parent.FieldRefs("m_Children").Select(c => scene.Require(c, "a child"))
                .Count(t => t.Stripped && ObjRef.Parse(t.Field("m_CorrespondingSourceObject")).Guid == guid);
            Assert.AreEqual(1, fridges, "one fridge under Ginny's ground furniture");
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
