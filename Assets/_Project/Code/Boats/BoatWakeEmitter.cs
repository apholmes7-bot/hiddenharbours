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
    /// <para><b>Graded by hull (the owner's brief).</b> The wake scales with hull SIZE + WEIGHT + SPEED: each
    /// tick it blends <see cref="BoatHullDef.LengthMeters"/> (size), <see cref="BoatHullDef.MassKg"/> (weight) and
    /// the live speed into one magnitude (the pure, EditMode-tested <see cref="WakeGrading"/>), picks an authored
    /// graded plume TIER (Small/Medium/Large/Huge) from tunable thresholds, and grows the foam footprint to match —
    /// so a bigger/heavier hull, or the same hull pushed harder, throws a visibly bigger wake, and future heavier
    /// hulls scale automatically (driven off the hull stats, never a per-hull hard-code). The tier sprites are
    /// built once from the authored textures via the <see cref="WakeSpriteLibrary"/> (Resources); if a load fails
    /// the plume falls back to the procedural foam puff so a bad load never leaves an invisible wake.</para>
    ///
    /// <para><b>Seam discipline (rule 4) &amp; determinism (rule 5).</b> Reads the boat through its public
    /// surface (<see cref="BoatController.Velocity"/>, <see cref="BoatController.IsAground"/>,
    /// <see cref="BoatController.Hull"/> for size/weight, bow = <c>transform.up</c>) and the sea ONLY through the
    /// Core <see cref="GameServices.Environment"/> accessor
    /// (the same <see cref="EnvironmentSample.CurrentVector"/> / <see cref="EnvironmentSample.SeaState"/> the
    /// water shader reads, so wake + water move together) — never the Environment concrete classes. It drives no
    /// simulation and saves nothing; the per-puff jitter is a deterministic hash, not <see cref="System.Random"/>.
    /// <b>Performance (rule 7):</b> fixed sprite pools (no per-frame allocation), a handful of shared sprites —
    /// the foam puff, the crest line, and the ≤4 graded tier plumes — all batched, sim sampled once per throttled
    /// tick. Mobile-portable.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoatWakeEmitter : MonoBehaviour
    {
        [Header("Wake feel (all tunable — no magic numbers, rule 6)")]
        [Tooltip("Every knob of the wake — shed-rate, V angle, lifetime, fade/spread, current-advect, decay, " +
                 "wave-distort, foam size. Tune to taste; defaults are a lively inshore greybox wake.")]
        [SerializeField] private WakeConfig _config = WakeConfig.Default;

        [Header("Wake CREST LINES (the little waves you see, not just foam)")]
        [Tooltip("The Kelvin wake's visible wave CRESTS — the thin, elongated feather-wave streaks that peel " +
                 "off the hull along the diverging V arms — as a SECOND pooled stream ALONGSIDE the foam " +
                 "bubbles. Same shed/advect/fade/lifetime infrastructure; a lighter, longer, oriented look. " +
                 "Every knob is tunable; defaults read a subtle feathered wake, not a busy one.")]
        [SerializeField] private WakeLineConfig _lineConfig = WakeLineConfig.Default;

        [Header("Wake GRADING (bigger/heavier/faster hull → a bigger wake — owner's brief)")]
        [Tooltip("How the wake tier (Small/Medium/Large/Huge authored plume) is chosen per boat, per tick from a " +
                 "blend of hull SIZE (LengthMeters) + WEIGHT (MassKg) + live SPEED. Every reference range, blend " +
                 "weight and tier threshold is tunable here — nothing is hard-coded to the current hulls, so future " +
                 "heavier hulls scale automatically. Also grows the foam footprint with the grade so the whole " +
                 "wake stays coherent.")]
        [SerializeField] private WakeGradeConfig _grade = WakeGradeConfig.Default;

        [Header("BOW SPRAY (speed-forward, gentle on the dory — owner's brief)")]
        [Tooltip("The graded spray at the cutwater: the same size+weight+speed blend as the wake but with its " +
                 "OWN speed-forward weights and a HIGHER speed onset, so the slow dory shows at most a subtle " +
                 "gradual wisp at a hard row and the full sheet is reserved for the faster hulls to come. Every " +
                 "number is tunable here.")]
        [SerializeField] private BowSprayGradeConfig _spray = BowSprayGradeConfig.Default;

        [Header("Pool & render")]
        [Tooltip("Max live foam puffs PER BOAT. The pool is fixed and recycled — zero per-frame allocation.")]
        [Min(8)] [SerializeField] private int _poolPerBoat = 96;
        [Tooltip("Max live crest-LINE streaks PER BOAT (a separate fixed, recycled pool). Kept smaller than the " +
                 "foam pool — a few crisp crests read richer than a wall of lines.")]
        [Min(0)] [SerializeField] private int _linePoolPerBoat = 48;
        [Tooltip("How many boats to drive at once (each gets its own pool). One active player boat dominates; " +
                 "spare slots cover NPC traffic when it arrives.")]
        [Min(1)] [SerializeField] private int _maxBoats = 4;
        [Tooltip("Foam tint. White-ish reads as sea-foam over the blue water; the alpha is driven per-puff by the fade.")]
        [SerializeField] private Color _foamColor = new Color(0.92f, 0.96f, 1f, 1f);
        [Tooltip("Crest-LINE tint. A lighter, cooler-than-foam wave-crest colour so the lines read as the small " +
                 "waves peeling off the hull rather than white churn. Alpha is driven per-streak by the fade.")]
        [SerializeField] private Color _lineColor = new Color(0.78f, 0.90f, 0.98f, 1f);
        [Tooltip("Bow-spray tint. Bright white droplets off the cutwater; the alpha is driven by the spray's " +
                 "speed-onset ramp (the dory only ever sees a faint fraction of it).")]
        [SerializeField] private Color _sprayColor = new Color(0.95f, 0.98f, 1f, 1f);
        [Tooltip("Sorting layer name for the wake sprites (leave blank for the default layer).")]
        [SerializeField] private string _sortingLayer = "";
        [Tooltip("Order in the sorting layer. Foam should sit ABOVE the water plane but BELOW the boat hull.")]
        [SerializeField] private int _sortingOrder = -1;
        [Tooltip("Order for the crest LINES. One BELOW the foam by default so the bright foam bubbles read on " +
                 "top of the fainter wave-crest streaks (both still above the water plane, below the hull).")]
        [SerializeField] private int _lineSortingOrder = -2;
        [Tooltip("Order for the graded PLUME (the broad authored wake sprite). BELOW the foam + crest lines by " +
                 "default so it reads as the base wash they sit on top of — still above the water plane, below the hull.")]
        [SerializeField] private int _plumeSortingOrder = -3;
        [Tooltip("Order for the BOW SPRAY. Same band as the foam by default — above the water plane, below the " +
                 "hull, so the droplets read as kicked up at the cutwater without covering the boat.")]
        [SerializeField] private int _spraySortingOrder = -1;

        [Header("Cadence")]
        [Tooltip("How often (Hz) the wake sim ticks (emit + advect + render). The sea is slow; the boat moves " +
                 "smoothly — a handful of Hz reads fine and stays cheap. Matches the water's refresh idiom.")]
        [Min(5f)] [SerializeField] private float _tickHz = 30f;
        [Tooltip("How often (Hz) to re-scan the scene for boats (cheap; boats don't appear often).")]
        [Min(0.25f)] [SerializeField] private float _rescanHz = 1f;

        // ---- runtime ----------------------------------------------------------------------------------------
        private readonly List<WakeRig> _rigs = new();
        private Sprite _foamSprite;
        private Sprite _lineSprite;
        // The four GRADED plume sprites [Small, Medium, Large, Huge], built once from the authored textures.
        // Any slot may be null if the library/texture failed to load — the rig falls back to the foam puff so a
        // bad load never leaves an invisible plume.
        private Sprite[] _tierSprites;
        // The four graded BOW SPRAY sprites [Small, Medium, Large, Huge] — same build, same per-tier fallback.
        private Sprite[] _spraySprites;
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
            _lineSprite = BuildLineSprite();
            // The pivot must sit on the art's NARROW apex whichever way the art is authored: the serialized
            // PlumePivotY when un-flipped, mirrored when the owner toggles PlumeFlip (art re-authored upside-down).
            _tierSprites = BuildTierSprites(WakeGrading.FlipPivotY(_grade.PlumePivotY, _grade.PlumeFlip));
        }

        /// <summary>Project PPU (1 world unit = 1 m at 32 px). Matches the Wake PNGs' import (spritePixelsToUnits: 32),
        /// so a built full-image plume sprite comes out at its authored metres (Small ≈ 2×3.25 m … Huge ≈ 6.6×10.3 m).</summary>
        private const int WakePlumePpu = 32;

        /// <summary>
        /// Build ONE full-image <see cref="Sprite"/> per graded wake TEXTURE (Small/Medium/Large/Huge), loaded from
        /// the <see cref="WakeSpriteLibrary"/> in Resources. We reference the TEXTURE (the always-present main asset)
        /// and build the sprite in code — the SAME technique the foam/crest sprites use — because the Wake PNGs
        /// import as <c>spriteMode: Multiple</c> and Unity auto-slices each into many disconnected alpha islands, so
        /// there is no single full-image sub-sprite to reference and <c>Resources.Load&lt;Sprite&gt;</c> returns null
        /// (the documented trap). Building from the texture yields the whole authored plume regardless of slicing.
        ///
        /// <para>The pivot is the art's narrow APEX (the boat end, at the top of the image) so the plume pins to the
        /// stern and widens astern. Any slot the library couldn't supply comes back null; the rig then falls back to
        /// the procedural foam puff so a bad load never leaves an invisible plume. Built once at boot, shared across
        /// every boat, so the ≤4 tier sprites batch (rule 7).</para>
        /// </summary>
        private static Sprite[] BuildTierSprites(float pivotY)
        {
            var sprites = new Sprite[WakeGrading.TierCount];
            var lib = Resources.Load<WakeSpriteLibrary>(WakeSpriteLibrary.ResourcesPath);
            if (lib == null)
            {
                Debug.LogWarning("[BoatWakeEmitter] No WakeSpriteLibrary in Resources — the graded plume falls " +
                                 "back to the procedural foam puff. (Expected at Resources/WakeSpriteLibrary.)");
                return sprites;   // all null → rig uses the foam fallback
            }

            Texture2D[] textures = lib.Ordered();
            var pivot = new Vector2(0.5f, Mathf.Clamp01(pivotY));
            for (int i = 0; i < sprites.Length && i < textures.Length; i++)
            {
                Texture2D tex = textures[i];
                if (tex == null)
                {
                    Debug.LogWarning($"[BoatWakeEmitter] Wake tier {i} texture missing in the library — that tier " +
                                     "falls back to the procedural foam puff.");
                    continue;
                }
                var full = new Rect(0f, 0f, tex.width, tex.height);
                sprites[i] = Sprite.Create(tex, full, pivot, WakePlumePpu);
                sprites[i].name = $"BoatWake.Plume[{i}]";
            }
            return sprites;
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
                roughness = SeaStateRoughness(s.SeaState01);
            }
            float time = GameServices.Clock != null ? (float)GameServices.Clock.TotalSeconds : Time.time;

            for (int r = 0; r < _rigs.Count; r++)
                _rigs[r].Tick(current, roughness, time, dt, _config, _foamColor, _lineConfig, _lineColor, _grade);
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
                _rigs.Add(new WakeRig(boat, _poolPerBoat, _linePoolPerBoat, _foamSprite, _lineSprite, _tierSprites,
                                      transform, _sortingLayer, _sortingOrder, _lineSortingOrder, _plumeSortingOrder));
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
        /// Map the CONTINUOUS sea-state axis (<see cref="EnvironmentSample.SeaState01"/>, 0 glass .. 1 storm)
        /// to a 0..1 roughness, the SAME scale the water surface uses for its choppiness — so the wake breaks
        /// up exactly when the water does (glassy → no distortion, storm → full wobble), easing with the wind
        /// instead of stepping at an enum band edge. Pure + static; unit-testable.
        /// </summary>
        public static float SeaStateRoughness(float seaState01)
        {
            return Mathf.Clamp01(seaState01);
        }

        // ==== CREST-LINE geometry (the wave-crest streaks — pure, EditMode-tested) ==========================

        /// <summary>
        /// The PURE geometry + shaping math for the wake CREST LINES (the "small waves you see" — the divergent
        /// feather-wave crests that peel off the hull along the Kelvin V). It is deliberately side-effect-free and
        /// static so the Kelvin-angle placement, the speed-onset ramp, the crest orientation and the length curve
        /// can be EditMode-tested headless (no scene, no Unity object). The particle SIMULATION (advect with the
        /// current, fade, spread, lifetime, recycle) is NOT re-implemented here — the crest streaks reuse the very
        /// same <see cref="WakeParticleSystem"/> the foam bubbles use; this class only supplies the streak-specific
        /// spawn geometry and per-streak render shaping on top of it.
        /// </summary>
        public static class WakeLineGeometry
        {
            /// <summary>
            /// Speed-onset ramp, 0..1: <b>0 at rest</b> (no crest lines when the boat isn't making way), rising to
            /// 1 once the boat is clearly underway. Below <paramref name="cfg"/>.SpeedOnset it is 0; it then ramps
            /// linearly to 1 over the next <paramref name="cfg"/>.SpeedOnsetRange (m/s). Monotonic non-decreasing in
            /// speed. Used to scale the streak shed-rate/length/brightness with speed (the brief: "a clear feathered
            /// wake underway, none at rest"). Pure + static.
            /// </summary>
            public static float SpeedOnset(float speed, in WakeLineConfig cfg)
            {
                float range = Mathf.Max(1e-3f, cfg.SpeedOnsetRange);
                return Mathf.Clamp01((speed - cfg.SpeedOnset) / range);
            }

            /// <summary>
            /// How many crest streaks to shed this tick: the foam's per-speed cadence
            /// (<see cref="WakeParticleSystem.EmissionCount"/> against the streak's own <see cref="WakeConfig"/>)
            /// further GATED by the <see cref="SpeedOnset"/> ramp so lines appear only once genuinely underway and
            /// the count grows smoothly with speed. Returns 0 at rest / aground / below onset. Pure + static (the
            /// stateful fractional carry is threaded in exactly like the foam's).
            /// </summary>
            public static int EmissionCount(float speed, bool aground, in WakeLineConfig cfg, float dt, ref float carry)
            {
                float onset = SpeedOnset(speed, in cfg);
                if (aground || onset <= 0f || dt <= 0f)
                {
                    carry = 0f;
                    return 0;
                }
                // Reuse the foam emission cadence against the streak arm-config, scaled by the onset ramp so the
                // line density eases in with speed rather than snapping on at the threshold.
                WakeConfig arm = cfg.ArmConfig;
                float over = Mathf.Max(0f, speed - arm.SpeedThreshold);
                carry += arm.ShedPerSpeed * over * dt * onset;
                int whole = Mathf.FloorToInt(carry);
                if (whole > 0) carry -= whole;
                return whole;
            }

            /// <summary>
            /// The render ROTATION (degrees) that orients a streak sprite ALONG its local crest / feather-wave
            /// direction. The crest peels off the hull down the V arm, so early in a streak's life (while it still
            /// carries its shed momentum) its own velocity vector points down that arm — orient the sprite to it.
            /// The sprite's long axis is +X at 0°, so the returned angle is simply <c>atan2(vel.y, vel.x)</c>. If
            /// the velocity has collapsed (a spent streak that now only drifts with the current) we fall back to
            /// <paramref name="fallbackDeg"/> so the orientation never snaps to a garbage angle. Pure + static.
            /// </summary>
            public static float CrestAngleDeg(Vector2 vel, float fallbackDeg = 0f)
            {
                if (vel.sqrMagnitude <= 1e-8f) return fallbackDeg;
                return Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
            }

            /// <summary>
            /// The streak LENGTH (m) at a moment in its life: base length scaled UP with the speed onset (a faster
            /// boat throws longer feather crests) and DOWN as the streak ages (<paramref name="life01"/> 0→1), so a
            /// fresh crest is a long clean line and an old one has shortened toward its foam-like remnant before it
            /// fades out. Never negative. Pure + static — this is the per-streak length the rig writes into the
            /// sprite's long-axis scale.
            /// </summary>
            public static float StreakLength(float speedOnset01, float life01, in WakeLineConfig cfg)
            {
                float onset = Mathf.Clamp01(speedOnset01);
                float life = Mathf.Clamp01(life01);
                float speedGrow = Mathf.Lerp(cfg.LengthAtOnset, 1f, onset);     // shorter just after onset, full at speed
                float ageShrink = Mathf.Lerp(1f, Mathf.Clamp01(cfg.LengthAtDeath), life);
                return Mathf.Max(0f, cfg.LineLength * speedGrow * ageShrink);
            }
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
        /// Build a small ELONGATED wave-crest streak sprite in code — a thin horizontal bar with a bright core
        /// and feathered ends (soft along its length, hard-ish across its thin axis), point-filtered so it stays
        /// crisp pixel-art over the water. This is the "small wave you see" peeling off the hull: at native scale
        /// it is roughly a 1×N crest line; the rig stretches it to the streak length and rotates it to the local
        /// crest (feather-wave) direction. Generated in code for the same reason as the foam puff — one shared
        /// sprite + one material for every streak (batched, rule 7), no BoatWake.png load gotcha.
        /// </summary>
        // Native pixel dims of the code-built crest-line sprite, shared by the builder and the render so the
        // world-space length/width scaling below is exact (no drift if the sprite is retuned).
        private const int LineSpritePx = 32;    // long axis (the crest line runs along X)
        private const int LineSpritePy = 5;     // thin cross axis
        private const int LineSpritePpu = 32;   // project PPU (1 world unit = 1 m at 32px)
        // World-space native size = pixels / PPU. Used to convert a desired world length/thickness into scale.
        private const float LineNativeLength = LineSpritePx / (float)LineSpritePpu;   // 1.0 m long
        private const float LineNativeWidth = LineSpritePy / (float)LineSpritePpu;    // 0.15625 m thin

        private static Sprite BuildLineSprite()
        {
            const int w = LineSpritePx;   // long axis (the crest line runs along X)
            const int h = LineSpritePy;   // thin cross axis
            const int ppu = LineSpritePpu;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
            {
                name = "BoatWake.CrestLine",
                filterMode = FilterMode.Point,        // pixel-crisp
                wrapMode = TextureWrapMode.Clamp,
            };
            float cx = (w - 1) * 0.5f;
            float cy = (h - 1) * 0.5f;
            float rx = w * 0.5f;
            float ry = h * 0.5f;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - cx) / rx;             // -1..1 along the crest
                float dy = (y - cy) / ry;             // -1..1 across the thin axis
                // Feather the ENDS (soft along length) but keep the crest reasonably solid across its width.
                float along = Mathf.Clamp01(1f - dx * dx);        // 1 centre → 0 ends (parabolic taper)
                float across = Mathf.Clamp01(1f - Mathf.Abs(dy)); // 1 centre-line → 0 top/bottom edge
                float a = along * across;
                a *= a;                                            // tighten toward a crisp line
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
                px[y * w + x] = new Color32(255, 255, 255, alpha);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
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
            private readonly WakeParticleSystem _sys;         // the foam BUBBLES (unchanged)
            private readonly SpriteRenderer[] _renderers;
            private readonly WakeParticleSystem _lineSys;     // the crest LINES (same infra, streak-tuned)
            private readonly SpriteRenderer[] _lineRenderers;
            private readonly Transform _root;
            // The graded PLUME: ONE stern-anchored renderer that swaps to the selected tier sprite each tick (the
            // primary size read). Shared across boats via the tier-sprite array; the foam puff is the fallback.
            private readonly Sprite[] _tierSprites;
            private readonly Sprite _fallbackSprite;
            private readonly SpriteRenderer _plume;
            private float _emitCarry;
            private float _lineEmitCarry;

            public WakeRig(BoatController boat, int pool, int linePool, Sprite foam, Sprite line, Sprite[] tierSprites,
                           Transform parent, string sortingLayer, int sortingOrder, int lineSortingOrder,
                           int plumeSortingOrder)
            {
                Boat = boat;
                _sys = new WakeParticleSystem(pool);
                _tierSprites = tierSprites;
                _fallbackSprite = foam;

                _root = new GameObject($"Wake[{boat.name}]").transform;
                _root.SetParent(parent, worldPositionStays: false);

                // The graded PLUME renderer (one, created once). Sits UNDER the foam/crest by sort order so it reads
                // as the base wash. Starts hidden; the tick shows it and swaps its sprite to the selected tier.
                var plumeGo = new GameObject("plume");
                plumeGo.transform.SetParent(_root, worldPositionStays: false);
                _plume = plumeGo.AddComponent<SpriteRenderer>();
                if (!string.IsNullOrEmpty(sortingLayer)) _plume.sortingLayerName = sortingLayer;
                _plume.sortingOrder = plumeSortingOrder;
                plumeGo.SetActive(false);

                _renderers = BuildRenderers(pool, "foam", foam, sortingLayer, sortingOrder);

                // The crest LINES: a SECOND pooled system + renderer slice under the same root, sharing all the
                // emit/advect/fade/lifetime machinery of the foam — only the sprite, config, colour, sorting and
                // per-streak orientation differ. Guarded so a 0 line-pool cleanly disables the lines.
                int lp = Mathf.Max(0, linePool);
                if (lp > 0 && line != null)
                {
                    _lineSys = new WakeParticleSystem(lp);
                    _lineRenderers = BuildRenderers(lp, "crest", line, sortingLayer, lineSortingOrder);
                }
            }

            /// <summary>The tier sprite for an index, or the foam-puff fallback if that tier failed to load (so a
            /// bad art load never leaves an invisible plume). Null only if even the fallback is missing.</summary>
            private Sprite TierSpriteOrFallback(int tier)
            {
                if (_tierSprites != null && tier >= 0 && tier < _tierSprites.Length && _tierSprites[tier] != null)
                    return _tierSprites[tier];
                return _fallbackSprite;
            }

            private SpriteRenderer[] BuildRenderers(int count, string name, Sprite sprite,
                                                    string sortingLayer, int sortingOrder)
            {
                var arr = new SpriteRenderer[count];
                for (int i = 0; i < count; i++)
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(_root, worldPositionStays: false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    if (!string.IsNullOrEmpty(sortingLayer)) sr.sortingLayerName = sortingLayer;
                    sr.sortingOrder = sortingOrder;
                    go.SetActive(false);
                    arr[i] = sr;
                }
                return arr;
            }

            public void Tick(Vector2 current, float roughness, float time, float dt,
                             in WakeConfig cfg, Color foamColor, in WakeLineConfig lineCfg, Color lineColor,
                             in WakeGradeConfig grade)
            {
                if (Boat == null) return;

                Vector2 pos = Boat.transform.position;
                Vector2 bow = Boat.transform.up;
                float speed = Boat.Velocity.magnitude;
                bool aground = Boat.IsAground;

                // --- GRADE the wake by hull SIZE + WEIGHT + SPEED (the owner's brief). Static hull stats come
                // through the boat's public seam (rule 4); speed is live. The magnitude drives BOTH the plume
                // tier/scale AND a coherent growth of the foam footprint so the whole wake scales with the boat. ---
                float length = Boat.Hull != null ? Boat.Hull.LengthMeters : grade.LengthRefMin;
                float mass = Boat.Hull != null ? Boat.Hull.MassKg : grade.MassRefMin;
                float magnitude = WakeGrading.Magnitude01(length, mass, speed, grade);
                int tier = WakeGrading.TierIndex(magnitude, grade);
                float foamFactor = WakeGrading.FoamExtentFactor(magnitude, grade);

                // Grow the foam bubbles + crisp Kelvin arms with the grade (a bigger hull's whole wake is bigger).
                // A per-tick copy of the tuned config — the pure functions take it by value, so this never mutates
                // the owner's serialized config and allocates nothing on the heap (struct copy).
                WakeConfig fcfg = ScaleFoamExtent(cfg, foamFactor);

                // --- GRADED PLUME (the primary size read) ---
                RenderPlume(pos, bow, speed, aground, magnitude, tier, length, foamColor, grade);

                // --- FOAM BUBBLES (now grown by the grade) ---
                // 1) EMIT from the stern, rate ∝ speed (none below threshold / when aground).
                int count = WakeParticleSystem.EmissionCount(speed, aground, fcfg, dt, ref _emitCarry);
                if (count > 0) _sys.Emit(count, pos, bow, speed, fcfg);
                // 2) TRAVEL WITH THE CURRENT (+ own momentum) and DISSIPATE (age toward lifetime).
                _sys.Step(current, fcfg.VelocityDecay, dt);
                // 3 + 4) RENDER: wave-distort the position, fade + spread over life.
                RenderFoam(roughness, time, fcfg, foamColor);

                // --- CREST LINES (added: the small waves you see) ---
                // Same three stages against the streak system, gated by the speed-onset ramp; each live streak is
                // rendered as an elongated sprite oriented to its crest direction and stretched by StreakLength.
                // The arm config grows with the grade too, so the feathered crests widen with the boat.
                if (_lineSys != null)
                {
                    WakeConfig arm = ScaleFoamExtent(lineCfg.ArmConfig, foamFactor);
                    int lineCount = WakeLineGeometry.EmissionCount(speed, aground, lineCfg, dt, ref _lineEmitCarry);
                    if (lineCount > 0) _lineSys.Emit(lineCount, pos, bow, speed, arm);
                    _lineSys.Step(current, arm.VelocityDecay, dt);
                    RenderLines(roughness, time, speed, lineCfg, arm, lineColor);
                }
            }

            /// <summary>A per-tick copy of a foam <see cref="WakeConfig"/> with its FOOTPRINT (foam size + Kelvin
            /// arm length) grown by the grade factor, so the procedural wake scales coherently with the graded
            /// plume. Struct copy — no heap allocation, never mutates the serialized config.</summary>
            private static WakeConfig ScaleFoamExtent(WakeConfig c, float factor)
            {
                c.FoamSize *= factor;
                c.ArmLength *= factor;
                return c;
            }

            /// <summary>
            /// Draw the graded plume: the authored tier sprite pinned APEX-at-the-stern and oriented so it widens
            /// astern, scaled continuously by the magnitude and faded in with the speed onset (none at rest / when
            /// aground). Hidden when the plume is disabled, off-onset, or has no usable sprite. One renderer, one
            /// shared sprite per tier — no allocation.
            ///
            /// <para><b>The orientation fix.</b> The apex is anchored at the boat's actual STERN
            /// (<see cref="WakeGrading.SternAnchor"/> walks half the hull length back from the boat-origin, which
            /// sits at the hull centre) — the old code offset from the ORIGIN, which buried ~the whole plume under
            /// the hull so only its wide faint tail showed past the stern and the wake read as backwards. The art's
            /// narrow apex is at the image TOP (pixel-verified by <c>WakeArtOrientationTests</c>); the apex pivot
            /// pins there and the body trails + widens astern because local +Y is aligned with the bow
            /// (<see cref="WakeGrading.OrientAngleDeg"/>, which also carries the owner's PlumeFlip escape hatch).</para>
            /// </summary>
            private void RenderPlume(Vector2 pos, Vector2 bow, float speed, bool aground, float magnitude, int tier,
                                     float hullLength, Color tint, in WakeGradeConfig grade)
            {
                if (_plume == null) return;

                float onset = WakeGrading.SpeedOnset(speed, in grade);
                Sprite sprite = TierSpriteOrFallback(tier);
                if (!grade.PlumeEnabled || aground || onset <= 0f || sprite == null)
                {
                    if (_plume.gameObject.activeSelf) _plume.gameObject.SetActive(false);
                    return;
                }

                _plume.sprite = sprite;

                Vector2 apex = WakeGrading.SternAnchor(pos, bow, hullLength, grade.PlumeAsternOffset);
                float scale = WakeGrading.PlumeScale(magnitude, in grade);
                float angleDeg = WakeGrading.OrientAngleDeg(bow, grade.PlumeFlip);

                var t = _plume.transform;
                t.position = new Vector3(apex.x, apex.y, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
                t.localScale = new Vector3(scale, scale, 1f);
                var col = tint; col.a = Mathf.Clamp01(grade.PlumeStartAlpha) * onset;
                _plume.color = col;
                if (!_plume.gameObject.activeSelf) _plume.gameObject.SetActive(true);
            }

            private void RenderFoam(float roughness, float time, in WakeConfig cfg, Color foamColor)
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

            private void RenderLines(float roughness, float time, float speed,
                                     in WakeLineConfig lineCfg, in WakeConfig arm, Color lineColor)
            {
                float onset = WakeLineGeometry.SpeedOnset(speed, in lineCfg);
                var pool = _lineSys.Pool;
                for (int i = 0; i < pool.Length; i++)
                {
                    var sr = _lineRenderers[i];
                    ref readonly var p = ref pool[i];
                    if (!p.Alive)
                    {
                        if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                        continue;
                    }

                    float life = WakeParticleSystem.Life01(p.Age, p.Lifetime);
                    // The crest lines fade + advect + distort exactly like the foam (shared arm config), but read
                    // subtler: their own StartAlpha (via LifeFade) is already the streak alpha, scaled by onset so
                    // faint at the onset speed and full underway.
                    float alpha = WakeParticleSystem.LifeFade(life, arm) * Mathf.Clamp01(lineCfg.LineOpacity) * onset;
                    Vector2 renderPos = WakeParticleSystem.RenderPosition(in p, time, roughness, arm);

                    // Orient along the crest (feather-wave) direction — the streak's own velocity while it carries
                    // its shed momentum; the sprite's long axis is +X, so we stretch X to the streak length and Y
                    // to a thin cross-width. Both are WORLD-space targets, converted to local scale by dividing by
                    // the sprite's native world size so the length/width are exact regardless of the sprite dims.
                    // The particle's BaseSize carries the cross-width seed (the foam's FoamSize idiom).
                    float lengthM = WakeLineGeometry.StreakLength(onset, life, in lineCfg);
                    float widthM = Mathf.Max(0.001f, p.BaseSize * lineCfg.LineWidthScale);
                    float angleDeg = WakeLineGeometry.CrestAngleDeg(p.Vel);

                    var t = sr.transform;
                    t.position = new Vector3(renderPos.x, renderPos.y, 0f);
                    t.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
                    t.localScale = new Vector3(lengthM / LineNativeLength, widthM / LineNativeWidth, 1f);
                    var col = lineColor; col.a = Mathf.Clamp01(alpha);
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

    /// <summary>
    /// Every tunable of the wake CREST LINES (the divergent feather-wave crests — the "small waves you see"),
    /// in one struct so the added stream stays free of magic numbers (CLAUDE.md rule 6). It wraps a full
    /// <see cref="WakeConfig"/> (<see cref="ArmConfig"/>) which the crest streaks reuse for the SAME
    /// emit-on-the-Kelvin-arm / advect-with-the-current / fade / lifetime machinery as the foam bubbles — so
    /// there is no duplicated simulation — plus the streak-only knobs (length, width, opacity, speed onset).
    /// <see cref="BoatWakeEmitter"/> serializes an owner-editable instance. Defaults read a subtle feathered
    /// wake alongside the foam, not a busy one.
    /// </summary>
    [System.Serializable]
    public struct WakeLineConfig
    {
        [Header("Where the crest lines live (reuses the foam's V-arm machinery)")]
        [Tooltip("The arm/emit/advect/fade config the crest streaks reuse — the SAME WakeConfig the foam bubbles " +
                 "use, tuned for lines: NO stern fill (clean arms only), a wider/longer V so the crests read as " +
                 "the feather waves peeling off the hull, a modest shed-rate and a shorter lifetime than the foam.")]
        public WakeConfig ArmConfig;

        [Header("Streak look")]
        [Tooltip("Base length (m) of a crest streak at full speed and birth. Scaled down near the onset speed and " +
                 "as the streak ages (see LengthAtOnset / LengthAtDeath). Longer = a more obvious feathered wake.")]
        public float LineLength;
        [Tooltip("Streak length as a fraction of LineLength right at the speed onset (0..1). <1 means the crests " +
                 "start short as the boat just gets underway and grow to full length at speed.")]
        public float LengthAtOnset;
        [Tooltip("Streak length as a fraction of full at the END of a streak's life (0..1). <1 shortens the crest " +
                 "as it ages so it dwindles toward a foam-like remnant before it fades out.")]
        public float LengthAtDeath;
        [Tooltip("Multiplies the streak's cross-width relative to the reused ArmConfig.FoamSize. <1 keeps the " +
                 "crest a THIN line (the whole point — a wave crest, not a blob).")]
        public float LineWidthScale;
        [Tooltip("Overall crest-line opacity multiplier (0..1) ON TOP of ArmConfig's own fade — a master dimmer " +
                 "so the lines stay subtler than the white foam. 0 = lines off.")]
        public float LineOpacity;

        [Header("Speed onset (none at rest, clear underway)")]
        [Tooltip("Boat speed (m/s) at which the crest lines BEGIN to appear. Below this: no lines at all. Usually " +
                 "a touch above the foam's own SpeedThreshold so faint dawdling leaves only foam, not crests.")]
        public float SpeedOnset;
        [Tooltip("Speed range (m/s) over which the crest lines ramp from just-appearing to full strength/length. " +
                 "Wider = a gentler fade-in of the feathered wake as the boat speeds up.")]
        public float SpeedOnsetRange;

        /// <summary>The greybox default — a subtle feathered crest wake alongside the foam. The owner tunes from here.</summary>
        public static WakeLineConfig Default => new WakeLineConfig
        {
            // Reuse the foam's default as the base, then tune it for CLEAN, LONGER, LINE-only arms.
            ArmConfig = MakeArmConfig(),
            LineLength      = 1.1f,   // m — a clear feather-wave streak
            LengthAtOnset   = 0.45f,  // short as the boat just gets underway
            LengthAtDeath   = 0.35f,  // dwindles toward a remnant before it fades
            LineWidthScale  = 0.35f,  // keep it a THIN crest line, not a blob
            LineOpacity     = 0.55f,  // subtler than the white foam
            SpeedOnset      = 0.8f,   // a touch above the foam's 0.4 threshold
            SpeedOnsetRange = 2.0f,   // ramp to full over the next 2 m/s
        };

        /// <summary>
        /// The streak arm config: the foam's <see cref="WakeConfig.Default"/> retuned for crest LINES — a wider,
        /// longer, clean-armed V (no stern fill), a lighter shed-rate, a shorter life and no size-spread (a crest
        /// keeps its shape then fades rather than blooming like foam). Pure builder so the defaults live in code,
        /// not scattered magic numbers.
        /// </summary>
        private static WakeConfig MakeArmConfig()
        {
            WakeConfig c = WakeConfig.Default;
            c.ShedPerSpeed      = 4f;     // fewer, cleaner crests than the dense foam
            c.VHalfAngleDeg     = 20f;    // the Kelvin feather angle, a hair wider than the foam V
            c.ArmLength         = 4.0f;   // the crests reach a little farther astern than the foam arms
            c.SternFillFraction = 0f;     // LINES only — no turbulent centre churn (that's the foam's job)
            c.SternFillWidth    = 0f;
            c.WashSpeedScale    = 0.25f;  // a touch more along-arm flow so the streak orients cleanly down the arm
            c.Lifetime          = 1.6f;   // shorter than the foam — a crest passes, then it's gone
            c.StartAlpha        = 0.9f;   // bright at birth; LineOpacity + onset dim the final result
            c.FadePower         = 1.2f;
            c.SpreadFactor      = 1f;     // a crest keeps its width (no bloom) — length/fade carry the dissolve
            c.FoamSize          = 0.5f;   // the cross-width seed; LineWidthScale thins it to a line
            c.SizeJitter        = 0.2f;
            return c;
        }
    }
}
