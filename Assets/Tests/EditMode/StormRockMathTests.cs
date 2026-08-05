using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>ADR 0018 B2.5 — the storm seakeeping read, pinned as pure math.</b> The owner's ask
    /// (2026-08-05): storm rocking that actually answers the sea, smooth, and obeying gravity.
    /// These tests pin the four guarantees the mechanism was designed around:
    ///
    /// <list type="bullet">
    ///   <item><b>Monotone response:</b> the storm blend/multiplier strictly grow with sea state
    ///   above the storm start until the ceiling — a gale always outranks a chop.</item>
    ///   <item><b>Calm is the negative control:</b> at/below the storm start everything is the
    ///   EXACT identity (blend 0, multiplier 1, weight passthrough bit-identical).</item>
    ///   <item><b>The free-fall cap:</b> chasing a surface that drops harder than gravity, the
    ///   weighted ride's own downward acceleration never exceeds g — with a permanent SABOTAGE arm
    ///   (the fish-school style, PR #406) proving the cap is load-bearing, not decoration.</item>
    ///   <item><b>Settle + band:</b> the chase lands EXACTLY (epsilon snap, no eternal
    ///   micro-motion) and never strays beyond the honesty band of the surface.</item>
    /// </list>
    ///
    /// Deterministic, headless, allocation-free — the <c>SeakeepingForcesMathTests</c> discipline.
    /// </summary>
    public class StormRockMathTests
    {
        const float Dt = 1f / 60f;
        static readonly float G = WaveFieldSettings.Default.Gravity;   // the one g (9.81)

        static readonly SeakeepingResponse Neutral = new SeakeepingResponse(1f, 0f);

        // ------------------------------------------------------------------ the response curve

        [Test]
        public void StormBlend_IsExactlyZero_AtAndBelowTheStormStart_AndWhenDisabled()
        {
            StormRockSettings s = StormRockSettings.Default;

            Assert.AreEqual(0f, StormRockMath.StormBlend01(0f, in s), "glass");
            Assert.AreEqual(0f, StormRockMath.StormBlend01(s.StormStartSeaState01 * 0.5f, in s), "mid-calm");
            Assert.AreEqual(0f, StormRockMath.StormBlend01(s.StormStartSeaState01, in s),
                "AT the storm start the blend must be exactly 0 — the calm band's byte-identity " +
                "boundary (the rock-smoothness fixtures sail at exactly this sea state).");

            StormRockSettings off = s;
            off.Enabled = false;
            Assert.AreEqual(0f, StormRockMath.StormBlend01(1f, in off),
                "disabled must be the exact A/B off side at EVERY sea state");
        }

        [Test]
        public void StormBlend_And_Multiplier_GrowMonotonically_ToTheCeiling()
        {
            StormRockSettings s = StormRockSettings.Default;

            float previousBlend = -1f;
            float previousMultiplier = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float sea = i / 100f;
                float blend = StormRockMath.StormBlend01(sea, in s);
                float multiplier = StormRockMath.ResponseMultiplier(blend, in s);

                Assert.GreaterOrEqual(blend, previousBlend, $"blend must never fall (sea {sea:F2})");
                Assert.GreaterOrEqual(multiplier, previousMultiplier, $"multiplier must never fall (sea {sea:F2})");
                if (sea > s.StormStartSeaState01 + 0.01f)
                    Assert.Greater(blend, previousBlend,
                        $"above the storm start the blend must STRICTLY grow (sea {sea:F2}) — " +
                        "a flat spot is exactly the 'not responsive to the waves' defect reborn");
                previousBlend = blend;
                previousMultiplier = multiplier;
            }

            Assert.AreEqual(1f, StormRockMath.StormBlend01(1f, in s), 1e-6f, "full storm = full blend");
            Assert.AreEqual(s.MaxResponseMultiplier,
                StormRockMath.ResponseMultiplier(1f, in s), 1e-6f, "full blend = the data ceiling");
            Assert.AreEqual(1f, StormRockMath.ResponseMultiplier(0f, in s),
                "blend 0 must yield EXACTLY 1f — ×1f is the float identity the calm byte-identity rides on");
        }

        [Test]
        public void TightenedSmoothing_KeepsCalmExact_AndTightensWithTheStorm()
        {
            StormRockSettings s = StormRockSettings.Default;
            const float calm = 0.2f;

            Assert.AreEqual(calm, StormRockMath.TightenedSmoothingSeconds(calm, 0f, in s),
                "blend 0 must return the calm constant EXACTLY");
            float full = StormRockMath.TightenedSmoothingSeconds(calm, 1f, in s);
            Assert.AreEqual(calm * s.StormSmoothingFactor, full, 1e-6f, "full blend = the tightened constant");
            Assert.Less(full, calm, "a storm must be LESS velveted than a calm");

            float previous = float.MaxValue;
            for (int i = 0; i <= 10; i++)
            {
                float tightened = StormRockMath.TightenedSmoothingSeconds(calm, i / 10f, in s);
                Assert.LessOrEqual(tightened, previous, "tightening must be monotone in the blend");
                previous = tightened;
            }
        }

        // ------------------------------------------------------------------ the weight filter

        [Test]
        public void HeaveWeight_Disengaged_IsABitExactPassthrough()
        {
            StormRockSettings s = StormRockSettings.Default;
            var state = new HeaveWeightState();

            for (int f = 0; f < 240; f++)
            {
                float t = f * Dt;
                float target = 0.8f * Mathf.Sin(1.7f * t) + 0.3f * Mathf.Sin(4.3f * t + 1f);
                float output = StormRockMath.StepHeaveWeight(ref state, target, Dt, G, 0f, in Neutral, in s);
                Assert.AreEqual(target, output,
                    $"frame {f}: engage 0 must return the surface BIT-IDENTICALLY — the calm " +
                    "negative control (and the displaced-OFF A/B) both stand on this");
            }
        }

        [Test]
        public void HeaveWeight_TracksASlowSwell_Closely()
        {
            StormRockSettings s = StormRockSettings.Default;
            var state = new HeaveWeightState();

            float worst = 0f;
            for (int f = 0; f < 600; f++)
            {
                float t = f * Dt;
                float target = 0.5f * Mathf.Sin(1.2f * t);      // a swell far below the chase rate
                float output = StormRockMath.StepHeaveWeight(ref state, target, Dt, G, 1f, in Neutral, in s);
                if (f > 60) worst = Mathf.Max(worst, Mathf.Abs(output - target));
            }

            // The analytic tracking error of the shipped constants (ω 7, ζ 1) on this swell
            // (ω_wave 1.2, A 0.5) is ≈ 0.17 m — the ~19° phase lag IS the felt weight, by design.
            // The bound catches gross detachment (band-scale failures), not the designed lag.
            Assert.Less(worst, 0.25f,
                "a gentle swell must be tracked closely — the weight filter adds mass and a small " +
                "phase lag, it does not detach the hull from an easy sea");
        }

        /// <summary>
        /// THE FREE-FALL CAP ("it must obey gravity"). The surface descends at 2 g — steeper than
        /// anything a hull could stay glued to — and the weighted ride's own downward acceleration
        /// (measured as the second difference of the output, which for the semi-implicit integrator
        /// IS the applied acceleration) must never exceed g. The first frames are skipped: inside
        /// the settle epsilon (4 mm) the ride is glued to the surface by design, and a 4 mm glue is
        /// not a slam. The window is sized so the gap stays inside the honesty band (the band clamp
        /// is a separate, documented guarantee with its own test).
        /// </summary>
        [Test]
        public void HeaveWeight_ChasingAPlummetingSurface_NeverExceedsFreeFall()
        {
            float minAccel = MeasureCliffChase(StormRockSettings.Default, out _);

            Assert.GreaterOrEqual(minAccel, -G * 1.02f,
                $"the weighted ride accelerated downward at {-minAccel:F1} m/s² against a cap of " +
                $"{G:F2} — the hull is being dragged down faster than gravity, the exact " +
                "physically-absurd read the owner named");
        }

        /// <summary>
        /// The permanent SABOTAGE arm (the fish-school style, PR #406): the same plummet with the
        /// cap DISABLED must exceed gravity, or the cap above is decoration and its test is vacuous.
        /// </summary>
        [Test]
        public void Sabotage_UncappedChase_ExceedsFreeFall()
        {
            StormRockSettings uncapped = StormRockSettings.Default;
            uncapped.MaxDownwardAccelInGs = float.PositiveInfinity;

            float minAccel = MeasureCliffChase(uncapped, out _);

            Assert.Less(minAccel, -G * 1.5f,
                "SABOTAGE NOT DETECTED — with the cap disabled the chase must exceed free fall " +
                $"(measured {-minAccel:F1} m/s² down vs g {G:F2}). If it no longer does, the " +
                "plummet scenario stopped demanding more than gravity and the cap test above " +
                "proves nothing.");
        }

        /// <summary>Chase a surface descending at 2 g for 0.35 s (gap stays inside the band).
        /// Returns the most negative second-difference acceleration after the settle-epsilon
        /// frames; outputs go to <paramref name="outputs"/> for reuse.</summary>
        static float MeasureCliffChase(in StormRockSettings settings, out float[] outputs)
        {
            const int frames = 21;                       // 0.35 s at 60 fps
            var state = new HeaveWeightState();
            outputs = new float[frames];
            for (int f = 0; f < frames; f++)
            {
                float t = (f + 1) * Dt;
                float target = -0.5f * (2f * G) * t * t; // the surface plummets at 2 g
                outputs[f] = StormRockMath.StepHeaveWeight(ref state, target, Dt, G, 1f,
                                                          in Neutral, in settings);
            }

            float minAccel = float.MaxValue;
            for (int f = 6; f < frames; f++)             // skip the settle-epsilon glue at the start
            {
                float accel = (outputs[f] - 2f * outputs[f - 1] + outputs[f - 2]) / (Dt * Dt);
                minAccel = Mathf.Min(minAccel, accel);
            }
            return minAccel;
        }

        [Test]
        public void HeaveWeight_NeverStraysBeyondTheHonestyBand()
        {
            StormRockSettings s = StormRockSettings.Default;
            var state = new HeaveWeightState();

            // An adversarial square sea teleporting ±3 m — far beyond anything the field produces.
            // Whatever the spring wants, the ride must stay within the band of the surface: never
            // hovering over a vanished crest, never submarining under a risen one.
            for (int f = 0; f < 240; f++)
            {
                float target = ((f / 30) % 2 == 0) ? 3f : -3f;
                float output = StormRockMath.StepHeaveWeight(ref state, target, Dt, G, 1f, in Neutral, in s);
                Assert.LessOrEqual(Mathf.Abs(output - target), s.SurfaceBandMeters + 1e-4f,
                    $"frame {f}: the ride strayed {Mathf.Abs(output - target):F3} m from the " +
                    $"surface against a band of {s.SurfaceBandMeters:F2}");
            }
        }

        [Test]
        public void HeaveWeight_SettlesExactly_AndStaysSettled()
        {
            StormRockSettings s = StormRockSettings.Default;
            var state = new HeaveWeightState();

            // Prime ON the water at 0 (the first engaged step seeds from the surface), THEN move
            // the sea to a new still level inside the band — the chase must genuinely happen and
            // then LAND exactly, not asymptote forever.
            StormRockMath.StepHeaveWeight(ref state, 0f, Dt, G, 1f, in Neutral, in s);
            Assert.AreNotEqual(0.5f, StormRockMath.StepHeaveWeight(ref state, 0.5f, Dt, G, 1f, in Neutral, in s),
                "harness: the first chase step must NOT already be at the new level, or this " +
                "test never exercises the settle at all");

            int settledAt = -1;
            for (int f = 0; f < 600; f++)
            {
                float output = StormRockMath.StepHeaveWeight(ref state, 0.5f, Dt, G, 1f, in Neutral, in s);
                if (output == 0.5f && state.VelocityMetersPerSec == 0f) { settledAt = f; break; }
            }
            Assert.GreaterOrEqual(settledAt, 0,
                "a still sea must be LANDED ON exactly (the epsilon snap) — an asymptote that " +
                "never arrives is eternal micro-motion, the thing the snap exists to kill");

            for (int f = 0; f < 60; f++)
                Assert.AreEqual(0.5f, StormRockMath.StepHeaveWeight(ref state, 0.5f, Dt, G, 1f, in Neutral, in s),
                    "once settled it must STAY settled, bit-still, frame after frame");
        }

        [Test]
        public void HeaveWeight_IsDeterministic_AcrossIdenticalRuns()
        {
            float[] first = RunScriptedSea();
            float[] second = RunScriptedSea();
            for (int f = 0; f < first.Length; f++)
                Assert.AreEqual(first[f], second[f],
                    $"frame {f}: identical tick sequences must produce bit-identical rides (rule 5's " +
                    "presentation-side cousin — the WaveFieldAnimator honesty contract)");
        }

        static float[] RunScriptedSea()
        {
            StormRockSettings s = StormRockSettings.Default;
            var state = new HeaveWeightState();
            var outputs = new float[300];
            for (int f = 0; f < outputs.Length; f++)
            {
                float t = f * Dt;
                float target = 1.1f * Mathf.Sin(1.5f * t) + 0.4f * Mathf.Sin(3.9f * t + 0.7f);
                outputs[f] = StormRockMath.StepHeaveWeight(ref state, target, Dt, G, 0.8f, in Neutral, in s);
            }
            return outputs;
        }

        [Test]
        public void HeaveWeight_SurvivesACoarseFrameRate()
        {
            StormRockSettings s = StormRockSettings.Default;
            var state = new HeaveWeightState();

            // 10 fps — far below anything playable. The sub-stepped integrator must stay stable
            // (bounded by the band, no NaN), not explode the way raw Euler at ω·dt ≈ 0.7 could.
            for (int f = 0; f < 100; f++)
            {
                float t = f * 0.1f;
                float target = 2f * Mathf.Sin(1.5f * t);
                float output = StormRockMath.StepHeaveWeight(ref state, target, 0.1f, G, 1f, in Neutral, in s);
                Assert.IsFalse(float.IsNaN(output) || float.IsInfinity(output), $"frame {f}: not finite");
                Assert.LessOrEqual(Mathf.Abs(output - target), s.SurfaceBandMeters + 1e-4f,
                    $"frame {f}: out of band at a coarse dt — the sub-step ceiling is not holding");
            }
        }

        [Test]
        public void HeaveWeight_AHeavierHull_ReFindsTheWaterMoreSlowly()
        {
            StormRockSettings s = StormRockSettings.Default;
            var lively = new SeakeepingResponse(1f, 0f);      // the dory's data default
            var heavy = new SeakeepingResponse(0.25f, 0f);    // a laden trader's character

            var livelyState = new HeaveWeightState();
            var heavyState = new HeaveWeightState();
            float livelyOut = 0f, heavyOut = 0f;

            // Prime both on a still sea, then step the surface up 0.3 m (inside the band) and give
            // them a beat to chase.
            StormRockMath.StepHeaveWeight(ref livelyState, 0f, Dt, G, 1f, in lively, in s);
            StormRockMath.StepHeaveWeight(ref heavyState, 0f, Dt, G, 1f, in heavy, in s);
            for (int f = 0; f < 6; f++)
            {
                livelyOut = StormRockMath.StepHeaveWeight(ref livelyState, 0.3f, Dt, G, 1f, in lively, in s);
                heavyOut = StormRockMath.StepHeaveWeight(ref heavyState, 0.3f, Dt, G, 1f, in heavy, in s);
            }

            Assert.Greater(Mathf.Abs(0.3f - heavyOut), Mathf.Abs(0.3f - livelyOut),
                "the heavy hull must lag the surface MORE than the lively one — per-hull weight " +
                "from the BoatHullDef seakeeping data, the 'a dory corks, a dragger shrugs' intent");
        }

        [Test]
        public void HullAttitudeScale_ClampsIntoTheDataRange()
        {
            StormRockSettings s = StormRockSettings.Default;
            Assert.AreEqual(1f, StormRockMath.HullAttitudeScale(new SeakeepingResponse(1f, 0f), in s), 1e-6f);
            Assert.AreEqual(0.5f, StormRockMath.HullAttitudeScale(new SeakeepingResponse(0.5f, 0f), in s), 1e-6f);
            Assert.AreEqual(s.MaxHullResponseScale,
                StormRockMath.HullAttitudeScale(new SeakeepingResponse(99f, 0f), in s), 1e-6f,
                "an extreme liveliness must clamp to the data ceiling, never fling the visual");
        }
    }
}
