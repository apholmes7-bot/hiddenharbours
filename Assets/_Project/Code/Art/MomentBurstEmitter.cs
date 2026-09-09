using System;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The LOOK of a burst mote — speeds, sizes, tints. The COUNTS and the SECONDS are the owner's in
    /// <c>GameConfig.Juice</c> (rule 6: <c>LandingSplashDrops*</c>, <c>SandChunkCount</c>,
    /// <c>CastRingCount</c>, the <c>*Seconds</c>); this struct is the sprite-side dressing, serialized on
    /// the host exactly as <see cref="WadeSplashConfig"/> is on the wade emitter.
    /// </summary>
    [Serializable]
    public struct MomentBurstConfig
    {
        [Header("Pool")]
        [Tooltip("Fixed pool of motes across all three bursts (rule 7: built once, never grown). A burst that " +
                 "needs more than are free recycles the oldest.")]
        [Min(1)] public int PoolSize;

        [Header("Landing splash")]
        [Tooltip("Outward launch speed (m/s) of a landing droplet.")]
        [Min(0f)] public float SplashSpeed;
        [Tooltip("Extra UP launch (m/s) so the splash crowns rather than spreading flat.")]
        [Min(0f)] public float SplashUp;
        [Tooltip("Droplet size at birth (m).")]
        [Min(0.005f)] public float DropletSize;
        [Tooltip("Splash tint (cool near-white reads as thrown water).")]
        public Color WaterColor;

        [Header("Dig strike — sand")]
        [Tooltip("Outward throw speed (m/s) of a sand chunk.")]
        [Min(0f)] public float ChunkSpeed;
        [Tooltip("UP throw (m/s) of a sand chunk — the shovel lifts before it scatters.")]
        [Min(0f)] public float ChunkUp;
        [Tooltip("Chunk size at birth (m).")]
        [Min(0.005f)] public float ChunkSize;
        [Tooltip("Wet-sand tint.")]
        public Color SandColor;

        [Header("Cast entry — rings")]
        [Tooltip("Ring size (m) at full spread.")]
        [Min(0.01f)] public float RingSize;
        [Tooltip("Ring scale at birth (× RingSize).")]
        [Min(0.01f)] public float RingStartScale;
        [Tooltip("Ring scale at death (× RingSize).")]
        [Min(0.01f)] public float RingEndScale;
        [Tooltip("Fraction of the ring lifetime the rings are staggered across (ring i starts at i/count of it).")]
        [Range(0f, 1f)] public float RingStagger01;

        [Header("Shared")]
        [Tooltip("Downward pull (m/s²) on droplets and chunks.")]
        [Min(0f)] public float Gravity;
        [Tooltip("Peak opacity before the life envelope and day/night scale it.")]
        [Range(0f, 1f)] public float MaxAlpha;
        [Tooltip("Fraction of life fading IN.")]
        [Range(0f, 1f)] public float FadeIn;
        [Tooltip("Fraction of life fading OUT.")]
        [Range(0f, 1f)] public float FadeOut;
        [Tooltip("How strongly night dims the motes (0 = ignore time of day).")]
        [Range(0f, 1f)] public float NightFade;

        public static MomentBurstConfig Default => new MomentBurstConfig
        {
            PoolSize       = 64,
            SplashSpeed    = 1.8f,
            SplashUp       = 1.2f,
            DropletSize    = 0.11f,
            WaterColor     = new Color(0.92f, 0.96f, 0.99f, 1f),
            ChunkSpeed     = 1.1f,
            ChunkUp        = 1.6f,
            ChunkSize      = 0.09f,
            SandColor      = new Color(0.62f, 0.52f, 0.36f, 1f),
            RingSize       = 0.6f,
            RingStartScale = 0.25f,
            RingEndScale   = 1.5f,
            RingStagger01  = 0.33f,
            Gravity        = 4.0f,
            MaxAlpha       = 0.7f,
            FadeIn         = 0.1f,
            FadeOut        = 0.5f,
            NightFade      = 0.35f,
        };
    }

    /// <summary>
    /// <b>The three moments' particles</b> (juice charter §4.1, §4.3): the landing splash sized by the
    /// fish's weight, the sand chunks on the shovel's strike, the rings on the cast line's entry. ONE
    /// self-installing pooled host, ONE subscription (<see cref="JuiceMomentCue"/>), no prefab assets
    /// and no new art — three sprites built once at <c>Awake</c> (a dot, a smaller sand dot, a ring),
    /// a fixed pool of <see cref="SpriteRenderer"/>s under a <see cref="SortingGroup"/>, and a tick at
    /// <c>MomentTickHz</c> on UNSCALED time (the landing hit-stop must not freeze its own splash).
    ///
    /// <para><b>Frame cost.</b> Nothing alive = one integer compare per frame. A burst alive = one loop
    /// over the pool at the tick rate; no allocation after <c>Awake</c>. The counts and the seconds
    /// are <c>GameConfig.Juice</c>'s; the look is <see cref="MomentBurstConfig"/>.</para>
    ///
    /// <para>Sale publishes nothing here — the coins are the notebook's (they fly on the page, not in
    /// the world). Hears the cue only; never a fishing or dig class (rule 4).</para>
    /// </summary>
    public sealed class MomentBurstEmitter : MonoBehaviour
    {
        private enum Kind : byte { Drop, Chunk, Ring }

        private struct Mote
        {
            public bool Alive;
            public Kind Kind;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Age;       // may start NEGATIVE (a staggered ring not yet born)
            public float Lifetime;
            public float Size;
            public float Seed;
        }

        [SerializeField] private MomentBurstConfig _config = MomentBurstConfig.Default;
        [Tooltip("Sorting order of the motes (a small positive: they clear the water mesh and sit under the fisher).")]
        [SerializeField] private int _sortingOrder = 6;
        [Tooltip("Camera-ward z clear (the #134 quirk — a 2D sprite exactly on the water plane can z-fight it).")]
        [SerializeField] private float _cameraZOffset = 0.05f;

        private Sprite _droplet, _chunk, _ring;
        private Mote[] _pool;
        private SpriteRenderer[] _renderers;
        private int _cursor;
        private int _alive;
        private float _tickTimer;
        private bool _built;
        private uint _spawnSalt;
        private JuiceSettings? _override;   // tests pin the settings; production reads GameServices.Config

        private static bool _installed;

        /// <summary>Motes alive right now (tests, tooling).</summary>
        public int AliveCount => _alive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("MomentBurstEmitter") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<MomentBurstEmitter>();
        }

        private JuiceSettings Settings => _override ?? (GameServices.Config != null ? GameServices.Config.Juice : JuiceSettings.Default);

        /// <summary>Tests pin the knobs without a GameConfig asset; null returns to the live config.</summary>
        public void OverrideSettings(JuiceSettings? settings) => _override = settings;

        private void Awake() => EnsureBuilt();

        private void OnEnable()
        {
            _tickTimer = 0f;
            EventBus.Subscribe<JuiceMomentCue>(OnMoment);
        }

        private void OnDisable() => EventBus.Unsubscribe<JuiceMomentCue>(OnMoment);

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            _droplet = AmbientGlobals.BuildDot("MomentBurst.Droplet", 6, 48);
            _chunk = AmbientGlobals.BuildDot("MomentBurst.Chunk", 4, 48);
            _ring = BuildRing("MomentBurst.Ring", 24, 48);
            if (GetComponent<SortingGroup>() == null)
                gameObject.AddComponent<SortingGroup>().sortingOrder = _sortingOrder;

            int n = Mathf.Max(1, _config.PoolSize);
            _pool = new Mote[n];
            _renderers = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("mote");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _droplet;
                sr.sortingOrder = _sortingOrder;
                go.SetActive(false);
                _renderers[i] = sr;
            }
        }

        // ==== the cue ======================================================================================

        /// <summary>Public so tests drive the same path the bus does.</summary>
        public void OnMoment(JuiceMomentCue e)
        {
            JuiceSettings j = Settings;
            if (!j.MomentsEnabled) return;
            EnsureBuilt();
            switch (e.Kind)
            {
                case JuiceMoment.Landing:
                    SpawnSplash(e.At, MomentBurstMath.SplashDrops(e.Strength, j.LandingSplashDropsMin,
                                                                   j.LandingSplashDropsPerKg, j.LandingSplashDropsMax),
                                j.LandingSplashSeconds);
                    break;
                case JuiceMoment.DigStrike:
                    SpawnChunks(e.At, j.SandChunkCount, j.SandChunkSeconds);
                    break;
                case JuiceMoment.CastEntry:
                    SpawnRings(e.At, j.CastRingCount, j.CastRingSeconds);
                    break;
                default:
                    break;   // Sale: the coins are the notebook's
            }
        }

        private void SpawnSplash(Vector2 at, int count, float seconds)
        {
            if (seconds <= 0f) return;
            for (int k = 0; k < count; k++)
            {
                float h1 = AmbientParticleMath.Hash01(++_spawnSalt);
                float h2 = AmbientParticleMath.Hash01(++_spawnSalt);
                float h3 = AmbientParticleMath.Hash01(++_spawnSalt);
                float ang = h1 * Mathf.PI * 2f;
                float spd = _config.SplashSpeed * (0.5f + 0.5f * h2);
                var vel = new Vector2(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd * 0.5f + _config.SplashUp * (0.6f + 0.4f * h3));
                Spawn(Kind.Drop, at, vel, 0f, seconds * (0.7f + 0.3f * h2), _config.DropletSize * (0.8f + 0.4f * h3), h1);
            }
        }

        private void SpawnChunks(Vector2 at, int count, float seconds)
        {
            if (seconds <= 0f) return;
            for (int k = 0; k < count; k++)
            {
                float h1 = AmbientParticleMath.Hash01(++_spawnSalt);
                float h2 = AmbientParticleMath.Hash01(++_spawnSalt);
                float h3 = AmbientParticleMath.Hash01(++_spawnSalt);
                float side = (h1 - 0.5f) * 2f * _config.ChunkSpeed;
                var vel = new Vector2(side, _config.ChunkUp * (0.6f + 0.4f * h2));
                Spawn(Kind.Chunk, at, vel, 0f, seconds * (0.8f + 0.2f * h3), _config.ChunkSize * (0.7f + 0.6f * h2), h1);
            }
        }

        private void SpawnRings(Vector2 at, int count, float seconds)
        {
            if (seconds <= 0f) return;
            for (int k = 0; k < count; k++)
            {
                float delay = MomentBurstMath.RingDelay(k, count, seconds, _config.RingStagger01);
                Spawn(Kind.Ring, at, Vector2.zero, -delay, seconds, _config.RingSize, 0f);
            }
        }

        /// <summary>Round-robin over the fixed pool: the oldest mote is recycled when none is free.</summary>
        private void Spawn(Kind kind, Vector2 pos, Vector2 vel, float age, float lifetime, float size, float seed)
        {
            int n = _pool.Length;
            int i = _cursor;
            for (int tries = 0; tries < n; tries++)
            {
                if (!_pool[i].Alive) break;
                i = (i + 1) % n;
            }
            if (!_pool[i].Alive) _alive++;
            _pool[i] = new Mote { Alive = true, Kind = kind, Pos = pos, Vel = vel, Age = age, Lifetime = lifetime, Size = size, Seed = seed };
            _cursor = (i + 1) % n;
        }

        // ==== the tick =====================================================================================

        private void Update()
        {
            if (_alive == 0) return;                      // the idle cost: one compare
            // UNSCALED: a landing's hit-stop dips Time.timeScale, and the splash it fires must not freeze with the world.
            _tickTimer -= Time.unscaledDeltaTime;
            if (_tickTimer > 0f) return;
            float hz = Mathf.Max(1f, Settings.MomentTickHz);
            float step = 1f / hz;
            _tickTimer = step;
            Tick(step);
        }

        /// <summary>Advance every live mote by <paramref name="dt"/> seconds and draw. Public for tests.</summary>
        public void Tick(float dt)
        {
            if (_pool == null || _alive == 0) return;
            Color tint = AmbientGlobals.DayNightTint;
            float brightness = AmbientParticleMath.DayNightBrightness(tint);
            float dayOpacity = AmbientParticleMath.DayNightOpacity(brightness, _config.NightFade);
            float gravity = _config.Gravity;

            for (int i = 0; i < _pool.Length; i++)
            {
                ref Mote m = ref _pool[i];
                var sr = _renderers[i];
                if (!m.Alive) continue;

                m.Age += dt;
                if (m.Age < 0f) { if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false); continue; }   // staggered, unborn
                if (m.Age >= m.Lifetime)
                {
                    m.Alive = false;
                    _alive--;
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }

                if (m.Kind != Kind.Ring)
                {
                    m.Vel.y -= gravity * dt;
                    m.Pos += m.Vel * dt;
                }

                float life = AmbientParticleMath.Life01(m.Age, m.Lifetime);
                float env01 = AmbientParticleMath.LifeEnvelope(life, _config.FadeIn, _config.FadeOut);
                float alpha = Mathf.Clamp01(_config.MaxAlpha * env01 * dayOpacity);

                float grow = m.Kind == Kind.Ring ? Mathf.Lerp(_config.RingStartScale, _config.RingEndScale, life) : 1f;
                float scale = m.Size * grow;

                Sprite want = m.Kind == Kind.Ring ? _ring : m.Kind == Kind.Chunk ? _chunk : _droplet;
                if (sr.sprite != want) sr.sprite = want;
                var t = sr.transform;
                t.position = new Vector3(m.Pos.x, m.Pos.y, -_cameraZOffset);
                t.localScale = new Vector3(scale, scale, 1f);
                Color col = (m.Kind == Kind.Chunk ? _config.SandColor : _config.WaterColor) * tint;
                col.a = alpha;
                sr.color = col;
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
            }
        }

        /// <summary>Kill every mote (tests / scene teardown).</summary>
        public void Clear()
        {
            if (_pool == null) return;
            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i].Alive = false;
                if (_renderers[i] != null && _renderers[i].gameObject.activeSelf) _renderers[i].gameObject.SetActive(false);
            }
            _alive = 0;
        }

        // ==== sprites ======================================================================================

        /// <summary>A thin ring (an annulus) — the same recipe as the wade ripple, built once.</summary>
        private static Sprite BuildRing(string name, int size, int ppu)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            float c = (size - 1) * 0.5f;
            float radius = c * 0.78f, half = c * 0.16f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Abs(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - radius);
                float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 1f, d / half));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }

    /// <summary>The pure sizing behind the bursts — EditMode-tested on their own.</summary>
    public static class MomentBurstMath
    {
        /// <summary>Droplets for a landing of <paramref name="kg"/>: <c>min + perKg·kg</c>, rounded, capped at <paramref name="max"/>.</summary>
        public static int SplashDrops(float kg, int min, float perKg, int max)
        {
            int n = Mathf.RoundToInt(Mathf.Max(0, min) + Mathf.Max(0f, perKg) * Mathf.Max(0f, kg));
            return Mathf.Clamp(n, 0, Mathf.Max(0, max));
        }

        /// <summary>Seconds before ring <paramref name="index"/> of <paramref name="count"/> is born: the rings are
        /// spread evenly across the first <paramref name="stagger01"/> of <paramref name="lifetime"/>.</summary>
        public static float RingDelay(int index, int count, float lifetime, float stagger01)
        {
            if (count <= 1 || index <= 0) return 0f;
            return lifetime * Mathf.Clamp01(stagger01) * index / (count - 1);
        }
    }
}
