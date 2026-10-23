using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using NUnit.Framework;
using B = HiddenHarbours.Core.WaveBathymetry;

namespace HiddenHarbours.Tests.EditMode
{
    public class WaveBathymetryTests
    {
        public static B.Settings Settings() => new B.Settings
        {
            CellSizeMm = 4000, OffshoreDepthMm = 10000, MinimumDepthMm = 100, DryDepthMm = 50,
            HeightCapMilli = 2500, TransportFloorMilli = 500, OpenEdges = 15,
            DirectionX = new[] { 23170 }, DirectionY = new[] { 23170 }, WaterLevelsMm = new[] { 0 },
        };

        [Test]
        public void RaysBendTowardShallowerWater()
        {
            const int w = 80, h = 100;
            int[] elevation = Enumerable.Range(0, w * h).Select(i => -10000 + (i % w) * 120).ToArray();
            var grid = new B.Grid(w, h, 0, 0, elevation);
            var slice = B.Bake(grid, Settings(), 0, 0);
            int point = 45 * w + 65;
            Assert.Greater(slice.X[point], 0);
            Assert.Greater(slice.Y[point], 0);
            Assert.Less(slice.Y[point] / (double)slice.X[point], 0.65,
                "A beach shoaling east must turn a diagonal incident crest toward east.");
        }

        [TestCase(32767, 0)]
        [TestCase(0, 32767)]
        [TestCase(-32767, 0)]
        [TestCase(0, -32767)]
        public void AFlatDeepBed_IsTheIdentity(int dx, int dy)
        {
            var settings = Settings(); settings.DirectionX[0] = dx; settings.DirectionY[0] = dy;
            var grid = new B.Grid(7, 5, -12000, -8000, Enumerable.Repeat(-20000, 35).ToArray());
            var slice = B.Bake(grid, settings, 0, 0);
            using var bytes = new BinaryReader(new MemoryStream(slice.Pixels()));
            for (int i = 0; i < 35; i++)
            {
                // Independently known planar bytes: cardinal components, height 1, cell-centre metres.
                Assert.AreEqual(dx / 32767f, bytes.ReadSingle());
                Assert.AreEqual(dy / 32767f, bytes.ReadSingle());
                Assert.AreEqual(1f, bytes.ReadSingle());
                Assert.AreEqual(((-10 + 4 * (i % 7)) * dx + (-6 + 4 * (i / 7)) * dy) / 32767f, bytes.ReadSingle());
            }
        }

        [Test]
        public void AOneDimensionalShoal_HasTheExpectedOpticalDistanceAndHeight()
        {
            var s = Settings(); s.DirectionX[0] = 32767; s.DirectionY[0] = 0;
            var g = new B.Grid(4, 3, 0, 0, Enumerable.Repeat(-2500, 12).ToArray());
            var r = B.Bake(g, s, 0, 0, Enumerable.Repeat(2048, 12).ToArray());
            // Prescribed n = 2: after the incident centre, each 4 m adds 8 optical metres.
            for (int i = 4; i < 8; i++)
            {
                Assert.AreEqual((2 + 8 * (i - 4)) * 1024, r.PhaseQ[i]);
                Assert.AreEqual(32767, r.X[i]); Assert.AreEqual(0, r.Y[i]);
                Assert.AreEqual(5792, r.HeightQ[i]); // floor(sqrt(2) * 4096), flux conserved.
            }
        }

        [Test]
        public void TheBake_IsDeterministic()
        {
            var s = Settings(); int[] heights = Enumerable.Range(0, 30 * 20).Select(i => -500 - (i * 31 % 8000)).ToArray();
            int[] saved = (int[])heights.Clone(); var g = new B.Grid(30, 20, 0, 0, heights);
            CollectionAssert.AreEqual(B.Bake(g, s, 0, 0).Pixels(), B.Bake(g, s, 0, 0).Pixels());
            CollectionAssert.AreEqual(saved, heights, "The pure bake cannot edit its inputs.");
        }

        [Test]
        public void AFlatShallowBed_DoesNotInventAnEntryRefraction()
        {
            var s = Settings(); var g = new B.Grid(8, 8, 0, 0, Enumerable.Repeat(-2000, 64).ToArray());
            var result = B.Bake(g, s, 0, 0);
            Assert.AreEqual(2000, B.EntryDepthMm(g, s, 0));
            CollectionAssert.AreEqual(Enumerable.Repeat(s.DirectionX[0], 64), result.X);
            CollectionAssert.AreEqual(Enumerable.Repeat(s.DirectionY[0], 64), result.Y);
            CollectionAssert.AreEqual(Enumerable.Repeat(B.HeightUnits, 64), result.HeightQ);
        }

        [Test]
        public void ExcessOpticalDistance_IsRefusedBeforeFloatPrecisionIsLost()
        {
            var slice = new B.Slice(1);
            slice.PhaseQ[0] = B.MaxExactPhaseUnits + 1;
            Assert.Throws<InvalidDataException>(() => slice.Pixels());
        }

        [Test]
        public void DryDisconnectedAndElevatedStillWater_CarryNoIncomingSea()
        {
            var s = Settings(); var heights = Enumerable.Repeat(-2000, 100).ToArray();
            var still = Enumerable.Repeat(int.MinValue, 100).ToArray();
            for (int i = 0; i < 100; i++)
                if (i % 10 < 2 || i % 10 > 7 || i / 10 < 2 || i / 10 > 7) heights[i] = 5000;
            var g = new B.Grid(10, 10, 0, 0, heights, still);
            Assert.IsTrue(B.Bake(g, s, 0, 0).HeightQ.All(q => q == 0), "An enclosed pool has no sea entry.");
            Array.Fill(heights, -2000); Array.Fill(still, 1000);
            Assert.IsTrue(B.Bake(g, s, 0, 0).HeightQ.All(q => q == 0), "Fresh water above the tide is excluded.");
        }

        [Test]
        public void UnsupportedPng_IsRefusedByPath()
        {
            string path = "Assets/Tests/UnsupportedHeight.png";
            StringAssert.Contains(path, Assert.Throws<InvalidDataException>(() => WaveBathymetrySource.Decode(new byte[33], path)).Message);
        }

        [Test]
        public void EightAndSixteenBitCodes_KeepPrecisionAndBottomUpSampling()
        {
            var eight = new WaveBathymetrySource.Png { Width = 2, Height = 2, Bits = 8, Codes = new ushort[] { 0, 255, 128, 64 } };
            var sixteen = new WaveBathymetrySource.Png { Width = 2, Height = 2, Bits = 16, Codes = new ushort[] { 0, 65535, 32768, 1 } };
            CollectionAssert.AreEqual(new[] { -6000, 6000, 24, -2988 }, WaveBathymetrySource.Sample(eight, 2, 2, -6000, 6000, false));
            CollectionAssert.AreEqual(new[] { -4000, 12000, 4000, -4000 }, WaveBathymetrySource.Sample(sixteen, 2, 2, -4000, 12000, false));
            Assert.AreEqual(int.MinValue, WaveBathymetrySource.Sample(sixteen, 2, 2, -4000, 12000, true)[0]);
            Assert.AreNotEqual(int.MinValue, WaveBathymetrySource.Sample(sixteen, 2, 2, -4000, 12000, true)[3]);
        }

        [TestCase("StPeters")]
        [TestCase("NineMileCreek")]
        [TestCase("RicePoint")]
        [TestCase("Cumberland")]
        public void ThePhase_IsContinuous(string region)
        {
            var recipe = ReadRecipe(region); var settings = recipe.Settings();
            var input = WaveBathymetrySource.Read(recipe, PathFor(region), ResolveGuid, _ => { });
            var g = input.Grid;
            long bound = (long)settings.CellSizeMm * B.PathUnitsPerMeter
                * B.Root((long)settings.OffshoreDepthMm * 1000000 / settings.MinimumDepthMm) / 1000000 + 2;
            foreach (int level in settings.WaterLevelsMm)
                for (int bin = 0; bin < settings.DirectionX.Length; bin++)
                {
                    var r = B.Bake(g, settings, level, bin);
                    for (int i = 0; i < r.X.Length; i++)
                    {
                        if (i % g.Width + 1 < g.Width) Check(i, i + 1);
                        if (i + g.Width < r.X.Length) Check(i, i + g.Width);
                    }
                    void Check(int a, int b)
                    {
                        if (r.Arrival[a] >= long.MaxValue / 8 || r.Arrival[b] >= long.MaxValue / 8) return;
                        Assert.LessOrEqual(Math.Abs(r.Arrival[a] - r.Arrival[b]), bound,
                            region + ": phase must obey the local optical-distance Lipschitz bound.");
                    }
                }
        }

        [TestCase(8)]
        [TestCase(16)]
        public void RawPngFilters_DecodeIndependentCodes_AndRefuseBadChecksums(int bits)
        {
            byte[] bytes = bits == 8 ? new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 3, 0, 0, 0, 5, 8, 0, 0, 0, 0, 165, 26, 9, 126, 0, 0, 0, 28, 73, 68, 65, 84, 120, 156, 99, 96, 96, 252, 207, 216, 240, 255, 32, 211, 148, 249, 47, 152, 53, 68, 68, 88, 228, 184, 184, 0, 71, 202, 5, 232, 16, 205, 37, 205, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 } : new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 3, 0, 0, 0, 5, 16, 0, 0, 0, 0, 245, 138, 213, 61, 0, 0, 0, 44, 73, 68, 65, 84, 120, 156, 99, 96, 96, 96, 96, 252, 255, 159, 177, 129, 161, 145, 129, 129, 145, 169, 249, 5, 219, 5, 174, 237, 204, 124, 58, 28, 23, 56, 46, 176, 40, 206, 212, 78, 215, 169, 7, 0, 163, 76, 9, 239, 24, 185, 96, 199, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
            ushort[] expected = bits == 8 ? new ushort[] { 80, 90, 100, 50, 60, 70, 20, 30, 40, 128, 127, 64, 0, 1, 255 } : new ushort[] { 12345, 23456, 34567, 4000, 5000, 6000, 1000, 2000, 3000, 32768, 256, 257, 0, 1, 65535 };
            const string path = "Assets/Tests/HeightCodes.png";
            var decoded = WaveBathymetrySource.Decode(bytes, path);
            Assert.AreEqual(3, decoded.Width); Assert.AreEqual(5, decoded.Height);
            Assert.AreEqual(bits, decoded.Bits);
            CollectionAssert.AreEqual(expected, decoded.Codes);
            bytes[16] ^= 1;
            StringAssert.Contains(path, Assert.Throws<InvalidDataException>(() => WaveBathymetrySource.Decode(bytes, path)).Message);
        }

        public static string PathFor(string region) => "Assets/_Project/Art/Terrain/Bathymetry/" + region + ".hhbathymetry";
        // Independent, integer-only recipe intake for the tests and standalone harness.
        public static WaveBathymetrySource.Recipe ReadRecipe(string region)
        {
            string text = File.ReadAllText(PathFor(region));
            int Number(string field) => int.Parse(Regex.Match(text, "\"" + field + "\"\\s*:\\s*(-?[0-9]+)").Groups[1].Value);
            string Text(string field) => Regex.Match(text, "\"" + field + "\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
            int[] ArrayOf(string field) => Regex.Matches(Regex.Match(text, "(?s)\"" + field + "\"\\s*:\\s*\\[([^]]+)\\]").Groups[1].Value,
                "-?[0-9]+").Cast<Match>().Select(m => int.Parse(m.Value)).ToArray();
            return new WaveBathymetrySource.Recipe
            {
                schemaVersion = Number("schemaVersion"), id = Text("id"), seabedPath = Text("seabedPath"),
                cellSizeMm = Number("cellSizeMm"), offshoreDepthMm = Number("offshoreDepthMm"),
                minimumDepthMm = Number("minimumDepthMm"), dryDepthMm = Number("dryDepthMm"),
                heightCapMilli = Number("heightCapMilli"), transportFloorMilli = Number("transportFloorMilli"), openEdges = Number("openEdges"),
                directionX = ArrayOf("directionX"), directionY = ArrayOf("directionY"), waterLevelsMm = ArrayOf("waterLevelsMm"),
            };
        }
        public static string ResolveGuid(string guid)
        {
            return Directory.EnumerateFiles("Assets/_Project/Data/Terrain", "*.meta", SearchOption.AllDirectories)
                .Where(path => Regex.IsMatch(File.ReadAllText(path), @"(?m)^guid: " + guid + @"\r?$"))
                .Select(path => path.Substring(0, path.Length - 5).Replace('\\', '/')).Single();
        }
    }
}
