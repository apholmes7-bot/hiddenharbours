using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>What a vehicle purchase or repair attempt came to. Append-only.</summary>
    public enum VehiclePurchaseResult
    {
        /// <summary>The money moved and the record was written.</summary>
        Done = 0,
        /// <summary>No offer, no wallet, no save, or an offer with an empty id: nothing was charged.</summary>
        Invalid = 1,
        /// <summary>The wallet could not cover it: nothing was charged or recorded.</summary>
        CannotAfford = 2,
        /// <summary>Buy: the deed is already held. Nothing was charged a second time.</summary>
        AlreadyOwned = 3,
        /// <summary>Repair: the player does not own this machine.</summary>
        NotOwned = 4,
        /// <summary>Repair: the machine already runs (bought sound, or already repaired).</summary>
        NothingToRepair = 5,
    }

    /// <summary>
    /// <b>SELLING A VEHICLE, as pure logic.</b> Spend through the Core wallet contract, record the deed
    /// (<see cref="VehicleDeeds"/>), publish <see cref="VehiclePurchased"/>. No MonoBehaviour, no UI, no
    /// scene: the dealer and industrial yard counters (slice V2/V3) call these.
    ///
    /// <para><b>Order matters and is the point.</b> Every refusal is decided BEFORE the wallet is
    /// touched, and the wallet's <see cref="IWallet.TrySpend"/> is atomic, so the player is never charged
    /// for nothing and never charged twice. A save service is required: a purchase with nowhere to write
    /// the deed would take the money and leave no record, so it is refused instead.</para>
    ///
    /// <para><b>HOOK (Vehicles module):</b> nothing hands the player a machine yet. A listener on
    /// <see cref="VehiclePurchased"/> in Vehicles spawns or unlocks it, and restores owned machines on
    /// load from <see cref="VehicleDeeds"/> (slice V3, with the parked-position save change).</para>
    /// </summary>
    public static class VehiclePurchase
    {
        /// <summary>
        /// Buy <paramref name="offer"/>. On success the price is spent, the deed is recorded, a sound
        /// machine is also marked repaired (so it runs), and <see cref="VehiclePurchased"/> is published.
        /// </summary>
        public static VehiclePurchaseResult TryBuy(VehicleOffer offer, IWallet wallet, ISaveService save)
        {
            if (offer == null || wallet == null || save == null || string.IsNullOrEmpty(offer.Id))
                return VehiclePurchaseResult.Invalid;
            if (VehicleDeeds.IsOwned(save, offer.Id)) return VehiclePurchaseResult.AlreadyOwned;
            if (offer.Price < 0) return VehiclePurchaseResult.Invalid;
            if (!wallet.TrySpend(offer.Price)) return VehiclePurchaseResult.CannotAfford;

            using (save.DeferWrites())
            {
                VehicleDeeds.Grant(save, offer.Id);
                if (!offer.StartsDamaged) VehicleDeeds.MarkRepaired(save, offer.Id);
            }

            EventBus.Publish(new VehiclePurchased(offer.Id, offer.VehicleId, offer.SellerId,
                                                  offer.Price, offer.StartsDamaged));
            return VehiclePurchaseResult.Done;
        }

        /// <summary>
        /// Pay <see cref="VehicleOffer.RepairCost"/> to make an owned damaged machine run. On success the
        /// repair is recorded and <see cref="VehicleRepaired"/> is published.
        /// </summary>
        public static VehiclePurchaseResult TryRepair(VehicleOffer offer, IWallet wallet, ISaveService save)
        {
            if (offer == null || wallet == null || save == null || string.IsNullOrEmpty(offer.Id))
                return VehiclePurchaseResult.Invalid;
            if (!VehicleDeeds.IsOwned(save, offer.Id)) return VehiclePurchaseResult.NotOwned;
            if (VehicleDeeds.IsRepaired(save, offer.Id)) return VehiclePurchaseResult.NothingToRepair;
            if (offer.RepairCost < 0) return VehiclePurchaseResult.Invalid;
            if (!wallet.TrySpend(offer.RepairCost)) return VehiclePurchaseResult.CannotAfford;

            VehicleDeeds.MarkRepaired(save, offer.Id);
            EventBus.Publish(new VehicleRepaired(offer.Id, offer.VehicleId, offer.SellerId, offer.RepairCost));
            return VehiclePurchaseResult.Done;
        }
    }
}
