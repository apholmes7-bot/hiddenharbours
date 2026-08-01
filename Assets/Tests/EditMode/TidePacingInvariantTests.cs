using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// GUARDS FOR THE OWNER'S TIDE-PACING RULING (2026-08-01). He judged that the tide fell too fast and
    /// ruled BOTH offered levers at once: <c>GameConfig.SecondsPerDay</c> 1200 → 1800 (a 30-minute day)
    /// and <c>StPetersBuilder.TideAmplitude</c> 3.5 → 2.2 m.
    ///
    /// <para><b>Why this file exists rather than a comment.</b> Those two numbers are not independent
    /// tunables — they are joined to a third, <c>SandbarCrestElevation</c>, by arithmetic nobody can see
    /// while editing one of them. Move the amplitude and the crest must move with it or the bar stops
    /// flooding at neaps; move either and the crossing window the owner tuned changes length. Every
    /// property he actually judged is pinned here, derived from the live constants, so the NEXT person to
    /// turn one of these dials is told immediately which promise they broke.</para>
    ///
    /// <para><b>The three invariants.</b>
    /// <list type="number">
    ///   <item>The bar floods at EVERY tide — the crest clears neap high water (the #280 neap gap,
    ///         re-checked against whatever the amplitude is now).</item>
    ///   <item>The spring crossing window keeps its IN-GAME duration across the ruling. It is a
    ///         function of crest/amplitude alone, so holding that ratio holds the window exactly; both
    ///         sides are computed from the closed form here rather than pinned to a remembered
    ///         "6 h 43 m", so the guard survives a re-tune of the ratio itself.</item>
    ///   <item>The peak rate of the water level stays at or under 1.6 cm/s of REAL time — the number the
    ///         ruling is actually about. This is the one that binds the two levers together: either of
    ///         them alone can put it back over the line.</item>
    /// </list></para>
    ///
    /// <para>Pure maths against <see cref="TideModel"/> and the authored constants; no scene, no clock.</para>
    /// </summary>
    public class TidePacingInvariantTests
    {
        /// <summary>The fastest the sea may rise or fall in REAL time, metres per second. The owner's
        /// complaint was about this quantity and nothing else; 0.016 m/s (1.6 cm/s) is the ceiling his
        /// ruling lands under, with the shipped dials producing ~1.48 cm/s.</summary>
        private const double MaxPeakRateMetresPerRealSecond = 0.016;

        // The pre-ruling dials, kept so invariant (2) can compare the window ACROSS the change rather
        // than against a remembered figure. Historical constants — never read by shipped code.
        private const double PreRulingAmplitude = 3.5;
        private const double PreRulingCrest     = 1.4;
        private const double PreRulingSecondsPerDay = 1200.0;

        // ---- closed forms ---------------------------------------------------------------------------

        /// <summary>
        /// Fraction of one tidal cycle for which ground at <paramref name="elevation"/> stands DRY, for a
        /// sinusoidal tide of the given amplitude about mean 0.
        ///
        /// <para>Water is <c>h(θ) = A·sin θ</c> and the ground is dry while <c>h &lt; e</c>, i.e. while
        /// <c>sin θ &lt; e/A</c> — a set whose measure is <c>(π + 2·asin(e/A)) / 2π</c> and which depends
        /// on NOTHING but the ratio <c>e/A</c>. That is the whole reason a tide-fraction elevation must
        /// scale with the amplitude: hold the ratio and every window is preserved to the second.</para>
        /// </summary>
        private static double DryFractionOfCycle(double elevation, double amplitude)
        {
            double r = elevation / amplitude;
            if (r >= 1.0) return 1.0;    // never wet
            if (r <= -1.0) return 0.0;   // never dry
            return (Math.PI + 2.0 * Math.Asin(r)) / (2.0 * Math.PI);
        }

        /// <summary>Peak |d(water level)/dt| in metres per REAL second, for a sinusoid of the given
        /// amplitude on a day of the given length. The clock advances one <c>TotalSeconds</c> per real
        /// second, so the day length is what converts in-game hours to real ones — which is exactly how
        /// the day-length lever slows the tide without touching the tide model.</summary>
        private static double PeakRateMetresPerRealSecond(double amplitude, double secondsPerDay,
                                                          double tidalPeriodHours)
        {
            double secondsPerHour = secondsPerDay / 24.0;
            return amplitude * 2.0 * Math.PI / tidalPeriodHours / secondsPerHour;
        }

        // ---- (1) the bar floods at every tide ---------------------------------------------------------

        /// <summary>
        /// The crest must stay under NEAP high water or the tide gate switches itself off for part of
        /// every lunar month. <see cref="StPetersTerrainTests"/> proves this by walking the real
        /// <see cref="TideModel"/> for a month; this states it as the one-line arithmetic the person
        /// turning the amplitude dial needs to see, and names the fix in its failure message.
        /// </summary>
        [Test]
        public void Invariant1_TheCrestClearsNeapHighWater_SoTheBarFloodsAtEveryTide()
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                double amplitude = StPetersBuilder.TideAmplitude;
                double crest = StPetersBuilder.SandbarCrestElevation;
                double neapHigh = amplitude * cfg.NeapAmplitudeFraction;

                Assert.Less(crest, neapHigh,
                    $"the sandbar crest ({crest:F3} m) is at or above neap high water ({neapHigh:F3} m = " +
                    $"amplitude {amplitude:F2} × neap fraction {cfg.NeapAmplitudeFraction}), so around " +
                    "neaps the bar never floods and St Peters' defining mechanic silently switches off. " +
                    "The crest is a TIDE FRACTION: scale it with the amplitude " +
                    $"(crest = {crest / amplitude:F3} × amplitude) rather than leaving it behind.");

                Debug.Log($"[tide-pacing] (1) crest {crest:F3} m vs neap high {neapHigh:F3} m — " +
                          $"{neapHigh - crest:F3} m of clearance; crest/amplitude = {crest / amplitude:F4}.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        // ---- (2) the spring crossing window keeps its in-game length ----------------------------------

        /// <summary>
        /// The window the owner tuned, in GAME time, must survive the ruling. Both sides come from
        /// <see cref="DryFractionOfCycle"/>, so this compares the actual pre- and post-change geometry
        /// instead of trusting a figure someone wrote down once.
        ///
        /// <para>The REAL duration deliberately does NOT match: it grows ×1.5 with the day length, along
        /// with every other real-time pace in the game. That is the ruling working, not a regression.</para>
        /// </summary>
        [Test]
        public void Invariant2_TheSpringCrossingWindow_KeepsItsInGameDurationAcrossTheRuling()
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                double period = cfg.TidalPeriodHours;

                double nowHours = DryFractionOfCycle(StPetersBuilder.SandbarCrestElevation,
                                                     StPetersBuilder.TideAmplitude) * period;
                double thenHours = DryFractionOfCycle(PreRulingCrest, PreRulingAmplitude) * period;

                Assert.AreEqual(thenHours, nowHours, 1.0 / 60.0,
                    $"the spring crossing window changed length in GAME time: {thenHours:F4} h before " +
                    $"the ruling, {nowHours:F4} h now. The window depends only on crest/amplitude " +
                    $"({PreRulingCrest / PreRulingAmplitude:F4} before, " +
                    $"{StPetersBuilder.SandbarCrestElevation / StPetersBuilder.TideAmplitude:F4} now) — if " +
                    "you meant to re-tune how long the bar is walkable, say so and move this guard; if " +
                    "you only meant to move the amplitude, scale the crest with it.");

                double realNow = nowHours * (cfg.SecondsPerDay / 24.0);
                double realThen = thenHours * (PreRulingSecondsPerDay / 24.0);
                Debug.Log($"[tide-pacing] (2) spring window {nowHours:F3} in-game h — unchanged. " +
                          $"In REAL time it grew {realThen:F0} s → {realNow:F0} s (×{realNow / realThen:F2}), " +
                          "which is the day-length lever doing its job.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        // ---- (3) the peak rate — the thing the owner actually judged ----------------------------------

        /// <summary>
        /// The complaint was "the tide falls too fast", and this is that sentence as a number. Both levers
        /// feed it, so either one can put it back over the line on its own — which is precisely why it is
        /// asserted on the product rather than on each dial.
        /// </summary>
        [Test]
        public void Invariant3_ThePeakTideRate_StaysUnderTheRuledCeiling()
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                double peak = PeakRateMetresPerRealSecond(StPetersBuilder.TideAmplitude,
                                                          cfg.SecondsPerDay, cfg.TidalPeriodHours);

                Assert.LessOrEqual(peak, MaxPeakRateMetresPerRealSecond,
                    $"the sea now moves at up to {peak * 100.0:F2} cm/s of real time, over the " +
                    $"{MaxPeakRateMetresPerRealSecond * 100.0:F1} cm/s the owner's 2026-08-01 pacing ruling " +
                    "settled on. Two dials feed this: St Peters' TideAmplitude " +
                    $"({StPetersBuilder.TideAmplitude} m) and GameConfig.SecondsPerDay ({cfg.SecondsPerDay}). " +
                    "Raising the amplitude or shortening the day both make the tide fall faster again.");

                double before = PeakRateMetresPerRealSecond(PreRulingAmplitude, PreRulingSecondsPerDay,
                                                            cfg.TidalPeriodHours);
                Debug.Log($"[tide-pacing] (3) peak rate {peak * 100.0:F3} cm/s real " +
                          $"(was {before * 100.0:F3} — ×{before / peak:F2} slower).");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        /// <summary>
        /// The closed form above is only worth asserting on if it describes the tide the game actually
        /// runs. This walks the REAL <see cref="TideModel"/> over a lunar month and checks its fastest
        /// observed rate against <see cref="PeakRateMetresPerRealSecond"/> — so invariant (3) cannot be
        /// quietly satisfied by algebra that has drifted away from the model.
        /// </summary>
        [Test]
        public void ThePeakRateClosedForm_MatchesTheRealTideModel()
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                var profile = new TideProfile
                {
                    MeanLevel = StPetersBuilder.TideMean,
                    Amplitude = StPetersBuilder.TideAmplitude,
                    PhaseHours = StPetersBuilder.TidePhaseHours,
                };

                double month = cfg.LunarMonthDays * cfg.SecondsPerDay;
                double step = (cfg.TidalPeriodHours * cfg.SecondsPerHour) / 200.0;

                double observed = 0.0;
                for (double t = 0.0; t < month; t += step)
                    observed = Math.Max(observed, Math.Abs(TideModel.Rate(t, profile, cfg)));

                double predicted = PeakRateMetresPerRealSecond(StPetersBuilder.TideAmplitude,
                                                               cfg.SecondsPerDay, cfg.TidalPeriodHours);

                Assert.AreEqual(predicted, observed, predicted * 0.01,
                    $"the sampled peak rate of the shipped TideModel ({observed * 100.0:F3} cm/s) has " +
                    $"drifted from the closed form invariant (3) is asserted on ({predicted * 100.0:F3} " +
                    "cm/s). The model's shape changed — re-derive the closed form before trusting the " +
                    "ceiling above.");
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        // ---- the magic-number mirror this ruling exposed ----------------------------------------------

        /// <summary>
        /// ONE DIAL, ONE READER. <c>MoonCycle</c> and <c>DayNightController</c> each used to carry their
        /// own serialized <c>_secondsPerDay = 1200f</c>, because Core exposed no accessor for it. That was
        /// untidy while the dial sat at 1200 and a real defect the moment it moved: the drawn moon would
        /// have advanced 1.5× faster than the tide's spring/neap envelope, so FULL MOON ON A SPRING TIDE
        /// (vision-and-pillars §5.5) would have come apart inside the first in-game week — visibly, and
        /// with nothing failing. Both now read <see cref="GameServices.SecondsPerDay"/>; this pins that
        /// the accessor really does resolve the owner's asset rather than a constant of its own.
        /// </summary>
        [Test]
        public void TheMoonAndTheTide_ReadTheSameDayLength_FromOneDial()
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.AreEqual(GameConfig.DefaultSecondsPerDay, GameServices.SecondsPerDay, 1e-4f,
                    "with no config wired the accessor must fall back to the Core default");
                Assert.AreEqual(GameConfig.DefaultLunarMonthDays, GameServices.LunarMonthDays, 1e-4f,
                    "same fallback contract for the lunar month");

                cfg.SecondsPerDay = 4321f;      // a value no literal anywhere could coincide with
                cfg.LunarMonthDays = 19f;
                GameServices.Config = cfg;

                Assert.AreEqual(4321f, GameServices.SecondsPerDay, 1e-4f,
                    "a wired config must WIN — otherwise the moon is reading a mirror again and the " +
                    "full-moon-on-a-spring alignment can drift without anything failing");
                Assert.AreEqual(19f, GameServices.LunarMonthDays, 1e-4f, "same for the lunar month");
            }
            finally
            {
                GameServices.Reset();
                UnityEngine.Object.DestroyImmediate(cfg);
            }
        }
    }
}
