using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The chest freezer's art: how full its bed is drawn, where its fish lie (inside the chest's mouth, never
    /// over its outline), the lid's frames, the baked sheet, and the prefab and Def that wire it.
    /// </summary>
    public sealed class ChestFreezerArtTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Props/Kitchen/GinnyFreezer.prefab";
        private const string DefPath = "Assets/_Project/Data/Stowage/GinnyCatchFreezer.asset";
        private const string CamperPrefabPath = "Assets/_Project/Prefabs/Props/Kitchen/CamperFreezer.prefab";
        private const string CamperDefPath = "Assets/_Project/Data/Stowage/CamperCatchFreezer.asset";

        private static readonly MethodInfo s_presenterAwake =
            typeof(FreezerContentsPresenter).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo s_presenterLate =
            typeof(FreezerContentsPresenter).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<Object> _made = new List<Object>();
        private GameConfig _prevConfig;
        private ISaveService _prevSave;
        private Transform _prevPlayer;

        [SetUp]
        public void SetUp()
        {
            _prevConfig = GameServices.Config;
            _prevSave = GameServices.Save;
            _prevPlayer = GameServices.PlayerTransform;
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _made.Add(config);
            GameServices.Config = config;
            GameServices.Save = null;   // no save to guard: the freezer takes writes as they come
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Config = _prevConfig;
            GameServices.Save = _prevSave;
            GameServices.PlayerTransform = _prevPlayer;
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ================================================================ the rung

        [Test]
        public void TheBed_IsEmptyWithNothing_FullFromItsShare_SomeBetween()
        {
            Assert.AreEqual(FreezerFillRung.Empty, FreezerLayout.Rung(0, 20, 0.6f));
            Assert.AreEqual(FreezerFillRung.Some, FreezerLayout.Rung(1, 20, 0.6f));
            Assert.AreEqual(FreezerFillRung.Some, FreezerLayout.Rung(11, 20, 0.6f));
            Assert.AreEqual(FreezerFillRung.Full, FreezerLayout.Rung(12, 20, 0.6f), "60% of 20 is full");
            Assert.AreEqual(FreezerFillRung.Full, FreezerLayout.Rung(20, 20, 0.6f));
            Assert.AreEqual(FreezerFillRung.Some, FreezerLayout.Rung(12, 40, 0.6f), "a bigger freezer fills later");
            Assert.AreEqual(FreezerFillRung.Full, FreezerLayout.Rung(3, 0, 0.6f), "no capacity but something in it");
            Assert.AreEqual(FreezerFillRung.Empty, FreezerLayout.Rung(-1, 20, 0.6f));
        }

        // ================================================================ the lay

        private static readonly Rect Opening = new Rect(-0.6f, 0.45f, 1.2f, 0.42f);

        [Test]
        public void TheFish_LieNewestFirst_RowByRowFromTheBack_InsideTheOpening()
        {
            var lengths = new List<float> { 0.3f, 0.4f, 0.5f, 0.35f, 0.45f, 0.3f, 0.3f, 0.3f };
            var into = new FreezerLayout.Placement[lengths.Count];
            int laid = FreezerLayout.Pack(lengths, lengths.Count, Opening, 0.14f, 1f, 0.3f, -0.02f, into);
            Assert.AreEqual(lengths.Count, laid, "eight small fish fit");
            Assert.AreEqual(lengths.Count - 1, into[0].Item, "the newest is laid first");
            Assert.AreEqual(0, into[0].Row, "the first row is at the back");
            int lastRow = 0;
            for (int k = 0; k < laid; k++)
            {
                FreezerLayout.Placement p = into[k];
                Assert.GreaterOrEqual(p.Row, lastRow, "rows run back to front");
                lastRow = p.Row;
                Assert.GreaterOrEqual(p.Centre.x - p.Size.x * 0.5f, Opening.xMin - 1e-4f, $"fish {k} runs past the left end");
                Assert.LessOrEqual(p.Centre.x + p.Size.x * 0.5f, Opening.xMax + 1e-4f, $"fish {k} runs past the right end");
                Assert.LessOrEqual(p.Centre.y + p.Size.y * 0.5f, Opening.yMax + 1e-4f, $"fish {k} climbs over the back rim");
                Assert.GreaterOrEqual(p.Centre.y, Opening.yMin - 1e-4f, $"fish {k} lies below the front rim");
                Assert.AreEqual(1f, p.Scale, 1e-5f, "a fish that fits is drawn at its own size");
            }
            Assert.Greater(lastRow, 0, "eight fish need more than one row");
        }

        [Test]
        public void AFishLongerThanTheChest_IsShrunkToFit_AndFishThatDoNotFit_AreNotLaid()
        {
            var big = new List<float> { 2.0f };
            var into = new FreezerLayout.Placement[1];
            Assert.AreEqual(1, FreezerLayout.Pack(big, 1, Opening, 0.14f, 1f, 0.3f, 0f, into));
            Assert.AreEqual(Opening.width, into[0].Size.x, 1e-5f);
            Assert.AreEqual(Opening.width / 2f, into[0].Scale, 1e-5f);

            var many = new List<float>();
            for (int i = 0; i < 40; i++) many.Add(0.5f);
            var room = new FreezerLayout.Placement[40];
            int laid = FreezerLayout.Pack(many, many.Count, Opening, 0.14f, 1f, 0.3f, 0f, room);
            int rows = FreezerLayout.RowsIn(Opening, 0.14f);
            Assert.AreEqual(3, rows);
            Assert.AreEqual(rows * 2, laid, "two half-metre fish a row, three rows: the rest lie under them");
            Assert.AreEqual(0, FreezerLayout.Pack(many, 0, Opening, 0.14f, 1f, 0.3f, 0f, room));
            Assert.AreEqual(0, FreezerLayout.Pack(many, 5, new Rect(0, 0, 0, 0), 0.14f, 1f, 0.3f, 0f, room));
        }

        // ================================================================ the lid and the sheet

        [Test]
        public void TheLid_PlaysItsFrames_ThenTheRungsOpenRow()
        {
            Assert.AreEqual(ChestFreezerSheet.Closed, ChestFreezerArt.RowFor(0, FreezerFillRung.Full), "shut shows no bed");
            Assert.AreEqual(ChestFreezerSheet.LidLow, ChestFreezerArt.RowFor(1, FreezerFillRung.Full));
            Assert.AreEqual(ChestFreezerSheet.LidHigh, ChestFreezerArt.RowFor(2, FreezerFillRung.Empty));
            Assert.AreEqual(ChestFreezerSheet.OpenEmpty, ChestFreezerArt.RowFor(3, FreezerFillRung.Empty));
            Assert.AreEqual(ChestFreezerSheet.OpenSome, ChestFreezerArt.RowFor(3, FreezerFillRung.Some));
            Assert.AreEqual(ChestFreezerSheet.OpenFull, ChestFreezerArt.RowFor(9, FreezerFillRung.Full));
        }

        [Test]
        public void EveryRow_IsDrawnWithItsOwnOptions_AndEveryCellIsBakedAndNamed()
        {
            var seen = new HashSet<string>();
            for (int r = 0; r < ChestFreezerSheet.Rows; r++)
                Assert.IsTrue(seen.Add(ChestFreezerSheet.OptsFor(r)), $"row {r} draws the same as another row");
            foreach ((string _, string stem) in ChestFreezerSheet.Pieces)
                for (int r = 0; r < ChestFreezerSheet.Rows; r++)
                for (int c = 0; c < ChestFreezerSheet.Facings; c++)
                {
                    Sprite s = ChestFreezerSheet.SpriteAt(stem, r, c);
                    Assert.IsNotNull(s, $"{stem} row {r} cell {c} is missing");
                    Assert.IsTrue(ChestFreezerSheet.TryParse(s.name, stem, out int pr, out int pc) && pr == r && pc == c, s.name);
                }
        }

        // ================================================================ the Def and the prefab

        [Test]
        public void GinnysFreezer_IsData_AndThePrefabWiresItsArt()
        {
            var def = AssetDatabase.LoadAssetAtPath<FreezerDef>(DefPath);
            Assert.IsNotNull(def, DefPath);
            Assert.AreEqual("freezer.ginny_freezer", def.Id);
            Assert.AreEqual(20, def.CapacityUnits, "Ginny's freezer still holds 20");
            Assert.IsTrue(string.IsNullOrEmpty(def.DeedHomeId), "Ginny's freezer is open with no deed");
            Assert.AreEqual(ChestFreezerSheet.Stem, def.SheetStem);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            var freezer = prefab.GetComponent<GinnyFreezer>();
            Assert.IsNotNull(freezer);
            Assert.AreSame(def, freezer.Def);
            Assert.AreEqual(20, freezer.CapacityUnits);
            var art = prefab.GetComponent<ChestFreezerArt>();
            Assert.IsNotNull(art);
            Assert.AreSame(def, art.Def);
            Assert.AreEqual(ChestFreezerSheet.FrontCell, art.Cell);
            Assert.IsNotNull(prefab.GetComponent<FreezerContentsPresenter>());
            Assert.IsNull(prefab.GetComponent<SpriteRenderer>(), "the greybox square is gone");
            Transform far = prefab.transform.Find("Art"), near = prefab.transform.Find("ArtNear");
            Assert.IsNotNull(far); Assert.IsNotNull(near);
            Assert.AreSame(ChestFreezerSheet.SpriteAt(def.SheetStem, ChestFreezerSheet.Closed, ChestFreezerSheet.FrontCell),
                           far.GetComponent<SpriteRenderer>().sprite, "it stands shut");
            Assert.IsFalse(near.GetComponent<SpriteRenderer>().enabled, "the near layer hides while it is shut");
        }

        [Test]
        public void TheCampersFreezer_IsData_TheBiggerChest_AndItsPrefabWiresItsArt()
        {
            var def = AssetDatabase.LoadAssetAtPath<FreezerDef>(CamperDefPath);
            var ginny = AssetDatabase.LoadAssetAtPath<FreezerDef>(DefPath);
            Assert.IsNotNull(def, CamperDefPath);
            Assert.AreEqual("freezer.camper_freezer", def.Id);
            Assert.AreEqual(40, def.CapacityUnits, "the camper's freezer still holds 40");
            Assert.AreEqual("home.ginny_lot_camper", def.DeedHomeId, "it still opens with the camper's deed");
            Assert.AreEqual(ChestFreezerSheet.LargeStem, def.SheetStem);
            Assert.Greater(def.ContentsOpeningMetres.width, ginny.ContentsOpeningMetres.width, "its mouth is wider than Ginny's");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CamperPrefabPath);
            Assert.IsNotNull(prefab, CamperPrefabPath);
            var freezer = prefab.GetComponent<CamperFreezer>();
            Assert.IsNotNull(freezer);
            Assert.AreSame(def, freezer.Def);
            Assert.IsNull(prefab.GetComponent<GinnyFreezer>(), "one freezer component, the camper's");
            var art = prefab.GetComponent<ChestFreezerArt>();
            Assert.IsNotNull(art);
            Assert.AreSame(def, art.Def);
            Assert.AreEqual(ChestFreezerSheet.FrontCell, art.Cell);
            Assert.IsNotNull(prefab.GetComponent<FreezerContentsPresenter>());
            Transform far = prefab.transform.Find("Art"), near = prefab.transform.Find("ArtNear");
            Assert.IsNotNull(far); Assert.IsNotNull(near);
            Sprite shut = ChestFreezerSheet.SpriteAt(def.SheetStem, ChestFreezerSheet.Closed, ChestFreezerSheet.FrontCell);
            Assert.AreSame(shut, far.GetComponent<SpriteRenderer>().sprite, "it stands shut");
            Assert.IsFalse(near.GetComponent<SpriteRenderer>().enabled, "the near layer hides while it is shut");
            Sprite ginnyShut = ChestFreezerSheet.SpriteAt(ginny.SheetStem, ChestFreezerSheet.Closed, ChestFreezerSheet.FrontCell);
            Assert.Greater(shut.rect.width, ginnyShut.rect.width, "and it is drawn bigger than Ginny's");
            Assert.AreEqual(ginnyShut.pixelsPerUnit, shut.pixelsPerUnit, 1e-4f, "at the same scale");
        }

        [Test]
        public void TheLid_OpensOnItsClock_AndTheNearLayerShowsOnlyWhileOpen()
        {
            ChestFreezerArt art = Chest(out GinnyFreezer _, out _, out SpriteRenderer far, out SpriteRenderer near);
            float per = art.Def.LidFrameSeconds;
            art.Step(true, per * 0.5f);
            Assert.AreEqual(ChestFreezerSheet.Closed, art.ShownRow, "half a frame in, still shut");
            art.Step(true, per * 0.6f);
            Assert.AreEqual(ChestFreezerSheet.LidLow, art.ShownRow);
            Assert.IsFalse(art.IsOpen);
            Assert.IsFalse(near.enabled, "the near layer showed before the lid was up");
            art.Step(true, per * 5f);
            Assert.IsTrue(art.IsOpen);
            Assert.AreEqual(ChestFreezerSheet.OpenEmpty, art.ShownRow, "an empty freezer opens on the empty bed");
            Assert.IsTrue(near.enabled);
            Assert.AreSame(ChestFreezerSheet.SpriteAt(art.Def.SheetStem, ChestFreezerSheet.OpenEmpty, art.Cell), far.sprite);
            art.Step(false, per * 5f);
            Assert.AreEqual(ChestFreezerSheet.Closed, art.ShownRow);
            Assert.IsFalse(near.enabled);
        }

        [Test]
        public void InTheChest_TheFishShowOnlyWhileTheLidIsUp_InsideItsMouth_BetweenTheChestAndItsFront()
        {
            ChestFreezerArt art = Chest(out GinnyFreezer freezer, out FreezerContentsPresenter presenter,
                                        out SpriteRenderer far, out SpriteRenderer near);
            CatchItemLibrary lib = FishArtLibrary.ResolveCatchArt(null, FishArtLibrary.Shared);
            Assert.IsNotNull(lib, "harness: no shared catch art");
            var items = new List<CatchItem>();
            items.Add(new CatchItem("fish.atlantic_cod", "Cod", FishCategory.InshoreGroundfish, 12f, 10, 0.1f));
            for (int i = 0; i < 7; i++)
                items.Add(new CatchItem("fish.mackerel", "Mackerel", FishCategory.Pelagic, 0.75f, 10, 0.1f));
            freezer.ReplaceAll(items);

            s_presenterLate.Invoke(presenter, null);
            Assert.AreEqual(0, presenter.ShownCount, "fish drawn through a shut lid");

            art.Step(true, 10f);
            Assert.AreEqual(FreezerFillRung.Some, art.Rung, "8 of 20 is some");
            Assert.AreEqual(ChestFreezerSheet.OpenSome, art.ShownRow);
            s_presenterLate.Invoke(presenter, null);
            Assert.Greater(presenter.ShownCount, 0, "the open chest shows no fish");
            Rect mouth = art.Def.ContentsOpeningMetres;
            for (int i = 0; i < presenter.ShownCount; i++)
            {
                SpriteRenderer sr = presenter.Slots[i];
                Assert.IsTrue(sr.enabled);
                Vector2 at = sr.transform.position - art.transform.position;
                float half = presenter.LaidLengthMetres(i) * 0.5f;
                Assert.Greater(half, 0f, $"fish {i} was laid with no length");
                StringAssert.IsMatch("_d[26]_", sr.sprite.name, $"fish {i} does not lie side-on along the chest");
                Assert.GreaterOrEqual(at.x - half, mouth.xMin - 1e-4f, $"fish {i} hangs over the left wall");
                Assert.LessOrEqual(at.x + half, mouth.xMax + 1e-4f, $"fish {i} hangs over the right wall");
                Assert.LessOrEqual(at.y, mouth.yMax + 1e-4f, $"fish {i} lies over the back rim");
                Assert.GreaterOrEqual(at.y, mouth.yMin - 0.1f, $"fish {i} lies below the chest's mouth");
                Assert.AreEqual(art.ContentsOrder, sr.sortingOrder);
                Assert.Greater(sr.sortingOrder, far.sortingOrder, "a fish under the chest");
                Assert.Less(sr.sortingOrder, near.sortingOrder, "a fish over the chest's front rim");
                Assert.AreEqual(far.sortingLayerID, sr.sortingLayerID);
            }

            art.Step(false, 10f);
            s_presenterLate.Invoke(presenter, null);
            Assert.AreEqual(0, presenter.ShownCount, "the lid shut but the fish still show");
        }

        [Test]
        public void AFullFreezer_ShowsTheFullBed_AndNoMoreFishThanItsMouthHolds()
        {
            ChestFreezerArt art = Chest(out GinnyFreezer freezer, out FreezerContentsPresenter presenter, out _, out _);
            var items = new List<CatchItem> { new CatchItem("fish.atlantic_cod", "Cod", FishCategory.InshoreGroundfish, 12f, 10, 0.1f) };
            for (int i = 0; i < 19; i++)
                items.Add(new CatchItem(i % 2 == 0 ? "fish.mackerel" : "fish.atlantic_cod", "Fish",
                                        FishCategory.Pelagic, i % 2 == 0 ? 0.75f : 2.5f, 10, 0.1f));
            freezer.ReplaceAll(items);
            Assert.AreEqual(20, freezer.UsedUnits);
            art.Step(true, 10f);
            Assert.AreEqual(FreezerFillRung.Full, art.Rung);
            Assert.AreEqual(ChestFreezerSheet.OpenFull, art.ShownRow);
            s_presenterLate.Invoke(presenter, null);
            Assert.Greater(presenter.ShownCount, 0);
            Assert.Less(presenter.ShownCount, 20, "twenty fish cannot all lie in the mouth: the bed stands for the rest");
            Rect mouth = art.Def.ContentsOpeningMetres;
            for (int i = 0; i < presenter.ShownCount; i++)
            {
                Vector2 at = presenter.Slots[i].transform.position - art.transform.position;
                Assert.IsTrue(at.x >= mouth.xMin && at.x <= mouth.xMax && at.y <= mouth.yMax + 1e-4f,
                              $"fish {i} at {at} is outside the chest's mouth {mouth}");
            }
        }

        // ================================================================ harness

        private ChestFreezerArt Chest(out GinnyFreezer freezer, out FreezerContentsPresenter presenter,
                                      out SpriteRenderer far, out SpriteRenderer near)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            var go = Object.Instantiate(prefab);
            _made.Add(go);
            go.transform.position = new Vector3(3f, 2f, 0f);
            freezer = go.GetComponent<GinnyFreezer>();
            presenter = go.GetComponent<FreezerContentsPresenter>();
            var art = go.GetComponent<ChestFreezerArt>();
            far = go.transform.Find("Art").GetComponent<SpriteRenderer>();
            near = go.transform.Find("ArtNear").GetComponent<SpriteRenderer>();
            art.Configure(art.Def, far, near, art.Cell);   // EditMode: Awake does not run
            typeof(ChestFreezerArt).GetMethod("Sort", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(art, null);
            s_presenterAwake.Invoke(presenter, null);
            Assert.IsTrue(freezer.IsBound(), "harness: the freezer did not bind");
            return art;
        }
    }
}
