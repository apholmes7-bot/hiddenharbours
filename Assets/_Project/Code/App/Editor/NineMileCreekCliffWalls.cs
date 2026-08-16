#if UNITY_EDITOR
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
    /// ⭐ <b>THE MAINLAND COAST STANDS UP.</b> Turns Nine Mile Creek's authored coast plan into standing
    /// cliff geometry — the same Cliff Face kit, the same <see cref="CliffWallSurface"/>, the same decor
    /// sorting — for a coast that is an open RUN rather than a ring.
    ///
    /// <para><b>⚠ ONE LAW, TWO CONSUMERS.</b> Nothing here decides where a cliff is or how tall it stands.
    /// The walls are sampled off <see cref="MainlandTidalTerrain.ProfileAt"/> — the frame-split twin of the
    /// method the walk gate and the seabed bake read — and the class comes from
    /// <see cref="MainlandTidalTerrain.CoastClassAt"/>, which
    /// <see cref="NineMileCreekShoreMap.IsRockCoastAt"/> and the ground paint already read. Retune
    /// <see cref="NineMileCreekMainland.CoastSectors"/> or <see cref="NineMileCreekMainland.CliffPlungeWidth"/>
    /// and the walls move with them without a line changing here.</para>
    ///
    /// <para><b>⭐ WHAT A RUN MAKES SIMPLER THAN AN ELLIPSE.</b> <see cref="StPetersCliffWalls"/> has to
    /// walk in BEARING and convert to a constant step of shore, because a degree buys different amounts of
    /// coast around an ellipse; and it has to MARCH out along the normal because the island's plunge width
    /// is authored in elliptical distance. Neither applies here. A polyline's arc length IS the parameter,
    /// so the stations are evenly spaced by construction, and this coast's plunge is authored in plain
    /// world metres seaward — so the toe is one multiply, exactly. The whole 60-line marching apparatus
    /// that file needs is simply absent, and that is a property of the landform rather than a shortcut.</para>
    ///
    /// <para><b>⚠ AND THE ONE THING THAT IS HARDER.</b> An open run has ENDS, and a polyline has CORNERS.
    /// A wall must not be drawn past either end of the run (there is no coast there to stand on), and at a
    /// vertex the outward normal STEPS — the two segments meeting there have different bearings, so a
    /// station either side of a corner can snap to different kit aspects. That is handled the way the
    /// texture change is handled everywhere else: it cuts a chunk. See <see cref="ResolveChunks"/>.</para>
    /// </summary>
    public static class NineMileCreekCliffWalls
    {
        /// <summary>The scene object every generated chunk parents under. Rebuilding destroys it whole,
        /// which is what makes a Refresh converge rather than stack a second coast on the first.</summary>
        public const string RootName = "CliffWalls";

        /// <inheritdoc cref="StPetersCliffWalls.CliffFaceMat"/>
        public const string CliffFaceMat = StPetersCliffWalls.CliffFaceMat;

        /// <inheritdoc cref="StPetersCliffWalls.StationMetres"/>
        public const float StationMetres = StPetersCliffWalls.StationMetres;

        /// <inheritdoc cref="StPetersCliffWalls.ChunkMetres"/>
        public const float ChunkMetres = StPetersCliffWalls.ChunkMetres;

        /// <inheritdoc cref="StPetersCliffWalls.ChunkToeSpanMetres"/>
        public const float ChunkToeSpanMetres = StPetersCliffWalls.ChunkToeSpanMetres;

        /// <inheritdoc cref="StPetersCliffWalls.MinFaceDropMetres"/>
        public const float MinFaceDropMetres = StPetersCliffWalls.MinFaceDropMetres;

        /// <inheritdoc cref="StPetersCliffWalls.MinBatterDegrees"/>
        public static float MinBatterDegrees => StPetersCliffWalls.MinBatterDegrees;

        /// <summary>
        /// Metres of RUN the generator carries a wall past each cliff sector's ends, so a run tapers out
        /// through the blend instead of stopping at the unfeathered class boundary.
        ///
        /// <para><b>⚠ METRES, and St Peters' equivalent is DEGREES.</b> That file's own comment records
        /// the bug this shape prevents — its first draft used a length where a bearing was wanted and read
        /// fine because 0.25 m and 0.25° are both small. On a run the plan's feather is already in metres
        /// (<see cref="NineMileCreekMainland.CoastFeatherMetres"/>), so there is no conversion to get
        /// wrong; it is mirrored rather than re-chosen.</para>
        /// </summary>
        public const float RunOverrunMetres = NineMileCreekMainland.CoastFeatherMetres;

        /// <summary>The rock this coast is built from. Nine Mile Creek is the same low RED SANDSTONE coast
        /// as the island across the bar — the owner's photographs are of red banks — so it takes the kit's
        /// shipped default, and the till overburden over it.</summary>
        public const string Rock = StPetersCliffWalls.Rock;

        /// <inheritdoc cref="StPetersCliffWalls.OverburdenRock"/>
        public const string OverburdenRock = StPetersCliffWalls.OverburdenRock;

        /// <summary>
        /// The soil horizon on this coast's faces, in metres of TRUE HEIGHT.
        ///
        /// <para><b>⭐ IT IS THIS REGION'S OWN MEADOW BAND, and it happens to equal the island's.</b> St
        /// Peters derives 1.8 m from "the plateau stands at 6 m and grass reaches down to 4.2 m". Nine Mile
        /// Creek's fields stand at <see cref="NineMileCreekMainland.LandElevation"/> (6 m) and its grass
        /// reaches down to <see cref="NineMileCreekShoreMap.GrassFloorElevation"/> (4.2 m) — the same two
        /// numbers, from this region's own plan. Derived rather than copied, so a re-tune of either moves
        /// the soil on the face with it.</para>
        ///
        /// <para><b>⚠ St Peters deliberately does NOT derive it</b>, and states why: the owner still owes a
        /// ruling on how far grass may reach over a brow, and that ruling must be free to move the island's
        /// GRASS without repainting every island cliff. That reasoning is about a ruling on ST PETERS. This
        /// coast is 27% cliff against a 93% ceiling and its bands were authored together in one pass, so
        /// the coupling is the honest one here — and if the ruling lands the other way, this is one line.</para>
        /// </summary>
        public static float OverburdenMetres =>
            NineMileCreekMainland.LandElevation - NineMileCreekShoreMap.GrassFloorElevation;

        /// <summary>One chunk's worth of resolved geometry — the same shape
        /// <see cref="StPetersCliffWalls.Chunk"/> carries, so the two coasts hand
        /// <see cref="CliffWallSurface"/> identical work.</summary>
        public struct Chunk
        {
            public List<CliffWallSample> Samples;
            public int AspectIndex;
            public int BatterIndex;
            public float WallAzimuth;
            public CoastClass Class;
            public int RunIndex;
            public float AlongOffsetMetres;
            public float RunSurfaceMetres;
        }

        // =============================================================================================
        //  the plan → geometry (pure; no scene, no assets — this is what the tests drive)
        // =============================================================================================

        /// <summary>
        /// Walk the whole coast run and resolve it into chunks of standing wall. Deterministic and total:
        /// a pure function of the terrain component's authored plan, so two runs on two machines produce
        /// the same coast (rule 5).
        /// </summary>
        public static List<Chunk> ResolveChunks(MainlandTidalTerrain terrain)
        {
            var chunks = new List<Chunk>();
            if (terrain == null) return chunks;

            Vector2[] points = terrain.CoastPoints;
            if (!MainlandCoast.IsRun(points)) return chunks;

            var stations = new List<(CliffWallSample sample, int aspect, int batter, float azimuth,
                                     CoastClass cls, int segment, bool live)>();

            // ⭐⭐ WALKED SEGMENT BY SEGMENT, AND EVERY VERTEX GETS A STATION ON BOTH SIDES OF IT.
            //
            // A uniform `i × StationMetres` walk over the whole run is the obvious shape and it is wrong
            // here. A polyline's seaward normal is a property of the SEGMENT, so it steps at each vertex
            // — and a uniform walk straddles that step, putting the vertex somewhere between two stations
            // with no station of its own. The wall then either spans the jump inside one chunk (measured
            // at 3.17 m of world Y against the 2 m sorting rule) or is cut with a hole at the corner.
            //
            // Walking per segment and always emitting BOTH endpoints gives each vertex two coincident
            // stations, one wearing each segment's facing. Every chunk then lives on exactly one segment
            // — constant normal, exact azimuth, exact aspect — and consecutive chunks meet ON the corner
            // rather than across it, so there is no hole and no jump.
            int segmentCount = MainlandCoast.SegmentCount(points);
            for (int seg = 0; seg < segmentCount; seg++)
            {
                float segStart = MainlandCoast.SegmentStart(points, seg);
                float segLength = MainlandCoast.SegmentLength(points, seg);
                int steps = Mathf.Max(1, Mathf.CeilToInt(segLength / StationMetres));

                for (int k = 0; k <= steps; k++)
                {
                    float along = segStart + Mathf.Min(segLength, k * StationMetres);
                    if (k == steps) along = segStart + segLength;       // land exactly on the vertex

                    if (TryStationAt(terrain, along, seg, out CliffWallSample sample,
                                     out CoastClass cls, out float azimuth))
                    {
                        int aspect = StPetersCliffWalls.SnapAspectIndex(azimuth);
                        float batterTrue = CliffWallGeometry.BatterDegrees(in sample);
                        int batter = CliffWallGeometry.SnapBatterIndex(
                            batterTrue, StPetersCliffWalls.BakedBatterAngles());
                        stations.Add((sample, aspect, batter, azimuth, cls, seg, true));
                    }
                    else
                    {
                        stations.Add((default, -1, -1, 0f, CoastClass.Beach, seg, false));
                    }
                }
            }

            // Cut runs where the wall stops being drawable, where the TEXTURE must change (aspect or
            // batter — which is also what a polyline CORNER trips), or where a chunk has run long enough
            // that one sorting order stops being honest. The bookkeeping is StPetersCliffWalls' B3 fix,
            // carried across verbatim: `u` and the row-count basis run continuously through every cut so
            // a chunk boundary is invisible, and reset only where the wall genuinely STOPS.
            var current = new Chunk { Samples = new List<CliffWallSample>() };
            float runMetres = 0f;
            float toeLow = float.MaxValue, toeHigh = float.MinValue;
            Vector2 lastBrow = Vector2.zero;
            int runIndex = 0;
            float runAlong = 0f;
            float chunkStartAlong = 0f;
            int currentSegment = -1;

            for (int i = 0; i < stations.Count; i++)
            {
                var st = stations[i];
                if (!st.live)
                {
                    if (current.Samples.Count > 0 || runAlong > 0f) runIndex++;
                    Flush(chunks, ref current);
                    runMetres = 0f; toeLow = float.MaxValue; toeHigh = float.MinValue;
                    runAlong = 0f; chunkStartAlong = 0f; currentSegment = -1;
                    continue;
                }

                float toeY = CliffWallGeometry.ToeScreen(st.sample).y;
                bool startsNew = current.Samples.Count == 0;
                if (!startsNew)
                {
                    float step = Vector2.Distance(lastBrow, st.sample.BrowPlan);
                    runMetres += step;
                    runAlong += step;
                    bool textureChanged = st.aspect != current.AspectIndex ||
                                          st.batter != current.BatterIndex;

                    // ⭐⭐ A POLYLINE VERTEX ALWAYS CUTS, and the aspect change does NOT imply it.
                    //
                    // This is the open run's own hazard and it cost a red on the first pass. The toe is
                    // brow + normal × plunge, so where two segments meet the toe JUMPS by
                    // 2·plunge·sin(Δ/2) even though the brow is continuous. The obvious hope — that a
                    // corner also changes the snapped aspect, and so cuts via `textureChanged` — is
                    // false: two normals 41° apart can both snap to the same authored facing (70° and
                    // 111° are both E), and one that does moves the foot ~2.1 m in a single 0.25 m step.
                    // MEASURED at 3.17 m of world Y in one chunk against a 2 m rule, by
                    // NineMileCreekShoreTests.NoChunkSortsOverMoreWorldYThanOneCharacterIsTall.
                    //
                    // Cutting on the segment index confines every chunk to ONE segment, which is
                    // strictly better than merely fixing the span: within a segment the seaward normal
                    // is CONSTANT, so the chunk's midpoint azimuth is exact rather than representative
                    // and its aspect snap cannot be wrong at either end. An ellipse cannot have this
                    // property; a polyline gets it for free.
                    bool crossedVertex = st.segment != currentSegment;

                    float span = Mathf.Max(toeHigh, toeY) - Mathf.Min(toeLow, toeY);
                    if (textureChanged || crossedVertex ||
                        runMetres >= ChunkMetres || span > ChunkToeSpanMetres)
                    {
                        // The boundary station SEEDS the next chunk and does not join this one, and its
                        // `along` comes forward with it — see StPetersCliffWalls for the two measured
                        // defects (a 0.62 m over-span, and RMS 0.40 m of torn overlap) that shape says.
                        //
                        // ⚠ EXCEPT ACROSS A VERTEX, where carrying it forward would be the very bug the
                        // vertex cut exists to prevent: the previous station wears the OLD segment's
                        // facing, so seeding the new chunk with it would put a foreign normal (and the
                        // toe jump) back inside a chunk that is supposed to be one segment's. The two
                        // chunks already meet ON the corner — the walk emits a station at the vertex on
                        // both sides of it — so no overlap is needed to close the seam there.
                        CliffWallSample shared = current.Samples[current.Samples.Count - 1];
                        Flush(chunks, ref current);
                        if (crossedVertex)
                        {
                            toeLow = float.MaxValue; toeHigh = float.MinValue;
                            runMetres = 0f;
                            chunkStartAlong = runAlong;
                        }
                        else
                        {
                            current.Samples.Add(shared);
                            float sharedToe = CliffWallGeometry.ToeScreen(shared).y;
                            toeLow = toeHigh = sharedToe;
                            runMetres = step;
                            chunkStartAlong = runAlong - step;
                        }
                        startsNew = true;
                    }
                }

                if (startsNew)
                {
                    current.AspectIndex = st.aspect;
                    current.BatterIndex = st.batter;
                    current.WallAzimuth = st.azimuth;
                    current.Class = st.cls;
                    current.RunIndex = runIndex;
                    current.AlongOffsetMetres = chunkStartAlong;
                    currentSegment = st.segment;
                }
                current.Samples.Add(st.sample);
                toeLow = Mathf.Min(toeLow, toeY);
                toeHigh = Mathf.Max(toeHigh, toeY);
                lastBrow = st.sample.BrowPlan;
            }
            Flush(chunks, ref current);
            ResolveRunSurfaces(chunks);
            return chunks;
        }

        /// <summary>Give every chunk of a run the same row-count basis: the longest face anywhere in that
        /// run, so two chunks meeting at a shared station approximate the displaced curve with the same
        /// polyline and the seam closes. (StPetersCliffWalls measured the alternative at 26 of 76 cuts.)</summary>
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
            if (current.Samples.Count >= 2)
            {
                // The chunk's MIDPOINT facing, unsnapped — the shader wants the true bearing so lighting
                // stays continuous along a curving coast, and taking it from one end would hand the far
                // end of every chunk a stale light direction leaning all the same way.
                current.WallAzimuth = CliffWallGeometry.AzimuthOf(
                    CliffWallGeometry.OutwardPlan(current.Samples[current.Samples.Count / 2]));
                into.Add(current);
            }
            current = new Chunk { Samples = new List<CliffWallSample>() };
        }

        /// <summary>
        /// The wall standing at a distance along the run, or false where none does.
        ///
        /// <para>The class is read UNFEATHERED (what the plan SAYS) but the heights come off the FEATHERED
        /// profile, and that asymmetry is deliberate: the plan decides WHERE rock is, the blend decides how
        /// tall it is there. Runs are extended <see cref="RunOverrunMetres"/> past their sector so the
        /// taper has somewhere to happen.</para>
        /// </summary>
        static bool TryStationAt(MainlandTidalTerrain terrain, float along, int segment,
                                 out CliffWallSample sample, out CoastClass cls, out float azimuth)
        {
            sample = default;
            azimuth = 0f;
            Vector2[] points = terrain.CoastPoints;

            // ⭐⭐ THE ASPECT LAW IS A GATE ON THE STATION, NOT ONLY ON THE SECTOR — and this is the
            // second thing the open run needs that the ellipse does not.
            //
            // The plan authors its cliff sectors to land on legal shore, and a coast test already holds
            // that. But the OVERRUN below deliberately carries a wall past a sector's end so the drop can
            // taper through the feather — and on a polyline the segment beyond that end can face
            // anywhere. Measured: the DeepShoreCliff sector ends at s 399 and the overrun reached 8 m
            // into the creek-mouth segment, whose normal is 52.1° — 37.9° from the nearest authored
            // facing, on the 41.8 m NineMileCreekMainland §4 names as "the ONLY stretch of this
            // coastline a cliff may not stand on". So the wall would have been drawn on precisely the
            // shore the plan forbids it, wearing a face chosen for a direction it does not point.
            // Caught by NineMileCreekShoreTests.EveryChunkWearsAFaceTheAspectLawAllows.
            //
            // Asked per SEGMENT because that is the frame the law is written in (MainlandCoast's own
            // note: a polyline's normal is constant along a segment, so a per-sample test would be the
            // same question asked redundantly).
            if (!MainlandCoast.CliffIsLegalOn(points, segment)) { cls = CoastClass.Beach; return false; }

            cls = NearestCliffClass(terrain, along);
            if (cls == CoastClass.Beach) return false;                  // no cliff within the overrun

            Vector2 brow = MainlandCoast.PositionAt(points, along);

            // ⚠ THE FACE RUNS DOWN THE COAST'S OUTWARD NORMAL. On a run that is not the caveat it is on
            // an ellipse (where the radial and the normal differ by up to 20° at the shoulder) — a
            // polyline segment's normal IS perpendicular to it — but the wall is still measured along the
            // normal and the azimuth read back off the finished sample, so there is one direction and one
            // source either way.
            // The SEGMENT's normal, not the run-position's: at a vertex station the two disagree about
            // which side of the corner you are on, and the walk has already decided.
            azimuth = MainlandCoast.OutwardNormalAzimuth(points, segment);
            float a = azimuth * Mathf.Deg2Rad;
            var normal = new Vector2(Mathf.Sin(a), Mathf.Cos(a));

            // The plunge is authored in PLAIN WORLD METRES SEAWARD on this coast, so the foot is one
            // multiply — no march, and no elliptical frame to convert out of.
            float plunge = Mathf.Max(0.01f, NineMileCreekMainland.CliffPlungeWidth);
            Vector2 toe = brow + normal * plunge;

            // ⭐ BOTH ELEVATIONS COME OFF ProfileAt IN THE RUN'S OWN FRAME, at this exact station. Taking
            // the toe's height by re-projecting the stepped-out world point would land at a slightly
            // different `along` wherever the coast turns — i.e. would sample a station the terrain never
            // had, which is the whole reason that overload exists.
            float browElevation = terrain.ProfileAt(along, 0f);
            float toeElevation = terrain.ProfileAt(along, plunge);

            float drop = browElevation - toeElevation;
            if (drop < MinFaceDropMetres) return false;

            sample = new CliffWallSample(brow, toe, drop, toeElevation);
            if (CliffWallGeometry.BatterDegrees(in sample) < MinBatterDegrees) return false;
            return true;
        }

        /// <summary>
        /// The cliff class in force at a distance along the run, allowing a run to overrun its sector by
        /// <see cref="RunOverrunMetres"/> on each side. Returns <see cref="CoastClass.Beach"/> for "no wall
        /// here" — beach is never a wall, so it doubles as the sentinel without a nullable.
        ///
        /// <para><b>⚠ CLAMPED TO THE RUN'S OWN ENDS.</b> An open coast has two, and
        /// <see cref="MainlandCoast.ClassAt"/> clamps rather than returning nothing — so without this the
        /// overrun would read the first and last sectors' classes from off the end of the coastline and,
        /// if either were rock, stand a wall past where there is any shore to stand it on.</para>
        /// </summary>
        static CoastClass NearestCliffClass(MainlandTidalTerrain terrain, float along)
        {
            CoastRunSector[] sectors = terrain.CoastSectors;
            float runLength = terrain.CoastRunLength;

            CoastClass here = MainlandCoast.ClassAt(sectors, along);
            if (CoastPlan.IsCliff(here)) return here;

            const float SweepStepMetres = 0.25f;
            for (float d = SweepStepMetres; d <= RunOverrunMetres; d += SweepStepMetres)
            {
                float back = along - d, on = along + d;
                if (back >= 0f)
                {
                    CoastClass c = MainlandCoast.ClassAt(sectors, back);
                    if (CoastPlan.IsCliff(c)) return c;
                }
                if (on <= runLength)
                {
                    CoastClass c = MainlandCoast.ClassAt(sectors, on);
                    if (CoastPlan.IsCliff(c)) return c;
                }
            }
            return CoastClass.Beach;
        }

        // =============================================================================================
        //  the scene
        // =============================================================================================

        /// <summary>
        /// Build (or rebuild) this region's cliff walls under a single <see cref="RootName"/> object.
        /// Destroys any previous root first, so a builder Refresh converges: running twice gives the same
        /// coast as running once.
        /// </summary>
        public static int Build(MainlandTidalTerrain terrain)
        {
            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            List<Chunk> chunks = ResolveChunks(terrain);
            if (chunks.Count == 0) return 0;

            var material = AssetDatabase.LoadAssetAtPath<Material>(CliffFaceMat);
            if (material == null)
            {
                Debug.LogError($"[nmc-cliff-walls] {CliffFaceMat} is missing — the coast will build " +
                               "without faces. The material is committed; its TEXTURES are not (the bake " +
                               "root is gitignored by design).");
                return 0;
            }

            var root = new GameObject(RootName);
            int built = 0, stratified = 0;
            foreach (Chunk chunk in chunks)
            {
                if (!TryLoadBands(chunk, out CliffFaceBand[] bands, out Texture2D profile)) continue;
                if (bands.Length > 1) stratified++;

                LoadDecals(chunk, out Texture2D browStrip, out Texture2D toeStrip);

                int n = chunk.Samples.Count;
                var brow = new Vector2[n];
                var toe = new Vector2[n];
                var drop = new float[n];
                var toeElevation = new float[n];
                for (int i = 0; i < n; i++)
                {
                    brow[i] = chunk.Samples[i].BrowPlan;
                    toe[i] = chunk.Samples[i].ToePlan;
                    drop[i] = chunk.Samples[i].DropMetres;
                    toeElevation[i] = chunk.Samples[i].ToeElevation;
                }

                var go = new GameObject($"CliffWall_{chunk.Class}_" +
                                        $"{CliffCatalog.Aspects[chunk.AspectIndex]}_" +
                                        $"{CliffCatalog.Batters[CatalogBatter(chunk)]}_" +
                                        $"{built:D3}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                // Park the chunk at its own first brow so its vertices stay small and local — a mesh
                // authored at absolute world coordinates hundreds of metres out loses float precision in
                // exactly the sub-texel range the kit's 32 px/m relies on.
                go.transform.position = new Vector3(brow[0].x, brow[0].y, 0f);

                var surface = go.AddComponent<CliffWallSurface>();
                surface.Configure(brow, toe, drop, material, bands, profile, browStrip, toeStrip,
                                  chunk.AlongOffsetMetres, chunk.RunSurfaceMetres,
                                  chunk.WallAzimuth,
                                  CliffCatalog.BatterAngles[CatalogBatter(chunk)],
                                  CliffCatalog.AspectBakeLights[chunk.AspectIndex],
                                  CliffCatalog.FaceMetresS, CliffCatalog.FaceMetresT,
                                  CliffCatalog.ProfileSubdivideMetres, CliffCatalog.ProfileMetres,
                                  CliffCatalog.StripMetresT, CliffCatalog.BrowLineAt,
                                  toeElevation);
                built++;
            }

            int runs = 0;
            foreach (Chunk c in chunks) runs = Mathf.Max(runs, c.RunIndex + 1);
            Debug.Log($"[nmc-cliff-walls] {built} chunks of standing cliff over {runs} runs along " +
                      $"{terrain.CoastRunLength:F1} m of coast (stations every {StationMetres} m). " +
                      $"{stratified} carry a {OverburdenMetres:F1} m topsoil band over the {Rock}. " +
                      "Sorted into the decor band by each chunk's own toe (ADR 0032).");
            return built;
        }

        static int CatalogBatter(Chunk c) => StPetersCliffWalls.BakedBatterToCatalogIndex(c.BatterIndex);

        /// <summary>A face's rock bands, brow-downward: the eroded topsoil, then the sandstone under it.
        /// One profile serves both — it is the LANDFORM's displacement and the bands are materials lying
        /// on it. The fallback to one unstratified band is deliberate: a hole in the coast is a far worse
        /// failure than an unstratified cliff.</summary>
        static bool TryLoadBands(Chunk chunk, out CliffFaceBand[] bands, out Texture2D profile)
        {
            bands = new CliffFaceBand[0];
            int catalogBatter = CatalogBatter(chunk);
            string aspect = CliffCatalog.Aspects[chunk.AspectIndex];

            profile = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Profile)}/" +
                $"{CliffCatalog.ProfileName(Rock, catalogBatter)}.png");

            if (!TryLoadFaceSet(Rock, aspect, catalogBatter, out CliffFaceBand rock))
            {
                Debug.LogWarning(
                    $"[nmc-cliff-walls] no baked face for {Rock} {aspect} " +
                    $"{CliffCatalog.Batters[catalogBatter]} — that stretch of coast will not stand up. " +
                    "The kit ships as the rig; run the builder (it bakes on missing) or " +
                    "'Hidden Harbours ▸ Dev ▸ Bake Cliff Face Kit'.");
                return false;
            }

            // The soil horizon is a vertical depth; the face is addressed along its own surface, so the
            // conversion uses the SNAPPED batter — the angle the pixels were baked at, not the station's
            // true one, or the band sits at a depth the texture disagrees with.
            float overburden = CliffWallGeometry.OverburdenSurfaceMetres(
                OverburdenMetres, CliffCatalog.BatterAngles[catalogBatter]);

            if (!TryLoadFaceSet(OverburdenRock, aspect, catalogBatter, out CliffFaceBand soil))
            {
                Debug.LogWarning(
                    $"[nmc-cliff-walls] no baked {OverburdenRock} face for {aspect} " +
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

        static bool TryLoadFaceSet(string rock, string aspect, int catalogBatter, out CliffFaceBand band)
        {
            band = CliffFaceBand.WholeFace(
                LoadFace(rock, aspect, catalogBatter, "_unlit"),
                LoadFace(rock, aspect, catalogBatter, "_normal"),
                LoadFace(rock, aspect, catalogBatter, "_mask"),
                rock);
            return band.HasChannels;
        }

        /// <summary>The kit's own finisher for a face's ends — the hanging sod lip at the brow, the sea's
        /// undercut at the toe. Loaded as TEXTURES, not sprites: the strips import as
        /// <c>SpriteImportMode.Multiple</c> and an unsliced Multiple-mode asset carries ZERO sprites, so
        /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> hands back null on a freshly baked checkout. Missing decals
        /// are NOT an error — the wall stands without them.</summary>
        static void LoadDecals(Chunk chunk, out Texture2D brow, out Texture2D toe)
        {
            int catalogBatter = CatalogBatter(chunk);
            string aspect = CliffCatalog.Aspects[chunk.AspectIndex];
            string folder = CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Strip);

            brow = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{folder}/Brow_{CliffCatalog.BrowName(aspect, CliffCatalog.BaseStep)}.png");

            string feature = CliffCatalog.ToeFeatureForBatter[catalogBatter];
            toe = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{folder}/Toe_{CliffCatalog.ToeName(aspect, CliffCatalog.BaseStep, feature)}.png");
        }

        static Texture2D LoadFace(string rock, string aspect, int catalogBatter, string channel) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Face)}/" +
                $"{CliffCatalog.FaceName(rock, aspect, catalogBatter, CliffCatalog.BaseStep, channel)}.png");
    }
}
#endif
