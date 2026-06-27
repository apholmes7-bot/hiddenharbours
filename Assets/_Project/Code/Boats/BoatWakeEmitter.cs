using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The self-installing driver for the boat WAKE — a pooled foam-particle trail that realises the owner's
    /// brief in full: "the wake animation [should] follow the boat, it needs to travel with the current as the
    /// waves distort it, once it loses force a distance from the boat it dissipates." The actual feel-math lives
    /// in the pure, EditMode-tested <see cref="WakeParticleSystem"/>; this MonoBehaviour is the thin Unity shell
    /// that finds the boats, reads the deterministic sim through Core, ticks the particle systems, and draws the
    /// foam through a fixed pool of <see cref="SpriteRenderer"/>s.
    ///
    /// <para><b>Why pooled particles (not a TrailRenderer).</b> A boat-locked TrailRenderer is rigidly tied to
    /// the hull — it cannot <i>advect with the current</i> independently of the boat, nor <i>dissipate</i> on its
    /// own once shed. Foam puffs that are shed and then live their own life are the only architecture that
    /// satisfies all four brief points at once.</para>
    ///
    /// <para><b>Self-installing (mirrors <c>GrassWindBridge</c> / the audio director).</b> A
    /// <see cref="RuntimeInitializeOnLoadMethod"/> spawns ONE hidden <c>[DontDestroyOnLoad]</c> host before the
    /// first scene, so the owner needs NO builder change and NO builder re-run — sail any boat and it leaves a
    /// wake. The host discovers <see cref="BoatController"/>s on a throttled scan and gives each a per-boat rig
    /// (its own <see cref="WakeParticleSystem"/> + a slice of the shared sprite pool).</para>
    ///
    /// <para><b>Seam discipline (rule 4) &amp; determinism (rule 5).</b> Reads the boat through its public
    /// surface (<see cref="BoatController.Velocity"/>, <see cref="BoatController.IsAground"/>, bow =
    /// <c>transform.up</c>) and the sea ONLY through the Core <see cref="GameServices.Environment"/> accessor
    /// (the same <see cref="EnvironmentSample.CurrentVector"/> / <see cref="EnvironmentSample.SeaState"/> the
    /// water shader reads, so wake + water move together) — never the Environment concrete classes. It drives no
    /// simulation and saves nothing; the per-puff jitter is a deterministic hash, not <see cref="System.Random"/>.
    /// <b>Performance (rule 7):</b> one fixed sprite pool (no per-frame allocation), one shared material/sprite
    /// (batched), sim sampled once per throttled tick. Mobile-portable.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoatWakeEmitter : MonoBehaviour
    {
        [Header("Wake feel (all tunable — no magic numbers, rule 6)")]
        [Tooltip("Every knob of the wake — shed-rate, V angle, lifetime, fade/spread, current-advect, decay, " +
                 "wave-distort, foam size. Tune to taste; defaults are a lively inshore greybox wake.")]
        [SerializeField] private WakeConfig _config = WakeConfig.Default;

        [Header("Pool & render")]
        [Tooltip("Max live foam puffs PER BOAT. The pool is fixed and recycled — zero per-frame allocation.")]
        [Min(8)] [SerializeField] private int _poolPerBoat = 96;
        [Tooltip("How many boats to drive at once (each gets its own pool). One active player boat dominates; " +
                 "spare slots cover NPC traffic when it arrives.")]
        [Min(1)] [SerializeField] private int _maxBoats = 4;
        [Tooltip("Foam tint. White-ish reads as sea-foam over the blue water; the alpha is driven per-puff by the fade.")]
        [SerializeField] private Color _foamColor = new Color(0.92f, 0.96f, 1f, 1f);
        [Tooltip("Sorting layer name for the foam sprites (leave blank for the default layer).")]
        [SerializeField] private string _sortingLayer = "";
        [Tooltip("Order in the sorting layer. Foam should sit ABOVE the water plane but BELOW the boat hull.")]
        [SerializeField] private int _sortingOrder = -1;

        [Header("Cadence")]
        [Tooltip("How often (Hz) the wake sim ticks (emit + advect + render). The sea is slow; the boat moves " +
                 "smoothly — a handful of Hz reads fine and stays cheap. Matches the water's refresh idiom.")]
        [Min(5f)] [SerializeField] private float _tickHz = 30f;
        [Tooltip("How often (Hz) to re-scan the scene for boats (cheap; boats don't appear often).")]
        [Min(0.25f)] [SerializeField] private float _rescanHz = 1f;

        // ---- runtime ----------------------------------------------------------------------------------------
        private readonly List<WakeRig> _rigs = new();
        private Sprite _foamSprite;
        private float _tickTimer;
        private float _rescanTimer;

        // ==== self-install (one hidden persistent host, like GrassWindBridge) ==============================

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("BoatWakeEmitter") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<BoatWakeEmitter>();
        }

        private void Awake()
        {
            _foamSprite = BuildFoamSprite();
        }

        private void OnEnable()
        {
            _tickTimer = 0f;
            _rescanTimer = 0f;
            Rescan();
        }

        private void OnDisable()
        {
            foreach (var rig in _rigs) rig.Dispose();
            _rigs.Clear();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            _rescanTimer -= dt;
            if (_rescanTimer <= 0f)
            {
                _rescanTimer = _rescanHz > 0f ? 1f / _rescanHz : 1f;
                Rescan();
            }

            _tickTimer -= dt;
            if (_tickTimer > 0f) return;
            float step = _tickHz > 0f ? 1f / _tickHz : 0.033f;
            // Use the throttle interval as the integration dt so feel is independent of frame rate.
            _tickTimer = step;
            Tick(step);
        }

        /// <summary>
        /// One wake tick across every live rig: read the sim once (the current advects the foam, the sea-state
        /// roughness distorts it), then for each boat emit from the stern at its speed and advance + render its
        /// pool. The sim read is shared by all boats (one active region) — cheap.
        /// </summary>
        private void Tick(float dt)
        {
            Vector2 current = Vector2.zero;
            float roughness = 0f;
            var env = GameServices.Environment;
            if (env != null)
            {
                EnvironmentSample s = env.Sample();
                current = s.CurrentVector;
                roughness = SeaStateRoughness(s.SeaState);
            }
            float time = GameServices.Clock != null ? (float)GameServices.Clock.TotalSeconds : Time.time;

            for (int r = 0; r < _rigs.Count; r++)
                _rigs[r].Tick(current, roughness, time, dt, _config, _foamColor);
        }

        /// <summary>
        /// Re-scan for boats. Adds a rig for any new <see cref="BoatController"/> (up to <see cref="_maxBoats"/>)
        /// and disposes rigs whose boat was destroyed. Cheap and infrequent.
        /// </summary>
        private void Rescan()
        {
            // Drop rigs whose boat is gone.
            for (int i = _rigs.Count - 1; i >= 0; i--)
            {
                if (_rigs[i].Boat == null)
                {
                    _rigs[i].Dispose();
                    _rigs.RemoveAt(i);
                }
            }

            if (_rigs.Count >= _maxBoats) return;

            var boats = FindObjectsByType<BoatController>(FindObjectsSortMode.None);
            if (boats == null) return;
            foreach (var boat in boats)
            {
                if (boat == null) continue;
                if (_rigs.Count >= _maxBoats) break;
                if (HasRigFor(boat)) continue;
                _rigs.Add(new WakeRig(boat, _poolPerBoat, _foamSprite, transform, _sortingLayer, _sortingOrder));
            }
        }

        private bool HasRigFor(BoatController boat)
        {
            for (int i = 0; i < _rigs.Count; i++)
                if (_rigs[i].Boat == boat) return true;
            return false;
        }

        // ==== sea-state → roughness (mirrors the water's Choppiness scale) =================================

        /// <summary>
        /// Map the discrete <see cref="SeaState"/> (Glass=0 .. Storm=7) to a 0..1 roughness, the SAME linear
        /// scale the water surface uses for its choppiness — so the wake breaks up exactly when the water does
        /// (glassy → no distortion, storm → full wobble). Pure + static; unit-testable.
        /// </summary>
        public static float SeaStateRoughness(SeaState seaState)
        {
            int max = (int)SeaState.Storm;   // 7
            return max > 0 ? Mathf.Clamp01((int)seaState / (float)max) : 0f;
        }

        // ==== procedural foam sprite (avoids the spriteMode-Multiple BoatWake.png load gotcha) =============

        /// <summary>
        /// Build a small, soft, round foam puff sprite in code — point-filtered and snapped to a power-of-two
        /// pixel grid so it stays crisp pixel-art over the water. Generating it avoids depending on the
        /// multiple-sprite-mode BoatWake.png (which <c>LoadAssetAtPath&lt;Sprite&gt;</c> can't return) and keeps
        /// the whole effect self-contained: one shared sprite + one material for every puff (batched, rule 7).
        /// </summary>
        private static Sprite BuildFoamSprite()
        {
            const int size = 16;       // tiny — pixel-art foam
            const int ppu = 32;        // matches the project PPU (1 world unit = 1 m at 32px)
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "BoatWake.FoamPuff",
                filterMode = FilterMode.Point,        // pixel-crisp
                wrapMode = TextureWrapMode.Clamp,
            };
            float c = (size - 1) * 0.5f;
            float r = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / r, dy = (y - c) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);     // 0 centre .. 1 edge
                // Soft round falloff: opaque core, feathered rim, transparent outside.
                float a = Mathf.Clamp01(1f - d);
                a = a * a;                                    // tighten the core
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
        }

        /// <summary>
        /// One boat's wake: a <see cref="WakeParticleSystem"/> plus a fixed pool of <see cref="SpriteRenderer"/>s
        /// (one per particle slot) parented under the host. Each tick it emits at the stern proportional to the
        /// boat's speed, advances every puff (own momentum + the current, with decay), and writes each live
        /// puff's render transform/colour from the pure life-curves. Dead puffs hide their renderer (kept, never
        /// destroyed) — zero allocation after construction.
        /// </summary>
        private sealed class WakeRig
        {
            public readonly BoatController Boat;
            private readonly WakeParticleSystem _sys;
            private readonly SpriteRenderer[] _renderers;
            private readonly Transform _root;
            private float _emitCarry;

            public WakeRig(BoatController boat, int pool, Sprite foam, Transform parent,
                           string sortingLayer, int sortingOrder)
            {
                Boat = boat;
                _sys = new WakeParticleSystem(pool);

                _root = new GameObject($"Wake[{boat.name}]").transform;
                _root.SetParent(parent, worldPositionStays: false);

                _renderers = new SpriteRenderer[pool];
                for (int i = 0; i < pool; i++)
                {
                    var go = new GameObject("foam");
                    go.transform.SetParent(_root, worldPositionStays: false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = foam;
                    if (!string.IsNullOrEmpty(sortingLayer)) sr.sortingLayerName = sortingLayer;
                    sr.sortingOrder = sortingOrder;
                    go.SetActive(false);
                    _renderers[i] = sr;
                }
            }

            public void Tick(Vector2 current, float roughness, float time, float dt,
                             in WakeConfig cfg, Color foamColor)
            {
                if (Boat == null) return;

                Vector2 pos = Boat.transform.position;
                Vector2 bow = Boat.transform.up;
                float speed = Boat.Velocity.magnitude;
                bool aground = Boat.IsAground;

                // 1) EMIT from the stern, rate ∝ speed (none below threshold / when aground).
                int count = WakeParticleSystem.EmissionCount(speed, aground, cfg, dt, ref _emitCarry);
                if (count > 0) _sys.Emit(count, pos, bow, speed, cfg);

                // 2) TRAVEL WITH THE CURRENT (+ own momentum) and DISSIPATE (age toward lifetime).
                _sys.Step(current, cfg.VelocityDecay, dt);

                // 3 + 4) RENDER: wave-distort the position, fade + spread over life.
                Render(roughness, time, cfg, foamColor);
            }

            private void Render(float roughness, float time, in WakeConfig cfg, Color foamColor)
            {
                var pool = _sys.Pool;
                for (int i = 0; i < pool.Length; i++)
                {
                    var sr = _renderers[i];
                    ref readonly var p = ref pool[i];
                    if (!p.Alive)
                    {
                        if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                        continue;
                    }

                    float life = WakeParticleSystem.Life01(p.Age, p.Lifetime);
                    float alpha = WakeParticleSystem.LifeFade(life, cfg);
                    float sizeM = WakeParticleSystem.LifeSpread(p.BaseSize, life, cfg);
                    Vector2 renderPos = WakeParticleSystem.RenderPosition(in p, time, roughness, cfg);

                    var t = sr.transform;
                    t.position = new Vector3(renderPos.x, renderPos.y, 0f);
                    t.localScale = new Vector3(sizeM, sizeM, 1f);
                    var col = foamColor; col.a = alpha;
                    sr.color = col;
                    if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
                }
            }

            public void Dispose()
            {
                if (_root != null) Destroy(_root.gameObject);
            }
        }
    }
}
