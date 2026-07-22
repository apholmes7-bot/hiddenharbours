using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The SPRITE hull's rock frames CYCLE with the swell — measured, not eyeballed.</b> The
    /// sprite-path sibling of <see cref="MeshRockSmoothnessTests"/> (ADR 0022 phase 5): #243 moved
    /// the MESH hull off the surface-reconstructed phase (an <c>atan2</c> exact only for a single
    /// pure-sine train, fed the shipped four-train crest-sharpened field) onto the animator's
    /// forward-read dominant phase. The sprite path stayed on the reconstruction — and pushed
    /// through the 8-frame quantiser that phase does not sweep, it wanders: the dory dwells on one
    /// or two of her rock frames and even steps BACKWARDS instead of cycling crest → trough → crest.
    /// The owner ruled 2026-07-22: feed the sprite frame selection the same forward phase.
    ///
    /// <para>This drives the REAL chain — <see cref="BoatWaveMotion"/> →
    /// <see cref="SpriteHullPresenter"/> → <see cref="DirectionalBoatSprite"/> — against a scripted
    /// clock and sea, records the rock frame the sprite was actually handed on every one of 600
    /// frames, and asserts the frame index advances monotonically-with-wrap (every change is +1 mod
    /// frameCount, never backwards, never a skip), visits every frame, completes cycles at the
    /// dominant train's own rate, and never dwells longer than the swell period allows. The frame
    /// QUANTISATION itself (count, calibration, hysteresis via
    /// <see cref="DoryRockMath.AdvanceFrame"/>) is deliberately untouched by the fix — only WHICH
    /// phase feeds it — so those knobs are exercised at their production defaults.</para>
    ///
    /// <para><b>The sabotage is the old code path</b>
    /// (<see cref="Sabotage_TheOldReconstructedPhase_FailsTheseBounds"/>): the deleted lines, run
    /// verbatim through this same harness and quantiser, must BREACH these bounds — otherwise the
    /// bounds cannot see the defect the owner saw.</para>
    ///
    /// <para>Headless-safe (null sprites — frame selection never reads pixels; no camera, no GPU)
    /// and deterministic (rule 5): a scripted clock, a constant scripted sea, no RNG.</para>
    /// </summary>
    public class SpriteRockFrameCycleTests
    {
        const float Dt = 1f / 60f;
        const int Frames = 600;                       // 10 s of sail
        const int FrameCount = 8;                     // the DoryIsoRock sheet, the production default
        const float Hysteresis = 8f;                  // BoatWaveMotion's default, replicated in the sabotage

        static readonly Vector2 Wind = new Vector2(6f, 3f);
        const float SeaState = 0.40f;                 // the sea the mesh stutter was measured on

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
            public Vector2 Wind = SpriteRockFrameCycleTests.Wind;
            public float SeaState01 = SeaState;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                Wind, Vector2.zero, tideHeight: 0f, HiddenHarbours.Core.SeaState.Moderate,
                visibility: 1f, seaState01: SeaState01);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        // ------------------------------------------------------------------ the rig under test

        /// <summary>Build the production sprite chain — a boat root with the real
        /// <see cref="DirectionalBoatSprite"/> carrying a full 8×8 rock grid, wrapped by the real
        /// <see cref="SpriteHullPresenter"/>, driven by the real <see cref="BoatWaveMotion"/> — and
        /// sail it against a scripted clock, recording the rock frame drawn each tick. Null sprites:
        /// <see cref="DirectionalBoatSprite.HasRockGrid"/> and frame selection are pure array-length
        /// and index logic, never pixels (same precedent as BoatHullPresenterSeamTests).</summary>
        static int[] Sail(GameObject root, float seaState01)
        {
            var clock = new ScriptedClock();
            GameServices.Clock = clock;
            GameServices.Environment = new ScriptedSea { SeaState01 = seaState01 };

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();

            var directional = root.AddComponent<DirectionalBoatSprite>();
            directional.Configure(new Sprite[FrameCount], sr);
            directional.ConfigureRock(new Sprite[FrameCount * FrameCount], FrameCount);
            Assert.IsTrue(directional.HasRockGrid, "harness: the rock grid must gate ON");

            var wave = root.AddComponent<BoatWaveMotion>();
            wave.Configure(visual.transform, new SpriteHullPresenter(directional));

            // Warm up (same reasoning as MeshRockSmoothnessTests): the first tick has no previous
            // clock reading and the animator snaps to its targets — correct on wake, but not what
            // "does the rock cycle while sailing?" asks.
            for (int w = 0; w < 5; w++) { clock.Advance(Dt); wave.Tick(); }

            var frames = new int[Frames];
            for (int f = 0; f < Frames; f++)
            {
                clock.Advance(Dt);
                wave.Tick();       // the real LateUpdate body
                frames[f] = directional.RockFrame;
            }
            return frames;
        }

        /// <summary>The dominant train the animator settles on: weather is CONSTANT here and the
        /// animator's first tick snaps straight to its <see cref="WaveMath.TrainsFrom"/> targets, so
        /// the eased train equals the target train for the whole sail — its period is exact, not
        /// approximate.</summary>
        static float DominantPeriodSeconds(float seaState01)
        {
            WaveTrain dominant = WaveMath.TrainsFrom(Wind, seaState01, WaveFieldSettings.Default)[0];
            return dominant.Wavelength / dominant.PhaseSpeed;
        }

        // ------------------------------------------------------------------ the measurement

        struct Cycling
        {
            public int ForwardSteps, BadSteps, DistinctFrames, MaxDwellTicks, LevelTicks;
            public override string ToString() =>
                $"forward steps {ForwardSteps}, bad steps {BadSteps}, distinct frames {DistinctFrames}/{FrameCount}, " +
                $"max dwell {MaxDwellTicks} ticks, level(-1) {LevelTicks} ticks";
        }

        /// <summary>Walk the recorded frame series: a healthy rock only ever holds (delta 0) or
        /// advances one frame with wrap (delta +1 mod count); anything else — a backward step or a
        /// skip — is a "bad step". Also counts full forward steps (the cycling rate), the distinct
        /// frames visited, the longest dwell on one frame, and any −1 (level/calm) ticks.</summary>
        static Cycling Measure(int[] frames)
        {
            var m = new Cycling { DistinctFrames = 0, MaxDwellTicks = 1 };
            var seen = new bool[FrameCount];
            int dwell = 1;
            for (int f = 0; f < frames.Length; f++)
            {
                if (frames[f] < 0) { m.LevelTicks++; continue; }
                seen[frames[f] % FrameCount] = true;
                if (f == 0 || frames[f - 1] < 0) continue;

                int delta = ((frames[f] - frames[f - 1]) % FrameCount + FrameCount) % FrameCount;
                if (delta == 0)
                {
                    dwell++;
                    m.MaxDwellTicks = Mathf.Max(m.MaxDwellTicks, dwell);
                }
                else
                {
                    dwell = 1;
                    if (delta == 1) m.ForwardSteps++;
                    else m.BadSteps++;
                }
            }
            for (int i = 0; i < FrameCount; i++) if (seen[i]) m.DistinctFrames++;
            return m;
        }

        [TearDown]
        public void TearDown() => GameServices.Reset();

        // ------------------------------------------------------------------ the acceptance

        [Test]
        public void SpriteRockFrames_CycleMonotonicallyWithTheSwell_OverATenSecondSail()
        {
            var root = new GameObject("Boat");
            try
            {
                Cycling m = Measure(Sail(root, SeaState));

                float period = DominantPeriodSeconds(SeaState);
                // The forward phase advances at exactly 360°/period, so the quantiser must take
                // frameCount steps per period. Hysteresis delays each individual step but cannot
                // change the RATE (it is a fixed offset, not a drag), so the count over 10 s is
                // exact to within the one partial cycle at each end.
                int expectedSteps = Mathf.FloorToInt(Frames * Dt / period * FrameCount);
                Debug.Log($"[sprite-rock] SHIPPED forward path: {m} (dominant period {period:0.00}s, expected ~{expectedSteps} steps)");

                Assert.AreEqual(0, m.LevelTicks,
                    $"seaState {SeaState} is well above the calm threshold — the hull must rock, not sit level. ({m})");
                Assert.AreEqual(0, m.BadSteps,
                    $"the rock frame stepped BACKWARDS or SKIPPED — a travelling swell's phase is " +
                    $"monotone, so the frame index must only hold or advance by one (with wrap). ({m})");
                Assert.AreEqual(FrameCount, m.DistinctFrames,
                    $"the rock must visit ALL {FrameCount} frames — dwelling on a subset is exactly " +
                    $"the defect the owner saw. ({m})");
                Assert.GreaterOrEqual(m.ForwardSteps, expectedSteps - 2,
                    $"the rock cycles SLOWER than the swell — it is dwelling. ({m})");
                Assert.LessOrEqual(m.ForwardSteps, expectedSteps + 2,
                    $"the rock cycles FASTER than the swell — the phase is not the dominant train's. ({m})");
                // Steady-state dwell on one frame is period/frameCount; double it for the hysteresis
                // lag on either edge of a bucket. The old path dwelt for whole seconds.
                int maxDwell = Mathf.CeilToInt(period / FrameCount / Dt) * 2;
                Assert.LessOrEqual(m.MaxDwellTicks, maxDwell,
                    $"the rock sat on one frame for {m.MaxDwellTicks} ticks (bound {maxDwell}) — the dwell the owner reported. ({m})");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SpriteRockFrames_HoldTheLevelHull_OnGlass()
        {
            var root = new GameObject("Boat");
            try
            {
                Cycling m = Measure(Sail(root, seaState01: 0f));
                Assert.AreEqual(Frames, m.LevelTicks,
                    $"glass is sacred: at sea state 0 the dory holds her static level hull (frame −1) " +
                    $"on every tick — no phantom rocking. ({m})");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ------------------------------------------------------------------ SABOTAGE

        /// <summary>
        /// ⚠️ <b>The sabotage is the code this PR deleted</b> — the sprite branch of
        /// <c>BoatWaveMotion.DriveRockFrame</c> as it stood: phase RECONSTRUCTED from the sampled
        /// surface (<see cref="DoryRockMath.PhaseDegrees"/>, exact only for a single pure-sine
        /// train), then quantised by the very same <see cref="DoryRockMath.AdvanceFrame"/> at the
        /// very same production defaults. Run through this harness's bounds it must FAIL them; if it
        /// passes, the bounds cannot see the dwell/reversal the owner saw and every green run above
        /// is decoration.
        /// </summary>
        [Test]
        public void Sabotage_TheOldReconstructedPhase_FailsTheseBounds()
        {
            var animator = new WaveFieldAnimator();
            var settings = WaveFieldSettings.Default;
            var animSettings = WaveFieldAnimatorSettings.Default;

            int current = -1;
            var frames = new int[Frames];
            for (int f = -5; f < Frames; f++)          // same 5-tick warm-up as the rig
            {
                animator.Tick(Dt, Wind, SeaState, in settings, in animSettings);
                WaveSample wave = animator.Sample(Vector2.zero);
                WaveTrain dominant = animator.Current[0];
                float k = (2f * Mathf.PI) / Mathf.Max(WaveTrain.MinWavelengthMeters, dominant.Wavelength);

                // THE OLD LINES, verbatim.
                float phaseDeg = DoryRockMath.PhaseDegrees(wave.Height, wave.Slope, dominant.Direction, k);
                current = DoryRockMath.AdvanceFrame(current, phaseDeg, FrameCount,
                                                    calibrationDegrees: 0f, hysteresisDegrees: Hysteresis);
                if (f >= 0) frames[f] = current;
            }

            Cycling m = Measure(frames);
            float period = DominantPeriodSeconds(SeaState);
            int expectedSteps = Mathf.FloorToInt(Frames * Dt / period * FrameCount);
            int maxDwell = Mathf.CeilToInt(period / FrameCount / Dt) * 2;
            Debug.Log($"[sprite-rock][SABOTAGE] the OLD reconstructed phase: {m} (expected ~{expectedSteps} steps, dwell bound {maxDwell})");

            Assert.IsTrue(m.BadSteps > 0
                          || m.DistinctFrames < FrameCount
                          || m.ForwardSteps < expectedSteps - 2 || m.ForwardSteps > expectedSteps + 2
                          || m.MaxDwellTicks > maxDwell,
                "SABOTAGE NOT DETECTED — the old surface-reconstructed phase passed the frame-cycling " +
                $"bounds. The measurement cannot see the dwell/reversal the owner reported. ({m})");
        }
    }
}
