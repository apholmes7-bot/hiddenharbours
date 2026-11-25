using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters' baked seabed colour must be the bake of St Peters' COMMITTED ground.
    ///
    /// <para><b>Why this exists.</b> The seabed colour is derived data: one palette ramp over the
    /// ground's elevation, one texel per cell centre (<see cref="SeabedBake.Build"/> through
    /// <see cref="SeabedPalette.Sample"/>). It was baked from the ground the island had in September and
    /// nothing tied it to the ground afterwards. When St Peters moved onto the Art desk's ground file the
    /// colour was not re-baked, and the sea kept drawing the OLD bottom under the new one: the shelf round
    /// the island, now a few metres deep, stayed painted as the black silt of the deep floor, and the
    /// pocket dredged north of the wharf head stayed painted as the weed line it used to be, a pale block
    /// in dark water.</para>
    ///
    /// <para>The ground here is the painted height map the sea reads its depth from
    /// (<c>StPetersSeabed.asset</c>), not the scene's analytic <see cref="TidalTerrain"/>: the colour must
    /// agree with the depth the water draws.</para>
    ///
    /// <para>No graphics device is needed: the ground is decoded on the CPU and the colour file is read
    /// from disk, so this runs where the rest of the suite runs.</para>
    /// </summary>
    public sealed class StPetersSeabedColourTests
    {
        private const string GroundAsset = "Assets/_Project/Data/Terrain/StPetersSeabed.asset";
        private const string SeabedPng = "Assets/_Project/Data/Terrain/StPetersSeabed_SeabedTex.png";
        private const string ScenePath = "Assets/_Project/Scenes/StPeters.unity";

        /// <summary>One 8-bit level of slack per channel for the float-to-byte rounding of the ramp. A
        /// stale bake is not a rounding matter: the one this test was written for was off by more than
        /// 16 levels on 8.9% of the map.</summary>
        private const int ToleranceLevels = 1;

        private const string HowToFix =
            "Re-bake St Peters' seabed colour from its painted ground: SeabedBakeTool.BakeFromTerrain with " +
            "the ground of StPetersSeabed.asset (its PaintedHeightMap.Field as the ITidalTerrain, not the " +
            "scene's analytic TidalTerrain), over the ground's own world rectangle at the committed " +
            "resolution, base name StPetersSeabed. Commit the PNG with the ground change that moved it.";

        [Test]
        public void TheCommittedSeabedColourIsTheBakeOfTheCommittedGround()
        {
            PaintedHeightField ground = Ground();
            Color32[] have = ReadSeabedColour(out int width, out int height);
            Color32[] want = Bake(ground, width, height);

            int stale = 0, worst = 0, worstIndex = -1;
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            for (int i = 0; i < have.Length; i++)
            {
                int off = Distance(have[i], want[i]);
                if (off <= ToleranceLevels) continue;
                stale++;
                int x = i % width, y = i / width;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                if (off > worst) { worst = off; worstIndex = i; }
            }

            if (stale == 0) return;
            Vector2 at = World(worstIndex % width, worstIndex / width, width, height, ground);
            Vector2 low = World(minX, minY, width, height, ground);
            Vector2 high = World(maxX, maxY, width, height, ground);
            Assert.Fail(
                $"{stale} of {have.Length} seabed colour texels ({100.0 * stale / have.Length:0.00}%) are not the " +
                $"bake of the committed ground, across world x {low.x:0.0}..{high.x:0.0}, y {low.y:0.0}..{high.y:0.0}. " +
                $"Worst at ({at.x:0.00}, {at.y:0.00}): the ground stands at {ground.ElevationAt(at):0.00} m, the file " +
                $"holds {have[worstIndex]}, the bake asks for {want[worstIndex]}. The sea draws this file as its " +
                "bottom, so every stale texel is old ground showing through the water.\n" + HowToFix);
        }

        /// <summary>
        /// The pale block in plain numbers: the pocket dredged north of the wharf head is deep water, and
        /// its bottom must be no paler than mid water, not the weed line it was before the dredge.
        /// </summary>
        [Test]
        public void ThePocketNorthOfTheWharfHeadIsNotPaintedPalerThanItsDepth()
        {
            PaintedHeightField ground = Ground();
            Color32[] have = ReadSeabedColour(out int width, out int height);
            const float midWater = -2f;
            Color32 midWaterColour = SeabedPalette.Sample(midWater);

            // Where the stale block showed on screen.
            var pocket = new Rect(208f, 6f, 10f, 4f);
            int deep = 0, pale = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 world = World(x, y, width, height, ground);
                if (!pocket.Contains(world) || ground.ElevationAt(world) > midWater) continue;
                deep++;
                if (Luma(have[y * width + x]) > Luma(midWaterColour) + ToleranceLevels) pale++;
            }

            Assert.Greater(deep, 0, "the pocket must be deeper than mid water for this to measure anything; if " +
                                    "it was filled in, retire this test with that change.");
            Assert.AreEqual(0, pale,
                $"{pale} of {deep} texels inside x {pocket.xMin}..{pocket.xMax}, y {pocket.yMin}..{pocket.yMax} lie " +
                $"below {midWater} m but are painted paler than mid water: the pale block north of the wharf head.\n" +
                HowToFix);
        }

        /// <summary>
        /// The shelf round the island in plain numbers: ground a few metres under water must not be painted
        /// as the black silt of the deep floor.
        /// </summary>
        [Test]
        public void TheShelfRoundTheIslandIsNotPaintedAsTheDeepFloor()
        {
            PaintedHeightField ground = Ground();
            Color32[] have = ReadSeabedColour(out int width, out int height);
            Color32 deepFloor = SeabedPalette.Sample(SeabedPalette.MinElevation);
            const float shelfFloor = -3f;   // a metre above the deep floor: its colour is 15 levels lighter

            int shelf = 0, black = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float e = ground.ElevationAt(World(x, y, width, height, ground));
                if (e < shelfFloor || e >= 0f) continue;
                shelf++;
                if (Distance(have[y * width + x], deepFloor) <= ToleranceLevels) black++;
            }

            Assert.Greater(shelf, 0, "St Peters must have ground between the deep floor and the mean waterline.");
            Assert.AreEqual(0, black,
                $"{black} of {shelf} texels over ground between {shelfFloor} m and the mean waterline are painted " +
                "as the deep floor: the shelf round the island drawn as black water.\n" + HowToFix);
        }

        /// <summary>
        /// The two files compared above must be the two the scene draws with: the sea's baked bottom, and
        /// the ground the sea reads its depth from, over one world rectangle.
        /// </summary>
        [Test]
        public void TheSceneDrawsThisColourOverThisGround()
        {
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(GroundAsset);
            Assert.IsNotNull(map, GroundAsset);
            string seabedGuid = AssetDatabase.AssetPathToGUID(SeabedPng);
            string heightGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(map.HeightTexture));

            string seaBottom = null, seaHeight = null, centre = null, size = null;
            foreach (string line in File.ReadLines(ScenePath))
            {
                if (line.StartsWith("  _seabedTexture:", StringComparison.Ordinal)) seaBottom = GuidOf(line);
                else if (line.StartsWith("  _paintedHeightTex:", StringComparison.Ordinal)) seaHeight = GuidOf(line);
                else if (line.StartsWith("  _heightWorldCenter:", StringComparison.Ordinal)) centre = line;
                else if (line.StartsWith("  _heightWorldSize:", StringComparison.Ordinal)) size = line;
            }

            Assert.AreEqual(seabedGuid, seaBottom, "St Peters' sea must draw the seabed colour this fixture checks.");
            Assert.AreEqual(heightGuid, seaHeight, "St Peters' sea must read the ground this fixture bakes from.");
            AssertVector(map.WorldCenter, centre, "the sea's world rectangle centre");
            AssertVector(map.WorldSize, size, "the sea's world rectangle size");
        }

        private static Color32[] Bake(PaintedHeightField ground, int width, int height) =>
            SeabedBake.Build(width, height, ground.WorldMin, ground.WorldSize,
                p => SeabedPalette.Sample(ground.ElevationAt(p)), new Color(0f, 0f, 0f, 0f));

        private static Vector2 World(int x, int y, int width, int height, PaintedHeightField ground) =>
            SeabedBake.TexelCenterWorld(x, y, width, height, ground.WorldMin, ground.WorldSize);

        private static PaintedHeightField Ground()
        {
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(GroundAsset);
            Assert.IsNotNull(map, GroundAsset);
            PaintedHeightField ground = map.Field;
            Assert.IsNotNull(ground, "St Peters' ground height texture must be CPU-readable.");
            return ground;
        }

        private static Color32[] ReadSeabedColour(out int width, out int height)
        {
            var disk = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(disk.LoadImage(File.ReadAllBytes(SeabedPng)), $"not a decodable PNG: {SeabedPng}");
                width = disk.width;
                height = disk.height;
                return disk.GetPixels32();
            }
            finally { Object.DestroyImmediate(disk); }
        }

        private static int Distance(Color32 a, Color32 b) =>
            Mathf.Max(Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g)),
                      Mathf.Max(Mathf.Abs(a.b - b.b), Mathf.Abs(a.a - b.a)));

        private static int Luma(Color32 c) => (c.r + c.g + c.b) / 3;

        private static string GuidOf(string line)
        {
            Match match = Regex.Match(line, "guid: ([0-9a-f]{32})");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static void AssertVector(Vector2 expected, string line, string what)
        {
            Assert.IsNotNull(line, what + " is not serialized in the St Peters scene.");
            Match match = Regex.Match(line, @"\{x: ([-0-9.e]+), y: ([-0-9.e]+)\}");
            Assert.IsTrue(match.Success, what + ": " + line);
            Assert.AreEqual(expected.x, float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), 1e-4f, what + " x");
            Assert.AreEqual(expected.y, float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), 1e-4f, what + " y");
        }
    }
}
