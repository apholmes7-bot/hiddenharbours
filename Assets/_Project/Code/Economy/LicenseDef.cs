using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// One fishing/gear license, as data (ADR 0003) — the legal right to fish a species or use a gear
    /// class (design/progression-and-housing.md §2.2). The St Peters opening sells the FIRST one, the
    /// <b>cod license</b>, at Greywick: pay the fee, and the rod may then take cod. Content is data, not
    /// code — add a license by creating one of these assets, never by hard-coding a price or a flag.
    /// Create via Assets ▸ Create ▸ Hidden Harbours ▸ License, save in Data/Licenses.
    ///
    /// <para><b>Minimal but structured to extend.</b> Today a license is money-only: <see cref="Price"/>
    /// is the only gate. The full proficiency/reputation eligibility tower (progression-and-housing
    /// §2.1/§2.3) is deliberately NOT modelled here yet — when it lands it adds fields to this asset and
    /// a check in the vendor, without changing the <c>ILicenseService</c> contract or the save shape.
    /// <see cref="PermittedSpeciesIds"/> is informational/flavour for the buy UI and validation (which
    /// species this unlocks); the runtime catch-gate keys off the license <see cref="Id"/> the species
    /// requires, not this list, so the two can't silently drift the gate.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/License", fileName = "License")]
    public class LicenseDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only license id (e.g. \"license.cod\"). Saved in the license wallet; " +
                 "a fish that requires a license names this id. Never reuse or rename.")]
        public string Id = "license.cod";
        [Tooltip("Player-facing name shown in the buy UI (ui-ux). Flavour only; the id is canonical.")]
        public string DisplayName = "Cod Fishing License";
        [TextArea] public string Flavor = "Greywick's harbourmaster signs you off to take cod on rod and line.";

        [Header("Cost")]
        [Min(0)]
        [Tooltip("Licence fee in ₲. The economy-owned tunable — what the vendor charges (no magic number " +
                 "in code).")]
        public int Price = 120;

        [Header("What it permits (informational for the buy UI / validation)")]
        [Tooltip("Stable FishSpeciesDef ids this licence legally unlocks (e.g. \"fish.atlantic_cod\"). " +
                 "The runtime gate keys off the licence id the SPECIES requires; this list is the buy-UI " +
                 "blurb and the validation cross-check, so authoring stays legible.")]
        public string[] PermittedSpeciesIds = { "fish.atlantic_cod" };
    }
}
