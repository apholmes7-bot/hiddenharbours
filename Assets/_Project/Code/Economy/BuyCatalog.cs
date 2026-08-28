using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// One row a seller's book shows: which vendor component Confirm invokes, WHICH LISTING it invokes it
    /// with, the offer's identity text (all from the Def assets — content is data, ADR 0003), and its
    /// resolved <see cref="BuyQuote"/>.
    ///
    /// <para><see cref="Vendor"/> is kept as the concrete component and <see cref="Offer"/> as the Def it
    /// should sell, so Confirm calls the EXISTING seams (<c>TryBuy(offer)</c>/<c>TryRepair(offer)</c>).
    /// The book is a skin over the vendors' purchase flow, never a second implementation of it. The
    /// offer is carried explicitly because one component now stands for a SELLER rather than for a single
    /// wired item: a chandler's one GearShop sells every gear listing tagged to her.</para>
    /// </summary>
    public readonly struct BuyRow
    {
        /// <summary>The vendor whose seam Confirm invokes (a Shipwright, GearShop, or LicenseVendor).</summary>
        public readonly Component Vendor;
        /// <summary>The listing Def this row buys — handed back to the vendor's offer-taking seam.</summary>
        public readonly ScriptableObject Offer;
        /// <summary>Stable content id (e.g. "boat.punt", "gear.rod", "license.cod").</summary>
        public readonly string Id;
        /// <summary>Player-facing name from the Def asset.</summary>
        public readonly string DisplayName;
        /// <summary>Flavour/description from the Def asset (may be empty).</summary>
        public readonly string Flavor;
        /// <summary>Condition note (e.g. the damaged-boat "sold as-is" warning; may be empty).</summary>
        public readonly string Note;
        /// <summary>The resolved action + price + affordability for this row.</summary>
        public readonly BuyQuote Quote;
        /// <summary>Which shelf of the book this row sits on.</summary>
        public readonly CatalogSection Section;

        public BuyRow(Component vendor, ScriptableObject offer, string id, string displayName,
                      string flavor, string note, BuyQuote quote, CatalogSection section)
        {
            Vendor = vendor; Offer = offer; Id = id; DisplayName = displayName;
            Flavor = flavor ?? ""; Note = note ?? ""; Quote = quote; Section = section;
        }
    }

    /// <summary>
    /// Builds a seller's book from the CATALOG TAG, not from a scene.
    ///
    /// <para><b>The inversion (design §2.3/§4).</b> This used to scan whatever vendor components a level
    /// designer had stacked on a stall GameObject, which made stock a SCENE fact: adding a boat to a yard
    /// was a <c>.unity</c> edit, and an offer asset could never say where it was sold. Now
    /// <see cref="CatalogSource"/> sweeps the shipped listings, each names the sellers that stock it, and
    /// this resolves that seller id to the components that own the purchase seams (owner ruling R1). One
    /// listing can be stocked by two ports without being duplicated, and nothing here touches a scene
    /// file (rule 9).</para>
    ///
    /// <para><b>What did NOT change.</b> Every quote arm and every note string below is the accumulated
    /// correctness of six vendor types and moved across unchanged. Ownership is still read through the
    /// Core seams the vendors themselves use (<see cref="SaveData"/>.OwnedBoats/OwnedGear,
    /// <see cref="RepairLedger"/>, <see cref="ILicenseService"/>) so the book and the purchase can never
    /// disagree. <see cref="BuyLogic"/> is untouched: no new purchase economics are written here.</para>
    ///
    /// <para><b>Runs on open, on tab change and after a purchase — never per frame</b> (rule 7). The
    /// component lookup is a scene sweep, which is why it happens once per rebuild and not once per
    /// row.</para>
    /// </summary>
    public static class BuyCatalog
    {
        /// <summary>
        /// The components on the loaded scenes that sell for one seller — resolved once per rebuild.
        ///
        /// <para>A seller with no component for a kind simply lists nothing of that kind: a listing
        /// tagged to a chandler who has no Shipwright on her counter is not an error, it is a boat she
        /// does not sell. Content validation catches the opposite mistake (a listing nobody sells).</para>
        /// </summary>
        public readonly struct SellerArms
        {
            public readonly Shipwright Shipwright;
            public readonly GearShop Gear;
            public readonly PotShop Pot;
            public readonly BaitShop Bait;
            public readonly SupplyShop Supply;
            public readonly InstrumentShop Instrument;
            public readonly LicenseVendor License;

            public SellerArms(Shipwright shipwright, GearShop gear, PotShop pot, BaitShop bait,
                              SupplyShop supply, InstrumentShop instrument, LicenseVendor license)
            {
                Shipwright = shipwright; Gear = gear; Pot = pot; Bait = bait;
                Supply = supply; Instrument = instrument; License = license;
            }

            /// <summary>True when this seller has at least one counter component in the loaded scenes.</summary>
            public bool Any => Shipwright != null || Gear != null || Pot != null || Bait != null
                               || Supply != null || Instrument != null || License != null;
        }

        /// <summary>
        /// Find the components that sell for <paramref name="sellerId"/>.
        ///
        /// <para><b>First match per kind wins, and that is deliberate.</b> Two GearShops claiming one
        /// seller id would be a content mistake, and picking either of them sells the same listing at the
        /// same price through the same save seam — so this is stable in the way that matters rather than
        /// arbitrary in the way that bites.</para>
        /// </summary>
        public static SellerArms ArmsFor(string sellerId)
        {
            if (string.IsNullOrEmpty(sellerId)) return default;
            return new SellerArms(
                Find<Shipwright>(sellerId, v => v.SellerId),
                Find<GearShop>(sellerId, v => v.SellerId),
                Find<PotShop>(sellerId, v => v.SellerId),
                Find<BaitShop>(sellerId, v => v.SellerId),
                Find<SupplyShop>(sellerId, v => v.SellerId),
                Find<InstrumentShop>(sellerId, v => v.SellerId),
                Find<LicenseVendor>(sellerId, v => v.SellerId));
        }

        private static T Find<T>(string sellerId, System.Func<T, string> idOf) where T : Component
        {
            T[] all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (string.Equals(idOf(all[i]), sellerId, System.StringComparison.Ordinal)) return all[i];
            return null;
        }

        /// <summary>
        /// The listings one seller stocks, by kind — swept once and handed to the build.
        ///
        /// <para><b>It is a parameter so the row rules are testable without shipped content.</b> The
        /// sweeping overload is what the game calls; EditMode hands this in directly, so "a damaged hull
        /// quotes as a purchase first" is asserted against three lines of fixture rather than against
        /// whatever happens to be tagged in Data at the time.</para>
        /// </summary>
        public readonly struct CatalogStock
        {
            public readonly IReadOnlyList<ShipwrightOffer> Boats;
            public readonly IReadOnlyList<GearOffer> Gear;
            public readonly IReadOnlyList<PotOffer> Pots;
            public readonly IReadOnlyList<BaitDef> Bait;
            public readonly IReadOnlyList<SupplyDef> Supplies;
            public readonly IReadOnlyList<InstrumentOffer> Instruments;
            public readonly IReadOnlyList<LicenseDef> Licenses;

            public CatalogStock(IReadOnlyList<ShipwrightOffer> boats, IReadOnlyList<GearOffer> gear,
                                IReadOnlyList<PotOffer> pots, IReadOnlyList<BaitDef> bait,
                                IReadOnlyList<SupplyDef> supplies,
                                IReadOnlyList<InstrumentOffer> instruments,
                                IReadOnlyList<LicenseDef> licenses)
            {
                Boats = boats; Gear = gear; Pots = pots; Bait = bait;
                Supplies = supplies; Instruments = instruments; Licenses = licenses;
            }

            /// <summary>Everything tagged to this seller, in shelf order.</summary>
            public static CatalogStock Sweep(string sellerId) => new CatalogStock(
                CatalogSource.For<ShipwrightOffer>(sellerId), CatalogSource.For<GearOffer>(sellerId),
                CatalogSource.For<PotOffer>(sellerId), CatalogSource.For<BaitDef>(sellerId),
                CatalogSource.For<SupplyDef>(sellerId), CatalogSource.For<InstrumentOffer>(sellerId),
                CatalogSource.For<LicenseDef>(sellerId));
        }

        /// <summary>
        /// Fill <paramref name="into"/> (cleared first) with every row <paramref name="sellerId"/> stocks.
        ///
        /// <para>Null-safe on save/licences (EditMode, pre-boot): an unknown ownership reads as not-owned,
        /// matching the vendors' own guards. A listing whose seller has no component for its kind is
        /// skipped rather than shown with nothing behind it.</para>
        /// </summary>
        public static void Build(string sellerId, int money, SaveData save, ILicenseService licenses,
            List<BuyRow> into)
            => Build(sellerId, ArmsFor(sellerId), money, save, licenses, into);

        /// <summary>The same build against pre-resolved arms — the seam EditMode tests use, so the row
        /// rules can be asserted without a scene sweep finding somebody else's counter.</summary>
        public static void Build(string sellerId, in SellerArms arms, int money, SaveData save,
            ILicenseService licenses, List<BuyRow> into)
        {
            if (string.IsNullOrEmpty(sellerId)) { into.Clear(); return; }
            BuildFrom(arms, CatalogStock.Sweep(sellerId), money, save, licenses, into);
        }

        /// <summary>The build itself, over stock already resolved. Every quote arm and note string here
        /// moved across from the component scan unchanged.</summary>
        public static void BuildFrom(in SellerArms arms, in CatalogStock stock, int money, SaveData save,
            ILicenseService licenses, List<BuyRow> into)
        {
            into.Clear();

            if (arms.Shipwright != null)
            {
                foreach (ShipwrightOffer o in Each(stock.Boats))
                {
                    bool owned = save?.OwnedBoats != null && !string.IsNullOrEmpty(o.BoatId)
                                 && save.OwnedBoats.Contains(o.BoatId);
                    bool repaired = RepairLedger.IsRepaired(save, o.BoatId);
                    BuyQuote q = BuyLogic.Boat(o.Price, o.RepairCost, money, owned, o.StartsDamaged, repaired);
                    into.Add(new BuyRow(arms.Shipwright, o, o.BoatId, o.DisplayName, "",
                                        NoteFor(q.Kind, o), q, o.Catalog.Section));
                }
            }

            if (arms.Gear != null)
            {
                foreach (GearOffer o in Each(stock.Gear))
                {
                    bool owned = save?.OwnedGear != null && !string.IsNullOrEmpty(o.Id)
                                 && save.OwnedGear.Contains(o.Id);
                    into.Add(new BuyRow(arms.Gear, o, o.Id, o.DisplayName, o.Flavor, "",
                                        BuyLogic.Gear(o.Price, money, owned), o.Catalog.Section));
                }
            }

            if (arms.Pot != null)
            {
                foreach (PotOffer o in Each(stock.Pots))
                {
                    // Pots are counted, repeatable stock — never "owned out". The Note carries the honest
                    // inventory read (own N, M in the water) so the buy decision is informed at a glance.
                    into.Add(new BuyRow(arms.Pot, o, o.Id, o.DisplayName, o.Flavor, PotNoteFor(save, o),
                                        BuyLogic.Pot(o.Price, money), o.Catalog.Section));
                }
            }

            if (arms.Bait != null)
            {
                foreach (BaitDef b in Each(stock.Bait))
                {
                    // Bait is counted, repeatable stock like pots. The row prices the whole LOT (nobody
                    // buys a single capelin) and the Note carries how many are already in the box.
                    into.Add(new BuyRow(arms.Bait, b, b.Id, BaitRowNameFor(b), b.Flavor, BaitNoteFor(save, b),
                                        BuyLogic.Bait(BaitShop.LotPriceOf(b), money), b.Catalog.Section));
                }
            }

            if (arms.Supply != null)
            {
                foreach (SupplyDef s in Each(stock.Supplies))
                {
                    into.Add(new BuyRow(arms.Supply, s, s.Id, s.DisplayName, s.Flavor, SupplyNoteFor(save, s),
                                        BuyLogic.Supply(s.Price, money), s.Catalog.Section));
                }
            }

            if (arms.Instrument != null)
            {
                // An instrument is fitted to ONE hull, so ownership is asked of the boat the player is
                // aboard — and the Note names it, because "you already own this" is only meaningful once
                // you know which boat it is on.
                string hull = InstrumentShop.TargetHull(save);
                bool hasHull = !string.IsNullOrEmpty(hull);
                foreach (InstrumentOffer o in Each(stock.Instruments))
                {
                    bool owned = hasHull && InstrumentLocker.Owns(save, hull, o.Id);
                    into.Add(new BuyRow(arms.Instrument, o, o.Id, o.DisplayName, o.Flavor,
                                        InstrumentNoteFor(hull, owned),
                                        BuyLogic.Instrument(o.Price, money, owned, hasHull), o.Catalog.Section));
                }
            }

            if (arms.License != null)
            {
                foreach (LicenseDef l in Each(stock.Licenses))
                {
                    // ILicenseService treats a null/empty id as "ungated → true"; an offer with no id must
                    // NOT read as already-held, so gate the lookup on a real id (the vendor refuses to sell
                    // an id-less licence anyway).
                    bool held = licenses != null && !string.IsNullOrEmpty(l.Id) && licenses.IsLicensed(l.Id);
                    into.Add(new BuyRow(arms.License, l, l.Id, l.DisplayName, l.Flavor, "",
                                        BuyLogic.License(l.Price, money, held), l.Catalog.Section));
                }
            }
        }

        /// <summary>A null list is an empty one — a seller stocking nothing of a kind is ordinary.</summary>
        private static IReadOnlyList<T> Each<T>(IReadOnlyList<T> list) => list ?? System.Array.Empty<T>();

        /// <summary>
        /// Take the row the cursor is on, through the vendor's own seam.
        ///
        /// <para><b>This is the only place a purchase is spent from a book</b>, so "the panel is a skin"
        /// is one function rather than a promise. It calls the offer-taking overloads, which forward to
        /// the same code the dev keys and the old screen always called: the wallet spend, the save write
        /// and the Core event do not move.</para>
        ///
        /// <para>Returns false for a row that cannot be bought (unaffordable, already owned, no vendor),
        /// which is the same answer the vendors give and lets the book refuse without knowing why.</para>
        /// </summary>
        public static bool Confirm(in BuyRow row)
        {
            if (!row.Quote.CanBuy || row.Vendor == null) return false;

            switch (row.Quote.Kind)
            {
                case BuyRowKind.Boat:
                    return ((Shipwright)row.Vendor).TryBuy((ShipwrightOffer)row.Offer);
                case BuyRowKind.BoatRepair:
                    return ((Shipwright)row.Vendor).TryRepair((ShipwrightOffer)row.Offer);
                case BuyRowKind.Gear:
                    return ((GearShop)row.Vendor).TryBuy((GearOffer)row.Offer);
                case BuyRowKind.License:
                    return ((LicenseVendor)row.Vendor).TryBuy((LicenseDef)row.Offer);
                case BuyRowKind.Pot:
                    return ((PotShop)row.Vendor).TryBuy((PotOffer)row.Offer);
                case BuyRowKind.Bait:
                    return ((BaitShop)row.Vendor).TryBuy((BaitDef)row.Offer);
                case BuyRowKind.Supply:
                    return ((SupplyShop)row.Vendor).TryBuy((SupplyDef)row.Offer);
                case BuyRowKind.Instrument:
                    return ((InstrumentShop)row.Vendor).TryBuy((InstrumentOffer)row.Offer);
                default:
                    return false;
            }
        }

        // Stock note for a pot row: how many the player owns and how many are working in the water —
        // read through the same Core save the purchase writes (PotLocker), so screen and stock can
        // never disagree. Empty until the first pot is owned. (Loc-seam literals, HudStrings convention.)
        private static string PotNoteFor(SaveData save, PotOffer o)
        {
            int owned = PotLocker.OwnedCount(save, o.TrapDefId);
            if (owned <= 0) return "";
            int wet = PotLocker.DeployedCount(save, o.TrapDefId);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return wet > 0
                ? "You own " + owned.ToString(ci) + " - " + wet.ToString(ci) + " in the water."
                : "You own " + owned.ToString(ci) + ".";
        }

        // "Capelin ×10" — the row name says what a purchase actually gets you, so the lot price beside it
        // is not mistaken for the unit price. (Loc-seam literals, HudStrings convention.)
        private static string BaitRowNameFor(BaitDef b)
        {
            int lot = BaitShop.LotSizeOf(b);
            return lot > 1
                ? b.DisplayName + " x" + lot.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : b.DisplayName;
        }

        // Stock note for a bait row: how many are already in the tackle box, read through the same Core
        // wallet the purchase writes (TackleBox), so screen and stock can never disagree.
        private static string BaitNoteFor(SaveData save, BaitDef b)
        {
            int have = TackleBox.BaitCount(save, b.Id);
            return have <= 0
                ? ""
                : "You have " + have.ToString(System.Globalization.CultureInfo.InvariantCulture) + " in the box.";
        }

        // Fitment note for an instrument row: WHICH BOAT this purchase would bolt it into, read through the
        // same save the vendor writes. Without a boat the row says so rather than silently refusing at
        // Confirm. (Loc-seam literals, HudStrings convention.)
        private static string InstrumentNoteFor(string hullId, bool owned)
        {
            if (string.IsNullOrEmpty(hullId)) return "Fitted to a boat - you aren't aboard one.";
            return owned ? "Already fitted to " + hullId + "." : "Fits to " + hullId + ".";
        }

        // Stock note for a supply row (ice), read through SupplyLocker — the same locker the purchase writes.
        private static string SupplyNoteFor(SaveData save, SupplyDef s)
        {
            int have = SupplyLocker.Count(save, s.Id);
            return have <= 0
                ? ""
                : "You have " + have.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".";
        }

        // Condition note for a boat row (loc-seam literals, same convention as HudStrings: centralise
        // now, route to loc tables when they land).
        private static string NoteFor(BuyRowKind kind, ShipwrightOffer o)
        {
            if (kind == BuyRowKind.BoatRepair)
                return "Owned, but she needs work - pay the yard to make her seaworthy.";
            if (o.StartsDamaged)
                return "Sold as-is - needs ₲" + o.RepairCost.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) + " of repairs before she'll sail.";
            return "";
        }
    }
}
