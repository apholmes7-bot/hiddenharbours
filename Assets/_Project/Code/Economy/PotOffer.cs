using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// One purchasable POT/TRAP kind at the shipwright, as data (ADR 0003) — the economic side of the
    /// trap gear: a stable offer id, which TrapDef it stocks, a display name, and a price. The sibling
    /// of <see cref="GearOffer"/> for COUNTED stock: gear is a presence-only wallet (you own a rod or
    /// you don't), pots are finite physical inventory you buy again and again — so a pot purchase
    /// increments <c>SaveData.PotStock</c> (via <c>PotLocker</c>) instead of joining OwnedGear, and the
    /// offer never sells out. This is the P2 money wheel's first turn: catch lobster → afford more pots
    /// → catch more lobster.
    ///
    /// <para><b>Lane boundary.</b> Purchase economics only. The pot's CAPABILITY — soak, depth band,
    /// catch list, the deck work — is the Fishing lane's <c>TrapDef</c>, referenced here by stable id
    /// only (Economy never references Fishing types, rule 4). Price pays for itself in ~2 good hauls
    /// (a full lobster pot sorts to ≈ ₲70 — the #194 balance note). Create via
    /// Assets ▸ Create ▸ Hidden Harbours ▸ Pot Offer, save in Data/Shipwright.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Pot Offer", fileName = "PotOffer")]
    public class PotOffer : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only offer id (e.g. \"offer.lobster_pot\"). Never reuse or rename.")]
        public string Id = "offer.lobster_pot";
        [Tooltip("Stable TrapDef id of the pot KIND this offer sells (e.g. \"trap.lobster\") — the id " +
                 "the owned stock is counted under in the save. Must name an authored TrapDef (content " +
                 "validation checks this).")]
        public string TrapDefId = "trap.lobster";
        [Tooltip("Player-facing name shown in the buy UI. Flavour only; the ids are canonical.")]
        public string DisplayName = "Lobster Pot";
        [TextArea] public string Flavor = "A slatted timber pot. Bait her, set her deep, come back to the buoy.";

        [Header("Cost")]
        [Min(0)]
        [Tooltip("Price in ₲ per pot. The economy-owned tunable — aim for a pot that pays for itself " +
                 "in about two good hauls (no magic number in code).")]
        public int Price = 120;
    }
}
