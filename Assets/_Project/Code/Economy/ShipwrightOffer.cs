using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// One boat offered for sale at the Shipwright: which boat (by stable id) and its price in ₲.
    /// Content is data, not code (ADR 0003) — add a boat to the showroom by creating one of these
    /// assets, never by hard-coding a price. The boat's <i>stats</i> live in the Boats module's
    /// <c>BoatHullDef</c> (gameplay-systems); this offer references the boat only by <see cref="BoatId"/>,
    /// so Economy never depends on Boats. The price is the economy-owned tunable.
    /// Create via Assets ▸ Create ▸ Hidden Harbours ▸ Shipwright Offer, save in Data/Shipwright.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Shipwright Offer", fileName = "ShipwrightOffer")]
    public class ShipwrightOffer : ScriptableObject
    {
        [Tooltip("Stable id of the boat this offer sells, matching its BoatHullDef.Id (e.g. \"boat.punt\").")]
        public string BoatId = "boat.punt";
        [Tooltip("Name shown in the buy UI (ui-ux). Flavor only; the boat's canonical name is on its hull def.")]
        public string DisplayName = "The Punt";
        [Min(0)]
        [Tooltip("Purchase price in ₲. The economy-owned tunable for this offer.")]
        public int Price = 1800;
    }
}
