using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The LIVING-MOON service: a self-installing component that makes the moon RISE, ARC across the night,
    /// and SET, and cycle through its PHASES, then publishes that state as shader GLOBALS the water shader
    /// reads to position + shape the reflected moon (the disc + the shimmering glitter path). The moon is the
    /// only sky body in this ¾ top-down game the player ever sees — and only in the water's reflection.
    ///
    /// <para><b>What it publishes</b> (via <see cref="Shader.SetGlobalVector(int, Vector4)"/>, outside any
    /// per-material CBUFFER, like <c>_SunDir</c>/<c>_WindWorld</c>):
    /// <list type="bullet">
    /// <item><c>_MoonDir</c> (xy) — the moon's CURRENT reflected ground-plane direction, sweeping east→west
    ///   across the night so the reflected disc + glitter travel over the water. (0,0) when the moon is down.</item>
    /// <item><c>_MoonPhaseState</c> — x = phase 0..1 (0 new, 0.5 full), y = signed terminator (the crescent
    ///   mask the shader carves the disc with), z = the live BRIGHTNESS (illuminated-fraction × how high the
    ///   moon is — a thin crescent low on the horizon is dim; a full moon overhead is bright), w = the
    ///   above-horizon presence 0..1 (so the shader can fade the moon in/out at the horizons).</item>
    /// </list></para>
    ///
    /// <para><b>Tied to the tides (vision-and-pillars §5.5).</b> The PHASE derives from the SAME lunar period
    /// that drives the tide's spring/neap envelope (<see cref="MoonMath.Phase01"/> proves the alignment), so
    /// FULL MOON lands on a SPRING tide. The period is a serialized tunable defaulting to the canon 28 days /
    /// 1200 s/day — it MIRRORS <c>GameConfig.LunarMonthDays</c>/<c>SecondsPerDay</c>. (GameConfig isn't in a
    /// Resources folder, so it can't be auto-loaded here without touching the builders/other lanes; if a
    /// future change exposes it through Core, wire it then. Until then keep these in sync with GameConfig.)</para>
    ///
    /// <para><b>Self-installing (mirrors <see cref="GrassWindBridge"/> / <see cref="DayNightController"/>).</b>
    /// A <see cref="RuntimeInitializeOnLoadMethod"/> spawns one hidden <c>[DontDestroyOnLoad]</c> host before
    /// the first scene, so it works in EVERY scene with NO wiring. <b>Seam discipline (rule 4) &amp;
    /// determinism (rule 5):</b> reads time ONLY through the Core <see cref="GameServices.Clock"/> accessor —
    /// never a concrete sim class — and never writes it; the moon state is a pure function of the clock + the
    /// lunar period (<see cref="MoonMath"/>), saving nothing. <b>Performance (rule 7):</b> two global vector
    /// sets on a throttled tick (the moon moves slowly), no per-frame allocation, no per-object cost.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MoonCycle : MonoBehaviour
    {
        private static readonly int IdMoonDir        = Shader.PropertyToID("_MoonDir");
        private static readonly int IdMoonPhaseState = Shader.PropertyToID("_MoonPhaseState");

        [Header("Lunar period (KEEP IN SYNC with GameConfig — drives phase = tide alignment)")]
        [Tooltip("Lunar month in in-game DAYS. Mirrors GameConfig.LunarMonthDays (canon 28). The PHASE derives " +
                 "from this same period as the tide's spring/neap envelope, so full moon ~ spring tide.")]
        [Min(0.1f)] [SerializeField] private float _lunarMonthDays = 28f;

        [Tooltip("In-game SECONDS per day. Mirrors GameConfig.SecondsPerDay (default 1200 = a 20-min day).")]
        [Min(1f)] [SerializeField] private float _secondsPerDay = 1200f;

        [Tooltip("Days to offset the start of the cycle, so a new game can begin on a chosen phase. " +
                 "0 = the game starts on a new moon.")]
        [SerializeField] private float _phaseOffsetDays = 7f;   // start near a waxing half-moon, not pitch dark

        [Header("Nightly arc (when the moon is up)")]
        [Tooltip("Day fraction (0..1) the moon RISES. 0.78 ≈ dusk.")]
        [Range(0f, 1f)] [SerializeField] private float _moonriseFraction = 0.78f;

        [Tooltip("Day fraction (0..1) the moon SETS (wraps past midnight when before moonrise). 0.30 ≈ after dawn.")]
        [Range(0f, 1f)] [SerializeField] private float _moonsetFraction = 0.30f;

        [Tooltip("Link the moon's per-night PRESENCE to its phase: a full moon is up most of the night " +
                 "(brighter nights), a new moon barely up (dark nights you need the boat light for). " +
                 "0 = phase doesn't affect presence; 1 = strong link (new-moon nights nearly moonless).")]
        [Range(0f, 1f)] [SerializeField] private float _phaseDrivesPresence = 0.6f;

        [Header("Refresh")]
        [Tooltip("How often (Hz) the moon state is re-published. The moon moves slowly; a couple Hz is plenty.")]
        [Min(0.5f)] [SerializeField] private float _refreshHz = 4f;

        private float _timer;

        /// <summary>
        /// Spawn the single self-installing host before the first scene. Guarded against double-install
        /// (domain reloads / additive scene loads), like the project's other self-installing services.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("MoonCycle") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<MoonCycle>();
        }

        private static bool _installed;

        private void OnEnable()
        {
            _timer = 0f;
            Publish();   // push once immediately so the first frame is correct, not a stale default
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _refreshHz > 0f ? 1f / _refreshHz : 0.25f;
            Publish();
        }

        private void Publish()
        {
            var clock = GameServices.Clock;
            if (clock == null) return;   // no sim (EditMode / pre-boot / bare demo) — leave the unset default

            double totalSeconds = clock.TotalSeconds;
            float dayFraction = clock.DayFraction;

            ComputeState(totalSeconds, dayFraction,
                         _lunarMonthDays, _secondsPerDay, _phaseOffsetDays,
                         _moonriseFraction, _moonsetFraction, _phaseDrivesPresence,
                         out Vector2 dir, out Vector4 phaseState);

            Shader.SetGlobalVector(IdMoonDir, new Vector4(dir.x, dir.y, 0f, 0f));
            Shader.SetGlobalVector(IdMoonPhaseState, phaseState);
        }

        // ==== PURE state assembly (testable headless; no Unity scene needed) ===============================

        /// <summary>
        /// Assemble the full moon state from the clock — the nightly ARC direction + presence and the PHASE +
        /// terminator + brightness — all from <see cref="MoonMath"/>. Pure and deterministic (rule 5): given the
        /// same clock + tunables it always returns the same state, saving nothing. Exposed static so the packing
        /// (what goes in <c>_MoonDir</c> / <c>_MoonPhaseState</c>) is unit-tested headless.
        /// </summary>
        /// <param name="dir">OUT: the moon's reflected ground direction (xy), or (0,0) when the moon is down.</param>
        /// <param name="phaseState">OUT: x = phase01, y = signed terminator, z = brightness, w = above-horizon.</param>
        public static void ComputeState(
            double totalSeconds, float dayFraction,
            float lunarMonthDays, float secondsPerDay, float phaseOffsetDays,
            float moonriseFraction, float moonsetFraction, float phaseDrivesPresence,
            out Vector2 dir, out Vector4 phaseState)
        {
            float phase01 = MoonMath.Phase01(totalSeconds, lunarMonthDays, secondsPerDay, phaseOffsetDays);
            float terminator = MoonMath.TerminatorSigned(phase01);
            float illum = MoonMath.IlluminatedFraction(phase01);   // 0 new .. 1 full

            MoonMath.MoonArc(dayFraction, out Vector2 arcDir, out float aboveHorizon,
                             moonriseFraction, moonsetFraction);

            // PRESENCE: how much of a moon there is to see right now. The arc height already fades it at the
            // horizons; optionally a NEW moon is dimmer all night (a dark night you need the boat light for),
            // a FULL moon brighter — lerp the presence floor down by phase so new-moon nights go nearly moonless.
            float phasePresence = Mathf.Lerp(1f, illum, Mathf.Clamp01(phaseDrivesPresence));
            float presence = aboveHorizon * phasePresence;

            // BRIGHTNESS the shader scales the disc + glitter by: the illuminated fraction (a thin crescent
            // gives little light) × the presence (low / new-moon = dim). Keeps the moon believable across the month.
            float brightness = Mathf.Clamp01(illum * presence);

            // When the moon is effectively down, zero the direction so the shader cleanly drops the reflection.
            dir = aboveHorizon > 1e-4f ? arcDir : Vector2.zero;
            phaseState = new Vector4(phase01, terminator, brightness, aboveHorizon);
        }
    }
}
