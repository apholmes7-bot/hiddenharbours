using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Environment
{
    /// <summary>
    /// The runtime <b>greybox tide-reveal</b> for the St Peters opening (P1 The Sea Has Moods): a grid of
    /// flat cells over the sandbar/clam-flats that <b>visibly bare and cover</b> as the deterministic tide
    /// swings. Each cell colours itself from the SAME single number the walkability/boat-cross sim reads —
    /// the authored ground elevation (<see cref="ITidalTerrain.ElevationAt"/>) vs the live water surface
    /// (<see cref="IEnvironmentService.WaterLevelAt"/>) — so the picture can never disagree with the gate
    /// (design/time-tides-weather.md §3.5, §10 OQ1 "tide → visual cue").
    ///
    /// <para><b>Why this exists.</b> The sim was already swinging (walkability flips via the Core
    /// tidal-exposure seam), but the scene drew the sandbar as a STATIC sprite — so the headline
    /// falling-tide reveal read as frozen on screen. This drives the rendering off the live water level so
    /// the reveal is unmistakable: a cell shows <em>sand</em> when its ground is exposed
    /// (<c>elevation ≥ waterLevel</c>), and ramps through wet sand → shallow → deep <em>blue</em> the more
    /// water covers it. As the big St Peters tide (±3.5 m) falls, the bar bares from its crest outward; as
    /// it floods, the blue creeps back and seals the island off — the kindest tide-gate.</para>
    ///
    /// <para><b>Greybox, not the shader.</b> This is flat per-cell tinting only — NO shader. The layered
    /// water shader (gradient/wavy/specular/caustics/foam) is the deferred art pass owned by art-pipeline
    /// (ADR 0010); this is the placeholder that makes the mechanic legible until then.</para>
    ///
    /// <para><b>Deterministic &amp; cheap.</b> Colour is a pure function of the deterministic
    /// <c>(worldSeed, gameTime)</c> water level and authored geometry — no RNG, nothing saved (CLAUDE.md
    /// rule 5). Cells are built ONCE on enable (pooled SpriteRenderers, no per-frame allocation) and only
    /// their <see cref="SpriteRenderer.color"/> is rewritten, on a throttled tick — the
    /// <see cref="ITidalTerrain.ElevationAt"/> sample per cell is cached at build time since the geometry
    /// is static, so a tick is just N water-depth comparisons + colour writes (mobile-budget safe, rule 7).</para>
    ///
    /// <para><b>Seam discipline (CLAUDE.md rule 4).</b> Reads the tide and the height map only through the
    /// Core <see cref="GameServices.Environment"/> / <see cref="GameServices.TidalTerrain"/> accessors —
    /// never the Environment or World concrete classes — and never WRITES the tide. Both services are
    /// optional/scene-scoped: with no terrain or no environment there is no falling-tide shoreline, so the
    /// overlay simply hides itself (the safe default for a non-tide-gated region).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TidalFlatVisual : MonoBehaviour
    {
        [Header("Grid extent (world units; covers the flats/bar)")]
        [Tooltip("Centre of the covered area (world XY).")]
        [SerializeField] private Vector2 _center = Vector2.zero;
        [Tooltip("Full size of the covered area (world units): the bar + flats the tide reveals.")]
        [SerializeField] private Vector2 _size = new Vector2(60f, 24f);
        [Tooltip("Side length of one cell (world units). Smaller = a smoother waterline, more renderers.")]
        [Min(0.25f)] [SerializeField] private float _cellSize = 2f;
        [Tooltip("Sorting order for the overlay cells. Above the sea/ground sprites, below characters.")]
        [SerializeField] private int _sortingOrder = -5;

        [Header("Tide palette (greybox; no shader)")]
        [Tooltip("Dry, fully-exposed ground (well above the waterline).")]
        [SerializeField] private Color _sandDry = new Color(0.84f, 0.76f, 0.55f, 1f);
        [Tooltip("Just-bared wet sand right at the waterline (the glistening reveal edge).")]
        [SerializeField] private Color _sandWet = new Color(0.62f, 0.60f, 0.50f, 1f);
        [Tooltip("Shallow water just covering the ground.")]
        [SerializeField] private Color _shallow = new Color(0.32f, 0.52f, 0.56f, 0.92f);
        [Tooltip("Deep water (depth >= DeepWaterDepth).")]
        [SerializeField] private Color _deep = new Color(0.10f, 0.22f, 0.32f, 0.96f);

        [Header("Tuning")]
        [Tooltip("Depth (m) over a cell at which the water reads fully 'deep' (caps the shallow→deep ramp).")]
        [Min(0.1f)] [SerializeField] private float _deepWaterDepth = 2.5f;
        [Tooltip("Margin (m) of exposure over which a just-bared cell ramps dry→wet (the glistening band).")]
        [Min(0.05f)] [SerializeField] private float _wetBandMetres = 0.8f;
        [Tooltip("Refresh cadence (Hz) of the colour pass. The tide is slow; a few Hz is plenty and cheap.")]
        [Min(0.5f)] [SerializeField] private float _refreshHz = 6f;
        [Tooltip("Sprite for the cells. A 1×1 white square (any opaque sprite) — colour does the work.")]
        [SerializeField] private Sprite _cellSprite;

        private SpriteRenderer[] _cells;
        private Vector2[] _cellWorldPos;  // cell centres in world XY (static geometry)
        private float[] _cellElevation;   // cached authored ground height per cell (geometry is static)
        private bool _elevationCached;    // false until the terrain seam was available to sample
        private float _refreshTimer;
        private float _lastWaterLevel = float.NaN;

        private void OnEnable()
        {
            BuildGrid();
            _refreshTimer = 0f;
            _lastWaterLevel = float.NaN;   // force a repaint on the first tick
        }

        private void Update()
        {
            if (_cells == null || _cells.Length == 0) return;

            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0f) return;
            _refreshTimer = _refreshHz > 0f ? 1f / _refreshHz : 0.2f;

            var env = GameServices.Environment;
            // No tide service → no falling-tide shoreline to draw: hide the overlay rather than draw a
            // stale static sheet (the safe default; matches the walkability gate's "region isn't tide-gated").
            if (env == null) { SetVisible(false); return; }

            // Resolve the authored ground elevations once the terrain seam is wired. Done here (not in
            // BuildGrid) so script-execution-order can't bite: if TidalTerrain.OnEnable hasn't registered
            // yet when ours runs, we simply cache on the first tick it IS present (geometry is static, so a
            // one-time sample is correct). A repaint is forced (_lastWaterLevel reset) once cached.
            EnsureElevationCached();

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            float waterLevel = env.WaterLevelAt(now);

            // Skip the colour rewrite when the water hasn't moved enough to matter (cheap no-op tick).
            if (!float.IsNaN(_lastWaterLevel) && Mathf.Abs(waterLevel - _lastWaterLevel) < 0.001f) return;
            _lastWaterLevel = waterLevel;

            SetVisible(true);
            for (int i = 0; i < _cells.Length; i++)
                _cells[i].color = CellColor(waterLevel, _cellElevation[i],
                                            _sandDry, _sandWet, _shallow, _deep,
                                            _deepWaterDepth, _wetBandMetres);
        }

        // Sample the authored ground height per cell once the terrain seam is present. Idempotent: caches
        // exactly once. A null terrain (open water / no height map) leaves cells at the deep floor so they
        // read as water — never throws (CLAUDE.md rule 4 optional/scene-scoped seam).
        private void EnsureElevationCached()
        {
            if (_elevationCached) return;
            var terrain = GameServices.TidalTerrain;
            if (terrain == null) return;   // try again next tick (ordering-safe)

            for (int i = 0; i < _cellWorldPos.Length; i++)
                _cellElevation[i] = terrain.ElevationAt(_cellWorldPos[i]);
            _elevationCached = true;
            _lastWaterLevel = float.NaN;   // force a repaint now real elevations are in
        }

        private void SetVisible(bool on)
        {
            if (_cells == null) return;
            // Toggle renderers (not the GameObject) so the cached grid survives.
            for (int i = 0; i < _cells.Length; i++)
                if (_cells[i] != null && _cells[i].enabled != on) _cells[i].enabled = on;
        }

        private void BuildGrid()
        {
            // Idempotent: tear down any prior grid (re-enable in the editor) before rebuilding.
            if (_cells != null)
                foreach (var c in _cells)
                    if (c != null) Destroy(c.gameObject);

            int nx = Mathf.Max(1, Mathf.CeilToInt(_size.x / _cellSize));
            int ny = Mathf.Max(1, Mathf.CeilToInt(_size.y / _cellSize));
            _cells = new SpriteRenderer[nx * ny];
            _cellWorldPos = new Vector2[nx * ny];
            _cellElevation = new float[nx * ny];
            _elevationCached = false;

            Sprite sprite = _cellSprite != null ? _cellSprite : BuiltinSquare();

            Vector2 origin = _center - _size * 0.5f + new Vector2(_cellSize, _cellSize) * 0.5f;
            int k = 0;
            for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++, k++)
            {
                var go = new GameObject("TideCell");
                go.transform.SetParent(transform, false);
                Vector2 worldXY = origin + new Vector2(x * _cellSize, y * _cellSize);
                go.transform.localPosition = new Vector3(worldXY.x - transform.position.x,
                                                         worldXY.y - transform.position.y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = _sortingOrder;
                // Scale the unit sprite to one cell. Sprite is 1 world unit at its PPU; a 1×1 square works.
                FitSpriteToCell(sr, go.transform, _cellSize);

                _cellWorldPos[k] = worldXY;
                // Until the terrain seam resolves (EnsureElevationCached), treat as the deep floor so a cell
                // reads as water rather than mis-baring — corrected on the first tick terrain is present.
                _cellElevation[k] = float.MinValue;
                _cells[k] = sr;
            }
        }

        private static void FitSpriteToCell(SpriteRenderer sr, Transform t, float cellSize)
        {
            // Use tiled draw at the cell size if the sprite supports it; otherwise scale the transform.
            var b = sr.sprite != null ? sr.sprite.bounds.size : Vector3.one;
            float sx = b.x > 1e-4f ? cellSize / b.x : cellSize;
            float sy = b.y > 1e-4f ? cellSize / b.y : cellSize;
            t.localScale = new Vector3(sx, sy, 1f);
        }

        private static Sprite _builtinSquare;
        private static Sprite BuiltinSquare()
        {
            if (_builtinSquare != null) return _builtinSquare;
            var tex = Texture2D.whiteTexture;
            _builtinSquare = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                           new Vector2(0.5f, 0.5f), tex.width); // 1 world unit
            _builtinSquare.name = "TidalFlatVisual.BuiltinSquare";
            return _builtinSquare;
        }

        /// <summary>
        /// Pure, testable per-cell colour mapping for a given water level and authored ground elevation
        /// (both metres above chart datum). Exposed (ground ≥ water) ramps <paramref name="sandDry"/> →
        /// <paramref name="sandWet"/> across the just-bared margin; submerged ramps
        /// <paramref name="shallow"/> → <paramref name="deep"/> by water depth. Deterministic — no RNG, no
        /// time except via the water level the caller passes in.
        /// </summary>
        public static Color CellColor(float waterLevel, float terrainElevation,
                                      Color sandDry, Color sandWet, Color shallow, Color deep,
                                      float deepWaterDepth, float wetBandMetres)
        {
            float depth = TidalExposure.WaterDepth(waterLevel, terrainElevation); // <= 0 = exposed/dry
            if (depth <= 0f)
            {
                // Exposed: how far ABOVE the waterline (m). 0 at the edge → wet; >= band → fully dry.
                float aboveWater = -depth;
                float t = wetBandMetres > 0f ? Mathf.Clamp01(aboveWater / wetBandMetres) : 1f;
                return Color.Lerp(sandWet, sandDry, t);
            }

            // Submerged: shallow at the edge → deep as the water column grows.
            float td = deepWaterDepth > 0f ? Mathf.Clamp01(depth / deepWaterDepth) : 1f;
            return Color.Lerp(shallow, deep, td);
        }
    }
}
