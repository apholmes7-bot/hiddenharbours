using System;

namespace HiddenHarbours.Fishing
{
    public enum TrawlSortDestination { KeepOnDeck, Release }

    /// <summary>Manual sorting ledger for an unwired codend proposal. Kept catch stays ON DECK;
    /// nothing calls a hold, storage, market, save, or living-occupant transaction.</summary>
    public sealed class TrawlDeckBatch
    {
        private readonly TrawlTowResult _catch;
        private readonly TrawlHandlingDef _handling;
        private readonly int[] _unsorted, _kept, _released;
        private double _lastWorkTime;
        public int UnsortedCount { get; private set; }
        public int KeptOnDeckCount { get; private set; }
        public int ReleasedCount { get; private set; }
        public bool ReadyToCarry => UnsortedCount == 0;

        public TrawlDeckBatch(TrawlTowResult catchProposal, in TrawlHandlingDef handling, double codendOnDeckGameSeconds)
        {
            if (catchProposal == null || catchProposal.Status != TrawlTowStatus.Complete)
                throw new ArgumentException("Sorting needs a complete catch proposal.", nameof(catchProposal));
            if (!handling.IsValid) throw new ArgumentException("Invalid handling definition.", nameof(handling));
            if (!TrawlNumbers.NonNegative(codendOnDeckGameSeconds)) throw new ArgumentOutOfRangeException(nameof(codendOnDeckGameSeconds));
            _catch = catchProposal; _handling = handling; _lastWorkTime = codendOnDeckGameSeconds;
            _unsorted = new int[_catch.LotCount]; _kept = new int[_catch.LotCount]; _released = new int[_catch.LotCount];
            for (int i = 0; i < _unsorted.Length; i++) _unsorted[i] = _catch[i].Count;
            UnsortedCount = _catch.Count;
        }

        public int UnsortedAt(int lot) => _unsorted[lot];
        public int KeptOnDeckAt(int lot) => _kept[lot];
        public int ReleasedAt(int lot) => _released[lot];

        public bool SortByHand(double gameSeconds, int lot, int individuals, TrawlSortDestination destination)
        {
            if (!TrawlNumbers.Finite(gameSeconds) || gameSeconds < _lastWorkTime
                || lot < 0 || lot >= _unsorted.Length || individuals <= 0 || individuals > _unsorted[lot]
                || (destination != TrawlSortDestination.KeepOnDeck && destination != TrawlSortDestination.Release)
                || gameSeconds - _lastWorkTime < individuals / _handling.SortIndividualsPerGameSecond) return false;
            _lastWorkTime = gameSeconds;
            _unsorted[lot] -= individuals; UnsortedCount -= individuals;
            if (destination == TrawlSortDestination.KeepOnDeck)
            { _kept[lot] += individuals; KeptOnDeckCount += individuals; }
            else { _released[lot] += individuals; ReleasedCount += individuals; }
            return true;
        }
    }
}
