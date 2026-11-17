using System;

namespace HiddenHarbours.Core
{
    public enum WorkoutStartResult { Started, AlreadyActive, InvalidPlan, InvalidEquipment, InvalidBody }

    /// <summary>An immutable settlement. Costs land now; EndGameSeconds is the exact work endpoint.</summary>
    public readonly struct WorkoutReceipt
    {
        public readonly string WorkoutId, EquipmentInstanceId, ExerciseId;
        public readonly bool Completed;
        public readonly double EndGameSeconds, GameHours, Units, CreditedUnits;
        public readonly float Hunger, Tiredness;

        internal WorkoutReceipt(string workoutId, string equipment, string exercise, bool completed,
                                double end, double hours, double units, double credited, float hunger, float tiredness)
        {
            WorkoutId = workoutId; EquipmentInstanceId = equipment; ExerciseId = exercise;
            Completed = completed; EndGameSeconds = end; GameHours = hours; Units = units;
            CreditedUnits = credited; Hunger = hunger; Tiredness = tiredness;
        }
    }

    /// <summary>
    /// One player's workout session. A future adapter calls Tick on the game-time slow tick and cancels
    /// before other activities. No scene wiring, global service, wall clock, random source or saved gains.
    /// Recreate on load; all timestamps on one instance must be nondecreasing.
    /// </summary>
    public sealed class WorkoutService
    {
        private readonly INeedsService _needs;
        private readonly ICleanlinessService _cleanliness;
        private readonly BodySettings _body;
        private readonly StrengthSettings _strength;
        private readonly double _hour, _kcalPerHungerUnit;
        private double _lastTime;
        private Session _session;

        private sealed class Session
        {
            public string Id, Equipment, Exercise;
            public double Start, End, Units, Hunger, TirednessPerHour;
            public float Limit;
            public TrainingDoses Training;
        }

        public WorkoutRuntimeStore Fitness { get; }
        public bool IsActive => _session != null;
        public string EquipmentInstanceId => _session?.Equipment ?? "";
        public double EndsAtGameSeconds => _session?.End ?? -1d;
        public WorkoutReceipt? LastReceipt { get; private set; }

        public WorkoutService(INeedsService needs, ICleanlinessService cleanliness, BodySettings body,
                              NeedsSettings needsSettings, StrengthSettings strength, double secondsPerGameHour)
        {
            _needs = needs ?? throw new ArgumentNullException(nameof(needs));
            _cleanliness = cleanliness ?? throw new ArgumentNullException(nameof(cleanliness));
            if (!WorkoutDef.Positive(secondsPerGameHour) || !WorkoutDef.Positive(secondsPerGameHour * 24d))
                throw new ArgumentOutOfRangeException(nameof(secondsPerGameHour));
            _kcalPerHungerUnit = BodyMath.KcalPerHungerUnit(needsSettings.Hunger.RisePerGameHour, body);
            if (!WorkoutDef.Positive(_kcalPerHungerUnit)) throw new ArgumentException("Invalid hunger energy conversion.");
            ValidateCurve(strength.Strength);
            ValidateCurve(strength.Cardio);
            if (!WorkoutDef.Positive(strength.MemoryRegainFactor)) throw new ArgumentException("Invalid memory factor.");
            _body = body; _strength = strength; _hour = secondsPerGameHour;
            Fitness = new WorkoutRuntimeStore(strength, secondsPerGameHour * 24d);
        }

        public WorkoutStartResult TryStart(WorkoutDef plan, string equipmentInstanceId, double now, double massKg)
        {
            Observe(now);
            if (IsActive) return WorkoutStartResult.AlreadyActive;
            if (plan == null || plan.ValidationError() != null) return WorkoutStartResult.InvalidPlan;
            if (string.IsNullOrWhiteSpace(equipmentInstanceId)) return WorkoutStartResult.InvalidEquipment;
            if (!WorkoutDef.Positive(massKg)) return WorkoutStartResult.InvalidBody;
            double pace = _needs.PlayerMultiplier(TraitStat.WalkSpeed);
            if (!WorkoutDef.Nonnegative(pace)) pace = plan.MinimumPace;
            pace = Math.Max(plan.MinimumPace, Math.Min(1d, pace));
            double end = now + plan.DurationGameMinutes / 60d * _hour / pace;
            double hunger = StrengthMath.ActivityKcal(plan.Exercise, plan.Units, massKg, 0f, _body, _strength) /
                            _kcalPerHungerUnit;
            double fatigue = (end - now) / _hour * plan.TirednessPerGameHour;
            if (!WorkoutDef.Nonnegative(end) || !(end > now) || !FitsFloat(hunger) || !FitsFloat(fatigue))
                return WorkoutStartResult.InvalidPlan;
            foreach (FitnessKind kind in StrengthMath.Kinds)
                if (!FitsFloat((double)plan.Exercise.Training.Of(kind) * plan.Units))
                    return WorkoutStartResult.InvalidPlan;
            _session = new Session
            {
                Id = plan.Id, Equipment = equipmentInstanceId, Exercise = plan.Exercise.Id,
                Start = now, End = end, Units = plan.Units, Hunger = hunger,
                TirednessPerHour = plan.TirednessPerGameHour, Limit = plan.DailyTrainingUnits,
                Training = plan.Exercise.Training,
            };
            return WorkoutStartResult.Started;
        }

        /// <summary>Called with the absolute game clock, not a frame delta. Returns true once on completion.</summary>
        public bool Tick(double now)
        {
            Observe(now);
            if (!IsActive || now < _session.End) return false;
            Settle(_session.End, true);
            return true;
        }

        /// <summary>Bank the work actually done once. Cancel at/after the end is completion.</summary>
        public bool Cancel(double now)
        {
            Observe(now);
            if (!IsActive) return false;
            Settle(Math.Min(now, _session.End), now >= _session.End);
            return true;
        }

        private void Settle(double at, bool completed)
        {
            Session session = _session;
            _session = null; // Needs can announce synchronously; a subscriber must not settle twice.
            double hours = (at - session.Start) / _hour;
            double fraction = completed ? 1d : (at - session.Start) / (session.End - session.Start);
            double units = session.Units * fraction;
            float hunger = (float)(session.Hunger * fraction);
            float tiredness = (float)(hours * session.TirednessPerHour);
            double credit = Fitness.Record(session.Exercise, units, session.Limit, session.Training, at);
            LastReceipt = new WorkoutReceipt(session.Id, session.Equipment, session.Exercise, completed,
                                            at, hours, units, credit, hunger, tiredness);
            if (!(hours > 0d)) return;
            _needs.Spend(hunger);
            _needs.Exert(tiredness);
            _cleanliness.ReportActivity(DirtActivity.Gym, hours);
        }

        private void Observe(double now)
        {
            if (!WorkoutDef.Nonnegative(now) || now < _lastTime)
                throw new ArgumentOutOfRangeException(nameof(now), "Use monotonic game time; recreate workouts on load.");
            _lastTime = now;
        }

        private static bool FitsFloat(double value) => WorkoutDef.Nonnegative(value) && value <= float.MaxValue;

        private static void ValidateCurve(FitnessCurve curve)
        {
            if (!WorkoutDef.Nonnegative(curve.GainPerDose) || !WorkoutDef.Nonnegative(curve.DecayPerGameDay) ||
                !WorkoutDef.Nonnegative(curve.GraceGameDays) || !WorkoutDef.Nonnegative(curve.MaintainDose))
                throw new ArgumentException("Fitness curves must be finite and nonnegative.");
        }
    }
}
