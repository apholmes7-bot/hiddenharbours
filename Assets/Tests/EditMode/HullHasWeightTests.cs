using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE HULL HAS WEIGHT</b> — water fidelity PR 10, pinned as pure math (owner playtest
    /// 2026-09-05: <i>"the boats seem to have too much hangtime after a big wave and bob up and down
    /// too fast and jerky as if they have no weight"</i>).
    ///
    /// <para><b>What the defect measured.</b> Before this PR a hull's ride was ONE
    /// <see cref="WaveMath.Sample"/> at her transform origin. Swept over wavelength on a pure 1 m
    /// train the ride/wave transfer function read <b>1.000 at every wavelength from 2 m to 64 m for
    /// the dory (4.5 m), the cape islander (12.9 m) and the 110 m tanker alike</b>. The full strip is
    /// in <c>docs/art/spikes/hull-has-weight/</c>.</para>
    ///
    /// <para>These tests pin the four claims the fix rests on, each with the sabotage arm that proves
    /// it is load-bearing rather than decorative (the fish-school discipline, PR #406):
    /// <list type="bullet">
    ///   <item><b>The footprint is a low-pass keyed to the hull's own length</b> — a wave shorter
    ///   than her cancels, a wave much longer passes. SABOTAGE: a single point sample passes
    ///   everything, which is the shipped defect.</item>
    ///   <item><b>The natural period is the closed form</b> and the fleet lands in the physical
    ///   order. SABOTAGE: reading <c>BoatHullDef.MassKg</c> as her displacement — the charter's
    ///   literal instruction — halves every period and flattens the fleet's spread.</item>
    ///   <item><b>The response is the closed-form transmissibility</b> of a hull on her own
    ///   waterplane spring. SABOTAGE: damping ratio 0 rings past the bound.</item>
    ///   <item><b>Nothing is saved and nothing wanders</b> — the same clock gives a bit-equal ride,
    ///   and a filter woken cold wakes ON the water.</item>
    /// </list></para>
    /// </summary>
    public class HullHasWeightTests
    {
        private const string BoatDataFolder = "Assets/_Project/Data/Boats/";
        private const float Dt = 1f / 60f;
        private const float Gravity = 9.81f;

        private static HullWeightSettings Shipped => HullWeightSettings.Default;

        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ------------------------------------------------------------------ the footprint

        /// <summary>The DISCRETE footprint the component actually samples: the production offsets,
        /// the production field sampler, the mean. Returns the half peak-to-peak of that mean as one
        /// unit train passes her — i.e. the footprint's transfer gain at that wavelength.</summary>
        private static float FootprintGainMeasured(float lengthMeters, int samples, float wavelengthMeters)
        {
            float halfLength = lengthMeters * 0.5f;
            float lo = float.MaxValue, hi = float.MinValue;
            const int Steps = 720;
            for (int step = 0; step < Steps; step++)
            {
                float phase = step * (2f * Mathf.PI / Steps);
                var train = new WaveTrain(Vector2.up, wavelengthMeters, 1f, phase, Gravity);
                var trains = new WaveTrains(in train, in train, in train, in train, 1, 1f);
                float sum = 0f;
                for (int i = 0; i < samples; i++)
                {
                    float offset = HullHeaveResponseMath.FootprintOffsetMeters(i, samples, halfLength);
                    sum += WaveMath.Sample(new Vector2(0f, offset), 0.0, in trains, 1f).Height;
                }
                float mean = sum / samples;
                lo = Mathf.Min(lo, mean);
                hi = Mathf.Max(hi, mean);
            }
            return (hi - lo) * 0.5f;
        }

        [Test]
        public void TheFootprint_CancelsAWaveTheLengthOfTheHull_AndPassesOneFarLongerThanHer()
        {
            // The cape islander at her shipped sample count. A wave exactly her length has one full
            // crest and trough under her and must average to nothing; a wave five times her length is
            // flat across her and must lift the whole boat.
            const float L = 12.9f;
            int n = HullHeaveResponseMath.FootprintSampleCount(L, Shipped);

            float atHerLength = FootprintGainMeasured(L, n, L);
            float fiveTimes = FootprintGainMeasured(L, n, L * 5f);
            float tenTimes = FootprintGainMeasured(L, n, L * 10f);

            Assert.Less(atHerLength, 0.05f,
                "a wave exactly the hull's length (" + L + " m) must cancel across her " + n +
                "-point waterline — measured " + atHerLength.ToString("0.000"));
            // sinc(1/5) = 0.936 and sinc(1/10) = 0.984 — a footprint averaging a FIFTH of a
            // wavelength really does shave a few per cent off it, and the bar says so rather than
            // rounding the physics up.
            Assert.Greater(fiveTimes, 0.93f,
                "a wave five times her length must lift essentially the whole boat — measured " +
                fiveTimes.ToString("0.000"));
            Assert.Greater(tenTimes, 0.98f,
                "…and one ten times her length must lift all of it — measured " +
                tenTimes.ToString("0.000"));
        }

        [Test]
        public void Sabotage_ASinglePointSample_PassesEveryWavelength_WhichIsTheShippedDefect()
        {
            // THE ARM THAT PROVES THE FOOTPRINT DOES THE WORK. N = 1 is precisely what f2c7105b did,
            // and it must FAIL the test above: no wavelength is attenuated at all.
            const float L = 12.9f;
            foreach (float lambda in new[] { 2f, 4f, L, 24f, 64f })
            {
                float gain = FootprintGainMeasured(L, 1, lambda);
                Assert.AreEqual(1f, gain, 0.01f,
                    "a point sample rides a " + lambda + " m wave in full under a " + L +
                    " m hull (measured " + gain.ToString("0.000") + ") — that is the defect this PR removes");
            }
        }

        [Test]
        public void TheDiscreteFootprint_TracksTheContinuousSinc_AwayFromItsOwnSamplingAlias()
        {
            // FootprintGain01 is the sinc the docs and the attitude envelopes use; the discrete mean
            // is what the ride actually takes. They must agree wherever the sampling can resolve the
            // wave — down to twice the sample spacing, which is the honest limit of ANY point
            // sampling and is why the settings cap the SPACING rather than the count.
            const float L = 12.9f;
            int n = HullHeaveResponseMath.FootprintSampleCount(L, Shipped);
            float spacing = L / n;
            for (float lambda = 4f * spacing; lambda <= 64f; lambda *= 1.3f)
            {
                float measured = FootprintGainMeasured(L, n, lambda);
                float analytic = HullHeaveResponseMath.FootprintGain01(L, lambda);
                Assert.AreEqual(analytic, measured, 0.12f,
                    "lambda " + lambda.ToString("0.0") + " m over a " + L + " m hull: discrete " +
                    measured.ToString("0.000") + " vs sinc " + analytic.ToString("0.000"));
            }
        }

        [Test]
        public void EveryShippedHull_TakesAnOddSampleCount_NeverCoarserThanTheSettingsSpacing()
        {
            // Odd, because the middle sample IS the amidships read the slope already needs. And never
            // coarser than the spacing, because a footprint sampled every s metres reads a wave of
            // exactly lambda = s at FULL amplitude — the alias that would put the twitch back on the
            // biggest hulls (measured: with a 9-sample cap the tanker rode a 12 m wave at 0.82).
            foreach (BoatHullDef hull in EveryHull())
            {
                if (hull.LengthMeters <= 0f) continue;
                int n = HullHeaveResponseMath.FootprintSampleCount(hull.LengthMeters, Shipped);
                Assert.AreEqual(1, n & 1, hull.Id + ": " + n + " samples is even");
                Assert.GreaterOrEqual(n, Shipped.MinFootprintSamples, hull.Id);
                Assert.LessOrEqual(n, Shipped.MaxFootprintSamples, hull.Id);
                Assert.LessOrEqual(hull.LengthMeters / n, Shipped.FootprintSampleSpacingMeters * 1.05f,
                    hull.Id + " (" + hull.LengthMeters + " m) samples every " +
                    (hull.LengthMeters / n).ToString("0.00") + " m, coarser than the settings' " +
                    Shipped.FootprintSampleSpacingMeters + " m — she will alias a wave of that " +
                    "length to full amplitude");
            }
        }

        // ------------------------------------------------------------------ the natural period

        [Test]
        public void TheNaturalPeriod_IsTheClosedForm_AndLengthAndBeamCancel()
        {
            // T = 2*pi*sqrt(m / (rho g A_wp)), against a hand-worked case so the code cannot drift
            // from the formula the class doc states.
            const float length = 12f, beam = 4f, draught = 1.2f;
            HullWeightSettings s = Shipped;
            float awp = HullHeaveResponseMath.WaterplaneAreaSqM(length, beam, in s);
            float displaced = HullHeaveResponseMath.DisplacedMassKg(length, beam, draught, in s);
            float m = HullHeaveResponseMath.HeavingMassKg(displaced, in s);

            Assert.AreEqual(length * beam * s.WaterplaneCoefficient, awp, 1e-3f);
            Assert.AreEqual(s.WaterDensityKgPerCubicMeter * length * beam * draught * s.BlockCoefficient,
                            displaced, 1e-1f);
            Assert.AreEqual(displaced * (1f + s.AddedMassFactor), m, 1e-1f);

            float expected = 2f * Mathf.PI * Mathf.Sqrt(m / (s.WaterDensityKgPerCubicMeter * Gravity * awp));
            Assert.AreEqual(expected,
                            HullHeaveResponseMath.NaturalPeriodSeconds(m, awp, Gravity, in s), 1e-4f);

            // …and the cancellation the class doc claims: L and B drop out of the ratio, so a
            // wall-sided body's heave period is a function of her DRAUGHT alone. That is why this
            // reads the one dimension every hull asset authors honestly.
            float sixTimesThePlan = HullHeaveResponseMath.NaturalPeriodSeconds(
                HullHeaveResponseMath.HeavingMassKg(
                    HullHeaveResponseMath.DisplacedMassKg(length * 2f, beam * 3f, draught, in s), in s),
                HullHeaveResponseMath.WaterplaneAreaSqM(length * 2f, beam * 3f, in s), Gravity, in s);
            Assert.AreEqual(expected, sixTimesThePlan, 1e-4f,
                "length and beam cancel — only the draught sets a wall-sided body's heave period");
        }

        [Test]
        public void TheFleet_LandsOnThePeriodsABoatOfEachSizeActuallyKeeps()
        {
            // The charter's own targets: "a lobster boat lands near 2-3 s, a dory nearer 1 s".
            Assert.AreEqual(1.25f, PeriodOf("Dory"), 0.05f);
            Assert.AreEqual(2.60f, PeriodOf("LobsterBoat"), 0.05f);
            Assert.AreEqual(2.70f, PeriodOf("CapeIslander"), 0.05f);
            Assert.AreEqual(5.82f, PeriodOf("Tanker"), 0.05f);

            // …and the ladder is monotone in size, which is what makes the fleet read as a fleet.
            string[] ladder =
            {
                "Dory", "Punt", "LobsterInshoreOpenFundy", "LobsterBoat", "CapeIslander",
                "SideDragger", "SternTrawler", "Tanker",
            };
            for (int i = 1; i < ladder.Length; i++)
                Assert.Greater(PeriodOf(ladder[i]), PeriodOf(ladder[i - 1]),
                    "the heave period must grow up the ladder — " + ladder[i - 1] + " " +
                    PeriodOf(ladder[i - 1]).ToString("0.00") + " s then " + ladder[i] + " " +
                    PeriodOf(ladder[i]).ToString("0.00") + " s");
        }

        [Test]
        public void Sabotage_ReadingMassKgAsHerDisplacement_HalvesEveryPeriodAndFlattensTheFleet()
        {
            // THE ARM FOR THE CHARTER'S OWN DEPARTURE. BoatHullDef.MassKg is the 2D physics body's
            // mass, not what she displaces; fed to the same formula it lands the fleet at roughly
            // half the period a real boat keeps AND collapses the dory-to-cape spread, which is the
            // per-hull character this PR exists to produce.
            Assert.Less(PeriodFromMassKg("CapeIslander"), PeriodOf("CapeIslander") * 0.6f,
                "MassKg would put a 42-foot cape islander under 1.6 s — still a twitch");
            float shippedSpread = PeriodOf("CapeIslander") / PeriodOf("Dory");
            float massKgSpread = PeriodFromMassKg("CapeIslander") / PeriodFromMassKg("Dory");
            Assert.Greater(shippedSpread, 2f,
                "displacement gives the fleet a real spread — measured " + shippedSpread.ToString("0.00") + "x");
            Assert.Less(massKgSpread, 1.6f,
                "…and MassKg does not: a dory and a 42-footer would heave almost alike — measured " +
                massKgSpread.ToString("0.00") + "x");
        }

        // ------------------------------------------------------------------ the response

        /// <summary>
        /// Drive the production filter with a sinusoidal surface and return its gain and phase lag —
        /// the hull's frequency response, which is what a ride IS.
        ///
        /// <para>A STEP is deliberately not used: this filter damps the hull's velocity RELATIVE to
        /// the surface's own rate (the water damps relative motion — B2.5's CI round-2 lesson), so a
        /// step hands it an unbounded surface rate and the classical step response does not apply.
        /// The closed form for spring-on-relative-position plus damper-on-relative-velocity is the
        /// TRANSMISSIBILITY, and that is what is asserted.</para>
        /// </summary>
        private static float ResponseGain(float omega, float zeta, float excitationOmega,
                                          out float lagDegrees, float amplitude = 0.5f)
        {
            StormRockSettings storm = StormRockSettings.Default;
            storm.MaxDownwardAccelInGs = 1e6f;    // the free-fall cap is a separate, already-pinned claim
            storm.SurfaceBandMeters = 1e6f;       // …and so is the submarine band
            storm.SettleEpsilonMeters = 0f;       // …and the settle snap
            var state = new HeaveWeightState();

            float period = 2f * Mathf.PI / excitationOmega;
            int settle = Mathf.CeilToInt(30f * Mathf.Max(period, 2f * Mathf.PI / omega) / Dt);
            int measure = Mathf.CeilToInt(8f * period / Dt);

            float t = 0f;
            for (int i = 0; i < settle; i++, t += Dt)
                StormRockMath.StepHeaveWeight(ref state, amplitude * Mathf.Sin(excitationOmega * t),
                                              Dt, Gravity, 1f, omega, zeta, in storm);

            // amplitude + phase by one-bin correlation against the drive — the honest way to read a
            // sinusoid's lag (a peak-finder on a 60 Hz trace quantises the phase badly at r = 1).
            double sinSum = 0, cosSum = 0;
            for (int i = 0; i < measure; i++, t += Dt)
            {
                float ride = StormRockMath.StepHeaveWeight(
                    ref state, amplitude * Mathf.Sin(excitationOmega * t), Dt, Gravity, 1f,
                    omega, zeta, in storm);
                sinSum += ride * Mathf.Sin(excitationOmega * t);
                cosSum += ride * Mathf.Cos(excitationOmega * t);
            }
            float a = (float)(2.0 * sinSum / measure), b = (float)(2.0 * cosSum / measure);
            lagDegrees = -Mathf.Atan2(b, a) * Mathf.Rad2Deg;
            return Mathf.Sqrt(a * a + b * b) / amplitude;
        }

        /// <summary>The closed-form transmissibility of this filter at frequency ratio r.</summary>
        private static float Transmissibility(float r, float zeta)
        {
            float damped = 2f * zeta * r;
            float num = 1f + damped * damped;
            float den = (1f - r * r) * (1f - r * r) + damped * damped;
            return Mathf.Sqrt(num / den);
        }

        [Test]
        public void TheHullAnswersHerOwnPeriod_ByTheClosedForm_AndCannotFollowAChop()
        {
            // The whole point, as one curve: she rides a long swell fully, is liveliest near her own
            // period, and is progressively deaf above it. zeta 0.65 is the cape islander's own value.
            const float T = 2.7f;
            float omega = 2f * Mathf.PI / T;
            const float zeta = 0.65f;

            foreach (float r in new[] { 0.2f, 0.5f, 1f, 2f, 4f })
            {
                float gain = ResponseGain(omega, zeta, omega * r, out float lag);
                Assert.AreEqual(Transmissibility(r, zeta), gain, 0.05f,
                    "r " + r + ": gain " + gain.ToString("0.000") + " vs the closed form " +
                    Transmissibility(r, zeta).ToString("0.000"));
                Assert.GreaterOrEqual(lag, -3f,
                    "r " + r + ": a passive hull never LEADS the water (lag " + lag.ToString("0.0") + " deg)");
            }

            // …and the three sentences that follow from it, as bounds a reader can check.
            Assert.Greater(ResponseGain(omega, zeta, omega * 0.2f, out _), 0.97f,
                "a swell five times her period lifts the whole boat");
            Assert.Greater(ResponseGain(omega, zeta, omega, out _), 1.1f,
                "…she is liveliest at her own period");
            Assert.Less(ResponseGain(omega, zeta, omega * 4f, out _), 0.45f,
                "…and cannot follow a chop four times faster than she is");
        }

        [Test]
        public void Sabotage_ZeroDamping_RingsPastTheBound()
        {
            // THE ARM FOR THE DAMPING FLOOR. Undamped, the response at her own period runs away —
            // which is exactly what a dory's authored SeakeepingDamping of 0 would give if the
            // settings did not floor it.
            float omega = 2f * Mathf.PI / 2.7f;
            float rung = ResponseGain(omega, 0f, omega, out _);
            float damped = ResponseGain(omega, Shipped.MinDampingRatio, omega, out _);
            Assert.Greater(rung, 4f, "zeta = 0 must ring at resonance — measured " + rung.ToString("0.00") + "x");
            Assert.Less(damped, 2f,
                "…and the shipped floor must hold it to one clear rise and settle — measured " +
                damped.ToString("0.00") + "x");

            // …and the floor is what stops zeta = 0 ever reaching a hull.
            HullWeightSettings s = Shipped;
            Assert.AreEqual(s.MinDampingRatio, HullHeaveResponseMath.DampingRatio(0f, in s), 1e-6f);
            Assert.AreEqual(s.MinDampingRatio,
                            HullHeaveResponseMath.DampingRatio(Hull("Dory").SeakeepingDamping, in s), 1e-6f,
                            "the dory authors 0 and must still be floored");
        }

        [Test]
        public void TheDampingRatio_IsTheHullsOwnData_HeldInsideTheBand()
        {
            HullWeightSettings s = Shipped;
            Assert.AreEqual(0.65f, HullHeaveResponseMath.DampingRatio(
                Hull("CapeIslander").SeakeepingDamping, in s), 1e-4f,
                "the cape authors 0.65 and keeps it — this is her own data, not a second knob");
            Assert.AreEqual(s.MaxDampingRatio, HullHeaveResponseMath.DampingRatio(3f, in s), 1e-6f);
            foreach (BoatHullDef hull in EveryHull())
            {
                float zeta = HullHeaveResponseMath.DampingRatio(hull.SeakeepingDamping, in s);
                Assert.GreaterOrEqual(zeta, s.MinDampingRatio, hull.Id);
                Assert.LessOrEqual(zeta, s.MaxDampingRatio, hull.Id);
            }
        }

        [Test]
        public void TheFilter_WakesOnTheWater_AndIsBackOnItInsideTwoNaturalPeriods()
        {
            // Nothing is saved (rule 5): a save/load re-runs the filter from a default state. So the
            // claim owed is this one — she wakes ON the water rather than easing in from zero, and
            // after the worst start available (woken on a flat sea that immediately stands at a
            // crest) she is back on it inside two natural periods.
            const float T = 2.7f;
            float omega = 2f * Mathf.PI / T;
            StormRockSettings storm = StormRockSettings.Default;

            var fresh = new HeaveWeightState();
            float first = StormRockMath.StepHeaveWeight(ref fresh, 1.7f, Dt, Gravity, 1f, omega, 0.65f, in storm);
            Assert.AreEqual(1.7f, first, 1e-5f, "an unprimed filter wakes ON the water, never eases in from 0");

            var cold = new HeaveWeightState();
            StormRockMath.StepHeaveWeight(ref cold, 0f, Dt, Gravity, 1f, omega, 0.65f, in storm);
            float ride = 0f;
            for (int i = 0; i < Mathf.CeilToInt(2f * T / Dt); i++)
                ride = StormRockMath.StepHeaveWeight(ref cold, 1.7f, Dt, Gravity, 1f, omega, 0.65f, in storm);
            Assert.AreEqual(1.7f, ride, 0.02f,
                "settled to " + ride.ToString("0.000") + " m against a 1.700 m surface after 2 T = " +
                (2f * T).ToString("0.0") + " s");
        }

        [Test]
        public void TheRide_IsBitEqual_AcrossIdenticalRuns()
        {
            CollectionAssert.AreEqual(DeterministicRun(), DeterministicRun(),
                "same clock, same ride — bit for bit (rule 5)");
        }

        private static float[] DeterministicRun()
        {
            StormRockSettings storm = StormRockSettings.Default;
            var state = new HeaveWeightState();
            var trace = new float[600];
            for (int i = 0; i < trace.Length; i++)
            {
                float surface = Mathf.Sin(i * Dt * 1.3f) * 1.4f + Mathf.Sin(i * Dt * 4.1f) * 0.3f;
                trace[i] = StormRockMath.StepHeaveWeight(ref state, surface, Dt, Gravity, 1f,
                                                         2f * Mathf.PI / 2.7f, 0.65f, in storm);
            }
            return trace;
        }

        // ------------------------------------------------------------------ the whole read, per hull

        [Test]
        public void TheDoryIsLivelierThanTheCape_AndTheTankerBarelyNods()
        {
            // The footprint's transfer at the field's dominant wavelength in a working breeze (wind
            // 8.75 m/s gives lambda = 6 + 1.5*8.75 = 19.1 m at the shipped WaveField). This is the
            // sentence the owner reads off the screen, as a number.
            const float lambda = 19.1f;
            float dory = FootprintGainAt("Dory", lambda);
            float cape = FootprintGainAt("CapeIslander", lambda);
            float tanker = FootprintGainAt("Tanker", lambda);

            Assert.Greater(dory, cape * 1.5f,
                "the dory (" + dory.ToString("0.000") + ") must be markedly livelier than the cape (" +
                cape.ToString("0.000") + ")");
            Assert.Greater(cape, tanker * 3f,
                "…and the cape (" + cape.ToString("0.000") + ") markedly livelier than the tanker (" +
                tanker.ToString("0.000") + ")");
            Assert.Less(tanker, 0.1f, "the tanker barely nods — measured " + tanker.ToString("0.000"));
            Assert.Greater(dory, 0.6f, "…and the dory still rides the swell — measured " + dory.ToString("0.000"));
        }

        [Test]
        public void Disabled_IsTheBoltedRide_WhichIsTheABContract()
        {
            HullWeightSettings off = Shipped;
            off.Enabled = false;
            HullHeaveResponse bolted = HullHeaveResponseMath.Resolve(12.9f, 2.4f, 1.4f, 0.65f, Gravity, in off);
            Assert.IsFalse(bolted.IsHonest, "Enabled off gives Bolted — the point sample and the storm chase");
            Assert.AreEqual(HullHeaveResponse.Bolted.FootprintSamples, bolted.FootprintSamples);

            // …and so is a hull nobody has measured, with the feature ON.
            HullWeightSettings on = Shipped;
            Assert.IsFalse(HullHeaveResponseMath.Resolve(0f, 0f, 0f, 0f, Gravity, in on).IsHonest);
        }

        // ------------------------------------------------------------------ the config block

        [Test]
        public void GameConfig_CarriesTheHullWeightBlock_AtTheReferenceDefaults()
        {
            // The shipped asset's PRESENCE is guarded generally by GameConfigAssetCoverageTests (the
            // LoadOrCreate trap); this pins the load-bearing VALUES, which coverage cannot see.
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);
            HullWeightSettings s = config.HullWeight;

            Assert.IsTrue(s.Enabled, "PR 10 ships ON — it is the owner's own report");
            Assert.AreEqual(2f, s.FootprintSampleSpacingMeters, 1e-6f);
            Assert.AreEqual(3, s.MinFootprintSamples);
            Assert.AreEqual(0.85f, s.WaterplaneCoefficient, 1e-6f);
            Assert.AreEqual(0.55f, s.BlockCoefficient, 1e-6f);
            Assert.AreEqual(1f, s.AddedMassFactor, 1e-6f);
            Assert.AreEqual(0.35f, s.MinDampingRatio, 1e-6f, "the dory authors zeta 0 and must not ring");
            Assert.AreEqual(1f, s.MaxDampingRatio, 1e-6f, "nothing on this ladder wallows past critical");

            // The cap is a GUARD, not a budget: no hull in the game may reach it, or she samples
            // coarser than the spacing and aliases a wave of that length to full amplitude.
            float longest = 0f;
            foreach (BoatHullDef hull in EveryHull()) longest = Mathf.Max(longest, hull.LengthMeters);
            int asked = Mathf.RoundToInt(longest / s.FootprintSampleSpacingMeters);
            Assert.GreaterOrEqual(s.MaxFootprintSamples, asked,
                "the longest hull in the fleet is " + longest + " m and asks for " + asked + " samples");
        }

        // ------------------------------------------------------------------ helpers

        private static float PeriodOf(string assetName)
        {
            BoatHullDef h = Hull(assetName);
            return HullHeaveResponseMath.Resolve(h.LengthMeters, HullPresence.HalfBeamOf(h),
                                                 h.DraughtMeters, h.SeakeepingDamping,
                                                 Gravity, Shipped).NaturalPeriodSeconds;
        }

        private static float PeriodFromMassKg(string assetName)
        {
            HullWeightSettings s = Shipped;
            BoatHullDef h = Hull(assetName);
            float beam = HullPresence.HalfBeamOf(h) * 2f;
            return HullHeaveResponseMath.NaturalPeriodSeconds(
                HullHeaveResponseMath.HeavingMassKg(h.MassKg, in s),
                HullHeaveResponseMath.WaterplaneAreaSqM(h.LengthMeters, beam, in s), Gravity, in s);
        }

        private static float FootprintGainAt(string assetName, float wavelengthMeters)
        {
            BoatHullDef h = Hull(assetName);
            int n = HullHeaveResponseMath.FootprintSampleCount(h.LengthMeters, Shipped);
            return FootprintGainMeasured(h.LengthMeters, n, wavelengthMeters);
        }

        private static BoatHullDef Hull(string assetName)
        {
#if UNITY_EDITOR
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatHullDef>(
                BoatDataFolder + assetName + ".asset");
            Assert.IsNotNull(def, assetName + ".asset is missing");
            return def;
#else
            throw new InvalidOperationException("EditMode only");
#endif
        }

        private static IEnumerable<BoatHullDef> EveryHull()
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BoatHullDef", new[] { "Assets/_Project/Data" });
            Assert.Greater(guids.Length, 10, "the hull list is the subject — an empty one passes vacuously");
            foreach (string guid in guids)
            {
                var def = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatHullDef>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) yield return def;
            }
#else
            yield break;
#endif
        }
    }
}
