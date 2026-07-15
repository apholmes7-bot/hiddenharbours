using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Pots are BOUGHT, not conjured (the trap loop's P2 money wheel): the shipwright's
    /// <see cref="PotShop"/> sells counted, repeatable pot stock. Covers: an affordable buy charges the
    /// price, increments the owned stock (<see cref="PotLocker"/>), and raises <c>PotPurchased</c>;
    /// buying again keeps counting (no "already owned" refusal — the deliberate difference from
    /// <see cref="GearShop"/>); an unaffordable buy or a recordless (null-save) buy changes nothing;
    /// the buy screen quotes pots as never-owned-out rows with an honest stock note; and the cozy
    /// STARTER KIT (<see cref="StartingPots"/>) grants GameConfig's data once, flag-guarded. Pure logic
    /// over the Core contracts + save DTO — headless.
    /// </summary>
    public class PotShopTests
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

        private sealed class FlagSaveService : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new();
            public FlagSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => _flags.TryGetValue(key, out var v) && v;
            public void SetFlag(string key, bool value) { if (!string.IsNullOrEmpty(key)) _flags[key] = value; }
            public void Save() { }
        }

        private readonly List<Object> _spawned = new();
        private readonly List<PotPurchased> _events = new();
        private void OnPurchased(PotPurchased e) => _events.Add(e);

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            EventBus.Clear<PotPurchased>();
            EventBus.Subscribe<PotPurchased>(OnPurchased);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<PotPurchased>(OnPurchased);
            EventBus.Clear<PotPurchased>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- builders -----------------------------------------------------------------------------

        private PotShop MakeShop()
        {
            var go = new GameObject("PotShop");
            _spawned.Add(go);
            return go.AddComponent<PotShop>();
        }

        private PotOffer MakeOffer(string id, string trapDefId, int price)
        {
            var offer = ScriptableObject.CreateInstance<PotOffer>();
            offer.Id = id;
            offer.TrapDefId = trapDefId;
            offer.DisplayName = id;
            offer.Price = price;
            _spawned.Add(offer);
            return offer;
        }

        // Wire a vendor's private serialized field, as the scene builders do (SetRef pattern).
        private static void SetField(Component c, string field, Object value)
        {
            var f = c.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"field {field} on {c.GetType().Name}");
            f.SetValue(c, value);
        }

        // ---- the purchase seam ----------------------------------------------------------------------

        [Test]
        public void AffordableBuy_ChargesPrice_IncrementsStock_RaisesEventWithNewTotal()
        {
            var shop = MakeShop();
            var offer = MakeOffer("offer.lobster_pot", "trap.lobster", 120);
            var wallet = new FakeWallet(200);
            var save = SaveMigration.NewGame();

            bool ok = shop.TryBuy(offer, wallet, save);

            Assert.IsTrue(ok);
            Assert.IsTrue(shop.LastPurchaseSucceeded);
            Assert.AreEqual(80, wallet.Money, "price deducted exactly");
            Assert.AreEqual(1, PotLocker.OwnedCount(save, "trap.lobster"), "one pot recorded, by TrapDef id");
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual("trap.lobster", _events[0].TrapDefId);
            Assert.AreEqual(120, _events[0].PricePaid);
            Assert.AreEqual(1, _events[0].OwnedCount);
        }

        [Test]
        public void RepeatBuys_KeepCounting_NoAlreadyOwnedRefusal()
        {
            var shop = MakeShop();
            var offer = MakeOffer("offer.crab_pot", "trap.crab", 60);
            var wallet = new FakeWallet(200);
            var save = SaveMigration.NewGame();

            Assert.IsTrue(shop.TryBuy(offer, wallet, save));
            Assert.IsTrue(shop.TryBuy(offer, wallet, save), "pots are counted stock — always re-buyable");
            Assert.IsTrue(shop.TryBuy(offer, wallet, save));

            Assert.AreEqual(200 - 3 * 60, wallet.Money, "charged every time");
            Assert.AreEqual(3, PotLocker.OwnedCount(save, "trap.crab"));
            Assert.AreEqual(3, _events.Count);
            Assert.AreEqual(3, _events[2].OwnedCount, "the event carries the running total");
        }

        [Test]
        public void Unaffordable_ChangesNothing_NoEvent()
        {
            var shop = MakeShop();
            var offer = MakeOffer("offer.lobster_pot", "trap.lobster", 120);
            var wallet = new FakeWallet(119);
            var save = SaveMigration.NewGame();

            Assert.IsFalse(shop.TryBuy(offer, wallet, save));
            Assert.AreEqual(119, wallet.Money);
            Assert.AreEqual(0, PotLocker.OwnedCount(save, "trap.lobster"));
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void NullSave_Refuses_ChargesNothing()
        {
            var shop = MakeShop();
            var offer = MakeOffer("offer.lobster_pot", "trap.lobster", 120);
            var wallet = new FakeWallet(200);

            Assert.IsFalse(shop.TryBuy(offer, wallet, null),
                "a pot with nowhere to be recorded must not eat the player's coin");
            Assert.AreEqual(200, wallet.Money);
            Assert.AreEqual(0, _events.Count);
        }

        [Test]
        public void OfferWithoutTrapDefId_Refuses_ChargesNothing()
        {
            var shop = MakeShop();
            var offer = MakeOffer("offer.broken", "", 10);
            var wallet = new FakeWallet(200);
            var save = SaveMigration.NewGame();

            // Logs a wiring WARNING (warnings don't fail the runner) and refuses.
            Assert.IsFalse(shop.TryBuy(offer, wallet, save));
            Assert.AreEqual(200, wallet.Money);
            Assert.IsEmpty(save.PotStock);
        }

        // ---- the buy-screen quote + row -------------------------------------------------------------

        [Test]
        public void BuyLogicPot_NeverOwnedOut_GatesOnMoneyOnly()
        {
            BuyQuote rich = BuyLogic.Pot(120, 200);
            Assert.AreEqual(BuyRowKind.Pot, rich.Kind);
            Assert.IsFalse(rich.Owned, "a pot row is never 'owned out' — always re-buyable");
            Assert.IsTrue(rich.CanBuy);

            BuyQuote poor = BuyLogic.Pot(120, 119);
            Assert.IsFalse(poor.Owned);
            Assert.IsFalse(poor.CanBuy, "money is the only gate");
        }

        [Test]
        public void BuyCatalog_ListsPotRows_WithTheHonestStockNote()
        {
            var stall = new GameObject("Stall");
            _spawned.Add(stall);
            var shop = stall.AddComponent<PotShop>();
            SetField(shop, "_offer", MakeOffer("offer.lobster_pot", "trap.lobster", 120));

            var save = SaveMigration.NewGame();
            var rows = new List<BuyRow>();

            // No pots owned yet → a plain buy row, no note.
            BuyCatalog.Build(stall, 200, save, null, rows);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("offer.lobster_pot", rows[0].Id);
            Assert.AreEqual(BuyRowKind.Pot, rows[0].Quote.Kind);
            Assert.IsTrue(rows[0].Quote.CanBuy);
            Assert.AreEqual("", rows[0].Note);

            // 3 owned, 2 in the water → the note reads the honest inventory.
            PotLocker.AddOwned(save, "trap.lobster", 3);
            save.PlacedTraps.Add(new PlacedTrapDto { TrapDefId = "trap.lobster", InstanceId = "a" });
            save.PlacedTraps.Add(new PlacedTrapDto { TrapDefId = "trap.lobster", InstanceId = "b" });
            BuyCatalog.Build(stall, 200, save, null, rows);
            Assert.AreEqual("You own 3 - 2 in the water.", rows[0].Note);
            Assert.IsFalse(rows[0].Quote.Owned, "still re-buyable at 3 owned");
        }

        // ---- the starter kit -------------------------------------------------------------------------

        private GameConfig MakeConfig(params PotStarterEntry[] kit)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.StarterPotKit = kit;
            _spawned.Add(config);
            return config;
        }

        [Test]
        public void StartingPotsGrant_AddsTheKit_AdditiveOverWhatIsOwned()
        {
            var save = SaveMigration.NewGame();
            PotLocker.AddOwned(save, "trap.lobster", 1);   // e.g. a v4 adoption from a wet pot

            int granted = StartingPots.Grant(save, new[]
            {
                new PotStarterEntry("trap.lobster", 2),
                new PotStarterEntry("trap.crab", 1),
                new PotStarterEntry("", 5),                // ignored: no id
                new PotStarterEntry("trap.eel", 0),        // ignored: zero count
            });

            Assert.AreEqual(2, granted, "two real entries applied");
            Assert.AreEqual(3, PotLocker.OwnedCount(save, "trap.lobster"), "additive: 1 adopted + 2 kit");
            Assert.AreEqual(1, PotLocker.OwnedCount(save, "trap.crab"));
            Assert.AreEqual(0, PotLocker.OwnedCount(save, "trap.eel"));
        }

        [Test]
        public void StartingPots_GrantOnce_FlagGuarded_SecondCallIsANoOp()
        {
            var save = SaveMigration.NewGame();
            var fake = new FlagSaveService(save);
            GameServices.Reset();
            GameServices.Save = fake;

            var go = new GameObject("StartingPots");
            _spawned.Add(go);
            var sp = go.AddComponent<StartingPots>();
            sp.Configure(MakeConfig(new PotStarterEntry("trap.lobster", 2), new PotStarterEntry("trap.crab", 1)),
                         "flag.pots_granted");

            Assert.AreEqual(2, sp.GrantOnce(), "first call grants the kit");
            Assert.AreEqual(2, PotLocker.OwnedCount(save, "trap.lobster"));
            Assert.AreEqual(1, PotLocker.OwnedCount(save, "trap.crab"));
            Assert.IsTrue(fake.GetFlag("flag.pots_granted"), "the grant flag persists the once-guard");

            Assert.AreEqual(0, sp.GrantOnce(), "the flag short-circuits a second grant");
            Assert.AreEqual(2, PotLocker.OwnedCount(save, "trap.lobster"), "no double kit");
        }

        [Test]
        public void StartingPots_NoConfigOrEmptyKit_GrantsNothing_AndStaysArmed()
        {
            var save = SaveMigration.NewGame();
            var fake = new FlagSaveService(save);
            GameServices.Reset();
            GameServices.Save = fake;

            var go = new GameObject("StartingPots");
            _spawned.Add(go);
            var sp = go.AddComponent<StartingPots>();
            sp.Configure(null, "flag.pots_granted");

            Assert.AreEqual(0, sp.GrantOnce(), "no config → nothing granted");
            Assert.IsFalse(fake.GetFlag("flag.pots_granted"),
                "the flag stays UNSET so a later wiring fix still delivers the kit");

            // Wire the config afterwards — the armed grant now lands.
            sp.Configure(MakeConfig(new PotStarterEntry("trap.lobster", 2)), "flag.pots_granted");
            Assert.AreEqual(1, sp.GrantOnce());
            Assert.AreEqual(2, PotLocker.OwnedCount(save, "trap.lobster"));
            Assert.IsTrue(fake.GetFlag("flag.pots_granted"));
        }
    }
}
