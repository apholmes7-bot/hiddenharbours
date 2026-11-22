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
