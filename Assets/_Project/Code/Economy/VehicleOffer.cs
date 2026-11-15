using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// One land vehicle offered for sale: which machine (by <c>VehicleDef</c> id), who sells it, and its
    /// price in ₲. The <see cref="ShipwrightOffer"/> shape for road and off-road machines. Content is
    /// data, not code (ADR 0003): stock a lot by adding one of these assets, never by hard-coding a price.
    ///
    /// <para>The machine's handling, mesh and camera live in the Vehicles module's <c>VehicleDef</c>
    /// (gameplay-systems); this offer names it only by <see cref="VehicleId"/>, so Economy never
    /// references Vehicles (rule 4). The price and the repair cost are the economy-owned tunables.</para>
    ///
    /// <para><b>Its own stable <see cref="Id"/>, separate from the model id</b>, because the deed is
    /// recorded against the offer (<c>deed.vehicleoffer.*</c>): two offers for the same model are two
    /// machines. Ids are append-only and stable once shipped.</para>
    ///
    /// <para>Two sellers stock these today: the NMC car and truck dealer
    /// (<c>seller.nmc_vehicle_dealer</c>) and the NMC industrial seller and repair shop
    /// (<c>seller.nmc_industrial_yard</c>). Seller ids are data on the asset.</para>
    ///
    /// <para><b>HOOK (dealer counter, slice V2/V3):</b> the shipped offers have
    /// <see cref="CatalogListing.Listed"/> OFF. The store wiring test requires every Listed ware to be
    /// sold by a committed vendor in a scene, and no dealer vendor exists yet. Tick Listed in the same
    /// change that places the dealer and industrial yard counters (seller ids above), and add a
    /// <c>CatalogSource.Vehicles()</c> accessor beside <c>Boats()</c> for the book's RIGS shelf.</para>
    ///
    /// Create via Assets ▸ Create ▸ Hidden Harbours ▸ Vehicle Offer, save in
    /// Data/Resources/Catalog/Vehicles.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Vehicle Offer", fileName = "VehicleOffer")]
    public class VehicleOffer : ScriptableObject, ICatalogListing
    {
        [Tooltip("Stable id of this offer (vehicleoffer.snake_case). The deed is recorded against it. " +
                 "Append-only: never rename a shipped id.")]
        public string Id = "vehicleoffer.unnamed";

        [Tooltip("Stable id of the machine this offer sells, matching its VehicleDef.Id (e.g. \"vehicle.modern_3500\").")]
        public string VehicleId = "vehicle.unnamed";

        [Tooltip("Name shown in the buy UI (ui-ux). Flavor only; the machine's canonical name is on its VehicleDef.")]
        public string DisplayName = "Unnamed Vehicle";

        [Tooltip("The seller's own blurb for the book, or empty.")]
        public string Flavor = "";

        [Tooltip("Who sells it (seller.snake_case): the counter the purchase is made at, named by the " +
                 "VehiclePurchased signal.")]
        public string SellerId = "seller.nmc_vehicle_dealer";

        [Min(0)]
        [Tooltip("Purchase price in ₲. The economy-owned tunable for this offer.")]
        public int Price = 1000;

        [Header("Damaged → repair (the repair shop)")]
        [Tooltip("If true, this machine is sold as a DAMAGED used vehicle: owned on purchase but it does " +
                 "not run until the player pays RepairCost at the repair shop. One paid repair, no wear. " +
                 "A normal offer (false) runs the moment it is bought.")]
        public bool StartsDamaged = false;

        [Min(0)]
        [Tooltip("₲ to repair this machine into a running one (only used when StartsDamaged).")]
        public int RepairCost = 0;

        [Header("Catalog")]
        [Tooltip("Whether a seller's wares book lists this, on which shelf (Vehicles), and who stocks it. " +
                 "Off until the counter that sells it exists in a scene.")]
        public CatalogListing Catalog;

        // ---- ICatalogListing: the deed and the book both key on the offer id.
        string ICatalogListing.ListingId => Id;
        string ICatalogListing.ListingName => DisplayName;
        string ICatalogListing.ListingFlavor => Flavor;
        CatalogListing ICatalogListing.Catalog => Catalog;
    }
}
