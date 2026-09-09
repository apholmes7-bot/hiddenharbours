using System;
using System.Text;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>THE SEA VIBRATES WHEN ITS MOOD CHANGES — named, and it is one multiplication.</b>
    ///
    /// <para>Owner, 2026-09-08, on the Nine Mile Creek beach: <i>"the water starts oscillating very
    /// quickly when it changes states from light to calm to moderate etc, its like its speed resynches
    /// and it vibrates and its very noticable."</i></para>
    ///
    /// <para><b>The mechanism is in <see cref="WaveMath.Sample"/>, and it is exact:</b></para>
    /// <code>
    ///     travel = d·x - PhaseSpeed * timeSeconds;
    ///     phase  = waveNumber * travel + PhaseOffset;
    /// </code>
    /// <para>which is <c>φ = k·x − ω·t</c> with <c>ω = k·c = √(2πg/λ)</c>. <b><c>λ</c> is a function of
    /// the wind.</b> So when the sea state changes, ω changes, and the phase at a FIXED world point and
    /// a FIXED instant moves by <c>Δω · t</c>. The drawn phase rate during a transition is</para>
    /// <code>
    ///     dφ/dt = ω + t · dω/dt
    ///             ^^^   ^^^^^^^^
    ///             the   THE BUG: an error proportional to how long the world has been running
    ///             wave
    /// </code>
    ///
    /// <para><b>It is not a constant defect — it gets worse the longer you play.</b> At one minute of
    /// game time it is invisible; at a few hours it is hundreds of cycles per second. That is exactly
    /// "its speed resynches and it vibrates", and it is why it reads as a glitch rather than as weather.</para>
    ///
    /// <para>⚠️ <b>Rule 5 is not the problem here and must not be blamed for it.</b> The field is a pure
    /// function of (seed, gameTime) and that is right. The defect is that the function is
    /// <c>ω(now)·t</c> — a frequency that is current multiplied by a time that is total. A sea does not
    /// re-phase its whole history when the wind freshens.</para>
    ///
    /// <para>Pure arithmetic over the shipped law: no scene, no clock, no graphics device.</para>
    /// </summary>
    public class WaveStateChangeVibrationTests
    {
        const float G = 9.81f;

        /// <summary>
        /// The sea the owner actually plays. ⚠️ <b>This used to be
        /// <c>WaveFieldSettings.Default</c> with <c>SeaFetchKilometres</c> patched, and that was
        /// wrong</b> — the asset also overrides <c>SeaStateAmplitudeExponent</c> (1.35 -> 1.5),
        /// <c>CrestSharpening</c> (2.2 -> 2.6) and, decisively, <c>SpectrumBlend</c> (0 -> 0.65),
        /// which is the difference between the hand-authored FOUR-train field and the eight-bin
        /// spectral one. Every number this fixture published before 2026-09-09 therefore described a
        /// sea nobody sails. <see cref="ShippedWaveField"/> carries the one mirror now, with a guard
        /// that walks the asset's own keys.
        /// </summary>
        static WaveFieldSettings Shipped() => ShippedWaveField.Settings();

        /// <summary>The primary train's angular frequency at a wind speed, through the SUPERSEDED
        /// peak-scaled law — <c>ω = k·c</c> with <c>c = √(gλ/2π)</c>, which is <c>√(2πg/λ)</c>.
        /// ⚠️ Kept as the RECORD of the defect: since 2026-09-09 the shipped field's bins do not
        /// scale with λ_p at all, so this is a transcription of what the sea used to do, not of what
        /// it does. <see cref="TheShippedField_DoesNoneOfThis"/> is the half that reads production.</summary>
        static double Omega(float windSpeed, in WaveFieldSettings s)
        {
            float lambda = WaveMath.PeakWavelengthMeters(windSpeed, in s);
            return Math.Sqrt(2.0 * Math.PI * G / lambda);
        }

        /// <summary>The drawn phase at a world point, exactly as <see cref="WaveMath.Sample"/> forms it:
        /// <c>k·x − ω·t</c>. Unwrapped, so a rate can be taken across it.</summary>
        static double Phase(float windSpeed, double t, double alongMetres, in WaveFieldSettings s)
        {
            float lambda = WaveMath.PeakWavelengthMeters(windSpeed, in s);
            double k = 2.0 * Math.PI / lambda;
            double c = Math.Sqrt(G * lambda / (2.0 * Math.PI));
            return k * (alongMetres - c * t);
        }

        /// <summary>A linear wind transition, the shape a mood change has while it blends.</summary>
        static float WindAt(double tau, float from, float to, float blendSeconds)
            => Mathf.Lerp(from, to, Mathf.Clamp01((float)(tau / Mathf.Max(1e-3f, blendSeconds))));

        /// <summary>Peak |dφ/dt| across a transition, in CYCLES PER SECOND at one world point — what the
        /// eye actually counts. Sampled at 60 fps, the rate the owner is looking at.</summary>
        static double PeakDrawnRateHz(double startTime, float fromWind, float toWind,
                                      float blendSeconds, in WaveFieldSettings s)
        {
            const double Dt = 1.0 / 60.0;
            double peak = 0.0;
            for (double tau = 0.0; tau <= blendSeconds; tau += Dt)
            {
                double p0 = Phase(WindAt(tau, fromWind, toWind, blendSeconds), startTime + tau, 0.0, in s);
                double p1 = Phase(WindAt(tau + Dt, fromWind, toWind, blendSeconds),
                                  startTime + tau + Dt, 0.0, in s);
                peak = Math.Max(peak, Math.Abs(p1 - p0) / Dt);
            }
            return peak / (2.0 * Math.PI);
        }

        // =============================================================================================

        /// <summary>
        /// 🔴 <b>THE DEFECT, AND ITS SIGNATURE: it grows with how long the world has been running.</b>
        /// A wave the eye can follow runs well under 1 Hz. This reports what the drawn field actually
        /// does during one light→blow transition, at a spread of game times.
        /// </summary>
        [Test]
        public void TheDrawnPhaseRate_DuringAStateChange_GrowsWithELAPSEDGAMETIME()
        {
            WaveFieldSettings s = Shipped();
            const float From = 1.63f, To = 5.70f;      // light airs -> a blow, the owner's "light to moderate"
            const float Blend = 4f;                    // seconds of transition

            double physical = Omega(To, in s) / (2.0 * Math.PI);   // what a wave at that state actually does

            var report = new StringBuilder();
            report.AppendLine("THE VIBRATION — drawn phase rate during a light->blow change, at one point");
            report.AppendLine($"  the wave itself runs at {physical:0.000} Hz " +
                              $"(peak {WaveMath.PeakWavelengthMeters(To, in s):0.0} m)");
            report.AppendLine();
            report.AppendLine("  game time     | drawn rate | times the real wave");

            double atOneMinute = 0, atThreeHours = 0;
            foreach (var (label, t) in new[]
                     { ("1 min", 60.0), ("10 min", 600.0), ("1 hour", 3600.0),
                       ("3 hours", 10800.0), ("1 day", 86400.0) })
            {
                double hz = PeakDrawnRateHz(t, From, To, Blend, in s);
                report.AppendLine($"  {label,-13} | {hz,7:0.0} Hz | {hz / physical,15:0} x");
                if (label == "1 min") atOneMinute = hz;
                if (label == "3 hours") atThreeHours = hz;
            }
            report.AppendLine();
            report.AppendLine("  dphi/dt = omega + t * domega/dt. The second term is the whole defect: a");
            report.AppendLine("  frequency that is CURRENT multiplied by a time that is TOTAL.");
            TestContext.WriteLine(report.ToString());

            Assert.Greater(atThreeHours, atOneMinute * 10.0,
                "⭐ THE SIGNATURE: the vibration must get worse the longer the world has run. That is " +
                "what identifies it as t * domega/dt and not as a wave, a blend curve or a frame-rate " +
                "artefact — none of those care how long you have been playing.");

            Assert.Greater(atThreeHours, 10.0,
                "⭐ AND IT MUST BE VIOLENT at a few hours in. A sea runs well under 1 Hz; anything in " +
                "the tens of Hz is the vibration the owner reported, not weather.");
        }

        /// <summary>
        /// ✅ <b>THE FIX, AND IT SHIPPED (owner ruling 2026-09-09, register row 34).</b> Fix the
        /// frequencies and let the sea state move only the AMPLITUDES. Physically this is what a growing sea does — it puts
        /// energy into frequencies that were always there, it does not slide existing waves up the
        /// scale — and arithmetically it removes <c>t·dω/dt</c> because ω stops depending on t at all.
        ///
        /// <para>The field was already an 8-train JONSWAP-shaped spectrum (<c>WaveTrains.MaxTrains</c>),
        /// so the bins to hold fixed already existed — they were simply all being scaled by
        /// <c>λ_p(U)</c>. ⚠️ Rule 5 stays satisfied: still a pure function of (seed, gameTime), still
        /// no accumulator, still nothing saved.</para>
        ///
        /// <para>⚠️ <b>This test models the fix; it does not read production.</b> That is
        /// <see cref="TheShippedField_DoesNoneOfThis"/>, and the difference matters — a model that
        /// agrees with itself is the failure mode this repo calls "two transcriptions agreeing".</para>
        /// </summary>
        [Test]
        public void FixedFrequencies_RemoveTheVibrationEntirely_AndStayDeterministic()
        {
            WaveFieldSettings s = Shipped();
            const float From = 1.63f, To = 5.70f, Blend = 4f;

            // The same transition, with omega FROZEN at one reference and only the amplitude moving.
            double refOmega = Omega(To, in s);
            double FixedPhase(double t) => -refOmega * t;

            double peakFixed = 0.0;
            const double Dt = 1.0 / 60.0;
            for (double tau = 0.0; tau <= Blend; tau += Dt)
            {
                double d = Math.Abs(FixedPhase(10800.0 + tau + Dt) - FixedPhase(10800.0 + tau)) / Dt;
                peakFixed = Math.Max(peakFixed, d);
            }
            peakFixed /= 2.0 * Math.PI;

            double peakShipped = PeakDrawnRateHz(10800.0, From, To, Blend, in s);
            double physical = refOmega / (2.0 * Math.PI);

            TestContext.WriteLine(
                $"  at 3 hours of game time, through a light->blow change:\n" +
                $"    shipped (omega follows the wind): {peakShipped,9:0.0} Hz\n" +
                $"    frequencies held fixed:           {peakFixed,9:0.000} Hz\n" +
                $"    the wave itself:                  {physical,9:0.000} Hz");

            Assert.AreEqual(physical, peakFixed, 1e-6,
                "⭐ WITH THE FREQUENCIES FIXED the drawn rate IS the wave's own rate — exactly, at every " +
                "game time, through any transition. The vibration is not reduced, it is absent, because " +
                "the term that produced it no longer exists.");

            Assert.Greater(peakShipped, peakFixed * 100.0,
                "...against the shipped law at the same moment. This ratio is the size of the proposal.");
        }

        /// <summary>
        /// ⚠️ <b>THE DEAD CONTROL, and it is the one that keeps this honest.</b> With the wind HELD
        /// STILL the shipped law is already perfect at every game time — the phase advances at exactly
        /// the wave's own rate. So the defect is not "the sea is drawn wrong"; it is <b>only</b> in the
        /// CHANGE. A fixture that reported trouble here would be measuring its own arithmetic.
        /// </summary>
        [Test]
        public void WithTheWindHELDSTILL_TheShippedLawIsExact_AtEveryGameTime()
        {
            WaveFieldSettings s = Shipped();
            const float U = 5.70f;
            double physical = Omega(U, in s) / (2.0 * Math.PI);

            foreach (double t in new[] { 60.0, 3600.0, 86400.0, 864000.0 })
            {
                double hz = PeakDrawnRateHz(t, U, U, 4f, in s);     // from == to: no state change
                Assert.AreEqual(physical, hz, physical * 1e-3,
                    $"DEAD CONTROL: at {t:0} s of game time with a steady wind the drawn rate must BE " +
                    "the wave's rate. If this drifts, the defect measured above is float error in this " +
                    "fixture rather than the state change, and every number in it is void.");
            }
            TestContext.WriteLine($"  steady wind: exact at 1 min through 10 days, {physical:0.000} Hz");
        }

        /// <summary>
        /// ✅ <b>AND THE SHIPPED FIELD DOES NONE OF IT — read out of production, not modelled.</b>
        ///
        /// <para>Everything above transcribes the superseded arithmetic, which is the right way to
        /// keep the RECORD of a defect but says nothing about the code that ships. This asks
        /// <see cref="WaveMath.TrainsFrom"/> itself, on the owner's own settings, and checks the two
        /// things the ruling promised: every bin's ω is the same number at every wind, and the drawn
        /// phase rate at a point is therefore the wave's own rate at any age of the world.</para>
        ///
        /// <para>⚠️ The bound is EXACT rather than tolerant. <c>Δω</c> is not small now, it is
        /// identically zero — and "small" is what the defect looked like for the first minute of every
        /// session before it grew into the owner's <i>"it vibrates"</i>.</para>
        /// </summary>
        [Test]
        public void TheShippedField_DoesNoneOfThis()
        {
            WaveFieldSettings s = Shipped();
            const float From = 1.63f, To = 5.70f;

            WaveTrains a = WaveMath.TrainsFrom(new Vector2(0f, From), Mathf.Clamp01(From / 10f), in s);
            WaveTrains b = WaveMath.TrainsFrom(new Vector2(0f, To), Mathf.Clamp01(To / 10f), in s);
            Assert.AreEqual(a.Count, b.Count, "the live train count moved with the wind");
            Assert.Greater(a.Count, 0, "no trains at all — every assertion below would be vacuous");

            var report = new StringBuilder();
            report.AppendLine("  bin   lambda@1.63   lambda@5.70   |d(omega)|   |d(phi)| at 30 h");
            double worst = 0.0;
            for (int i = 0; i < a.Count; i++)
            {
                double wA = Math.Sqrt(2.0 * Math.PI * G / a[i].Wavelength);
                double wB = Math.Sqrt(2.0 * Math.PI * G / b[i].Wavelength);
                double dPhi = Math.Abs(wB - wA) * 30.0 * 3600.0;
                worst = Math.Max(worst, dPhi);
                report.AppendLine($"  {i,3} {a[i].Wavelength,13:0.000} {b[i].Wavelength,13:0.000} " +
                                  $"{Math.Abs(wB - wA),12:0.###e+0} {dPhi,17:0.###}");
            }
            TestContext.WriteLine(report.ToString());

            Assert.AreEqual(0.0, worst, 1e-9,
                "⭐ ROW 34's ACCEPTANCE, on the shipped derivation: across a light->blow change, at " +
                "thirty hours of game time, the phase at a fixed point must not move at all. The " +
                "superseded law put up to 474 858 radians through here — about 75 000 whole cycles.");

            // ...and the sea did change, so the zero above is not the zero of a field that ignores
            // the weather. This is the same dead control the ladder guard carries, kept here too
            // because THIS fixture's whole subject is a change that produced no motion.
            Assert.Greater(b.TotalAmplitude, a.TotalAmplitude * 1.5f,
                "DEAD CONTROL: the light->blow step must be a real change in the sea's height, or a " +
                "phase that did not move proves nothing whatever.");
        }
    }
}
