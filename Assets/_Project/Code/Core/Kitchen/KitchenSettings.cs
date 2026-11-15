using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The kitchen numbers</b> (kitchens slice 1, owner ask 2026-11-15) on <see cref="GameConfig"/>,
    /// read through <see cref="GameServices.KitchenSettings"/> so an unwired config falls back to
    /// <see cref="Default"/>. Shared by every place a kitchen stands: a galley aboard, a home, a staffed
    /// shop, a player-built building. Nothing here is drawn as a bar or a number (owner ruling 2026-11-14).
    ///
    /// <para>Every reader goes through the <c>...InForce</c> accessors, which treat an unset (zero or less) value as
    /// the default, so a struct built field by field in a test, or an asset written before a field
    /// existed, can never zero out a meal or let sitting match a bed.</para>
    /// </summary>
    [Serializable]
    public struct KitchenSettings
    {
        /// <summary>The shipped dining reach (m).</summary>
        public const float DefaultDiningSeatReachMetres = 1.25f;
        /// <summary>The shipped interact reach for a food store or a seat (m).</summary>
        public const float DefaultPieceReachMetres = 1f;
        /// <summary>The shipped seated-but-no-table relief multiplier.</summary>
        public const float DefaultSeatedReliefMultiplier = 1.1f;
        /// <summary>The shipped dining relief multiplier.</summary>
        public const float DefaultDiningReliefMultiplier = 1.5f;
        /// <summary>The shipped sitting rest floor (tiredness level; a night's sleep reaches 0).</summary>
        public const float DefaultSitRestFloor = 0.35f;
        /// <summary>The shipped sitting recovery rate (tiredness per in-game hour): half of what an hour of
        /// sleep takes away at the shipped <see cref="NeedsSettings.SleepReliefPerGameHour"/> (1/8).</summary>
        public const float DefaultSitRecoveryPerGameHour = 0.0625f;

        /// <summary>
        /// A RULE, not a dial (owner 2026-11-15: sitting "does not match sleeping"): an hour seated never
        /// takes away more than this share of what an hour ASLEEP takes away
        /// (<see cref="NeedsSettings.SleepReliefPerGameHour"/>). Under the chosen-duration sleep (PR 1226) a
        /// short sleep relieves per hour, so without this a fast sitting rate would beat a short nap hour for
        /// hour. <see cref="SitRecoveryPerGameHour"/> is held under it whatever it is set to.
        /// </summary>
        public const float SitRateMaxShareOfSleep = 0.5f;

        [Tooltip("How close (m, plan) a seat's sitting point must be to a table top's CENTRE or a counter's " +
                 "prep station, on the same floor, to be a DINING seat. Eating there gives the dining boost. " +
                 "Measured to the centre, so a 1.1 m-deep table across from a bench reads ~1.2 m.")]
        [Min(0.1f)] public float DiningSeatReachMetres;

        [Tooltip("How close (m) the player must stand to a food store's or a seat's reach point to use it.")]
        [Min(0.1f)] public float PieceReachMetres;

        [Tooltip("Food eaten SEATED away from any table counts this many times its own relief. 1 = no bonus.")]
        [Min(1f)] public float SeatedReliefMultiplier;

        [Tooltip("Food eaten at a DINING seat (a seat by a table or counter) counts this many times its own " +
                 "relief: the greater food boost of sitting down to eat (owner 2026-11-15). Keep it at or " +
                 "above the seated multiplier; a cooked meal at the table is then the best a meal can do.")]
        [Min(1f)] public float DiningReliefMultiplier;

        [Tooltip("The most rested SITTING can make you, as a tiredness level (0 = fully rested, 1 = worn out). " +
                 "Must stay above 0: a night's sleep brings tiredness to 0, and sitting must never match it " +
                 "(owner 2026-11-15). 0.35 = sitting takes the edge off but you still want your bunk.")]
        [Range(0.01f, 1f)] public float SitRestFloor;

        [Tooltip("How fast sitting brings tiredness down toward the floor, per IN-GAME hour seated. Held at " +
                 "or under half of Needs > Sleep Relief Per Game Hour, so an hour seated never rests you as " +
                 "much as an hour asleep.")]
        [Min(0f)] public float SitRecoveryPerGameHour;

        /// <summary>The shipped starting numbers (guesses for the owner to tune by feel).</summary>
        public static KitchenSettings Default => new KitchenSettings
        {
            DiningSeatReachMetres = DefaultDiningSeatReachMetres,
            PieceReachMetres = DefaultPieceReachMetres,
            SeatedReliefMultiplier = DefaultSeatedReliefMultiplier,
            DiningReliefMultiplier = DefaultDiningReliefMultiplier,
            SitRestFloor = DefaultSitRestFloor,
            SitRecoveryPerGameHour = DefaultSitRecoveryPerGameHour,
        };

        /// <summary>The dining reach in force.</summary>
        public float DiningSeatReachInForce => DiningSeatReachMetres > 0f ? DiningSeatReachMetres : DefaultDiningSeatReachMetres;

        /// <summary>The piece reach in force.</summary>
        public float PieceReachInForce => PieceReachMetres > 0f ? PieceReachMetres : DefaultPieceReachMetres;

        /// <summary>The seated multiplier in force (never below 1: sitting never makes food worth less).</summary>
        public float SeatedReliefInForce => SeatedReliefMultiplier >= 1f ? SeatedReliefMultiplier : DefaultSeatedReliefMultiplier;

        /// <summary>The dining multiplier in force: never below the seated one, so a table always beats a
        /// bare seat.</summary>
        public float DiningReliefInForce
        {
            get
            {
                float dining = DiningReliefMultiplier >= 1f ? DiningReliefMultiplier : DefaultDiningReliefMultiplier;
                float seated = SeatedReliefInForce;
                return dining >= seated ? dining : seated;
            }
        }

        /// <summary>The sitting floor in force: strictly above 0 (what sleep reaches), at most 1.</summary>
        public float SitRestFloorInForce
        {
            get
            {
                float f = SitRestFloor > 0f ? SitRestFloor : DefaultSitRestFloor;
                return f > 1f ? 1f : f;
            }
        }

        /// <summary>The sitting recovery rate in force (an unset rate reads as the default).</summary>
        public float SitRecoveryInForce => SitRecoveryPerGameHour > 0f ? SitRecoveryPerGameHour : DefaultSitRecoveryPerGameHour;

        /// <summary>The sitting recovery rate in force beside a sleep that relieves
        /// <paramref name="sleepReliefPerGameHour"/> per hour: held at or under
        /// <see cref="SitRateMaxShareOfSleep"/> of it. A non-positive sleep relief is the pre-ruling rule
        /// (any sleep rests you fully), which no sitting rate can beat, so the rate stands.</summary>
        public float SitRecoveryBeside(float sleepReliefPerGameHour)
        {
            float rate = SitRecoveryInForce;
            if (!(sleepReliefPerGameHour > 0f)) return rate;
            float cap = sleepReliefPerGameHour * SitRateMaxShareOfSleep;
            return rate <= cap ? rate : cap;
        }
    }
}
