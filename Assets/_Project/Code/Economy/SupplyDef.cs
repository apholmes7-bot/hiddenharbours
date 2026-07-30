using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// A consumable SUPPLY, as data (ADR 0003) — ice first; §7.3's lids and later fuel cans append beside it.
    /// One asset = one supply. Content is data, not code: add a supply by creating one of these assets, never
    /// by hard-coding a price. Create via Assets ▸ Create ▸ Hidden Harbours ▸ Supply, save in Data/Supplies.
    ///
    /// <para><b>Why a new Def and not a <see cref="GearOffer"/>.</b> Gear is PRESENCE-owned — you have a rod
    /// or you don't, and buying a second is meaningless (<c>GearShop</c> refuses it). A supply is COUNTED and
    /// spent: you buy ice again every trip, which is exactly what makes the general store a repeat
    /// destination rather than a one-visit shop (plan-to-m1 §7.5/§7.3). That split is the same one
    /// <c>TackleBox</c> draws between bait and tackle, and it is load-bearing enough to deserve its own type
    /// rather than a bool on the gear offer.</para>
    ///
    /// <para><b>What this asset does NOT decide.</b> How long a bag of ice protects a catch is the freshness
    /// system's business (protection is a DURATION, not a state — <c>Freshness.SettleThroughProtection</c>),
    /// and the interactable that applies it to a container is still to build (§7.3's "ice and lids" bullet).
    /// This Def is the ECONOMIC half only: a stable id, a player-facing name, and a price. Adding the
    /// protection duration here later is an additive field, not a reshape.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Supply", fileName = "Supply")]
    public class SupplyDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only supply id (e.g. \"supply.ice\"). Counted in SaveData.SupplyStock " +
                 "through SupplyLocker. Never reuse or rename.")]
        public string Id = "supply.ice";
        [Tooltip("Player-facing name shown in the buy UI (ui-ux). Flavour only; the id is canonical.")]
        public string DisplayName = "Bag of Ice";
        [TextArea] public string Flavor = "Crushed ice by the bag. Packed round a catch it buys you hours, not days - and it is melting from the moment you pay for it.";

        [Header("Cost")]
        [Min(0)]
        [Tooltip("Price in ₲. The economy-owned tunable — what the counter charges (no magic number in " +
                 "code). Ice is a RECURRING cost, so this number sets the running cost of keeping a catch " +
                 "cold away from Ginny's freezer (m1-progression-pacing §5).")]
        public int Price = 6;
    }
}
