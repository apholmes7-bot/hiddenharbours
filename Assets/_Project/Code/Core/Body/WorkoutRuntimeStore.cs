using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>Per-player, runtime-only workout fitness and daily credit. Never attached to a save.</summary>
    public sealed class WorkoutRuntimeStore
    {
        private readonly FitnessStateDto[] _fitness =
        {
            FitnessStateDto.Untrained, FitnessStateDto.Untrained,
            FitnessStateDto.Untrained, FitnessStateDto.Untrained,
        };
        private readonly Dictionary<string, DailyCredit> _credit = new Dictionary<string, DailyCredit>(StringComparer.Ordinal);
        private readonly StrengthSettings _settings;
        private readonly double _secondsPerDay;

        private readonly struct DailyCredit
        {
            public readonly double Day, Units;
            public DailyCredit(double day, double units) { Day = day; Units = units; }
        }

        internal WorkoutRuntimeStore(StrengthSettings settings, double secondsPerDay)
        {
            _settings = settings;
            _secondsPerDay = secondsPerDay;
        }

        public float Level(FitnessKind kind, double now)
        {
            if (!WorkoutDef.Nonnegative(now)) throw new ArgumentOutOfRangeException(nameof(now));
            int index = (int)kind;
            if (index < 0 || index >= _fitness.Length) throw new ArgumentOutOfRangeException(nameof(kind));
            FitnessStateDto state = _fitness[index];
            if (now < state.SinceGameSeconds) throw new ArgumentOutOfRangeException(nameof(now));
            return FitnessMath.LevelAt(state, StrengthMath.CurveFor(kind, _settings), now, _secondsPerDay);
        }

        /// <summary>Units credited on this day, not a count of button presses or equipment instances.</summary>
        public double CreditedUnits(string exerciseId, double now)
        {
            if (!WorkoutDef.Nonnegative(now)) throw new ArgumentOutOfRangeException(nameof(now));
            return exerciseId != null && _credit.TryGetValue(exerciseId, out DailyCredit used) &&
                   used.Day == Math.Floor(now / _secondsPerDay) ? used.Units : 0d;
        }

        internal double Record(string exerciseId, double units, float limit, TrainingDoses training, double at)
        {
            double used = CreditedUnits(exerciseId, at);
            double credit = Math.Min(units, Math.Max(0d, limit - used));
            if (!(credit > 0d)) return 0d;
            _credit[exerciseId] = new DailyCredit(Math.Floor(at / _secondsPerDay), used + credit);
            foreach (FitnessKind kind in StrengthMath.Kinds)
            {
                float dose = (float)(training.Of(kind) * credit);
                FitnessMath.Train(ref _fitness[(int)kind], dose, at, StrengthMath.CurveFor(kind, _settings),
                                  _settings.MemoryRegainFactor, _secondsPerDay);
            }
            return credit;
        }
    }
}
