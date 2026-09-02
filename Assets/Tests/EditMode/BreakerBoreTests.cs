using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The bore — one crest at a time, PINNED and MEASURED (ADR 0040 revision 3, water-fidelity PR 1).</b>
    ///
    /// <para>The shipped surf was steady-state: a place, an age in metres, a boil. Revision 3 gives every
    /// position in the surf zone a CLOCK — the field's published phase at the break line it was born on,
    /// carried inshore by the march's own travel time — so a crest arrives, peaks and passes, T seconds
    /// after the last one, at the bore speed. These tests hold that the clock is periodic at the train's
    /// period, that it advances at √(g·d) and not at the deep-water celerity, that it is a read of the
    /// PUBLISHED phase and nothing accumulated, and that it does not sit on the march grid.</para>
    ///
    /// <para><b>The measurement is the important file, again.</b> The age lane learned twice that a derived
    /// quantity dies in the pipeline that consumes it, and only a measurement saw it. The travel time here
    /// rides the same taps as the metres, so it inherits the same defence — the partial gate at the
    /// surf-zone boundary — and the sabotage arm below proves it is that gate, and not luck, holding the
    /// resolution.</para>
    /// </summary>
    public class BreakerBoreTests
    {
        private const float G = 9.81f;

        /// <summary>A 1:25 sandy shoal, 3 m deep at the origin, rising toward +X; shore at x = 75 m. At the
        /// default tuning a 1 m swell breaks near x = 42, so the surf zone is ~33 m wide.</summary>
        private sealed class SandyShoal : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 0.04f * worldPos.x - 3f;
        }

        private static WaveTrain Swell(float amplitude = 0.5f, float wavelength = 18f)
            => new WaveTrain(Vector2.right, wavelength, amplitude, 0f, G);

        /// <summary>A one-train field: the swell alone (three silent fillers, count 1).</summary>
        private static WaveTrains OneTrain(in WaveTrain swell, float sharpening = 2.6f)
        {
            var silent = new WaveTrain(swell.Direction, swell.Wavelength, 0f, 0f, G);
            return new WaveTrains(swell, silent, silent, silent, 1, sharpening);
        }

        private static BreakerSettings Settings => BreakerSettings.Default;

        private static BreakerContour Contour(in WaveTrain train, in BreakerSettings settings)
            => BreakerMath.ContourFor(in train, 1f, in settings);

        /// <summary>Where the swell breaks on the shoal: the first x, walking in from deep water, where the
        /// contour gate is at least half open.</summary>
        private static float BreakX(ITidalTerrain terrain, in BreakerContour contour, float waterLevel)
        {
            for (float x = -300f; x < 120f; x += 0.05f)
            {
                float depth = waterLevel - terrain.ElevationAt(new Vector2(x, 0f));
                if (BreakerMath.Breaking01FromContour(depth, in contour, 1f) >= 0.5f) return x;
            }
            Assert.Fail("the swell must break on this shoal at all");
            return float.NaN;
        }

        private static void March(Vector2 pos, ITidalTerrain terrain, in BreakerContour contour,
                                  in BreakerSettings settings, out float meters, out float seconds, float level = 0f)
            => BreakerMath.MarchSinceBreakAlong(pos, Vector2.right, level, terrain, in contour, 1f, G,
                                                in settings, out meters, out seconds);

        // =========================================================================================
        //  Determinism, and the march that did not become two
        // =========================================================================================

        [Test]
        public void SameInputs_YieldTheSameBore_Exactly()
        {
            var terrain = new SandyShoal();
            var train = Swell();
            var field = OneTrain(in train);
            var settings = Settings;
            var contour = Contour(in train, in settings);

            for (float x = 44f; x < 74f; x += 1.5f)
            {
                var pos = new Vector2(x, 0f);
                SurfState a = BreakerMath.SurfAt(pos, 0f, terrain, in contour, 1f, in field, G, in settings);
                SurfState b = BreakerMath.SurfAt(pos, 0f, terrain, in contour, 1f, in field, G, in settings);
                Assert.AreEqual(a.Bore01, b.Bore01, "the bore is bit-stable");
                Assert.AreEqual(a.BorePhaseDegrees, b.BorePhaseDegrees, "the phase is bit-stable");
                Assert.AreEqual(a.TravelSeconds, b.TravelSeconds, "the travel time is bit-stable");
                Assert.AreEqual(a.RunUpMeters, b.RunUpMeters, "the run-up is bit-stable");
            }
        }

        [Test]
        public void MetersSinceBreakAlong_IsBitIdentical_ThroughTheCombinedMarch()
        {
            // The metres now come out of the same loop as the seconds. This is the loop as it shipped,
            // tap for tap — the combined march must return exactly what it returned before.
            var terrain = new SandyShoal();
            var train = Swell();
            var settings = Settings;
            var contour = Contour(in train, in settings);
            float step = Mathf.Max(BreakerMath.MinStepMeters, settings.WhitewaterStepMeters);

            for (float x = 40f; x < 75f; x += 0.7f)
            {
                var pos = new Vector2(x, 0f);
                float contiguous = 1f, age = 0f;
                for (int i = 1; i <= BreakerMath.MarchSteps; i++)
                {
                    Vector2 p = WaveFetch.Pixelize(new Vector2(pos.x - step * i, pos.y));
                    float depth = 0f - terrain.ElevationAt(p);
                    contiguous *= BreakerMath.Breaking01FromContour(depth, in contour, 1f);
                    age += contiguous;
                }
                float shipped = step * age;

                float now = BreakerMath.MetersSinceBreakAlong(pos, Vector2.right, 0f, terrain, in contour, 1f, in settings);
                Assert.AreEqual(shipped, now, $"x = {x}: the metres must not move by one bit");
            }
        }

        // =========================================================================================
        //  The travel time is physics: the march's integral of Δs / √(g·d)
        // =========================================================================================

        [Test]
        public void TheTravelTime_IsTheMarchsOwnIntegral_OfStepOverBoreSpeed()
        {
            // Deep inside the surf zone every tap's gate is 1, so the seconds are Σ step/√(g·dᵢ) over the
            // taps behind the position — compare with a fine numerical integral of the same integrand
            // along the same path from the break line. The march samples the integrand at 2 m; the
            // integrand varies slowly on a 1:25 shoal, so they agree to a few percent.
            var terrain = new SandyShoal();
            var train = Swell();
            var settings = Settings;
            var contour = Contour(in train, in settings);
            float breakX = BreakX(terrain, in contour, 0f);

            var pos = new Vector2(breakX + 14f, 0f);
            March(pos, terrain, in contour, in settings, out float meters, out float seconds);
            Assert.Greater(meters, 10f, "the position must be well inside the surf zone");

            // ∫ ds / √(g·d) from (pos − meters) to pos, at 1 cm.
            double integral = 0;
            const float ds = 0.01f;
            for (float s = 0f; s < meters; s += ds)
            {
                float depth = 0f - terrain.ElevationAt(new Vector2(pos.x - s, 0f));
                integral += ds / Math.Sqrt(G * Mathf.Max(BreakerMath.MinDepthMeters, depth));
            }

            Assert.AreEqual(integral, seconds, integral * 0.06,
                $"the marched travel time ({seconds:F3} s) must be the integral of Δs/√(g·d) ({integral:F3} s)");
        }

        [Test]
        public void TheBore_AdvancesAtTheBoreSpeed_NotAtTheDeepWaterCelerity()
        {
            // Two positions a known distance apart inside the zone: the extra travel time between them is
            // the distance over √(g·d), which on a metre of water is ~3 m/s — half the 18 m swell's
            // deep-water celerity. A bore that ran at the deep-water speed would arrive far too early.
            var terrain = new SandyShoal();
            var train = Swell();
            var settings = Settings;
            var contour = Contour(in train, in settings);
            float breakX = BreakX(terrain, in contour, 0f);

            var near = new Vector2(breakX + 8f, 0f);
            var far = new Vector2(breakX + 16f, 0f);
            March(near, terrain, in contour, in settings, out _, out float tNear);
            March(far, terrain, in contour, in settings, out _, out float tFar);

            float meanDepth = 0f - 0.5f * (terrain.ElevationAt(near) + terrain.ElevationAt(far));
            float atBoreSpeed = 8f / Mathf.Sqrt(G * meanDepth);
            float atDeepCelerity = 8f / train.PhaseSpeed;

            float dt = tFar - tNear;
            Assert.AreEqual(atBoreSpeed, dt, atBoreSpeed * 0.10f,
                $"8 m further inshore must cost ~{atBoreSpeed:F2} s at √(g·d), got {dt:F2} s");
            Assert.Greater(Mathf.Abs(dt - atDeepCelerity), atDeepCelerity * 0.25f,
                "and it must NOT be the deep-water celerity's crossing time");
        }

        // =========================================================================================
        //  The clock: periodic at T, and a crest's characteristic reappears inshore later
        // =========================================================================================

        [Test]
        public void ThePeriod_IsWavelengthOverCelerity_AndIsConservedThroughShoaling()
        {
            var train = Swell();
            float period = BreakerMath.PeriodSeconds(in train);
            Assert.AreEqual(train.Wavelength / train.PhaseSpeed, period, 1e-6f);
            Assert.AreEqual(Mathf.Sqrt(2f * Mathf.PI * train.Wavelength / G), period, 1e-4f,
                "an 18 m deep-water train has a ~3.4 s period");
            Assert.AreEqual(0f, BreakerMath.PeriodSeconds(new WaveTrain(Vector2.right, 18f, 0.5f, 0f, 0f)),
                "no gravity, no celerity, no period — a guard, not a crash");
        }

        [Test]
        public void TheBorePulse_IsPeriodicAtTheTrainsPeriod_AndSmoothEverywhere()
        {
            var train = Swell();
            float period = BreakerMath.PeriodSeconds(in train);
            var breakLine = new Vector2(42f, 0f);

            for (float tau = 0f; tau < 30f; tau += 0.37f)
            {
                float a = BreakerMath.BorePhaseDegrees(in train, breakLine, tau);
                float b = BreakerMath.BorePhaseDegrees(in train, breakLine, tau + period);
                Assert.AreEqual(a, b, 0.05f, $"τ = {tau}: one period later the phase must come round again");
                Assert.AreEqual(BreakerMath.BorePulse01(a, 2.6f), BreakerMath.BorePulse01(b, 2.6f), 1e-3f);
            }

            // SMOOTH: no cutoff anywhere. The largest step between neighbouring phases at a fine spacing
            // is bounded by the derivative of a raised cosine — a hard front would show a jump of ~1.
            float previous = BreakerMath.BorePulse01(0f, 2.6f);
            float largestStep = 0f;
            for (float phase = 0.5f; phase <= 360f; phase += 0.5f)
            {
                float pulse = BreakerMath.BorePulse01(phase, 2.6f);
                largestStep = Mathf.Max(largestStep, Mathf.Abs(pulse - previous));
                previous = pulse;
            }
            Assert.Less(largestStep, 0.02f, "the pulse has no front you could sit on a grid");
            Assert.AreEqual(1f, BreakerMath.BorePulse01(90f, 2.6f), 1e-6f, "the front is the crest, 90°");
            Assert.AreEqual(0f, BreakerMath.BorePulse01(270f, 2.6f), 1e-6f, "the quiet is the trough, 270°");
        }

        [Test]
        public void ACrestsCharacteristic_ReappearsInshore_ExactlyItsTravelTimeLater()
        {
            // ⭐ THE CLAIM THAT MAKES IT A BORE. Watch one position P inside the zone and the break line B
            // it was born on over one wave period of published time. The pulse at P must peak exactly
            // TravelSeconds after the pulse at B — the crest that broke at B is the same crest that
            // arrives at P, carried at the bore speed. Getting the sign of the phase read wrong would put
            // the peak τ seconds EARLY, and this test is what would catch it.
            var terrain = new SandyShoal();
            var train = Swell();
            var settings = Settings;
            var contour = Contour(in train, in settings);
            float breakX = BreakX(terrain, in contour, 0f);
            float period = BreakerMath.PeriodSeconds(in train);

            var p = new Vector2(breakX + 12f, 0f);
            March(p, terrain, in contour, in settings, out float meters, out float tau);
            Vector2 b = BreakerMath.BreakLinePoint(p, Vector2.right, meters);
            Assert.AreEqual(p.x - meters, b.x, 1e-4f, "the break line is the marched distance back upwave");

            // Published time t advances the whole field: a train sampled at time t is the train with its
            // travel baked in. The bore phase at P at time t is the phase at B at (t − τ).
            const float dt = 0.01f;
            float peakAtB = -1f, bestB = -1f, peakAtP = -1f, bestP = -1f;
            for (float t = 0f; t < period; t += dt)
            {
                float phaseB = WaveMath.TrainPhaseDegrees(in train, b, t);
                float pulseB = BreakerMath.BorePulse01(phaseB, settings.BorePulseSharpness);
                if (pulseB > bestB) { bestB = pulseB; peakAtB = t; }

                float phaseP = WaveMath.TrainPhaseDegrees(in train, b, t - tau);   // = BorePhaseDegrees at time t
                float pulseP = BreakerMath.BorePulse01(phaseP, settings.BorePulseSharpness);
                if (pulseP > bestP) { bestP = pulseP; peakAtP = t; }
            }
            float lag = Mathf.Repeat(peakAtP - peakAtB, period);
            float expected = Mathf.Repeat(tau, period);
            float error = Mathf.Min(Mathf.Abs(lag - expected), period - Mathf.Abs(lag - expected));
            Assert.Less(error, dt * 2f,
                $"the front must reach P {tau:F3} s after it left B (mod T = {period:F3} s); measured lag {lag:F3} s");

            // And BorePhaseDegrees is that read, at published time 0.
            Assert.AreEqual(WaveMath.TrainPhaseDegrees(in train, b, -tau),
                            BreakerMath.BorePhaseDegrees(in train, b, tau), 1e-3f);
        }

        // =========================================================================================
        //  The measurement: the clock does not sit on the march grid
        // =========================================================================================

        private static (List<float> seconds, List<float> phases) SweepTheSurfZone(in BreakerSettings settings)
        {
            var terrain = new SandyShoal();
            var train = Swell();
            var contour = Contour(in train, in settings);
            float breakX = BreakX(terrain, in contour, 0f);

            var seconds = new List<float>();
            var phases = new List<float>();
            for (float x = breakX; ; x += 0.25f)
            {
                var pos = new Vector2(x, 0f);
                float depth = 0f - terrain.ElevationAt(pos);
                if (depth <= 0.05f) break;
                March(pos, terrain, in contour, in settings, out float meters, out float tau);
                seconds.Add(tau);
                phases.Add(BreakerMath.BorePhaseDegrees(in train, BreakerMath.BreakLinePoint(pos, Vector2.right, meters), tau));
            }
            return (seconds, phases);
        }

        private static int DistinctTo(IEnumerable<float> values, int decimals)
            => values.Select(v => (float)Math.Round(v, decimals)).Distinct().Count();

        private static float TopShare(IEnumerable<float> values, int decimals)
        {
            var rounded = values.Select(v => (float)Math.Round(v, decimals)).ToList();
            return rounded.GroupBy(v => v).Max(g => g.Count()) / (float)rounded.Count;
        }

        [Test]
        public void TheTravelTime_KeepsItsResolution_AcrossTheSurfZone_Measured()
        {
            // Measured 2026-09-01 at the shipped tuning: the same 128 samples the age measurement walks,
            // and the seconds are as distinct as the metres — the partial gate at the boundary supplies
            // the sub-step fraction to both integrals, because they are the same integral.
            var (seconds, phases) = SweepTheSurfZone(Settings);
            Assert.GreaterOrEqual(seconds.Count, 100, "the sweep must actually cover a surf zone");
            Assert.GreaterOrEqual(DistinctTo(seconds, 3), 100,
                "the travel time must not be quantized to the march step — the #665 metric, on the clock");
            for (int i = 1; i < seconds.Count; i++)
                Assert.GreaterOrEqual(seconds[i], seconds[i - 1] - 1e-4f, $"travel time grows shoreward (sample {i})");
            Assert.Less(TopShare(phases, 0), 0.10f,
                "no single bore phase may dominate the zone — a clock on a grid would read the same value for metres");
            Assert.Greater(seconds[seconds.Count - 1], 5f, "a bore that has run to the top of the beach has been running for seconds");
        }

        /// <summary>The smooth expectation for the clock's increment between two samples Δs apart: the
        /// time a bore takes to cross that distance at the local bore speed, <c>Δs / √(g·d)</c>.</summary>
        private static float SmoothIncrement(float x, float ds)
        {
            var terrain = new SandyShoal();
            float depth = 0f - terrain.ElevationAt(new Vector2(x, 0f));
            return ds / Mathf.Sqrt(G * Mathf.Max(BreakerMath.MinDepthMeters, depth));
        }

        [Test]
        public void ANearHardBreakGate_MakesTheClockJUMP_WhichIsWhatTheSmoothGateBuys()
        {
            // ⭐ The sabotage arm — and a clock needs a different metric from the age's. The metres fall
            // onto the 2 m grid under a hard gate and a distinct-value count sees it. The SECONDS do not:
            // each tap's contribution is 1/√(g·dᵢ) at that tap's own depth, and the taps slide down the
            // slope as the sample moves, so a hard-gated clock stays continuous WITHIN each plateau of
            // tap count and then JUMPS by a whole tap's worth (~0.5 s at the outer taps) every 2 m.
            // Measured on the first run of this file: 129 distinct values shipped, 113 hard-gated — the
            // count could not tell them apart. What tells them apart is CONTINUITY: the largest increment
            // between neighbouring samples against the smooth expectation Δs/√(g·d). A bore whose clock
            // jumps half a second every two metres is a front that teleports.
            var hardCutoff = Settings;
            hardCutoff.BreakBandRatio = 0.01f;

            float shippedWorst = WorstJumpRatio(Settings);
            float cutoffWorst = WorstJumpRatio(hardCutoff);
            var (shippedSeconds, _) = SweepTheSurfZone(Settings);
            var (cutoffSeconds, _) = SweepTheSurfZone(hardCutoff);
            Debug.Log($"[bore-clock] worst neighbour increment vs smooth: shipped {shippedWorst:F2}x, " +
                      $"hard-gated {cutoffWorst:F2}x; distinct travel times shipped {DistinctTo(shippedSeconds, 3)}, " +
                      $"hard-gated {DistinctTo(cutoffSeconds, 3)} of {shippedSeconds.Count} samples");
            Assert.LessOrEqual(shippedWorst, 2f,
                $"the shipped clock must advance smoothly — its worst neighbour increment is {shippedWorst:F2}× " +
                "the local Δs/√(g·d)");
            Assert.GreaterOrEqual(cutoffWorst, 3f,
                $"a near-hard gate must make the clock jump (worst increment {cutoffWorst:F2}× smooth) — the " +
                "partial gate at the surf-zone boundary is what keeps a bore front off the march grid");
        }

        /// <summary>The largest increment of the travel time between neighbouring sweep samples, as a
        /// multiple of the smooth expectation at that spot. 1 = a perfectly continuous clock.</summary>
        private static float WorstJumpRatio(in BreakerSettings settings)
        {
            var terrain = new SandyShoal();
            var train = Swell();
            var contour = Contour(in train, in settings);
            float breakX = BreakX(terrain, in contour, 0f);
            const float ds = 0.25f;

            float worst = 0f;
            float previous = float.NaN;
            for (float x = breakX + 2f; ; x += ds)     // start clear of the outer boundary's own ramp
            {
                var pos = new Vector2(x, 0f);
                if (0f - terrain.ElevationAt(pos) <= 0.3f) break;   // stop before the beach, where d → 0
                March(pos, terrain, in contour, in settings, out _, out float tau);
                if (!float.IsNaN(previous))
                {
                    float ratio = (tau - previous) / SmoothIncrement(x - ds * 0.5f, ds);
                    worst = Mathf.Max(worst, ratio);
                }
                previous = tau;
            }
            return worst;
        }

        // =========================================================================================
        //  The tide, glass, a stale asset — the invariants every breaker read keeps
        // =========================================================================================

        [Test]
        public void TheTide_MovesTheBoreWithTheBreakLine()
        {
            var terrain = new SandyShoal();
            var train = Swell();
            var settings = Settings;
            var contour = Contour(in train, in settings);

            // One position, two tides: at high water the break line has walked shoreward, so this spot
            // is closer to (or outside) the surf and its bore is younger.
            var pos = new Vector2(60f, 0f);
            March(pos, terrain, in contour, in settings, out float mLow, out float tLow, level: -0.5f);
            March(pos, terrain, in contour, in settings, out float mHigh, out float tHigh, level: 0.6f);
            Assert.Greater(mLow, mHigh, "at low water the same spot is further past the break line");
            Assert.Greater(tLow, tHigh, "…and its bore has been running longer");
            Assert.Less(BreakerMath.BreakLinePoint(pos, Vector2.right, mLow).x,
                        BreakerMath.BreakLinePoint(pos, Vector2.right, mHigh).x,
                        "the break line this bore was born on sits further out at low water");
        }

        [Test]
        public void GlassCalm_HasNoBore()
        {
            var terrain = new SandyShoal();
            var glass = new WaveTrain(Vector2.right, 18f, 0f, 0f, G);
            var field = OneTrain(in glass);
            var settings = Settings;
            var contour = Contour(in glass, in settings);
            Assert.IsFalse(contour.Breaks, "a glass sea has no contour");

            for (float x = 40f; x < 74f; x += 2f)
            {
                SurfState s = BreakerMath.SurfAt(new Vector2(x, 0f), 0f, terrain, in contour, 1f, in field, G, in settings);
                Assert.AreEqual(0f, s.Bore01, "glass is sacred: no surf, no bore");
                Assert.AreEqual(0f, s.RunUpMeters, "…and no wash runs up");
            }
        }

        [Test]
        public void AStaleSettingsStruct_IsTheSteadyState_NotWrong()
        {
            // A GameConfig serialized before revision 3 reads the four new fields as 0. Zero sharpness is
            // NO pulse (every phase reads 1) and zero coefficient is NO run-up: exactly the surf that
            // shipped before the bore existed. Inert, not wrong — the WaveFetch/Breakers property.
            var stale = Settings;
            stale.BorePulseSharpness = 0f;
            stale.BoreSetStrength = 0f;
            stale.RunUpCoefficient = 0f;
            stale.RunUpCapMeters = 0f;

            var terrain = new SandyShoal();
            var train = Swell();
            var field = OneTrain(in train);
            var contour = Contour(in train, in stale);
            var live = Settings;
            var liveContour = Contour(in train, in live);

            for (float x = 44f; x < 74f; x += 1f)
            {
                var pos = new Vector2(x, 0f);
                SurfState s = BreakerMath.SurfAt(pos, 0f, terrain, in contour, 1f, in field, G, in stale);
                Assert.AreEqual(1f, s.Bore01, "no pulse — the steady state");
                Assert.AreEqual(0f, s.RunUpMeters, "no run-up");

                // …and the steady terms are bit-identical to the overload that has no field at all.
                SurfState steady = BreakerMath.SurfAt(pos, 0f, terrain, in liveContour, 1f,
                                                      2f * train.Amplitude, train.Wavelength, in live);
                SurfState withBore = BreakerMath.SurfAt(pos, 0f, terrain, in liveContour, 1f, in field, G, in live);
                Assert.AreEqual(steady.DepthMeters, withBore.DepthMeters);
                Assert.AreEqual(steady.Breaking01, withBore.Breaking01);
                Assert.AreEqual(steady.Whitewater01, withBore.Whitewater01);
                Assert.AreEqual(steady.StandingHeightMeters, withBore.StandingHeightMeters);
                Assert.AreEqual(steady.PlungingWeight01, withBore.PlungingWeight01);
                Assert.AreEqual(steady.ShorewardDirection, withBore.ShorewardDirection);
                Assert.AreEqual(1f, steady.Bore01, "the field-less overload is the steady state by construction");
                Assert.AreEqual(0f, steady.RunUpMeters);
            }
        }

        // =========================================================================================
        //  The birth energy and the run-up
        // =========================================================================================

        [Test]
        public void BirthEnergy_IsOneForEveryCrestOfASingleTrain_AndSwingsWithASet()
        {
            var terrain = new SandyShoal();
            var train = Swell();
            var settings = Settings;
            var contour = Contour(in train, in settings);
            float breakX = BreakX(terrain, in contour, 0f);
            float period = BreakerMath.PeriodSeconds(in train);
            var b = new Vector2(breakX, 0f);

            // One train: every crest is the envelope, so every bore is born at exactly 1 — however far
            // into the bore's back the read is taken.
            var one = OneTrain(in train);
            for (float tau = 0f; tau < 12f; tau += 0.31f)
            {
                float phase = BreakerMath.BorePhaseDegrees(in train, b, tau);
                float birth = BreakerMath.BoreBirthEnergy01(in one, b, tau, phase, period, 1f, 1f);
                Assert.AreEqual(1f, birth, 2e-3f, $"τ = {tau}: a lone train's crest IS the envelope");
            }

            // The real field: eight JONSWAP-shaped trains with groups. Born energies over a minute of
            // bores must SWING — big ones and small ones — which is the set, for free.
            WaveTrains sea = WaveMath.TrainsFrom(new Vector2(6f, -5.3f), 0.55f, WaveFieldSettings.Default);
            WaveTrain dominant = sea.Dominant;
            float seaPeriod = BreakerMath.PeriodSeconds(in dominant);
            float min = 1f, max = 0f;
            for (float tau = 0f; tau < 90f; tau += 0.5f)
            {
                float phase = BreakerMath.BorePhaseDegrees(in dominant, b, tau);
                float birth = BreakerMath.BoreBirthEnergy01(in sea, b, tau, phase, seaPeriod, 1f, 1f);
                min = Mathf.Min(min, birth); max = Mathf.Max(max, birth);
            }
            Assert.Greater(max - min, 0.3f, $"a spectrum makes sets: born energies must range, got {min:F2}..{max:F2}");

            // Set strength 0 = every bore born at full energy.
            Assert.AreEqual(1f, BreakerMath.BoreBirthEnergy01(in sea, b, 3f, 200f, seaPeriod, 1f, 0f));
        }

        [Test]
        public void TheTravelWhitewater_AgreesAtTheBreakLine_AndOutlivesTheLocalLawInTheShallows()
        {
            var terrain = new SandyShoal();
            var train = Swell();
            var field = OneTrain(in train);
            var settings = Settings;
            var contour = Contour(in train, in settings);
            float breakX = BreakX(terrain, in contour, 0f);

            Assert.AreEqual(1f, BreakerMath.WhitewaterByTravel01(0f, settings.WhitewaterDecaySeconds), 1e-6f,
                "no travel, no decay: the two laws agree at the break line");
            Assert.AreEqual(Mathf.Exp(-1f), BreakerMath.WhitewaterByTravel01(settings.WhitewaterDecaySeconds,
                settings.WhitewaterDecaySeconds), 1e-6f, "one time constant of travel is one e-fold");

            // Shoreward of the break line the bed shoals, so the local speed at any point is slower than
            // the speeds the bore actually ran at to get there: the local law over-ages every point, and
            // the gap widens toward the edge. Measured on the shoal, not argued.
            float lastLocal = 1f, lastTravel = 1f;
            int shallower = 0, outlived = 0;
            for (float x = breakX + 1f; x < breakX + 30f; x += 0.5f)
            {
                SurfState s = BreakerMath.SurfAt(new Vector2(x, 0f), 0f, terrain, in contour, 1f, in field, G, in settings);
                if (s.Whitewater01 <= 0f && s.TravelSeconds <= 0f) continue;
                float byTravel = BreakerMath.WhitewaterByTravel01(s.TravelSeconds, settings.WhitewaterDecaySeconds);
                Assert.GreaterOrEqual(byTravel, s.Whitewater01 - 1e-5f,
                    $"x = {x}: the travel law ({byTravel:F3}) fell below the local law ({s.Whitewater01:F3}) on a shoaling bed");
                if (s.DepthMeters < contour.BreakDepths.x * 0.5f) { shallower++; if (byTravel > s.Whitewater01 * 1.5f) outlived++; }
                lastLocal = s.Whitewater01; lastTravel = byTravel;
            }
            Assert.Greater(shallower, 3, "the sweep must reach the shallows");
            Assert.Greater(outlived, 0, "in the shallows the travel law must outlive the local one by half again");
            Assert.Greater(lastTravel, lastLocal, "at the last point asked the wash is more alive by travel than by the local speed");
        }

        [Test]
        public void TheSignedSeconds_ArePositiveBehindTheFront_NegativeAhead_AndWrapAtHalfAPeriod()
        {
            const float period = 4f;
            Assert.AreEqual(0f, BreakerMath.SignedSecondsFromCrest(90f, period), 1e-6f, "at the front");
            Assert.AreEqual(1f, BreakerMath.SignedSecondsFromCrest(0f, period), 1e-6f, "a quarter period BEHIND the front");
            Assert.AreEqual(-1f, BreakerMath.SignedSecondsFromCrest(180f, period), 1e-6f, "a quarter period AHEAD of it");
            Assert.AreEqual(-2f, BreakerMath.SignedSecondsFromCrest(270f, period), 1e-6f, "the trough is half a period either way; the wrap lands on -T/2");
            Assert.AreEqual(BreakerMath.SignedSecondsFromCrest(30f, period), BreakerMath.SignedSecondsFromCrest(390f, period), 1e-6f);
            // Behind the front the unsigned and signed clocks agree; ahead, the unsigned one says "almost a period ago".
            Assert.AreEqual(BreakerMath.SecondsSinceTheCrest(0f, period), BreakerMath.SignedSecondsFromCrest(0f, period), 1e-6f);
            Assert.AreEqual(3f, BreakerMath.SecondsSinceTheCrest(180f, period), 1e-6f);
        }

        [Test]
        public void TheSheet_IsBornAtTheFront_AndAgesOnTheWhitewatersSeconds()
        {
            const float period = 4f, decay = 3.5f;
            Assert.AreEqual(1f, BreakerMath.BoreSheet01(90f, period, decay), 1e-6f, "at the front the sheet is whole");
            float behind = BreakerMath.BoreSheet01(0f, period, decay);          // a quarter period behind the front
            Assert.AreEqual(Mathf.Exp(-1f / decay), behind, 1e-6f, "one second behind: the whitewater's own decay");
            float ahead = BreakerMath.BoreSheet01(180f, period, decay);         // ahead of the front: the previous crest's water
            Assert.Less(ahead, behind, "ahead of the front is the PREVIOUS crest's old water");
            Assert.Greater(ahead, 0f, "and it is old, not gone");
            Assert.AreEqual(BreakerMath.BoreSheet01(30f, period, decay), BreakerMath.BoreSheet01(390f, period, decay), 1e-6f,
                "a whole turn of phase is the same sheet");
            Assert.AreEqual(1f, BreakerMath.BoreSheet01(90f, period, 0f), 1e-6f, "a zero decay is floored, never a division by zero");
        }

        [Test]
        public void TheBirthRead_IsOfTheCrest_SoEveryPointOnOneBoresBackReadsTheSameBirth()
        {
            // Two points on the same bore's back (their phases differ, their crest is the same crest)
            // must be born of the same read. SecondsSinceTheCrest is what folds a phase back to it.
            Assert.AreEqual(0f, BreakerMath.SecondsSinceTheCrest(90f, 4f), 1e-6f, "at the front, no time has passed");
            Assert.AreEqual(1f, BreakerMath.SecondsSinceTheCrest(0f, 4f), 1e-6f, "a quarter period past the front");
            Assert.AreEqual(3f, BreakerMath.SecondsSinceTheCrest(180f, 4f), 1e-6f,
                "ahead of the front the water belongs to the PREVIOUS crest, three quarters of a period ago");

            var train = Swell();
            float period = BreakerMath.PeriodSeconds(in train);
            var b = new Vector2(42f, 0f);
            // A point where the front passed δ seconds ago has phase 90° − ω·δ; folding it back lands on
            // the same crest time as the front itself.
            float tauFront = 5f;
            float delta = 0.7f;
            float phaseBehind = BreakerMath.BorePhaseDegrees(in train, b, tauFront + delta);
            double crestTimeFront = -(tauFront + BreakerMath.SecondsSinceTheCrest(BreakerMath.BorePhaseDegrees(in train, b, tauFront), period));
            double crestTimeBehind = -(tauFront + delta + BreakerMath.SecondsSinceTheCrest(phaseBehind, period));
            // Both fold to a crest time; the two crest times differ by a whole number of periods (or none).
            double periods = (crestTimeFront - crestTimeBehind) / period;
            Assert.AreEqual(Math.Round(periods), periods, 0.02, "the fold lands both reads on a crest passage");
        }

        [Test]
        public void RunUp_IsHuntsLaw_AndNeverExceedsTheDrawnEdgeCeiling()
        {
            var settings = Settings;
            // R = ξ · H on a bed the law was measured on, scaled by what is left of the bore and its pulse.
            Assert.AreEqual(0.5f * 0.4f, BreakerMath.RunUpMeters(0.4f, 1f, 0.5f, 1f, in settings), 1e-6f);
            Assert.AreEqual(0.5f * 0.4f * 0.5f, BreakerMath.RunUpMeters(0.4f, 0.5f, 0.5f, 1f, in settings), 1e-6f);
            Assert.AreEqual(0.5f * 0.4f * 0.25f, BreakerMath.RunUpMeters(0.4f, 0.5f, 0.5f, 0.5f, in settings), 1e-6f);

            // The ceiling: a steep bank under a tall bore would run up a metre; the drawn edge may not.
            Assert.AreEqual(settings.RunUpCapMeters, BreakerMath.RunUpMeters(1.2f, 1f, 2f, 1f, in settings), 1e-6f);
            Assert.LessOrEqual(BreakerMath.RunUpMeters(3f, 1f, 5f, 1f, in settings), settings.RunUpCapMeters);

            // Hunt's range: ξ is clamped at its measured limit rather than growing without bound.
            Assert.AreEqual(BreakerMath.RunUpMeters(0.1f, 1f, BreakerMath.HuntIribarrenLimit, 1f, in settings),
                            BreakerMath.RunUpMeters(0.1f, 1f, 9f, 1f, in settings), 1e-6f);

            // Inert: a stale coefficient or cap.
            var stale = settings; stale.RunUpCoefficient = 0f;
            Assert.AreEqual(0f, BreakerMath.RunUpMeters(1f, 1f, 1f, 1f, in stale));
            var capped = settings; capped.RunUpCapMeters = 0f;
            Assert.AreEqual(0f, BreakerMath.RunUpMeters(1f, 1f, 1f, 1f, in capped));
        }

        [Test]
        public void TheRunUp_PulsesWithTheBore_AcrossTheZone()
        {
            var terrain = new SandyShoal();
            var train = Swell();
            var field = OneTrain(in train);
            var settings = Settings;
            var contour = Contour(in train, in settings);
            float breakX = BreakX(terrain, in contour, 0f);

            float minRunUp = float.MaxValue, maxRunUp = 0f;
            for (float x = breakX + 1f; x < breakX + 30f; x += 0.25f)
            {
                SurfState s = BreakerMath.SurfAt(new Vector2(x, 0f), 0f, terrain, in contour, 1f, in field, G, in settings);
                Assert.LessOrEqual(s.RunUpMeters, settings.RunUpCapMeters + 1e-6f);
                // Hunt's law on the height the bore was BORN with (gamma times the break-line depth),
                // not the local standing height, which is 0 at the very edge where the reach matters.
                float standingAtBreak = settings.BreakerIndex *
                    BreakerMath.DepthAtEnvelope(contour.BreakDepths, contour.LeeEnvelope, 1f);
                float aliveByTravel = BreakerMath.WhitewaterByTravel01(s.TravelSeconds, settings.WhitewaterDecaySeconds);
                Assert.AreEqual(BreakerMath.RunUpMeters(standingAtBreak, aliveByTravel,
                                    BreakerMath.Iribarren(BreakerMath.BedSlopeAlong(new Vector2(x, 0f), s.ShorewardDirection,
                                        settings.SlopeProbeMeters, terrain), 2f * train.Amplitude, train.Wavelength),
                                    s.Bore01, in settings),
                                s.RunUpMeters, 1e-6f, "SurfAt composes the same run-up the pure law gives");
                minRunUp = Mathf.Min(minRunUp, s.RunUpMeters);
                maxRunUp = Mathf.Max(maxRunUp, s.RunUpMeters);
            }
            Assert.Greater(maxRunUp, 0f, "somewhere in the zone the wash reaches up");
            Assert.Less(minRunUp, maxRunUp * 0.2f, "and between bores it drains — the run-up pulses");
        }

        // =========================================================================================
        //  Guards
        // =========================================================================================

        [Test]
        public void NoOutputIsEverNaNOrInfinite_AcrossAHostileSweep()
        {
            var terrain = new SandyShoal();
            var settings = Settings;
            foreach (float amplitude in new[] { 0f, 1e-5f, 0.5f, 3f })
            foreach (float wavelength in new[] { 0.01f, 2f, 18f, 60f })
            foreach (float level in new[] { -3f, 0f, 2f, 50f })
            {
                var train = new WaveTrain(Vector2.right, wavelength, amplitude, 0f, G);
                var field = OneTrain(in train);
                var contour = Contour(in train, in settings);
                foreach (float x in new[] { -100f, 0f, 42f, 60f, 74.9f, 200f })
                {
                    var pos = new Vector2(x, 0f);
                    SurfState s = BreakerMath.SurfAt(pos, level, terrain, in contour, 1f, in field, G, in settings);
                    foreach (float v in new[] { s.Bore01, s.BorePhaseDegrees, s.TravelSeconds, s.BirthEnergy01, s.RunUpMeters })
                    {
                        Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), $"NaN/Inf at A={amplitude} L={wavelength} lvl={level} x={x}");
                    }
                    Assert.That(s.Bore01, Is.InRange(0f, 1f));
                    Assert.That(s.BirthEnergy01, Is.InRange(0f, 1f));
                    Assert.That(s.BorePhaseDegrees, Is.InRange(0f, 360f));
                    Assert.GreaterOrEqual(s.TravelSeconds, 0f);
                }
            }
        }

        [Test]
        public void TheWaveField_IsNotTouched_TheBoreIsAReadOverIt()
        {
            // Revision 3 reads WaveMath.TrainPhaseDegrees and WaveMath.Sample and rewrites nothing: the
            // field a hull rides is the field it rode yesterday. Pinned by evaluating the field before and
            // after every bore read.
            var train = Swell();
            var field = OneTrain(in train);
            var terrain = new SandyShoal();
            var settings = Settings;
            var contour = Contour(in train, in settings);

            var probe = new Vector2(30f, 4f);
            WaveSample before = WaveMath.Sample(probe, 12.5, in field);
            for (float x = 44f; x < 74f; x += 3f)
                BreakerMath.SurfAt(new Vector2(x, 0f), 0f, terrain, in contour, 1f, in field, G, in settings);
            WaveSample after = WaveMath.Sample(probe, 12.5, in field);
            Assert.AreEqual(before.Height, after.Height);
            Assert.AreEqual(before.Slope, after.Slope);
            Assert.AreEqual(before.CrestFactor, after.CrestFactor);
        }

        [Test]
        public void TheDefaults_AreTheFieldsOwnPinch_HuntsSlope_AndTheDrawnEdgeCeiling()
        {
            var d = BreakerSettings.Default;
            // The SHIPPED field's crest sharpening is 2.6 — on GameConfig.asset since #372 (the owner's
            // spectrum verdict); the code's WaveFieldSettings.Default still says 2.2, and the asset is the
            // authority (the GameConfigAssetCoverage law). A bore is as pinched as the crest that made it.
            Assert.AreEqual(2.6f, d.BorePulseSharpness, 1e-6f,
                "a bore is as pinched as the crest that made it — the asset's CrestSharpening, 2.6");
            Assert.AreEqual(1f, d.BoreSetStrength, "the sets are on");
            Assert.AreEqual(1f, d.RunUpCoefficient, "Hunt 1959: R = ξ·H");
            Assert.AreEqual(0.35f, d.RunUpCapMeters, 1e-6f, "the swash's drawn-edge ceiling, shared");
        }
    }
}
