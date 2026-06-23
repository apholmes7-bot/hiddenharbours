using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters opening — the gear shop (rod / shovel / bucket as purchasable data). Covers that an
    /// affordable buy charges the price, records the gear id as owned in the save, and raises
    /// <c>GearPurchased</c>; an unaffordable or already-owned buy changes nothing. Pure logic over the
    /// Core contracts + save DTO — headless.
    /// </summary>
    public class GearShopTests
    {
        private sealed class FakeWallet : IWallet
        {
            public FakeWallet(int starting) { Money = starting; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private readonly List<Object> _spawned = new();
        private readonly List<GearPurchased> _events = new();
        private void OnPurchased(GearPurchased e) => _events.Add(e);

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            EventBus.Clear<GearPurchased>();
            EventBus.Subscribe<GearPurchased>(OnPurchased);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<GearPurchased>(OnPurchased);
            EventBus.Clear<GearPurchased>();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private GearShop MakeShop()
        {
            var go = new GameObject("GearShop");
            _spawned.Add(go);
            return go.AddComponent<GearShop>();
        }

        private GearOffer MakeOffer(string id, int price)
        {
            var offer = ScriptableObject.CreateInstance<GearOffer>();
            offer.Id = id;
            offer.DisplayName = id;
            offer.Price = price;
            _spawned.Add(offer);
            return offer;
        }

        [Test]
        public void AffordableBuy_ChargesPrice_RecordsOwnership_RaisesEvent()
        {
            var shop = MakeShop();
            var rod = MakeOffer("gear.rod", 60);
            var wallet = new FakeWallet(100);
            var save = SaveMigration.NewGame();

            bool ok = shop.TryBuy(rod, wallet, save);

            Assert.IsTrue(ok);
            Assert.IsTrue(shop.LastPurchaseSucceeded);
            Assert.AreEqual(40, wallet.Money, "price deducted exactly");
            CollectionAssert.Contains(save.OwnedGear, "gear.rod", "the rod is recorded as owned");
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual("gear.rod", _events[0].GearId);
            Assert.AreEqual(60, _events[0].PricePaid);
        }

        [Test]
        public void Unaffordable_ChangesNothing_NoEvent()
        {
            var shop = MakeShop();
            var rod = MakeOffer("gear.rod", 60);
            var wallet = new FakeWallet(59);
            var save = SaveMigration.NewGame();

            bool ok = shop.TryBuy(rod, wallet, save);

            Assert.IsFalse(ok);
            Assert.AreEqual(59, wallet.Money);
            CollectionAssert.DoesNotContain(save.OwnedGear, "gear.rod");
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void AlreadyOwned_DoesNotDoubleCharge()
        {
            var shop = MakeShop();
            var shovel = MakeOffer("gear.shovel", 25);
            var wallet = new FakeWallet(100);
            var save = SaveMigration.NewGame();

            Assert.IsTrue(shop.TryBuy(shovel, wallet, save));
            int afterFirst = wallet.Money;
            bool second = shop.TryBuy(shovel, wallet, save);

            Assert.IsFalse(second, "buying gear you already own is a no-op");
            Assert.AreEqual(afterFirst, wallet.Money, "no double charge");
            Assert.AreEqual(1, save.OwnedGear.Count, "the gear is recorded once");
            Assert.AreEqual(1, _events.Count);
        }
    }
}
