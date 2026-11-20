namespace HiddenHarbours.Core
{
    /// <summary>Per-owned-boat purchase and installation state. No save implementation yet.
    /// Calls and mutations belong on the simulation thread.</summary>
    public interface IAutopilotFitments
    {
        bool IsPurchased(string ownedBoatId);
        bool IsFitted(string ownedBoatId);

        /// <summary>Record a paid purchase or refit an already purchased unit. The fitting service
        /// validates eligibility and pays before calling this; valid ids must be accepted.</summary>
        void Fit(string ownedBoatId);

        /// <summary>Restore unfitted state, retaining the purchase for a free refit on this boat.
        /// True only when an installed unit was removed. No money or inventory transfer.</summary>
        bool Remove(string ownedBoatId);
    }
}
