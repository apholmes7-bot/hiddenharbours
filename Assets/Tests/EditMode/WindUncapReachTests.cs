using System;
using System.Text;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>THE CALM BAND IS OVER — every rung of the sea-state ladder is reachable. ✅ RULED
    /// 2026-09-09 ("thats ok"), shipped as water PR D.</b>
    ///
    /// <para>⚠️ <b>The ruling was phrased as taking a cap off, and the cap was never the problem.</b>
    /// The law was <c>MeanStrength 3 + strN·1.3 + gustN·1.2</c> with both noises in [−1, 1], so it
    /// topped out at <b>5.5 m/s against a `CalmMaxStrength` of 5.7</b> — the clamp could not be
    /// reached, let alone bind. Measured over a game week on nine seeds before this PR: wind
    /// 0.58–5.33 m/s, and <b>five of eight rungs never occurred</b> (Glass, Lively, Rough, Gale,
    /// Storm). Deleting the cap would have changed nothing. What held the sea down was the law's own
    /// amplitudes.</para>
    ///
    /// <code>
    ///   BEFORE (nine seeds)  Glass  0.0  Calm 15.3  Light 71.0  Mod 13.7  Lively 0.0  Rough 0.0  Gale 0.0  Storm 0.0
    ///   AFTER  (nine seeds)  Glass  6.3  Calm 24.9  Light 40.0  Mod 12.5  Lively 5.7  Rough 6.1  Gale 4.3  Storm 0.3
    /// </code>
    ///
    /// <para><b>What changed, and why it is three channels rather than a bigger number.</b> Scaling
    /// the mean until the range spanned [0, 15] would have needed a mean near 7.5 — putting the
    /// AVERAGE day at Lively, i.e. permanently rough, which is not what P5 asks for. A gale is not a
    /// big gust; it is a <b>weather system</b>. So the strength gained a third channel, much slower
    /// than the swell and deliberately skewed (<c>max(0, n)^SystemShape</c>) so it rests at zero most
    /// of the time and occasionally builds for hours. The mean also came DOWN (3 → 2.2) because the
    /// old floor of 0.5 m/s sat exactly on the Glass/Calm edge, so a mirror was as unreachable as a
    /// gale.</para>
    ///
    /// <para><b>Rule 5 intact.</b> The third channel is one more <c>Noise(t, seed)</c> read: a pure
    /// function of <c>(worldSeed, gameTime)</c>, no accumulator, no state, nothing saved.</para>
    ///
    /// <para>Pure arithmetic over the shipped law: no scene, no clock, no graphics device.</para>
    /// </summary>
    public class WindUncapReachTests
    {
        /// <summary>The clock the shipped `GameConfig` runs: a 1200 s day, so 72× real time.</summary>
        const double SecondsPerHour = 3600.0 / 72.0;

        /// <summary>A game week, sampled every two in-game minutes — fine enough that a gust cannot
        /// hide between samples (the gust channel's own timescale is 0.4 h).</summary>
        const double WeekHours = 24 * 7;
        const double StepHours = 2.0 / 60.0;

        /// <summary>The owner's own seed first; the rest are the spread the acceptance is stated
        /// over. ⚠️ A single seed is one world's weather, not the law's behaviour — the bands below
        /// are aggregates for that reason, and the per-seed reach is reported separately.</summary>
        static readonly int[] Seeds = { 12345, 1, 7, 99, 4242, 555, 31337, 2026, 8 };

        static readonly string[] RungNames =
        { "Glass", "Calm", "Light", "Moderate", "Lively", "Rough", "Gale", "Storm" };

        /// <summary>Hours spent on each rung across the given seeds, plus the longest unbroken
        /// Gale-or-worse spell and how many seeds saw one at all.</summary>
        static double[] HoursPerRung(WindProfile p, int[] seeds,
                                     out double longestGalePlusHours, out int seedsThatSawAGale)
        {
            var hours = new double[8];
            longestGalePlusHours = 0;
            seedsThatSawAGale = 0;
            foreach (int seed in seeds)
            {
                double run = 0;
                bool saw = false;
                for (double h = 0; h < WeekHours; h += StepHours)
                {
                    float u = WeatherModel.SampleWind(h * SecondsPerHour, seed, SecondsPerHour, p).magnitude;
                    int rung = (int)WeatherModel.SeaFromWind(u);
                    hours[rung] += StepHours;
                    if (rung >= (int)SeaState.Gale)
                    {
                        run += StepHours;
                        saw = true;
                        longestGalePlusHours = Math.Max(longestGalePlusHours, run);
                    }
                    else run = 0;
                }
                if (saw) seedsThatSawAGale++;
            }
            return hours;
        }

        static double[] Percent(double[] hours)
        {
            double total = 0;
            foreach (double h in hours) total += h;
            var pct = new double[hours.Length];
            for (int i = 0; i < hours.Length; i++) pct[i] = hours[i] / total * 100.0;
            return pct;
        }

        // =============================================================================================

        /// <summary>
        /// 🔴 <b>THE RULING'S ACCEPTANCE: every rung of the ladder gets real hours, and the game stays
        /// mostly cosy.</b> Reachable AND livable — "every rung reachable" on its own would be
        /// satisfied by a Storm that happens for ten minutes a fortnight, which is not weather, it is
        /// a rumour.
        /// </summary>
        [Test]
        public void EveryRungOfTheLadder_IsReached_AndTheOrdinaryDayIsStillCosy()
        {
            double[] hours = HoursPerRung(WindProfile.CoddleCove, Seeds,
                                          out double longestGale, out int seedsWithGale);
            double[] pct = Percent(hours);

            var report = new StringBuilder();
            report.AppendLine($"  a game week x {Seeds.Length} seeds, sampled every {StepHours * 60:0} in-game minutes:");
            for (int i = 0; i < 8; i++)
                report.AppendLine($"    {RungNames[i],-9} {pct[i],5:0.0}%   {hours[i],7:0.0} h");
            report.AppendLine($"    longest unbroken Gale-or-worse spell: {longestGale:0.0} h");
            report.AppendLine($"    seeds that saw a Gale within the week: {seedsWithGale}/{Seeds.Length}");
            TestContext.WriteLine(report.ToString());

            for (int i = 0; i < 8; i++)
                Assert.Greater(hours[i], 0.0,
                    $"⭐ THE RULING: {RungNames[i]} never happens in a game week across " +
                    $"{Seeds.Length} seeds. Before this PR five of eight rungs were in that state and " +
                    "the wind could not reach its own cap — so if this fires, check the law's " +
                    "AMPLITUDES (MeanStrength, StrengthVariability, SystemStrength) before touching " +
                    "CalmMaxStrength, which has never once bound.");

            double cosy = pct[(int)SeaState.Calm] + pct[(int)SeaState.Light] + pct[(int)SeaState.Moderate];
            Assert.GreaterOrEqual(cosy, 100.0 * 2.0 / 3.0,
                $"⭐ AND STILL COSY: Calm+Light+Moderate is {cosy:0.0}% of the week. P5 is 'cozy but " +
                "with teeth' — a sea that is rough more often than it is pleasant has traded the " +
                "pillar for the feature. This is the number that stops a gale being 'fixed' by " +
                "raising the mean.");

            Assert.That(pct[(int)SeaState.Glass], Is.InRange(3.0, 8.0),
                $"Glass is {pct[0]:0.0}% of the week. Below 3 % a mirror is a rumour (row 5 calls the " +
                "glass calm sacred and it was UNREACHABLE before this PR); above 8 % the sea is a " +
                "pond. The dial is MeanStrength against StrengthVariability.");

            double blow = pct[(int)SeaState.Gale] + pct[(int)SeaState.Storm];
            Assert.That(blow, Is.InRange(2.0, 5.0),
                $"Gale+Storm is {blow:0.0}% of the week. Below 2 % the teeth are theoretical; above " +
                "5 % the coast is a permanent emergency. The dial is SystemStrength against " +
                "WeatherModel.SystemShape.");

            Assert.Less(pct[(int)SeaState.Storm], 1.0,
                $"Storm is {pct[7]:0.0}% of the week. A Storm should be the rarest thing the weather " +
                "does — it is the top of the ladder, not a weekly event.");
        }

        /// <summary>
        /// ⚠️ <b>A blow must be an EVENT, not a flicker.</b> The percentages above can be satisfied by
        /// a hundred one-minute gales, which would read as the weather stuttering rather than as a
        /// depression coming through. This asserts the shape: a real spell lasting hours, and most
        /// worlds seeing one inside a week.
        /// </summary>
        [Test]
        public void AGaleIsAnEventThatLastsHours_AndMostWorldsSeeOneInAWeek()
        {
            HoursPerRung(WindProfile.CoddleCove, Seeds, out double longestGale, out int seedsWithGale);

            Assert.Greater(longestGale, 4.0,
                $"⭐ the longest unbroken Gale-or-worse spell in the whole sweep is {longestGale:0.0} h. " +
                "A gale that never lasts a watch is the gust channel wearing a hat: the point of the " +
                "SYSTEM channel is that it is slow (WeatherModel.SystemChangeHours), so a blow builds, " +
                "sits, and passes.");

            Assert.GreaterOrEqual(seedsWithGale, Seeds.Length / 2,
                $"only {seedsWithGale} of {Seeds.Length} worlds saw a Gale inside a game week. The " +
                "owner uncapped the wind to get weather, not to get a statistical possibility of it. " +
                "⚠️ The dial here is SystemChangeHours: a slower system makes each blow longer but " +
                "rarer, and this is the half of that trade that gets forgotten.");
        }

        /// <summary>
        /// ⚠️ <b>THE DEAD CONTROL: the OLD law fails the assertions above.</b> Without this, every
        /// number in this fixture could be satisfied by a law that had not changed at all — and the
        /// whole finding was that the shipped law looked capped and was not.
        /// </summary>
        [Test]
        public void DEADCONTROL_TheSupersededLaw_CouldNotReachFiveOfTheEightRungs()
        {
            WindProfile old = WindProfile.CoddleCove;
            old.MeanStrength = 3f;                 // the values that shipped through M1
            old.StrengthVariability = 1.3f;
            old.GustStrength = 1.2f;
            old.SystemStrength = 0f;               // there was no system channel
            old.CalmMaxStrength = 5.7f;

            double[] hours = HoursPerRung(old, Seeds, out _, out int seedsWithGale);
            int missing = 0;
            var never = new StringBuilder();
            for (int i = 0; i < 8; i++)
                if (hours[i] <= 0) { missing++; never.Append(RungNames[i]).Append(' '); }

            TestContext.WriteLine($"  the superseded law reached {8 - missing}/8 rungs; never: {never}");

            Assert.AreEqual(5, missing,
                "⭐ THE DEFECT, kept as the record: the law that shipped through M1 could not produce " +
                "Glass, Lively, Rough, Gale or Storm in a game week on any of nine seeds. If this " +
                "count ever changes, the historical claim in this file's summary is wrong and the " +
                "before/after table above must be re-measured, not re-typed.");
            Assert.AreEqual(0, seedsWithGale, "…and no world ever saw a gale");

            // ...and the cap it supposedly hit.
            double peak = 0;
            foreach (int seed in Seeds)
                for (double h = 0; h < WeekHours; h += StepHours)
                    peak = Math.Max(peak, WeatherModel.SampleWind(h * SecondsPerHour, seed,
                                                                  SecondsPerHour, old).magnitude);
            Assert.Less(peak, old.CalmMaxStrength,
                $"⭐ AND THE CAP NEVER BOUND: the superseded law peaked at {peak:0.00} m/s against a " +
                $"CalmMaxStrength of {old.CalmMaxStrength}. The ruling was phrased as removing that " +
                "cap; removing it would have changed nothing at all. This assertion is the reason " +
                "the register's row carries a correction.");
        }

        /// <summary>
        /// ✅ <b>Rule 5: still a pure function of (worldSeed, gameTime).</b> The third channel is one
        /// more noise read, not an accumulator — so the same instant sampled twice, in any order, is
        /// the same wind, and no history is needed to arrive at it.
        /// </summary>
        [Test]
        public void TheWidenedLaw_IsStillDeterministic_AndNeedsNoHistory()
        {
            WindProfile p = WindProfile.CoddleCove;
            var probes = new[] { 0.0, 3.5, 61.25, 400.0, 9_000.0 };

            foreach (int seed in new[] { 12345, 7 })
            {
                // Forwards, then backwards: an accumulator would not survive the second order.
                var forward = new Vector2[probes.Length];
                for (int i = 0; i < probes.Length; i++)
                    forward[i] = WeatherModel.SampleWind(probes[i] * SecondsPerHour, seed, SecondsPerHour, p);
                for (int i = probes.Length - 1; i >= 0; i--)
                {
                    Vector2 again = WeatherModel.SampleWind(probes[i] * SecondsPerHour, seed, SecondsPerHour, p);
                    Assert.AreEqual(forward[i].x, again.x, 0f, $"seed {seed} at {probes[i]} h: not pure in time");
                    Assert.AreEqual(forward[i].y, again.y, 0f, $"seed {seed} at {probes[i]} h: not pure in time");
                }
            }

            // Two seeds must actually differ, or "deterministic" is being satisfied by a constant.
            float a = WeatherModel.SampleWind(500.0 * SecondsPerHour, 12345, SecondsPerHour, p).magnitude;
            float b = WeatherModel.SampleWind(500.0 * SecondsPerHour, 7, SecondsPerHour, p).magnitude;
            Assert.AreNotEqual(a, b, "DEAD CONTROL: two world seeds must give two different winds");
        }
    }
}
