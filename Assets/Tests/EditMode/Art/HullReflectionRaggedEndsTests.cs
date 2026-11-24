using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The hull reflection's streak rows end ragged.</b>
    ///
    /// <para>Under a square stern every streak row of the reflection stopped on the same column: a
    /// straight vertical edge, 27 to 35 levels across one pixel, that travelled with the boat. Each
    /// row now stops short of the outline by its own 0..cut hull pixels and thins over the next few
    /// (<see cref="HullMeshReflection.EndThins"/>, twin of <c>HHMeshReflectEndThins</c>), keyed on the
    /// hull's own cells so it cannot crawl, and a short row (an oar, a mast) keeps its ends
    /// (<see cref="HullMeshReflection.EndDrops"/>, twin of <c>HHMeshReflectEndDrops</c>).</para>
    /// </summary>
    public class HullReflectionRaggedEndsTests
    {
        static Vector4 Shipped => HullMeshReflection.PackEnds(GameConfig.DefaultHullReflectionEndCutPixels,
            GameConfig.DefaultHullReflectionEndThinPixels, GameConfig.DefaultHullReflectionEndMinRunPixels);

        [Test]
        public void PackEnds_Off_WhenUnderAPixel_OrWhileTheKeylineFloods()
        {
            Assert.AreEqual(Vector4.zero, HullMeshReflection.PackEnds(0f, 0f, 12f));
            Assert.AreEqual(Vector4.zero, HullMeshReflection.PackEnds(0.9f, 0.5f, 12f), "whole pixels only");
            Assert.AreEqual(Vector4.zero, HullMeshReflection.PackEnds(3f, 3f, 12f, keylineFlood: true),
                "the flood reads the key colour the ends ride on");
        }

        [Test]
        public void PackEnds_StoresWholePixels_AndTheOnFlag()
        {
            Vector4 e = HullMeshReflection.PackEnds(3.7f, 2.2f, 12.5f);
            Assert.AreEqual(new Vector4(3f, 2f, 12f, 1f), e);
            Assert.AreEqual(new Vector4(0f, 2f, 0f, 1f), HullMeshReflection.PackEnds(-1f, 2f, -4f));
        }

        [Test]
        public void EndHash01_IsAStableShare_SpreadEvenly()
        {
            var buckets = new int[8];
            for (uint k = 0; k < 4096; k++)
            {
                float v = HullMeshReflection.EndHash01(k);
                Assert.That(v, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                Assert.AreEqual(v, HullMeshReflection.EndHash01(k), 0f, "the same key, the same share");
                buckets[(int)(v * 8)]++;
            }
            foreach (int b in buckets) Assert.AreEqual(512, b, 96, "an even spread");
        }

        [Test]
        public void Off_NeverDrops()
        {
            for (int i = 0; i < 40; i++)
                Assert.IsFalse(HullMeshReflection.EndDrops(i, 40, 0, 7, Vector4.zero));
        }

        // A block of reflection rows that all stop on the same column: the square stern.
        static List<int> LastKeptPerRow(Vector4 ends, int rows, int runLength, int firstCellX, int firstRow)
        {
            var last = new List<int>();
            for (int r = 0; r < rows; r++)
            {
                int lastKept = -1;
                for (int i = 0; i < runLength; i++)
                    if (!HullMeshReflection.EndDrops(i, runLength, firstCellX, firstRow + r, ends)) lastKept = i;
                last.Add(lastKept);
            }
            return last;
        }

        [Test]
        public void ASquareEnd_NoLongerEndsOnOneColumn()
        {
            Vector4 e = Shipped;
            const int Rows = 24, Run = 60;
            List<int> last = LastKeptPerRow(e, Rows, Run, 17, -9);
            var distinct = new HashSet<int>(last);
            Assert.GreaterOrEqual(distinct.Count, 3, "the rows' ends are spread over several columns");
            int longestSame = 1, same = 1;
            for (int r = 1; r < Rows; r++)
            {
                same = last[r] == last[r - 1] ? same + 1 : 1;
                longestSame = Mathf.Max(longestSame, same);
            }
            Assert.LessOrEqual(longestSame, 4, "no straight vertical edge more than a few rows tall");
            foreach (int l in last)
                Assert.GreaterOrEqual(l, Run - 1 - (int)(e.x + e.y), "a row never stops short by more than cut + thin");
        }

        [Test]
        public void TheLeftEnd_IsRaggedToo()
        {
            Vector4 e = Shipped;
            var firsts = new HashSet<int>();
            for (int r = 0; r < 24; r++)
            {
                int first = -1;
                for (int i = 0; i < 60 && first < 0; i++)
                    if (!HullMeshReflection.EndDrops(i, 60, -40, r, e)) first = i;
                firsts.Add(first);
            }
            Assert.GreaterOrEqual(firsts.Count, 3);
        }

        [Test]
        public void OnlyTheEndsChange_TheRowBetweenKeepsItsStrength()
        {
            Vector4 e = Shipped;
            int band = (int)(e.x + e.y);
            for (int row = -30; row < 30; row++)
                for (int i = band; i < 80 - band; i++)
                    Assert.IsFalse(HullMeshReflection.EndDrops(i, 80, 3, row, e), $"row {row} cell {i}");
        }

        [Test]
        public void TheEndThins_MoreKeptFurtherIn()
        {
            Vector4 e = HullMeshReflection.PackEnds(0f, 4f, 0f);
            float prev = -1f;
            for (int d = 0; d < 4; d++)
            {
                int kept = 0, n = 0;
                for (int row = 0; row < 256; row++)
                    for (int x = 0; x < 16; x++, n++)
                        if (!HullMeshReflection.EndThins(d, row, x, 0, e)) kept++;
                float share = kept / (float)n;
                Assert.AreEqual((d + 1) / 5f, share, 0.06f, $"{d} in from the end");
                Assert.Greater(share, prev);
                prev = share;
            }
            for (int row = 0; row < 64; row++)
                Assert.IsFalse(HullMeshReflection.EndThins(4, row, 0, 0, e), "past the band: kept");
        }

        [Test]
        public void ARowsCut_IsItsOwn_FromNoneToTheMost()
        {
            Vector4 e = HullMeshReflection.PackEnds(3f, 0f, 0f);
            var cuts = new int[4];
            for (int row = 0; row < 256; row++)
            {
                int cut = 0;
                while (cut < 4 && HullMeshReflection.EndThins(cut, row, 0, 0, e)) cut++;
                Assert.LessOrEqual(cut, 3);
                cuts[cut]++;
            }
            foreach (int c in cuts) Assert.AreEqual(64, c, 24, "each cut 0..3 is about as common");
        }

        [Test]
        public void AShortRow_AnOarOrAMast_KeepsItsEnds()
        {
            Vector4 e = Shipped;
            int run = (int)e.z - 1;
            for (int row = 0; row < 64; row++)
                for (int i = 0; i < run; i++)
                    Assert.IsFalse(HullMeshReflection.EndDrops(i, run, 5, row, e), $"row {row} cell {i}");
        }

        [Test]
        public void AScreenEdge_IsNoEnd()
        {
            Vector4 e = Shipped;
            for (int row = 0; row < 64; row++)
            {
                Assert.IsFalse(HullMeshReflection.EndDrops(0, 40, 0, row, e, leftIsEnd: false));
                Assert.IsFalse(HullMeshReflection.EndDrops(39, 40, 0, row, e, rightIsEnd: false));
            }
        }

        [Test]
        public void TheEnds_AreFixedToTheHull_NotTheScreen()
        {
            // The decision reads only the hull's own cells, by their low byte: the same cells give the
            // same ragged end wherever the boat is on screen and on every frame.
            Vector4 e = Shipped;
            List<int> a = LastKeptPerRow(e, 32, 50, 12, 40);
            List<int> b = LastKeptPerRow(e, 32, 50, 12, 40);
            List<int> wrapped = LastKeptPerRow(e, 32, 50, 12 + 256, 40 - 256);
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreEqual(a, wrapped);
        }

        [Test]
        public void TheShaderTwin_UsesTheSameHashAndKeys()
        {
            string hlsl = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Project/Art/Shaders/Include/HullReflection.hlsl"));
            StringAssert.Contains("0x7feb352du", hlsl);
            StringAssert.Contains("0x846ca68bu", hlsl);
            StringAssert.Contains("row | (side << 8) | (1u << 10)", hlsl);
            StringAssert.Contains("cellX | (row << 8) | (side << 16) | (1u << 17)", hlsl);
        }

        [Test]
        public void TheShippedConfig_FraysTheEnds()
        {
            Vector4 e = Shipped;
            Assert.AreEqual(1f, e.w);
            Assert.GreaterOrEqual(e.x, 1f);
            Assert.GreaterOrEqual(e.z, e.x + e.y, "a row must be longer than both ends' bands to fray");
        }
    }
}
