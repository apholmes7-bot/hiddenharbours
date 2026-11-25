using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using static HiddenHarbours.Tests.EditMode.NineMileCreekLiveGround;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>WENDELL BUYS THE PAIL YOU CARRY TO HIS COUNTER</b> (owner ruling 2026-11-11: delivered pails, with the empty pail
    /// retained). The hold a carried counter sells is the container in the player's hands, found through Core's hands
    /// alone, right hand first. It buys everything sellable in that container at the same price the same seam has always
    /// paid, the pail stays in hand empty, and empty hands sell nothing. The counter's boat-hold provider is never read,
    /// and a counter that sells its provider's hold still does.
    ///
    /// <para>In the committed scene, Wendell's counter names itself with the id his sell row names, and it sells what
    /// is carried. The builder has nothing left to do, and it rebuilds both fields to the byte. No St Peters sell point
    /// sells what is carried.</para>
    /// </summary>
    public class NmcCarriedCounterTests
    {
        const string WendellFirstPath = "Assets/_Project/Data/NPCs/Dialogue/WendellFirst.asset";
        const string StPetersPath = "Assets/_Project/Scenes/StPeters.unity";
        const string Clam = "fish.soft_shell_clam";

        readonly List<GameObject> _spawned = new List<GameObject>();
        ICarrier _handsBefore;

        [SetUp]
        public void SetUp() => _handsBefore = GameServices.Hands;

        [TearDown]
        public void TearDown()
        {
            GameServices.Hands = _handsBefore;
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        // ---- the logic: which container is "carried" ---------------------------------------------------------------

        [Test]
        public void TheCarriedHold_IsTheContainerInHand_RightHandFirst_AndNothingElse()
        {
            var pail = new TestPail("container.bucket");
            var other = new TestPail("container.bucket");
            var rod = new TestTool("tool.rod");

            Assert.That(CarriedHold.From(null), Is.Null, "no hands published: nothing is carried");
            Assert.That(CarriedHold.From(new OneHand(null)), Is.Null, "empty hands carry nothing");
            Assert.That(CarriedHold.From(new OneHand(rod)), Is.Null, "a rod is not a container");
            Assert.That(CarriedHold.From(new OneHand(pail)), Is.SameAs(pail), "the pail in hand is the hold");

            var toolRightPailLeft = new TwoHands();
            Take(toolRightPailLeft, rod, CarryHandSide.Right);
            Take(toolRightPailLeft, pail, CarryHandSide.Left);
            Assert.That(CarriedHold.From(toolRightPailLeft), Is.SameAs(pail), "the container in the off hand is still found");

            var pailRightToolLeft = new TwoHands();
            Take(pailRightToolLeft, other, CarryHandSide.Right);
            Take(pailRightToolLeft, rod, CarryHandSide.Left);
            Assert.That(CarriedHold.From(pailRightToolLeft), Is.SameAs(other), "the right hand's container");

            // Deterministic: the same hands give the same answer every time they are asked.
            for (int i = 0; i < 5; i++)
            {
                Assert.That(CarriedHold.From(toolRightPailLeft), Is.SameAs(pail));
                Assert.That(CarriedHold.From(pailRightToolLeft), Is.SameAs(other));
            }
        }

        // ---- the sale: the counter reads the carried pail ----------------------------------------------------------

        [Test]
        public void ACarriedCounter_BuysThePailInHand_AtTheSeamsPrice_AndNeverItsProvidersHold()
        {
            var pail = Filled(new TestPail("container.bucket"), 4);
            var boat = Filled(new TestPail("hold.boat"), 3);
            GameServices.Hands = new OneHand(pail);
            var wallet = new TestWallet();
            WharfSellPoint counter = Counter(carried: true, provider: boat, wallet: wallet);

            int expected = SellService.SellAll(Filled(new TestPail("container.bucket"), 4), new TestWallet(), null);
            Assert.That(expected, Is.GreaterThan(0), "four clams are worth something");

            int paid = counter.SellOverTheCounter();
            Assert.That(paid, Is.EqualTo(expected), "the same seam's price for the same four clams");
            Assert.That(wallet.Money, Is.EqualTo(expected), "the wallet rises by the payout, once");
            Assert.That(counter.LastUnitsSold, Is.EqualTo(4), "all four clams were bought");
            Assert.That(pail.UsedUnits, Is.EqualTo(0), "the pail is empty");
            Assert.That(GameServices.Hands.Carried, Is.SameAs(pail), "and still in hand");
            Assert.That(boat.UsedUnits, Is.EqualTo(3), "the boat's hold is untouched");

            // Determinism: the same pail at a fresh, identical counter pays the same.
            var again = Filled(new TestPail("container.bucket"), 4);
            GameServices.Hands = new OneHand(again);
            var wallet2 = new TestWallet();
            Assert.That(Counter(carried: true, provider: boat, wallet: wallet2).SellOverTheCounter(), Is.EqualTo(paid));
        }

        [Test]
        public void ACarriedCounter_WithEmptyHands_SellsNothing_AndLeavesItsProvidersHold()
        {
            var boat = Filled(new TestPail("hold.boat"), 3);
            var wallet = new TestWallet();
            WharfSellPoint counter = Counter(carried: true, provider: boat, wallet: wallet);

            GameServices.Hands = new OneHand(null);
            Assert.That(counter.SellOverTheCounter(), Is.EqualTo(0), "empty hands sell nothing");
            GameServices.Hands = new OneHand(new TestTool("tool.rod"));
            Assert.That(counter.SellOverTheCounter(), Is.EqualTo(0), "a rod in hand sells nothing");
            GameServices.Hands = null;
            Assert.That(counter.SellOverTheCounter(), Is.EqualTo(0), "no hands at all sells nothing");

            Assert.That(counter.LastUnitsSold, Is.EqualTo(0));
            Assert.That(wallet.Money, Is.EqualTo(0), "nothing was paid");
            Assert.That(boat.UsedUnits, Is.EqualTo(3), "the boat's hold is untouched");
        }

        [Test]
        public void AProviderCounter_StillSellsItsProvidersHold_AndNotThePailInHand()
        {
            var pail = Filled(new TestPail("container.bucket"), 2);
            var provider = Filled(new TestPail("container.bucket"), 3);
            GameServices.Hands = new OneHand(pail);
            var wallet = new TestWallet();
            WharfSellPoint counter = Counter(carried: false, provider: provider, wallet: wallet);

            Assert.That(counter.SellOverTheCounter(), Is.GreaterThan(0), "St Peters' counters sell as they did");
            Assert.That(provider.UsedUnits, Is.EqualTo(0), "its provider's hold was sold");
            Assert.That(pail.UsedUnits, Is.EqualTo(2), "the pail in hand was not");
        }

        [Test]
        public void TheDesk_FindsABuyOnlyCounter_ByTheIdItNamesItselfWith()
        {
            const string id = "seller.test_buy_only_counter";
            Assert.That(CounterSellDesk.CounterFor(id), Is.Null, "nothing answers before the counter stands");
            WharfSellPoint counter = Counter(carried: true, provider: null, wallet: new TestWallet());
            Set(counter, "_buyOnlySellerId", id);
            Assert.That(CounterSellDesk.CounterFor(id), Is.SameAs(counter), "the desk finds it by its own id");
            Assert.That(CounterSellDesk.CounterFor("seller.test_nobody"), Is.Null, "and nothing else by it");
        }

        // ---- the committed scene and data --------------------------------------------------------------------------

        [Test]
        public void WendellsCounter_SellsWhatIsCarried_UnderTheIdHisSellRowNames()
        {
            Doc go = Scene.RootNamed(NineMileCreekWharfLandSide.MarketCounterName);
            Doc sell = Script(go, "HiddenHarbours.Economy.WharfSellPoint");
            Assert.That(sell.Field("_buyOnlySellerId"), Is.EqualTo(NineMileCreekWharfLandSide.WendellSellerId), "the counter names itself");
            Assert.That(sell.Field("_sellsWhatIsCarried"), Is.EqualTo("1"), "the counter sells what is carried");

            var wendell = AssetDatabase.LoadAssetAtPath<DialogueDef>(WendellFirstPath);
            Assert.That(wendell, Is.Not.Null, WendellFirstPath);
            var rows = wendell.Options.Where(o => o.SellsAtCounter).ToList();
            Assert.That(rows.Count, Is.EqualTo(1), "Wendell has one sell row");
            Assert.That(rows[0].SellAtSellerId, Is.EqualTo(NineMileCreekWharfLandSide.WendellSellerId), "naming his counter");
            Assert.That(string.IsNullOrEmpty(rows[0].CatalogSellerId), Is.True, "a buy-only counter has no wares book");

            Doc wendellGo = Scene.Docs.Single(x => x.ClassId == 1 && x.Field("m_Name") == "WendellArsenault");
            Doc talk = Script(wendellGo, "HiddenHarbours.World.Interactable");
            var npc = AssetDatabase.LoadAssetAtPath<NpcDef>(AssetDatabase.GUIDToAssetPath(RefGuid(talk.Field("_npc"))));
            Assert.That(npc != null && npc.HasDialogue && npc.Dialogue == wendell, Is.True, "the Wendell you talk to speaks WendellFirst");
            float d = Vector2.Distance(WorldPosition(wendellGo), WorldPosition(go));
            TestContext.WriteLine($"Wendell stands {d:0.###} m from his counter; it sells what is carried as {sell.Field("_buyOnlySellerId")}");

            int others = Scene.Docs.Count(x => x.ClassId == 114 && x.Field("_buyOnlySellerId") == NineMileCreekWharfLandSide.WendellSellerId);
            Assert.That(others, Is.EqualTo(1), "one counter answers to Wendell's id");
            string stPeters = File.ReadAllText(StPetersPath);
            Assert.That(stPeters.Contains("_sellsWhatIsCarried: 1"), Is.False, "no St Peters counter sells what is carried");
            Assert.That(stPeters.Contains("_buyOnlySellerId: seller."), Is.False, "no St Peters counter names itself");
        }

        [Test]
        public void TheMarketCounterBuilder_HasNothingToDo_AndRebuildsWendellsCounterToTheByte()
        {
            string committed = File.ReadAllText(NineMileCreekLayerRefresh.ScenePath);
            SceneYaml scene = SceneYaml.Parse(committed);
            LayerPatch patch = NineMileCreekWharfLandSide.PlaceMarketCounter(scene);
            Assert.That(patch.IsEmpty, Is.True, $"the builder would still change the committed scene: {patch.Summary()}");

            string idLine = "  _buyOnlySellerId: " + NineMileCreekWharfLandSide.WendellSellerId + "\n";
            const string carriedLine = "  _sellsWhatIsCarried: 1\n";
            Assert.That(committed.Split(new[] { idLine }, System.StringSplitOptions.None).Length, Is.EqualTo(2), "one id line");
            Assert.That(committed.Split(new[] { carriedLine }, System.StringSplitOptions.None).Length, Is.EqualTo(2), "one carried line");
            string stripped = committed.Replace(idLine, "").Replace(carriedLine, "");

            SceneYaml before = SceneYaml.Parse(stripped);
            string rebuilt = NineMileCreekWharfLandSide.PlaceMarketCounter(before).Apply(before).Render();
            Assert.That(rebuilt == committed, Is.True, "the builder gives the committed scene back to the byte");
        }

        // ---- fixtures ------------------------------------------------------------------------------------------------

        WharfSellPoint Counter(bool carried, IHold provider, IWallet wallet)
        {
            var go = new GameObject("CarriedCounterFixture");
            _spawned.Add(go);
            var buyer = go.AddComponent<FishBuyer>();
            var sell = go.AddComponent<WharfSellPoint>();
            Set(sell, "_buyer", buyer);
            Set(sell, "_sellsWhatIsCarried", carried);
            Set(sell, "_hold", provider);    // as Awake would resolve it for a provider counter
            Set(sell, "_wallet", wallet);
            return sell;
        }

        static void Set(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(f, Is.Not.Null, $"{target.GetType().Name}.{field}");
            f.SetValue(target, value);
        }

        static TestPail Filled(TestPail pail, int clams)
        {
            for (int i = 0; i < clams; i++)
                Assert.That(pail.TryAdd(new CatchItem(Clam, "Clam", FishCategory.Shellfish, 0.05f, 9, 0.3f)), Is.True);
            return pail;
        }

        static void Take(TwoHands hands, ICarriable thing, CarryHandSide side) =>
            Assert.That(hands.Slots.TryTake(thing, false, side, out _), Is.EqualTo(HandSlotRefusal.None), $"{thing.DefId} into the {side} hand");

        sealed class TestPail : ICarriable, IHold
        {
            readonly List<CatchItem> _items = new List<CatchItem>();
            public TestPail(string defId) => DefId = defId;
            public string DefId { get; }
            public bool IsCarriable => true;
            public Transform Transform => null;
            public int BakedFacings => 0;
            public void ShowFacing(int facingIndex) { }
            public void RideSortingBand(int sortingLayerId, int sortingOrder) { }
            public void OnLifted(ICarrier carrier) { }
            public void OnPlaced() { }
            public int CapacityUnits => 20;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (_items.Count >= CapacityUnits) return false; _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        sealed class TestTool : ICarriable
        {
            public TestTool(string defId) => DefId = defId;
            public string DefId { get; }
            public bool IsCarriable => true;
            public Transform Transform => null;
            public int BakedFacings => 0;
            public void ShowFacing(int facingIndex) { }
            public void RideSortingBand(int sortingLayerId, int sortingOrder) { }
            public void OnLifted(ICarrier carrier) { }
            public void OnPlaced() { }
        }

        sealed class OneHand : ICarrier
        {
            public OneHand(ICarriable held) => Carried = held;
            public ICarriable Carried { get; }
            public bool IsCarrying => Carried != null;
        }

        sealed class TwoHands : IHandsCarrier
        {
            public HandSlots Slots { get; } = new HandSlots();
            public ICarriable Carried => Slots.Right ?? Slots.Left;
            public bool IsCarrying => Carried != null;
        }

        sealed class TestWallet : IWallet
        {
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount) { if (amount > Money) return false; Money -= amount; return true; }
        }
    }
}
