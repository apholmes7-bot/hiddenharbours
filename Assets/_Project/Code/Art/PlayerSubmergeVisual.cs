using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The PURE, deterministic maths behind the wade SUBMERSION — the fraction of the way UP the body the
    /// waterline sits, given the water DEPTH over the player's feet. Split out (like
    /// <see cref="WadeSplashMath"/>) so the mapping is EditMode-testable headless (rule 5) and the
    /// MonoBehaviour shell stays thin. No RNG, no side effects, no <c>Time</c> — a pure function of its inputs.
    /// </summary>
    public static class PlayerSubmergeMath
    {
        /// <summary>
        /// The waterline fraction (0..maxSubmerge) — how far up the body, from the feet (0) toward the head
        /// (1), the water surface reaches, given the water <paramref name="depth"/> (m) over the feet:
        /// <list type="bullet">
        /// <item><description><b>Dry</b> (<paramref name="depth"/> ≤ 0, or
        /// <see cref="float.NegativeInfinity"/> when the region isn't tide-gated) → <b>0</b>: a
        /// PIXEL-IDENTICAL passthrough of the normal sprite (dry land looks exactly as today).</description></item>
        /// <item><description><b>Wading</b> → the depth as a fraction of the body height
        /// (<paramref name="bodyHeightMeters"/>): feet, then shins, then knees, then chest as it
        /// deepens.</description></item>
        /// <item><description><b>Neck-deep cap</b> → clamped at <paramref name="maxSubmerge"/> (≈ 0.85) so the
        /// HEAD (and hat) is NEVER submerged — even in the swim band at ~2 m the waterline stops at the
        /// neck.</description></item>
        /// </list>
        /// Monotonic non-decreasing in depth; deterministic; NaN/negative-safe (garbage inputs clamp, never
        /// propagate). <paramref name="bodyHeightMeters"/> ≤ 0 collapses safely to the cap for any positive
        /// depth rather than dividing by zero.
        /// </summary>
        /// <param name="depth">Water depth over the feet (m). ≤ 0 (or NegativeInfinity) is dry → 0.</param>
        /// <param name="bodyHeightMeters">The body height the depth is measured against (~1.8 m).</param>
        /// <param name="maxSubmerge">The neck-deep cap (0..1, ~0.85) — the head never submerges.</param>
        public static float WaterlineFraction(float depth, float bodyHeightMeters, float maxSubmerge)
        {
            float cap = Mathf.Clamp01(maxSubmerge);
            // Dry / un-gated (NegativeInfinity) / NaN → passthrough. (NaN compares false in every ordered test,
            // so guard it explicitly rather than letting it slip through as "not ≤ 0".)
            if (float.IsNaN(depth) || depth <= 0f) return 0f;

            float frac = bodyHeightMeters > 1e-4f ? depth / bodyHeightMeters : cap;
            return Mathf.Clamp(frac, 0f, cap);
        }

        /// <summary>
        /// Whether a control mode WADES — i.e. the player's own feet are in the water, so the water depth
        /// under them may drive the submersion shader. Only <see cref="ControlMode.OnFoot"/> does: on the
        /// DECK or at the helm the fisher stands on planking ABOVE the water, so however deep the sea under
        /// the hull, their body must read fully dry (the owner's "underwater animation on deck" bug). Pure +
        /// static so the gate is EditMode-testable.
        /// </summary>
        public static bool DrivesSubmersion(ControlMode mode) => mode == ControlMode.OnFoot;

        /// <summary>The depth the submersion driver may act on for a control mode: the real depth on foot,
        /// FULLY DRY (NegativeInfinity → a pixel-identical passthrough) aboard — deck or helm. Pure + static;
        /// the single rule <see cref="PlayerSubmergeVisual.Tick"/> reads through.</summary>
        public static float GatedDepth(ControlMode mode, float depth)
            => DrivesSubmersion(mode) ? depth : float.NegativeInfinity;
    }

    /// <summary>
    /// The on-foot SUBMERSION + UNDERWATER REFRACTION visual — as the fisher wades deeper, a WATERLINE rises
    /// up their body (feet hidden first, then shins, knees, chest), the submerged part reading UNDERWATER
    /// (tinted toward a water colour, DIMMED so it's submerged-but-visible, and REFRACTED — a small animated
    /// pixel-snapped horizontal shimmer) with a bright FOAM/RIPPLE line at the waterline. The head + hat never
    /// go under (a neck-deep cap). Your own body becomes the depth gauge (Pillars 1 &amp; 5: the sea has moods,
    /// cozy but with teeth — wading out on a falling tide is physical and a little risky). Pairs with the #163
    /// wade model and the #164 wade splashes: the same real wade depth drives all three.
    ///
    /// <para><b>Drives the PLAYER'S OWN shader (rule 4, seam-clean).</b> This drops onto the player's
    /// <see cref="SpriteRenderer"/> — the water plane can't warp a different sprite that sorts above it, so the
    /// warp lives on the player's material. It assigns a PER-PLAYER instance of the
    /// <c>HiddenHarbours/PlayerSubmerge</c> material (so batching / other sprites are untouched), and each
    /// frame reads the live water DEPTH over the player's feet and pushes the waterline (plus the animating
    /// sprite's texture) via a <see cref="MaterialPropertyBlock"/>. It reads the depth through the SAME Core
    /// services the wade model + water render read — <see cref="GameServices.TidalTerrain"/> /
    /// <see cref="GameServices.Environment"/> / <see cref="GameServices.Clock"/> composed via
    /// <see cref="TidalExposure.WaterDepth"/> — so render == sim: the waterline on the body is the exact same
    /// number the walkability gate and the water shader use. It never references the Player module (mirroring
    /// how <see cref="WadeSplashEmitter"/> stays seam-free); when there's no tide-gated terrain (a land scene,
    /// EditMode) the depth is dry and the effect is a pixel-identical passthrough.</para>
    ///
    /// <para><b>Overlay-compatible.</b> The shader is a plain Sprite-Unlit-shaped pass, so the day/night
    /// MULTIPLY overlay re-darkens the player afterward exactly as it does the normal player sprite and the
    /// SpriteShadow / wade-splash sprites — no special handling.</para>
    ///
    /// <para><b>Self-installing (mirrors <see cref="WadeSplashEmitter"/>).</b> A
    /// <see cref="RuntimeInitializeOnLoadMethod"/> host finds the persistent "Player" GameObject once the first
    /// scene is up and adds this component to its <see cref="SpriteRenderer"/> — so submersion works in every
    /// scene with NO builder change and it survives rebuilds. If the player already carries the component (e.g.
    /// added by the builder) the installer is a no-op.</para>
    ///
    /// <para><b>Determinism &amp; budget (rules 5 &amp; 7).</b> The waterline is the pure, unit-tested
    /// <see cref="PlayerSubmergeMath.WaterlineFraction"/> of the deterministic depth — nothing saved or
    /// randomised. Allocation-free per frame (one cached MPB, no GC), one texture fetch in the shader (the
    /// refraction just offsets the sample coord), and the material instance is minted ONCE. The refraction is
    /// pixel-snapped to the sprite's pixel grid so it stays crisp pixel-art. flipX-agnostic: the shader clips
    /// on uv.y (feet→head) only, so the mirrored Right facing submerges identically.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerSubmergeVisual : MonoBehaviour
    {
        private const string MaterialPath = "PlayerSubmerge";               // Resources/PlayerSubmerge.mat
        private const string ShaderName   = "HiddenHarbours/PlayerSubmerge";

        private static readonly int IdMainTex            = Shader.PropertyToID("_MainTex");
        private static readonly int IdWaterlineFrac      = Shader.PropertyToID("_WaterlineFrac");
        private static readonly int IdSubmergeTint       = Shader.PropertyToID("_SubmergeTint");
        private static readonly int IdSubmergeTintAmount = Shader.PropertyToID("_SubmergeTintAmount");
        private static readonly int IdSubmergeDim        = Shader.PropertyToID("_SubmergeDim");
        private static readonly int IdRefractAmount      = Shader.PropertyToID("_RefractAmount");
        private static readonly int IdRefractFrequency   = Shader.PropertyToID("_RefractFrequency");
        private static readonly int IdRefractSpeed       = Shader.PropertyToID("_RefractSpeed");
        private static readonly int IdWaterlineFoam      = Shader.PropertyToID("_WaterlineFoam");
        private static readonly int IdWaterlineFoamWidth = Shader.PropertyToID("_WaterlineFoamWidth");
        private static readonly int IdPixelsPerUnit      = Shader.PropertyToID("_PixelsPerUnit");
        private static readonly int IdSpriteHeightPx     = Shader.PropertyToID("_SpriteHeightPx");

        [Header("Depth → waterline mapping")]
        [Tooltip("The body height (m) the wade depth is measured against — the depth at which the waterline " +
                 "would reach the top of the sprite before the neck cap clamps it. FisherSheet ≈ 1.8 m of body.")]
        [Min(0.1f)] [SerializeField] private float _bodyHeightMeters = 1.8f;

        [Tooltip("The NECK-DEEP cap (0..1): the highest the waterline ever climbs, so the head + hat NEVER " +
                 "submerge — even in the swim band at ~2 m of water the line stops at the neck (~0.85).")]
        [Range(0f, 1f)] [SerializeField] private float _maxSubmerge = 0.85f;

        [Header("Underwater look (tunable — owner tunes the feel)")]
        [Tooltip("The water colour the submerged part of the body tints toward — a cool North-Atlantic green/blue.")]
        [SerializeField] private Color _submergeTint = new Color(0.16f, 0.34f, 0.44f, 1f);

        [Tooltip("How strongly the submerged body takes the water tint (0 = the sprite's own colour, 1 = fully " +
                 "the tint colour).")]
        [Range(0f, 1f)] [SerializeField] private float _submergeTintAmount = 0.45f;

        [Tooltip("How much the submerged part DIMS (0 = as bright as above water, 1 = black). Kept modest so " +
                 "the underwater body stays DIM-but-VISIBLE, not hidden.")]
        [Range(0f, 1f)] [SerializeField] private float _submergeDim = 0.35f;

        [Tooltip("Amplitude (in UV) of the underwater REFRACTION wobble — the horizontal shimmer of the " +
                 "submerged body. Subtle; snapped to the sprite's pixel grid so it stays pixel-art.")]
        [Range(0f, 0.2f)] [SerializeField] private float _refractAmount = 0.012f;

        [Tooltip("How many wobble cycles run up the submerged body (more = tighter ripples).")]
        [Min(0f)] [SerializeField] private float _refractFrequency = 9f;

        [Tooltip("How fast the refraction wobble animates.")]
        [Min(0f)] [SerializeField] private float _refractSpeed = 2.2f;

        [Header("Waterline ripple")]
        [Tooltip("Brightness (0..1) of the thin FOAM / ripple line right at the waterline where the body enters " +
                 "the water.")]
        [Range(0f, 1f)] [SerializeField] private float _waterlineFoam = 0.7f;

        [Tooltip("Half-height (in uv.y) of the foam band around the waterline — how thick the ripple reads.")]
        [Range(0.001f, 0.2f)] [SerializeField] private float _waterlineFoamWidth = 0.03f;

        [Header("Pixel snap")]
        [Tooltip("Pixels-per-unit of the sprite (match the project's PPU) — used to keep the refraction offset " +
                 "on the pixel grid.")]
        [Min(1f)] [SerializeField] private float _pixelsPerUnit = 32f;

        [Tooltip("How often (Hz) the waterline is recomputed from the tide depth. The tide is slow; a few Hz is " +
                 "plenty (the shimmer itself animates every frame on the GPU from _Time).")]
        [Min(1f)] [SerializeField] private float _refreshHz = 15f;

        private SpriteRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Material _instanceMaterial;      // the per-player material instance (minted once)
        private Material _originalMaterial;       // the player's original sharedMaterial (restored on teardown)
        private float _timer;
        private float _waterlineFrac;
        private float _spriteHeightPx = 64f;
        private Texture _lastTexture;
        // Where the player's control lives (Core signal): only ON FOOT does the wade depth drive the shader —
        // on the deck / at the helm the body is forced fully dry (standing on planking, not in the sea). Boot
        // starts ashore and every transition (and the region-arrival re-assert) republishes the mode.
        private ControlMode _mode = ControlMode.OnFoot;

        private void Reset() => _renderer = GetComponent<SpriteRenderer>();

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _mpb = new MaterialPropertyBlock();
            EnsureMaterial();
        }

        private void OnEnable()
        {
            if (_instanceMaterial != null && _renderer != null)
                _renderer.sharedMaterial = _instanceMaterial;
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
            _timer = 0f;
            Tick();   // correct on the first frame, not a stale default
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);
            // Restore the player's original material so a disabled component leaves the sprite exactly as it
            // was (no lingering shader). The per-player instance is kept for a re-enable, freed in OnDestroy.
            if (_renderer != null && _originalMaterial != null)
                _renderer.sharedMaterial = _originalMaterial;
        }

        /// <summary>Track the control mode (board / helm / ashore) and re-push the waterline IMMEDIATELY on a
        /// change — stepping onto the deck dries the body the same frame, not up to a refresh-tick later.
        /// Public so tests can drive the gate through the same path the bus uses (the established pattern).</summary>
        public void OnControlModeChanged(ControlModeChanged e)
        {
            if (_mode == e.Mode) return;
            _mode = e.Mode;
            Tick();
        }

        /// <summary>The control mode the driver last saw (the submersion gate's input). For tests/tooling.</summary>
        public ControlMode Mode => _mode;

        private void OnDestroy()
        {
            if (_instanceMaterial != null) Destroy(_instanceMaterial);
        }

        private void EnsureMaterial()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_instanceMaterial != null) return;

            _originalMaterial = _renderer != null ? _renderer.sharedMaterial : null;

            Material src = Resources.Load<Material>(MaterialPath);
            if (src == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader != null) src = new Material(shader) { name = "PlayerSubmerge (runtime)" };
            }
            if (src == null) return;   // no shader/material available → the component is inert (no effect)

            // A PER-PLAYER instance so pushing the waterline via the material can't touch any other sprite and
            // the shipped Resources material stays a clean template. Seed the tunables from the inspector once.
            _instanceMaterial = new Material(src) { name = "PlayerSubmerge (player)" };
            _instanceMaterial.SetColor(IdSubmergeTint, _submergeTint);
            _instanceMaterial.SetFloat(IdSubmergeTintAmount, _submergeTintAmount);
            _instanceMaterial.SetFloat(IdSubmergeDim, _submergeDim);
            _instanceMaterial.SetFloat(IdRefractAmount, _refractAmount);
            _instanceMaterial.SetFloat(IdRefractFrequency, _refractFrequency);
            _instanceMaterial.SetFloat(IdRefractSpeed, _refractSpeed);
            _instanceMaterial.SetFloat(IdWaterlineFoam, _waterlineFoam);
            _instanceMaterial.SetFloat(IdWaterlineFoamWidth, _waterlineFoamWidth);
            _instanceMaterial.SetFloat(IdPixelsPerUnit, _pixelsPerUnit);

            if (_renderer != null) _renderer.sharedMaterial = _instanceMaterial;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _refreshHz > 0f ? 1f / _refreshHz : 0.1f;
            Tick();
        }

        /// <summary>Read the live wade depth over the player's feet, map it to the waterline fraction, and
        /// push it (plus the animating frame's texture + the sprite's pixel height) to the material.</summary>
        private void Tick()
        {
            if (_renderer == null || _instanceMaterial == null) return;

            // Live water depth over the player's feet, read through the SAME Core services the wade model + the
            // water render use (render == sim). Seam-clean: no Player-module reference — compose the depth from
            // Core the way TidalWalkability.DepthNow does internally. GATED by control mode: only ON FOOT does
            // the depth drive the waterline — on the deck / at the helm the fisher stands on planking above the
            // water, so the body is forced fully dry (a pixel-identical passthrough) however deep the sea under
            // the hull. The buoys' bobbing waterline uses its own driver and is untouched.
            float depth = PlayerSubmergeMath.GatedDepth(_mode, DepthOverFeet(transform.position));
            _waterlineFrac = PlayerSubmergeMath.WaterlineFraction(depth, _bodyHeightMeters, _maxSubmerge);

            // The sprite may animate every frame; keep the material's texture + pixel height in sync so the
            // refraction snaps to the CURRENT frame's grid and the [PerRendererData] _MainTex renders it.
            Sprite spr = _renderer.sprite;
            Texture tex = spr != null ? spr.texture : null;
            if (spr != null)
            {
                // The sprite's pixel HEIGHT (feet→head), for the refraction pixel-snap. Cheap; only when it changes.
                if (tex != _lastTexture)
                {
                    _spriteHeightPx = Mathf.Max(1f, spr.rect.height);
                    _lastTexture = tex;
                }
            }

            _renderer.GetPropertyBlock(_mpb);
            if (tex != null) _mpb.SetTexture(IdMainTex, tex);
            _mpb.SetFloat(IdWaterlineFrac, _waterlineFrac);
            _mpb.SetFloat(IdSpriteHeightPx, _spriteHeightPx);
            _renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>The last-computed waterline fraction (0 dry .. neck cap). Exposed for tests / tooling.</summary>
        public float WaterlineFrac => _waterlineFrac;

        /// <summary>
        /// Water depth (m) over a world position, composed from the live Core services exactly as
        /// <c>TidalWalkability.DepthNow</c> does — but WITHOUT referencing the Player module (rule 4): read the
        /// tidal terrain + environment + clock straight off <see cref="GameServices"/> and combine with
        /// <see cref="TidalExposure.WaterDepth"/>. Returns <see cref="float.NegativeInfinity"/> (fully dry →
        /// passthrough) when the region isn't tide-gated (no terrain / no environment).
        /// </summary>
        private static float DepthOverFeet(Vector2 worldPos)
        {
            ITidalTerrain terrain = GameServices.TidalTerrain;
            IEnvironmentService env = GameServices.Environment;
            if (terrain == null || env == null) return float.NegativeInfinity;

            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            float waterLevel = env.WaterLevelAt(now);
            float ground = terrain.ElevationAt(worldPos);
            return TidalExposure.WaterDepth(waterLevel, ground);
        }

        // ==== self-install (mirrors WadeSplashEmitter) ====================================================

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("PlayerSubmergeInstaller") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<PlayerSubmergeInstaller>();
        }
    }

    /// <summary>
    /// A tiny persistent host that attaches <see cref="PlayerSubmergeVisual"/> to the on-foot player once it
    /// exists — the SELF-INSTALL path so the submersion effect needs no builder wiring and survives rebuilds
    /// (mirrors the way <see cref="WadeSplashEmitter"/> locates the player by name). It polls a few times for
    /// the persistent "Player" GameObject (it may not exist the instant the first scene loads), adds the
    /// component to its <see cref="SpriteRenderer"/> if not already present, then removes itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSubmergeInstaller : MonoBehaviour
    {
        [SerializeField] private string _playerObjectName = "Player";
        private float _timer;

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 0.5f;   // cheap poll — the player appears within a scene or two of boot

            var go = GameObject.Find(_playerObjectName);
            if (go == null) return;   // not up yet (menu scene / mid-load) — try again

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null && go.GetComponent<PlayerSubmergeVisual>() == null)
                go.AddComponent<PlayerSubmergeVisual>();

            // Whether or not we added it (it may already be there via the builder), our job is done.
            Destroy(gameObject);
        }
    }
}
