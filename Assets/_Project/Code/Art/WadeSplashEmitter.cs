using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Every tunable of the WADE / SWIM SPLASH effect, in one serializable struct so the maths stays free of
    /// magic numbers (rule 6). <see cref="WadeSplashEmitter"/> serializes an owner-editable instance. Kept in
    /// this lane's own file (not the shared <c>AmbientParticleConfig</c>) since the splashes are consumer-of-a-
    /// -signal, not an ambient-mood effect. Defaults are a gentle, readable kick that grows with movement and
    /// bursts on entering the water — the feature is inherently GATED (no splash while Dry).
    /// </summary>
    [System.Serializable]
    public struct WadeSplashConfig
    {
        [Header("Pool")]
        [Tooltip("Max live splash particles (the pool is fixed and recycled — zero per-frame allocation). Rule 7 cap.")]
        [Min(1)] public int MaxSplashes;

        [Header("Ongoing rate (feet-splashes while wading / swimming)")]
        [Tooltip("Splashes/sec while standing STILL in the water (a faint disturbance). Little/no splash at rest.")]
        [Min(0f)] public float IdleRate;
        [Tooltip("Extra splashes/sec at full speed while WADING — the brisk kick as you stride through shallows.")]
        [Min(0f)] public float WadeRatePerSpeed;
        [Tooltip("Extra splashes/sec at full speed while SWIMMING — usually GENTLER than wading (heavier, slower).")]
        [Min(0f)] public float SwimRatePerSpeed;
        [Tooltip("Move speed (m/s) at which the speed-driven rate is fully on (saturates).")]
        [Min(0.05f)] public float SpeedForFullRate;
        [Tooltip("Hard cap on the ongoing splash rate (splashes/sec) so a fast wade can't flood the pool (rule 7).")]
        [Min(0f)] public float MaxRate;

        [Header("Entering the water (one-shot burst)")]
        [Tooltip("Droplets/rings in the burst when stepping Dry→Wade — the first splash off the sandbar.")]
        [Min(0)] public int WadeEntryBurst;
        [Tooltip("Droplets/rings in the burst when going Wade→Swim — BIGGER than the wade burst (footing flooded).")]
        [Min(0)] public int SwimEntryBurst;
        [Tooltip("Fraction of an ENTRY burst that is ring-ripples vs droplets (0..1).")]
        [Range(0f, 1f)] public float EntryRingShare;
        [Tooltip("Outward launch speed (m/s) of burst droplets — how wide the entry splash crowns.")]
        [Min(0f)] public float BurstSpeed;
        [Tooltip("Extra UP launch (m/s) added to burst droplets so the entry splash crowns rather than spreading flat.")]
        [Min(0f)] public float BurstUp;

        [Header("Feet-splash motion")]
        [Tooltip("Metres the ongoing splashes scatter around the feet so they don't all fire from one pixel.")]
        [Min(0f)] public float FeetScatter;
        [Tooltip("Fraction of ongoing feet-splashes that are ring-ripples vs droplets (0..1).")]
        [Range(0f, 1f)] public float FeetRingShare;
        [Tooltip("UP kick (m/s) of a WADE droplet — a brisk stride throws water up.")]
        [Min(0f)] public float WadeKick;
        [Tooltip("UP kick (m/s) of a SWIM droplet — gentler/lazier than a wade kick (heavier water).")]
        [Min(0f)] public float SwimKick;
        [Tooltip("Forward spray (m/s) thrown in the MOVE direction — a little spray ahead of the stride.")]
        [Min(0f)] public float ForwardSpray;
        [Tooltip("Sideways spray (m/s) jitter so droplets fan out rather than firing in a rigid line.")]
        [Min(0f)] public float SideSpray;
        [Tooltip("Downward pull (m/s²) on droplets so a kicked-up drop arcs and falls back to the surface.")]
        [Min(0f)] public float Gravity;

        [Header("Look — droplet")]
        [Tooltip("Droplet size at birth (m). Small white pixel-art specks.")]
        [Min(0.005f)] public float DropletSize;
        [Tooltip("Seconds a droplet lives (a short kicked-up arc).")]
        [Min(0.05f)] public float DropletLifetime;

        [Header("Look — ring ripple")]
        [Tooltip("Ring size at birth (m) before it grows.")]
        [Min(0.01f)] public float RingSize;
        [Tooltip("Seconds a ring-ripple lives as it spreads and fades.")]
        [Min(0.05f)] public float RingLifetime;
        [Tooltip("Ring scale at birth (× RingSize) — starts small.")]
        [Min(0.01f)] public float RingStartScale;
        [Tooltip("Ring scale at death (× RingSize) — the ripple has spread out to this by the end of its life.")]
        [Min(0.01f)] public float RingEndScale;

        [Header("Look — shared")]
        [Tooltip("Bigger splashes entering/while SWIMMING vs wading (× size) — a swim reads as a bigger disturbance.")]
        [Min(1f)] public float SwimSizeScale;
        [Tooltip("Splash tint. A cool near-white reads as kicked-up water; alpha is driven by life + day/night.")]
        public Color Color;
        [Tooltip("Peak opacity (0..1) before the life envelope + day/night scale it.")]
        [Range(0f, 1f)] public float MaxAlpha;
        [Tooltip("Fraction of life spent fading IN (0..1) — splashes burst in fast.")]
        [Range(0f, 1f)] public float FadeIn;
        [Tooltip("Fraction of life spent fading OUT (0..1) — splashes settle back down.")]
        [Range(0f, 1f)] public float FadeOut;

        [Header("Day / night")]
        [Tooltip("How strongly night dims the splashes (0 = ignore time of day, 1 = tracks the light exactly).")]
        [Range(0f, 1f)] public float NightFade;
        [Tooltip("How faintly the splashes catch MOONLIGHT at night so a night wade never blacks out (0..1 floor).")]
        [Range(0f, 1f)] public float MoonlightCatch;

        public static WadeSplashConfig Default => new WadeSplashConfig
        {
            MaxSplashes      = 48,
            IdleRate         = 1.5f,
            WadeRatePerSpeed = 14f,
            SwimRatePerSpeed = 7f,
            SpeedForFullRate = 2.0f,
            MaxRate          = 24f,
            WadeEntryBurst   = 8,
            SwimEntryBurst   = 16,
            EntryRingShare   = 0.35f,
            BurstSpeed       = 1.6f,
            BurstUp          = 0.8f,
            FeetScatter      = 0.12f,
            FeetRingShare    = 0.3f,
            WadeKick         = 1.2f,
            SwimKick         = 0.7f,
            ForwardSpray     = 0.6f,
            SideSpray        = 0.4f,
            Gravity          = 3.0f,
            DropletSize      = 0.11f,
            DropletLifetime  = 0.55f,
            RingSize         = 0.5f,
            RingLifetime     = 0.7f,
            RingStartScale   = 0.3f,
            RingEndScale     = 1.4f,
            SwimSizeScale    = 1.5f,
            Color            = new Color(0.92f, 0.96f, 0.99f, 1f),
            MaxAlpha         = 0.6f,
            FadeIn           = 0.12f,
            FadeOut          = 0.5f,
            NightFade        = 0.35f,
            MoonlightCatch   = 0.12f,
        };
    }

    /// <summary>
    /// SELF-INSTALLING WADE / SWIM SPLASHES — the water REACTS as the on-foot player walks through it. When the
    /// fisher steps off the drying sandbar into the shallows a splash bursts up; while they wade or swim, little
    /// droplets and ring-ripples kick up at their feet, faster the faster they move and bigger/slower once they
    /// are swimming (Pillars 1 &amp; 5: the sea has moods, cozy but with teeth — wading out on a falling tide feels
    /// physical, and the swim band reads as "you are in the water now, head back").
    ///
    /// <para><b>Consumes the wading signal — drives no gameplay (verified, rules 4 &amp; 5).</b> The on-foot wade
    /// model (#163) publishes <see cref="OnFootWaterStateChanged"/> on the Core <see cref="EventBus"/> (carrying
    /// the new/previous <see cref="OnFootWaterState"/>, a <c>Deepening</c> flag, and the water <c>Depth</c>).
    /// This emitter SUBSCRIBES to that signal and, on a DEEPENING transition (Dry→Wade, Wade→Swim), fires a
    /// one-shot splash BURST — a bigger burst entering Swim than Wade. It also tracks the current band from the
    /// signal so it knows when to keep the ongoing feet-splashes going. It writes nothing back, saves nothing,
    /// and never touches the gameplay emitter — purely cosmetic (art-only), like <see cref="RainEmitter"/> /
    /// <see cref="SprayEmitter"/>.</para>
    ///
    /// <para><b>Where the player is (seam discipline, rule 4).</b> The Art module must not reference the Player
    /// module's concrete controller. For the ongoing feet-splashes the emitter needs the player's live position
    /// and move speed each tick, so it locates the persistent on-foot player GameObject by NAME
    /// (<c>GameObject.Find("Player")</c>, cached) — the exact seam-free pattern the Economy lane's
    /// <c>StallReach</c> already uses. Speed is a simple frame-to-frame position delta (no controller field
    /// read). If the player can't be found (e.g. a menu scene) the ongoing splashes simply don't emit.</para>
    ///
    /// <para><b>Self-installing (mirrors <see cref="SprayEmitter"/>).</b> A
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns ONE hidden <c>[DontDestroyOnLoad]</c> host before the
    /// first scene, so wade splashes work in EVERY scene with no wiring. The pool is fixed and camera-agnostic
    /// (splashes happen AT THE PLAYER, wherever they are), capped for the 60fps budget (rule 7).</para>
    ///
    /// <para><b>Sorting (the mesh-vs-sprite gotcha — see MEMORY urp2d-mesh-vs-sprite-sorting / the #134
    /// boat-spotlight bug).</b> Splashes live at the water surface around the wading player: they should draw
    /// just ABOVE the water plane but AROUND/BELOW the player sprite (like spray, not out in front). A
    /// <see cref="SpriteRenderer"/> does NOT reliably sort against the water <see cref="MeshRenderer"/> by
    /// <c>sortingOrder</c> alone (the mesh falls back to world-z), so the host carries a <see cref="SortingGroup"/>
    /// ("sort as 2D") AND every splash is nudged a SMALL amount toward the camera to clear the water plane — the
    /// same small nudge the spray uses, at a low sortingOrder, so the player still reads on top. THE OWNER MUST
    /// VERIFY in Unity that the splashes read above the water yet below/around the player — a headless test can't
    /// catch mesh-vs-sprite depth.</para>
    ///
    /// <para><b>Determinism &amp; budget (rules 5 &amp; 7).</b> Per-splash variation is the deterministic
    /// <see cref="AmbientParticleMath.Hash01(int,int)"/>, never <see cref="System.Random"/>. One fixed pool (no
    /// per-frame allocation), two shared sprites (a droplet + a ring, batched), a throttled tick, MaxSplashes
    /// capped, short lifetimes. The look dims with the global day/night tint and faintly catches moonlight so a
    /// night wade never blacks out. The feature is inherently GATED — no splash at all while the player is Dry.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WadeSplashEmitter : MonoBehaviour
    {
        [Tooltip("Every knob of the wade/swim splashes — pool, per-band splash rates, entry-burst sizes, " +
                 "droplet/ring look, forward spray, day/night fade. The feature is gated (no splash when Dry).")]
        [SerializeField] private WadeSplashConfig _config = WadeSplashConfig.Default;

        [Tooltip("Sorting order — splashes draw just ABOVE the water but should read AROUND/BELOW the player, so " +
                 "this sits LOW (a small positive), like the spray. A SortingGroup + a small camera-ward z clear " +
                 "the water MeshRenderer without jumping in front of the player (see class docs; #134).")]
        [SerializeField] private int _sortingOrder = 6;

        [Tooltip("Metres toward the camera (−z) each splash is nudged so it clears the water plane. Kept SMALL " +
                 "(like the spray's) so splashes stay near the surface, not out in front of the player.")]
        [SerializeField] private float _cameraZOffset = 0.25f;

        [Tooltip("How often (Hz) the splash system ticks (spawn + rise/fall + render). A modest rate is plenty " +
                 "for feet-splashes — still cheap.")]
        [Min(2f)] [SerializeField] private float _tickHz = 24f;

        [Tooltip("The persistent on-foot player GameObject name, located by Find (cached). Matches the name the " +
                 "persistent-core builder gives the player, like the Economy lane's StallReach.")]
        [SerializeField] private string _playerObjectName = "Player";

        // ---- runtime pool (struct-of-fields, recycled, never re-allocated) ----------------------------------
        private struct Splash
        {
            public bool Alive;
            public Vector2 Pos;
            public Vector2 Vel;      // launch burst velocity (decays); gravity-ish pull is added live
            public float Age;
            public float Lifetime;
            public float Size;       // per-splash size scale
            public float Seed;       // per-splash phase for variation
            public bool IsRing;      // ring-ripple (flat, grows) vs droplet (arcs up, gravity)
        }

        private Splash[] _pool;
        private SpriteRenderer[] _renderers;
        private Sprite _droplet;
        private Sprite _ring;
        private float _tickTimer;
        private float _spawnCarry;
        private int _spawnCursor;
        private int _spawnCounter;

        // Current on-foot water band, tracked from the wading signal (so ongoing feet-splashes know when to run).
        private OnFootWaterState _state = OnFootWaterState.Dry;

        // Cached player transform + last position (for the frame-to-frame speed that scales the splash rate).
        private Transform _cachedPlayer;
        private Vector2 _prevPlayerPos;
        private bool _prevPlayerValid;

        // How quickly a droplet's launch burst decays (1/s) — the kicked-up water arcs then falls back.
        private const float LaunchDecayPerSecond = 1.8f;

        // ==== self-install =================================================================================

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("WadeSplashEmitter") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<WadeSplashEmitter>();
        }

        private void Awake()
        {
            // Two shared point-filtered sprites so every splash batches (rule 7): a small crisp droplet and a
            // thin ring-ripple (the widening ring where a foot breaks the surface).
            _droplet = AmbientGlobals.BuildDot("WadeSplash.Droplet", 6, 48);
            _ring = BuildRing("WadeSplash.Ring", 24, 48);
            // "Sort as 2D": make the sprite pool sort by sortingOrder against 2D content instead of falling back
            // to world-z, so the splashes reliably clear the water mesh (the #134 quirk).
            var group = gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = _sortingOrder;
            BuildPool();
        }

        private void OnEnable()
        {
            _tickTimer = 0f;
            _prevPlayerValid = false;
            EventBus.Subscribe<OnFootWaterStateChanged>(OnWaterStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OnFootWaterStateChanged>(OnWaterStateChanged);
        }

        private void BuildPool()
        {
            int n = Mathf.Max(1, _config.MaxSplashes);
            _pool = new Splash[n];
            _renderers = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("splash");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _droplet;
                sr.sortingOrder = _sortingOrder;
                go.SetActive(false);
                _renderers[i] = sr;
            }
        }

        // ==== the wading signal → track the band + burst on entering wade/swim ============================

        private void OnWaterStateChanged(OnFootWaterStateChanged e)
        {
            _state = e.State;
            // Only a DEEPENING transition into the water (Dry→Wade, Wade→Swim) bursts a splash — a return toward
            // shore doesn't kick water up. A bigger, wetter burst entering Swim than the first wade step.
            if (!e.Deepening) return;
            if (e.State != OnFootWaterState.Wade && e.State != OnFootWaterState.Swim) return;

            Vector2 at = ResolvePlayerPos(out bool have);
            if (!have) return;

            int burst = e.State == OnFootWaterState.Swim ? _config.SwimEntryBurst : _config.WadeEntryBurst;
            float sizeScale = e.State == OnFootWaterState.Swim ? _config.SwimSizeScale : 1f;
            EmitBurst(at, Vector2.zero, burst, sizeScale, ringShare: _config.EntryRingShare);
        }

        // ==== tick: ongoing feet-splashes while wading / swimming =========================================

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
            // --- shared day/night signals (read-only) ---
            Color tint = AmbientGlobals.DayNightTint;
            float brightness = AmbientParticleMath.DayNightBrightness(tint);
            float dayOpacity = AmbientParticleMath.DayNightOpacity(brightness, _config.NightFade);
            float moon = AmbientParticleMath.MoonlightCatch(brightness, _config.MoonlightCatch);

            // --- player position + this-tick move speed (the ongoing-splash driver) ---
            Vector2 playerPos = ResolvePlayerPos(out bool havePlayer);
            Vector2 moveDir = Vector2.zero;
            float speed = 0f;
            if (havePlayer)
            {
                if (_prevPlayerValid && dt > 1e-5f)
                {
                    Vector2 delta = playerPos - _prevPlayerPos;
                    speed = delta.magnitude / dt;
                    if (delta.sqrMagnitude > 1e-8f) moveDir = delta.normalized;
                }
                _prevPlayerPos = playerPos;
                _prevPlayerValid = true;
            }
            else
            {
                _prevPlayerValid = false;
            }

            // --- ongoing spawn: rate is a pure function of the band + move speed (little/none when still) ---
            if (havePlayer && (_state == OnFootWaterState.Wade || _state == OnFootWaterState.Swim))
            {
                float rate = WadeSplashMath.SplashRate(
                    _state, speed, _config.IdleRate, _config.WadeRatePerSpeed, _config.SwimRatePerSpeed,
                    _config.SpeedForFullRate, _config.MaxRate);
                _spawnCarry += rate * dt;
                int toSpawn = Mathf.FloorToInt(_spawnCarry);
                if (toSpawn > 0) _spawnCarry -= toSpawn;

                bool swimming = _state == OnFootWaterState.Swim;
                float sizeScale = swimming ? _config.SwimSizeScale : 1f;
                // Swim splashes launch a touch slower/lazier than a brisk wade kick (heavier, wetter).
                float kick = swimming ? _config.SwimKick : _config.WadeKick;
                for (int k = 0; k < toSpawn; k++)
                    SpawnFeet(playerPos, moveDir, sizeScale, kick);
            }
            else
            {
                // Not in the water → let the carry bleed off so a fresh entry doesn't dump a backlog.
                _spawnCarry = 0f;
            }

            // --- rise/fall + age + render ---
            float gravity = _config.Gravity;
            float decay = Mathf.Exp(-LaunchDecayPerSecond * dt);
            for (int i = 0; i < _pool.Length; i++)
            {
                ref Splash s = ref _pool[i];
                var sr = _renderers[i];
                if (!s.Alive)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }

                if (s.IsRing)
                {
                    // A ring-ripple stays flat on the surface and just spreads — no launch/gravity.
                    s.Vel *= decay;
                    s.Pos += s.Vel * dt;
                }
                else
                {
                    // A droplet arcs up on its launch burst then falls back under a gentle gravity.
                    s.Vel.y -= gravity * dt;
                    s.Pos += s.Vel * dt;
                }
                s.Age += dt;
                if (s.Age >= s.Lifetime) { s.Alive = false; sr.gameObject.SetActive(false); continue; }

                float life = AmbientParticleMath.Life01(s.Age, s.Lifetime);
                float env01 = AmbientParticleMath.LifeEnvelope(life, _config.FadeIn, _config.FadeOut);
                float alpha = Mathf.Clamp01(_config.MaxAlpha * env01 * dayOpacity + moon * env01 * 0.5f);

                // A ring grows over its life (the ripple widening); a droplet holds its size.
                float grow = s.IsRing ? Mathf.Lerp(_config.RingStartScale, _config.RingEndScale, life) : 1f;
                float scale = s.Size * grow;

                var t = sr.transform;
                t.position = new Vector3(s.Pos.x, s.Pos.y, -_cameraZOffset);
                t.localScale = new Vector3(scale, scale, 1f);
                if (sr.sprite != (s.IsRing ? _ring : _droplet)) sr.sprite = s.IsRing ? _ring : _droplet;
                Color col = _config.Color * tint;      // dim/warm with the day/night look
                col.a = alpha;
                sr.color = col;
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
            }
        }

        // ==== spawning ====================================================================================

        /// <summary>One ongoing feet-splash: a droplet kicked up at the player's feet with a touch of forward
        /// spray in the move direction, plus an occasional ring-ripple. Deterministic per-splash variation.</summary>
        private void SpawnFeet(Vector2 feet, Vector2 moveDir, float sizeScale, float kick)
        {
            int salt = _spawnCounter++;
            float hx = AmbientParticleMath.Hash01(salt, 13);
            float hy = AmbientParticleMath.Hash01(salt, 31);
            float hs = AmbientParticleMath.Hash01(salt, 57);
            float hseed = AmbientParticleMath.Hash01(salt, 89);
            float hring = AmbientParticleMath.Hash01(salt, 71);

            // Scatter the origin a little around the feet so splashes don't all fire from one pixel.
            Vector2 pos = feet + new Vector2((hx * 2f - 1f), (hy * 2f - 1f)) * _config.FeetScatter;

            bool ring = hring < Mathf.Clamp01(_config.FeetRingShare);
            if (ring)
            {
                // A flat ring-ripple that spreads a touch downwind of travel.
                Vector2 drift = moveDir * (_config.ForwardSpray * 0.3f);
                Emit(pos, drift, isRing: true, sizeScale, hs, hseed);
                return;
            }

            // A droplet: kicked UP with a forward bias in the move direction (spray ahead of the stride).
            float up = kick * (0.7f + 0.6f * hs);
            Vector2 fwd = moveDir * (_config.ForwardSpray * (0.5f + hs));
            Vector2 vel = new Vector2(fwd.x + (hx * 2f - 1f) * _config.SideSpray, up + fwd.y);
            Emit(pos, vel, isRing: false, sizeScale, hs, hseed);
        }

        /// <summary>A one-shot burst of <paramref name="count"/> splashes at <paramref name="at"/> (entering
        /// wade/swim). A share are rings; the rest are droplets thrown outward in all directions.</summary>
        private void EmitBurst(Vector2 at, Vector2 baseVel, int count, float sizeScale, float ringShare)
        {
            int n = Mathf.Max(0, count);
            for (int k = 0; k < n; k++)
            {
                int salt = _spawnCounter++;
                float ha = AmbientParticleMath.Hash01(salt, 17);
                float hsp = AmbientParticleMath.Hash01(salt, 41);
                float hs = AmbientParticleMath.Hash01(salt, 57);
                float hseed = AmbientParticleMath.Hash01(salt, 89);
                float hring = AmbientParticleMath.Hash01(salt, 71);

                if (hring < Mathf.Clamp01(ringShare))
                {
                    Emit(at, baseVel, isRing: true, sizeScale, hs, hseed);
                    continue;
                }
                // Throw droplets outward in a ring, biased UP so the burst crowns.
                float ang = ha * Mathf.PI * 2f;
                float sp = _config.BurstSpeed * (0.6f + 0.8f * hsp);
                Vector2 vel = new Vector2(Mathf.Cos(ang) * sp, Mathf.Abs(Mathf.Sin(ang)) * sp + _config.BurstUp);
                Emit(at, baseVel + vel, isRing: false, sizeScale, hs, hseed);
            }
        }

        /// <summary>Place one splash into the recycled pool (round-robin), ring or droplet.</summary>
        private void Emit(Vector2 pos, Vector2 vel, bool isRing, float sizeScale, float sizeHash, float seed)
        {
            int i = _spawnCursor;
            _spawnCursor = (_spawnCursor + 1) % _pool.Length;

            float baseSize = isRing ? _config.RingSize : _config.DropletSize;
            float life = isRing ? _config.RingLifetime : _config.DropletLifetime;

            _pool[i] = new Splash
            {
                Alive = true,
                Pos = pos,
                Vel = vel,
                Age = 0f,
                Lifetime = Mathf.Max(0.05f, life),
                Size = Mathf.Max(0.01f, baseSize * sizeScale * (0.7f + 0.6f * sizeHash)),
                Seed = seed,
                IsRing = isRing,
            };
        }

        // ==== locate the player (seam-free, like Economy's StallReach) ====================================

        private Vector2 ResolvePlayerPos(out bool have)
        {
            Transform p = ResolvePlayer();
            if (p == null) { have = false; return Vector2.zero; }
            have = true;
            return p.position;
        }

        private Transform ResolvePlayer()
        {
            if (_cachedPlayer != null) return _cachedPlayer;
            if (!string.IsNullOrEmpty(_playerObjectName))
            {
                var go = GameObject.Find(_playerObjectName);
                if (go != null) _cachedPlayer = go.transform;
            }
            return _cachedPlayer;
        }

        // ==== procedural ring-ripple sprite (thin hollow circle; one shared instance → batched) ============

        /// <summary>
        /// A thin hollow ring (the ripple where a foot breaks the surface): a bright annulus feathered on both
        /// edges on a point-filtered texture so it stays crisp pixel-art when scaled. One shared instance → every
        /// ring batches (rule 7). Kept local to this lane (no shared ring builder exists).
        /// </summary>
        private static Sprite BuildRing(string name, int size, int ppu)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            float c = (size - 1) * 0.5f;
            float r = size * 0.5f;
            // The ring sits near the rim: peak alpha at ~0.78 of the radius, feathering to nothing either side.
            const float ringCenter = 0.78f;
            const float ringHalfWidth = 0.16f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / r, dy = (y - c) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);          // 0 centre .. 1 edge
                float off = Mathf.Abs(d - ringCenter) / ringHalfWidth;
                float a = Mathf.Clamp01(1f - off);                // bright on the ring, 0 off it
                a = a * a * (3f - 2f * a);                         // smoothstep for a soft annulus
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
        }
    }

    /// <summary>
    /// The PURE, deterministic maths behind the wade/swim splashes — the ongoing feet-splash SPAWN RATE as a
    /// function of the player's water band and move speed. Split out (like <see cref="AmbientParticleMath"/>) so
    /// the rate curve is EditMode-testable headless (rule 5) and the MonoBehaviour shell stays thin. No RNG,
    /// no side effects, no <c>Time</c> — a pure function of its inputs.
    /// </summary>
    public static class WadeSplashMath
    {
        /// <summary>
        /// Splashes per second at the player's feet, given their <paramref name="state"/> and this-tick move
        /// <paramref name="speed"/> (m/s):
        /// <list type="bullet">
        /// <item><description><see cref="OnFootWaterState.Dry"/> → <b>0</b> (the feature is gated: no splash on
        /// dry ground, whatever the speed).</description></item>
        /// <item><description><see cref="OnFootWaterState.Wade"/> / <see cref="OnFootWaterState.Swim"/> →
        /// <c>idleRate</c> (a faint disturbance even standing still in the water) plus a term that rises with
        /// speed: the speed is normalised against <paramref name="speedForFull"/> and saturated, then scaled by
        /// the band's per-speed weight (<paramref name="wadeRatePerSpeed"/> for a brisk wade,
        /// <paramref name="swimRatePerSpeed"/> — usually gentler — for the heavier swim).</description></item>
        /// </list>
        /// The result is clamped to <paramref name="maxRate"/> so a fast wade can't flood the pool (rule 7).
        /// Monotonic non-decreasing in speed within a band; 0 when Dry; deterministic. Negative inputs are
        /// clamped, never propagated.
        /// </summary>
        /// <param name="state">The on-foot water band. Dry gates the whole effect to 0.</param>
        /// <param name="speed">This tick's move speed (m/s). Little/none when standing still.</param>
        /// <param name="idleRate">Splash rate at zero speed while in the water (a faint standing disturbance).</param>
        /// <param name="wadeRatePerSpeed">Extra splashes/sec at full speed while WADING (the brisk kick).</param>
        /// <param name="swimRatePerSpeed">Extra splashes/sec at full speed while SWIMMING (usually gentler).</param>
        /// <param name="speedForFull">Move speed (m/s) at which the speed term is fully on (saturates).</param>
        /// <param name="maxRate">Hard cap on the rate (rule 7 budget).</param>
        public static float SplashRate(OnFootWaterState state, float speed,
                                       float idleRate, float wadeRatePerSpeed, float swimRatePerSpeed,
                                       float speedForFull, float maxRate)
        {
            if (state == OnFootWaterState.Dry) return 0f;   // gated — no splash on dry ground

            float sp = Mathf.Clamp01(Mathf.Max(0f, speed) / Mathf.Max(1e-3f, speedForFull));
            float perSpeed = state == OnFootWaterState.Swim
                ? Mathf.Max(0f, swimRatePerSpeed)
                : Mathf.Max(0f, wadeRatePerSpeed);

            float rate = Mathf.Max(0f, idleRate) + perSpeed * sp;
            return Mathf.Clamp(rate, 0f, Mathf.Max(0f, maxRate));
        }
    }
}
