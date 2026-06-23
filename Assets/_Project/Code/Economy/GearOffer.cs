using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// One purchasable piece of gear/equipment, as data (ADR 0003) — the ECONOMIC side of a gear item:
    /// a stable id, a display name, and a price. The St Peters opening sells the <b>rod</b> and the
    /// <b>shovel</b> as these. Content is data, not code — add gear to a shop by creating one of these
    /// assets, never by hard-coding a price.
    ///
    /// <para><b>Lane boundary.</b> This is purchase + ownership economics only. The gear's <i>capability</i>
    /// — which <c>Gear</c> flag the rod maps to, the shovel's dig method, the bucket-hold's capacity — is
    /// gameplay-systems' (the <c>Gear</c> enum and hold mechanics live in their modules). They map an
    /// owned-gear <see cref="Id"/> to that capability; Economy never references the Fishing/Boats gear
    /// types. The rod only fishes cod once the cod license is also held (the licence gate, separate from
    /// owning the rod). Create via Assets ▸ Create ▸ Hidden Harbours ▸ Gear Offer, save in Data/Gear.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Gear Offer", fileName = "GearOffer")]
    public class GearOffer : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only gear id (e.g. \"gear.rod\", \"gear.shovel\"). Saved in the owned-gear " +
                 "wallet; gameplay-systems maps it to a Gear capability. Never reuse or rename.")]
        public string Id = "gear.rod";
        [Tooltip("Player-facing name shown in the buy UI (ui-ux). Flavour only; the id is canonical.")]
        public string DisplayName = "Fishing Rod";
        [TextArea] public string Flavor = "A proper rod and reel - the step up from a hand-line.";

        [Header("Cost")]
        [Min(0)]
        [Tooltip("Price in ₲. The economy-owned tunable — what the shop charges (no magic number in code).")]
        public int Price = 60;
    }
}
