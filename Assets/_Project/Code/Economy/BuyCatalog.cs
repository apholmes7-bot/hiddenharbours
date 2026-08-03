using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// One row the buy screen shows: which vendor component Confirm invokes, the offer's identity
    /// text (all from the Def assets — content is data, ADR 0003), and its resolved
    /// <see cref="BuyQuote"/>. <see cref="Vendor"/> is kept as the concrete component so the screen
    /// calls the EXISTING no-arg seams (<c>TryBuy()</c>/<c>TryRepair()</c>) — the screen is a skin
    /// over the vendors' purchase flow, never a second implementation of it.
    /// </summary>
    public readonly struct BuyRow
    {
        /// <summary>The vendor whose seam Confirm invokes (a Shipwright, GearShop, or LicenseVendor).</summary>
        public readonly Component Vendor;
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

        public BuyRow(Component vendor, string id, string displayName, string flavor, string note, BuyQuote quote)
        {
            Vendor = vendor; Id = id; DisplayName = displayName;
            Flavor = flavor ?? ""; Note = note ?? ""; Quote = quote;
        }
    }

    /// <summary>
    /// Builds the buy screen's rows from whatever vendor components sit on a stall GameObject
    /// (VS-16): every <see cref="Shipwright"/>, <see cref="GearShop"/>, <see cref="PotShop"/>,
    /// <see cref="BaitShop"/>, <see cref="SupplyShop"/>, and <see cref="LicenseVendor"/> contributes one row
    /// from its wired Def asset. That is what lets ONE counter be a whole general store (plan-to-m1 §7.5):
    /// stack a gear shop, a bait shop, a supply shop and a licence vendor on a single stall and the screen
    /// lists the rod, the bait, the ice and the clam licence together, with no new UI. Ownership is read through the Core seams the
    /// vendors themselves use (<see cref="SaveData"/>.OwnedBoats/OwnedGear, <see cref="RepairLedger"/>,
    /// <see cref="ILicenseService"/>) so the screen and the purchase can never disagree. Runs only when
    /// the screen opens or refreshes after a purchase — never per frame.
    /// </summary>
    public static class BuyCatalog
    {
        /// <summary>
        /// Fill <paramref name="into"/> (cleared first) with the rows for <paramref name="stall"/>.
        /// Null-safe on save/licences (EditMode, pre-boot): an unknown ownership reads as not-owned,
        /// matching the vendors' own guards. Vendors with no wired offer are skipped.
        /// </summary>
        public static void Build(GameObject stall, int money, SaveData save, ILicenseService licenses,
            List<BuyRow> into)
        {
            into.Clear();
            if (stall == null) return;

            foreach (var sw in stall.GetComponents<Shipwright>())
            {
                ShipwrightOffer o = sw.Offer;
                if (o == null) continue;
                bool owned = save?.OwnedBoats != null && !string.IsNullOrEmpty(o.BoatId)
                             && save.OwnedBoats.Contains(o.BoatId);
                bool repaired = RepairLedger.IsRepaired(save, o.BoatId);
                BuyQuote q = BuyLogic.Boat(o.Price, o.RepairCost, money, owned, o.StartsDamaged, repaired);
                into.Add(new BuyRow(sw, o.BoatId, o.DisplayName, "", NoteFor(q.Kind, o), q));
            }

            foreach (var gs in stall.GetComponents<GearShop>())
            {
                GearOffer o = gs.Offer;
                if (o == null) continue;
                bool owned = save?.OwnedGear != null && !string.IsNullOrEmpty(o.Id)
                             && save.OwnedGear.Contains(o.Id);
                into.Add(new BuyRow(gs, o.Id, o.DisplayName, o.Flavor, "",
                    BuyLogic.Gear(o.Price, money, owned)));
            }

            foreach (var ps in stall.GetComponents<PotShop>())
            {
                PotOffer o = ps.Offer;
                if (o == null) continue;
                // Pots are counted, repeatable stock — never "owned out". The Note carries the honest
                // inventory read (own N, M in the water) so the buy decision is informed at a glance.
                into.Add(new BuyRow(ps, o.Id, o.DisplayName, o.Flavor, PotNoteFor(save, o),
                    BuyLogic.Pot(o.Price, money)));
            }

            foreach (var bs in stall.GetComponents<BaitShop>())
            {
                BaitDef b = bs.Bait;
                if (b == null) continue;
                // Bait is counted, repeatable stock like pots — never "owned out". The row prices the whole
                // LOT (nobody buys a single capelin) and the Note carries how many are already in the box.
                into.Add(new BuyRow(bs, b.Id, BaitRowNameFor(b), b.Flavor, BaitNoteFor(save, b),
                    BuyLogic.Bait(BaitShop.LotPriceOf(b), money)));
            }

            foreach (var ss in stall.GetComponents<SupplyShop>())
            {
                SupplyDef s = ss.Supply;
                if (s == null) continue;
                into.Add(new BuyRow(ss, s.Id, s.DisplayName, s.Flavor, SupplyNoteFor(save, s),
                    BuyLogic.Supply(s.Price, money)));
            }

            foreach (var ins in stall.GetComponents<InstrumentShop>())
            {
                InstrumentOffer o = ins.Offer;
                if (o == null) continue;
                // An instrument is fitted to ONE hull, so ownership is asked of the boat the player is
                // aboard — and the Note names it, because "you already own this" is only meaningful
                // once you know which boat it is on.
                string hull = InstrumentShop.TargetHull(save);
                bool hasHull = !string.IsNullOrEmpty(hull);
                bool owned = hasHull && InstrumentLocker.Owns(save, hull, o.Id);
                into.Add(new BuyRow(ins, o.Id, o.DisplayName, o.Flavor, InstrumentNoteFor(hull, owned),
                    BuyLogic.Instrument(o.Price, money, owned, hasHull)));
            }

            foreach (var lv in stall.GetComponents<LicenseVendor>())
            {
                LicenseDef l = lv.License;
                if (l == null) continue;
                // ILicenseService treats a null/empty id as "ungated → true"; an offer with no id must
                // NOT read as already-held, so gate the lookup on a real id (the vendor refuses to sell
                // an id-less licence anyway).
                bool held = licenses != null && !string.IsNullOrEmpty(l.Id) && licenses.IsLicensed(l.Id);
                into.Add(new BuyRow(lv, l.Id, l.DisplayName, l.Flavor, "",
                    BuyLogic.License(l.Price, money, held)));
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
