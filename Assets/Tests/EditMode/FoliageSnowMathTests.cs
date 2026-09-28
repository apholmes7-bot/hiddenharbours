using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The tree-snow calendar (<see cref="FoliageSnowMath"/>) — the number the pass-4 trees threshold their
    /// snow maps against. It is recomputed from the day the clock is on and never saved (CLAUDE.md rule 5),
    /// so it has to be a pure function that gives the same answer for the same day every time, stays in
    /// 0..1 for any tuning, and draws the shape the owner rules: bare through High Summer, climbing late in
    /// The Turn, full through Hard Winter, melting through Early Spring.
    ///
    /// <para>Pure — no services, no clock, no Unity runtime beyond <c>Mathf</c>.</para>
    /// </summary>
    public class FoliageSnowMathTests
    {
        private const int Days = GameConfig.DefaultDaysPerSeason;
        private const float Eps = 1e-6f;
        private static readonly FoliageSnowSettings S = FoliageSnowSettings.Default;

        private static float Cover(Season season, int day) => FoliageSnowMath.Coverage(season, day, Days, S);

        // ---- deterministic and clamped ------------------------------------------------------------

        [Test]
        public void EveryDayOfTheYear_IsWithinZeroAndOne_AndRepeatsBitForBit()
        {
            for (int s = 0; s < 4; s++)
            for (int d = 1; d <= Days; d++)
            {
                float a = Cover((Season)s, d);
                float b = Cover((Season)s, d);
                Assert.That(a, Is.InRange(0f, 1f), $"{(Season)s} day {d}");
                Assert.AreEqual(a, b, 0f, $"{(Season)s} day {d} must be the same number every time it is asked");
            }
        }

        [Test]
        public void DaysOutsideTheSeason_AreClampedToItsEnds()
        {
            Assert.AreEqual(Cover(Season.TheTurn, 1), Cover(Season.TheTurn, 0), 0f);
            Assert.AreEqual(Cover(Season.TheTurn, 1), Cover(Season.TheTurn, -9), 0f);
            Assert.AreEqual(Cover(Season.EarlySpring, Days), Cover(Season.EarlySpring, Days + 40), 0f);
            Assert.That(FoliageSnowMath.Coverage(Season.HardWinter, 5, 0, S), Is.InRange(0f, 1f),
                "a zero-day calendar must not divide by zero");
        }

        // ---- the proposed shape -------------------------------------------------------------------

        [Test]
        public void HighSummer_IsBare_EveryDay()
        {
            for (int d = 1; d <= Days; d++)
                Assert.AreEqual(0f, Cover(Season.HighSummer, d), 0f, $"High Summer day {d}");
        }

        [Test]
        public void TheTurn_IsBareUntilTheFirstSnow_ThenClimbsEveryDay()
        {
            for (int d = 1; d < S.FirstSnowDayOfTheTurn; d++)
                Assert.AreEqual(0f, Cover(Season.TheTurn, d), 0f, $"The Turn day {d} is before the first snow");

            float prev = 0f;
            for (int d = S.FirstSnowDayOfTheTurn; d <= Days; d++)
            {
                float c = Cover(Season.TheTurn, d);
                Assert.Greater(c, prev, $"The Turn day {d}: the cover must climb a step every day of the snowfall");
                prev = c;
            }
        }

        [Test]
        public void HardWinter_IsFullCover_EveryDay()
        {
            for (int d = 1; d <= Days; d++)
                Assert.AreEqual(1f, Cover(Season.HardWinter, d), Eps, $"Hard Winter day {d}");
        }

        [Test]
        public void EarlySpring_MeltsEveryDay_FromNearlyFullToBare()
        {
            float first = Cover(Season.EarlySpring, 1);
            Assert.Less(first, 1f, "the melt starts on the first day of spring");
            Assert.Greater(first, 0.9f, "one day of melt takes only a step off the winter's cover");

            float prev = 1f;
            for (int d = 1; d <= Days; d++)
            {
                float c = Cover(Season.EarlySpring, d);
                if (prev > 0f) Assert.Less(c, prev, $"Early Spring day {d}: the melt must not stall or refreeze");
                prev = c;
            }
            Assert.AreEqual(0f, Cover(Season.EarlySpring, Days), Eps, "bare by the last day of spring");
        }

        [Test]
        public void ConifersLoadUp_OnThe25thOfTheTurn()
        {
            // The ruled 60 % jump (the conifers' between-leaf snow switches on at 0.6, never faded). The
            // default curve lands it on the 25th; this pins the day the tooltip promises.
            int firstDay = -1;
            for (int d = 1; d <= Days && firstDay < 0; d++)
                if (Cover(Season.TheTurn, d) > 0.6f) firstDay = d;
            Assert.AreEqual(25, firstDay);
        }

        [Test]
        public void TheYear_NeverJumps_IncludingAcrossTheNewYear()
        {
            // A step a day, never more than the faster of the two ramps allows — so a season boundary or
            // the Hard Winter → Early Spring wrap cannot pop the woods white or bare in one morning.
            float maxStep = 1f / System.Math.Min(S.SnowfallDays, S.MeltDays) + Eps;
            float prev = Cover(Season.HardWinter, Days);
            for (int s = 0; s < 4; s++)
            for (int d = 1; d <= Days; d++)
            {
                float c = Cover((Season)s, d);
                Assert.LessOrEqual(System.Math.Abs(c - prev), maxStep, $"{(Season)s} day {d}");
                prev = c;
            }
        }

        // ---- any tuning stays continuous -----------------------------------------------------------

        [Test]
        public void ALateFirstSnow_MeltsFromTheCoverTheWinterReached()
        {
            var late = new FoliageSnowSettings
            {
                FirstSnowDayOfTheTurn = 28, SnowfallDays = 40, MeltStartDayOfEarlySpring = 1, MeltDays = 28,
            };
            float winterEnd = FoliageSnowMath.Coverage(Season.HardWinter, Days, Days, late);
            float springStart = FoliageSnowMath.Coverage(Season.EarlySpring, 1, Days, late);
            Assert.Less(winterEnd, 1f, "a snowfall longer than the winter never reaches full cover");
            Assert.LessOrEqual(springStart, winterEnd, "the melt starts from the cover the winter reached, not from 1");
        }

        [Test]
        public void AMeltLongerThanTheYear_IsDoneBeforeTheFirstSnow()
        {
            var slow = new FoliageSnowSettings
            {
                FirstSnowDayOfTheTurn = 19, SnowfallDays = 11, MeltStartDayOfEarlySpring = 1, MeltDays = 500,
            };
            Assert.AreEqual(0f, FoliageSnowMath.Coverage(Season.TheTurn, 18, Days, slow), Eps,
                "the melt is cut short so the autumn starts bare and climbs, never drops then climbs");
        }

        [Test]
        public void ANullClock_IsBare()
        {
            Assert.AreEqual(0f, FoliageSnowMath.Coverage((IGameClock)null, Days, S), 0f);
        }
    }
}
