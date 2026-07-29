using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// **THE PASSTHROUGH PROOF** for the ADR 0027 P2 spectrum work (the shared wave field, ADR 0018).
    ///
    /// <para>P2 widens the field from a fixed 4 trains to <see cref="WaveTrains.MaxTrains"/> and then
    /// (PR B) re-weights it with a JONSWAP spectrum. Both steps are only safe if the DEFAULT sea is
    /// provably unchanged — the hulls physically ride this field, so a silent drift here is a silent
    /// change to how every boat handles. This file is the guard, and it is deliberately written
    /// <b>before</b> the feature (the P1 discipline).</para>
    ///
    /// <para><b>Why a frozen copy and not captured goldens.</b> <see cref="LegacyTrainsFrom"/> is a
    /// verbatim transcription of the derivation as it shipped at <c>0a11099</c> — the 4-train
    /// primary + three-secondary sum, its <c>WaveTrain</c> clamps, its integer phase hash and its
    /// rotation, character for character. Asserting the live <see cref="WaveMath.TrainsFrom"/>
    /// against it is stronger than a golden table: a golden pins the values at the sampled inputs,
    /// this pins the whole FUNCTION across the sweep, needs no capture run, and stays readable — a
    /// reviewer can diff the two derivations side by side. It also survives into PR B unchanged as
    /// the <b>blend-0</b> proof: with the spectrum dialled out, the sea must be this sea.</para>
    ///
    /// <para><b>To the last ULP — and why not bit-for-bit.</b> This file originally asserted bit
    /// equality and <b>failed by 1–2 ULP</b> on the rotated secondary directions. The cause is not a
    /// transcription error: .NET may contract <c>a*b + c*d</c> into an FMA, and whether it does
    /// depends on inlining context, so <b>two copies of the same source expression are not
    /// guaranteed to produce the same bits</b> even in the same process. That is a fact about the
    /// runtime, not about the sea. Comparisons here are therefore in <b>ULP distance</b> (see
    /// <see cref="AssertUlp"/>) — around 7 orders of magnitude tighter than an epsilon, tight enough
    /// that any reordered expression, moved clamp or changed constant still fails, and honest about
    /// what a second transcription can promise.</para>
    ///
    /// <para><b>Where bit-exactness IS asserted:</b>
    /// <c>WaveFieldSeamWidthTests.TrainsFrom_IsDeterministicAcrossProcessRuns</c>, which hashes the
    /// raw bit patterns against a golden captured in an earlier process. That test has only ONE copy
    /// of the code, so the FMA question does not arise, and it is the one that actually states rule
    /// 5's claim: the same inputs give the same bits, in another process, on another day.</para>
    ///
    /// <para>(The C#↔HLSL twin tolerance is a third, looser question and stays at ADR 0018 §(4)'s
    /// visual parity — see <c>WaveFieldBridgeTests</c>.)</para>
    /// </summary>
    public class WaveSpectrumPassthroughTests
    {
        // A sweep that exercises every branch of the derivation: dead calm (the +Y downwind
        // fallback AND glass), light/strong/gale winds through all four quadrants, and the sea-state
        // axis at both ends plus the middle. Wavelengths hit the DominantWavelengthMax ceiling at
        // the gale, so the clamp branch is covered too.
        private static readonly Vector2[] Winds =
        {
            Vector2.zero,                      // dead calm — the defined +Y downwind
            new Vector2(3f, 1f),
            new Vector2(-6f, 4f),
            new Vector2(0f, -11f),
            new Vector2(-2.5f, -7.25f),
            new Vector2(24f, -18f),            // gale: past the wavelength ceiling
        };

        private static readonly float[] SeaStates = { 0f, 0.05f, 0.25f, 0.6f, 0.87f, 1f };

        // ===== THE PROOF =================================================================================

        [Test]
        public void TrainsFrom_AtDefaults_MatchesTheShippedDerivation_ToTheLastUlp()
        {
            var settings = WaveFieldSettings.Default;

            foreach (var wind in Winds)
            foreach (var sea in SeaStates)
            {
                WaveTrains live = WaveMath.TrainsFrom(wind, sea, in settings);
                WaveTrains legacy = LegacyTrainsFrom(wind, sea, in settings);
                AssertFieldsMatch(legacy, live, $"wind {wind} sea {sea}");
            }
        }

        [Test]
        public void TrainsFrom_AcrossTunedSettings_MatchesTheShippedDerivation_ToTheLastUlp()
        {
            // The owner tunes this struct (rule 6), so passthrough must hold off the defaults too —
            // including the secondary-count clamp, which is the one line the widening genuinely
            // changes shape (it used to clamp against MaxTrains − 1 = 3; it must keep clamping to
            // the three secondaries the settings can actually express, whatever MaxTrains becomes).
            var tunings = new[]
            {
                Mutate(s => { s.SecondaryTrainCount = 0; return s; }),   // primary only
                Mutate(s => { s.SecondaryTrainCount = 1; return s; }),
                Mutate(s => { s.SecondaryTrainCount = 2; return s; }),
                Mutate(s => { s.SecondaryTrainCount = 3; return s; }),
                Mutate(s => { s.SecondaryTrainCount = 7; return s; }),   // ⚠ over-set: must still clamp to 4 live
                Mutate(s => { s.SecondaryTrainCount = 99; return s; }),  // ⚠ absurd: same
                Mutate(s => { s.SecondaryTrainCount = -4; return s; }),  // ⚠ negative: primary only
                Mutate(s => { s.CrestSharpening = 1f; return s; }),
                Mutate(s => { s.CrestSharpening = 4.6f; return s; }),
                Mutate(s => { s.SeaStateAmplitudeExponent = 1f; return s; }),
                Mutate(s => { s.PhaseSeed = 20260729; return s; }),
                Mutate(s => { s.Gravity = 1.62f; return s; }),           // the moon, why not — g is a tunable
                Mutate(s => { s.DominantWavelengthPerWindSpeed = 0f; return s; }),
                Mutate(s => { s.Secondary2AmplitudeRatio = 0f; return s; }),   // a silent middle slot
            };

            foreach (var settings in tunings)
            foreach (var wind in Winds)
            foreach (var sea in SeaStates)
            {
                WaveTrains live = WaveMath.TrainsFrom(wind, sea, in settings);
                WaveTrains legacy = LegacyTrainsFrom(wind, sea, in settings);
                AssertFieldsMatch(legacy, live,
                    $"secondaries {settings.SecondaryTrainCount} p {settings.CrestSharpening} " +
                    $"seed {settings.PhaseSeed} wind {wind} sea {sea}");
            }
        }

        [Test]
        public void TrainsFrom_LiveSlotCount_NeverExceedsTheFourTheSettingsCanExpress()
        {
            // The widening's sharpest edge: MaxTrains grows, but WaveFieldSettings still describes a
            // primary + three secondaries. If the count clamp were re-pointed at the new MaxTrains,
            // an over-set SecondaryTrainCount would report live slots that were never derived — the
            // shader would then read `default` trains (all zeros) as live, and TotalAmplitude, the
            // crest-factor normalizer, would be computed over slots that do not exist.
            var settings = WaveFieldSettings.Default;
            for (int requested = -2; requested <= WaveTrains.MaxTrains + 2; requested++)
            {
                settings.SecondaryTrainCount = requested;
                WaveTrains trains = WaveMath.TrainsFrom(new Vector2(5f, 2f), 0.7f, in settings);
                Assert.LessOrEqual(trains.Count, 1 + WaveFieldSettings.DerivedSecondaryTrainSlots,
                    $"SecondaryTrainCount {requested}: never more live slots than the settings derive");
                for (int i = 0; i < trains.Count; i++)
                    Assert.Greater(trains[i].Wavelength, 0f,
                        $"SecondaryTrainCount {requested}: live slot {i} is a real derived train, not a default");
            }
        }

        [Test]
        public void Dominant_IsTheTrainThePhaseConsumersUsedToTakeBySlotIndex()
        {
            // BoatWaveMotion / the buoys / the seaweed read the DOMINANT train's phase forward
            // (never atan2 — see WaveMath.TrainPhaseDegrees). That train used to be "slot 0 by
            // convention". Making the choice explicit is what stops a re-weighting from silently
            // moving it; at today's flat weighting the deliberate pick must still BE slot 0.
            var settings = WaveFieldSettings.Default;
            foreach (var wind in Winds)
            foreach (var sea in SeaStates)
            {
                WaveTrains trains = WaveMath.TrainsFrom(wind, sea, in settings);
                if (trains.Count == 0) continue;
                Assert.AreEqual(0, trains.DominantIndex,
                    $"flat weighting: the dominant train is still the downwind primary (wind {wind} sea {sea})");
                AssertTrainMatches(trains[0], trains.Dominant, "dominant", $"wind {wind} sea {sea}");
            }
        }

        [Test]
        public void SampleAndPhase_AtDefaults_AreUnchangedThroughTheLegacyField()
        {
            // TrainsFrom is only half the field; Sample is what the hull and the water actually read.
            // Feeding BOTH derivations through the live Sample is a SENSITIVITY check on the
            // derivation: it asks whether the 1–2 ULP the two transcriptions differ by can grow into
            // something the surface shows.
            //
            // ⚠ EVALUATED AT t = 0 ONLY, and the reason is worth stating because it looks like a gap.
            // The phase is θ = k·(d·pos − c·t), so any difference in the derived phase SPEED is
            // multiplied by t. Measured: 1 ULP of c (~2.4e-7 m/s) is ~5e-6 rad of phase at t = 7.5 and
            // ~3e-4 rad at t = 1234 — which showed up as a 1.6e-4 RELATIVE slope difference and failed
            // this test. That number is real arithmetic, not a defect, and no tolerance that admits it
            // would still catch a genuine change. Testing at large t against a SECOND transcription
            // measures ULP amplification, not the code.
            //
            // Large-t behaviour is covered where it can be measured honestly — by tests with only ONE
            // copy of the derivation, so no ULP gap exists to amplify:
            //   · WaveFieldBridgeTests.ShaderTwin_AgreesWithTheAnimatorSurface_TheRuntimePath
            //     (5000 uneven ticks, the double-accumulate wrap discipline), and
            //   · WaveFieldSeamWidthTests.TrainsFrom_IsDeterministicAcrossProcessRuns (bit-exact).
            var settings = WaveFieldSettings.Default;
            float[] coords = { -40f, -15f, 0f, 12.5f, 37.25f };
            double[] times = { 0.0 };

            foreach (var wind in Winds)
            foreach (var sea in SeaStates)
            {
                WaveTrains live = WaveMath.TrainsFrom(wind, sea, in settings);
                WaveTrains legacy = LegacyTrainsFrom(wind, sea, in settings);

                foreach (double t in times)
                foreach (float x in coords)
                foreach (float y in coords)
                {
                    var pos = new Vector2(x, y);
                    WaveSample a = WaveMath.Sample(pos, t, in legacy);
                    WaveSample b = WaveMath.Sample(pos, t, in live);
                    AssertClose(a.Height, b.Height, $"height at ({x},{y}) t {t} wind {wind} sea {sea}");
                    AssertClose(a.Slope.x, b.Slope.x, $"slope.x at ({x},{y}) t {t}");
                    AssertClose(a.Slope.y, b.Slope.y, $"slope.y at ({x},{y}) t {t}");
                    AssertClose(a.CrestFactor, b.CrestFactor, $"crest at ({x},{y}) t {t}");

                    if (live.Count == 0) continue;
                    AssertClose(WaveMath.TrainPhaseDegrees(legacy[0], pos, t),
                               WaveMath.TrainPhaseDegrees(live.Dominant, pos, t),
                               $"dominant phase at ({x},{y}) t {t}");
                }
            }
        }

        [Test]
        public void GlassIsStillSacred_EverySlotExactlyZeroAtSeaStateZero()
        {
            // Not redundant with the sweep above: this is the owner ruling (ADR 0018 §(1)) stated in
            // its own right, so a future re-weighting that introduces a spectral FLOOR — the obvious
            // way to get "variance in sizes" wrong — fails on the ruling, by name.
            var settings = WaveFieldSettings.Default;
            foreach (var wind in Winds)
            {
                WaveTrains trains = WaveMath.TrainsFrom(wind, 0f, in settings);
                for (int i = 0; i < trains.Count; i++)
                    Assert.AreEqual(0, BitConverter.SingleToInt32Bits(trains[i].Amplitude),
                        $"glass is sacred: slot {i} amplitude is EXACTLY +0 at sea state 0 (wind {wind})");
                Assert.AreEqual(0, BitConverter.SingleToInt32Bits(trains.TotalAmplitude),
                    "glass is sacred: the envelope is exactly +0 — the sea is the full mirror");
            }
        }

        // ===== assertion helpers =========================================================================

        private static void AssertFieldsMatch(in WaveTrains expected, in WaveTrains actual, string what)
        {
            Assert.AreEqual(expected.Count, actual.Count, $"live train count ({what})");
            AssertUlp(expected.CrestSharpening, actual.CrestSharpening, $"crest sharpening ({what})");
            AssertUlp(expected.TotalAmplitude, actual.TotalAmplitude, $"total amplitude ({what})");
            for (int i = 0; i < expected.Count; i++)
                AssertTrainMatches(expected[i], actual[i], $"slot {i}", what);
        }

        private static void AssertTrainMatches(in WaveTrain expected, in WaveTrain actual,
                                                    string slot, string what)
        {
            AssertUlp(expected.Direction.x, actual.Direction.x, $"{slot} dir.x ({what})");
            AssertUlp(expected.Direction.y, actual.Direction.y, $"{slot} dir.y ({what})");
            AssertUlp(expected.Wavelength, actual.Wavelength, $"{slot} wavelength ({what})");
            AssertUlp(expected.Amplitude, actual.Amplitude, $"{slot} amplitude ({what})");
            AssertUlp(expected.PhaseSpeed, actual.PhaseSpeed, $"{slot} phase speed ({what})");
            AssertUlp(expected.PhaseOffset, actual.PhaseOffset, $"{slot} phase offset ({what})");
        }

        /// <summary>
        /// Maximum ULP distance allowed between the two transcriptions. Measured need is 1–2 (FMA
        /// contraction in the direction rotate + renormalize); 8 leaves headroom for a CI machine
        /// whose JIT contracts differently, and is still ~1e-6 RELATIVE near 1.0 — far tighter than
        /// any epsilon that could hide a real formula change.
        /// </summary>
        private const long MaxUlps = 8;

        /// <summary>
        /// Assert two floats are within <see cref="MaxUlps"/> representable steps of each other.
        /// Stronger than an epsilon at every magnitude (an absolute epsilon is far too loose near
        /// zero and far too tight near the top of the range), and it fails loudly with the actual
        /// distance so a genuine drift is obvious from the message alone.
        /// </summary>
        private static void AssertUlp(float expected, float actual, string what)
        {
            long distance = UlpDistance(expected, actual);
            if (distance > MaxUlps)
                Assert.Fail($"{what}: expected {expected:R} (0x{BitConverter.SingleToInt32Bits(expected):X8}) " +
                            $"but was {actual:R} (0x{BitConverter.SingleToInt32Bits(actual):X8}) " +
                            $"— {distance} ULP apart, budget {MaxUlps}");
        }

        private static long UlpDistance(float a, float b)
        {
            if (float.IsNaN(a) || float.IsNaN(b)) return long.MaxValue;
            return Math.Abs(Monotonic(a) - Monotonic(b));
        }

        /// <summary>Map float bits onto a monotone integer line, so adjacent representable values are
        /// 1 apart and +0/−0 coincide.</summary>
        private static long Monotonic(float f)
        {
            int bits = BitConverter.SingleToInt32Bits(f);
            return bits >= 0 ? bits : -(long)(bits & 0x7FFFFFFF);
        }

        /// <summary>
        /// Relative comparison for SAMPLED quantities. The derivation's 1–2 ULP amplifies through the
        /// sharpening <c>pow</c> and the four-train sum — measured at ~23 ULP on height — so a fixed
        /// ULP budget is the wrong instrument downstream. 1e-4 relative is ~0.1 mm on a 1 m sea, about
        /// 1/300 of a pixel at PPU = 32: invisible, and still far too tight for a real change to slip
        /// through.
        /// </summary>
        private static void AssertClose(float expected, float actual, string what)
        {
            // ⚠ The absolute floor is 1e-5, not 1e-7, and the reason is CANCELLATION. Slope is a sum
            // of per-train terms each of order A·p·k (~1.8 at full sea) that can cancel to near zero;
            // where it does the result carries no relative precision at all, and a floor sized for
            // the RESULT rather than for the TERMS that made it fails on arithmetic noise. (Measured:
            // slope.x expected 2.59e-4, off by 1.04e-7 — 4e-4 relative to the result, but ~6e-8
            // relative to the terms.) 1e-5 is still ~1/3000 of a pixel at PPU = 32 on height.
            float tolerance = Mathf.Max(1e-5f, 1e-4f * Mathf.Abs(expected));
            if (Mathf.Abs(expected - actual) > tolerance)
                Assert.Fail($"{what}: expected {expected:R} but was {actual:R} " +
                            $"(diff {Mathf.Abs(expected - actual):R}, tolerance {tolerance:R})");
        }

        private static WaveFieldSettings Mutate(Func<WaveFieldSettings, WaveFieldSettings> edit)
            => edit(WaveFieldSettings.Default);

        // ===== THE FROZEN DERIVATION (verbatim, as shipped at 0a11099) ===================================
        // ⚠ DO NOT "tidy", refactor, or re-point this at WaveMath. Its whole value is that it is an
        // INDEPENDENT copy: the moment it delegates to the live code the test proves nothing. If a
        // deliberate change to the sea is ever ratified by the owner, this copy moves in the SAME PR
        // that carries his verdict — never quietly ahead of it.

        private const double LegacyTwoPi = Math.PI * 2.0;

        private static WaveTrains LegacyTrainsFrom(Vector2 windVector, float seaState01,
                                                   in WaveFieldSettings settings)
        {
            float sea = Mathf.Clamp01(seaState01);
            float amplitudeScale = Mathf.Pow(sea, Mathf.Max(0.01f, settings.SeaStateAmplitudeExponent));

            float windSpeed = Mathf.Sqrt(windVector.x * windVector.x + windVector.y * windVector.y);
            Vector2 downwind = windSpeed > 1e-6f
                ? new Vector2(windVector.x / windSpeed, windVector.y / windSpeed)
                : Vector2.up;

            float wavelengthCeiling = Mathf.Max(LegacyMinWavelength, settings.DominantWavelengthMax);
            float dominantWavelength = Mathf.Clamp(
                settings.DominantWavelengthBase + settings.DominantWavelengthPerWindSpeed * windSpeed,
                LegacyMinWavelength, wavelengthCeiling);

            float primaryAmplitude = Mathf.Max(0f, settings.PrimaryAmplitude) * amplitudeScale;
            float gravity = settings.Gravity;

            var primary = LegacyTrain(
                downwind, dominantWavelength, primaryAmplitude,
                LegacyPhaseOffsetRadians(0, settings.PhaseSeed), gravity);

            var secondary1 = LegacyTrain(
                LegacyRotate(downwind, settings.Secondary1AngleDegrees),
                dominantWavelength * settings.Secondary1WavelengthRatio,
                primaryAmplitude * Mathf.Max(0f, settings.Secondary1AmplitudeRatio),
                LegacyPhaseOffsetRadians(1, settings.PhaseSeed), gravity);

            var secondary2 = LegacyTrain(
                LegacyRotate(downwind, settings.Secondary2AngleDegrees),
                dominantWavelength * settings.Secondary2WavelengthRatio,
                primaryAmplitude * Mathf.Max(0f, settings.Secondary2AmplitudeRatio),
                LegacyPhaseOffsetRadians(2, settings.PhaseSeed), gravity);

            var secondary3 = LegacyTrain(
                LegacyRotate(downwind, settings.Secondary3AngleDegrees),
                dominantWavelength * settings.Secondary3WavelengthRatio,
                primaryAmplitude * Mathf.Max(0f, settings.Secondary3AmplitudeRatio),
                LegacyPhaseOffsetRadians(3, settings.PhaseSeed), gravity);

            // As shipped: `1 + Mathf.Clamp(settings.SecondaryTrainCount, 0, WaveTrains.MaxTrains - 1)`
            // with MaxTrains == 4. The literal 3 is that value FROZEN — the whole point of the
            // clamp-shape test above is that the live code must keep clamping here, not at the new
            // (wider) MaxTrains.
            int count = 1 + Mathf.Clamp(settings.SecondaryTrainCount, 0, 3);
            return new WaveTrains(primary, secondary1, secondary2, secondary3,
                                  count, settings.CrestSharpening);
        }

        private const float LegacyMinWavelength = 0.01f;   // WaveTrain.MinWavelengthMeters, frozen

        /// <summary>The <c>WaveTrain</c> constructor as shipped — normalize (near-zero → +Y), clamp
        /// wavelength/amplitude, derive c = √(g·λ/2π).</summary>
        private static WaveTrain LegacyTrain(Vector2 direction, float wavelengthMeters,
                                             float amplitudeMeters, float phaseOffsetRadians, float gravity)
        {
            Vector2 dir;
            float sqrMagnitude = direction.x * direction.x + direction.y * direction.y;
            if (sqrMagnitude < 1e-12f)
            {
                dir = Vector2.up;
            }
            else
            {
                float invMagnitude = 1f / Mathf.Sqrt(sqrMagnitude);
                dir = new Vector2(direction.x * invMagnitude, direction.y * invMagnitude);
            }

            float wavelength = Mathf.Max(LegacyMinWavelength, wavelengthMeters);
            float amplitude = Mathf.Max(0f, amplitudeMeters);
            // The live ctor derives PhaseSpeed itself from the same expression; constructing through
            // it is unavoidable (the field is readonly) and is exactly what the sweep re-proves.
            var train = new WaveTrain(dir, wavelength, amplitude, phaseOffsetRadians, gravity);
            AssertUlp(Mathf.Sqrt(Mathf.Max(0f, gravity) * wavelength / (2f * Mathf.PI)),
                       train.PhaseSpeed, "frozen dispersion relation c = √(g·λ/2π)");
            return train;
        }

        private static float LegacyPhaseOffsetRadians(int trainIndex, int seed)
        {
            unchecked
            {
                int n = (trainIndex * 73856093) ^ (seed * 19349663) ^ 0x5f3759df;
                n = (n << 13) ^ n;
                int m = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
                return (m / 2147483648f) * (2f * Mathf.PI);
            }
        }

        private static Vector2 LegacyRotate(Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
        }
    }
}
