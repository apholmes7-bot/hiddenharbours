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
    /// <b>A face is not drawn over a clifftop that stands in front of it.</b> The ground is drawn in plan and a face hangs
    /// down-screen from its brow, so a face whose brow turns back into its own land, or that is drawn on across the next
    /// clifftop, would paint rock over grass (the West Ledges bay). The cut is read from the stations alone
    /// (<see cref="CliffWallGeometry.OcclusionKeepAt"/>, <see cref="CliffWallSurface.RunClipFor"/>), so this holds
    /// headless. Steps (stations stacked on one brow point) draw no face. The blockers are not touched by any of it.
    /// </summary>
    public class CliffWallOcclusionTests
    {
        const float Drop = 6f;

        static Vector2[] Line(float x0, float x1, float y, int stations)
        {
            var p = new Vector2[stations];
            for (int i = 0; i < stations; i++) p[i] = new Vector2(Mathf.Lerp(x0, x1, i / (float)(stations - 1)), y);
            return p;
        }

        /// <summary>A wall along <paramref name="brow"/> whose toe lies <paramref name="toeY"/> off it (negative: south,
        /// facing the camera).</summary>
        static CliffWallSample[] Wall(Vector2[] brow, float toeY, float drop = Drop, float toeElevation = 0f) =>
            brow.Select(b => new CliffWallSample(b, b + new Vector2(0f, toeY), drop, toeElevation)).ToArray();

        static float[] Clip(CliffWallSample[] mine, bool known, params CliffWallSample[][] others)
        {
            var run = new List<CliffWallSample[]> { mine };
            run.AddRange(others);
            var reach = new CliffWallGeometry.RunReach { BridgeTo = -1, JoinedBefore = -1, JoinedAfter = -1 };
            return CliffWallSurface.RunClipFor(CliffWallSurface.Unsliced(mine), run,
                                               Enumerable.Repeat(known, run.Count).ToList(), 0, reach, out _);
        }

        [Test]
        public void AFaceTurnedAwayFromTheCameraIsNotDrawn()
        {
            // A north-facing wall seen from the south: its face would hang down over its own clifftop.
            float[] clip = Clip(Wall(Line(0f, 5f, 0f, 6), toeY: 0.5f), known: false);
            Assert.IsNotNull(clip);
            foreach (float k in clip) Assert.AreEqual(0f, k, 1e-6f, "a face turned into its own clifftop shows nothing.");
        }

        [Test]
        public void AFaceTowardTheCameraIsDrawnWhole()
        {
            Assert.IsNull(Clip(Wall(Line(0f, 5f, 0f, 6), toeY: -0.5f), known: false),
                          "a lone south-facing wall must draw exactly as before.");
        }

        [Test]
        public void AFaceIsCutWhereItCrossesTheClifftopInFront()
        {
            // The far wall faces the camera; 2 m in front of it a raised spit's north wall (land to the south).
            CliffWallSample[] far = Wall(Line(0f, 5f, 0f, 6), toeY: -0.5f);
            CliffWallSample[] spit = Wall(Line(-1f, 6f, -2f, 8), toeY: 0.5f);
            float[] clip = Clip(far, false, spit);
            Assert.IsNotNull(clip);
            float expect = 2f / -CliffWallGeometry.ToeScreen(in far[0]).y;
            foreach (float k in clip) Assert.AreEqual(expect, k, 1e-3f, "the face should stop at the spit's brow line.");
        }

        [Test]
        public void ALowLedgeInFrontDoesNotHideTheFaceBehindIt()
        {
            CliffWallSample[] far = Wall(Line(0f, 5f, 0f, 6), toeY: -0.5f, drop: Drop, toeElevation: 0f);
            CliffWallSample[] ledge = Wall(Line(-1f, 6f, -2f, 8), toeY: 0.5f, drop: 0.5f, toeElevation: 0.5f);
            Assert.IsNull(Clip(far, true, ledge), "a 1 m ledge cannot hide a face 3.6 m up.");
            CliffWallSample[] high = Wall(Line(-1f, 6f, -2f, 8), toeY: 0.5f, drop: 1f, toeElevation: 5f);
            Assert.IsNotNull(Clip(far, true, high), "a clifftop as high as the face hides it.");
        }

        [Test]
        public void AStepDrawsNoFace()
        {
            // Stations stacked on one brow point, the drop falling from one face's height to the other's.
            var step = new[]
            {
                new CliffWallSample(new Vector2(0f, 0f), new Vector2(0f, -0.5f), 1.5f, 4.5f),
                new CliffWallSample(new Vector2(0.02f, 0f), new Vector2(0.02f, -0.5f), 1.8f, 4.2f),
                new CliffWallSample(new Vector2(0.04f, 0f), new Vector2(0.04f, -0.5f), 2.1f, 3.9f),
            };
            Assert.IsTrue(CliffWallGeometry.IsStep(step));
            Assert.IsFalse(CliffWallGeometry.IsStep(Wall(Line(0f, 0.3f, 0f, 2), -0.5f)), "a short level chunk is no step.");
            Assert.IsFalse(CliffWallGeometry.IsStep(Wall(Line(0f, 5f, 0f, 6), -0.5f)));
            float[] clip = Clip(step, true);
            Assert.IsNotNull(clip);
            foreach (float k in clip) Assert.AreEqual(0f, k);
        }

        [Test]
        public void TheCutStopsTheFootAndAWholeClipLaysTheFaceAsBefore()
        {
            CliffWallColumns cols = CliffWallSurface.Unsliced(Wall(Line(0f, 6f, 0f, 13), -0.5f));
            System.Func<float, float, float> profile = (u, t) => 0.2f * Mathf.Sin(u * 7f + t * 3f);
            CliffWallMeshData plain = CliffWallSurface.FaceMesh(cols, 0f, 0f, 6, 1.5f, 12f, 9f, profile, 1f, Vector3.zero);
            cols.Clip = Enumerable.Repeat(1f, cols.Whole.Length).ToArray();
            CliffWallMeshData whole = CliffWallSurface.FaceMesh(cols, 0f, 0f, 6, 1.5f, 12f, 9f, profile, 1f, Vector3.zero);
            CollectionAssert.AreEqual(plain.Verts, whole.Verts);
            CollectionAssert.AreEqual(plain.Tris, whole.Tris);

            const int rows = 8;
            cols.Clip[0] = 0.25f;
            CliffWallMeshData cut = CliffWallSurface.FaceMesh(cols, 0f, 0f, rows, 0f, 12f, 9f, (u, t) => 0f, 1f, Vector3.zero);
            CliffWallSample s0 = cols.Whole[0];
            Vector2 foot = Vector2.Lerp(s0.BrowPlan, CliffWallGeometry.ToeScreen(in s0), 0.25f);
            Assert.AreEqual(foot.y, cut.Verts[rows].y, 1e-4f, "a cut column's foot sits at the clifftop in front.");
            Assert.IsNotNull(cut.Colors);
            foreach (Color c in cut.Colors) Assert.AreEqual(1f, c.a, 1e-6f, "a cut is a hard edge, never a fade.");

            CliffWallMeshData toe = CliffWallSurface.DecalMesh(cols, false, 0.6f, 0.1f, 0.5f, 0f, 12f, (u, t) => 0f, 1f,
                                                               Vector3.zero);
            Assert.IsNotNull(toe.Colors);
            Assert.AreEqual(0f, toe.Colors[0].a, 1e-6f, "a cut column has no foot on the ground, so no toe strip.");
        }

        [Test]
        public void ATaperedFootStaysSolidUntilTheFaceIsShort()
        {
            Assert.AreEqual(1f, CliffWallGeometry.RunEndAlphaAt(0.6f, 0.6f, 8f), 1e-6f,
                            "a face that keeps more than half its height does not fade: faded rock shows the faces behind it.");
            Assert.AreEqual(0f, CliffWallGeometry.RunEndAlphaAt(CliffWallGeometry.RunEndKeep, CliffWallGeometry.RunEndKeep, 8f),
                            1e-6f, "at the open end the foot fades out.");
            float keep = 0.3f, surface = 8f;
            float band = CliffWallGeometry.RunEndFadeShareOfKept * keep * surface;
            Assert.AreEqual(1f, CliffWallGeometry.RunEndAlphaAt(keep - band / surface - 0.01f, keep, surface), 1e-6f,
                            "above the fade band the rock is solid.");
            Assert.AreEqual(0f, CliffWallGeometry.ToeStripShownAt(CliffWallGeometry.ToeStripGoneKeep), 1e-6f);
            Assert.AreEqual(1f, CliffWallGeometry.ToeStripShownAt(1f), 1e-6f);
        }

        [Test]
        public void StPeters_TheWestLedgesBayIsCutAndTheStepsDrawNothing()
        {
            Measure(StPetersCliffWalls.LoadDefs(), StPetersTerrainPlan.LoadPlan().WallToeSpanMetres,
                    out int hidden, out int cut, out int steps, out List<Vector2> where);
            Assert.AreEqual(16, steps, "St Peters step chunks: six at the wall 168 beach, ten on the bay's two stairs.");
            Assert.Greater(hidden + cut, 0, "the bay's back-facing wall and the walls drawn over the spit must be cut.");
            // The hook at wall 171 and the jog at wall 063 turn a single column away; a lone cut column would open a
            // slot down the face, so those stay whole.
            List<Vector2> elsewhere = where.Where(p => !InBay(p) && !InStairs(p)).ToList();
            Assert.IsEmpty(elsewhere, "faces cut outside the West Ledges bay and the steps: " + string.Join(", ", elsewhere));
        }

        [Test]
        public void ALoneCutColumnStaysWhole()
        {
            // A one-column hook in a wall facing the camera: the tip turns away, but cutting it alone opens a slot.
            Vector2[] brow = { new(0f, 0f), new(1f, 0f), new(1.2f, 0.6f), new(1.4f, 0f), new(2.4f, 0f) };
            CliffWallSample[] hook = Wall(brow, -0.5f);
            hook[2] = new CliffWallSample(brow[2], brow[2] + new Vector2(0f, 0.5f), Drop, 0f);
            Assert.IsNull(Clip(hook, false), "a lone cut column must be left whole.");
        }

        // The bay, where a wall's brow runs back into the land, and the walls drawn on across the spit.
        static bool InBay(Vector2 p) => p.x > -29f && p.x < -24f && p.y > -44f && p.y < -38f;
        static bool InStairs(Vector2 p) => p.x > -1f && p.x < 1f && p.y > -58f && p.y < -56f;

        [Test]
        public void NineMileCreek_NoFaceIsCut()
        {
            Measure(NineMileCreekCliffWalls.LoadDefs(), NineMileCreekCliffWalls.ChunkToeSpanMetres,
                    out int hidden, out int cut, out int steps, out List<Vector2> where);
            Assert.AreEqual(0, hidden + cut + steps, "Nine Mile Creek faces cut at: " + string.Join(", ", where));
        }

        /// <summary>Every chunk's cut, run by run the way the scene's chunks read it. A step's columns count as steps,
        /// not cuts. <paramref name="where"/> holds the brow point of every cut column.</summary>
        static void Measure(List<CliffWallDef> defs, float toeSpanMetres, out int hidden, out int cut, out int steps,
                            out List<Vector2> where)
        {
            List<StPetersCliffWalls.Chunk> chunks = StPetersCliffWalls.ChunksOfDefs(defs, toeSpanMetres, out _);
            Assert.Greater(chunks.Count, 1, "no wall chunks to measure.");
            var run = new List<CliffWallSample[]>();
            var known = new List<bool>();
            var brows = new List<Vector2[]>();
            var drops = new List<float[]>();
            foreach (StPetersCliffWalls.Chunk ch in chunks)
            {
                StPetersCliffWalls.ChunkFields f = StPetersCliffWalls.FieldsOf(in ch);
                CliffWallSample[] s = CliffWallSurface.BuildSamples(f.BrowPlan, f.ToePlan, f.DropMetres, f.ToeElevations);
                run.Add(s);
                known.Add(f.ToeElevations != null && f.BrowPlan != null && f.ToeElevations.Length >= f.BrowPlan.Length);
                brows.Add(f.BrowPlan);
                drops.Add(f.DropMetres);
            }
            hidden = cut = steps = 0;
            where = new List<Vector2>();
            for (int k = 0; k < run.Count; k++)
            {
                if (run[k].Length < 2) continue;
                CliffWallGeometry.RunReach reach = CliffWallGeometry.RunReachOf(brows, drops, k);
                CliffWallColumns cols = CliffWallSurface.Unsliced(run[k]);
                float[] clip = CliffWallSurface.RunClipFor(cols, run, known, k, reach, out _);
                if (clip == null) continue;
                bool step = CliffWallGeometry.IsStep(run[k]);
                for (int c = 0; c < clip.Length; c++)
                {
                    if (clip[c] >= 1f) continue;
                    where.Add(cols.Whole[c].BrowPlan);
                    if (step) steps += c == 0 ? 1 : 0;
                    else if (clip[c] <= 0f) hidden++;
                    else cut++;
                }
            }
        }
    }
}
