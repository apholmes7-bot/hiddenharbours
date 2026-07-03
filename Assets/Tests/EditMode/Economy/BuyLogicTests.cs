using NUnit.Framework;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-16 buy screen — the pure affordability/state rules (<see cref="BuyLogic"/>). Covers gear,
    /// licence, and the boat's whole life (buy → owned-damaged → repair → owned-usable), and that
    /// CanBuy is exactly "not owned AND affordable". Pure logic, headless — the screen renders these
    /// quotes and never re-decides them.
    /// </summary>
    public class BuyLogicTests
    {
        // ---- gear -----------------------------------------------------------------------------

        [Test]
        public void Gear_Affordable_CanBuy()
        {
            var q = BuyLogic.Gear(price: 60, money: 60, owned: false);
            Assert.AreEqual(BuyRowKind.Gear, q.Kind);
            Assert.AreEqual(60, q.Price);
            Assert.IsTrue(q.CanBuy, "exactly-enough money buys (TrySpend is >=)");
        }

        [Test]
        public void Gear_TooDear_CannotBuy_ButNotOwned()
        {
            var q = BuyLogic.Gear(price: 60, money: 59, owned: false);
            Assert.IsFalse(q.CanBuy);
            Assert.IsFalse(q.Owned);
            Assert.IsFalse(q.Affordable);
        }

        [Test]
        public void Gear_Owned_NeverBuyable_EvenWithMoney()
        {
            var q = BuyLogic.Gear(price: 60, money: 10_000, owned: true);
            Assert.IsTrue(q.Owned);
            Assert.IsFalse(q.CanBuy, "owned gear is a no-op at the vendor; the screen must not offer it");
        }

        // ---- licence ---------------------------------------------------------------------------

        [Test]
        public void License_Held_ShowsOwned()
        {
            var q = BuyLogic.License(fee: 120, money: 500, held: true);
            Assert.AreEqual(BuyRowKind.License, q.Kind);
            Assert.IsTrue(q.Owned);
            Assert.IsFalse(q.CanBuy);
        }

        [Test]
        public void License_NotHeld_GatesOnMoney()
        {
            Assert.IsTrue(BuyLogic.License(120, 120, held: false).CanBuy);
            Assert.IsFalse(BuyLogic.License(120, 119, held: false).CanBuy);
        }

        // ---- boat: buy → owned-damaged → repair → owned-usable ---------------------------------

        [Test]
        public void Boat_NotOwned_QuotesPurchasePrice()
        {
            var q = BuyLogic.Boat(price: 1800, repairCost: 300, money: 2000,
                owned: false, startsDamaged: false, repaired: false);
            Assert.AreEqual(BuyRowKind.Boat, q.Kind);
            Assert.AreEqual(1800, q.Price);
            Assert.IsTrue(q.CanBuy);
        }

        [Test]
        public void Boat_NotOwned_TooDear_CannotBuy()
        {
            var q = BuyLogic.Boat(1800, 300, money: 1799, owned: false, startsDamaged: true, repaired: false);
            Assert.AreEqual(BuyRowKind.Boat, q.Kind, "an unowned damaged boat is still a PURCHASE first");
            Assert.IsFalse(q.CanBuy);
        }

        [Test]
        public void Boat_OwnedDamagedUnrepaired_QuotesRepairAtRepairCost()
        {
            var q = BuyLogic.Boat(price: 400, repairCost: 300, money: 350,
                owned: true, startsDamaged: true, repaired: false);
            Assert.AreEqual(BuyRowKind.BoatRepair, q.Kind, "the St Peters dory: bought, now needs the yard");
            Assert.AreEqual(300, q.Price, "the repair row charges the REPAIR cost, not the hull price");
            Assert.IsTrue(q.CanBuy, "₲350 covers the ₲300 repair even though it wouldn't re-buy the hull");
        }

        [Test]
        public void Boat_OwnedDamagedUnrepaired_RepairTooDear()
        {
            var q = BuyLogic.Boat(400, 300, money: 299, owned: true, startsDamaged: true, repaired: false);
            Assert.AreEqual(BuyRowKind.BoatRepair, q.Kind);
            Assert.IsFalse(q.CanBuy);
            Assert.IsFalse(q.Owned, "there IS something left to buy here — the repair");
        }

        [Test]
        public void Boat_OwnedAndRepaired_ShowsOwned()
        {
            var q = BuyLogic.Boat(400, 300, money: 10_000, owned: true, startsDamaged: true, repaired: true);
            Assert.IsTrue(q.Owned);
            Assert.IsFalse(q.CanBuy, "a repaired boat has nothing left to sell — no double-buys from the screen");
        }

        [Test]
        public void Boat_OwnedNonDamaged_ShowsOwned()
        {
            var q = BuyLogic.Boat(1800, 0, money: 10_000, owned: true, startsDamaged: false, repaired: true);
            Assert.IsTrue(q.Owned);
            Assert.IsFalse(q.CanBuy, "the old dev-P could re-buy the Punt; the screen must not");
        }
    }
}
