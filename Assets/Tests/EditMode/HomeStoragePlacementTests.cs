using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Row = HiddenHarbours.App.Editor.HomeStoragePlan.Row;
using Site = HiddenHarbours.App.Editor.InteriorFixturePlacement.Site;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Storage with memory, stood in the world (owner 2026-11-17): Ginny's rod rack and gear locker are sites of
    /// the room-fixture step under their own <c>storage.*</c> ids, their prefabs carry their row's Def (so the
    /// capacity and the deed are the Def's) and no save key of their own, the scene holds each once, Ginny's are
    /// open with no deed while a camper Def stays shut until the camper's deed is his, and the shore power lead
    /// runs from under the cottage to the foot of the camper's <c>shore_power</c> hookup. The camper's own stores
    /// are not stood until the camper has a room to walk into.
    /// </summary>
    public class HomeStoragePlacementTests
    {
        const float Eps = 1e-3f;
        const string StowageFolder = "Assets/_Project/Data/Stowage";

        sealed class FlagSave : ISaveService
        {
            public SaveData Current { get; set; } = SaveMigration.NewGame();
            public bool GetFlag(string key)
            {
                if (Current?.OnboardingFlags == null) return false;
                foreach (SaveFlag f in Current.OnboardingFlags) if (f.Key == key) return f.Value;
                return false;
            }
            public void SetFlag(string key, bool value)
            {
                Current.OnboardingFlags ??= new List<SaveFlag>();
                Current.OnboardingFlags.RemoveAll(f => f.Key == key);
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }
            public void Save() { }
        }

        readonly List<Object> _spawned = new List<Object>();
        FlagSave _save;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            _save = new FlagSave();
            GameServices.Save = _save;
        }

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        static StowageDef Def(string id) =>
            AssetDatabase.FindAssets("t:" + nameof(StowageDef), new[] { StowageFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<StowageDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Single(d => d != null && d.Id == id);

        static IEnumerable<Row> Placed() => HomeStoragePlacement.PlacedRows();

        static Site SiteOf(Row row) => InteriorFixturePlacement.Sites.Single(s => s.InteractId == row.StorageId);

        GameObject Spawn(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, prefabPath);
            var go = Object.Instantiate(prefab);
            _spawned.Add(go);
            return go;
        }

        // ---- Ginny's: the sites -------------------------------------------------------------------------

        [Test]
        public void EveryPositionedRowIsASiteOfTheStep_UnderItsOwnStorageId()
        {
            CollectionAssert.AreEquivalent(
                new[] { "storage.stpeters.ginny_rod_rack", "storage.stpeters.ginny_gear_locker" },
                Placed().Select(r => r.StorageId).ToArray(), "Ginny's two, and only they, have a place yet");
            foreach (Row row in Placed())
            {
                Site site = SiteOf(row);
                Assert.IsTrue(site.IsInterior, row.StorageId);
                Assert.AreEqual(StPetersGinnyPlot.RootName, site.BuildingRoot);
                Assert.AreEqual(StPetersGinnyPlot.CottageKey, site.Building);
                Assert.AreEqual(row.Level, site.Level, $"{row.StorageId}: his room upstairs");
                Assert.AreEqual(row.At, site.At, $"{row.StorageId}: the plan's model point");
                Assert.IsTrue(site.PlanFrame, $"{row.StorageId}: the UpperLevelFor frame the bed is planned in, door at +y, half-turned like a fixture row");
                Assert.AreEqual(HomeStoragePlacement.PrefabFor(row), site.Prefab);
            }
        }

        [Test]
        public void EachRackPrefabCarriesItsRowsDef_AndNoSaveKeyOfItsOwn()
        {
            foreach (Row row in Placed())
            {
                string path = HomeStoragePlacement.PrefabFor(row);
                Assert.IsTrue(File.Exists(path), path);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var rack = prefab.GetComponent<StowageRack>();
                Assert.IsNotNull(rack, $"{path} is a StowageRack");
                Assert.IsNotNull(rack.Def, $"{path} has no Def");
                Assert.AreEqual(row.DefId, rack.Def.Id, path);
                Assert.IsFalse(rack.IsRemembered, $"{path}: only the placed instance carries the storage id (the save key)");
                Assert.IsNotNull(prefab.GetComponentInChildren<SpriteRenderer>(), path + " draws nothing");
                Assert.IsNotNull(prefab.GetComponent<Collider2D>(), path + " is walked through");
                InteriorFixturePlacement.PrefabFacts facts =
                    InteriorFixturePlacement.PrefabFacts.Read(Directory.GetCurrentDirectory(), path);
                Assert.Greater(facts.Point, 0L, $"{path}: the rack is the one component with an _id");
            }
        }

        [Test]
        public void TheSceneStandsEachRackOnce_AsItsPrefab_UnderItsStorageId()
        {
            string[] docs = File.ReadAllText(HomeStoragePlan.Scene).Replace("\r\n", "\n").Split(new[] { "\n--- " }, System.StringSplitOptions.None);
            foreach (Row row in Placed())
            {
                string guid = AssetDatabase.AssetPathToGUID(HomeStoragePlacement.PrefabFor(row));
                Assert.IsNotEmpty(guid);
                string source = "\n  m_SourcePrefab: {fileID: 100100000, guid: " + guid + ", type: 3}";
                var blocks = docs.Where(d => d.StartsWith("!u!1001 ", System.StringComparison.Ordinal) &&
                                             d.Contains(source)).ToList();
                Assert.AreEqual(1, blocks.Count, $"{row.StorageId}: one instance of its prefab in the scene");
                StringAssert.Contains("      propertyPath: _id\n      value: " + row.StorageId + "\n", blocks[0]);
                StringAssert.Contains("      propertyPath: m_Name\n      value: " + HomeStoragePlacement.NameOf(row) + "\n", blocks[0]);
            }
        }

        // ---- the capacities and the gate are the Def's ------------------------------------------------------

        [Test]
        public void APlacedRacksCapacityAndWhatItTakesAreItsDefs()
        {
            foreach (Row row in Placed())
            {
                var rack = Spawn(HomeStoragePlacement.PrefabFor(row)).GetComponent<StowageRack>();
                StowageDef def = Def(row.DefId);
                Assert.AreEqual(def.Capacity, rack.Capacity, row.DefId);
                Assert.AreEqual(def.Accepts, rack.Accepts, row.DefId);
            }
        }

        [Test]
        public void GinnysRackIsOpenWithNoDeed_ACamperDefStaysShutUntilHisDeed()
        {
            foreach (Row row in Placed())
                Assert.IsTrue(Spawn(HomeStoragePlacement.PrefabFor(row)).GetComponent<StowageRack>().IsOpen,
                              $"{row.StorageId}: lodging needs no deed");

            // The same prefab wired to the camper's Def (what the camper's rack will be once it has a room).
            var camper = Spawn(HomeStoragePlacement.GinnyRodRackPrefab).GetComponent<StowageRack>();
            StowageDef camperDef = Def("stowage.camper_rod_rack");
            camper.Configure(camperDef, null, "");
            Assert.IsFalse(camper.IsOpen, "shut until the camper is his");
            HomeDeeds.Grant(_save, camperDef.DeedHomeId);
            Assert.IsTrue(camper.IsOpen, "open once the deed is held");
        }

        // ---- the camper -------------------------------------------------------------------------------------

        [Test]
        public void TheCampersAndTheShedsStoresAreNotStoodYet()
        {
            foreach (Row row in HomeStoragePlan.Rows.Where(r => r.BuildingKey != "ginny_cottage"))
            {
                Assert.IsFalse(row.HasPosition, $"{row.StorageId}: no room to walk into yet, so no place");
                Assert.IsFalse(InteriorFixturePlacement.Sites.Any(s => s.InteractId == row.StorageId),
                               $"{row.StorageId} is stood somewhere");
            }
        }

        [Test]
        public void TheLeadRunsFromUnderTheCottage_ToTheFootOfTheShorePowerHookup()
        {
            Assert.IsTrue(HomeStoragePlacement.TryShoreLeadPath(out List<Vector2> path), "the camper sidecar reads");
            Assert.IsTrue(HomeStoragePlacement.TryHookupGround(out Vector2 hookup), "the shell declares shore_power");
            Assert.Less(Vector2.Distance(path[path.Count - 1], hookup), Eps, "it ends under the hookup");
            Assert.AreEqual(HomeStoragePlacement.CottageTapMetres,
                            Vector2.Distance(path[0], StPetersGinnyPlot.CottagePos), Eps, "it leaves from under the cottage");

            // The hookup is on the street side, away from the cottage: the lead goes round the camper's tail.
            Vector2 toCottage = (StPetersGinnyPlot.CottagePos - StPetersCamperLot.LotPos).normalized;
            Assert.Less(Vector2.Dot(hookup - StPetersCamperLot.LotPos, toCottage), 0f, "shore_power is on the far side");

            // Every corner it turns is clear of the camper's body (the body frame: |x| within half its width,
            // y from the tail to the nose of the body).
            CamperSidecar.Dimensions d = StPetersCamperLot.Dimensions;
            Vector2 curb = toCottage, nose = StPetersCamperLot.NoseDirection;
            for (int i = 1; i < path.Count - 1; i++)
            {
                Vector2 rel = path[i] - StPetersCamperLot.LotPos;
                float x = Vector2.Dot(rel, curb), y = Vector2.Dot(rel, nose);
                bool inBody = Mathf.Abs(x) < d.WidthMetres * 0.5f && y > -d.TailMetres && y < d.BodyLengthMetres * 0.5f;
                Assert.IsFalse(inBody, $"corner {i} at {path[i]} is inside the camper");
            }
        }

        [Test]
        public void TheLeadPrefabIsTheRoute_AndTheSceneStandsItAtTheHookup()
        {
            Assert.IsTrue(HomeStoragePlacement.TryShoreLeadPath(out List<Vector2> path));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HomeStoragePlacement.ShoreLeadPrefab);
            Assert.IsNotNull(prefab, HomeStoragePlacement.ShoreLeadPrefab);
            List<(Vector2 a, Vector2 b)> runs = HomeStoragePlacement.LeadRuns(prefab, path[path.Count - 1]);
            Assert.AreEqual(path.Count - 1, runs.Count, "one run per leg");
            for (int i = 0; i < runs.Count; i++)
            {
                Assert.Less(Vector2.Distance(runs[i].a, path[i]), 0.01f, $"run {i} starts at {path[i]}, not {runs[i].a}");
                Assert.Less(Vector2.Distance(runs[i].b, path[i + 1]), 0.01f, $"run {i} ends at {path[i + 1]}, not {runs[i].b}");
            }
            foreach (SpriteRenderer sr in prefab.GetComponentsInChildren<SpriteRenderer>())
                Assert.AreEqual(SortingBands.DecorFloor, sr.sortingOrder, "it lies on the grass, under everything standing");

            Site site = InteriorFixturePlacement.Sites.Single(s => s.Name == HomeStoragePlacement.ShoreLeadName);
            Assert.IsFalse(site.IsInterior);
            Assert.IsNull(site.InteractId, "a picture claims no interact id");
            Assert.AreEqual(path[path.Count - 1], site.At);
        }
    }
}
