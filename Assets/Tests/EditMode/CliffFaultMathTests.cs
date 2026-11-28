using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests
{
    /// <summary>
    /// The faulted bedding of a wall laid on the screen grid (owner 11 26: rough and jagged, and no mixels): blocks
    /// of uneven width and height, each moved by WHOLE texels, cracks only on block edges and stepping a texel at a
    /// time, the same answer every time, and off unless a region's mix asks for it.
    /// </summary>
    public class CliffFaultMathTests
    {
        static CliffFault Fault => new CliffFault(new Vector2(56f, 40f), 6, 96, 5, 0.7f, 30f);

        [Test]
        public void ADefaultFault_IsOff_AndSoIsAMixWithNoBlock()
        {
            Assert.IsFalse(default(CliffFault).On);
            Assert.IsFalse(CliffFault.From(null).On);
            Assert.AreEqual(0f, default(CliffFault).ShaderB(true).w, "off must reach the shader as off");
            Assert.AreEqual(0f, Fault.ShaderB(false).w, "a face that is not on the screen grid is not faulted");
            Assert.AreEqual(1f, Fault.ShaderB(true).w);
        }

        [Test]
        public void EveryBlockIsMovedByWholeTexels_WithinItsReach()
        {
            CliffFault f = Fault;
            bool thrown = false, slid = false;
            for (int y = -200; y < 200; y += 7)
                for (int x = -900; x < 900; x += 5)
                {
                    CliffFaultMath.At(in f, new Vector2(x, y), out _, out Vector2 move, out _);
                    Assert.AreEqual(Mathf.Round(move.x), move.x, $"({x},{y}): the slide is part of a texel");
                    Assert.AreEqual(Mathf.Round(move.y), move.y, $"({x},{y}): the throw is part of a texel");
                    Assert.LessOrEqual(Mathf.Abs(move.y), f.ThrowTexels);
                    Assert.That(move.x, Is.InRange(0f, (float)f.SlideTexels));
                    thrown |= move.y != 0f;
                    slid |= move.x != 0f;
                }
            Assert.IsTrue(thrown && slid, "no block moved: the bedding still runs the wall in level courses");
        }

        [Test]
        public void BlocksAreOfUnevenWidth_AndCoursesStepAlongARow()
        {
            CliffFault f = Fault;
            var widths = new HashSet<int>();
            var throws = new HashSet<float>();
            int run = 0, blocks = 0;
            Vector2 last = CliffFaultMath.BlockOf(in f, new Vector2(-2000f, 3f));
            for (int x = -1999; x < 2000; x++)
            {
                var cell = new Vector2(x, 3f);
                Vector2 b = CliffFaultMath.BlockOf(in f, cell);
                run++;
                if (b != last)
                {
                    if (blocks > 0) widths.Add(run);
                    blocks++;
                    run = 0;
                    last = b;
                }
                CliffFaultMath.At(in f, cell, out _, out Vector2 move, out _);
                throws.Add(move.y);
            }
            Assert.Greater(blocks, 40, "4000 texels of wall should hold about seventy blocks");
            Assert.Less(blocks, 120);
            Assert.Greater(widths.Count, 10, "the blocks are all one width: that is brickwork");
            Assert.Greater(throws.Count, 4, "the courses do not step up and down along the wall");
        }

        [Test]
        public void TiersAreOfUnevenHeight_AndAColumnsTiersDoNotLineUpWithItsNeighbours()
        {
            CliffFault f = Fault;
            var heights = new HashSet<int>();
            int run = 0, tiers = 0;
            Vector2 last = CliffFaultMath.BlockOf(in f, new Vector2(10f, -1500f));
            var edgesHere = new HashSet<int>();
            for (int y = -1499; y < 1500; y++)
            {
                Vector2 b = CliffFaultMath.BlockOf(in f, new Vector2(10f, y));
                run++;
                if (b.y != last.y)
                {
                    if (tiers > 0) heights.Add(run);
                    tiers++;
                    run = 0;
                    edgesHere.Add(y);
                }
                last = b;
            }
            Assert.Greater(heights.Count, 6, "the tiers are all one height");

            // Far enough along to be another master column: its tier edges are its own.
            int shared = 0, edgesThere = 0;
            last = CliffFaultMath.BlockOf(in f, new Vector2(700f, -1500f));
            for (int y = -1499; y < 1500; y++)
            {
                Vector2 b = CliffFaultMath.BlockOf(in f, new Vector2(700f, y));
                if (b.y != last.y) { edgesThere++; if (edgesHere.Contains(y)) shared++; }
                last = b;
            }
            Assert.Less(shared, edgesThere / 2, "two columns' tiers line up: the courses would run the whole wall");
        }

        [Test]
        public void ACrackIsOnlyOnABlockEdge_OneTexelWide_AndStepsATexelAtATime()
        {
            CliffFault f = Fault;
            int cracks = 0;
            for (int y = 0; y < 120; y++)
                for (int x = -600; x < 600; x++)
                {
                    var cell = new Vector2(x, y);
                    CliffFaultMath.At(in f, cell, out Vector2 block, out _, out bool crack);
                    if (!crack) continue;
                    cracks++;
                    Vector2 stepped = CliffFaultMath.Stepped(in f, cell);
                    Assert.AreNotEqual(CliffFaultMath.BlockOf(in f, stepped - Vector2.right).x, block.x,
                        $"({x},{y}): a crack inside a block");
                    Assert.LessOrEqual(Mathf.Abs(stepped.x - x), 1f, "a crack steps more than a texel");
                    Assert.AreEqual((float)y, stepped.y, "a crack is never moved up or down");
                }
            Assert.Greater(cracks, 100, "no cracks at the asset's share");

            var none = new CliffFault(f.BlockTexels, f.ThrowTexels, f.SlideTexels, f.CrackRunTexels, 0f, f.FacetDegrees);
            for (int x = -600; x < 600; x++)
            {
                CliffFaultMath.At(in none, new Vector2(x, 9f), out _, out _, out bool crack);
                Assert.IsFalse(crack, "a share of 0 draws no crack");
            }
        }

        /// <summary>A crack breaks off and picks up again down one block edge: it never rules a block's whole height,
        /// which reads as a joint between planks.</summary>
        [Test]
        public void ACrackBreaksAlongItsEdge_RunByRun()
        {
            CliffFault f = Fault;
            int brokenEdges = 0, edges = 0;
            for (int x = -600; x < 600; x++)
            {
                int on = 0, off = 0;
                Vector2 first = default;
                for (int y = 0; y < 400; y++)
                {
                    var cell = new Vector2(x, y);
                    Vector2 stepped = CliffFaultMath.Stepped(in f, cell);
                    Vector2 block = CliffFaultMath.BlockOf(in f, stepped);
                    if (CliffFaultMath.BlockOf(in f, stepped - Vector2.right).x == block.x) continue;   // not an edge
                    if (on + off == 0) first = block;
                    if (block != first) break;                                                           // one block's edge
                    CliffFaultMath.At(in f, cell, out _, out _, out bool crack);
                    if (crack) on++; else off++;
                }
                if (on + off < f.CrackRunTexels * 3) continue;
                edges++;
                if (on > 0 && off > 0) brokenEdges++;
            }
            Assert.Greater(edges, 10, "no block edge long enough to judge");
            Assert.Greater(brokenEdges, edges / 3, "cracks rule whole block edges: that is a plank joint");
        }

        [Test]
        public void TheSameCell_IsTheSameAnswer()
        {
            CliffFault f = Fault;
            for (int i = 0; i < 200; i++)
            {
                var cell = new Vector2(i * 37 - 3000, i * 11 - 900);
                CliffFaultMath.At(in f, cell, out Vector2 b1, out Vector2 m1, out bool c1);
                CliffFaultMath.At(in f, cell, out Vector2 b2, out Vector2 m2, out bool c2);
                Assert.AreEqual(b1, b2); Assert.AreEqual(m1, m2); Assert.AreEqual(c1, c2);
            }
        }

        [Test]
        public void ABlockIsShadedAtOneOfTheTwoNearestFacets_TheNearerMoreOften()
        {
            CliffFault f = Fault;
            int upper = 0, n = 0;
            for (int bx = 0; bx < 60; bx++)
                for (int by = 0; by < 20; by++)
                {
                    float az = CliffFaultMath.FacetAzimuth(in f, 97.5f, new Vector2(bx, by));
                    Assert.That(az, Is.EqualTo(90f).Or.EqualTo(120f), "a block's facing is not one of the two steps");
                    if (az == 120f) upper++;
                    n++;
                }
            // 97.5 is a quarter of the way from 90 to 120.
            Assert.That(upper / (float)n, Is.InRange(0.1f, 0.4f));

            // Two chunks a few degrees apart differ in few blocks, so no straight line of tone stands between them.
            int differ = 0;
            for (int bx = 0; bx < 60; bx++)
                for (int by = 0; by < 20; by++)
                    if (CliffFaultMath.FacetAzimuth(in f, 97.5f, new Vector2(bx, by)) !=
                        CliffFaultMath.FacetAzimuth(in f, 102f, new Vector2(bx, by))) differ++;
            Assert.Less(differ, n / 3);

            var off = new CliffFault(f.BlockTexels, f.ThrowTexels, f.SlideTexels, f.CrackRunTexels, f.CrackShare, 0f);
            Assert.AreEqual(97.5f, CliffFaultMath.FacetAzimuth(in off, 97.5f, Vector2.one), "0 leaves the chunk's own facing");
        }

        [Test]
        public void StPeters_FaultsItsBedding_InWholeTexels()
        {
            CliffFaceMixDef.ClearCache();
            CliffFaceMixDef mix = CliffFaceMixDef.ForScene("StPeters");
            Assert.IsNotNull(mix, "St Peters has a cliff mix");
            CliffFault f = CliffFault.From(mix);
            Assert.IsTrue(f.On, "St Peters' walls on the screen grid must be faulted, or they read as plank siding");
            Assert.Greater(f.ThrowTexels, 0);
            Assert.Greater(f.SlideTexels, 0);
            Assert.Greater(f.CrackShare, 0f);
            Assert.Greater(f.FacetDegrees, 0f);
        }

        [Test]
        public void TheVariantFace_TakesItsOwnBlocks_InWholeTexels_AndOneLeavesThemAlone()
        {
            CliffFault f = Fault;
            CliffFault same = f.OnVariantSkin();
            Assert.AreEqual(f.BlockTexels, same.BlockTexels, "a scale of 1 leaves the blocks");
            Assert.AreEqual(f.ThrowTexels, same.ThrowTexels, "a scale of 1 leaves the throw");

            var scaled = new CliffFault(new Vector2(18f, 14f), 9, 160, 4, 0.35f, 30f, 0.5f, 1.5f).OnVariantSkin();
            Assert.AreEqual(new Vector2(9f, 7f), scaled.BlockTexels);
            Assert.AreEqual(Mathf.RoundToInt(9 * 1.5f), scaled.ThrowTexels);
            Assert.IsTrue(scaled.On);

            var tiny = new CliffFault(new Vector2(18f, 14f), 9, 160, 4, 0.35f, 30f, 0.01f, 1f).OnVariantSkin();
            Assert.AreEqual(new Vector2(2f, 2f), tiny.BlockTexels, "never under two texels: a block has an inside");
        }
    }
}
