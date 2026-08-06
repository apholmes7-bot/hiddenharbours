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
    /// </summary>
    public static class StPetersCliffWalls
    {
        /// <summary>The scene object every generated chunk parents under. Rebuilding destroys it whole,
        /// which is what makes a Refresh converge rather than stack a second coast on the first.</summary>
        public const string RootName = "CliffWalls";

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
        /// what <see cref="CliffBaker.DefaultRock"/> ships.</summary>
        public const string Rock = CliffBaker.DefaultRock;

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
            var chunks = new List<Chunk>();
            if (terrain == null) return chunks;

            // Walk in BEARING, but step by a constant length of SHORE: a degree near due south buys
            // 120 m/rad of coast on this ellipse where one near the harbour buys 70, so a constant
            // angular step would make the stations bunch at the ends and thin out along the flanks.
            var stations = new List<(CliffWallSample sample, int aspect, int batter, float azimuth,
                                     CoastClass cls, bool live)>();

            float bearing = 0f;
            while (bearing < 360f)
            {
                float speed = Mathf.Max(1e-3f, ShoreSpeed(bearing));         // metres per radian
                float stepDeg = StationMetres / speed * Mathf.Rad2Deg;

                if (TryStationAt(terrain, bearing, out CliffWallSample sample, out CoastClass cls,
                                 out float azimuth))
                {
                    int aspect = SnapAspectIndex(azimuth);
                    float batterTrue = CliffWallGeometry.BatterDegrees(in sample);
                    int batter = CliffWallGeometry.SnapBatterIndex(batterTrue, BakedBatterAngles());
                    stations.Add((sample, aspect, batter, azimuth, cls, true));
                }
                else
                {
                    stations.Add((default, -1, -1, 0f, CoastClass.Beach, false));
                }

                bearing += Mathf.Max(1e-4f, stepDeg);
            }

            // Cut runs where the wall stops being drawable, where the TEXTURE must change (aspect or
            // batter), or where a chunk has run long enough that one sorting order stops being honest.
            var current = new Chunk { Samples = new List<CliffWallSample>() };
            float runMetres = 0f;
            float toeLow = float.MaxValue, toeHigh = float.MinValue;
            Vector2 lastBrow = Vector2.zero;

            for (int i = 0; i < stations.Count; i++)
            {
                var st = stations[i];
                if (!st.live)
                {
                    Flush(chunks, ref current);
                    runMetres = 0f; toeLow = float.MaxValue; toeHigh = float.MinValue;
                    continue;
                }

                float toeY = CliffWallGeometry.ToeScreen(st.sample).y;
                bool startsNew = current.Samples.Count == 0;
                if (!startsNew)
                {
                    runMetres += Vector2.Distance(lastBrow, st.sample.BrowPlan);
                    bool textureChanged = st.aspect != current.AspectIndex ||
                                          st.batter != current.BatterIndex;
                    // The Y span INCLUDING this station — a chunk is cut before it grows too tall to
                    // sort honestly, not after (see ChunkToeSpanMetres).
                    float span = Mathf.Max(toeHigh, toeY) - Mathf.Min(toeLow, toeY);
                    if (textureChanged || runMetres >= ChunkMetres || span > ChunkToeSpanMetres)
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
                        CliffWallSample shared = current.Samples[current.Samples.Count - 1];
                        Flush(chunks, ref current);
                        current.Samples.Add(shared);
                        float sharedToe = CliffWallGeometry.ToeScreen(shared).y;
                        toeLow = toeHigh = sharedToe;
                        runMetres = Vector2.Distance(shared.BrowPlan, st.sample.BrowPlan);
                        startsNew = true;
                    }
                }

                if (startsNew)
                {
                    current.AspectIndex = st.aspect;
                    current.BatterIndex = st.batter;
                    current.WallAzimuth = st.azimuth;
                    current.Class = st.cls;
                }
                current.Samples.Add(st.sample);
                toeLow = Mathf.Min(toeLow, toeY);
                toeHigh = Mathf.Max(toeHigh, toeY);
                lastBrow = st.sample.BrowPlan;
            }
            Flush(chunks, ref current);
            return chunks;
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
            current = new Chunk { Samples = new List<CliffWallSample>() };
        }

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

            sample = new CliffWallSample(brow, toe, drop);
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
            for (float d = StationMetres; d <= RunOverrunDegrees; d += 0.25f)
            {
                foreach (float side in new[] { -d, d })
                {
                    CoastClass c = terrain.CoastClassAt(ShorePoint(bearing + side));
                    if (CoastPlan.IsCliff(c)) return c;
                }
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
        /// Build (or rebuild) the region's cliff walls under a single <see cref="RootName"/> object.
        /// Destroys any previous root first, so a builder Refresh converges: hand-placed decor elsewhere
        /// in the scene is untouched, and running twice gives the same coast as running once.
        /// </summary>
        public static int Build(TidalTerrain terrain)
        {
            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            List<Chunk> chunks = ResolveChunks(terrain);
            if (chunks.Count == 0) return 0;

            var material = AssetDatabase.LoadAssetAtPath<Material>(CliffFaceMat);
            if (material == null)
            {
                Debug.LogError($"[cliff-walls] {CliffFaceMat} is missing — the coast will build without " +
                               "faces. The material is committed; its TEXTURES are not (the bake root " +
                               "is gitignored by design).");
                return 0;
            }

            var root = new GameObject(RootName);
            int built = 0;
            foreach (Chunk chunk in chunks)
            {
                if (!TryLoadChannels(chunk, out Texture2D unlit, out Texture2D normal,
                                     out Texture2D mask, out Texture2D profile))
                    continue;

                int n = chunk.Samples.Count;
                var brow = new Vector2[n];
                var toe = new Vector2[n];
                var drop = new float[n];
                for (int i = 0; i < n; i++)
                {
                    brow[i] = chunk.Samples[i].BrowPlan;
                    toe[i] = chunk.Samples[i].ToePlan;
                    drop[i] = chunk.Samples[i].DropMetres;
                }

                var go = new GameObject($"CliffWall_{chunk.Class}_" +
                                        $"{CliffCatalog.Aspects[chunk.AspectIndex]}_" +
                                        $"{CliffCatalog.Batters[BakedBatterToCatalogIndex(chunk.BatterIndex)]}_" +
                                        $"{built:D3}");
                go.transform.SetParent(root.transform, worldPositionStays: false);
                // Park the chunk at its own first brow so its vertices stay small and local — a mesh
                // authored at absolute world coordinates 200 m from the origin loses float precision in
                // exactly the sub-texel range the kit's 32 px/m relies on.
                go.transform.position = new Vector3(brow[0].x, brow[0].y, 0f);

                var surface = go.AddComponent<CliffWallSurface>();
                surface.Configure(brow, toe, drop, material, unlit, normal, mask, profile,
                                  chunk.WallAzimuth,
                                  CliffCatalog.BatterAngles[BakedBatterToCatalogIndex(chunk.BatterIndex)],
                                  CliffCatalog.AspectBakeLights[chunk.AspectIndex],
                                  CliffCatalog.FaceMetresS, CliffCatalog.FaceMetresT,
                                  CliffCatalog.ProfileSubdivideMetres, CliffCatalog.ProfileMetres);
                built++;
            }

            Debug.Log($"[cliff-walls] {built} chunks of standing cliff from {chunks.Count} resolved runs " +
                      $"(stations every {StationMetres} m, chunks up to {ChunkMetres} m). " +
                      "Sorted into the decor band by each chunk's own toe (ADR 0032).");
            return built;
        }

        static bool TryLoadChannels(Chunk chunk, out Texture2D unlit, out Texture2D normal,
                                    out Texture2D mask, out Texture2D profile)
        {
            int catalogBatter = BakedBatterToCatalogIndex(chunk.BatterIndex);
            string aspect = CliffCatalog.Aspects[chunk.AspectIndex];

            unlit = LoadFace(aspect, catalogBatter, "_unlit");
            normal = LoadFace(aspect, catalogBatter, "_normal");
            mask = LoadFace(aspect, catalogBatter, "_mask");
            profile = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Profile)}/" +
                $"{CliffCatalog.ProfileName(Rock, catalogBatter)}.png");

            if (unlit != null && normal != null && mask != null) return true;

            Debug.LogWarning(
                $"[cliff-walls] no baked face for {Rock} {aspect} " +
                $"{CliffCatalog.Batters[catalogBatter]} — that stretch of coast will not stand up. " +
                "The kit ships as the rig; run the builder (it bakes on missing) or " +
                "'Hidden Harbours ▸ Dev ▸ Bake Cliff Face Kit'.");
            return false;
        }

        static Texture2D LoadFace(string aspect, int catalogBatter, string channel) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Face)}/" +
                $"{CliffCatalog.FaceName(Rock, aspect, catalogBatter, CliffCatalog.BaseStep, channel)}.png");

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

        /// <summary>A point <paramref name="outMetres"/> of ELLIPTICAL distance seaward of the plateau
        /// edge — the frame the plunge width is authored in. In world metres it is not the same distance
        /// on both axes, which is exactly why the batter varies along one sector.</summary>
        public static Vector2 ShorePointOut(float bearingDegrees, float outMetres)
        {
            float r = bearingDegrees * Mathf.Deg2Rad;
            float d = StPetersBuilder.IslandRadius + outMetres;
            return StPetersBuilder.IslandCenter
                   + new Vector2(d * Mathf.Sin(r),
                                 d * Mathf.Cos(r) * StPetersBuilder.IslandRadiusY
                                   / StPetersBuilder.IslandRadius);
        }

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
