using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The ADR 0027 P0 dev sea-state override: the owner must be able to VIEW THE SEA IN A STORM
    /// before the P0 tuning pass, and the M1 calm-band wind clamp (<c>WindProfile.CalmMaxStrength</c>)
    /// never produces one naturally. Guards three contracts:
    ///
    /// <list type="number">
    /// <item><b>The inverse is exact:</b> <c>WeatherModel.WindStrengthFor</c> is the piecewise-linear
    /// inverse of <c>SeaState01</c> over the same band-edge table, so a forced sea-state lands exactly
    /// where the dial says (round trip pinned across the axis and at every band edge).</item>
    /// <item><b>OFF is byte-identical:</b> with the toggle off (the default), the sampled environment
    /// is the pure deterministic sim — the override must be impossible to detect.</item>
    /// <item><b>ON is internally consistent:</b> the forced sample's wind magnitude, sea-state enum
    /// and continuous SeaState01 all agree through the same thresholds, and the sim's deterministic
    /// wind DIRECTION is preserved (only the strength is forced).</item>
    /// </list>
    ///
    /// Sabotage MEASURED (2026-07-28, headless run): feeding the RAW dial straight into the strength
    /// (skipping the inverse — the obvious wrong implementation) fails exactly the two consistency
    /// tests, targeted not a collapse (6 of 8 stay green): <c>Forced_SeaState01_LandsExactly</c> at
    /// dial 0.25 returns 0.0714 (off by 0.179 — strength 0.25 m/s lands mid-Glass), and
    /// <c>ForcedStorm_ReachesStormConsistently</c> never reaches Storm (1 m/s is Ripple).
    /// </summary>
    public class DevSeaStateOverrideTests
    {
        private static GameConfig Config() => ScriptableObject.CreateInstance<GameConfig>();

        private static EnvironmentService Service(GameConfig cfg, int seed)
        {
            var go = new GameObject("EnvServiceUnderTest");
            var svc = go.AddComponent<EnvironmentService>();
            SetPrivate(svc, "_config", cfg);
            SetPrivate(svc, "_worldSeed", seed);
            return svc;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            FieldInfo fi = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(fi, $"Missing private field {field} — the test setup is stale.");
            fi.SetValue(target, value);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == "EnvServiceUnderTest") Object.DestroyImmediate(go);
        }

        // ===== (1) the exact inverse =====================================================

        [Test]
        public void WindStrengthFor_RoundTripsThroughSeaState01()
        {
            for (int i = 0; i <= 100; i++)
            {
                float s = i / 100f;
                float strength = WeatherModel.WindStrengthFor(s);
                Assert.AreEqual(s, WeatherModel.SeaState01(strength), 1e-4f,
                    $"Round trip broke at sea-state {s}: strength {strength}");
            }
        }

        [Test]
        public void WindStrengthFor_IsExactAtEveryBandEdge()
        {
            // The band edges of the canon scale (WeatherModel.SeaBandEdges — private, pinned here
            // by value like SeaState01's own tests pin the thresholds).
            float[] edges = { 0f, 0.5f, 2f, 4f, 6f, 8f, 11f, 14f };
            for (int k = 0; k < edges.Length; k++)
            {
                float s = k / 7f;
                Assert.AreEqual(edges[k], WeatherModel.WindStrengthFor(s), 1e-4f,
                    $"Band edge {k} (sea-state {s}) should invert to {edges[k]} m/s.");
            }
        }

        [Test]
        public void WindStrengthFor_ClampsAndIsMonotone()
        {
            Assert.AreEqual(0f, WeatherModel.WindStrengthFor(-0.5f));
            Assert.AreEqual(14f, WeatherModel.WindStrengthFor(1.5f));
            float prev = -1f;
            for (int i = 0; i <= 50; i++)
            {
                float v = WeatherModel.WindStrengthFor(i / 50f);
                Assert.GreaterOrEqual(v, prev, "The inverse must be monotone non-decreasing.");
                prev = v;
            }
        }

        // ===== (2) OFF = the pure sim, (3) ON = a consistent forced sea ===================

        [Test]
        public void OverrideOff_SampleMatchesPureModel()
        {
            var cfg = Config();
            var svc = Service(cfg, 4242);

            EnvironmentSample s = svc.Sample();

            // Clock is null in this rig -> t = 0; recompute the pure model at t = 0 and compare.
            WeatherModel.Sample(0.0, 4242, cfg, WindProfile.CoddleCove,
                                out Vector2 wind, out SeaState sea, out float vis);
            Assert.AreEqual(wind, s.WindVector, "Override OFF must leave the deterministic wind untouched.");
            Assert.AreEqual(sea, s.SeaState);
            Assert.AreEqual(vis, s.Visibility, 1e-6f);
            Assert.AreEqual(WeatherModel.SeaState01(wind.magnitude), s.SeaState01, 1e-6f);
        }

        [Test]
        public void ForcedStorm_ReachesStormConsistently()
        {
            var cfg = Config();
            var svc = Service(cfg, 4242);
            Vector2 naturalWind = svc.Sample().WindVector;

            svc.DevForceSeaState = true;
            svc.DevSeaState01 = 1f;
            EnvironmentSample s = svc.Sample();

            Assert.AreEqual(SeaState.Storm, s.SeaState, "Sea-state 1 must be a STORM.");
            Assert.AreEqual(1f, s.SeaState01, 1e-4f, "The continuous axis must saturate at the dial.");
            Assert.AreEqual(14f, s.WindVector.magnitude, 1e-3f, "Storm begins at the 14 m/s band edge.");
            // Direction preserved: the forced wind is collinear with (and same-signed as) the sim's.
            if (naturalWind.sqrMagnitude > 1e-8f)
            {
                float dot = Vector2.Dot(naturalWind.normalized, s.WindVector.normalized);
                Assert.AreEqual(1f, dot, 1e-4f, "The override must keep the deterministic wind direction.");
            }
        }

        [Test]
        public void Forced_SeaState01_LandsExactly()
        {
            var cfg = Config();
            var svc = Service(cfg, 7);
            svc.DevForceSeaState = true;
            foreach (float dial in new[] { 0.25f, 0.5f, 0.75f, 1f })
            {
                svc.DevSeaState01 = dial;
                Assert.AreEqual(dial, svc.Sample().SeaState01, 1e-4f,
                    $"Forcing sea-state {dial} must land exactly on the dial (the inverse contract).");
            }
        }

        [Test]
        public void ToggleOff_RestoresThePureSim()
        {
            var cfg = Config();
            var svc = Service(cfg, 99);
            EnvironmentSample before = svc.Sample();

            svc.DevForceSeaState = true;
            svc.DevSeaState01 = 1f;
            svc.Sample();   // storm while on...
            svc.DevForceSeaState = false;
            EnvironmentSample after = svc.Sample();

            // ...and off again is the same deterministic sample (nothing latched, nothing saved).
            Assert.AreEqual(before.WindVector, after.WindVector);
            Assert.AreEqual(before.SeaState, after.SeaState);
            Assert.AreEqual(before.SeaState01, after.SeaState01, 1e-6f);
            Assert.AreEqual(before.TideHeight, after.TideHeight, 1e-6f);
        }

        [Test]
        public void Override_LeavesTideAndFogAlone()
        {
            var cfg = Config();
            var svc = Service(cfg, 123);
            EnvironmentSample natural = svc.Sample();

            svc.DevForceSeaState = true;
            svc.DevSeaState01 = 1f;
            EnvironmentSample forced = svc.Sample();

            Assert.AreEqual(natural.TideHeight, forced.TideHeight, 1e-6f,
                "The override forces the WIND only — the tide is untouched.");
            Assert.AreEqual(natural.Visibility, forced.Visibility, 1e-6f,
                "Fog/visibility is an independent channel and stays the pure sim.");
        }
    }
}
