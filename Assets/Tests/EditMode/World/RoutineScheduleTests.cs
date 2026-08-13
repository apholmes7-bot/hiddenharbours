using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The routine engine's PURE time maths (<see cref="RoutineSchedule"/>) — which block of the day is
    /// running, how long it has been running, how long a walk takes, and the seeded departure jitter.
    ///
    /// <para>Every case here is headless arithmetic with no clock, no scene and no assets, which is the
    /// whole point of splitting the file out: the determinism contract (CLAUDE.md rule 5) is a property of
    /// these functions, and a property you can only observe by playing a full game day is one nobody
    /// checks.</para>
    /// </summary>
    public class RoutineScheduleTests
    {
        // A five-block day, roughly the shape a villager's is.
        static float[] Day() => new[] { 7f, 8.5f, 12.5f, 18f, 21.5f };

        // ---- Wrap24 -----------------------------------------------------------------------------

        [Test]
        public void Wrap24_FoldsAnyHourIntoTheDay_NegativesIncluded()
        {
            Assert.That(RoutineSchedule.Wrap24(0f), Is.EqualTo(0f));
            Assert.That(RoutineSchedule.Wrap24(23.9f), Is.EqualTo(23.9f).Within(1e-4f));
            Assert.That(RoutineSchedule.Wrap24(24f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(RoutineSchedule.Wrap24(25.5f), Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(RoutineSchedule.Wrap24(-1f), Is.EqualTo(23f).Within(1e-4f),
                        "A negative hour has to fold forward, or the block that spans midnight measures " +
                        "its elapsed time as a huge negative and every villager teleports at 00:00.");
        }

        // ---- BlockIndexAt -----------------------------------------------------------------------

        [Test]
        public void BlockIndexAt_PicksTheMostRecentDeparture()
        {
            float[] day = Day();
            Assert.That(RoutineSchedule.BlockIndexAt(7f, day), Is.EqualTo(0), "on the departure itself");
            Assert.That(RoutineSchedule.BlockIndexAt(8f, day), Is.EqualTo(0));
            Assert.That(RoutineSchedule.BlockIndexAt(8.5f, day), Is.EqualTo(1));
            Assert.That(RoutineSchedule.BlockIndexAt(12.49f, day), Is.EqualTo(1));
            Assert.That(RoutineSchedule.BlockIndexAt(17.99f, day), Is.EqualTo(2));
            Assert.That(RoutineSchedule.BlockIndexAt(21.5f, day), Is.EqualTo(4));
            Assert.That(RoutineSchedule.BlockIndexAt(23.99f, day), Is.EqualTo(4));
        }

        /// <summary>
        /// ⭐ THE MIDNIGHT WRAP. Before the first departure of the day the current block is the LAST one,
        /// still running through from yesterday — which is what "asleep at home" is, and the one case an
        /// array index would get wrong.
        /// </summary>
        [Test]
        public void BlockIndexAt_BeforeTheFirstDeparture_IsStillLastNightsBlock()
        {
            float[] day = Day();
            Assert.That(RoutineSchedule.BlockIndexAt(0f, day), Is.EqualTo(4));
            Assert.That(RoutineSchedule.BlockIndexAt(3f, day), Is.EqualTo(4));
            Assert.That(RoutineSchedule.BlockIndexAt(6.99f, day), Is.EqualTo(4));
        }

        [Test]
        public void BlockIndexAt_IsTotal_ForOneBlockAndForNothing()
        {
            Assert.That(RoutineSchedule.BlockIndexAt(13f, new[] { 9f }), Is.EqualTo(0));
            Assert.That(RoutineSchedule.BlockIndexAt(13f, System.Array.Empty<float>()), Is.EqualTo(-1));
            Assert.That(RoutineSchedule.BlockIndexAt(13f, null), Is.EqualTo(-1));
        }

        /// <summary>The search measures BACKWARDS from now rather than assuming sorted input, so a table the
        /// jitter has nudged out of exact order still resolves. Order-independence is what stops two runs
        /// disagreeing about who is where.</summary>
        [Test]
        public void BlockIndexAt_DoesNotRequireSortedDepartures()
        {
            var shuffled = new[] { 18f, 7f, 21.5f, 8.5f, 12.5f };
            Assert.That(RoutineSchedule.BlockIndexAt(13f, shuffled), Is.EqualTo(4), "12.5 is index 4 here");
            Assert.That(RoutineSchedule.BlockIndexAt(2f, shuffled), Is.EqualTo(2), "21.5 is index 2 here");
        }

        // ---- ElapsedHours -----------------------------------------------------------------------

        [Test]
        public void ElapsedHours_MeasuresStraightThroughMidnight()
        {
            Assert.That(RoutineSchedule.ElapsedHours(9f, 7f), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(RoutineSchedule.ElapsedHours(1f, 21.5f), Is.EqualTo(3.5f).Within(1e-4f),
                        "21:30 to 01:00 is three and a half hours, not minus twenty and a half.");
            Assert.That(RoutineSchedule.ElapsedHours(7f, 7f), Is.EqualTo(0f).Within(1e-4f));
        }

        // ---- gaps -------------------------------------------------------------------------------

        [Test]
        public void GapHours_IsTheDistanceToTheNextBlock_AndTheLastOneWrapsMidnight()
        {
            float[] day = Day();
            Assert.That(RoutineSchedule.GapHours(day, 0), Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(RoutineSchedule.GapHours(day, 3), Is.EqualTo(3.5f).Within(1e-4f));
            Assert.That(RoutineSchedule.GapHours(day, 4), Is.EqualTo(9.5f).Within(1e-4f),
                        "21:30 through midnight to 07:00 is nine and a half hours.");
        }

        [Test]
        public void GapHours_TotalsAFullDay()
        {
            float[] day = Day();
            float total = 0f;
            for (int i = 0; i < day.Length; i++) total += RoutineSchedule.GapHours(day, i);
            Assert.That(total, Is.EqualTo(24f).Within(1e-3f),
                        "The blocks tile the day exactly — there is no way to author a gap or an overlap, " +
                        "and that is the reason an entry carries only a START hour.");
        }

        [Test]
        public void SmallestGapHours_FindsTheTightestBlock()
        {
            Assert.That(RoutineSchedule.SmallestGapHours(Day()), Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(RoutineSchedule.SmallestGapHours(new[] { 6f }), Is.EqualTo(24f));
        }

        // ---- travel -----------------------------------------------------------------------------

        [Test]
        public void TravelHours_ConvertsMetresToGameHoursThroughTheDayLength()
        {
            // The pinned 1800 s day: one game hour is 75 real seconds, so 1.15 m/s covers 86.25 m of it.
            const float secondsPerGameHour = 1800f / 24f;
            Assert.That(RoutineSchedule.TravelHours(86.25f, 1.15f, secondsPerGameHour),
                        Is.EqualTo(1f).Within(1e-3f));
            Assert.That(RoutineSchedule.TravelHours(0f, 1.15f, secondsPerGameHour), Is.EqualTo(0f));
        }

        [Test]
        public void TravelHours_IsZeroRatherThanInfinite_ForNonsenseInputs()
        {
            Assert.That(RoutineSchedule.TravelHours(50f, 0f, 75f), Is.EqualTo(0f));
            Assert.That(RoutineSchedule.TravelHours(50f, 1.2f, 0f), Is.EqualTo(0f),
                        "A zero day length must leave a villager standing, not divide by zero and NaN " +
                        "their position for the rest of the session.");
        }

        [Test]
        public void DistanceWalked_IsTheInverseOfTravelHours()
        {
            const float secondsPerGameHour = 1800f / 24f;
            float hours = RoutineSchedule.TravelHours(120f, 1.3f, secondsPerGameHour);
            Assert.That(RoutineSchedule.DistanceWalked(hours, 1.3f, secondsPerGameHour),
                        Is.EqualTo(120f).Within(1e-2f));
        }

        // ---- the seeded jitter ------------------------------------------------------------------

        [Test]
        public void RoutineSeed_IsStableAcrossCalls_AndDiffersBySeedAndById()
        {
            uint a = RoutineSchedule.RoutineSeed(1234, "routine.rose_macisaac");
            Assert.That(RoutineSchedule.RoutineSeed(1234, "routine.rose_macisaac"), Is.EqualTo(a));
            Assert.That(RoutineSchedule.RoutineSeed(1235, "routine.rose_macisaac"), Is.Not.EqualTo(a));
            Assert.That(RoutineSchedule.RoutineSeed(1234, "routine.basil_samson"), Is.Not.EqualTo(a));
        }

        [Test]
        public void Hash01_IsInRange_AndSpreadsAcrossTheUnitInterval()
        {
            uint seed = RoutineSchedule.RoutineSeed(7, "routine.spread");
            float min = 1f, max = 0f, sum = 0f;
            const int n = 512;
            for (int i = 0; i < n; i++)
            {
                float v = RoutineSchedule.Hash01(seed, 3, i);
                Assert.That(v, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
                sum += v;
            }
            Assert.That(min, Is.LessThan(0.05f), "should reach the bottom of the interval");
            Assert.That(max, Is.GreaterThan(0.95f), "and the top");
            Assert.That(sum / n, Is.EqualTo(0.5f).Within(0.05f), "and sit around the middle on average");
        }

        [Test]
        public void DepartureHour_ShiftsTheAuthoredHour_ButNeverReordersTheDay()
        {
            float[] day = Day();
            uint seed = RoutineSchedule.RoutineSeed(99, "routine.jitter");

            var departures = new float[day.Length];
            for (int i = 0; i < day.Length; i++)
                departures[i] = RoutineSchedule.DepartureHour(day, i, seed, jitterMinutes: 6f);

            // Every one moved by at most the authored amount, and the ORDER survived.
            for (int i = 0; i < day.Length; i++)
                Assert.That(Mathf.Abs(departures[i] - day[i]), Is.LessThanOrEqualTo(6f / 60f + 1e-4f),
                            $"block {i} moved further than the authored jitter");
            for (int i = 0; i + 1 < day.Length; i++)
                Assert.That(departures[i], Is.LessThan(departures[i + 1]),
                            "jitter must not swap two blocks — a reordered day is a different day, and it " +
                            "would happen silently");
        }

        /// <summary>
        /// The clamp is a GUARANTEE, not a suggestion: ask for a wildly over-sized jitter and the order
        /// still survives, because the amplitude is capped at just under half the tighter of the two gaps
        /// each block sits between.
        /// </summary>
        [Test]
        public void DepartureHour_ClampsAnAbsurdJitter_RatherThanScramblingTheDay()
        {
            float[] day = Day();
            for (int trial = 0; trial < 32; trial++)
            {
                uint seed = RoutineSchedule.RoutineSeed(trial, "routine.absurd");
                var departures = new float[day.Length];
                for (int i = 0; i < day.Length; i++)
                    departures[i] = RoutineSchedule.DepartureHour(day, i, seed, jitterMinutes: 600f);

                for (int i = 0; i + 1 < day.Length; i++)
                    Assert.That(departures[i], Is.LessThan(departures[i + 1]),
                                $"seed {trial} scrambled the day with a ten-hour jitter request");
            }
        }

        /// <summary>The day index is deliberately NOT part of the seed: a villager's lateness is a
        /// personality a player can learn, and folding the day in would put a visible jump at midnight in
        /// the block that spans it.</summary>
        [Test]
        public void DepartureHour_IsTheSameEveryDay_ForTheSameSeed()
        {
            float[] day = Day();
            uint seed = RoutineSchedule.RoutineSeed(4321, "routine.stable");
            float monday = RoutineSchedule.DepartureHour(day, 1, seed, 6f);
            float tuesday = RoutineSchedule.DepartureHour(day, 1, seed, 6f);
            Assert.That(tuesday, Is.EqualTo(monday));
        }

        [Test]
        public void DepartureHour_WithNoJitter_IsTheAuthoredHourExactly()
        {
            float[] day = Day();
            uint seed = RoutineSchedule.RoutineSeed(5, "routine.none");
            for (int i = 0; i < day.Length; i++)
                Assert.That(RoutineSchedule.DepartureHour(day, i, seed, 0f), Is.EqualTo(day[i]));
        }
    }
}
