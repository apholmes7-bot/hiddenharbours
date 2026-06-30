using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// SELF-INSTALLING sea mist / shore spray — soft drifting wisps low over the water that ride the SAME
    /// deterministic wind the grass and sea read, subtle by default and THICKER when visibility is low (fog)
    /// or the sea-state is up (spray kicking off the whitecaps). One of the living-coast ambient effects
    /// (Pillar 3): a few drifting wisps make St Peters feel like a real, breathing coast.
    ///
    /// <para><b>Self-installing (mirrors <see cref="GrassWindBridge"/> / <see cref="BoatWakeEmitter"/>).</b> A
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns ONE hidden <c>[DontDestroyOnLoad]</c> host before the
    /// first scene, so the mist appears in EVERY scene with no wiring. The wisp field is kept centred on the
    /// active camera so a small fixed pool always covers what the player sees — scene-wide coverage from a few
    /// dozen sprites (rule 7).</para>
    ///
    /// <para><b>Shared signals (cohesion), seam discipline (rule 4) &amp; determinism (rule 5).</b> Drift reads
    /// the shared global wind <c>_WindWorld</c> (via <see cref="AmbientGlobals.Wind"/>) so a gust carries the
    /// mist the SAME way it leans the grass and ruffles the water. Density reads the sea mood ONLY through the
    /// Core <see cref="GameServices.Environment"/> accessor (visibility + sea-state) — never a concrete sim
    /// class. The look dims/warms with the global day/night tint (<c>_DayNightTint</c>, read-only) and faintly
    /// catches moonlight so it never blacks out. Per-wisp variation is the deterministic
    /// <see cref="AmbientParticleMath.Hash01(int,int)"/>, never <see cref="System.Random"/>. It drives no sim
    /// and saves nothing — purely cosmetic.</para>
    ///
    /// <para><b>Performance (rule 7).</b> One fixed sprite pool (no per-frame allocation), one shared sprite +
    /// material (batched), sim sampled once per throttled tick. Cheap enough for the 60fps budget and mobile-
    /// portable.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeaMistEmitter : MonoBehaviour
    {
        [Tooltip("Every knob of the sea mist — pool, area, density response, drift, look, day/night fade. " +
                 "Tune to taste; defaults are a subtle drifting haze that thickens with fog + sea-state.")]
        [SerializeField] private SeaMistConfig _config = SeaMistConfig.Default;

        [Tooltip("Sorting order — mist sits ABOVE the water plane but should read as low haze. Negative keeps " +
                 "it under boats/props.")]
        [SerializeField] private int _sortingOrder = -2;

        [Tooltip("How often (Hz) the mist ticks (drift + spawn + render). The sea is slow; a handful of Hz " +
                 "reads fine and stays cheap.")]
        [Min(2f)] [SerializeField] private float _tickHz = 20f;

        // ---- runtime pool (struct-of-fields, recycled, never re-allocated) ----------------------------------
        private struct Wisp
        {
            public bool Alive;
            public Vector2 Pos;
            public Vector2 OwnDrift;
            public float Age;
            public float Lifetime;
            public float Size;
            public float Seed;     // per-wisp phase for variation
        }

        private Wisp[] _pool;
        private SpriteRenderer[] _renderers;
        private Sprite _sprite;
        private float _tickTimer;
        private float _spawnCarry;
        private int _spawnCursor;
        private int _spawnCounter;

        // ==== self-install =================================================================================

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("SeaMistEmitter") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<SeaMistEmitter>();
        }

        private void Awake()
        {
            _sprite = AmbientGlobals.BuildSoftPuff("SeaMist.Wisp", 32, 32, softness: 1f);
            BuildPool();
        }

        private void OnEnable() => _tickTimer = 0f;

        private void BuildPool()
        {
            int n = Mathf.Max(1, _config.MaxWisps);
            _pool = new Wisp[n];
            _renderers = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("wisp");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _sprite;
                sr.sortingOrder = _sortingOrder;
                go.SetActive(false);
                _renderers[i] = sr;
            }
        }

        private void Update()
        {
            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f) return;
            float step = _tickHz > 0f ? 1f / _tickHz : 0.05f;
            _tickTimer = step;
            Tick(step);
        }

        private void Tick(float dt)
        {
            Camera cam = AmbientGlobals.ResolveCamera();
            if (cam == null) { HideAll(); return; }
            Vector2 center = cam.transform.position;

            // --- shared signals (read-only) ---
            Vector2 wind = AmbientGlobals.Wind;
            Color tint = AmbientGlobals.DayNightTint;
            float brightness = AmbientParticleMath.DayNightBrightness(tint);

            float visibility = 1f;
            SeaState seaState = SeaState.Glass;
            var env = GameServices.Environment;
            if (env != null)
            {
                EnvironmentSample s = env.Sample();
                visibility = s.Visibility;
                seaState = s.SeaState;
            }

            float intensity = AmbientParticleMath.MistIntensity(
                visibility, seaState, _config.BaselineIntensity, _config.FogWeight, _config.SeaStateWeight);

            // --- spawn (rate ∝ intensity, integrated with a carry; recycle round-robin) ---
            // Aim to keep ~intensity fraction of the pool alive: target a spawn rate that fills the pool over
            // a lifetime, scaled by intensity. No allocation; deterministic placement via the hash.
            float targetRate = _config.MaxWisps / Mathf.Max(0.1f, _config.Lifetime) * Mathf.Clamp01(intensity);
            _spawnCarry += targetRate * dt;
            int toSpawn = Mathf.FloorToInt(_spawnCarry);
            if (toSpawn > 0) _spawnCarry -= toSpawn;
            for (int k = 0; k < toSpawn; k++) Spawn(center);

            // --- drift + age + render ---
            float dayOpacity = AmbientParticleMath.DayNightOpacity(brightness, _config.NightFade);
            float moon = AmbientParticleMath.MoonlightCatch(brightness, _config.MoonlightCatch);

            for (int i = 0; i < _pool.Length; i++)
            {
                ref Wisp w = ref _pool[i];
                var sr = _renderers[i];
                if (!w.Alive)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }

                w.Pos = AmbientParticleMath.Drift(w.Pos, w.OwnDrift, wind, _config.WindResponse, dt);
                w.Age += dt;
                if (w.Age >= w.Lifetime) { w.Alive = false; sr.gameObject.SetActive(false); continue; }

                float life = AmbientParticleMath.Life01(w.Age, w.Lifetime);
                float env01 = AmbientParticleMath.LifeEnvelope(life, _config.FadeIn, _config.FadeOut);
                float alpha = Mathf.Clamp01(_config.MaxAlpha * intensity * env01 * dayOpacity + moon * env01 * 0.5f);

                var t = sr.transform;
                t.position = new Vector3(w.Pos.x, w.Pos.y, 0f);
                t.localScale = new Vector3(w.Size, w.Size, 1f);
                Color col = _config.Color * tint;        // dim/warm with the day/night look
                col.a = alpha;
                sr.color = col;
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
            }
        }

        private void Spawn(Vector2 center)
        {
            int i = _spawnCursor;
            _spawnCursor = (_spawnCursor + 1) % _pool.Length;

            int salt = _spawnCounter++;
            float hx = AmbientParticleMath.Hash01(salt, 11);
            float hy = AmbientParticleMath.Hash01(salt, 29);
            float hs = AmbientParticleMath.Hash01(salt, 53);
            float hd = AmbientParticleMath.Hash01(salt, 71);
            float hseed = AmbientParticleMath.Hash01(salt, 97);

            Vector2 pos = center + new Vector2(
                (hx * 2f - 1f) * _config.AreaHalfSize.x,
                (hy * 2f - 1f) * _config.AreaHalfSize.y);
            float sizeJit = 1f + (hs - 0.5f) * 2f * _config.SizeJitter;

            _pool[i] = new Wisp
            {
                Alive = true,
                Pos = pos,
                OwnDrift = _config.BaseDrift * (0.5f + hd),   // a little per-wisp speed variety
                Age = 0f,
                Lifetime = Mathf.Max(0.1f, _config.Lifetime),
                Size = Mathf.Max(0.01f, _config.Size * sizeJit),
                Seed = hseed,
            };
        }

        private void HideAll()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && _renderers[i].gameObject.activeSelf)
                    _renderers[i].gameObject.SetActive(false);
        }
    }
}
