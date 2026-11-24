using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// One fishing/gear license, as data (ADR 0003) — the legal right to fish a species or use a gear
    /// class (design/progression-and-housing.md §2.2). The St Peters opening sells the FIRST one, the
    /// <b>cod license</b>, at Nine Mile Creek: pay the fee, and the rod may then take cod. Content is data, not
    /// code — add a license by creating one of these assets, never by hard-coding a price or a flag.
    /// Create via Assets ▸ Create ▸ Hidden Harbours ▸ License, save in Data/Licenses.
    ///
    /// <para><b>Minimal but structured to extend.</b> Today a license is money-only: <see cref="Price"/>
    /// is the only gate. The full proficiency/reputation eligibility tower (progression-and-housing
    /// §2.1/§2.3) is deliberately NOT modelled here yet — when it lands it adds fields to this asset and
    /// a check in the vendor, without changing the <c>ILicenseService</c> contract or the save shape.
    /// <see cref="PermittedSpeciesIds"/> IS the runtime catch gate, not flavour: a species listed here
    /// needs this licence to be kept, and a species no licence lists is free
    /// (<see cref="CatchLicensePolicy.RequiredLicenseFor"/>). Every landing path (rod, dig, trap haul)
    /// reads it through one shared rule; a species caught without the licence is released at once and
    /// not recorded (owner ruling 2026-11-23). Authoring this list is the only place a gate is declared.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/License", fileName = "License")]
    public class LicenseDef : ScriptableObject, ICatalogListing
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only license id (e.g. \"license.cod\"). Saved in the license wallet; " +
                 "a fish that requires a license names this id. Never reuse or rename.")]
        public string Id = "license.cod";
        [Tooltip("Player-facing name shown in the buy UI (ui-ux). Flavour only; the id is canonical.")]
        public string DisplayName = "Cod Fishing License";
        [TextArea] public string Flavor = "Nine Mile Creek's harbourmaster signs you off to take cod on rod and line.";

        [Header("Cost")]
        [Min(0)]
        [Tooltip("Licence fee in ₲. The economy-owned tunable — what the vendor charges (no magic number " +
                 "in code).")]
        public int Price = 120;

        [Header("What it permits (the runtime catch gate)")]
        [Tooltip("Stable FishSpeciesDef ids this licence legally unlocks (e.g. \"fish.atlantic_cod\"). " +
                 "This list IS the gate: a species listed here is released at the landing (rod, dig or " +
                 "trap) unless the player holds this licence. A species no licence lists is free.")]
        public string[] PermittedSpeciesIds = { "fish.atlantic_cod" };

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
