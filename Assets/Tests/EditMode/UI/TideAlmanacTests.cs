using System;
using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// VS-06 — the tide table's maths, checked against a synthetic sine whose highs and lows are known
    /// analytically. This is the shared finder behind BOTH the player's almanac page and the editor
    /// Tide Table window, so a regression here would quietly mislead a player planning a crossing.
    ///
    /// <para>The reference tide is <c>h(t) = A·sin(2πt/T)</c>: rising at t = 0, high water at T/4, low at
    /// 3T/4, and so on every half-period. Times, heights, ordering, alternation and the degenerate cases
    /// are all pinned.</para>
    /// </summary>
    public class TideAlmanacTests
    {
        // A TEST-LOCAL clock scale, deliberately NOT the shipped dial: everything below runs against the
        // synthetic Sine, so the almanac's turn-finding is proved as pure maths on its own time base. It
        // keeps the historical 1200 s day (⇒ 50 s per in-game hour) so the numbers in these assertions
        // stay meaningful; the game itself now ships 1800 (GameConfig.DefaultSecondsPerDay).
        private const double SecondsPerDay = 1200.0;
        private const double SecondsPerHour = SecondsPerDay / 24.0;   // 50
        private const double TidalPeriodHours = 12.4206;
        private const double PeriodSeconds = TidalPeriodHours * SecondsPerHour;
        private const float Amplitude = 1.6f;

        // The shipped step sizes, read through the defaults rather than retyped.
        private static readonly TideTableSettings S = TideTableSettings.Default;
        private static double SlopeDt => SecondsPerHour * S.SlopeStepHours;   // 2.5 s
        private static double ScanStep => SecondsPerHour * S.ScanStepHours;   // 5.0 s
        private static double Horizon => SecondsPerHour * TidalPeriodHours;

        private static float Sine(double t) => (float)(Amplitude * Math.Sin(2.0 * Math.PI * t / PeriodSeconds));

        /// <summary>The k-th turn of the reference sine: (2k+1)·T/4 — high for even k, low for odd.</summary>
        private static double ExpectedTurn(int k) => (2 * k + 1) * PeriodSeconds / 4.0;

        private static List<TideTurn> Find(Func<double, float> h, double start, double end,
                                           int maxTurns = 64)
        {
            var results = new List<TideTurn>();
            TideAlmanac.FindTurns(h, start, end, SlopeDt, ScanStep, Horizon, maxTurns, results);
            return results;
        }

        // ---- the turns themselves -------------------------------------------------------------

        [Test]
        public void FindTurns_OverTwoDays_FindsEveryTurnAndNoMore()
        {
            // (2k+1)·T/4 < 2400 s ⇒ k = 0..7. Eight turns across today and tomorrow.
            List<TideTurn> turns = Find(Sine, 0.0, 2.0 * SecondsPerDay);
            Assert.AreEqual(8, turns.Count, "A 48 h page of a semidiurnal tide holds eight turns.");
        }

        [Test]
        public void FindTurns_TimesMatchTheAnalyticExtrema()
        {
            List<TideTurn> turns = Find(Sine, 0.0, 2.0 * SecondsPerDay);

            for (int k = 0; k < turns.Count; k++)
            {
                // The rising test is a FORWARD difference over SlopeDt, so it flips half a step before
                // the true peak — a known, bounded bias. One slope step is the honest tolerance, and at
                // the shipped tuning that is ~3 in-game minutes on a ~6 in-game hour half-cycle.
                Assert.AreEqual(ExpectedTurn(k), turns[k].Seconds, SlopeDt,
                    $"Turn {k} should sit at (2k+1)·T/4.");
            }
        }

        [Test]
        public void FindTurns_AlternateHighAndLow_StartingHigh()
        {
            List<TideTurn> turns = Find(Sine, 0.0, 2.0 * SecondsPerDay);

            for (int k = 0; k < turns.Count; k++)
                Assert.AreEqual(k % 2 == 0, turns[k].IsHigh,
                    $"Turn {k} should be a {(k % 2 == 0 ? "high" : "low")} — the water rises out of t=0.");
        }

        [Test]
        public void FindTurns_HeightsSitAtTheCrestAndTrough()
        {
            List<TideTurn> turns = Find(Sine, 0.0, 2.0 * SecondsPerDay);

            foreach (TideTurn turn in turns)
            {
                float expected = turn.IsHigh ? Amplitude : -Amplitude;
                // Sampling half a slope-step off the peak costs ~0.01 % of amplitude (cosine is flat
                // there) — so a 1 % tolerance is generous and still catches a real error.
                Assert.AreEqual(expected, turn.HeightMeters, Amplitude * 0.01f,
                    "A turn's height is the crest or the trough.");
            }
        }

        [Test]
        public void FindTurns_ReturnsTurnsInAscendingTimeOrder()
        {
            List<TideTurn> turns = Find(Sine, 0.0, 2.0 * SecondsPerDay);

            for (int i = 1; i < turns.Count; i++)
                Assert.Less(turns[i - 1].Seconds, turns[i].Seconds, "The page reads forward in time.");
        }

        [Test]
        public void FindTurns_StaysInsideTheWindow()
        {
            double start = 3.0 * SecondsPerDay;      // a page opened on day 3, not day 0
            double end = start + 2.0 * SecondsPerDay;
            List<TideTurn> turns = Find(Sine, start, end);

            Assert.IsNotEmpty(turns);
            foreach (TideTurn turn in turns)
            {
                Assert.GreaterOrEqual(turn.Seconds, start, "No turn from before the page starts.");
                Assert.Less(turn.Seconds, end, "No turn from past the page's end.");
            }
        }

        [Test]
        public void FindTurns_PhaseShiftedTide_ShiftsTheTurnsWithIt()
        {
            // A region whose high water is three hours later must read three hours later. This is what
            // stops every region peaking together — and what makes a tide table worth carrying.
            double shift = 3.0 * SecondsPerHour;
            List<TideTurn> shifted = Find(t => Sine(t - shift), 0.0, 2.0 * SecondsPerDay);

            Assert.IsNotEmpty(shifted);
            // First turn of the shifted tide = first crest at or after 0, i.e. T/4 + shift.
            Assert.AreEqual(ExpectedTurn(0) + shift, shifted[0].Seconds, SlopeDt);
            Assert.IsTrue(shifted[0].IsHigh);
        }

        // ---- degenerate and defensive cases ----------------------------------------------------

        [Test]
        public void FindTurns_FlatTide_FindsNothingAndTerminates()
        {
            // A zero-amplitude profile never turns. The finder must come back empty rather than spin —
            // this is the case the MaxTurns guard exists for.
            List<TideTurn> turns = Find(_ => 0f, 0.0, 2.0 * SecondsPerDay);
            Assert.IsEmpty(turns, "Still water has no high and no low.");
        }

        [Test]
        public void FindTurns_MaxTurnsCapsTheList()
        {
            List<TideTurn> turns = Find(Sine, 0.0, 30.0 * SecondsPerDay, maxTurns: 5);
            Assert.LessOrEqual(turns.Count, 5, "The guard caps how much one page will print.");
        }

        [Test]
        public void FindTurns_ClearsTheCallersListSoPagesCanReuseIt()
        {
            var results = new List<TideTurn> { new TideTurn(-1.0, 99f, true) };   // stale row
            TideAlmanac.FindTurns(Sine, 0.0, 2.0 * SecondsPerDay, SlopeDt, ScanStep, Horizon, 64, results);

            Assert.AreEqual(8, results.Count, "A reused list holds this page only.");
            Assert.AreNotEqual(-1.0, results[0].Seconds, "The stale row is gone.");
        }

        [Test]
        public void FindTurns_EmptyOrInvertedWindow_ReturnsEmpty()
        {
            Assert.IsEmpty(Find(Sine, 1000.0, 1000.0), "A zero-width page holds nothing.");
            Assert.IsEmpty(Find(Sine, 2000.0, 1000.0), "An inverted window is not a page.");
        }

        [Test]
        public void FindTurns_NonPositiveSteps_ReturnEmptyRatherThanThrow()
        {
            // A misconfigured GameConfig must degrade to a blank page, never take the game down with it.
            var results = new List<TideTurn>();
            Assert.DoesNotThrow(() =>
                TideAlmanac.FindTurns(Sine, 0.0, 2.0 * SecondsPerDay, 0.0, ScanStep, Horizon, 64, results));
            Assert.IsEmpty(results);

            Assert.DoesNotThrow(() =>
                TideAlmanac.FindTurns(Sine, 0.0, 2.0 * SecondsPerDay, SlopeDt, 0.0, Horizon, 64, results));
            Assert.IsEmpty(results);
        }

        [Test]
        public void FindTurns_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() =>
                TideAlmanac.FindTurns(null, 0.0, 100.0, SlopeDt, ScanStep, Horizon, 64, new List<TideTurn>()));
            Assert.Throws<ArgumentNullException>(() =>
                TideAlmanac.FindTurns(Sine, 0.0, 100.0, SlopeDt, ScanStep, Horizon, 64, null));
        }

        // ---- calendar helpers ------------------------------------------------------------------

        [Test]
        public void DayIndexOf_BucketsSecondsIntoAbsoluteDays()
        {
            Assert.AreEqual(0, TideAlmanac.DayIndexOf(0.0, SecondsPerDay));
            Assert.AreEqual(0, TideAlmanac.DayIndexOf(SecondsPerDay - 1.0, SecondsPerDay));
            Assert.AreEqual(1, TideAlmanac.DayIndexOf(SecondsPerDay, SecondsPerDay));
            Assert.AreEqual(7, TideAlmanac.DayIndexOf(7.5 * SecondsPerDay, SecondsPerDay));
        }

        [Test]
        public void DayStartSeconds_ReturnsTheTopOfThatDay()
        {
            Assert.AreEqual(0.0, TideAlmanac.DayStartSeconds(600.0, SecondsPerDay), 1e-9);
            Assert.AreEqual(SecondsPerDay, TideAlmanac.DayStartSeconds(SecondsPerDay + 1.0, SecondsPerDay), 1e-9);
            Assert.AreEqual(4.0 * SecondsPerDay,
                TideAlmanac.DayStartSeconds(4.25 * SecondsPerDay, SecondsPerDay), 1e-9);
        }

        [Test]
        public void HourOfDayAt_WrapsWithinTheDay()
        {
            Assert.AreEqual(0f, TideAlmanac.HourOfDayAt(0.0, SecondsPerDay), 1e-4f);
            Assert.AreEqual(6f, TideAlmanac.HourOfDayAt(0.25 * SecondsPerDay, SecondsPerDay), 1e-4f);
            Assert.AreEqual(12f, TideAlmanac.HourOfDayAt(0.5 * SecondsPerDay, SecondsPerDay), 1e-4f);
            // A later day reads the same clock time — the page prints times, not elapsed seconds.
            Assert.AreEqual(6f, TideAlmanac.HourOfDayAt(9.25 * SecondsPerDay, SecondsPerDay), 1e-4f);
        }

        [Test]
        public void CalendarHelpers_ZeroDayLength_DoNotDivideByZero()
        {
            Assert.AreEqual(0, TideAlmanac.DayIndexOf(500.0, 0.0));
            Assert.AreEqual(0.0, TideAlmanac.DayStartSeconds(500.0, 0.0), 1e-9);
            Assert.AreEqual(0f, TideAlmanac.HourOfDayAt(500.0, 0.0), 1e-6f);
        }

        // ---- the tunables ------------------------------------------------------------------------

        [Test]
        public void Default_ReproducesTheStepSizesTheHudAndEditorToolAlwaysUsed()
        {
            // Promoting these to owner tunables must not have moved a single displayed value: the HUD's
            // tide read and the editor Tide Table window both hard-coded SecondsPerHour × 0.05 / × 0.10.
            Assert.AreEqual(0.05f, S.SlopeStepHours, 1e-6f, "The rising test still mirrors TideModel.");
            Assert.AreEqual(0.10f, S.ScanStepHours, 1e-6f);
            Assert.AreEqual(2, S.WindowDays, "Today and tomorrow — the span a crossing is planned over.");
            Assert.Greater(S.MaxTurns, 8, "The guard must sit above an honest page's turn count.");
        }

        [Test]
        public void Default_TurnsAgreeWithTheHudsOwnTideRead()
        {
            // The page's "next turn" and the always-on HUD gauge derive from the same call, so they must
            // agree exactly. This pins the seam that keeps the band and the paper telling one story.
            const double now = 100.0;
            TideState hud = TideReadout.Derive(Sine, now, SlopeDt, ScanStep, Horizon);

            List<TideTurn> page = Find(Sine, now, now + 2.0 * SecondsPerDay);

            Assert.IsTrue(hud.HasTurn);
            Assert.IsNotEmpty(page);
            Assert.AreEqual(now + hud.SecondsToTurn, page[0].Seconds, 1e-6,
                "The page's first turn IS the HUD's next turn.");
            Assert.AreEqual(hud.Rising, page[0].IsHigh,
                "Rising into the turn means the page prints a high water.");
        }
    }
}
