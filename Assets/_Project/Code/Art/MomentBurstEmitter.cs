using System;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The LOOK of a burst mote — speeds, sizes, tints. The COUNTS and the SECONDS are the owner's in
    /// <c>GameConfig.Juice</c> (rule 6: <c>LandingSplashDrops*</c>, <c>SandChunkCount</c>, <c>DigToss*</c>,
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

        [Header("Dig strike — wet dirt")]
        [Tooltip("Outward throw speed (m/s) of a dirt clod across the ground, in any direction from the hole.")]
        [Min(0f)] public float ChunkSpeed;
        [Tooltip("How much of the ground throw shows on screen Y (the ¾ view foreshortens the ground's depth).")]
        [Range(0f, 1f)] public float ChunkGroundYScale;
        [Tooltip("UP throw (m/s) of a dirt clod: it lifts out of the hole, arcs and lands.")]
        [Min(0f)] public float ChunkUp;
        [Tooltip("Downward pull (m/s²) on a clod's height. Heavier than the water's so wet dirt drops, not floats.")]
        [Min(0f)] public float ChunkGravity;
        [Tooltip("Clod size (m) — the sprite is one metre wide, so this is its world size (one art pixel is 1/32 m).")]
        [Min(0.005f)] public float ChunkSize;
        [Tooltip("Darkest wet-dirt tone (the mud out of the bottom of the hole).")]
        public Color DirtDark;
        [Tooltip("Middle wet-dirt tone.")]
        public Color DirtMid;
        [Tooltip("Lightest wet-dirt tone. Still clearly darker than the dry flat (DirtMinContrast).")]
        public Color DirtLight;
        [Tooltip("Peak opacity of a clod (wet dirt is solid; the water's MaxAlpha is not used).")]
        [Range(0f, 1f)] public float ChunkMaxAlpha;
        [Tooltip("Fraction of a clod's life fading OUT at its end, after it has landed (it pops in solid).")]
        [Range(0f, 1f)] public float ChunkFadeOut;

        [Header("Dig toss — off the blade at the lift")]
        [Tooltip("Sideways throw speed (m/s) of a clod tossed off the blade (one side per dig).")]
        [Min(0f)] public float TossSpeed;
        [Tooltip("UP throw (m/s) of a tossed clod.")]
        [Min(0f)] public float TossUp;
        [Tooltip("Height (m) above the hole the blade is at when the toss leaves it.")]
        [Min(0f)] public float TossLiftHeight;
        [Tooltip("Tossed clod size (× ChunkSize).")]
        [Range(0.1f, 1f)] public float TossSizeScale;

        [Header("Dig dirt — legibility guard (EditMode-tested)")]
        [Tooltip("The dry flat the clam beds sit on, as it renders at midday (measured from the game).")]
        public Color FlatReferenceColor;
        [Tooltip("Minimum luminance contrast ratio (WCAG style, (L1+0.05)/(L2+0.05)) every dirt tone must hold " +
                 "against FlatReferenceColor, so the dirt reads at game scale.")]
        [Min(1f)] public float DirtMinContrast;

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
        [Tooltip("Downward pull (m/s²) on landing droplets (dirt clods use ChunkGravity).")]
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
            ChunkGroundYScale = 0.5f,
            ChunkUp        = 1.5f,
            ChunkGravity   = 9.0f,
            ChunkSize      = 0.09f,
            DirtDark       = new Color(0.27f, 0.21f, 0.15f, 1f),
            DirtMid        = new Color(0.36f, 0.28f, 0.20f, 1f),
            DirtLight      = new Color(0.45f, 0.36f, 0.26f, 1f),
            ChunkMaxAlpha  = 1f,
            ChunkFadeOut   = 0.3f,
            TossSpeed      = 2.0f,
            TossUp         = 1.4f,
            TossLiftHeight = 0.25f,
            TossSizeScale  = 0.75f,
            FlatReferenceColor = new Color(0.61f, 0.51f, 0.41f, 1f),
            DirtMinContrast = 1.5f,
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
    /// fish's weight, the wet dirt when the shovel's blade enters the ground (and a smaller toss off the
    /// blade at the lift), the rings on the cast line's entry. ONE
    /// self-installing pooled host, ONE subscription (<see cref="JuiceMomentCue"/>), no prefab assets
    /// and no new art — three sprites built once at <c>Awake</c> (a dot, a clod, a ring),
    /// a fixed pool of <see cref="SpriteRenderer"/>s, each under its own <see cref="SortingGroup"/>, and a tick at
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
            // Dirt clods only: Pos is the point on the ground under the clod, Height its lift above it.
            public float Height;
            public float VelUp;
            public bool Landed;
            public byte Tone;       // 0 dark, 1 mid, 2 light
        }

        [SerializeField] private MomentBurstConfig _config = MomentBurstConfig.Default;
        // ADR 0059 step 2: no fixed order. Each mote carries its own SortingGroup (#134) sorted at the cue's
        // point by the decor band's Y-sort, one rung behind (EffectSorting, SortedEffect.MomentBurst), so it
        // sits under the fisher who stands there and interleaves with decor by position.
        [Tooltip("Camera-ward z clear (the #134 quirk — a 2D sprite exactly on the water plane can z-fight it).")]
        [SerializeField] private float _cameraZOffset = 0.05f;

        private Sprite _droplet, _chunk, _ring;
        private Mote[] _pool;
        private SpriteRenderer[] _renderers;
        private SortingGroup[] _groups;   // one per mote: its order is set once, at spawn
        private int _cursor;
        private int _alive;
        private float _tickTimer;
        private bool _built;
        private uint _spawnSalt;
        private JuiceSettings? _override;   // tests pin the settings; production reads GameServices.Config

        private static bool _installed;

        /// <summary>Texels across the clod sprite: a rounded 4×4 clod (no corners), drawn point-filtered.</summary>
        private const int ChunkSpritePixels = 4;

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
            // One metre wide (pixels = ppu), so ChunkSize is the clod's world size. It used to be 4 px at 48 ppu,
            // which shrank a 0.09 m clod to under a centimetre: the dirt was there and nobody could see it.
            _chunk = AmbientGlobals.BuildDot("MomentBurst.Chunk", ChunkSpritePixels, ChunkSpritePixels);
            _ring = BuildRing("MomentBurst.Ring", 24, 48);
            int n = Mathf.Max(1, _config.PoolSize);
            _pool = new Mote[n];
            _renderers = new SpriteRenderer[n];
            _groups = new SortingGroup[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("mote");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _droplet;
                _groups[i] = go.AddComponent<SortingGroup>();   // "sort as 2D" (#134); ordered at spawn
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
                case JuiceMoment.DigImpact:   // the blade in the ground, not the press (DigStrike)
                    SpawnChunks(e.At, j.SandChunkCount, j.SandChunkSeconds);
                    SpawnToss(e.At, j.DigTossChunkCount, j.DigTossDelaySeconds, j.DigTossChunkSeconds);
                    break;
                case JuiceMoment.DigStrike:   // a new press restarts the clip: an earlier dig's toss never leaves the blade
                    DropUnbornDirt();
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

        /// <summary>The impact: wet clods lift out of the hole in every direction across the ground, arc, land
        /// round it and lie there until they fade.</summary>
        private void SpawnChunks(Vector2 at, int count, float seconds)
        {
            if (seconds <= 0f) return;
            for (int k = 0; k < count; k++)
            {
                float h1 = AmbientParticleMath.Hash01(++_spawnSalt);
                float h2 = AmbientParticleMath.Hash01(++_spawnSalt);
                float h3 = AmbientParticleMath.Hash01(++_spawnSalt);
                float ang = h1 * Mathf.PI * 2f;
                float spd = _config.ChunkSpeed * (0.5f + 0.5f * h2);
                var ground = new Vector2(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd * _config.ChunkGroundYScale);
                int i = Spawn(Kind.Chunk, at, ground, 0f, seconds * (0.85f + 0.15f * h3),
                              _config.ChunkSize * (0.75f + 0.5f * h3), h1);
                _pool[i].VelUp = _config.ChunkUp * (0.6f + 0.4f * h3);
                _pool[i].Tone = MomentBurstMath.DirtTone(h2);
            }
        }

        /// <summary>The lift and toss: a few smaller clods leave the raised blade to ONE side, born
        /// <paramref name="delay"/> seconds after the impact (unborn until then, like a staggered ring).</summary>
        private void SpawnToss(Vector2 at, int count, float delay, float seconds)
        {
            if (seconds <= 0f || count <= 0) return;
            float side = AmbientParticleMath.Hash01(++_spawnSalt) < 0.5f ? -1f : 1f;
            for (int k = 0; k < count; k++)
            {
                float h1 = AmbientParticleMath.Hash01(++_spawnSalt);
                float h2 = AmbientParticleMath.Hash01(++_spawnSalt);
                float h3 = AmbientParticleMath.Hash01(++_spawnSalt);
                var ground = new Vector2(side * _config.TossSpeed * (0.6f + 0.4f * h1),
                                         (h2 - 0.5f) * _config.TossSpeed * _config.ChunkGroundYScale);
                int i = Spawn(Kind.Chunk, at, ground, -Mathf.Max(0f, delay), seconds * (0.85f + 0.15f * h3),
                              _config.ChunkSize * _config.TossSizeScale * (0.8f + 0.4f * h3), h1);
                _pool[i].Height = _config.TossLiftHeight;
                _pool[i].VelUp = _config.TossUp * (0.7f + 0.3f * h3);
                _pool[i].Tone = MomentBurstMath.DirtTone(h2);
            }
        }

        /// <summary>Kill the dirt not yet born (a toss still on the blade). Born clods finish their arc.</summary>
        private void DropUnbornDirt()
        {
            if (_pool == null) return;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Alive || _pool[i].Kind != Kind.Chunk || _pool[i].Age >= 0f) continue;
                _pool[i].Alive = false;
                _alive--;
                if (_renderers[i].gameObject.activeSelf) _renderers[i].gameObject.SetActive(false);
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
        private int Spawn(Kind kind, Vector2 pos, Vector2 vel, float age, float lifetime, float size, float seed)
        {
            int n = _pool.Length;
            int i = _cursor;
            for (int tries = 0; tries < n; tries++)
            {
                if (!_pool[i].Alive) break;
                i = (i + 1) % n;
            }
            if (!_pool[i].Alive) _alive++;
            // ADR 0059 step 2. The dig's dirt sorts at the hole (he stands beside it); the rest at the feet.
            _groups[i].sortingOrder = kind == Kind.Chunk
                ? EffectSorting.OrderAt(SortedEffect.DigDirt, pos.y)
                : EffectSorting.OrderAt(SortedEffect.MomentBurst, pos.y);
            _pool[i] = new Mote { Alive = true, Kind = kind, Pos = pos, Vel = vel, Age = age, Lifetime = lifetime, Size = size, Seed = seed };
            _cursor = (i + 1) % n;
            return i;
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
            float chunkGravity = _config.ChunkGravity;

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

                bool chunk = m.Kind == Kind.Chunk;
                if (chunk)
                {
                    if (!m.Landed)
                    {
                        m.VelUp -= chunkGravity * dt;
                        m.Height += m.VelUp * dt;
                        m.Pos += m.Vel * dt;
                        if (m.Height <= 0f && m.VelUp < 0f) { m.Height = 0f; m.Landed = true; }   // it lands and lies there
                    }
                }
                else if (m.Kind != Kind.Ring)
                {
                    m.Vel.y -= gravity * dt;
                    m.Pos += m.Vel * dt;
                }

                float life = AmbientParticleMath.Life01(m.Age, m.Lifetime);
                float env01 = chunk
                    ? AmbientParticleMath.LifeEnvelope(life, 0f, _config.ChunkFadeOut)
                    : AmbientParticleMath.LifeEnvelope(life, _config.FadeIn, _config.FadeOut);
                float alpha = Mathf.Clamp01((chunk ? _config.ChunkMaxAlpha : _config.MaxAlpha) * env01 * dayOpacity);

                float grow = m.Kind == Kind.Ring ? Mathf.Lerp(_config.RingStartScale, _config.RingEndScale, life) : 1f;
                float scale = m.Size * grow;

                Sprite want = m.Kind == Kind.Ring ? _ring : m.Kind == Kind.Chunk ? _chunk : _droplet;
                if (sr.sprite != want) sr.sprite = want;
                var t = sr.transform;
                t.position = new Vector3(m.Pos.x, m.Pos.y + m.Height, -_cameraZOffset);
                t.localScale = new Vector3(scale, scale, 1f);
                Color col = (chunk ? DirtColor(m.Tone) : _config.WaterColor) * tint;
                col.a = alpha;
                sr.color = col;
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
            }
        }

        private Color DirtColor(byte tone) =>
            tone == 0 ? _config.DirtDark : tone == 1 ? _config.DirtMid : _config.DirtLight;

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

        /// <summary>Which of the three wet-dirt tones a clod drawn with hash <paramref name="h01"/> takes:
        /// 0 dark, 1 mid, 2 light, in equal thirds.</summary>
        public static byte DirtTone(float h01) =>
            (byte)Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(h01) * 3f), 0, 2);

        /// <summary>Relative luminance of a colour (sRGB → linear, Rec. 709 weights), 0..1.</summary>
        public static float RelativeLuminance(Color c)
        {
            Color lin = c.linear;
            return 0.2126f * lin.r + 0.7152f * lin.g + 0.0722f * lin.b;
        }

        /// <summary>The luminance contrast ratio of two colours, <c>(Lhi + 0.05) / (Llo + 0.05)</c>: 1 = identical.</summary>
        public static float ContrastRatio(Color a, Color b)
        {
            float la = RelativeLuminance(a), lb = RelativeLuminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }
    }
}
