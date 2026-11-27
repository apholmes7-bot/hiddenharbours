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
        /// <summary>Per whole column: how much of the face is drawn, 0..1 of its surface from the brow. Null draws it
        /// all. Below 1 only near an open run end, where the face tapers into the ground
        /// (<see cref="CliffWallGeometry.RunEndKeepAt"/>).</summary>
        public float[] Keep;
        /// <summary>Per whole column: the vertex alpha. Null writes no colours. Set only on a seam feather.</summary>
        public float[] Alpha;
        /// <summary>Per whole column: how much of the face shows past the clifftops in front of it, 0..1 of its surface
        /// from the brow (<see cref="CliffWallGeometry.OcclusionKeepAt"/>). Null shows it all. Cut, never faded: the
        /// clifftop in front is a hard edge.</summary>
        public float[] Clip;
    }

    /// <summary>One generated mesh's arrays, before they are a <see cref="Mesh"/>, so a test can read the exact floats.</summary>
    public sealed class CliffWallMeshData
    {
        public Vector3[] Verts;
        public Vector2[] Uvs;
        public Vector2[] ElevationUv;
        public Vector2[] SeaPlanUv;
        public int[] Tris;
        /// <summary>Vertex colours (white, alpha = the column's <see cref="CliffWallColumns.Alpha"/>), or null.</summary>
        public Color[] Colors;
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
    /// <para><b>A look, and the one wall the look implies.</b> Drives no sim and saves nothing (rule 5). Where a
    /// player may stand is still the height field's to say, with one exception the picture forces: <b>a cliff face
    /// drawn over the sea is a wall</b> (ruling t10, 2026-11-08). The face is drawn down-screen of its plan toe by
    /// its drop (<see cref="CliffWallGeometry.ToeScreen"/>), so at St Peters' West Ledges it covers 5.9 m of sea
    /// inside the walker's swim band, and she swam in and vanished behind it. So each chunk carries a BLOCKER
    /// along its DRAWN toe (<see cref="BlockerChildName"/>): the quay wall's collider kind and layer (one
    /// <see cref="PolygonCollider2D"/> quad per segment, <see cref="CliffWallGeometry.BlockerThicknessMetres"/>
    /// thick, each run half a thickness past both ends, on Default, as <c>PropertyBoundary</c> builds them), built
    /// from the stations alone, so it stands whether or not the face's channels are baked, and where the drawn toe
    /// lies over dry ground too. It is derived on every build like the meshes and never saved. The waterline is
    /// colour on rock and changes nothing about any of this.</para>
    ///
    /// <para><b>The face is a wall from above too (St Peters cliffs walk fix).</b> The toe blocker held the sea side
    /// only: from every clifftop he walked over the brow and down the plunge to it, hidden under the face, and where
    /// a run ended he came up the face's open side and over the brow. So each chunk also fills its whole drawn face,
    /// brow to drawn toe (<see cref="FaceBlockerChildName"/>, <see cref="CliffWallGeometry.FacePieces"/>): he stops at
    /// the brow from above and at the foot from below. Same layer, same derivation, never saved.</para>
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
        private static readonly int IdBaseTexelScale = Shader.PropertyToID("_BaseTexelScale");
        private static readonly int IdFaultA = Shader.PropertyToID("_FaultA");
        private static readonly int IdFaultB = Shader.PropertyToID("_FaultB");
        private static readonly int IdPaletteVariant =Shader.PropertyToID("_PaletteVariant");
        private static readonly int IdVariantOn = Shader.PropertyToID("_VariantOn");
        private static readonly int IdVariantBlock = Shader.PropertyToID("_VariantBlock");
        private static readonly int IdVariantFaceOn = Shader.PropertyToID("_VariantFaceOn");
        private static readonly int IdUnlitVariant = Shader.PropertyToID("_UnlitVariant");
        private static readonly int IdNormalVariant = Shader.PropertyToID("_NormalVariant");
        private static readonly int IdMaskVariant = Shader.PropertyToID("_MaskVariant");

        /// <summary>The region's cliff mix for this build, or null (see <see cref="CliffFaceMixDef"/>).</summary>
        private CliffFaceMixDef _mix;

        /// <summary>The mix this wall was last built with, or null: for a test or a plate.</summary>
        public CliffFaceMixDef FaceMix => _mix;

        /// <summary>The name of the generated child carrying a band's quads, so a hierarchy reads as
        /// geology and a test can find one without a magic string of its own.</summary>
        public const string BandChildPrefix = "CliffWallQuads_";
        public const string BrowChildName = "CliffWallBrowDecal";
        public const string ToeChildName = "CliffWallToeDecal";
        /// <summary>The generated child carrying the turf lip (<see cref="CliffTurfLipDef"/>), where the region's mix
        /// names one: it then stands in for the brow strip.</summary>
        public const string TurfLipChildName = "CliffWallTurfLip";

        /// <summary>The generated child carrying the drawn toe's blocker (see the class note).</summary>
        public const string BlockerChildName = "CliffWallToeBlocker";

        /// <summary>The generated child carrying the face's own blocker: the whole drawn face, brow to drawn toe, as
        /// solid ground nobody stands on (see <see cref="BuildFaceBlocker"/>).</summary>
        public const string FaceBlockerChildName = "CliffWallFaceBlocker";

        /// <summary>The blocker's layer: Default, the layer the quay wall's colliders stand on and the walker's
        /// foot (and every hull) collides with.</summary>
        public const int BlockerLayer = 0;

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

        /// <summary>How many blocker quads this chunk built: one per drawn-toe segment, 0 before it has built.
        /// The collider count the cost line reads.</summary>
        public int BlockerColliderCount { get; private set; }

        /// <summary>How many colliders the face blocker built: one per segment (two where a segment's face folds over
        /// itself), 0 before it has built.</summary>
        public int FaceBlockerColliderCount { get; private set; }

        /// <summary>The chunk's toe in PLAN, one per station, world XY: the Def's line as the builder pushed it.</summary>
        public IReadOnlyList<Vector2> ToePlan => _toePlan;

        /// <summary>The ground height at each station's toe, metres (empty for a scene saved before the waterline).</summary>
        public IReadOnlyList<float> ToeElevations => _toeElevations;

        /// <summary>The chunk's DRAWN toe, one point per station, world XY: the line its blocker stands on.</summary>
        public Vector2[] DrawnToe()
        {
            _jag = CliffJag.From(CliffFaceMixDef.ForScene(gameObject.scene.name));
            return CollisionToe(BuildSamples(_browPlan, _toePlan, _dropMetres, _toeElevations));
        }

        /// <summary>The region's rough-edges jag for this build (see <see cref="CliffJagMath"/>); off by default.</summary>
        private CliffJag _jag;
        private bool _planarFace;

        /// <summary>True when this chunk's face was laid on the screen art grid (it faces across the screen).</summary>
        public bool PlanarFace => _planarFace;

        /// <summary>
        /// The line both blockers stand on. With the jag off it is the stations' drawn toe, exactly as before. With it
        /// on it is the foot the face is DRAWN at — the drawn toe pushed out by the same displacement the mesh reads at
        /// t = 1 (the kit profile plus the jag) — closed over the narrowest bite a walker may enter, so the picture,
        /// the collision and the wash (which rides the moved vertices) agree, and no notch can wedge him.
        /// </summary>
        /// <summary>The rib warp as this chunk reads it now (<see cref="CliffRibWarpDef"/>). The slide steps in whole
        /// profile texels: a rib moves by art pixels, never by a fraction of one.</summary>
        private CliffRibWarp RibWarpNow()
        {
            CliffRibWarp warp = CliffRibWarpDef.CurrentDrawn();
            if (_profile != null && _profile.width > 0) warp.StepMetres = _faceMetresS / _profile.width;
            return warp;
        }

        private Vector2[] CollisionToe(CliffWallSample[] samples)
        {
            Vector2[] toe = CliffWallGeometry.DrawnToe(samples);
            if (!_jag.On || samples.Length < 2) return toe;
            CliffRibWarp warp = RibWarpNow();
            // Along the run the way the face mesh counts it: the texture brow where one was moved out, else the brow.
            Vector2[] alongBrow = _textureBrowPlan != null && _textureBrowPlan.Length == samples.Length
                ? _textureBrowPlan : _browPlan;
            var along = new float[samples.Length];
            var outward = new float[samples.Length];
            float arc = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                if (i > 0) arc += Vector2.Distance(alongBrow[i - 1], alongBrow[i]);
                along[i] = arc;
                // The drawn foot's own read: slid and scaled by the rib warp exactly as FaceMesh lays it, so the
                // blocker stands on the foot that is drawn.
                float metres = _alongOffsetMetres + arc;
                if (!warp.IsOff) metres += CliffRibWarpMath.ShiftMetres(in warp, alongBrow[i]);
                Vector2 push = CliffWallGeometry.OutwardPlan(in samples[i]);
                float facing = CliffJagMath.FacingGain(in _jag, push, CliffWallGeometry.DrawnDown(
                    samples[i].BrowPlan, CliffWallGeometry.ToeScreen(in samples[i])));
                // (The jag is read at the tile coordinate turned back into metres, the very number the mesh reads.)
                float uRelief = CliffWallGeometry.TileU(metres, _faceMetresS);
                outward[i] = CliffJagMath.Outward(in _jag, ProfileAt(uRelief, 1f),
                                                  CliffRibWarpMath.DepthGain(in warp, alongBrow[i]), facing,
                                                  uRelief * _faceMetresS, 1f);
            }
            float[] closed = CliffJagMath.Close(along, outward, _jag.MinBiteMetres);
            for (int i = 0; i < samples.Length; i++)
                toe[i] += CliffWallGeometry.OutwardPlan(in samples[i]) * closed[i];
            return toe;
        }

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

        /// <summary>The rib warp this build lays its face with (<see cref="CliffRibWarpDef"/>); set at the top of each
        /// build. The drawn toe's blocker reads the same warp (<see cref="RibWarpNow"/>), so it stands on the foot drawn.</summary>
        private CliffRibWarp _ribWarp;

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
                if (child.name.StartsWith(BandChildPrefix) || child.name.StartsWith(JoinChildPrefix) ||
                    child.name == BrowChildName || child.name == ToeChildName || child.name == TurfLipChildName || child.name == BlockerChildName ||
                    child.name == FaceBlockerChildName)
                    DestroyGo(child.gameObject);
            }

            RendererCount = 0;
            BlockerColliderCount = 0;
            FaceBlockerColliderCount = 0;
        }

        private void Rebuild()
        {
            Teardown();
            int stations = StationCount;
            if (stations < 2 || _toePlan.Length < stations || _dropMetres.Length < stations) return;

            // The wall the face implies, from the stations alone: before the channel and material checks, so a
            // checkout that has never baked the kit's PNGs walks the same coast as one that has.
            _jag = CliffJag.From(CliffFaceMixDef.ForScene(gameObject.scene.name));
            Vector2[] drawnToe = CollisionToe(BuildSamples(_browPlan, _toePlan, _dropMetres, _toeElevations));
            BuildBlocker(drawnToe);
            BuildFaceBlocker(_browPlan, drawnToe);

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
            // One mapping for the whole chunk, its bands and its join pieces alike (never a blend).
            _planarFace = CliffJagMath.FacesAcrossTheScreen(in _jag, samples);

            // The region's cliff mix (owner 11 25: our cliffs and the boards' for variance), found by the
            // scene's name: presentation only, it touches nothing built above.
            CliffFaceMixDef mix = CliffFaceMixDef.ForScene(gameObject.scene.name);
            _mix = mix != null && mix.IsActive ? mix : null;

            // The rib warp every band and decal of this chunk is laid with (CliffRibWarpDef): read once per build.
            _ribWarp = RibWarpNow();

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

            // ⭐ THE RUN. A chunk reads its siblings' stations to learn whether its run ends nearby (the face then
            // tapers into the ground instead of stopping in a square cut), whether a slit follows it (the face is
            // drawn on across it), and whether the chunk beside it wears other rock (the front one feathers its rock
            // over the seam). Only the picture changes: both blockers above stand on the full stations.
            List<CliffWallSurface> run = RunOf(out int me, out List<Vector2[]> brows, out List<float[]> drops);
            CliffWallGeometry.RunReach reach = default;
            if (run != null)
            {
                reach = CliffWallGeometry.RunReachOf(brows, drops, me);
                columns.Keep = KeepFor(columns, reach);
            }

            // Where a clifftop nearer the camera stands over part of the face, that part is not drawn: a face pushed
            // across its own clifftop, or across the next one, would paint rock over the grass in front.
            List<CliffWallGeometry.BrowEdge> occluders;
            if (run == null)
            {
                occluders = new List<CliffWallGeometry.BrowEdge>();
                CliffWallGeometry.AddBrowEdges(occluders, samples, HasToeElevations);
                columns.Clip = ClipFor(columns, occluders, HasToeElevations, false, default, false, default);
            }
            else
            {
                var runSamples = new List<CliffWallSample[]>(run.Count);
                var known = new List<bool>(run.Count);
                foreach (CliffWallSurface s in run)
                {
                    runSamples.Add(s == this ? samples : BuildSamples(s._browPlan, s._toePlan, s._dropMetres, s._toeElevations));
                    known.Add(s.HasToeElevations);
                }
                columns.Clip = RunClipFor(columns, runSamples, known, me, reach, out occluders);
            }

            for (int b = 0; b < _bands.Length; b++)
            {
                if (!_bands[b].HasChannels) continue;
                string label = string.IsNullOrEmpty(_bands[b].Label) ? b.ToString() : _bands[b].Label;
                BuildBand(columns, _bands[b], rows, BandChildPrefix + label, localOrder: 0);
            }

            // The decals ride ONE row band above the face so they composite over the rock they finish.
            // Where the region's mix names a turf lip, that generated strip is the clifftop's edge and the kit's
            // brow strip is not drawn (owner 11 26: the brow strip stretched its grass and mixed texel sizes).
            CliffTurfLipDef lip = _mix != null ? _mix.TurfLip : null;
            if (lip != null && lip.IsDrawable) BuildTurfLip(columns, lip);
            else if (_browDecal != null) BuildDecal(columns, _browDecal, brow: true, BrowChildName);
            if (_toeDecal != null) BuildDecal(columns, _toeDecal, brow: false, ToeChildName);

            if (run != null) BuildJoins(run, reach, samples, columns, rows, occluders);
        }

        /// <summary>
        /// The brow edges that may stand in front of this chunk's face: its own, and those of every chunk of its run
        /// whose brow comes within reach of the face (with room for a bridge or feather drawn past its ends).
        /// </summary>
        public static List<CliffWallGeometry.BrowEdge> OccludersFor(IReadOnlyList<CliffWallSample[]> run,
                                                                     IReadOnlyList<bool> known, int me)
        {
            var edges = new List<CliffWallGeometry.BrowEdge>();
            CliffWallSample[] mine = run[me];
            float margin = CliffWallGeometry.RunSeamFeatherMetres + CliffWallGeometry.RunBridgeMetres;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            foreach (CliffWallSample s in mine)
            {
                Vector2 toe = CliffWallGeometry.ToeScreen(in s);
                min = Vector2.Min(min, Vector2.Min(s.BrowPlan, toe));
                max = Vector2.Max(max, Vector2.Max(s.BrowPlan, toe));
            }
            min -= Vector2.one * margin;
            max += Vector2.one * margin;
            for (int k = 0; k < run.Count; k++)
            {
                CliffWallSample[] b = run[k];
                if (b == null || b.Length < 2) continue;
                Vector2 bmin = new Vector2(float.MaxValue, float.MaxValue), bmax = -bmin;
                for (int i = 0; i < b.Length; i++)
                {
                    bmin = Vector2.Min(bmin, b[i].BrowPlan);
                    bmax = Vector2.Max(bmax, b[i].BrowPlan);
                }
                if (bmax.x < min.x || bmin.x > max.x || bmax.y < min.y || bmin.y > max.y) continue;
                CliffWallGeometry.AddBrowEdges(edges, b, known[k]);
            }
            return edges;
        }

        /// <summary>
        /// ⭐ <b>What of a run chunk's face shows</b>, per whole column (null where all of it does): cut where a clifftop
        /// in front stands over it (<see cref="ClipFor"/>, against <see cref="OccludersFor"/> with the joined chunks' edges
        /// past its ends), and none at all on a step (<see cref="CliffWallGeometry.IsStep"/>). Public so a test reads the
        /// same cut a scene's chunks draw.
        /// </summary>
        public static float[] RunClipFor(CliffWallColumns columns, IReadOnlyList<CliffWallSample[]> run,
                                         IReadOnlyList<bool> known, int me, CliffWallGeometry.RunReach reach,
                                         out List<CliffWallGeometry.BrowEdge> occluders)
        {
            occluders = OccludersFor(run, known, me);
            if (CliffWallGeometry.IsStep(run[me])) return new float[columns.Whole.Length];
            bool hasBefore = false, hasAfter = false;
            CliffWallGeometry.BrowEdge before = default, after = default;
            if (!columns.Sliced)
            {
                if (reach.JoinedBefore >= 0)
                {
                    CliffWallSample[] ps = run[reach.JoinedBefore];
                    hasBefore = ps.Length >= 2 &&
                                CliffWallGeometry.TryBrowEdge(in ps[ps.Length - 2], in ps[ps.Length - 1],
                                                              known[reach.JoinedBefore], out before);
                }
                if (reach.JoinedAfter >= 0)
                {
                    CliffWallSample[] qs = run[reach.JoinedAfter];
                    hasAfter = qs.Length >= 2 &&
                               CliffWallGeometry.TryBrowEdge(in qs[0], in qs[1], known[reach.JoinedAfter], out after);
                }
            }
            return ClipFor(columns, occluders, known[me], hasBefore, in before, hasAfter, in after);
        }

        /// <summary>
        /// Per whole column, how much of the face shows past the clifftops in front of it
        /// (<see cref="CliffWallGeometry.OcclusionKeepAt"/>), or null where every column shows whole. A column's own
        /// brow edges are the segments to the columns either side; past the first and last, the joined chunks' edges
        /// when given.
        /// </summary>
        public static float[] ClipFor(CliffWallColumns columns, IReadOnlyList<CliffWallGeometry.BrowEdge> occluders,
                                      bool elevationsKnown, bool hasBefore, in CliffWallGeometry.BrowEdge before,
                                      bool hasAfter, in CliffWallGeometry.BrowEdge after)
        {
            CliffWallSample[] w = columns.Whole;
            float[] clip = null;
            for (int c = 0; c < w.Length; c++)
            {
                CliffWallGeometry.BrowEdge eb = before, ea = after;
                bool hb = c > 0
                    ? CliffWallGeometry.TryBrowEdge(in w[c - 1], in w[c], elevationsKnown, out eb)
                    : hasBefore;
                bool ha = c + 1 < w.Length
                    ? CliffWallGeometry.TryBrowEdge(in w[c], in w[c + 1], elevationsKnown, out ea)
                    : hasAfter;
                float k = CliffWallGeometry.OcclusionKeepAt(
                    w[c].BrowPlan, CliffWallGeometry.ToeScreen(in w[c]),
                    elevationsKnown ? w[c].ToeElevation + w[c].DropMetres : float.NaN, w[c].DropMetres,
                    hb, in eb, ha, in ea, occluders);
                if (k >= 1f) continue;
                if (clip == null)
                {
                    clip = new float[w.Length];
                    for (int i = 0; i < clip.Length; i++) clip[i] = 1f;
                }
                clip[c] = k;
            }
            if (clip == null || w.Length < 2) return clip;
            // A lone cut column between two whole ones is a brow wiggle (a hook, a jog), not a clifftop in front: cutting
            // it would open a slot down the face to the sea. Only a cut that spans at least two columns is kept.
            var kept = (float[])clip.Clone();
            bool any = false;
            for (int c = 0; c < w.Length; c++)
            {
                if (clip[c] >= 1f) continue;
                bool leftWhole = c == 0 || clip[c - 1] >= 1f;
                bool rightWhole = c + 1 >= w.Length || clip[c + 1] >= 1f;
                if (leftWhole && rightWhole) kept[c] = 1f;
                else any = true;
            }
            return any ? kept : null;
        }

        // ── The run: tapered ends, bridged slits, feathered seams ───────────────────────────────────

        /// <summary>The generated children that draw past this chunk's own stations: a bridge over a slit, or a
        /// feather over the next chunk's face where the rock swaps. Prefixed so a rebuild sweeps them.</summary>
        public const string JoinChildPrefix = "CliffWallJoin_";

        /// <summary>
        /// This chunk's run: every sibling chunk under the same parent, with their brow lines and drops, and this
        /// chunk's index among them. Null for a chunk with no siblings (a test's lone chunk), which draws exactly as
        /// before: with nothing beside it, nothing says where its run ends.
        /// </summary>
        private List<CliffWallSurface> RunOf(out int me, out List<Vector2[]> brows, out List<float[]> drops)
        {
            me = -1;
            brows = null;
            drops = null;
            Transform parent = transform.parent;
            if (parent == null) return null;
            var run = new List<CliffWallSurface>(parent.childCount);
            for (int i = 0; i < parent.childCount; i++)
            {
                CliffWallSurface s = parent.GetChild(i).GetComponent<CliffWallSurface>();
                if (s == null || s.StationCount < 2) continue;
                if (s == this) me = run.Count;
                run.Add(s);
            }
            if (me < 0 || run.Count < 2) return null;
            brows = new List<Vector2[]>(run.Count);
            drops = new List<float[]>(run.Count);
            foreach (CliffWallSurface s in run)
            {
                brows.Add(s._browPlan);
                drops.Add(s._dropMetres);
            }
            return run;
        }

        /// <summary>How much of the face each whole column draws, from the run ends' distances. Null where no open end
        /// is near, so the mesh is laid exactly as before.</summary>
        public static float[] KeepFor(CliffWallColumns columns, CliffWallGeometry.RunReach reach)
        {
            if (float.IsPositiveInfinity(reach.StartDistance) && float.IsPositiveInfinity(reach.EndDistance)) return null;
            float[] arc = ArcOf(columns.Whole, null);
            float first = StationArc(columns, arc, 0);
            float last = StationArc(columns, arc, columns.Gap.Length - 1);
            float startTaper = reach.StartTaperMetres, endTaper = reach.EndTaperMetres;
            if (!float.IsPositiveInfinity(reach.StartDistance) && !float.IsPositiveInfinity(reach.EndDistance))
            {
                // A short run with both ends near: each taper takes at most its share, so the middle of the run
                // still stands at full height instead of the whole cliff fading away.
                float runMetres = reach.StartDistance + (last - first) + reach.EndDistance;
                float cap = runMetres * CliffWallGeometry.RunEndTaperMaxShareOfRun;
                startTaper = Mathf.Min(startTaper, cap);
                endTaper = Mathf.Min(endTaper, cap);
            }
            var keep = new float[arc.Length];
            for (int c = 0; c < arc.Length; c++)
            {
                float fromStart = reach.StartDistance + Mathf.Max(0f, arc[c] - first);
                float fromEnd = reach.EndDistance + Mathf.Max(0f, last - arc[c]);
                keep[c] = Mathf.Min(CliffWallGeometry.RunEndKeepAt(fromStart, startTaper),
                                    CliffWallGeometry.RunEndKeepAt(fromEnd, endTaper));
            }
            return keep;
        }

        /// <summary>Cumulative distance (m) along whole columns: along the brows, or along <paramref name="along"/>.</summary>
        private static float[] ArcOf(CliffWallSample[] whole, Vector2[] along)
        {
            var arc = new float[whole.Length];
            for (int c = 1; c < whole.Length; c++)
                arc[c] = arc[c - 1] + (along != null
                    ? Vector2.Distance(along[c - 1], along[c])
                    : Vector2.Distance(whole[c - 1].BrowPlan, whole[c].BrowPlan));
            return arc;
        }

        private static float StationArc(CliffWallColumns columns, float[] arc, int station)
        {
            int g = columns.Gap[station];
            float f = columns.At[station];
            return f > 0f && g + 1 < arc.Length ? Mathf.LerpUnclamped(arc[g], arc[g + 1], f) : arc[g];
        }

        private void BuildJoins(List<CliffWallSurface> run, CliffWallGeometry.RunReach reach,
                                CliffWallSample[] samples, CliffWallColumns columns, int rows,
                                List<CliffWallGeometry.BrowEdge> occluders)
        {
            float alongArcLast = StationArc(columns, ArcOf(columns.Whole, columns.AlongBrow), columns.Gap.Length - 1);
            CliffWallSample last = samples[samples.Length - 1];

            // A slit: draw the face on across it, as this chunk's rock, to the next run's first station.
            if (reach.BridgeTo >= 0)
            {
                CliffWallSurface next = run[reach.BridgeTo];
                CliffWallSample[] ns = BuildSamples(next._browPlan, next._toePlan, next._dropMetres, next._toeElevations);
                if (ns.Length > 0)
                    BuildExtension(Unsliced(new[] { last, ns[0] }), _alongOffsetMetres + alongArcLast, rows, "Bridge", occluders);
            }

            // A rock swap at a join: the chunk drawn in front carries its rock over the other's face, fading out.
            // Not inside a taper: the neighbour's face is shortened there and a full-height feather would show.
            bool tapering = !float.IsPositiveInfinity(reach.StartDistance) || !float.IsPositiveInfinity(reach.EndDistance);
            if (tapering) return;
            if (reach.JoinedAfter >= 0)
            {
                CliffWallSurface next = run[reach.JoinedAfter];
                CliffWallSample[] ns = BuildSamples(next._browPlan, next._toePlan, next._dropMetres, next._toeElevations);
                if ((WearsOtherRock(next) || MapsOtherwise(ns)) && DrawsInFrontOf(ns, ahead: true))
                {
                    CliffWallColumns feather = FeatherColumns(ns, fromFirst: true, out _);
                    if (feather != null) BuildExtension(feather, _alongOffsetMetres + alongArcLast, rows, "FeatherAfter", occluders);
                }
            }
            if (reach.JoinedBefore >= 0)
            {
                CliffWallSurface prev = run[reach.JoinedBefore];
                CliffWallSample[] ps = BuildSamples(prev._browPlan, prev._toePlan, prev._dropMetres, prev._toeElevations);
                if ((WearsOtherRock(prev) || MapsOtherwise(ps)) && DrawsInFrontOf(ps, ahead: false))
                {
                    CliffWallColumns feather = FeatherColumns(ps, fromFirst: false, out float length);
                    if (feather != null) BuildExtension(feather, _alongOffsetMetres - length, rows, "FeatherBefore", occluders);
                }
            }
        }

        /// <summary>Whether the neighbour draws with other rock textures or another landform profile.</summary>
        private bool WearsOtherRock(CliffWallSurface other)
        {
            if (other._profile != _profile || other._bands.Length != _bands.Length) return true;
            for (int i = 0; i < _bands.Length; i++)
                if (other._bands[i].Unlit != _bands[i].Unlit || other._bands[i].Normal != _bands[i].Normal ||
                    other._bands[i].Mask != _bands[i].Mask)
                    return true;
            return false;
        }

        /// <summary>Whether a neighbour with these stations lays its face on the other mapping (screen grid against
        /// down the column, <see cref="CliffJagMath.FacesAcrossTheScreen"/>). The rock does not line up across such a
        /// join, so it is finished like a rock swap: the chunk in front carries its own over, dithered out.</summary>
        private bool MapsOtherwise(CliffWallSample[] other) =>
            CliffJagMath.FacesAcrossTheScreen(in _jag, other) != _planarFace;

        /// <summary>Whether this chunk sorts in front of a neighbour with these stations. On a tie the earlier chunk
        /// of the pair (the one the seam is ahead of) carries the feather, so exactly one side does.</summary>
        private bool DrawsInFrontOf(CliffWallSample[] other, bool ahead)
        {
            int theirs = YSortSprite.OrderFor(CliffWallGeometry.SortY(other), SortingBands.DecorBase,
                                              SortingBands.OrdersPerMetre, SortingBands.DecorFloor,
                                              SortingBands.DecorCeiling);
            return SortingOrder > theirs || (SortingOrder == theirs && ahead);
        }

        /// <summary>
        /// The neighbour's stations within <see cref="CliffWallGeometry.RunSeamFeatherMetres"/> of the join, as
        /// unsliced columns in run order, with alpha 1 at the join falling to 0 at the far end. From the neighbour's
        /// first station when it follows this chunk, else up to its last. Null where it has no length.
        /// </summary>
        public static CliffWallColumns FeatherColumns(CliffWallSample[] neighbour, bool fromFirst, out float length)
        {
            length = 0f;
            int n = neighbour == null ? 0 : neighbour.Length;
            if (n < 2) return null;
            var picked = new List<CliffWallSample>();
            var dist = new List<float>();
            float reachMetres = CliffWallGeometry.RunSeamFeatherMetres;
            float arc = 0f;
            for (int k = 0; k < n; k++)
            {
                int i = fromFirst ? k : n - 1 - k;
                if (k > 0)
                {
                    int j = fromFirst ? i - 1 : i + 1;
                    float step = Vector2.Distance(neighbour[j].BrowPlan, neighbour[i].BrowPlan);
                    if (arc + step >= reachMetres)
                    {
                        float f = step > 1e-6f ? (reachMetres - arc) / step : 1f;
                        picked.Add(LerpSample(neighbour[j], neighbour[i], f));
                        dist.Add(reachMetres);
                        arc = reachMetres;
                        break;
                    }
                    arc += step;
                }
                picked.Add(neighbour[i]);
                dist.Add(arc);
            }
            length = arc;
            if (!(length > 1e-4f)) return null;
            if (!fromFirst)
            {
                picked.Reverse();
                dist.Reverse();
            }
            CliffWallColumns columns = Unsliced(picked.ToArray());
            columns.Alpha = new float[picked.Count];
            for (int c = 0; c < picked.Count; c++)
            {
                float x = Mathf.Clamp01(dist[c] / length);
                columns.Alpha[c] = 1f - x * x * (3f - 2f * x);
            }
            return columns;
        }

        private static CliffWallSample LerpSample(in CliffWallSample a, in CliffWallSample b, float f) =>
            new CliffWallSample(Vector2.LerpUnclamped(a.BrowPlan, b.BrowPlan, f),
                                Vector2.LerpUnclamped(a.ToePlan, b.ToePlan, f),
                                Mathf.LerpUnclamped(a.DropMetres, b.DropMetres, f),
                                Mathf.LerpUnclamped(a.ToeElevation, b.ToeElevation, f));

        /// <summary>Draw this chunk's bands and decals on columns past its own stations (a bridge or a feather).</summary>
        private void BuildExtension(CliffWallColumns ext, float alongOffsetMetres, int rows, string what,
                                    List<CliffWallGeometry.BrowEdge> occluders)
        {
            float elevationValid = HasToeElevations ? 1f : 0f;
            if (occluders != null)
                ext.Clip = ClipFor(ext, occluders, HasToeElevations, false, default, false, default);
            for (int b = 0; b < _bands.Length; b++)
            {
                if (!_bands[b].HasChannels) continue;
                string label = string.IsNullOrEmpty(_bands[b].Label) ? b.ToString() : _bands[b].Label;
                CliffWallMeshData m = FaceMesh(ext, _bands[b].StartSurfaceMetres, _bands[b].EndSurfaceMetres, rows,
                                               alongOffsetMetres, _faceMetresS, _faceMetresT, ProfileAt,
                                               elevationValid, transform.position, _ribWarp, _jag, _planarFace);
                Emit(JoinChildPrefix + what + "_" + label, m.Verts, m.Uvs, m.Tris, _bands[b].Unlit, _bands[b].Normal,
                     _bands[b].Mask, localOrder: 0, m.ElevationUv, m.SeaPlanUv, decal: false, m.Colors);
            }
            for (int d = 0; d < 2; d++)
            {
                Texture2D strip = d == 0 ? _browDecal : _toeDecal;
                if (strip == null) continue;
                CliffWallMeshData m = DecalMesh(ext, d == 0, _stripMetresT, _browLineAt, _subdivideMetres,
                                                alongOffsetMetres, _faceMetresS, ProfileAt, elevationValid,
                                                transform.position, _ribWarp, _jag);
                Emit(JoinChildPrefix + what + (d == 0 ? "_Brow" : "_Toe"), m.Verts, m.Uvs, m.Tris, strip, null, null,
                     localOrder: 1, m.ElevationUv, m.SeaPlanUv, decal: true, m.Colors);
            }
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
        ///
        /// <para>The rough-edges jag is NOT in here: the mesh builders and the blocker add it themselves
        /// (<see cref="CliffJagMath.Outward"/>), read along the RUN so it never repeats with the tile, and held back
        /// on a wall that faces across the screen.</para>
        /// </summary>
        private float ProfileAt(float uTiles, float t)
        {
            if (_profile == null) return 0f;
            float grey = _profile.GetPixelBilinear(CliffWallGeometry.WrapTile01(uTiles), 1f - t).r;
            return CliffWallGeometry.ProfileMetresFromGrey(grey, _profileMetres);
        }

        /// <summary>
        /// Stand the blocker on the drawn toe: one solid <see cref="PolygonCollider2D"/> quad per segment, centred
        /// on the line, <see cref="CliffWallGeometry.BlockerThicknessMetres"/> thick and run half a thickness past
        /// both ends, on a DontSave child at <see cref="BlockerLayer"/>. The quad is <c>PropertyBoundary</c>'s, for
        /// its reasons: one collider per segment, because several paths on one collider turn their overlaps (every
        /// corner) into holes; the ends overlapped, because butt-ended quads leave a pinhole on the outside of each
        /// turn. A chunk's last point is the next chunk's first, so the run's blocker is closed across the join.
        /// </summary>
        private void BuildBlocker(Vector2[] drawnToe)
        {
            if (drawnToe == null || drawnToe.Length < 2) return;

            var go = new GameObject(BlockerChildName) { hideFlags = HideFlags.DontSave, layer = BlockerLayer };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            float half = CliffWallGeometry.BlockerThicknessMetres * 0.5f;
            int built = 0;
            for (int s = 0; s + 1 < drawnToe.Length; s++)
            {
                Vector2 a = go.transform.InverseTransformPoint(drawnToe[s]);
                Vector2 b = go.transform.InverseTransformPoint(drawnToe[s + 1]);
                Vector2 along = b - a;
                float len = along.magnitude;
                if (len <= 1e-4f) continue;                       // a repeated station is not a wall
                along /= len;
                Vector2 side = new Vector2(-along.y, along.x) * half;
                Vector2 ext = along * half;

                var quad = go.AddComponent<PolygonCollider2D>();
                quad.pathCount = 1;
                quad.SetPath(0, new[] { a - ext + side, b + ext + side, b + ext - side, a - ext - side });
                built++;
            }

            _generated.Add(go);
            BlockerColliderCount = built;
        }

        /// <summary>
        /// Fill the face itself: for each segment, the ground from its brow down to its drawn toe
        /// (<see cref="CliffWallGeometry.FacePieces"/>), one solid <see cref="PolygonCollider2D"/> per piece on a DontSave
        /// child at <see cref="BlockerLayer"/>. One path per collider for the toe blocker's reason (several paths on one
        /// collider turn their overlaps into holes). The brow edge stops the walker at the clifftop, the drawn toe edge
        /// stops him from the sea, and the end edges close the run's open sides, so the face he would vanish behind can
        /// be entered from nowhere. Built from the stations alone, like the toe blocker, so it stands with or without
        /// the baked face. A joined chunk shares its join station, so the fill is continuous along a run.
        /// </summary>
        private void BuildFaceBlocker(Vector2[] brow, Vector2[] drawnToe)
        {
            if (brow == null || drawnToe == null) return;
            int n = Mathf.Min(brow.Length, drawnToe.Length);
            if (n < 2) return;

            var go = new GameObject(FaceBlockerChildName) { hideFlags = HideFlags.DontSave, layer = BlockerLayer };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            int built = 0;
            for (int s = 0; s + 1 < n; s++)
            {
                Vector2 b0 = go.transform.InverseTransformPoint(brow[s]);
                Vector2 b1 = go.transform.InverseTransformPoint(brow[s + 1]);
                Vector2 t0 = go.transform.InverseTransformPoint(drawnToe[s]);
                Vector2 t1 = go.transform.InverseTransformPoint(drawnToe[s + 1]);
                foreach (Vector2[] piece in CliffWallGeometry.FacePieces(b0, b1, t1, t0))
                {
                    var poly = go.AddComponent<PolygonCollider2D>();
                    poly.pathCount = 1;
                    poly.SetPath(0, piece);
                    built++;
                }
            }

            _generated.Add(go);
            FaceBlockerColliderCount = built;
        }

        private void BuildBand(CliffWallColumns columns, in CliffFaceBand band, int rows,
                               string childName, int localOrder)
        {
            float elevationValid = HasToeElevations ? 1f : 0f;
            CliffWallMeshData m = FaceMesh(columns, band.StartSurfaceMetres, band.EndSurfaceMetres, rows,
                                           _alongOffsetMetres, _faceMetresS, _faceMetresT, ProfileAt,
                                           elevationValid, transform.position, _ribWarp, _jag, _planarFace);
            Emit(childName, m.Verts, m.Uvs, m.Tris, band.Unlit, band.Normal, band.Mask, localOrder,
                 m.ElevationUv, m.SeaPlanUv, decal: false, m.Colors);
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
                                            _alongOffsetMetres, _faceMetresS, ProfileAt, elevationValid,
                                            transform.position, _ribWarp, _jag);
            // ⭐ localOrder 1: INSIDE the sorting group, so a decal composites over the rock it finishes
            // without spending an order of the region's decor band.
            Emit(childName, m.Verts, m.Uvs, m.Tris, strip, null, null, localOrder: 1,
                 m.ElevationUv, m.SeaPlanUv, decal: true, m.Colors);
        }

        private void BuildTurfLip(CliffWallColumns columns, CliffTurfLipDef lip)
        {
            float elevationValid = HasToeElevations ? 1f : 0f;
            CliffWallMeshData m = TurfLipMesh(columns, lip.StripMetresS, lip.InlandMetres, lip.FaceMetres,
                                              _alongOffsetMetres, _faceMetresS, ProfileAt, elevationValid,
                                              transform.position, _ribWarp, _jag, lip.RiserFadeMetres);
            Emit(TurfLipChildName, m.Verts, m.Uvs, m.Tris, lip.Strip, null, null, localOrder: 1,
                 m.ElevationUv, m.SeaPlanUv, decal: true, m.Colors);
        }

        /// <summary>
        /// ⭐ The turf lip's mesh: TWO vertices a station, and the strip laid on them RIGID. A station's line point is
        /// its displaced brow; its top vertex stands <paramref name="inlandMetres"/> up the drawn column from it and
        /// its bottom vertex <paramref name="faceMetres"/> down it, so every station carries the same metres of strip
        /// and a jag in the brow moves the strip's rows, never squeezes them. Where the face is cut short (a taper, a
        /// clifftop in front) the strip is CUT there, its v with it, not squeezed onto what is left. u counts metres
        /// along the brows over the strip's own length, so the lip repeats every <paramref name="stripMetresS"/> and
        /// not with the face. One strip texel is one ground texel (owner 11 26: no mixels). Pure.
        /// </summary>
        public static CliffWallMeshData TurfLipMesh(CliffWallColumns columns, float stripMetresS, float inlandMetres,
                                                    float faceMetres, float alongOffsetMetres, float faceMetresS,
                                                    System.Func<float, float, float> displacement,
                                                    float elevationValid, Vector3 origin,
                                                    CliffRibWarp ribWarp = default, CliffJag jag = default,
                                                    float riserFadeMetres = 0f)
        {
            CliffWallSample[] samples = columns.Whole;
            int stations = samples.Length;
            var verts = new Vector3[stations * 2];
            var uvs = new Vector2[verts.Length];
            var elevationUv = new Vector2[verts.Length];
            var seaPlanUv = new Vector2[verts.Length];
            var colors = new Color[verts.Length];
            float stripMetresT = inlandMetres + faceMetres;
            float along = 0f;
            // For the riser fade below: each station's pushed distance, and how far across the screen a push shows.
            float[] pushed = riserFadeMetres > 0f ? new float[stations] : null;
            float[] across = riserFadeMetres > 0f ? new float[stations] : null;

            for (int c = 0; c < stations; c++)
            {
                if (c > 0) along += Vector2.Distance(columns.AlongBrow[c - 1], columns.AlongBrow[c]);

                CliffWallSample s = samples[c];
                Vector2 toeP = CliffWallGeometry.ToeScreen(in s);
                Vector2 down = CliffWallGeometry.DrawnDown(s.BrowPlan, toeP);
                float drawn = Vector2.Distance(s.BrowPlan, toeP);
                float metres = alongOffsetMetres + along;
                // The same slide and gain as the face at this station (see FaceMesh), so the lip stays on its brow.
                Vector2 at = columns.AlongBrow[c];
                float uRelief = CliffWallGeometry.TileU(ribWarp.IsOff ? metres : metres + CliffRibWarpMath.ShiftMetres(in ribWarp, at), faceMetresS);
                Vector2 outward = CliffWallGeometry.OutwardPlan(in s);
                float push = CliffJagMath.Outward(
                    in jag, displacement(uRelief, 0f), CliffRibWarpMath.DepthGain(in ribWarp, at),
                    CliffJagMath.FacingGain(in jag, outward, down), uRelief * faceMetresS, 0f);
                Vector2 line = s.BrowPlan + outward * push;
                if (pushed != null)
                {
                    pushed[c] = push;
                    across[c] = CliffJagMath.AcrossTheColumn(in jag, outward, down);
                }
                float keep = KeepAt(columns, c);
                float clip = ClipAt(columns, c);
                float shown = Mathf.Min(faceMetres, drawn * Mathf.Min(keep, clip));
                float faceT = drawn > 1e-4f ? Mathf.Clamp01(shown / drawn) : 0f;
                float u = stripMetresS > 0f ? metres / stripMetresS : 0f;

                // The same fades the brow strip took: out before an open run end, and under a clifftop in front.
                float strength = CliffWallGeometry.RunEndFadeStrength(keep);
                float a = AlphaAt(columns, c) * (1f - strength * strength) *
                          Mathf.SmoothStep(0f, 1f, clip / CliffWallGeometry.OcclusionLipFadeKeep);

                for (int r = 0; r < 2; r++)
                {
                    int idx = c * 2 + r;
                    Vector2 p = r == 0 ? line - down * inlandMetres : line + down * shown;
                    verts[idx] = new Vector3(p.x - origin.x, p.y - origin.y, 0f);
                    // The strip's top row is v = 1 (a PNG's first row is the texture's top).
                    uvs[idx] = new Vector2(u, r == 0 || stripMetresT <= 0f ? 1f : 1f - (inlandMetres + shown) / stripMetresT);
                    float t = r == 0 ? 0f : faceT;
                    elevationUv[idx] = new Vector2(
                        CliffWaterlineMath.ElevationAt(s.ToeElevation + s.DropMetres, s.DropMetres, t), elevationValid);
                    seaPlanUv[idx] = s.ToePlan;
                    colors[idx] = new Color(1f, 1f, 1f, a);
                }
            }

            // ⭐ THE RISER FADE. Where the jag steps the brow line and the wall faces across the screen, the step's
            // riser is seen side on and the rigid strip laid over it is a wedge of turf hanging into the rock. The
            // lip dissolves out at the two stations of such a step (alpha, so the shader crumbles it in pixels) and
            // the wall's own top edge is the brow there. A wall at full facing has no riser to see and is untouched.
            if (pushed != null)
                for (int c = 0; c < stations; c++)
                {
                    float step = Mathf.Max(c > 0 ? Mathf.Abs(pushed[c] - pushed[c - 1]) : 0f,
                                           c + 1 < stations ? Mathf.Abs(pushed[c + 1] - pushed[c]) : 0f);
                    float seen = step * across[c];
                    if (seen <= 0f) continue;
                    float kept = 1f - Mathf.SmoothStep(0f, 1f, seen / riserFadeMetres);
                    colors[c * 2].a *= kept;
                    colors[c * 2 + 1].a *= kept;
                }

            return Laid(columns, 1, verts, uvs, elevationUv, seaPlanUv, colors);
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
        /// <paramref name="ribWarp"/> slides the tile and scales the relief by plan position (<see cref="CliffRibWarpDef"/>);
        /// its default is off, and then the mesh is exactly as it was before the warp.
        /// </summary>
        public static CliffWallMeshData FaceMesh(CliffWallColumns columns, float startSurfaceMetres,
                                                 float endSurfaceMetres, int rows, float alongOffsetMetres,
                                                 float faceMetresS, float faceMetresT,
                                                 System.Func<float, float, float> displacement,
                                                 float elevationValid, Vector3 origin,
                                                 CliffRibWarp ribWarp = default, CliffJag jag = default,
                                                 bool planar = false)
        {
            CliffWallSample[] samples = columns.Whole;
            int stations = samples.Length;
            var verts = new Vector3[stations * (rows + 1)];
            var uvs = new Vector2[verts.Length];
            var elevationUv = new Vector2[verts.Length];
            var seaPlanUv = new Vector2[verts.Length];
            Color[] colors = columns.Alpha != null || columns.Keep != null || columns.Clip != null
                ? new Color[verts.Length] : null;

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
                // The rib warp is read at the station's texture brow, the point "along" is counted on, so every band
                // and both decals of a station, and both chunks that share it, read the same slide and the same gain.
                // The painted rock keeps the plain u (no texel is stretched); only the relief is read slid.
                Vector2 at = columns.AlongBrow[c];
                float u = CliffWallGeometry.TileU(alongOffsetMetres + along, faceMetresS);
                float uRelief = ribWarp.IsOff ? u : CliffWallGeometry.TileU(
                    alongOffsetMetres + along + CliffRibWarpMath.ShiftMetres(in ribWarp, at), faceMetresS);
                float relief = CliffRibWarpMath.DepthGain(in ribWarp, at);
                // A wall that faces across the screen takes less of the roughness, or none (CliffJagMath.FacingGain).
                float facing = CliffJagMath.FacingGain(in jag, outward, CliffWallGeometry.DrawnDown(brow, toe));
                float alongRelief = uRelief * faceMetresS;

                // A band is bounded in DEPTH below the brow, so each station converts it to its own t —
                // which is what lets two bands meet exactly at every station of a face whose height is
                // changing under them. A station shorter than the band's start collapses to the toe,
                // which is exactly right: on a face shorter than the soil horizon there is no rock band.
                float t0 = Mathf.Clamp01(startSurfaceMetres / surface);
                float t1 = endSurfaceMetres > startSurfaceMetres
                         ? Mathf.Clamp01(endSurfaceMetres / surface)
                         : 1f;
                if (t1 < t0) t1 = t0;
                // Near an open run end the face is drawn only part way down, so it tapers into the ground. The
                // texture is cut, never squeezed: v still counts drawn metres from the band's top.
                float keep = KeepAt(columns, c);
                // A clifftop in front hides the rest of the column: cut there, with no fade.
                float shown = Mathf.Min(keep, ClipAt(columns, c));
                t0 = Mathf.Min(t0, shown);
                t1 = Mathf.Min(t1, shown);

                Vector2 above = default;
                float drawnMetres = 0f;

                for (int r = 0; r <= rows; r++)
                {
                    float t = Mathf.Lerp(t0, t1, (float)r / rows);
                    Vector2 p = Vector2.Lerp(brow, toe, t) + outward * CliffJagMath.Outward(
                        in jag, displacement(uRelief, t), relief, facing, alongRelief, t);
                    // v counts DRAWN metres down the column from the BAND's own top, row by row (owner 11-26: no
                    // mixels; CliffWallGeometry.DrawnMetres). It was surface metres, the kit's rule, which draws a
                    // foreshortened face's texels shorter than the ground's.
                    drawnMetres = r == 0 ? 0f : CliffWallGeometry.DrawnMetres(drawnMetres, above, p);
                    above = p;

                    int idx = c * (rows + 1) + r;
                    verts[idx] = new Vector3(p.x - origin.x, p.y - origin.y, 0f);
                    uvs[idx] = new Vector2(u, CliffWallGeometry.TileV(drawnMetres, faceMetresT));
                    // A chunk that faces across the screen (CliffJagMath.FacesAcrossTheScreen) is mapped on the
                    // screen art grid instead: u counts world x and v world y downward, so its bedding lies level,
                    // its texels are square and unrotated, and two such chunks agree where they meet.
                    if (planar) uvs[idx] = CliffWallGeometry.ScreenPlanarUv(p, faceMetresS, faceMetresT);
                    // The elevation walks the SURFACE parameter, brow → toe, which is the row parameter
                    // the geometry is laid on — so the waterline crossing lands on the rows the mesh
                    // already has instead of between them.
                    elevationUv[idx] = new Vector2(
                        CliffWaterlineMath.ElevationAt(s.ToeElevation + s.DropMetres, s.DropMetres, t),
                        elevationValid);
                    seaPlanUv[idx] = s.ToePlan;
                    if (colors != null)
                        colors[idx] = new Color(1f, 1f, 1f,
                            AlphaAt(columns, c) * CliffWallGeometry.RunEndAlphaAt(t, keep, surface));
                }
            }

            return Laid(columns, rows, verts, uvs, elevationUv, seaPlanUv, colors);
        }

        private static float AlphaAt(CliffWallColumns columns, int c) =>
            columns.Alpha != null && c < columns.Alpha.Length ? columns.Alpha[c] : 1f;

        private static float KeepAt(CliffWallColumns columns, int c) =>
            columns.Keep != null && c < columns.Keep.Length ? Mathf.Clamp01(columns.Keep[c]) : 1f;

        private static float ClipAt(CliffWallColumns columns, int c) =>
            columns.Clip != null && c < columns.Clip.Length ? Mathf.Clamp01(columns.Clip[c]) : 1f;

        /// <summary>
        /// A brow (<paramref name="brow"/> true) or toe strip's mesh, laid on <paramref name="columns"/> the way
        /// <see cref="FaceMesh"/> lays a band, and sliced the same way where a station is cut.
        /// </summary>
        public static CliffWallMeshData DecalMesh(CliffWallColumns columns, bool brow, float stripMetresT,
                                                  float browLineAt, float subdivideMetres, float alongOffsetMetres,
                                                  float faceMetresS, System.Func<float, float, float> displacement,
                                                  float elevationValid, Vector3 origin,
                                                  CliffRibWarp ribWarp = default, CliffJag jag = default)
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
            Color[] colors = columns.Alpha != null || columns.Keep != null || columns.Clip != null
                ? new Color[verts.Length] : null;

            float along = 0f;

            for (int c = 0; c < stations; c++)
            {
                if (c > 0) along += Vector2.Distance(columns.AlongBrow[c - 1], columns.AlongBrow[c]);

                CliffWallSample s = samples[c];
                Vector2 browP = s.BrowPlan;
                Vector2 toeP = CliffWallGeometry.ToeScreen(in s);
                Vector2 outward = CliffWallGeometry.OutwardPlan(in s);
                float surface = Mathf.Max(1e-4f, CliffWallGeometry.SurfaceLengthMetres(in s));
                // The same slide and gain as the face at this station (see FaceMesh), so the strip stays on its rock.
                Vector2 at = columns.AlongBrow[c];
                float u = CliffWallGeometry.TileU(alongOffsetMetres + along, faceMetresS);
                float uRelief = ribWarp.IsOff ? u : CliffWallGeometry.TileU(
                    alongOffsetMetres + along + CliffRibWarpMath.ShiftMetres(in ribWarp, at), faceMetresS);
                float relief = CliffRibWarpMath.DepthGain(in ribWarp, at);
                float facing = CliffJagMath.FacingGain(in jag, outward, CliffWallGeometry.DrawnDown(browP, toeP));
                float alongRelief = uRelief * faceMetresS;
                float atBrow = CliffJagMath.Outward(in jag, displacement(uRelief, 0f), relief, facing, alongRelief, 0f);
                // Where the face tapers into the ground the toe strip seats on the face's shortened foot. Where a
                // clifftop in front hides the lower face, both strips are cut there like the rock.
                float keep = KeepAt(columns, c);
                float clip = ClipAt(columns, c);

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
                            : keep * surface - (1f - v) * toeFace;         // ...up from the (kept) toe
                        faceT = Mathf.Min(Mathf.Clamp01(depth / surface), Mathf.Min(keep, clip));
                        p = Vector2.Lerp(browP, toeP, faceT) + outward * CliffJagMath.Outward(
                            in jag, displacement(uRelief, faceT), relief, facing, alongRelief, faceT);
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
                    if (colors != null)
                    {
                        // A tapered column's toe strip fades with the taper. The brow strip (the sod lip) fades only in
                        // the last stretch before the open end, so the lip does not run on past the rock as a spike,
                        // and its face part also fades above a shortened foot like the rock under it.
                        // A strip squeezed onto a shortened foot would draw a pale line along the taper, so the face part
                        // of either strip goes quickly.
                        float a = AlphaAt(columns, c);
                        float strength = CliffWallGeometry.RunEndFadeStrength(keep);
                        float faceFade = (1f - strength) * (1f - strength);
                        // A face cut short (by a taper or by the clifftop in front) has no foot on the ground, so its toe
                        // strip goes before the cut is deep: squeezed onto the cut it draws a pale line down the face
                        // (the West Ledges streak at wall 057).
                        if (!brow) a *= faceFade * CliffWallGeometry.ToeStripShownAt(Mathf.Min(keep, clip));
                        else if (faceT > 0f) a *= faceFade;
                        else a *= 1f - strength * strength;
                        if (brow) a *= Mathf.SmoothStep(0f, 1f, clip / CliffWallGeometry.OcclusionLipFadeKeep);
                        colors[idx] = new Color(1f, 1f, 1f, a);
                    }
                }
            }

            return Laid(columns, rows, verts, uvs, elevationUv, seaPlanUv, colors);
        }

        private static CliffWallMeshData Laid(CliffWallColumns columns, int rows, Vector3[] verts, Vector2[] uvs,
                                              Vector2[] elevationUv, Vector2[] seaPlanUv, Color[] colors = null)
        {
            var laid = new CliffWallMeshData
            {
                Verts = verts, Uvs = uvs, ElevationUv = elevationUv, SeaPlanUv = seaPlanUv, Colors = colors,
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

            Color[] colors = null;
            if (laid.Colors != null)
            {
                // A feather is laid unsliced; this keeps a sliced wall honest if one ever carries colours.
                colors = new Color[count];
                for (int j = 0; j < n; j++)
                {
                    int left = columns.Gap[j] * rowVerts;
                    int right = Mathf.Min(left + rowVerts, laid.Colors.Length - rowVerts);
                    for (int r = 0; r <= rows; r++)
                        colors[j * rowVerts + r] = Color.LerpUnclamped(laid.Colors[left + r], laid.Colors[right + r], columns.At[j]);
                    if (columns.At[j] > 0f)
                        for (int r = 0; r < rows; r++)
                            colors[diagonals + ordinal[j] * rows + r] =
                                Color.LerpUnclamped(laid.Colors[left + r + 1], laid.Colors[right + r], columns.At[j]);
                }
            }

            return new CliffWallMeshData
            {
                Verts = verts, Uvs = uvs, ElevationUv = elevation, SeaPlanUv = sea, Tris = tris.ToArray(), Colors = colors,
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
                          Vector2[] elevationUv = null, Vector2[] seaPlanUv = null, bool decal = false,
                          Color[] colors = null)
        {
            var mesh = new Mesh { name = "HH" + childName, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            // The shader multiplies by the vertex colour, alpha included: a seam feather fades out this way.
            if (colors != null) mesh.SetColors(colors);
            // ⭐ THE WATERLINE CHANNELS (2026-08-06). uv1 = (this vertex's ELEVATION in metres, 1 if that
            // elevation is REAL); uv2 = the station's TOE PLAN, which is where the sea actually is — the
            // DRAWN toe has already been pushed down-screen by the drop, so sampling the wave field at it
            // would read the swell several metres from the wall it belongs to.
            if (elevationUv != null) mesh.SetUVs(1, elevationUv);
            if (seaPlanUv != null) mesh.SetUVs(2, seaPlanUv);
            // uv3 = (the mix's variant weight at the station's toe, 1 if the mix is real). From the toe plan, so
            // two chunks or two walls meeting at one station carry one weight and the skin never jumps at a cut.
            if (_mix != null && !decal && seaPlanUv != null)
            {
                var mixUv = new Vector2[seaPlanUv.Length];
                for (int i = 0; i < seaPlanUv.Length; i++) mixUv[i] = new Vector2(_mix.VariantWeightAt(seaPlanUv[i]), 1f);
                mesh.SetUVs(3, mixUv);
            }
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
            // A kit face is drawn two pixels a texel; a mix may ask for one kit texel per ground texel instead.
            _mpb.SetFloat(IdBaseTexelScale, _mix != null && !decal ? Mathf.Max(1f, _mix.BaseFaceTexelScale) : 1f);
            // The mix's second palette: only on a face band (a decal is drawn as baked), only with a mix.
            bool variant = _mix != null && !decal;
            _mpb.SetFloat(IdVariantOn, variant ? 1f : 0f);
            if (variant)
            {
                _mpb.SetTexture(IdPaletteVariant, _mix.VariantSkin.Palette);
                _mpb.SetVector(IdVariantBlock, new Vector4(Mathf.Max(1f, _mix.BlendBlockTexels.x),
                                                           Mathf.Max(1f, _mix.BlendBlockTexels.y), 0f, 0f));
            }
            // The variant skin's own FACES (a style), where it has one for this band's kit face: the same
            // blocks then draw a different rock pattern too, not only a different palette. A band whose face
            // the style does not redraw (the till cap) keeps its face and takes the palette alone.
            CliffStyleFace face = default;
            bool styled = variant && _mix.VariantSkin.FaceStyle != null &&
                          _mix.VariantSkin.FaceStyle.TryGetFace(unlit, out face);
            _mpb.SetFloat(IdVariantFaceOn, styled ? 1f : 0f);
            if (styled)
            {
                _mpb.SetTexture(IdUnlitVariant, face.Index);
                _mpb.SetTexture(IdNormalVariant, face.Normal);
                _mpb.SetTexture(IdMaskVariant, face.Mask);
            }
            // The faulted bedding: only on a face band laid on the screen grid, only with the mix's numbers.
            CliffFault fault = CliffFault.From(_mix);
            _mpb.SetVector(IdFaultA, fault.ShaderA);
            _mpb.SetVector(IdFaultB, fault.ShaderB(_planarFace && !decal));
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
