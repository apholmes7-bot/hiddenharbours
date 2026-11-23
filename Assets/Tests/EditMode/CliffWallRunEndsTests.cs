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
    /// <b>Where a cliff run ends, its face tapers into the ground; where two runs almost meet, the face is drawn on
    /// across the slit; where joined chunks swap rock, the front one feathers its rock over the seam.</b> All of it is
    /// read from the chunks' stations alone (<see cref="CliffWallGeometry.RunReachOf"/>), and the mesh arithmetic is
    /// static, so this holds headless with no baked face. The blockers are not touched by any of it.
    /// </summary>
    public class CliffWallRunEndsTests
    {
        const float Drop = 6f;

        static Vector2[] Line(float x0, float x1, int stations)
        {
            var p = new Vector2[stations];
            for (int i = 0; i < stations; i++) p[i] = new Vector2(Mathf.Lerp(x0, x1, i / (float)(stations - 1)), 0f);
            return p;
        }

        static float[] Drops(int n) => Enumerable.Repeat(Drop, n).ToArray();

        static CliffWallSample[] Samples(Vector2[] brow) =>
            brow.Select(b => new CliffWallSample(b, b + new Vector2(0f, -0.5f), Drop, 0f)).ToArray();

        [Test]
        public void AnOpenRunTapersAtBothEndsAndNowhereElse()
        {
            // Three joined 5 m chunks: one run, open at x 0 and x 15.
            var brows = new List<Vector2[]> { Line(0f, 5f, 6), Line(5f, 10f, 6), Line(10f, 15f, 6) };
            var drops = brows.Select(b => Drops(b.Length)).ToList();
            CliffWallGeometry.RunReach a = CliffWallGeometry.RunReachOf(brows, drops, 0);
            CliffWallGeometry.RunReach b = CliffWallGeometry.RunReachOf(brows, drops, 1);
            CliffWallGeometry.RunReach c = CliffWallGeometry.RunReachOf(brows, drops, 2);

            Assert.AreEqual(0f, a.StartDistance, 1e-4f, "the run's first chunk starts at an open end.");
            Assert.AreEqual(5f, b.StartDistance, 1e-4f, "the middle chunk is one chunk from the start.");
            Assert.AreEqual(5f, b.EndDistance, 1e-4f, "the middle chunk is one chunk from the end.");
            Assert.AreEqual(0f, c.EndDistance, 1e-4f, "the run's last chunk ends at an open end.");
            Assert.AreEqual(1, a.JoinedAfter);
            Assert.AreEqual(1, c.JoinedBefore);
            Assert.AreEqual(-1, a.BridgeTo);
            Assert.AreEqual(CliffWallGeometry.RunEndTaperMetres(Drop), a.StartTaperMetres, 1e-4f);
        }

        [Test]
        public void ALongRunIsNotTaperedFarFromItsEnds()
        {
            var brows = new List<Vector2[]>();
            for (int k = 0; k < 8; k++) brows.Add(Line(k * 5f, k * 5f + 5f, 6));   // chunk 3: 15 m and 20 m from the ends
            var drops = brows.Select(b => Drops(b.Length)).ToList();
            CliffWallGeometry.RunReach mid = CliffWallGeometry.RunReachOf(brows, drops, 3);
            Assert.IsTrue(float.IsPositiveInfinity(mid.StartDistance) && float.IsPositiveInfinity(mid.EndDistance),
                          $"a chunk at least 15 m from both ends found an end (start {mid.StartDistance}, end {mid.EndDistance}).");
            CliffWallColumns cols = CliffWallSurface.Unsliced(Samples(brows[3]));
            Assert.IsNull(CliffWallSurface.KeepFor(cols, mid), "a chunk far from its run's ends must draw exactly as before.");
        }

        [Test]
        public void ASlitIsBridgedAndNeitherSideTapers()
        {
            // The East Ledges slit in miniature: a run ends 0.94 m before the next one starts.
            var brows = new List<Vector2[]> { Line(-20f, -10f, 11), Line(-10f, 0f, 11), Line(0.94f, 10f, 11), Line(10f, 20f, 11) };
            var drops = brows.Select(b => Drops(b.Length)).ToList();
            CliffWallGeometry.RunReach before = CliffWallGeometry.RunReachOf(brows, drops, 1);
            CliffWallGeometry.RunReach after = CliffWallGeometry.RunReachOf(brows, drops, 2);
            Assert.AreEqual(2, before.BridgeTo, "the chunk before the slit should draw across it.");
            Assert.IsTrue(float.IsPositiveInfinity(before.EndDistance), "a bridged end is not an open end.");
            Assert.IsTrue(float.IsPositiveInfinity(after.StartDistance), "the far side of a slit is not an open end.");
            Assert.AreEqual(-1, after.BridgeTo);
        }

        [Test]
        public void AGapWiderThanTheBridgeTapersBothSides()
        {
            var brows = new List<Vector2[]> { Line(-10f, 0f, 11), Line(CliffWallGeometry.RunBridgeMetres + 0.5f, 12f, 11) };
            var drops = brows.Select(b => Drops(b.Length)).ToList();
            Assert.AreEqual(0f, CliffWallGeometry.RunReachOf(brows, drops, 0).EndDistance, 1e-4f);
            Assert.AreEqual(0f, CliffWallGeometry.RunReachOf(brows, drops, 1).StartDistance, 1e-4f);
            Assert.AreEqual(-1, CliffWallGeometry.RunReachOf(brows, drops, 0).BridgeTo);
        }

        [Test]
        public void TheTaperedFaceStopsShortAtTheEndAndIsWholePastTheTaper()
        {
            Vector2[] brow = Line(0f, 12f, 25);
            CliffWallColumns cols = CliffWallSurface.Unsliced(Samples(brow));
            var reach = new CliffWallGeometry.RunReach
            {
                StartDistance = 0f, StartTaperMetres = CliffWallGeometry.RunEndTaperMetres(Drop),
                EndDistance = float.PositiveInfinity, BridgeTo = -1, JoinedBefore = -1, JoinedAfter = -1,
            };
            cols.Keep = CliffWallSurface.KeepFor(cols, reach);
            Assert.AreEqual(CliffWallGeometry.RunEndKeep, cols.Keep[0], 1e-5f, "the end column should keep only the lip.");
            for (int c = 1; c < cols.Keep.Length; c++)
                Assert.GreaterOrEqual(cols.Keep[c], cols.Keep[c - 1], "the taper must rise away from the end.");
            Assert.AreEqual(1f, cols.Keep[cols.Keep.Length - 1], 1e-6f, "past the taper the face is whole.");

            const int rows = 8;
            CliffWallMeshData m = CliffWallSurface.FaceMesh(cols, 0f, 0f, rows, 0f, 12f, 9f, (u, t) => 0f, 1f, Vector3.zero);
            CliffWallSample s0 = cols.Whole[0];
            Vector2 expectFoot = Vector2.Lerp(s0.BrowPlan, CliffWallGeometry.ToeScreen(in s0), CliffWallGeometry.RunEndKeep);
            Vector3 foot = m.Verts[rows];
            Assert.AreEqual(expectFoot.x, foot.x, 1e-4f);
            Assert.AreEqual(expectFoot.y, foot.y, 1e-4f, "the end column's foot should sit at the kept depth, not the toe.");
            int lastFoot = (cols.Whole.Length - 1) * (rows + 1) + rows;
            CliffWallSample sN = cols.Whole[cols.Whole.Length - 1];
            Assert.AreEqual(CliffWallGeometry.ToeScreen(in sN).y, m.Verts[lastFoot].y, 1e-4f, "past the taper the foot is the drawn toe.");
            Assert.IsNotNull(m.Colors, "a tapered face fades into the ground through its vertex alpha.");
            Assert.AreEqual(0f, m.Colors[rows].a, 1e-5f, "at the open end the face's foot fades out completely.");
            for (int r = 1; r <= rows; r++)
                Assert.LessOrEqual(m.Colors[r].a, m.Colors[r - 1].a + 1e-6f, "at the end the face fades from the brow down to its foot.");
            Assert.Greater(m.Colors[0].a, 0f, "the lip at the very end still shows a trace of rock.");
            for (int r = 0; r <= rows; r++)
                Assert.AreEqual(1f, m.Colors[(cols.Whole.Length - 1) * (rows + 1) + r].a, 1e-5f, "past the taper nothing fades.");

            CliffWallMeshData plain = CliffWallSurface.FaceMesh(CliffWallSurface.Unsliced(cols.Whole), 0f, 0f, rows, 0f, 12f, 9f,
                                                                (u, t) => 0f, 1f, Vector3.zero);
            Assert.IsNull(plain.Colors, "a face with no taper writes no vertex colours.");
        }

        [Test]
        public void AShortRunStillStandsFullHeightInItsMiddle()
        {
            // One 6 m chunk, open at both ends: the tapers share the run instead of fading all of it.
            var brows = new List<Vector2[]> { Line(0f, 6f, 13), Line(40f, 50f, 11) };
            var drops = brows.Select(b => Drops(b.Length)).ToList();
            CliffWallGeometry.RunReach r = CliffWallGeometry.RunReachOf(brows, drops, 0);
            CliffWallColumns cols = CliffWallSurface.Unsliced(Samples(brows[0]));
            float[] keep = CliffWallSurface.KeepFor(cols, r);
            Assert.AreEqual(CliffWallGeometry.RunEndKeep, keep[0], 1e-5f);
            Assert.AreEqual(CliffWallGeometry.RunEndKeep, keep[keep.Length - 1], 1e-5f);
            Assert.AreEqual(1f, keep[keep.Length / 2], 1e-5f, "the middle of a short run should stand at full height.");
        }

        [Test]
        public void NoKeepLaysTheFaceExactlyAsBefore()
        {
            CliffWallColumns cols = CliffWallSurface.Unsliced(Samples(Line(0f, 6f, 13)));
            System.Func<float, float, float> profile = (u, t) => 0.2f * Mathf.Sin(u * 7f + t * 3f);
            CliffWallMeshData plain = CliffWallSurface.FaceMesh(cols, 0f, 0f, 6, 1.5f, 12f, 9f, profile, 1f, Vector3.zero);
            cols.Keep = Enumerable.Repeat(1f, cols.Whole.Length).ToArray();
            CliffWallMeshData kept = CliffWallSurface.FaceMesh(cols, 0f, 0f, 6, 1.5f, 12f, 9f, profile, 1f, Vector3.zero);
            CollectionAssert.AreEqual(plain.Verts, kept.Verts);
            CollectionAssert.AreEqual(plain.Uvs, kept.Uvs);
            CollectionAssert.AreEqual(plain.Tris, kept.Tris);
        }

        [Test]
        public void ASeamFeatherFadesFromTheJoinToNothing()
        {
            CliffWallSample[] neighbour = Samples(Line(0f, 5f, 21));
            CliffWallColumns ahead = CliffWallSurface.FeatherColumns(neighbour, fromFirst: true, out float len);
            Assert.AreEqual(CliffWallGeometry.RunSeamFeatherMetres, len, 1e-4f);
            Assert.AreEqual(1f, ahead.Alpha[0], 1e-6f, "the feather is solid at the join.");
            Assert.AreEqual(0f, ahead.Alpha[ahead.Alpha.Length - 1], 1e-6f, "the feather fades to nothing.");
            Assert.AreEqual(0f, ahead.Whole[0].BrowPlan.x, 1e-6f, "a feather ahead starts at the join.");
            Assert.AreEqual(len, ahead.Whole[ahead.Whole.Length - 1].BrowPlan.x, 1e-4f);

            CliffWallColumns behind = CliffWallSurface.FeatherColumns(neighbour, fromFirst: false, out float len2);
            Assert.AreEqual(len, len2, 1e-4f);
            Assert.AreEqual(0f, behind.Alpha[0], 1e-6f, "a feather behind fades in toward the join.");
            Assert.AreEqual(1f, behind.Alpha[behind.Alpha.Length - 1], 1e-6f);
            Assert.AreEqual(5f, behind.Whole[behind.Whole.Length - 1].BrowPlan.x, 1e-6f, "a feather behind ends at the join.");

            CliffWallMeshData m = CliffWallSurface.FaceMesh(ahead, 0f, 0f, 4, 0f, 12f, 9f, (u, t) => 0f, 1f, Vector3.zero);
            Assert.IsNotNull(m.Colors);
            Assert.AreEqual(m.Verts.Length, m.Colors.Length);
            Assert.AreEqual(1f, m.Colors[0].a, 1e-6f);
            Assert.AreEqual(0f, m.Colors[m.Colors.Length - 1].a, 1e-6f);
        }

        [Test]
        public void StPeters_RunEndsAndTheOneSlit()
        {
            Measure(StPetersCliffWalls.LoadDefs(), StPetersTerrainPlan.LoadPlan().WallToeSpanMetres,
                    out int open, out List<(Vector2 From, float Gap)> bridges);
            // Eight run ends: the East Ledges slit's two sides are bridged, the other six taper.
            Assert.AreEqual(6, open, "St Peters open run ends.");
            Assert.AreEqual(1, bridges.Count, "St Peters slits: " + string.Join(", ", bridges));
            Assert.AreEqual(124.2f, bridges[0].From.x, 0.5f, "the bridged slit should be the East Ledges one near x 124.");
            Assert.Less(bridges[0].Gap, 1f);
        }

        [Test]
        public void NineMileCreek_RunEndsAndTheOneSlit()
        {
            Measure(NineMileCreekCliffWalls.LoadDefs(), NineMileCreekCliffWalls.ChunkToeSpanMetres,
                    out int open, out List<(Vector2 From, float Gap)> bridges);
            Assert.AreEqual(4, open, "Nine Mile Creek open run ends.");
            Assert.AreEqual(1, bridges.Count, "Nine Mile Creek slits: " + string.Join(", ", bridges));
            Assert.Less(bridges[0].Gap, 0.5f);
        }

        static void Measure(List<CliffWallDef> defs, float toeSpanMetres, out int open, out List<(Vector2, float)> bridges)
        {
            List<StPetersCliffWalls.Chunk> chunks = StPetersCliffWalls.ChunksOfDefs(defs, toeSpanMetres, out _);
            Assert.Greater(chunks.Count, 1, "no wall chunks to measure.");
            var brows = new List<Vector2[]>();
            var drops = new List<float[]>();
            foreach (StPetersCliffWalls.Chunk ch in chunks)
            {
                StPetersCliffWalls.ChunkFields f = StPetersCliffWalls.FieldsOf(in ch);
                brows.Add(f.BrowPlan);
                drops.Add(f.DropMetres);
            }
            open = 0;
            bridges = new List<(Vector2, float)>();
            for (int k = 0; k < brows.Count; k++)
            {
                if (brows[k] == null || brows[k].Length < 2) continue;
                CliffWallGeometry.RunReach r = CliffWallGeometry.RunReachOf(brows, drops, k);
                if (r.StartDistance == 0f) open++;
                if (r.EndDistance == 0f) open++;
                if (r.BridgeTo >= 0)
                {
                    Vector2 from = brows[k][brows[k].Length - 1];
                    bridges.Add((from, Vector2.Distance(from, brows[r.BridgeTo][0])));
                }
            }
        }
    }
}
