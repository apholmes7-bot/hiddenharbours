using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Weather must also be deterministic from (seed, time) — same guarantees as the tide.</summary>
    public class WeatherModelTests
    {
        private static GameConfig Config() => ScriptableObject.CreateInstance<GameConfig>();

        [Test]
        public void Weather_IsReproducible()
        {
            var cfg = Config();
            WeatherModel.Sample(5000.0, 42, cfg, out var w1, out var s1, out var v1);
            WeatherModel.Sample(5000.0, 42, cfg, out var w2, out var s2, out var v2);
            Assert.AreEqual(w1, w2);
            Assert.AreEqual(s1, s2);
            Assert.AreEqual(v1, v2);
        }

        [Test]
        public void Visibility_StaysInRange()
        {
            var cfg = Config();
            for (int i = 0; i < 500; i++)
            {
                WeatherModel.Sample(i * 1000.0, 7, cfg, out _, out _, out float v);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
        }

        [Test]
        public void DifferentSeeds_GenerallyDiffer()
        {
            var cfg = Config();
            int differing = 0;
            for (int i = 0; i < 60; i++)
            {
                WeatherModel.Sample(i * 3000.0, 1, cfg, out var a, out _, out _);
                WeatherModel.Sample(i * 3000.0, 2, cfg, out var b, out _, out _);
                if ((a - b).sqrMagnitude > 0.0001f) differing++;
            }
            Assert.Greater(differing, 45, "Two different world seeds should mostly produce different weather.");
        }

        [Test]
        public void SeaFromWind_RisesWithStrength()
        {
            Assert.AreEqual(SeaState.Glass, WeatherModel.SeaFromWind(0f));
            Assert.AreEqual(SeaState.Storm, WeatherModel.SeaFromWind(25f));
            Assert.LessOrEqual((int)WeatherModel.SeaFromWind(3f), (int)WeatherModel.SeaFromWind(9f));
        }

        // ===== the CONTINUOUS sea-state axis (SeaState01) — the de-quantized twin of SeaFromWind =========
        // The owner-facing fix for the "sudden shader change between weather states" pop: presentation
        // consumers read this smooth 0..1 axis instead of (int)enum/7, so nothing visual jumps 1/7 when the
        // wind noise crosses a band threshold. These pin the contract: monotonic, clamped, exact at every
        // band edge, in agreement with the stepped enum, and deterministic. Gameplay stays on the enum.

        // The wind-strength band edges of the canon scale — mirrors WeatherModel.SeaBandEdges. Edge k is
        // where the enum flips to state k+1 (0.5 m/s → Calm, ... 14 m/s → Storm).
        private static readonly float[] BandEdges = { 0.5f, 2f, 4f, 6f, 8f, 11f, 14f };

        [Test]
        public void SeaState01_IsMonotonicInWindStrength()
        {
            float prev = -1f;
            for (int i = 0; i <= 2000; i++)
            {
                float s = i * 0.01f;                       // 0 .. 20 m/s, well past the Storm edge
                float v = WeatherModel.SeaState01(s);
                Assert.GreaterOrEqual(v, prev, $"SeaState01 must never fall as the wind rises (at {s} m/s)");
                prev = v;
            }
        }

        [Test]
        public void SeaState01_EqualsTheEnumsNormalisedValue_ExactlyAtEveryBandEdge()
        {
            // At the edge where the enum flips to state k+1, the continuous axis must read (k+1)/7 — the
            // SAME value (int)state/7 produced there before, so converting a consumer is a pure
            // de-quantization: identical output at every flip point, no re-tune.
            Assert.AreEqual(0f, WeatherModel.SeaState01(0f), 1e-6f, "no wind = glass = 0");
            for (int k = 0; k < BandEdges.Length; k++)
                Assert.AreEqual((k + 1) / 7f, WeatherModel.SeaState01(BandEdges[k]), 1e-5f,
                    $"at the {BandEdges[k]} m/s band edge the axis equals the enum's normalised value");
        }

        [Test]
        public void SeaState01_ClampsToUnitRange()
        {
            Assert.AreEqual(0f, WeatherModel.SeaState01(-3f), 1e-6f, "negative strength clamps to 0");
            Assert.AreEqual(1f, WeatherModel.SeaState01(14f), 1e-6f, "the Storm edge saturates at 1");
            Assert.AreEqual(1f, WeatherModel.SeaState01(100f), 1e-6f, "anything above stays at 1");
            for (int i = 0; i <= 2000; i++)
            {
                float v = WeatherModel.SeaState01(i * 0.01f);
                Assert.That(v, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void SeaState01_AgreesWithTheSteppedEnum_InsideEveryBand()
        {
            // Sample points safely inside each band (away from float-noise at the edges): the axis's band
            // (floor of v*7) must be exactly the enum SeaFromWind returns there.
            float[] insideBand = { 0.1f, 0.3f, 0.6f, 1.5f, 2.5f, 3.9f, 4.1f, 5.5f, 6.5f, 7.9f,
                                   9f, 10.9f, 11.1f, 12f, 13.9f, 14.1f, 18f };
            foreach (float s in insideBand)
            {
                int expected = (int)WeatherModel.SeaFromWind(s);
                int banded = Mathf.Min((int)SeaState.Storm,
                                       Mathf.FloorToInt(WeatherModel.SeaState01(s) * 7f + 1e-4f));
                Assert.AreEqual(expected, banded, $"floor(SeaState01*7) must track the enum at {s} m/s");
            }
        }

        [Test]
        public void SeaState01_TracksTheEnumWithinOneBand_AcrossADenseSweep()
        {
            // Dense sweep, tolerant at the edges: the continuous axis never strays more than one band from
            // the stepped enum anywhere (the loose envelope that holds even AT a threshold).
            for (int i = 0; i <= 2000; i++)
            {
                float s = i * 0.01f;
                float v7 = WeatherModel.SeaState01(s) * 7f;
                int k = (int)WeatherModel.SeaFromWind(s);
                Assert.GreaterOrEqual(v7, k - 1e-3f, $"axis below its enum band at {s} m/s");
                Assert.LessOrEqual(v7, k + 1f + 1e-3f, $"axis above its enum band at {s} m/s");
            }
        }

        [Test]
        public void SeaState01_IsDeterministic_FromSeedAndTime()
        {
            // The full deterministic pipeline (rule 5): same (seed, time) → same wind → same axis, bitwise.
            const double secondsPerHour = 50.0;
            for (int i = 0; i < 50; i++)
            {
                double t = i * 733.0;
                float a = WeatherModel.SeaState01(
                    WeatherModel.SampleWind(t, 42, secondsPerHour, WindProfile.CoddleCove).magnitude);
                float b = WeatherModel.SeaState01(
                    WeatherModel.SampleWind(t, 42, secondsPerHour, WindProfile.CoddleCove).magnitude);
                Assert.AreEqual(a, b, $"SeaState01 must be reproducible at t={t}");
            }
        }

        [Test]
        public void SeaFromWind_BehaviourUnchanged_LegacyThresholdPin()
        {
            // Determinism guard for GAMEPLAY (no behaviour change): SeaFromWind now derives from the shared
            // band-edge table, so pin it against the original hand-written threshold chain across a dense
            // sweep — gameplay gates (MaxSafeSeaState) and the HUD readout must see the exact same enum.
            for (int i = 0; i <= 2000; i++)
            {
                float s = i * 0.01f;
                Assert.AreEqual(LegacySeaFromWind(s), WeatherModel.SeaFromWind(s), $"enum changed at {s} m/s");
            }
        }

        /// <summary>The pre-refactor SeaFromWind, verbatim — the behavioural pin.</summary>
        private static SeaState LegacySeaFromWind(float strength)
        {
            if (strength < 0.5f) return SeaState.Glass;
            if (strength < 2f)   return SeaState.Calm;
            if (strength < 4f)   return SeaState.Light;
            if (strength < 6f)   return SeaState.Moderate;
            if (strength < 8f)   return SeaState.Lively;
            if (strength < 11f)  return SeaState.Rough;
            if (strength < 14f)  return SeaState.Gale;
            return SeaState.Storm;
        }
    }
}
