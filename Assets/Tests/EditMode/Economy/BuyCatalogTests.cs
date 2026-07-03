using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-16 buy screen — <see cref="BuyCatalog"/> turns a stall GameObject's vendor components into
    /// the rows the screen renders. Covers: every vendor kind contributes its Def-asset data; ownership
    /// reads through the same seams the vendors use (save lists, RepairLedger, ILicenseService); the
    /// damaged-dory row flips Buy → Repair once owned; unwired vendors are skipped. Headless EditMode —
    /// GameObjects but no scene, mirroring GearShopTests.
    /// </summary>
    public class BuyCatalogTests
    {
        private sealed class FakeLicenses : ILicenseService
        {
            private readonly HashSet<string> _held = new();
            public bool IsLicensed(string id) => string.IsNullOrEmpty(id) || _held.Contains(id);
            public void Grant(string id) { if (!string.IsNullOrEmpty(id)) _held.Add(id); }
            public int Count => _held.Count;
        }

        private readonly List<Object> _spawned = new();
        private readonly List<BuyRow> _rows = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            _rows.Clear();
        }

        // ---- builders ---------------------------------------------------------------------------

        private GameObject MakeStall()
        {
            var go = new GameObject("Stall");
            _spawned.Add(go);
            return go;
        }

        // Wire a vendor's private serialized offer field, as the scene builders do (SetRef pattern).
        private static void SetField(Component c, string field, Object value)
        {
            var f = c.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"field {field} on {c.GetType().Name}");
            f.SetValue(c, value);
        }

        private ShipwrightOffer MakeBoatOffer(string boatId, int price, bool damaged = false, int repairCost = 0)
        {
            var o = ScriptableObject.CreateInstance<ShipwrightOffer>();
            o.BoatId = boatId; o.DisplayName = boatId; o.Price = price;
            o.StartsDamaged = damaged; o.RepairCost = repairCost;
            _spawned.Add(o);
            return o;
        }

        private GearOffer MakeGearOffer(string id, int price)
        {
            var o = ScriptableObject.CreateInstance<GearOffer>();
            o.Id = id; o.DisplayName = id; o.Price = price; o.Flavor = "flavour " + id;
            _spawned.Add(o);
            return o;
        }

        private LicenseDef MakeLicense(string id, int fee)
        {
            var l = ScriptableObject.CreateInstance<LicenseDef>();
            l.Id = id; l.DisplayName = id; l.Price = fee;
            _spawned.Add(l);
            return l;
        }

        // ---- tests ------------------------------------------------------------------------------

        [Test]
        public void MixedStall_OneRowPerVendor_FromDefData()
        {
            var stall = MakeStall();
            SetField(stall.AddComponent<Shipwright>(), "_offer", MakeBoatOffer("boat.punt", 1800));
            SetField(stall.AddComponent<GearShop>(), "_offer", MakeGearOffer("gear.rod", 60));
            SetField(stall.AddComponent<LicenseVendor>(), "_license", MakeLicense("license.cod", 120));

            BuyCatalog.Build(stall, money: 200, SaveMigration.NewGame(), new FakeLicenses(), _rows);

            Assert.AreEqual(3, _rows.Count);
            Assert.AreEqual(BuyRowKind.Boat, _rows[0].Quote.Kind);
            Assert.AreEqual("boat.punt", _rows[0].Id);
            Assert.IsFalse(_rows[0].Quote.CanBuy, "₲200 doesn't buy a ₲1800 Punt");
            Assert.AreEqual(BuyRowKind.Gear, _rows[1].Quote.Kind);
            Assert.IsTrue(_rows[1].Quote.CanBuy);
            Assert.AreEqual("flavour gear.rod", _rows[1].Flavor, "flavour text comes from the Def asset");
            Assert.AreEqual(BuyRowKind.License, _rows[2].Quote.Kind);
            Assert.IsTrue(_rows[2].Quote.CanBuy);
        }

        [Test]
        public void OwnedGearAndHeldLicense_ReadThroughTheSeams()
        {
            var stall = MakeStall();
            SetField(stall.AddComponent<GearShop>(), "_offer", MakeGearOffer("gear.rod", 60));
            SetField(stall.AddComponent<LicenseVendor>(), "_license", MakeLicense("license.cod", 120));

            var save = SaveMigration.NewGame();
            save.OwnedGear.Add("gear.rod");
            var licenses = new FakeLicenses();
            licenses.Grant("license.cod");

            BuyCatalog.Build(stall, money: 10_000, save, licenses, _rows);

            Assert.IsTrue(_rows[0].Quote.Owned, "owned rod shows owned");
            Assert.IsTrue(_rows[1].Quote.Owned, "held licence shows owned");
            Assert.IsFalse(_rows[0].Quote.CanBuy);
            Assert.IsFalse(_rows[1].Quote.CanBuy);
        }

        [Test]
        public void DamagedDory_RowFlipsToRepair_OnceOwned_ThenToOwned_OnceRepaired()
        {
            var stall = MakeStall();
            SetField(stall.AddComponent<Shipwright>(), "_offer",
                MakeBoatOffer("boat.dory", 400, damaged: true, repairCost: 300));
            var save = SaveMigration.NewGame();

            // Not owned yet → a purchase row at the hull price, warning of the repairs to come.
            BuyCatalog.Build(stall, 500, save, null, _rows);
            Assert.AreEqual(BuyRowKind.Boat, _rows[0].Quote.Kind);
            Assert.AreEqual(400, _rows[0].Quote.Price);
            StringAssert.Contains("as-is", _rows[0].Note, "the sold-as-is warning rides the buy row");

            // Bought (owned, unrepaired) → the row becomes the repair at the repair cost.
            save.OwnedBoats.Add("boat.dory");
            BuyCatalog.Build(stall, 500, save, null, _rows);
            Assert.AreEqual(BuyRowKind.BoatRepair, _rows[0].Quote.Kind);
            Assert.AreEqual(300, _rows[0].Quote.Price);
            Assert.IsTrue(_rows[0].Quote.CanBuy);

            // Repaired → owned, nothing left to sell.
            RepairLedger.MarkRepaired(save, "boat.dory");
            BuyCatalog.Build(stall, 500, save, null, _rows);
            Assert.IsTrue(_rows[0].Quote.Owned);
            Assert.IsFalse(_rows[0].Quote.CanBuy);
        }

        [Test]
        public void OwnedPunt_NotBuyableAgain()
        {
            var stall = MakeStall();
            SetField(stall.AddComponent<Shipwright>(), "_offer", MakeBoatOffer("boat.punt", 1800));
            var save = SaveMigration.NewGame();
            save.OwnedBoats.Add("boat.punt");

            BuyCatalog.Build(stall, 10_000, save, null, _rows);

            Assert.IsTrue(_rows[0].Quote.Owned, "the screen closes the dev-P double-buy hole");
            Assert.IsFalse(_rows[0].Quote.CanBuy);
        }

        [Test]
        public void UnwiredVendors_AreSkipped_AndNullSaveIsSafe()
        {
            var stall = MakeStall();
            stall.AddComponent<Shipwright>();      // no offer wired
            stall.AddComponent<GearShop>();        // no offer wired
            SetField(stall.AddComponent<LicenseVendor>(), "_license", MakeLicense("license.cod", 120));

            BuyCatalog.Build(stall, 200, save: null, licenses: null, _rows);

            Assert.AreEqual(1, _rows.Count, "only the wired vendor contributes a row");
            Assert.AreEqual(BuyRowKind.License, _rows[0].Quote.Kind);
            Assert.IsFalse(_rows[0].Quote.Owned, "no licence service → treated as not held, never as held");
            Assert.IsTrue(_rows[0].Quote.CanBuy);
        }
    }
}
