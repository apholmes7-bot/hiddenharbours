using System;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// Promises use the seller's existing supply calendar. Availability alone is not delivery:
    /// the host separately reports the last completed unloading at that counter at the given time.
    /// Both callbacks must be deterministic for the same world and game time, without side effects.
    /// </summary>
    public sealed class SpecialOrderSupplySchedule
    {
        private readonly Func<string, int, bool> _runsOn;
        private readonly Func<string, double, int?> _lastCompletedRun;
        private readonly int _searchDays;

        /// <param name="runsOn">The seller's IslandStoreLedger.RunsOn (or the same IslandRunCalendar policy).</param>
        /// <param name="lastCompletedRun">Last run day actually unloaded by game seconds, or null.
        /// Supply this from the delivery host; a possible crossing is not a completed run.</param>
        /// <param name="searchDays">Positive, caller-configured bound on days examined for a new promise.
        /// With no run in this window the order is refused before payment.</param>
        public SpecialOrderSupplySchedule(Func<string, int, bool> runsOn,
            Func<string, double, int?> lastCompletedRun, int searchDays)
        {
            _runsOn = runsOn ?? throw new ArgumentNullException(nameof(runsOn));
            _lastCompletedRun = lastCompletedRun ?? throw new ArgumentNullException(nameof(lastCompletedRun));
            if (searchDays <= 0) throw new ArgumentOutOfRangeException(nameof(searchDays));
            _searchDays = searchDays;
        }

        public bool TryPromise(string sellerId, int earliestDay, out int dueDay)
        {
            dueDay = 0;
            if (earliestDay < 0) return false;
            long end = Math.Min(int.MaxValue, (long)earliestDay + _searchDays - 1);
            for (long day = earliestDay; day <= end; day++)
                if (_runsOn(sellerId, (int)day))
                {
                    dueDay = (int)day;
                    return true;
                }
            return false;
        }

        public bool HasDelivered(string sellerId, int dueDay, int currentDay, double seconds)
        {
            if (dueDay < 0 || currentDay < dueDay || seconds < 0
                || double.IsNaN(seconds) || double.IsInfinity(seconds)) return false;
            int? completed = _lastCompletedRun(sellerId, seconds);
            return completed.HasValue && completed.Value >= dueDay && completed.Value <= currentDay
                && _runsOn(sellerId, completed.Value);
        }
    }
}
