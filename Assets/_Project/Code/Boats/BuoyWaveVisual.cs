using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// THE WAVE-DRIVEN BUOY — a self-contained <b>visual-only</b> prop (Build 1 of the trap-fishing
    /// arc; NO traps/bait/soak/haul/placement gameplay — those are banked). A lobster buoy that rides
    /// the ONE shared deterministic wave field (P1 "The Sea Has Moods"): it BOBS on the same eased
    /// swell the boat rocks on, sits PARTIALLY SUBMERGED with a waterline that rises and falls as
    /// crests and troughs pass, and — the owner's exact ask — VANISHES COMPLETELY under a big crest,
    /// reappearing as the crest slides on.
    ///
    /// <para><b>Two proven seams, reused unchanged — this component only composes them:</b>
    /// <list type="bullet">
    /// <item><b>BOB</b> is the <see cref="BoatWaveMotion"/> pattern: a per-instance
    /// <see cref="WaveFieldAnimator"/> is ticked every frame with the game-time delta + the live
    /// <c>WindVector</c>/<c>SeaState01</c> from <see cref="GameServices"/>, then
    /// <see cref="WaveFieldAnimator.Sample"/> reads the surface under the buoy. Ticking the IDENTICAL
    /// animator code with the same inputs = the buoy rides the SAME eased sea the boat and the water
    /// shader do, by construction. The crest height offsets a CHILD visual transform's world +Y.</item>
    /// <item><b>SUBMERGE + VANISH</b> reuses the <c>HiddenHarbours/PlayerSubmerge</c> shader
    /// UNCHANGED (via a per-buoy material instance minted from <c>Resources/PlayerSubmerge.mat</c>,
    /// the <c>PlayerSubmergeVisual</c> pattern): each frame it pushes <c>_WaterlineFrac</c> — computed
    /// from the WAVE surface (<see cref="BuoyWaveMath.WaterlineFraction"/>), not tide depth — so the
    /// line climbs the flank as the local crest lifts past. The buoy gets its OWN tint/foam on the
    /// instance (the shader's shipped tints are seeded for a human body — never mutated here). The
    /// full VANISH is the sprite's own alpha fading to 0 under a crest taller than a
    /// <see cref="BuoyWaveMath.SwampThreshold"/> scaled off the field's <c>TotalAmplitude</c>.</item>
    /// </list></para>
    ///
    /// <para><b>Vanish vs awash (owner comparison toggle).</b> Default (owner's ask) is FULL VANISH.
    /// Flip <see cref="_swampMode"/> to <see cref="SwampMode.StayAwash"/> to instead drown the buoy —
    /// the waterline snaps to the top (<c>_WaterlineFrac</c> → 1, the whole buoy underwater-tinted/
    /// dimmed) while it stays visible — so the owner can compare the two reads. Ship default = vanish.</para>
    ///
    /// <para><b>Determinism &amp; rules.</b> The wave FIELD is deterministic (wind, seaState01,
    /// position, time — no RNG, nothing saved, rule 5); the animator is presentation-only stateful
    /// (documented on its class) and NEVER feeds sim or saved state — exactly the contract
    /// <see cref="BoatWaveMotion"/> documents for itself. This prop drives no gameplay-consequential
    /// state whatsoever. Allocation-free per frame (one cached MPB, one animator tick + one
    /// <c>Sample</c>); the material instance is minted ONCE (rule 7 — keep it to a handful of buoys).
    /// Reads the sim only through <see cref="GameServices"/> (rule 4 — Boats references Core only, and
    /// the shader is reused purely as a Resources material + MPB, with no Art-module code reference).
    /// Every amplitude/threshold/colour is a serialized tunable (rule 6).</para>
    ///
    /// <para>⚠ <b>Settings parity (ADR 0018 §(4)).</b> <see cref="_settings"/> /
    /// <see cref="_animatorSettings"/> start from the SAME <c>Default</c>s <see cref="BoatWaveMotion"/>
    /// and the Art-side <c>WaveFieldBridge</c> use, so the buoy rides the same waves the player sees.
    /// Until GameConfig unifies them, keep the field's shape identical across all three (or tune all
    /// three together); only the RESPONSE tunables here are buoy-specific.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuoyWaveVisual : MonoBehaviour
    {
        /// <summary>How a crest bigger than the swamp threshold reads (see the class doc).</summary>
        public enum SwampMode
        {
            /// <summary>The owner's ask: fade the sprite alpha to 0 — the buoy disappears under the crest.</summary>
            Vanish = 0,
            /// <summary>Comparison mode: keep it visible but fully awash (waterline → top, underwater-tinted/dimmed).</summary>
            StayAwash = 1,
        }

        private const string MaterialPath = "PlayerSubmerge";               // Resources/PlayerSubmerge.mat (reused UNCHANGED)
        private const string ShaderName   = "HiddenHarbours/PlayerSubmerge";

        private static readonly int IdMainTex            = Shader.PropertyToID("_MainTex");
        private static readonly int IdWaterlineFrac      = Shader.PropertyToID("_WaterlineFrac");
        private static readonly int IdSubmergeTint       = Shader.PropertyToID("_SubmergeTint");
        private static readonly int IdSubmergeTintAmount = Shader.PropertyToID("_SubmergeTintAmount");
        private static readonly int IdSubmergeDim        = Shader.PropertyToID("_SubmergeDim");
        private static readonly int IdWaterlineFoam      = Shader.PropertyToID("_WaterlineFoam");
        private static readonly int IdWaterlineFoamWidth = Shader.PropertyToID("_WaterlineFoamWidth");
        private static readonly int IdSpriteHeightPx     = Shader.PropertyToID("_SpriteHeightPx");

        [Header("Bob (the crest lifts the buoy)")]
        [Tooltip("Screen-vertical lift (world units) per metre of wave height under the buoy. The buoy floats HIGH on the water, so it reads a bigger bob than the heavy boat (BoatWaveMotion uses ~0.12) — a cork on the swell.")]
        [SerializeField] private float _bobPerMeter = 0.35f;
        [Tooltip("Hard cap on the bob (world units) so a freak sum of trains can't fling the sprite off the water.")]
        [SerializeField] private float _maxBob = 0.6f;

        [Header("Waterline (rides the wave up/down the buoy's side — reused PlayerSubmerge shader)")]
        [Tooltip("Resting draught (0 = base .. 1 = top): how far up the buoy the water sits in CALM. ~0.4 keeps it always low + wet, sitting ~mid-sprite in the water (tune by eye against the CENTER-pivot sprite).")]
        [Range(0f, 1f)] [SerializeField] private float _floatLineFrac = 0.4f;
        [Tooltip("The wave height (m) treated as 'rest' — the line sits at floatLineFrac here. 0 = the mean surface (the buoy's calm draught is at the average water level).")]
        [SerializeField] private float _restOffsetMeters = 0f;
        [Tooltip("The buoy's height (m) the crest climb is measured against — a bigger buoy has the same crest climb a SMALLER fraction of its side. The 16×32 sprite at 32 PPU is ~1 m tall.")]
        [Min(0.05f)] [SerializeField] private float _buoyHeightMeters = 1f;

        [Header("Vanish under a big crest (the owner's ask: 'larger waves make them disappear completely')")]
        [Tooltip("Default VANISH (fade to alpha 0) or StayAwash (waterline→top, stays visible) so the owner can compare. Ship default = Vanish.")]
        [SerializeField] private SwampMode _swampMode = SwampMode.Vanish;
        [Tooltip("Fraction of the field's TotalAmplitude a crest must reach to start swamping the buoy. Scaling off the envelope keeps it meaningful across sea states (~0.7 = only near-peak crests bury it).")]
        [Range(0f, 1.5f)] [SerializeField] private float _swampThresholdFraction = 0.7f;
        [Tooltip("Absolute floor (m) on the swamp threshold so a near-glass sea (tiny amplitude) never hides the buoy on a nothing crest.")]
        [Min(0f)] [SerializeField] private float _swampMinThresholdMeters = 0.6f;
        [Tooltip("Extra crest height (m) over the threshold across which the sprite fades 1→0 (Vanish) — small = a crisp duck-under, bigger = a softer fade.")]
        [Min(0.01f)] [SerializeField] private float _swampFadeBandMeters = 0.35f;

        [Header("Underwater look (the buoy's OWN tint/foam — the shared .mat is NEVER mutated)")]
        [Tooltip("The water colour the submerged part of the buoy tints toward — a cool North-Atlantic green/blue (seed the buoy's own, not the human-body default on the shipped material).")]
        [SerializeField] private Color _submergeTint = new Color(0.12f, 0.30f, 0.40f, 1f);
        [Tooltip("How strongly the submerged part takes the water tint (0 = the buoy's own colour, 1 = fully the tint).")]
        [Range(0f, 1f)] [SerializeField] private float _submergeTintAmount = 0.5f;
        [Tooltip("How much the submerged part dims (0 = as bright, 1 = black). Modest so the wet part stays visible.")]
        [Range(0f, 1f)] [SerializeField] private float _submergeDim = 0.3f;
        [Tooltip("Brightness (0..1) of the foam/ripple line at the waterline where the buoy meets the water.")]
        [Range(0f, 1f)] [SerializeField] private float _waterlineFoam = 0.75f;
        [Tooltip("Half-height (uv.y) of the foam band around the waterline — how thick the ripple reads.")]
        [Range(0.001f, 0.2f)] [SerializeField] private float _waterlineFoamWidth = 0.05f;

        [Header("Wiring (the spawner/builder sets these)")]
        [Tooltip("The buoy's SpriteRenderer — the material instance + the vanish alpha drive this. Null → the component finds one in its children on Awake.")]
        [SerializeField] private SpriteRenderer _renderer;
        [Tooltip("The CHILD visual transform the BOB offsets in world +Y (like the boat's visual child). NEVER the root the field samples at — that would move the sample point with the bob. Null → the renderer's transform is used (the buoy bobs about its own origin).")]
        [SerializeField] private Transform _visual;

        [Header("Wave field (parity: keep identical to BoatWaveMotion / WaveFieldBridge until GameConfig unifies them)")]
        [SerializeField] private WaveFieldSettings _settings = WaveFieldSettings.Default;
        [SerializeField] private WaveFieldAnimatorSettings _animatorSettings = WaveFieldAnimatorSettings.Default;

        private readonly WaveFieldAnimator _animator = new WaveFieldAnimator();
        private bool _hasLastTime;
        private double _lastTimeSeconds;

        private MaterialPropertyBlock _mpb;
        private Material _instanceMaterial;     // the per-buoy PlayerSubmerge instance (minted once)
        private Material _originalMaterial;     // the renderer's original sharedMaterial (restored on teardown)
        private float _spriteHeightPx = 32f;
        private Texture _lastTexture;

        private bool _baseCached;
        private Vector3 _baseLocalPosition;
        private Color _baseColor = Color.white;
        private bool _applied;

        /// <summary>The last-computed waterline fraction (0 base .. 1 top). Exposed for tests / tooling.</summary>
        public float WaterlineFrac { get; private set; }

        /// <summary>The last-computed sprite alpha (0 vanished .. 1 visible). Exposed for tests / tooling.</summary>
        public float VanishAlpha { get; private set; } = 1f;

        /// <summary>Wire the buoy from code (the spawner's path — mirrors <see cref="BoatWaveMotion.Configure"/>).</summary>
        public void Configure(SpriteRenderer renderer, Transform visual)
        {
            _renderer = renderer;
            _visual = visual;
            _baseCached = false;
        }

        private void Reset() => _renderer = GetComponentInChildren<SpriteRenderer>();

        private void Awake()
        {
            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_visual == null && _renderer != null) _visual = _renderer.transform;
            _mpb = new MaterialPropertyBlock();
            EnsureMaterial();
        }

        private void OnEnable()
        {
            _hasLastTime = false;     // fresh dt baseline — never ease across a disabled gap
            _animator.Reset();        // snap to the live weather on wake, don't chase a stale sea
            if (_instanceMaterial != null && _renderer != null)
                _renderer.sharedMaterial = _instanceMaterial;
        }

        private void OnDisable()
        {
            RestoreVisual();
            // Put the original material + colour back so a disabled buoy leaves its sprite untouched.
            if (_renderer != null)
            {
                if (_originalMaterial != null) _renderer.sharedMaterial = _originalMaterial;
                if (_baseCached) _renderer.color = _baseColor;
            }
        }

        private void OnDestroy()
        {
            if (_instanceMaterial != null) Destroy(_instanceMaterial);
        }

        private void EnsureMaterial()
        {
            if (_renderer == null || _instanceMaterial != null) return;

            _originalMaterial = _renderer.sharedMaterial;

            Material src = Resources.Load<Material>(MaterialPath);
            if (src == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader != null) src = new Material(shader) { name = "PlayerSubmerge (buoy runtime)" };
            }
            if (src == null) return;   // no shader/material (first checkout before import) → the component is inert

            // A PER-BUOY instance so pushing the waterline can't touch any other sprite and the shipped
            // Resources material stays a clean template. Seed the BUOY'S OWN tunables (NOT the human-body
            // defaults baked into the .mat) — the shared material is never mutated (rule 5).
            _instanceMaterial = new Material(src) { name = "PlayerSubmerge (buoy)" };
            _instanceMaterial.SetColor(IdSubmergeTint, _submergeTint);
            _instanceMaterial.SetFloat(IdSubmergeTintAmount, _submergeTintAmount);
            _instanceMaterial.SetFloat(IdSubmergeDim, _submergeDim);
            _instanceMaterial.SetFloat(IdWaterlineFoam, _waterlineFoam);
            _instanceMaterial.SetFloat(IdWaterlineFoamWidth, _waterlineFoamWidth);
            _renderer.sharedMaterial = _instanceMaterial;
        }

        private void LateUpdate()
        {
            if (_renderer == null) return;

            if (!_baseCached)
            {
                _baseLocalPosition = _visual != null ? _visual.localPosition : Vector3.zero;
                _baseColor = _renderer.color;
                _baseCached = true;
            }

            // --- Sample the shared eased field under the buoy (the BoatWaveMotion pattern) ---------
            // Game-time delta for this presentation tick (a paused clock yields dt 0 and the sea freezes).
            double time = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : Time.timeAsDouble;
            float dt = _hasLastTime ? Mathf.Max(0f, (float)(time - _lastTimeSeconds)) : Time.deltaTime;
            _lastTimeSeconds = time;
            _hasLastTime = true;

            WaveSample wave;
            float totalAmplitude;
            var env = GameServices.Environment;
            if (env != null)
            {
                EnvironmentSample sample = env.Sample();
                WaveTrains trains = _animator.Tick(dt, sample.WindVector, sample.SeaState01, in _settings, in _animatorSettings);
                wave = _animator.Sample((Vector2)transform.position);   // sample at the ROOT, not the bobbing visual
                totalAmplitude = trains.TotalAmplitude;
            }
            else
            {
                wave = WaveSample.Flat;   // no sim, no sea — dead still (EditMode / pre-boot), never stale
                totalAmplitude = 0f;
            }

            // --- Turn the one Height read into bob + waterline + vanish (the pure math) ------------
            float bob = BuoyWaveMath.BobOffset(wave.Height, _bobPerMeter, _maxBob);
            float waterline = BuoyWaveMath.WaterlineFraction(wave.Height, _floatLineFrac, _restOffsetMeters, _buoyHeightMeters);
            float swampThreshold = BuoyWaveMath.SwampThreshold(totalAmplitude, _swampThresholdFraction, _swampMinThresholdMeters);
            float alpha = BuoyWaveMath.VanishAlpha(wave.Height, swampThreshold, _swampFadeBandMeters);

            if (_swampMode == SwampMode.StayAwash)
            {
                // Comparison mode: don't vanish — drown it. A crest over the threshold snaps the
                // waterline to the top (whole buoy awash, underwater-tinted/dimmed) and keeps it visible.
                waterline = Mathf.Max(waterline, 1f - alpha);   // alpha 1→0 over the band ⇒ waterline →1
                alpha = 1f;
            }

            WaterlineFrac = waterline;
            VanishAlpha = alpha;

            Apply(bob, waterline, alpha);
        }

        private void Apply(float bob, float waterline, float alpha)
        {
            // BOB: base local pose first, then the lift in WORLD +Y (screen-up), like the boat visual.
            if (_visual != null)
            {
                _visual.localPosition = _baseLocalPosition;
                _visual.position += new Vector3(0f, bob, 0f);
            }

            // WATERLINE + the animating frame's texture/height (the PlayerSubmergeVisual push).
            Sprite spr = _renderer.sprite;
            Texture tex = spr != null ? spr.texture : null;
            if (spr != null && tex != _lastTexture)
            {
                _spriteHeightPx = Mathf.Max(1f, spr.rect.height);
                _lastTexture = tex;
            }

            _renderer.GetPropertyBlock(_mpb);
            if (tex != null) _mpb.SetTexture(IdMainTex, tex);
            _mpb.SetFloat(IdWaterlineFrac, waterline);
            _mpb.SetFloat(IdSpriteHeightPx, _spriteHeightPx);
            _renderer.SetPropertyBlock(_mpb);

            // VANISH: fade the sprite's own alpha (folded into the shader via IN.color) — the full
            // disappear under a crest, continuous, no stored state. Preserve the base RGB/alpha tint.
            var c = _baseColor;
            c.a = _baseColor.a * alpha;
            _renderer.color = c;

            _applied = true;
        }

        /// <summary>Put the visual back exactly as built (base pose, full colour, no waterline). Idempotent.</summary>
        private void RestoreVisual()
        {
            if (!_applied) return;
            _applied = false;
            if (!_baseCached) return;
            if (_visual != null) _visual.localPosition = _baseLocalPosition;
            if (_renderer != null)
            {
                _renderer.color = _baseColor;
                if (_mpb != null)
                {
                    _renderer.GetPropertyBlock(_mpb);
                    _mpb.SetFloat(IdWaterlineFrac, 0f);
                    _renderer.SetPropertyBlock(_mpb);
                }
            }
        }
    }
}
