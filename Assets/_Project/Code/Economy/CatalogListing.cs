using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>Which shelf of a seller's book a listing sits on.</b> The six the owner's brief named.
    ///
    /// <para><b>APPEND-ONLY, like every shipped enum here.</b> These values are serialised into Def
    /// assets the moment anything is tagged, so re-ordering them silently re-shelves the content that
    /// was already authored. New sections go on the end.</para>
    ///
    /// <para><b>The lettering is not the name.</b> Fore-edge chips clip at
    /// <see cref="HiddenHarbours.Core.NotebookKit.ChipChars"/> = 5, so the stubs read
    /// LOTS · TRADE · TOOLS · RIGS · BOATS · GEAR — <c>RIGS</c> for vehicles because that is what a
    /// truck is called on this coast, and <c>TRADE</c> for businesses because "BUSIN" is not a word.
    /// <see cref="CatalogSections.ChipFor"/> is the one place that mapping lives.</para>
    /// </summary>
    public enum CatalogSection
    {
        /// <summary>Land for sale. Reserved now, authored when M2-42 lands (owner ruling R5).</summary>
        Lots = 0,
        /// <summary>Going concerns for sale. Reserved now, authored when M2-42 lands (R5).</summary>
        Businesses = 1,
        /// <summary>Hand tools and working gear that is not fishing tackle.</summary>
        Tools = 2,
        /// <summary>Road vehicles.</summary>
        Vehicles = 3,
        /// <summary>Hulls, and the yard work on them.</summary>
        Boats = 4,
        /// <summary>Tackle, bait, ice, licences, instruments — the counter's everyday stock.</summary>
        Gear = 5,
    }

    /// <summary>The section enum's lettering and parsing, in one place so the book and the authoring
    /// side can never spell a stub two ways.</summary>
    public static class CatalogSections
    {
        /// <summary>Every section, in shelf order. The book draws stubs in this order and skips the ones
        /// a seller does not stock, so a chandler with three sections shows three stubs.</summary>
        public static readonly CatalogSection[] InOrder =
        {
            CatalogSection.Lots, CatalogSection.Businesses, CatalogSection.Tools,
            CatalogSection.Vehicles, CatalogSection.Boats, CatalogSection.Gear,
        };

        /// <summary>The fore-edge chip for a section — at most
        /// <see cref="HiddenHarbours.Core.NotebookKit.ChipChars"/> characters, because that is what a
        /// stub holds. (Loc-seam literals, the HudStrings convention: centralise now, route to loc
        /// tables when they land.)</summary>
        public static string ChipFor(CatalogSection section)
        {
            switch (section)
            {
                case CatalogSection.Lots:       return "LOTS";
                case CatalogSection.Businesses: return "TRADE";
                case CatalogSection.Tools:      return "TOOLS";
                case CatalogSection.Vehicles:   return "RIGS";
                case CatalogSection.Boats:      return "BOATS";
                default:                        return "GEAR";
            }
        }

        /// <summary>
        /// Parse a section named in authored data — a dialogue row's optional section pointer, which
        /// crosses the module line as a plain string because World may not name this enum.
        ///
        /// <para><b>Lenient on purpose.</b> Case-insensitive, and anything unrecognised (including
        /// empty) answers false rather than throwing: the book then opens on the first stub the seller
        /// actually stocks, which is a kinder failure than a conversation that cannot be had.</para>
        /// </summary>
        public static bool TryParse(string name, out CatalogSection section)
        {
            section = CatalogSection.Gear;
            if (string.IsNullOrEmpty(name)) return false;

            for (int i = 0; i < InOrder.Length; i++)
            {
                if (!string.Equals(name, InOrder[i].ToString(),
                                   System.StringComparison.OrdinalIgnoreCase)) continue;
                section = InOrder[i];
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// <b>THE CATALOG TAG</b> — the block that turns a Def asset into something a seller's book can
    /// list. One <c>[Serializable]</c> struct, added to the offer Defs that already exist.
    ///
    /// <para><b>The inversion this exists for.</b> Before this, stock was a SCENE fact: a stall listed
    /// whatever vendor components a level designer had stacked on it, so adding a boat to a yard meant
    /// editing a <c>.unity</c> file and the offer asset itself could never say where it was sold. Now
    /// the LISTING names the seller, so adding stock is one new asset — no scene edit, no prefab touch,
    /// no merge on a scene file (rule 9) — and one listing can be stocked by two ports without being
    /// duplicated.</para>
    ///
    /// <para><b>It fails CLOSED.</b> <see cref="Listed"/> defaults false, so nothing appears in any book
    /// until an asset says so, and importing a half-authored Def cannot quietly put it on a shelf.</para>
    /// </summary>
    [System.Serializable]
    public struct CatalogListing
    {
        [Tooltip("THE TAG. Off by default: a listing is invisible to every book until this is ticked, so " +
                 "a half-authored Def cannot quietly appear on a counter.")]
        public bool Listed;

        [Tooltip("Which shelf of the book this sits on. Append-only — re-ordering the enum re-shelves " +
                 "everything already authored.")]
        public CatalogSection Section;

        [Tooltip("The seller ids that stock it (seller.snake_case). One listing may name several: the " +
                 "same rod sold at two ports is one asset, not two. EMPTY is a draft — listed nowhere, " +
                 "which content validation reports as a listing nobody sells.")]
        public string[] Sellers;

        [Tooltip("Order within the section, low first. Ties break on the listing id, so leaving this at " +
                 "zero is still deterministic — it is not a shuffle.")]
        public int SortOrder;

        /// <summary>True when this listing is on a shelf at all.</summary>
        public bool IsListed => Listed;

        /// <summary>
        /// True when <paramref name="sellerId"/> stocks this listing.
        ///
        /// <para>Ordinal and case-sensitive, like every other id comparison in this codebase: ids are
        /// authored data with a stated spelling (<c>seller.snake_case</c>), and a case-insensitive match
        /// here would let two spellings of one seller both half-work.</para>
        /// </summary>
        public bool IsStockedBy(string sellerId)
        {
            if (!Listed || string.IsNullOrEmpty(sellerId) || Sellers == null) return false;
            for (int i = 0; i < Sellers.Length; i++)
                if (string.Equals(Sellers[i], sellerId, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>True when the tag names nobody — a listing that exists but is sold nowhere. Content
        /// validation makes this loud, because a listing nobody sells is exactly the silent empty tab.</summary>
        public bool IsOrphaned => Listed && (Sellers == null || Sellers.Length == 0);
    }

    /// <summary>
    /// <b>What every listable Def can be asked</b>, so the sweep and the book can read a rod, a hull and
    /// a licence through one shape without either of them learning the seven concrete types.
    ///
    /// <para>Implemented by the offer Defs themselves, all of which already live in Economy — no
    /// gameplay Def in another module grows a price to satisfy this (rule 4). A vehicle for sale is a
    /// <c>VehicleOffer</c> in Economy pointing at a <c>vehicle.*</c> id, exactly as
    /// <see cref="ShipwrightOffer"/> already does for hulls.</para>
    /// </summary>
    public interface ICatalogListing
    {
        /// <summary>The stable content id this listing is for (e.g. "boat.punt", "gear.rod").</summary>
        string ListingId { get; }

        /// <summary>What the row is called on the page.</summary>
        string ListingName { get; }

        /// <summary>The seller's own blurb, or empty. Drawn in her hand on the right leaf.</summary>
        string ListingFlavor { get; }

        /// <summary>The tag: whether it is listed, on which shelf, sold by whom, in what order.</summary>
        CatalogListing Catalog { get; }
    }
}
