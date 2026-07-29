using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The WAVE-FIELD bridge (ADR 0018, Arc B1): publishes the ONE shared deterministic wave field to
    /// the water shader as packed SHADER GLOBALS, so the whitecaps and the swell brightness ride the
    /// SAME advancing crests the hull rocks on (<c>BoatWaveMotion</c>, B2). One wave truth, two
    /// consumers, by construction.
    ///
    /// <para><b>Smooth by construction (ADR 0018 addendum).</b> The bridge does not publish raw
    /// throttled <see cref="WaveMath.TrainsFrom"/> snapshots — that design JUMPS the phase whenever the
    /// drifting wind moves the dominant wavelength under a large running t (the owner's "jittery"
    /// boat-rocking bug, which would have hit the water identically). Instead it owns a
    /// <see cref="WaveFieldAnimator"/> — the SAME presentation smoother <c>BoatWaveMotion</c> ticks —
    /// and ticks it every frame with the game-time delta: the animator eases each train's
    /// wavelength/amplitude/direction toward the weather-derived targets, re-derives the dispersion
    /// speed from the EASED wavelength (speed is never free), accumulates phase INCREMENTALLY in double,
    /// and returns trains whose accumulated phase is BAKED INTO <see cref="WaveTrain.PhaseOffset"/> —
    /// to be sampled at <c>timeSeconds = 0</c>. Both consumers tick the same animator code with the
    /// same inputs each frame, so the water and the hull ride the IDENTICAL eased sea.</para>
    ///
    /// <para><b>What it publishes</b> (via <see cref="Shader.SetGlobalVector(int, Vector4)"/>, outside
    /// any per-material CBUFFER, like <c>_SunDir</c>/<c>_WindWorld</c>/<c>_MoonDir</c>):
    /// <list type="bullet">
    /// <item><c>_WaveTrain0..7</c> — per train: xy = unit travel direction, z = wave number
    ///   <c>k = 2π/λ</c> (precomputed here so the shader never divides by a wavelength), w = amplitude
    ///   (metres). Slots at or beyond the live count are zero.</item>
    /// <item><c>_WavePhases</c> (trains 0–3) and <c>_WavePhases2</c> (trains 4–7) — the animator-baked
    ///   phase (radians, wrapped to [0, 2π) in C# DOUBLE before the float cast). The shader evaluates
    ///   <c>θ = k·(dir·worldPos) + φᵢ</c> — <b>NO time uniform at all</b>: the advancing time lives in
    ///   the phase the animator accumulates, so the unbounded game time never touches float trig on
    ///   the GPU, and the shader NEVER re-derives the phase speed (dispersion lives in C# only —
    ///   ADR 0018 §(4)).</item>
    /// <item><c>_WaveFieldParams</c> — x = live train count (0 = nothing published → the shader's
    ///   legacy noise-swell look holds), y = crest sharpening p, z = total amplitude (metres; the
    ///   crest-factor normalizer), <b>w = the dominant (spectral-peak) slot index</b> — formerly
    ///   <c>reserved</c>, published as 0, which is what the flat weighting still publishes.</item>
    /// </list>
    /// The whole payload travels as ONE <see cref="PackedWaveField"/>; see that type for why the
    /// width lives in a single value rather than in every signature that reads the sea.
    /// Per-frame cost: one animator tick + six <c>SetGlobalVector</c> calls, no allocation (rule 7).</para>
    ///
    /// <para><b>Self-installing</b> (mirrors <see cref="GrassWindBridge"/>/<see cref="MoonCycle"/>): a
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns one hidden <c>[DontDestroyOnLoad]</c> host
    /// before the first scene, so every scene's water reads the field with no wiring. The host SURVIVES
    /// scene changes, so it calls <see cref="WaveFieldAnimator.Reset"/> on every scene load — a region
    /// teleport must snap to the live weather, never ease across the travel gap.</para>
    ///
    /// <para><b>Cycle-off / edit-mode convention.</b> When there is no sim (<c>GameServices.Environment</c>
    /// or <c>Clock</c> null — EditMode, pre-boot, a bare art scene), the bridge publishes an EMPTY field
    /// (count 0, all zeros) so the shader's wave path is silent and the legacy tuned look holds — the
    /// same "unset" convention as <c>_DayNightTint</c>/<c>_MoonDir</c>. <see cref="OnDisable"/> also
    /// publishes the empty field so a stopped play session can't leave stale trains on the editor's
    /// globals.</para>
    ///
    /// <para><b>Seam discipline (rule 4) &amp; determinism (rule 5).</b> Reads the sim ONLY through the
    /// Core <see cref="GameServices.Environment"/>/<see cref="GameServices.Clock"/> accessors and never
    /// writes it. Nothing is saved; the animator's presentation-only statefulness is documented on its
    /// class (the sim contract — B3 forces — keeps reading the pure <see cref="WaveMath"/> path).
    /// Visual-only: the sim never touches this bridge.</para>
    ///
    /// <para><b>Tuning (rule 6) — ONE settings instance (ADR 0018 §(5)).</b> The derivation constants
    /// and the smoothing come from <c>GameConfig.WaveField</c> / <c>.WaveFieldAnimator</c> through
    /// <see cref="GameServices"/>, which is the same instance <c>BoatWaveMotion</c>, the seakeeping
    /// forces, the wake, the buoys, the trap haul and the drift weed read. The hull therefore rocks on
    /// the waves the player sees because there is only one set of numbers — not because two copies
    /// were kept in step by hand.</para>
    ///
    /// <para>⚠️ This host is created at runtime with <c>HideAndDontSave</c>, so it has no inspector.
    /// Its old serialized copy was the one the owner could NEVER reach: a tuned config would have
    /// moved the hull, the wake and the buoys while the drawn sea stayed at <c>Default</c>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveFieldBridge : MonoBehaviour
    {
        /// <summary>One id per slot, indexed by slot — so publishing and reading back are both a loop
        /// over <see cref="PackedWaveField.MaxTrains"/> and neither can drift from the other when the
        /// seam widens again.</summary>
        private static readonly int[] IdWaveTrain =
        {
            Shader.PropertyToID("_WaveTrain0"), Shader.PropertyToID("_WaveTrain1"),
            Shader.PropertyToID("_WaveTrain2"), Shader.PropertyToID("_WaveTrain3"),
            Shader.PropertyToID("_WaveTrain4"), Shader.PropertyToID("_WaveTrain5"),
            Shader.PropertyToID("_WaveTrain6"), Shader.PropertyToID("_WaveTrain7"),
        };

        private static readonly int IdWavePhases      = Shader.PropertyToID("_WavePhases");
        private static readonly int IdWavePhases2     = Shader.PropertyToID("_WavePhases2");
        private static readonly int IdWaveFieldParams = Shader.PropertyToID("_WaveFieldParams");

        private const double TwoPi = Math.PI * 2.0;

        // The derivation constants come from GameConfig.WaveField via GameServices (ADR 0018 §(5)).
        // ⚠️ This host is created at runtime with HideAndDontSave, so it has no inspector and nothing
        // could ever have been wired on it — its serialized copy was permanently stuck at Default
        // while a tuned GameConfig sat unread. That is the sharpest edge of the old eight-copies
        // design: the WATER's settings were the one copy the owner could not reach.

        private readonly WaveFieldAnimator _animator = new WaveFieldAnimator();
        private bool _hasLastTime;
        private double _lastTimeSeconds;

        /// <summary>
        /// Spawn the single self-installing host before the first scene. Guarded against double-install
        /// (domain reloads / additive scene loads), like the project's other self-installing services.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("WaveFieldBridge") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<WaveFieldBridge>();
        }

        private static bool _installed;

        private void OnEnable()
        {
            _hasLastTime = false;          // fresh dt baseline — never ease across a disabled gap
            _animator.Reset();             // snap to the live weather on wake, don't chase a stale sea
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            // Leave the globals SILENT (count 0 -> the shader's legacy look) rather than frozen on the
            // last live trains — a stopped play session must not haunt the editor's scene view.
            PublishEmpty();
            _animator.Reset();
            _hasLastTime = false;
        }

        /// <summary>The host is [DontDestroyOnLoad] and outlives every scene: snap the eased sea to the
        /// live weather on each scene load (a region teleport must not ease across the travel gap).</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _animator.Reset();
            _hasLastTime = false;
        }

        private void Update()
        {
            var env = GameServices.Environment;
            var clock = GameServices.Clock;
            if (env == null || clock == null)
            {
                // No sim (EditMode / pre-boot / bare demo): publish the EMPTY field so the wave path is
                // silent and the legacy tuned look holds (the _DayNightTint/_MoonDir "unset" convention).
                PublishEmpty();
                _animator.Reset();
                _hasLastTime = false;
                return;
            }

            // Game-time delta for this presentation tick (the BoatWaveMotion pattern): the clock
            // advances smoothly per frame; a paused clock yields dt 0 and the sea freezes with it.
            double time = clock.TotalSeconds;
            float dt = _hasLastTime ? Mathf.Max(0f, (float)(time - _lastTimeSeconds)) : Time.deltaTime;
            _lastTimeSeconds = time;
            _hasLastTime = true;

            // Tick the shared eased, phase-continuous field (the same animator code the boat ticks with
            // the same inputs — the identical eased sea) and publish it. The accumulated phase rides in
            // each train's PhaseOffset, so the packed field is the t = 0 evaluation frame.
            EnvironmentSample sample = env.Sample();
            WaveFieldSettings field = GameServices.WaveField;
            WaveFieldAnimatorSettings smoothing = GameServices.WaveFieldAnimator;
            WaveTrains trains = _animator.Tick(dt, sample.WindVector, sample.SeaState01,
                                               in field, in smoothing);
            PublishGlobals(Pack(in trains));
        }

        /// <summary>
        /// Push a packed field to the shader globals. Allocation-free (rule 7): 8 + 2 + 1
        /// <c>SetGlobalVector</c> calls on the throttled presentation tick.
        ///
        /// <para><b>Public because it is the ONLY place the uniform names live.</b> The rendering
        /// acceptance suites and the shore-seam proof harness each publish a field of their own to
        /// drive the real shader, and each used to spell the six <c>SetGlobalVector</c> calls out by
        /// hand. Three hand-written copies of a seam is three places to forget when the seam widens
        /// — and forgetting is silent: the extra slots simply keep whatever the last publisher left
        /// in them, so a test would go on passing against a field that is not the one that ships.</para>
        /// </summary>
        public static void PublishGlobals(in PackedWaveField field)
        {
            for (int i = 0; i < PackedWaveField.MaxTrains; i++)
                Shader.SetGlobalVector(IdWaveTrain[i], field.Train(i));
            Shader.SetGlobalVector(IdWavePhases, field.PhasesLow);
            Shader.SetGlobalVector(IdWavePhases2, field.PhasesHigh);
            Shader.SetGlobalVector(IdWaveFieldParams, field.Params);
        }

        private void PublishEmpty() => PublishGlobals(PackedWaveField.Empty);

        // ==== PURE packing (testable headless; no Unity scene needed) =====================================

        /// <summary>
        /// Pack a <see cref="WaveTrains"/> field into the six global vectors the shader twin reads —
        /// THE packing contract (see the class doc for the layout). Pure and deterministic (rule 5):
        /// same trains → the same vectors, forever. Slots at or beyond the live count pack as zero
        /// (their contents are undefined and must not be read). The trains are expected in the
        /// animator's output frame — accumulated phase baked into <see cref="WaveTrain.PhaseOffset"/>,
        /// sampled at t = 0 — so the published phase IS the (wrapped) phase offset; raw
        /// <see cref="WaveMath.TrainsFrom"/> trains pack correctly too (their offsets are the t = 0
        /// hash phases).
        /// </summary>
        public static PackedWaveField Pack(in WaveTrains trains)
        {
            int count = trains.Count;
            return new PackedWaveField(
                PackTrain(in trains, 0, count), PackTrain(in trains, 1, count),
                PackTrain(in trains, 2, count), PackTrain(in trains, 3, count),
                PackTrain(in trains, 4, count), PackTrain(in trains, 5, count),
                PackTrain(in trains, 6, count), PackTrain(in trains, 7, count),
                new Vector4(PhaseAt(in trains, 0, count), PhaseAt(in trains, 1, count),
                            PhaseAt(in trains, 2, count), PhaseAt(in trains, 3, count)),
                new Vector4(PhaseAt(in trains, 4, count), PhaseAt(in trains, 5, count),
                            PhaseAt(in trains, 6, count), PhaseAt(in trains, 7, count)),
                // .w carries the spectral-peak slot. It is 0 under the flat weighting — the same
                // bytes the old `reserved` 0f published, which is the P2 PR-A passthrough contract.
                new Vector4(count, trains.CrestSharpening, trains.TotalAmplitude,
                            trains.DominantIndex));
        }

        /// <summary>xy = direction, z = wave number k = 2π/λ, w = amplitude; zero for a dead slot.</summary>
        private static Vector4 PackTrain(in WaveTrains trains, int index, int count)
        {
            if (index >= count) return Vector4.zero;
            WaveTrain train = trains[index];
            float wavelength = Mathf.Max(WaveTrain.MinWavelengthMeters, train.Wavelength);
            float waveNumber = (2f * Mathf.PI) / wavelength;   // the same float expression WaveMath.Sample uses
            return new Vector4(train.Direction.x, train.Direction.y, waveNumber, train.Amplitude);
        }

        /// <summary>The train's phase at the packed field's t = 0 frame: its (animator-baked) phase
        /// offset, wrapped to [0, 2π) in DOUBLE before the float cast — the WaveMath.Sample wrap
        /// discipline, so a large offset never reaches float trig unwrapped.</summary>
        private static float PhaseAt(in WaveTrains trains, int index, int count)
        {
            if (index >= count) return 0f;
            double phase = trains[index].PhaseOffset;
            phase -= Math.Floor(phase / TwoPi) * TwoPi;
            return (float)phase;
        }

        /// <summary>
        /// Read the six published wave-field globals back — the packed field exactly as the water
        /// shader will sample it this frame (empty/zero when no bridge is live: the "unset"
        /// convention, under which <see cref="ShaderTwinSample"/> returns height 0). The watertight
        /// hull clamp (<c>DisplacedWaterMath.WatertightZHeaveMeters</c>) reads the sea through this,
        /// so hull and water can never disagree about the field — the ONE-SEA rule, closed at the
        /// globals. Allocation-free (rule 7).
        /// </summary>
        public static PackedWaveField ReadPublishedField()
        {
            return new PackedWaveField(
                Shader.GetGlobalVector(IdWaveTrain[0]), Shader.GetGlobalVector(IdWaveTrain[1]),
                Shader.GetGlobalVector(IdWaveTrain[2]), Shader.GetGlobalVector(IdWaveTrain[3]),
                Shader.GetGlobalVector(IdWaveTrain[4]), Shader.GetGlobalVector(IdWaveTrain[5]),
                Shader.GetGlobalVector(IdWaveTrain[6]), Shader.GetGlobalVector(IdWaveTrain[7]),
                Shader.GetGlobalVector(IdWavePhases),
                Shader.GetGlobalVector(IdWavePhases2),
                Shader.GetGlobalVector(IdWaveFieldParams));
        }

        // ==== The C# MIRROR of the shader twin (parity documentation, pinned by tests) ====================

        /// <summary>
        /// A line-by-line C# transcription of the shader's <c>WaveFieldSample()</c> (which is itself the
        /// HLSL twin of <see cref="WaveMath.Sample"/> reading the packed globals), at the twin's
        /// reference frequency scale 1. Exists so the WHOLE pipeline — derive → animate →
        /// <see cref="Pack"/> → reconstruct on the "GPU side" — is pinned HEADLESS against
        /// <see cref="WaveMath.Sample"/> (<c>WaveFieldBridgeTests</c>); an HLSL review diffs the shader
        /// against THIS method. The only deliberate deviations from <see cref="WaveMath.Sample"/>:
        /// θ = k·(dir·pos) + φ with the pre-wrapped published phase (float-safe — the unbounded time
        /// lives in φ, accumulated in double by the animator), and the pow bases are floored at 1e-6
        /// (HLSL's <c>pow(0, 0)</c> is NaN on some GPUs; the deviation lives where cos θ ≈ 0, so it is
        /// invisible). Also the RUNTIME sampler behind the watertight hull clamp
        /// (<c>DisplacedWaterMath.WatertightZHeaveMeters</c>): the clamp must bound the lift the
        /// SHADER draws, so it evaluates the shader's own transcription over the published globals
        /// — never the sim-side <see cref="WaveMath.Sample"/> path directly. Pure and
        /// allocation-free (rule 7).
        /// </summary>
        public static WaveSample ShaderTwinSample(Vector2 worldPos, in PackedWaveField field)
            => ShaderTwinSample(worldPos, in field, 1f);

        /// <summary>
        /// The same transcription at an explicit FREQUENCY SCALE — the shader's <c>freqScale</c>
        /// argument, which multiplies every train's wave number (<c>float k = trains[i].z * fs</c>).
        ///
        /// <para>⚠️ <b>Why this overload has to exist.</b> The displaced vertex stage does NOT sample
        /// at scale 1: it passes <c>_OceanSwellScale / 0.025</c>, a knob that was pure col.rgb
        /// dressing until ADR 0023 made the same sampling line move real vertices. The owner's
        /// material carries 0.07, so the DRAWN sea runs at 2.8 while this twin — the sampler behind
        /// the watertight hull clamp — evaluated at 1 and guarded a sea that was not on screen. A
        /// clamp that scans the wrong wavelengths finds its worst case in the wrong place and lets
        /// the real crests board the boat, on every hull, worse the rougher it gets (owner playtest
        /// 2026-07-25). The scale reaches the clamp through
        /// <c>WaterIsoDepthFrame.FreqScale</c>, republished live each tick.</para>
        ///
        /// <para>The scale-1 overload above is kept exactly as it was: <c>WaveFieldBridgeTests</c>
        /// pins it against <see cref="WaveMath.Sample"/>, which is the SIM field and genuinely has
        /// no visual scale — that parity proof must not move.</para>
        /// </summary>
        public static WaveSample ShaderTwinSample(Vector2 worldPos, in PackedWaveField field,
                                                  float freqScale)
        {
            float height = 0f;
            float slopeX = 0f;
            float slopeY = 0f;
            int count = field.Count;
            float sharpening = field.CrestSharpening;
            float totalAmplitude = field.TotalAmplitude;

            for (int i = 0; i < PackedWaveField.MaxTrains; i++)   // the shader's FIXED loop bound
            {
                Vector4 train = field.Train(i);
                float phi = field.Phase(i);
                float amplitude = train.w;
                if (i >= count || amplitude <= 0f) continue;  // the shader masks on (i < count && amp > 0)

                // `float k = trains[i].z * fs` — the shader scales the published wave number, and
                // every downstream term (theta AND the analytic slope) then reads the SCALED k.
                float waveNumber = train.z * Mathf.Max(freqScale, 1e-3f);
                float theta = waveNumber * (train.x * worldPos.x + train.y * worldPos.y) + phi;
                float sin = Mathf.Sin(theta);
                float cos = Mathf.Cos(theta);

                float s = (sin + 1f) * 0.5f;
                float shaped = Mathf.Pow(Mathf.Max(s, 1e-6f), sharpening);
                height += amplitude * (2f * shaped - 1f);

                float slopeMagnitude = amplitude * sharpening
                                     * Mathf.Pow(Mathf.Max(s, 1e-6f), sharpening - 1f)
                                     * cos * waveNumber;
                slopeX += slopeMagnitude * train.x;
                slopeY += slopeMagnitude * train.y;
            }

            float crestFactor = 0f;
            if (totalAmplitude > 1e-6f)
                crestFactor = Mathf.Pow(Mathf.Clamp01(height / totalAmplitude), sharpening);

            return new WaveSample(height, new Vector2(slopeX, slopeY), crestFactor);
        }
    }
}
