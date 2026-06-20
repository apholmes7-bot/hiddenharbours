using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-17 — the HUD's tide derivation. No Core interface exposes tide *state* (rising/falling)
    /// or *time-to-turn*; the HUD derives them in <see cref="TideReadout"/> from the height
    /// function. These tests pin the derivation against a synthetic sine with analytically-known
    /// turning points, and cross-check it against the real <see cref="TideModel"/> so the HUD and
    /// the sim never disagree about which way the tide is going (Pillar 1 truth).
    /// </summary>
    public class TideReadoutTests
    {
        // A pure cosine-free sine: h(t) = A*sin(2π t / period). Rising on (0, period/4),
        // a HIGH-water turn at period/4, falling to a LOW-water turn at 3*period/4, etc.
        private const double Period = 1000.0;     // arbitrary "seconds" for the synthetic wave
        private const double Amplitude = 2.0;

        private static float Sine(double t) => (float)(Amplitude * Math.Sin(2.0 * Math.PI * t / Period));

        // Step/horizon chosen as the HUD does: fine scan, horizon = one full period.
        private const double RisingDt = Period * 0.001; // tiny forward difference
        private const double ScanStep = Period * 0.01;  // 1% granularity
        private const double Horizon  = Period;         // one period always contains a turn

        [Test]
        public void Rising_True_OnTheMakingLimb()
        {
            // Just after t=0, sin is increasing → rising.
            var s = TideReadout.Derive(Sine, 10.0, RisingDt, ScanStep, Horizon);
            Assert.IsTrue(s.Rising, "tide should read rising on the making limb of the sine");
        }

        [Test]
        public void Rising_False_OnTheEbbingLimb()
        {
            // Just after the high-water turn (period/4), sin is decreasing → falling.
            double justAfterHigh = Period * 0.25 + 5.0;
            var s = TideReadout.Derive(Sine, justAfterHigh, RisingDt, ScanStep, Horizon);
            Assert.IsFalse(s.Rising, "tide should read falling just after high water");
        }

        [Test]
        public void Height_MatchesTheFunction()
        {
            double t = 137.0;
            var s = TideReadout.Derive(Sine, t, RisingDt, ScanStep, Horizon);
            Assert.That(s.HeightMeters, Is.EqualTo(Sine(t)).Within(1e-4));
        }

        [Test]
        public void SecondsToTurn_FindsTheHighWater()
        {
            // From t=0 (rising), the next turn is the HIGH at period/4 = 250.
            var s = TideReadout.Derive(Sine, 0.0, RisingDt, ScanStep, Horizon);
            Assert.IsTrue(s.HasTurn, "a turn must be found within one period");
            Assert.That(s.SecondsToTurn, Is.EqualTo(Period * 0.25).Within(Period * 0.01),
                "next turn from a rising start should be the high-water at quarter period");
        }

        [Test]
        public void SecondsToTurn_FindsTheLowWater()
        {
            // From just after the high (falling), the next turn is the LOW at 3*period/4 = 750.
            double start = Period * 0.25 + 1.0;
            var s = TideReadout.Derive(Sine, start, RisingDt, ScanStep, Horizon);
            Assert.IsTrue(s.HasTurn);
            double expected = Period * 0.75 - start;
            Assert.That(s.SecondsToTurn, Is.EqualTo(expected).Within(Period * 0.01),
                "next turn from a falling start should be the low-water at three-quarter period");
        }

        [Test]
        public void SecondsToTurn_DecreasesAsTheTurnApproaches()
        {
            var a = TideReadout.Derive(Sine, 100.0, RisingDt, ScanStep, Horizon);
            var b = TideReadout.Derive(Sine, 200.0, RisingDt, ScanStep, Horizon);
            // Both are rising toward the high at 250; b is closer, so less time remains.
            Assert.Less(b.SecondsToTurn, a.SecondsToTurn);
        }

        [Test]
        public void AgreesWithTideModel_OnRisingState()
        {
            // Cross-check the HUD derivation against the real sim model across a full day.
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            var profile = TideProfile.CoddleCove;
            double sph = cfg.SecondsPerHour;
            double risingDt = sph * 0.05;                  // exactly TideModel's dt
            double scanStep = sph * 0.10;
            double horizon  = sph * cfg.TidalPeriodHours;

            Func<double, float> heightAt = t => TideModel.Height(t, profile, cfg);

            int samples = 48;
            for (int i = 0; i < samples; i++)
            {
                double now = cfg.SecondsPerDay * (i / (double)samples);
                bool modelRising = TideModel.IsRising(now, profile, cfg);
                var hud = TideReadout.Derive(heightAt, now, risingDt, scanStep, horizon);
                Assert.AreEqual(modelRising, hud.Rising,
                    $"HUD and TideModel must agree on rising at t={now:0}");
            }
        }

        [Test]
        public void NullHeightFunction_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => TideReadout.Derive(null, 0.0, RisingDt, ScanStep, Horizon));
        }

        [Test]
        public void NonPositiveSteps_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TideReadout.Derive(Sine, 0.0, 0.0, ScanStep, Horizon));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TideReadout.Derive(Sine, 0.0, RisingDt, 0.0, Horizon));
        }
    }
}
