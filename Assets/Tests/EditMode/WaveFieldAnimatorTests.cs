using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The presentation-side wave-field smoother (<see cref="WaveFieldAnimator"/>, ADR 0018
    /// addendum — the fix for the owner's "jittery rocking, especially in calm seas"). These pin
    /// the four properties the class exists for: (1) the sampled surface is PHASE-CONTINUOUS across
    /// a sharp weather step (the old closed-form path jumped at every trains refresh because k and
    /// c changed under a large running t); (2) GLASS IS SACRED — eased amplitudes snap to exactly 0
    /// at sea state 0, never an asymptotic almost-zero; (3) dispersion holds on the EASED
    /// parameters (c = √(g·λ/2π), speed never free); (4) the smoothing is fps-independent (two
    /// half-steps == one full step; same accumulated field at any tick rate for constant weather).
    /// All pure math, headless, deterministic.
    /// </summary>
    public class WaveFieldAnimatorTests
    {
        private static readonly Vector2 SamplePos = new Vector2(12.3f, -7.7f);
        private static readonly Vector2 WindA = new Vector2(6f, 0f);
        private static readonly Vector2 WindB = new Vector2(2.5f, 9.5f);   // sharp shift: speed AND direction

        private const float Dt60 = 1f / 60f;

        private static WaveFieldSettings Field => WaveFieldSettings.Default;
        private static WaveFieldAnimatorSettings Anim => WaveFieldAnimatorSettings.Default;

        private static float Height(WaveFieldAnimator animator) => animator.Sample(SamplePos).Height;

        // ---- (1) phase continuity — THE jitter fix ---------------------------------------------

        [Test]
        public void SharpWindStep_WithTinyDt_HeightIsContinuous()
        {
            // The distilled bug: the OLD path re-derived TrainsFrom under a large running t, so a
            // wind change JUMPED the phase even across an infinitesimal instant. By construction
            // the animator cannot: with dt→0 nothing eases and no phase accrues, step or no step.
            var field = Field;
            var anim = Anim;
            var animator = new WaveFieldAnimator();

            for (int i = 0; i < 200; i++)
                animator.Tick(Dt60, WindA, 0.35f, in field, in anim);   // build up real phase
            float before = Height(animator);

            animator.Tick(1e-5f, WindB, 0.8f, in field, in anim);       // the weather STEPS, time doesn't
            float after = Height(animator);

            Assert.AreEqual(before, after, 1e-3f,
                "a sharp weather step across a near-zero instant must not move the surface — " +
                "the phase is accumulated incrementally, so it cannot jump when k/c retarget");
        }

        [Test]
        public void SharpWindStep_AtSixtyFps_FrameDeltasStayBounded()
        {
            // Belt and braces at a real frame rate: through the whole eased transition (calm-ish
            // sea → near-gale on a different bearing) every frame-to-frame height delta stays
            // inside the physical rate of the sea itself (~0.1 m/frame for the Default field at
            // 60 fps — see the analytic bound A·p·ω per train). The old refresh jump could be an
            // arbitrary fraction of the full envelope in ONE frame.
            var field = Field;
            var anim = Anim;
            var animator = new WaveFieldAnimator();

            for (int i = 0; i < 200; i++)
                animator.Tick(Dt60, WindA, 0.35f, in field, in anim);

            float previous = Height(animator);
            float maxDelta = 0f;
            for (int i = 0; i < 600; i++)   // 10 s: the whole ease through the step and beyond
            {
                animator.Tick(Dt60, WindB, 0.8f, in field, in anim);
                float h = Height(animator);
                maxDelta = Mathf.Max(maxDelta, Mathf.Abs(h - previous));
                previous = h;
            }

            Assert.Less(maxDelta, 0.15f,
                "frame deltas through a sharp weather transition must stay within the sea's own " +
                "physical rate — no refresh pops");
        }

        // ---- (2) glass is sacred ----------------------------------------------------------------

        [Test]
        public void SeaStateZero_EasedAmplitudes_SnapToExactZero()
        {
            // Exponential easing toward 0 is asymptotic — the snap floor must land it EXACTLY, or
            // the dead-calm mirror (the owner's ruling) never arrives.
            var field = Field;
            var anim = Anim;
            var animator = new WaveFieldAnimator();

            for (int i = 0; i < 100; i++)
                animator.Tick(Dt60, WindA, 0.5f, in field, in anim);    // a real sea first
            Assert.Greater(animator.Current.TotalAmplitude, 0f, "sanity: the sea was running");

            for (int i = 0; i < 1500; i++)                              // 25 s of dead calm
                animator.Tick(Dt60, WindA, 0f, in field, in anim);

            WaveTrains trains = animator.Current;
            for (int i = 0; i < trains.Count; i++)
                Assert.AreEqual(0f, trains[i].Amplitude,
                    $"train {i}: at sea state 0 the eased amplitude must snap to EXACTLY 0 (glass), " +
                    "not ease asymptotically forever");

            WaveSample sample = animator.Sample(SamplePos);
            Assert.AreEqual(0f, sample.Height, "glass is dead flat");
            Assert.AreEqual(0f, sample.CrestFactor, "no crests on glass");
        }

        // ---- (3) dispersion is canon, on the EASED parameters too --------------------------------

        [Test]
        public void EasedTrains_MidTransition_PhaseSpeedDerivesFromEasedWavelength()
        {
            var field = Field;
            var anim = Anim;
            var animator = new WaveFieldAnimator();

            for (int i = 0; i < 200; i++)
                animator.Tick(Dt60, WindA, 0.35f, in field, in anim);
            for (int i = 0; i < 20; i++)                                // mid-ease: λ between A and B
                animator.Tick(Dt60, WindB, 0.8f, in field, in anim);

            WaveTrains trains = animator.Current;
            Assert.Greater(trains.Count, 0, "sanity: trains live");
            for (int i = 0; i < trains.Count; i++)
            {
                WaveTrain train = trains[i];
                float expected = Mathf.Sqrt(field.Gravity * train.Wavelength / (2f * Mathf.PI));
                Assert.AreEqual(expected, train.PhaseSpeed, 1e-4f,
                    $"train {i}: c = √(g·λ/2π) must hold on the EASED wavelength — dispersion is " +
                    "canon, speed is never free (ADR 0018 owner ruling)");
            }
        }

        // ---- (4) fps independence ----------------------------------------------------------------

        [Test]
        public void SmoothHelper_TwoHalfSteps_EqualOneFullStep()
        {
            const float target = 3.7f;
            const float tau = 0.2f;
            const float dt = 1f / 30f;

            float oneStep = WaveFieldAnimator.Smooth(1.2f, target, dt, tau);
            float twoHalf = WaveFieldAnimator.Smooth(1.2f, target, dt * 0.5f, tau);
            twoHalf = WaveFieldAnimator.Smooth(twoHalf, target, dt * 0.5f, tau);

            Assert.AreEqual(oneStep, twoHalf, 1e-5f,
                "exponential smoothing must compose: two half-steps == one full step toward a " +
                "constant target — that IS fps independence");
        }

        [Test]
        public void ConstantWeather_SameSpanAtDifferentTickRates_SameSurface()
        {
            var field = Field;
            var anim = Anim;

            var at30 = new WaveFieldAnimator();
            var at60 = new WaveFieldAnimator();
            for (int i = 0; i < 90; i++)  at30.Tick(1f / 30f, WindA, 0.6f, in field, in anim);   // 3 s
            for (int i = 0; i < 180; i++) at60.Tick(1f / 60f, WindA, 0.6f, in field, in anim);   // 3 s

            Assert.AreEqual(Height(at30), Height(at60), 1e-3f,
                "the same span of constant weather must land on the same surface whether ticked at " +
                "30 or 60 fps — the phase integral and the ease are both fps-independent");
        }

        // ---- housekeeping: deterministic given the same tick sequence ----------------------------

        [Test]
        public void SameTickSequence_SameResult()
        {
            var field = Field;
            var anim = Anim;
            var a = new WaveFieldAnimator();
            var b = new WaveFieldAnimator();

            for (int i = 0; i < 300; i++)
            {
                // A deterministic, drifting weather script — no RNG in tests either.
                Vector2 wind = new Vector2(4f + Mathf.Sin(i * 0.01f) * 3f, Mathf.Cos(i * 0.013f) * 2f);
                float sea = 0.3f + 0.2f * Mathf.Sin(i * 0.007f);
                a.Tick(Dt60, wind, sea, in field, in anim);
                b.Tick(Dt60, wind, sea, in field, in anim);
            }

            Assert.AreEqual(Height(a), Height(b), 0f,
                "identical tick sequences must produce identical fields — stateful, but " +
                "deterministic (no hidden randomness, rule 5)");
        }

        [Test]
        public void BeforeFirstTick_CurrentIsGlass()
        {
            var animator = new WaveFieldAnimator();
            Assert.AreEqual(0, animator.Current.Count, "no trains before the first tick");
            Assert.AreEqual(0f, animator.Sample(SamplePos).Height, "flat before the first tick");
        }
    }
}
