using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>Pure purchase/fitting rule. Prices come from data at the call site. Nothing consumes
    /// this service yet; heading hold keeps its existing availability rule.</summary>
    public sealed class AutopilotFittingService : IAutopilotFittingService
    {
        readonly IAutopilotEligibility _eligibility;
        readonly IAutopilotFitments _fitments;

        public AutopilotFittingService(IAutopilotEligibility eligibility, IAutopilotFitments fitments)
        {
            _eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
            _fitments = fitments ?? throw new ArgumentNullException(nameof(fitments));
        }

        public bool CanFit(string ownedBoatId)
            => Valid(ownedBoatId) && _eligibility.CanTakeAutopilot(OwnedBoatId.HullOf(ownedBoatId));

        public bool IsFitted(string ownedBoatId) => Valid(ownedBoatId) && _fitments.IsFitted(ownedBoatId);

        public bool TryFit(string ownedBoatId, int price, IWallet wallet)
        {
            if (!CanFit(ownedBoatId) || price < 0) return false;
            if (!_fitments.IsPurchased(ownedBoatId) && (wallet == null || !wallet.TrySpend(price))) return false;
            _fitments.Fit(ownedBoatId);
            return true;
        }

        public bool Remove(string ownedBoatId) => Valid(ownedBoatId) && _fitments.Remove(ownedBoatId);

        static bool Valid(string id) => !string.IsNullOrWhiteSpace(id) && OwnedBoatId.IsValid(id);
    }
}
