using System;
using System.Text;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>THE DRAWN SEA WAS NOT A FUNCTION OF (worldSeed, gameTime) — it was a function of the
    /// frame sequence. ✅ RETIRED 2026-09-09 (PR E): the presentation phase is the closed form.</b>
    ///
    /// <para><see cref="WaveFieldAnimator"/> advanced each train's travel phase incrementally —
    /// <c>Φ += k·c·dt</c> — and baked it into <c>PhaseOffset</c>. Its own class doc said what that
    /// cost: <i>"it is NOT a pure function of gameTime: two machines running different frame rates
    /// ease and accumulate along different paths, and a save/load does not reproduce its state."</i>
    /// That is a <b>rule-5 leak in the thing the player looks at</b>, and it is why the drawn sea and
    /// the ridden sea travelled two different code paths at all.</para>
    ///
    /// <para><b>Why the accumulator existed, and why that reason has expired.</b> It was there because
    /// a parameter change JUMPED the phase: <c>ω</c> came from <c>λ_p(U)</c>, so every mood change
    /// moved it and <c>Δω·t</c> threw the sea across the wave — register row 34, measured at up to
    /// 474 858 radians at thirty hours. <b>PR B put the bins on a fixed ladder</b>, so <c>λ</c> no
    /// longer depends on the wind, <c>ω</c> is constant, and <c>ω·t</c> is continuous by itself. The
    /// accumulator stopped being what made the phase continuous and became only the leak.</para>
    ///
    /// <para><b>The change is small on purpose.</b> <c>Φ(t) = wrap(k·c·t)</c> in double, wrapped
    /// before it drops to float — exactly what <see cref="WaveMath.Sample"/> does, and for the same
    /// reason (ω·t reaches millions of radians in a long session; float32 would lose the wave).
    /// <b>The shader does not change at all:</b> it still reads the baked <c>_WavePhases</c> and adds
    /// its own look-back. Only the SOURCE of that number moves, from a running total to a clock.</para>
    ///
    /// <para>Pure arithmetic over the shipped derivation: no scene, no clock service, no GPU.</para>
    /// </summary>
    public class WaveFieldPhaseIsClosedFormTests
    {
        const float Dt60 = 1f / 60f;
        const float Dt30 = 1f / 30f;
        static readonly Vector2 Wind = new Vector2(4.2f, 1.1f);
        const float Sea = 0.55f;

        /// <summary>The sea the owner plays — the asset, not the code defaults, so the ladder is the
        /// fixed one this change depends on.</summary>
        static WaveFieldSettings Field() => ShippedWaveField.Settings();
        static WaveFieldAnimatorSettings Smoothing() => WaveFieldAnimatorSettings.Default;

        /// <summary>Run a fresh animator from t = 0 to <paramref name="untilSeconds"/> in steps of
        /// <paramref name="dt"/>, and hand back what it ended up drawing.</summary>
        static WaveTrains RunTo(double untilSeconds, float dt)
        {
            var animator = new WaveFieldAnimator();
            WaveFieldSettings f = Field();
            WaveFieldAnimatorSettings s = Smoothing();
            double t = 0;
            WaveTrains last = WaveTrains.None;
            while (t < untilSeconds - 1e-9)
            {
                double step = Math.Min(dt, untilSeconds - t);
                t += step;
                last = animator.Tick((float)step, t, Wind, Sea, in f, in s);
            }
            return last;
        }

        static double Wrap(double r) => r - Math.Floor(r / (2.0 * Math.PI)) * (2.0 * Math.PI);

        /// <summary>Signed smallest angle between two phases, so 0.001 and 2π−0.001 read as close.</summary>
        static double PhaseGap(float a, float b)
        {
            double d = Wrap(a - b);
            return d > Math.PI ? 2.0 * Math.PI - d : d;
        }

        // =============================================================================================

        /// <summary>
        /// 🔴 <b>THE LEAK, CLOSED: two frame rates reach the SAME sea at the same game time.</b> This
        /// is the assertion the superseded animator could not pass, by its own admission — and it is
        /// the whole of why the drawn sea was not a function of <c>(worldSeed, gameTime)</c>.
        /// </summary>
        [Test]
        public void TwoFrameRates_DrawTheSameSea_AtTheSameGameTime()
        {
            const double Until = 600.0;                 // ten minutes of game time
            WaveTrains at60 = RunTo(Until, Dt60);
            WaveTrains at30 = RunTo(Until, Dt30);

            Assert.AreEqual(at60.Count, at30.Count, "the two runs must have the same live trains");
            var report = new StringBuilder("  bin   phase@60fps   phase@30fps        gap\n");
            for (int i = 0; i < at60.Count; i++)
            {
                double gap = PhaseGap(at60[i].PhaseOffset, at30[i].PhaseOffset);
                report.AppendLine($"  {i,3} {at60[i].PhaseOffset,13:0.000000} {at30[i].PhaseOffset,13:0.000000} {gap,10:0.###e+0}");
                Assert.Less(gap, 1e-4,
                    $"⭐ bin {i}: at {Until:0} s of game time a 60 fps machine and a 30 fps machine " +
                    $"draw phases {gap:0.####} radians apart. They must draw the SAME sea — the phase " +
                    "is ω·t at the clock, not a running total, so the frame sequence cannot enter it. " +
                    "If this fires, an accumulator has come back.");
            }
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// ⚠️ <b>THE DEAD CONTROL, and it is the one that makes the test above mean anything.</b> The
        /// SUPERSEDED accumulator is modelled here and must FAIL the same comparison. Without it,
        /// "two frame rates agree" would also be satisfied by a field that ignored time entirely.
        /// </summary>
        [Test]
        public void DEADCONTROL_TheSupersededAccumulator_DivergedBetweenFrameRates()
        {
            // Φ += k·c·dt, in float, exactly as the retired code did it.
            float Accumulate(double until, float dt, float waveNumber, float phaseSpeed)
            {
                float phi = 0f;
                double t = 0;
                while (t < until - 1e-9)
                {
                    double step = Math.Min(dt, until - t);
                    t += step;
                    phi = (float)Wrap(phi + waveNumber * phaseSpeed * step);
                }
                return phi;
            }

            WaveFieldSettings f = Field();
            WaveTrains trains = WaveMath.TrainsFrom(Wind, Sea, in f);
            float k = 2f * Mathf.PI / trains[0].Wavelength;

            const double Until = 600.0;
            float a = Accumulate(Until, Dt60, k, trains[0].PhaseSpeed);
            float b = Accumulate(Until, Dt30, k, trains[0].PhaseSpeed);
            double gap = PhaseGap(a, b);

            TestContext.WriteLine($"  the retired accumulator at {Until:0} s: 60 fps {a:0.000000}, " +
                                  $"30 fps {b:0.000000}, gap {gap:0.####} rad");
            Assert.Greater(gap, 1e-4,
                "⭐ the superseded accumulator MUST diverge between frame rates here, or the test " +
                "above is not measuring anything. Its own class doc said so: 'two machines running " +
                "different frame rates ease and accumulate along different paths'. This is that " +
                "sentence, as a number.");
        }

        /// <summary>
        /// ✅ <b>A COLD START AT ANY GAME TIME DRAWS THE SAME SEA AS A LONG RUN TO IT.</b> This is
        /// what a save/load has to survive, and what the accumulator could not give: its state was
        /// the history, and nothing saved it (rule 5 says nothing should).
        /// </summary>
        [Test]
        public void AColdStart_DrawsWhatALongRunWouldHave_SoALoadResumesTheSameSea()
        {
            const double Until = 3600.0;                          // an hour in
            WaveTrains longRun = RunTo(Until, Dt60);              // 216 000 ticks of history
            WaveTrains coldStart = RunTo(Until, (float)Until);    // one tick, straight to the hour

            Assert.AreEqual(longRun.Count, coldStart.Count);
            for (int i = 0; i < longRun.Count; i++)
                Assert.Less(PhaseGap(longRun[i].PhaseOffset, coldStart[i].PhaseOffset), 1e-4,
                    $"⭐ bin {i}: an animator that has run for an hour and one that has just been " +
                    "created must draw the same phase at the same game time. The retired accumulator " +
                    "could not — its phase WAS the history — which is why a save/load did not " +
                    "reproduce the sea the player left.");
        }

        /// <summary>
        /// ⭐ <b>SEE == FEEL, MADE EXACT (P1).</b> The drawn field sampled at <c>t = 0</c> must be the
        /// closed form sampled at the game time — the same function, not two paths that agree.
        /// <c>BoatController</c> rides <c>WaveMath.Sample(pos, now, TrainsFrom(...))</c>; the shader
        /// draws <c>WaveMath.Sample(pos, 0, animator.Current)</c>. Since PR E those are one thing.
        ///
        /// <para>⚠️ Compared after the amplitude easing has settled: the animator eases AMPLITUDE on
        /// purpose (a mood should arrive over a second and a half, not in a frame), and that easing is
        /// still frame-rate-shaped. It is the PHASE that had to stop being a running total, and the
        /// amplitude difference is deliberately not asserted away here.</para>
        /// </summary>
        [Test]
        public void TheDrawnSurface_IsTheRiddenSurface_OnceTheEasingHasSettled()
        {
            const double Until = 120.0;
            WaveTrains drawn = RunTo(Until, Dt60);
            WaveFieldSettings f = Field();
            WaveTrains ridden = WaveMath.TrainsFrom(Wind, Sea, in f);

            var report = new StringBuilder("  probe        drawn h      ridden h        diff\n");
            double worst = 0;
            foreach (var p in new[] { new Vector2(0f, 0f), new Vector2(17.5f, -8.25f),
                                      new Vector2(-40.75f, 22.5f), new Vector2(103.25f, 61f) })
            {
                float a = WaveMath.Sample(p, 0.0, in drawn).Height;      // the animator bakes the phase
                float b = WaveMath.Sample(p, Until, in ridden).Height;   // the sim's own closed form
                worst = Math.Max(worst, Math.Abs(a - b));
                report.AppendLine($"  {p.x,6:0.0},{p.y,6:0.0} {a,12:0.00000} {b,13:0.00000} {a - b,11:0.000000}");
            }
            TestContext.WriteLine(report.ToString());

            Assert.Less(worst, 0.01,
                $"⭐ the drawn sea and the ridden sea differ by up to {worst:0.0000} m at the same " +
                "instant. They are supposed to be ONE function of one clock now: the animator's " +
                "PhaseOffset is φ_hash − ω·t, so sampling it at t = 0 reproduces the closed form at " +
                "t exactly. A gap here means the presentation path has drifted off the sim path " +
                "again — which is the thing P1 forbids and the thing PR E exists to close.");
        }

        /// <summary>
        /// The phase the animator bakes must literally BE <c>wrap(φ_hash − ω·t)</c>. The tests above
        /// compare two runs against each other, which a consistently-wrong implementation would also
        /// pass; this one names the number.
        /// </summary>
        [Test]
        public void ThePhaseIsLiterallyTheClosedForm_NotMerelyConsistent()
        {
            const double Until = 500.0;
            WaveTrains drawn = RunTo(Until, Dt60);
            WaveFieldSettings f = Field();
            WaveTrains target = WaveMath.TrainsFrom(Wind, Sea, in f);

            for (int i = 0; i < drawn.Count; i++)
            {
                double k = 2.0 * Math.PI / target[i].Wavelength;
                double expected = Wrap(target[i].PhaseOffset - Wrap(k * target[i].PhaseSpeed * Until));
                Assert.Less(PhaseGap(drawn[i].PhaseOffset, (float)expected), 1e-4,
                    $"bin {i}: the baked phase must be wrap(φ_hash − ω·t) at the game clock, and it " +
                    "is not. Everything else in this file compares runs to each other and would not " +
                    "notice a consistent error; this is the assertion that names the value.");
            }
        }

        /// <summary>
        /// ⚠️ <b>What the ladder buys, stated where it will be read.</b> The closed form is only
        /// continuous because <c>ω</c> is constant, and <c>ω</c> is only constant because PR B fixed
        /// the bins. This asserts that dependency directly, so anyone who un-fixes the ladder finds
        /// out here rather than in the owner's next playtest.
        /// </summary>
        [Test]
        public void ThisOnlyWorksBecauseTheBinsAreFIXED_AndThatIsAssertedHere()
        {
            WaveFieldSettings f = Field();
            WaveTrains light = WaveMath.TrainsFrom(new Vector2(1.63f, 0f), 0.18f, in f);
            WaveTrains blow = WaveMath.TrainsFrom(new Vector2(5.70f, 0f), 0.55f, in f);

            for (int i = 0; i < Math.Min(light.Count, blow.Count); i++)
                Assert.AreEqual(light[i].Wavelength, blow[i].Wavelength, 0f,
                    $"⭐ bin {i}'s wavelength moved with the wind. The presentation phase is now ω·t " +
                    "evaluated at the clock, which is continuous ONLY while ω is constant. Put the " +
                    "wind back into the wavelength and every mood change throws the drawn sea across " +
                    "the wave by Δω·t — register row 34, which measured 474 858 radians at thirty " +
                    "hours. The fixed ladder is load-bearing for this file.");
        }
    }
}
