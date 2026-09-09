using System;
using System.Text;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>REGISTER ROW 34 — the sea vibrated when its mood changed, and it was one multiplication.
    /// ✅ RULED AND SHIPPED (owner, 2026-09-09): the frequency bins are HELD; the wind moves the
    /// amplitudes across them.</b>
    ///
    /// <para>Owner, 2026-09-08, on the Nine Mile Creek beach: <i>"the water starts oscillating very
    /// quickly when it changes states from light to calm to moderate etc, its like its speed
    /// resynches and it vibrates and its very noticable."</i></para>
    ///
    /// <para><b>The mechanism.</b> <see cref="WaveMath.Sample"/> forms <c>φ = k·x − ω·t</c> with
    /// <c>t</c> the TOTAL game time. The superseded derivation scaled every bin's wavelength by
    /// <c>λ_p(U)</c>, so every ω was a function of the wind and a mood change moved the phase at a
    /// fixed point by <c>Δω·t</c> — an error proportional to how long the world has been running.
    /// Measured on the SHIPPED config across a light→blow step, per bin:</para>
    /// <code>
    ///   |Δφ| radians       bin0    bin1    bin2    bin3    bin4    bin5    bin6    bin7
    ///     at  3 h        35 537  41 041  37 439  47 486  29 851  44 066  27 008  46 909
    ///     at 30 h       355 369 410 407 374 392 474 858 298 510 440 657 270 080 469 087
    /// </code>
    /// <para>Thousands of whole cycles, on all eight bins at once. That is not a wave; that is the
    /// "resynch" he saw.</para>
    ///
    /// <para><b>The fix, and why it is structural rather than a tuning.</b> The bins are now an
    /// absolute geometric ladder (<see cref="WaveSpectrum.BinWavelengthMeters"/>) with no wind term at
    /// all. The wind enters only in the ratio <c>ω_i/ω_p(U)</c> that the JONSWAP shape is read at — so
    /// <b>the peak walks across bins that stand still</b>, which is also what a growing sea physically
    /// does: it puts energy into frequencies that were always there. <c>Δω</c> is not reduced, it is
    /// <b>identically zero</b>, so <c>Δω·t</c> cannot come back at any game time.</para>
    ///
    /// <para><b>Rule 5 is untouched and was never the problem.</b> Still a pure function of
    /// <c>(seed, gameTime)</c>: no accumulator, no integrated phase, no state between samples. The
    /// ladder's jitter hashes off the settings' seed alone — no wind, no clock.</para>
    ///
    /// <para>⚠️ <b>What it costs, stated here because the owner is choosing from it.</b> A fixed
    /// ladder must span an absolute wavelength range, and at a fixed bin count a wider ladder means
    /// bins further apart in frequency — which is a FASTER group beat, because groups are what
    /// neighbouring frequencies beating produce. See
    /// <see cref="TheLadderSpendsGroupRhythmToBuyWindCoverage_AndThisIsTheMenu"/>. The shipped
    /// 8 bins over <b>5–30 m</b> passes <c>WaveSpectrumTests</c>' group-run acceptance where 3–30 m
    /// fails it (2.12 against 1.71, bar 1.84).
    ///
    /// <para>⚠️ <b>But NOT because it is narrower, and this correction matters for whoever retunes
    /// the ladder next.</b> Measured across ten ladders, the run-length metric is <b>not monotone in
    /// the spacing</b>: 6–24 m has a TIGHTER spacing than 5–30 m and fails (1.70), while 3–50 m has
    /// the widest spacing tried and scores best (3.44). The run length depends on where the bins fall
    /// relative to the PEAK at the condition measured, not on the spacing formula. 5–30 m is a ladder
    /// that passes, not an optimum — and a single-condition run-length metric is a weak instrument
    /// for choosing one. The beat period IS monotone in the spacing (that is analytic, and is what
    /// the menu test asserts); the grouping is not.</para>
    ///
    /// <para>⚠️⚠️ <b>WHICH PATH ACTUALLY CARRIED THE DEFECT — a correction to row 34's own
    /// wording, found while building this fix (2026-09-09).</b> The register describes this as the
    /// DRAWN water vibrating. It is not, or not only:</para>
    /// <list type="bullet">
    /// <item><b>The drawn surface was already mitigated.</b> <c>WaveFieldBridge</c> publishes trains
    /// through <see cref="WaveFieldAnimator"/>, which advances each train's phase INCREMENTALLY
    /// (<c>Φ += k·c·dt</c>) and bakes it into <c>PhaseOffset</c>; the shader reads that baked phase
    /// (<c>theta = k·dot(dir,xy) + phis[i] + …</c>, <c>WaveFieldSampleAt</c>) rather than
    /// <c>ω·gameTime</c>. An incremental accumulator is continuous however k and c move, so
    /// <c>Δω·t</c> never reached the pixels.</item>
    /// <item><b>The SIM path carried it in full.</b> <c>WaveMath.TrainsFrom</c> +
    /// <c>Sample(pos, gameTime)</c> is the ADR 0018 sim reference — the animator is explicitly
    /// forbidden there, because it is stateful and "NOT a pure function of gameTime" (its own class
    /// doc: two machines at different frame rates accumulate along different paths). Its live
    /// callers with a game-time argument are <c>BoatController</c> (the hull's ride) and
    /// <c>BreakerMath</c> (the bore's birth).</item>
    /// </list>
    /// <para>So the honest claim for this PR is narrower than the register's and, in one way, larger.
    /// Narrower: it is not established that the fix is what the owner saw, and this lane cannot
    /// establish that without the editor. Larger: <b>with the bins standing still the accumulator is
    /// no longer what makes the phase continuous</b> — the closed form is continuous by itself. That
    /// removes the reason the drawn sea and the ridden sea had to travel down two different paths,
    /// which is P1's SEE == FEEL at its root. Actually retiring the animator's phase accumulation is
    /// NOT in this PR; it becomes possible, and it needs the parity guards and a lead-architect call
    /// (ADR 0018).</para>
    ///
    /// <para>Pure arithmetic over the shipped derivation: no scene, no clock, no graphics device.</para>
    /// </summary>
    public class WaveFixedBinLadderTests
    {
        const float G = 9.81f;

        /// <summary>The sea the owner plays — the asset, not the code defaults. See
        /// <see cref="ShippedWaveField"/> for why that distinction has its own guard.</summary>
        static WaveFieldSettings Shipped() => ShippedWaveField.Settings();

        /// <summary>The fixtures' wind → sea-state map, shared with the amplitude and vibration
        /// measurements so the three describe one sea.</summary>
        static float SeaState01(float windSpeed) => Mathf.Clamp01(windSpeed / 10f);

        static WaveTrains At(float windSpeed, in WaveFieldSettings s)
            => WaveMath.TrainsFrom(new Vector2(0f, windSpeed), SeaState01(windSpeed), in s);

        static double Omega(float wavelength) => Math.Sqrt(2.0 * Math.PI * G / wavelength);

        /// <summary>The winds the weather can actually produce. #797 measured the shipped WindProfile:
        /// the law is <c>3 + strN·1.3 + gustN·1.2</c> with both noises bottoming at −1, so the wind
        /// never leaves [0.50, 5.70] m/s. The sweep steps finely enough to catch a bin crossing.</summary>
        static readonly float[] ReachableWinds =
        {
            0.50f, 0.75f, 1.00f, 1.63f, 2.00f, 2.50f, 3.00f, 3.50f,
            4.00f, 4.50f, 5.00f, 5.35f, 5.70f,
        };

        // =============================================================================================
        //  1. The bins do not move. This is the whole of row 34.
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>THE FIX, ASSERTED AT ITS ROOT: every bin's wavelength is the SAME NUMBER at every wind
        /// the weather can produce.</b> Not close — the same float. Everything else in this file is a
        /// consequence of this one property, and if it ever fails the vibration is back whatever the
        /// other tests say.
        /// </summary>
        [Test]
        public void EveryBinsWavelength_IsIdenticalAtEveryReachableWind()
        {
            WaveFieldSettings s = Shipped();
            WaveTrains reference = At(ReachableWinds[0], in s);

            var report = new StringBuilder();
            report.AppendLine("  the standing ladder (metres), slot 0 = longest:");
            report.Append("   ");
            for (int i = 0; i < reference.Count; i++) report.Append($" {reference[i].Wavelength,7:0.00}");
            TestContext.WriteLine(report.ToString());

            foreach (float u in ReachableWinds)
            {
                WaveTrains t = At(u, in s);
                Assert.AreEqual(reference.Count, t.Count,
                    $"the live bin count moved at {u:0.00} m/s — the ladder's arity must not depend " +
                    "on the weather either");
                for (int i = 0; i < t.Count; i++)
                    Assert.AreEqual(reference[i].Wavelength, t[i].Wavelength, 0f,
                        $"⭐ bin {i} is {t[i].Wavelength:0.0000} m at {u:0.00} m/s but " +
                        $"{reference[i].Wavelength:0.0000} m at {ReachableWinds[0]:0.00} m/s. A bin " +
                        "whose wavelength follows the wind has a wind-dependent ω, and φ = k·x − ω·t " +
                        "then jumps by Δω·t at every mood change — register row 34, the owner's " +
                        "\"it vibrates\".");
            }
        }

        /// <summary>
        /// 🔴 <b>AND THEREFORE THE PHASE AT A FIXED POINT IS CONTINUOUS ACROSS A MOOD CHANGE — at
        /// three ages of the world, because the defect grew with elapsed game time.</b> The superseded
        /// law put 27 000–47 500 radians through here at three hours; the bar is now exactly zero,
        /// which is what "the term no longer exists" means as opposed to "the term is small".
        /// </summary>
        [Test]
        public void ThePhaseAtAPoint_IsContinuousAcrossEveryMoodChange_AtEveryAgeOfTheWorld()
        {
            WaveFieldSettings s = Shipped();
            double worst = 0.0;
            var report = new StringBuilder();
            report.AppendLine("  wind step        t=0 s      t=3 h       t=30 h   (max |Δφ| over bins, radians)");

            for (int w = 0; w + 1 < ReachableWinds.Length; w++)
            {
                WaveTrains a = At(ReachableWinds[w], in s);
                WaveTrains b = At(ReachableWinds[w + 1], in s);
                int n = Math.Min(a.Count, b.Count);

                var row = new StringBuilder($"  {ReachableWinds[w]:0.00}->{ReachableWinds[w + 1]:0.00} ");
                foreach (double t in new[] { 0.0, 3 * 3600.0, 30 * 3600.0 })
                {
                    double peak = 0.0;
                    for (int i = 0; i < n; i++)
                        peak = Math.Max(peak, Math.Abs((Omega(b[i].Wavelength) - Omega(a[i].Wavelength)) * t));
                    row.Append($" {peak,12:0.###}");
                    worst = Math.Max(worst, peak);
                }
                report.AppendLine(row.ToString());
            }
            TestContext.WriteLine(report.ToString());

            Assert.AreEqual(0.0, worst, 1e-9,
                "⭐ ROW 34's ACCEPTANCE: across every wind step the weather can produce, at 0 s, 3 h " +
                "and 30 h of game time, the phase at a fixed world point must not move at all. The " +
                "bound is zero rather than small because Δω is identically zero once the bins stand " +
                "still — a non-zero reading here means a wind term has come back into a wavelength, " +
                "and it will be invisible in a short test and violent after an evening's play.");
        }


        /// <summary>
        /// 🔴 <b>THE RIDDEN SEA — and on the evidence this is the half the player actually saw.</b>
        ///
        /// <para><c>BoatController</c> samples the pure closed form at the game clock
        /// (<c>WaveMath.Sample(pos, now, in trains, …)</c>), and the camera rides the hull. The DRAWN
        /// sea never jumped, because <c>WaveFieldAnimator</c> accumulates its phase; so on a mood
        /// change the boat — and the whole picture with it — lurched against water that was itself
        /// continuous. That is what a player at the helm would call <i>"the water starts
        /// oscillating"</i>.</para>
        ///
        /// <para>This asserts the thing the hull actually reads: the surface HEIGHT at the boat's own
        /// position, across every wind step the weather can produce, at three ages of the world.
        /// Height rather than phase because height is what becomes a force, a heave and a camera
        /// move — and because a phase that is continuous while the height is not would still be a
        /// jolt.</para>
        ///
        /// <para>⚠️ <b>The claim is bounded.</b> This shows the ridden sea is continuous where it
        /// was not; it does not prove the owner's report had this cause. Only the helm settles that,
        /// and the plate is owed when a slot opens.</para>
        /// </summary>
        [Test]
        public void TheRIDDENSea_IsContinuousAcrossEveryMoodChange_WhichIsWhatTheHullAndCameraFeel()
        {
            WaveFieldSettings s = Shipped();

            // A handful of world points, because a single point can sit at a node where every train
            // happens to cancel and no jump could show.
            var probes = new[]
            {
                new Vector2(0f, 0f), new Vector2(13.7f, -4.25f),
                new Vector2(-31.5f, 18.25f), new Vector2(96.5f, 63.75f),
            };

            double worst = 0.0;
            string worstWhere = "";
            var report = new StringBuilder();
            report.AppendLine("  wind step      t=0 s       t=3 h      t=30 h   (max |dHeight| over probes, metres)");

            for (int w = 0; w + 1 < ReachableWinds.Length; w++)
            {
                WaveTrains before = At(ReachableWinds[w], in s);
                WaveTrains after = At(ReachableWinds[w + 1], in s);

                var row = new StringBuilder($"  {ReachableWinds[w]:0.00}->{ReachableWinds[w + 1]:0.00}");
                foreach (double age in new[] { 0.0, 3 * 3600.0, 30 * 3600.0 })
                {
                    double peak = 0.0;
                    foreach (Vector2 p in probes)
                    {
                        // The SAME instant either side of the step: any difference is the step's own
                        // doing, not the passage of time.
                        float h0 = WaveMath.Sample(p, age, in before).Height;
                        float h1 = WaveMath.Sample(p, age, in after).Height;
                        peak = Math.Max(peak, Math.Abs(h1 - h0));
                    }
                    row.Append($" {peak,11:0.000000}");
                    if (peak > worst)
                    {
                        worst = peak;
                        worstWhere = $"{ReachableWinds[w]:0.00}->{ReachableWinds[w + 1]:0.00} at {age / 3600.0:0.#} h";
                    }
                }
                report.AppendLine(row.ToString());
            }
            TestContext.WriteLine(report.ToString());

            // ⚠️ NOT zero, and it must not be: a wind step legitimately changes the sea's AMPLITUDES,
            // so the height under the hull moves. The question is BY HOW MUCH, and against what bound.
            //
            // ⚠️ An earlier revision of this test asserted that |dHeight| must not GROW between 0 s
            // and 30 h. That was wrong, and wrong in an instructive way: with the bins fixed,
            //     dh(t) = SUM_i (A_i(U2) - A_i(U1)) * profile(k_i*x - omega_i*t + phi_i)
            // whose profile terms still oscillate in t. |dh| therefore VARIES with the age of the
            // world — bounded, but not monotone — so comparing two instants measures which phase the
            // probes happened to catch, not whether anything grows. It failed on sampling luck
            // (0.498 m at 30 h against 0.299 m at 0 s) while the field was perfectly correct.
            //
            // ⭐ The bound above is the real invariant, and it is exact: because the phases are
            // IDENTICAL either side of the step, the difference can only be the amplitude difference.
            // Before the fixed ladder it could not be — delta-omega*t put up to 474 858 radians
            // through the closed form at 30 h, so the two samples were at unrelated points of the
            // wave and the step could reach the sum of BOTH envelopes.
            double amplitudeDelta = 0.0;
            double bothEnvelopes = 0.0;
            {
                WaveTrains a = At(1.63f, in s), b = At(5.70f, in s);
                for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
                {
                    amplitudeDelta += Math.Abs(b[i].Amplitude - a[i].Amplitude);
                    bothEnvelopes += a[i].Amplitude + b[i].Amplitude;
                }
            }
            TestContext.WriteLine($"  worst step {worst:0.000000} m ({worstWhere}) | " +
                                  $"amplitude-only bound {amplitudeDelta:0.0000} m | " +
                                  $"arbitrary-phase bound {bothEnvelopes:0.0000} m");

            double worstLightToBlow = 0.0;
            {
                WaveTrains a = At(1.63f, in s), b = At(5.70f, in s);
                foreach (Vector2 p in probes)
                foreach (double age in new[] { 0.0, 3 * 3600.0, 30 * 3600.0, 300 * 3600.0 })
                    worstLightToBlow = Math.Max(worstLightToBlow,
                        Math.Abs(WaveMath.Sample(p, age, in b).Height
                               - WaveMath.Sample(p, age, in a).Height));
            }

            Assert.LessOrEqual(worstLightToBlow, amplitudeDelta + 1e-4,
                $"⭐ THE RIDDEN SEA'S ACCEPTANCE: a wind step may move the surface under the hull by at " +
                $"most the AMPLITUDE difference ({amplitudeDelta:0.0000} m), at any age of the world. " +
                "That bound holds only because the bins stand still, so every train is at the same " +
                "phase either side of the step and nothing but the amplitude can differ. With the " +
                "bins following the wind the two samples sat at unrelated points of the wave and the " +
                $"step could reach the sum of both envelopes ({bothEnvelopes:0.0000} m) — which is the " +
                "lurch the hull, and the camera riding it, actually took.");

            Assert.Less(amplitudeDelta, bothEnvelopes,
                "DEAD CONTROL: the amplitude-only bound must be strictly tighter than the " +
                "arbitrary-phase one, or the assertion above is not saying anything the old field " +
                "would have failed.");
        }

        /// <summary>
        /// ⚠️ <b>THE DEAD CONTROL, and it is the one that keeps the test above honest.</b> The
        /// assertion "Δφ is zero" would also pass if the field had no waves at all, or if the two
        /// samples were the same object. So: the sea must actually be there, its ω must be non-zero,
        /// and the two ends of the step must genuinely DIFFER — in amplitude, which is where the wind
        /// is now allowed to act.
        /// </summary>
        [Test]
        public void DEADCONTROL_TheSeaIsReallyThere_AndTheWindReallyChangedIt()
        {
            WaveFieldSettings s = Shipped();
            WaveTrains light = At(1.63f, in s);
            WaveTrains blow = At(5.70f, in s);

            Assert.Greater(light.Count, 0, "no trains at all: every Δφ would be vacuously zero");
            for (int i = 0; i < light.Count; i++)
                Assert.Greater(Omega(light[i].Wavelength), 0.1,
                    $"bin {i} has no frequency worth speaking of, so its phase cannot move whatever " +
                    "the wind does");

            Assert.Greater(blow.TotalAmplitude, light.TotalAmplitude * 1.5f,
                "⭐ THE CONTROL: a light→blow step must be a real change in the sea. It is the " +
                "AMPLITUDES that carry it now — if they did not move either, the continuity test " +
                "above would be measuring a field that simply ignores the weather.");

            int lightPeak = 0, blowPeak = 0;
            for (int i = 1; i < light.Count; i++)
                if (light[i].Amplitude > light[lightPeak].Amplitude) lightPeak = i;
            for (int i = 1; i < blow.Count; i++)
                if (blow[i].Amplitude > blow[blowPeak].Amplitude) blowPeak = i;

            TestContext.WriteLine($"  the peak walked: bin {lightPeak} at 1.63 m/s -> bin {blowPeak} at 5.70 m/s " +
                                  $"({light[lightPeak].Wavelength:0.0} m -> {blow[blowPeak].Wavelength:0.0} m)");
            Assert.AreNotEqual(lightPeak, blowPeak,
                "⭐ AND THE PEAK MUST WALK. That is the ruling's other half: the wind is supposed to " +
                "move WHICH bin carries the energy. If the same bin dominates at every wind, the sea " +
                "has stopped responding to the weather and this fix has traded one defect for a worse one.");
        }

        // =============================================================================================
        //  2. The ladder itself
        // =============================================================================================

        /// <summary>
        /// The ladder must be a ladder: strictly ordered longest→shortest, spanning exactly the ends
        /// the settings name, with no two bins collapsed onto each other. ⚠️ The jitter is what makes
        /// this worth asserting — it exists to break exact harmonic ratios between bins (which read as
        /// a repeating pattern), and a jitter wide enough to reorder them would be a silent tuning bug.
        /// </summary>
        [Test]
        public void TheLadder_IsOrdered_SpansItsEndsExactly_AndNoTwoBinsCollapse()
        {
            const int Bins = 8;
            const float Lo = 5f, Hi = 30f;

            var lambda = new float[Bins];
            for (int i = 0; i < Bins; i++)
                lambda[i] = WaveSpectrum.BinWavelengthMeters(i, Bins, Lo, Hi, 0);

            Assert.AreEqual(Hi, lambda[0], 1e-3f, "slot 0 is the ladder's LONG end, pinned exactly");
            Assert.AreEqual(Lo, lambda[Bins - 1], 1e-3f, "the last slot is the SHORT end, pinned exactly");

            var report = new StringBuilder("  slot  lambda      omega   ratio to previous\n");
            for (int i = 0; i < Bins; i++)
            {
                double w = Omega(lambda[i]);
                double r = i == 0 ? 0 : Omega(lambda[i]) / Omega(lambda[i - 1]);
                report.AppendLine($"  {i,4}  {lambda[i],7:0.000}  {w,9:0.0000}  {(i == 0 ? "-" : r.ToString("0.0000")),8}");
                if (i > 0)
                    Assert.Less(lambda[i], lambda[i - 1] * 0.999f,
                        $"bin {i} ({lambda[i]:0.000} m) did not come out strictly shorter than bin " +
                        $"{i - 1} ({lambda[i - 1]:0.000} m) — the jitter is reordering the ladder, " +
                        "which makes 'slot 0 is the longest' a lie the rest of the field relies on");
            }
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// ⚠️ <b>A UNIFORM HASH IS NOT A SPREAD — so the spread is checked, not assumed.</b> The
        /// ladder's interior bins are jittered off the seed; this asserts the jitter actually moves
        /// them (a hash that returned a constant would leave a perfectly geometric ladder, whose exact
        /// harmonic ratios are the repeating pattern the jitter exists to break) while keeping every
        /// bin inside its own stratum.
        /// </summary>
        [Test]
        public void TheLadderIsSeeded_AndTheSpreadIsCheckedRatherThanAssumed()
        {
            const int Bins = 8;
            const float Lo = 5f, Hi = 30f;

            float Geometric(int i) => Hi * Mathf.Pow(Lo / Hi, i / (float)(Bins - 1));

            int moved = 0;
            float worstDrift = 0f;
            var seen = new System.Collections.Generic.List<float>();
            for (int i = 0; i < Bins; i++)
            {
                float actual = WaveSpectrum.BinWavelengthMeters(i, Bins, Lo, Hi, 0);
                float even = Geometric(i);
                float step = Mathf.Abs(Geometric(Mathf.Min(i + 1, Bins - 1)) - even);
                if (i > 0 && i < Bins - 1)
                {
                    if (Mathf.Abs(actual - even) > 1e-4f) moved++;
                    worstDrift = Mathf.Max(worstDrift, step > 1e-6f ? Mathf.Abs(actual - even) / step : 0f);
                }
                seen.Add(actual);
            }

            Assert.Greater(moved, 0,
                "⭐ every interior bin sits exactly on the even geometric ladder, so the seeded jitter " +
                "is doing nothing. An evenly-spaced ladder has exact frequency ratios between bins, " +
                "which superpose into a repeating pattern — the same reason WaveFieldSettings' " +
                "hand-authored angles are asymmetric.");
            Assert.Less(worstDrift, 1f,
                $"an interior bin moved {worstDrift:0.00} of a full step from its stratum — a jitter " +
                "that can reach its neighbour's slot can reorder the ladder");

            // Two different seeds must give two different ladders, with the SAME ends.
            var other = new float[Bins];
            for (int i = 0; i < Bins; i++) other[i] = WaveSpectrum.BinWavelengthMeters(i, Bins, Lo, Hi, 4242);
            Assert.AreEqual(seen[0], other[0], 1e-3f, "the ends are pinned, so they do not vary by seed");
            Assert.AreEqual(seen[Bins - 1], other[Bins - 1], 1e-3f, "…both of them");

            bool anyInteriorDiffers = false;
            for (int i = 1; i < Bins - 1; i++)
                if (Mathf.Abs(seen[i] - other[i]) > 1e-4f) anyInteriorDiffers = true;
            Assert.IsTrue(anyInteriorDiffers,
                "two different seeds produced the identical ladder — the seed is not reaching the " +
                "jitter, so every world would carry the same interference pattern");

            // …and it is still DETERMINISTIC: the same seed twice is the same ladder (rule 5).
            for (int i = 0; i < Bins; i++)
                Assert.AreEqual(seen[i], WaveSpectrum.BinWavelengthMeters(i, Bins, Lo, Hi, 0), 0f,
                    $"bin {i} is not deterministic in (slot, seed) — rule 5");
        }

        /// <summary>
        /// ⚠️ <b>THE TRADE, PRICED — this is the table the owner's tuning choice comes from.</b>
        /// A fixed ladder spans an absolute range, so at a fixed bin count a WIDER ladder puts its bins
        /// further apart in frequency, and the group beat (2π/Δω between neighbours) gets faster.
        /// There is no way around it; there is only where to stand on it.
        ///
        /// <para>This test asserts the SHAPE of the trade (wider is faster, more bins is slower) rather
        /// than any particular number, because the numbers are the owner's to pick. The numbers
        /// themselves are logged.</para>
        /// </summary>
        [Test]
        public void TheLadderSpendsGroupRhythmToBuyWindCoverage_AndThisIsTheMenu()
        {
            var report = new StringBuilder();
            report.AppendLine("  bins  ladder      spacing   beat@30m  @15.6m   @8m    @3m");
            foreach (var (bins, lo, hi) in new[]
                     { (8, 3f, 30f), (8, 5f, 30f), (8, 8f, 30f) })
            {
                float sp = WaveSpectrum.LadderRelativeSpacing(bins, lo, hi);
                report.AppendLine($"  {bins,4}  {lo,4:0}-{hi,2:0} m    {sp,7:0.0000}  " +
                                  $"{WaveSpectrum.BeatPeriodSeconds(30f, G, sp),7:0.0}  " +
                                  $"{WaveSpectrum.BeatPeriodSeconds(15.6f, G, sp),6:0.0}  " +
                                  $"{WaveSpectrum.BeatPeriodSeconds(8f, G, sp),5:0.0}  " +
                                  $"{WaveSpectrum.BeatPeriodSeconds(3f, G, sp),5:0.0}");
            }
            report.AppendLine("  (superseded: a spacing of 0.08 relative to a peak that SLID with the wind)");
            TestContext.WriteLine(report.ToString());

            float wide = WaveSpectrum.LadderRelativeSpacing(8, 5f, 30f);
            float narrow = WaveSpectrum.LadderRelativeSpacing(8, 8f, 30f);
            Assert.Greater(wide, narrow,
                "a wider ladder at the same bin count MUST space its bins further apart in frequency — " +
                "that is the trade, and if this reverses the ladder arithmetic is wrong");

            float more = WaveSpectrum.LadderRelativeSpacing(8, 5f, 30f);
            float fewer = WaveSpectrum.LadderRelativeSpacing(4, 5f, 30f);
            Assert.Less(more, fewer,
                "…and more bins across the same range must bring them CLOSER, which is the other half " +
                "of the menu: bin count buys back the group rhythm a wide ladder spends");

            Assert.Greater(WaveSpectrum.BeatPeriodSeconds(30f, G, wide), 15f,
                "⭐ THE SHIPPED DEFAULT'S ACCEPTANCE: at the ladder's long end — where the energy sits " +
                "in the blow the owner sails — the group beat must still read as a group (>15 s) " +
                "rather than as chop. ⚠️ At the ladder's SHORT end the beat is faster than that, which " +
                "is the honest cost of a finite ladder; the binding acceptance for grouping is " +
                "WaveSpectrumTests' run-length metric, and 5-30 m is the widest ladder that passes it.");
        }

        /// <summary>
        /// ✅ <b>What the ladder conserves across the blend — and it is a DIFFERENT quantity
        /// depending on whether row 33's height law is on.</b>
        ///
        /// <para><b>Height law OFF (the reference tuning, and every pre-2026-09-09 asset):</b> the
        /// spectrum normalizes onto the hand-authored trains' amplitude ENVELOPE, so <c>Σa</c> is
        /// unchanged by the ladder. That was row 34's own guarantee and it still holds.</para>
        ///
        /// <para><b>Height law ON (the shipped asset since row 33):</b> the finished field is scaled
        /// so its significant height matches the fetch law, so <b><c>Hs</c> is what the blend
        /// conserves and the envelope is not</b>. That is arithmetic, not a choice: at a fixed
        /// <c>Hs</c> (fixed <c>Σa²</c>), spreading the same energy across eight bins instead of four
        /// RAISES <c>Σa</c> — measured at 1.44x on the shipped config.</para>
        ///
        /// <para>⚠️ <b>So row 33 moves two calibrated numbers, and this is where that is said out
        /// loud.</b> <c>TotalAmplitude</c> is the whitecap crest-factor normalizer AND the bound the
        /// watertight hull clamp scans against. Against the superseded sea it drops (0.664 -> 0.393 m
        /// at 5.70 m/s, the sea simply being shorter); across the blend at fixed height it rises
        /// 1.44x. Neither is a defect — they are what "the height comes from the fetch" means — but
        /// both are worth the owner's eye at the helm, because foam scoring and how high the hull
        /// sits both read from that number.</para>
        /// </summary>
        [Test]
        public void WhatTheLadderConservesAcrossTheBlend_IsTheEnvelope_OrTheHEIGHT_IfRow33IsOn()
        {
            WaveFieldSettings ladder = Shipped();
            WaveFieldSettings legacy = Shipped();
            legacy.SpectrumBlend = 0f;                    // the hand-authored 4-train passthrough

            bool heightLaw = ladder.HeightFromFetch;
            var report = new StringBuilder(
                $"  height law {(heightLaw ? "ON - Hs is conserved" : "OFF - the envelope is conserved")}\n" +
                "  wind   4-train SumA   8-bin SumA   ratio |   4-train Hs    8-bin Hs\n");

            foreach (float u in ReachableWinds)
            {
                WaveTrains a = At(u, in legacy), b = At(u, in ladder);
                float sumA = a.TotalAmplitude, sumB = b.TotalAmplitude;
                float hsA = WaveMath.SignificantHeightMeters(in a);
                float hsB = WaveMath.SignificantHeightMeters(in b);
                report.AppendLine($"  {u,5:0.00} {sumA,14:0.0000} {sumB,12:0.0000} {(sumA > 1e-9f ? sumB / sumA : 1f),7:0.000} |" +
                                  $" {hsA,12:0.0000} {hsB,11:0.0000}");

                if (heightLaw)
                    Assert.AreEqual(hsA, hsB, Mathf.Max(1e-4f, hsA * 0.02f),
                        $"at {u:0.00} m/s the ladder draws Hs {hsB:0.0000} m against the hand-authored " +
                        $"field's {hsA:0.0000} m. With row 33's height law on, BOTH are scaled onto the " +
                        "fetch law's height, so the blend must not change how tall the sea is - only " +
                        "how its energy is distributed across frequencies.");
                else
                    Assert.AreEqual(sumA, sumB, Mathf.Max(1e-4f, sumA * 0.02f),
                        $"at {u:0.00} m/s the ladder's total amplitude is {sumB:0.0000} against the " +
                        $"hand-authored field's {sumA:0.0000}. With the height law off the spectrum " +
                        "normalizes onto the legacy ENVELOPE by construction; if that has broken, the " +
                        "whitecap crest factor and the hull clamp have both moved for no reason.");
            }
            TestContext.WriteLine(report.ToString());

            // ...and the OTHER quantity is reported rather than asserted, because which one moves is
            // the consequence of a ruling, not a defect to be pinned.
            if (heightLaw)
            {
                WaveTrains a = At(5.70f, in legacy), b = At(5.70f, in ladder);
                Assert.Greater(b.TotalAmplitude, a.TotalAmplitude,
                    "⭐ AND THE ENVELOPE MUST RISE across the blend at a fixed height, because " +
                    "spreading the same energy over more bins raises Σa while holding Σa². If it " +
                    "did not, the eight-bin field would be carrying its energy in fewer effective " +
                    "bins than the four-train one, which would mean the spectrum is not spreading.");
            }
        }

        /// <summary>
        /// ⚠️ <b>The passthrough is still the passthrough.</b> <c>SpectrumBlend = 0</c> must return the
        /// hand-authored four-train field bit-for-bit — ADR 0027's promise, and what keeps every
        /// pre-spectrum guard in the suite meaningful. The ladder lives on the spectral path only.
        /// (It is also why this PR could change the sea's structure without moving 30 other test
        /// classes: they all build settings whose blend is 0.)
        /// </summary>
        [Test]
        public void AtBlendZero_TheHandAuthoredFieldIsUntouched()
        {
            WaveFieldSettings s = Shipped();
            s.SpectrumBlend = 0f;

            foreach (float u in ReachableWinds)
            {
                WaveTrains t = At(u, in s);
                float peak = Mathf.Clamp(WaveMath.PeakWavelengthMeters(u, in s),
                                         WaveTrain.MinWavelengthMeters, s.DominantWavelengthMax);
                Assert.AreEqual(1 + s.SecondaryTrainCount, t.Count,
                    "the passthrough is the four-train field, not the eight-bin one");
                Assert.AreEqual(peak, t[0].Wavelength, peak * 1e-4f,
                    $"at blend 0 and {u:0.00} m/s the primary must still be the peak law's own " +
                    "wavelength — the ladder must not have leaked into the passthrough");
            }
        }
    }
}
