using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The SHARED-WIND bridge for the grass shader (<c>HiddenHarbours/GrassWind</c>). It reads the SAME
    /// deterministic wind the water reads — <see cref="EnvironmentSample.WindVector"/> via the Core
    /// <see cref="GameServices.Environment"/> accessor — on a throttled tick and publishes it to a GLOBAL shader
    /// vector <c>_WindWorld</c> with <see cref="Shader.SetGlobalVector(int, Vector4)"/>. EVERY grass instance then
    /// reads that one global with no per-object wiring, so a gust leans the grass AND ruffles the water TOGETHER
    /// (the cohesion the owner asked for: the wind's wandering direction carries across the whole scene).
    ///
    /// <para><b>Self-installing (mirrors the water plumbing, but a SEPARATE component).</b> A
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns a single hidden <c>[DontDestroyOnLoad]</c> host before
    /// the first scene loads, so neither the owner nor world-content has to place anything — drop grass into any
    /// scene and it moves. (It does NOT touch <see cref="WaterSurface"/> or the water material; the two systems
    /// just happen to read the same sim wind.)</para>
    ///
    /// <para><b>Strength mapping.</b> <see cref="EnvironmentSample.WindVector"/> is <c>direction * strength</c> in
    /// m/s. The shader wants an amplitude in 0..1, so this bridge normalizes the strength against
    /// <see cref="_windForFullSway"/> (a breeze barely stirs the grass; a gale lays it over) and publishes
    /// <c>direction * normalizedStrength</c>. The direction (which the sim VARIES over time) is preserved so the
    /// blades lean and the gust ripple travels the way the wind blows. See <see cref="WindToShaderVector"/>.</para>
    ///
    /// <para><b>Seam discipline (rule 4) and determinism (rule 5).</b> Reads the sim ONLY through the Core
    /// <see cref="GameServices.Environment"/> accessor — never the Environment concrete class — and never writes
    /// it. Visual-only: drives no simulation, saves nothing. When there is no sim yet (EditMode, pre-boot, or the
    /// bare demo scene) it publishes nothing, leaving the grass on its wind-independent idle baseline (or whatever
    /// a dev test-wind sets). <b>Performance (rule 7):</b> one global vector set on a throttled tick (the wind is
    /// slow — a few Hz is plenty), no per-frame allocation, no per-object cost regardless of tuft count.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrassWindBridge : MonoBehaviour
    {
        private static readonly int IdWindWorld = Shader.PropertyToID("_WindWorld");

        [Tooltip("Wind speed (m/s) that maps to FULL grass sway (saturates here). A breeze barely stirs the " +
                 "grass; a gale lays it over. Mirrors the water's wind-for-full-roughness so the two read off " +
                 "the same scale.")]
        [Min(0.01f)] [SerializeField] private float _windForFullSway = 12f;

        [Tooltip("How often (Hz) the wind is re-published to the shader. The wind is slow; a few Hz is plenty " +
                 "and cheap.")]
        [Min(0.5f)] [SerializeField] private float _refreshHz = 8f;

        private float _timer;

        /// <summary>
        /// Spawn the single self-installing host before the first scene. Guarded so it never double-installs
        /// (domain reloads / additive scene loads). Mirrors the project's other self-installing services.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("GrassWindBridge") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<GrassWindBridge>();
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
            _timer = _refreshHz > 0f ? 1f / _refreshHz : 0.2f;
            Publish();
        }

        private void Publish()
        {
            var env = GameServices.Environment;
            if (env == null) return;   // no sim (EditMode / pre-boot / bare demo) — leave the idle baseline alone

            Vector2 v = WindToShaderVector(env.Sample().WindVector, _windForFullSway);
            Shader.SetGlobalVector(IdWindWorld, new Vector4(v.x, v.y, 0f, 0f));
        }

        // ==== PURE mapping (testable headless; no Unity scene needed) =====================================

        /// <summary>
        /// Map the sim wind (<see cref="EnvironmentSample.WindVector"/> = direction * strength, m/s) to the
        /// shader's <c>_WindWorld</c>: the wind DIRECTION (preserved, since the sim wanders it over time) times
        /// the strength NORMALIZED to 0..1 against <paramref name="windForFullSway"/> and saturated. A breeze
        /// barely stirs the grass; a gale lays it over. NaN-safe: a near-zero wind returns
        /// <see cref="Vector2.zero"/> (the shader then falls back to its idle baseline along +X), so the grass
        /// never freezes to a NaN lean. Deterministic and monotonic in wind strength.
        /// </summary>
        public static Vector2 WindToShaderVector(Vector2 windVector, float windForFullSway)
        {
            float mag = windVector.magnitude;
            if (mag < 1e-5f) return Vector2.zero;
            float norm = Mathf.Clamp01(mag / Mathf.Max(windForFullSway, 1e-3f));
            return windVector / mag * norm;   // unit direction * normalized strength
        }
    }
}
