using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One need's numbers</b> (needs slice 1): how fast it rises, where its two stages begin, and what
    /// each stage costs. Every value is the owner's to tune in <see cref="GameConfig"/> (rule 6).
    /// Penalties are fractions taken off a factor of 1: a walk penalty of 0.05 is "5 % slower".
    /// </summary>
    [Serializable]
    public struct NeedCurve
    {
        [Tooltip("How much the need rises per IN-GAME hour (0..1 scale). 1/16 = 0.0625 means a full meal " +
                 "wears off completely in sixteen game hours. In-game hours, so the day-length setting " +
                 "never changes how many hours it takes.")]
        [Min(0f)] public float RisePerGameHour;

        [Tooltip("Level at which the first stage begins (peckish / tired).")]
        [Range(0f, 1f)] public float MildAt;

        [Tooltip("Level at which the second stage begins (hungry / worn out). At or above MildAt.")]
        [Range(0f, 1f)] public float StrongAt;

        [Tooltip("Walk and sprint speed taken off in the first stage (0.05 = 5 % slower).")]
        [Range(0f, 1f)] public float MildWalkPenalty;

        [Tooltip("Walk and sprint speed taken off in the second stage. The total never goes below the " +
                 "walk floor: hunger slows, it never stops (owner ruling 2026-11-14).")]
        [Range(0f, 1f)] public float StrongWalkPenalty;

        [Tooltip("Price taken off an in-person sale in the first stage (0.02 = 2 % less).")]
        [Range(0f, 1f)] public float MildSellPenalty;

        [Tooltip("Price taken off an in-person sale in the second stage.")]
        [Range(0f, 1f)] public float StrongSellPenalty;
    }

    /// <summary>
    /// <b>All the needs numbers in one block</b> on <see cref="GameConfig"/> (needs slice 1), read through
    /// <see cref="GameServices.NeedsSettings"/> so an unwired config falls back to <see cref="Default"/>.
    /// </summary>
    [Serializable]
    public struct NeedsSettings
    {
        [Tooltip("Hunger: rises with game time, eased by eating.")]
        public NeedCurve Hunger;

        [Tooltip("Tiredness: rises with game time awake, cleared by a night's sleep.")]
        public NeedCurve Tiredness;

        [Tooltip("The slowest the needs can ever make the walk, as a fraction of normal speed. Well above " +
                 "zero on purpose: hunger slows the character, it never stops them.")]
        [Range(0.5f, 1f)] public float WalkFloor;

        [Tooltip("The lowest the needs can ever push an in-person sale price, as a fraction of normal.")]
        [Range(0.5f, 1f)] public float SellFloor;

        [Tooltip("How often, in IN-GAME minutes, the clock checks whether a need crossed a stage (the " +
                 "slow tick that will raise the speech bubble). The speed and price never wait for it.")]
        [Min(1f)] public float TickGameMinutes;

        /// <summary>The shipped starting numbers (guesses for the owner to tune by feel).</summary>
        public static NeedsSettings Default => new NeedsSettings
        {
            Hunger = new NeedCurve
            {
                RisePerGameHour = 1f / 16f,   // full to empty in 16 game hours
                MildAt = 0.4f,                // peckish ~6.4 game hours after a full meal
                StrongAt = 0.75f,             // hungry ~12 game hours after a full meal
                MildWalkPenalty = 0.04f,
                StrongWalkPenalty = 0.10f,
                MildSellPenalty = 0.02f,
                StrongSellPenalty = 0.04f,
            },
            Tiredness = new NeedCurve
            {
                RisePerGameHour = 1f / 18f,   // worn right out after 18 game hours awake
                MildAt = 0.6f,                // tired ~10.8 game hours after waking (06:00 -> ~16:50)
                StrongAt = 0.9f,              // worn out ~16.2 game hours after waking (-> ~22:10)
                MildWalkPenalty = 0.04f,
                StrongWalkPenalty = 0.08f,
                MildSellPenalty = 0.02f,
                StrongSellPenalty = 0.04f,
            },
            WalkFloor = 0.85f,
            SellFloor = 0.92f,
            TickGameMinutes = 5f,
        };
    }
}
