using System;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    public class TerrainPlanStillPaddingTests
    {
        static TerrainPlanGrid Grid(int w, int h) => new TerrainPlanGrid(w, h, .5, 0, 0);
        static double[] Empty(int n) => Enumerable.Repeat(double.NaN, n).ToArray();
        static double[] Pad(int w, int h, double[] ground, double[] water) =>
            TerrainPlanDerivation.PadStillForPublication(Grid(w, h), ground, water, -4, 12);

        [Test]
        public void EightNeighbours_FillOnlyTheOriginalOneRing()
        {
            var ground = Enumerable.Repeat(10d, 25).ToArray();
            var water = Empty(25); water[12] = 5.25;
            var before = (double[])water.Clone();
            var result = Pad(5, 5, ground, water);
            CollectionAssert.AreEqual(before, water, "publication must not mutate physical water");
            Assert.AreEqual(9, result.Count(v => !double.IsNaN(v)));
            foreach (int i in new[] { 6, 8, 16, 18 }) Assert.AreEqual(5.25, result[i], "bilinear diagonal missing");
            foreach (int i in new[] { 2, 10, 14, 22 }) Assert.IsTrue(double.IsNaN(result[i]), "support cascaded");
        }

        [Test]
        public void OriginalWetTexels_AreNeverOverwritten()
        {
            var water = new[] { 3d, 5d, double.NaN };
            CollectionAssert.AreEqual(new[] { 3d, 5d, 5d }, Pad(3, 1, new[] { 2d, 4d, 10d }, water));
        }

        [Test]
        public void DifferentLevels_UseTheHighestDryCandidate()
        {
            Assert.AreEqual(5d, Pad(3, 1, new[] { 2d, 10d, 4d }, new[] { 3d, double.NaN, 5d })[1]);
        }

        [Test]
        public void HigherWetCandidate_FallsBackToADryLowerLevel()
        {
            Assert.AreEqual(3d, Pad(3, 1, new[] { 2d, 4d, 4d }, new[] { 3d, double.NaN, 5d })[1]);
        }

        [Test]
        public void NoDryCandidate_StaysEmpty()
        {
            Assert.IsTrue(double.IsNaN(Pad(3, 1, new[] { 2d, 2d, 4d }, new[] { 3d, double.NaN, 5d })[1]),
                "dry padding must not add the separately authored wet head-of-tide extension");
        }

        [Test]
        public void ReversingNeighbourOrder_IsIdentical()
        {
            var ground = Enumerable.Repeat(10d, 25).ToArray();
            var water = Empty(25); water[6] = 3; water[8] = 5; water[16] = 4; water[18] = 5;
            ground[12] = 4.5;
            var forward = Pad(5, 5, ground, water);
            // Rotating the entire grid reverses donor visitation order at every corresponding receiver.
            var reverse = Pad(5, 5, ground.Reverse().ToArray(), water.Reverse().ToArray()).Reverse().ToArray();
            CollectionAssert.AreEqual(forward, reverse);
        }

        [Test]
        public void MapEdges_ClipTheRing()
        {
            CollectionAssert.AreEqual(new[] { 5d, 5d, 5d, 5d },
                Pad(2, 2, new[] { 4d, 10d, 10d, 10d }, new[] { 5d, double.NaN, double.NaN, double.NaN }));
            CollectionAssert.AreEqual(new[] { 5d }, Pad(1, 1, new[] { 4d }, new[] { 5d }));
        }

        [Test]
        public void EncodedSupport_DoesNotStandAboveGround()
        {
            const double level = 5.25010;
            var result = Pad(2, 1, new[] { 4d, level }, new[] { level, double.NaN });
            Assert.IsTrue(double.IsNaN(result[1]), "rounded-up R16 level would wet this dry receiver");
            Assert.AreEqual(5.25, Pad(2, 1, new[] { 4d, 5.25 }, new[] { 5.25, double.NaN })[1],
                "ground at the level is eligible when encoding does not raise it");
        }

        [Test]
        public void PublishedPadding_PreservesThePhysicalWetMask_HeightAndSplats()
        {
            var grid = Grid(5, 5);
            var r = new TerrainPlanResult { Grid = grid, E = Enumerable.Repeat(10d, 25).ToArray(),
                Still = Empty(25), Zone = new byte[25], PathWeight = Enumerable.Range(0, 25).Select(i => i / 25d).ToArray() };
            r.E[12] = 4; r.Still[12] = 5.25;
            var physical = (double[])r.Still.Clone();
            var height = TerrainPlanMaps.Codes(TerrainPlanMaps.HeightR01(grid, r.E, -4, 12));
            var splats = TerrainPlanMaps.Splat(grid, r.Zone, r.PathWeight);
            r.Still = TerrainPlanDerivation.PadStillForPublication(grid, r.E, physical, -4, 12);
            Assert.AreEqual(9, r.Still.Count(v => !double.IsNaN(v)), "test must actually add support");
            for (int i = 0; i < grid.Count; i++)
                Assert.AreEqual(physical[i], r.Still[i] > r.E[i] ? r.Still[i] : double.NaN, "physical mask at " + i);
            CollectionAssert.AreEqual(height, TerrainPlanMaps.Codes(TerrainPlanMaps.HeightR01(grid, r.E, -4, 12)));
            var after = TerrainPlanMaps.Splat(grid, r.Zone, r.PathWeight);
            for (int i = 0; i < splats.Length; i++) CollectionAssert.AreEqual(splats[i], after[i], "splat " + i);
        }
    }
}
