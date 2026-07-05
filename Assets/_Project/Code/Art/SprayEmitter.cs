using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// SELF-INSTALLING wind-blown SPRAY — small torn puffs FLUNG off the wave crests and streaking DOWNWIND once
    /// the sea starts to whitecap, so a building sea reads as a physical, spray-torn surface (Pillars 1 &amp; 5:
    /// the sea has moods, cozy but with teeth). Companion to the falling <see cref="RainEmitter"/> and the low
    /// drifting <see cref="SeaMistEmitter"/>: mist is a steady haze UNDER the boats, rain FALLS in front of
    /// everything, and spray LAUNCHES near the water surface and blows off downwind.
    ///
    /// <para><b>Spray is DERIVED, art-only (verified).</b> There is NO spray/whitecap signal in the sim —
    /// <see cref="EnvironmentSample"/> exposes only wind/current/tide/sea-state/visibility. The per-pixel wave
    /// crest-factor that decides where a crest actually breaks lives GPU-side (the shader whitecaps of ADR 0018
    /// B1). So the particle lane uses the SCENE-LEVEL <c>SeaState01</c> axis as its whitecapping proxy via
    /// <see cref="AmbientParticleMath.SprayIntensity"/>: a glassy or gently rippled sea throws NO spray, then
    /// past a whitecap threshold spray comes on sharply into a gale. Sea-state is a pure function of
    /// <c>(worldSeed, gameTime)</c> — no sim change, no save change, no determinism concern.</para>
    ///
    /// <para><b>Self-installing (mirrors <see cref="RainEmitter"/>).</b> A
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns ONE hidden <c>[DontDestroyOnLoad]</c> host before the
    /// first scene, so spray appears in EVERY scene with no wiring. The wisp field is kept centred on the active
    /// camera so a small fixed pool always covers what the player sees (rule 7).</para>
    ///
    /// <para><b>Launched by the REAL wind (seam discipline rule 4, determinism rule 5).</b> Unlike the smoke/mist
    /// which lean on the normalised 0..1 <c>_WindWorld</c> global, spray LAUNCHES at a SPEED proportional to the
    /// wind MAGNITUDE in m/s — a gale flings it hard, a breeze barely lifts it — so it must read the REAL sim
    /// <see cref="EnvironmentSample.WindVector"/> (m/s) through the Core <see cref="GameServices.Environment"/>
    /// accessor, NOT the normalised global. Density reads the sea mood only through that same Core accessor. The
    /// look dims with the global day/night tint (<c>_DayNightTint</c>, read-only) and faintly catches moonlight
    /// so a night gale's spray never blacks out. Per-wisp variation is the deterministic
    /// <see cref="AmbientParticleMath.Hash01(int,int)"/>, never <see cref="System.Random"/>. Drives no sim,
    /// saves nothing — purely cosmetic.</para>
    ///
    /// <para><b>Sorting (the mesh-vs-sprite gotcha — see MEMORY urp2d-mesh-vs-sprite-sorting / the #134
    /// boat-spotlight bug).</b> Unlike the falling rain (which sits well ABOVE everything), spray lives NEAR the
    /// water surface: it should draw just ABOVE the water plane but AROUND/BELOW the boats. A
    /// <see cref="SpriteRenderer"/> does NOT reliably sort against the water <see cref="MeshRenderer"/> by
    /// <c>sortingOrder</c> alone (the mesh falls back to world-z), so the host carries a
    /// <see cref="SortingGroup"/> ("sort as 2D") AND every wisp is nudged a SMALL amount toward the camera so it
    /// clears the water plane — but LESS than the rain's nudge, and at a LOWER sortingOrder, so boats still read
    /// on top. The owner must VERIFY in Unity that the spray reads above the water yet below the boats — a
    /// headless test can't catch mesh-vs-sprite depth.</para>
    ///
    /// <para><b>NOT storm-foam lanes.</b> The surface-locked col.rgb foam STREAKS that ride the crests are a
    /// shader-side field (a later water-shader pass, ADR 0018 B-arc C3) — this emitter is only the airborne torn
    /// spray blown OFF the crests, not the foam painted ON the surface.</para>
    ///
    /// <para><b>Performance (rule 7).</b> One fixed sprite pool (no per-frame allocation), one shared soft puff
    /// sprite + material (batched), sim sampled once per throttled tick, MaxWisps capped, short lifetimes. Cheap
    /// for the 60fps budget and mobile-portable.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SprayEmitter : MonoBehaviour
    {
        [Tooltip("Every knob of the spray — pool, area, the whitecap-gate intensity, launch speed, downwind " +
                 "drift, look, day/night fade. Defaults ship the feature OFF (baseline 0) until you dial it in.")]
        [SerializeField] private SprayConfig _config = SprayConfig.Default;

        [Tooltip("Sorting order — spray draws just ABOVE the water but should read AROUND/BELOW the boats, so " +
                 "this sits LOW (a small positive), below the rain's order. A SortingGroup + a small camera-ward " +
                 "z clear the water MeshRenderer without jumping in front of the boats (see class docs).")]
        [SerializeField] private int _sortingOrder = 5;

        [Tooltip("Metres toward the camera (−z) each wisp is nudged so it clears the water plane. Kept SMALL " +
                 "(smaller than the rain's) so spray stays near the surface, not out in front of the boats.")]
        [SerializeField] private float _cameraZOffset = 0.25f;

        [Tooltip("How often (Hz) the spray ticks (launch + drift + spawn + render). A little slower than rain is " +
                 "plenty for torn puffs — still cheap.")]
        [Min(2f)] [SerializeField] private float _tickHz = 24f;

        // ---- runtime pool (struct-of-fields, recycled, never re-allocated) ----------------------------------
        private struct Wisp
        {
            public bool Alive;
            public Vector2 Pos;
            public Vector2 Vel;     // launch burst velocity (decays); the sustained downwind drift is added live
            public float Age;
            public float Lifetime;
            public float Size;      // per-wisp size scale
            public float Seed;      // per-wisp phase for variation
        }

        private Wisp[] _pool;
        private SpriteRenderer[] _renderers;
        private Sprite _sprite;
        private float _tickTimer;
        private float _spawnCarry;
        private int _spawnCursor;
        private int _spawnCounter;

        // How quickly the launch burst decays (1/s). The initial fling fades over the wisp's short life so the
        // puff bursts up off the crest then hands off to the sustained downwind drift — a torn-then-carried arc.
        private const float LaunchDecayPerSecond = 2.2f;

        // ==== self-install =================================================================================

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("SprayEmitter") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<SprayEmitter>();
        }

        private void Awake()
        {
            // A soft torn puff (point-filtered) — one shared sprite so every wisp batches (rule 7). A high
            // softness reads as a wispy tear of spray rather than a hard dot.
            _sprite = AmbientGlobals.BuildSoftPuff("Spray.Puff", 16, 48, 0.9f);
            // "Sort as 2D": make the sprite pool sort by sortingOrder against 2D content instead of falling back
            // to world-z, so the spray reliably clears the water mesh (the #134 quirk).
            var group = gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = _sortingOrder;
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
            float step = _tickHz > 0f ? 1f / _tickHz : 0.042f;
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

            // Spray reads the REAL sim WindVector (m/s) so its launch speed scales with true wind magnitude —
            // NOT the normalised _WindWorld global. Density reads the sea-state. Core accessor only (rule 4).
            Vector2 windMs = Vector2.zero;
            float seaState01 = 0f;   // glassy default → no spray
            var env = GameServices.Environment;
            if (env != null)
            {
                EnvironmentSample s = env.Sample();
                windMs = s.WindVector;
                seaState01 = s.SeaState01;
            }

            float intensity = AmbientParticleMath.SprayIntensity(
                seaState01, _config.BaselineIntensity, _config.SeaStateWeight, _config.SeaStateThreshold);

            // Wind direction (unit) and magnitude (m/s). Dead calm → no launch direction, so nothing to fling.
            float windMag = windMs.magnitude;
            Vector2 windDir = windMag > 1e-4f ? windMs / windMag : Vector2.zero;

            // --- spawn (rate ∝ intensity, integrated with a carry; recycle round-robin) ---
            float targetRate = _config.MaxWisps / Mathf.Max(0.1f, _config.Lifetime) * Mathf.Clamp01(intensity);
            _spawnCarry += targetRate * dt;
            int toSpawn = Mathf.FloorToInt(_spawnCarry);
            if (toSpawn > 0) _spawnCarry -= toSpawn;
            for (int k = 0; k < toSpawn; k++) Spawn(center, windDir, windMag);

            // --- drift + age + render ---
            float dayOpacity = AmbientParticleMath.DayNightOpacity(brightness, _config.NightFade);
            float moon = AmbientParticleMath.MoonlightCatch(brightness, _config.MoonlightCatch);

            // The sustained downwind carry (m/s) after the launch burst decays — the torn puff streaks off downwind.
            Vector2 drift = windMs * _config.DownwindDrift;
            float decay = Mathf.Exp(-LaunchDecayPerSecond * dt);   // per-tick decay of the launch burst

            for (int i = 0; i < _pool.Length; i++)
            {
                ref Wisp w = ref _pool[i];
                var sr = _renderers[i];
                if (!w.Alive)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }

                // Position: the decaying launch burst plus the sustained downwind drift.
                w.Pos += (w.Vel + drift) * dt;
                w.Vel *= decay;
                w.Age += dt;
                if (w.Age >= w.Lifetime) { w.Alive = false; sr.gameObject.SetActive(false); continue; }

                float life = AmbientParticleMath.Life01(w.Age, w.Lifetime);
                float env01 = AmbientParticleMath.LifeEnvelope(life, _config.FadeIn, _config.FadeOut);
                float alpha = Mathf.Clamp01(_config.MaxAlpha * intensity * env01 * dayOpacity + moon * env01 * 0.5f);

                var t = sr.transform;
                // Small camera-ward nudge so the wisp clears the water plane — LESS than the rain's, so it stays
                // near the surface rather than jumping in front of the boats.
                t.position = new Vector3(w.Pos.x, w.Pos.y, -_cameraZOffset);
                t.localScale = new Vector3(w.Size, w.Size, 1f);
                Color col = _config.Color * tint;        // dim/warm with the day/night look
                col.a = alpha;
                sr.color = col;
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
            }
        }

        private void Spawn(Vector2 center, Vector2 windDir, float windMag)
        {
            // No wind → no crest to blow off → don't launch (a dead-calm sea throws no spray).
            if (windMag <= 1e-4f) return;

            int i = _spawnCursor;
            _spawnCursor = (_spawnCursor + 1) % _pool.Length;

            int salt = _spawnCounter++;
            float hx = AmbientParticleMath.Hash01(salt, 13);
            float hy = AmbientParticleMath.Hash01(salt, 31);
            float hs = AmbientParticleMath.Hash01(salt, 57);
            float hseed = AmbientParticleMath.Hash01(salt, 89);
            float hspread = AmbientParticleMath.Hash01(salt, 71);

            // Spawn anywhere across the field (spray tears off crests all over the visible water).
            Vector2 pos = center + new Vector2(
                (hx * 2f - 1f) * _config.AreaHalfSize.x,
                (hy * 2f - 1f) * _config.AreaHalfSize.y);

            // Launch DOWNWIND at a speed proportional to the wind magnitude (m/s): a gale flings hard, a breeze
            // barely lifts. A small per-wisp spread rotates the launch a touch off the pure downwind so the
            // spray tears rather than firing in a rigid line, and a slight speed jitter varies the reach.
            float speed = windMag * _config.LaunchSpeedPerWind * (0.7f + 0.6f * hs);
            float spreadDeg = (hspread * 2f - 1f) * 20f;   // ±20° tear off the downwind line
            Vector2 launchDir = Rotate(windDir, spreadDeg);
            Vector2 vel = launchDir * speed;

            _pool[i] = new Wisp
            {
                Alive = true,
                Pos = pos,
                Vel = vel,
                Age = 0f,
                Lifetime = Mathf.Max(0.1f, _config.Lifetime),
                Size = Mathf.Max(0.01f, _config.Size * (0.7f + 0.6f * hseed)),
                Seed = hseed,
            };
        }

        /// <summary>Rotate a 2D vector by <paramref name="degrees"/> (deterministic, allocation-free).</summary>
        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
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
