using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// ONE CHUNK of standing cliff — a strip of face quads carrying
    /// <c>HiddenHarbours/CliffFace</c>, generated from the coast the region actually authored and
    /// displaced by the kit's own plan-displacement profile.
    ///
    /// <para><b>Why a chunk and not a wall.</b> A cliff run is 30–100 m long and spans that much world
    /// Y, but a renderer has ONE sorting order. Sorting a whole run by one number is the ADR 0032 defect
    /// in miniature — most of its length would be layered by draw order rather than by position. So the
    /// builder cuts each run into chunks short enough that a single order is honest for all of it, and
    /// each chunk sorts by <b>its own toe</b>: the wall's foot is its ground contact exactly as a tuft's
    /// base is, so a wall obeys the same law as the grass beside it and nothing here is a fixed order
    /// parked inside the band.</para>
    ///
    /// <para><b>The scene stores samples, not a mesh.</b> Serialized state is a handful of stations
    /// along the shore (brow, toe, drop) plus the material's per-chunk parameters; the grid is built on
    /// enable with <see cref="HideFlags.DontSave"/> — the ADR 0023 overlay pattern that
    /// <see cref="TerrainSplatSurface"/> already follows. A builder re-run rewrites the floats and the
    /// mesh follows, so a Refresh CONVERGES instead of accumulating geometry.</para>
    ///
    /// <para><b>Pure look.</b> Drives no sim and saves nothing (rule 5). Walkability at a cliff belongs
    /// to the terrain's own slope, not to this component — a wall is a picture OF the height field, and
    /// the height field remains the single source of truth for where a player may stand.</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CliffWallSurface : MonoBehaviour
    {
        [Header("The run, sampled off the analytic profile (builder-pushed)")]
        [Tooltip("Clifftop lip, world XY, one per station along the shore.")]
        [SerializeField] private Vector2[] _browPlan = new Vector2[0];
        [Tooltip("Foot of the face IN PLAN, world XY — where the plunge stops falling. Not where the " +
                 "toe is DRAWN; the drop below is projected on top of this.")]
        [SerializeField] private Vector2[] _toePlan = new Vector2[0];
        [Tooltip("Brow elevation minus toe elevation, metres, per station.")]
        [SerializeField] private float[] _dropMetres = new float[0];

        [Header("Kit channels for this chunk's aspect and batter")]
        [SerializeField] private Material _material;
        [SerializeField] private Texture2D _unlit;
        [SerializeField] private Texture2D _normal;
        [SerializeField] private Texture2D _mask;
        [Tooltip("The plan-displacement map for this chunk's rock and batter. Imported CPU-readable " +
                 "(CliffCatalog.IsCpuReadable) because THIS is what reads it. Null = an undisplaced " +
                 "wall, which still draws — it just stops at the texture, the complaint kit v10 exists " +
                 "to answer.")]
        [SerializeField] private Texture2D _profile;

        [Header("Material parameters (see the shader's own property notes)")]
        [Tooltip("Compass azimuth of this chunk's OUTWARD normal — the TRUE unsnapped bearing, so " +
                 "lighting stays continuous along a curving coast even though the texture snapped.")]
        [SerializeField] private float _wallAzimuth = 180f;
        [Tooltip("Angle from horizontal. SNAPPED to a baked batter, unlike the azimuth: the kit " +
                 "respaces its bedding per batter and that respacing is in the pixels.")]
        [SerializeField] private float _batter = 90f;
        [Tooltip("The tangent-space light this aspect's textures were baked at — CliffCatalog." +
                 "AspectBakeLights. Used only to un-divide the baked cast shadow out of mask.R.")]
        [SerializeField] private Vector3 _bakeLight = new Vector3(-0.60f, -0.55f, 0.58f);

        [Header("Tiling and subdivision (kit constants, builder-pushed — rule 6)")]
        [SerializeField] private float _faceMetresS = 12f;
        [SerializeField] private float _faceMetresT = 9f;
        [SerializeField] private float _subdivideMetres = 0.25f;
        [SerializeField] private float _profileMetres = 1.15f;

        private GameObject _meshGo;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;

        private static readonly int IdUnlit = Shader.PropertyToID("_Unlit");
        private static readonly int IdNormal = Shader.PropertyToID("_Normal");
        private static readonly int IdMask = Shader.PropertyToID("_Mask");
        private static readonly int IdWallAzimuth = Shader.PropertyToID("_WallAzimuth");
        private static readonly int IdBatter = Shader.PropertyToID("_Batter");
        private static readonly int IdBakeL = Shader.PropertyToID("_BakeL");
        private static readonly int IdTileMetresS = Shader.PropertyToID("_TileMetresS");
        private static readonly int IdTileMetresT = Shader.PropertyToID("_TileMetresT");

        /// <summary>How many stations this chunk carries — read by the builder's own idempotence check
        /// and by the tests, so neither has to reach into a serialized field by name.</summary>
        public int StationCount => _browPlan != null ? _browPlan.Length : 0;

        /// <summary>The sorting order this chunk resolved to, or 0 before it has built. Exposed so an
        /// EditMode pin can assert the ladder without a camera.</summary>
        public int SortingOrder { get; private set; }

        /// <summary>The lowest DRAWN toe Y over the chunk — the point the whole chunk sorts by, and the
        /// number the ladder pin is derived from.</summary>
        public float SortY { get; private set; }

        /// <summary>
        /// Push a chunk's authored run. Every argument is data the BUILDER owns — the stations come off
        /// the region's own <c>TidalTerrain</c> profile and the kit constants come off
        /// <c>CliffCatalog</c>, so nothing about the coast or the kit is re-declared here (rule 6).
        /// </summary>
        public void Configure(Vector2[] browPlan, Vector2[] toePlan, float[] dropMetres,
                              Material material, Texture2D unlit, Texture2D normal, Texture2D mask,
                              Texture2D profile, float wallAzimuth, float batter, Vector3 bakeLight,
                              float faceMetresS, float faceMetresT, float subdivideMetres,
                              float profileMetres)
        {
            _browPlan = browPlan ?? new Vector2[0];
            _toePlan = toePlan ?? new Vector2[0];
            _dropMetres = dropMetres ?? new float[0];
            _material = material;
            _unlit = unlit;
            _normal = normal;
            _mask = mask;
            _profile = profile;
            _wallAzimuth = wallAzimuth;
            _batter = batter;
            _bakeLight = bakeLight;
            _faceMetresS = faceMetresS;
            _faceMetresT = faceMetresT;
            _subdivideMetres = subdivideMetres;
            _profileMetres = profileMetres;
            Rebuild();
        }

        private void OnEnable() => Rebuild();

        private void OnDisable() => Teardown();

        private void OnValidate()
        {
            if (isActiveAndEnabled) Rebuild();
        }

        /// <summary>Drop the generated child and mesh. Idempotent — a Rebuild always starts here, which
        /// is what makes a builder Refresh converge instead of stacking a second wall on the first.</summary>
        private void Teardown()
        {
            if (_mesh != null) { DestroyMesh(_mesh); _mesh = null; }
            if (_meshGo != null) { DestroyGo(_meshGo); _meshGo = null; }
            _renderer = null;
        }

        private void Rebuild()
        {
            Teardown();
            int stations = StationCount;
            if (stations < 2 || _toePlan.Length < stations || _dropMetres.Length < stations) return;

            if (_unlit == null || _normal == null || _mask == null)
            {
                // The kit ships as the rig and its PNGs are gitignored (PR #427), so a checkout that has
                // never been built genuinely has no faces. Warn and draw nothing rather than render an
                // untextured slab: a missing wall is a re-run away, a grey one gets shipped.
                Debug.LogWarning(
                    $"[cliff-wall] '{name}' has no baked face channels — skipping. Run the region " +
                    "builder (it bakes on missing) or 'Hidden Harbours ▸ Dev ▸ Bake Cliff Face Kit'.",
                    this);
                return;
            }
            if (_material == null)
            {
                Debug.LogWarning($"[cliff-wall] '{name}' has no CliffFace material — skipping.", this);
                return;
            }

            BuildMesh(stations);
        }

        private void BuildMesh(int stations)
        {
            // The whole strip shares ONE batter (it is snapped per chunk), so one surface length and one
            // row count serve every column — which is also what keeps the grid rectangular and its
            // triangles trivially correct.
            float maxSurface = 0f;
            for (int i = 0; i < stations; i++)
            {
                var s = new CliffWallSample(_browPlan[i], _toePlan[i], _dropMetres[i]);
                maxSurface = Mathf.Max(maxSurface, CliffWallGeometry.SurfaceLengthMetres(in s));
            }
            int rows = CliffWallGeometry.RowsFor(maxSurface, _subdivideMetres);

            var verts = new Vector3[stations * (rows + 1)];
            var uvs = new Vector2[verts.Length];
            var tris = new int[(stations - 1) * rows * 6];

            Vector3 origin = transform.position;
            float along = 0f;                       // plan arc length down the shore, metres

            for (int c = 0; c < stations; c++)
            {
                if (c > 0) along += Vector2.Distance(_browPlan[c - 1], _browPlan[c]);

                var s = new CliffWallSample(_browPlan[c], _toePlan[c], _dropMetres[c]);
                Vector2 brow = s.BrowPlan;
                Vector2 toe = CliffWallGeometry.ToeScreen(in s);
                Vector2 outward = CliffWallGeometry.OutwardPlan(in s);
                float surface = CliffWallGeometry.SurfaceLengthMetres(in s);
                float u = CliffWallGeometry.TileU(along, _faceMetresS);

                for (int r = 0; r <= rows; r++)
                {
                    float t = (float)r / rows;                       // 0 at the brow, 1 at the toe
                    Vector2 p = Vector2.Lerp(brow, toe, t);

                    // ⭐ THE FORM PASS, made geometry (rig README §5): push each vertex along the wall's
                    // outward PLAN normal by the profile's displacement in metres. Skip this and the
                    // depth stops at the texture — the silhouette stays a smooth curve and the brow line
                    // reads as a drawn edge rather than as rock. The sample is (u, t) because the
                    // profile is co-registered with the face it belongs to.
                    if (_profile != null)
                    {
                        float grey = _profile.GetPixelBilinear(u, 1f - t).r;
                        p += outward * CliffWallGeometry.ProfileMetresFromGrey(grey, _profileMetres);
                    }

                    int idx = c * (rows + 1) + r;
                    verts[idx] = new Vector3(p.x - origin.x, p.y - origin.y, 0f);
                    // v counts SURFACE metres, never height and never drawn screen height — the one
                    // rule the kit states twice and the reason _TileMetresT is not asked of the shader.
                    uvs[idx] = new Vector2(u, CliffWallGeometry.TileV(surface * t, _faceMetresT));
                }
            }

            int w = 0;
            for (int c = 0; c < stations - 1; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    int a = c * (rows + 1) + r;
                    int b = a + 1;
                    int d = (c + 1) * (rows + 1) + r;
                    int e = d + 1;
                    // Cull is Off in the shader, so winding does not decide visibility — but a
                    // consistent one keeps the normals sane for anything that ever wants them.
                    tris[w++] = a; tris[w++] = d; tris[w++] = b;
                    tris[w++] = b; tris[w++] = d; tris[w++] = e;
                }
            }

            _mesh = new Mesh { name = "HHCliffWall", hideFlags = HideFlags.HideAndDontSave };
            _mesh.SetVertices(verts);
            _mesh.SetUVs(0, uvs);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateBounds();

            _meshGo = new GameObject("CliffWallQuads") { hideFlags = HideFlags.DontSave };
            _meshGo.transform.SetParent(transform, false);
            _meshGo.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _meshGo.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.allowOcclusionWhenDynamic = false;

            // ⭐ THE LADDER. The chunk sorts by its lowest DRAWN toe through the decor band's own
            // mapping — not a constant, and not a slot of its own. ADR 0032's lesson is that a fixed
            // order inside the band is buried deterministically the moment the band is re-based, so the
            // wall derives from position like everything else that shares the band.
            var samples = new CliffWallSample[stations];
            for (int i = 0; i < stations; i++)
                samples[i] = new CliffWallSample(_browPlan[i], _toePlan[i], _dropMetres[i]);

            SortY = CliffWallGeometry.SortY(samples);
            SortingOrder = YSortSprite.OrderFor(SortY, SortingBands.DecorBase,
                                                SortingBands.OrdersPerMetre,
                                                SortingBands.DecorFloor, SortingBands.DecorCeiling);

            // Mesh renderers do not compete with sprites by sortingOrder on their own — the SortingGroup
            // ("sort as 2D") is the house workaround (ADR 0023), and RegionValidatorWindow FAILS any
            // MeshRenderer without one.
            var group = _meshGo.AddComponent<SortingGroup>();
            group.sortingOrder = SortingOrder;
            _renderer.sortingOrder = SortingOrder;

            PushMaterial();
        }

        /// <summary>
        /// Push this chunk's per-face parameters once, through a property block, so every chunk shares
        /// the one shipped <c>CliffFace.mat</c> instead of minting a material asset per aspect × batter.
        /// Set at build time only — nothing here runs per frame (rule 7).
        /// </summary>
        private void PushMaterial()
        {
            if (_renderer == null) return;
            _mpb ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_mpb);

            _mpb.SetTexture(IdUnlit, _unlit);
            _mpb.SetTexture(IdNormal, _normal);
            _mpb.SetTexture(IdMask, _mask);
            _mpb.SetFloat(IdWallAzimuth, _wallAzimuth);
            _mpb.SetFloat(IdBatter, _batter);
            _mpb.SetVector(IdBakeL, new Vector4(_bakeLight.x, _bakeLight.y, _bakeLight.z, 0f));
            // Declared by the shader and never read in frag — pushed anyway so anyone inspecting the
            // renderer sees the metre scale the UVs were actually built at, rather than the default.
            _mpb.SetFloat(IdTileMetresS, _faceMetresS);
            _mpb.SetFloat(IdTileMetresT, _faceMetresT);

            _renderer.SetPropertyBlock(_mpb);
        }

        private static void DestroyGo(GameObject go)
        {
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
    }
}
