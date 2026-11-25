namespace HiddenHarbours.Core
{
    /// <summary>Dormant fitting seam. Callers supply an owned boat id and the authored offer price;
    /// this does not grant boat ownership, drive a helm, transfer goods or write a save.</summary>
    public interface IAutopilotFittingService
    {
        bool CanFit(string ownedBoatId);
        bool IsFitted(string ownedBoatId);

        /// <summary>Fit if eligible and paid. Repeated fitting and refitting a purchased unit succeed
        /// without spending again. A first purchase requires a wallet and non-negative data price.</summary>
        bool TryFit(string ownedBoatId, int price, IWallet wallet);

        /// <summary>Remove the installation, preserving its purchase. No refund.</summary>
        bool Remove(string ownedBoatId);
    }
}
