using UnityEngine;
using UnityEngine.Tilemaps;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The SIM-DRIVEN bridge for the layered water shader (ADR 0010 / design/water-rendering.md): feeds the
    /// deterministic <see cref="EnvironmentSample"/> + <see cref="IEnvironmentService.WaterLevelAt"/> into the
    /// <c>HiddenHarbours/Water</c> material each THROTTLED tick, so what the player SEES on the surface matches
    /// what the physics DOES (the P1 integrity rule):
    /// <list type="bullet">
    /// <item><description><b>Current → flow.</b> The tidal set (<see cref="EnvironmentSample.CurrentVector"/>)
    /// becomes the surface scroll direction + speed — the water visibly runs with the tide.</description></item>
    /// <item><description><b>Wind → roughness.</b> Wind strength (<see cref="EnvironmentSample.WindVector"/>)
    /// raises the whitecap / foam roughness — a breeze ruffles, a gale whitens.</description></item>
    /// <item><description><b>Water level → shoreline.</b> <see cref="IEnvironmentService.WaterLevelAt"/> drives
    /// the shader's <c>_WaterLevel</c>; combined with the baked seabed height map the depth gradient + foam band
    /// sweep across the SAME zero-crossing the walkability sim gates on.</description></item>
    /// <item><description><b>Sea-state → chop.</b> The CONTINUOUS sea-state axis
    /// (<see cref="EnvironmentSample.SeaState01"/>) sets the swell amplitude / choppiness — glassy to storm,
    /// easing smoothly with the wind (never popping at an enum band edge).</description></item>
    /// </list>
    ///
    /// <para><b>Depth source.</b> The shader needs the seabed elevation per pixel to draw shallow→deep and place
    /// the foam band. This component BAKES a low-res height texture (<c>_HeightTex</c>) over the water plane's
    /// world bounds ONCE on enable (the geometry is static), in one of two modes (see <see cref="DepthSource"/>):
    /// <list type="number">
    /// <item><description><b>Tidal terrain</b> — when an <see cref="ITidalTerrain"/> is wired for the active
    /// region (St Peters), bake <see cref="ITidalTerrain.ElevationAt"/> so the VISIBLE depth == the PHYSICAL
    /// depth the gameplay reads (they cannot disagree — the P1 integrity rule). This stays the source when it is
    /// present.</description></item>
    /// <item><description><b>Distance to land</b> — the FALLBACK when there is NO tidal terrain (the hand-painted
    /// cove, Greywick): derive a smooth shallow→deep gradient purely from the scene geometry. Build a land mask
    /// from the best available source (painted land tilemaps, land/shore colliders, the closed shore fence), run a
    /// distance transform, and map distance-to-nearest-land → depth via a tunable curve (shallow at the shoreline,
    /// deepening offshore). This gives any scene a gradual drop-off + foam at the coast WITHOUT an authored
    /// per-region height map. It is a VISUAL-ONLY estimate (no sim/save change) — exactly why it is the fallback
    /// and tidal terrain wins when present (there the depth is gameplay-true).</description></item>
    /// </list>
    /// With neither source resolvable the shader falls back to uniform deep water (no false shoreline).</para>
    ///
    /// <para><b>Seam discipline (CLAUDE.md rule 4) &amp; determinism (rule 5).</b> Reads the sim only through the
    /// Core <see cref="GameServices.Environment"/> / <see cref="GameServices.TidalTerrain"/> accessors — never the
    /// Environment or World concrete classes — and never WRITES it. The land mask is built from generic Unity
    /// geometry (<see cref="Tilemap"/> / <see cref="Physics2D"/> / <see cref="EdgeCollider2D"/>), not another
    /// feature module's types. Visual-only: drives no simulation, saves nothing. The surface ANIMATION is
    /// <c>_Time</c> in the shader (motion only). The bake is a pure function of static scene geometry, so the look
    /// is reproducible.</para>
    ///
    /// <para><b>Performance (rule 7).</b> Uniforms are pushed through a pooled <see cref="MaterialPropertyBlock"/>
    /// on a throttled tick (default a few Hz — the tide/wind are slow), NOT per frame, with no per-frame
    /// allocation. Both the terrain bake and the distance-to-land transform run ONCE on enable. Mobile-portable.</para>
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    [DisallowMultipleComponent]
    [ExecuteAlways]   // ADR 0014: bake/feed the height map + push the (preview) water level in EDIT MODE too,
                      // so the owner SEES the coast bare/flood while designing — not only in Play.
    public sealed class WaterSurface : MonoBehaviour
    {
        /// <summary>Which seabed-elevation source feeds the shader's <c>_HeightTex</c>.</summary>
        public enum DepthSource
        {
            /// <summary>Tidal terrain when present (gameplay-true depth); else distance-to-land. The default.</summary>
            Auto = 0,
            /// <summary>Force the <see cref="ITidalTerrain.ElevationAt"/> bake (uniform deep water if absent).</summary>
            TidalTerrain = 1,
            /// <summary>Force the distance-to-nearest-land depth estimate (the no-height-map shore gradient).</summary>
            DistanceToLand = 2,
            /// <summary>
            /// Feed a HAND-PAINTED height texture (ADR 0014) STRAIGHT into the shader's <c>_HeightTex</c> —
            /// no re-bake. The SAME texture the sim's <c>PaintedTidalTerrain</c> decodes, so render == sim by
            /// construction. Wire <see cref="_paintedHeightTex"/> + the painted world rect / elevation range
            /// (the St Peters builder + the paint tool copy these from the <c>PaintedHeightMap</c> asset).
            /// </summary>
            PaintedHeightMap = 3,
        }

        /// <summary>Shape of the distance→depth ramp from the shoreline out to the drop-off distance.</summary>
        public enum DropoffCurve
        {
            /// <summary>Depth rises linearly with distance — a straight, even slope.</summary>
            Linear = 0,
            /// <summary>Ease-in-out (smoothstep) — a gentle shelf at the shore that steepens then eases to depth.</summary>
            Smooth = 1,
        }

        [Header("Sim → surface mapping (tunable; no magic numbers — rule 6)")]
        [Tooltip("Current speed (m/s) that maps to full surface scroll. The tidal set is scaled into 0..1 flow " +
                 "against this, then onto the material's base Flow.")]
        [Min(0.01f)] [SerializeField] private float _currentForFullFlow = 1.2f;
        [Tooltip("Wind speed (m/s) that maps to full surface roughness / whitecaps (saturates here).")]
        [Min(0.01f)] [SerializeField] private float _windForFullRoughness = 12f;
        [Tooltip("Base flow speed multiplier the normalized current scales — the material's own Flow is the floor; " +
                 "the live current adds on top so even slack water drifts a little.")]
        [Min(0f)] [SerializeField] private float _flowSpeedScale = 0.12f;

        [Header("Refresh")]
        [Tooltip("How often (Hz) the sim → uniform push runs. The sea is slow; a few Hz is plenty and cheap.")]
        [Min(0.5f)] [SerializeField] private float _refreshHz = 8f;

        [Header("Flow momentum (the water has MASS — rule 6)")]
        [Tooltip("Response time (seconds) for the VISUAL surface motion to ease toward the live sim flow. The " +
                 "sim's wind/current directions WANDER over time; rather than the surface SNAPPING to a new " +
                 "heading the instant the force shifts, the pushed flow/wind vectors are exponentially smoothed " +
                 "toward the live sim with this time constant — so the surface decelerates through a heading " +
                 "change and accelerates out of it (momentum). Heavier (larger) = more sluggish inertia; lighter " +
                 "(smaller) = livelier, snappier. Frame-rate independent. PRESENTATION ONLY — the boat physics " +
                 "still read the real EnvironmentSample directly; this lags only what the player SEES, and saves " +
                 "nothing (rule 5). 0 = no smoothing (instant snap, the old behaviour).")]
        [Min(0f)] [SerializeField] private float _flowResponseTime = 3f;

        [Header("Height map bake (the DEPTH source)")]
        [Tooltip("Bake a height texture so the shader's depth gradient + foam band have a seabed to read. Off → " +
                 "the shader reads uniform deep water (no false shoreline).")]
        [SerializeField] private bool _bakeHeightMap = true;
        [Tooltip("Which seabed source to bake. Auto: use ITidalTerrain.ElevationAt when a region wires it (the " +
                 "visible depth then equals the gameplay depth); otherwise estimate depth from distance-to-land so " +
                 "even a scene with NO tidal terrain (the painted cove, Greywick) shallows up at the coast.")]
        [SerializeField] private DepthSource _depthSource = DepthSource.Auto;
        [Tooltip("World-space rectangle the height map covers (centre + size). Should span the visible water.")]
        [SerializeField] private Vector2 _heightWorldCenter = new Vector2(0f, 0f);
        [SerializeField] private Vector2 _heightWorldSize = new Vector2(160f, 120f);
        [Tooltip("Baked texture resolution (px). Coarse is fine — the gradient is smooth and the shader pixelizes.")]
        [Range(16, 256)] [SerializeField] private int _heightResolution = 96;
        [Tooltip("Elevation range (m above datum) the baked R channel maps across. Must bracket the terrain AND " +
                 "the distance-to-land depths (Reference water level − Max depth at the low end, land above at top).")]
        [SerializeField] private float _heightMin = -4f;
        [SerializeField] private float _heightMax = 6f;

        [Header("Painted height map (ADR 0014; DepthSource.PaintedHeightMap)")]
        [Tooltip("A hand-painted, CPU-readable height texture (R = normalized elevation) fed STRAIGHT to the " +
                 "shader's _HeightTex — no re-bake. The SAME texture the sim's PaintedTidalTerrain decodes, so " +
                 "the visible depth and the gameplay depth come from the same bytes. The St Peters builder / " +
                 "Terrain Paint Tool copy this (and the world rect / elevation range fields above) from the " +
                 "PaintedHeightMap asset. Used when Depth Source is PaintedHeightMap (or Auto, if assigned).")]
        [SerializeField] private Texture2D _paintedHeightTex;

        [Header("Edit-mode shoreline preview (ADR 0014; design without pressing Play)")]
        [Tooltip("In the Scene view (not playing) drive the shader's water level from the slider below so the " +
                 "coast is VISIBLE — land dry, the bar baring, the channel flooded — while you design. " +
                 "Presentation only: feeds no sim and saves nothing (rule 5). At runtime the live tide " +
                 "(WaterLevelAt) overrides it.")]
        [SerializeField] private bool _previewInEditMode = true;
        [Tooltip("The water-surface level (m above datum) to show in the Scene view while not playing. Scrub " +
                 "this to watch what bares/floods at any tide WITHOUT entering Play.")]
        [Range(-6f, 6f)] [SerializeField] private float _previewTideLevel = 0.5f;

        [Header("Weather-driven palette (ADR 0017; OPT-IN — off = today's static look)")]
        [Tooltip("Master ENABLE for the weather-driven water mood (ADR 0017). When ON, the sea's MOOD/COLOUR " +
                 "(palette / foam character / swell / specular / caustics / reflection — NOT the physics props " +
                 "the sim already drives) EASES through the assigned anchor presets as the deterministic weather " +
                 "shifts: calm <-> storm by sea-state, pulled toward fog by low visibility. OFF (default) = the " +
                 "Sea reads as its authored Water.mat preset exactly (today's look). Presentation only: reads the " +
                 "sim, never writes it, blends via the per-renderer MaterialPropertyBlock (the Water.mat asset is " +
                 "NEVER mutated), saves nothing (rule 5).")]
        [SerializeField] private bool _weatherPaletteEnabled = false;
        [Tooltip("Master STRENGTH of the weather mood (0..1). 0 (or this whole mode DISABLED) = the BASE anchor " +
                 "only = exactly the live Water.mat look the renderer already shows (the feature is inert — no " +
                 "departure at any weather); 1 = the full weather-driven blend. Lerps every mood prop from the " +
                 "BASE anchor toward the weather-blended set, so the owner dials how far the sea departs from its " +
                 "base mood as the weather turns. NB the BASE anchor is the renderer's live Water.mat unless an " +
                 "explicit Base Mood Material is assigned below — so at strength 0 the sea is the live Water.mat, " +
                 "and the owner's Water.mat tuning always flows through the calm baseline.")]
        [Range(0f, 1f)] [SerializeField] private float _weatherPaletteStrength = 1f;
        [Tooltip("OPTIONAL explicit BASE / region mood preset — the calm baseline the storm/fog moods blend " +
                 "RELATIVE TO and that the blend backfills toward (at strength 0 the sea reads as this). LEAVE " +
                 "EMPTY (the St Peters default) to use the renderer's own live Water.mat as the base — then the " +
                 "owner's hand-tuning of Water.mat always drives the calm sea, and weather-off / strength-0 is " +
                 "byte-identical to Water.mat. Assign one only to PIN the calm look to a fixed preset COPY " +
                 "instead of the live material (it then stops tracking Water.mat edits). Must use the " +
                 "HiddenHarbours/Water shader (the same property key set).")]
        [SerializeField] private Material _baseMoodMaterial;
        [Tooltip("The serene/glassy CALM mood preset — strongest at the lowest sea-state (e.g. Water_GlassyCalm).")]
        [SerializeField] private Material _calmMoodMaterial;
        [Tooltip("The grey/choppy/desaturated STORM mood preset — strongest at high sea-state (e.g. Water_StormGrey).")]
        [SerializeField] private Material _stormMoodMaterial;
        [Tooltip("The pale/low-contrast/soft FOG mood preset — strongest at low visibility (e.g. Water_FoggySmother).")]
        [SerializeField] private Material _fogMoodMaterial;

        [Header("Weather-palette mapping (tunable; no magic numbers — rule 6)")]
        [Tooltip("Sea-state axis THRESHOLD (0..1 over Glass..Storm) below which the sea stays at its calm/base " +
                 "mood — the storm pull only engages above this. Raise it so only a real blow turns the sea grey.")]
        [Range(0f, 0.99f)] [SerializeField] private float _seaStateThreshold = 0.15f;
        [Tooltip("Sea-state axis CURVE exponent. 1 = linear; >1 = the storm mood bites LATE (a slow build that " +
                 "ramps near the top of the scale — most of the range stays calm-ish, only a gale/storm goes grey).")]
        [Min(0.05f)] [SerializeField] private float _seaStateCurve = 1.4f;
        [Tooltip("Fog axis THRESHOLD (0..1 over 1 - Visibility) below which no fog pull — light haze leaves the " +
                 "mood alone; only a genuine smother pulls the sea pale.")]
        [Range(0f, 0.99f)] [SerializeField] private float _fogThreshold = 0.25f;
        [Tooltip("Fog axis CURVE exponent. 1 = linear; >1 = the fog mood bites LATE (only a thick smother goes pale).")]
        [Min(0.05f)] [SerializeField] private float _fogCurve = 1.2f;
        [Tooltip("How far the LOWEST sea-state pulls toward the pure CALM preset vs sitting on the BASE preset " +
                 "(0..1). 0 = the base IS the calm look (the Calm anchor is unused); 1 = glassy water reads fully " +
                 "as the Calm preset. The base always backfills.")]
        [Range(0f, 1f)] [SerializeField] private float _calmReach = 0.8f;
        [Tooltip("Response time (seconds) for the visible MOOD to ease toward the weather target — the same " +
                 "frame-rate-independent exponential ease as the flow momentum. Larger = the palette slides more " +
                 "slowly between moods (it never POPS); 0 = snap. Presentation only; the sim is unaffected (rule 5).")]
        [Min(0f)] [SerializeField] private float _weatherPaletteResponseTime = 8f;

        [Header("Distance-to-land depth (fallback when there is no tidal terrain)")]
        [Tooltip("How far (m) the shallows reach from shore before the water hits full depth. Larger = a wider, " +
                 "gentler shelf; smaller = the bottom drops away quickly right off the beach.")]
        [Min(0.1f)] [SerializeField] private float _shoreDropoffDistance = 14f;
        [Tooltip("The deep-water depth (m) offshore, past the drop-off. The shader's deep-water tint maxes out " +
                 "around here; the foam band sits at depth≈0 right at the shore.")]
        [Min(0.1f)] [SerializeField] private float _maxDepth = 3.5f;
        [Tooltip("Shape of the shallow→deep ramp. Linear = an even slope; Smooth = a gentle shelf at the shore " +
                 "that eases into deep water (reads more like a real beach).")]
        [SerializeField] private DropoffCurve _dropoffCurve = DropoffCurve.Smooth;
        [Tooltip("The water-surface level (m above datum) the distance→depth estimate measures depth DOWN from. " +
                 "Leave at 0 (mean datum) unless the scene's water plane sits at a different level; this is the " +
                 "estimate's reference, NOT the live sim tide (which the shader applies as _WaterLevel on top).")]
        [SerializeField] private float _referenceWaterLevel = 0f;
        [Tooltip("Painted LAND tilemaps to read as land (any cell holding a tile = land). For the hand-painted " +
                 "cove, assign the terrain tilemap(s) you paint the island/beach on. Empty → every Tilemap in the " +
                 "scene is treated as land (fine when you only paint ground; assign explicitly if you also paint " +
                 "water tiles, so the sea isn't mistaken for land).")]
        [SerializeField] private Tilemap[] _landTilemaps = System.Array.Empty<Tilemap>();
        [Tooltip("Physics layers whose colliders count as solid LAND (buildings, island bodies, a filled shore). " +
                 "Sampled by overlap at each cell. Leave empty to rely on tilemaps + the shore fence.")]
        [SerializeField] private LayerMask _landColliderMask = 0;
        [Tooltip("The closed shore-fence EdgeCollider2D dividing land from water (the cove's 'ShoreEdge', " +
                 "Greywick's 'Shoreline'). If its polyline is CLOSED, the interior is filled as land. Auto-found " +
                 "by name when left empty; assign to override. An OPEN fence is skipped (use tilemaps/colliders).")]
        [SerializeField] private EdgeCollider2D _shoreFence;
        [Tooltip("Names searched (in order) to auto-find the shore fence when none is assigned.")]
        [SerializeField] private string[] _shoreFenceNames = { "ShoreEdge", "Shoreline" };

        // --- shader property ids (cached; no per-frame string lookups) ---
        private static readonly int IdWaterLevel = Shader.PropertyToID("_WaterLevel");
        private static readonly int IdFlow       = Shader.PropertyToID("_Flow");
        private static readonly int IdFlowDir    = Shader.PropertyToID("_FlowDir");
        private static readonly int IdWindDir    = Shader.PropertyToID("_WindDir");
        private static readonly int IdRoughness  = Shader.PropertyToID("_Roughness");
        private static readonly int IdChop       = Shader.PropertyToID("_Chop");
        private static readonly int IdHeightTex  = Shader.PropertyToID("_HeightTex");
        private static readonly int IdHeightMin  = Shader.PropertyToID("_HeightMin");
        private static readonly int IdHeightMax  = Shader.PropertyToID("_HeightMax");
        private static readonly int IdHWorldMin  = Shader.PropertyToID("_HeightWorldMin");
        private static readonly int IdHWorldSize = Shader.PropertyToID("_HeightWorldSize");

        // ==== Weather-palette MOOD property key set (ADR 0017) ============================================
        // The MOOD/COLOUR properties the weather blend lerps from the anchor presets and pushes via the MPB.
        // This is EXACTLY the non-sim-overridden set the §12 preset library varies (palette grade / colours /
        // foam character / swell / specular / caustics / reflection / fbm tint / surface dressing). It
        // DELIBERATELY EXCLUDES every PHYSICS prop WaterSurface already drives from the sim — _Chop,
        // _Roughness, _Flow, _FlowDir, _WindDir, _WaterLevel, _HeightTex/_Height* — so the two are disjoint
        // and compose (no double-drive). The blend reads each anchor material's value PER KEY at runtime
        // (HasProperty-guarded), so the set is read from the SHARED keys the presets actually carry and can't
        // drift from the materials. Names are append-only/stable like the shader properties.
        private static readonly string[] MoodFloatNames =
        {
            // palette grade (ADR 0015) — the guard-rail bounds, per-mood
            "_PaletteGradeStrength", "_PaletteValueFloor", "_PaletteValueCeil", "_PaletteSatCap",
            "_PalettePullStrength", "_PaletteNightFloor",
            // surface tint + dressing strengths
            "_SurfaceTint", "_SurfaceTexStrength", "_FbmStrength", "_SparkleTexStrength",
            // swell character (rolling cohesion bands — visual, NOT _Chop)
            "_OceanSwellStrength", "_OceanSwellSharpness", "_OceanSwellScale",
            // foam character (the §5.9–§5.11 lifecycle/density — NOT _Roughness)
            "_FoamDensity", "_FoamDensityWind", "_FoamThreshold", "_FoamThresholdSoft", "_FoamSolidThreshold",
            "_FoamStreakStretch", "_FoamCrestGate", "_FoamSoftness", "_FoamWidth", "_FoamNoise",
            "_FoamTexStrength", "_WhitecapTexStrength",
            "_WhitecapFormSharpness", "_WhitecapPeakDensity", "_WhitecapCollapseRate",
            // specular
            "_SpecAmount", "_SpecSharpness", "_SpecSwellBias",
            // caustics (+ the Arc C day gate, so a FoggySmother preset can kill the sun-dapple)
            "_CausticAmount", "_CausticScale", "_CausticDepth", "_CausticTexStrength", "_CausticDayGate",
            // shallows see-through (Arc C — how much the seabed hints through the water per mood)
            "_ShallowTranslucency",
            // reflection (the §11 sea-state mirror)
            "_ReflectionStrength", "_ReflectionFadeChop", "_ReflectionWindFade", "_ReflectionChopScatter",
            "_ReflectionWindScatter", "_ReflectionSkyTint", "_ReflectionSmear", "_ReflectionSunStreak",
            "_ReflectionSunSharp",
        };
        private static readonly string[] MoodColorNames =
        {
            "_DeepColor", "_ShallowColor", "_FoamColor", "_SpecColor", "_CausticColor", "_ReflectionColor",
            "_FbmTint",
            // palette grade anchor colours (ADR 0015)
            "_PaletteDeep", "_PaletteMid", "_PaletteShallow", "_PaletteFoam",
        };

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Texture2D _heightTex;
        private float _baseFlow = 0.06f;   // the material's authored Flow floor (read once)
        private float _timer;

        // --- flow-momentum state (the water's MASS): the VISUAL flow eases toward the live sim ----------
        // Persistent SMOOTHED twins of the live EnvironmentSample current/wind vectors. Each push moves these
        // toward the real sim vectors via frame-rate-independent exponential smoothing (SmoothVectorToward),
        // and ALL pushed uniforms are derived from THESE — so when the sim's wind/current heading wanders the
        // surface eases round instead of snapping. We smooth the VECTORS (not heading+speed apart) so the
        // magnitude naturally DIPS as the vector rotates through a reversal — the "slows, turns, speeds back
        // up" momentum, for free. Presentation only: the boat physics still read the real sample (rule 5).
        private Vector2 _smoothedCurrent;
        private Vector2 _smoothedWind;
        private bool _flowInitialized;     // first push snaps to the live sim (no ease-in from zero on enable)

        // ==== weather-palette state (ADR 0017) — all cached/preallocated so the per-tick blend never allocs --
        private int[] _moodFloatIds;       // cached Shader.PropertyToID for each MoodFloatNames entry
        private int[] _moodColorIds;       // cached Shader.PropertyToID for each MoodColorNames entry
        private float[] _weatherTargetWeights;   // freshly computed per tick (over {Base,Calm,Storm,Fog})
        private float[] _weatherSmoothedWeights; // persistent eased twin the blend reads
        private bool _weatherInitialized;        // first push snaps the smoothed weights (no ease-in from zero)
        private Material[] _anchorMaterials;      // {Base,Calm,Storm,Fog} resolved once, indexed by Anchor
        private bool _anchorsResolved;            // whether _anchorMaterials + the cached ids are ready

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            if (_renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty(IdFlow))
                _baseFlow = _renderer.sharedMaterial.GetFloat(IdFlow);
        }

        private void OnEnable()
        {
            BakeHeightMapIfNeeded();
            _timer = 0f;
            // First push snaps the smoothed flow to the LIVE sim (no ease-in from a stale zero on enable) — the
            // momentum kicks in only once the surface is already tracking the real current/wind.
            _flowInitialized = false;
            // (ADR 0017) Prepare the weather-palette caches (anchor materials + cached property ids + scratch
            // weight buffers); the first push snaps the smoothed mood weights to the live weather (no ease-in
            // from a stale zero). Cheap; a no-op when the mode is disabled.
            ResolveWeatherAnchorsIfNeeded();
            _weatherInitialized = false;
            // Push once immediately so the surface is correct on the first frame (not a stale material default).
            PushUniforms();
        }

        private void OnDisable()
        {
            // Clear the per-renderer overrides so the shared material reads as authored if this is removed.
            if (_renderer != null) _renderer.SetPropertyBlock(null);
            // (WS-2) Free the baked fallback height texture (the painted path never allocates _heightTex).
            DestroyBakedHeightTexture();
        }

        private void OnDestroy()
        {
            // (WS-2) Belt-and-braces: ensure the baked texture is freed even if OnDisable didn't run.
            DestroyBakedHeightTexture();
        }

        /// <summary>
        /// (WS-2) Destroy the baked distance-to-land fallback texture if one was allocated, choosing the
        /// play/edit-correct teardown. The painted St Peters path feeds <see cref="_paintedHeightTex"/> (an
        /// asset texture we never own) and never allocates <see cref="_heightTex"/>, so this is a no-op there.
        /// </summary>
        private void DestroyBakedHeightTexture()
        {
            if (_heightTex == null) return;
            if (Application.isPlaying) Destroy(_heightTex);
            else DestroyImmediate(_heightTex);
            _heightTex = null;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _refreshHz > 0f ? 1f / _refreshHz : 0.2f;
            PushUniforms();
        }

#if UNITY_EDITOR
        /// <summary>
        /// (ADR 0014) When an Inspector value changes in EDIT MODE — the painted texture, the world rect, or
        /// the preview tide slider — re-feed the height map + re-push the preview level so the Scene view
        /// updates immediately (no Play, no re-enable). Skipped in Play (the live sim drives it) and guarded
        /// for the un-awoken state. Editor-only; presentation, drives no sim (rule 5).
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (this == null || !isActiveAndEnabled) return;
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_renderer == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            // Defer to avoid SendMessage-during-OnValidate warnings on some Unity versions.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || _renderer == null) return;
                BakeHeightMapIfNeeded();
                PushUniforms();
            };
        }
#endif

        private void PushUniforms()
        {
            if (_renderer == null) return;
            var env = GameServices.Environment;
            if (env == null)
            {
                // No sim yet (EDIT MODE / pre-boot). ADR 0014: rather than leave the material at its
                // uniform-deep default (the coast invisible while designing), push the PREVIEW water level so
                // the baked/painted height map reveals the coast — land dry, the bar baring, the channel
                // flooded — and the owner can scrub _previewTideLevel to see any tide WITHOUT pressing Play.
                // Presentation only: feeds no sim, saves nothing (rule 5). In Play this branch is skipped and
                // the live tide drives _WaterLevel below.
                if (_previewInEditMode && !Application.isPlaying)
                {
                    _renderer.GetPropertyBlock(_mpb);
                    _mpb.SetFloat(IdWaterLevel, _previewTideLevel);
                    _renderer.SetPropertyBlock(_mpb);
                }
                return;
            }

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            float waterLevel = env.WaterLevelAt(now);
            EnvironmentSample s = env.Sample();

            // --- ease the VISUAL flow toward the live sim (the water's MASS) ---------------------------------
            // dt is the push cadence (the throttle interval, the spacing Update enforces between pushes). The
            // smoothing time-constant is in SECONDS, so the eased look is independent of BOTH frame rate (the
            // push is throttled, not per-frame) and the chosen refresh rate (a faster cadence just takes more,
            // smaller steps to the same place — the exponential factor composes). The FIRST push snaps to the
            // live sim (no ease-in from a stale zero); subsequent pushes ease the smoothed vectors toward the
            // real current/wind. All the uniforms below are derived from the SMOOTHED vectors, so every
            // wind/current-driven layer inherits the same momentum (cohesive). The boat physics still read the
            // real EnvironmentSample directly (rule 5).
            if (!_flowInitialized)
            {
                _smoothedCurrent = s.CurrentVector;
                _smoothedWind = s.WindVector;
                _flowInitialized = true;
            }
            else
            {
                float dt = _refreshHz > 0f ? 1f / _refreshHz : 0.2f;   // the push cadence (Update's throttle)
                _smoothedCurrent = SmoothVectorToward(_smoothedCurrent, s.CurrentVector, _flowResponseTime, dt);
                _smoothedWind    = SmoothVectorToward(_smoothedWind,    s.WindVector,    _flowResponseTime, dt);
            }

            float flow = FlowSpeed(_smoothedCurrent, _baseFlow, _flowSpeedScale, _currentForFullFlow);
            Vector2 flowDir = FlowDirection(_smoothedCurrent);
            Vector2 windDir = WindDirection(_smoothedWind);
            float roughness = Roughness(_smoothedWind, _windForFullRoughness);
            float chop = Choppiness(s.SeaState01);

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdWaterLevel, waterLevel);
            _mpb.SetFloat(IdFlow, flow);
            _mpb.SetVector(IdFlowDir, new Vector4(flowDir.x, flowDir.y, 0f, 0f));
            // Push the WIND direction too (the sim varies it over time): the shader scrolls its wind-chop
            // octave along this, so the surface follows the wind instead of only marching down the fixed
            // current axis. Direction only — wind STRENGTH still drives _Roughness below. Both come from the
            // SMOOTHED wind vector, so the wind-driven layers ease round with the same momentum as the flow.
            _mpb.SetVector(IdWindDir, new Vector4(windDir.x, windDir.y, 0f, 0f));
            _mpb.SetFloat(IdRoughness, roughness);
            _mpb.SetFloat(IdChop, chop);

            // (ADR 0017) WEATHER-DRIVEN MOOD: when enabled, blend the MOOD/COLOUR props from the anchor presets
            // by the eased weather weights and override them on the SAME MPB — composing with the physics props
            // just pushed (disjoint key sets, no double-drive). Reads the SAME deterministic EnvironmentSample
            // `s`. The dt is the push cadence (Update's throttle), so the ease is frame-rate independent. Visual
            // only: the Water.mat asset is never written (all overrides ride the MPB), saves nothing (rule 5).
            ApplyWeatherPalette(s, _mpb);

            _renderer.SetPropertyBlock(_mpb);
        }

        // ==== Weather-driven palette (ADR 0017) ===========================================================

        /// <summary>
        /// (ADR 0017) Enable the weather-driven mood and assign the four anchor preset materials in one call —
        /// the builder uses this so a "Build St Peters Scene" re-run gives weather-driven water immediately.
        /// Takes Unity-generic <see cref="Material"/> args (the preset moods), keeping Art decoupled from World
        /// (rule 4). The anchors must use the <c>HiddenHarbours/Water</c> shader (the shared mood key set); a
        /// null anchor falls back to the base (or, for the base, to the renderer's own material) so a partial
        /// wiring still renders. Re-resolves the caches immediately. Visual only; mutates no asset, saves
        /// nothing (rule 5).
        /// </summary>
        public void ConfigureWeatherPalette(bool enabled, Material baseMood, Material calmMood,
                                            Material stormMood, Material fogMood)
        {
            _weatherPaletteEnabled = enabled;
            _baseMoodMaterial = baseMood;
            _calmMoodMaterial = calmMood;
            _stormMoodMaterial = stormMood;
            _fogMoodMaterial = fogMood;
            _anchorsResolved = false;
            _weatherInitialized = false;
            ResolveWeatherAnchorsIfNeeded();
        }

        /// <summary>
        /// Resolve the weather-palette caches once: the {Base,Calm,Storm,Fog} anchor material array, the cached
        /// <see cref="Shader.PropertyToID"/> for every mood key (no per-tick string lookups — rule 7), and the
        /// preallocated scratch weight buffers (no per-tick alloc — rule 7). A no-op once resolved or when the
        /// mode is disabled. The base anchor falls back to the renderer's own shared material (the LIVE
        /// Water.mat) when no explicit base preset is wired — see <see cref="ResolveBaseAnchor"/> — so the
        /// blend's calm/base term IS the live material and the owner's Water.mat tuning always flows through the
        /// calm sea (strength 0 = exactly Water.mat).
        /// </summary>
        private void ResolveWeatherAnchorsIfNeeded()
        {
            if (_anchorsResolved) return;
            if (!_weatherPaletteEnabled) return;

            if (_moodFloatIds == null || _moodFloatIds.Length != MoodFloatNames.Length)
            {
                _moodFloatIds = new int[MoodFloatNames.Length];
                for (int i = 0; i < MoodFloatNames.Length; i++)
                    _moodFloatIds[i] = Shader.PropertyToID(MoodFloatNames[i]);
            }
            if (_moodColorIds == null || _moodColorIds.Length != MoodColorNames.Length)
            {
                _moodColorIds = new int[MoodColorNames.Length];
                for (int i = 0; i < MoodColorNames.Length; i++)
                    _moodColorIds[i] = Shader.PropertyToID(MoodColorNames[i]);
            }

            if (_anchorMaterials == null || _anchorMaterials.Length != WeatherWaterPalette.AnchorCount)
                _anchorMaterials = new Material[WeatherWaterPalette.AnchorCount];
            Material baseMat = ResolveBaseAnchor(_baseMoodMaterial,
                                                 _renderer != null ? _renderer.sharedMaterial : null);
            _anchorMaterials[(int)WeatherWaterPalette.Anchor.Base]  = baseMat;
            _anchorMaterials[(int)WeatherWaterPalette.Anchor.Calm]  = _calmMoodMaterial  != null ? _calmMoodMaterial  : baseMat;
            _anchorMaterials[(int)WeatherWaterPalette.Anchor.Storm] = _stormMoodMaterial != null ? _stormMoodMaterial : baseMat;
            _anchorMaterials[(int)WeatherWaterPalette.Anchor.Fog]   = _fogMoodMaterial   != null ? _fogMoodMaterial   : baseMat;

            if (_weatherTargetWeights == null)
                _weatherTargetWeights = new float[WeatherWaterPalette.AnchorCount];
            if (_weatherSmoothedWeights == null)
                _weatherSmoothedWeights = new float[WeatherWaterPalette.AnchorCount];

            _anchorsResolved = true;
        }

        /// <summary>
        /// (ADR 0017) Resolve the BASE/calm anchor for the weather blend: the explicit
        /// <paramref name="explicitBase"/> preset if one is assigned, otherwise the renderer's own
        /// <paramref name="sharedMaterial"/> — the LIVE <c>Water.mat</c>. Leaving the base unwired (the St
        /// Peters default) is deliberate: the calm/base term of the blend then IS the live material, so the
        /// owner's hand-tuning of <c>Water.mat</c> always flows through the calm sea and weather-off / strength-0
        /// is byte-identical to <c>Water.mat</c>. Assigning an explicit base instead PINS the calm look to a
        /// fixed preset COPY that no longer tracks <c>Water.mat</c> edits. Pure (no scene state); the same
        /// decision is unit-tested headless. Returns null only if BOTH are null (a partial wiring with no
        /// material at all — the blend then no-ops for the base term, the surface reads its own material).
        /// </summary>
        public static Material ResolveBaseAnchor(Material explicitBase, Material sharedMaterial)
        {
            return explicitBase != null ? explicitBase : sharedMaterial;
        }

        /// <summary>
        /// (ADR 0017) Blend the MOOD/COLOUR shader props from the anchor presets by the eased, weather-driven
        /// weights and OVERRIDE them on the supplied MPB — alongside (never replacing) the physics props the
        /// caller already set. Disabled / unresolved → a no-op (the surface reads its authored material). The
        /// weights come from the deterministic <paramref name="s"/> via <see cref="WeatherWaterPalette"/>; they
        /// are eased (presentation only) so the mood never pops. The master strength lerps the whole blend back
        /// toward the BASE anchor, so strength 0 = the base preset = today's static look. No per-tick alloc:
        /// the weight buffers + property ids are cached; per key we do one weighted sum across ≤4 materials.
        /// Visual only: every value goes onto the MPB (the Water.mat asset is never written), saves nothing
        /// (rule 5). Does NOT touch _Chop/_Roughness/_Flow/_FlowDir/_WindDir/_WaterLevel/_HeightTex/_Height*.
        /// </summary>
        private void ApplyWeatherPalette(in EnvironmentSample s, MaterialPropertyBlock mpb)
        {
            if (!_weatherPaletteEnabled) return;
            ResolveWeatherAnchorsIfNeeded();
            if (!_anchorsResolved || _anchorMaterials == null) return;

            // Target weights from the deterministic weather (pure helper).
            WeatherWaterPalette.BlendWeightsNonAlloc(
                _weatherTargetWeights, s.SeaState01, s.Visibility,
                _seaStateThreshold, _seaStateCurve, _fogThreshold, _fogCurve, _calmReach);

            // Ease the visible weights toward the target (the mood never POPS). First push snaps (no ease-in
            // from a stale zero on enable); subsequent pushes ease with the push cadence as dt (fps-independent).
            if (!_weatherInitialized)
            {
                for (int i = 0; i < _weatherSmoothedWeights.Length; i++)
                    _weatherSmoothedWeights[i] = _weatherTargetWeights[i];
                _weatherInitialized = true;
            }
            else
            {
                float dt = _refreshHz > 0f ? 1f / _refreshHz : 0.2f;
                WeatherWaterPalette.EaseWeights(
                    _weatherSmoothedWeights, _weatherTargetWeights, _weatherPaletteResponseTime, dt);
            }

            // Master strength: lerp the whole blend back toward the BASE anchor (strength 0 = base only =
            // today's look). Apply to a copy so the smoothed (eased) state is unscaled for the next tick.
            for (int i = 0; i < _weatherSmoothedWeights.Length; i++)
                _weatherTargetWeights[i] = _weatherSmoothedWeights[i];   // reuse the target buffer as scratch
            WeatherWaterPalette.ApplyStrengthInPlace(_weatherTargetWeights, _weatherPaletteStrength);

            BlendMoodProps(_weatherTargetWeights, mpb);
        }

        /// <summary>
        /// Override every mood FLOAT + COLOUR key on the MPB with the weighted blend of the anchor materials'
        /// values for that key. Reads each anchor's value PER KEY (HasProperty-guarded) so the blend uses the
        /// SHARED keys the presets actually carry (it can't drift from the materials); a missing key on an
        /// anchor contributes nothing (its weight is redistributed implicitly by skipping it). Allocation-free.
        /// </summary>
        private void BlendMoodProps(float[] weights, MaterialPropertyBlock mpb)
        {
            // ---- floats ----
            for (int k = 0; k < _moodFloatIds.Length; k++)
            {
                int id = _moodFloatIds[k];
                float sum = 0f, wsum = 0f;
                for (int a = 0; a < _anchorMaterials.Length; a++)
                {
                    Material m = _anchorMaterials[a];
                    if (m == null || !m.HasProperty(id)) continue;
                    float w = weights[a];
                    if (w <= 0f) continue;
                    sum += m.GetFloat(id) * w;
                    wsum += w;
                }
                if (wsum > 1e-6f) mpb.SetFloat(id, sum / wsum);
            }

            // ---- colours ----
            for (int k = 0; k < _moodColorIds.Length; k++)
            {
                int id = _moodColorIds[k];
                Color sum = new Color(0f, 0f, 0f, 0f);
                float wsum = 0f;
                for (int a = 0; a < _anchorMaterials.Length; a++)
                {
                    Material m = _anchorMaterials[a];
                    if (m == null || !m.HasProperty(id)) continue;
                    float w = weights[a];
                    if (w <= 0f) continue;
                    sum += m.GetColor(id) * w;
                    wsum += w;
                }
                if (wsum > 1e-6f) mpb.SetColor(id, sum / wsum);
            }
        }

        /// <summary>
        /// (ADR 0014) Point this surface at a hand-painted height map: set the painted texture + its world
        /// rect (centre/size) + elevation range, select the painted depth source, and (in edit mode) re-feed
        /// the shader immediately so the Scene view updates. Takes Unity-generic args (a
        /// <see cref="Texture2D"/> + scalars), NOT a World type, so Art stays decoupled from World (rule 4);
        /// the St Peters builder / Terrain Paint Tool copy these from the <c>PaintedHeightMap</c> asset.
        /// </summary>
        public void ConfigurePaintedHeightMap(Texture2D heightTex, Vector2 worldCenter, Vector2 worldSize,
                                              float minElevation, float maxElevation)
        {
            _paintedHeightTex = heightTex;
            _heightWorldCenter = worldCenter;
            _heightWorldSize = worldSize;
            _heightMin = minElevation;
            _heightMax = maxElevation;
            _depthSource = DepthSource.PaintedHeightMap;
            _bakeHeightMap = true;
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            BakeHeightMapIfNeeded();   // feed it now so the (ExecuteAlways) Scene view shows the painted coast
        }

        // ---- the height-map bake (depth source) -----------------------------------------------------------

        private void BakeHeightMapIfNeeded()
        {
            if (!_bakeHeightMap) return;

            // (ADR 0014) PAINTED path: feed the hand-painted texture STRAIGHT to the shader — no re-bake. The
            // same texture the sim's PaintedTidalTerrain decodes, so render == sim by construction. Chosen
            // explicitly (PaintedHeightMap) or auto-detected (Auto with a painted texture assigned). The
            // world rect / elevation-range fields above describe the painted map's frame (the builder + the
            // paint tool copy them from the PaintedHeightMap asset).
            bool usePainted = _paintedHeightTex != null &&
                              (_depthSource == DepthSource.PaintedHeightMap ||
                               (_depthSource == DepthSource.Auto && GameServices.TidalTerrain == null));
            if (usePainted)
            {
                FeedPaintedHeightTexture();
                return;
            }
            if (_depthSource == DepthSource.PaintedHeightMap)
                return;   // painted source chosen but no texture wired — leave the uniform-deep fallback

            // Pick the source. Auto prefers gameplay-true tidal terrain; falls back to the distance-to-land
            // estimate so a scene with no height map still shallows up at the coast.
            var terrain = GameServices.TidalTerrain;
            bool useTerrain = _depthSource switch
            {
                DepthSource.TidalTerrain   => terrain != null,
                DepthSource.DistanceToLand => false,
                _                          => terrain != null,   // Auto
            };
            bool useDistance = !useTerrain &&
                               (_depthSource == DepthSource.Auto || _depthSource == DepthSource.DistanceToLand);

            int res = Mathf.Clamp(_heightResolution, 16, 256);
            if (_heightTex == null || _heightTex.width != res)
            {
                // (WS-2) Reallocating (e.g. dragging _heightResolution in edit mode) must DESTROY the prior
                // bake texture — otherwise it leaks (a new Texture2D each resolution change, unreachable).
                DestroyBakedHeightTexture();
                _heightTex = new Texture2D(res, res, TextureFormat.R8, false, true)
                {
                    name = "WaterSurface.HeightBake",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
            }

            float[] elevation;   // metres above datum, one per cell (row-major, y outer)
            if (useTerrain)
                elevation = BakeTerrainElevation(terrain, res);
            else if (useDistance)
                elevation = BakeDistanceToLandElevation(res);
            else
                return;   // no source resolvable — leave the shader on its uniform-deep fallback

            Vector2 min = _heightWorldCenter - _heightWorldSize * 0.5f;
            WriteElevationTexture(elevation, res);

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetTexture(IdHeightTex, _heightTex);
            _mpb.SetFloat(IdHeightMin, _heightMin);
            _mpb.SetFloat(IdHeightMax, _heightMax);
            _mpb.SetVector(IdHWorldMin, new Vector4(min.x, min.y, 0f, 0f));
            _mpb.SetVector(IdHWorldSize, new Vector4(_heightWorldSize.x, _heightWorldSize.y, 0f, 0f));
            _renderer.SetPropertyBlock(_mpb);

            // Enable the shader's height-map branch so the depth read uses the bake.
            EnableHeightTexKeyword();
        }

        /// <summary>
        /// (ADR 0014) Feed the hand-painted height texture straight into the shader's <c>_HeightTex</c> +
        /// its world rect / elevation range — no re-bake. The render then samples the EXACT bytes the sim's
        /// <see cref="ITidalTerrain"/> (PaintedTidalTerrain) decoded, so the visible depth and the gameplay
        /// depth cannot diverge. The texture is wrapped Clamp so off-rect reads the edge depth (matching the
        /// CPU sampler's clamp).
        /// </summary>
        private void FeedPaintedHeightTexture()
        {
            Vector2 min = _heightWorldCenter - _heightWorldSize * 0.5f;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetTexture(IdHeightTex, _paintedHeightTex);
            _mpb.SetFloat(IdHeightMin, _heightMin);
            _mpb.SetFloat(IdHeightMax, _heightMax);
            _mpb.SetVector(IdHWorldMin, new Vector4(min.x, min.y, 0f, 0f));
            _mpb.SetVector(IdHWorldSize, new Vector4(_heightWorldSize.x, _heightWorldSize.y, 0f, 0f));
            _renderer.SetPropertyBlock(_mpb);

            EnableHeightTexKeyword();
        }

        /// <summary>
        /// (WS-1) Ensure the shader's <c>_USE_HEIGHTTEX</c> branch is on. The committed <c>Water.mat</c>
        /// PRE-ENABLES this keyword (it's a <c>multi_compile_local</c> runtime toggle, so the variant is
        /// always compiled and enabling it globally is safe — with the default black <c>_HeightTex</c> it
        /// reads as uniform-deep). We therefore must NOT write to the SHARED, COMMITTED material in EDIT
        /// MODE (that marks the asset dirty → a spurious "save Water.mat?" prompt + a stray diff). At RUNTIME
        /// we still enable it defensively (cheap, no asset to dirty); in edit mode we only enable it if a
        /// material somehow shipped without it, so the headline edit-mode coast preview still renders.
        /// </summary>
        private void EnableHeightTexKeyword()
        {
            var mat = _renderer != null ? _renderer.sharedMaterial : null;
            if (mat == null) return;
            if (Application.isPlaying)
            {
                mat.EnableKeyword("_USE_HEIGHTTEX");
                return;
            }
            // Edit mode: only touch the shared material if the keyword is genuinely missing (don't dirty the
            // committed asset on every bake/feed when it already pre-enables _USE_HEIGHTTEX).
            if (!mat.IsKeywordEnabled("_USE_HEIGHTTEX"))
                mat.EnableKeyword("_USE_HEIGHTTEX");
        }

        /// <summary>Bake the gameplay-true seabed: <see cref="ITidalTerrain.ElevationAt"/> per cell.</summary>
        private float[] BakeTerrainElevation(ITidalTerrain terrain, int res)
        {
            Vector2 min = _heightWorldCenter - _heightWorldSize * 0.5f;
            var elev = new float[res * res];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float wx = min.x + (x + 0.5f) / res * _heightWorldSize.x;
                float wy = min.y + (y + 0.5f) / res * _heightWorldSize.y;
                elev[y * res + x] = terrain.ElevationAt(new Vector2(wx, wy));
            }
            return elev;
        }

        /// <summary>
        /// Bake the FALLBACK seabed: a smooth shallow→deep gradient from the distance to the nearest land cell.
        /// Land cells are written ABOVE the reference water level (so the shader clips them — land shows through);
        /// water cells get <c>elevation = referenceWaterLevel − DistanceToDepth(distance)</c>, i.e. depth grows
        /// from ~0 at the shoreline to <see cref="_maxDepth"/> past the drop-off.
        /// </summary>
        private float[] BakeDistanceToLandElevation(int res)
        {
            bool[] land = BuildLandMask(res);
            // Per-axis metres/cell so a non-square rect measures true world distance, not cell counts.
            float cellW = _heightWorldSize.x / res;
            float cellH = _heightWorldSize.y / res;
            float[] dist = JumpFloodDistance(land, res, cellW, cellH); // world-metre distance to nearest land

            // Land that pokes above the surface: a fixed margin above the reference level so depth<=0 there and the
            // shader clips it (the painted ground / Rule-Tile land shows through, no false sea on top of the island).
            float landElevation = _referenceWaterLevel + Mathf.Max(0.5f, _maxDepth * 0.25f);

            var elev = new float[res * res];
            for (int i = 0; i < elev.Length; i++)
            {
                if (land[i] || dist[i] < 0f)
                {
                    elev[i] = landElevation;
                    continue;
                }
                float depth = DistanceToDepth(dist[i], _shoreDropoffDistance, _maxDepth, _dropoffCurve);
                elev[i] = _referenceWaterLevel - depth;
            }
            return elev;
        }

        /// <summary>
        /// Rasterize the scene's land geometry into a boolean mask over the bake rect. A cell is land if ANY
        /// source marks it: a painted land tilemap holds a tile there, a land-layer collider overlaps it, or it
        /// lies inside the closed shore fence. Source-agnostic so it works in the painted cove (tilemap) and the
        /// collider-fenced regions (Greywick) without per-scene authoring.
        /// </summary>
        private bool[] BuildLandMask(int res)
        {
            var land = new bool[res * res];
            Vector2 min = _heightWorldCenter - _heightWorldSize * 0.5f;

            // Resolve the land sources once (outside the per-cell loop).
            Tilemap[] tilemaps = ResolveLandTilemaps();
            bool hasColliderMask = _landColliderMask.value != 0;
            EdgeCollider2D fence = ResolveShoreFence();
            Vector2[] fencePoly = fence != null ? ClosedPolygonWorld(fence) : null; // null if open/degenerate

            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float wx = min.x + (x + 0.5f) / res * _heightWorldSize.x;
                float wy = min.y + (y + 0.5f) / res * _heightWorldSize.y;
                var world = new Vector2(wx, wy);
                bool isLand = false;

                // (1) painted land tilemap cells
                if (tilemaps != null)
                {
                    for (int t = 0; t < tilemaps.Length && !isLand; t++)
                    {
                        var tm = tilemaps[t];
                        if (tm == null) continue;
                        Vector3Int cell = tm.WorldToCell(new Vector3(wx, wy, 0f));
                        if (tm.HasTile(cell)) isLand = true;
                    }
                }

                // (2) solid land colliders (buildings, island bodies, a filled shore)
                if (!isLand && hasColliderMask && Physics2D.OverlapPoint(world, _landColliderMask) != null)
                    isLand = true;

                // (3) inside the closed shore fence
                if (!isLand && fencePoly != null && PointInPolygon(world, fencePoly))
                    isLand = true;

                land[y * res + x] = isLand;
            }
            return land;
        }

        private Tilemap[] ResolveLandTilemaps()
        {
            if (_landTilemaps != null && _landTilemaps.Length > 0) return _landTilemaps;
            // Auto: every Tilemap in the scene reads as land. Fine when only ground is painted; assign explicitly
            // if water tiles are also painted (else the sea would be mistaken for land).
            var found = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            return found != null && found.Length > 0 ? found : null;
        }

        private EdgeCollider2D ResolveShoreFence()
        {
            if (_shoreFence != null) return _shoreFence;
            if (_shoreFenceNames == null) return null;
            var all = FindObjectsByType<EdgeCollider2D>(FindObjectsSortMode.None);
            if (all == null) return null;
            foreach (string fenceName in _shoreFenceNames)
            {
                if (string.IsNullOrEmpty(fenceName)) continue;
                foreach (var e in all)
                    if (e != null && e.gameObject.name == fenceName) return e;
            }
            return null;
        }

        /// <summary>
        /// The shore fence's points in WORLD space IF the polyline is closed (first == last vertex); otherwise
        /// null. An open fence (Greywick's west waterline) cannot be filled by point-in-polygon, so we skip it and
        /// rely on tilemaps/colliders for that scene.
        /// </summary>
        private static Vector2[] ClosedPolygonWorld(EdgeCollider2D edge)
        {
            var pts = edge.points;
            if (pts == null || pts.Length < 4) return null;
            var t = edge.transform;
            // The collider is closed when its first and last points coincide (the builders author it that way:
            // the cove's ShoreEdge ends where it began). Drop the duplicate closing vertex.
            bool closed = (pts[0] - pts[pts.Length - 1]).sqrMagnitude < 1e-4f;
            if (!closed) return null;
            int n = pts.Length - 1;
            if (n < 3) return null;
            var world = new Vector2[n];
            for (int i = 0; i < n; i++)
                world[i] = (Vector2)t.TransformPoint(pts[i] + edge.offset);
            return world;
        }

        private void WriteElevationTexture(float[] elevation, int res)
        {
            float span = Mathf.Max(_heightMax - _heightMin, 1e-3f);
            var pixels = new Color32[res * res];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte r = (byte)Mathf.Clamp(Mathf.RoundToInt((elevation[i] - _heightMin) / span * 255f), 0, 255);
                pixels[i] = new Color32(r, r, r, 255);
            }
            _heightTex.SetPixels32(pixels);
            _heightTex.Apply(false, false);
        }

        // ==== PURE distance → depth helpers (testable; no Unity scene needed) =============================

        /// <summary>
        /// Map a world-metre distance-to-nearest-land into a water DEPTH (metres below the surface). At the
        /// shoreline (<paramref name="distance"/> ≤ 0) the depth is ~0 (shallow, foam). It rises smoothly to
        /// <paramref name="maxDepth"/> by <paramref name="dropoffDistance"/> metres offshore and saturates there.
        /// <see cref="DropoffCurve.Linear"/> is an even slope; <see cref="DropoffCurve.Smooth"/> is an
        /// ease-in-out (smoothstep) — a gentle shelf at the shore that eases into deep water. Deterministic and
        /// monotonic non-decreasing in distance.
        /// </summary>
        public static float DistanceToDepth(float distance, float dropoffDistance, float maxDepth, DropoffCurve curve)
        {
            float drop = Mathf.Max(dropoffDistance, 1e-3f);
            float t = Mathf.Clamp01(distance / drop);
            float shaped = curve == DropoffCurve.Smooth ? Smoothstep01(t) : t;
            return Mathf.Max(maxDepth, 0f) * shaped;
        }

        /// <summary>Smoothstep on an already-clamped 0..1 input: 3t² − 2t³ (ease-in-out).</summary>
        public static float Smoothstep01(float t)
        {
            return t * t * (3f - 2f * t);
        }

        /// <summary>Even-odd point-in-polygon (ray cast) over a world-space polygon. Used to fill a closed shore fence.</summary>
        public static bool PointInPolygon(Vector2 p, Vector2[] poly)
        {
            if (poly == null || poly.Length < 3) return false;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                bool straddles = (a.y > p.y) != (b.y > p.y);
                if (straddles)
                {
                    float xCross = (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x;
                    if (p.x < xCross) inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// Distance (world metres) from every WATER cell to the nearest LAND cell, via a Jump Flood Algorithm
        /// (O(n log n) over the grid — cheap for a ~96² bake, run once). Land cells return a negative sentinel
        /// (they are not water). If there is NO land at all, every cell returns <see cref="float.MaxValue"/> so
        /// the depth saturates to deep water (no false shoreline). The per-axis metre scale handles non-square
        /// rects so distances are true world metres, not cell counts.
        /// </summary>
        private static float[] JumpFloodDistance(bool[] land, int res, float cellW, float cellH)
        {
            int n = res * res;
            // seed[i] = index of the nearest land cell found so far (-1 = none yet). Land cells seed themselves.
            var seed = new int[n];
            for (int i = 0; i < n; i++) seed[i] = land[i] ? i : -1;

            // JFA passes at step sizes res/2, res/4, ... 1.
            for (int step = res / 2; step >= 1; step >>= 1)
            {
                for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    int idx = y * res + x;
                    int best = seed[idx];
                    float bestD = best >= 0 ? SqDistCells(idx, best, res, cellW, cellH) : float.MaxValue;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx * step, ny = y + dy * step;
                        if (nx < 0 || nx >= res || ny < 0 || ny >= res) continue;
                        int ncand = seed[ny * res + nx];
                        if (ncand < 0) continue;
                        float d = SqDistCells(idx, ncand, res, cellW, cellH);
                        if (d < bestD) { bestD = d; best = ncand; }
                    }
                    seed[idx] = best;
                }
            }

            var dist = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (land[i]) { dist[i] = -1f; continue; }          // land sentinel
                dist[i] = seed[i] >= 0 ? Mathf.Sqrt(SqDistCells(i, seed[i], res, cellW, cellH)) : float.MaxValue;
            }
            return dist;
        }

        /// <summary>Squared world-metre distance between two grid indices (per-axis cell size).</summary>
        private static float SqDistCells(int a, int b, int res, float cellW, float cellH)
        {
            int ax = a % res, ay = a / res;
            int bx = b % res, by = b / res;
            float dx = (ax - bx) * cellW;
            float dy = (ay - by) * cellH;
            return dx * dx + dy * dy;
        }

        // ==== PURE sim → uniform mappings (testable; no Unity scene needed) ===============================

        /// <summary>
        /// Ease a SMOOTHED flow vector one step toward the live sim vector — the water's MASS. Frame-rate
        /// independent exponential smoothing: <c>smoothed += (target − smoothed)·(1 − exp(−dt/τ))</c>, where
        /// <paramref name="responseTime"/> (τ) is the time constant in seconds. Larger τ = more sluggish
        /// (heavier inertia); τ ≤ 0 returns the target unchanged (instant snap, no momentum).
        ///
        /// <para>This is the heart of the "water has mass" feel: <see cref="WaterSurface"/> keeps the smoothed
        /// current/wind vectors and steps them here each push, deriving ALL pushed uniforms from the result. We
        /// smooth the <b>vector</b> (not heading + magnitude separately) on purpose — when the sim's flow
        /// reverses heading, the smoothed vector travels THROUGH a low-magnitude region as it rotates, so the
        /// surface speed DIPS mid-turn and recovers: "slows, turns, then speeds back up" for free.</para>
        ///
        /// <para>The exponential form is exact under sub-stepping — smoothing once over <c>dt</c> reaches the
        /// same state as N steps of <c>dt/N</c> toward a fixed target (the <c>1 − exp</c> factors compose), so
        /// the look is independent of the refresh rate. Deterministic; presentation only (drives no sim,
        /// saves nothing — rule 5).</para>
        /// </summary>
        public static Vector2 SmoothVectorToward(Vector2 smoothed, Vector2 target, float responseTime, float dt)
        {
            if (responseTime <= 0f || dt < 0f) return target;   // no inertia (snap) / guard a negative dt
            float alpha = 1f - Mathf.Exp(-dt / responseTime);   // 0 (no move) .. 1 (full move) — fps-independent
            return smoothed + (target - smoothed) * alpha;
        }

        /// <summary>
        /// Surface scroll speed from the tidal current. The material's authored <paramref name="baseFlow"/> is
        /// the floor (so slack water still drifts); the live current adds on top, its magnitude normalized
        /// against <paramref name="currentForFullFlow"/> and scaled by <paramref name="flowSpeedScale"/>.
        /// Deterministic; monotonic in current speed.
        /// </summary>
        public static float FlowSpeed(Vector2 currentVector, float baseFlow, float flowSpeedScale,
                                      float currentForFullFlow)
        {
            float norm = Mathf.Clamp01(currentVector.magnitude / Mathf.Max(currentForFullFlow, 1e-3f));
            return baseFlow + norm * Mathf.Max(flowSpeedScale, 0f);
        }

        /// <summary>
        /// Surface scroll DIRECTION from the tidal set: the normalized current vector. Slack water (near-zero
        /// current) falls back to +x so the surface never freezes to a NaN direction.
        /// </summary>
        public static Vector2 FlowDirection(Vector2 currentVector)
        {
            return currentVector.sqrMagnitude > 1e-6f ? currentVector.normalized : Vector2.right;
        }

        /// <summary>
        /// WIND DIRECTION for the shader's wind-chop octave: the normalized wind vector
        /// (<see cref="EnvironmentSample.WindVector"/> is direction × strength, so normalizing drops the
        /// strength — that still drives <c>_Roughness</c> separately). The sim VARIES wind direction over
        /// time (the prevailing wander + gust veer in WeatherModel.SampleWind), so this is what makes the
        /// surface follow the wind rather than only the fixed current axis. Slack wind (near-zero,
        /// sqrMagnitude &lt; 1e-6) falls back to <see cref="Vector2.up"/> — matching the shader's +Y default
        /// — so the surface never freezes to a NaN direction. Mirrors <see cref="FlowDirection"/>.
        /// </summary>
        public static Vector2 WindDirection(Vector2 windVector)
        {
            return windVector.sqrMagnitude > 1e-6f ? windVector.normalized : Vector2.up;
        }

        // ==== Cohesion-pass DIRECTION twins — pure mirrors of the shader's SwellDir()/FoamDriftDir() ========
        // The shader derives the rolling-swell axis and the foam-drift axis IN-SHADER from the already-pushed
        // _WindDir / _FlowDir (no NEW uniform — the cohesion follows the LIVE, time-wandering sim directions,
        // the P1 "sea has moods" integrity). These C# twins are NOT pushed to the material; they exist only so
        // the direction LOGIC (auto-from-wind swell, the wind/current foam-drift blend, the NaN-safe fallbacks)
        // is unit-tested headless without opening Unity — the determinism guard for the cohesion reorientation.

        /// <summary>
        /// The rolling-ocean-swell DIRECTION the shader's <c>SwellDir()</c> uses. Wind generates swell, so the
        /// axis defaults to the (time-wandering) wind direction; an explicit <paramref name="swellDirOverride"/>
        /// (non-zero) wins — matching the shader's <c>_OceanSwellDir (0,0) = auto-from-_WindDir</c>. Normalized;
        /// near-zero inputs fall back to <see cref="Vector2.up"/> (the shader's +Y default) so the swell bands
        /// never freeze to a NaN axis. As the sim wanders the wind, the swell bands REORIENT with it.
        /// </summary>
        public static Vector2 SwellDirection(Vector2 windVector, Vector2 swellDirOverride)
        {
            Vector2 d = swellDirOverride.sqrMagnitude > 1e-6f ? swellDirOverride : windVector;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.up;
        }

        /// <summary>
        /// The foam-DRIFT direction the shader's <c>FoamDriftDir()</c> uses — a BLEND of the (wandering) wind
        /// and the (wandering) tidal current, both sim-driven. <paramref name="windVsCurrent"/> (0..1) dials
        /// current-led (0) ↔ wind-led (1), mirroring <c>_FoamDriftWindVsCurrent</c>. Replaces the old fixed
        /// counter-diagonal so the foam flows WITH the one connected body and reorients as the weather shifts.
        /// Each axis is normalized (NaN-safe fallbacks: wind→+Y, current→+X) before the blend, and the blended
        /// result is re-normalized; a slack blend falls back to +X so the drift never freezes to NaN.
        /// </summary>
        public static Vector2 FoamDriftDirection(Vector2 windVector, Vector2 currentVector, float windVsCurrent)
        {
            Vector2 wind    = windVector.sqrMagnitude > 1e-6f ? windVector.normalized : Vector2.up;
            Vector2 current = currentVector.sqrMagnitude > 1e-6f ? currentVector.normalized : Vector2.right;
            Vector2 blend   = Vector2.Lerp(current, wind, Mathf.Clamp01(windVsCurrent));
            return blend.sqrMagnitude > 1e-6f ? blend.normalized : Vector2.right;
        }

        // ==== Shoreward-bias DIRECTION twins — pure mirrors of the shader's BiasTowardShore()/ShorewardWeight() =
        // The owner saw the sea's swell + foam read as ORIGINATING AT THE SHORELINE and streaming OUT to sea
        // ("foam blowing out of the sand") whenever the (wandering) wind blew offshore — because the rolling
        // swell + the foam drift followed ONLY the wind/current. Real swell rolls SHOREWARD regardless of the
        // local wind. The shader fixes this by deriving a per-pixel SHORE direction from the seabed height
        // GRADIENT (shallower = toward land) and biasing the swell/foam direction toward it NEAR the coast,
        // fading to the wind/current axis in deep water. The height-gradient sampling is GPU-side (reads
        // _HeightTex, no C# mirror — it can't be evaluated headless), but the DIRECTION-BLEND + the near-shore
        // WEIGHT — the part that determines whether waves roll IN — are pure functions mirrored here so the
        // logic is unit-tested headless (the determinism guard). NOT pushed to the material; the shader owns
        // the live per-pixel bias. VISUAL direction only — drives no sim, saves nothing (P1, rule 5).

        /// <summary>
        /// The near-shore WEIGHT the shader's <c>ShorewardWeight(depth)</c> uses to steer the swell/foam toward
        /// the shore: full (= <paramref name="bias"/>) at the wet edge (<paramref name="depth"/> ≤ 0), fading
        /// smoothly to 0 by <paramref name="falloffDepth"/> metres deep, so waves/foam roll IN near the coast
        /// while the OPEN sea keeps its wind-driven direction. <paramref name="bias"/> is the master strength
        /// (<c>_ShorewardBias</c>); 0 = no bias (the old wind-led behaviour). Returns a 0..1 weight, monotonic
        /// non-increasing in depth. Visual steer only — never touches the gameplay depth/waterline (rule 5).
        /// </summary>
        public static float ShorewardWeight(float depth, float bias, float falloffDepth)
        {
            float falloff = Mathf.Max(falloffDepth, 1e-3f);
            float near = 1f - Smoothstep01(Mathf.Clamp01(Mathf.Max(depth, 0f) / falloff)); // 1 at edge -> 0 deep
            return Mathf.Clamp01(bias) * near;
        }

        /// <summary>
        /// Bias a base (wind/current) direction toward the shore by a weight — the pure mirror of the shader's
        /// <c>BiasTowardShore</c>. <c>lerp(baseDir, shoreDir, w)</c> then re-normalize. When the shore direction
        /// is zero (flat seabed / open deep water — the shader's height gradient was flat) or <paramref name="w"/>
        /// is ~0, the base direction is returned UNCHANGED (the open sea keeps its wind-driven cohesion). The
        /// result is unit-length and NaN-safe (a degenerate blend falls back to the base direction). The shore
        /// direction is a VISUAL steer for the swell/foam layers only — it never moves the waterline (rule 5).
        /// </summary>
        public static Vector2 BiasTowardShore(Vector2 baseDir, Vector2 shoreDir, float w)
        {
            if (w <= 1e-4f || shoreDir.sqrMagnitude < 1e-6f) return baseDir;
            Vector2 blended = Vector2.Lerp(baseDir, shoreDir, Mathf.Clamp01(w));
            return blended.sqrMagnitude > 1e-10f ? blended.normalized : baseDir;
        }

        /// <summary>
        /// Surface roughness / whitecap amount (0..1) from wind strength: wind speed normalized against
        /// <paramref name="windForFullRoughness"/> and saturated. A breeze ruffles; a gale whitens.
        /// </summary>
        public static float Roughness(Vector2 windVector, float windForFullRoughness)
        {
            return Mathf.Clamp01(windVector.magnitude / Mathf.Max(windForFullRoughness, 1e-3f));
        }

        /// <summary>
        /// Choppiness / swell amplitude (0..1) from the CONTINUOUS sea-state axis
        /// (<see cref="EnvironmentSample.SeaState01"/>): glassy water (0) is flat; a storm (1) is fully choppy.
        /// The axis already carries the canon Glass..Storm scale linearly (it equals <c>(int)state/7</c> at
        /// every band edge), so this is just the saturate — the old <c>(int)state/7</c> quantize is gone and
        /// the swell/chop eases with the wind instead of jumping 1/7 per enum step (the "sudden shader change"
        /// the owner reported).
        /// </summary>
        public static float Choppiness(float seaState01)
        {
            return Mathf.Clamp01(seaState01);
        }

        // ==== Beach swash (ALWAYS-ON cosmetic shoreline wash) — pure mirror of the shader's BeachSwash ======
        // The shader drives the wet-edge advance/recede off _Time; this C# twin lets the math be unit-tested
        // (oscillation, amplitude bound, foam-band confinement) without opening Unity. It feeds NO sim and is
        // NOT pushed to the material — the shader owns the live wash; this is the determinism/contract guard
        // for the "swash stays in the foam band and never moves the gameplay waterline" rule (P1, rule 5).

        /// <summary>
        /// The signed swash depth offset (metres) at a shoreline position and time — the same two-beat sine the
        /// shader's <c>BeachSwash</c> uses. Positive pulls the wet edge inshore (run-up), negative pushes it back
        /// (backwash). Bounded by |result| ≤ <paramref name="amplitude"/>. Continuous and periodic in time,
        /// independent of the tide. <paramref name="alongShore"/> is the shader's <c>(worldX+worldY)*_SwashScale</c>
        /// phase term that keeps the wash from pulsing as one flat line.
        /// </summary>
        public static float SwashOffset(float time, float speed, float amplitude, float alongShore)
        {
            float w = time * speed * 2f * Mathf.PI;
            float wave = Mathf.Sin(w + alongShore) * 0.7f
                       + Mathf.Sin(w * 0.5f + alongShore * 1.7f) * 0.3f;
            return wave * amplitude;
        }

        /// <summary>
        /// The foam-band gate (1 at the wet edge → 0 in deeper water) the shader multiplies the swash by, so the
        /// wash is CONFINED to the shallow/foam band and cannot move deep water or the gameplay waterline. Mirrors
        /// the shader's <c>1 - smoothstep(0, reach, depth)</c> with <c>reach = foamWidth*2 + |amplitude|</c>.
        /// Returns 0 for any depth at/beyond the reach — the invariant the P1 integrity rule depends on.
        /// </summary>
        public static float SwashBandGate(float depth, float foamWidth, float amplitude)
        {
            float reach = Mathf.Max(foamWidth, 1e-3f) * 2f + Mathf.Max(Mathf.Abs(amplitude), 1e-3f);
            float t = Mathf.Clamp01(depth / reach);
            return 1f - Smoothstep01(t);   // 1 at depth 0, 0 at depth >= reach
        }

        // ==== Living foam: the SOFT-THRESHOLD (merge/separate) twin — pure mirror of the shader ==============
        // The shader's whitecaps + foam-fringe used to mask the foam with a HARD step() on a single ValueNoise
        // that only TRANSLATED — a fixed-shape sliding stamp, the "repeating pattern, shapes never change" the
        // owner saw. The fix turns the mask into a smoothstep around a threshold on an EVOLVING field
        // (EvolvingField, GPU-only). The EVOLUTION itself is value-noise on the GPU and isn't unit-testable
        // headless, but the SOFT-THRESHOLD math — the part that produces the metaball MERGE/SEPARATE behaviour —
        // is a pure function mirrored here so the mechanism is guarded without opening Unity. NOT pushed to the
        // material; the shader owns the live field. col.rgb/col.a foam dressing only — drives no sim (P1, rule 5).

        /// <summary>
        /// Smoothstep with explicit edges (the HLSL <c>smoothstep(edge0, edge1, x)</c>): 0 below
        /// <paramref name="edge0"/>, 1 above <paramref name="edge1"/>, an ease-in-out (3t²−2t³) between. If the
        /// edges are equal/inverted it degrades to a hard step at the edge (guarded, no divide-by-zero).
        /// </summary>
        public static float Smoothstep(float edge0, float edge1, float x)
        {
            float denom = edge1 - edge0;
            if (Mathf.Abs(denom) < 1e-6f) return x < edge0 ? 0f : 1f;
            float t = Mathf.Clamp01((x - edge0) / denom);
            return Smoothstep01(t);
        }

        /// <summary>
        /// The foam SOFT-THRESHOLD the shader applies to its evolving foam field —
        /// <c>smoothstep(threshold − softness, threshold + softness, field)</c>. This is the metaball mechanism:
        /// because it is a SOFT band (not a hard <c>step</c>), when two field maxima grow toward each other the
        /// rising valley between them crosses <c>threshold − softness</c> and the foam blobs MERGE; when the field
        /// dips below between them they SEPARATE; and a maximum rising through / falling back across the band
        /// fades the blob in / out. Monotonic non-decreasing in <paramref name="field"/>; returns a 0..1 coverage.
        /// <paramref name="softness"/> is floored so a zero softness still yields a (near-hard) finite edge.
        /// </summary>
        public static float FoamSoftThreshold(float field, float threshold, float softness)
        {
            float soft = Mathf.Max(softness, 1e-3f);
            return Smoothstep(threshold - soft, threshold + soft, field);
        }
    }
}
