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
    /// <b>Fish at the stove and in the freezer</b> (fish art pass 2, plan 4a and 4b): a station draws the
    /// fish its job started from at its food anchor, browning along the one cooked ramp on a stove; Ginny's
    /// freezer lays its frozen catch on its slot grid, at each fish's own rung, while he stands at it.
    /// </summary>
    public sealed class FishAtStationTests
    {
        private const string KitchenFolder = "Assets/_Project/Prefabs/Props/Kitchen/";
        private const string LibraryPath = "Assets/_Project/Data/Art/CatchItemLibrary.asset";

        private static readonly MethodInfo s_stationAwake =
            typeof(CookingStationFoodPresenter).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo s_stationLate =
            typeof(CookingStationFoodPresenter).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo s_freezerAwake =
            typeof(FreezerContentsPresenter).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo s_freezerLate =
            typeof(FreezerContentsPresenter).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<Object> _made = new List<Object>();
        private GameConfig _prevConfig;
        private IGameClock _prevClock;
        private ISaveService _prevSave;
        private RecipeRunner _prevCooking;
        private Transform _prevPlayer;
        private StampClock _clock;
        private GameConfig _config;

        private sealed class StampClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private sealed class OneHand : ICatchHands
        {
            public CatchItem? Held;
            public bool TryPutInHand(in CatchItem item) { if (Held.HasValue) return false; Held = item; return true; }
            public bool TryTakeFromHand(out CatchItem item) { item = Held ?? default; if (!Held.HasValue) return false; Held = null; return true; }
            public bool TryPeekHand(out CatchItem item) { item = Held ?? default; return Held.HasValue; }
        }

        [SetUp]
        public void SetUp()
        {
            _prevConfig = GameServices.Config;
            _prevClock = GameServices.Clock;
            _prevSave = GameServices.Save;
            _prevCooking = GameServices.Cooking;
            _prevPlayer = GameServices.PlayerTransform;
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _made.Add(_config);
            GameServices.Config = _config;
            _clock = new StampClock();
            GameServices.Clock = _clock;
            GameServices.Save = new TraitFixtures.MemorySave();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Config = _prevConfig;
            GameServices.Clock = _prevClock;
            GameServices.Save = _prevSave;
            GameServices.Cooking = _prevCooking;
            GameServices.PlayerTransform = _prevPlayer;
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ================================================================ which fish, what colour

        [Test]
        public void TheFishOnAStation_IsTheSpeciesItsRecipeChainStartedFrom()
        {
            RecipeBook book = RecipeBook.FromResources();
            Assert.AreEqual("fish.atlantic_cod", CookingStationFoodPresenter.SpeciesOf(Recipe(book, "food.cod_fillet"), book));
            Assert.AreEqual("fish.atlantic_cod", CookingStationFoodPresenter.SpeciesOf(Recipe(book, "food.pan_fried_cod"), book),
                            "a fillet on the stove is followed back to the cod");
            Assert.AreEqual("fish.mackerel", CookingStationFoodPresenter.SpeciesOf(Recipe(book, "food.grilled_mackerel"), book));
            Assert.IsNull(CookingStationFoodPresenter.SpeciesOf(Recipe(book, "food.white_fish_fillet"), book),
                          "a whole category names no species");
            Assert.IsNull(CookingStationFoodPresenter.SpeciesOf(Recipe(book, "food.hot_beans"), book));
            Assert.IsNull(CookingStationFoodPresenter.SpeciesOf(null, book));
        }

        [Test]
        public void TheCookedRamp_RunsFromItsFirstStopToItsLast_AsTheJobRuns()
        {
            Color[] ramp = { Color.white, Color.red, Color.black };
            Assert.AreEqual(Color.white, CookingStationFoodPresenter.SampleRamp(ramp, 0f));
            Assert.AreEqual(Color.red, CookingStationFoodPresenter.SampleRamp(ramp, 0.5f));
            Assert.AreEqual(Color.black, CookingStationFoodPresenter.SampleRamp(ramp, 1f));
            Assert.AreEqual(Color.black, CookingStationFoodPresenter.SampleRamp(ramp, 7f));
            Assert.AreEqual(Color.Lerp(Color.white, Color.red, 0.5f), CookingStationFoodPresenter.SampleRamp(ramp, 0.25f));
            Assert.AreEqual(Color.white, CookingStationFoodPresenter.SampleRamp(null, 0.5f), "no ramp, no tint");

            Assert.AreEqual(0f, CookingStationFoodPresenter.Progress(100d, 200d, 100f), 1e-6f);
            Assert.AreEqual(0.5f, CookingStationFoodPresenter.Progress(150d, 200d, 100f), 1e-6f);
            Assert.AreEqual(1f, CookingStationFoodPresenter.Progress(250d, 200d, 100f), 1e-6f);
            Assert.AreEqual(1f, CookingStationFoodPresenter.Progress(0d, 200d, 0f), "a job with no length is done");

            Color[] shipped = ScriptableObject.CreateInstance<GameConfig>().CookedFishRamp;
            Assert.GreaterOrEqual(shipped.Length, 2, "the shipped ramp has a raw and a cooked stop");
            Assert.AreEqual(Color.white, shipped[0], "the raw stop draws the fish as drawn");
        }

        // ================================================================ the shipped data and prefabs

        [Test]
        public void TheStoveBrowns_ThePrepBoardShowsRaw_TheMicrowaveShowsNothing_AndThePrefabsDrawIt()
        {
            foreach ((string file, bool shows, bool browns) in new[]
                     { ("Stove", true, true), ("PrepCounter", true, false), ("Microwave", false, false) })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitchenFolder + file + ".prefab");
                Assert.IsNotNull(prefab, file);
                CookingStationDef def = prefab.GetComponent<CookingStationFixture>().Def;
                Assert.AreEqual(shows, def.ShowsFood, file + ": ShowsFood");
                Assert.AreEqual(browns, def.BrownsAsItCooks, file + ": BrownsAsItCooks");
                var presenter = prefab.GetComponent<CookingStationFoodPresenter>();
                if (!shows) continue;
                Assert.IsNotNull(presenter, file + " shows food but has no presenter");
                Assert.Greater(def.FoodAnchorMeters.y, 0f, file + ": the pan sits on the station, not under it");
                var art = new SerializedObject(presenter).FindProperty("_art").objectReferenceValue as CatchItemLibrary;
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<CatchItemLibrary>(LibraryPath), art, file + ": art not wired");
            }
        }

        // ================================================================ a live station

        [Test]
        public void AStationWithACodOnIt_DrawsTheCod_BrownsItOnTheStove_AndClearsWhenDone()
        {
            var hands = new OneHand { Held = new CatchItem("fish.atlantic_cod", "Cod", FishCategory.InshoreGroundfish, 2f, 10, 0.1f) };
            GameServices.Cooking = new RecipeRunner(RecipeBook.FromResources, FoodBook.FromResources,
                                                    () => GameServices.Save, () => hands, () => _clock.TotalSeconds);

            CookingStationFoodPresenter prep = Station("PrepCounter", out CookingStationFixture prepFixture);
            Assert.AreEqual(CookRefusal.None, GameServices.Cooking.TryStart(prepFixture.Def, prepFixture.Id));
            Late(prep);
            Assert.IsTrue(prep.Showing, "the cod on the board is not drawn");
            Assert.AreEqual("cod", prep.ShownKind);
            Assert.AreEqual(Color.white, prep.Food.color, "the prep board does not brown");
            Assert.AreEqual(prepFixture.Def.FoodAnchorMeters, (Vector2)prep.Food.transform.localPosition);

            _clock.TotalSeconds += 100000d;
            Assert.IsTrue(GameServices.Cooking.TryFinish(prepFixture.Id, out _));
            Late(prep);
            Assert.IsFalse(prep.Showing, "the board is clear, yet the cod still draws");

            CookingStationFoodPresenter stove = Station("Stove", out CookingStationFixture stoveFixture);
            Assert.AreEqual(CookRefusal.None, GameServices.Cooking.TryStart(stoveFixture.Def, stoveFixture.Id), "the fillet did not go on the stove");
            Assert.IsTrue(GameServices.Cooking.IsCooking(stoveFixture.Id, out CookJob job));
            float length = RecipeRunner.CookSeconds(job.Recipe, stoveFixture.Def);
            Assert.Greater(length, 0f);
            Late(stove);
            Assert.IsTrue(stove.Showing);
            Assert.AreEqual("cod", stove.ShownKind, "the pan-fried cod is drawn as the cod it came from");
            Color[] ramp = _config.CookedFishRamp;
            Assert.AreEqual(ramp[0], stove.Food.color, "at the start the fish is raw");

            _clock.TotalSeconds = job.ReadyAt - length * 0.5f;
            Late(stove);
            Assert.AreEqual(CookingStationFoodPresenter.SampleRamp(ramp, 0.5f), stove.Food.color, "half way, half browned");
            _clock.TotalSeconds = job.ReadyAt;
            Late(stove);
            Assert.AreEqual(ramp[ramp.Length - 1], stove.Food.color, "done, fully browned");

            Assert.IsTrue(GameServices.Cooking.TryFinish(stoveFixture.Id, out _));
            Late(stove);
            Assert.IsFalse(stove.Showing);
        }

        // ================================================================ the freezer

        [Test]
        public void TheSlotGrid_RunsRowByRowFromTheBackLeft_AndTheNewestFishAreShown()
        {
            var origin = new Vector2(-1f, 0.5f);
            var pitch = new Vector2(0.25f, 0.2f);
            Assert.AreEqual(origin, FreezerContentsPresenter.SlotPoint(0, 4, origin, pitch));
            Assert.AreEqual(origin + new Vector2(0.75f, 0f), FreezerContentsPresenter.SlotPoint(3, 4, origin, pitch));
            Assert.AreEqual(origin + new Vector2(0f, -0.2f), FreezerContentsPresenter.SlotPoint(4, 4, origin, pitch));
            Assert.AreEqual(0, FreezerContentsPresenter.FirstShown(5, 8));
            Assert.AreEqual(4, FreezerContentsPresenter.FirstShown(12, 8));
            Assert.IsTrue(FreezerContentsPresenter.Near(Vector3.zero, new Vector3(1f, 0f, 5f), 1f), "height is not distance");
            Assert.IsFalse(FreezerContentsPresenter.Near(Vector3.zero, new Vector3(1.1f, 0f, 0f), 1f));
            Assert.IsFalse(FreezerContentsPresenter.Near(Vector3.zero, Vector3.zero, 0f), "range 0 never shows");
        }

        [Test]
        public void TheFreezer_LaysItsFish_AtTheirOwnRung_FrostTinted_OnlyWhileHeStandsAtIt()
        {
            var go = new GameObject("FishAtStationFreezer", typeof(SpriteRenderer));
            _made.Add(go);
            go.transform.localScale = new Vector3(1.2f, 1f, 1f);
            var freezer = go.AddComponent<GinnyFreezer>();
            GameServices.Save = null;   // no save to guard: the freezer takes writes as they come
            Assert.IsTrue(freezer.IsBound(), "harness: the freezer did not bind");
            freezer.ReplaceAll(new[]
            {
                new CatchItem("fish.atlantic_cod", "Cod", FishCategory.InshoreGroundfish, 12f, 10, 0.1f),
                new CatchItem("fish.mackerel", "Mackerel", FishCategory.Pelagic, 0.75f, 10, 0.1f),
            });
            var presenter = go.AddComponent<FreezerContentsPresenter>();
            s_freezerAwake.Invoke(presenter, null);
            CatchItemLibrary art = FishArtLibrary.ResolveCatchArt(null, FishArtLibrary.Shared);
            Assert.IsNotNull(art, "harness: no shared catch art");

            var player = new GameObject("FishAtStationPlayer");
            _made.Add(player);
            GameServices.PlayerTransform = player.transform;
            player.transform.position = go.transform.position + new Vector3(_config.FreezerFishShowRangeMeters * 2f, 0f, 0f);
            s_freezerLate.Invoke(presenter, null);
            Assert.AreEqual(0, presenter.ShownCount, "nobody at the freezer, yet its fish are drawn");

            player.transform.position = go.transform.position;
            s_freezerLate.Invoke(presenter, null);
            Assert.AreEqual(2, presenter.ShownCount);
            SpriteRenderer cod = presenter.Slots[0];
            Assert.IsTrue(cod.enabled);
            Assert.AreSame(art.SpriteFor(art.SizedKindFor("fish.atlantic_cod", 12f), 0), cod.sprite,
                           "the 12 kg cod is not drawn at its own rung");
            Assert.AreEqual(_config.FrozenFishTint, cod.color);
            Vector2 want = FreezerContentsPresenter.SlotPoint(0, _config.FreezerFishColumns,
                                                              _config.FreezerFishOriginMeters, _config.FreezerFishPitchMeters);
            Vector2 got = cod.transform.position - go.transform.position;
            Assert.Less((got - want).magnitude, 1e-4f, "the slot point is not true metres from the pivot");

            freezer.ReplaceAll(new CatchItem[0]);
            s_freezerLate.Invoke(presenter, null);
            Assert.AreEqual(0, presenter.ShownCount, "the freezer is empty, yet a fish is drawn");
            Assert.IsFalse(cod.enabled);
        }

        // ================================================================ harness

        private CookingStationFoodPresenter Station(string file, out CookingStationFixture fixture)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitchenFolder + file + ".prefab");
            GameObject go = Object.Instantiate(prefab);
            _made.Add(go);
            fixture = go.GetComponent<CookingStationFixture>();
            var presenter = go.GetComponent<CookingStationFoodPresenter>();
            Assert.IsNotNull(presenter, file + ": no food presenter");
            s_stationAwake.Invoke(presenter, null);
            return presenter;
        }

        private static void Late(CookingStationFoodPresenter p) => s_stationLate.Invoke(p, null);

        private static RecipeDef Recipe(RecipeBook book, string outputId)
        {
            foreach (RecipeDef r in book.All)
                if (r.OutputId == outputId) return r;
            Assert.Fail("harness: no recipe makes " + outputId);
            return null;
        }
    }
}
