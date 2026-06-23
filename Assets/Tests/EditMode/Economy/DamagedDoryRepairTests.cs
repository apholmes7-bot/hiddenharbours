using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters opening — the damaged-dory BUY + REPAIR flow. Covers the repair-state TRANSITION: a
    /// boat bought damaged is owned but NOT repaired (unusable) until the player pays the repair fee, at
    /// which point it flips to repaired (usable) and <c>BoatRepaired</c> fires. Also: a normal (non-
    /// damaged) buy is repaired-on-grant, repair is idempotent / fails when unaffordable, and you can't
    /// repair a boat that isn't damaged. Pure logic over the save DTO — no scene, runs headless.
    /// </summary>
    public class DamagedDoryRepairTests
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
        private readonly List<BoatPurchased> _purchases = new();
        private readonly List<BoatRepaired> _repairs = new();
        private void OnPurchased(BoatPurchased e) => _purchases.Add(e);
        private void OnRepaired(BoatRepaired e) => _repairs.Add(e);

        [SetUp]
        public void SetUp()
        {
            _purchases.Clear();
            _repairs.Clear();
            EventBus.Clear<BoatPurchased>();
            EventBus.Clear<BoatRepaired>();
            EventBus.Subscribe<BoatPurchased>(OnPurchased);
            EventBus.Subscribe<BoatRepaired>(OnRepaired);
            GameServices.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<BoatPurchased>(OnPurchased);
            EventBus.Unsubscribe<BoatRepaired>(OnRepaired);
            EventBus.Clear<BoatPurchased>();
            EventBus.Clear<BoatRepaired>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private Shipwright MakeShipwright()
        {
            var go = new GameObject("Shipwright");
            _spawned.Add(go);
            return go.AddComponent<Shipwright>();
        }

        private ShipwrightOffer MakeOffer(string boatId, int price, bool damaged, int repairCost)
        {
            var offer = ScriptableObject.CreateInstance<ShipwrightOffer>();
            offer.BoatId = boatId;
            offer.DisplayName = boatId;
            offer.Price = price;
            offer.StartsDamaged = damaged;
            offer.RepairCost = repairCost;
            _spawned.Add(offer);
            return offer;
        }

        private static SaveData NewSave() => SaveMigration.NewGame();

        // ---- the repair-state transition (the headline AC) ------------------------------------

        [Test]
        public void BuyDamaged_ThenRepair_FlipsFromUnusableToUsable()
        {
            var sw = MakeShipwright();
            var offer = MakeOffer("boat.dory", 400, damaged: true, repairCost: 300);
            var wallet = new FakeWallet(1000);
            var save = NewSave();

            // Buy: owned (BoatPurchased) but DAMAGED → not repaired → unusable.
            bool bought = sw.TryBuy(offer, wallet);
            Assert.IsTrue(bought, "an affordable damaged boat can be bought");
            Assert.AreEqual(600, wallet.Money, "buy price deducted");
            Assert.AreEqual(1, _purchases.Count, "BoatPurchased fires so the fleet grants the hull");
            Assert.IsFalse(RepairLedger.IsRepaired(save, "boat.dory"),
                "a freshly-bought damaged boat is NOT repaired (unusable until paid for)");

            // Repair: pay the fee → flips to repaired (usable) and BoatRepaired fires.
            bool repaired = sw.TryRepair(offer, wallet, save);
            Assert.IsTrue(repaired, "paying the repair fee repairs the boat");
            Assert.AreEqual(300, wallet.Money, "repair cost deducted (600 - 300)");
            Assert.IsTrue(RepairLedger.IsRepaired(save, "boat.dory"),
                "after paying, the boat is repaired (usable)");
            Assert.AreEqual(1, _repairs.Count, "exactly one BoatRepaired");
            Assert.AreEqual("boat.dory", _repairs[0].BoatId);
            Assert.AreEqual(300, _repairs[0].PricePaid);
        }

        [Test]
        public void Repair_WhenUnaffordable_LeavesBoatDamaged_NoEvent()
        {
            var sw = MakeShipwright();
            var offer = MakeOffer("boat.dory", 400, damaged: true, repairCost: 300);
            var wallet = new FakeWallet(500);
            var save = NewSave();

            Assert.IsTrue(sw.TryBuy(offer, wallet));   // 500 - 400 = 100 left, can't afford 300 repair
            bool repaired = sw.TryRepair(offer, wallet, save);

            Assert.IsFalse(repaired, "can't repair without the fee");
            Assert.AreEqual(100, wallet.Money, "wallet untouched by the failed repair");
            Assert.IsFalse(RepairLedger.IsRepaired(save, "boat.dory"), "still damaged");
            Assert.AreEqual(0, _repairs.Count);
        }

        [Test]
        public void Repair_IsIdempotent_NoDoubleCharge()
        {
            var sw = MakeShipwright();
            var offer = MakeOffer("boat.dory", 400, damaged: true, repairCost: 300);
            var wallet = new FakeWallet(1000);
            var save = NewSave();

            Assert.IsTrue(sw.TryBuy(offer, wallet));
            Assert.IsTrue(sw.TryRepair(offer, wallet, save));
            int afterFirstRepair = wallet.Money;

            bool second = sw.TryRepair(offer, wallet, save);
            Assert.IsFalse(second, "repairing an already-repaired boat is a no-op");
            Assert.AreEqual(afterFirstRepair, wallet.Money, "no double charge");
            Assert.AreEqual(1, _repairs.Count, "only one repair event");
        }

        [Test]
        public void BuyUndamaged_IsRepairedOnGrant_AndCannotBeRepaired()
        {
            var sw = MakeShipwright();
            var offer = MakeOffer("boat.punt", 1800, damaged: false, repairCost: 0);
            var wallet = new FakeWallet(2000);
            var save = NewSave();

            // Stand in for the live save so TryBuy's repaired-on-grant marking lands on our DTO.
            GameServices.Reset();
            var fakeSave = new FakeSaveService(save);
            GameServices.Save = fakeSave;

            try
            {
                Assert.IsTrue(sw.TryBuy(offer, wallet));
                Assert.IsTrue(RepairLedger.IsRepaired(save, "boat.punt"),
                    "a non-damaged boat is usable (repaired) the moment it's bought");

                bool repaired = sw.TryRepair(offer, wallet, save);
                Assert.IsFalse(repaired, "a non-damaged boat has nothing to repair");
                Assert.AreEqual(0, _repairs.Count);
            }
            finally
            {
                GameServices.Reset();
            }
        }

        // A tiny ISaveService stand-in so Shipwright.TryBuy can find GameServices.Save.Current.
        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }
    }
}
