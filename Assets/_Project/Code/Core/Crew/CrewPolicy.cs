using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The crew and trip-length rules the owner tunes</b> (crew slice R2), one block on
    /// <see cref="GameConfig"/>, read through <see cref="GameServices.CrewPolicy"/> so an unwired config
    /// falls back to <see cref="Default"/>.
    ///
    /// <para><b>Settled:</b> players never share a bunk (owner ruling 2026-11-15) — that is not a knob.
    /// <b>Open (audit Q3):</b> whether hired hands may share a bunk in two watches ("hot-bunking").
    /// Shipped OFF, so until the owner rules, everyone aboard overnight needs a bunk of their own.
    /// <b>Open (audit Q5):</b> whether month trips are only for boats run by a hired skipper. Shipped ON,
    /// the audit's recommendation (a month is 15 real hours at 1800 s per day).</para>
    /// </summary>
    [Serializable]
    public struct CrewPolicy
    {
        [Tooltip("OPEN QUESTION (audit Q3). May hired hands share a bunk in two watches? OFF: every hand " +
                 "needs a bunk of their own overnight. ON: each bunk left after the players have theirs " +
                 "sleeps 'Hands Per Shared Bunk' hands. Players never share either way (owner ruling 2026-11-15).")]
        public bool HiredHandsShareBunks;

        [Tooltip("With hand bunk-sharing ON: how many hired hands one bunk sleeps (two watches = 2).")]
        [Min(1)] public int HandsPerSharedBunk;

        [Tooltip("How many nights an OVERNIGHT trip plans for, at most. Used to size stores, water and ice " +
                 "and to name a trip from its nights (TripKindFor).")]
        [Min(1)] public int OvernightNights;

        [Tooltip("How many nights a WEEK trip plans for, at most. Above this a trip is a MONTH.")]
        [Min(1)] public int WeekNights;

        [Tooltip("How many nights a MONTH trip plans for.")]
        [Min(1)] public int MonthNights;

        [Tooltip("OPEN QUESTION (audit Q5). Month trips only with a hired skipper aboard (resolved " +
                 "off-screen later)? ON is the audit's recommendation: a month is 15 real hours of play.")]
        public bool MonthTripsNeedHiredSkipper;

        /// <summary>The shipped starting numbers (audit §2.2; the owner tunes them).</summary>
        public static CrewPolicy Default => new CrewPolicy
        {
            HiredHandsShareBunks = false,   // audit Q3 is open: not allowed until the owner rules
            HandsPerSharedBunk = 2,         // two watches
            OvernightNights = 3,            // audit §2.1: "2-4 nights" column, a one-to-three-night trip
            WeekNights = 10,                // audit §2.1: draggers 6-10 days, offshore lobster 8
            MonthNights = 30,
            MonthTripsNeedHiredSkipper = true,
        };

        /// <summary>The nights a trip kind plans for: 0 for a day trip.</summary>
        public int NightsFor(TripKind trip)
        {
            switch (trip)
            {
                case TripKind.Day: return 0;
                case TripKind.Overnight: return Math.Max(1, OvernightNights);
                case TripKind.Week: return Math.Max(Math.Max(1, OvernightNights), WeekNights);
                default: return Math.Max(Math.Max(Math.Max(1, OvernightNights), WeekNights), MonthNights);
            }
        }

        /// <summary>The trip kind a plan of <paramref name="nights"/> nights is.</summary>
        public TripKind TripKindFor(int nights)
        {
            if (nights <= 0) return TripKind.Day;
            if (nights <= NightsFor(TripKind.Overnight)) return TripKind.Overnight;
            if (nights <= NightsFor(TripKind.Week)) return TripKind.Week;
            return TripKind.Month;
        }
    }
}
