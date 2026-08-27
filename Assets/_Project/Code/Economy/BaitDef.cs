using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// A bait, as data (ADR 0003) — herring, mackerel, fish scrap. One asset = one bait. Content is data,
    /// not code: add a bait by creating one of these assets, never by hard-coding a price or a preference.
    /// Create via Assets ▸ Create ▸ Hidden Harbours ▸ Bait, save in Data/Bait. Trap-fishing design:
    /// design/fish-and-content.md §3.5b.
    ///
    /// <para><b>Lives in Economy, not Fishing, on purpose.</b> A future bait shop is economy's, and the
    /// asmdefs run one way — <c>HiddenHarbours.Fishing</c> references <c>HiddenHarbours.Economy</c>, never
    /// the reverse. So a <see cref="TrapDef"/> in Fishing can refer to a bait by its stable <c>Id</c>
    /// (a string), and both a trap and a bait-shop offer resolve that id — with no backwards module
    /// dependency. The catch resolver (trap arc Build 3) reads <see cref="FavorsSpeciesIds"/> to
    /// soft-weight which species a baited trap lands.</para>
    ///
    /// <para><b>Price is a greybox placeholder</b> — flagged for economy-sim to tune against the market.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Bait", fileName = "Bait")]
    public class BaitDef : ScriptableObject, ICatalogListing
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only bait id (e.g. \"bait.herring\"). A trap names this id as its " +
                 "RequiredBaitId. Never reuse or rename.")]
        public string Id = "bait.herring";
        public string DisplayName = "Herring";
        [TextArea] public string Flavor = "Oily, cheap, and irresistible to a lobster. The pot-fisher's staple.";

        [Header("Cost")]
        [Min(0)]
        [Tooltip("Price in ₲ of ONE bait at a neutral market. Per-bait, not per-lot: the pacing model " +
                 "divides by it (m1-progression-pacing §7), so it must stay the unit price.")]
        public int Price = 3;

        [Min(1)]
        [Tooltip("How many baits one purchase at a counter buys (nobody buys a single capelin). The lot " +
                 "costs Price × LotSize. A shop convenience only — it never changes the unit price the " +
                 "pacing model reads. Clamped to at least 1 at read time, so an old asset that predates " +
                 "this field still sells singly rather than nothing.")]
        public int LotSize = 10;

        [Header("What it draws")]
        [Tooltip("Stable FishSpeciesDef ids this bait favours (each must name a real FishSpeciesDef — " +
                 "content validation checks this). The Build 3 resolver soft-weights the catch off this: a " +
                 "trap baited with something a species favours is likelier to land it.")]
        public string[] FavorsSpeciesIds = { "fish.lobster" };

        [Header("Catalog")]
        [Tooltip("Whether a seller's wares book lists this, on which shelf, and who stocks it. Off by " +
                 "default: nothing appears in any book until this says so.")]
        public CatalogListing Catalog;

        // ---- ICatalogListing: read through one shape by the sweep and the book, so neither of them
        //      has to learn the seven concrete Def types.
        string ICatalogListing.ListingId => Id;
        string ICatalogListing.ListingName => DisplayName;
        string ICatalogListing.ListingFlavor => Flavor;
        CatalogListing ICatalogListing.Catalog => Catalog;
    }
}
