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
    /// <item><description><b>Sea-state → chop.</b> <see cref="EnvironmentSample.SeaState"/> sets the swell
    /// amplitude / choppiness — glassy to storm.</description></item>
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
    public sealed class WaterSurface : MonoBehaviour
    {
        /// <summary>Which seabed-elevation source feeds the baked <c>_HeightTex</c>.</summary>
        public enum DepthSource
        {
            /// <summary>Tidal terrain when present (gameplay-true depth); else distance-to-land. The default.</summary>
            Auto = 0,
            /// <summary>Force the <see cref="ITidalTerrain.ElevationAt"/> bake (uniform deep water if absent).</summary>
            TidalTerrain = 1,
            /// <summary>Force the distance-to-nearest-land depth estimate (the no-height-map shore gradient).</summary>
            DistanceToLand = 2,
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

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Texture2D _heightTex;
        private float _baseFlow = 0.06f;   // the material's authored Flow floor (read once)
        private float _timer;

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
            // Push once immediately so the surface is correct on the first frame (not a stale material default).
            PushUniforms();
        }

        private void OnDisable()
        {
            // Clear the per-renderer overrides so the shared material reads as authored if this is removed.
            if (_renderer != null) _renderer.SetPropertyBlock(null);
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _refreshHz > 0f ? 1f / _refreshHz : 0.2f;
            PushUniforms();
        }

        private void PushUniforms()
        {
            if (_renderer == null) return;
            var env = GameServices.Environment;
            if (env == null) return;   // no sim yet (EditMode / pre-boot) — leave the material at its defaults

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            float waterLevel = env.WaterLevelAt(now);
            EnvironmentSample s = env.Sample();

            float flow = FlowSpeed(s.CurrentVector, _baseFlow, _flowSpeedScale, _currentForFullFlow);
            Vector2 flowDir = FlowDirection(s.CurrentVector);
            Vector2 windDir = WindDirection(s.WindVector);
            float roughness = Roughness(s.WindVector, _windForFullRoughness);
            float chop = Choppiness(s.SeaState);

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdWaterLevel, waterLevel);
            _mpb.SetFloat(IdFlow, flow);
            _mpb.SetVector(IdFlowDir, new Vector4(flowDir.x, flowDir.y, 0f, 0f));
            // Push the WIND direction too (the sim varies it over time): the shader scrolls its wind-chop
            // octave along this, so the surface follows the wind instead of only marching down the fixed
            // current axis. Direction only — wind STRENGTH still drives _Roughness below.
            _mpb.SetVector(IdWindDir, new Vector4(windDir.x, windDir.y, 0f, 0f));
            _mpb.SetFloat(IdRoughness, roughness);
            _mpb.SetFloat(IdChop, chop);
            _renderer.SetPropertyBlock(_mpb);
        }

        // ---- the height-map bake (depth source) -----------------------------------------------------------

        private void BakeHeightMapIfNeeded()
        {
            if (!_bakeHeightMap) return;

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

            // Enable the shader's height-map branch on the shared material so the depth read uses the bake.
            if (_renderer.sharedMaterial != null)
                _renderer.sharedMaterial.EnableKeyword("_USE_HEIGHTTEX");
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

        /// <summary>
        /// Surface roughness / whitecap amount (0..1) from wind strength: wind speed normalized against
        /// <paramref name="windForFullRoughness"/> and saturated. A breeze ruffles; a gale whitens.
        /// </summary>
        public static float Roughness(Vector2 windVector, float windForFullRoughness)
        {
            return Mathf.Clamp01(windVector.magnitude / Mathf.Max(windForFullRoughness, 1e-3f));
        }

        /// <summary>
        /// Choppiness / swell amplitude (0..1) from the sea-state scale (Glass=0 .. Storm=7), linear across the
        /// canon range. Glassy water is flat; a storm is fully choppy.
        /// </summary>
        public static float Choppiness(SeaState seaState)
        {
            int max = (int)SeaState.Storm;   // 7
            return max > 0 ? Mathf.Clamp01((int)seaState / (float)max) : 0f;
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
    }
}
