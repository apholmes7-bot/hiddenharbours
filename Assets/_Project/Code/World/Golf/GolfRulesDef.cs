using System;
using UnityEngine;

namespace HiddenHarbours.World.Golf
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Golf/Rules")]
    public sealed class GolfRulesDef : ScriptableObject
    {
        public string Id = "golf_rules.practice";
        [Min(0f)] public float Gravity = 9.81f;
        [Min(0f)] public float StepSeconds = 0.02f;
        [Min(0f)] public float MaxTravelPerStepMetres = 0.25f;
        [Min(0f)] public float MaxShotSeconds = 60f;
        [Min(1)] public int MaxSteps = 100000;
        [Min(0f)] public float AirDragPerSecond = 0.02f;
        [Min(0f)] public float WindAccelerationPerMetrePerSecond = 0.12f;
        [Min(0f)] public float StopSpeed = 0.03f;
        [Min(0f)] public float MinimumBounceSpeed = 0.4f;
        [Min(0f)] public float CupCaptureRadiusMetres = 0.055f;
        [Min(0f)] public float CupCaptureSpeed = 1.8f;
        [Min(0)] public int WaterPenaltyStrokes = 1;
        [Min(0)] public int OutOfBoundsPenaltyStrokes = 1;

        public GolfRules Build()
        {
            GolfCheck.Id(Id, "golf_rules.");
            GolfCheck.Positive(Gravity, nameof(Gravity)); GolfCheck.Positive(StepSeconds, nameof(StepSeconds));
            GolfCheck.Positive(MaxTravelPerStepMetres, nameof(MaxTravelPerStepMetres));
            GolfCheck.Positive(MaxShotSeconds, nameof(MaxShotSeconds)); GolfCheck.Positive(StopSpeed, nameof(StopSpeed));
            GolfCheck.Positive(MinimumBounceSpeed, nameof(MinimumBounceSpeed));
            GolfCheck.Positive(CupCaptureRadiusMetres, nameof(CupCaptureRadiusMetres));
            GolfCheck.Positive(CupCaptureSpeed, nameof(CupCaptureSpeed));
            GolfCheck.Nonnegative(AirDragPerSecond, nameof(AirDragPerSecond));
            GolfCheck.Nonnegative(WindAccelerationPerMetrePerSecond, nameof(WindAccelerationPerMetrePerSecond));
            if (MaxSteps < 1 || WaterPenaltyStrokes < 0 || OutOfBoundsPenaltyStrokes < 0)
                throw new ArgumentException("Invalid iteration or penalty count.");
            return new GolfRules(this);
        }
    }

    /// <summary>Value snapshot: editing an authoring asset cannot change a round already under way.</summary>
    public readonly struct GolfRules
    {
        public readonly float Gravity, StepSeconds, MaxTravel, MaxSeconds, AirDrag, WindAcceleration,
            StopSpeed, MinimumBounceSpeed, CupRadius, CupSpeed;
        public readonly int MaxSteps, WaterPenalty, OutOfBoundsPenalty;
        internal GolfRules(GolfRulesDef d)
        {
            Gravity = d.Gravity; StepSeconds = d.StepSeconds; MaxTravel = d.MaxTravelPerStepMetres;
            MaxSeconds = d.MaxShotSeconds; MaxSteps = d.MaxSteps; AirDrag = d.AirDragPerSecond;
            WindAcceleration = d.WindAccelerationPerMetrePerSecond; StopSpeed = d.StopSpeed;
            MinimumBounceSpeed = d.MinimumBounceSpeed; CupRadius = d.CupCaptureRadiusMetres;
            CupSpeed = d.CupCaptureSpeed; WaterPenalty = d.WaterPenaltyStrokes;
            OutOfBoundsPenalty = d.OutOfBoundsPenaltyStrokes;
        }
    }
}
