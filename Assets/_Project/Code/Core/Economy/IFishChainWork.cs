namespace HiddenHarbours.Core
{
    public enum FishProduct { Raw, FreshFillet, Frozen, Canned, Lobster }

    public readonly struct FishChainTransfer
    {
        public readonly string WorkId, SourceId, DestinationId, SpeciesId;
        public readonly FishProduct Product;
        public readonly int IndividualRawGrams, IndividualGrams, IndividualVolumeMl, RequestedFish;
        public readonly long GameSeconds;

        public FishChainTransfer(string workId, string sourceId, string destinationId, string speciesId,
            FishProduct product, int rawGrams, int grams, int volumeMl, int requestedFish, long gameSeconds)
        {
            WorkId = workId; SourceId = sourceId; DestinationId = destinationId; SpeciesId = speciesId;
            Product = product; IndividualRawGrams = rawGrams; IndividualGrams = grams;
            IndividualVolumeMl = volumeMl; RequestedFish = requestedFish; GameSeconds = gameSeconds;
        }
    }

    /// <summary>Integration ports only. No implementation is installed by the isolated plant model.</summary>
    public interface IFishChainHands
    {
        /// <summary>
        /// Return completed, previously unreported physical hand transfers, at most requestedFish.
        /// The worker must be present and the named source/destination reachable and supported.
        /// A zero response leaves inventory where it is. Implementations must not allocate or throw.
        /// </summary>
        int ConfirmTransfer(in FishChainTransfer transfer);
    }

    public interface IFishChainCarrier
    {
        /// <summary>False for unresolved routes, inaccessible tide gates or absent vehicles/drivers.</summary>
        bool CanLoad(string runId, string routeId, long gameSeconds);

        /// <summary>Confirm actual arrival, including the island boundary for an export destination.</summary>
        bool HasArrived(string runId, bool returning, long departedAt, long gameSeconds);
    }

    public readonly struct FishChainPressure
    {
        public readonly string WharfId, SpeciesId;
        public readonly long LandedFish, WaitingFish, FreeIntakeGrams, GameSeconds;
        public readonly bool PlantBlocked;

        public FishChainPressure(string wharfId, string speciesId, long landedFish, long waitingFish,
            long freeIntakeGrams, bool plantBlocked, long gameSeconds)
        {
            WharfId = wharfId; SpeciesId = speciesId; LandedFish = landedFish;
            WaitingFish = waitingFish; FreeIntakeGrams = freeIntakeGrams;
            PlantBlocked = plantBlocked; GameSeconds = gameSeconds;
        }
    }

    public interface IFishChainMarket
    {
        /// <summary>
        /// A cumulative, idempotent snapshot, not a fresh sale on every call. Economy maps species
        /// to categories and deltas to its existing market API. Never credits the player wallet.
        /// Implementations must not allocate or throw during a tick.
        /// </summary>
        void Report(in FishChainPressure pressure);
    }
}
