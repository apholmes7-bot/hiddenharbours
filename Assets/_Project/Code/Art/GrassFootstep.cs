using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The PLAYER-TRAIL bridge for the grass shader. Drop it on whatever moves through the grass (the player, an
    /// NPC, a cart) and it records that transform's recent PATH and publishes it to the global shader array
    /// <c>_GrassTrail</c> with <see cref="Shader.SetGlobalVectorArray(int, Vector4[])"/>. The grass shader parts
    /// each tuft away from the NEAREST recent trail point within a footprint-sized radius, fading by the point's
    /// recency — so the grass bends along the path the walker actually trod and springs back behind them (a
    /// trodden trail), instead of a wide halo that circles the player.
    ///
    /// <para><b>How the trail works.</b> A ring buffer of the last <see cref="TrailLength"/> world positions: the
    /// HEAD point tracks the live position (the footprint under the walker), and a new point is laid down every
    /// <see cref="_pointSpacing"/> metres of travel. Each point fades from full to nothing over
    /// <see cref="_trailLifetime"/> seconds (the spring-back). The shader takes the strongest nearby point (not a
    /// sum) so overlapping footprints never stack into a bulge.</para>
    ///
    /// <para><b>No per-blade state, nothing saved (rule 5).</b> The recovery is the recency fade — there is no
    /// per-tuft state and nothing is persisted; <see cref="Time.time"/> only drives the cosmetic fade (visual-only,
    /// like the water shader's <c>_Time</c>). <b>Seam discipline (rule 4):</b> it only writes a shader global — it
    /// references no other feature module. <b>Performance (rule 7):</b> one reused array uploaded per frame (no
    /// per-frame allocation), independent of how many tufts read it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrassFootstep : MonoBehaviour
    {
        /// <summary>Trail point count — MUST match <c>TRAIL_N</c> in HiddenHarboursGrass.shader.</summary>
        public const int TrailLength = 24;

        private static readonly int IdGrassTrail = Shader.PropertyToID("_GrassTrail");

        [Tooltip("Optional explicit source to track. Leave empty to track THIS object's transform (the usual " +
                 "case: put this component on the player).")]
        [SerializeField] private Transform _source;

        [Tooltip("Seconds a trodden footprint takes to spring back (fade to nothing). Shorter = the grass " +
                 "recovers right behind you; longer = a path lingers.")]
        [Min(0.05f)] [SerializeField] private float _trailLifetime = 1.2f;

        [Tooltip("Lay down a new footprint every this many metres of travel. Roughly a stride; smaller = a " +
                 "denser, smoother path (uses up the trail length faster).")]
        [Min(0.02f)] [SerializeField] private float _pointSpacing = 0.25f;

        [Tooltip("Clear the trail in OnDisable so the grass relaxes when the walker leaves (e.g. boarding a " +
                 "boat). Off keeps the last path so the grass holds its parting.")]
        [SerializeField] private bool _resetOnDisable = true;

        private readonly Vector2[] _pos = new Vector2[TrailLength];
        private readonly float[] _born = new float[TrailLength];
        private readonly Vector4[] _buf = new Vector4[TrailLength];
        private int _head;
        private bool _seeded;

        private Transform Source => _source != null ? _source : transform;

        private void OnEnable()
        {
            for (int i = 0; i < TrailLength; i++) { _born[i] = -1e9f; _pos[i] = Vector2.zero; }
            _head = 0;
            _seeded = false;
            Step();
        }

        private void LateUpdate() => Step();

        private void OnDisable()
        {
            if (!_resetOnDisable) return;
            // Zero every point's recency so no tuft thinks the walker is still standing in it.
            for (int i = 0; i < TrailLength; i++) { _born[i] = -1e9f; _buf[i] = Vector4.zero; }
            Shader.SetGlobalVectorArray(IdGrassTrail, _buf);
        }

        private void Step()
        {
            float now = Time.time;
            Vector2 cur = Source.position;

            if (!_seeded)
            {
                _head = 0;
                _pos[0] = cur;
                _born[0] = now;
                _seeded = true;
            }
            else
            {
                // Lay a new footprint once we've travelled a stride; otherwise keep dragging the head along.
                if ((cur - _pos[_head]).sqrMagnitude >= _pointSpacing * _pointSpacing)
                    _head = (_head + 1) % TrailLength;
                _pos[_head] = cur;
                _born[_head] = now;   // the head footprint stays fresh while it's the active one
            }

            for (int i = 0; i < TrailLength; i++)
            {
                float str = TrailStrength(now - _born[i], _trailLifetime);
                _buf[i] = new Vector4(_pos[i].x, _pos[i].y, str, 0f);
            }
            Shader.SetGlobalVectorArray(IdGrassTrail, _buf);
        }

        // ==== PURE helpers (testable headless; mirror the shader math) ====================================

        /// <summary>
        /// A trail point's recency strength (0..1) the shader multiplies its bend by: 1 the instant it is trodden,
        /// fading LINEARLY to 0 after <paramref name="lifetime"/> seconds (the spring-back). Clamped, so a stale
        /// or never-set point (huge/negative age) reads 0 — no bend. Monotonic non-increasing in age.
        /// </summary>
        public static float TrailStrength(float age, float lifetime)
        {
            if (age < 0f) return 1f;   // freshly stamped (head refreshed this frame)
            return Mathf.Clamp01(1f - age / Mathf.Max(lifetime, 1e-3f));
        }

        /// <summary>
        /// The per-point footprint falloff the shader applies: <c>1 - smoothstep(0, radius, dist)</c> — full at
        /// the footprint centre, easing to 0 at and beyond <paramref name="radius"/>. Mirrors the HLSL so the
        /// footprint shape is unit-tested headless. Monotonic non-increasing in distance; 0 at/past the radius.
        /// </summary>
        public static float FootstepFalloff(float distance, float radius)
        {
            float r = Mathf.Max(radius, 1e-3f);
            float t = Mathf.Clamp01(distance / r);
            float s = t * t * (3f - 2f * t);   // smoothstep(0,1,t)
            return 1f - s;
        }
    }
}
