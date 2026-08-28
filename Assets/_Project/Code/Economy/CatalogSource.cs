using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>WHERE A SELLER'S STOCK COMES FROM in a running game</b> — the shipped listing Defs, swept out
    /// of Resources and filtered to one seller. The content half of the inversion
    /// (<see cref="CatalogListing"/>): the listing names the seller, so nothing here reads a scene.
    ///
    /// <para><b>Resolved on every open, never cached</b> — <c>NotebookContentSource</c>'s rule, and its
    /// reasoning transfers verbatim: a listing added while the book was shut is simply there next time,
    /// with no cache to invalidate and no subscription to leak. The cost is a <c>Resources.LoadAll</c>
    /// per open of a handful of small assets, on a page the player is about to read, which is not a
    /// frame-budget item (rule 7).</para>
    ///
    /// <para><b>Sorted, always.</b> <c>Resources.LoadAll</c> makes no ordering promise, and a book whose
    /// rows shuffled between two opens of the same save would be a bug nobody could reproduce. Order is
    /// <see cref="CatalogListing.SortOrder"/> then the listing id, ordinal — so an unset SortOrder is
    /// still deterministic rather than arbitrary.</para>
    ///
    /// <para><b>⚠️ An asset authored outside the Resources root is INVISIBLE to every book.</b>
    /// <c>Resources.LoadAll</c> reaches only inside a folder literally named <c>Resources</c>, and there
    /// is no error when it finds nothing — only an empty shelf. That is the silent failure content
    /// validation exists to make loud, and <see cref="CatalogFolderPath"/> is the one spelling of the
    /// path both the loader and that check read.</para>
    /// </summary>
    public static class CatalogSource
    {
        /// <summary>The load key — the folder under any <c>Resources</c> root the listings live in.</summary>
        public const string CatalogFolder = "Catalog";

        /// <summary>Where that root sits on disk. Under <c>Data/</c> still, so the content validator's
        /// sweep of the data root reaches these exactly as before; under a <c>Resources</c> root as well,
        /// because a book opened by a conversation has no builder to bake references into a scene. Both
        /// facts have to be true at once, and this folder is where they both are.</summary>
        public const string ResourcesRoot = "Assets/_Project/Data/Resources";

        /// <summary>The catalog folder on disk — <see cref="ResourcesRoot"/> plus the load key.</summary>
        public const string CatalogFolderPath = ResourcesRoot + "/" + CatalogFolder;

        /// <summary>Every shipped hull offer, in a fixed order. Empty is a real answer.</summary>
        public static IReadOnlyList<ShipwrightOffer> Boats() => All<ShipwrightOffer>();

        /// <inheritdoc cref="Boats"/>
        public static IReadOnlyList<GearOffer> Gear() => All<GearOffer>();

        /// <inheritdoc cref="Boats"/>
        public static IReadOnlyList<PotOffer> Pots() => All<PotOffer>();

        /// <inheritdoc cref="Boats"/>
        public static IReadOnlyList<InstrumentOffer> Instruments() => All<InstrumentOffer>();

        /// <inheritdoc cref="Boats"/>
        public static IReadOnlyList<BaitDef> Bait() => All<BaitDef>();

        /// <inheritdoc cref="Boats"/>
        public static IReadOnlyList<SupplyDef> Supplies() => All<SupplyDef>();

        /// <inheritdoc cref="Boats"/>
        public static IReadOnlyList<LicenseDef> Licenses() => All<LicenseDef>();

        /// <summary>
        /// Every shipped listing of one type, sorted and null-free.
        ///
        /// <para>Not filtered by seller: the filter is <see cref="CatalogListing.IsStockedBy"/> at the
        /// point of use, so the same swept list serves the book, the content validator and a future
        /// for-sale app without any of them re-deriving the order.</para>
        /// </summary>
        public static List<T> All<T>() where T : Object, ICatalogListing
        {
            T[] loaded = Resources.LoadAll<T>(CatalogFolder);
            var list = new List<T>();
            if (loaded == null) return list;

            // A null slot comes back for an asset whose script failed to compile, and one of those
            // reaching the layout would be a NullReferenceException in the middle of drawing a page.
            for (int i = 0; i < loaded.Length; i++)
                if (loaded[i] != null) list.Add(loaded[i]);

            list.Sort(Compare);
            return list;
        }

        /// <summary>Those of them a given seller actually stocks, order preserved.</summary>
        public static List<T> For<T>(string sellerId) where T : Object, ICatalogListing
        {
            List<T> all = All<T>();
            var stocked = new List<T>();
            for (int i = 0; i < all.Count; i++)
                if (all[i].Catalog.IsStockedBy(sellerId)) stocked.Add(all[i]);
            return stocked;
        }

        /// <summary>Shelf order then id, both stable. Exposed so tests can assert the order rule itself
        /// rather than a particular content set's happens-to-be order.</summary>
        public static int Compare(ICatalogListing a, ICatalogListing b)
        {
            if (a == null) return b == null ? 0 : 1;
            if (b == null) return -1;

            int byOrder = a.Catalog.SortOrder.CompareTo(b.Catalog.SortOrder);
            if (byOrder != 0) return byOrder;
            return string.CompareOrdinal(a.ListingId ?? "", b.ListingId ?? "");
        }
    }
}
