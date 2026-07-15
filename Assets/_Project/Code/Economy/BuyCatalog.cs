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
    /// (VS-16): every <see cref="Shipwright"/>, <see cref="GearShop"/>, <see cref="PotShop"/>, and
    /// <see cref="LicenseVendor"/> contributes one row from its wired Def asset. Ownership is read through the Core seams the
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
