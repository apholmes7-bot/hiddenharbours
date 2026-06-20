using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-16 — the Shipwright buy-the-Punt flow. Covers that an affordable purchase deducts the price
    /// and raises <see cref="BoatPurchased"/>, that an unaffordable one changes nothing and raises no
    /// event, the exact-coin boundary, and null safety. The economy side only — no Boats module here;
    /// the boat swap is gameplay-systems' job, driven off the published event.
    /// </summary>
    public class ShipwrightTests
    {
        // ---- in-file fake for the Core wallet contract --------------------------------------
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

        // Captured BoatPurchased payloads for the current test.
        private readonly List<BoatPurchased> _purchases = new();
        private void OnPurchased(BoatPurchased e) => _purchases.Add(e);

        [SetUp]
        public void SetUp()
        {
            _purchases.Clear();
            EventBus.Clear<BoatPurchased>();
            EventBus.Subscribe<BoatPurchased>(OnPurchased);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<BoatPurchased>(OnPurchased);
            EventBus.Clear<BoatPurchased>();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- helpers ------------------------------------------------------------------------
        private Shipwright MakeShipwright()
        {
            var go = new GameObject("Shipwright");
            _spawned.Add(go);
            return go.AddComponent<Shipwright>();
        }

        private ShipwrightOffer MakeOffer(string boatId, int price)
        {
            var offer = ScriptableObject.CreateInstance<ShipwrightOffer>();
            offer.BoatId = boatId;
            offer.DisplayName = boatId;
            offer.Price = price;
            _spawned.Add(offer);
            return offer;
        }

        // ---- tests --------------------------------------------------------------------------

        [Test]
        public void TryBuy_WhenAffordable_DeductsPrice_AndRaisesEvent()
        {
            var sw = MakeShipwright();
            var offer = MakeOffer("boat.punt", 1800);
            var wallet = new FakeWallet(2000);

            bool ok = sw.TryBuy(offer, wallet);

            Assert.IsTrue(ok, "an affordable purchase should succeed");
            Assert.IsTrue(sw.LastPurchaseSucceeded);
            Assert.AreEqual(200, wallet.Money, "wallet should be debited exactly the price");
            Assert.AreEqual(1, _purchases.Count, "exactly one BoatPurchased should be raised");
            Assert.AreEqual("boat.punt", _purchases[0].BoatId);
            Assert.AreEqual(1800, _purchases[0].PricePaid);
        }

        [Test]
        public void TryBuy_WhenNotAffordable_ChangesNothing_AndRaisesNoEvent()
        {
            var sw = MakeShipwright();
            var offer = MakeOffer("boat.punt", 1800);
            var wallet = new FakeWallet(1799);   // one coin short

            bool ok = sw.TryBuy(offer, wallet);

            Assert.IsFalse(ok, "an unaffordable purchase should fail");
            Assert.IsFalse(sw.LastPurchaseSucceeded);
            Assert.AreEqual(1799, wallet.Money, "wallet must be untouched when you can't afford it");
            Assert.AreEqual(0, _purchases.Count, "no BoatPurchased on a failed purchase");
        }

        [Test]
        public void TryBuy_WithExactCoin_Succeeds_AndLeavesZero()
        {
            var sw = MakeShipwright();
            var offer = MakeOffer("boat.punt", 1800);
            var wallet = new FakeWallet(1800);   // exactly enough

            bool ok = sw.TryBuy(offer, wallet);

            Assert.IsTrue(ok, "exact coin should afford the boat");
            Assert.AreEqual(0, wallet.Money);
            Assert.AreEqual(1, _purchases.Count);
            Assert.AreEqual("boat.punt", _purchases[0].BoatId);
        }

        [Test]
        public void TryBuy_NullOfferOrWallet_ChangesNothing()
        {
            var sw = MakeShipwright();
            var offer = MakeOffer("boat.punt", 1800);
            var wallet = new FakeWallet(5000);

            Assert.IsFalse(sw.TryBuy(null, wallet), "null offer buys nothing");
            Assert.IsFalse(sw.TryBuy(offer, null), "null wallet buys nothing");

            Assert.AreEqual(5000, wallet.Money, "wallet unchanged on null inputs");
            Assert.AreEqual(0, _purchases.Count, "no event on null inputs");
            Assert.IsFalse(sw.LastPurchaseSucceeded);
        }
    }
}
