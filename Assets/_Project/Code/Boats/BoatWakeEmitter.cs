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
    /// the plume falls back to the procedural foam puff so a bad load never leaves an invisible wake. A graded
    /// <b>BOW SPRAY</b> rides the same machinery at the other end of the hull (<see cref="BowSprayGrading"/>) with
    /// its own SPEED-FORWARD config, so the slow dory shows only a gradual wisp and faster hulls earn the sheet.</para>
    ///
    /// <para><b>The DEPOSITED TRAIL + DYNAMIC BOW WAVE (owner ask 2026-07-23).</b> The wake is no longer a
    /// boat-locked stamp: foam churn and shoulder wavelets are DEPOSITED at the stern's swept track per metre
    /// of travel (<see cref="WakeTrailMath"/>), persist + decay WHERE THEY WERE LAID, and spread outward at
    /// <c>speed·tan(Kelvin angle)</c> so the V EMERGES in world space and curves through turns. The
    /// boat-attached plume/spray sprites are alive (a bounded deterministic churn pulse; the rigid plume
    /// fades with turn rate, handing hard turns to the trail), and the bow throws pooled DROPLETS off the
    /// cutwater that she drives past.
    /// <b>The rendered READ (owner playtest 2026-07-23, same day):</b> the trail reads as a wake in three
    /// zones — a dense BUBBLING CHURN BAND right behind the transom (big, overlapping, short-lived foam
    /// puffs that boil while young — "bubble close to the boat, be foamy close to the boat"), a fading
    /// centre lane, and the LONG PATTERN: shoulder streaks BAKED along the emergent arm's analytic
    /// direction (never their decaying velocity — that read as horizontal dashes) at a length that
    /// GUARANTEES neighbour overlap (<see cref="WakeTrailMath.ArmStreakLength"/>), so the arms fuse into
    /// long coherent lines. While the displaced sea is active (ADR 0023), every wake element rides
    /// <c>ShoreFadeMath.DisplacedHeight</c> of the shared swell under its OWN position, with the
    /// exaggeration + band read live from the Core <see cref="DisplacedSea"/> seam — never a config copy;
    /// displaced OFF, everything sits on the flat plane exactly as before (the A/B contract).</para>
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

        [Header("TRAIL DEPOSITION (owner ask 2026-07-23: the wake LEAVES A TRAIL, laid in world space)")]
        [Tooltip("The world-deposited trail: wavelet + churn deposits laid at the stern per metre of track, " +
                 "persisting and decaying WHERE THEY WERE LAID (the trail curves through turns), spreading " +
                 "outward at boatSpeed·tan(Kelvin angle) so the V emerges in world space instead of being " +
                 "stamped on the boat. Also the live-plume knobs (churn pulse + turn fade). Every number is " +
                 "tunable; deposits are hard-capped per tick so the pools can never flood.")]
        [SerializeField] private WakeTrailConfig _trail = WakeTrailConfig.Default;

        [Header("BOW WAVE (owner ask 2026-07-23: water visibly crashing against the bow)")]
        [Tooltip("The dynamic bow wave: a churn pulse on the authored spray sheet (it boils instead of " +
                 "sitting glued) plus pooled droplets thrown off the cutwater and left behind in world " +
                 "space as she drives past. Grading (tier, size, the dory-gentle onset) stays in the BOW " +
                 "SPRAY config above — this only animates it.")]
        [SerializeField] private BowWaveConfig _bowWave = BowWaveConfig.Default;

        [Header("Displaced sea (ADR 0023 — the wake rides the same sea the boat rides)")]
        [Tooltip("The wave field the wake's displaced-sea ride samples. PARITY: keep identical to " +
                 "BoatWaveMotion / WaveFieldBridge until GameConfig unifies them (ADR 0018 §(4)) — the foam " +
                 "must ride the same swell the hull and the drawn surface ride. The exaggeration + shore " +
                 "band are NEVER tuned here: they arrive live from the Core DisplacedSea seam.")]
        [SerializeField] private WaveFieldSettings _waveSettings = WaveFieldSettings.Default;
        [SerializeField] private WaveFieldAnimatorSettings _waveAnimatorSettings = WaveFieldAnimatorSettings.Default;

        [Header("Pool & render")]
        [Tooltip("Max live foam puffs PER BOAT. The pool is fixed and recycled — zero per-frame allocation.")]
        [Min(8)] [SerializeField] private int _poolPerBoat = 96;
        [Tooltip("Max live crest-LINE streaks PER BOAT (a separate fixed, recycled pool). Kept smaller than the " +
                 "foam pool — a few crisp crests read richer than a wall of lines.")]
        [Min(0)] [SerializeField] private int _linePoolPerBoat = 48;
        [Tooltip("Max live BOW-WAVE droplets PER BOAT (a third fixed, recycled pool). Small — spray is flecks " +
                 "with a sub-second life, not foam.")]
        [Min(0)] [SerializeField] private int _dropletPoolPerBoat = 24;
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
        // The wake's own eased wave-field animator (the BuoyWaveVisual pattern): ticked once per wake tick
        // with the live wind/sea-state so, while the displaced sea is active, every wake element can read
        // the height of the SAME swell under its own world position and ride it. Presentation-only state.
        private readonly WaveFieldAnimator _seaAnimator = new WaveFieldAnimator();

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

            // ONE library load feeds both graded sprite sets. The pivot must sit on the art's BOAT end
            // whichever way the art is authored: the serialized pivot when un-flipped, mirrored when the owner
            // toggles the flip (art re-authored upside-down) — WakeArtOrientationTests pixel-verifies the
            // defaults against the actual textures.
            var lib = Resources.Load<WakeSpriteLibrary>(WakeSpriteLibrary.ResourcesPath);
            if (lib == null)
            {
                Debug.LogWarning("[BoatWakeEmitter] No WakeSpriteLibrary in Resources — the graded plume and " +
                                 "bow spray fall back to the procedural foam puff. (Expected at " +
                                 "Resources/WakeSpriteLibrary.)");
            }
            _tierSprites = BuildTierSprites(lib != null ? lib.Ordered() : null,
                                            WakeGrading.FlipPivotY(_grade.PlumePivotY, _grade.PlumeFlip),
                                            "Plume");
            _spraySprites = BuildTierSprites(lib != null ? lib.OrderedSpray() : null,
                                             WakeGrading.FlipPivotY(_spray.SprayPivotY, _spray.SprayFlip),
                                             "BowSpray");
        }

        /// <summary>Project PPU (1 world unit = 1 m at 32 px). Matches the Wake PNGs' import (spritePixelsToUnits: 32),
        /// so a built full-image plume sprite comes out at its authored metres (Small ≈ 2×3.25 m … Huge ≈ 6.6×10.3 m).</summary>
        private const int WakePlumePpu = 32;

        /// <summary>
        /// Build ONE full-image <see cref="Sprite"/> per graded TEXTURE (Small/Medium/Large/Huge) from a
        /// <see cref="WakeSpriteLibrary"/> tier array — used for both the wake plume and the bow spray. We
        /// reference the TEXTURE (the always-present main asset) and build the sprite in code — the SAME
        /// technique the foam/crest sprites use — because these PNGs import as <c>spriteMode: Multiple</c> and
        /// Unity auto-slices each into many disconnected alpha islands, so there is no single full-image
        /// sub-sprite to reference and <c>Resources.Load&lt;Sprite&gt;</c> returns null (the documented trap).
        /// Building from the texture yields the whole authored image regardless of slicing.
        ///
        /// <para>The pivot is the art's BOAT end — the plume's narrow apex (image top) or the spray's impact
        /// churn (image bottom), pixel-verified by <c>WakeArtOrientationTests</c> and flip-mirrored by the
        /// caller. Any slot the library couldn't supply comes back null; the rig then falls back to the
        /// procedural foam puff so a bad load never leaves an invisible effect. Built once at boot, shared
        /// across every boat, so the ≤8 graded sprites batch (rule 7).</para>
        /// </summary>
        private static Sprite[] BuildTierSprites(Texture2D[] textures, float pivotY, string label)
        {
            var sprites = new Sprite[WakeGrading.TierCount];
            if (textures == null) return sprites;   // no library → all null → rig uses the foam fallback

            var pivot = new Vector2(0.5f, Mathf.Clamp01(pivotY));
            for (int i = 0; i < sprites.Length && i < textures.Length; i++)
            {
                Texture2D tex = textures[i];
                if (tex == null)
                {
                    Debug.LogWarning($"[BoatWakeEmitter] {label} tier {i} texture missing in the library — that " +
                                     "tier falls back to the procedural foam puff.");
                    continue;
                }
                var full = new Rect(0f, 0f, tex.width, tex.height);
                sprites[i] = Sprite.Create(tex, full, pivot, WakePlumePpu);
                sprites[i].name = $"BoatWake.{label}[{i}]";
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
            EnvironmentSample s = default;
            if (env != null)
            {
                s = env.Sample();
                current = s.CurrentVector;
                roughness = SeaStateRoughness(s.SeaState01);
            }
            double totalSeconds = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : Time.timeAsDouble;
            float time = (float)totalSeconds;

            // THE DISPLACED-SEA RIDE (ADR 0023): while the displaced surface is active, every wake element
            // lifts by ShoreFadeMath.DisplacedHeight of the shared swell under ITS OWN world position — the
            // exaggeration + shore band read LIVE from the Core DisplacedSea seam each tick, never a config
            // copy (the overlay-pose lesson: a cached scale once breathed a 1.6 m gap into this very wake).
            // Displaced OFF ⇒ an inactive context, every lift is exactly 0, and the wake sits on the flat
            // plane byte-identically to before (the A/B contract).
            SeaLift lift = default;
            bool displaced = DisplacedSea.TryGet(out DisplacedSeaState sea);
            if (env != null)
            {
                _seaAnimator.Tick(dt, s.WindVector, s.SeaState01, in _waveSettings, in _waveAnimatorSettings);
                if (displaced)
                    lift = new SeaLift(_seaAnimator, GameServices.TidalTerrain, env, totalSeconds,
                                       sea.ShoreFadeBandMeters, sea.Exaggeration);
            }

            for (int r = 0; r < _rigs.Count; r++)
                _rigs[r].Tick(current, roughness, time, dt, _config, _foamColor, _lineConfig, _lineColor, _grade,
                              _spray, _sprayColor, _trail, _bowWave, in lift);
        }

        /// <summary>
        /// The per-tick context that answers "how high does the displaced sea lift a wake element AT THIS
        /// world position?" — the ONE shared rule (<see cref="ShoreFadeMath.DisplacedHeight"/>) on the wake's
        /// own eased sample of the shared field, with the surface's live-published exaggeration + shore-fade
        /// band (<see cref="DisplacedSea"/>). <c>default</c> is the inactive context: every lift is 0 (the
        /// displaced-sea-OFF flat plane). Depth comes from the game's one depth rule
        /// (<see cref="BoatCrossing.DepthAt"/>; open water ⇒ +∞ ⇒ fade 1), so foam laid in the shallows
        /// settles exactly as the water under it does. Read-only, allocation-free, presentation-only.
        /// </summary>
        internal readonly struct SeaLift
        {
            private readonly WaveFieldAnimator _animator;
            private readonly ITidalTerrain _terrain;
            private readonly IEnvironmentService _environment;
            private readonly double _totalSeconds;
            private readonly float _bandMeters;
            private readonly float _exaggeration;
            public readonly bool Active;

            public SeaLift(WaveFieldAnimator animator, ITidalTerrain terrain, IEnvironmentService environment,
                           double totalSeconds, float bandMeters, float exaggeration)
            {
                _animator = animator;
                _terrain = terrain;
                _environment = environment;
                _totalSeconds = totalSeconds;
                _bandMeters = bandMeters;
                _exaggeration = exaggeration;
                Active = animator != null;
            }

            /// <summary>Metres of screen lift for an element at <paramref name="worldPos"/> — 0 when inactive.</summary>
            public float LiftAt(Vector2 worldPos)
            {
                if (!Active) return 0f;
                float height = _animator.Sample(worldPos).Height;
                float depth = BoatCrossing.DepthAt(_terrain, _environment, _totalSeconds, worldPos);
                return ShoreFadeMath.DisplacedHeight(height, depth, _bandMeters, _exaggeration);
            }
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
                _rigs.Add(new WakeRig(boat, _poolPerBoat, _linePoolPerBoat, _dropletPoolPerBoat, _foamSprite,
                                      _lineSprite, _tierSprites, _spraySprites, transform, _sortingLayer,
                                      _sortingOrder, _lineSortingOrder, _plumeSortingOrder, _spraySortingOrder));
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
            /// The render ROTATION (degrees) of a velocity direction (sprite long axis = +X, so
            /// <c>atan2(vel.y, vel.x)</c>); a degenerate velocity falls back to <paramref name="fallbackDeg"/>
            /// so the angle is never garbage. <b>History (owner playtest 2026-07-23):</b> the renderer used to
            /// call this per frame on the LIVE velocity — for a deposited shoulder (mostly-lateral spread +
            /// astern drift, decaying into the current) that painted the trail as rows of screen-horizontal
            /// dashes. Orientation is now BAKED at emit (<see cref="WakeParticleSystem.Particle.OrientDeg"/>;
            /// trail deposits bake the analytic arm direction via <see cref="WakeTrailMath.ArmOrientDeg"/>)
            /// and this stays as the pure emit-time fallback contract. Pure + static.
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

        /// <summary>Alpha levels of the code-built wake sprites. The KTC style law: PIXEL foam — banded +
        /// dithered coverage, never a smooth airbrush gradient.</summary>
        private const int FoamAlphaBands = 4;

        // 2×2 ordered-dither (Bayer) thresholds, as band fractions, indexed (x&1) + (y&1)*2 — the classic
        // pixel-art dither that breaks each band edge into a checker instead of a smooth ramp.
        private static readonly float[] BayerThresholds = { 0.2f, 0.6f, 0.8f, 0.4f };

        /// <summary>
        /// Quantize a 0..1 coverage into <see cref="FoamAlphaBands"/> hard alpha bands with a 2×2 ordered
        /// dither at the band edges — the KTC pixel-foam look (banded/dithered, no airbrush blobs).
        /// </summary>
        private static byte BandDitherAlpha(float a, int x, int y)
        {
            float bayer = BayerThresholds[(x & 1) + (y & 1) * 2];
            int band = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(a) * FoamAlphaBands + bayer), 0, FoamAlphaBands);
            return (byte)Mathf.RoundToInt(band * (255f / FoamAlphaBands));
        }

        /// <summary>
        /// Build a small, round foam puff sprite in code — point-filtered, alpha BANDED + DITHERED (the KTC
        /// pixel-foam law) so it reads as crisp pixel foam over the water, not an airbrush blob. Generating
        /// it avoids depending on the multiple-sprite-mode BoatWake.png (which
        /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> can't return) and keeps the whole effect self-contained: one
        /// shared sprite + one material for every puff (batched, rule 7).
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
                // Round falloff: solid core out to a dithered banded rim (KTC pixel foam, not airbrush).
                float a = Mathf.Clamp01(1f - d);
                a = a * a;                                    // tighten the core
                px[y * size + x] = new Color32(255, 255, 255, BandDitherAlpha(a, x, y));
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
                // Banded + dithered ends (KTC pixel foam) — the streak stays a crisp line, its taper steps.
                px[y * w + x] = new Color32(255, 255, 255, BandDitherAlpha(a, x, y));
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
            // The graded BOW SPRAY: ONE bow-anchored sibling renderer, same tier-swap + fallback discipline.
            private readonly Sprite[] _spraySprites;
            private readonly SpriteRenderer _sprayRenderer;
            // The BOW-WAVE droplets: a third pooled stream (short-lived flecks thrown off the cutwater and
            // left behind in world space). Same infra as the foam; its own tiny pool.
            private readonly WakeParticleSystem _dropletSys;
            private readonly SpriteRenderer[] _dropletRenderers;
            private float _emitCarry;
            private float _lineEmitCarry;
            // --- TRAIL DEPOSITION state (metres-of-track carries + the previous frame anchors) -------------
            private float _depositCarry;        // metres of stern travel since the last laid deposit
            private float _dropletCarry;        // fractional bow droplets carried between ticks
            private Vector2 _prevStern;         // where the stern anchor was last tick (world)
            private bool _hasPrevStern;
            private Vector2 _prevBow;           // last tick's bow direction (the plume turn-fade reads it)
            private bool _hasPrevBow;
            private uint _depositCounter;       // deterministic dice for the centre-churn fraction
            private readonly float _pulseSeed;  // per-boat churn-pulse phase (decorrelates boats; visual only)
            // The boat's hull presenter — the source of its artwork's bake elevation, read through the
            // seam (ADR 0022 phase 4) so a mesh hull answers the same question a sprite compass does.
            // Resolved lazily (a boat can be skinned after its wake rig was built); the HOST is preferred
            // live each tick because a hull swap (the dev picker, the A/B toggle) replaces the presenter.
            private IBoatHullPresenter _hullFallback;

            /// <summary>The elevation that means "a plan view": no foreshortening, today's placement. What a
            /// boat with no directional skin reads.</summary>
            private const float PlanViewElevationDegrees = 90f;

            public WakeRig(BoatController boat, int pool, int linePool, int dropletPool, Sprite foam, Sprite line,
                           Sprite[] tierSprites, Sprite[] spraySprites, Transform parent, string sortingLayer,
                           int sortingOrder, int lineSortingOrder, int plumeSortingOrder, int spraySortingOrder)
            {
                Boat = boat;
                _sys = new WakeParticleSystem(pool);
                _tierSprites = tierSprites;
                _spraySprites = spraySprites;
                _fallbackSprite = foam;
                // A stable per-boat phase for the churn pulses so two boats never boil in lockstep. Hashed
                // from the boat's name — presentation phase only, never a sim input.
                _pulseSeed = WakeParticleSystem.Hash01((uint)(boat.name != null ? boat.name.GetHashCode() : 0));

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

                // The BOW SPRAY renderer (one, created once) — the plume's sibling at the other end of the hull.
                var sprayGo = new GameObject("bowSpray");
                sprayGo.transform.SetParent(_root, worldPositionStays: false);
                _sprayRenderer = sprayGo.AddComponent<SpriteRenderer>();
                if (!string.IsNullOrEmpty(sortingLayer)) _sprayRenderer.sortingLayerName = sortingLayer;
                _sprayRenderer.sortingOrder = spraySortingOrder;
                sprayGo.SetActive(false);

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

                // The BOW-WAVE droplets: a third pooled slice (foam sprite, spray sorting band). Guarded so
                // a 0 pool cleanly disables the droplets.
                int dp = Mathf.Max(0, dropletPool);
                if (dp > 0 && foam != null)
                {
                    _dropletSys = new WakeParticleSystem(dp);
                    _dropletRenderers = BuildRenderers(dp, "bowDroplet", foam, sortingLayer, spraySortingOrder);
                }
            }

            /// <summary>The tier sprite for an index from a graded set (plume or spray), or the foam-puff fallback
            /// if that tier failed to load (so a bad art load never leaves an invisible effect). Null only if even
            /// the fallback is missing.</summary>
            private Sprite TierSpriteOrFallback(Sprite[] set, int tier)
            {
                if (set != null && tier >= 0 && tier < set.Length && set[tier] != null)
                    return set[tier];
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
                             in WakeGradeConfig grade, in BowSprayGradeConfig spray, Color sprayColor,
                             in WakeTrailConfig trail, in BowWaveConfig bowWave, in SeaLift lift)
            {
                if (Boat == null) return;

                Vector2 pos = Boat.transform.position;
                Vector2 bow = Boat.transform.up;
                float speed = Boat.Velocity.magnitude;
                bool aground = Boat.IsAground;
                float bakeElev = BakeElevationDegrees();

                // --- GRADE the wake by hull SIZE + WEIGHT + SPEED (the owner's brief). Static hull stats come
                // through the boat's public seam (rule 4); speed is live. The magnitude drives BOTH the plume
                // tier/scale AND a coherent growth of the foam footprint so the whole wake scales with the boat. ---
                float length = Boat.Hull != null ? Boat.Hull.LengthMeters : grade.LengthRefMin;
                float mass = Boat.Hull != null ? Boat.Hull.MassKg : grade.MassRefMin;
                float magnitude = WakeGrading.Magnitude01(length, mass, speed, grade);
                int tier = WakeGrading.TierIndex(magnitude, grade);
                float foamFactor = WakeGrading.FoamExtentFactor(magnitude, grade);

                // The plume's TURN RATE (deg/s) — the rigid straight-V sprite fades in a hard turn and hands
                // the read to the deposited trail, which actually curves with the track.
                float turnRate = _hasPrevBow ? WakeTrailMath.HeadingRateDegPerSec(_prevBow, bow, dt) : 0f;
                _prevBow = bow;
                _hasPrevBow = true;

                // Grow the foam bubbles + crisp Kelvin arms with the grade (a bigger hull's whole wake is bigger).
                // A per-tick copy of the tuned config — the pure functions take it by value, so this never mutates
                // the owner's serialized config and allocates nothing on the heap (struct copy).
                WakeConfig fcfg = ScaleFoamExtent(cfg, foamFactor);

                // --- GRADED PLUME (the boat-attached transom churn — now alive: pulse + turn fade) ---
                RenderPlume(pos, bow, speed, aground, magnitude, tier, length, bakeElev, foamColor, grade,
                            trail, turnRate, time, in lift);

                // --- GRADED BOW SPRAY (speed-forward, its own grade — gentle on the dory by onset) ---
                RenderSpray(pos, bow, speed, aground, length, mass, bakeElev, sprayColor, spray, bowWave, time,
                            in lift);

                // --- THE DEPOSITED TRAIL (owner ask 2026-07-23) --------------------------------------------
                // Deposits are laid at the STERN'S SWEPT TRACK — per metre travelled, not per second — and
                // then live their own life in world space (spread, advect, decay). The trail therefore
                // persists and CURVES where the boat actually went; nothing about it is glued to the hull.
                WakeConfig arm2 = ScaleFoamExtent(lineCfg.ArmConfig, foamFactor);
                if (trail.Enabled)
                {
                    DepositTrail(pos, bow, speed, aground, magnitude, length, bakeElev, fcfg, lineCfg, arm2,
                                 in trail);
                }
                else
                {
                    // The legacy boat-locked V stamp (the A/B escape hatch, owner-toggleable).
                    int count = WakeParticleSystem.EmissionCount(speed, aground, fcfg, dt, ref _emitCarry);
                    if (count > 0) _sys.Emit(count, pos, bow, speed, fcfg);
                    if (_lineSys != null)
                    {
                        int lineCount = WakeLineGeometry.EmissionCount(speed, aground, lineCfg, dt, ref _lineEmitCarry);
                        if (lineCount > 0) _lineSys.Emit(lineCount, pos, bow, speed, arm2);
                    }
                }

                // --- BOW-WAVE DROPLETS (thrown off the cutwater, left behind in world space) ---------------
                if (_dropletSys != null && bowWave.DropletsEnabled)
                    DepositBowDroplets(pos, bow, speed, aground, length, mass, bakeElev, spray, bowWave, dt);

                // --- STEP + RENDER every stream (advect with the current, dissipate, ride the displaced sea) ---
                _sys.Step(current, fcfg.VelocityDecay, dt);
                RenderFoam(roughness, time, fcfg, foamColor, in trail, in lift);

                if (_lineSys != null)
                {
                    _lineSys.Step(current, arm2.VelocityDecay, dt);
                    RenderLines(roughness, time, lineCfg, arm2, lineColor, in trail, in lift);
                }

                if (_dropletSys != null)
                {
                    _dropletSys.Step(current, bowWave.DropletVelocityDecay, dt);
                    RenderDroplets(time, bowWave, sprayColor, in lift);
                }
            }

            /// <summary>
            /// Lay this tick's TRAIL DEPOSITS along the stern's swept track: per metre of travel (the
            /// distance carry keeps the spacing exact across ticks), each deposit is two SHOULDER wavelets
            /// (into the crest-line pool) spreading outward at boatSpeed·tan(Kelvin angle) — the V that
            /// EMERGES in world space — plus, for a tunable fraction, one CENTRE churn puff (into the foam
            /// pool). Every particle is deposited at a WORLD point on the track with its birth strength
            /// baked, so the trail persists and curves after the boat has gone. Counts are hard-clamped per
            /// tick (pool safety, rule 7); a teleport-sized jump resets rather than draws a line across the
            /// map. All geometry is the pure, EditMode-tested <see cref="WakeTrailMath"/>.
            /// </summary>
            private void DepositTrail(Vector2 pos, Vector2 bow, float speed, bool aground, float magnitude,
                                      float hullLength, float bakeElevationDegrees, in WakeConfig fcfg,
                                      in WakeLineConfig lineCfg, in WakeConfig armCfg, in WakeTrailConfig trail)
            {
                // The trail is laid at the drawn transom: the same projected stern anchor the plume pins to
                // (hull half-length + nudge, foreshortened per artwork) — never the boat's centre.
                Vector2 stern = WakeGrading.SternAnchor(pos, bow, hullLength, trail.DepositAsternOffset,
                                                        bakeElevationDegrees);

                if (!_hasPrevStern)
                {
                    _prevStern = stern;
                    _hasPrevStern = true;
                    return;
                }

                Vector2 prev = _prevStern;
                float dist = (stern - prev).magnitude;

                // A teleport (region travel, dev picker) must not stripe foam across the map.
                if (dist > Mathf.Max(1f, trail.TeleportResetMeters))
                {
                    _prevStern = stern;
                    _depositCarry = 0f;
                    return;
                }

                _prevStern = stern;

                // Gates: no trail below the foam's own speed threshold, none aground (and drop the carry so
                // a restart doesn't burp a clump — the EmissionCount discipline).
                if (aground || speed <= fcfg.SpeedThreshold)
                {
                    _depositCarry = 0f;
                    return;
                }

                int deposits = WakeTrailMath.DepositCount(dist, trail.DepositSpacingMeters, ref _depositCarry,
                                                          trail.MaxDepositsPerTick);
                if (deposits <= 0) return;

                Vector2 trackDir = WakeTrailMath.TrackDir(prev, stern, bow);
                float spread = WakeTrailMath.ShoulderSpreadSpeed(speed, in trail);
                float halfWidth = WakeTrailMath.ShoulderHalfWidth(hullLength, magnitude, in trail);
                float lifeScale = WakeTrailMath.Graded(trail.LifetimeScaleAtMagnitude0,
                                                       trail.LifetimeScaleAtMagnitude1, magnitude);
                float sizeScale = WakeTrailMath.Graded(trail.SizeScaleAtMagnitude0,
                                                       trail.SizeScaleAtMagnitude1, magnitude);
                // Birth strength: the crest lines' own speed-onset ramp, baked at emit — a trail laid at
                // speed keeps reading at the strength it was laid with after the boat slows (the persistence
                // the owner asked for), while a barely-moving boat lays a faint trail.
                float lineBirth = WakeLineGeometry.SpeedOnset(speed, in lineCfg);
                float foamBirth = Mathf.Clamp01(WakeGrading.Ramp01(speed, fcfg.SpeedThreshold,
                                                                   Mathf.Max(0.2f, fcfg.SpeedThreshold)));

                int churnPuffs = WakeTrailMath.ChurnPuffCount(in trail);
                float churnHalf = WakeTrailMath.ChurnHalfWidth(hullLength, in trail);

                for (int i = 0; i < deposits; i++)
                {
                    float t = WakeTrailMath.DepositT(i, deposits);
                    Vector2 basePos = WakeTrailMath.PointOnTrack(prev, stern, t);

                    // Two shoulder wavelets — the emergent V arms (crest-line pool: thin oriented streaks).
                    // Orientation is BAKED along the analytic arm locus (ArmOrientDeg), never the decaying
                    // velocity — cause 1 of the "horizontal dashes" playtest read (2026-07-23).
                    if (_lineSys != null)
                    {
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector2 p = WakeTrailMath.ShoulderPoint(basePos, trackDir, side, halfWidth);
                            Vector2 v = WakeTrailMath.ShoulderVelocity(trackDir, side, spread, speed, in trail);
                            float orient = WakeTrailMath.ArmOrientDeg(trackDir, side, trail.KelvinHalfAngleDeg);
                            _lineSys.EmitAt(p, v, in armCfg, lifeScale, sizeScale, lineBirth, orient);
                        }
                    }

                    // The centre lane (foam pool) for a deterministic fraction of deposits — the long white
                    // wash down the middle of the trail, fading astern.
                    float dice = WakeParticleSystem.Hash01(_depositCounter * 0x9E3779B1u + 0x2Fu);
                    _depositCounter++;
                    if (dice < Mathf.Clamp01(trail.CenterChurnFraction))
                    {
                        Vector2 v = -trackDir * (speed * Mathf.Clamp01(trail.AsternDriftFraction));
                        _sys.EmitAt(basePos, v, in fcfg, lifeScale, sizeScale, foamBirth);
                    }

                    // The NEAR-STERN CHURN BAND ("bubble close to the boat, be foamy close to the boat"):
                    // big overlapping puffs jittered across the churn strip. Deliberately SHORT-lived —
                    // they die before the boat gets far, so the dense bubbling white band exists only
                    // right behind the transom and hands the read to the long pattern. Count hard-clamped
                    // per deposit (rule 7); lateral dice deterministic (rule 5).
                    for (int cpuff = 0; cpuff < churnPuffs; cpuff++)
                    {
                        float lat = WakeParticleSystem.Hash01(_depositCounter * 0x165667B1u + 0x51u) * 2f - 1f;
                        _depositCounter++;
                        Vector2 p = WakeTrailMath.ChurnPoint(basePos, trackDir, lat, churnHalf);
                        Vector2 v = -trackDir * (speed * Mathf.Clamp01(trail.AsternDriftFraction));
                        _sys.EmitAt(p, v, in fcfg, Mathf.Max(0.01f, trail.ChurnLifetimeScale),
                                    Mathf.Max(0.01f, trail.ChurnSizeScale) * sizeScale, foamBirth);
                    }
                }
            }

            /// <summary>
            /// Shed this tick's BOW-WAVE droplets: flecks thrown forward off the cutwater inside a fan, at a
            /// fraction of boat speed, which the boat then drives PAST — deposited in world space, they
            /// stream down the flanks and die in under a second: the "crashing against the bow" read. Rate
            /// rides the SAME dory-gentle speed-onset ramp as the spray sheet (a rowed dory sees a few
            /// flecks; the fast hulls the full spatter), count hard-clamped per tick (rule 7), fan ray +
            /// jitter deterministic from the droplet system's own emit counter (rule 5).
            /// </summary>
            private void DepositBowDroplets(Vector2 pos, Vector2 bow, float speed, bool aground,
                                            float hullLength, float massKg, float bakeElevationDegrees,
                                            in BowSprayGradeConfig spray, in BowWaveConfig bowWave, float dt)
            {
                float onset = BowSprayGrading.SpeedOnset(speed, in spray);
                if (aground) onset = 0f;
                int count = WakeTrailMath.DropletCount(onset, bowWave.DropletsPerSecond, dt, ref _dropletCarry,
                                                       bowWave.MaxDropletsPerTick);
                if (count <= 0) return;

                float magnitude = BowSprayGrading.Magnitude01(hullLength, massKg, speed, in spray);
                float sizeScale = 1f + Mathf.Max(0f, bowWave.DropletSizeMagnitudeBoost) * magnitude;
                Vector2 cutwater = BowSprayGrading.BowAnchor(pos, bow, hullLength, spray.SprayBowOffset,
                                                             bakeElevationDegrees);

                // A per-droplet config carrying the droplet lifetime/size so EmitAt's jitter machinery is
                // reused verbatim (struct copy — no allocation, the serialized configs untouched).
                WakeConfig dcfg = default;
                dcfg.Lifetime = Mathf.Max(0.05f, bowWave.DropletLifetime);
                dcfg.FoamSize = Mathf.Max(0.01f, bowWave.DropletSize);
                dcfg.LifetimeJitter = 0.3f;
                dcfg.SizeJitter = 0.35f;
                dcfg.StartAlpha = 1f;
                dcfg.FadePower = 1.2f;
                dcfg.SpreadFactor = 1.3f;

                for (int i = 0; i < count; i++)
                {
                    // The fan ray, deterministic per droplet (−1..1 across the fan).
                    float fan = WakeParticleSystem.Hash01(_depositCounter * 0x27D4EB2Fu + 0xB5u) * 2f - 1f;
                    _depositCounter++;
                    Vector2 v = WakeTrailMath.DropletVelocity(bow, speed, fan, in bowWave);
                    _dropletSys.EmitAt(cutwater, v, in dcfg, 1f, sizeScale, Mathf.Clamp01(onset));
                }
            }

            /// <summary>
            /// The bake elevation of THIS boat's current skin — what the plume and spray anchors must be
            /// projected through, because the hull is drawn by a ¾ camera while the anchors are computed in
            /// honest top-down world metres. Read off the boat's own
            /// <see cref="DirectionalBoatSprite.BakeElevationDegrees"/>, which the skinner sets from the
            /// artwork's <see cref="BoatVisualDef.ArtBakeElevationDegrees"/> — per-artwork, never a global
            /// sin(40°): the iso kits bake at 40, the hand-drawn compass the ambient fleet wears is not a bake
            /// at all and must not be foreshortened.
            ///
            /// <para>An unskinned boat (no compass component) has no baked camera to speak of, so it reads 90 —
            /// a plan view, i.e. exactly where its wake has always gone. The component is re-resolved while it
            /// is missing so a boat skinned AFTER its rig was built still finds it; once found it is cached,
            /// and a hull swap re-Configures the same component in place rather than replacing it.</para>
            /// </summary>
            private float BakeElevationDegrees()
            {
                if (Boat == null) return PlanViewElevationDegrees;

                // The live presenter first (no allocation: the host component holds it) — the skinner
                // republishes it on every hull apply, so a swap to a mesh hull (whose compass component
                // is destroyed) keeps answering with the new artwork's elevation instead of decaying to
                // the plan view.
                var host = Boat.GetComponent<BoatHullPresenterHost>();
                if (host != null && host.Presenter != null) return host.Presenter.BakeElevationDegrees;

                // Legacy fallback (a rig the skinner has not touched): wrap the compass once and keep it.
                if (_hullFallback == null)
                {
                    var directional = Boat.GetComponent<DirectionalBoatSprite>();
                    if (directional != null) _hullFallback = new SpriteHullPresenter(directional);
                }
                return _hullFallback != null ? _hullFallback.BakeElevationDegrees : PlanViewElevationDegrees;
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
                                     float hullLength, float bakeElevationDegrees, Color tint,
                                     in WakeGradeConfig grade, in WakeTrailConfig trail, float turnRateDegPerSec,
                                     float time, in SeaLift lift)
            {
                if (_plume == null) return;

                float onset = WakeGrading.SpeedOnset(speed, in grade);
                // The rigid straight-V sprite cannot bend: fade it with turn rate so a hard turn hands the
                // wake read to the deposited trail (which curves). 1 on a straight run.
                float turnFade = WakeTrailMath.TurnFade01(turnRateDegPerSec, in trail);
                Sprite sprite = TierSpriteOrFallback(_tierSprites, tier);
                if (!grade.PlumeEnabled || aground || onset <= 0f || turnFade <= 0f || sprite == null)
                {
                    if (_plume.gameObject.activeSelf) _plume.gameObject.SetActive(false);
                    return;
                }

                _plume.sprite = sprite;

                Vector2 apex = WakeGrading.SternAnchor(pos, bow, hullLength, grade.PlumeAsternOffset, bakeElevationDegrees);
                // The churn pulse: the transom wash BOILS (bounded, deterministic) instead of sitting glued.
                float scalePulse = WakeTrailMath.ChurnPulse(time, _pulseSeed, trail.PlumePulseHz,
                                                            trail.PlumePulseScaleAmount);
                float alphaPulse = WakeTrailMath.ChurnPulse(time, _pulseSeed + 0.37f, trail.PlumePulseHz,
                                                            trail.PlumePulseAlphaAmount);
                float scale = WakeGrading.PlumeScale(magnitude, in grade) * scalePulse;
                float angleDeg = WakeGrading.OrientAngleDeg(bow, grade.PlumeFlip);
                // Ride the displaced sea at the transom (0 when the displaced sea is off — the flat plane).
                float ride = lift.LiftAt(apex);

                var t = _plume.transform;
                t.position = new Vector3(apex.x, apex.y + ride, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
                t.localScale = new Vector3(scale, scale, 1f);
                var col = tint;
                col.a = Mathf.Clamp01(Mathf.Clamp01(grade.PlumeStartAlpha) * onset * turnFade * alphaPulse);
                _plume.color = col;
                if (!_plume.gameObject.activeSelf) _plume.gameObject.SetActive(true);
            }

            /// <summary>
            /// Draw the graded BOW SPRAY: the authored tier sprite pinned IMPACT-at-the-cutwater
            /// (<see cref="BowSprayGrading.BowAnchor"/> — half the hull ahead of the boat origin) with the
            /// droplet fan spreading ahead of the bow, tier-swapped + continuously scaled by the spray's own
            /// speed-forward magnitude and faded in by its speed-onset ramp. That ramp is what keeps the dory
            /// honest to the owner's brief: its rowed top speed only reaches the bottom of the ramp, so the
            /// slowest boat in the game shows at most a subtle gradual wisp while faster hulls earn the full
            /// sheet. Hidden at rest, when aground, when disabled, or with no usable sprite. One renderer, one
            /// shared sprite per tier — no allocation (rule 7); visual-only (rule 5).
            /// </summary>
            private void RenderSpray(Vector2 pos, Vector2 bow, float speed, bool aground,
                                     float hullLength, float massKg, float bakeElevationDegrees, Color tint,
                                     in BowSprayGradeConfig spray, in BowWaveConfig bowWave, float time,
                                     in SeaLift lift)
            {
                if (_sprayRenderer == null) return;

                float onset = BowSprayGrading.SpeedOnset(speed, in spray);
                float magnitude = BowSprayGrading.Magnitude01(hullLength, massKg, speed, in spray);
                int tier = BowSprayGrading.TierIndex(magnitude, in spray);
                Sprite sprite = TierSpriteOrFallback(_spraySprites, tier);
                if (!spray.SprayEnabled || aground || onset <= 0f || sprite == null)
                {
                    if (_sprayRenderer.gameObject.activeSelf) _sprayRenderer.gameObject.SetActive(false);
                    return;
                }

                _sprayRenderer.sprite = sprite;

                Vector2 impact = BowSprayGrading.BowAnchor(pos, bow, hullLength, spray.SprayBowOffset, bakeElevationDegrees);
                // The impact churn: the sheet CRASHES (a faster, deeper pulse than the plume's boil) instead
                // of sitting glued to the stem. Bounded + deterministic; 0 amounts restore the decal.
                float scalePulse = WakeTrailMath.ChurnPulse(time, _pulseSeed + 0.71f, bowWave.SprayPulseHz,
                                                            bowWave.SprayPulseScaleAmount);
                float alphaPulse = WakeTrailMath.ChurnPulse(time, _pulseSeed + 0.11f, bowWave.SprayPulseHz,
                                                            bowWave.SprayPulseAlphaAmount);
                float scale = BowSprayGrading.SprayScale(magnitude, in spray) * scalePulse;
                float angleDeg = WakeGrading.OrientAngleDeg(bow, spray.SprayFlip);
                // Ride the displaced sea at the cutwater — the bow wave climbs the swell with the bow.
                float ride = lift.LiftAt(impact);

                var t = _sprayRenderer.transform;
                t.position = new Vector3(impact.x, impact.y + ride, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
                t.localScale = new Vector3(scale, scale, 1f);
                var col = tint;
                col.a = Mathf.Clamp01(Mathf.Clamp01(spray.SprayStartAlpha) * onset * alphaPulse);
                _sprayRenderer.color = col;
                if (!_sprayRenderer.gameObject.activeSelf) _sprayRenderer.gameObject.SetActive(true);
            }

            private void RenderFoam(float roughness, float time, in WakeConfig cfg, Color foamColor,
                                    in WakeTrailConfig trail, in SeaLift lift)
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
                    // BirthStrength is baked at emit — a deposited trail keeps the brightness it was laid
                    // with even after the boat stops (template-emitted puffs carry 1, unchanged).
                    float alpha = WakeParticleSystem.LifeFade(life, cfg) * p.BirthStrength;
                    float sizeM = WakeParticleSystem.LifeSpread(p.BaseSize, life, cfg);
                    // The BUBBLING read ("bubble close to the boat"): fresh foam boils — size + alpha pulse
                    // at full amount at birth, easing to calm by end of life so only the near-stern churn
                    // band visibly bubbles. Render-only, bounded, deterministic (rule 5); gated on the
                    // trail so Enabled=false restores the legacy stamp byte-identically (the A/B contract).
                    if (trail.Enabled)
                    {
                        sizeM *= WakeTrailMath.AgedPulse(time, p.Seed, trail.FoamPulseHz,
                                                         trail.FoamPulseAmount, life);
                        alpha *= WakeTrailMath.AgedPulse(time, p.Seed + 0.53f, trail.FoamPulseHz,
                                                         trail.FoamPulseAmount * 0.6f, life);
                    }
                    Vector2 renderPos = WakeParticleSystem.RenderPosition(in p, time, roughness, cfg);
                    // Ride the displaced sea at the FOAM'S OWN position — laid foam heaves with the swell
                    // passing under it (display-only; the integrated position is untouched).
                    float ride = lift.LiftAt(p.Pos);

                    var t = sr.transform;
                    t.position = new Vector3(renderPos.x, renderPos.y + ride, 0f);
                    t.localScale = new Vector3(sizeM, sizeM, 1f);
                    var col = foamColor; col.a = alpha;
                    sr.color = col;
                    if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
                }
            }

            /// <summary>
            /// Draw the live BOW-WAVE droplets: tiny flecks fading fast, riding the displaced sea like every
            /// other wake element. Same pooled discipline as the foam (hidden when dead, never destroyed).
            /// </summary>
            private void RenderDroplets(float time, in BowWaveConfig bowWave, Color sprayColor, in SeaLift lift)
            {
                // A minimal per-tick render config for the life curves (struct copy, no allocation).
                WakeConfig rcfg = default;
                rcfg.StartAlpha = 1f;
                rcfg.FadePower = 1.2f;
                rcfg.SpreadFactor = 1.3f;

                var pool = _dropletSys.Pool;
                for (int i = 0; i < pool.Length; i++)
                {
                    var sr = _dropletRenderers[i];
                    ref readonly var p = ref pool[i];
                    if (!p.Alive)
                    {
                        if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                        continue;
                    }

                    float life = WakeParticleSystem.Life01(p.Age, p.Lifetime);
                    float alpha = WakeParticleSystem.LifeFade(life, rcfg) * p.BirthStrength;
                    float sizeM = WakeParticleSystem.LifeSpread(p.BaseSize, life, rcfg);
                    float ride = lift.LiftAt(p.Pos);

                    var t = sr.transform;
                    t.position = new Vector3(p.Pos.x, p.Pos.y + ride, 0f);
                    t.localScale = new Vector3(sizeM, sizeM, 1f);
                    var col = sprayColor; col.a = alpha;
                    sr.color = col;
                    if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
                }
            }

            private void RenderLines(float roughness, float time,
                                     in WakeLineConfig lineCfg, in WakeConfig arm, Color lineColor,
                                     in WakeTrailConfig trail, in SeaLift lift)
            {
                // The trail's arm streaks all share ONE rendered length — the OVERLAP LAW: at least
                // ArmOverlapFactor × the along-arm deposit spacing, so consecutive streaks fuse into a
                // continuous arm by construction (cause 2 of the dotted playtest read). Constant for life:
                // the alpha fade dissolves the arm; shrinking the streaks re-opened the gaps.
                float trailLengthM = trail.Enabled
                    ? WakeTrailMath.ArmStreakLength(trail.DepositSpacingMeters, trail.KelvinHalfAngleDeg,
                                                    trail.ArmOverlapFactor)
                    : 0f;

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
                    // subtler: their own StartAlpha (via LifeFade) is already the streak alpha, scaled by the
                    // BIRTH-BAKED onset (p.BirthStrength) — a streak laid at speed keeps its strength where it was
                    // laid instead of dimming with the boat's LIVE speed (the trail must survive the boat).
                    float alpha = WakeParticleSystem.LifeFade(life, arm) * Mathf.Clamp01(lineCfg.LineOpacity)
                                  * p.BirthStrength;
                    Vector2 renderPos = WakeParticleSystem.RenderPosition(in p, time, roughness, arm);
                    float ride = lift.LiftAt(p.Pos);

                    // Orient along the BAKED arm direction (p.OrientDeg, laid at emit down the emergent V's
                    // analytic locus — world-locked, so it never drifts as the velocity decays into the
                    // current; the velocity-following orientation was cause 1 of the "horizontal dashes"
                    // playtest read). The sprite's long axis is +X: stretch X to the streak length and Y to
                    // a thin cross-width, both WORLD-space targets converted to local scale by the sprite's
                    // native world size. The particle's BaseSize carries the cross-width seed.
                    float lengthM = trail.Enabled
                        ? trailLengthM
                        : WakeLineGeometry.StreakLength(p.BirthStrength, life, in lineCfg);
                    float widthM = Mathf.Max(0.001f, p.BaseSize * lineCfg.LineWidthScale);
                    float angleDeg = p.OrientDeg;

                    var t = sr.transform;
                    t.position = new Vector3(renderPos.x, renderPos.y + ride, 0f);
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
