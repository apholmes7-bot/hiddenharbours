using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.App.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class TerrainShoreLinesTests
    {
        TerrainShoreLinesDef _def;
        [SetUp] public void SetUp() => _def = ScriptableObject.CreateInstance<TerrainShoreLinesDef>();
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(_def);

        static ShoreLineDerivation.Grid Fixture(Func<Vector2, float> elevation)
        {
            const int size = 64;
            var values = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) values[y * size + x] = elevation(new Vector2((x + .5f) * .5f - 16, (y + .5f) * .5f - 16));
            return new ShoreLineDerivation.Grid(values, size, size, new Vector2(-16, -16), new Vector2(32, 32));
        }

        static string Hash(ShoreLineDerivation.Result result)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            foreach (var slice in result.Addresses)
            foreach (Color c in slice) { writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a); }
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream.ToArray()));
        }

        [Test] public void SamePaintedHeight_ProducesIdenticalAddressBytes()
        {
            var grid = Fixture(p => 6 - Mathf.Max(0, p.magnitude - 5) * 2);
            var a = ShoreLineDerivation.Build(grid, _def);
            var b = ShoreLineDerivation.Build(grid, _def);
            Assert.AreEqual(Hash(a), Hash(b));
            Assert.Greater(a.Lines[0].Length, 0, "A steep shore must yield a turf brow.");
            var changed = Fixture(p => 7 - Mathf.Max(0, p.magnitude - 5) * 2);
            Assert.AreNotEqual(Hash(a), Hash(ShoreLineDerivation.Build(changed, _def)), "Height changes must invalidate the derivation.");
        }

        [Test] public void BrowFollowsSlopeBreak_AboveSpringHigh()
        {
            var grid = Fixture(p => 6 - Mathf.Max(0, p.magnitude - 5) * 2);
            var result = ShoreLineDerivation.Build(grid, _def);
            Assert.Greater(result.Lines[0].Length, 0);
            foreach (var segment in result.Lines[0])
            {
                Assert.Greater(grid.At(segment.A), _def.SpringHigh);
                Assert.That(segment.A.magnitude, Is.InRange(3.8f, 6.2f), "Brow must follow the plateau edge, not the tide contour.");
            }
            Assert.AreEqual(0, result.Lines[1].Length, "A steep cliff is a turf brow, not a second scarp.");
            var gentle = Fixture(p => 6 - Mathf.Max(0, p.magnitude - 5) * .5f);
            Assert.Greater(ShoreLineDerivation.Build(gentle, _def).Lines[1].Length, 0, "A beach bank must yield a scarp.");
        }

        [Test] public void OpenContour_HasMetricDistanceAndNoBandOutsideFourMetres()
        {
            var result = ShoreLineDerivation.Build(Fixture(p => -p.x), _def);
            int visible = 0;
            // A straight spring-high line at x=-2.2; seaward is +x, independently known.
            int y = result.Height / 2;
            for (int x = 0; x < result.Width; x++)
            {
                Color a = result.Sample(2, new Vector2(-16 + x + .5f, -16 + y + .5f));
                float expectedDistance = -16 + x + .5f + 2.2f;
                if (Mathf.Abs(expectedDistance) < 5) Assert.AreEqual(expectedDistance, a.r, 1e-4f);
                bool inBand = ShoreLineDerivation.InBand(2, a);
                Assert.AreEqual(expectedDistance >= -1.84f && expectedDistance <= 2.16f, inBand);
                if (inBand) visible++;
            }
            Assert.AreEqual(4, visible, "A four-metre band spans four one-metre sample centres.");
            for (int kind = 0; kind < 5; kind++)
            {
                float land = -ShoreLineDerivation.Anchors[kind] * 4;
                float sea = land + 4;
                Assert.IsFalse(ShoreLineDerivation.InBand(kind, land - .001f));
                Assert.IsFalse(ShoreLineDerivation.InBand(kind, sea + .001f));
            }
        }

        [Test] public void ClosedContour_RepeatsMeetAtTheSeam()
        {
            var result = ShoreLineDerivation.Build(Fixture(p => 6 - p.magnitude), _def);
            var line = result.Lines[2];
            Assert.Greater(line.Length, 0);
            var first = line[0]; var last = line[line.Length - 1];
            Assert.Less(Vector2.Distance(first.A, last.B), 1e-4f);
            float repeats = (last.Arc + Vector2.Distance(last.A, last.B)) * last.RepeatScale / 8;
            Assert.AreEqual(Mathf.Round(repeats), repeats, 1e-4f);
        }

        [Test] public void BendDistance_IsInMetresInEveryQuadrant()
        {
            var result = ShoreLineDerivation.Build(Fixture(p => 6 - p.magnitude), _def);
            int checkedPoints = 0;
            for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++)
            {
                float radius = new Vector2(-16 + x + .5f, -16 + y + .5f).magnitude;
                if (Mathf.Abs(radius - 3.8f) > 2) continue;
                Color address = result.Sample(2, new Vector2(-16 + x + .5f, -16 + y + .5f));
                Assert.AreEqual(Mathf.Abs(address.r), address.a, 1e-4f);
                Assert.AreEqual(radius - 3.8f, address.r, .08f, "Distance must follow the bend, not a world axis or height delta.");
                checkedPoints++;
            }
            Assert.Greater(checkedPoints, 50);
        }

        [Test] public void InlandBump_DoesNotBecomeAShoreBrowOrStrip()
        {
            var grid = Fixture(p => 2.2f + p.y * (p.y < 0 ? .5f : .02f)
                + 6 * Mathf.Max(0, 1 - (p - new Vector2(0, 8)).sqrMagnitude / 9));
            var result = ShoreLineDerivation.Build(grid, _def);
            Assert.AreEqual(0, result.Lines[0].Length, "An inland hill beyond the first shallow break is not a turf brow.");
            Assert.AreEqual(0, result.Lines[1].Length, "A flat shore must not search onward to an inland hill.");
            for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++)
            {
                var p = new Vector2(-16 + x + .5f, -16 + y + .5f);
                if (Vector2.Distance(p, new Vector2(0, 8)) > 4) continue;
                for (int kind = 0; kind < 5; kind++) Assert.IsFalse(ShoreLineDerivation.InBand(kind,
                    result.Sample(kind, p)), "No strip belongs at the inland bump.");
            }
        }

        [Test] public void FilteringOppositeSigns_CannotInventAnInlandBand()
        {
            // Both samples are five metres from a curve. Filtering their signs must not make a new curve.
            Color filtered = Color.Lerp(new Color(-5, 0, 1, 5), new Color(5, 0, 1, 5), .5f);
            Assert.AreEqual(0, filtered.r);
            for (int kind = 0; kind < 5; kind++) Assert.IsFalse(ShoreLineDerivation.InBand(kind, filtered));
        }

        [Test] public void ArrayPins_AllFifteenSlicesAndTheirBytes()
        {
            var array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(TerrainEdgeArrayImporter.RecipePath);
            Assert.IsNotNull(array);
            Assert.AreEqual(256, array.width); Assert.AreEqual(128, array.height); Assert.AreEqual(15, array.depth);
            Assert.AreEqual(TextureFormat.RGBA32, array.format); Assert.AreEqual(1, array.mipmapCount);
            Assert.IsTrue(array.isDataSRGB); Assert.AreEqual(FilterMode.Point, array.filterMode);
            Assert.AreEqual(TextureWrapMode.Repeat, array.wrapModeU); Assert.AreEqual(TextureWrapMode.Clamp, array.wrapModeV);
            string[] kinds = { "Turf", "Scarp", "Wrack", "Weedline", "Reefedge" };
            string[] steps = { "_Lo", "", "_Hi" };
            for (int kind = 0; kind < kinds.Length; kind++)
            for (int step = 0; step < steps.Length; step++)
            {
                string path = "Assets/_Project/Art/Terrain/Edges/" + kinds[kind] + steps[step] + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(FilterMode.Point, importer.filterMode, path);
                Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapModeU, path);
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapModeV, path);
                Assert.IsFalse(importer.mipmapEnabled, path);
                Assert.IsTrue(importer.alphaIsTransparency, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.IsTrue(ImageConversion.LoadImage(source, File.ReadAllBytes(path)));
                    CollectionAssert.AreEqual(source.GetPixels32(), array.GetPixels32(kind * 3 + step), path);
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }
            }
        }

        [Test] public void ShaderSamplesOnlyInsideBand_SeawardFirst()
        {
            string shader = File.ReadAllText("Assets/_Project/Art/Shaders/Include/TerrainShoreLines.hlsl");
            StringAssert.Contains("t >= 0.0 && t <= 1.0", shader);
            StringAssert.Contains("if (SL_InBand(a, kind))", shader);
            StringAssert.Contains("for (int order = 0; order < 5; order++)", shader);
            StringAssert.Contains("int kind = 4 - order;", shader);
            Assert.AreEqual(1, Regex.Matches(shader, @"SAMPLE_TEXTURE2D_ARRAY_LOD\(_ShoreStrips").Count);
            StringAssert.Contains("_ShoreLoaded < 0.5", shader);
            StringAssert.Contains("_ShoreFields.Load", shader);
            StringAssert.DoesNotContain("SAMPLE_TEXTURE2D_ARRAY_LOD(_ShoreFields", shader);
        }

        [Test] public void NoShoreCpuWorkInFrameCallbacks_AndSplatLayoutUnchanged()
        {
            string source = File.ReadAllText("Assets/_Project/Code/Art/TerrainSplatSurface.cs");
            var update = Regex.Match(source, @"private void Update\(\)\s*\{(?<body>.*?)\n        \}", RegexOptions.Singleline);
            Assert.IsTrue(update.Success);
            StringAssert.DoesNotContain("Shore", update.Groups["body"].Value);
            StringAssert.Contains("PushWaterLevel();", update.Groups["body"].Value);
            string derive = File.ReadAllText("Assets/_Project/Code/Art/ShoreLineDerivation.cs");
            StringAssert.DoesNotContain("MonoBehaviour", derive);
            StringAssert.DoesNotContain("Time.", derive);
            Assert.AreEqual(6, TerrainSplatBrush.TextureCount); Assert.AreEqual(21, TerrainSplatBrush.MaterialCount);
            string surface = File.ReadAllText("Assets/_Project/Code/Art/TerrainSplatSurface.cs");
            Assert.AreEqual(1, Regex.Matches(surface, @"TerrainShoreLineBinding.Create\(").Count);
        }
    }
}
