using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-05 — the wind field must be DETERMINISTIC (same seed+time → identical wind, no hidden RNG),
    /// SMOOTH over time (no popping at the sample cadence), and stay in the CALM BAND (no storms in
    /// M1). Tests drive the pure <see cref="WeatherModel.SampleWind"/> directly — no GameConfig needed.
    /// </summary>
    public class WindFieldTests
    {
        // = SecondsPerDay(1200)/24. Passed explicitly so the field is testable without a GameConfig.
        private const double SecondsPerHour = 50.0;
        private static WindProfile Cove => WindProfile.CoddleCove;

        [Test]
        public void Wind_IsBitStable_ForIdenticalInputs()
        {
            // The sim contract: identical (seed, gameTime) → byte-identical wind, every time.
            foreach (int seed in new[] { 1, 42, 12345, -7 })
            {
                for (int i = 0; i < 200; i++)
                {
                    double t = i * 37.0;
                    Vector2 a = WeatherModel.SampleWind(t, seed, SecondsPerHour, Cove);
                    Vector2 b = WeatherModel.SampleWind(t, seed, SecondsPerHour, Cove);
                    Assert.AreEqual(a, b, $"wind not reproducible at t={t}, seed={seed}");
                }
            }
        }

        [Test]
        public void Wind_StaysInCalmBand()
        {
            float cap = Cove.CalmMaxStrength;
            foreach (int seed in new[] { 1, 7, 42, 12345, -9999 })
            {
                // ~12.5 in-game days at ~2.5 s steps — many slow- and gust-channel cells.
                for (int i = 0; i < 6000; i++)
                {
                    double t = i * (SecondsPerHour * 0.05);
                    Vector2 w = WeatherModel.SampleWind(t, seed, SecondsPerHour, Cove);
                    float s = w.magnitude;
                    Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s), "wind must be finite");
                    Assert.LessOrEqual(s, cap + 1e-3f, $"wind {s:0.00} m/s exceeded calm cap at t={t}, seed={seed}");
                    Assert.LessOrEqual((int)WeatherModel.SeaFromWind(s), (int)SeaState.Moderate,
                        $"sea state climbed past the calm band at t={t}, seed={seed}");
                }
            }
        }

        [Test]
        public void Wind_IsSmooth_NoPopping()
        {
            // Step finely relative to the fastest (gust) channel: adjacent change must be tiny,
            // yet over the sweep the wind must genuinely move (proving it's not just constant).
            double step = (Cove.GustChangeHours / 200.0) * SecondsPerHour;
            const int n = 8000;   // ~16 in-game hours
            Vector2 prev = WeatherModel.SampleWind(0.0, 7, SecondsPerHour, Cove);
            float maxDelta = 0f, lo = float.MaxValue, hi = float.MinValue;
            for (int i = 1; i <= n; i++)
            {
                Vector2 w = WeatherModel.SampleWind(i * step, 7, SecondsPerHour, Cove);
                maxDelta = Mathf.Max(maxDelta, (w - prev).magnitude);
                lo = Mathf.Min(lo, w.magnitude);
                hi = Mathf.Max(hi, w.magnitude);
                prev = w;
            }
            Assert.Less(maxDelta, 0.25f, $"wind popped: max adjacent step was {maxDelta:0.000} m/s");
            Assert.Greater(hi - lo, 1.0f, "wind barely moved over the sweep — not a living field");
        }

        [Test]
        public void Wind_AveragesToThePrevailing()
        {
            // Wander + gust veer are zero-mean noise, so the long-run mean vector ≈ the prevailing
            // bearing — i.e. Coddle Cove really does sit under a steady SW'ly.
            Vector2 sum = Vector2.zero;
            const int n = 20000;
            double step = (Cove.ChangeHours * SecondsPerHour) * 0.05;   // many slow-channel cells
            for (int i = 0; i < n; i++)
                sum += WeatherModel.SampleWind(i * step, 3, SecondsPerHour, Cove);

            Vector2 mean = sum / n;
            Assert.Greater(mean.magnitude, 0.3f, "no net prevailing wind");
            float ang = Mathf.Atan2(mean.y, mean.x) * Mathf.Rad2Deg;
            float diff = Mathf.DeltaAngle(ang, Cove.PrevailingDirectionDeg);
            Assert.Less(Mathf.Abs(diff), 25f,
                $"mean wind {ang:0}° not near prevailing {Cove.PrevailingDirectionDeg:0}°");
        }

        [Test]
        public void Wind_DiffersBySeed()
        {
            int differing = 0;
            for (int i = 0; i < 60; i++)
            {
                double t = i * 3000.0;
                Vector2 a = WeatherModel.SampleWind(t, 1, SecondsPerHour, Cove);
                Vector2 b = WeatherModel.SampleWind(t, 2, SecondsPerHour, Cove);
                if ((a - b).sqrMagnitude > 1e-4f) differing++;
            }
            Assert.Greater(differing, 45, "two different world seeds should mostly produce different wind");
        }

        [Test]
        public void DefaultProfile_HealsToCoddleCove()
        {
            // A zero/unset WindProfile (e.g. a freshly-added serialized field) must not produce dead
            // wind — it heals to the cove default rather than NaN/zero.
            var w = WeatherModel.SampleWind(1234.0, 5, SecondsPerHour, default);
            var expected = WeatherModel.SampleWind(1234.0, 5, SecondsPerHour, WindProfile.CoddleCove);
            Assert.AreEqual(expected, w);
        }
    }
}
