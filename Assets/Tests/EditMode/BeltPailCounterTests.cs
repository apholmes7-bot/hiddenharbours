using System.Collections.Generic;
using System.Reflection;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The pail he carries is what the island counter buys.</b> A counter whose provider is the player's own belt
    /// pail (<see cref="GameServices.PlayerHold"/>, the St Peters store) reads the container in his hands when there is
    /// catch in it, and the belt pail otherwise: the pail the clams went into is the pail that is sold. Each sale reads
    /// one hold, so nothing is sold twice; an empty carried pail never hides a belt pail with catch, so nothing is
    /// stranded. A counter whose provider is not his belt pail (a boat's hold) sells that hold exactly as before, and a
    /// counter that sells what is carried is untouched (<see cref="NmcCarriedCounterTests"/>).
    /// </summary>
    public class BeltPailCounterTests
    {
        const string Clam = "fish.soft_shell_clam";

        readonly List<GameObject> _spawned = new List<GameObject>();
        ICarrier _handsBefore;
        IHold _beltBefore;

        [SetUp]
        public void SetUp()
        {
            _handsBefore = GameServices.Hands;
            _beltBefore = GameServices.PlayerHold;
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Hands = _handsBefore;
            GameServices.PlayerHold = _beltBefore;
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        [Test]
        public void ThePailInHisHands_IsWhatTheBeltCounterBuys_AndTheBeltPailIsLeftAlone()
        {
            var belt = Filled(new TestPail("container.player.clam_bucket"), 2);
            var pail = Filled(new TestPail("container.bucket"), 5);
            GameServices.PlayerHold = belt;
            GameServices.Hands = new OneHand(pail);
            var wallet = new TestWallet();
            WharfSellPoint counter = Counter(provider: belt, wallet: wallet);

            int expected = SellService.SellAll(Filled(new TestPail("container.bucket"), 5), new TestWallet(), null);
            Assert.That(expected, Is.GreaterThan(0), "five clams are worth something");
            Assert.That(counter.HoldAtTheCounter(), Is.SameAs(pail), "the counter reads the pail in his hands");

            int paid = counter.SellOverTheCounter();
            Assert.That(paid, Is.EqualTo(expected), "the seam's own price for the five clams");
            Assert.That(wallet.Money, Is.EqualTo(expected), "the purse rises by the payout, once");
            Assert.That(counter.LastUnitsSold, Is.EqualTo(5), "every clam in the pail was bought");
            Assert.That(pail.UsedUnits, Is.EqualTo(0), "the pail is empty");
            Assert.That(GameServices.Hands.Carried, Is.SameAs(pail), "and still in his hands");
            Assert.That(belt.UsedUnits, Is.EqualTo(2), "the belt pail keeps its clams for the next sale");

            // The next press, the carried pail now empty, sells the belt pail: nothing is stranded.
            int second = counter.SellOverTheCounter();
            Assert.That(second, Is.GreaterThan(0), "the belt pail's clams sell on the next press");
            Assert.That(counter.LastUnitsSold, Is.EqualTo(2), "both of them");
            Assert.That(belt.UsedUnits, Is.EqualTo(0), "the belt pail is empty");
            Assert.That(wallet.Money, Is.EqualTo(paid + second), "seven clams paid for, none twice");

            // And the next press has nothing left to sell.
            Assert.That(counter.SellOverTheCounter(), Is.EqualTo(0), "nothing left anywhere");
            Assert.That(counter.LastUnitsSold, Is.EqualTo(0));
        }

        [Test]
        public void WithNoPailInHand_OrAnEmptyOne_TheBeltCounterSellsTheBeltPail_AsBefore()
        {
            foreach (ICarriable held in new ICarriable[] { null, new TestTool("tool.shovel"), new TestPail("container.bucket") })
            {
                var belt = Filled(new TestPail("container.player.clam_bucket"), 3);
                GameServices.PlayerHold = belt;
                GameServices.Hands = new OneHand(held);
                var wallet = new TestWallet();
                WharfSellPoint counter = Counter(provider: belt, wallet: wallet);
                string what = held == null ? "empty hands" : held.DefId;

                Assert.That(counter.HoldAtTheCounter(), Is.SameAs(belt), $"{what}: the counter reads the belt pail");
                Assert.That(counter.SellOverTheCounter(), Is.GreaterThan(0), $"{what}: the belt pail sells");
                Assert.That(counter.LastUnitsSold, Is.EqualTo(3), $"{what}: all three clams");
                Assert.That(belt.UsedUnits, Is.EqualTo(0), $"{what}: the belt pail is empty");
                Assert.That(wallet.Money, Is.GreaterThan(0), $"{what}: and paid for");
            }
        }

        [Test]
        public void ACounterWhoseProviderIsNotHisBeltPail_StillSellsThatHold_AndNotThePailInHand()
        {
            var belt = Filled(new TestPail("container.player.clam_bucket"), 1);
            var boat = Filled(new TestPail("hold.boat"), 3);
            var pail = Filled(new TestPail("container.bucket"), 2);
            GameServices.PlayerHold = belt;
            GameServices.Hands = new OneHand(pail);
            WharfSellPoint counter = Counter(provider: boat, wallet: new TestWallet());

            Assert.That(counter.HoldAtTheCounter(), Is.SameAs(boat), "a boat-hold counter reads its own provider");
            Assert.That(counter.SellOverTheCounter(), Is.GreaterThan(0));
            Assert.That(boat.UsedUnits, Is.EqualTo(0), "its provider's hold was sold");
            Assert.That(pail.UsedUnits, Is.EqualTo(2), "the pail in hand was not");
            Assert.That(belt.UsedUnits, Is.EqualTo(1), "nor the belt pail");
        }

        [Test]
        public void TheSameHandsAndPails_GiveTheSamePayout_EveryTime()
        {
            int first = -1;
            for (int run = 0; run < 3; run++)
            {
                var belt = Filled(new TestPail("container.player.clam_bucket"), 1);
                var pail = Filled(new TestPail("container.bucket"), 4);
                GameServices.PlayerHold = belt;
                GameServices.Hands = new OneHand(pail);
                int paid = Counter(provider: belt, wallet: new TestWallet()).SellOverTheCounter();
                if (first < 0) first = paid;
                Assert.That(paid, Is.EqualTo(first), $"run {run}: the same pail at an identical counter pays the same");
            }
        }

        // ---- fixtures ------------------------------------------------------------------------------------------------

        WharfSellPoint Counter(IHold provider, IWallet wallet)
        {
            var go = new GameObject("BeltPailCounterFixture");
            _spawned.Add(go);
            var buyer = go.AddComponent<FishBuyer>();
            var sell = go.AddComponent<WharfSellPoint>();
            Set(sell, "_buyer", buyer);
            Set(sell, "_sellsWhatIsCarried", false);
            Set(sell, "_hold", provider);    // as Awake resolves it off the hold provider
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
                Assert.That(pail.TryAdd(new CatchItem(Clam, "Clam", FishCategory.Shellfish, 0.05f, 2, 0.45f)), Is.True);
            return pail;
        }

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

        sealed class TestWallet : IWallet
        {
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount) { if (amount > Money) return false; Money -= amount; return true; }
        }
    }
}
