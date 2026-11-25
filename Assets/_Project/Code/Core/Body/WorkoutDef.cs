using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>A timed set on one kind of equipment. Balance only; no appearance or scene binding.</summary>
    [CreateAssetMenu(fileName = "Workout", menuName = "Hidden Harbours/Body/Workout")]
    public sealed class WorkoutDef : ScriptableObject
    {
        public string Id;
        public GymEquipmentDef Equipment;
        public ActivityDef Exercise;
        [Min(0f)] public float DurationGameMinutes;
        [Min(0f)] public float Units;
        [Min(0f)] public float TirednessPerGameHour;
        [Min(0f)] public float DailyTrainingUnits;
        [Range(0.01f, 1f)] public float MinimumPace;

        /// <summary>Shared runtime/content validation. No malformed plan can start a session.</summary>
        public string ValidationError()
        {
            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("workout.", StringComparison.Ordinal))
                return "A workout needs a stable workout.* id.";
            if (Equipment == null || Exercise == null) return "Equipment and exercise are required.";
            if (string.IsNullOrWhiteSpace(Equipment.Id) || string.IsNullOrWhiteSpace(Exercise.Id))
                return "Equipment and exercise need stable ids.";
            if (Exercise.Kind != ActivityKind.Exercise || Exercise.Signal != ActivitySignal.None)
                return "Only explicitly reported gym exercises are supported.";
            if (Exercise.Unit != ActivityUnit.Minute && Exercise.Unit != ActivityUnit.Repetition)
                return "Unknown exercise unit.";
            if (Array.IndexOf(Equipment.ExerciseIds ?? Array.Empty<string>(), Exercise.Id) < 0 ||
                Array.IndexOf(Exercise.EquipmentIds ?? Array.Empty<string>(), Equipment.Id) < 0)
                return "Equipment and exercise must name each other.";
            if (!Positive(DurationGameMinutes) || !Positive(Units) || !Positive(DailyTrainingUnits) ||
                !Nonnegative(TirednessPerGameHour) || !Positive(MinimumPace) || MinimumPace > 1f)
                return "Duration, units, daily limit and pace must be finite and positive; exertion nonnegative.";
            if (Exercise.Unit == ActivityUnit.Minute && Units != DurationGameMinutes)
                return "Minute exercises count one unit per normal game minute.";
            if (!Nonnegative(Exercise.KcalPerUnitAboveRest)) return "Energy must be finite and nonnegative.";
            double total = 0d;
            foreach (FitnessKind kind in StrengthMath.Kinds)
            {
                float dose = Exercise.Training.Of(kind);
                if (!Nonnegative(dose) || !Nonnegative((double)dose * Units)) return "Invalid training dose.";
                total += dose;
            }
            return total > 0d ? null : "An exercise must train something.";
        }

        internal static bool Positive(double value) => value > 0d && !double.IsInfinity(value);
        internal static bool Nonnegative(double value) => value >= 0d && !double.IsInfinity(value);
    }
}
