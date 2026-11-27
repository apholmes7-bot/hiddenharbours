using System.Collections.Generic;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>The turf lip on a bank with no cliff wall is laid in upright columns of whole ground texels, so it
    /// cannot shear or scale a texel wherever the brow runs. CI has no graphics device; these pin the arithmetic.</summary>
    public sealed class ShoreBrowLipMathTests
    {
        const float T = ShoreBrowLipMath.TexelsPerMetre;
        static readonly ShoreBrowLip Lip = new ShoreBrowLip(4f, 0.5f, 2f, 0.15f, 0.4f, 0.75f, 0.75f);
        static readonly CliffJag Jag = new CliffJag(0.8f, 0.6f, new Vector3(2.5f, 0.75f, 0.25f),
                                                    new Vector3(0.55f, 0.4f, 0.25f), 1f, 6, Vector2.zero, Vector4.zero);

        static ShoreLineDerivation.Segment Brow(Vector2 a, Vector2 b, Vector2 seaward) =>
            new ShoreLineDerivation.Segment(a, b, seaward, 0f, 1f);

        // Turf above a sloping edge, bare below it.
        static float Edge(float x) => 3f + 0.2f * x;
        static float Turf(Vector2 p) => p.y > Edge(p.x) ? 1f : 0f;

        [Test]
        public void TheEdge_IsWhereTheTurfEndsGoingDownTheScreen()
        {
            Assert.IsTrue(ShoreBrowLipMath.EdgeBelow(Turf, 5f, 5f, 4f, out float edge));
            Assert.AreEqual(Edge(5f), edge, 0.01f);
            Assert.IsFalse(ShoreBrowLipMath.EdgeBelow(p => 1f, 5f, 5f, 4f, out _), "all turf has no edge");
            Assert.IsFalse(ShoreBrowLipMath.EdgeBelow(Turf, 5f, 20f, 4f, out _), "an edge out of reach is not found");
            // Bare above and turf below is the far side of a patch, not a brow.
            Assert.IsFalse(ShoreBrowLipMath.EdgeBelow(p => p.y < 5f ? 1f : 0f, 5f, 5f, 4f, out _));
        }

        [Test]
        public void ABankFacingDownTheScreen_HangsOneColumnATexel_OnWholeRows_InsideTheTurf()
        {
            var brows = new[] { Brow(new Vector2(0f, 4f), new Vector2(10f, 6f), new Vector2(0.2f, -0.98f)) };
            List<ShoreBrowLipMath.Column[]> runs = ShoreBrowLipMath.Runs(brows, Turf, Lip, Jag);
            Assert.AreEqual(1, runs.Count);
            ShoreBrowLipMath.Column[] run = runs[0];
            Assert.Greater(run.Length, 8f * T, "most of ten metres of bank keeps its columns");
            var rows = new HashSet<int>();
            for (int i = 0; i < run.Length; i++)
            {
                if (i > 0) Assert.Greater(run[i].X, run[i - 1].X, "columns run left to right, one a texel at most");
                float edge = Edge((run[i].X + 0.5f) / T);
                float line = run[i].Row / T;
                Assert.GreaterOrEqual(line, edge + Lip.InsetMetres - 1.5f / T, "the line is never outside the turf");
                Assert.LessOrEqual(line, edge + Lip.InsetMetres + Lip.JagMetres + 1.5f / T, "the jag bites no further than its reach");
                rows.Add(run[i].Row - Mathf.RoundToInt((edge + Lip.InsetMetres) * T));
            }
            Assert.Greater(rows.Count, 3, "the line is jagged, not the edge redrawn");
            // The same bank is the same lip: no randomness.
            ShoreBrowLipMath.Column[] again = ShoreBrowLipMath.Runs(brows, Turf, Lip, Jag)[0];
            Assert.AreEqual(run.Length, again.Length);
            for (int i = 0; i < run.Length; i++) { Assert.AreEqual(run[i].X, again[i].X); Assert.AreEqual(run[i].Row, again[i].Row); }
        }

        [Test]
        public void ABankFacingAcrossTheScreenOrAway_OrAShortScrap_HangsNothing()
        {
            Assert.AreEqual(0, ShoreBrowLipMath.Runs(new[] { Brow(new Vector2(0f, 4f), new Vector2(10f, 6f), new Vector2(0.95f, -0.3f)) }, Turf, Lip, Jag).Count,
                "a bank facing across the screen cannot be crossed by upright columns");
            Assert.AreEqual(0, ShoreBrowLipMath.Runs(new[] { Brow(new Vector2(0f, 4f), new Vector2(10f, 6f), new Vector2(0f, 1f)) }, Turf, Lip, Jag).Count,
                "a bank facing away shows no lip");
            Assert.AreEqual(0, ShoreBrowLipMath.Runs(new[] { Brow(new Vector2(0f, 3f), new Vector2(1f, 3.2f), new Vector2(0f, -1f)) }, Turf, Lip, Jag).Count,
                "a scrap shorter than the minimum run is dropped");
            Assert.AreEqual(0, ShoreBrowLipMath.Runs(new[] { Brow(new Vector2(0f, 4f), new Vector2(10f, 6f), new Vector2(0f, -1f)) }, Turf, default, Jag).Count,
                "a region with no reach hangs no lip");
        }

        [Test]
        public void StPeters_HangsTheLipOnItsBanks_AndDrawsNoSoftTurfStrip()
        {
            CliffFaceMixDef mix = CliffFaceMixDef.ForScene("StPeters");
            Assert.IsNotNull(mix);
            Assert.IsTrue(ShoreBrowLip.From(mix).On, "the region's banks hang a lip");
            Assert.IsNotNull(mix.ShoreBrowMaterial, "the lip needs the cliff face's material");
            Assert.IsNotNull(mix.TurfLip);
            var lines = Resources.Load<TerrainShoreLinesDef>("TerrainShoreLines/StPetersShoreLines");
            Assert.IsNotNull(lines);
            Assert.AreEqual(TerrainShoreLinesDef.Step.Off, lines.Turf,
                "the soft strip is drawn at its own scale; the lip replaces it here");
        }

        [Test]
        public void TheMesh_IsQuadsOnTheTexelGrid_EachTheStripTall_AStripTexelAGroundTexel()
        {
            var brows = new[] { Brow(new Vector2(0f, 4f), new Vector2(10f, 6f), new Vector2(0.2f, -0.98f)) };
            List<ShoreBrowLipMath.Column[]> runs = ShoreBrowLipMath.Runs(brows, Turf, Lip, Jag);
            var strip = new Vector2Int(2048, 96);
            const int lineRow = 10;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            ShoreBrowLipMath.Mesh(runs, strip, lineRow, Vector2.zero, verts, uvs, tris);
            Assert.Greater(verts.Count, 0);
            Assert.AreEqual(0, verts.Count % 4);
            Assert.AreEqual(verts.Count / 4 * 6, tris.Count);
            int columns = 0;
            for (int q = 0; q < verts.Count; q += 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    Assert.AreEqual(Mathf.Round(verts[q + i].x * T), verts[q + i].x * T, 1e-3f, "a corner off the texel grid in x");
                    Assert.AreEqual(Mathf.Round(verts[q + i].y * T), verts[q + i].y * T, 1e-3f, "a corner off the texel grid in y");
                }
                float wide = (verts[q + 1].x - verts[q].x) * T, tall = (verts[q].y - verts[q + 3].y) * T;
                Assert.AreEqual(strip.y, tall, 1e-3f, "a quad is the strip's height, never squeezed");
                Assert.AreEqual(wide / strip.x, uvs[q + 1].x - uvs[q].x, 1e-5f, "a strip texel is a ground texel across");
                Assert.AreEqual(1f, uvs[q].y); Assert.AreEqual(0f, uvs[q + 3].y);
                columns += Mathf.RoundToInt(wide);
            }
            Assert.AreEqual(runs[0].Length, columns, "every column is drawn once");
            // The line row of the strip lies on the column's row.
            Assert.AreEqual((runs[0][0].Row + lineRow) / T, verts[0].y, 1e-4f);
        }
    }
}
