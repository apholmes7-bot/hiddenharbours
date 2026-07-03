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
    /// <item><c>_WaveTrain0..3</c> — per train: xy = unit travel direction, z = wave number
    ///   <c>k = 2π/λ</c> (precomputed here so the shader never divides by a wavelength), w = amplitude
    ///   (metres). Slots at or beyond the live count are zero.</item>
    /// <item><c>_WavePhases</c> — per train, the animator-baked phase (radians, wrapped to [0, 2π) in
    ///   C# DOUBLE before the float cast). The shader evaluates <c>θ = k·(dir·worldPos) + φᵢ</c> —
    ///   <b>NO time uniform at all</b>: the advancing time lives in the phase the animator accumulates,
    ///   so the unbounded game time never touches float trig on the GPU, and the shader NEVER re-derives
    ///   the phase speed (dispersion lives in C# only — ADR 0018 §(4)).</item>
    /// <item><c>_WaveFieldParams</c> — x = live train count (0 = nothing published → the shader's
    ///   legacy noise-swell look holds), y = crest sharpening p, z = total amplitude (metres; the
    ///   crest-factor normalizer), w = reserved.</item>
    /// </list>
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
    /// <para><b>Tuning (rule 6) — settings parity (ADR 0018 §(4)).</b> <see cref="_settings"/> and
    /// <see cref="_animatorSettings"/> start from the same <c>Default</c>s <c>BoatWaveMotion</c> uses,
    /// so the hull rocks on the same waves the player sees. B3/GameConfig will unify the settings
    /// instances into ONE owner-tunable source; until then tune the field's shape identically in both
    /// places (they are serialized on this hidden host and on the boat).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveFieldBridge : MonoBehaviour
    {
        private static readonly int IdWaveTrain0      = Shader.PropertyToID("_WaveTrain0");
        private static readonly int IdWaveTrain1      = Shader.PropertyToID("_WaveTrain1");
        private static readonly int IdWaveTrain2      = Shader.PropertyToID("_WaveTrain2");
        private static readonly int IdWaveTrain3      = Shader.PropertyToID("_WaveTrain3");
        private static readonly int IdWavePhases      = Shader.PropertyToID("_WavePhases");
        private static readonly int IdWaveFieldParams = Shader.PropertyToID("_WaveFieldParams");

        private const double TwoPi = Math.PI * 2.0;

        [Tooltip("The wind + sea-state -> wave-train derivation constants (ADR 0018 §(1)). Starts at " +
                 "WaveFieldSettings.Default — keep IDENTICAL to BoatWaveMotion's field settings so the " +
                 "hull rocks on the waves the player sees. GameConfig unification comes with a later " +
                 "Arc B PR.")]
        [SerializeField] private WaveFieldSettings _settings = WaveFieldSettings.Default;

        [Tooltip("The shared presentation smoother's tunables (ADR 0018 addendum): how languidly the " +
                 "train parameters chase the drifting weather, and the glass snap floor (glass is " +
                 "sacred). Keep in step with BoatWaveMotion's animator settings.")]
        [SerializeField] private WaveFieldAnimatorSettings _animatorSettings = WaveFieldAnimatorSettings.Default;

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
            WaveTrains trains = _animator.Tick(dt, sample.WindVector, sample.SeaState01,
                                               in _settings, in _animatorSettings);
            Pack(in trains,
                 out Vector4 train0, out Vector4 train1, out Vector4 train2, out Vector4 train3,
                 out Vector4 phases, out Vector4 fieldParams);
            Shader.SetGlobalVector(IdWaveTrain0, train0);
            Shader.SetGlobalVector(IdWaveTrain1, train1);
            Shader.SetGlobalVector(IdWaveTrain2, train2);
            Shader.SetGlobalVector(IdWaveTrain3, train3);
            Shader.SetGlobalVector(IdWavePhases, phases);
            Shader.SetGlobalVector(IdWaveFieldParams, fieldParams);
        }

        private void PublishEmpty()
        {
            Shader.SetGlobalVector(IdWaveTrain0, Vector4.zero);
            Shader.SetGlobalVector(IdWaveTrain1, Vector4.zero);
            Shader.SetGlobalVector(IdWaveTrain2, Vector4.zero);
            Shader.SetGlobalVector(IdWaveTrain3, Vector4.zero);
            Shader.SetGlobalVector(IdWavePhases, Vector4.zero);
            Shader.SetGlobalVector(IdWaveFieldParams, Vector4.zero);
        }

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
        public static void Pack(in WaveTrains trains,
                                out Vector4 train0, out Vector4 train1, out Vector4 train2,
                                out Vector4 train3, out Vector4 phases, out Vector4 fieldParams)
        {
            int count = trains.Count;
            train0 = PackTrain(in trains, 0, count);
            train1 = PackTrain(in trains, 1, count);
            train2 = PackTrain(in trains, 2, count);
            train3 = PackTrain(in trains, 3, count);
            phases = new Vector4(
                PhaseAt(in trains, 0, count),
                PhaseAt(in trains, 1, count),
                PhaseAt(in trains, 2, count),
                PhaseAt(in trains, 3, count));
            fieldParams = new Vector4(count, trains.CrestSharpening, trains.TotalAmplitude, 0f);
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
        /// invisible). Not called at runtime.
        /// </summary>
        public static WaveSample ShaderTwinSample(Vector2 worldPos,
                                                  Vector4 train0, Vector4 train1, Vector4 train2,
                                                  Vector4 train3, Vector4 phases, Vector4 fieldParams)
        {
            float height = 0f;
            float slopeX = 0f;
            float slopeY = 0f;
            int count = (int)(fieldParams.x + 0.5f);
            float sharpening = Mathf.Max(fieldParams.y, 1f);
            float totalAmplitude = fieldParams.z;

            for (int i = 0; i < WaveTrains.MaxTrains; i++)   // the shader's FIXED loop bound
            {
                Vector4 train = i == 0 ? train0 : i == 1 ? train1 : i == 2 ? train2 : train3;
                float phi = i == 0 ? phases.x : i == 1 ? phases.y : i == 2 ? phases.z : phases.w;
                float amplitude = train.w;
                if (i >= count || amplitude <= 0f) continue;  // the shader masks on (i < count && amp > 0)

                float waveNumber = train.z;
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
