using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// SELF-INSTALLING dust motes / pollen — tiny drifting specks that hang and shimmer in the daylight air and
    /// FADE AWAY after dark (sunbeam motes only show when there's sun to catch). The lightest living-coast
    /// touch (Pillar 3); kept very cheap (a couple dozen point-sized sprites).
    ///
    /// <para><b>Self-installing, shared signals, determinism (rules 4/5).</b> Spawns one hidden persistent host
    /// before the first scene (mirrors <see cref="GrassWindBridge"/>); the motes drift on the shared global
    /// wind <c>_WindWorld</c> and fade with the global day/night tint <c>_DayNightTint</c> (NightFade = 1 by
    /// default → gone in the dark) — both READ-ONLY. Per-mote variation is the deterministic
    /// <see cref="AmbientParticleMath.Hash01(int,int)"/>, not <see cref="System.Random"/>. Drives no sim, saves
    /// nothing. <b>Performance (rule 7):</b> a fixed recycled pool, one shared dot sprite (batched), throttled;
    /// and when it's fully dark the whole effect early-outs so it costs nothing at night.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DustMotes : MonoBehaviour
    {
        [Tooltip("Every knob of the motes — pool, area, drift/bob, life, look, day/night fade. Tune to taste; " +
                 "defaults are tiny specks that drift by day and vanish after dark.")]
        [SerializeField] private DustMoteConfig _config = DustMoteConfig.Default;

        [Tooltip("Sorting order — motes float in front of most things (they catch the foreground light).")]
        [SerializeField] private int _sortingOrder = 8;

        [Tooltip("How often (Hz) the motes tick. Slow drift reads fine at a handful of Hz.")]
        [Min(2f)] [SerializeField] private float _tickHz = 20f;

        private struct Mote
        {
            public bool Alive;
            public Vector2 Pos;
            public Vector2 OwnDrift;
            public float Age;
            public float Lifetime;
            public float Size;
            public float Seed;
        }

        private Mote[] _pool;
        private SpriteRenderer[] _renderers;
        private Sprite _sprite;
        private float _tickTimer;
        private float _spawnCarry;
        private int _spawnCursor;
        private int _spawnCounter;

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("DustMotes") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<DustMotes>();
        }

        private void Awake()
        {
            _sprite = AmbientGlobals.BuildDot("DustMotes.Dot", 8, 32);
            BuildPool();
        }

        private void OnEnable() => _tickTimer = 0f;

        private void BuildPool()
        {
            int n = Mathf.Max(1, _config.MaxMotes);
            _pool = new Mote[n];
            _renderers = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("mote");
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
            Color tint = AmbientGlobals.DayNightTint;
            float brightness = AmbientParticleMath.DayNightBrightness(tint);
            float dayOpacity = AmbientParticleMath.DayNightOpacity(brightness, _config.NightFade);

            // Full dark → nothing to draw. Early-out so motes cost nothing at night.
            if (dayOpacity <= 1e-3f) { HideAll(); return; }

            Camera cam = AmbientGlobals.ResolveCamera();
            if (cam == null) { HideAll(); return; }
            Vector2 center = cam.transform.position;
            Vector2 wind = AmbientGlobals.Wind;
            float time = Time.time;

            // spawn to keep the pool populated
            float targetRate = _config.MaxMotes / Mathf.Max(0.1f, _config.Lifetime);
            _spawnCarry += targetRate * dt;
            int toSpawn = Mathf.FloorToInt(_spawnCarry);
            if (toSpawn > 0) _spawnCarry -= toSpawn;
            for (int k = 0; k < toSpawn; k++) Spawn(center);

            for (int i = 0; i < _pool.Length; i++)
            {
                ref Mote m = ref _pool[i];
                var sr = _renderers[i];
                if (!m.Alive)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }

                m.Pos = AmbientParticleMath.Drift(m.Pos, m.OwnDrift, wind, _config.WindResponse, dt);
                m.Age += dt;
                if (m.Age >= m.Lifetime) { m.Alive = false; sr.gameObject.SetActive(false); continue; }

                float bob = AmbientParticleMath.MoteBob(time, _config.BobAmp, m.Seed, _config.BobSpeed);
                float life = AmbientParticleMath.Life01(m.Age, m.Lifetime);
                float env01 = AmbientParticleMath.LifeEnvelope(life, _config.FadeIn, _config.FadeOut);
                float alpha = Mathf.Clamp01(_config.MaxAlpha * env01 * dayOpacity);

                var t = sr.transform;
                t.position = new Vector3(m.Pos.x, m.Pos.y + bob, 0f);
                t.localScale = new Vector3(m.Size, m.Size, 1f);
                Color col = _config.Color * tint;
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
            float hx = AmbientParticleMath.Hash01(salt, 19);
            float hy = AmbientParticleMath.Hash01(salt, 37);
            float hs = AmbientParticleMath.Hash01(salt, 61);
            float hd = AmbientParticleMath.Hash01(salt, 83);
            float hseed = AmbientParticleMath.Hash01(salt, 101);

            Vector2 pos = center + new Vector2(
                (hx * 2f - 1f) * _config.AreaHalfSize.x,
                (hy * 2f - 1f) * _config.AreaHalfSize.y);
            float sizeJit = 1f + (hs - 0.5f) * 2f * _config.SizeJitter;

            _pool[i] = new Mote
            {
                Alive = true,
                Pos = pos,
                OwnDrift = _config.BaseDrift * (0.5f + hd),
                Age = 0f,
                Lifetime = Mathf.Max(0.1f, _config.Lifetime),
                Size = Mathf.Max(0.005f, _config.Size * sizeJit),
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
