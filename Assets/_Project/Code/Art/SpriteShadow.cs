using UnityEngine;
using HiddenHarbours.Core;   // SortingBands — the ONE place the sorting axis is partitioned

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
    /// <para><b>It is anchored at the caster's PIVOT.</b> A projected shadow is pinned where its caster meets
    /// the ground, and that point is the pivot — the feet by contract across every rig family (ADR 0026: a
    /// tree's pivot is its TRUNK FOOT, a character's is <c>(0.5, GroundInsetPx/cellH)</c>, a shrub's and a
    /// shore plant's is the root crown). So the shear is proportional to height ABOVE THE PIVOT, which
    /// <see cref="PivotShearMap(Sprite)"/> derives from the sprite's own rect / pivot / sheet height / PPU and
    /// publishes for the vertex stage. It is NOT proportional to raw <c>uv.y</c>: uv is TEXTURE space, and every
    /// caster in production is a sliced sheet, so raw uv.y both misses the pivot and barely varies across a cell
    /// on a multi-row sheet. A caster whose pivot IS its cell-bottom-centre and whose sprite fills its texture
    /// gets <see cref="IdentityShearMap"/> and draws exactly as it always did.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> The shadow is a pure function of <c>(hour, weather, profile, caster
    /// height)</c> — nothing is saved or randomised. <b>Performance (rule 7):</b> the child shadow renderer is
    /// created ONCE and POOLED (reused every frame), updated on a throttled tick with NO per-frame allocation;
    /// the heavy shear is on the GPU. Per frame a caster costs only the pose and one sprite-reference compare
    /// — static decor never gets past that compare, and only an ANIMATED caster (the walking player) pays for
    /// the silhouette swap. <b>Pixel-art faithful:</b> the shadow position is pixel-snapped to the project's
    /// PPU grid (toggleable).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteShadow : MonoBehaviour, ILampShadowCaster
    {
        private const string ShadowMaterialPath = "SpriteShadow";          // Resources/SpriteShadow.mat
        private const string ShadowShaderName   = "HiddenHarbours/SpriteShadow";

        private static readonly int IdMainTex      = Shader.PropertyToID("_MainTex");
        private static readonly int IdShadowColor  = Shader.PropertyToID("_ShadowColor");
        private static readonly int IdShadowDir    = Shader.PropertyToID("_ShadowDir");
        private static readonly int IdShadowLen    = Shader.PropertyToID("_ShadowLen");
        private static readonly int IdShadowUV     = Shader.PropertyToID("_ShadowUV");
        private static readonly int IdEdgeSoftness = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int IdGroundContact = Shader.PropertyToID("_GroundContact");
        private static readonly int IdSunDir        = Shader.PropertyToID("_SunDir");
        private static readonly int IdSunElevation  = Shader.PropertyToID("_SunElevation");
        private static readonly int IdShadowStrength = Shader.PropertyToID("_ShadowStrength");

        // ONE shared fallback material for the missing-Resources path, minted at most once across ALL
        // casters (the normal path loads Resources/SpriteShadow.mat). A per-instance Material here would
        // leak one material per caster and break the shared-material GPU batching this component relies on.
        private static Material _sharedFallbackMaterial;

        /// <summary>Resources/SpriteShadowProfile.asset — the owner's look dials (optional; defaults otherwise).</summary>
        public const string ProfileResourcePath = "SpriteShadowProfile";

        // ONE profile across every caster in the game — a shadow's look is a property of the WORLD's sun,
        // not of the thing standing in it, and 438 casters resolving their own would be 438 Resources
        // lookups for one answer.
        private static SpriteShadowProfile _sharedShadowProfile;

        /// <summary>
        /// The look profile in force for every caster: <c>Resources/SpriteShadowProfile</c> if the owner has
        /// shipped one, otherwise the built-in default — which carries the component's own historical
        /// numbers, so a project with no asset renders exactly the pre-PR frame.
        ///
        /// <para>Settable so a test can drive a dial without an asset on disk. ⚠️ It is STATIC and therefore
        /// leaks between tests: set it back to <c>null</c> in TearDown to fall back to the shipped asset.</para>
        /// </summary>
        public static SpriteShadowProfile SharedProfile
        {
            get
            {
                if (_sharedShadowProfile == null)
                    _sharedShadowProfile = Resources.Load<SpriteShadowProfile>(ProfileResourcePath);
                if (_sharedShadowProfile == null)
                    _sharedShadowProfile = SpriteShadowProfile.CreateDefault();
                return _sharedShadowProfile;
            }
            set => _sharedShadowProfile = value;
        }

        // The GROUND-CONTACT pool's quad: ONE 1x1-world-unit sprite shared by every caster, scaled into an
        // ellipse per caster by the transform. Its texels are never read — the shader draws a radial
        // falloff from the quad's uv in pool mode — so a 4 px white square is the whole texture.
        private static Sprite _sharedPoolSprite;

        private static Sprite PoolSprite()
        {
            if (_sharedPoolSprite != null) return _sharedPoolSprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                name = "SpriteShadowPool",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            // 4 px at 4 PPU = exactly one world unit, pivoted at its centre: the transform's scale then IS
            // the pool's diameter, which is what makes the ellipse one multiply instead of a mesh.
            _sharedPoolSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            _sharedPoolSprite.name = "SpriteShadowPool";
            _sharedPoolSprite.hideFlags = HideFlags.DontSave;
            return _sharedPoolSprite;
        }

        // 🔴 THE LOOK LIVES ON THE PROFILE, NOT HERE (tree shading PR 2). Darkness, colour, the length
        // curve, the cap and the edge feather used to be [SerializeField]s on this component — and
        // AcadianTreeCatalog attaches it with NO per-tree dials, so the length of a dawn rake was a
        // constant in a C# file that the owner could not reach without a code change and a re-plant.
        // They are now Resources/SpriteShadowProfile.asset, read through SharedProfile below. What stays
        // here is per-caster MACHINERY: where this caster's feet are, what it does with no clock, how
        // often it recomputes, the pixel grid it snaps to, and where it sorts.
        //
        // ⚠️ Every SpriteShadow in the project was at the code defaults when they moved (verified across
        // every scene and prefab), so nothing lost a hand-tuned value. Stale keys left in scene YAML are
        // ignored by Unity and cost a re-save nobody needs to make.

        [Header("Sorting")]
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
        private SpriteRenderer _pool;            // the pooled GROUND-CONTACT child (created once, may stay off)
        private MaterialPropertyBlock _mpb;
        private DayNightProfile _resolvedProfile;
        private float _timer;
        private Sprite _lastSprite;
        private Texture _lastTexture;    // the caster's current SHEET — rewriting the block is gated on it
        // The affine map uv.y -> height above the PIVOT, in caster heights (see PivotShearMap). Cached because
        // it is pure sprite geometry: it only moves when the caster's sprite moves to a different CELL, and it
        // is IDENTICAL across the frames of one animation row, so a walking fisher recomputes it once per
        // turn rather than once per step. Identity until a sprite arrives.
        private Vector2 _shearMap = IdentityShearMap;

        /// <summary>
        /// The map for a caster whose pivot IS its cell-bottom-centre and whose sprite fills its whole texture:
        /// <c>upFrac == uv.y</c>. This is exactly what the shader did for every caster before the pivot anchor
        /// was fixed, so such a caster renders byte-for-byte as it did — the negative control.
        /// </summary>
        public static readonly Vector2 IdentityShearMap = new Vector2(1f, 0f);

        /// <summary>
        /// <b>The pivot anchor, as a pure function.</b> Returns <c>(x, y)</c> such that
        /// <c>upFrac = uv.y * x + y</c> is the vertex's height above the caster's PIVOT measured in CASTER
        /// HEIGHTS — so <c>upFrac == 0</c> exactly at the pivot (the feet, where the caster meets the ground)
        /// and <c>1</c> one caster-height above it. That is what the shear must be proportional to: a projected
        /// shadow's length is set by height ABOVE THE GROUND, and the ground is the pivot (ADR 0026).
        ///
        /// <para>It is the inverse of the sprite's own texture mapping, which is
        /// <c>uv.y = (rectBottomPx + pivotPx + localY * ppu) / sheetHeightPx</c>. Inverting and dividing by the
        /// caster height gives the two coefficients below. Because it inverts only the texture mapping — which
        /// FullRect and Tight meshes share — it is exact for both, and the trees are Tight.</para>
        ///
        /// <para><b>Why it cannot just be <c>uv.y</c>.</b> uv is TEXTURE space. Every caster in production is a
        /// sliced sheet, so a cell on the top row of a 4-row shrub sheet has uv.y in [0.75, 1.0]: raw uv.y both
        /// misses the pivot and barely varies across the sprite. Deriving the map from the sheet's own geometry
        /// is what makes one component correct for a 1-row tree sheet, a 4-row shrub sheet and an 8-row
        /// character sheet alike.</para>
        ///
        /// <para>Degenerate input (no sheet, no height, a nonsense PPU) returns
        /// <see cref="IdentityShearMap"/> — the old behaviour — rather than a divide-by-zero that would blow
        /// the silhouette to infinity. Pure / deterministic / allocation-free.</para>
        /// </summary>
        /// <param name="sheetHeightPx">Height of the whole TEXTURE the sprite is sliced from, in pixels.</param>
        /// <param name="rectBottomPx">The sprite cell's bottom edge within that texture, in pixels.</param>
        /// <param name="pivotPx">The pivot's height above the cell's bottom edge, in pixels.</param>
        /// <param name="pixelsPerUnit">The sprite's own PPU.</param>
        /// <param name="casterLocalHeight">The caster's height in local units — the sprite's bounds height, the
        /// same quantity the shear length is scaled by.</param>
        public static Vector2 PivotShearMap(float sheetHeightPx, float rectBottomPx, float pivotPx,
                                            float pixelsPerUnit, float casterLocalHeight)
        {
            float denom = pixelsPerUnit * casterLocalHeight;
            if (!(sheetHeightPx > 0f) || !(denom > 1e-6f)) return IdentityShearMap;
            return new Vector2(sheetHeightPx / denom, -(rectBottomPx + pivotPx) / denom);
        }

        /// <summary>
        /// <see cref="PivotShearMap(float,float,float,float,float)"/> read off a live sprite. A null sprite (or
        /// one with no readable sheet) maps to <see cref="IdentityShearMap"/>.
        /// </summary>
        public static Vector2 PivotShearMap(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return IdentityShearMap;
            // sprite.rect is the CELL within the source texture and sprite.pivot is in pixels from that cell's
            // bottom-left — the pair Unity's own slicing writes. (A packed SpriteAtlas would need textureRect /
            // textureRectOffset instead; this project ships none, and adding one is an ADR-level change.)
            return PivotShearMap(sprite.texture.height, sprite.rect.y, sprite.pivot.y,
                                 sprite.pixelsPerUnit, sprite.bounds.size.y);
        }

        /// <summary>
        /// <b>The ground-contact pool's ellipse, as a pure function.</b> Returns the quad's WORLD size
        /// <c>(width, height)</c> for a caster of <paramref name="casterWorldWidth"/> at a profile
        /// <paramref name="radius"/>: the width is the pool's diameter, and the height is that diameter
        /// squashed to the GROUND PLANE.
        ///
        /// <para>The squash is <see cref="SpriteLightMath.GroundDepthScale"/> — <c>sin(40°)</c>, the same
        /// factor the lit-sprite path carries and the reason a circle lying flat on the ground reads as an
        /// ellipse under this camera. Taking it from there rather than restating 0.6428 is what stops the
        /// shade and the light disagreeing about what the ground plane is if the camera is ever re-pitched.</para>
        ///
        /// <para>Sizing from the caster's own drawn WIDTH is what lets one dial serve a 4.9 m spruce and a
        /// 0.4 m shore plant with no per-species table: a crown's footprint is about as wide as the crown.
        /// A radius of 0 returns <see cref="Vector2.zero"/> — no pool, which is the built-in default and the
        /// pre-PR frame.</para>
        /// </summary>
        public static Vector2 GroundContactSize(float casterWorldWidth, float radius)
        {
            if (!(radius > 0f) || !(casterWorldWidth > 0f)) return Vector2.zero;
            float diameter = 2f * radius * casterWorldWidth;
            return new Vector2(diameter, diameter * SpriteLightMath.GroundDepthScale);
        }

        /// <summary>
        /// <b>How many sorting orders a shadow drops when it is sorted by its FAR END, as a pure function.</b>
        ///
        /// <para>A shadow is a flat thing lying on the ground from the caster's feet to a tip
        /// <c>shadowDirY x worldLength</c> metres NORTH. Y-sort gives a sprite
        /// <see cref="SortingBands.OrdersPerMetre"/> more order per metre SOUTH, so a point that far north
        /// belongs that many orders LOWER — and this returns the (negative) difference.</para>
        ///
        /// <para><b>Why it is worth doing.</b> Sorted at the caster's own feet, a rake is drawn AFTER every
        /// sprite it crosses on its way north, so a neighbouring tree wears the caster's crown as a
        /// tree-shaped blot across its own canopy. Sorted by the tip it slides UNDER them all, and a tree
        /// standing in a shadow simply draws over it. Neither is a real shading model — the honest one is a
        /// receiver that knows it is in shade — but one puts a blot on a canopy and the other does not, and
        /// this one costs an integer.</para>
        ///
        /// <para>⚠️ It uses <see cref="SortingBands.OrdersPerMetre"/> rather than a number of its own,
        /// because a shadow that dropped by a different metre-to-order rate than the Y-sort it is competing
        /// with would slide past the wrong neighbours as the sun swung.</para>
        /// </summary>
        public static int FarEndSortingDelta(float shadowDirY, float worldLength, float ordersPerMetre)
            => -Mathf.RoundToInt(shadowDirY * worldLength * ordersPerMetre);

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
            // NOT switched on here: TickPool decides, and the profile may want no pool at all.
            
            _timer = 0f;
            Tick();   // correct on the first frame, not a stale default
            // Every sun caster is a LAMP caster too (ADR 0016, lights PR B): the lamp-shadow system draws
            // its own pooled quads from this caster's silhouette and touches nothing of this component's
            // — the sun shadow above is byte-identical with or without a lamp in range.
            LampShadowSystem.RegisterCaster(this);
        }

        private void OnDisable()
        {
            if (_shadow != null) _shadow.enabled = false;   // pooled, not destroyed — reused on re-enable
            if (_pool != null) _pool.enabled = false;
            LampShadowSystem.UnregisterCaster(this);
        }

        /// <summary>
        /// This caster as the lamp-shadow system sees it: its feet (the pivot, shifted down by the foot
        /// offset exactly as <see cref="PoseShadow"/> anchors the sun shadow), the world rect its sprite
        /// CELL fills, and the sheet + cell uv the shader samples the silhouette from. A disabled or
        /// sprite-less caster has nothing to throw.
        /// </summary>
        public bool TryGetLampShadowCaster(out LampShadowCasterState state)
        {
            state = default;
            if (_caster == null) _caster = GetComponent<SpriteRenderer>();
            if (_caster == null || !_caster.enabled) return false;
            Sprite sprite = _caster.sprite;
            if (sprite == null || sprite.texture == null) return false;

            Vector3 foot = transform.TransformPoint(new Vector3(0f, -_footOffset, 0f));
            LampShadowMath.SpriteWorldRect(
                (Vector2)transform.position, transform.lossyScale, sprite.rect, sprite.pivot,
                sprite.pixelsPerUnit, _caster.flipX, _caster.flipY, out Vector2 min, out Vector2 max);

            state.Foot = new Vector2(foot.x, foot.y);
            state.RectMin = min;
            state.RectMax = max;
            state.Sheet = sprite.texture;
            state.UvRect = LampShadowMath.SpriteUvRect(
                sprite.rect, sprite.texture.width, sprite.texture.height, _caster.flipX, _caster.flipY);
            return state.IsValid;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _refreshHz > 0f ? 1f / _refreshHz : 0.1f;
            Tick();
        }

        // The pose AND the silhouette must follow the caster every frame (it can move/animate faster than
        // the throttle); both are cheap, and the heavier light recompute stays throttled in Update.
        private void LateUpdate()
        {
            SyncSilhouette();
            PoseShadow();
        }

        /// <summary>
        /// Keep the shadow's shape on the caster's CURRENT sprite. <b>Every frame, not on the throttled
        /// tick</b>: an ANIMATED caster — the walking player, the first one in production — changes sprite
        /// several times a second, and at the 10 Hz recompute the silhouette could lag the body by up to a
        /// whole walk frame, so the shadow's legs stepped out of time with the fisher's. Static decor (every
        /// tree, shrub and shore plant) never trips the reference compare below, so this costs them one
        /// comparison per frame and nothing else.
        /// </summary>
        private void SyncSilhouette()
        {
            if (_shadow == null || _caster == null) return;

            Sprite sprite = _caster.sprite;
            if (sprite == _lastSprite) return;      // the overwhelmingly common case — one compare, no work
            _lastSprite = sprite;
            _shadow.sprite = sprite;

            // The PIVOT ANCHOR travels with the cell, so it is re-derived here and not on the throttled tick:
            // a different cell sits on a different row of the sheet, and the shadow would otherwise stay
            // anchored to the row the last light tick happened to see. It is IDENTICAL across the frames of one
            // animation row, so the compare below means a walking fisher rewrites the block when they TURN, not
            // on every step — and static decor never gets here at all.
            Vector2 map = PivotShearMap(sprite);
            bool mapMoved = map != _shearMap;
            _shearMap = map;

            // Only when the SHEET changes (a walk skin handing over to a fight skin) is the texture worth
            // rewriting — frames from one sheet all share a texture, and the block already points at it.
            Texture tex = sprite != null ? sprite.texture : null;
            bool texMoved = tex != null && tex != _lastTexture;
            if (texMoved) _lastTexture = tex;

            if (!mapMoved && !texMoved) return;
            _shadow.GetPropertyBlock(_mpb);
            if (texMoved) _mpb.SetTexture(IdMainTex, tex);
            if (mapMoved) _mpb.SetVector(IdShadowUV, _shearMap);
            _shadow.SetPropertyBlock(_mpb);
        }

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

            // The GROUND-CONTACT pool: a second pooled child on the SAME material (so the two batch
            // together) carrying the shared unit quad. Minted whether or not the profile currently asks
            // for one — creating a disabled renderer once is cheaper than deciding per tick whether to
            // create it, and the owner turning the radius up must not require a re-plant.
            var poolGo = new GameObject("SpriteShadowPool") { hideFlags = HideFlags.DontSave };
            poolGo.transform.SetParent(transform, worldPositionStays: false);
            _pool = poolGo.AddComponent<SpriteRenderer>();
            _pool.sortingLayerID = _shadow.sortingLayerID;
            _pool.sprite = PoolSprite();
            _pool.enabled = false;          // TickPool turns it on if the profile asks for a pool
            if (mat != null) _pool.sharedMaterial = mat;
        }

        /// <summary>Read the sun + shadow-strength globals (or the fallback hour) and push the projection to the shadow material.</summary>
        private void Tick()
        {
            if (_shadow == null || _caster == null) return;

            // The silhouette itself is synced in LateUpdate (see SyncSilhouette) so an animating caster
            // cannot lag it; this call still seeds it, which is what makes OnEnable's first tick correct.
            SyncSilhouette();

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
            SpriteShadowProfile look = SharedProfile;
            float alpha = DayNightMath.ShadowAlpha(look.MaxAlpha, strength);

            // Length multiplier (× height) from the elevation, clamped so dawn/dusk don't shoot to infinity.
            // ⚠️ The cap is on the MULTIPLIER, and the multiplier's own ceiling is LengthAtHorizon — so a cap
            // above that never binds. The component's historical 7 never once clamped a caster in this game;
            // the shipped asset carries 3, which does. See SpriteShadowProfile.
            float lenMul = DayNightMath.ShadowLength(elevation, look.LengthAtNoon, look.LengthAtHorizon, look.MaxLength);

            // Convert the world shear length into the shadow sprite's LOCAL-Y units (the shader shears in
            // object space, scaled by uv.y feet->head). worldHeight = caster height; localHeight = the sprite
            // quad's local height; their ratio maps world length -> local length so 1 sprite-height of shear
            // == the sprite's own height regardless of PPU/scale.
            float worldHeight = CasterWorldHeight();
            float worldLen = lenMul * worldHeight;
            float localLen = WorldToLocalShearLength(worldLen);

            Color tint = look.ShadowColor;
            var color = new Color(tint.r, tint.g, tint.b, alpha);

            _shadow.GetPropertyBlock(_mpb);
            if (_caster.sprite != null && _caster.sprite.texture != null)
                _mpb.SetTexture(IdMainTex, _caster.sprite.texture);
            _mpb.SetColor(IdShadowColor, color);
            _mpb.SetVector(IdShadowDir, new Vector4(shadowDir.x, shadowDir.y, 0f, 0f));
            _mpb.SetFloat(IdShadowLen, localLen);
            // Republished on every tick as well as on the sprite change above: this is the block write that
            // seeds a caster which never changes sprite (all of the decor), so the anchor is right from the
            // first frame without depending on a silhouette swap ever happening.
            _mpb.SetVector(IdShadowUV, _shearMap);
            _mpb.SetFloat(IdEdgeSoftness, look.EdgeSoftness);
            // 0 = draw this caster's sheared SILHOUETTE. Published explicitly rather than left to the
            // material's default, because a MaterialPropertyBlock is STICKY: the pool below sets this on
            // ITS block, and a block that once carried pool mode would keep it forever.
            _mpb.SetFloat(IdGroundContact, 0f);
            _shadow.SetPropertyBlock(_mpb);

            // Sort just UNDER the caster — and, when the profile asks, under everything the rake crosses
            // as well (see FarEndSortingDelta). The `off` branch is the exact pre-PR expression, unclamped,
            // so a project with no profile asset sorts byte-for-byte as it always did.
            _shadow.sortingLayerID = _caster.sortingLayerID;
            if (look.SortByFarEnd)
            {
                int order = _caster.sortingOrder + _sortingOffset
                          + FarEndSortingDelta(shadowDir.y, worldLen, SortingBands.OrdersPerMetre);
                _shadow.sortingOrder = Mathf.Clamp(order, SortingBands.DecorFloor, SortingBands.DecorCeiling);
            }
            else
            {
                _shadow.sortingOrder = _caster.sortingOrder + _sortingOffset;
            }
            _shadow.enabled = _caster.enabled && alpha > 0f && _shadow.sharedMaterial != null;

            TickPool(look, color, alpha);
        }

        /// <summary>
        /// The GROUND-CONTACT pool: the shade a crown throws STRAIGHT DOWN, which the sheared silhouette
        /// cannot draw. At noon the shear is short and runs north, so the trunk foot — the one place you are
        /// certainly under the tree — was left in full sun; standing at a trunk read as standing in a field.
        ///
        /// <para>It rides the SAME <c>_ShadowStrength</c> as the cast shadow (it is handed that shadow's own
        /// alpha), so it fades under cloud and vanishes at night with everything else, and it writes the same
        /// stencil, so a crown's pool and its own rake meet without doubling.</para>
        ///
        /// <para><b>Off is free and exact.</b> At <see cref="SpriteShadowProfile.GroundContactRadius"/> 0 —
        /// the built-in default — the renderer is simply disabled, which is the pre-PR frame with no pool
        /// drawn and nothing else changed.</para>
        /// </summary>
        private void TickPool(SpriteShadowProfile look, Color color, float castAlpha)
        {
            if (_pool == null) return;

            float radius = look.GroundContactRadius;
            float poolAlpha = castAlpha * look.GroundContactAlpha;
            // ⚠️ HEIGHT GATE, and it is a cost decision as much as a look one. A short caster does not need
            // a pool — its own noon shadow is LengthAtNoon x its height, so for anything under a couple of
            // metres the sheared silhouette already lands on its own footprint and a pool would be a second
            // quad drawing the same shade. Measured on St Peters: ungated, 439 pool quads cost 4.5 ms a
            // frame at 900x900; gated at 3 m only the 259 trees draw one, and the 148 shrubs and 384 shore
            // plants are left bit-identical.
            bool tallEnough = CasterWorldHeight() >= look.GroundContactMinHeight;
            bool on = radius > 0f && poolAlpha > 0f && tallEnough && _caster.enabled
                   && _caster.sprite != null && _pool.sharedMaterial != null;
            _pool.enabled = on;
            if (!on) return;

            float worldWidth = _caster.sprite.bounds.size.x * Mathf.Abs(transform.lossyScale.x);
            Vector2 size = GroundContactSize(worldWidth, radius);

            float parentX = Mathf.Abs(transform.lossyScale.x); if (parentX < 1e-5f) parentX = 1f;
            float parentY = Mathf.Abs(transform.lossyScale.y); if (parentY < 1e-5f) parentY = 1f;
            _pool.transform.localScale = new Vector3(size.x / parentX, size.y / parentY, 1f);

            _pool.GetPropertyBlock(_mpb);
            _mpb.SetColor(IdShadowColor, new Color(color.r, color.g, color.b, poolAlpha));
            // ⚠️ ONE float carries BOTH "this is a pool" and "this is its softness", and the shader's mode
            // test is `> 0`. So a hard-edged pool (softness 0) must still publish something positive or it
            // would fall back to silhouette mode and draw the caster's sprite flat on the ground.
            _mpb.SetFloat(IdGroundContact, Mathf.Max(look.GroundContactSoftness, 1e-3f));
            _mpb.SetFloat(IdShadowLen, 0f);
            _pool.SetPropertyBlock(_mpb);

            // One order UNDER the cast shadow, so with a lowered GroundContactAlpha the pool is the
            // deterministic winner of the stencil where the two overlap (at the default 1 they are the same
            // shade and the question does not arise).
            _pool.sortingLayerID = _caster.sortingLayerID;
            _pool.sortingOrder = _caster.sortingOrder + _sortingOffset - 1;
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

            // The pool sits on the SAME feet and lies just as flat. Its localScale is the ellipse and is
            // owned by TickPool, so it is deliberately not touched here.
            if (_pool != null)
            {
                _pool.transform.position = footWorld;
                _pool.transform.rotation = Quaternion.identity;
            }
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
