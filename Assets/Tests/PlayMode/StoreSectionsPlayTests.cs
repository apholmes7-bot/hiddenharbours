using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>He walks up to the food section, the chooser opens, and he buys the second food</b> (store sections,
    /// owner ask 2026-11-25), joined up in a running scene with the store the game builds: the shipped St
    /// Peters shelves built by <see cref="IslandShelvesHost"/> from the clock and the water, the shipped shelf
    /// slots and sections hung by <see cref="SoldOutSigns"/>, and the counter's supply seam standing for
    /// <c>seller.leblancs</c> as the region builder stands it up. The section registers itself and its foods
    /// do not; walking toward it, the one resolver starts offering it in reach; the press opens the chooser
    /// with the bread, oatcakes and beans, each with its price and how many are left; picking the second row
    /// takes exactly the oatcakes' price from his purse, puts one packet in the paper bag in his hands, takes
    /// one off the oatcakes' shelf only, and records the purchase in the game. Headless-safe: nothing renders.
    /// </summary>
    public class StoreSectionsPlayTests
    {
        private const string SlotsId = "shelfslots.st_peters_general";
        private const string Seller = "seller.leblancs";
        private const string SuppliesFolder = "Assets/_Project/Data/Resources/Catalog/Supplies";
        private const float Arc = 120f;

        private sealed class DrivenClock : IGameClock
        {
            public double Seconds;
            public float DayLength = GameConfig.DefaultSecondsPerDay;
            public double TotalSeconds => Seconds;
            public GameTime Now => new GameTime(Seconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => (int)(Seconds / DayLength);
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => (float)(Seconds / DayLength % 1.0) * 24f;
            public float DayFraction => (float)(Seconds / DayLength % 1.0);
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        // The bar is dry all day: the supply run can always come.
        private sealed class LowWater : IEnvironmentService
        {
            public int WorldSeed => 11;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Vector2.zero, Vector2.zero, 0f, SeaState.Calm, 1f, 0f);
            public float TideHeightAt(double totalSeconds) => -50f;
            public float WaterLevelAt(double totalSeconds) => -50f;
        }

        private sealed class FakeSave : ISaveService
        {
            public FakeSave(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private sealed class TestWallet : MonoBehaviour, IWallet
        {
            public int Money { get; private set; }
            public void Seed(int money) => Money = money;
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        // The paper bag in his hands, as the shelf sees it through Core.
        private sealed class BagInHand : IShoppingBagHands
        {
            public readonly List<string> Bagged = new List<string>();
            public bool HoldsBag => Bagged.Count > 0;
            public bool BagIsSetDown => false;
            public ShoppingBagRefusal CanBag(ShoppingBagDef bag) => ShoppingBagRefusal.None;
            public ShoppingBagRefusal TryBag(ShoppingBagDef bag, string supplyId)
            {
                Bagged.Add(supplyId);
                return ShoppingBagRefusal.None;
            }
            public bool TryPutAway(string storageId) { Bagged.Clear(); return true; }
        }

        private readonly List<Object> _spawned = new List<Object>();
        private GameConfig _cfg;
        private DrivenClock _clock;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            InteractChooser.Reset();
            InteractVerb.Reset();
            InteractionGate.Reset();
            _cfg = ScriptableObject.CreateInstance<GameConfig>();
            _clock = new DrivenClock { DayLength = _cfg.SecondsPerDay };
            GameServices.Config = _cfg;
            GameServices.Clock = _clock;
            GameServices.Environment = new LowWater();
        }

        [TearDown]
        public void TearDown()
        {
            InteractChooser.Reset();
            InteractVerb.Reset();
            InteractionGate.Reset();
            GameServices.Reset();
            EventBus.Publish(new GameLoaded());   // the host lets go of the stores built for this test
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            Interactables.Clear();
            if (_cfg != null) Object.Destroy(_cfg);
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, "field '" + field + "' not found on " + target.GetType().Name);
            f.SetValue(target, value);
        }

        private void EnsureHost()
        {
            IslandShelvesHost host = Object.FindFirstObjectByType<IslandShelvesHost>();
            if (host == null)
            {
                Spawn("[IslandShelvesHost test]").AddComponent<IslandShelvesHost>();
                return;
            }
            // The fixture's service reset drops every listener: the running host listens again.
            host.enabled = false;
            host.enabled = true;
        }

        private static string Offered(Vector2 at)
        {
            var actor = new InteractActor(at, Vector2.zero, InteractContext.OnFoot);
            return InteractResolver.TryResolveNow(actor, Arc, out IInteractable best) ? best.VerbLabel : null;
        }

        private static SupplyDef Supply(string file)
        {
            var s = AssetDatabase.LoadAssetAtPath<SupplyDef>(SuppliesFolder + "/" + file + ".asset");
            Assert.IsNotNull(s, file);
            return s;
        }

        [UnityTest]
        public IEnumerator HeWalksToTheFoodSection_TheChooserOpens_AndHeBuysTheSecondFood()
        {
            ShopShelfSlotsDef slots = Resources.LoadAll<ShopShelfSlotsDef>(ShopShelfSlotsDef.ResourcesFolder)
                                               .Single(d => d.Id == SlotsId);
            ShopShelfSection food = slots.Sections.Single(s => s.Id == "section.food");
            string[] lines = food.SlotIds.Select(id => slots.Slots.Single(s => s.Id == id).LineId).ToArray();
            SupplyDef[] foods = { Supply("Bread"), Supply("Oatcakes"), Supply("TinnedBeans") };
            double day = _cfg.SecondsPerDay;

            // The game, the purse, the bag in his hands and the counter's supply seam.
            SaveData save = SaveMigration.NewGame();
            GameServices.Save = new FakeSave(save);
            TestWallet wallet = Spawn("Wallet").AddComponent<TestWallet>();
            wallet.Seed(100);
            GameServices.Wallet = wallet;
            var bag = new BagInHand();
            GameServices.BagHands = bag;
            GameObject counter = Spawn("GeneralStoreCounter");
            counter.SetActive(false);
            SupplyShop supplies = counter.AddComponent<SupplyShop>();
            SetPrivate(supplies, "_supply", foods[0]);
            SetPrivate(supplies, "_walletProvider", wallet.gameObject);
            SetPrivate(supplies, "_sellerId", Seller);
            counter.SetActive(true);

            EnsureHost();
            _clock.Seconds = 10.0 * day;
            EventBus.Publish(new GameLoaded());
            yield return null;
            yield return null;
            Assert.IsTrue(IslandShelvesDirectory.TryGet(slots.ShopId, out IIslandShelves shelves), "the game built the store");

            // A morning with all three foods on the shelf, two packets of oatcakes at least.
            double now = -1.0;
            for (int d = 10; d < 60 && now < 0.0; d++)
            {
                double t = (d + 10.0 / 24.0) * day;
                if (shelves.CountAt(lines[0], t) >= 1 && shelves.CountAt(lines[1], t) >= 2 && shelves.CountAt(lines[2], t) >= 1)
                    now = t;
            }
            Assert.That(now, Is.GreaterThan(0.0), "some morning has all three foods");
            _clock.Seconds = now;
            int[] before = lines.Select(l => shelves.CountAt(l, now)).ToArray();

            // The store's shelves, hung as the director hangs them, the player inside downstairs.
            GameObject store = Spawn("generalStore");
            store.SetActive(false);
            SoldOutSigns signs = store.AddComponent<SoldOutSigns>();
            signs.SetInsideTest(() => true);
            signs.Configure(slots, null);
            store.SetActive(true);
            yield return null;
            signs.Refresh(now, true);

            ShelfSection section = signs.Sections.Single(s => s.Id == slots.Id + "." + food.Id);
            var registered = new List<IInteractable>(Interactables.Active);
            Assert.That(registered, Has.Member(section), "the section registers itself");
            foreach (ShelfGood g in section.Goods)
                Assert.That(registered, Has.No.Member(g), g.ListingId + " is sold only through the section");

            // He walks toward the section, a step a frame, until it is offered.
            Vector2 target = section.WorldPosition;
            Vector2 at = target + new Vector2(0f, -5f);
            Assert.That(Offered(at), Is.Not.EqualTo(food.VerbLabel), "not from across the room");
            string offered = null;
            for (int frame = 0; frame < 120 && offered != food.VerbLabel; frame++)
            {
                at = Vector2.MoveTowards(at, target, 0.1f);
                yield return null;
                offered = Offered(at);
            }
            Assert.That(offered, Is.EqualTo(food.VerbLabel), "the section is offered in reach");

            // The press opens the chooser with one row per food.
            var actor = new InteractActor(at, Vector2.up, InteractContext.OnFoot);
            Assert.IsTrue(InteractVerb.TryPress(actor, Arc), "the press is taken");
            Assert.IsTrue(InteractChooser.IsOpen, "three foods: the chooser opens");
            InteractChoiceList rows = InteractChooser.Rows;
            var foodRows = new List<int>();
            for (int r = 0; r < rows.Count; r++) if (ReferenceEquals(rows[r].Candidate, section)) foodRows.Add(r);
            Assert.That(foodRows.Count, Is.EqualTo(3), "a row for each food");
            for (int k = 0; k < 3; k++)
                Assert.That(rows[foodRows[k]].Label, Is.EqualTo(foods[k].DisplayName + " — " + MoneyFormat.Balance(foods[k].Price)
                                                                + " · " + before[k] + " left"), "row " + k);

            // He picks the second row: the oatcakes.
            Assert.IsTrue(InteractChooser.Pick(foodRows[1]));
            yield return null;
            Assert.IsFalse(InteractChooser.IsOpen, "the list goes down");

            Assert.That(wallet.Money, Is.EqualTo(100 - foods[1].Price), "he paid the oatcakes' price, once");
            Assert.That(bag.Bagged, Is.EqualTo(new[] { foods[1].Id }), "one packet into the paper bag in his hands");
            Assert.That(shelves.CountAt(lines[1], now), Is.EqualTo(before[1] - 1), "one packet off the oatcakes' shelf");
            Assert.That(shelves.CountAt(lines[0], now), Is.EqualTo(before[0]), "the bread is its own stock");
            Assert.That(shelves.CountAt(lines[2], now), Is.EqualTo(before[2]), "the beans are their own stock");
            Assert.That(StorePurchaseRows.CountFor(save.StorePurchases, slots.ShopId), Is.EqualTo(1), "the purchase is in the game");
            Assert.That(section.ActionLabel(1), Is.EqualTo(foods[1].DisplayName + " — " + MoneyFormat.Balance(foods[1].Price)
                                                          + " · " + (before[1] - 1) + " left"), "the row counts down");
        }
    }
}
