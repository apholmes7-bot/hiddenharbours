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
    /// Nine Mile Creek's baked seabed colour must be the bake of Nine Mile Creek's COMMITTED ground.
    ///
    /// <para><b>Why this exists.</b> The seabed colour is derived data: one palette ramp over the
    /// ground's elevation, one texel per cell centre (<see cref="SeabedBake.Build"/> through
    /// <see cref="SeabedPalette.Sample"/>). It was baked once, from the ground the creek had then, and
    /// nothing tied it to the ground afterwards. The ground was later replaced and the wharf moved; the
    /// colour was not re-baked. The water kept drawing the OLD bottom under the new sea: the old west
    /// wall's deck, 11 m wide, stayed painted as dry sand over a basin that is now four metres deep, and
    /// showed through the water as a hard-edged pale green rectangle off the wharf.</para>
    ///
    /// <para>No graphics device is needed: the ground is decoded on the CPU and the colour file is read
    /// from disk, so this runs where the rest of the suite runs.</para>
    /// </summary>
    public sealed class NineMileCreekSeabedColourTests
    {
        private const string GroundAsset =
            "Assets/_Project/Data/Terrain/NineMileCreekGround/NineMileCreekGround.asset";
        private const string SeabedPng = "Assets/_Project/Data/Terrain/NineMileCreekSeabed_SeabedTex.png";
        private const string ScenePath = "Assets/_Project/Scenes/NineMileCreek.unity";

        /// <summary>One 8-bit level of slack per channel for the float-to-byte rounding of the ramp. A
        /// stale bake is not a rounding matter: the one this test was written for was off by more than
        /// 32 levels on 15% of the map.</summary>
        private const int ToleranceLevels = 1;

        private const string HowToFix =
            "Re-bake the creek's seabed colour from its ground: open the creek scene, then " +
            "Hidden Harbours/Dev/Bake Seabed Texture (_SeabedTex), source 'Terrain elevation', over the " +
            "ground's own world rectangle at the committed resolution, base name NineMileCreekSeabed " +
            "(SeabedBakeTool.BakeFromTerrain). Commit the PNG with the ground change that moved it.";

        [Test]
        public void TheCommittedSeabedColourIsTheBakeOfTheCommittedGround()
        {
            PaintedHeightField ground = Ground();
            Color32[] have = ReadSeabedColour(out int width, out int height);
            Color32[] want = SeabedBake.Build(width, height, ground.WorldMin, ground.WorldSize,
                p => SeabedPalette.Sample(ground.ElevationAt(p)), new Color(0f, 0f, 0f, 0f));

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
            Vector2 at = SeabedBake.TexelCenterWorld(worstIndex % width, worstIndex / width, width, height,
                ground.WorldMin, ground.WorldSize);
            Vector2 low = SeabedBake.TexelCenterWorld(minX, minY, width, height, ground.WorldMin, ground.WorldSize);
            Vector2 high = SeabedBake.TexelCenterWorld(maxX, maxY, width, height, ground.WorldMin, ground.WorldSize);
            Assert.Fail(
                $"{stale} of {have.Length} seabed colour texels ({100.0 * stale / have.Length:0.00}%) are not the " +
                $"bake of the committed ground, across world x {low.x:0.0}..{high.x:0.0}, y {low.y:0.0}..{high.y:0.0}. " +
                $"Worst at ({at.x:0.00}, {at.y:0.00}): the ground stands at {ground.ElevationAt(at):0.00} m, the file " +
                $"holds {have[worstIndex]}, the bake asks for {want[worstIndex]}. The sea draws this file as its " +
                "bottom, so every stale texel is old ground showing through the water.\n" + HowToFix);
        }

        /// <summary>
        /// The rectangle itself, in plain numbers: off the wharf the old west wall's footprint is now the
        /// berth basin, and its bottom must be the dark of deep water, not the pale of dry sand.
        /// </summary>
        [Test]
        public void TheBasinWhereTheOldWestWallStoodIsNotPaintedAsDrySand()
        {
            PaintedHeightField ground = Ground();
            Color32[] have = ReadSeabedColour(out int width, out int height);
            Color32 drySand = SeabedPalette.Sample(SeabedPalette.MaxElevation);

            // The old wall's deck, as the stale bake held it, where the view off the wharf showed it.
            var footprint = new Rect(81.5f, 60f, 11f, 20f);
            int wet = 0, pale = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 world = SeabedBake.TexelCenterWorld(x, y, width, height, ground.WorldMin, ground.WorldSize);
                if (!footprint.Contains(world)) continue;
                if (ground.ElevationAt(world) >= SeabedPalette.MaxElevation) continue;   // truly dry: pale is right
                wet++;
                if (Distance(have[y * width + x], drySand) <= ToleranceLevels) pale++;
            }

            Assert.Greater(wet, 0, "the footprint must lie on ground below the dry-sand stop for this to measure " +
                                   "anything; if the wharf moved back here, retire this test with that change.");
            Assert.AreEqual(0, pale,
                $"{pale} of {wet} texels inside x {footprint.xMin}..{footprint.xMax}, y {footprint.yMin}..{footprint.yMax} " +
                "are painted as dry sand over ground that is under water: the pale rectangle off the wharf.\n" + HowToFix);
        }

        /// <summary>
        /// The two files compared above must be the two the scene draws with: the sea's baked bottom, and
        /// the ground both the sea and the simulation read, over one world rectangle.
        /// </summary>
        [Test]
        public void TheSceneDrawsThisColourOverThisGround()
        {
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(GroundAsset);
            Assert.IsNotNull(map, GroundAsset);
            string seabedGuid = AssetDatabase.AssetPathToGUID(SeabedPng);
            string groundGuid = AssetDatabase.AssetPathToGUID(GroundAsset);
            string heightGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(map.HeightTexture));

            string seaBottom = null, seaHeight = null, terrainMap = null, centre = null, size = null;
            bool inTerrain = false;
            foreach (string line in File.ReadLines(ScenePath))
            {
                if (line.StartsWith("--- ", StringComparison.Ordinal)) inTerrain = false;
                else if (line.Contains("HiddenHarbours.World.PaintedTidalTerrain")) inTerrain = true;
                else if (inTerrain && line.StartsWith("  _map:", StringComparison.Ordinal)) terrainMap = GuidOf(line);
                else if (line.StartsWith("  _seabedTexture:", StringComparison.Ordinal)) seaBottom = GuidOf(line);
                else if (line.StartsWith("  _paintedHeightTex:", StringComparison.Ordinal)) seaHeight = GuidOf(line);
                else if (line.StartsWith("  _heightWorldCenter:", StringComparison.Ordinal)) centre = line;
                else if (line.StartsWith("  _heightWorldSize:", StringComparison.Ordinal)) size = line;
            }

            Assert.AreEqual(seabedGuid, seaBottom, "the creek's sea must draw the seabed colour this fixture checks.");
            Assert.AreEqual(groundGuid, terrainMap, "the creek's terrain must read the ground this fixture bakes from.");
            Assert.AreEqual(heightGuid, seaHeight, "the creek's sea must read that same ground's height texture.");
            AssertVector(map.WorldCenter, centre, "the sea's world rectangle centre");
            AssertVector(map.WorldSize, size, "the sea's world rectangle size");
        }

        private static PaintedHeightField Ground()
        {
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(GroundAsset);
            Assert.IsNotNull(map, GroundAsset);
            PaintedHeightField ground = map.Field;
            Assert.IsNotNull(ground, "the creek's ground height texture must be CPU-readable.");
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

        private static string GuidOf(string line)
        {
            Match match = Regex.Match(line, "guid: ([0-9a-f]{32})");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static void AssertVector(Vector2 expected, string line, string what)
        {
            Assert.IsNotNull(line, what + " is not serialized in the creek scene.");
            Match match = Regex.Match(line, @"\{x: ([-0-9.e]+), y: ([-0-9.e]+)\}");
            Assert.IsTrue(match.Success, what + ": " + line);
            Assert.AreEqual(expected.x, float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), 1e-4f, what + " x");
            Assert.AreEqual(expected.y, float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), 1e-4f, what + " y");
        }
    }
}
