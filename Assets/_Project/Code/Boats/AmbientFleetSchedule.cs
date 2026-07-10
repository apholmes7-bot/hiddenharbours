using System;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The PURE place → soak → haul <b>schedule</b> each ambient fisher lives by (canon M2-33, P3) —
    /// a function of the game clock, never a saved state machine (rule 5): join the session at any
    /// time and the fleet is exactly where the clock says it should be, buoys and all.
    ///
    /// <para><b>The day-fraction domain.</b> All timing is expressed in fractions of the game day
    /// (the clock's <c>DayFraction</c>), so the schedule follows the owner's day length with no unit
    /// conversion. A day divides into <c>slotsPerDay</c> work slots; in slot <c>n</c> a boat sails to
    /// her spot <c>n mod K</c> (K = spots per boat), lays alongside through the work window, does the
    /// work at the window's midpoint (the FLIP), and bears away for the next spot. Visits to a spot
    /// alternate: the first visit of the day PLACES a buoy, the next HAULS it (so the soak is exactly
    /// K slots), then places again — so <see cref="BuoyPresent"/> is closed-form: the parity of the
    /// latest completed visit. Each boat carries a seeded phase offset so the fleet's beats stagger.</para>
    ///
    /// <para><b>Day-scoped by design.</b> The cycle restarts at day rollover (spots re-plan daily —
    /// fishers shift grounds; see <see cref="AmbientFleetPlan"/>), which keeps every function here free
    /// of negative-time and cross-day edge cases: before a spot's first work of the day there is
    /// simply no buoy. Pure, engine-light, no allocation — EditMode-testable headless.</para>
    /// </summary>
    public static class AmbientFleetSchedule
    {
        /// <summary>
        /// The continuous slot coordinate <c>s</c> for a boat: whole part = slot index, fractional
        /// part = progress through the slot. <paramref name="phaseSlots"/> is the boat's seeded stagger
        /// (in slots, ≥ 0) so boats don't beat in unison.
        /// </summary>
        public static double SlotPosition(float dayFraction, int slotsPerDay, float phaseSlots)
            => (double)dayFraction * slotsPerDay + phaseSlots;

        /// <summary>The slot index at slot-position <paramref name="s"/> (floor).</summary>
        public static int SlotIndex(double s) => (int)Math.Floor(s);

        /// <summary>Which of the boat's <paramref name="spotCount"/> spots slot <paramref name="slot"/>
        /// works (round-robin, non-negative for any slot).</summary>
        public static int SpotForSlot(int slot, int spotCount)
        {
            if (spotCount <= 0) return 0;
            int j = slot % spotCount;
            return j < 0 ? j + spotCount : j;
        }

        /// <summary>
        /// The spot the boat should be HEADED FOR at slot-position <paramref name="s"/>: the current
        /// slot's spot until the work window closes (<paramref name="workEndFraction"/>), then the next
        /// slot's spot — she bears away as soon as the pot is worked (ruling: sail off, work the others).
        /// </summary>
        public static int TargetSpot(double s, int spotCount, float workEndFraction)
        {
            int n = SlotIndex(s);
            double f = s - n;
            return f <= workEndFraction ? SpotForSlot(n, spotCount) : SpotForSlot(n + 1, spotCount);
        }

        /// <summary>True while the boat should be holding alongside (the work window of the slot).</summary>
        public static bool IsWorking(double s, float workStartFraction, float workEndFraction)
        {
            double f = s - Math.Floor(s);
            return f >= workStartFraction && f <= workEndFraction;
        }

        /// <summary>The in-slot fraction where the place/haul FLIP lands: the work window's midpoint.</summary>
        public static float WorkFlipFraction(float workStartFraction, float workEndFraction)
            => 0.5f * (workStartFraction + Math.Max(workStartFraction, workEndFraction));

        /// <summary>
        /// Is a buoy out at spot <paramref name="spotIndex"/> at slot-position <paramref name="s"/>?
        /// Closed-form off the visit parity: work events at spot <c>j</c> complete at slot-positions
        /// <c>j + v·K + flip</c> (v = 0, 1, 2, …); the latest completed visit <c>v*</c> decides — even
        /// (a PLACE) means the buoy is soaking, odd (a HAUL) means it's aboard; none yet today means no
        /// buoy. Same clock ⇒ same answer, always — this is the fleet's determinism anchor.
        /// </summary>
        public static bool BuoyPresent(double s, int spotIndex, int spotCount, float workFlipFraction)
        {
            if (spotCount <= 0) return false;
            double v = Math.Floor((s - workFlipFraction - spotIndex) / spotCount);
            if (v < 0) return false;               // not worked yet today
            return ((long)v & 1L) == 0L;           // even visit = placed → out; odd = hauled → gone
        }
    }
}
