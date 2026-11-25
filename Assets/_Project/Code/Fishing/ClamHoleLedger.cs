using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The save-side record of spent clam holes (<see cref="SaveData.SpentClamHoles"/>): which hole was
    /// spent, and the game time it happened at. Pure helpers over the save blob, so the rule is testable
    /// without a scene and <see cref="ClamDig"/> stays a gate-and-yield.
    ///
    /// <para><b>The rule.</b> A hole is spent for one tide. Whether "the tide it was spent in" is still
    /// the tide now is never stored: it is asked of the deterministic tide through Core
    /// (<see cref="IEnvironmentService.TideCycleIndexAt"/>), so it reads the same after a save and load
    /// as before (rule 5). The save keeps the timestamp and nothing else.</para>
    ///
    /// <para><b>Bounded.</b> One row per hole id (a re-spend overwrites), and every write drops the rows
    /// left over from earlier tides, so the list never outgrows the holes spent on the current tide.
    /// Nothing here runs per frame: reads happen on enable and on load, writes on a dig.</para>
    /// </summary>
    public static class ClamHoleLedger
    {
        /// <summary>Do two moments belong to the same tide? With no environment service there is no
        /// tide to turn, so the answer is yes: a spent hole stays spent, which is how a hole behaved
        /// before the tide was asked.</summary>
        public static bool SameTide(IEnvironmentService environment, double secondsA, double secondsB)
            => environment == null
               || environment.TideCycleIndexAt(secondsA) == environment.TideCycleIndexAt(secondsB);

        /// <summary>When was this hole last spent, according to the save? False if the save has no row
        /// for it (it has not been spent, or the row belonged to an earlier tide and was dropped).</summary>
        public static bool TryGetSpentAt(SaveData save, string holeId, out double spentAtSeconds)
        {
            spentAtSeconds = 0.0;
            if (save?.SpentClamHoles == null || string.IsNullOrEmpty(holeId)) return false;

            List<SpentClamHoleDto> rows = save.SpentClamHoles;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].HoleId != holeId) continue;
                spentAtSeconds = rows[i].SpentAtGameSeconds;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Record that <paramref name="holeId"/> was spent at <paramref name="atSeconds"/>: overwrite its
        /// row (or add one), and drop every row that belongs to an earlier tide than this one — those
        /// holes are fresh again and their rows say nothing. No-op without a save or an id.
        /// </summary>
        public static void MarkSpent(SaveData save, string holeId, double atSeconds,
                                     IEnvironmentService environment)
        {
            if (save == null || string.IsNullOrEmpty(holeId)) return;

            List<SpentClamHoleDto> rows = save.SpentClamHoles ??= new List<SpentClamHoleDto>();
            bool written = false;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                SpentClamHoleDto row = rows[i];
                if (row.HoleId == holeId)
                {
                    if (written) { rows.RemoveAt(i); continue; }   // a duplicate row: keep one
                    rows[i] = new SpentClamHoleDto(holeId, atSeconds);
                    written = true;
                    continue;
                }
                if (!SameTide(environment, row.SpentAtGameSeconds, atSeconds)) rows.RemoveAt(i);
            }
            if (!written) rows.Add(new SpentClamHoleDto(holeId, atSeconds));
        }
    }
}
