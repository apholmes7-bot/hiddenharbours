using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <see cref="BuyCatalog"/> after the inversion — rows come from the CATALOG TAG on listing Defs,
    /// resolved against the vendor components that sell for a seller, not from whatever a level designer
    /// stacked on a stall GameObject.
    ///
    /// <para>Covers: every vendor kind contributes its Def-asset data; ownership reads through the same
    /// seams the vendors use (save lists, RepairLedger, ILicenseService); the damaged-dory row flips
    /// Buy → Repair once owned; a seller with no arm for a kind lists nothing of it; and the tag's own
    /// rules — who stocks what, and in what order.</para>
    ///
    /// <para><b>Stock is handed in, never swept.</b> <c>BuildFrom</c> takes a <c>CatalogStock</c> so
    /// these assertions stand on three lines of fixture rather than on whatever happens to be tagged in
    /// <c>Data/</c> at the time — content moves, and a row rule that only holds for today's shop is not
    /// a row rule. Headless EditMode: GameObjects but no scene.</para>
    /// </summary>
    public class BuyCatalogTests
    {
        const string Seller = "seller.leblancs";
        const string Elsewhere = "seller.somebody_else";

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

        // ---- fixture ----------------------------------------------------------------------------

        private GameObject MakeStall()
        {
            var go = new GameObject("Stall");
            _spawned.Add(go);
            return go;
        }

        /// <summary>Wire a vendor's private serialized field, as the scene builders do (SetRef pattern).</summary>
        private static void SetField(Component c, string field, object value)
        {
            var f = c.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"field {field} on {c.GetType().Name}");
            f.SetValue(c, value);
        }

        private T Vendor<T>(GameObject stall, string sellerId) where T : Component
        {
            var v = stall.AddComponent<T>();
            SetField(v, "_sellerId", sellerId);
            return v;
        }

        private static CatalogListing Tag(string seller, CatalogSection section = CatalogSection.Gear,
                                          int order = 0)
            => new CatalogListing
            {
                Listed = true, Section = section, Sellers = new[] { seller }, SortOrder = order,
            };

        private ShipwrightOffer MakeBoatOffer(string boatId, int price, string seller,
                                              bool damaged = false, int repairCost = 0, int order = 0)
        {
            var o = ScriptableObject.CreateInstance<ShipwrightOffer>();
            o.BoatId = boatId; o.DisplayName = boatId; o.Price = price;
            o.StartsDamaged = damaged; o.RepairCost = repairCost;
            o.Catalog = Tag(seller, CatalogSection.Boats, order);
            _spawned.Add(o);
            return o;
        }

        private GearOffer MakeGearOffer(string id, int price, string seller, int order = 0)
        {
            var o = ScriptableObject.CreateInstance<GearOffer>();
            o.Id = id; o.DisplayName = id; o.Price = price; o.Flavor = "flavour " + id;
            o.Catalog = Tag(seller, CatalogSection.Gear, order);
            _spawned.Add(o);
            return o;
        }

        private LicenseDef MakeLicense(string id, int fee, string seller, int order = 0)
        {
            var l = ScriptableObject.CreateInstance<LicenseDef>();
            l.Id = id; l.DisplayName = id; l.Price = fee;
            l.Catalog = Tag(seller, CatalogSection.Gear, order);
            _spawned.Add(l);
            return l;
        }

        /// <summary>Stock of exactly the kinds a case cares about; the rest are empty.</summary>
        private static BuyCatalog.CatalogStock Stock(
            IReadOnlyList<ShipwrightOffer> boats = null,
            IReadOnlyList<GearOffer> gear = null,
            IReadOnlyList<LicenseDef> licenses = null)
            => new BuyCatalog.CatalogStock(boats, gear, null, null, null, null, licenses);

        // ---- the rows ---------------------------------------------------------------------------

        [Test]
        public void OneCounter_OneRowPerListing_FromDefData()
        {
            var stall = MakeStall();
            var arms = new BuyCatalog.SellerArms(
                Vendor<Shipwright>(stall, Seller), Vendor<GearShop>(stall, Seller),
                null, null, null, null, Vendor<LicenseVendor>(stall, Seller));

            BuyCatalog.BuildFrom(arms,
                Stock(boats: new[] { MakeBoatOffer("boat.punt", 1800, Seller) },
                      gear: new[] { MakeGearOffer("gear.rod", 60, Seller) },
                      licenses: new[] { MakeLicense("license.cod", 120, Seller) }),
                money: 200, SaveMigration.NewGame(), new FakeLicenses(), _rows);

            Assert.AreEqual(3, _rows.Count);
            Assert.AreEqual(BuyRowKind.Boat, _rows[0].Quote.Kind);
            Assert.AreEqual("boat.punt", _rows[0].Id);
            Assert.IsFalse(_rows[0].Quote.CanBuy, "200 does not buy an 1800 Punt");
            Assert.AreEqual(BuyRowKind.Gear, _rows[1].Quote.Kind);
            Assert.IsTrue(_rows[1].Quote.CanBuy);
            Assert.AreEqual("flavour gear.rod", _rows[1].Flavor, "flavour text comes from the Def asset");
            Assert.AreEqual(BuyRowKind.License, _rows[2].Quote.Kind);
            Assert.IsTrue(_rows[2].Quote.CanBuy);
        }

        [Test]
        public void EveryRow_CarriesTheListingItWouldBuy()
        {
            // The inversion's own requirement: one GearShop now stands for a SELLER, so Confirm has to
            // be told WHICH listing — the component's own wired offer is no longer the answer.
            var stall = MakeStall();
            var arms = new BuyCatalog.SellerArms(null, Vendor<GearShop>(stall, Seller),
                                                 null, null, null, null, null);
            GearOffer rod = MakeGearOffer("gear.rod", 60, Seller, order: 1);
            GearOffer gaff = MakeGearOffer("gear.gaff", 25, Seller, order: 2);

            BuyCatalog.BuildFrom(arms, Stock(gear: new[] { rod, gaff }),
                                 10_000, SaveMigration.NewGame(), null, _rows);

            Assert.AreEqual(2, _rows.Count);
            Assert.AreSame(rod, _rows[0].Offer);
            Assert.AreSame(gaff, _rows[1].Offer);
            Assert.AreSame(arms.Gear, _rows[0].Vendor, "both sell through the one counter");
            Assert.AreSame(arms.Gear, _rows[1].Vendor);
        }

        [Test]
        public void OwnedGearAndHeldLicense_ReadThroughTheSeams()
        {
            var stall = MakeStall();
            var arms = new BuyCatalog.SellerArms(null, Vendor<GearShop>(stall, Seller),
                                                 null, null, null, null, Vendor<LicenseVendor>(stall, Seller));

            var save = SaveMigration.NewGame();
            save.OwnedGear.Add("gear.rod");
            var licenses = new FakeLicenses();
            licenses.Grant("license.cod");

            BuyCatalog.BuildFrom(arms,
                Stock(gear: new[] { MakeGearOffer("gear.rod", 60, Seller) },
                      licenses: new[] { MakeLicense("license.cod", 120, Seller) }),
                money: 10_000, save, licenses, _rows);

            Assert.IsTrue(_rows[0].Quote.Owned, "owned rod shows owned");
            Assert.IsTrue(_rows[1].Quote.Owned, "held licence shows owned");
            Assert.IsFalse(_rows[0].Quote.CanBuy);
            Assert.IsFalse(_rows[1].Quote.CanBuy);
        }

        [Test]
        public void DamagedDory_RowFlipsToRepair_OnceOwned_ThenToOwned_OnceRepaired()
        {
            var stall = MakeStall();
            var arms = new BuyCatalog.SellerArms(Vendor<Shipwright>(stall, Seller), null,
                                                 null, null, null, null, null);
            var dory = MakeBoatOffer("boat.dory", 400, Seller, damaged: true, repairCost: 300);
            var stock = Stock(boats: new[] { dory });
            var save = SaveMigration.NewGame();

            // Not owned yet → a purchase row at the hull price, warning of the repairs to come.
            BuyCatalog.BuildFrom(arms, stock, 500, save, null, _rows);
            Assert.AreEqual(BuyRowKind.Boat, _rows[0].Quote.Kind);
            Assert.AreEqual(400, _rows[0].Quote.Price);
            StringAssert.Contains("as-is", _rows[0].Note, "the sold-as-is warning rides the buy row");

            // Bought (owned, unrepaired) → the row becomes the repair at the repair cost.
            save.OwnedBoats.Add("boat.dory");
            BuyCatalog.BuildFrom(arms, stock, 500, save, null, _rows);
            Assert.AreEqual(BuyRowKind.BoatRepair, _rows[0].Quote.Kind);
            Assert.AreEqual(300, _rows[0].Quote.Price);
            Assert.IsTrue(_rows[0].Quote.CanBuy);

            // Repaired → owned, nothing left to sell.
            RepairLedger.MarkRepaired(save, "boat.dory");
            BuyCatalog.BuildFrom(arms, stock, 500, save, null, _rows);
            Assert.IsTrue(_rows[0].Quote.Owned);
            Assert.IsFalse(_rows[0].Quote.CanBuy);
        }

        [Test]
        public void OwnedPunt_NotBuyableAgain()
        {
            var stall = MakeStall();
            var arms = new BuyCatalog.SellerArms(Vendor<Shipwright>(stall, Seller), null,
                                                 null, null, null, null, null);
            var save = SaveMigration.NewGame();
            save.OwnedBoats.Add("boat.punt");

            BuyCatalog.BuildFrom(arms, Stock(boats: new[] { MakeBoatOffer("boat.punt", 1800, Seller) }),
                                 10_000, save, null, _rows);

            Assert.IsTrue(_rows[0].Quote.Owned, "the book closes the dev-P double-buy hole too");
            Assert.IsFalse(_rows[0].Quote.CanBuy);
        }

        [Test]
        public void AKindWithNoArm_ListsNothing_AndNullSaveIsSafe()
        {
            // A hull tagged to a chandler who has no yard is not an error — it is a boat she does not
            // sell. The listing is simply not on her shelf.
            var stall = MakeStall();
            var arms = new BuyCatalog.SellerArms(null, null, null, null, null, null,
                                                 Vendor<LicenseVendor>(stall, Seller));

            BuyCatalog.BuildFrom(arms,
                Stock(boats: new[] { MakeBoatOffer("boat.punt", 1800, Seller) },
                      licenses: new[] { MakeLicense("license.cod", 120, Seller) }),
                200, save: null, licenses: null, into: _rows);

            Assert.AreEqual(1, _rows.Count, "no Shipwright on this counter, so no hull on her shelf");
            Assert.AreEqual(BuyRowKind.License, _rows[0].Quote.Kind);
            Assert.IsFalse(_rows[0].Quote.Owned, "no licence service → treated as not held, never as held");
            Assert.IsTrue(_rows[0].Quote.CanBuy);
        }

        [Test]
        public void EmptyStock_ClearsTheRows_RatherThanLeavingTheLastSellersOnScreen()
        {
            var stall = MakeStall();
            var arms = new BuyCatalog.SellerArms(null, Vendor<GearShop>(stall, Seller),
                                                 null, null, null, null, null);
            _rows.Add(default);   // whatever the previous open left behind

            BuyCatalog.BuildFrom(arms, Stock(), 10_000, SaveMigration.NewGame(), null, _rows);

            Assert.IsEmpty(_rows);
        }

        // ---- the tag itself ---------------------------------------------------------------------

        [Test]
        public void AListingIsInvisible_UntilItSaysOtherwise()
        {
            var listing = new CatalogListing { Sellers = new[] { Seller } };   // Listed defaults false

            Assert.IsFalse(listing.IsListed, "the tag fails CLOSED");
            Assert.IsFalse(listing.IsStockedBy(Seller),
                           "an untagged Def cannot appear on a counter by being named on one");
        }

        [Test]
        public void OneListing_CanBeStockedByTwoPorts()
        {
            // The reason the tag holds a LIST: the same rod sold at the island store and at the creek
            // chandlery is one asset, not two that can drift apart in price.
            var listing = new CatalogListing
            {
                Listed = true, Sellers = new[] { Seller, Elsewhere }, Section = CatalogSection.Gear,
            };

            Assert.IsTrue(listing.IsStockedBy(Seller));
            Assert.IsTrue(listing.IsStockedBy(Elsewhere));
            Assert.IsFalse(listing.IsStockedBy("seller.nobody"));
            Assert.IsFalse(listing.IsStockedBy(null), "a missing seller id stocks nothing");
            Assert.IsFalse(listing.IsStockedBy(""));
        }

        [Test]
        public void AListingNobodySells_IsOrphaned_SoValidationCanSaySo()
        {
            Assert.IsTrue(new CatalogListing { Listed = true, Sellers = new string[0] }.IsOrphaned);
            Assert.IsTrue(new CatalogListing { Listed = true, Sellers = null }.IsOrphaned);
            Assert.IsFalse(new CatalogListing { Listed = true, Sellers = new[] { Seller } }.IsOrphaned);
            Assert.IsFalse(new CatalogListing { Listed = false, Sellers = null }.IsOrphaned,
                           "an unlisted draft is not an orphan — it is simply not on a shelf");
        }

        [Test]
        public void Order_IsSortOrderThenId_SoAnUnsetOrderIsStillDeterministic()
        {
            GearOffer b = MakeGearOffer("gear.b", 10, Seller);   // both SortOrder 0
            GearOffer a = MakeGearOffer("gear.a", 10, Seller);

            Assert.Less(CatalogSource.Compare(a, b), 0, "ties break on the id, ordinal");

            GearOffer first = MakeGearOffer("gear.z", 10, Seller, order: -1);
            Assert.Less(CatalogSource.Compare(first, a), 0, "and SortOrder wins over the id");
        }

        [Test]
        public void SectionChips_FitTheForeEdge()
        {
            foreach (CatalogSection s in CatalogSections.InOrder)
            {
                string chip = CatalogSections.ChipFor(s);
                Assert.IsNotEmpty(chip, $"{s} has no lettering");
                Assert.LessOrEqual(chip.Length, NotebookKit.ChipChars,
                                   $"'{chip}' does not fit a {NotebookKit.ChipChars}-character stub");
            }
        }

        [Test]
        public void SectionParse_IsLenient_BecauseItCrossesAModuleLine()
        {
            Assert.IsTrue(CatalogSections.TryParse("gear", out CatalogSection gear));
            Assert.AreEqual(CatalogSection.Gear, gear);
            Assert.IsTrue(CatalogSections.TryParse("BOATS", out CatalogSection boats));
            Assert.AreEqual(CatalogSection.Boats, boats);

            Assert.IsFalse(CatalogSections.TryParse("", out _), "empty opens the first stub, not Lots");
            Assert.IsFalse(CatalogSections.TryParse(null, out _));
            Assert.IsFalse(CatalogSections.TryParse("tackle", out _),
                           "an unrecognised section is a first-tab open, never a throw");
        }
    }
}
