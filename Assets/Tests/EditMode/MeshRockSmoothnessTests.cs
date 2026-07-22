using System;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>ADR 0022 phase 5, job 1: the mesh hull's rock is SMOOTH — measured, not eyeballed.</b>
    /// The owner's playtest of the lobster mesh: <i>"the rocking was a little stuttery."</i>
    ///
    /// <para>This drives the REAL components (<see cref="BoatWaveMotion"/> → the presenter seam →
    /// <see cref="MeshHullDriver"/>) against a scripted clock and a scripted sea, records the roll
    /// the renderer was actually handed on every one of 600 frames, and measures three properties of
    /// that series:</para>
    /// <list type="bullet">
    ///   <item><b>Phase reversals</b> — a travelling swell's phase advances monotonically. A sign
    ///   flip means the hull rocked BACKWARDS for one frame. The single most legible stutter signal.</item>
    ///   <item><b>Hitch factor</b> = max|Δroll| / mean|Δroll|. A pure sinusoid sampled at 60 fps
    ///   measures exactly π/2 ≈ 1.571 (mean |cos| over a period is 2/π of its peak).</item>
    ///   <item><b>Acceleration ratio</b> = max|Δ²roll| / mean|Δ²roll|. The SECOND derivative is what
    ///   the eye reads as a pop — the same quantity ADR 0022 judged the whole spike on.</item>
    /// </list>
    ///
    /// <para><b>Measured, 2026-07-22</b> (10 s sail, seaState 0.40, wind (6,3), lobster amplitudes —
    /// identical at every sea state, because the defect is scale-invariant):</para>
    /// <code>
    ///                                  reversals   hitch   accel ratio
    ///   ideal (clean sinusoid)              0.0%    1.571        1.571
    ///   SHIPPED (dominant train, forward)   0.0%    1.61          1.54
    ///   OLD (phase reconstructed)           1.7%    5.32         21.80   ← 13.9× the acceleration
    ///   OLD + 50 Hz transform staircase    15.5%    6.18         13.12
    /// </code>
    /// <para>The old path's phase also advanced 6.4× faster at some moments than others
    /// (max Δ 18.72° against a mean of 2.92°) and did not complete cycles at all — over frames
    /// 200–260 it wandered 222° → 283° → 227° instead of sweeping. That is the stutter.</para>
    ///
    /// <para><b>The sabotage is the old code path</b> (<see cref="Sabotage_TheOldReconstructedPhase_FailsTheseBounds"/>):
    /// run through this same harness it must BREACH these bounds, or the bounds are decoration.</para>
    ///
    /// <para>Headless-safe: no camera, no render, no GPU — pure component behaviour over scripted
    /// frames. Deterministic (rule 5): the clock is scripted, so the animator sees the exact same
    /// tick sequence on every machine.</para>
    /// </summary>
    public class MeshRockSmoothnessTests
    {
        const float Dt = 1f / 60f;
        const int Frames = 600;                       // 10 s of sail

        /// <summary>A clean 60 fps sinusoid measures π/2; 2.0 leaves real headroom over that without
        /// admitting anything a player would see. The old path measured 5.32.</summary>
        const float MaxHitchFactor = 2.0f;
        /// <summary>Same reasoning on the second derivative (ideal π/2). The old path measured 21.80,
        /// an order of magnitude clear of this line.</summary>
        const float MaxAccelRatio = 3.0f;

        // ------------------------------------------------------------------ scripted world

        sealed class ScriptedClock : IGameClock
        {
            public double TotalSeconds { get; private set; }
            public void Advance(double dt) => TotalSeconds += dt;
            public GameTime Now => new GameTime(TotalSeconds);
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public int DayIndex => 0;
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float DayFraction => 0f;
            public float HourOfDay => 12f;
            public void SeekTo(double totalSeconds) => TotalSeconds = totalSeconds;
        }

        sealed class ScriptedSea : IEnvironmentService
        {
            public Vector2 Wind = new Vector2(6f, 3f);
            public float SeaState01 = 0.40f;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                Wind, Vector2.zero, tideHeight: 0f, HiddenHarbours.Core.SeaState.Moderate,
                visibility: 1f, seaState01: SeaState01);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        /// <summary>Captures what the driver pushed, frame by frame. Stands in for the Art-side
        /// facet renderer, which needs a GPU this fixture deliberately does not use.</summary>
        sealed class RecordingRenderer : IHullMeshRenderer
        {
            public float HeadingDirUnits { get; set; }
            public float RollDegrees { get; set; }
            public float PitchDegrees { get; set; }
            public float HeavePixels { get; set; }
            public bool IsConfigured => true;
            public void SetSorting(int sortingLayerId, int sortingOrder) { }
        }

        // ------------------------------------------------------------------ the measurement

        struct Smoothness
        {
            public float ReversalPercent, HitchFactor, AccelRatio;
            public float MeanAbsDPhase, MaxAbsDPhase;
            public override string ToString() =>
                $"reversals {ReversalPercent:0.0}%, hitch {HitchFactor:0.00}, accel ratio {AccelRatio:0.00}, " +
                $"dPhase mean {MeanAbsDPhase:0.000}° max {MaxAbsDPhase:0.000}°";
        }

        /// <summary>The roll series' two derivative statistics. Reversals are NOT counted here —
        /// roll is a sine of the phase and legitimately turns twice a cycle; it is the PHASE that
        /// must be monotone, which <see cref="MeasurePhase"/> fills in.</summary>
        static Smoothness Measure(float[] roll)
        {
            double sumD = 0, sumD2 = 0;
            float maxD = 0, maxD2 = 0;
            int n = 0, n2 = 0;

            for (int f = 1; f < roll.Length; f++)
            {
                float d = roll[f] - roll[f - 1];
                sumD += Mathf.Abs(d);
                maxD = Mathf.Max(maxD, Mathf.Abs(d));
                n++;
                if (f >= 2)
                {
                    float d2 = roll[f] - 2f * roll[f - 1] + roll[f - 2];
                    sumD2 += Mathf.Abs(d2);
                    maxD2 = Mathf.Max(maxD2, Mathf.Abs(d2));
                    n2++;
                }
            }

            return new Smoothness
            {
                HitchFactor = sumD > 1e-9 ? maxD / (float)(sumD / n) : 0f,
                AccelRatio = sumD2 > 1e-9 ? maxD2 / (float)(sumD2 / n2) : 0f,
            };
        }

        static Smoothness MeasurePhase(float[] phase, Smoothness rollPart)
        {
            double sumD = 0;
            float maxD = 0, prevD = 0;
            int reversals = 0, n = 0;
            for (int f = 1; f < phase.Length; f++)
            {
                float d = Mathf.DeltaAngle(phase[f - 1], phase[f]);   // wrap-safe
                sumD += Mathf.Abs(d);
                maxD = Mathf.Max(maxD, Mathf.Abs(d));
                if (n > 0 && Mathf.Abs(d) > 1e-4f && Mathf.Sign(d) != Mathf.Sign(prevD)) reversals++;
                prevD = d;
                n++;
            }
            rollPart.MeanAbsDPhase = (float)(sumD / Mathf.Max(1, n));
            rollPart.MaxAbsDPhase = maxD;
            rollPart.ReversalPercent = reversals / (float)Mathf.Max(1, n) * 100f;
            return rollPart;
        }

        // ------------------------------------------------------------------ the rig under test

        /// <summary>Build the production chain — a boat root carrying the real
        /// <see cref="MeshHullDriver"/> and <see cref="BoatWaveMotion"/>, wired through the real
        /// <see cref="MeshHullPresenter"/> seam — and sail it for <see cref="Frames"/> frames against
        /// a scripted clock. Returns the roll the renderer was handed each frame, and the phase it
        /// was posed at.</summary>
        static (float[] roll, float[] phase) Sail(GameObject root, float seaState01)
        {
            var clock = new ScriptedClock();
            GameServices.Clock = clock;
            GameServices.Environment = new ScriptedSea { SeaState01 = seaState01 };

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            var renderer = new RecordingRenderer();
            var driver = root.AddComponent<MeshHullDriver>();
            var def = ScriptableObject.CreateInstance<HullMeshDef>();
            def.ElevationDeg = 40f;
            def.AzimuthCounterClockwise = true;
            def.RockRollDegrees = 2.8f;      // the committed lobster's measured amplitudes
            def.RockPitchDegrees = 1.6f;
            def.RockHeavePixels = 1.2f;
            driver.Configure(visual.transform, renderer, def, zeroHeadingDegrees: 0f);

            var wave = root.AddComponent<BoatWaveMotion>();
            wave.Configure(visual.transform, new MeshHullPresenter(driver));

            // Warm up before recording. The FIRST tick has no previous clock reading, so it falls
            // back to Time.deltaTime (meaningless in EditMode), and the animator snaps to its targets
            // rather than easing. That is correct behaviour on a freshly-woken component and a
            // legitimate one-frame artefact — but it is not what "is the rock smooth while sailing?"
            // is asking, and a single artificial step would dominate a max/mean ratio.
            for (int w = 0; w < 5; w++) { clock.Advance(Dt); wave.Tick(); driver.Drive(); }

            var roll = new float[Frames];
            var phase = new float[Frames];

            for (int f = 0; f < Frames; f++)
            {
                clock.Advance(Dt);
                wave.Tick();       // the real LateUpdate body
                driver.Drive();    // the real driver push, in its real order (wave −120 → driver −110)
                roll[f] = renderer.RollDegrees;
                // The pose's own phase, recovered exactly: the rig's rockMotion makes roll and pitch
                // a sine/cosine PAIR (roll = R·sin θ, pitch = P·cos θ), so atan2 of the two
                // normalised channels IS θ — no reconstruction assumption, just inverting a pair.
                phase[f] = Mathf.Atan2(renderer.RollDegrees / def.RockRollDegrees,
                                       renderer.PitchDegrees / def.RockPitchDegrees) * Mathf.Rad2Deg;
            }

            Object.DestroyImmediate(def);
            return (roll, phase);
        }

        [TearDown]
        public void TearDown() => GameServices.Reset();

        // ------------------------------------------------------------------ the acceptance

        [Test]
        public void MeshRock_AdvancesSmoothly_OverATenSecondSail()
        {
            var root = new GameObject("Boat");
            try
            {
                var (roll, phase) = Sail(root, seaState01: 0.40f);
                var m = MeasurePhase(phase, Measure(roll));
                Debug.Log($"[mesh-rock] SHIPPED continuous path: {m}");

                Assert.AreEqual(0f, m.ReversalPercent, 1e-3f,
                    $"the rock phase REVERSED on {m.ReversalPercent:0.0}% of frames — the hull rocked " +
                    $"backwards. A travelling swell's phase is monotone. ({m})");
                Assert.LessOrEqual(m.HitchFactor, MaxHitchFactor,
                    $"the applied roll hitches: one frame moved {m.HitchFactor:0.00}× the average step " +
                    $"(a clean sinusoid measures 1.571). ({m})");
                Assert.LessOrEqual(m.AccelRatio, MaxAccelRatio,
                    $"the applied roll's ACCELERATION spikes {m.AccelRatio:0.00}× its mean (clean " +
                    $"sinusoid: 1.571). Acceleration is what reads as a pop — ADR 0022's own metric. ({m})");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MeshRock_IsSmoothAtEverySeaState_IncludingNearCalm()
        {
            var report = new StringBuilder();
            foreach (float sea in new[] { 0.10f, 0.35f, 0.70f, 1.00f })
            {
                var root = new GameObject("Boat");
                try
                {
                    var (roll, phase) = Sail(root, seaState01: sea);
                    var m = MeasurePhase(phase, Measure(roll));
                    report.AppendLine($"  seaState {sea:0.00}: {m}");
                    Assert.AreEqual(0f, m.ReversalPercent, 1e-3f, $"seaState {sea}: {m}");
                    Assert.LessOrEqual(m.HitchFactor, MaxHitchFactor, $"seaState {sea}: {m}");
                    Assert.LessOrEqual(m.AccelRatio, MaxAccelRatio, $"seaState {sea}: {m}");
                }
                finally { Object.DestroyImmediate(root); GameServices.Reset(); }
            }
            Debug.Log($"[mesh-rock] across sea states:\n{report}");
        }

        // ------------------------------------------------------------------ SABOTAGE

        /// <summary>
        /// ⚠️ <b>The sabotage is the code this PR deleted.</b> Reconstructing the phase from the
        /// sampled surface — <c>atan2(height, (slope·d)/k)</c>, exact only for a single pure-sine
        /// train, fed a four-train crest-sharpened field — must BREACH the bounds above when run
        /// through this same harness. If it passes, the bounds cannot see the defect the owner saw
        /// and every green run above is worth less than it looks.
        /// </summary>
        [Test]
        public void Sabotage_TheOldReconstructedPhase_FailsTheseBounds()
        {
            var animator = new WaveFieldAnimator();
            var settings = WaveFieldSettings.Default;
            var animSettings = WaveFieldAnimatorSettings.Default;
            const float rollA = 2.8f, pitchA = 1.6f;

            var roll = new float[Frames];
            var phase = new float[Frames];
            for (int f = 0; f < Frames; f++)
            {
                animator.Tick(Dt, new Vector2(6f, 3f), 0.40f, in settings, in animSettings);
                WaveSample wave = animator.Sample(Vector2.zero);
                WaveTrain dominant = animator.Current[0];
                float k = (2f * Mathf.PI) / Mathf.Max(WaveTrain.MinWavelengthMeters, dominant.Wavelength);

                // THE OLD LINE, verbatim.
                float p = DoryRockMath.PhaseDegrees(wave.Height, wave.Slope, dominant.Direction, k);

                HullMeshMath.RockPose(p, rollA, pitchA, 1.2f, out float r, out float pi, out _);
                roll[f] = r;
                phase[f] = Mathf.Atan2(r / rollA, pi / pitchA) * Mathf.Rad2Deg;
            }

            var m = MeasurePhase(phase, Measure(roll));
            Debug.Log($"[mesh-rock][SABOTAGE] the OLD reconstructed phase: {m}");

            Assert.IsTrue(m.ReversalPercent > 1e-3f || m.HitchFactor > MaxHitchFactor
                                                    || m.AccelRatio > MaxAccelRatio,
                "SABOTAGE NOT DETECTED — the old surface-reconstructed phase passed the smoothness " +
                $"bounds. The measurement cannot see the stutter the owner reported. ({m})");
        }

        // ------------------------------------------------------------------ the primitive

        /// <summary>
        /// The new forward phase must be the SAME NUMBER <see cref="WaveMath.Sample"/> uses inside —
        /// the two would otherwise drift the day someone edits one of them. Proved against the
        /// observable: the sharpened profile's height peaks at θ = 90° and troughs at θ = 270°, so
        /// sampling at positions the forward phase says are crest and trough must return the
        /// envelope's extremes.
        /// </summary>
        [Test]
        public void TrainPhaseDegrees_AgreesWithSamplesOwnProfile()
        {
            WaveTrains field = WaveMath.TrainsFrom(new Vector2(6f, 3f), 0.6f, WaveFieldSettings.Default);
            WaveTrain train = field[0];
            // A single-train field, so Sample's height IS this train's profile with nothing added.
            var solo = new WaveTrains(train, default, default, default, 1, field.CrestSharpening);

            // Walk along the train's own direction and confirm the height extremes land exactly where
            // the phase says: max at 90°, min at 270°.
            float bestHeight = float.NegativeInfinity, worstHeight = float.PositiveInfinity;
            float phaseAtBest = 0f, phaseAtWorst = 0f;
            for (int i = 0; i < 2000; i++)
            {
                Vector2 pos = train.Direction * (i * train.Wavelength / 500f);
                float h = WaveMath.Sample(pos, 0.0, in solo).Height;
                float p = WaveMath.TrainPhaseDegrees(train, pos, 0.0);
                if (h > bestHeight) { bestHeight = h; phaseAtBest = p; }
                if (h < worstHeight) { worstHeight = h; phaseAtWorst = p; }
            }

            Assert.AreEqual(0f, Mathf.DeltaAngle(90f, phaseAtBest), 1.0f,
                $"the crest must sit at phase 90° (found {phaseAtBest:0.0}°) — if this drifts, every " +
                "rock pose is off by the difference and the mesh and sprite hulls stop agreeing.");
            // The trough gets a LOOSER band on purpose, and it is not slack. Crest sharpening pinches
            // narrow crests over BROAD troughs: with p = 2.2, height + A ∝ (Δθ)^2p near θ = 270°, so
            // the bottom is flat to fourth order and its numerical minimum is not sharply located —
            // 2000 samples land within a couple of degrees and float cannot do better. The crest,
            // which is the sharp feature and the one the rock convention is actually defined by, is
            // pinned to 1° above. The trough's HEIGHT is exact regardless, so that is asserted too.
            Assert.AreEqual(0f, Mathf.DeltaAngle(270f, phaseAtWorst), 5.0f,
                $"the trough must sit at phase 270° (found {phaseAtWorst:0.0}°)");
            Assert.AreEqual(train.Amplitude, bestHeight, 1e-3f, "crest height = +the train's amplitude");
            Assert.AreEqual(-train.Amplitude, worstHeight, 1e-3f, "trough height = −the train's amplitude");
        }

        [Test]
        public void TrainPhaseDegrees_IsAlwaysInRange_AndDefinedForASilentTrain()
        {
            var silent = new WaveTrain(Vector2.right, 12f, 0f, 1.2f, 9.81f);
            for (int i = -50; i < 50; i++)
            {
                float p = WaveMath.TrainPhaseDegrees(silent, new Vector2(i * 3.7f, i * -1.1f), i * 0.9);
                Assert.IsFalse(float.IsNaN(p), "a silent train still has a defined phase");
                Assert.GreaterOrEqual(p, 0f);
                Assert.Less(p, 360f);
            }
        }
    }
}
