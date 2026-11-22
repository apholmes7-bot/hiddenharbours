using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The holes the villagers dig on the flats</b> (owner ask 2026-11-22: "a dark hole left for the
    /// remainder of that day, even by working npcs"). The director already knows each villager's outing for
    /// today; a digging outing's places and the hours they stand at them are a function of the clock, so the
    /// holes they have dug by now are too. Answered through Core (<see cref="IDugHoleSource"/>); nothing is
    /// remembered and nothing is saved (rule 5). Who draws the holes, and how long the tide lets them last,
    /// is not this module's business.
    /// </summary>
    public sealed partial class BeachLifeDirector : IDugHoleSource
    {
        /// <inheritdoc/>
        public void CollectDugHoles(double nowSeconds, List<DugHole> into)
        {
            if (into == null || _outings.Count == 0) return;
            IGameClock clock = GameServices.Clock;
            IEnvironmentService env = GameServices.Environment;
            if (clock == null) return;

            float secondsPerDay = GameServices.SecondsPerDay;
            float secondsPerGameHour = secondsPerDay / RoutineSchedule.HoursPerDay;
            double dayStart = DugHoleMath.DayStartSeconds(nowSeconds, secondsPerDay);
            int dayIndex = secondsPerDay > 0f ? (int)System.Math.Floor(nowSeconds / secondsPerDay) : 0;
            int worldSeed = env != null ? env.WorldSeed : 0;
            DugHoleSettings settings = DugHoleSettings.Current;

            foreach (KeyValuePair<VillagerRoutine, DayOuting> entry in _outings)
            {
                VillagerRoutine villager = entry.Key;
                if (villager == null || villager.Routine == null) continue;
                DayOuting day = entry.Value;
                if (!ReferenceEquals(villager.Today, day.Plan)) continue;   // a plan of another day
                uint person = RoutineSchedule.Fold(RoutineSchedule.RoutineSeed(worldSeed, villager.Routine.Id), dayIndex);
                AppendDigHoles(day.Plan, day.Outing, person, dayStart, secondsPerGameHour, nowSeconds,
                               settings, secondsPerDay, into);
            }
        }

        /// <summary>
        /// The holes one villager's digging outing has made by <paramref name="nowSeconds"/>: for each place
        /// of the outing, from when they reach it until they set off from it, the holes
        /// <see cref="DugHoleMath.AppendPlaceHoles"/> schedules in front of where they stand, seeded by
        /// <paramref name="personSeed"/> and the place. Only a <see cref="BeachActivityKind.Dig"/> outing digs.
        /// Pure; returns how many it appended.
        /// </summary>
        public static int AppendDigHoles(RoutinePlan plan, in BeachOuting outing, uint personSeed,
                                         double dayStartSeconds, float secondsPerGameHour, double nowSeconds,
                                         in DugHoleSettings settings, double secondsPerDay, List<DugHole> into)
        {
            if (plan == null || into == null || outing.Activity == null) return 0;
            if (outing.Activity.Kind != BeachActivityKind.Dig || outing.StopCount <= 0) return 0;

            int added = 0;
            int end = Mathf.Min(outing.FirstStop + outing.StopCount, plan.BlockCount);
            for (int b = Mathf.Max(0, outing.FirstStop); b < end; b++)
            {
                float arriveHour = plan.DepartureHours[b] + plan.TravelHours(b, secondsPerGameHour);
                float leaveHour = b + 1 < plan.BlockCount && plan.DepartureHours[b + 1] > plan.DepartureHours[b]
                    ? plan.DepartureHours[b + 1]
                    : RoutineSchedule.HoursPerDay;
                if (leaveHour <= arriveHour) continue;

                int start = plan.RouteStart[b], count = plan.RouteCount[b];
                if (count <= 0) continue;
                Vector2 standAt = RoutineLaneTree.PointAlong(plan.Waypoints, start, count, plan.RouteLengthMetres[b]);
                float heading = plan.StandHeadingDegrees.Length > b ? plan.StandHeadingDegrees[b] : 180f;

                uint seed = RoutineSchedule.Finalize(RoutineSchedule.Fold(personSeed, b));
                added += DugHoleMath.AppendPlaceHoles(dayStartSeconds + (double)arriveHour * secondsPerGameHour,
                                                      dayStartSeconds + (double)leaveHour * secondsPerGameHour,
                                                      nowSeconds, standAt, heading, seed, settings,
                                                      secondsPerDay, into);
            }
            return added;
        }
    }
}
