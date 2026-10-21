using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// One horizontal BAND of a cliff face — a stretch of the wall between two depths below the brow,
    /// wearing one rock's channels.
    ///
    /// <para><b>⭐ WHY A FACE HAS BANDS AT ALL: the owner's stratified-cliff direction, and the Prince
    /// Edward Island coast it is drawn from.</b> A PEI sea cliff is not one material. The top is eroded
    /// red boulder clay — soft, soil-coloured, gullied, with the sward lipping over the edge — and only
    /// below that does the bedded red sandstone show. The kit already draws BOTH: <c>till</c> is
    /// literally "the soft cliff — red boulder clay over the rock" (vertical rill gullies, slump benches,
    /// grass tongues running down them) and <c>sandstone</c> is the horizontal red bedding. So the strata
    /// are authorable through the rig's EXISTING rock axis and no rig change is owed — the wall just has
    /// to stop assuming it is made of one thing.</para>
    ///
    /// <para><b>Depths, not fractions.</b> A soil horizon is so many metres deep whatever the cliff below
    /// it does, so a band is bounded in SURFACE metres from the brow and each station converts that to its
    /// own <c>t</c>. Two bands sharing a boundary depth therefore meet exactly at every station even
    /// though the stations are different heights.</para>
    /// </summary>
    [System.Serializable]
    public struct CliffFaceBand
    {
        [Tooltip("Albedo with non-directional AO only — this project lights its walls live.")]
        public Texture2D Unlit;
        [Tooltip("Tangent-space normal; A is cavity + macro AO.")]
        public Texture2D Normal;
        [Tooltip("R key light (with the baked cast shadow), G sky, B depth, A coverage.")]
        public Texture2D Mask;

        [Tooltip("Depth below the brow, in SURFACE metres, where this band starts.")]
        public float StartSurfaceMetres;
        [Tooltip("Depth below the brow where it ends. Less than or equal to Start means 'to the toe'.")]
        public float EndSurfaceMetres;

        [Tooltip("Names the generated child object, so a hierarchy reads as geology.")]
        public string Label;

        /// <summary>The whole face, in one rock — the shape every wall had before the strata landed, and
        /// what the single-band <c>Configure</c> overload still builds.</summary>
        public static CliffFaceBand WholeFace(Texture2D unlit, Texture2D normal, Texture2D mask,
                                              string label = "Rock") =>
            new CliffFaceBand
            {
                Unlit = unlit, Normal = normal, Mask = mask,
                StartSurfaceMetres = 0f, EndSurfaceMetres = 0f, Label = label,
            };

        public bool HasChannels => Unlit != null && Normal != null && Mask != null;
    }

    /// <summary>
    /// The whole station past one end of a CUT wall (terrain PR 5w, the 1 m re-cut): where a chunk's first or last station
    /// is cut from between two whole stations, this is the whole one beyond it, so the cut station can be drawn as a slice
    /// of the face the two of them draw. Builder-pushed, like the stations; it draws nothing of its own.
    /// </summary>
    [System.Serializable]
    public struct CliffWallSliceEnd
    {
        public Vector2 BrowPlan;
        public Vector2 ToePlan;
        public float DropMetres;
        public float ToeElevation;
        [Tooltip("Where its texture runs along: the brow, unless a top moved out kept its texture where it was.")]
        public Vector2 TextureBrowPlan;

        public CliffWallSliceEnd(Vector2 browPlan, Vector2 toePlan, float dropMetres, float toeElevation,
                                 Vector2 textureBrowPlan)
        {
            BrowPlan = browPlan;
            ToePlan = toePlan;
            DropMetres = dropMetres;
            ToeElevation = toeElevation;
            TextureBrowPlan = textureBrowPlan;
        }

        public CliffWallSample Sample => new CliffWallSample(BrowPlan, ToePlan, DropMetres, ToeElevation);
    }

    /// <summary>
    /// The columns a wall's mesh is laid on: the WHOLE stations it draws from (its own whole stations, and the slice end
    /// past an end that is cut), the brows its texture runs along at each, and where each of the wall's own stations sits
    /// among them: on its own column, or at a fraction of the way from one column to the next.
    /// </summary>
    public sealed class CliffWallColumns
    {
        public CliffWallSample[] Whole;
        public Vector2[] AlongBrow;
        /// <summary>Per station: the whole column at or before it.</summary>
        public int[] Gap;
        /// <summary>Per station: 0 on its column, else the fraction of the way on to the next.</summary>
        public float[] At;
        /// <summary>False: one column per station, laid and triangulated exactly as an uncut wall always was.</summary>
        public bool Sliced;
    }

    /// <summary>One generated mesh's arrays, before they are a <see cref="Mesh"/>, so a test can read the exact floats.</summary>
    public sealed class CliffWallMeshData
    {
        public Vector3[] Verts;
        public Vector2[] Uvs;
        public Vector2[] ElevationUv;
        public Vector2[] SeaPlanUv;
        public int[] Tris;
    }

    /// <summary>
    /// ONE CHUNK of standing cliff — a strip of face quads carrying
    /// <c>HiddenHarbours/CliffFace</c>, generated from the coast the region actually authored, displaced
    /// by the kit's own plan-displacement profile, stratified into rock bands, and finished at top and
    /// bottom by the kit's brow and toe decals.
    ///
    /// <para><b>Why a chunk and not a wall.</b> A cliff run is 30–150 m long and spans that much world Y,
    /// but a renderer has ONE sorting order. Sorting a whole run by one number is the ADR 0032 defect in
    /// miniature — most of its length would be layered by draw order rather than by position. So the
    /// builder cuts each run into chunks short enough that a single order is honest for all of it, and
    /// each chunk sorts by <b>its own toe</b>: the wall's foot is its ground contact exactly as a tuft's
    /// base is, so a wall obeys the same law as the grass beside it and nothing here is a fixed order
    /// parked inside the band.</para>
    ///
    /// <para><b>⭐⭐ A CHUNK IS A SLICE OF A RUN, AND WHERE THE SLICE FALLS MAY NOT CHANGE THE PICTURE.</b>
    /// That is the law this component was violating, and it is the whole of the owner's "cliff gaps"
    /// (B3). The cut is a SORTING decision — it has no business being visible. Two things leaked it into
    /// the render and both are now RUN quantities the builder carries across every cut:</para>
    /// <list type="number">
    /// <item><description><see cref="AlongOffsetMetres"/> — the arc length already spent before this
    /// chunk. Without it <c>u</c> restarted at zero per chunk, so the rock texture jumped a measured mean
    /// of 3.30 m of its 12 m period at 76 of the coast's 78 boundaries — and, far worse, the profile is
    /// sampled at that same <c>u</c>, so the boundary station that exists to prevent a hole was pushed to
    /// two DIFFERENT places (RMS 0.40 m, up to 1.10 m, measured against the rig). The overlap tore open
    /// instead of closing.</description></item>
    /// <item><description><see cref="RowsBasisSurfaceMetres"/> — the run's longest face, which sets the
    /// row count for every chunk in it. The displaced face is a CURVE; two chunks that subdivide their
    /// shared station differently approximate it with different polylines and the seam does not close.
    /// 26 of 76 cuts did exactly that.</description></item>
    /// </list>
    ///
    /// <para><b>The scene stores samples, not a mesh.</b> Serialized state is a handful of stations along
    /// the shore (brow, toe, drop) plus the material's per-chunk parameters; the grid is built on enable
    /// with <see cref="HideFlags.DontSave"/> — the ADR 0023 overlay pattern that
    /// <see cref="TerrainSplatSurface"/> already follows. A builder re-run rewrites the floats and the
    /// mesh follows, so a Refresh CONVERGES instead of accumulating geometry.</para>
    ///
    /// <para><b>⭐ THE SEA RISES AGAINST IT (owner ask 2026-08-06).</b> A cliff that plunges into deep
    /// water used to be drawn dry rock all the way down, with the water plane passing behind it —
    /// deep-shore cliffs are tide-worked BY DESIGN, and that was the ask that opened this whole arc. Every
    /// band and both decals now carry two extra channels for it: <c>uv1</c> = (the vertex's ELEVATION in
    /// metres, 1 if that elevation is real) and <c>uv2</c> = the station's TOE PLAN, which is where the
    /// sea actually is — the DRAWN toe has already been pushed down-screen by the drop and would read the
    /// swell metres from the wall it belongs to. <c>HiddenHarboursCliffFace.shader</c> does the rest from
    /// the ONE waterline <c>WaterSurface</c> publishes; <see cref="CliffWaterlineMath"/> is the testable
    /// twin. A chunk with no authored toe elevations (a scene saved before this existed) flags the channel
    /// invalid and the shader skips the band entirely, so it draws exactly the wall that shipped until the
    /// region builder is re-run.</para>
    ///
    /// <para><b>⭐ A RE-CUT WALL DRAWS AS SLICES OF THE FACE IT HAD (terrain PR 5w).</b> Cutting a run finer (St
    /// Peters' 1 m cut) sets stations between whole ones, and a chunk may start or end on one. Such a station is a
    /// SLICE, not a new face: the mesh is laid on the WHOLE stations exactly as before (the whole station past a cut
    /// end rides along as a <see cref="CliffWallSliceEnd"/>), and each cut station is the straight mix of the two
    /// columns either side of it, in every channel. The quads it cuts are re-cut along their own diagonal, so every
    /// new triangle lies inside one the whole face drew and draws exactly what that one drew there. A wall with no cut
    /// station is laid and triangulated exactly as it always was, to the bit. Where a top was moved out, the texture
    /// can keep running along where it was (<c>_textureBrowPlan</c>), and the run's offsets downstream with it.</para>
    ///
    /// <para><b>Pure look.</b> Drives no sim and saves nothing (rule 5). Walkability at a cliff belongs to
    /// the terrain's own slope, not to this component — a wall is a picture OF the height field, and the
    /// height field remains the single source of truth for where a player may stand. The waterline is
    /// colour on rock and changes nothing about that.</para>
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
        [Tooltip("Toe ELEVATION, metres above chart datum, per station — the absolute height the drop " +
                 "falls TO, and what the WATERLINE is measured against (2026-08-06). The drop alone is a " +
                 "difference and cannot say where the sea meets the rock. Builder-pushed off the same " +
                 "analytic profile the brow and toe plans come from. An array shorter than the stations " +
                 "(a scene saved before the waterline existed) leaves the waterline INERT for that chunk, " +
                 "which draws exactly the wall that shipped — a region builder re-run turns it on.")]
        [SerializeField] private float[] _toeElevations = new float[0];

        [Header("A re-cut wall: slices of the face its whole stations draw (terrain PR 5w)")]
        [Tooltip("Where the texture runs along at each station (world XY), where that is not the brow: a top the " +
                 "builder moved out keeps the face's texture, and the run's offsets downstream, where they were. " +
                 "Empty = the brow.")]
        [SerializeField] private Vector2[] _textureBrowPlan = new Vector2[0];
        [Tooltip("Per station: 0 for a whole station, else how far it is cut from the whole station before it " +
                 "towards the one after (0..1). A cut station draws as that slice of the face the two of them draw, " +
                 "in every channel, so cutting a wall finer moves no rock. Empty = every station whole.")]
        [SerializeField] private float[] _sliceAt = new float[0];
        [Tooltip("The whole station before the first one, when the first is cut (one, or none).")]
        [SerializeField] private CliffWallSliceEnd[] _sliceBefore = new CliffWallSliceEnd[0];
        [Tooltip("The whole station after the last one, when the last is cut (one, or none).")]
        [SerializeField] private CliffWallSliceEnd[] _sliceAfter = new CliffWallSliceEnd[0];

        [Header("⭐ Run continuity — a chunk is a SLICE, and slicing must not show (B3)")]
        [Tooltip("Metres of shore already spent by earlier chunks of this RUN. Restart it at zero and " +
                 "the texture AND the profile displacement both jump at every cut.")]
        [SerializeField] private float _alongOffsetMetres;
        [Tooltip("The RUN's longest face, in surface metres — the row count every chunk in the run " +
                 "shares, so two chunks approximate their shared displaced edge identically.")]
        [SerializeField] private float _rowsBasisSurfaceMetres;

        [Header("The rock, in bands from the brow down (kit v10 rocks)")]
        [SerializeField] private CliffFaceBand[] _bands = new CliffFaceBand[0];

        [Header("Kit channels shared by every band")]
        [SerializeField] private Material _material;
        [Tooltip("The plan-displacement map for this chunk. Imported CPU-readable " +
                 "(CliffCatalog.IsCpuReadable) because THIS is what reads it. Null = an undisplaced " +
                 "wall, which still draws — it just stops at the texture, the complaint kit v10 exists " +
                 "to answer.\n\n⚠ ONE profile serves EVERY band. The profile is the LANDFORM's plan " +
                 "displacement — the ribs, clefts and benches the whole cliff is cut into — and the " +
                 "bands are materials lying on that one landform. Give each band its own and they tear " +
                 "apart at the horizon by up to twice the profile's range.")]
        [SerializeField] private Texture2D _profile;

        [Header("Brow and toe decals — the kit's own finisher for a face's ends")]
        [Tooltip("The hanging sod lip where the clifftop's turf rolls over and hangs. Straddles the " +
                 "brow: the top BrowLineAt of it is plan-view grass, the rest is the undercut shadow " +
                 "and the soil wash spilling onto the rock.")]
        [SerializeField] private Texture2D _browDecal;
        [Tooltip("Where the face lands — the sea's undercut notch, the salt-bleached basal beds, and " +
                 "the debris lying against the foot. Its bottom edge seats on the drawn toe.")]
        [SerializeField] private Texture2D _toeDecal;

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
        [Tooltip("Height of a brow/toe decal strip in metres — CliffCatalog.StripMetresT.")]
        [SerializeField] private float _stripMetresT = 4f;
        [Tooltip("Where the sod line sits in the brow strip — CliffCatalog.BrowLineAt.")]
        [SerializeField] private float _browLineAt = 0.42f;

        private readonly List<GameObject> _generated = new List<GameObject>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private SortingGroup _group;
        private MaterialPropertyBlock _mpb;

        private static readonly int IdUnlit = Shader.PropertyToID("_Unlit");
        private static readonly int IdNormal = Shader.PropertyToID("_Normal");
        private static readonly int IdMask = Shader.PropertyToID("_Mask");
        private static readonly int IdWallAzimuth = Shader.PropertyToID("_WallAzimuth");
        private static readonly int IdBatter = Shader.PropertyToID("_Batter");
        private static readonly int IdBakeL = Shader.PropertyToID("_BakeL");
        private static readonly int IdTileMetresS = Shader.PropertyToID("_TileMetresS");
        private static readonly int IdTileMetresT = Shader.PropertyToID("_TileMetresT");
        private static readonly int IdPxDecal = Shader.PropertyToID("_PxDecal");

        /// <summary>The name of the generated child carrying a band's quads, so a hierarchy reads as
        /// geology and a test can find one without a magic string of its own.</summary>
        public const string BandChildPrefix = "CliffWallQuads_";
        public const string BrowChildName = "CliffWallBrowDecal";
        public const string ToeChildName = "CliffWallToeDecal";

        /// <summary>How many stations this chunk carries — read by the builder's own idempotence check
        /// and by the tests, so neither has to reach into a serialized field by name.</summary>
        public int StationCount => _browPlan != null ? _browPlan.Length : 0;

        /// <summary>The sorting order this chunk resolved to, or 0 before it has built. Exposed so an
        /// EditMode pin can assert the ladder without a camera.</summary>
        public int SortingOrder { get; private set; }

        /// <summary>The lowest DRAWN toe Y over the chunk — the point the whole chunk sorts by, and the
        /// number the ladder pin is derived from.</summary>
        public float SortY { get; private set; }

        /// <summary>How many renderers this chunk actually built — face bands plus whichever decals had a
        /// texture. Pinned rather than inferred, because "the strata are on" is otherwise invisible.</summary>
        public int RendererCount { get; private set; }

        /// <summary>Metres of shore spent before this chunk, within its run. See the class note.</summary>
        public float AlongOffsetMetres => _alongOffsetMetres;

        /// <summary>The run's longest face, which sets this chunk's row count. See the class note.</summary>
        public float RowsBasisSurfaceMetres => _rowsBasisSurfaceMetres;

        // =============================================================================================
        //  configure
        // =============================================================================================

        /// <summary>
        /// Push a chunk's authored run. Every argument is data the BUILDER owns — the stations come off
        /// the region's own <c>TidalTerrain</c> profile and the kit constants come off
        /// <c>CliffCatalog</c>, so nothing about the coast or the kit is re-declared here (rule 6).
        /// </summary>
        public void Configure(Vector2[] browPlan, Vector2[] toePlan, float[] dropMetres,
                              Material material, CliffFaceBand[] bands, Texture2D profile,
                              Texture2D browDecal, Texture2D toeDecal,
                              float alongOffsetMetres, float rowsBasisSurfaceMetres,
                              float wallAzimuth, float batter, Vector3 bakeLight,
                              float faceMetresS, float faceMetresT, float subdivideMetres,
                              float profileMetres, float stripMetresT, float browLineAt,
                              float[] toeElevations = null,
                              Vector2[] textureBrowPlan = null, float[] sliceAt = null,
                              CliffWallSliceEnd[] sliceBefore = null, CliffWallSliceEnd[] sliceAfter = null)
        {
            _browPlan = browPlan ?? new Vector2[0];
            _toePlan = toePlan ?? new Vector2[0];
            _dropMetres = dropMetres ?? new float[0];
            _toeElevations = toeElevations ?? new float[0];
            _textureBrowPlan = textureBrowPlan ?? new Vector2[0];
            _sliceAt = sliceAt ?? new float[0];
            _sliceBefore = sliceBefore ?? new CliffWallSliceEnd[0];
            _sliceAfter = sliceAfter ?? new CliffWallSliceEnd[0];
            _material = material;
            _bands = bands ?? new CliffFaceBand[0];
            _profile = profile;
            _browDecal = browDecal;
            _toeDecal = toeDecal;
            _alongOffsetMetres = alongOffsetMetres;
            _rowsBasisSurfaceMetres = rowsBasisSurfaceMetres;
            _wallAzimuth = wallAzimuth;
            _batter = batter;
            _bakeLight = bakeLight;
            _faceMetresS = faceMetresS;
            _faceMetresT = faceMetresT;
            _subdivideMetres = subdivideMetres;
            _profileMetres = profileMetres;
            _stripMetresT = stripMetresT;
            _browLineAt = browLineAt;
            Rebuild();
        }

        /// <summary>
        /// The pre-strata shape: one rock over the whole face, no decals, and a chunk that is its own run.
        /// Kept because it IS the degenerate case — a single band starting at the brow and running to the
        /// toe with a zero offset builds exactly the mesh this component built before, which is what makes
        /// the richer path above provably a superset rather than a replacement.
        /// </summary>
        public void Configure(Vector2[] browPlan, Vector2[] toePlan, float[] dropMetres,
                              Material material, Texture2D unlit, Texture2D normal, Texture2D mask,
                              Texture2D profile, float wallAzimuth, float batter, Vector3 bakeLight,
                              float faceMetresS, float faceMetresT, float subdivideMetres,
                              float profileMetres)
        {
            var samples = BuildSamples(browPlan, toePlan, dropMetres);
            Configure(browPlan, toePlan, dropMetres, material,
                      new[] { CliffFaceBand.WholeFace(unlit, normal, mask) },
                      profile, browDecal: null, toeDecal: null,
                      alongOffsetMetres: 0f,
                      rowsBasisSurfaceMetres: CliffWallGeometry.RowsBasisSurfaceMetres(samples),
                      wallAzimuth, batter, bakeLight, faceMetresS, faceMetresT, subdivideMetres,
                      profileMetres, stripMetresT: 4f, browLineAt: 0.42f);
        }

        private void OnEnable() => Rebuild();

        private void OnDisable() => Teardown();

        private void OnValidate()
        {
            if (isActiveAndEnabled) Rebuild();
        }

        // =============================================================================================
        //  build
        // =============================================================================================

        /// <summary>Drop every generated child and mesh. Idempotent — a Rebuild always starts here, which
        /// is what makes a builder Refresh converge instead of stacking a second wall on the first.</summary>
        private void Teardown()
        {
            for (int i = 0; i < _meshes.Count; i++)
                if (_meshes[i] != null) DestroyMesh(_meshes[i]);
            _meshes.Clear();

            for (int i = 0; i < _generated.Count; i++)
                if (_generated[i] != null) DestroyGo(_generated[i]);
            _generated.Clear();

            // ⚠ Children created by an EARLIER session are not in _generated (the lists are runtime
            // state and the children are DontSave, but a domain reload between Configure and Rebuild
            // leaves orphans). Sweep by name so a Refresh still converges.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith(BandChildPrefix) ||
                    child.name == BrowChildName || child.name == ToeChildName)
                    DestroyGo(child.gameObject);
            }

            RendererCount = 0;
        }

        private void Rebuild()
        {
            Teardown();
            int stations = StationCount;
            if (stations < 2 || _toePlan.Length < stations || _dropMetres.Length < stations) return;

            bool anyBand = false;
            for (int i = 0; i < _bands.Length; i++) if (_bands[i].HasChannels) { anyBand = true; break; }
            if (!anyBand)
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

            CliffWallSample[] samples = BuildSamples(_browPlan, _toePlan, _dropMetres, _toeElevations);
            ResolveSorting(samples);

            // ⭐ THE SORTING GROUP SITS ON THE CHUNK, NOT ON A MESH.
            //
            // A chunk now draws several renderers at one place — its rock bands and its two decals — and
            // their order among THEMSELVES is a private detail (a decal draws over the face it seats).
            // A SortingGroup is exactly that: the group competes with the region by ONE position-derived
            // order, and its children sort inside it. So the decals' +1 never reaches the decor band and
            // ADR 0032's rule against fixed orders in the band is not bent. It also satisfies
            // RegionValidatorWindow, which looks for a group in the PARENTS of every MeshRenderer.
            _group = GetComponent<SortingGroup>();
            if (_group == null) _group = gameObject.AddComponent<SortingGroup>();
            _group.sortingOrder = SortingOrder;

            int rows = CliffWallGeometry.RowsFor(
                _rowsBasisSurfaceMetres > 0f
                    ? _rowsBasisSurfaceMetres
                    : CliffWallGeometry.RowsBasisSurfaceMetres(samples),
                _subdivideMetres);

            // A re-cut wall is laid on its whole stations and sliced at its own (see the class note). Slice data
            // that does not describe a slice is not guessed at: the wall draws its stations as columns, and says so.
            if (!TryColumns(samples, _textureBrowPlan, _sliceAt, _sliceBefore, _sliceAfter,
                            out CliffWallColumns columns, out string problem))
            {
                Debug.LogWarning($"[cliff-wall] '{name}': {problem} — drawing its stations unsliced.", this);
                columns = Unsliced(samples);
            }

            for (int b = 0; b < _bands.Length; b++)
            {
                if (!_bands[b].HasChannels) continue;
                string label = string.IsNullOrEmpty(_bands[b].Label) ? b.ToString() : _bands[b].Label;
                BuildBand(columns, _bands[b], rows, BandChildPrefix + label, localOrder: 0);
            }

            // The decals ride ONE row band above the face so they composite over the rock they finish.
            if (_browDecal != null) BuildDecal(columns, _browDecal, brow: true, BrowChildName);
            if (_toeDecal != null) BuildDecal(columns, _toeDecal, brow: false, ToeChildName);
        }

        /// <summary>A chunk's stations as samples, the way <see cref="Rebuild"/> reads its serialized lines. Public so a
        /// test lays a wall's mesh from the same samples the component would.</summary>
        public static CliffWallSample[] BuildSamples(Vector2[] brow, Vector2[] toe, float[] drop,
                                                     float[] toeElevation = null)
        {
            int n = brow == null ? 0 : brow.Length;
            if (toe == null || drop == null || toe.Length < n || drop.Length < n) n = 0;
            // ⚠️ NO authored toe elevations = a scene saved before the waterline existed. Every sample
            // then carries elevation 0, which the VALID FLAG below is what stops the shader believing:
            // an unflagged 0 would read as metres under water at any flood tide and draw the whole cliff
            // drowned. A missing elevation has to be able to SAY so rather than look like chart datum.
            bool haveElevation = toeElevation != null && toeElevation.Length >= n;
            var samples = new CliffWallSample[n];
            for (int i = 0; i < n; i++)
                samples[i] = new CliffWallSample(brow[i], toe[i], drop[i],
                                                 haveElevation ? toeElevation[i] : 0f);
            return samples;
        }

        /// <summary>Whether this chunk knows where the sea meets it — i.e. whether the builder pushed
        /// absolute toe elevations. 0 in the mesh's elevation-valid channel when it does not, which is
        /// the shader's own gate. See <see cref="CliffWaterlineMath"/>.</summary>
        private bool HasToeElevations =>
            _toeElevations != null && _toeElevations.Length >= StationCount;

        private void ResolveSorting(CliffWallSample[] samples)
        {
            // ⭐ THE LADDER. The chunk sorts by its lowest DRAWN toe through the decor band's own
            // mapping — not a constant, and not a slot of its own. ADR 0032's lesson is that a fixed
            // order inside the band is buried deterministically the moment the band is re-based, so the
            // wall derives from position like everything else that shares the band.
            SortY = CliffWallGeometry.SortY(samples);
            SortingOrder = YSortSprite.OrderFor(SortY, SortingBands.DecorBase,
                                                SortingBands.OrdersPerMetre,
                                                SortingBands.DecorFloor, SortingBands.DecorCeiling);
        }

        /// <summary>
        /// The plan displacement at a point on the face, in metres along the outward normal — the kit's
        /// form pass made geometry (rig README §5). Skip it and the depth stops at the texture: the
        /// silhouette stays a smooth curve and the brow line reads as a drawn edge rather than as rock.
        ///
        /// <para><b>⚠ <paramref name="uTiles"/> is wrapped HERE and nowhere else.</b> The mesh's UVs stay
        /// unwrapped so the GPU interpolates across a period boundary correctly; only this CPU read needs
        /// a number inside the texture.</para>
        /// </summary>
        private float DisplacementAt(float uTiles, float t)
        {
            if (_profile == null) return 0f;
            float grey = _profile.GetPixelBilinear(CliffWallGeometry.WrapTile01(uTiles), 1f - t).r;
            return CliffWallGeometry.ProfileMetresFromGrey(grey, _profileMetres);
        }

        private void BuildBand(CliffWallColumns columns, in CliffFaceBand band, int rows,
                               string childName, int localOrder)
        {
            float elevationValid = HasToeElevations ? 1f : 0f;
            CliffWallMeshData m = FaceMesh(columns, band.StartSurfaceMetres, band.EndSurfaceMetres, rows,
                                           _alongOffsetMetres, _faceMetresS, _faceMetresT, DisplacementAt,
                                           elevationValid, transform.position);
            Emit(childName, m.Verts, m.Uvs, m.Tris, band.Unlit, band.Normal, band.Mask, localOrder,
                 m.ElevationUv, m.SeaPlanUv);
        }

        /// <summary>
        /// A brow or toe strip, laid on the SAME stations as the face so it curves with the coast and
        /// rides the same displacement — a decal that did not would slide off the wall it is finishing
        /// the moment the profile pushed the rock a metre out.
        ///
        /// <para><b>The brow strip straddles the brow and the toe strip does not.</b> Above its sod line
        /// the brow strip is drawing PLAN ground (the clifftop's turf, seen from above), so that part
        /// steps INLAND against the outward normal and is held rigid at the brow's own displacement —
        /// the ground edge is the brow. Below the line, and for the whole toe strip, it is drawing the
        /// FACE, so it follows brow-to-toe in surface metres and samples the profile at the matching
        /// depth, exactly as the rock under it does.</para>
        /// </summary>
        private void BuildDecal(CliffWallColumns columns, Texture2D strip, bool brow, string childName)
        {
            float elevationValid = HasToeElevations ? 1f : 0f;
            CliffWallMeshData m = DecalMesh(columns, brow, _stripMetresT, _browLineAt, _subdivideMetres,
                                            _alongOffsetMetres, _faceMetresS, DisplacementAt, elevationValid,
                                            transform.position);
            // ⭐ localOrder 1: INSIDE the sorting group, so a decal composites over the rock it finishes
            // without spending an order of the region's decor band.
            Emit(childName, m.Verts, m.Uvs, m.Tris, strip, null, null, localOrder: 1,
                 m.ElevationUv, m.SeaPlanUv, decal: true);
        }

        // ── The mesh as data ────────────────────────────────────────────────────────────────────────
        // Static and engine-light (the profile is read through a delegate, the origin is passed in), so a
        // test lays the exact floats a chunk draws with no scene, texture or renderer.

        /// <summary>How far (m) a cut station may sit off the slice its whole stations draw at its fraction, in brow,
        /// toe, drop, toe elevation or texture brow, before its slice data is refused. The builder writes exact mixes.</summary>
        public const float SliceToleranceMetres = 1e-3f;

        /// <summary>One column per station, along the brows: how every wall was laid before the re-cut, and how a wall
        /// with no cut station still is.</summary>
        public static CliffWallColumns Unsliced(CliffWallSample[] samples)
        {
            int n = samples.Length;
            var alongBrow = new Vector2[n];
            var gap = new int[n];
            for (int i = 0; i < n; i++)
            {
                alongBrow[i] = samples[i].BrowPlan;
                gap[i] = i;
            }
            return new CliffWallColumns
            {
                Whole = samples, AlongBrow = alongBrow, Gap = gap, At = new float[n], Sliced = false,
            };
        }

        /// <summary>
        /// The columns a chunk's stations are laid on, from its slice data, all of it optional: none at all is
        /// <see cref="Unsliced"/>, and texture brows alone keep one column per station. False, with the reason, where
        /// the data does not describe a slice: a length does not match, a cut end has no whole station past it (or a
        /// whole one has one), the cuts do not run in order, or a cut station is not the straight mix of the whole
        /// stations either side of it.
        /// </summary>
        public static bool TryColumns(CliffWallSample[] samples, Vector2[] textureBrowPlan, float[] sliceAt,
                                      CliffWallSliceEnd[] sliceBefore, CliffWallSliceEnd[] sliceAfter,
                                      out CliffWallColumns columns, out string problem)
        {
            columns = null;
            problem = null;
            int n = samples.Length;
            int texN = textureBrowPlan == null ? 0 : textureBrowPlan.Length;
            int atN = sliceAt == null ? 0 : sliceAt.Length;
            int beforeN = sliceBefore == null ? 0 : sliceBefore.Length;
            int afterN = sliceAfter == null ? 0 : sliceAfter.Length;
            if (texN != 0 && texN != n) { problem = $"{texN} texture brows for {n} stations"; return false; }
            if (atN != 0 && atN != n) { problem = $"{atN} slice fractions for {n} stations"; return false; }

            bool anyCut = false;
            for (int i = 0; i < atN; i++)
            {
                if (!(sliceAt[i] >= 0f && sliceAt[i] < 1f))
                {
                    problem = $"station {i} is cut at {sliceAt[i]}, outside [0, 1)";
                    return false;
                }
                if (sliceAt[i] > 0f) anyCut = true;
            }
            bool firstCut = anyCut && sliceAt[0] > 0f;
            bool lastCut = anyCut && sliceAt[n - 1] > 0f;
            if (beforeN != (firstCut ? 1 : 0))
            {
                problem = $"{beforeN} whole stations before a first station cut at {(atN == 0 ? 0f : sliceAt[0])}";
                return false;
            }
            if (afterN != (lastCut ? 1 : 0))
            {
                problem = $"{afterN} whole stations after a last station cut at {(atN == 0 ? 0f : sliceAt[n - 1])}";
                return false;
            }

            if (!anyCut)
            {
                columns = Unsliced(samples);
                if (texN != 0) columns.AlongBrow = (Vector2[])textureBrowPlan.Clone();
                return true;
            }

            var whole = new List<CliffWallSample>(n + 2);
            var alongBrow = new List<Vector2>(n + 2);
            var gap = new int[n];
            var at = new float[n];
            if (firstCut)
            {
                whole.Add(sliceBefore[0].Sample);
                alongBrow.Add(sliceBefore[0].TextureBrowPlan);
            }
            for (int i = 0; i < n; i++)
            {
                if (sliceAt[i] == 0f)
                {
                    whole.Add(samples[i]);
                    alongBrow.Add(texN == 0 ? samples[i].BrowPlan : textureBrowPlan[i]);
                }
                gap[i] = whole.Count - 1;
                at[i] = sliceAt[i];
                if (i > 0 && gap[i] == gap[i - 1] && !(at[i] > at[i - 1]))
                {
                    problem = $"station {i} is cut at {at[i]}, not past station {i - 1} at {at[i - 1]}";
                    return false;
                }
            }
            if (lastCut)
            {
                whole.Add(sliceAfter[0].Sample);
                alongBrow.Add(sliceAfter[0].TextureBrowPlan);
            }

            for (int i = 0; i < n; i++)
            {
                float f = at[i];
                if (f == 0f) continue;
                CliffWallSample a = whole[gap[i]], b = whole[gap[i] + 1], s = samples[i];
                Vector2 tex = texN == 0 ? s.BrowPlan : textureBrowPlan[i];
                float off = Mathf.Max(
                    Mathf.Max(Vector2.Distance(s.BrowPlan, Vector2.LerpUnclamped(a.BrowPlan, b.BrowPlan, f)),
                              Vector2.Distance(s.ToePlan, Vector2.LerpUnclamped(a.ToePlan, b.ToePlan, f))),
                    Mathf.Max(Mathf.Abs(s.DropMetres - Mathf.LerpUnclamped(a.DropMetres, b.DropMetres, f)),
                              Mathf.Abs(s.ToeElevation - Mathf.LerpUnclamped(a.ToeElevation, b.ToeElevation, f))));
                off = Mathf.Max(off, Vector2.Distance(
                    tex, Vector2.LerpUnclamped(alongBrow[gap[i]], alongBrow[gap[i] + 1], f)));
                if (!(off <= SliceToleranceMetres))
                {
                    problem = $"station {i} sits {off:0.####} m off the slice at {f} of the stations either side";
                    return false;
                }
            }

            columns = new CliffWallColumns
            {
                Whole = whole.ToArray(), AlongBrow = alongBrow.ToArray(), Gap = gap, At = at, Sliced = true,
            };
            return true;
        }

        /// <summary>
        /// One band's mesh: the face between two depths below the brow, laid on <paramref name="columns"/>. A wall with
        /// no cut station is laid and triangulated exactly as it always was; a cut one is laid on its whole stations
        /// and then sliced (<see cref="Slice"/>). <paramref name="displacement"/> is the profile read, (u, t) → metres.
        /// </summary>
        public static CliffWallMeshData FaceMesh(CliffWallColumns columns, float startSurfaceMetres,
                                                 float endSurfaceMetres, int rows, float alongOffsetMetres,
                                                 float faceMetresS, float faceMetresT,
                                                 System.Func<float, float, float> displacement,
                                                 float elevationValid, Vector3 origin)
        {
            CliffWallSample[] samples = columns.Whole;
            int stations = samples.Length;
            var verts = new Vector3[stations * (rows + 1)];
            var uvs = new Vector2[verts.Length];
            var elevationUv = new Vector2[verts.Length];
            var seaPlanUv = new Vector2[verts.Length];

            float along = 0f;                       // plan arc length down the shore, metres

            for (int c = 0; c < stations; c++)
            {
                // Along where the texture runs: the brow, unless a top was moved out (see the class note).
                if (c > 0) along += Vector2.Distance(columns.AlongBrow[c - 1], columns.AlongBrow[c]);

                CliffWallSample s = samples[c];
                Vector2 brow = s.BrowPlan;
                Vector2 toe = CliffWallGeometry.ToeScreen(in s);
                Vector2 outward = CliffWallGeometry.OutwardPlan(in s);
                float surface = Mathf.Max(1e-4f, CliffWallGeometry.SurfaceLengthMetres(in s));
                float u = CliffWallGeometry.TileU(alongOffsetMetres + along, faceMetresS);

                // A band is bounded in DEPTH below the brow, so each station converts it to its own t —
                // which is what lets two bands meet exactly at every station of a face whose height is
                // changing under them. A station shorter than the band's start collapses to the toe,
                // which is exactly right: on a face shorter than the soil horizon there is no rock band.
                float t0 = Mathf.Clamp01(startSurfaceMetres / surface);
                float t1 = endSurfaceMetres > startSurfaceMetres
                         ? Mathf.Clamp01(endSurfaceMetres / surface)
                         : 1f;
                if (t1 < t0) t1 = t0;

                for (int r = 0; r <= rows; r++)
                {
                    float t = Mathf.Lerp(t0, t1, (float)r / rows);
                    Vector2 p = Vector2.Lerp(brow, toe, t) + outward * displacement(u, t);

                    int idx = c * (rows + 1) + r;
                    verts[idx] = new Vector3(p.x - origin.x, p.y - origin.y, 0f);
                    // v counts SURFACE metres from the BAND's own top, never height and never drawn
                    // screen height — the one rule the kit states twice and the reason _TileMetresT is
                    // not asked of the shader.
                    uvs[idx] = new Vector2(
                        u, CliffWallGeometry.TileV(surface * t - startSurfaceMetres, faceMetresT));
                    // The elevation walks the SURFACE parameter, brow → toe, which is the row parameter
                    // the geometry is laid on — so the waterline crossing lands on the rows the mesh
                    // already has instead of between them.
                    elevationUv[idx] = new Vector2(
                        CliffWaterlineMath.ElevationAt(s.ToeElevation + s.DropMetres, s.DropMetres, t),
                        elevationValid);
                    seaPlanUv[idx] = s.ToePlan;
                }
            }

            return Laid(columns, rows, verts, uvs, elevationUv, seaPlanUv);
        }

        /// <summary>
        /// A brow (<paramref name="brow"/> true) or toe strip's mesh, laid on <paramref name="columns"/> the way
        /// <see cref="FaceMesh"/> lays a band, and sliced the same way where a station is cut.
        /// </summary>
        public static CliffWallMeshData DecalMesh(CliffWallColumns columns, bool brow, float stripMetresT,
                                                  float browLineAt, float subdivideMetres, float alongOffsetMetres,
                                                  float faceMetresS, System.Func<float, float, float> displacement,
                                                  float elevationValid, Vector3 origin)
        {
            CliffWallSample[] samples = columns.Whole;
            int stations = samples.Length;
            int rows = Mathf.Max(2, Mathf.CeilToInt(stripMetresT / Mathf.Max(0.01f, subdivideMetres)));

            float inland = CliffWallGeometry.BrowDecalInlandMetres(stripMetresT, browLineAt);
            float browFace = CliffWallGeometry.BrowDecalFaceMetres(stripMetresT, browLineAt);
            float toeFace = CliffWallGeometry.ToeDecalFaceMetres(stripMetresT);
            float line = Mathf.Clamp(browLineAt, 1e-3f, 1f - 1e-3f);

            var verts = new Vector3[stations * (rows + 1)];
            var uvs = new Vector2[verts.Length];
            var elevationUv = new Vector2[verts.Length];
            var seaPlanUv = new Vector2[verts.Length];

            float along = 0f;

            for (int c = 0; c < stations; c++)
            {
                if (c > 0) along += Vector2.Distance(columns.AlongBrow[c - 1], columns.AlongBrow[c]);

                CliffWallSample s = samples[c];
                Vector2 browP = s.BrowPlan;
                Vector2 toeP = CliffWallGeometry.ToeScreen(in s);
                Vector2 outward = CliffWallGeometry.OutwardPlan(in s);
                float surface = Mathf.Max(1e-4f, CliffWallGeometry.SurfaceLengthMetres(in s));
                float u = CliffWallGeometry.TileU(alongOffsetMetres + along, faceMetresS);
                float atBrow = displacement(u, 0f);

                for (int r = 0; r <= rows; r++)
                {
                    float v = (float)r / rows;              // 0 at the strip's top, 1 at its bottom
                    Vector2 p;
                    // The strip's own depth down the face, for the waterline. Above the brow line the
                    // brow strip is drawing PLAN GROUND, which is at the brow's own elevation — so t 0.
                    float faceT = 0f;

                    if (brow && v <= line)
                    {
                        // Plan ground behind the lip, held rigid at the brow's displacement.
                        p = browP - outward * (inland * (1f - v / line)) + outward * atBrow;
                    }
                    else
                    {
                        float depth = brow
                            ? (v - line) / (1f - line) * browFace          // metres below the brow
                            : surface - (1f - v) * toeFace;                // ...up from the toe
                        faceT = Mathf.Clamp01(depth / surface);
                        p = Vector2.Lerp(browP, toeP, faceT) + outward * displacement(u, faceT);
                    }

                    int idx = c * (rows + 1) + r;
                    verts[idx] = new Vector3(p.x - origin.x, p.y - origin.y, 0f);
                    // The strip clamps in V (CliffCatalog.WrapV) — one strip covers the height exactly,
                    // and it tiles along the shore on the same continuous u the face uses.
                    uvs[idx] = new Vector2(u, v);
                    // The TOE strip is the piece the sea actually reaches, so the waterline has to reach
                    // it too — a decal drawn dry across a wet foot is the seam this whole item exists to
                    // close. Same channels, same rule, same gate as the rock above it.
                    elevationUv[idx] = new Vector2(
                        CliffWaterlineMath.ElevationAt(s.ToeElevation + s.DropMetres, s.DropMetres, faceT),
                        elevationValid);
                    seaPlanUv[idx] = s.ToePlan;
                }
            }

            return Laid(columns, rows, verts, uvs, elevationUv, seaPlanUv);
        }

        private static CliffWallMeshData Laid(CliffWallColumns columns, int rows, Vector3[] verts, Vector2[] uvs,
                                              Vector2[] elevationUv, Vector2[] seaPlanUv)
        {
            var laid = new CliffWallMeshData
            {
                Verts = verts, Uvs = uvs, ElevationUv = elevationUv, SeaPlanUv = seaPlanUv,
            };
            if (columns.Sliced) return Slice(columns, rows, laid);

            int stations = columns.Whole.Length;
            laid.Tris = new int[(stations - 1) * rows * 6];
            Triangulate(laid.Tris, stations, rows);
            return laid;
        }

        /// <summary>
        /// ⭐ A re-cut wall's mesh, from the one its whole stations lay (<paramref name="laid"/>, a column per whole
        /// station). Each of the wall's own stations is a column: a whole one copies its own, a cut one is the
        /// straight mix of the two either side of it, in every channel. The old quads are two triangles split
        /// along the diagonal from a row's top on the left to its foot on the right (<see cref="Triangulate"/>),
        /// so a cut also takes the mix along that diagonal, and the pieces either side of every cut are triangles
        /// inside the old ones. A triangle's channels are linear across it, so each piece draws exactly what the
        /// old triangle drew there: cutting a wall finer moves no rock and slides no texture.
        /// </summary>
        private static CliffWallMeshData Slice(CliffWallColumns columns, int rows, CliffWallMeshData laid)
        {
            int n = columns.Gap.Length;
            int rowVerts = rows + 1;
            var ordinal = new int[n];
            int cuts = 0;
            for (int j = 0; j < n; j++) ordinal[j] = columns.At[j] > 0f ? cuts++ : -1;

            int diagonals = n * rowVerts;           // where the cuts' diagonal vertices start
            int count = diagonals + cuts * rows;
            var verts = new Vector3[count];
            var uvs = new Vector2[count];
            var elevation = new Vector2[count];
            var sea = new Vector2[count];

            for (int j = 0; j < n; j++)
            {
                int left = columns.Gap[j] * rowVerts;
                float f = columns.At[j];
                if (f == 0f)
                {
                    for (int r = 0; r <= rows; r++)
                    {
                        int o = j * rowVerts + r;
                        verts[o] = laid.Verts[left + r];
                        uvs[o] = laid.Uvs[left + r];
                        elevation[o] = laid.ElevationUv[left + r];
                        sea[o] = laid.SeaPlanUv[left + r];
                    }
                    continue;
                }
                int right = left + rowVerts;
                for (int r = 0; r <= rows; r++)
                    Mix(laid, left + r, right + r, f, j * rowVerts + r, verts, uvs, elevation, sea);
                // Where this cut crosses each row's diagonal: from the left column's row above to the right's own.
                for (int r = 0; r < rows; r++)
                    Mix(laid, left + r + 1, right + r, f, diagonals + ordinal[j] * rows + r, verts, uvs, elevation, sea);
            }

            var tris = new List<int>((n - 1) * rows * 12);
            for (int i = 0; i < n - 1; i++)
            {
                // This strip runs from station i to i + 1, inside ONE old column gap: fi and fj are how far across it.
                float fi = columns.At[i];
                float fj = columns.Gap[i + 1] == columns.Gap[i] ? columns.At[i + 1] : 1f;
                for (int r = 0; r < rows; r++)
                {
                    int mi = i * rowVerts + r, ni = mi + 1;
                    int mj = (i + 1) * rowVerts + r, nj = mj + 1;
                    int pi = fi == 0f ? ni : diagonals + ordinal[i] * rows + r;
                    int pj = fj == 1f ? mj : diagonals + ordinal[i + 1] * rows + r;
                    // Under the diagonal (the old a, d, b), then over it (the old b, d, e), in the old winding.
                    // Where both stations are whole the middle two ARE the old pair, in the old order.
                    if (fj < 1f) { tris.Add(mi); tris.Add(mj); tris.Add(pj); }
                    tris.Add(mi); tris.Add(pj); tris.Add(pi);
                    tris.Add(pi); tris.Add(pj); tris.Add(nj);
                    if (fi > 0f) { tris.Add(pi); tris.Add(nj); tris.Add(ni); }
                }
            }

            return new CliffWallMeshData
            {
                Verts = verts, Uvs = uvs, ElevationUv = elevation, SeaPlanUv = sea, Tris = tris.ToArray(),
            };
        }

        private static void Mix(CliffWallMeshData laid, int a, int b, float f, int o,
                                Vector3[] verts, Vector2[] uvs, Vector2[] elevation, Vector2[] sea)
        {
            verts[o] = Vector3.LerpUnclamped(laid.Verts[a], laid.Verts[b], f);
            uvs[o] = Vector2.LerpUnclamped(laid.Uvs[a], laid.Uvs[b], f);
            elevation[o] = Vector2.LerpUnclamped(laid.ElevationUv[a], laid.ElevationUv[b], f);
            sea[o] = Vector2.LerpUnclamped(laid.SeaPlanUv[a], laid.SeaPlanUv[b], f);
        }

        private static void Triangulate(int[] tris, int stations, int rows)
        {
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
        }

        private void Emit(string childName, Vector3[] verts, Vector2[] uvs, int[] tris,
                          Texture2D unlit, Texture2D normal, Texture2D mask, int localOrder,
                          Vector2[] elevationUv = null, Vector2[] seaPlanUv = null, bool decal = false)
        {
            var mesh = new Mesh { name = "HH" + childName, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            // ⭐ THE WATERLINE CHANNELS (2026-08-06). uv1 = (this vertex's ELEVATION in metres, 1 if that
            // elevation is REAL); uv2 = the station's TOE PLAN, which is where the sea actually is — the
            // DRAWN toe has already been pushed down-screen by the drop, so sampling the wave field at it
            // would read the swell several metres from the wall it belongs to.
            if (elevationUv != null) mesh.SetUVs(1, elevationUv);
            if (seaPlanUv != null) mesh.SetUVs(2, seaPlanUv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            _meshes.Add(mesh);

            var go = new GameObject(childName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.sortingOrder = SortingOrder + localOrder;

            PushMaterial(renderer, unlit, normal, mask, decal);
            _generated.Add(go);
            RendererCount++;
        }

        /// <summary>
        /// Push a renderer's per-face parameters once, through a property block, so every band and every
        /// chunk shares the one shipped <c>CliffFace.mat</c> instead of minting a material asset per rock
        /// × aspect × batter. Set at build time only — nothing here runs per frame (rule 7).
        ///
        /// <para><b>⭐ A DECAL RIDES THE SAME SHADER, AND THAT IS NOT A SHORTCUT.</b> The kit's brow and
        /// toe strips ship ONE pre-lit RGBA channel — README §8 is explicit that they are fixed-sun,
        /// their darks being cast shadow and geometric occlusion rather than <c>N·L</c>. Leaving
        /// <c>_Normal</c> and <c>_Mask</c> unset hands the shader its own declared defaults, a flat
        /// "bump" and a white mask, and the maths then collapses to exactly the right thing: a flat
        /// tangent normal makes <c>N·L</c> the wall's own facing term, the white mask divides out to a
        /// cast shadow of 1, and coverage falls through to the strip's own alpha. So a decal is lit as a
        /// FLAT texel of the wall it sits on — it tracks the sun with the face instead of sitting still
        /// beside it — and this project does not gain a second cliff shader to force-compile, which on a
        /// CI runner with no graphics device is a magenta face nobody would catch.</para>
        ///
        /// <para><b>⚠ The px look draws the same decal by a FLAG instead.</b> There a face's
        /// <c>_Unlit</c> slot holds the px kit's <c>_index</c> map (palette rows and bands, not colours),
        /// and a shader cannot tell that from a strip's pre-lit colours by looking at them. So a decal
        /// renderer carries <c>_PxDecal</c> = 1 and the px branch draws its strip exactly as the rig
        /// baked it. Said by the caller, not guessed from a missing normal, because a face band whose
        /// normal and mask failed to load would otherwise be drawn as raw index values. The v10 branch
        /// never reads the flag.</para>
        /// </summary>
        private void PushMaterial(MeshRenderer renderer, Texture2D unlit, Texture2D normal, Texture2D mask,
                                  bool decal)
        {
            if (renderer == null) return;
            _mpb ??= new MaterialPropertyBlock();
            // ⚠ CLEARED, not read back. One block now serves several renderers per chunk, and a block
            // reused without clearing would hand the toe decal the rock band's normal and mask — which
            // renders, and renders wrong.
            _mpb.Clear();

            if (unlit != null) _mpb.SetTexture(IdUnlit, unlit);
            if (normal != null) _mpb.SetTexture(IdNormal, normal);
            if (mask != null) _mpb.SetTexture(IdMask, mask);
            _mpb.SetFloat(IdPxDecal, decal ? 1f : 0f);
            _mpb.SetFloat(IdWallAzimuth, _wallAzimuth);
            _mpb.SetFloat(IdBatter, _batter);
            _mpb.SetVector(IdBakeL, new Vector4(_bakeLight.x, _bakeLight.y, _bakeLight.z, 0f));
            // Declared by the shader and never read in frag — pushed anyway so anyone inspecting the
            // renderer sees the metre scale the UVs were actually built at, rather than the default.
            _mpb.SetFloat(IdTileMetresS, _faceMetresS);
            _mpb.SetFloat(IdTileMetresT, _faceMetresT);

            renderer.SetPropertyBlock(_mpb);
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
