using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The chandlery counter that sells a HELM INSTRUMENT and fits it to a boat (ADR 0025 S2 — the depth
    /// sounder is the first). Covers the whole purchase contract: an affordable buy charges the price,
    /// records the fitment against the hull the player is ABOARD, and raises
    /// <see cref="InstrumentPurchased"/>; an unaffordable buy, an already-fitted hull, and — the case
    /// that separates this vendor from <see cref="GearShop"/> — having <b>no boat to fit it to</b> all
    /// change nothing and charge nothing. Pure logic over the Core contracts + save DTO — headless.
    /// </summary>
    public class InstrumentShopTests
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
        private readonly List<InstrumentPurchased> _events = new();
        private void OnPurchased(InstrumentPurchased e) => _events.Add(e);

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            EventBus.Clear<InstrumentPurchased>();
            EventBus.Subscribe<InstrumentPurchased>(OnPurchased);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<InstrumentPurchased>(OnPurchased);
            EventBus.Clear<InstrumentPurchased>();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private InstrumentShop MakeShop()
        {
            var go = new GameObject("InstrumentShop");
            _spawned.Add(go);
            return go.AddComponent<InstrumentShop>();
        }

        private InstrumentOffer MakeOffer(string id, int price)
        {
            var offer = ScriptableObject.CreateInstance<InstrumentOffer>();
            offer.Id = id;
            offer.DisplayName = id;
            offer.Price = price;
            _spawned.Add(offer);
            return offer;
        }

        private static SaveData Aboard(string hullId)
        {
            SaveData save = SaveMigration.NewGame();
            save.ActiveHullId = hullId;
            if (!string.IsNullOrEmpty(hullId)) save.OwnedBoats.Add(hullId);
            return save;
        }

        [Test]
        public void AffordableBuy_ChargesPrice_FitsTheBoatYouAreOn_RaisesEvent()
        {
            var shop = MakeShop();
            var offer = MakeOffer("instrument.depth_sounder", 140);
            var wallet = new FakeWallet(200);
            SaveData save = Aboard("boat.skiff");

            Assert.IsTrue(shop.TryBuy(offer, wallet, save));
            Assert.IsTrue(shop.LastPurchaseSucceeded);
            Assert.AreEqual(60, wallet.Money, "the price came out of the purse exactly once");
            Assert.IsTrue(InstrumentLocker.Owns(save, "boat.skiff", "instrument.depth_sounder"));
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual("instrument.depth_sounder", _events[0].InstrumentId);
            Assert.AreEqual("boat.skiff", _events[0].HullId, "the signal names the boat it went into");
            Assert.AreEqual(140, _events[0].PricePaid);
        }

        [Test]
        public void WithNoBoatAboard_ItRefuses_AndChargesNothing()
        {
            var shop = MakeShop();
            var offer = MakeOffer("instrument.depth_sounder", 140);
            var wallet = new FakeWallet(200);
            SaveData save = Aboard("");

            Assert.IsFalse(shop.TryBuy(offer, wallet, save),
                "an instrument bolts into a dash — with no boat there is nothing to fit it to");
            Assert.AreEqual(200, wallet.Money, "and the counter never eats the coin");
            Assert.IsEmpty(save.HullInstruments);
            Assert.IsEmpty(_events);
        }

        [Test]
        public void AlreadyFitted_ToThisHull_IsANoOp()
        {
            var shop = MakeShop();
            var offer = MakeOffer("instrument.depth_sounder", 140);
            var wallet = new FakeWallet(400);
            SaveData save = Aboard("boat.skiff");

            Assert.IsTrue(shop.TryBuy(offer, wallet, save));
            Assert.IsFalse(shop.TryBuy(offer, wallet, save), "no second unit in the same cutout");
            Assert.AreEqual(260, wallet.Money, "charged once, not twice");
            Assert.AreEqual(1, save.HullInstruments.Count);
            Assert.AreEqual(1, _events.Count);
        }

        [Test]
        public void TheSameInstrument_CanBeBoughtAgain_ForTheNextBoat()
        {
            var shop = MakeShop();
            var offer = MakeOffer("instrument.depth_sounder", 140);
            var wallet = new FakeWallet(400);
            SaveData save = Aboard("boat.skiff");

            Assert.IsTrue(shop.TryBuy(offer, wallet, save));
            save.ActiveHullId = "boat.punt";                  // walked down and got aboard the other boat
            Assert.IsTrue(shop.TryBuy(offer, wallet, save),
                "ownership is PER HULL — the punt needs its own unit");
            Assert.AreEqual(120, wallet.Money);
            Assert.IsTrue(InstrumentLocker.Owns(save, "boat.skiff", "instrument.depth_sounder"));
            Assert.IsTrue(InstrumentLocker.Owns(save, "boat.punt", "instrument.depth_sounder"));
        }

        [Test]
        public void UnaffordableBuy_ChangesNothing()
        {
            var shop = MakeShop();
            var offer = MakeOffer("instrument.depth_sounder", 140);
            var wallet = new FakeWallet(60);
            SaveData save = Aboard("boat.skiff");

            Assert.IsFalse(shop.TryBuy(offer, wallet, save));
            Assert.AreEqual(60, wallet.Money);
            Assert.IsEmpty(save.HullInstruments);
            Assert.IsEmpty(_events);
        }

        [Test]
        public void NullSave_OrIdlessOffer_Refuses_RatherThanEatTheCoin()
        {
            var shop = MakeShop();
            var wallet = new FakeWallet(500);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("InstrumentShop"));
            Assert.IsFalse(shop.TryBuy(MakeOffer("instrument.depth_sounder", 10), wallet, null));
            Assert.AreEqual(500, wallet.Money);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("InstrumentShop"));
            Assert.IsFalse(shop.TryBuy(MakeOffer("", 10), wallet, Aboard("boat.skiff")));
            Assert.AreEqual(500, wallet.Money);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("InstrumentShop"));
            Assert.IsFalse(shop.TryBuy(null, wallet, Aboard("boat.skiff")));
            Assert.AreEqual(500, wallet.Money);
        }

        // ---- the buy screen's view of it -------------------------------------------------------------

        [Test]
        public void TheBuyRow_NamesTheBoat_AndGoesOwnedOnlyForThatHull()
        {
            var stall = new GameObject("Counter");
            _spawned.Add(stall);
            var shop = stall.AddComponent<InstrumentShop>();
            var offer = MakeOffer("instrument.depth_sounder", 140);
            typeof(InstrumentShop)
                .GetField("_offer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(shop, offer);

            SaveData save = Aboard("boat.skiff");
            var rows = new List<BuyRow>();

            CatalogTestStall.BuildWired(stall, 500, save, null, rows);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(BuyRowKind.Instrument, rows[0].Quote.Kind);
            Assert.IsFalse(rows[0].Quote.Owned);
            Assert.IsTrue(rows[0].Quote.CanBuy);
            StringAssert.Contains("boat.skiff", rows[0].Note, "the row says which boat it goes on");

            InstrumentLocker.Add(save, "boat.skiff", "instrument.depth_sounder");
            CatalogTestStall.BuildWired(stall, 500, save, null, rows);
            Assert.IsTrue(rows[0].Quote.Owned, "owned — for the boat you are on");
            Assert.IsFalse(rows[0].Quote.CanBuy);

            save.ActiveHullId = "boat.punt";
            CatalogTestStall.BuildWired(stall, 500, save, null, rows);
            Assert.IsFalse(rows[0].Quote.Owned, "…and available again from the punt's cockpit");
            Assert.IsTrue(rows[0].Quote.CanBuy);

            save.ActiveHullId = "";
            CatalogTestStall.BuildWired(stall, 500, save, null, rows);
            Assert.IsFalse(rows[0].Quote.CanBuy, "with no boat the row cannot be confirmed");
            StringAssert.Contains("aren't aboard", rows[0].Note);
        }

        [Test]
        public void BuyLogic_Instrument_GatesOnMoneyAndOnHavingABoat()
        {
            Assert.IsTrue(BuyLogic.Instrument(100, 100, ownedByThisHull: false, hasHull: true).CanBuy);
            Assert.IsFalse(BuyLogic.Instrument(100, 99, ownedByThisHull: false, hasHull: true).CanBuy);
            Assert.IsFalse(BuyLogic.Instrument(100, 500, ownedByThisHull: true, hasHull: true).CanBuy);
            Assert.IsFalse(BuyLogic.Instrument(100, 500, ownedByThisHull: false, hasHull: false).CanBuy);
        }
    }
}
