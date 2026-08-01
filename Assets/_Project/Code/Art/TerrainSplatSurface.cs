using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The splat-shaded GROUND of a region (ADR 0028): one full-region quad carrying
    /// <c>HiddenHarbours/TerrainSplat</c>, fed the SAME painted height data the water shader and
    /// the walk gate read, so the picture and the gameplay can never disagree (rule 5). Replaces
    /// the per-cell ground/fringe tilemaps whose visible repetition motivated the ADR.
    ///
    /// <para>Pure look: drives no sim, saves nothing. All region-specific numbers (the band
    /// ladder, the meander, the weather sector, the sandbar) arrive through the <c>Configure*</c>
    /// calls — the builder pushes <c>StPetersShoreMap</c>'s constants so the CPU classifier stays
    /// the single source of truth; nothing here re-declares a design number (rule 6).</para>
    ///
    /// <para>The mesh child is created on enable with <see cref="HideFlags.DontSave"/> (the
    /// ADR 0023 overlay pattern): the scene stores only this component and its config, and the
    /// quad sorts through a SortingGroup — mesh renderers do not compete with sprites by
    /// sortingOrder on their own — at <see cref="DefaultSortingOrder"/>, BELOW the Sea plane's −5
    /// so the ADR 0012 tide reveal keeps working unchanged.</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class TerrainSplatSurface : MonoBehaviour
    {
        /// <summary>Below the tile band (ground −20/fringe −19/contact −18) and far below the Sea
        /// plane (−5). A test pins this under both (TerrainSplatBandPinTests).</summary>
        public const int DefaultSortingOrder = -21;

        [Header("Extent (the region rectangle, from RegionDef)")]
        [SerializeField] private Vector2 _worldCenter;
        [SerializeField] private Vector2 _worldSize = new Vector2(160f, 120f);
        [SerializeField] private Material _material;
        [SerializeField] private int _sortingOrder = DefaultSortingOrder;

        [Header("Height field (the shared painted data)")]
        [SerializeField] private Texture2D _heightTexture;
        [SerializeField] private float _heightMin = -4f;
        [SerializeField] private float _heightMax = 6f;

        [Header("Detail arrays (derived — TerrainTexArrayBuilder) + painted splat maps")]
        [SerializeField] private Texture2DArray _detailArray256;
        [SerializeField] private Texture2DArray _detailArray512;
        [SerializeField] private Texture2D _splatA;   // r grass, g marram, b sand, a shingle
        [SerializeField] private Texture2D _splatB;   // r ripple, g shelf, b silt, a dirt
        [SerializeField] private Texture2D _splatC;   // r marsh, g sedge, b foreshore, a talus
        [SerializeField] private Texture2D _splatD;   // r ledge, g rockweed, b and a free

        [Header("Bands (builder-pushed from StPetersShoreMap — not owned here)")]
        [SerializeField] private float _floorPaint = -2.6f;
        [SerializeField] private float _floorRipple = -1.7f;
        [SerializeField] private float _floorSand = -0.4f;
        [SerializeField] private float _floorMarram = 1.6f;
        [SerializeField] private float _floorGrass = 4.2f;
        [SerializeField] private float _floorShingle = -0.4f;
        [SerializeField] private float _bandWiggleMetres = 0.8f;
        [SerializeField] private float _bandWiggleScale = 16f;
        [SerializeField] private float _bandDetailMetres = 0.3f;
        [SerializeField] private float _bandDetailScale = 6f;

        [Header("Weather sector")]
        [SerializeField] private Vector2 _islandCenter;
        [SerializeField] private float _islandAspect = 1f;
        [SerializeField] private Vector2 _weatherFacing = new Vector2(1f, -1f);
        [SerializeField] private float _sectorFeather = 0.12f;

        [Header("Sandbar (half width 0 = no bar)")]
        [SerializeField] private Vector2 _barFrom;
        [SerializeField] private Vector2 _barTo;
        [SerializeField] private float _barHalfWidth;
        [SerializeField] private float _barSpineHalfWidth = 8f;
        [SerializeField] private float _barSpineFloor = 0.6f;

        [Header("Tide (edit-mode preview mirrors WaterSurface's)")]
        [SerializeField] private bool _previewInEditMode = true;
        [SerializeField, Range(-6f, 6f)] private float _previewTideLevel = 0.5f;
        [SerializeField] private float _refreshHz = 8f;

        private GameObject _meshGo;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Material _runtimeMaterial;
        private float _timer;

        private static readonly int IdHeightTex = Shader.PropertyToID("_HeightTex");
        private static readonly int IdHeightMin = Shader.PropertyToID("_HeightMin");
        private static readonly int IdHeightMax = Shader.PropertyToID("_HeightMax");
        private static readonly int IdHWorldMin = Shader.PropertyToID("_HeightWorldMin");
        private static readonly int IdHWorldSize = Shader.PropertyToID("_HeightWorldSize");
        private static readonly int IdWaterLevel = Shader.PropertyToID("_WaterLevel");
        private static readonly int IdFloorPaint = Shader.PropertyToID("_FloorPaint");
        private static readonly int IdFloorRipple = Shader.PropertyToID("_FloorRipple");
        private static readonly int IdFloorSand = Shader.PropertyToID("_FloorSand");
        private static readonly int IdFloorMarram = Shader.PropertyToID("_FloorMarram");
        private static readonly int IdFloorGrass = Shader.PropertyToID("_FloorGrass");
        private static readonly int IdFloorShingle = Shader.PropertyToID("_FloorShingle");
        private static readonly int IdWiggleMetres = Shader.PropertyToID("_BandWiggleMetres");
        private static readonly int IdWiggleScale = Shader.PropertyToID("_BandWiggleScale");
        private static readonly int IdDetailMetres = Shader.PropertyToID("_BandDetailMetres");
        private static readonly int IdDetailScale = Shader.PropertyToID("_BandDetailScale");
        private static readonly int IdIslandCenter = Shader.PropertyToID("_IslandCenter");
        private static readonly int IdIslandAspect = Shader.PropertyToID("_IslandAspect");
        private static readonly int IdWeatherFacing = Shader.PropertyToID("_WeatherFacing");
        private static readonly int IdSectorFeather = Shader.PropertyToID("_SectorFeather");
        private static readonly int IdDetailArr256 = Shader.PropertyToID("_DetailArr256");
        private static readonly int IdDetailArr512 = Shader.PropertyToID("_DetailArr512");
        private static readonly int IdDetailLoaded = Shader.PropertyToID("_DetailLoaded");
        private static readonly int IdSplatA = Shader.PropertyToID("_SplatA");
        private static readonly int IdSplatB = Shader.PropertyToID("_SplatB");
        private static readonly int IdSplatC = Shader.PropertyToID("_SplatC");
        private static readonly int IdSplatD = Shader.PropertyToID("_SplatD");
        private static readonly int IdBarFrom = Shader.PropertyToID("_BarFrom");
        private static readonly int IdBarTo = Shader.PropertyToID("_BarTo");
        private static readonly int IdBarHalfWidth = Shader.PropertyToID("_BarHalfWidth");
        private static readonly int IdBarSpineHalfWidth = Shader.PropertyToID("_BarSpineHalfWidth");
        private static readonly int IdBarSpineFloor = Shader.PropertyToID("_BarSpineFloor");

        /// <summary>The builder's one-call wiring (the WaterSurface Configure convention).</summary>
        public void Configure(Vector2 worldCenter, Vector2 worldSize, Material material, int sortingOrder)
        {
            _worldCenter = worldCenter;
            _worldSize = worldSize;
            _material = material;
            _sortingOrder = sortingOrder;
        }

        /// <summary>The shared painted height data — raw Unity types only, exactly like
        /// <c>WaterSurface.ConfigurePaintedHeightMap</c> (the builder unwraps the asset).</summary>
        public void ConfigureHeightMap(Texture2D heightTexture, float minElevation, float maxElevation)
        {
            _heightTexture = heightTexture;
            _heightMin = minElevation;
            _heightMax = maxElevation;
        }

        /// <summary>The kit's packed detail arrays (derived assets — TerrainTexArrayBuilder). Null
        /// leaves the PR-1 flat-colour fallback active (_DetailLoaded stays 0).</summary>
        public void ConfigureDetail(Texture2DArray array256, Texture2DArray array512)
        {
            _detailArray256 = array256;
            _detailArray512 = array512;
        }

        /// <summary>The painted splat maps (fourteen material channels across four RGBA textures,
        /// same world rect as the height map). Null channels fall back to a transparent 1x1 —
        /// painted nothing — NOT Unity's default "black" whose alpha of 1 would read as a painted
        /// channel (the WaterSurface ClearSeabedFallback lesson). D is the kit-v2 map: passing null
        /// for it is legal and simply means nothing is painted with ledge or rockweed.</summary>
        public void ConfigureSplat(Texture2D splatA, Texture2D splatB, Texture2D splatC,
            Texture2D splatD)
        {
            _splatA = splatA;
            _splatB = splatB;
            _splatC = splatC;
            _splatD = splatD;
        }

        /// <summary>The band ladder + meander, pushed from the CPU classifier's constants.</summary>
        public void ConfigureBands(
            float floorPaint, float floorRipple, float floorSand, float floorMarram,
            float floorGrass, float floorShingle,
            float wiggleMetres, float wiggleScale, float detailMetres, float detailScale)
        {
            _floorPaint = floorPaint;
            _floorRipple = floorRipple;
            _floorSand = floorSand;
            _floorMarram = floorMarram;
            _floorGrass = floorGrass;
            _floorShingle = floorShingle;
            _bandWiggleMetres = wiggleMetres;
            _bandWiggleScale = wiggleScale;
            _bandDetailMetres = detailMetres;
            _bandDetailScale = detailScale;
        }

        /// <summary>The weather/sheltered split (aspect = radiusX / radiusY).</summary>
        public void ConfigureWeatherSector(Vector2 islandCenter, float islandAspect,
                                           Vector2 weatherFacing, float sectorFeather)
        {
            _islandCenter = islandCenter;
            _islandAspect = islandAspect;
            _weatherFacing = weatherFacing;
            _sectorFeather = sectorFeather;
        }

        /// <summary>The bar's wiggle-exempt vocabulary (a path is signage, not scenery).</summary>
        public void ConfigureSandbar(Vector2 from, Vector2 to, float halfWidth,
                                     float spineHalfWidth, float spineFloor)
        {
            _barFrom = from;
            _barTo = to;
            _barHalfWidth = halfWidth;
            _barSpineHalfWidth = spineHalfWidth;
            _barSpineFloor = spineFloor;
        }

        private void OnEnable()
        {
            _mpb ??= new MaterialPropertyBlock();
            EnsureBuilt();
            PushAll();
        }

        private void OnDisable()
        {
            if (_meshGo != null) _meshGo.SetActive(false);
        }

        private void OnDestroy()
        {
            static void Kill(Object o)
            {
                if (o == null) return;
                if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
            }
            Kill(_meshGo);
            Kill(_mesh);
            Kill(_runtimeMaterial);
            _meshGo = null;
            _mesh = null;
            _runtimeMaterial = null;
            _renderer = null;
        }

        private void OnValidate()
        {
            // Safe subset only: no object creation in OnValidate. The mesh rebuild (extent edits)
            // happens on the next enable; the uniform push keeps the Inspector live.
            if (_renderer != null && _mpb != null) PushAll();
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;   // edit mode pushes on enable/validate; the preview level is static
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = _refreshHz > 0f ? 1f / _refreshHz : 0.2f;
            PushWaterLevel();
        }

        private void EnsureBuilt()
        {
            if (_meshGo != null)
            {
                _meshGo.SetActive(true);
                return;
            }

            Material mat = _material;
            if (mat == null)
            {
                var shader = Shader.Find("HiddenHarbours/TerrainSplat");
                if (shader == null)
                {
                    Debug.LogWarning("[TerrainSplatSurface] HiddenHarbours/TerrainSplat shader " +
                                     "missing — ground stays on whatever sits beneath (no quad built).");
                    return;
                }
                _runtimeMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                mat = _runtimeMaterial;
            }

            // World-metre offsets from this transform (parent scale compensated) — the ADR 0023
            // overlay-quad pattern verbatim, so a scaled parent cannot distort the ground.
            Vector3 origin = transform.position;
            Vector2 min = _worldCenter - _worldSize * 0.5f;
            Vector2 max = _worldCenter + _worldSize * 0.5f;

            _mesh = new Mesh { name = "HHTerrainSplatQuad", hideFlags = HideFlags.HideAndDontSave };
            _mesh.SetVertices(new[]
            {
                new Vector3(min.x - origin.x, min.y - origin.y, 0f),
                new Vector3(max.x - origin.x, min.y - origin.y, 0f),
                new Vector3(max.x - origin.x, max.y - origin.y, 0f),
                new Vector3(min.x - origin.x, max.y - origin.y, 0f),
            });
            _mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);

            _meshGo = new GameObject("TerrainSplatQuad") { hideFlags = HideFlags.DontSave };
            _meshGo.transform.SetParent(transform, false);
            _meshGo.transform.localScale = InverseScale(transform.lossyScale);
            _meshGo.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _meshGo.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = mat;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.allowOcclusionWhenDynamic = false;

            // Mesh renderers do not compete with sprites by sortingOrder on their own — the
            // SortingGroup ("sort as 2D") is the documented workaround (ADR 0023).
            var group = _meshGo.AddComponent<SortingGroup>();
            group.sortingOrder = _sortingOrder;
            _renderer.sortingOrder = _sortingOrder;
        }

        private void PushAll()
        {
            if (_renderer == null) return;

            _renderer.GetPropertyBlock(_mpb);
            if (_heightTexture != null)
            {
                Vector2 min = _worldCenter - _worldSize * 0.5f;
                _mpb.SetTexture(IdHeightTex, _heightTexture);
                _mpb.SetFloat(IdHeightMin, _heightMin);
                _mpb.SetFloat(IdHeightMax, _heightMax);
                _mpb.SetVector(IdHWorldMin, new Vector4(min.x, min.y, 0f, 0f));
                _mpb.SetVector(IdHWorldSize, new Vector4(_worldSize.x, _worldSize.y, 0f, 0f));
            }

            bool detailLoaded = _detailArray256 != null && _detailArray512 != null;
            if (detailLoaded)
            {
                _mpb.SetTexture(IdDetailArr256, _detailArray256);
                _mpb.SetTexture(IdDetailArr512, _detailArray512);
            }
            _mpb.SetFloat(IdDetailLoaded, detailLoaded ? 1f : 0f);
            _mpb.SetTexture(IdSplatA, _splatA != null ? _splatA : ClearSplat());
            _mpb.SetTexture(IdSplatB, _splatB != null ? _splatB : ClearSplat());
            _mpb.SetTexture(IdSplatC, _splatC != null ? _splatC : ClearSplat());
            _mpb.SetTexture(IdSplatD, _splatD != null ? _splatD : ClearSplat());

            _mpb.SetFloat(IdFloorPaint, _floorPaint);
            _mpb.SetFloat(IdFloorRipple, _floorRipple);
            _mpb.SetFloat(IdFloorSand, _floorSand);
            _mpb.SetFloat(IdFloorMarram, _floorMarram);
            _mpb.SetFloat(IdFloorGrass, _floorGrass);
            _mpb.SetFloat(IdFloorShingle, _floorShingle);
            _mpb.SetFloat(IdWiggleMetres, _bandWiggleMetres);
            _mpb.SetFloat(IdWiggleScale, _bandWiggleScale);
            _mpb.SetFloat(IdDetailMetres, _bandDetailMetres);
            _mpb.SetFloat(IdDetailScale, _bandDetailScale);

            _mpb.SetVector(IdIslandCenter, new Vector4(_islandCenter.x, _islandCenter.y, 0f, 0f));
            _mpb.SetFloat(IdIslandAspect, _islandAspect);
            _mpb.SetVector(IdWeatherFacing, new Vector4(_weatherFacing.x, _weatherFacing.y, 0f, 0f));
            _mpb.SetFloat(IdSectorFeather, _sectorFeather);

            _mpb.SetVector(IdBarFrom, new Vector4(_barFrom.x, _barFrom.y, 0f, 0f));
            _mpb.SetVector(IdBarTo, new Vector4(_barTo.x, _barTo.y, 0f, 0f));
            _mpb.SetFloat(IdBarHalfWidth, _barHalfWidth);
            _mpb.SetFloat(IdBarSpineHalfWidth, _barSpineHalfWidth);
            _mpb.SetFloat(IdBarSpineFloor, _barSpineFloor);

            _mpb.SetFloat(IdWaterLevel, ResolveWaterLevel());
            _renderer.SetPropertyBlock(_mpb);
        }

        private void PushWaterLevel()
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdWaterLevel, ResolveWaterLevel());
            _renderer.SetPropertyBlock(_mpb);
        }

        private float ResolveWaterLevel()
        {
            if (!Application.isPlaying)
                return _previewInEditMode ? _previewTideLevel : 0f;
            var env = GameServices.Environment;
            if (env == null) return 0f;
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            return env.WaterLevelAt(now);
        }

        private static Texture2D s_clearSplat;

        /// <summary>A shared 1x1 fully-transparent texture — "nothing painted". Unity's built-in
        /// blackTexture is (0,0,0,1): its alpha channel would read as material 4/8 painted at full
        /// weight everywhere. Never that.</summary>
        private static Texture2D ClearSplat()
        {
            if (s_clearSplat == null)
            {
                s_clearSplat = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "HHTerrainSplatClear",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                s_clearSplat.SetPixel(0, 0, Color.clear);
                s_clearSplat.Apply(false, false);
            }
            return s_clearSplat;
        }

        private static Vector3 InverseScale(Vector3 s) => new Vector3(
            Mathf.Abs(s.x) > 1e-5f ? 1f / s.x : 1f,
            Mathf.Abs(s.y) > 1e-5f ? 1f / s.y : 1f,
            Mathf.Abs(s.z) > 1e-5f ? 1f / s.z : 1f);
    }
}
