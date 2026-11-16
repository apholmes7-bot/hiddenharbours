using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>How one kind of fitness grows, holds and fades</b> (body slice 2). Levels are 0..1: 0 untrained,
    /// 1 the most this frame can be trained to. A DOSE is the training an <see cref="ActivityDef"/> gives per
    /// repetition or minute (one squat is about 1.5 leg doses; a minute of reeling about 1 grip dose).
    /// </summary>
    [Serializable]
    public struct FitnessCurve
    {
        [Tooltip("How fast doses raise the level, with diminishing returns: level = 1 - (1 - level) x " +
                 "e^(-this x dose). 0.0005: about 1,390 doses from untrained to half-way (a hundred squats are 150).")]
        [Min(0f)] public float GainPerDose;

        [Tooltip("Level lost per IN-GAME day once the grace below has run out with no maintaining dose.")]
        [Min(0f)] public float DecayPerGameDay;

        [Tooltip("In-game days a level holds after its last maintaining dose before it starts to fade.")]
        [Min(0f)] public float GraceGameDays;

        [Tooltip("Doses that count as 'kept it up' and restart the grace. Small on purpose: holding a level " +
                 "costs far less than growing it (owner, 2026-11-15).")]
        [Min(0f)] public float MaintainDose;
    }

    /// <summary>
    /// <b>All the strength and cardio numbers</b> on <see cref="GameConfig"/> (body slice 2, owner ask
    /// 2026-11-15: lifting capacity, carry speed by strength, muscle memory, workouts, cardio). Read through
    /// <see cref="GameServices.StrengthSettings"/>. Every value is the owner's to tune (rule 6).
    ///
    /// <para><b>Defaults change nothing.</b> Every level starts at 0 and the reference body (slice 1) has
    /// strength factors of exactly 1, cardio factor exactly 1 and carries nothing, so today's walk, rod and
    /// haul are bit for bit what they were.</para>
    /// </summary>
    [Serializable]
    public struct StrengthSettings
    {
        [Header("Training")]
        [Tooltip("Grip/arms, back/pull and legs: how strength grows, holds and fades.")]
        public FitnessCurve Strength;

        [Tooltip("Cardio: how fitness for moving grows, holds and fades (it fades faster than strength).")]
        public FitnessCurve Cardio;

        [Tooltip("Muscle memory: below a level once reached, training regains this many times faster than " +
                 "first-time gain, up to that old peak.")]
        [Min(1f)] public float MemoryRegainFactor;

        [Header("What a body can lift")]
        [Tooltip("Strength scales as (mass / reference lean mass) ^ this. 2/3: strength follows muscle " +
                 "cross-section, so a bigger body lifts more at the same training.")]
        [Range(0f, 1.5f)] public float MassExponent;

        [Tooltip("How much stronger than untrained a SMALL frame can train to (0.6 = 60 % stronger at level 1).")]
        [Min(0f)] public float SmallFrameTrainableGain;

        [Tooltip("How much stronger than untrained a LARGE frame can train to.")]
        [Min(0f)] public float LargeFrameTrainableGain;

        [Tooltip("What the reference body, untrained, carries comfortably at a walk (kg). Capacity scales " +
                 "with leg and back strength.")]
        [Min(1f)] public float ReferenceCarryCapacityKg;

        [Header("Carrying")]
        [Tooltip("Walk speed taken off when carrying exactly the capacity (0.4 = 40 % slower). A load is " +
                 "slowed in proportion to load / capacity.")]
        [Range(0f, 1f)] public float CarrySlowAtCapacity;

        [Tooltip("The slowest a load can make the walk, as a fraction of normal. Carrying slows, it never stops.")]
        [Range(0.6f, 1f)] public float CarryWalkFloor;

        [Header("Cardio")]
        [Tooltip("Walk and run speed added at full cardio (0.06 = 6 % faster).")]
        [Range(0f, 0.25f)] public float CardioSpeedGainMax;

        [Tooltip("Energy cost of moving (walking, running, rowing) saved at full cardio (0.25 = a quarter).")]
        [Range(0f, 0.6f)] public float CardioMovementSavingMax;

        [Header("Work signals")]
        [Tooltip("Turn the work the game already announces (a fish fight, a pot haul, driving) into activity " +
                 "for the body. Off: only explicit reports (a gym machine) count.")]
        public bool ReportWorkFromSignals;

        [Tooltip("The longest one announced stretch of work is counted for, in in-game minutes (a guard " +
                 "against a missed 'stopped' signal counting a whole day).")]
        [Min(1f)] public float MaxSignalWindowGameMinutes;

        /// <summary>The shipped starting numbers (guesses for the owner to tune by feel).</summary>
        public static StrengthSettings Default => new StrengthSettings
        {
            Strength = new FitnessCurve
            {
                GainPerDose = 0.0005f, DecayPerGameDay = 0.01f, GraceGameDays = 3f, MaintainDose = 15f,
            },
            Cardio = new FitnessCurve
            {
                GainPerDose = 0.0007f, DecayPerGameDay = 0.02f, GraceGameDays = 2f, MaintainDose = 20f,
            },
            MemoryRegainFactor = 4f,
            MassExponent = 2f / 3f,
            SmallFrameTrainableGain = 0.6f,
            LargeFrameTrainableGain = 0.9f,
            ReferenceCarryCapacityKg = 30f,
            CarrySlowAtCapacity = 0.4f,
            CarryWalkFloor = 0.6f,
            CardioSpeedGainMax = 0.06f,
            CardioMovementSavingMax = 0.25f,
            ReportWorkFromSignals = true,
            MaxSignalWindowGameMinutes = 240f,
        };
    }
}
