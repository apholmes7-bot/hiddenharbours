using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>A CLIFF FACE DRAWN OVER THE SEA IS A WALL (ruling t10, 2026-11-08).</b> The face is drawn down-screen of
    /// its plan toe by its drop × cos 40°, so at St Peters' West Ledges (<c>wall.stp_058</c>) it covers 5.9 m of sea
    /// inside the walker's swim band, and the piece-2 walk swam her in behind it, out of sight. Every chunk now stands
    /// a blocker on its DRAWN toe. This holds the blocker to the Defs, chunk by chunk, headless:
    ///
    /// <list type="bullet">
    /// <item><b>Its vertices are the drawn toe:</b> each quad's centreline runs from one station's drawn toe to the
    /// next, within 1 cm, where the drawn toe is worked here straight from the Def's own <c>Toe</c> and
    /// <c>DropMetres</c> (plan y less drop × cos of the camera's 40°), not from the kit's code.</item>
    /// <item><b>It is continuous across joined chunks:</b> where a wall follows another in a run, its blocker starts
    /// on the point the other's ends on (1 cm): no gap at 058, 241, 057 or anywhere else.</item>
    /// <item><b>It is the quay wall's kind:</b> one solid quad per segment, <c>PropertyBoundary</c>'s thickness, on
    /// Default (the layer the quay wall stands on and the walker's foot collides with).</item>
    /// <item><b>It stands without the face:</b> the chunks here are laid with no baked channels and no material, as a
    /// checkout that has never baked the kit's PNGs is, and the blocker is there all the same.</item>
    /// </list>
    ///
    /// The walk that proves she is stopped by it is <c>StPetersMainBeachWalkPlayTests</c>' face leg.
    /// </summary>
    public class CliffWallToeBlockerTests
    {
        const float WithinMetres = 0.01f;
        const float ThicknessWithinMetres = 0.001f;

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void Clean()
        {
            foreach (GameObject go in _made)
                if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        [Test]
        public void StPeters_EveryWallChunkStandsABlockerOnItsDrawnToe() =>
            Pin("St Peters", StPetersCliffWalls.LoadDefs(), StPetersTerrainPlan.LoadPlan().WallToeSpanMetres);

        [Test]
        public void NineMileCreek_EveryWallChunkStandsABlockerOnItsDrawnToe() =>
            Pin("Nine Mile Creek", NineMileCreekCliffWalls.LoadDefs(), NineMileCreekCliffWalls.ChunkToeSpanMetres);

        [Test]
        public void TheBlockerIsAsThickAsTheQuayWall() =>
            Assert.AreEqual(PropertyBoundary.DefaultThicknessMetres, CliffWallGeometry.BlockerThicknessMetres, 0f,
                "the cliff blocker restates PropertyBoundary's wall thickness (Art cannot reach World); they have drifted.");

        [Test]
        public void ARebuildLeavesOneBlocker()
        {
            List<StPetersCliffWalls.Chunk> chunks =
                StPetersCliffWalls.ChunksOfDefs(StPetersCliffWalls.LoadDefs(), StPetersTerrainPlan.LoadPlan().WallToeSpanMetres,
                                                out List<CliffWallDef> owners);
            Assert.Greater(chunks.Count, 0, "St Peters lays no wall chunks from its Defs.");
            CliffWallSurface surface = Lay(chunks[0], owners[0]);
            int first = surface.BlockerColliderCount;
            Lay(chunks[0], owners[0], surface);                         // configure the same chunk again
            int blockers = 0;
            foreach (Transform child in surface.transform)
                if (child.name == CliffWallSurface.BlockerChildName) blockers++;
            Assert.AreEqual(1, blockers, $"'{owners[0].Id}' carries {blockers} blockers after a second build: a rebuild stacked one.");
            Assert.AreEqual(first, surface.BlockerColliderCount, "a second build changed the blocker's quad count.");
        }

        // =============================================================================================

        CliffWallSurface Lay(StPetersCliffWalls.Chunk chunk, CliffWallDef owner, CliffWallSurface onto = null)
        {
            StPetersCliffWalls.ChunkFields f = StPetersCliffWalls.FieldsOf(in chunk);
            CliffWallSurface surface = onto;
            if (surface == null)
            {
                var go = new GameObject(StPetersCliffWalls.WallName(owner));
                _made.Add(go);
                // As the builder parks it: at its own first brow, translation only.
                go.transform.position = new Vector3(f.BrowPlan[0].x, f.BrowPlan[0].y, 0f);
                surface = go.AddComponent<CliffWallSurface>();
            }
            // No channels and no material: the face skips (and says so); the blocker must not.
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

        void Pin(string region, List<CliffWallDef> defs, float toeSpanMetres)
        {
            List<StPetersCliffWalls.Chunk> chunks = StPetersCliffWalls.ChunksOfDefs(defs, toeSpanMetres, out List<CliffWallDef> owners);
            Assert.Greater(chunks.Count, 0, $"{region} lays no wall chunks from its Defs: this pin would measure nothing.");

            var problems = new List<string>();
            float half = CliffWallGeometry.BlockerThicknessMetres * 0.5f;
            int quads = 0, joins = 0;
            Vector2 lastEnd = default;
            int lastRun = -1;
            string lastId = null;

            for (int k = 0; k < chunks.Count; k++)
            {
                CliffWallDef owner = owners[k];
                CliffWallSurface surface = Lay(chunks[k], owner);
                Vector2[] want = DrawnToeOfDef(owner);

                Transform blocker = surface.transform.Find(CliffWallSurface.BlockerChildName);
                if (blocker == null) { problems.Add($"{owner.Id}: no blocker at all."); continue; }
                if (blocker.gameObject.layer != CliffWallSurface.BlockerLayer || CliffWallSurface.BlockerLayer != 0)
                    problems.Add($"{owner.Id}: the blocker is on layer {LayerMask.LayerToName(blocker.gameObject.layer)}, not Default.");

                PolygonCollider2D[] colliders = blocker.GetComponents<PolygonCollider2D>();
                if (colliders.Length != want.Length - 1)
                {
                    problems.Add($"{owner.Id}: {colliders.Length} quads for {want.Length - 1} drawn-toe segments.");
                    continue;
                }
                quads += colliders.Length;

                Vector2 firstStart = default, end = default;
                for (int s = 0; s < colliders.Length; s++)
                {
                    PolygonCollider2D c = colliders[s];
                    if (c.isTrigger) problems.Add($"{owner.Id} quad {s}: a trigger, which stops nobody.");
                    if (c.pathCount != 1 || c.GetPath(0).Length != 4) { problems.Add($"{owner.Id} quad {s}: not one four-point path."); continue; }
                    Vector2[] p = c.GetPath(0);
                    for (int i = 0; i < 4; i++) p[i] = c.transform.TransformPoint(p[i]);
                    // The quad is a − ext ± side .. b + ext ± side: its short sides' midpoints, pulled in by half a
                    // thickness along it, are the two points it stands between.
                    Vector2 aOut = (p[0] + p[3]) * 0.5f, bOut = (p[1] + p[2]) * 0.5f;
                    Vector2 along = (bOut - aOut).normalized;
                    Vector2 a = aOut + along * half, b = bOut - along * half;
                    float width = Vector2.Distance(p[0], p[3]);
                    if (Mathf.Abs(width - CliffWallGeometry.BlockerThicknessMetres) > ThicknessWithinMetres)
                        problems.Add($"{owner.Id} quad {s}: {width:0.####} m thick.");
                    float da = Vector2.Distance(a, want[s]), db = Vector2.Distance(b, want[s + 1]);
                    if (da > WithinMetres || db > WithinMetres)
                        problems.Add($"{owner.Id} quad {s}: stands from ({a.x:0.###}, {a.y:0.###}) to ({b.x:0.###}, {b.y:0.###}); " +
                                     $"the drawn toe runs ({want[s].x:0.###}, {want[s].y:0.###}) to ({want[s + 1].x:0.###}, {want[s + 1].y:0.###}) " +
                                     $"({da * 100f:0.##} / {db * 100f:0.##} cm off).");
                    if (s == 0) firstStart = a;
                    end = b;
                }

                Vector2[] drawn = surface.DrawnToe();
                for (int i = 0; i < drawn.Length && i < want.Length; i++)
                    if (Vector2.Distance(drawn[i], want[i]) > WithinMetres)
                        problems.Add($"{owner.Id}: DrawnToe()[{i}] is {Vector2.Distance(drawn[i], want[i]) * 100f:0.##} cm off the Def's drawn toe.");

                if (chunks[k].RunIndex == lastRun)
                {
                    joins++;
                    if (owner.Follows != lastId)
                        problems.Add($"{owner.Id} is laid after {lastId} in one run but follows '{owner.Follows}'.");
                    float gap = Vector2.Distance(firstStart, lastEnd);
                    if (gap > WithinMetres)
                        problems.Add($"{owner.Id} joins {lastId}: the blockers leave a {gap * 100f:0.##} cm gap at the join.");
                }
                lastRun = chunks[k].RunIndex;
                lastId = owner.Id;
                lastEnd = end;
            }

            Debug.Log($"[cliff-blocker] {region}: {chunks.Count} chunks, {quads} blocker quads, {joins} joins.");
            Assert.That(problems, Is.Empty,
                $"{region}: {problems.Count} blocker problem(s) over {chunks.Count} chunks ({quads} quads, {joins} joins):\n  " +
                string.Join("\n  ", problems));
        }
    }
}
