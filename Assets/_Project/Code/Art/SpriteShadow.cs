using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// A drop-on PROJECTED SPRITE SHADOW (PR 2, ADR 0013 §"Projected shadows"). Attach it to any
    /// <see cref="SpriteRenderer"/> caster (the player, a boat, a tree, a post, a building) and it draws a
    /// flat, dark, semi-transparent, SHEARED + LENGTH-SCALED copy of that sprite on the ground — anchored at
    /// the caster's FEET — that swings and lengthens with the sun across the day: long WEST at dawn, a short
    /// NORTHWARD stub at noon, long EAST at dusk. The player reads the time of day from their shadow
    /// (P1 "The Sea Has Moods"). It fades to nothing at night and softens under overcast (the weather hook).
    ///
    /// <para><b>Reads the globals the controller already pushes — no new wiring.</b> It consumes
    /// <c>_SunDir</c> + <c>_SunElevation</c> (the sun's heading + height, for the swing/length) and
    /// <c>_ShadowStrength</c> (how firmly the shadow reads NOW — the sun being up folded with the LIVE
    /// weather; this is the weather hook, so overcast/storm genuinely softens the shadow in-game), all
    /// published every tick by <see cref="DayNightController"/>, plus the owner's
    /// <see cref="DayNightProfile"/> shadow-arc tuning. It evaluates the PURE <see cref="DayNightMath"/>
    /// projection (length / skew / alpha — all unit-tested headless) and feeds the result to ONE shared
    /// <c>HiddenHarbours/SpriteShadow</c> material (the shear runs in the shader's vertex stage; every caster
    /// shares the one material via a <see cref="MaterialPropertyBlock"/> — GPU-batch friendly, CLAUDE.md
    /// rule 7). When the cycle isn't running (a bare art scene / EditMode, no sim) it falls back to a tunable
    /// daylight hour and computes the strength locally (no weather) so the shadow still shows.</para>
    ///
    /// <para><b>Mirrors <see cref="CottageDayNight"/>'s drop-on pattern.</b> No scene wiring beyond attaching
    /// it (the editor menu "Hidden Harbours ▸ Lighting ▸ Add Sprite Shadow to Selection" batch-adds it; the
    /// "Build Shadow Test" demo shows it off). World-content / the menu add it to real casters later.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> The shadow is a pure function of <c>(hour, weather, profile, caster
    /// height)</c> — nothing is saved or randomised. <b>Performance (rule 7):</b> the child shadow renderer is
    /// created ONCE and POOLED (reused every frame), updated on a throttled tick with NO per-frame allocation;
    /// the heavy shear is on the GPU. <b>Pixel-art faithful:</b> the shadow position is pixel-snapped to the
    /// project's PPU grid (toggleable).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteShadow : MonoBehaviour
    {
        private const string ShadowMaterialPath = "SpriteShadow";          // Resources/SpriteShadow.mat
        private const string ShadowShaderName   = "HiddenHarbours/SpriteShadow";

        private static readonly int IdMainTex      = Shader.PropertyToID("_MainTex");
        private static readonly int IdShadowColor  = Shader.PropertyToID("_ShadowColor");
        private static readonly int IdShadowDir    = Shader.PropertyToID("_ShadowDir");
        private static readonly int IdShadowLen    = Shader.PropertyToID("_ShadowLen");
        private static readonly int IdEdgeSoftness = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int IdSunDir        = Shader.PropertyToID("_SunDir");
        private static readonly int IdSunElevation  = Shader.PropertyToID("_SunElevation");
        private static readonly int IdShadowStrength = Shader.PropertyToID("_ShadowStrength");

        // ONE shared fallback material for the missing-Resources path, minted at most once across ALL
        // casters (the normal path loads Resources/SpriteShadow.mat). A per-instance Material here would
        // leak one material per caster and break the shared-material GPU batching this component relies on.
        private static Material _sharedFallbackMaterial;

        [Header("Darkness")]
        [Tooltip("The darkest the shadow ever gets (its alpha at a firm clear noon). Scaled DOWN by the sun " +
                 "being low and by overcast — so this is the cap, not the constant. 0 = invisible, 1 = solid.")]
        [Range(0f, 1f)] [SerializeField] private float _maxAlpha = 0.45f;

        [Tooltip("The flat shadow colour (RGB). Near-black with a hint of the cool sky reads best on the " +
                 "North-Atlantic palette; pure black is harsher.")]
        [SerializeField] private Color _shadowColor = new Color(0.04f, 0.05f, 0.10f, 1f);

        [Header("Length (× the caster's height)")]
        [Tooltip("Shadow length at NOON (sun overhead), as a multiple of the caster's height — a short stub " +
                 "under the feet. 0.3..0.5 reads well.")]
        [Min(0f)] [SerializeField] private float _lengthAtNoon = 0.35f;

        [Tooltip("Shadow length at a LOW sun (near the horizon), as a multiple of the caster's height — a " +
                 "long dawn/dusk rake. Bigger = more dramatic raking shadows.")]
        [Min(0f)] [SerializeField] private float _lengthAtHorizon = 5f;

        [Tooltip("Hard CAP on the shadow length (× height) so a near-horizon sun can't shoot the silhouette " +
                 "off-screen toward infinity. Keeps dawn/dusk shadows long-but-bounded.")]
        [Min(0f)] [SerializeField] private float _maxLength = 7f;

        [Header("Look")]
        [Tooltip("Edge feather of the silhouette (0 = crisp pixel cutout — the pixel-art default; up to 1 = " +
                 "soft-edged). The shape is always the caster's own sprite alpha.")]
        [Range(0f, 1f)] [SerializeField] private float _edgeSoftness = 0f;

        [Tooltip("How far UNDER the caster the shadow sorts (sorting-order offset, negative = behind). The " +
                 "shadow must draw beneath its caster and beneath things in front.")]
        [SerializeField] private int _sortingOffset = -1;

        [Tooltip("Snap the shadow's anchor to the pixel grid so it stays crisp pixel art (no shimmer as it " +
                 "swings). Off = smooth sub-pixel motion.")]
        [SerializeField] private bool _pixelSnap = true;

        [Tooltip("Pixels-per-unit the snap uses (match the project's sprite PPU). Ignored when snap is off.")]
        [Min(1f)] [SerializeField] private float _pixelsPerUnit = 32f;

        [Header("Caster")]
        [Tooltip("World-Y of the caster's FEET below its transform origin (metres). 0 = the transform sits at " +
                 "the feet. Used to anchor the shadow at the ground and to measure the caster's height.")]
        [SerializeField] private float _footOffset = 0f;

        [Header("Sun (when no clock is running)")]
        [Tooltip("Hour (0..24) used for the sun arc when the day/night cycle isn't pushing the globals yet " +
                 "(EditMode / a bare art scene), so the demo shadow still shows. Ignored once the cycle runs.")]
        [Range(0f, 24f)] [SerializeField] private float _fallbackHour = 10f;

        [Tooltip("How often (Hz) the shadow is recomputed. The sun is slow; a few Hz is plenty and cheap.")]
        [Min(1f)] [SerializeField] private float _refreshHz = 10f;

        [Tooltip("Optional explicit profile for the shadow arc + weather fade. Leave empty to use " +
                 "Resources/DayNightProfile (the same the controller uses), or a built-in default.")]
        [SerializeField] private DayNightProfile _profile;

        private SpriteRenderer _caster;
        private SpriteRenderer _shadow;          // the pooled child renderer (created once)
        private MaterialPropertyBlock _mpb;
        private DayNightProfile _resolvedProfile;
        private float _timer;
        private Sprite _lastSprite;

        private void Reset() => _caster = GetComponent<SpriteRenderer>();

        private void Awake()
        {
            _caster = GetComponent<SpriteRenderer>();
            _mpb = new MaterialPropertyBlock();
            _resolvedProfile = _profile != null ? _profile : Resources.Load<DayNightProfile>("DayNightProfile");
            if (_resolvedProfile == null) _resolvedProfile = DayNightProfile.CreateDefault();
            EnsureShadow();
        }

        private void OnEnable()
        {
            if (_shadow != null) _shadow.enabled = true;
            _timer = 0f;
            Tick();   // correct on the first frame, not a stale default
        }

        private void OnDisable()
        {
            if (_shadow != null) _shadow.enabled = false;   // pooled, not destroyed — reused on re-enable
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _refreshHz > 0f ? 1f / _refreshHz : 0.1f;
            Tick();
        }

        // The pose must follow the caster every frame (it can move/animate faster than the throttle); the
        // POSE is cheap, the heavier light recompute stays throttled in Update.
        private void LateUpdate() => PoseShadow();

        private void EnsureShadow()
        {
            if (_shadow != null) return;
            if (_caster == null) _caster = GetComponent<SpriteRenderer>();

            var go = new GameObject("SpriteShadow") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, worldPositionStays: false);
            _shadow = go.AddComponent<SpriteRenderer>();
            _shadow.sortingLayerID = _caster != null ? _caster.sortingLayerID : 0;

            Material mat = Resources.Load<Material>(ShadowMaterialPath);
            if (mat == null)
            {
                // Missing Resources material: mint ONE shared fallback for all casters (not per-instance —
                // that would leak + break batching). Reused on every subsequent caster.
                if (_sharedFallbackMaterial == null)
                {
                    var shader = Shader.Find(ShadowShaderName);
                    if (shader != null)
                        _sharedFallbackMaterial = new Material(shader) { name = "SpriteShadow (runtime shared)" };
                }
                mat = _sharedFallbackMaterial;
            }
            if (mat != null) _shadow.sharedMaterial = mat;
            else _shadow.enabled = false;   // no shader/material yet -> no shadow (still harmless)
        }

        /// <summary>Read the sun + shadow-strength globals (or the fallback hour) and push the projection to the shadow material.</summary>
        private void Tick()
        {
            if (_shadow == null || _caster == null) return;

            // Keep the silhouette in sync with the caster's current sprite (it may animate).
            if (_caster.sprite != _lastSprite)
            {
                _shadow.sprite = _caster.sprite;
                _lastSprite = _caster.sprite;
            }

            DayNightProfile p = _resolvedProfile;
            float sunrise = p != null ? p.SunriseHour : 6f;
            float sunset  = p != null ? p.SunsetHour : 20f;
            float bias    = p != null ? p.ShadowSouthBias : 0.2f;
            float lift    = p != null ? p.ShadowNoonLift : 0.9f;
            float overcastFades = p != null ? p.OvercastFadesShadow : 0.85f;

            // Prefer the LIVE globals the controller publishes; fall back to evaluating the arc ourselves at a
            // daylight hour so a bare art scene (no cycle) still shows a shadow.
            float elevation;
            Vector2 shadowDir;

            Vector4 gSun = Shader.GetGlobalVector(IdSunDir);
            float gElev = Shader.GetGlobalFloat(IdSunElevation);
            bool cycleRunning = gSun.sqrMagnitude > 1e-6f || Mathf.Abs(gElev) > 1e-6f;
            if (cycleRunning)
            {
                elevation = gElev;
                shadowDir = new Vector2(-gSun.x, -gSun.y);          // shadow runs opposite the sun
                if (shadowDir.sqrMagnitude < 1e-6f)
                    shadowDir = DayNightMath.ShadowDirection(_fallbackHour, sunrise, sunset, bias, lift);
            }
            else
            {
                elevation = DayNightMath.SunElevation(_fallbackHour, sunrise, sunset);
                shadowDir = DayNightMath.ShadowDirection(_fallbackHour, sunrise, sunset, bias, lift);
            }
            shadowDir = shadowDir.sqrMagnitude > 1e-6f ? shadowDir.normalized : Vector2.up;

            // Alpha = maxAlpha × ShadowStrength (folds the sun being up + the weather). When the live cycle is
            // on, READ the controller's published _ShadowStrength global — it already folds the LIVE weather
            // (overcast/storm fades the shadow, OvercastFadesShadow live), computed once per tick where the
            // real sim is, so OvercastFadesShadow takes effect in-game. Off the cycle (a bare art scene, no
            // sim) we evaluate the arc locally at the fallback hour with no weather.
            float strength = cycleRunning
                ? Mathf.Clamp01(Shader.GetGlobalFloat(IdShadowStrength))
                : DayNightMath.ShadowStrength(_fallbackHour, sunrise, sunset, 0f, overcastFades);
            float alpha = DayNightMath.ShadowAlpha(_maxAlpha, strength);

            // Length multiplier (× height) from the elevation, clamped so dawn/dusk don't shoot to infinity.
            float lenMul = DayNightMath.ShadowLength(elevation, _lengthAtNoon, _lengthAtHorizon, _maxLength);

            // Convert the world shear length into the shadow sprite's LOCAL-Y units (the shader shears in
            // object space, scaled by uv.y feet->head). worldHeight = caster height; localHeight = the sprite
            // quad's local height; their ratio maps world length -> local length so 1 sprite-height of shear
            // == the sprite's own height regardless of PPU/scale.
            float worldHeight = CasterWorldHeight();
            float worldLen = lenMul * worldHeight;
            float localLen = WorldToLocalShearLength(worldLen);

            var color = new Color(_shadowColor.r, _shadowColor.g, _shadowColor.b, alpha);

            _shadow.GetPropertyBlock(_mpb);
            if (_caster.sprite != null && _caster.sprite.texture != null)
                _mpb.SetTexture(IdMainTex, _caster.sprite.texture);
            _mpb.SetColor(IdShadowColor, color);
            _mpb.SetVector(IdShadowDir, new Vector4(shadowDir.x, shadowDir.y, 0f, 0f));
            _mpb.SetFloat(IdShadowLen, localLen);
            _mpb.SetFloat(IdEdgeSoftness, _edgeSoftness);
            _shadow.SetPropertyBlock(_mpb);

            // Sort just UNDER the caster.
            _shadow.sortingLayerID = _caster.sortingLayerID;
            _shadow.sortingOrder = _caster.sortingOrder + _sortingOffset;
            _shadow.enabled = _caster.enabled && alpha > 0f && _shadow.sharedMaterial != null;
        }

        /// <summary>Pose the pooled shadow child at the caster's feet (every frame; cheap, no alloc).</summary>
        private void PoseShadow()
        {
            if (_shadow == null || _caster == null) return;

            // Anchor at the caster's feet (its origin shifted DOWN by the foot offset). The shadow always lies
            // FLAT on the world ground plane (identity world rotation) so the shear — applied along the
            // WORLD-space _ShadowDir in the vertex stage — stays correct even if the caster sprite itself
            // rotates (a top-down boat turning); only its on-screen size tracks the caster (localScale 1, so it
            // inherits the parent's scale and matches the caster's footprint).
            Vector3 footWorld = transform.TransformPoint(new Vector3(0f, -_footOffset, 0f));
            if (_pixelSnap && _pixelsPerUnit > 0f)
            {
                // Snap the WORLD anchor to the pixel grid so the swinging shadow stays crisp pixel art.
                float ppu = _pixelsPerUnit;
                footWorld.x = Mathf.Round(footWorld.x * ppu) / ppu;
                footWorld.y = Mathf.Round(footWorld.y * ppu) / ppu;
            }
            _shadow.transform.position = footWorld;
            _shadow.transform.rotation = Quaternion.identity;
            _shadow.transform.localScale = Vector3.one;
        }

        /// <summary>The caster's on-screen height in world units (sprite bounds × lossy Y scale).</summary>
        private float CasterWorldHeight()
        {
            if (_caster != null && _caster.sprite != null)
                return _caster.sprite.bounds.size.y * Mathf.Abs(transform.lossyScale.y);
            return Mathf.Abs(transform.lossyScale.y);   // fallback: 1 unit at unit scale
        }

        /// <summary>
        /// Map a shear length in WORLD units to the shadow sprite's LOCAL-Y units (the shader shears in object
        /// space). Local height of the sprite quad is its bounds.size.y; world height multiplies by the scale.
        /// So localLen = worldLen / lossyScaleY.
        /// </summary>
        private float WorldToLocalShearLength(float worldLen)
        {
            float scaleY = Mathf.Abs(_shadow != null ? _shadow.transform.lossyScale.y : transform.lossyScale.y);
            return scaleY > 1e-5f ? worldLen / scaleY : worldLen;
        }
    }
}
