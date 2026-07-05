using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// SELF-INSTALLING falling rain — short vertical streaks that fall over the sea and SLANT downwind, so a
    /// squall reads as a physical downpour above the water (Pillars 1 &amp; 5: the sea has moods, cozy but with
    /// teeth). Companion to <see cref="SeaMistEmitter"/>: the mist is low drifting haze UNDER the boats; the
    /// rain falls IN FRONT of the water and the boats.
    ///
    /// <para><b>Rain is DERIVED, art-only (verified).</b> There is NO precipitation signal in the sim —
    /// <see cref="EnvironmentSample"/> exposes only wind/current/tide/sea-state/visibility. So rain is a pure
    /// function of the two mood axes via <see cref="AmbientParticleMath.RainIntensity"/>: it appears when the
    /// deterministic weather drives the sea-state UP (chop building) AND drops the Visibility (the light gone
    /// murky). No sim change, no save change, no determinism concern — both axes are pure functions of
    /// <c>(worldSeed, gameTime)</c>. That same helper is the SHARED source of truth a later water-shader pass
    /// reads for surface rain-rings, so the falling drops and the surface rings always agree.</para>
    ///
    /// <para><b>Self-installing (mirrors <see cref="SeaMistEmitter"/>).</b> A
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns ONE hidden <c>[DontDestroyOnLoad]</c> host before the
    /// first scene, so rain appears in EVERY scene with no wiring. The drop field is kept centred on the active
    /// camera so a small fixed pool always covers what the player sees (rule 7).</para>
    ///
    /// <para><b>Seam discipline (rule 4) &amp; determinism (rule 5).</b> The slant reads the REAL sim
    /// <see cref="EnvironmentSample.WindVector"/> (m/s) through the Core <see cref="GameServices.Environment"/>
    /// accessor — NOT the normalised 0..1 <c>_WindWorld</c> global — so a gale visibly slants the rain at true
    /// speed. Density reads the sea mood ONLY through that same Core accessor. The look dims with the global
    /// day/night tint (<c>_DayNightTint</c>, read-only) and faintly catches moonlight so a night downpour never
    /// blacks out. Per-drop variation is the deterministic <see cref="AmbientParticleMath.Hash01(int,int)"/>,
    /// never <see cref="System.Random"/>. Drives no sim, saves nothing — purely cosmetic.</para>
    ///
    /// <para><b>Sorting (the one real gotcha — see MEMORY urp2d-mesh-vs-sprite-sorting / the #134 boat-spotlight
    /// bug).</b> A <see cref="SpriteRenderer"/> does NOT reliably sort against the water <see cref="MeshRenderer"/>
    /// by <c>sortingOrder</c> alone — the mesh falls back to world-z. So the rain host carries a
    /// <see cref="SortingGroup"/> ("sort as 2D") AND every drop is nudged to a small negative z (toward the
    /// camera) so it draws in FRONT of the water plane and the boats regardless. The owner must still VERIFY in
    /// Unity that the rain reads in front — a headless test can't catch mesh-vs-sprite depth.</para>
    ///
    /// <para><b>Performance (rule 7).</b> One fixed sprite pool (no per-frame allocation), one shared streak
    /// sprite + material (batched), sim sampled once per throttled tick, MaxDrops capped. Cheap for the 60fps
    /// budget and mobile-portable.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RainEmitter : MonoBehaviour
    {
        [Tooltip("Every knob of the rain — pool, area, derived intensity, fall speed, wind slant, look, " +
                 "day/night fade. Defaults ship the feature OFF (baseline 0) until you dial it in.")]
        [SerializeField] private RainConfig _config = RainConfig.Default;

        [Tooltip("Sorting order — rain draws ABOVE the water and boats (POSITIVE). A SortingGroup + a small " +
                 "camera-ward z on each drop make this robust against the water MeshRenderer (see class docs).")]
        [SerializeField] private int _sortingOrder = 50;

        [Tooltip("Metres toward the camera (−z) each drop is nudged so it draws in FRONT of the water plane " +
                 "even where sortingOrder alone can't beat the mesh's world-z (the #134 boat-spotlight quirk).")]
        [SerializeField] private float _cameraZOffset = 1f;

        [Tooltip("How often (Hz) the rain ticks (fall + spawn + render). Rain is fast, so tick a little quicker " +
                 "than the mist for smooth streaks — still cheap.")]
        [Min(2f)] [SerializeField] private float _tickHz = 30f;

        // ---- runtime pool (struct-of-fields, recycled, never re-allocated) ----------------------------------
        private struct Drop
        {
            public bool Alive;
            public Vector2 Pos;
            public float Age;
            public float Lifetime;
            public float Size;      // per-drop length scale
            public float Seed;      // per-drop phase for variation
        }

        private Drop[] _pool;
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
            var host = new GameObject("RainEmitter") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<RainEmitter>();
        }

        private void Awake()
        {
            // A short crisp vertical streak (point-filtered) — one shared sprite so every drop batches.
            _sprite = BuildRainStreak("Rain.Streak", 4, 16, 32);
            // "Sort as 2D": make the sprite pool sort by sortingOrder against 2D content instead of falling back
            // to world-z, so the rain reliably reads in front of the water mesh (the #134 quirk).
            var group = gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = _sortingOrder;
            BuildPool();
        }

        private void OnEnable() => _tickTimer = 0f;

        private void BuildPool()
        {
            int n = Mathf.Max(1, _config.MaxDrops);
            _pool = new Drop[n];
            _renderers = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("drop");
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
            float step = _tickHz > 0f ? 1f / _tickHz : 0.033f;
            _tickTimer = step;
            Tick(step);
        }

        private void Tick(float dt)
        {
            Camera cam = AmbientGlobals.ResolveCamera();
            if (cam == null) { HideAll(); return; }
            Vector2 center = cam.transform.position;

            // --- shared signals (read-only) ---
            Color tint = AmbientGlobals.DayNightTint;
            float brightness = AmbientParticleMath.DayNightBrightness(tint);

            // Rain reads the REAL sim WindVector (m/s) for the slant — NOT the normalised _WindWorld global —
            // so a gale slants the rain at true speed. Density reads visibility + sea-state. Core accessor only.
            Vector2 windMs = Vector2.zero;
            float visibility = 1f;
            float seaState01 = 0f;   // glassy default
            var env = GameServices.Environment;
            if (env != null)
            {
                EnvironmentSample s = env.Sample();
                windMs = s.WindVector;
                visibility = s.Visibility;
                seaState01 = s.SeaState01;
            }

            float intensity = AmbientParticleMath.RainIntensity(
                visibility, seaState01, _config.BaselineIntensity, _config.SeaStateWeight,
                _config.VisOnset, _config.VisFull, _config.SeaOnset);

            // --- spawn (rate ∝ intensity, integrated with a carry; recycle round-robin) ---
            float targetRate = _config.MaxDrops / Mathf.Max(0.1f, _config.Lifetime) * Mathf.Clamp01(intensity);
            _spawnCarry += targetRate * dt;
            int toSpawn = Mathf.FloorToInt(_spawnCarry);
            if (toSpawn > 0) _spawnCarry -= toSpawn;
            for (int k = 0; k < toSpawn; k++) Spawn(center);

            // --- fall + slant + age + render ---
            float dayOpacity = AmbientParticleMath.DayNightOpacity(brightness, _config.NightFade);
            float moon = AmbientParticleMath.MoonlightCatch(brightness, _config.MoonlightCatch);

            // The per-tick velocity: straight DOWN at FallSpeed, plus a sideways slant = wind(m/s)·WindResponse.
            Vector2 vel = new Vector2(windMs.x * _config.WindResponse, -_config.FallSpeed);
            // Rotate the streak sprite to lie along its travel so it reads as a slanted streak, not a tilted bar.
            float slantDeg = Mathf.Atan2(vel.x, -vel.y) * Mathf.Rad2Deg;   // 0 = straight down

            for (int i = 0; i < _pool.Length; i++)
            {
                ref Drop d = ref _pool[i];
                var sr = _renderers[i];
                if (!d.Alive)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }

                d.Pos += vel * dt;
                d.Age += dt;
                if (d.Age >= d.Lifetime) { d.Alive = false; sr.gameObject.SetActive(false); continue; }

                float life = AmbientParticleMath.Life01(d.Age, d.Lifetime);
                float env01 = AmbientParticleMath.LifeEnvelope(life, _config.FadeIn, _config.FadeOut);
                float alpha = Mathf.Clamp01(_config.MaxAlpha * intensity * env01 * dayOpacity + moon * env01 * 0.5f);

                var t = sr.transform;
                // Nudge toward the camera (−z) so the streak draws in front of the water plane / boats.
                t.position = new Vector3(d.Pos.x, d.Pos.y, -_cameraZOffset);
                t.localScale = new Vector3(_config.Size, d.Size, 1f);
                t.rotation = Quaternion.Euler(0f, 0f, slantDeg);
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
            float hx = AmbientParticleMath.Hash01(salt, 13);
            float hy = AmbientParticleMath.Hash01(salt, 31);
            float hs = AmbientParticleMath.Hash01(salt, 57);
            float hseed = AmbientParticleMath.Hash01(salt, 89);

            // Spawn across the field width and from a band at/above the top so drops fall INTO view.
            Vector2 pos = center + new Vector2(
                (hx * 2f - 1f) * _config.AreaHalfSize.x,
                _config.AreaHalfSize.y * (0.6f + 0.6f * hy));   // top ~60%..120% of the half-height
            float lenJit = 0.7f + 0.6f * hs;                    // per-drop streak length variety

            _pool[i] = new Drop
            {
                Alive = true,
                Pos = pos,
                Age = 0f,
                Lifetime = Mathf.Max(0.1f, _config.Lifetime),
                Size = Mathf.Max(0.01f, _config.Length * lenJit),
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

        // ==== procedural streak sprite (thin crisp vertical line; one shared instance → batched) ============

        /// <summary>
        /// A short crisp VERTICAL streak (the rain drop): a bright soft-tapered line up the centre of a thin
        /// point-filtered texture, pointing +y so <see cref="Tick"/> can rotate it to its slanted travel. One
        /// shared instance → every drop batches (rule 7). Kept local to the rain lane (no shared builder needed).
        /// </summary>
        private static Sprite BuildRainStreak(string name, int width, int height, int ppu)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            float cx = (width - 1) * 0.5f;
            var px = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                // Taper the streak's alpha along its length so the head/tail feather rather than popping.
                float ty = (height <= 1) ? 1f : y / (float)(height - 1);   // 0 bottom .. 1 top
                float lengthFade = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(ty - 0.5f) * 2f);
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Abs(x - cx) / Mathf.Max(0.5f, cx);   // 0 centre .. 1 edge
                    float across = Mathf.Clamp01(1f - dx);               // bright core, soft edge
                    float a = Mathf.Clamp01(across * lengthFade);
                    byte alpha = (byte)Mathf.RoundToInt(a * 255f);
                    px[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), ppu);
        }
    }
}
