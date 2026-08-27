using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A stall's own wired vendors, as a seller.</b> The pre-inversion convenience, kept HERE rather
    /// than in <see cref="BuyCatalog"/>.
    ///
    /// <para><b>Why it exists.</b> Several vendor tests are about a vendor's ARM — that the instrument
    /// row names the hull it would be bolted into, that the pot row reads the honest stock, that a
    /// general store is five vendors on one counter — and not about where stock comes from. Those
    /// questions did not change with the inversion, so their fixtures should not have to. This stamps one
    /// seller id across whatever vendors a test stacked on a GameObject, tags each one's wired offer to
    /// that seller, and builds.</para>
    ///
    /// <para><b>It is deliberately not production code.</b> The thing the inversion removed is exactly
    /// "rows come from the components on a GameObject" (design §2.3); putting that back in
    /// <see cref="BuyCatalog"/> would keep the seam it exists to close. In a test it is a fixture, and
    /// <c>BuyCatalogTests</c> covers the real path — seller ids, tags and stock — directly.</para>
    /// </summary>
    internal static class CatalogTestStall
    {
        /// <summary>The seller every vendor on a test stall is stamped with.</summary>
        public const string Seller = "seller.test_stall";

        /// <summary>Fill <paramref name="into"/> with the rows this stall's wired vendors sell.</summary>
        public static void BuildWired(GameObject stall, int money, SaveData save,
                                      ILicenseService licenses, List<BuyRow> into)
        {
            into.Clear();
            if (stall == null) return;

            var boats = new List<ShipwrightOffer>();
            var gear = new List<GearOffer>();
            var pots = new List<PotOffer>();
            var bait = new List<BaitDef>();
            var supplies = new List<SupplyDef>();
            var instruments = new List<InstrumentOffer>();
            var licences = new List<LicenseDef>();

            foreach (var v in stall.GetComponents<Shipwright>())     Take(v, v.Offer, boats, CatalogSection.Boats);
            foreach (var v in stall.GetComponents<GearShop>())       Take(v, v.Offer, gear, CatalogSection.Gear);
            foreach (var v in stall.GetComponents<PotShop>())        Take(v, v.Offer, pots, CatalogSection.Gear);
            foreach (var v in stall.GetComponents<BaitShop>())       Take(v, v.Bait, bait, CatalogSection.Gear);
            foreach (var v in stall.GetComponents<SupplyShop>())     Take(v, v.Supply, supplies, CatalogSection.Gear);
            foreach (var v in stall.GetComponents<InstrumentShop>()) Take(v, v.Offer, instruments, CatalogSection.Gear);
            foreach (var v in stall.GetComponents<LicenseVendor>())  Take(v, v.License, licences, CatalogSection.Gear);

            var arms = new BuyCatalog.SellerArms(
                stall.GetComponent<Shipwright>(), stall.GetComponent<GearShop>(),
                stall.GetComponent<PotShop>(), stall.GetComponent<BaitShop>(),
                stall.GetComponent<SupplyShop>(), stall.GetComponent<InstrumentShop>(),
                stall.GetComponent<LicenseVendor>());

            var stock = new BuyCatalog.CatalogStock(boats, gear, pots, bait, supplies, instruments, licences);
            BuyCatalog.BuildFrom(arms, stock, money, save, licenses, into);
        }

        /// <summary>Stamp the seller on a vendor, tag its wired offer to that seller, and collect it.
        /// A vendor with nothing wired contributes nothing, exactly as the old scan skipped it.</summary>
        private static void Take<TOffer>(Component vendor, TOffer offer, List<TOffer> into,
                                         CatalogSection section)
            where TOffer : ScriptableObject
        {
            SetPrivate(vendor, "_sellerId", Seller);
            if (offer == null) return;

            FieldInfo tag = typeof(TOffer).GetField("Catalog", BindingFlags.Instance | BindingFlags.Public);
            if (tag != null)
            {
                tag.SetValue(offer, new CatalogListing
                {
                    Listed = true, Section = section, Sellers = new[] { Seller },
                    SortOrder = into.Count,   // keep the order the test stacked them in
                });
            }
            into.Add(offer);
        }

        private static void SetPrivate(Component c, string field, object value)
        {
            FieldInfo f = c.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) f.SetValue(c, value);
        }
    }
}
