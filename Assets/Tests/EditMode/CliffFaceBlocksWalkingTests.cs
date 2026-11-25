using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The brow and the foot of a cliff are not walkable from each other.</b> The face is drawn from its brow down to
    /// its drawn toe, and what the face covers nobody stands on (ruling t10). Only the drawn toe used to carry a
    /// blocker, so from every St Peters clifftop the walker went over the brow and down the plunge into the water hidden
    /// under the face, and where a run ended he walked up the face's open side onto the plateau. Each chunk now fills
    /// its whole face (<see cref="CliffWallSurface.FaceBlockerChildName"/>). This holds that, headless:
    ///
    /// <list type="bullet">
    /// <item><b>Every face is solid, brow to foot:</b> for every live wall chunk, laid from its Def with no baked face
    /// (as CI lays it), points across every segment of its face, on the brow line, on the drawn toe line and on the end
    /// lines, lie inside the face blocker's colliders. A walk from the clifftop to the sea (or back) across any segment
    /// has to pass through solid.</item>
    /// <item><b>The intended ways stay open:</b> no St Peters path crosses a face, except the crossings listed in
    /// <see cref="IntendedCrossings"/> (none today).</item>
    /// </list>
    /// </summary>
    public class CliffFaceBlocksWalkingTests
    {
        /// <summary>Points this close to a face's own edge are measured just inside it (m).</summary>
        const float EdgeInsetMetres = 0.02f;
        /// <summary>Samples across a segment, each way.</summary>
        const int SamplesAlong = 4, SamplesDown = 6;
        /// <summary>How finely a path is walked for the crossing check (m).</summary>
        const float PathStepMetres = 0.25f;

        /// <summary>Paths that are meant to cross a face, by path id. None today: every way up or down at St Peters is a
        /// gap between runs (the West Gap and East Gap paths, the main beach steps, the slip road and the wharf).</summary>
        static readonly HashSet<string> IntendedCrossings = new HashSet<string>();

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void Clean()
        {
            foreach (GameObject go in _made)
                if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        [Test]
        public void StPeters_EveryCliffFaceIsSolidFromBrowToFoot() =>
            PinSolid("St Peters", StPetersCliffWalls.LoadDefs(), StPetersTerrainPlan.LoadPlan().WallToeSpanMetres);

        [Test]
        public void NineMileCreek_EveryCliffFaceIsSolidFromBrowToFoot() =>
            PinSolid("Nine Mile Creek", NineMileCreekCliffWalls.LoadDefs(), NineMileCreekCliffWalls.ChunkToeSpanMetres);

        [Test]
        public void StPeters_NoPathCrossesACliffFace()
        {
            List<StPetersCliffWalls.Chunk> chunks = StPetersCliffWalls.ChunksOfDefs(
                StPetersCliffWalls.LoadDefs(), StPetersTerrainPlan.LoadPlan().WallToeSpanMetres, out List<CliffWallDef> owners);
            Assert.Greater(chunks.Count, 0, "St Peters lays no wall chunks from its Defs.");
            var faces = new List<(string Id, Vector2[] Piece)>();
            foreach (CliffWallDef d in owners.Distinct())
            {
                Vector2[] toe = DrawnToeOfDef(d);
                for (int s = 0; s + 1 < toe.Length; s++)
                    foreach (Vector2[] p in CliffWallGeometry.FacePieces(d.Brow[s], d.Brow[s + 1], toe[s + 1], toe[s]))
                        faces.Add((d.Id, p));
            }

            PathDef[] paths = StPetersTerrainPlan.LoadPlan().Paths;
            Assert.IsNotNull(paths, "the St Peters plan carries no paths.");
            Assert.Greater(paths.Length, 0, "the St Peters plan carries no paths: this pin would measure nothing.");
            var problems = new List<string>();
            int walked = 0;
            foreach (PathDef path in paths)
            {
                if (path == null || path.Points == null || path.Points.Length < 2) continue;
                walked++;
                string hit = FirstFaceOn(path.Points, faces, out Vector2 at);
                if (hit == null) continue;
                if (IntendedCrossings.Contains(path.Id)) continue;
                problems.Add($"{path.Id} crosses the face of {hit} at ({at.x:0.##}, {at.y:0.##}): the new face blocker would cut this way.");
            }
            Assert.Greater(walked, 0, "no St Peters path has two points to walk.");
            Assert.That(problems, Is.Empty, $"{problems.Count} path(s) cross a cliff face:\n  " + string.Join("\n  ", problems));
        }

        [Test]
        public void AFaceThatFoldsOverItselfIsStillCovered()
        {
            // The drawn toe crosses back over the brow: a bow-tie quad. Both lobes must be covered.
            Vector2 b0 = new Vector2(0f, 0f), b1 = new Vector2(2f, 0f), t1 = new Vector2(0f, -2f), t0 = new Vector2(2f, -2f);
            List<Vector2[]> pieces = CliffWallGeometry.FacePieces(b0, b1, t1, t0);
            Assert.Greater(pieces.Count, 1, "a crossed quad should be split into triangles.");
            foreach (Vector2 p in new[] { new Vector2(1f, -0.3f), new Vector2(1f, -1.7f), new Vector2(0.3f, -1f), new Vector2(1.7f, -1f) })
                Assert.IsTrue(pieces.Any(q => Inside(p, q)), $"({p.x}, {p.y}) is in the face but in no piece.");
        }

        [Test]
        public void ARebuildLeavesOneFaceBlocker()
        {
            List<StPetersCliffWalls.Chunk> chunks = StPetersCliffWalls.ChunksOfDefs(
                StPetersCliffWalls.LoadDefs(), StPetersTerrainPlan.LoadPlan().WallToeSpanMetres, out List<CliffWallDef> owners);
            Assert.Greater(chunks.Count, 0, "St Peters lays no wall chunks from its Defs.");
            CliffWallSurface surface = Lay(chunks[0], owners[0]);
            int first = surface.FaceBlockerColliderCount;
            Lay(chunks[0], owners[0], surface);
            int count = 0;
            foreach (Transform child in surface.transform)
                if (child.name == CliffWallSurface.FaceBlockerChildName) count++;
            Assert.AreEqual(1, count, $"'{owners[0].Id}' carries {count} face blockers after a second build.");
            Assert.AreEqual(first, surface.FaceBlockerColliderCount, "a second build changed the face blocker's collider count.");
        }

        // =============================================================================================

        void PinSolid(string region, List<CliffWallDef> defs, float toeSpanMetres)
        {
            List<StPetersCliffWalls.Chunk> chunks = StPetersCliffWalls.ChunksOfDefs(defs, toeSpanMetres, out List<CliffWallDef> owners);
            Assert.Greater(chunks.Count, 0, $"{region} lays no wall chunks from its Defs: this pin would measure nothing.");

            var problems = new List<string>();
            int samples = 0;
            for (int k = 0; k < chunks.Count; k++)
            {
                CliffWallDef owner = owners[k];
                CliffWallSurface surface = Lay(chunks[k], owner);
                Transform face = surface.transform.Find(CliffWallSurface.FaceBlockerChildName);
                if (face == null) { problems.Add($"{owner.Id}: no face blocker: from the clifftop he walks over the brow."); continue; }
                if (face.gameObject.layer != CliffWallSurface.BlockerLayer)
                    problems.Add($"{owner.Id}: the face blocker is on layer {LayerMask.LayerToName(face.gameObject.layer)}, not the toe blocker's.");

                var solid = new List<Vector2[]>();
                foreach (PolygonCollider2D c in face.GetComponents<PolygonCollider2D>())
                {
                    if (c.isTrigger) { problems.Add($"{owner.Id}: a face piece is a trigger, which stops nobody."); continue; }
                    for (int p = 0; p < c.pathCount; p++)
                        solid.Add(c.GetPath(p).Select(v => (Vector2)c.transform.TransformPoint(v)).ToArray());
                }

                Vector2[] brow = owner.Brow, toe = DrawnToeOfDef(owner);
                int missed = 0;
                string firstMiss = null;
                for (int s = 0; s + 1 < toe.Length; s++)
                {
                    // Only where the face has area: a repeated station is not a wall.
                    if (CliffWallGeometry.FacePieces(brow[s], brow[s + 1], toe[s + 1], toe[s]).Count == 0) continue;
                    for (int i = 0; i <= SamplesAlong; i++)
                        for (int j = 0; j <= SamplesDown; j++)
                        {
                            float u = Mathf.Lerp(EdgeInset(brow[s], brow[s + 1]), 1f - EdgeInset(brow[s], brow[s + 1]), i / (float)SamplesAlong);
                            float v = Mathf.Lerp(EdgeInset(brow[s], toe[s]), 1f - EdgeInset(brow[s], toe[s]), j / (float)SamplesDown);
                            Vector2 top = Vector2.Lerp(brow[s], brow[s + 1], u), foot = Vector2.Lerp(toe[s], toe[s + 1], u);
                            Vector2 p = Vector2.Lerp(top, foot, v);
                            if (!InsideAny(p, CliffWallGeometry.FacePieces(brow[s], brow[s + 1], toe[s + 1], toe[s])))
                                continue;                                // a fold's empty middle is not face
                            samples++;
                            if (solid.Any(q => Inside(p, q))) continue;
                            missed++;
                            if (firstMiss == null) firstMiss = $"segment {s} at ({p.x:0.###}, {p.y:0.###})";
                        }
                }
                if (missed > 0)
                    problems.Add($"{owner.Id}: {missed} point(s) of the drawn face stand on no blocker, first {firstMiss}: he can walk there.");
            }

            Debug.Log($"[cliff-face-blocker] {region}: {chunks.Count} chunks, {samples} face points measured.");
            Assert.Greater(samples, 0, $"{region}: no face point was measured.");
            Assert.That(problems, Is.Empty,
                $"{region}: {problems.Count} face(s) can be walked over ({chunks.Count} chunks):\n  " + string.Join("\n  ", problems));
        }

        static float EdgeInset(Vector2 a, Vector2 b)
        {
            float len = Vector2.Distance(a, b);
            return len > 1e-4f ? Mathf.Min(0.25f, EdgeInsetMetres / len) : 0f;
        }

        CliffWallSurface Lay(StPetersCliffWalls.Chunk chunk, CliffWallDef owner, CliffWallSurface onto = null)
        {
            StPetersCliffWalls.ChunkFields f = StPetersCliffWalls.FieldsOf(in chunk);
            CliffWallSurface surface = onto;
            if (surface == null)
            {
                var go = new GameObject(StPetersCliffWalls.WallName(owner));
                _made.Add(go);
                go.transform.position = new Vector3(f.BrowPlan[0].x, f.BrowPlan[0].y, 0f);
                surface = go.AddComponent<CliffWallSurface>();
            }
            // No channels and no material, as CI lays it: the face skips (and says so); the blockers must not.
            surface.Configure(f.BrowPlan, f.ToePlan, f.DropMetres, null, new CliffFaceBand[0], null, null, null,
                              f.AlongOffsetMetres, f.RowsBasisSurfaceMetres, f.WallAzimuth, f.Batter, Vector3.zero,
                              1f, 1f, 0.25f, 1f, 1f, 0.5f,
                              f.ToeElevations, f.TextureBrowPlan, f.SliceAt, f.SliceBefore, f.SliceAfter);
            return surface;
        }

        /// <summary>The Def's own drawn toe: plan toe, y pushed down-screen by drop × cos(camera elevation).</summary>
        static Vector2[] DrawnToeOfDef(CliffWallDef d)
        {
            float heightScale = Mathf.Cos(SpriteLightMath.CameraElevationDeg * Mathf.Deg2Rad);
            var toe = new Vector2[d.Toe.Length];
            for (int i = 0; i < toe.Length; i++) toe[i] = new Vector2(d.Toe[i].x, d.Toe[i].y - d.DropMetres[i] * heightScale);
            return toe;
        }

        static string FirstFaceOn(Vector2[] line, List<(string Id, Vector2[] Piece)> faces, out Vector2 at)
        {
            for (int i = 0; i + 1 < line.Length; i++)
            {
                float len = Vector2.Distance(line[i], line[i + 1]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / PathStepMetres));
                for (int k = 0; k <= steps; k++)
                {
                    Vector2 p = Vector2.Lerp(line[i], line[i + 1], k / (float)steps);
                    foreach ((string id, Vector2[] piece) in faces)
                        if (Inside(p, piece)) { at = p; return id; }
                }
            }
            at = default;
            return null;
        }

        static bool InsideAny(Vector2 p, List<Vector2[]> pieces) => pieces.Any(q => Inside(p, q));

        static bool Inside(Vector2 p, Vector2[] q)
        {
            bool inside = false;
            for (int i = 0, j = q.Length - 1; i < q.Length; j = i++)
                if (((q[i].y > p.y) != (q[j].y > p.y)) &&
                    (p.x < (q[j].x - q[i].x) * (p.y - q[i].y) / (q[j].y - q[i].y) + q[i].x))
                    inside = !inside;
            return inside;
        }
    }
}
