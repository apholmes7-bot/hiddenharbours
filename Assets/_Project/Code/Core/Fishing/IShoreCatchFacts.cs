namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What a villager angler lands at a named spot</b>, for presentation only (ADR 0053 §11). Pure:
    /// equal inputs give the equal answer. Reads the same stock and season data the player's catch does;
    /// never removes a fish and never writes anything. Published by the Fishing module beside
    /// <see cref="IFishBehaviourFacts"/> (the adapter over the catch function lands with the first angler);
    /// read once per session slot by the angler's stand, which hands the answer to
    /// <see cref="AnglerTimeline.Evaluate"/>. The timeline itself never calls it.
    ///
    /// FLAG lead-architect: new Core contract (the shore catch seam).
    /// </summary>
    public interface IShoreCatchFacts
    {
        /// <summary>
        /// What bites at <paramref name="spotId"/> for this angler's session slot. False when nothing bites
        /// there then (season, a closed spot, an unknown id). <paramref name="slotStartTotalSeconds"/> is the
        /// slot's start in routine time (<see cref="IGameClock.TotalSeconds"/>), so the season read is the
        /// sim's.
        /// </summary>
        bool TryPick(string spotId, int worldSeed, uint anglerKeyHash, long slotIndex,
                     double slotStartTotalSeconds, out ShoreCatch shoreCatch);
    }

    /// <summary>One fish a villager angler lands: presentation only, never a fish taken from a stock.</summary>
    public readonly struct ShoreCatch
    {
        /// <summary>A fish Def id from <c>Data/Fish</c>.</summary>
        public readonly string FishId;
        public readonly float WeightKg;
        /// <summary>The same size read <see cref="FishingState.Size01"/> carries.</summary>
        public readonly float Size01;

        public ShoreCatch(string fishId, float weightKg, float size01)
        {
            FishId = fishId;
            WeightKg = weightKg;
            Size01 = size01;
        }
    }

    /// <summary>The "no Fishing module registered" answer: nothing bites anywhere, so a villager angler
    /// casts, waits and reels in empty.</summary>
    public sealed class EmptyShoreCatchFacts : IShoreCatchFacts
    {
        public static readonly EmptyShoreCatchFacts Instance = new EmptyShoreCatchFacts();
        private EmptyShoreCatchFacts() { }

        public bool TryPick(string spotId, int worldSeed, uint anglerKeyHash, long slotIndex,
                            double slotStartTotalSeconds, out ShoreCatch shoreCatch)
        {
            shoreCatch = default;
            return false;
        }
    }
}
