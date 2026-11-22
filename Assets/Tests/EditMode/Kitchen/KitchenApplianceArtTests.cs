using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The fridge and the kitchen sink: their baked sheets are all there, and their prefabs wire the art and the
    /// Defs they stand for (the fridge its food store, the sink the hand-washing station), the way the chest
    /// freezer's prefab wires its FreezerDef.
    /// </summary>
    public sealed class KitchenApplianceArtTests
    {
        private const string FridgePrefab = "Assets/_Project/Prefabs/Props/Kitchen/Fridge.prefab";
        private const string SinkPrefab = "Assets/_Project/Prefabs/Props/Kitchen/KitchenSink.prefab";
        private const string FridgeDefPath = "Assets/_Project/Data/Resources/Kitchen/Storage/Fridge.asset";
        private const string SinkStationPath = "Assets/_Project/Data/Resources/WashStations/Sink.asset";

        [TestCase(ApplianceKind.Fridge)]
        [TestCase(ApplianceKind.KitchenSink)]
        public void EverySpriteOfTheSheetIsBaked(ApplianceKind kind)
        {
            ApplianceLayout l = ApplianceSheets.For(kind);
            Sprite[] sheet = ApplianceArt.SheetFor(l);
            Assert.AreEqual(l.Rows * ApplianceSheets.Facings, sheet.Length);
            for (int i = 0; i < sheet.Length; i++)
                Assert.IsNotNull(sheet[i], $"{l.Stem}: row {i / ApplianceSheets.Facings} cell {i % ApplianceSheets.Facings} is missing");
            Assert.AreEqual(32f, sheet[0].pixelsPerUnit, 1e-4f, "the locked PPU");
        }

        [Test]
        public void TheFridgePrefab_BindsTheFridgeStore_AndWiresItsArt()
        {
            var def = AssetDatabase.LoadAssetAtPath<KitchenStorageDef>(FridgeDefPath);
            Assert.IsNotNull(def, FridgeDefPath);
            Assert.AreEqual("kitchen_storage.fridge", def.Id);
            Assert.AreEqual(KitchenStorageKind.Fridge, def.Kind);

            GameObject prefab = Load(FridgePrefab);
            var store = prefab.GetComponent<KitchenStoreFixture>();
            Assert.IsNotNull(store, "the fridge names its food store");
            Assert.AreSame(def, store.Def);
            AssertArt(prefab, ApplianceKind.Fridge);
            Assert.IsNull(prefab.GetComponent<WashStationPoint>());
        }

        [Test]
        public void TheKitchenSinkPrefab_IsAHandWashingStation_AndWiresItsArt()
        {
            var station = AssetDatabase.LoadAssetAtPath<WashStationDef>(SinkStationPath);
            Assert.IsNotNull(station, SinkStationPath);
            Assert.AreEqual("washstation.sink", station.Id);

            GameObject prefab = Load(SinkPrefab);
            var wash = prefab.GetComponent<WashStationPoint>();
            Assert.IsNotNull(wash, "the sink washes his hands like the basin");
            Assert.AreEqual("washstation.sink", wash.StationId);
            Assert.IsFalse(string.IsNullOrEmpty(wash.Id));
            Assert.AreNotEqual("fixture.washstand", wash.Id, "never the washstand's id");
            AssertArt(prefab, ApplianceKind.KitchenSink);
            Assert.IsNull(prefab.GetComponent<KitchenStoreFixture>());
        }

        [TestCase(FridgePrefab)]
        [TestCase(SinkPrefab)]
        public void BothPrefabsAreSolid_AFootprintBoxWiredToTheArt(string path)
        {
            GameObject prefab = Load(path);
            var art = prefab.GetComponent<ApplianceArt>();
            var box = prefab.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(box, "he cannot walk through it");
            Assert.IsFalse(box.isTrigger);
            Assert.AreSame(box, art.Footprint, "the art fits the box to the wall it stands on");
            Rect r = ApplianceFacing.FootprintBox(ApplianceSheets.For(art.Kind), 0);
            Assert.AreEqual(r.size.x, box.size.x, 1e-3f); Assert.AreEqual(r.size.y, box.size.y, 1e-3f);
        }

        [TestCase(ApplianceKind.Fridge)]
        [TestCase(ApplianceKind.KitchenSink)]
        public void TheFootprintBoxTurnsWithTheWall_AndLeavesTheFrontOpen(ApplianceKind kind)
        {
            ApplianceLayout l = ApplianceSheets.For(kind);
            Rect n = ApplianceFacing.FootprintBox(l, 0), e = ApplianceFacing.FootprintBox(l, 2);
            Assert.AreEqual(l.Foot.x, n.width, 1e-4f, "on a north/south wall it is as wide as the piece");
            Assert.AreEqual(l.Foot.y, e.width, 1e-4f, "a quarter turn puts its depth across");
            const float reach = 1.2f;   // the shipped WashStationPoint reach
            foreach (int cell in ApplianceFacing.WallOffsets)
            {
                Rect r = ApplianceFacing.FootprintBox(l, cell);
                Vector2 stand = ApplianceFacing.ModelToWorldOffset(cell, ApplianceFacing.EntryProp(l, 0.25f));
                Assert.IsFalse(r.Contains(stand), $"cell {cell}: the stand spot in front is outside the box");
                Assert.Less(stand.magnitude, reach, $"cell {cell}: and within reach of the piece");
            }
        }

        [Test]
        public void TheKitchenSinkSaysSink_NeverBasin()
        {
            var so = new SerializedObject(Load(SinkPrefab).GetComponent<WashStationPoint>());
            foreach (string field in new[] { "_washLabel", "_beginLine", "_doneLine" })
            {
                string s = so.FindProperty(field).stringValue;
                Assert.IsFalse(string.IsNullOrEmpty(s), field);
                StringAssert.DoesNotContain("basin", s.ToLowerInvariant(), field);
            }
            StringAssert.Contains("sink", so.FindProperty("_washLabel").stringValue);
        }

        [Test]
        public void ADoorIsOnlyAskedOpen_NeverStored()
        {
            var go = new GameObject("fridge-test");
            try
            {
                var far = new GameObject("Art").AddComponent<SpriteRenderer>();
                far.transform.SetParent(go.transform, false);
                var near = new GameObject("ArtNear").AddComponent<SpriteRenderer>();
                near.transform.SetParent(go.transform, false);
                var art = go.AddComponent<ApplianceArt>();
                art.Configure(ApplianceKind.Fridge, far, near);
                Assert.IsFalse(art.DoorOpen);
                art.SetDoorOpen(true);
                Assert.IsTrue(art.DoorOpen);
                Assert.IsTrue(art.enabled, "asked open, it wakes to swing the door");
                art.SetDoorOpen(false);
                Assert.IsFalse(art.DoorOpen);
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            return prefab;
        }

        private static void AssertArt(GameObject prefab, ApplianceKind kind)
        {
            var art = prefab.GetComponent<ApplianceArt>();
            Assert.IsNotNull(art);
            Assert.AreEqual(kind, art.Kind);
            Assert.IsNull(prefab.GetComponent<SpriteRenderer>(), "no greybox square on the root");
            Transform far = prefab.transform.Find("Art"), near = prefab.transform.Find("ArtNear");
            Assert.IsNotNull(far); Assert.IsNotNull(near);
            Assert.AreSame(ApplianceArt.SheetFor(ApplianceSheets.For(kind))[0], far.GetComponent<SpriteRenderer>().sprite,
                           "it shows its idle, unturned picture in the editor");
            Assert.IsFalse(near.GetComponent<SpriteRenderer>().enabled, "no near layer: nothing stands inside it");
        }
    }
}
