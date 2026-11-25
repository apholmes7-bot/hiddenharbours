using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>What sort of effort an <see cref="ActivityDef"/> is.</summary>
    public enum ActivityKind
    {
        /// <summary>A job of work: hauling, reeling, digging, farm work.</summary>
        Work = 0,
        /// <summary>Getting about under your own power: walking, running, rowing. Cardio makes it cheaper.</summary>
        Movement = 1,
        /// <summary>A gym exercise on a machine or with a weight.</summary>
        Exercise = 2,
        /// <summary>Sitting still at something: driving. Trains nothing.</summary>
        Sedentary = 3,
    }

    /// <summary>What one unit of an activity is.</summary>
    public enum ActivityUnit
    {
        /// <summary>A game minute of it.</summary>
        Minute = 0,
        /// <summary>One repetition (a lift, a pull, a squat).</summary>
        Repetition = 1,
    }

    /// <summary>Which signal the game already publishes that counts as this activity (body slice 2). The
    /// listener counts the minutes such a signal says the player spent at it.</summary>
    public enum ActivitySignal
    {
        /// <summary>Only an explicit report counts (a gym machine, a later wire).</summary>
        None = 0,
        /// <summary>A rod fight or a hand-gather tend (<see cref="FishingState.IsFightPhase"/>).</summary>
        FishFight = 1,
        /// <summary>Hauling a pot up (<see cref="TrapHaulPhase.Hauling"/>).</summary>
        TrapHaul = 2,
        /// <summary>Working a pot on the stern deck (any non-None <see cref="SternDeckStage"/>).</summary>
        DeckWork = 3,
        /// <summary>Driving a vehicle (<see cref="ControlMode.Driving"/>).</summary>
        Driving = 4,
    }

    /// <summary>The training an activity gives each kind of fitness, per unit (doses; see <see cref="FitnessCurve"/>).</summary>
    [Serializable]
    public struct TrainingDoses
    {
        [Min(0f)] public float Grip;
        [Min(0f)] public float Back;
        [Min(0f)] public float Legs;
        [Min(0f)] public float Cardio;

        public float Of(FitnessKind kind) => kind switch
        {
            FitnessKind.Grip => Grip,
            FitnessKind.Back => Back,
            FitnessKind.Legs => Legs,
            _ => Cardio,
        };
    }

    /// <summary>
    /// <b>One kind of effort, as data</b> (body slice 2, owner asks 2026-11-15: "working out burns energy and
    /// calories faster than sedentary movement" and "work related tasks burn energy faster"). One asset each
    /// under <c>Data/Resources/Activities</c>, a stable id (<c>activity.snake_case</c>). Work and the gym go
    /// through the same door: <see cref="IBodyService.ReportActivity"/> with the def and how many units.
    ///
    /// <para><b>Energy</b> is the kcal above resting per unit for the reference body; a heavier body spends
    /// more in proportion to its mass, and a fit one spends less moving (<see cref="ActivityKind.Movement"/>,
    /// <see cref="StrengthSettings.CardioMovementSavingMax"/>). It raises hunger: the same kcal per hunger
    /// point for everyone (<see cref="BodyMath.KcalPerHungerUnit"/>), so a hard day empties a meal sooner.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Activity", menuName = "Hidden Harbours/Body/Activity")]
    public sealed class ActivityDef : ScriptableObject
    {
        [Tooltip("Stable id, activity.snake_case. Append-only: never rename.")]
        public string Id;

        public string DisplayName;

        public ActivityKind Kind;

        public ActivityUnit Unit;

        [Tooltip("Energy above resting per unit, kcal, for the reference body (a 67 kg adult).")]
        [Min(0f)] public float KcalPerUnitAboveRest;

        [Tooltip("Training each kind of fitness gets per unit.")]
        public TrainingDoses Training;

        [Tooltip("Gym equipment ids this needs (empty: none, it happens at work or on foot).")]
        public string[] EquipmentIds = Array.Empty<string>();

        [Tooltip("The published game signal that counts as this activity, if any.")]
        public ActivitySignal Signal;
    }
}
