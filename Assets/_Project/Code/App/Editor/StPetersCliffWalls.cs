using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// ⭐ <b>THE COAST STANDS UP.</b> Turns the region's authored coast plan into standing cliff
    /// geometry — face quads consuming the Cliff Face kit v10, sorted into the decor band by their own
    /// feet, and displaced by the kit's plan-displacement profile so the silhouette is rock rather than
    /// a drawn edge.
    ///
    /// <para><b>⚠ ONE LAW, TWO CONSUMERS.</b> Nothing here decides where a cliff is or how tall it
    /// stands. The walls are sampled off <see cref="TidalTerrain.IslandProfileAt"/> — literally the
    /// method the walk gate and the seabed bake read — so the picture cannot disagree with the
    /// simulation. Retune <c>StPetersBuilder.CoastSectors</c> or <c>CliffPlungeWidth</c> and the walls
    /// move with them without a line changing here. That also means the sector FEATHER comes through for
    /// free: a run's drop tapers as it approaches a beach, so a cliff dies into the sand the way a
    /// headland does instead of ending in a step.</para>
    ///
    /// <para><b>What this is NOT.</b> It is not walkability — a wall is a picture OF the height field
    /// and the height field stays the single source of truth for where a player may stand (the slope
    /// gate is its own lane). It is not the coast plan — the 40.2% authored share and every named
    /// constraint are rendered AS AUTHORED and nothing here retunes them.</para>
    ///
    /// <para><b>⭐ ST PETERS' WALLS ARE ITS DEFS NOW (terrain PR 5w).</b> Since the island's ground became the
    /// package's import, the analytic walk above no longer stands where the ground does, so the scene's walls are
    /// data: one <see cref="CliffWallDef"/> per wall, by its real id, under <see cref="WallsFolder"/>. The builder
    /// draws them through <see cref="ChunksOfDefs"/> — the same chunk math, cutting where each Def ends — and the
    /// scene's own walls are read back by <see cref="WallsInScene"/>. <see cref="ResolveChunks"/> stays: it is
    /// the cliff proof's walk and the analytic coast's own pins.</para>
    ///
    /// <para><b>⭐ CUT AT 1 M, AS SLICES (terrain PR 5w).</b> St Peters' walls are cut where their toes would span
    /// <see cref="RegionTerrainPlanDef.WallToeSpanMetres"/> (its plan's 1 m, not the builder's own 2 m, which Nine Mile
    /// Creek keeps). Re-cutting set stations between whole ones (a k that is not whole): the chunk math carries each
    /// one's slice, and the whole stations past a chunk's cut ends, to <see cref="CliffWallSurface"/>, which draws it
    /// as that slice of the face its whole stations draw. <c>u</c> runs along the whole stations only, so a cut
    /// station moves no texture, and where a top was moved out its texture runs along where it was
    /// (<see cref="CliffWallDef.TextureBrow"/>).</para>
    /// </summary>
    public static class StPetersCliffWalls
    {
        /// <summary>The scene object every generated chunk parents under. Rebuilding destroys it whole,
        /// which is what makes a Refresh converge rather than stack a second coast on the first.</summary>
        public const string RootName = "CliffWalls";

        /// <summary>St Peters' walls' Defs, one per wall by real id (<c>wall.stp_NNN</c>).</summary>
        public const string WallsFolder = "Assets/_Project/Data/Terrain/StPetersWalls";

        /// <summary>The shipped face material — all texture slots empty by design, because the bake root
        /// is gitignored and the channels arrive per chunk through a property block.</summary>
        public const string CliffFaceMat = "Assets/_Project/Art/Materials/CliffFace.mat";

        /// <summary>
        /// Stations per metre of shore. Matches the kit's own <c>ProfileSubdivideMetres</c>: finer buys
        /// nothing at 32 px/m and coarser straightens the brow line back out, which is the whole thing
        /// the profile exists to prevent.
        /// </summary>
        public const float StationMetres = CliffCatalog.ProfileSubdivideMetres;

        /// <summary>
        /// How long a chunk may run, in metres of shore, before it is cut and given its own sorting
        /// order.
        ///
        /// <para><b>⚠ This is a SORTING number, not a rendering one.</b> A chunk has one order and spans
        /// a stretch of world Y; too long and most of its length is layered by draw order rather than by
        /// position — the ADR 0032 defect at cliff scale. 6 m sits under
        /// <c>CliffWallGeometry.MaxSafeChunkMetres</c> for every drop this coast produces, and
        /// <c>StPetersCliffWallTests</c> re-derives the true worst case off the BUILT walls rather than
        /// trusting that bound.</para>
        /// </summary>
        public const float ChunkMetres = 6f;

        /// <summary>
        /// ⭐ <b>THE CUT THAT ACTUALLY MATTERS: how much world Y one chunk may cover.</b>
        ///
        /// <para><b>Why shore length is the wrong ruler.</b> The decor band resolves POSITION, at four
        /// orders a metre of Y (ADR 0032) — so what makes a single sorting order dishonest is not how
        /// long a chunk is but how much Y it spans. Where the coast runs east–west those are miles
        /// apart: 6 m of shore near due south covers under a metre of Y, while 6 m at the island's east
        /// end covers nearly six. Cutting on shore length alone left chunks whose foot wandered 5.6 m,
        /// which is a walker at one end being sorted by the wall at the other. MEASURED, by the test
        /// below, not reasoned about.</para>
        ///
        /// <para><b>Two metres, because that is about one character.</b> A chunk sorts at its LOWEST
        /// toe, so a walker standing at its shallowest foot is mis-sorted by at most this span — and a
        /// sorting error smaller than the height of the thing being sorted cannot be seen. Halving it
        /// would double the chunk count for no visible gain; <c>StPetersCliffWallTests</c> measures the
        /// worst error this actually produces and states it.</para>
        /// </summary>
        public const float ChunkToeSpanMetres = 2f;

        /// <summary>
        /// The shortest face worth drawing, in metres of drop. Inside a sector's feather the blended
        /// profile hands back a drop that tapers toward the neighbouring beach's; below this it is no
        /// longer a wall, and a sliver of face quad there reads as a rendering artefact rather than as
        /// rock. (The kit's brow and toe DECALS are what properly finish a run's ends — a later slice.)
        /// </summary>
        public const float MinFaceDropMetres = 1f;

        /// <summary>
        /// ⭐ <b>THE BATTER LAW — the aspect law's twin, for angle instead of facing.</b> A face
        /// shallower than this is not drawn at all.
        ///
        /// <para><b>Why it must exist.</b> The batter is DERIVED from the profile, and inside a sector's
        /// feather the profile tapers toward the neighbouring beach's — so the derived angle runs
        /// smoothly down from ~79° to nearly flat as a cliff dies into a gully. The snap has no
        /// tolerance of its own, so without this rule a 22° taper would be handed the 62° ramp texture
        /// and drawn with ramp bedding: the kit's rule 11 failure, "a batter is not a shading
        /// multiplier", made literal. The wall would not look broken — it would look like a slope with
        /// the wrong rock on it.</para>
        ///
        /// <para><b>Derived, not chosen.</b> Half a step below the shallowest angle the kit authors at
        /// all — <c>ramp</c> (62°) less half its gap to <c>bank</c> (48°) — which is precisely the same
        /// construction as <c>CoastPlan.AspectSnapToleranceDegrees</c>. Note it is measured against the
        /// KIT's ladder, not the default bake's: a face at 50° is one the kit COULD draw if bank were
        /// baked, and the fix for that would be a bake, not a wider tolerance.</para>
        /// </summary>
        public static float MinBatterDegrees =>
            CliffCatalog.BatterAngles[2] - (CliffCatalog.BatterAngles[2] - CliffCatalog.BatterAngles[3]) * 0.5f;

        /// <summary>Degrees of bearing the generator runs PAST each cliff sector's ends, so a run tapers
        /// out through the blend instead of stopping at the unfeathered class boundary. Mirrors the
        /// plan's own feather; the taper itself comes from the profile, not from here.</summary>
        public const float RunOverrunDegrees = StPetersBuilder.CoastBlendDegrees;

        /// <summary>The rock the coast is built from — St Peters is "red sandstone cliffs", and it is
        /// what <see cref="CliffBaker.DefaultRock"/> ships. The LOWER band of every face.</summary>
        public const string Rock = CliffBaker.DefaultRock;

        /// <summary>
        /// The rock of the face's UPPER band — the kit's <c>till</c>, which its own rig calls "the soft
        /// cliff: red boulder clay over the rock". Rill gullies, slump benches and grass tongues running
        /// down them, against sandstone's horizontal red bedding.
        /// </summary>
        public const string OverburdenRock = CliffBaker.OverburdenRock;

        /// <summary>
        /// ⭐ <b>THE SOIL HORIZON — how deep the eroded topsoil goes before the rock shows, in metres of
        /// TRUE HEIGHT.</b> This is the whole of the owner's stratified-cliff direction, as one number.
        ///
        /// <para><b>Why one number is enough for a brief with two halves.</b> He asked for stratified
        /// faces — topsoil above, red sandstone below — and also said solid rock is right in SOME places.
        /// A soil horizon has a real depth that does not care how tall the cliff under it is, so a fixed
        /// one gives both at once: on this coast's 10 m faces it is a sixth of the wall and reads as rock
        /// with a soil lip; on its shortest 2.85 m ones it is over half and the same wall reads as an
        /// eroding bank. Height does the art direction, because on a real coast it does.</para>
        ///
        /// <para><b>1.8 m, and where that comes from.</b> It is the island's own meadow band — the
        /// plateau stands at 6 m and grass reaches down to 4.2 m — so the soil on the face is exactly as
        /// deep as the soil the clifftop is already growing on. <b>⚠ It is deliberately its OWN constant
        /// and not a reference to those two.</b> The owner still owes a ruling on how far grass may reach
        /// over the brow, and that ruling must be free to move the GRASS without silently repainting
        /// every cliff face in the region.</para>
        ///
        /// <para><b>The low-cliff dial, too.</b> A face shorter than this carries no rock band at all —
        /// it is drawn entirely in till, which is the topsoil-erosion treatment stated as geometry rather
        /// than as paint. Below <see cref="MinFaceDropMetres"/> no wall stands at any rate.</para>
        /// </summary>
        public const float OverburdenMetres = 1.8f;

        /// <summary>One chunk's worth of resolved geometry — the builder's own intermediate, exposed so
        /// the tests can assert the RULES (span extraction, batter snap, aspect choice, sorting) without
        /// a scene or a baked texture in sight.</summary>
        public struct Chunk
        {
            public List<CliffWallSample> Samples;
            public int AspectIndex;         // into CliffCatalog.Aspects
            public int BatterIndex;         // into CliffCatalog.Batters
            public float WallAzimuth;       // TRUE, unsnapped — the shader wants the real bearing
            public CoastClass Class;

            /// <summary>Which unbroken RUN of coast this chunk belongs to. Chunks are cut for sorting;
            /// runs are where the wall genuinely stops.</summary>
            public int RunIndex;

            /// <summary>⭐ Metres of shore this chunk's run has already spent before this chunk's first
            /// station. The B3 fix: see <see cref="CliffWallGeometry.TileU"/> for what restarting it at
            /// zero per chunk measured out at.</summary>
            public float AlongOffsetMetres;

            /// <summary>⭐ The longest face anywhere in this chunk's RUN, in surface metres — the row
            /// count every chunk of the run shares, so a shared edge is approximated identically on both
            /// sides. See <see cref="CliffWallGeometry.RowsBasisSurfaceMetres"/>.</summary>
            public float RunSurfaceMetres;

            /// <summary>Per station, in step with <see cref="Samples"/>: 0 for a whole station, else how far it is cut
            /// from the whole station before it toward the one after (terrain PR 5w). Null or empty: all whole.</summary>
            public List<float> SliceAt;

            /// <summary>Per station: where its texture runs along, its brow unless a top was moved out. Null or empty:
            /// the brows.</summary>
            public List<Vector2> TextureBrow;

            /// <summary>The whole station before the first station and after the last, where those are cut (one, or
            /// none, each): the columns their slices are laid between.</summary>
            public CliffWallSliceEnd[] SliceBefore, SliceAfter;
        }

        /// <summary>One station of the coast walk, as the chunk math reads it: the wall standing there
        /// (when <see cref="Live"/>), the aspect and batter its face snaps to, its true azimuth and its
        /// class. A station that is not live is where the wall stops: it ends the run.</summary>
        public struct Station
        {
            public CliffWallSample Sample;
            public int AspectIndex;         // into CliffCatalog.Aspects; -1 where no wall stands
            public int BatterIndex;         // into BakedBatterAngles(); -1 where no wall stands
            public float Azimuth;           // TRUE, unsnapped
            public CoastClass Class;
            public bool Live;

            /// <summary>A chunk ends at the station before this one, which seeds the next, as a texture change cuts
            /// it. A wall laid from its Def (<see cref="ChunksOfDefs"/>) cuts here, so each Def is one chunk.</summary>
            public bool CutBefore;

            /// <summary>0 for a whole station; else how far it is cut from the whole station before it toward the one
            /// after (terrain PR 5w). Its chunk draws it as that slice of the face the two of them draw.</summary>
            public float SliceAt;

            /// <summary>Where its texture runs along, where that is not its brow: a top moved out keeps its texture, and
            /// the run's `u` downstream, where they were. Null: the brow.</summary>
            public Vector2? TextureBrowPlan;

            /// <summary>A standing station, its aspect and batter snapped exactly as the walk snaps them.</summary>
            public static Station Standing(in CliffWallSample sample, float azimuth, CoastClass cls) =>
                new Station
                {
                    Sample = sample,
                    AspectIndex = SnapAspectIndex(azimuth),
                    BatterIndex = CliffWallGeometry.SnapBatterIndex(CliffWallGeometry.BatterDegrees(in sample),
                                                                    BakedBatterAngles()),
                    Azimuth = azimuth,
                    Class = cls,
                    Live = true,
                };

            /// <summary>A station where no wall stands.</summary>
            public static Station Gap =>
                new Station { AspectIndex = -1, BatterIndex = -1, Azimuth = 0f, Class = CoastClass.Beach };
        }

        /// <summary>A chunk's serialized fields: what <see cref="Build"/> configures its
        /// <see cref="CliffWallSurface"/> with, less the assets. The soil band ends, and the rock band
        /// starts, <see cref="OverburdenSurfaceMetres"/> down the face; the rock runs on to the toe.</summary>
        public struct ChunkFields
        {
            public Vector2[] BrowPlan;
            public Vector2[] ToePlan;
            public float[] DropMetres;
            public float[] ToeElevations;
            public float AlongOffsetMetres;
            public float RowsBasisSurfaceMetres;
            public float WallAzimuth;
            public float Batter;
            public float OverburdenSurfaceMetres;
            public Vector2[] TextureBrowPlan;           // empty where they are the brows
            public float[] SliceAt;                     // empty where every station is whole
            public CliffWallSliceEnd[] SliceBefore;     // one where the first station is cut, else none
            public CliffWallSliceEnd[] SliceAfter;      // one where the last station is cut, else none
        }

        // =============================================================================================
        //  the plan → geometry (pure; no scene, no assets — this is what the tests drive)
        // =============================================================================================

        /// <summary>
        /// Walk the whole coast and resolve it into chunks of standing wall. Deterministic and total: a
        /// pure function of the terrain component's authored plan, so two runs on two machines produce
        /// the same coast (rule 5).
        /// </summary>
        public static List<Chunk> ResolveChunks(TidalTerrain terrain)
        {
            if (terrain == null) return new List<Chunk>();

            // Walk in BEARING, but step by a constant length of SHORE: a degree near due south buys
            // 120 m/rad of coast on this ellipse where one near the harbour buys 70, so a constant
            // angular step would make the stations bunch at the ends and thin out along the flanks.
            var stations = new List<Station>();

            float bearing = 0f;
            while (bearing < 360f)
            {
                float speed = Mathf.Max(1e-3f, ShoreSpeed(bearing));         // metres per radian
                float stepDeg = StationMetres / speed * Mathf.Rad2Deg;

                stations.Add(TryStationAt(terrain, bearing, out CliffWallSample sample, out CoastClass cls,
                                          out float azimuth)
                             ? Station.Standing(in sample, azimuth, cls)
                             : Station.Gap);

                bearing += Mathf.Max(1e-4f, stepDeg);
            }
            return ChunksOf(stations);
        }

        /// <summary>
        /// ⭐ <b>THE CHUNK MATH, PURE.</b> The walk's stations, in order, into chunks of standing wall: the
        /// cuts, the run bookkeeping and the run's shared row basis. No terrain and no scene, so the
        /// analytic walk above and stations read off any other ground are cut, chained and sized by the
        /// one rule. Deterministic to the bit (rule 5). Cut at the builder's own <see cref="ChunkToeSpanMetres"/>.
        /// </summary>
        public static List<Chunk> ChunksOf(IReadOnlyList<Station> stations) => ChunksOf(stations, ChunkToeSpanMetres);

        /// <summary>The chunk math, cut where a chunk's toes would span more than <paramref name="toeSpanMetres"/>.</summary>
        public static List<Chunk> ChunksOf(IReadOnlyList<Station> stations, float toeSpanMetres)
        {
            var chunks = new List<Chunk>();
            if (stations == null) return chunks;

            // The whole stations either side of each one, inside its run: a chunk that starts or ends on a cut station
            // carries the whole one past it, the column its slice is laid toward (terrain PR 5w).
            int count = stations.Count;
            var prevWhole = new int[count];
            var nextWhole = new int[count];
            for (int i = 0, w = -1; i < count; i++)
            {
                if (!stations[i].Live) { w = -1; prevWhole[i] = -1; continue; }
                prevWhole[i] = w;
                if (stations[i].SliceAt == 0f) w = i;
            }
            for (int i = count - 1, w = -1; i >= 0; i--)
            {
                if (!stations[i].Live) { w = -1; nextWhole[i] = -1; continue; }
                nextWhole[i] = w;
                if (stations[i].SliceAt == 0f) w = i;
            }

            // Cut runs where the wall stops being drawable, where the TEXTURE must change (aspect or
            // batter), or where a chunk has run long enough that one sorting order stops being honest.
            Chunk current = NewChunk();
            float runMetres = 0f;
            float toeLow = float.MaxValue, toeHigh = float.MinValue;
            Vector2 lastBrow = Vector2.zero;
            Vector2 lastTexture = Vector2.zero;     // the last WHOLE station's, which is where `u` has run to
            int lastIndex = -1;                     // the station `current` ends on

            // ⭐ THE RUN BOOKKEEPING — the B3 fix. A cut is a SORTING decision and must not be visible,
            // so the two things a chunk would otherwise re-derive from its own first station are carried
            // across every cut instead: how far along the run it starts (which is what `u` counts, for
            // the texture AND for the profile displacement), and which run it belongs to (so the row
            // count can be shared afterwards). Both reset only where the wall genuinely STOPS.
            int runIndex = 0;
            float runAlong = 0f;            // arc length from the run's start to `lastTexture`
            float chunkStartAlong = 0f;     // ...and to `current`'s first column

            for (int i = 0; i < stations.Count; i++)
            {
                Station st = stations[i];
                if (!st.Live)
                {
                    if (current.Samples.Count > 0 || runAlong > 0f) runIndex++;
                    if (current.Samples.Count > 0) current.SliceAfter = SliceEnd(stations, lastIndex, nextWhole);
                    Flush(chunks, ref current);
                    runMetres = 0f; toeLow = float.MaxValue; toeHigh = float.MinValue;
                    runAlong = 0f; chunkStartAlong = 0f; lastIndex = -1;
                    continue;
                }

                float toeY = CliffWallGeometry.ToeScreen(st.Sample).y;
                bool whole = st.SliceAt == 0f;
                Vector2 texture = st.TextureBrowPlan ?? st.Sample.BrowPlan;
                bool startsNew = current.Samples.Count == 0;
                if (!startsNew)
                {
                    float step = Vector2.Distance(lastBrow, st.Sample.BrowPlan);
                    runMetres += step;
                    // ⭐ `u` RUNS ALONG THE WHOLE STATIONS ONLY (terrain PR 5w). A cut station is a slice of the
                    // face between two whole ones, so it adds nothing to the run: re-cutting a wall finer slides
                    // no texture, here or downstream.
                    float alongBefore = runAlong, alongStep = 0f;
                    if (whole)
                    {
                        alongStep = Vector2.Distance(lastTexture, texture);
                        runAlong += alongStep;              // `runAlong` is now THIS station's along
                    }
                    bool textureChanged = st.AspectIndex != current.AspectIndex ||
                                          st.BatterIndex != current.BatterIndex;
                    // The Y span INCLUDING this station — a chunk is cut before it grows too tall to
                    // sort honestly, not after (see ChunkToeSpanMetres).
                    float span = Mathf.Max(toeHigh, toeY) - Mathf.Min(toeLow, toeY);
                    if (textureChanged || st.CutBefore || runMetres >= ChunkMetres || span > toeSpanMetres)
                    {
                        // ⭐ THE BOUNDARY STATION SEEDS THE NEXT CHUNK; it does NOT join this one.
                        //
                        // The two chunks must overlap by a station or the coast gets a 0.25 m hole of
                        // open sky at every cut. The obvious way round — let the station that TRIPPED
                        // the cut finish the old chunk — is wrong, and measurably so: it pushes the old
                        // chunk past the very span the cut just enforced, and at a sector feather (where
                        // the drop tapers fastest) one station moved the foot 0.62 m, so chunks came out
                        // 2.34 m wide against a 2 m rule. Carrying the PREVIOUS station forward instead
                        // gives the same seamless overlap while leaving every chunk inside its bound.
                        //
                        // ⚠ AND THE OVERLAP ONLY CLOSES IF ITS `u` COMES FORWARD WITH IT. The shared
                        // station starts the next chunk at the along it already had — `runAlong - step`,
                        // this station's along less the step just taken. Without that the two copies of
                        // one station are displaced by different amounts and the overlap tears open
                        // instead: measured at RMS 0.40 m and up to 1.10 m, at 76 of 78 boundaries.
                        //
                        // A chunk's `u` starts at its first COLUMN: the shared station where it and this one
                        // are whole (today's arithmetic, to the bit), else the whole station the slices are laid
                        // from, whose along is the one before this station.
                        int last = current.Samples.Count - 1;
                        CliffWallSample shared = current.Samples[last];
                        float sharedSlice = current.SliceAt[last];
                        Vector2 sharedTexture = current.TextureBrow[last];
                        current.SliceAfter = SliceEnd(stations, lastIndex, nextWhole);
                        Flush(chunks, ref current);
                        current.Samples.Add(shared);
                        current.SliceAt.Add(sharedSlice);
                        current.TextureBrow.Add(sharedTexture);
                        current.SliceBefore = SliceEnd(stations, lastIndex, prevWhole);
                        float sharedToe = CliffWallGeometry.ToeScreen(shared).y;
                        toeLow = toeHigh = sharedToe;
                        runMetres = step;
                        chunkStartAlong = sharedSlice == 0f && whole ? runAlong - alongStep : alongBefore;
                        startsNew = true;
                    }
                }
                else current.SliceBefore = SliceEnd(stations, i, prevWhole);

                if (startsNew)
                {
                    current.AspectIndex = st.AspectIndex;
                    current.BatterIndex = st.BatterIndex;
                    current.WallAzimuth = st.Azimuth;
                    current.Class = st.Class;
                    current.RunIndex = runIndex;
                    current.AlongOffsetMetres = chunkStartAlong;
                }
                current.Samples.Add(st.Sample);
                current.SliceAt.Add(st.SliceAt);
                current.TextureBrow.Add(texture);
                toeLow = Mathf.Min(toeLow, toeY);
                toeHigh = Mathf.Max(toeHigh, toeY);
                lastBrow = st.Sample.BrowPlan;
                if (whole) lastTexture = texture;
                lastIndex = i;
            }
            if (current.Samples.Count > 0) current.SliceAfter = SliceEnd(stations, lastIndex, nextWhole);
            Flush(chunks, ref current);
            ResolveRunSurfaces(chunks);
            return chunks;
        }

        static Chunk NewChunk() => new Chunk
        {
            Samples = new List<CliffWallSample>(), SliceAt = new List<float>(), TextureBrow = new List<Vector2>(),
            SliceBefore = new CliffWallSliceEnd[0], SliceAfter = new CliffWallSliceEnd[0],
        };

        /// <summary>The whole station past a chunk's end station <paramref name="at"/>, where that is cut (none where it
        /// is whole): <paramref name="wholeBeside"/> is the nearest whole station before it, or after it, in its run.</summary>
        static CliffWallSliceEnd[] SliceEnd(IReadOnlyList<Station> stations, int at, int[] wholeBeside)
        {
            if (at < 0 || stations[at].SliceAt == 0f) return new CliffWallSliceEnd[0];
            int w = wholeBeside[at];
            if (w < 0)
                throw new System.InvalidOperationException(
                    $"station {at} is cut at {stations[at].SliceAt}, with no whole station past it in its run: a run starts and ends whole.");
            Station s = stations[w];
            return new[]
            {
                new CliffWallSliceEnd(s.Sample.BrowPlan, s.Sample.ToePlan, s.Sample.DropMetres, s.Sample.ToeElevation,
                                      s.TextureBrowPlan ?? s.Sample.BrowPlan),
            };
        }

        /// <summary>
        /// Give every chunk of a run the same row-count basis: the longest face anywhere in that run.
        ///
        /// <para>A second pass because a run's tallest station is not known until the run has finished,
        /// and because the alternative — letting each chunk size its own subdivision — is the other half
        /// of the B3 defect. The displaced face is a curve; two chunks that meet at a shared station and
        /// subdivide it differently approximate that curve with different polylines, so the seam does not
        /// close. Measured at <b>26 of the coast's 76 cuts</b>.</para>
        /// </summary>
        static void ResolveRunSurfaces(List<Chunk> chunks)
        {
            var longest = new Dictionary<int, float>();
            foreach (Chunk c in chunks)
            {
                float s = CliffWallGeometry.RowsBasisSurfaceMetres(c.Samples);
                longest[c.RunIndex] = longest.TryGetValue(c.RunIndex, out float had)
                                    ? Mathf.Max(had, s) : s;
            }
            for (int i = 0; i < chunks.Count; i++)
            {
                Chunk c = chunks[i];
                c.RunSurfaceMetres = longest[c.RunIndex];
                chunks[i] = c;
            }
        }

        static void Flush(List<Chunk> into, ref Chunk current)
        {
            // Two stations is the minimum that spans anything; one is a line, and a chunk of one would
            // emit an empty mesh with a real sorting order — invisible, and confusing to debug.
            if (current.Samples.Count >= 2)
            {
                // The shader gets the chunk's MIDPOINT facing, not the facing it happened to start at.
                // The azimuth is deliberately unsnapped so lighting stays continuous along a curving
                // coast (the shader's own note on _WallAzimuth); taking it from one end would then hand
                // the far end of every chunk a light direction several degrees stale, and those errors
                // would all lean the same way. Every station in a chunk snaps to the SAME aspect by
                // construction, so the midpoint is inside that aspect's arc too.
                current.WallAzimuth = CliffWallGeometry.AzimuthOf(
                    CliffWallGeometry.OutwardPlan(current.Samples[current.Samples.Count / 2]));
                into.Add(current);
            }
            current = NewChunk();
        }

        /// <summary>⭐ A chunk's serialized fields, pure: the arrays straight off its samples, and the
        /// scalars the run bookkeeping and the snapped batter give it.</summary>
        public static ChunkFields FieldsOf(in Chunk chunk)
        {
            int n = chunk.Samples.Count;
            var fields = new ChunkFields
            {
                BrowPlan = new Vector2[n],
                ToePlan = new Vector2[n],
                DropMetres = new float[n],
                // The absolute toe elevations — what the WATERLINE is measured against (2026-08-06).
                ToeElevations = new float[n],
                AlongOffsetMetres = chunk.AlongOffsetMetres,
                RowsBasisSurfaceMetres = chunk.RunSurfaceMetres,
                WallAzimuth = chunk.WallAzimuth,
                Batter = CliffCatalog.BatterAngles[BakedBatterToCatalogIndex(chunk.BatterIndex)],
                OverburdenSurfaceMetres = OverburdenSurfaceMetresOf(chunk.BatterIndex),
            };
            for (int i = 0; i < n; i++)
            {
                fields.BrowPlan[i] = chunk.Samples[i].BrowPlan;
                fields.ToePlan[i] = chunk.Samples[i].ToePlan;
                fields.DropMetres[i] = chunk.Samples[i].DropMetres;
                fields.ToeElevations[i] = chunk.Samples[i].ToeElevation;
            }
            // The slices (terrain PR 5w), each written only where it says something: a wall with none is the wall it
            // always was, field for field.
            bool movedTexture = false, anyCut = false;
            for (int i = 0; i < n; i++)
            {
                if (chunk.TextureBrow != null && chunk.TextureBrow.Count == n && !chunk.TextureBrow[i].Equals(fields.BrowPlan[i]))
                    movedTexture = true;
                if (chunk.SliceAt != null && chunk.SliceAt.Count == n && chunk.SliceAt[i] != 0f) anyCut = true;
            }
            fields.TextureBrowPlan = movedTexture ? chunk.TextureBrow.ToArray() : new Vector2[0];
            fields.SliceAt = anyCut ? chunk.SliceAt.ToArray() : new float[0];
            fields.SliceBefore = chunk.SliceBefore ?? new CliffWallSliceEnd[0];
            fields.SliceAfter = chunk.SliceAfter ?? new CliffWallSliceEnd[0];
            return fields;
        }

        /// <summary>A chunk's name: its class, aspect and batter, and its index along the coast.</summary>
        public static string ChunkName(in Chunk chunk, int index) =>
            $"CliffWall_{chunk.Class}_" +
            $"{CliffCatalog.Aspects[chunk.AspectIndex]}_" +
            $"{CliffCatalog.Batters[BakedBatterToCatalogIndex(chunk.BatterIndex)]}_" +
            $"{index:D3}";

        // =============================================================================================
        //  the walls' Defs → chunks (pure; terrain PR 5w)
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>THE WALLS FROM THEIR DEFS.</b> Each live <see cref="CliffWallDef"/> is one chunk, laid through
        /// <see cref="ChunksOf"/> so the run bookkeeping is the walk's own: the Defs a run chains through
        /// (<see cref="CliffWallDef.Follows"/>) are fed as one walk, each Def's first station skipped where it
        /// is the station the wall before it ended on, and a <see cref="Station.CutBefore"/> at its second, so
        /// the chunk math cuts exactly where each Def ends. Offsets march on through every cut, and the run's
        /// row basis is the longest face in it. Runs go in the order of their least id, a gap between each.
        ///
        /// <para>Each station takes its Def's aspect and batter, which the Def holds as the face it wears. It
        /// refuses what would not round-trip: a Def that is not one chunk, a run that does not join station to
        /// station to the bit, a wall that follows none or is followed twice, an aspect or a batter the
        /// default bake does not carry. <paramref name="owners"/> is each chunk's Def, in step.</para>
        ///
        /// <para>Cut at <paramref name="toeSpanMetres"/>, the plan's (<see cref="RegionTerrainPlanDef.WallToeSpanMetres"/>),
        /// so a Def whose toes span more is refused, not drawn. A station whose k is not whole is cut between the whole
        /// stations either side of it on its own wall (across the Defs that wall was cut into), and must say so
        /// (<see cref="CliffStationSource.Interpolated"/>); its slice is how far its k is between theirs.</para>
        /// </summary>
        public static List<Chunk> ChunksOfDefs(IReadOnlyList<CliffWallDef> defs, float toeSpanMetres,
                                               out List<CliffWallDef> owners)
        {
            owners = new List<CliffWallDef>();
            var live = new Dictionary<string, CliffWallDef>(System.StringComparer.Ordinal);
            if (defs == null) return new List<Chunk>();
            foreach (CliffWallDef d in defs)
            {
                if (d == null || !d.IsLive) continue;
                if (live.ContainsKey(d.Id)) throw new System.InvalidOperationException($"two live walls are '{d.Id}'.");
                live.Add(d.Id, d);
            }

            var next = new Dictionary<string, CliffWallDef>(System.StringComparer.Ordinal);
            var starts = new List<CliffWallDef>();
            foreach (CliffWallDef d in live.Values)
            {
                if (string.IsNullOrEmpty(d.Follows)) { starts.Add(d); continue; }
                if (!live.ContainsKey(d.Follows))
                    throw new System.InvalidOperationException($"'{d.Id}' follows '{d.Follows}', which is no live wall.");
                if (next.ContainsKey(d.Follows))
                    throw new System.InvalidOperationException($"'{d.Id}' and '{next[d.Follows].Id}' both follow '{d.Follows}'.");
                next.Add(d.Follows, d);
            }

            var runs = new List<List<CliffWallDef>>();
            int walked = 0;
            foreach (CliffWallDef s in starts)
            {
                var run = new List<CliffWallDef>();
                for (CliffWallDef d = s; d != null; d = next.TryGetValue(d.Id, out CliffWallDef n) ? n : null)
                    run.Add(d);
                walked += run.Count;
                runs.Add(run);
            }
            if (walked != live.Count)
                throw new System.InvalidOperationException($"{live.Count - walked} live walls follow one another in a ring: no run starts them.");
            runs.Sort((a, b) => string.CompareOrdinal(LeastId(a), LeastId(b)));

            var stations = new List<Station>();
            foreach (List<CliffWallDef> run in runs)
            {
                if (stations.Count > 0) stations.Add(Station.Gap);
                float[][] slices = SlicesOf(run);
                for (int w = 0; w < run.Count; w++)
                {
                    CliffWallDef d = run[w];
                    int n = d.Brow.Length;
                    int aspect = System.Array.IndexOf(CliffCatalog.Aspects, d.Aspect);
                    int baked = System.Array.IndexOf(CliffBaker.DefaultBatters, System.Array.IndexOf(CliffCatalog.Batters, d.Batter));
                    if (aspect < 0 || baked < 0)
                        throw new System.InvalidOperationException($"'{d.Id}' wears {d.Aspect} {d.Batter}, which the default bake does not carry.");
                    if (w > 0)
                    {
                        CliffWallDef p = run[w - 1];
                        int last = p.Brow.Length - 1;
                        // Exact, not Vector2's ==: that one forgives 1e-5 m.
                        if (!p.Brow[last].Equals(d.Brow[0]) || !p.Toe[last].Equals(d.Toe[0]) ||
                            p.DropMetres[last] != d.DropMetres[0] || p.ToeElevations[last] != d.ToeElevations[0] ||
                            !TextureBrowOf(p, last).Equals(TextureBrowOf(d, 0)))
                            throw new System.InvalidOperationException($"'{d.Id}' follows '{p.Id}' but does not start on its last station.");
                    }
                    for (int i = w == 0 ? 0 : 1; i < n; i++)
                        stations.Add(new Station
                        {
                            Sample = new CliffWallSample(d.Brow[i], d.Toe[i], d.DropMetres[i], d.ToeElevations[i]),
                            AspectIndex = aspect,
                            BatterIndex = baked,
                            Azimuth = CoastPlan.AspectAzimuths[aspect],
                            Class = d.Class,
                            Live = true,
                            CutBefore = w > 0 && i == 1,
                            SliceAt = slices[w][i],
                            TextureBrowPlan = d.TextureBrow != null && d.TextureBrow.Length > 0 ? d.TextureBrow[i] : (Vector2?)null,
                        });
                    owners.Add(d);
                }
            }

            List<Chunk> chunks = ChunksOf(stations, toeSpanMetres);
            if (chunks.Count != owners.Count)
                throw new System.InvalidOperationException($"the chunk math cut {owners.Count} walls into {chunks.Count} chunks.");
            for (int k = 0; k < chunks.Count; k++)
                if (chunks[k].Samples.Count != owners[k].Brow.Length)
                    throw new System.InvalidOperationException($"the chunk math cut '{owners[k].Id}' inside its own stations.");
            return chunks;
        }

        /// <summary>Each station's slice, Def by Def along a run: 0 where its k is whole, else how far its k is from the
        /// whole k before it to the whole k after it on its own wall. Checks each Def's station lines first.</summary>
        static float[][] SlicesOf(List<CliffWallDef> run)
        {
            foreach (CliffWallDef d in run)
            {
                int n = d.Brow == null ? 0 : d.Brow.Length;
                if (n < 2 || d.Toe == null || d.Toe.Length != n || d.DropMetres == null || d.DropMetres.Length != n ||
                    d.ToeElevations == null || d.ToeElevations.Length != n)
                    throw new System.InvalidOperationException($"'{d.Id}' needs two or more stations, each with a brow, a toe, a drop and a toe height.");
                if (d.Stations == null || d.Stations.Length != n || d.BrowFrom == null || d.BrowFrom.Length != n ||
                    d.ToeFrom == null || d.ToeFrom.Length != n)
                    throw new System.InvalidOperationException($"'{d.Id}' needs a k, and where its brow and toe came from, at each station.");
                if (d.TextureBrow != null && d.TextureBrow.Length != 0 && d.TextureBrow.Length != n)
                    throw new System.InvalidOperationException($"'{d.Id}' has {d.TextureBrow.Length} texture brows for {n} stations.");
            }
            var slices = new float[run.Count][];
            for (int w = 0; w < run.Count; w++)
            {
                CliffWallDef d = run[w];
                slices[w] = new float[d.Stations.Length];
                for (int i = 0; i < d.Stations.Length; i++)
                {
                    float k = d.Stations[i];
                    bool cut = Mathf.Floor(k) != k;
                    if ((d.BrowFrom[i] == CliffStationSource.Interpolated) != cut || (d.ToeFrom[i] == CliffStationSource.Interpolated) != cut)
                        throw new System.InvalidOperationException(
                            $"'{d.Id}' k{k.ToString(System.Globalization.CultureInfo.InvariantCulture)}: a station is interpolated where, and only where, its k is not whole.");
                    if (!cut) continue;
                    float before = WholeK(run, w, i, -1), after = WholeK(run, w, i, +1);
                    slices[w][i] = (k - before) / (after - before);
                }
            }
            return slices;
        }

        /// <summary>The whole k beside the run's Def <paramref name="w"/>'s station <paramref name="i"/>, back (-1) or on
        /// (+1): through its cut stations, and across a join into the Def its wall goes on in (the same wall's).</summary>
        static float WholeK(List<CliffWallDef> run, int w, int i, int dir)
        {
            CliffWallDef from = run[w];
            string root = RootOf(from);
            float k = from.Stations[i];
            string at = $"'{from.Id}' k{k.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            while (true)
            {
                int next = i + dir;
                if (next < 0 || next >= run[w].Stations.Length)
                {
                    int w2 = w + dir;
                    if (w2 < 0 || w2 >= run.Count)
                        throw new System.InvalidOperationException($"{at} is cut, with no whole station {(dir < 0 ? "before" : "after")} it in its run.");
                    CliffWallDef there = run[w2];
                    int join = dir < 0 ? there.Stations.Length - 1 : 0;     // the station both Defs hold
                    if (RootOf(there) != root || there.Stations[join] != run[w].Stations[i])
                        throw new System.InvalidOperationException($"{at} is cut, and its wall goes on in '{there.Id}', which does not hold it.");
                    w = w2;
                    next = join + dir;
                }
                i = next;
                float kk = run[w].Stations[i];
                if (Mathf.Floor(kk) != kk) continue;
                if (dir < 0 ? !(kk < k) : !(kk > k))
                    throw new System.InvalidOperationException(
                        $"{at} is cut, and the whole station {(dir < 0 ? "before" : "after")} it is k{kk.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
                return kk;
            }
        }

        /// <summary>The wall a Def was cut from (its <see cref="CliffWallDef.SplitFrom"/>), else its own: the wall its
        /// k count along.</summary>
        static string RootOf(CliffWallDef d) =>
            string.IsNullOrEmpty(d.SplitFrom) ? d.RealId : d.SplitFrom.Substring(System.Math.Max(0, d.SplitFrom.Length - 3));

        static Vector2 TextureBrowOf(CliffWallDef d, int i) =>
            d.TextureBrow != null && d.TextureBrow.Length > 0 ? d.TextureBrow[i] : d.Brow[i];

        static string LeastId(List<CliffWallDef> run)
        {
            string least = run[0].Id;
            foreach (CliffWallDef d in run) if (string.CompareOrdinal(d.Id, least) < 0) least = d.Id;
            return least;
        }

        /// <summary>A wall's scene name: its class, aspect and batter, and its real id.</summary>
        public static string WallName(CliffWallDef def) =>
            $"CliffWall_{def.Class}_{def.Aspect}_{def.Batter}_{def.RealId}";

        /// <summary>Every St Peters wall Def, live, retired and held, by id.</summary>
        public static List<CliffWallDef> LoadDefs()
        {
            var defs = new List<CliffWallDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CliffWallDef), new[] { WallsFolder }))
            {
                var d = AssetDatabase.LoadAssetAtPath<CliffWallDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null) defs.Add(d);
            }
            defs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return defs;
        }

        // =============================================================================================
        //  the scene's walls, read as text (pure; terrain PR 5w)
        // =============================================================================================

        /// <summary>One wall as the scene file holds it: its name and the parts its name says, its documents, its
        /// <see cref="CliffWallSurface"/>'s fields, where it stands and how it sorts, and the run it is in.</summary>
        public sealed class SceneWall
        {
            public string Name, RealId;
            public long GameObject, Transform, SortingGroup, Surface;
            public CoastClass Class;
            public int AspectIndex;             // into CliffCatalog.Aspects
            public int BatterIndex;             // into BakedBatterAngles()
            public Vector2[] Brow, Toe;
            public float[] DropMetres, ToeElevations;
            /// <summary>The slices (terrain PR 5w): each empty where the scene holds none.</summary>
            public Vector2[] TextureBrow;
            public float[] SliceAt;
            public CliffWallSliceEnd[] SliceBefore, SliceAfter;
            public float AlongOffsetMetres, RowsBasisSurfaceMetres, WallAzimuth, Batter;
            public Vector3 Position;
            public int SortingOrder;

            /// <summary>Its run, rebuilt from the scene: runs in the order of their least id.</summary>
            public int RunIndex;

            public List<CliffWallSample> Samples
            {
                get
                {
                    var s = new List<CliffWallSample>(Brow.Length);
                    for (int i = 0; i < Brow.Length; i++) s.Add(new CliffWallSample(Brow[i], Toe[i], DropMetres[i], ToeElevations[i]));
                    return s;
                }
            }

            /// <summary>The wall as the chunk math's chunk, its fields as the scene holds them.</summary>
            public Chunk AsChunk() => new Chunk
            {
                Samples = Samples,
                AspectIndex = AspectIndex,
                BatterIndex = BatterIndex,
                WallAzimuth = WallAzimuth,
                Class = Class,
                RunIndex = RunIndex,
                AlongOffsetMetres = AlongOffsetMetres,
                RunSurfaceMetres = RowsBasisSurfaceMetres,
                SliceAt = new List<float>(SliceAt != null && SliceAt.Length > 0 ? SliceAt : new float[Brow.Length]),
                TextureBrow = new List<Vector2>(TextureBrows),
                SliceBefore = SliceBefore ?? new CliffWallSliceEnd[0],
                SliceAfter = SliceAfter ?? new CliffWallSliceEnd[0],
            };

            /// <summary>The brows its texture runs along: <see cref="TextureBrow"/>, else its brows.</summary>
            public Vector2[] TextureBrows => TextureBrow != null && TextureBrow.Length > 0 ? TextureBrow : Brow;
        }

        /// <summary>
        /// The walls under the scene's <see cref="RootName"/>, read from the file's text (the scene is never opened),
        /// in run order: a wall whose first brow station is another's last follows it, and the runs go in the order
        /// of their least id. Refuses a name that does not parse, a wall that is not one GameObject with a transform,
        /// a sorting group and a <see cref="CliffWallSurface"/>, an id two walls share, and a join two walls claim.
        /// </summary>
        public static List<SceneWall> WallsInScene(string sceneText) =>
            InRuns(WallsUnder(StPetersLayerRefresh.SceneYaml.Parse(sceneText)));

        /// <summary>The walls under the scene's <see cref="RootName"/> in the scene's own order (the root's
        /// <c>m_Children</c>), not chained into runs; <see cref="SceneWall.RunIndex"/> is left at 0.</summary>
        public static List<SceneWall> WallsUnder(StPetersLayerRefresh.SceneYaml scene)
        {
            StPetersLayerRefresh.Doc rootTr = scene.TransformOf(scene.RootNamed(RootName));
            var walls = new List<SceneWall>();
            var byId = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (long childTr in rootTr.FieldRefs("m_Children"))
            {
                StPetersLayerRefresh.Doc tr = scene.Require(childTr, $"a child of '{RootName}'");
                StPetersLayerRefresh.Doc go = scene.Require(tr.FieldRef("m_GameObject"), $"the GameObject of &{childTr}");
                SceneWall w = ReadWall(scene, go, tr);
                if (!byId.Add(w.RealId)) throw new System.InvalidOperationException($"two walls are {w.RealId}.");
                walls.Add(w);
            }
            return walls;
        }

        /// <summary>The scene's walls as the chunk math's chunks, in run order (<see cref="WallsInScene"/>).</summary>
        public static List<Chunk> ChunksInScene(string sceneText)
        {
            var chunks = new List<Chunk>();
            foreach (SceneWall w in WallsInScene(sceneText)) chunks.Add(w.AsChunk());
            return chunks;
        }

        static SceneWall ReadWall(StPetersLayerRefresh.SceneYaml scene, StPetersLayerRefresh.Doc go, StPetersLayerRefresh.Doc tr)
        {
            string name = go.Field("m_Name") ?? "";
            string[] parts = name.Split('_');
            int catalogBatter = parts.Length == 5 ? System.Array.IndexOf(CliffCatalog.Batters, parts[3]) : -1;
            var w = new SceneWall
            {
                Name = name,
                RealId = parts.Length == 5 ? parts[4] : "",
                GameObject = go.FileId,
                Transform = tr.FileId,
                AspectIndex = parts.Length == 5 ? System.Array.IndexOf(CliffCatalog.Aspects, parts[2]) : -1,
                BatterIndex = System.Array.IndexOf(CliffBaker.DefaultBatters, catalogBatter),
            };
            if (parts.Length != 5 || parts[0] != "CliffWall" || !System.Enum.IsDefined(typeof(CoastClass), parts[1]) ||
                w.AspectIndex < 0 || w.BatterIndex < 0 || w.RealId.Length != 3 || !IsDigits(w.RealId))
                throw new System.InvalidOperationException($"'{name}' is not a CliffWall_<class>_<aspect>_<batter>_<NNN> name.");
            w.Class = (CoastClass)System.Enum.Parse(typeof(CoastClass), parts[1]);

            StPetersLayerRefresh.Doc surface = null, group = null;
            foreach (StPetersLayerRefresh.Doc c in scene.ComponentsOf(go))
            {
                if (c.ClassId == SortingGroupClass) group = c;
                else if (c.IsScript(typeof(CliffWallSurface).FullName)) surface = c;
            }
            if (surface == null || group == null)
                throw new System.InvalidOperationException($"'{name}' needs a CliffWallSurface and a SortingGroup.");
            w.Surface = surface.FileId;
            w.SortingGroup = group.FileId;
            w.SortingOrder = StPetersLayerRefresh.ParseInt(group.Field("m_SortingOrder"));
            w.Position = StPetersLayerRefresh.ParseVec3(tr.Field("m_LocalPosition"));
            w.Brow = Vec2List(surface, "_browPlan");
            w.Toe = Vec2List(surface, "_toePlan");
            w.DropMetres = FloatList(surface, "_dropMetres");
            w.ToeElevations = FloatList(surface, "_toeElevations");
            w.AlongOffsetMetres = StPetersLayerRefresh.ParseFloat(surface.Field("_alongOffsetMetres"));
            w.RowsBasisSurfaceMetres = StPetersLayerRefresh.ParseFloat(surface.Field("_rowsBasisSurfaceMetres"));
            w.WallAzimuth = StPetersLayerRefresh.ParseFloat(surface.Field("_wallAzimuth"));
            w.Batter = StPetersLayerRefresh.ParseFloat(surface.Field("_batter"));
            int n = w.Brow.Length;
            if (n < 2 || w.Toe.Length != n || w.DropMetres.Length != n || w.ToeElevations.Length != n)
                throw new System.InvalidOperationException($"'{name}' holds {n} brow, {w.Toe.Length} toe, {w.DropMetres.Length} drop and " +
                                                           $"{w.ToeElevations.Length} toe height stations.");
            // The slices are optional: a wall saved before the re-cut has none, and reads as whole.
            w.TextureBrow = Vec2List(surface, "_textureBrowPlan", optional: true);
            w.SliceAt = FloatList(surface, "_sliceAt", optional: true);
            w.SliceBefore = SliceEndList(surface, "_sliceBefore");
            w.SliceAfter = SliceEndList(surface, "_sliceAfter");
            if ((w.TextureBrow.Length != 0 && w.TextureBrow.Length != n) || (w.SliceAt.Length != 0 && w.SliceAt.Length != n) ||
                w.SliceBefore.Length > 1 || w.SliceAfter.Length > 1)
                throw new System.InvalidOperationException($"'{name}' holds {w.TextureBrow.Length} texture brows, {w.SliceAt.Length} slices, " +
                                                           $"{w.SliceBefore.Length} and {w.SliceAfter.Length} slice ends for {n} stations.");
            return w;
        }

        /// <summary>A SortingGroup's YAML class id.</summary>
        public const int SortingGroupClass = 210;

        static bool IsDigits(string s)
        {
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        /// <summary>The entries of one of a document's own sequence fields: the lines "  - …" under "  key:". A field
        /// that is <paramref name="optional"/> and not there reads as empty.</summary>
        static List<string> ListOf(StPetersLayerRefresh.Doc d, string key, bool optional = false)
        {
            var values = new List<string>();
            string[] lines = d.Lines;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == "  " + key + ": []") return values;
                if (lines[i] != "  " + key + ":") continue;
                for (int j = i + 1; j < lines.Length && lines[j].StartsWith("  - ", System.StringComparison.Ordinal); j++)
                    values.Add(lines[j].Substring(4));
                return values;
            }
            if (optional) return values;
            throw new System.InvalidOperationException($"no '{key}' in {lines[0]}.");
        }

        /// <summary>A sequence of <see cref="CliffWallSliceEnd"/>s: each "  - BrowPlan: …" and the "    Key: …" lines under
        /// it. Not there reads as none.</summary>
        static CliffWallSliceEnd[] SliceEndList(StPetersLayerRefresh.Doc d, string key)
        {
            var ends = new List<CliffWallSliceEnd>();
            string[] lines = d.Lines;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == "  " + key + ": []") break;
                if (lines[i] != "  " + key + ":") continue;
                var fields = new Dictionary<string, string>(System.StringComparer.Ordinal);
                for (int j = i + 1; j <= lines.Length; j++)
                {
                    bool item = j < lines.Length && lines[j].StartsWith("  - ", System.StringComparison.Ordinal);
                    bool more = j < lines.Length && lines[j].StartsWith("    ", System.StringComparison.Ordinal);
                    if ((item || !more) && fields.Count > 0)
                    {
                        ends.Add(SliceEndOf(fields, key));
                        fields.Clear();
                    }
                    if (!item && !more) break;
                    string entry = lines[j].Substring(4);
                    int colon = entry.IndexOf(": ", System.StringComparison.Ordinal);
                    if (colon < 0) throw new System.InvalidOperationException($"'{entry}' in {key} is not a field.");
                    fields[entry.Substring(0, colon)] = entry.Substring(colon + 2);
                }
                break;
            }
            return ends.ToArray();
        }

        static CliffWallSliceEnd SliceEndOf(Dictionary<string, string> fields, string key)
        {
            string Field(string name) =>
                fields.TryGetValue(name, out string v) ? v : throw new System.InvalidOperationException($"a slice end in {key} has no {name}.");
            return new CliffWallSliceEnd(Vec2Of(Field("BrowPlan"), key), Vec2Of(Field("ToePlan"), key),
                                         StPetersLayerRefresh.ParseFloat(Field("DropMetres")),
                                         StPetersLayerRefresh.ParseFloat(Field("ToeElevation")),
                                         Vec2Of(Field("TextureBrowPlan"), key));
        }

        static Vector2 Vec2Of(string value, string key)
        {
            var m = Vec2Rx.Match(value);
            if (!m.Success) throw new System.InvalidOperationException($"'{value}' in {key} is not a vector.");
            return new Vector2(StPetersLayerRefresh.ParseFloat(m.Groups[1].Value), StPetersLayerRefresh.ParseFloat(m.Groups[2].Value));
        }

        static readonly System.Text.RegularExpressions.Regex Vec2Rx =
            new System.Text.RegularExpressions.Regex(@"^\{x: ([^,]+), y: ([^}]+)\}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        static Vector2[] Vec2List(StPetersLayerRefresh.Doc d, string key, bool optional = false)
        {
            List<string> values = ListOf(d, key, optional);
            var v = new Vector2[values.Count];
            for (int i = 0; i < v.Length; i++) v[i] = Vec2Of(values[i], key);
            return v;
        }

        static float[] FloatList(StPetersLayerRefresh.Doc d, string key, bool optional = false)
        {
            List<string> values = ListOf(d, key, optional);
            var v = new float[values.Count];
            for (int i = 0; i < v.Length; i++) v[i] = StPetersLayerRefresh.ParseFloat(values[i]);
            return v;
        }

        /// <summary>The walls in run order: each run a chain of walls, one starting where the last ended, to the bit;
        /// the runs in the order of their least id.</summary>
        static List<SceneWall> InRuns(List<SceneWall> walls)
        {
            var endingAt = new Dictionary<Vector2, SceneWall>();
            foreach (SceneWall w in walls)
            {
                Vector2 last = w.Brow[w.Brow.Length - 1];
                if (endingAt.ContainsKey(last))
                    throw new System.InvalidOperationException($"{endingAt[last].Name} and {w.Name} end on the same station.");
                endingAt.Add(last, w);
            }
            var after = new Dictionary<SceneWall, SceneWall>();
            var starts = new List<SceneWall>();
            foreach (SceneWall w in walls)
            {
                if (endingAt.TryGetValue(w.Brow[0], out SceneWall before) && before != w)
                {
                    if (after.ContainsKey(before))
                        throw new System.InvalidOperationException($"{after[before].Name} and {w.Name} both start where {before.Name} ends.");
                    after.Add(before, w);
                }
                else starts.Add(w);
            }
            var runs = new List<List<SceneWall>>();
            int walked = 0;
            foreach (SceneWall s in starts)
            {
                var run = new List<SceneWall>();
                for (SceneWall w = s; w != null; w = after.TryGetValue(w, out SceneWall n) ? n : null) run.Add(w);
                walked += run.Count;
                runs.Add(run);
            }
            if (walked != walls.Count)
                throw new System.InvalidOperationException($"{walls.Count - walked} walls join one another in a ring.");
            runs.Sort((a, b) => string.CompareOrdinal(LeastRealId(a), LeastRealId(b)));
            var ordered = new List<SceneWall>(walls.Count);
            for (int r = 0; r < runs.Count; r++)
                foreach (SceneWall w in runs[r]) { w.RunIndex = r; ordered.Add(w); }
            return ordered;
        }

        static string LeastRealId(List<SceneWall> run)
        {
            string least = run[0].RealId;
            foreach (SceneWall w in run) if (string.CompareOrdinal(w.RealId, least) < 0) least = w.RealId;
            return least;
        }

        /// <summary>How far down the face, in surface metres, the soil band ends and the rock begins, at
        /// a baked batter (see <see cref="TryLoadBands"/>).</summary>
        public static float OverburdenSurfaceMetresOf(int bakedBatterIndex) =>
            CliffWallGeometry.OverburdenSurfaceMetres(
                OverburdenMetres, CliffCatalog.BatterAngles[BakedBatterToCatalogIndex(bakedBatterIndex)]);

        /// <summary>
        /// The wall standing at a bearing, or false where none does.
        ///
        /// <para>The class is read UNFEATHERED (<see cref="TidalTerrain.CoastClassAt"/> — what the plan
        /// SAYS), but the heights come off the FEATHERED profile, and that asymmetry is deliberate: the
        /// plan decides where rock is, the blend decides how tall it is there. Runs are extended by
        /// <see cref="RunOverrunDegrees"/> past their sector so the taper has somewhere to happen.</para>
        /// </summary>
        static bool TryStationAt(TidalTerrain terrain, float bearing, out CliffWallSample sample,
                                 out CoastClass cls, out float azimuth)
        {
            sample = default;
            azimuth = 0f;
            cls = NearestCliffClass(terrain, bearing);
            if (cls == CoastClass.Beach) return false;                  // no cliff within the overrun

            Vector2 brow = ShorePoint(bearing);

            // ⚠⚠ THE FACE RUNS DOWN THE COAST'S OUTWARD NORMAL, NOT OUTWARD ALONG THE BEARING.
            //
            // CoastPlan's own standing warning says the aspect law is a constraint on the NORMAL and
            // that on an ellipse the two are not the same angle. The first version of this method took
            // the toe from ShorePointOut — a RADIAL step in the normalised frame — while snapping the
            // aspect from the true normal, and near the island's shoulder those disagree by 20°: the
            // wall was drawn skewed to the coast it stands on, wearing a face chosen for a different
            // direction. Caught by the chunk-aspect pin, which found a chunk drawn facing 98.5° in a
            // 135° face. So the plan run is measured ALONG the normal, and the azimuth is then read back
            // off the finished sample — one direction, one source, nothing to drift.
            azimuth = CoastPlan.OutwardNormalAzimuth(bearing, StPetersBuilder.IslandRadius,
                                                     StPetersBuilder.IslandRadiusY);
            float a = azimuth * Mathf.Deg2Rad;
            var normal = new Vector2(Mathf.Sin(a), Mathf.Cos(a));

            // How far along that normal the plunge's END lies. The plunge width is authored in
            // ELLIPTICAL distance (that is the frame the profile is written in), so the world run is
            // whatever it takes to get there — shorter on the flanks, longer at the ends.
            float targetDistance = StPetersBuilder.IslandRadius + StPetersBuilder.CliffPlungeWidth;
            if (!TryMarchToDistance(brow, normal, targetDistance, out Vector2 toe)) return false;

            float browElevation = terrain.IslandProfileAt(StPetersBuilder.IslandRadius, brow);
            float toeElevation = terrain.IslandProfileAt(targetDistance, toe);

            float drop = browElevation - toeElevation;
            if (drop < MinFaceDropMetres) return false;

            // The toe's ABSOLUTE elevation travels with the sample: the waterline (2026-08-06) has to
            // compare the face against the sea's own level, and a drop is only a difference. Same
            // analytic profile, same call — nothing is re-derived downstream.
            sample = new CliffWallSample(brow, toe, drop, toeElevation);
            // The batter law (see MinBatterDegrees): a face the kit cannot draw honestly is not drawn.
            if (CliffWallGeometry.BatterDegrees(in sample) < MinBatterDegrees) return false;
            return true;
        }

        /// <summary>
        /// Walk out from <paramref name="from"/> along <paramref name="direction"/> until the island's
        /// ELLIPTICAL distance reaches <paramref name="targetDistance"/>, and hand back that world point.
        ///
        /// <para>A march rather than a closed form because the closed form is a quadratic whose two roots
        /// have to be discriminated by hand, and this runs a few thousand times in an editor build. The
        /// step is fixed and the refinement is linear, so it is deterministic to the bit (rule 5).</para>
        /// </summary>
        static bool TryMarchToDistance(Vector2 from, Vector2 direction, float targetDistance,
                                       out Vector2 hit)
        {
            const float Step = 0.05f;
            float limit = StPetersBuilder.CliffPlungeWidth * 4f + 1f;   // the normal cannot be slower than this
            float previous = Distance(from);
            for (float L = Step; L <= limit; L += Step)
            {
                Vector2 p = from + direction * L;
                float d = Distance(p);
                if (d >= targetDistance)
                {
                    float span = d - previous;
                    float t = span > 1e-6f ? (targetDistance - previous) / span : 0f;
                    hit = from + direction * (L - Step * (1f - t));
                    return true;
                }
                previous = d;
            }
            hit = from;
            return false;
        }

        static float Distance(Vector2 p) =>
            TidalTerrain.IslandDistance(p, StPetersBuilder.IslandCenter,
                                        StPetersBuilder.IslandRadius, StPetersBuilder.IslandRadiusY);

        /// <summary>
        /// The cliff class in force at a bearing, allowing the run to overrun its sector by
        /// <see cref="RunOverrunDegrees"/> on each side. Returns <see cref="CoastClass.Beach"/> for "no
        /// wall here" — beach is never a wall, so it doubles as the sentinel without a nullable.
        /// </summary>
        static CoastClass NearestCliffClass(TidalTerrain terrain, float bearing)
        {
            CoastClass here = terrain.CoastClassAt(ShorePoint(bearing));
            if (CoastPlan.IsCliff(here)) return here;

            // Just outside a cliff sector: adopt the neighbouring wall's class so the run continues into
            // the feather, where the blended profile tapers its drop away to nothing.
            //
            // ⚠ EVERY TERM HERE IS DEGREES. The first draft started this sweep at StationMetres, which
            // is a length — it read fine because 0.25 m and 0.25° are both small, and it would have gone
            // on reading fine after a station-spacing change silently moved the overrun.
            const float SweepStepDegrees = 0.25f;
            for (float d = SweepStepDegrees; d <= RunOverrunDegrees; d += SweepStepDegrees)
            {
                if (CoastPlan.IsCliff(terrain.CoastClassAt(ShorePoint(bearing - d))))
                    return terrain.CoastClassAt(ShorePoint(bearing - d));
                if (CoastPlan.IsCliff(terrain.CoastClassAt(ShorePoint(bearing + d))))
                    return terrain.CoastClassAt(ShorePoint(bearing + d));
            }
            return CoastClass.Beach;
        }

        /// <summary>Index of the kit aspect nearest an azimuth. The aspect law is a snap to one of five
        /// authored facings; <c>StPetersCoastTests</c> already guarantees no cliff metre is further than
        /// the tolerance from one, so this cannot silently pick a face the coast may not wear.</summary>
        public static int SnapAspectIndex(float azimuthDegrees)
        {
            int best = 0;
            float bestError = float.MaxValue;
            for (int i = 0; i < CoastPlan.AspectAzimuths.Length; i++)
            {
                float e = Mathf.Abs(Mathf.DeltaAngle(azimuthDegrees, CoastPlan.AspectAzimuths[i]));
                if (e < bestError) { bestError = e; best = i; }
            }
            return best;
        }

        /// <summary>The batter angles a checkout actually carries — <c>CliffBaker.DefaultBatters</c>, not
        /// all four. <c>bank</c> (48°) is deliberately not baked, so snapping to it would pick a texture
        /// that is not on disk and leave a hole in the coast.</summary>
        public static float[] BakedBatterAngles()
        {
            var angles = new float[CliffBaker.DefaultBatters.Length];
            for (int i = 0; i < angles.Length; i++)
                angles[i] = CliffCatalog.BatterAngles[CliffBaker.DefaultBatters[i]];
            return angles;
        }

        /// <summary>Map an index into <see cref="BakedBatterAngles"/> back to a <c>CliffCatalog.Batters</c>
        /// index, which is what the file names are built from.</summary>
        public static int BakedBatterToCatalogIndex(int bakedIndex) =>
            CliffBaker.DefaultBatters[Mathf.Clamp(bakedIndex, 0, CliffBaker.DefaultBatters.Length - 1)];

        // =============================================================================================
        //  the scene
        // =============================================================================================

        /// <summary>
        /// Build (or rebuild) the region's cliff walls under a single <see cref="RootName"/> object, from
        /// its walls' Defs (<see cref="LoadDefs"/>): each live Def is one chunk, named by its real id.
        /// Destroys any previous root first, so a builder Refresh converges: hand-placed decor elsewhere
        /// in the scene is untouched, and running twice gives the same coast as running once.
        ///
        /// <para>⚠ It draws what the Defs say, which is what the scene holds (the round trip in
        /// <c>StPetersWallsAgreeWithGroundTests</c>), and never the analytic walk: that walk's 79 walls
        /// stood on the old ground, and a rebuild that drew them would undo every wall the import moved.</para>
        /// </summary>
        public static int Build(IReadOnlyList<CliffWallDef> defs)
        {
            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            // Cut where the plan says (St Peters' 1 m), so Defs cut for it are refused if they no longer meet it.
            List<Chunk> chunks = ChunksOfDefs(defs, StPetersTerrainPlan.LoadPlan().WallToeSpanMetres,
                                              out List<CliffWallDef> owners);
            if (chunks.Count == 0) return 0;

            var material = AssetDatabase.LoadAssetAtPath<Material>(CliffFaceMat);
            if (material == null)
            {
                Debug.LogError($"[cliff-walls] {CliffFaceMat} is missing — the coast will build without " +
                               "faces. The material is committed; its TEXTURES are not (the bake root " +
                               "is gitignored by design).");
                return 0;
            }

            // The colour slot's file by the material's look — v10 colour, or the px index the bake
            // moved into it keeping the GUID. Asked once per build, of the catalog, never spelled here.
            string colour = CliffCatalog.ColourChannel(CliffBakeMenu.IsPxLook);

            var root = new GameObject(RootName);
            int built = 0, stratified = 0, browDecals = 0, toeDecals = 0;
            for (int k = 0; k < chunks.Count; k++)
            {
                Chunk chunk = chunks[k];
                if (!TryLoadBands(chunk, colour, out CliffFaceBand[] bands, out Texture2D profile))
                    continue;
                if (bands.Length > 1) stratified++;

                LoadDecals(chunk, out Texture2D browStrip, out Texture2D toeStrip);
                if (browStrip != null) browDecals++;
                if (toeStrip != null) toeDecals++;

                ChunkFields fields = FieldsOf(in chunk);
                Vector2[] brow = fields.BrowPlan;

                var go = new GameObject(WallName(owners[k]));
                go.transform.SetParent(root.transform, worldPositionStays: false);
                // Park the chunk at its own first brow so its vertices stay small and local — a mesh
                // authored at absolute world coordinates 200 m from the origin loses float precision in
                // exactly the sub-texel range the kit's 32 px/m relies on.
                go.transform.position = new Vector3(brow[0].x, brow[0].y, 0f);

                var surface = go.AddComponent<CliffWallSurface>();
                surface.Configure(brow, fields.ToePlan, fields.DropMetres, material, bands, profile,
                                  browStrip, toeStrip,
                                  fields.AlongOffsetMetres, fields.RowsBasisSurfaceMetres,
                                  fields.WallAzimuth,
                                  fields.Batter,
                                  CliffCatalog.AspectBakeLights[chunk.AspectIndex],
                                  CliffCatalog.FaceMetresS, CliffCatalog.FaceMetresT,
                                  CliffCatalog.ProfileSubdivideMetres, CliffCatalog.ProfileMetres,
                                  CliffCatalog.StripMetresT, CliffCatalog.BrowLineAt,
                                  fields.ToeElevations,
                                  fields.TextureBrowPlan, fields.SliceAt, fields.SliceBefore, fields.SliceAfter);
                built++;
            }

            int runs = 0;
            foreach (Chunk c in chunks) runs = Mathf.Max(runs, c.RunIndex + 1);
            Debug.Log($"[cliff-walls] {built} chunks of standing cliff over {runs} runs " +
                      $"(stations every {StationMetres} m, chunks up to {ChunkMetres} m). " +
                      $"{stratified} carry a {OverburdenMetres} m topsoil band over the rock; " +
                      $"{browDecals} brow and {toeDecals} toe decals seat their ends. " +
                      "Sorted into the decor band by each chunk's own toe (ADR 0032); `u` and the row " +
                      "count run continuously across every cut, so a chunk boundary is invisible.");
            return built;
        }

        /// <summary>
        /// ⭐ <b>THE STRATA.</b> A face's rock bands, brow-downward: the eroded topsoil, then the
        /// sandstone under it. One profile serves both — it is the LANDFORM's displacement and the bands
        /// are materials lying on it, so giving each its own would tear them apart at the horizon.
        ///
        /// <para><b>The fallback is deliberate and it is not silent.</b> If the till bake is missing, the
        /// face falls back to ONE sandstone band covering the whole wall rather than leaving the top
        /// 1.8 m empty — a hole in the coast is a far worse failure than an unstratified cliff, and it is
        /// the failure a naive "skip the band you cannot load" would produce.</para>
        /// </summary>
        static bool TryLoadBands(Chunk chunk, string colour, out CliffFaceBand[] bands, out Texture2D profile)
        {
            bands = new CliffFaceBand[0];
            int catalogBatter = BakedBatterToCatalogIndex(chunk.BatterIndex);
            string aspect = CliffCatalog.Aspects[chunk.AspectIndex];

            // ⚠ THE ONE PLACE THE DISPLACEMENT STILL STEPS, AND IT IS MEASURED RATHER THAN ASSUMED.
            //
            // The profile depends on rock and BATTER, so where the coast changes batter mid-run the map
            // swaps and the silhouette steps — the only survivor of the B3 fix, which made `u` continuous
            // everywhere else. It happens at 6 of the coast's 76 cuts, and it is left alone deliberately:
            // measured off the rig, adjacent baked batters differ by RMS 0.045 m and at most 0.113 m
            // (correlation 0.985 — the same ribs, respaced), against the RMS 0.40 m / 1.10 m the u defect
            // was tearing open. A centimetres-wide step at six places where the ROCK TEXTURE also changes
            // — the kit's own break-up mechanism — is not worth the cure, which would be pinning one
            // batter's profile across a whole run and thereby putting the silhouette's ribs somewhere the
            // face's own shading does not agree they are.
            profile = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Profile)}/" +
                $"{CliffCatalog.ProfileName(Rock, catalogBatter)}.png");

            if (!TryLoadFaceSet(Rock, aspect, catalogBatter, colour, out CliffFaceBand rock))
            {
                Debug.LogWarning(
                    $"[cliff-walls] no baked face for {Rock} {aspect} " +
                    $"{CliffCatalog.Batters[catalogBatter]} — that stretch of coast will not stand up. " +
                    "The kit ships as the rig; run the builder (it bakes on missing) or " +
                    "'Hidden Harbours ▸ Dev ▸ Bake Cliff Face Kit'.");
                return false;
            }

            // The soil horizon is a vertical depth; the face is addressed along its own surface, so the
            // conversion uses the SNAPPED batter — the angle the pixels were actually baked at, not the
            // station's true one, or the band would sit at a depth the texture disagrees with.
            float overburden = OverburdenSurfaceMetresOf(chunk.BatterIndex);

            if (!TryLoadFaceSet(OverburdenRock, aspect, catalogBatter, colour, out CliffFaceBand soil))
            {
                Debug.LogWarning(
                    $"[cliff-walls] no baked {OverburdenRock} face for {aspect} " +
                    $"{CliffCatalog.Batters[catalogBatter]} — this face will stand as bare {Rock} with " +
                    "no topsoil band. Re-run the builder (it bakes on missing) or " +
                    $"'Hidden Harbours ▸ Dev ▸ Bake Cliff Face Kit — {OverburdenRock}'.");
                rock.Label = "Rock";
                bands = new[] { rock };
                return true;
            }

            soil.Label = "Overburden";
            soil.StartSurfaceMetres = 0f;
            soil.EndSurfaceMetres = overburden;
            rock.Label = "Rock";
            rock.StartSurfaceMetres = overburden;
            rock.EndSurfaceMetres = 0f;             // ...to the toe
            bands = new[] { soil, rock };
            return true;
        }

        static bool TryLoadFaceSet(string rock, string aspect, int catalogBatter, string colour,
                                   out CliffFaceBand band)
        {
            band = CliffFaceBand.WholeFace(
                LoadFace(rock, aspect, catalogBatter, colour),
                LoadFace(rock, aspect, catalogBatter, "_normal"),
                LoadFace(rock, aspect, catalogBatter, "_mask"),
                rock);
            return band.HasChannels;
        }

        /// <summary>
        /// The kit's own finisher for a face's ends: the hanging sod lip at the brow, and the sea's
        /// undercut plus the salt-bleached basal beds at the toe.
        ///
        /// <para><b>⚠ Loaded as TEXTURES, not as sprites.</b> The strips import as
        /// <c>SpriteImportMode.Multiple</c> (the kit's own contract — they have silhouettes and were
        /// meant to sort), and a Multiple-mode asset that nobody has sliced carries ZERO sprites, so
        /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> would hand back null on a freshly baked checkout. The
        /// wall consumes them on a mesh that follows the coast rather than as flat quads, so the
        /// <see cref="Texture2D"/> — which is the asset's main object either way — is the honest handle
        /// and the import contract is left exactly as the kit states it.</para>
        ///
        /// <para>Missing decals are NOT an error: the wall stands without them and the warning for a
        /// missing face already covers an unbaked checkout.</para>
        /// </summary>
        static void LoadDecals(Chunk chunk, out Texture2D brow, out Texture2D toe)
        {
            int catalogBatter = BakedBatterToCatalogIndex(chunk.BatterIndex);
            string aspect = CliffCatalog.Aspects[chunk.AspectIndex];
            string folder = CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Strip);

            brow = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{folder}/Brow_{CliffCatalog.BrowName(aspect, CliffCatalog.BaseStep)}.png");

            // A wall or a steep face is undercut into a notch; a ramp keeps its debris and slumps. The
            // kit pairs them by batter and the coast's batter is already snapped, so this cannot pick a
            // feature the bake did not write.
            string feature = CliffCatalog.ToeFeatureForBatter[catalogBatter];
            toe = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{folder}/Toe_{CliffCatalog.ToeName(aspect, CliffCatalog.BaseStep, feature)}.png");
        }

        static Texture2D LoadFace(string rock, string aspect, int catalogBatter, string channel) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Face)}/" +
                $"{CliffCatalog.FaceName(rock, aspect, catalogBatter, CliffCatalog.BaseStep, channel)}.png");

        // =============================================================================================
        //  the ellipse (mirrors StPetersCoastTests' own measures — same frame, same three functions)
        // =============================================================================================

        /// <summary>A point on the plateau-edge ellipse at a bearing — where the brow stands.</summary>
        public static Vector2 ShorePoint(float bearingDegrees)
        {
            float r = bearingDegrees * Mathf.Deg2Rad;
            return StPetersBuilder.IslandCenter
                   + new Vector2(StPetersBuilder.IslandRadius * Mathf.Sin(r),
                                 StPetersBuilder.IslandRadiusY * Mathf.Cos(r));
        }

        // ⚠ THERE IS DELIBERATELY NO ShorePointOut HERE. StPetersCoastTests has one — a RADIAL step
        // outward in the normalised frame — and reaching for the same construction is what put the first
        // draft of this generator 20° off the coast's outward normal. A wall goes out along the NORMAL;
        // see TryMarchToDistance.

        /// <summary>Arc length per radian of bearing — the ellipse's own speed, so a constant step of
        /// SHORE can be walked in bearing.</summary>
        public static float ShoreSpeed(float bearingDegrees)
        {
            float r = bearingDegrees * Mathf.Deg2Rad;
            return new Vector2(StPetersBuilder.IslandRadius * Mathf.Cos(r),
                               StPetersBuilder.IslandRadiusY * Mathf.Sin(r)).magnitude;
        }
    }
}
