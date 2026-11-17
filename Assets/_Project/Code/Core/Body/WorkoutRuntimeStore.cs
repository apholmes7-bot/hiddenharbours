using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>Per-player, session-only daily workout credit. Fitness belongs to the saved body.</summary>
    public sealed class WorkoutRuntimeStore
    {
        private readonly Dictionary<string, DailyCredit> _credit = new Dictionary<string, DailyCredit>(StringComparer.Ordinal);
        private readonly double _secondsPerDay;

        private readonly struct DailyCredit
        {
            public readonly double Day, Units;
            public DailyCredit(double day, double units) { Day = day; Units = units; }
        }

        internal WorkoutRuntimeStore(double secondsPerDay)
        {
            _secondsPerDay = secondsPerDay;
        }

        /// <summary>Units credited on this day, not a count of button presses or equipment instances.</summary>
        public double CreditedUnits(string exerciseId, double now)
        {
            if (!WorkoutDef.Nonnegative(now)) throw new ArgumentOutOfRangeException(nameof(now));
            return exerciseId != null && _credit.TryGetValue(exerciseId, out DailyCredit used) &&
                   used.Day == Math.Floor(now / _secondsPerDay) ? used.Units : 0d;
        }

        internal double Record(string exerciseId, double units, float limit, double at)
        {
            double used = CreditedUnits(exerciseId, at);
            double credit = Math.Min(units, Math.Max(0d, limit - used));
            if (!(credit > 0d)) return 0d;
            _credit[exerciseId] = new DailyCredit(Math.Floor(at / _secondsPerDay), used + credit);
            return credit;
        }
    }
}
