using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// One station along a cliff run, sampled off the SAME analytic profile the walk gate reads —
    /// the lip, the foot, and how far it is between them.
    ///
    /// <para><b>Everything here is PLAN geometry plus one elevation drop.</b> The projection into
    /// screen units happens once, in <see cref="CliffWallGeometry"/>, so the sampler never has to
    /// know about the camera and the geometry never has to know about the coast.</para>
    /// </summary>
    public struct CliffWallSample
    {
        /// <summary>The clifftop lip, in world XY — a point on the plateau edge.</summary>
        public Vector2 BrowPlan;

        /// <summary>The foot of the face, in world XY, <b>in plan</b> — where the plunge stops
        /// falling. Not where the toe is DRAWN; see <see cref="CliffWallGeometry.ToeScreen"/>.</summary>
        public Vector2 ToePlan;

        /// <summary>Brow elevation minus toe elevation, in metres. Always positive on a real face.</summary>
        public float DropMetres;

        /// <summary>
        /// The toe's ABSOLUTE elevation, metres above chart datum — the height the drop falls TO.
        ///
        /// <para><b>Why a difference was not enough (2026-08-06).</b> Everything the geometry needs is
        /// relative: a drop projects to a screen offset, a surface length tiles a texture. The WATERLINE
        /// is the first thing that has to compare the face against something outside it — the sea's own
        /// level — and a difference cannot say where that crossing falls. The builder already reads this
        /// off the same analytic profile it takes the drop from, so it is carried rather than re-derived.
        /// Defaults to 0 on the three-argument constructor, which every geometry call and every geometry
        /// test uses and none of them read.</para>
        /// </summary>
        public float ToeElevation;

        public CliffWallSample(Vector2 browPlan, Vector2 toePlan, float dropMetres,
                               float toeElevation = 0f)
        {
            BrowPlan = browPlan;
            ToePlan = toePlan;
            DropMetres = dropMetres;
            ToeElevation = toeElevation;
        }
    }

    /// <summary>
    /// The cliff wall's <b>geometry law</b>: how a run of sampled coast becomes a face quad strip that
    /// the Cliff Face kit v10 can be tiled across honestly.
    ///
    /// <para><b>Pure and engine-light on purpose.</b> Nothing here touches a scene, an asset, the clock
    /// or a random number — it is <see cref="Vector2"/> arithmetic and trigonometry, so every rule below
    /// is asserted headless by <c>CliffWallGeometryTests</c>. That matters more than usual here, because
    /// all three of its failure modes are SILENT: a wall built at the wrong metre scale still renders
    /// perfectly and merely looks like slightly wrong rock.</para>
    ///
    /// <para><b>⭐ THE THREE RULES THIS CLASS EXISTS TO HOLD.</b></para>
    /// <list type="number">
    /// <item><description><b>Height draws at <c>cos 40°</c>, ground travel does not.</b> The project's
    /// shared ¾ camera sits 40° above the horizon (<see cref="SpriteLightMath.CameraElevationDeg"/>), so
    /// one metre of HEIGHT draws <see cref="SpriteLightMath.HeightScale"/> ≈ 0.766 world units up the
    /// screen. A cliff's drop is height; its plunge is plan. They are projected by DIFFERENT terms and
    /// adding them as if they were the same is how a 6 m wall comes out looking like a 9 m one.</description></item>
    /// <item><description><b>The texture is addressed in SURFACE metres, never in height.</b> The kit
    /// bakes 12 × 9 m at 32 px/m measured ALONG THE SLOPING FACE (<c>CliffCatalog.FaceMetresT</c>, and
    /// the rig README §4: "Size the quad by surface length, not by height"). A battered face is longer
    /// than it is tall, so <see cref="SurfaceLengthMetres"/> — not the drop, and not the drawn screen
    /// height — is what the <c>v</c> coordinate counts.</description></item>
    /// <item><description><b>The tiling is baked into the UVs, not asked of the shader.</b>
    /// <c>HiddenHarboursCliffFace.shader</c> DECLARES <c>_TileMetresS</c>/<c>_TileMetresT</c> and never
    /// reads them in the fragment stage — they are documentation, not a transform. So this class emits
    /// UVs already in tile units (<see cref="TileU"/>/<see cref="TileV"/>) and the material is left with
    /// its defaults. Wiring the shader instead would work too, but it would move a pinned constant into
    /// <c>CliffLightMathTests</c>' blast radius for no gain.</description></item>
    /// </list>
    /// </summary>
    public static class CliffWallGeometry
    {
        /// <summary>
        /// Where a sample's toe is DRAWN, in world XY: its plan position pushed down-screen by the drop,
        /// projected at the shared camera's <see cref="SpriteLightMath.HeightScale"/>.
        ///
        /// <para><b>⚠ Both terms push the same way and neither may be dropped.</b> A south-facing face
        /// recedes down-screen in PLAN as well (its toe is south of its brow), so the drawn face is
        /// taller than <c>drop × 0.766</c> alone; an east-facing one recedes sideways instead and the
        /// quad comes out SHEARED. That shear is not a defect — it is what a ¾ view of an east wall
        /// looks like, and squaring it up is what makes a cliff read as a flat painted band.</para>
        /// </summary>
        public static Vector2 ToeScreen(in CliffWallSample s) =>
            new Vector2(s.ToePlan.x, s.ToePlan.y - s.DropMetres * SpriteLightMath.HeightScale);

        /// <summary>
        /// ⭐ <b>A chunk's DRAWN toe, station by station, in world XY: the line its blocker stands on</b>
        /// (<see cref="ToeScreen"/> of each sample, in order). A cliff face drawn over the sea is a wall (ruling
        /// t10, 2026-11-08): what the face covers, nobody enters on foot, so the line where the picture ends is
        /// the line that stops her. Joined walls share their join station exactly (the builder refuses a Def that
        /// does not start on the last station of the wall it follows), so joined chunks' lines meet with no gap.
        /// </summary>
        public static Vector2[] DrawnToe(CliffWallSample[] samples)
        {
            int n = samples == null ? 0 : samples.Length;
            var toe = new Vector2[n];
            for (int i = 0; i < n; i++) toe[i] = ToeScreen(in samples[i]);
            return toe;
        }

        /// <summary>
        /// The blocker's thickness, metres: <c>PropertyBoundary.DefaultThicknessMetres</c>'s 0.35, the figure the
        /// quay wall the walker is stopped by stands at (thicker than her per-step travel, so a fast body cannot
        /// tunnel it). Restated here only because Art depends on Core alone (project-structure §5) and cannot
        /// reach World's constant; <c>CliffWallToeBlockerTests</c> pins the two equal, so they cannot drift.
        /// </summary>
        public const float BlockerThicknessMetres = 0.35f;

        /// <summary>A face piece smaller than this (m²) is a repeated or collinear station, not ground: it is skipped.</summary>
        public const float MinFacePieceAreaSqMetres = 1e-4f;

        // ── Where a run of chunks ends, and how its faces meet ──────────────────────────────────────
        // A run is the chain of chunks whose end stations meet. These read only the chunks' brow lines and drops, so
        // the picture they shape is a pure function of the stations, the same on every machine and on every build.

        /// <summary>Two chunk ends closer than this (m) are one join: the builder writes a join as one shared station.</summary>
        public const float RunJoinToleranceMetres = 0.01f;

        /// <summary>A run end this close (m) to the start of another run is a slit, not an end: the face is drawn on
        /// across it (the East Ledges slit is 0.94 m, Nine Mile Creek's 0.20 m). Wider gaps are real gaps (paths, the
        /// creek mouth) and their ends taper.</summary>
        public const float RunBridgeMetres = 1.5f;

        /// <summary>At an open run end the face is drawn only this fraction of the way down: it tapers into the ground
        /// instead of stopping in a square cut.</summary>
        public const float RunEndKeep = 0.08f;

        /// <summary>How far back from an open end the taper reaches, per metre of drop at the end.</summary>
        public const float RunEndTaperPerDropMetre = 1.4f;

        /// <summary>The shortest taper (m), so a low end still rounds off instead of stepping.</summary>
        public const float RunEndTaperMinMetres = 3f;

        /// <summary>The longest taper (m), and so the farthest a chunk looks along its run for an end.</summary>
        public const float RunEndTaperMaxMetres = 12f;

        /// <summary>On a short run with both ends near, each end's taper takes at most this share of the run's length.</summary>
        public const float RunEndTaperMaxShareOfRun = 0.3f;

        /// <summary>Where two joined chunks wear different rock, the one drawn in front carries its own rock this far
        /// (m) over the other's face, fading out, so the swap reads as one wall instead of a straight seam.</summary>
        public const float RunSeamFeatherMetres = 1.6f;

        /// <summary>Over the last this many metres (m) above a tapered face's foot the rock fades out, and the toe strip
        /// fades with the taper, so the face sinks into the ground below it instead of ending on a drawn line.</summary>
        public const float RunEndFadeMetres = 3f;

        /// <summary>How strongly a column fades at its foot: 0 where the face is whole, 1 at an open end.</summary>
        public static float RunEndFadeStrength(float keep) =>
            Mathf.Clamp01((1f - keep) / (1f - RunEndKeep));

        /// <summary>The vertex alpha at depth <paramref name="t"/> (0..1 of the surface) of a column that keeps
        /// <paramref name="keep"/> of a <paramref name="surfaceMetres"/> face. 1 everywhere on a whole face.</summary>
        public static float RunEndAlphaAt(float t, float keep, float surfaceMetres)
        {
            float strength = RunEndRockFadeStrength(keep);
            if (strength <= 0f) return 1f;
            float band = Mathf.Min(RunEndFadeMetres, RunEndFadeShareOfKept * keep * surfaceMetres);
            if (!(band > 1e-4f)) return 1f - strength;
            float x = Mathf.Clamp01((keep - t) * surfaceMetres / band);
            return 1f - strength * (1f - x * x * (3f - 2f * x));
        }

        /// <summary>A toe strip shows whole only where the face keeps at least this share of its surface, and is gone
        /// by <see cref="ToeStripGoneKeep"/>.</summary>
        public const float ToeStripWholeKeep = 0.97f;

        /// <summary>See <see cref="ToeStripWholeKeep"/>.</summary>
        public const float ToeStripGoneKeep = 0.8f;

        /// <summary>The toe strip's alpha on a column that keeps <paramref name="keep"/> of its face.</summary>
        public static float ToeStripShownAt(float keep) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ToeStripGoneKeep, ToeStripWholeKeep, keep));

        /// <summary>The rock fades at a tapered foot only where the face keeps less than this share of its surface.
        /// Higher up the taper the foot stays solid: a half-clear face shows the faces and ground behind it through
        /// the rock as faint layers.</summary>
        public const float RunEndRockFadeFromKeep = 0.5f;

        /// <summary>At most this share of the kept face fades at its foot: the upper part of a tapered face stays solid.</summary>
        public const float RunEndFadeShareOfKept = 0.4f;

        /// <summary>How strongly the rock fades at a tapered foot: 0 where the face keeps
        /// <see cref="RunEndRockFadeFromKeep"/> or more, 1 at an open end.</summary>
        public static float RunEndRockFadeStrength(float keep) =>
            Mathf.Clamp01((RunEndRockFadeFromKeep - keep) / (RunEndRockFadeFromKeep - RunEndKeep));

        /// <summary>The taper length (m) for an open end with this drop.</summary>
        public static float RunEndTaperMetres(float dropMetres) =>
            Mathf.Clamp(dropMetres * RunEndTaperPerDropMetre, RunEndTaperMinMetres, RunEndTaperMaxMetres);

        /// <summary>How much of the face (0..1 of its surface, from the brow) is drawn at a column this far (m) along
        /// the run from an open end whose taper is <paramref name="taperMetres"/> long. 1 past the taper.</summary>
        public static float RunEndKeepAt(float distanceMetres, float taperMetres)
        {
            if (!(distanceMetres < taperMetres) || taperMetres <= 0f) return 1f;
            float x = Mathf.Clamp01(distanceMetres / taperMetres);
            return Mathf.Lerp(RunEndKeep, 1f, x * x * (3f - 2f * x));
        }

        /// <summary>
        /// One chunk's place in its run. Distances are along the brow, from this chunk's first (or last) station to the
        /// open run end behind (or ahead of) it; <see cref="float.PositiveInfinity"/> where no open end lies within
        /// <see cref="RunEndTaperMaxMetres"/>. Indexes are into the list the reach was read from, -1 for none.
        /// </summary>
        public struct RunReach
        {
            public float StartDistance, StartTaperMetres;
            public float EndDistance, EndTaperMetres;
            /// <summary>The chunk whose first station closes the slit after this chunk's last station.</summary>
            public int BridgeTo;
            /// <summary>The chunks joined to this one's first and last station.</summary>
            public int JoinedBefore, JoinedAfter;
        }

        /// <summary>The polyline length (m) of a brow line.</summary>
        public static float BrowLengthMetres(Vector2[] brow)
        {
            float len = 0f;
            if (brow != null)
                for (int i = 1; i < brow.Length; i++) len += Vector2.Distance(brow[i - 1], brow[i]);
            return len;
        }

        /// <summary>
        /// ⭐ <b>Where chunk <paramref name="me"/> sits in its run</b>, from every sibling chunk's brow line and drops.
        /// A run end is a first station no chunk ends at, or a last station no chunk starts at. An end within
        /// <see cref="RunBridgeMetres"/> of another run's end is a slit and is not open; every other end is open and
        /// tapers. Chunks with fewer than two stations are ignored.
        /// </summary>
        public static RunReach RunReachOf(IReadOnlyList<Vector2[]> brows, IReadOnlyList<float[]> drops, int me)
        {
            var reach = new RunReach
            {
                StartDistance = float.PositiveInfinity, EndDistance = float.PositiveInfinity,
                BridgeTo = -1, JoinedBefore = -1, JoinedAfter = -1,
            };
            if (brows == null || me < 0 || me >= brows.Count || !Usable(brows[me])) return reach;
            reach.JoinedBefore = JoinedBefore(brows, me);
            reach.JoinedAfter = JoinedAfter(brows, me);

            // Back along the run to its first station.
            float dist = 0f;
            int cur = me;
            for (int guard = 0; guard < brows.Count; guard++)
            {
                int prev = cur == me ? reach.JoinedBefore : JoinedBefore(brows, cur);
                if (prev < 0)
                {
                    Vector2 at = brows[cur][0];
                    if (!NearOtherRunEnd(brows, cur, at, first: true))
                    {
                        reach.StartDistance = dist;
                        reach.StartTaperMetres = RunEndTaperMetres(DropAt(drops, cur, 0));
                    }
                    break;
                }
                if (prev == me) break;                           // a closed ring has no end
                dist += BrowLengthMetres(brows[prev]);
                if (dist >= RunEndTaperMaxMetres) break;
                cur = prev;
            }

            // On along the run to its last station.
            dist = 0f;
            cur = me;
            for (int guard = 0; guard < brows.Count; guard++)
            {
                int next = cur == me ? reach.JoinedAfter : JoinedAfter(brows, cur);
                if (next < 0)
                {
                    Vector2 at = brows[cur][brows[cur].Length - 1];
                    if (!NearOtherRunEnd(brows, cur, at, first: false))
                    {
                        reach.EndDistance = dist;
                        reach.EndTaperMetres = RunEndTaperMetres(DropAt(drops, cur, brows[cur].Length - 1));
                    }
                    break;
                }
                if (next == me) break;
                dist += BrowLengthMetres(brows[next]);
                if (dist >= RunEndTaperMaxMetres) break;
                cur = next;
            }

            // A slit after my last station: the nearest run start within the bridge distance.
            if (reach.JoinedAfter < 0)
            {
                Vector2 last = brows[me][brows[me].Length - 1];
                float best = RunBridgeMetres;
                for (int k = 0; k < brows.Count; k++)
                {
                    if (k == me || !Usable(brows[k])) continue;
                    float d = Vector2.Distance(last, brows[k][0]);
                    if (d <= best && JoinedBefore(brows, k) < 0) { best = d; reach.BridgeTo = k; }
                }
            }
            return reach;
        }

        static bool Usable(Vector2[] brow) => brow != null && brow.Length >= 2;

        static float DropAt(IReadOnlyList<float[]> drops, int chunk, int station)
        {
            if (drops == null || chunk >= drops.Count || drops[chunk] == null || drops[chunk].Length == 0) return 0f;
            return drops[chunk][Mathf.Clamp(station, 0, drops[chunk].Length - 1)];
        }

        static int JoinedBefore(IReadOnlyList<Vector2[]> brows, int i)
        {
            Vector2 first = brows[i][0];
            for (int k = 0; k < brows.Count; k++)
                if (k != i && Usable(brows[k]) &&
                    Vector2.Distance(brows[k][brows[k].Length - 1], first) <= RunJoinToleranceMetres) return k;
            return -1;
        }

        static int JoinedAfter(IReadOnlyList<Vector2[]> brows, int i)
        {
            Vector2 last = brows[i][brows[i].Length - 1];
            for (int k = 0; k < brows.Count; k++)
                if (k != i && Usable(brows[k]) &&
                    Vector2.Distance(brows[k][0], last) <= RunJoinToleranceMetres) return k;
            return -1;
        }

        /// <summary>Whether another run's open end lies within <see cref="RunBridgeMetres"/> of this run end.</summary>
        static bool NearOtherRunEnd(IReadOnlyList<Vector2[]> brows, int chunk, Vector2 at, bool first)
        {
            for (int k = 0; k < brows.Count; k++)
            {
                if (!Usable(brows[k])) continue;
                Vector2[] b = brows[k];
                // Distance first: the join scans are the dear part, and only an end this close needs one.
                float d0 = Vector2.Distance(b[0], at);
                if (!(k == chunk && first) && d0 <= RunBridgeMetres && d0 > RunJoinToleranceMetres &&
                    JoinedBefore(brows, k) < 0)
                    return true;
                float d1 = Vector2.Distance(b[b.Length - 1], at);
                if (!(k == chunk && !first) && d1 <= RunBridgeMetres && d1 > RunJoinToleranceMetres &&
                    JoinedAfter(brows, k) < 0)
                    return true;
            }
            return false;
        }

        // ── Where the clifftop in front hides a face ────────────────────────────────────────────────
        // The ground is drawn in plan and a face hangs down-screen from its brow, so where a face is drawn over a
        // clifftop that stands nearer the camera, the clifftop is what the eye should see. The West Ledges bay is the
        // case: a wall whose brow runs back into the land is drawn over its own clifftop, and the bay's far wall is
        // drawn on over the spit in front of it. Read from brow lines alone, like the run ends above.

        /// <summary>The first this share of a face column is never clipped: the column starts on brow lines it touches.</summary>
        public const float OcclusionStartFraction = 0.02f;

        /// <summary>A clifftop in front hides a face point only if it stands at least this high relative to that point (m,
        /// below the point's own elevation): a low ledge in front does not hide the upper face behind it.</summary>
        public const float OcclusionElevationToleranceMetres = 0.25f;

        /// <summary>Under this much of its face kept, a column's brow strip fades out too: a hidden face has no lip to show.</summary>
        public const float OcclusionLipFadeKeep = 0.15f;

        /// <summary>One segment of a brow line, with the side its clifftop lies on.</summary>
        public struct BrowEdge
        {
            public Vector2 A, B;
            /// <summary>Unit normal toward the clifftop: away from the side the toe lies on.</summary>
            public Vector2 Land;
            /// <summary>Brow elevation (m) at each end, NaN where the chunk has no toe elevations.</summary>
            public float ElevationA, ElevationB;
        }

        /// <summary>The brow edge from one station to the next, its land side found from where the toes lie.</summary>
        public static bool TryBrowEdge(in CliffWallSample a, in CliffWallSample b, bool elevationsKnown, out BrowEdge edge)
        {
            edge = default;
            Vector2 e = b.BrowPlan - a.BrowPlan;
            if (e.sqrMagnitude < 1e-10f) return false;
            Vector2 toeSide = (a.ToePlan + b.ToePlan) * 0.5f - (a.BrowPlan + b.BrowPlan) * 0.5f;
            Vector2 left = new Vector2(-e.y, e.x).normalized;
            edge.A = a.BrowPlan;
            edge.B = b.BrowPlan;
            edge.Land = Cross(e, toeSide) > 0f ? -left : left;
            edge.ElevationA = elevationsKnown ? a.ToeElevation + a.DropMetres : float.NaN;
            edge.ElevationB = elevationsKnown ? b.ToeElevation + b.DropMetres : float.NaN;
            return true;
        }

        /// <summary>Every brow edge of a chunk's stations, appended to <paramref name="into"/>.</summary>
        public static void AddBrowEdges(List<BrowEdge> into, CliffWallSample[] samples, bool elevationsKnown)
        {
            if (samples == null) return;
            for (int i = 0; i + 1 < samples.Length; i++)
                if (TryBrowEdge(in samples[i], in samples[i + 1], elevationsKnown, out BrowEdge e)) into.Add(e);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>A chunk whose brow runs less than this (m) can be a step: see <see cref="IsStep"/>.</summary>
        public const float StepMaxBrowMetres = 0.5f;

        /// <summary>A step's drop changes at least this many times faster than its brow runs.</summary>
        public const float StepDropPerBrowMetre = 2f;

        /// <summary>
        /// Whether a chunk is a STEP: where the toe climbs to a higher ledge, the builder stacks stations on nearly one
        /// brow point, their drops falling from one face's height to the other's. Its face is a sliver standing edge-on
        /// (the West Ledges streak at wall 168), with no rock to show: the faces either side draw the cliff, and the step
        /// draws none. Its blockers still stand.
        /// </summary>
        public static bool IsStep(CliffWallSample[] samples)
        {
            if (samples == null || samples.Length < 2) return false;
            float brow = 0f, minDrop = float.MaxValue, maxDrop = float.MinValue;
            for (int i = 0; i < samples.Length; i++)
            {
                if (i > 0) brow += Vector2.Distance(samples[i - 1].BrowPlan, samples[i].BrowPlan);
                minDrop = Mathf.Min(minDrop, samples[i].DropMetres);
                maxDrop = Mathf.Max(maxDrop, samples[i].DropMetres);
            }
            return brow < StepMaxBrowMetres && maxDrop - minDrop > StepDropPerBrowMetre * brow;
        }

        /// <summary>
        /// Whether a face column leaving its brow point along <paramref name="v"/> heads straight into its own clifftop:
        /// the face there faces away from the camera and the clifftop hides it. With both brow edges at the point, the land
        /// near it is the wedge between them: the inside of both where the line turns toward the land, else either.
        /// </summary>
        public static bool HeadsIntoOwnLand(Vector2 v, bool hasBefore, in BrowEdge before, bool hasAfter, in BrowEdge after)
        {
            if (hasBefore && hasAfter)
            {
                bool inBefore = Vector2.Dot(v, before.Land) > 0f, inAfter = Vector2.Dot(v, after.Land) > 0f;
                bool convex = Vector2.Dot(after.B - after.A, before.Land) > 0f;
                return convex ? inBefore && inAfter : inBefore || inAfter;
            }
            if (hasBefore) return Vector2.Dot(v, before.Land) > 0f;
            if (hasAfter) return Vector2.Dot(v, after.Land) > 0f;
            return false;
        }

        /// <summary>
        /// ⭐ <b>How much of a face column (0..1 of its surface, from the brow) shows past the clifftops in front of it.</b>
        /// 0 where the column heads straight into its own clifftop (<see cref="HeadsIntoOwnLand"/>). Else the first point
        /// where its drawn line, brow to drawn toe, crosses a brow edge into that edge's clifftop, where that clifftop
        /// stands high enough to hide the face point there; everything past it lies behind that clifftop or the face it
        /// hangs. 1 where nothing hides it. <paramref name="browElevation"/> NaN skips the height check.
        /// </summary>
        public static float OcclusionKeepAt(Vector2 brow, Vector2 drawnToe, float browElevation, float dropMetres,
                                            bool hasBefore, in BrowEdge before, bool hasAfter, in BrowEdge after,
                                            IReadOnlyList<BrowEdge> edges)
        {
            Vector2 v = drawnToe - brow;
            if (v.sqrMagnitude < 1e-10f) return 1f;
            if (HeadsIntoOwnLand(v, hasBefore, before, hasAfter, after)) return 0f;
            if (edges == null) return 1f;
            float minX = Mathf.Min(brow.x, drawnToe.x), maxX = Mathf.Max(brow.x, drawnToe.x);
            float minY = Mathf.Min(brow.y, drawnToe.y), maxY = Mathf.Max(brow.y, drawnToe.y);
            float best = 1f;
            for (int k = 0; k < edges.Count; k++)
            {
                BrowEdge e = edges[k];
                if (Mathf.Max(e.A.x, e.B.x) < minX || Mathf.Min(e.A.x, e.B.x) > maxX ||
                    Mathf.Max(e.A.y, e.B.y) < minY || Mathf.Min(e.A.y, e.B.y) > maxY) continue;
                if (!(Vector2.Dot(v, e.Land) > 0f)) continue;             // leaving that clifftop, or along it
                Vector2 d = e.B - e.A;
                float den = Cross(v, d);
                if (Mathf.Abs(den) < 1e-9f) continue;
                Vector2 ab = e.A - brow;
                float t = Cross(ab, d) / den;
                float s = Cross(ab, v) / den;
                if (!(t > OcclusionStartFraction && t < best && s >= 0f && s <= 1f)) continue;
                if (!float.IsNaN(browElevation))
                {
                    float ground = Mathf.LerpUnclamped(e.ElevationA, e.ElevationB, s);
                    if (!float.IsNaN(ground) &&
                        ground < browElevation - t * dropMetres - OcclusionElevationToleranceMetres) continue;
                }
                best = t;
            }
            return best;
        }

        /// <summary>
        /// ⭐ <b>The ground one face segment covers, as solid pieces</b>: the quad from the brow at station
        /// <paramref name="b0"/>..<paramref name="b1"/> down to the drawn toe at <paramref name="t1"/>..<paramref name="t0"/>.
        /// What the face covers, nobody stands on (ruling t10): not from the sea, and not from the clifftop either.
        /// The toe blocker alone held the sea side only, so from every clifftop the walker went over the brow, down
        /// the height field's plunge and into the water hidden under the picture, and where a run ended he came up
        /// the open side of the face and over the brow onto the plateau.
        ///
        /// <para>One piece when the quad is simple (a concave quad is fine for a polygon collider). Where the drawn toe
        /// folds back across the brow and the quad crosses itself, both of its diagonal splits as triangles, so every
        /// lobe the picture covers is covered. Pieces under <see cref="MinFacePieceAreaSqMetres"/> are dropped.</para>
        /// </summary>
        public static System.Collections.Generic.List<Vector2[]> FacePieces(Vector2 b0, Vector2 b1, Vector2 t1, Vector2 t0)
        {
            var pieces = new System.Collections.Generic.List<Vector2[]>(4);
            bool crosses = SegmentsCross(b0, b1, t1, t0) || SegmentsCross(b1, t1, t0, b0);
            if (!crosses)
            {
                var quad = new[] { b0, b1, t1, t0 };
                if (Mathf.Abs(SignedArea(quad)) > MinFacePieceAreaSqMetres) pieces.Add(quad);
                return pieces;
            }
            foreach (Vector2[] tri in new[] { new[] { b0, b1, t1 }, new[] { b0, t1, t0 }, new[] { b0, b1, t0 }, new[] { b1, t1, t0 } })
                if (Mathf.Abs(SignedArea(tri)) > MinFacePieceAreaSqMetres) pieces.Add(tri);
            return pieces;
        }

        /// <summary>Shoelace area of a polygon, signed by winding (m²).</summary>
        public static float SignedArea(Vector2[] p)
        {
            float a = 0f;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++) a += (p[j].x * p[i].y) - (p[i].x * p[j].y);
            return a * 0.5f;
        }

        /// <summary>True when the open segments a–b and c–d cross at a single interior point.</summary>
        public static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            static float Side(Vector2 p, Vector2 q, Vector2 r) => (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x);
            float d1 = Side(c, d, a), d2 = Side(c, d, b), d3 = Side(a, b, c), d4 = Side(a, b, d);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }

        /// <summary>How far out the face reaches in PLAN — the horizontal run of the plunge, metres.</summary>
        public static float PlanRunMetres(in CliffWallSample s) => (s.ToePlan - s.BrowPlan).magnitude;

        /// <summary>
        /// The face's length ALONG ITS OWN SURFACE, metres — <c>√(run² + drop²)</c>, the hypotenuse of
        /// the plunge. This is the only length the <c>v</c> coordinate may be measured in (rule 2), and
        /// it is strictly the largest of the three candidates: a battered face is longer than it is tall
        /// and longer than it reaches.
        /// </summary>
        public static float SurfaceLengthMetres(in CliffWallSample s)
        {
            float run = PlanRunMetres(s);
            return Mathf.Sqrt(run * run + s.DropMetres * s.DropMetres);
        }

        /// <summary>
        /// The face's TRUE batter — its angle from horizontal, degrees. 90° is a vertical wall, 48° a
        /// bank. Derived from the analytic profile the sim walks, never authored: the coast plan states a
        /// plunge WIDTH and a toe ELEVATION, and the angle between them is a consequence.
        ///
        /// <para><b>Why it varies along one sector.</b> The plunge width is authored in ELLIPTICAL
        /// distance, so on a 120 × 70 m island a degree of bearing near due south buys less world-metre
        /// run than one near the harbour — the same authored plunge is steeper at the island's flank than
        /// at its end. That is honest, and it is why <see cref="SnapBatterIndex"/> exists.</para>
        /// </summary>
        public static float BatterDegrees(in CliffWallSample s)
        {
            float run = PlanRunMetres(s);
            if (run <= 1e-4f) return 90f;
            return Mathf.Atan2(Mathf.Max(0f, s.DropMetres), run) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Index of the BAKED batter nearest an angle, out of the angles a checkout actually carries.
        ///
        /// <para><b>⚠ The batter must SNAP where the azimuth must not.</b> The shader takes the true,
        /// unsnapped <c>_WallAzimuth</c> so lighting stays continuous along a curving coast — but its
        /// <c>_Batter</c> property says the opposite in as many words: <i>"Must match the batter the face
        /// texture was baked at, or the bedding spacing in the pixels disagrees with the frame the light
        /// is resolved in."</i> The kit respaces its bedding per batter (rig README §4.3: beds are
        /// horizontal in the WORLD, so a sloped surface carries fewer per texture metre), and that
        /// respacing is in the PIXELS. So the drawn angle is quantised to the bake and only the azimuth
        /// runs free.</para>
        /// </summary>
        public static int SnapBatterIndex(float batterDegrees, float[] bakedAngles)
        {
            if (bakedAngles == null || bakedAngles.Length == 0) return 0;
            int best = 0;
            float bestError = Mathf.Abs(batterDegrees - bakedAngles[0]);
            for (int i = 1; i < bakedAngles.Length; i++)
            {
                float e = Mathf.Abs(batterDegrees - bakedAngles[i]);
                if (e < bestError) { bestError = e; best = i; }
            }
            return best;
        }

        /// <summary>
        /// How many face tiles wide a run of shore is: plan arc length over the kit's 12 m period.
        /// Fractional by design — the face is exactly periodic in <c>s</c> and imported Repeat, so a run
        /// stops wherever the coast does rather than rounding to a whole tile.
        ///
        /// <para><b>⭐⭐ <paramref name="alongShoreMetres"/> IS MEASURED FROM THE START OF THE RUN, NOT OF
        /// THE CHUNK — and getting that wrong is the whole of the B3 "cliff gaps" defect.</b> A chunk is
        /// a SLICE of a run, cut for sorting reasons alone (see
        /// <c>StPetersCliffWalls.ChunkToeSpanMetres</c>), so nothing about where the slice falls may
        /// change the picture. Restart this at zero per chunk and two things break at once, both
        /// MEASURED on the real coast:</para>
        /// <list type="number">
        /// <item><description>The rock TEXTURE jumps, because the shared boundary station is addressed at
        /// <c>u</c> by one chunk and at 0 by the next — a mean of <b>3.30 m</b> and a worst of 6.00 m of
        /// the kit's 12 m period, at <b>76 of the coast's 78 chunk boundaries</b>.</description></item>
        /// <item><description>Worse, the plan-displacement profile is sampled at that same <c>u</c>, so
        /// the two copies of the shared station are pushed DIFFERENT distances along the outward normal
        /// and the overlap that exists to prevent a hole is torn open instead. Measured against the rig
        /// itself: <b>RMS 0.40 m and up to 1.10 m</b> at the coast's mean cut. A metre of open sky, 76
        /// times.</description></item>
        /// </list>
        /// <para>So the offset is a RUN quantity that the builder carries across every cut, and
        /// <see cref="RowsBasisSurfaceMetres"/> is its twin for the other axis.</para>
        /// </summary>
        public static float TileU(float alongShoreMetres, float faceMetresS) =>
            faceMetresS <= 0f ? 0f : alongShoreMetres / faceMetresS;

        /// <summary>
        /// Wrap a tile coordinate into <c>[0,1)</c> for a CPU texture read.
        ///
        /// <para><b>⚠ For the CPU sample ONLY — never for the mesh UVs.</b> The GPU wraps a Repeat
        /// texture itself and interpolates <c>u</c> correctly across a period boundary (0.98 → 1.02); a
        /// UV that had been wrapped per-vertex would read 0.98 → 0.02 and run the whole texture backwards
        /// between those two columns. <see cref="UnityEngine.Texture2D.GetPixelBilinear"/> is the one that
        /// needs it, and doing it here rather than trusting the importer means a profile that arrived
        /// Clamp still samples the right texel instead of smearing its last column down a whole run.</para>
        /// </summary>
        public static float WrapTile01(float tiles) => tiles - Mathf.Floor(tiles);

        /// <summary>
        /// The surface length a whole RUN's row count is derived from — the largest face anywhere in it.
        ///
        /// <para><b>Why the run and not the chunk.</b> Rows are how finely the profile displacement is
        /// resolved, and the displaced face is a CURVE. Two chunks that meet at a shared station but
        /// subdivide it differently approximate that curve with different polylines, so the shared edge
        /// does not close — the T-junction case of the same "a slice must not change the picture" rule
        /// <see cref="TileU"/> states. MEASURED before the fix: <b>26 of the coast's 76 cuts</b> joined
        /// chunks with different row counts.</para>
        /// </summary>
        public static float RowsBasisSurfaceMetres(
            System.Collections.Generic.IReadOnlyList<CliffWallSample> samples)
        {
            if (samples == null) return 0f;
            float longest = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                CliffWallSample s = samples[i];
                longest = Mathf.Max(longest, SurfaceLengthMetres(in s));
            }
            return longest;
        }

        /// <summary>
        /// ⭐ <b>THE SOIL HORIZON, AS THE FACE SEES IT.</b> A depth of overburden is a VERTICAL thickness
        /// — a soil profile is so many metres deep, whatever the ground below it does — but a face is
        /// addressed in SURFACE metres, so a battered wall presents more of it than a vertical one.
        /// <c>overburden / sin(batter)</c>, the same conversion the kit's README applies to a bank's
        /// height ("an 8 m bank at 48° needs 8/sin(48°) of t").
        ///
        /// <para>This is what makes ONE number express the owner's whole brief. He asked for stratified
        /// faces — eroded topsoil above, red sandstone below — but also said solid rock is right in SOME
        /// places. A soil horizon of fixed depth gives both without a second dial: on St Peters' 10 m
        /// faces it is a sixth of the wall and the cliff reads as rock with a soil lip, while on its
        /// shortest 2.85 m ones it is over half and the same cliff reads as an eroding bank. Height does
        /// the art direction, because on a real coast it does.</para>
        /// </summary>
        public static float OverburdenSurfaceMetres(float overburdenMetres, float batterDegrees)
        {
            float sin = Mathf.Sin(Mathf.Clamp(batterDegrees, 1f, 90f) * Mathf.Deg2Rad);
            return Mathf.Max(0f, overburdenMetres) / Mathf.Max(sin, 0.02f);
        }

        /// <summary>
        /// How many face tiles LONG a face is, measured down its own surface (rule 2). An 8 m bank at 48°
        /// is 10.8 m of surface = 1.2 tiles, and sizing it by the 8 m drop instead is the mistake the
        /// kit's README calls out by name.
        /// </summary>
        public static float TileV(float surfaceMetres, float faceMetresT) =>
            faceMetresT <= 0f ? 0f : surfaceMetres / faceMetresT;

        /// <summary>
        /// The unit direction a column is DRAWN down, brow to drawn toe, in world XY (straight down the screen for
        /// a column with no length). Pure.
        /// </summary>
        public static Vector2 DrawnDown(Vector2 browPlan, Vector2 toeScreen)
        {
            Vector2 d = toeScreen - browPlan;
            float len = d.magnitude;
            return len > 1e-5f ? d / len : Vector2.down;
        }

        /// <summary>
        /// ⭐ <b>What a face's v counts: DRAWN metres down its column</b>, from the band's own top vertex, vertex
        /// to vertex. The owner's ruling of 2026-11-26 (no mixels: a face texel is square and the size of the
        /// ground's on screen) overrides the kit's "v counts SURFACE metres": a face is drawn foreshortened by the
        /// camera's tilt and pushed about by its profile, and surface metres laid on that column are texels shorter
        /// or longer than the meadow's beside them. Counted along the laid vertices, so it only ever grows down a
        /// column and a displaced row keeps the texels it is drawn over. Pure.
        /// </summary>
        public static float DrawnMetres(float metresAbove, Vector2 vertexAbove, Vector2 vertex) =>
            metresAbove + Vector2.Distance(vertexAbove, vertex);

        /// <summary>
        /// How many rows to subdivide a face into so the profile displacement has somewhere to land.
        /// The README asks for 0.25 m along the wall (<c>CliffCatalog.ProfileSubdivideMetres</c>); at
        /// 32 px/m that is 8 texels a step, which is as fine as the displacement map can express.
        /// Always at least one row, so a degenerate sample still yields a quad rather than nothing.
        /// </summary>
        public static int RowsFor(float surfaceMetres, float subdivideMetres) =>
            Mathf.Max(1, Mathf.CeilToInt(surfaceMetres / Mathf.Max(0.01f, subdivideMetres)));

        /// <summary>
        /// The wall's outward PLAN normal at a sample — brow to toe, normalised. The direction profile
        /// displacement pushes along, and the direction whose compass azimuth becomes
        /// <c>_WallAzimuth</c>.
        ///
        /// <para>Falls back to due south on a degenerate (zero-run, vertical) sample: a wall with no plan
        /// run has no derivable facing, and south is the aspect the kit treats as canonical.</para>
        /// </summary>
        public static Vector2 OutwardPlan(in CliffWallSample s)
        {
            Vector2 d = s.ToePlan - s.BrowPlan;
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector2.down;
        }

        /// <summary>
        /// Compass azimuth (degrees, N = 0, clockwise) of a plan direction — the frame the whole cliff
        /// stack is written in (<c>CoastPlan.AspectAzimuths</c>, <c>CliffCatalog.AspectAzimuths</c>, and
        /// the shader's <c>_WallAzimuth</c>). <c>atan2(east, north)</c>, not the other way round: a
        /// mislabelled compass is this repo's most-repeated art defect.
        /// </summary>
        public static float AzimuthOf(Vector2 planDirection)
        {
            if (planDirection.sqrMagnitude < 1e-12f) return 180f;
            float a = Mathf.Atan2(planDirection.x, planDirection.y) * Mathf.Rad2Deg;
            return a < 0f ? a + 360f : a;
        }

        /// <summary>
        /// Decode one texel of the kit's plan-displacement profile into metres:
        /// <c>(grey − 0.5) × 2 × CliffCatalog.ProfileMetres</c>, exactly the decode the baker's grey
        /// encoding was written against.
        ///
        /// <para><b>⚠ The scale is a parameter, not a literal.</b> <c>CliffBaker</c> throws if the rig's
        /// own <c>metres</c> and the catalog's <c>ProfileMetres</c> ever disagree — but the catalog lives
        /// in an Editor assembly this module cannot reference, so the number is passed in and the
        /// builder is what reads it from the one true place.</para>
        /// </summary>
        public static float ProfileMetresFromGrey(float grey01, float profileMetres) =>
            (grey01 - 0.5f) * 2f * profileMetres;

        /// <summary>
        /// The world Y a chunk SORTS at: the lowest drawn toe over its stations.
        ///
        /// <para><b>Why the lowest and not the mean.</b> The order must satisfy two inequalities at once
        /// — over everything at the brow, under everything at the foot — and only the extreme toe
        /// guarantees the second for the whole chunk. Using the mean would put the southern end of every
        /// chunk in front of a player standing at its foot.</para>
        ///
        /// <para>Shared by <see cref="CliffWallSurface"/> and by the pins, so a test asserts the
        /// PROPERTY (a brow sorts behind, a foot sorts in front) rather than restating the formula and
        /// agreeing with itself.</para>
        /// </summary>
        public static float SortY(System.Collections.Generic.IReadOnlyList<CliffWallSample> samples)
        {
            if (samples == null || samples.Count == 0) return 0f;
            float lowest = float.MaxValue;
            for (int i = 0; i < samples.Count; i++)
            {
                CliffWallSample s = samples[i];
                float y = ToeScreen(in s).y;
                if (y < lowest) lowest = y;
            }
            return lowest;
        }

        /// <summary>
        /// How far below its own brow a face's toe is DRAWN, in world units — <c>drop × cos 40°</c>.
        ///
        /// <para><b>⭐ WHY THIS NUMBER IS THE ONE THE SORTING RESTS ON.</b> A chunk carries a single
        /// sorting order and takes it from its LOWEST toe. For that to put the wall over everything on
        /// the clifftop, the chunk's brow must nowhere dip below that toe — and this separation is the
        /// slack it has to do it in. A chunk whose brow wanders further than this in Y would sort behind
        /// the ground it stands on.</para>
        ///
        /// <para><b>⚠ It is a SEPARATION, not a chunk length.</b> An earlier draft used it as the cut,
        /// on the reasoning that the brow's Y moves at most a metre per metre of shore — which is true
        /// but irrelevant, because what makes one order dishonest is the Y a chunk spans, not the shore
        /// it covers, and on an east–west coast those differ by six times. The cut lives in
        /// <c>StPetersCliffWalls.ChunkToeSpanMetres</c> and is measured in Y.</para>
        /// </summary>
        public static float BrowToToeSeparationMetres(float dropMetres) =>
            Mathf.Max(0f, dropMetres) * SpriteLightMath.HeightScale;

        // =============================================================================================
        //  the decals — where the kit's brow and toe strips sit against a face
        // =============================================================================================

        /// <summary>
        /// How far INLAND of the brow, in PLAN metres, a brow strip's top edge sits.
        ///
        /// <para>The kit's brow strip is 12 × 4 m and its sod line — <c>CliffCatalog.BrowLineAt</c>, the
        /// rig's own <c>anchor</c> — sits 42% of the way down it. Above that line the strip is drawing the
        /// PLAN surface of the clifftop (grass, seen from above); below it, the face. So the strip
        /// straddles the brow and this is the plan half of it.</para>
        /// </summary>
        public static float BrowDecalInlandMetres(float stripMetresT, float browLineAt) =>
            Mathf.Max(0f, stripMetresT) * Mathf.Clamp01(browLineAt);

        /// <summary>How far DOWN THE SURFACE of the face a brow strip reaches — the rest of the strip.
        /// This is where the sod's undercut shadow, its hanging clods and the soil wash spilling onto the
        /// rock are drawn, and it is why the strip is measured in surface metres like everything else that
        /// touches a face.</summary>
        public static float BrowDecalFaceMetres(float stripMetresT, float browLineAt) =>
            Mathf.Max(0f, stripMetresT) * (1f - Mathf.Clamp01(browLineAt));

        /// <summary>
        /// Where a toe strip's TOP edge sits, as a depth in surface metres measured UP from the drawn toe
        /// — i.e. the strip occupies the last <paramref name="stripMetresT"/> of the face.
        ///
        /// <para><b>⚠ The kit gives no anchor constant for the toe, unlike <c>BrowLineAt</c> for the
        /// brow.</b> Its own note says what the strip contains — the undercut notch the sea cuts at
        /// high water, the salt-bleached basal beds "in every low-tide photograph", and the debris lying
        /// against the foot — all of which are features OF the bottom of the face. MEASURED on the baked
        /// strip, its alpha is zero across the top quarter and only reaches full weight in the bottom
        /// three-eighths, so seating its bottom edge on the drawn toe puts the notch and the bleached
        /// band where they belong and lets the top fade out onto plain rock. Anything else would have to
        /// invent an anchor the rig does not state.</para>
        /// </summary>
        public static float ToeDecalFaceMetres(float stripMetresT) => Mathf.Max(0f, stripMetresT);
    }
}
